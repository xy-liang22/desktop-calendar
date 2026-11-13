using System.Collections.Immutable;

namespace DesktopCalendar.Core;

internal static partial class PlannerCommands
{
    static CommandChange BulkEdit(PlannerState s, BulkEditTodos c, DateTimeOffset now)
    {
        if (c.Todos.IsDefaultOrEmpty || c.Todos.Length > 1000 || c.Todos.Select(t => t.Id).Distinct().Count() != c.Todos.Length || c.Date == null && c.Kind == null) throw new PlannerException("InvalidBulkEdit");
        var selected = c.Todos.Select(r => Get(s, r)).ToArray();
        foreach (var reference in c.Todos) s = Materialize(s, reference);
        if (selected.Any(t => t.Status == TodoStatus.Cancelled)) throw new PlannerException("CancelledTodo");
        var ids = selected.Select(t => t.Id).ToHashSet(); var converted = new HashSet<Guid>();
        // A series conversion is explicitly subtree-scoped. Reject, never implicitly select descendants.
        foreach (var t in selected.Where(t => c.Kind != null && (t.Origin != null || c.Kind == TodoKind.SeriesTemplate)))
        {
            if (t.Status == TodoStatus.Completed && (t.ParentId == null || !ids.Contains(t.ParentId.Value))) throw new PlannerException("BulkCompletedHistory");
            if (t.ParentId != null && !ids.Contains(t.ParentId.Value)) throw new PlannerException("BulkSelectSeriesRoot");
            if (TodoCompletion.Descendants(s, t).Any(child => !ids.Contains(child.Id))) throw new PlannerException("BulkSelectSubtree");
        }
        foreach (var original in selected.OrderBy(t => Depth(s, t)))
        {
            if (converted.Contains(original.Id)) continue;
            var t = Get(s, TodoRef.Of(original));
            if (c.Kind is TodoKind kind && (t.Origin != null || kind == TodoKind.SeriesTemplate))
            {
                foreach (var child in TodoCompletion.Descendants(s, t)) converted.Add(child.Id);
                var rule = kind == TodoKind.SeriesTemplate ? c.Rule ?? throw new PlannerException("InvalidTodoConversion") : null;
                s = Apply(s, new ConvertTodo(TodoRef.Of(t), kind, c.Date ?? c.EffectiveDate, rule), now).State;
                continue;
            }
            if (c.Kind is TodoKind ordinary && t.Kind != ordinary) { s = Apply(s, new ConvertTodo(TodoRef.Of(t), ordinary, c.Date ?? c.EffectiveDate), now).State; t = Get(s, TodoRef.Of(t)); }
            if (c.Date is DateOnly date)
            {
                if (t.Origin != null && t.Status == TodoStatus.Completed) throw new PlannerException("BulkCompletedHistory");
                int delta = date.DayNumber - t.PlannedDate.DayNumber;
                DateOnly? Shift(DateOnly? day) => day?.AddDays(delta);
                var value = t with { PlannedDate = date, CarryDate = Shift(t.CarryDate), FrozenCarryDate = Shift(t.FrozenCarryDate), Moves = [.. t.Moves.Select(m => m with { From = m.From.AddDays(delta), To = m.To.AddDays(delta) })], ModifiedAt = now };
                // Explicit date command can translate carry history; ordinary SaveTodo cannot.
                var before = s; s = s with { Todos = Put(s.Todos, value) }; s = TodoCompletion.Reconcile(before, s, now);
            }
            PlannerIntegrity.Validate(s);
        }
        return new(s);
    }
    static int Depth(PlannerState s, Todo t)
    {
        int depth = 0;
        while (t.ParentId is Guid id) { t = s.Todos.SingleOrDefault(n => n.Id == id) ?? PlannerQueries.TodoTreeOn(s, t.PlannedDate, t.PlannedDate).SelectMany(Flatten).Single(n => n.Id == id); depth++; }
        return depth;
    }
    static IEnumerable<Todo> Flatten(TodoDateNode node) => new[] { node.Value }.Concat(node.Children.SelectMany(Flatten));
    static CommandChange Reparent(PlannerState s, ReparentTodo c, DateTimeOffset now)
    {
        if (c.Scope is not (EditScope.Single or EditScope.FromDate)) throw new PlannerException("InvalidEditScope");
        s = Materialize(s, c.Todo); var root = Get(s, c.Todo);
        if (c.Parent != null) s = Materialize(s, c.Parent);
        var parent = c.Parent == null ? null : Get(s, c.Parent);
        var tree = TodoCompletion.Descendants(s, root).Prepend(root).ToArray();
        if (root.Status == TodoStatus.Cancelled || parent?.Status == TodoStatus.Cancelled) throw new PlannerException("CancelledTodo");
        if (parent != null && tree.Any(t => t.Id == parent.Id)) throw new PlannerException("ParentCycle");
        if (root.ParentId == parent?.Id && c.Scope == EditScope.Single) return new(s, root.Id);
        bool sameSeries = root.Origin is OccurrenceKey source && parent?.Origin is OccurrenceKey target && source.SeriesId == target.SeriesId && source.Date == target.Date;
        if (root.Origin is OccurrenceKey key)
        {
            if (root.Status == TodoStatus.Completed) throw new PlannerException("BulkCompletedHistory");
            if (!sameSeries)
            {
                if (c.DetachKind is not (TodoKind.Once or TodoKind.CarryHistory or TodoKind.CarryLatest)) throw new PlannerException("HierarchyConversionRequired");
                if (c.Scope != EditScope.Single) throw new PlannerException("HierarchyDetachSingle");
                // Detach only this occurrence subtree, keeping IDs, links and completed descendants.
                // Tombstones prevent lazy recurrence generation from duplicating the detached nodes.
                foreach (var t in tree) s = Materialize(s, TodoRef.Of(t));
                var keys = tree.Where(t => t.Origin != null).Select(t => t.Origin!).ToHashSet();
                s = s with { Series = [.. s.Series.Select(series => series with { Cancelled = [.. series.Cancelled.Concat(keys.Where(k => k.SeriesId == series.Id)).Distinct()] })] };
                var changed = new Dictionary<Guid, TodoRef>();
                foreach (var t in tree.Where(t => t.Origin != null))
                {
                    bool carry = c.DetachKind != TodoKind.Once;
                    var value = t with { Kind = c.DetachKind!.Value, Origin = null, PlannedTimeZoneId = TodoTime.SourceZone(s, t), ModifiedAt = now, CarryDate = carry ? t.PlannedDate : null,
                        FrozenCarryDate = carry && t.Status == TodoStatus.Completed ? new[] { t.PlannedDate, PlannerTime.LocalDate(t.CompletedAt!.Value, s.Options.TimeZoneId) }.Max() : null };
                    s = s with { Todos = Put(s.Todos, value) }; changed[t.Id] = TodoRef.Of(value);
                }
                s = s with { Activities = [.. s.Activities.Select(a => a.Todo != null && changed.TryGetValue(a.Todo.Id, out var r) ? a with { Todo = r, ModifiedAt = now } : a)],
                    Goals = [.. s.Goals.Select(g => g with { Todos = [.. g.Todos.Select(r => changed.GetValueOrDefault(r.Id, r))] })] };
                root = s.Todos.Single(t => t.Id == root.Id);
            }
            else if (c.Scope == EditScope.FromDate)
            {
                var series = s.Series.Single(x => x.Id == key.SeriesId); var version = PlannerQueries.Version(series, key.Date)!;
                if (key.NodeId == series.RootTodoId || !version.Templates.Any(n => n.Id == key.NodeId) || !version.Templates.Any(n => n.Id == parent!.Origin!.NodeId)) throw new PlannerException("LocalExceptionRequiresSingleScope");
                var next = version.Templates.Select(n => n.Id == key.NodeId ? n with { ParentId = parent!.Origin!.NodeId } : n).ToImmutableArray();
                // Validate template cycles before editing or recursively walking it.
                for (Guid? p = parent!.Origin!.NodeId; p is Guid id; p = next.Single(n => n.Id == id).ParentId) if (id == key.NodeId) throw new PlannerException("ParentCycle");
                var change = Edit(s, new(key.SeriesId, key.Date, EditScope.FromDate, Templates: next), now); s = change.State;
                foreach (var old in s.Todos.Where(t => t.Origin?.SeriesId == key.SeriesId && t.Origin.NodeId == key.NodeId && t.Origin.Date >= key.Date && t.Status == TodoStatus.Pending).ToArray())
                {
                    // Completed history (including its ancestors) retains its original structure.
                    if (TodoCompletion.Descendants(s, old).Any(t => t.Status == TodoStatus.Completed)) continue;
                    var targetRef = TodoRef.For(new(key.SeriesId, parent.Origin.NodeId, old.Origin!.Date));
                    try { s = Materialize(s, targetRef); } catch (PlannerException e) when (e.Code == "OccurrenceNotFound") { continue; }
                    if (Get(s, targetRef).Status == TodoStatus.Completed) continue;
                    s = s with { Todos = Put(s.Todos, old with { ParentId = targetRef.Id, ModifiedAt = now }) };
                }
                return new(s, root.Id);
            }
        }
        else if (c.Scope != EditScope.Single) throw new PlannerException("InvalidEditScope");
        foreach (var t in tree) if (!s.Todos.Any(n => n.Id == t.Id)) s = Materialize(s, TodoRef.Of(t));
        decimal nextKey = parent == null ? s.Todos.Where(t => t.ParentId == null).Select(t => t.SortKey).DefaultIfEmpty(0).Max() + 1024 : PlannerQueries.Children(s, parent).Select(t => t.SortKey).DefaultIfEmpty(0).Max() + 1024;
        root = root with { ParentId = parent?.Id, SortKey = nextKey, ModifiedAt = now };
        return new(s with { Todos = Put(s.Todos, root) }, root.Id);
    }
}
