using System.Collections.Immutable;

namespace DesktopCalendar.Core;

// Commands are the mutation boundary. No UI collection or control is a persistence model.
public abstract record PlannerCommand;
public sealed record BatchCommand(ImmutableArray<PlannerCommand> Commands) : PlannerCommand;
public sealed record SaveTodo(Todo Value) : PlannerCommand;
public sealed record RenameTodo(TodoRef Todo, string Title, string ExpectedTitle) : PlannerCommand;
public sealed record SetTodoCompleted(TodoRef Todo, bool Completed) : PlannerCommand;
public sealed record MoveCarryTodo(TodoRef Todo, DateOnly Date) : PlannerCommand;
public sealed record SetCarryMode(TodoRef Todo, bool KeepHistory) : PlannerCommand;
public sealed record ConvertTodo(TodoRef Todo, TodoKind Kind, DateOnly EffectiveDate, RecurrenceRule? Rule = null) : PlannerCommand;
public sealed record AddOccurrenceTodo(Todo Value, TodoRef Parent) : PlannerCommand;
public sealed record AddSeriesTodo(Todo Value, TodoRef Parent, EditScope Scope) : PlannerCommand;
public sealed record EditOccurrence(Todo Value, EditScope Scope) : PlannerCommand;
public sealed record ReorderTodos(ImmutableArray<TodoRef> Siblings, DateOnly? DisplayDate = null, DateOnly? AsOf = null) : PlannerCommand;
public sealed record SetTodoOrderMode(Guid? ParentId, DateOnly? Date, TodoSort Mode) : PlannerCommand;
public sealed record DeleteTodo(TodoRef Todo, ChildDisposition Children) : PlannerCommand;
public sealed record CopyTodo(TodoRef Todo, DateOnly TargetDate) : PlannerCommand;
public sealed record CreateSeries(RecurrenceRule Rule, ImmutableArray<Todo> Templates, Guid? SeriesId = null) : PlannerCommand;
public sealed record EditSeries(Guid SeriesId, DateOnly From, EditScope Scope, RecurrenceRule? Rule = null,
    ImmutableArray<Todo> Templates = default, DateOnly? MoveTo = null) : PlannerCommand;
public sealed record CancelSeries(Guid SeriesId, DateOnly From, EditScope Scope, Guid? NodeId = null) : PlannerCommand;
public sealed record SaveActivity(Activity Value, bool InheritCategory = true) : PlannerCommand;
public sealed record DeleteActivity(Guid Id) : PlannerCommand;
public sealed record SaveGoal(WeekGoal Value, bool ConfirmTypeChange = false) : PlannerCommand;
public sealed record DeleteGoal(Guid Id) : PlannerCommand;
public sealed record SaveReview(WeeklyReview Value) : PlannerCommand;
public sealed record DeleteReview(Guid Id) : PlannerCommand;
public sealed record AppendStatistics(Guid ReviewId, string Markdown) : PlannerCommand;
public sealed record SaveCategory(Category Value) : PlannerCommand;
public sealed record ReorderCategories(ImmutableArray<Guid> Ids) : PlannerCommand;
public sealed record ArchiveCategory(Guid Id) : PlannerCommand;
public sealed record InitializeCategories : PlannerCommand;
public sealed record SaveAsset(LocalAsset Value) : PlannerCommand;
public sealed record RemoveAsset(Guid Id) : PlannerCommand;
public sealed record SaveDecoration(DayDecoration Value) : PlannerCommand;
public sealed record DeleteDecoration(Guid Id) : PlannerCommand;
internal sealed record CommandChange(PlannerState State, Guid? EntityId = null);

internal static partial class PlannerCommands
{
    static T Stamp<T>(T value, T? previous, DateTimeOffset now) where T : Entity => value with
    { Id = previous?.Id ?? (value.Id == Guid.Empty ? Guid.NewGuid() : value.Id), CreatedAt = previous?.CreatedAt ?? now, ModifiedAt = now };
    static ImmutableArray<T> Put<T>(ImmutableArray<T> rows, T value) where T : Entity => [.. rows.Where(r => r.Id != value.Id), value];
    static Todo Get(PlannerState s, TodoRef r) => PlannerQueries.Resolve(s, r);
    static PlannerState Materialize(PlannerState s, TodoRef reference)
    {
        if (s.Todos.Any(t => t.Id == reference.Id)) { _ = Get(s, reference); return s; }
        var todo = Get(s, reference);
        if (todo.Origin is OccurrenceKey key && todo.ParentId is Guid parent)
        {
            var template = PlannerQueries.Version(s.Series.Single(x => x.Id == key.SeriesId), key.Date)!.Templates.Single(t => new OccurrenceKey(key.SeriesId, t.Id, key.Date).StableId() == parent);
            s = Materialize(s, TodoRef.For(new(key.SeriesId, template.Id, key.Date)));
        }
        return s with { Todos = s.Todos.Add(todo) };
    }
    static PlannerState Detach(PlannerState s, HashSet<Guid> ids, DateTimeOffset now) => s with
    {
        Activities = [.. s.Activities.Select(a => a.Todo != null && ids.Contains(a.Todo.Id) ? a with { FormerTodoTitle = Get(s, a.Todo).Title, Todo = null, ModifiedAt = now } : a)],
        Goals = [.. s.Goals.Select(g => g.Todos.Any(r => ids.Contains(r.Id)) ? g with { Todos = [.. g.Todos.Where(r => !ids.Contains(r.Id))], ModifiedAt = now } : g)]
    };
    static HashSet<Guid> Subtree(IEnumerable<Todo> rows, Guid root)
    {
        var result = new HashSet<Guid> { root }; bool changed;
        do { changed = false; foreach (var t in rows) if (t.ParentId is Guid p && result.Contains(p)) changed |= result.Add(t.Id); } while (changed);
        return result;
    }
    internal static CommandChange Apply(PlannerState s, PlannerCommand command, DateTimeOffset now, int depth = 0)
    {
        var change = ApplyCore(s, command, now, depth);
        if (command is BatchCommand) return change;
        var protect = command is SetTodoCompleted completion && change.State.Todos.SingleOrDefault(t => t.Id == completion.Todo.Id) is Todo root
            ? TodoCompletion.Descendants(change.State, root).Select(t => t.Id).Append(root.Id).ToHashSet() : null;
        return change with { State = TodoCompletion.Reconcile(s, change.State, now, protect) };
    }
    static CommandChange ApplyCore(PlannerState s, PlannerCommand command, DateTimeOffset now, int depth)
    {
        if (depth > 16) throw new PlannerException("CommandTooDeep");
        switch (command)
        {
            case BatchCommand c:
            {
                if (c.Commands.IsDefaultOrEmpty || c.Commands.Length > 1000) throw new PlannerException("InvalidCommandBatch");
                foreach (var part in c.Commands) { s = Apply(s, part, now, depth + 1).State; PlannerIntegrity.Validate(s); }
                return new(s);
            }
            case SaveTodo c:
            {
                var old = s.Todos.SingleOrDefault(t => t.Id == c.Value.Id);
                if (c.Value.Origin != null && old == null) { s = Materialize(s, TodoRef.Of(c.Value)); old = Get(s, TodoRef.Of(c.Value)); }
                var t = Stamp(c.Value, old, now);
                if (old == null)
                {
                    if (t.Kind is not (TodoKind.Once or TodoKind.CarryHistory or TodoKind.CarryLatest) || t.Status != TodoStatus.Pending || t.CompletedAt != null || t.CompletionIntent != CompletionIntent.Automatic || t.CompletionSnapshot != null) throw new PlannerException("UseExplicitTodoCommand");
                    if (t.Kind is TodoKind.CarryHistory or TodoKind.CarryLatest) t = t with { CarryDate = t.PlannedDate, FrozenCarryDate = null, Moves = [] };
                }
                else if (t.Status != old.Status || t.CompletedAt != old.CompletedAt || t.Kind != old.Kind || t.Origin != old.Origin || t.CarryDate != old.CarryDate || t.FrozenCarryDate != old.FrozenCarryDate || !t.Moves.SequenceEqual(old.Moves) || t.CompletionIntent != old.CompletionIntent || t.CompletionSnapshot != old.CompletionSnapshot || t.CompletionSnapshotInvalidated != old.CompletionSnapshotInvalidated) throw new PlannerException("UseExplicitTodoCommand");
                if (old?.Origin != null && old.ParentId != t.ParentId) throw new PlannerException("OccurrenceReparentNotSupported");
                return new(s with { Todos = Put(s.Todos, t) }, t.Id);
            }
            case RenameTodo c:
            {
                s = Materialize(s, c.Todo); var t = Get(s, c.Todo);
                if (t.Title != c.ExpectedTitle) throw new PlannerException("StaleTodoTitle");
                return new(s with { Todos = Put(s.Todos, t with { Title = c.Title, ModifiedAt = now }) }, t.Id);
            }
            case SetTodoCompleted c:
            {
                s = Materialize(s, c.Todo); var t = Get(s, c.Todo);
                foreach (var child in TodoCompletion.Descendants(s, t)) s = Materialize(s, TodoRef.Of(child));
                return new(TodoCompletion.Set(s, t, c.Completed, now), t.Id);
            }
            case MoveCarryTodo c:
            {
                var t = Get(s, c.Todo);
                if (t.Kind is not (TodoKind.CarryHistory or TodoKind.CarryLatest) || t.Status != TodoStatus.Pending || c.Date <= t.CarryDate) throw new PlannerException("InvalidCarryMove");
                t = t with { CarryDate = c.Date, Moves = t.Moves.Add(new(t.CarryDate!.Value, c.Date, now)), ModifiedAt = now };
                return new(s with { Todos = Put(s.Todos, t) }, t.Id);
            }
            case SetCarryMode c:
            {
                var t = Get(s, c.Todo); if (t.Kind is not (TodoKind.CarryHistory or TodoKind.CarryLatest)) throw new PlannerException("NotCarryTodo");
                t = t with { Kind = c.KeepHistory ? TodoKind.CarryHistory : TodoKind.CarryLatest, ModifiedAt = now };
                return new(s with { Todos = Put(s.Todos, t) }, t.Id);
            }
            case AddOccurrenceTodo c: return AddOccurrence(s, c, now);
            case ConvertTodo c: return Convert(s, c, now);
            case AddSeriesTodo c: return AddSeriesChild(s, c, now);
            case EditOccurrence c: return EditOccurrenceFields(s, c, now);
            case ReorderTodos c:
            {
                if (c.Siblings.IsDefaultOrEmpty || c.Siblings.Select(r => r.Id).Distinct().Count() != c.Siblings.Length) throw new PlannerException("InvalidSiblingOrder");
                foreach (var r in c.Siblings) s = Materialize(s, r);
                var rows = c.Siblings.Select(r => Get(s, r)).ToArray();
                if (rows.Select(t => t.ParentId).Distinct().Count() != 1) throw new PlannerException("DifferentSiblingGroups");
                var siblings = rows[0].ParentId is Guid parent
                    ? PlannerQueries.Children(s, s.Todos.Single(t => t.Id == parent)).Where(t => t.Status != TodoStatus.Cancelled).Select(t => t.Id).ToHashSet()
                    : PlannerQueries.TodosOn(s, c.DisplayDate ?? rows[0].PlannedDate, c.AsOf ?? c.DisplayDate ?? rows[0].PlannedDate).Where(t => t.ParentId == null).Select(t => t.Id).ToHashSet();
                if (!siblings.SetEquals(rows.Select(t => t.Id))) throw new PlannerException("IncompleteSiblingOrder");
                for (int i = 0; i < rows.Length; i++) s = s with { Todos = Put(s.Todos, rows[i] with { SortKey = i * 1024m, ModifiedAt = now }) };
                return Apply(s, new SetTodoOrderMode(rows[0].ParentId, rows[0].ParentId == null ? c.DisplayDate ?? rows[0].PlannedDate : null, TodoSort.Manual), now, depth + 1);
            }
            case SetTodoOrderMode c:
            {
                var old = s.TodoOrderings.SingleOrDefault(o => o.ParentId == c.ParentId && o.Date == c.Date);
                var ordering = Stamp(new TodoOrdering { ParentId = c.ParentId, Date = c.Date, Mode = c.Mode }, old, now);
                return new(s with { TodoOrderings = Put(s.TodoOrderings, ordering) }, ordering.Id);
            }
            case DeleteTodo c:
            {
                var t = Get(s, c.Todo);
                if (t.Origin != null) throw new PlannerException("UseSeriesScopeCommand");
                if (!Enum.IsDefined(c.Children)) throw new PlannerException("InvalidChildDisposition");
                var ids = c.Children == ChildDisposition.DeleteSubtree ? Subtree(s.Todos, t.Id) : [t.Id];
                s = Detach(s, ids, now);
                return new(s with { Todos = [.. s.Todos.Where(x => !ids.Contains(x.Id)).Select(x => x.ParentId == t.Id ? x with { ParentId = t.ParentId, ModifiedAt = now } : x)], TodoOrderings = [.. s.TodoOrderings.Where(o => o.ParentId == null || !ids.Contains(o.ParentId.Value))] });
            }
            case CopyTodo c:
            {
                var root = Get(s, c.Todo); if (root.Status == TodoStatus.Cancelled || root.Kind == TodoKind.SeriesTemplate) throw new PlannerException("CancelledTodo");
                var all = TodoCompletion.Descendants(s, root).Prepend(root).ToDictionary(t => t.Id);
                var source = all.Values.Where(t => t.Status != TodoStatus.Cancelled).ToArray(); var tree = source.Select(t => t.Id).ToHashSet();
                var ids = source.ToDictionary(t => t.Id, _ => Guid.NewGuid());
                Guid? Parent(Todo t) { if (t.Id == root.Id) return null; var p = t.ParentId; while (p is Guid id && !ids.ContainsKey(id)) p = all[id].ParentId; return p is Guid included ? ids[included] : null; }
                decimal next = s.Todos.Where(t => t.ParentId == null && t.PlannedDate == c.TargetDate).Select(t => t.SortKey).DefaultIfEmpty(-1024).Max() + 1024;
                var copies = source.Select(t => t with { Id = ids[t.Id], ParentId = Parent(t), Kind = TodoKind.Once, Origin = null, SeriesId = null, CarryDate = null, FrozenCarryDate = null, Moves = [],
                    PlannedDate = t.PlannedDate.AddDays(c.TargetDate.DayNumber - root.PlannedDate.DayNumber), SortKey = t.Id == root.Id ? next : t.SortKey,
                    Status = TodoStatus.Pending, CompletedAt = null, CompletionIntent = CompletionIntent.Automatic, CompletionSnapshot = null, CompletionSnapshotInvalidated = false, ManualProgress = 0, CopiedFromId = t.Id, CreatedAt = now, ModifiedAt = now }).ToArray();
                var ordering = s.TodoOrderings.Where(o => o.ParentId is Guid parent && tree.Contains(parent)).Select(o => o with { Id = Guid.NewGuid(), ParentId = ids[o.ParentId!.Value], CreatedAt = now, ModifiedAt = now });
                return new(s with { Todos = s.Todos.AddRange(copies), TodoOrderings = s.TodoOrderings.AddRange(ordering) }, ids[root.Id]);
            }
            case CreateSeries c:
            {
                var id = c.SeriesId ?? Guid.NewGuid(); if (id == Guid.Empty) throw new PlannerException("EmptySeriesId");
                var nodes = PrepareTemplates(c.Templates, id, now, []);
                var root = nodes.Single(t => t.ParentId == null);
                var series = new TodoSeries { Id = id, RootTodoId = root.Id, CreatedAt = now, ModifiedAt = now, Versions = [new(Guid.NewGuid(), c.Rule.Start, null, c.Rule, nodes)] };
                return new(s with { Series = s.Series.Add(series) }, id);
            }
            case EditSeries c: return Edit(s, c, now);
            case CancelSeries c: return Cancel(s, c, now);
            case SaveActivity c:
            {
                if (c.Value.Todo != null) s = Materialize(s, c.Value.Todo);
                var previous = s.Activities.SingleOrDefault(x => x.Id == c.Value.Id);
                var a = Stamp(c.Value, previous, now);
                if (c.InheritCategory && a.CategoryId == null && a.Todo != null && (previous == null || previous.Todo != a.Todo))
                    a = a with { CategoryId = Get(s, a.Todo).CategoryId };
                a = a with { Intervals = [.. a.Intervals.Select(i => i with { Id = i.Id == Guid.Empty ? Guid.NewGuid() : i.Id })] };
                return new(s with { Activities = Put(s.Activities, a) }, a.Id);
            }
            case DeleteActivity c:
                if (!s.Activities.Any(a => a.Id == c.Id)) throw new PlannerException("ActivityNotFound");
                return new(s with { Activities = [.. s.Activities.Where(a => a.Id != c.Id)], Goals = [.. s.Goals.Select(g => g with { Activities = g.Activities.Remove(c.Id) })] });
            case SaveGoal c:
            {
                var old = s.Goals.SingleOrDefault(g => g.Id == c.Value.Id);
                if (old != null && (old.Kind != c.Value.Kind || old.Source != c.Value.Source) && !c.ConfirmTypeChange) throw new PlannerException("GoalTypeChangeNeedsConfirmation");
                foreach (var r in c.Value.Todos) s = Materialize(s, r);
                var g = Stamp(c.Value, old, now);
                return new(s with { Goals = Put(s.Goals, g) }, g.Id);
            }
            case DeleteGoal c:
                if (!s.Goals.Any(g => g.Id == c.Id)) throw new PlannerException("GoalNotFound");
                return new(s with { Goals = [.. s.Goals.Where(g => g.Id != c.Id)] });
            case SaveReview c:
            {
                var old = s.Reviews.SingleOrDefault(r => c.Value.Id != Guid.Empty ? r.Id == c.Value.Id : r.Week == c.Value.Week);
                var r = Stamp(c.Value, old, now);
                // Statistics are changed only through AppendStatistics; saving prose cannot erase them.
                r = r with { Statistics = old?.Statistics ?? [], Sections = [.. r.Sections.Select(x => x with { Id = x.Id == Guid.Empty ? Guid.NewGuid() : x.Id })] };
                return new(s with { Reviews = Put(s.Reviews, r) }, r.Id);
            }
            case AppendStatistics c:
            {
                var r = s.Reviews.SingleOrDefault(x => x.Id == c.ReviewId) ?? throw new PlannerException("ReviewNotFound");
                r = r with { Statistics = r.Statistics.Add(new(Guid.NewGuid(), now, c.Markdown)), ModifiedAt = now };
                return new(s with { Reviews = Put(s.Reviews, r) }, r.Id);
            }
            case DeleteReview c:
                if (!s.Reviews.Any(r => r.Id == c.Id)) throw new PlannerException("ReviewNotFound");
                return new(s with { Reviews = [.. s.Reviews.Where(r => r.Id != c.Id)], Goals = [.. s.Goals.Select(g => g.ReviewId == c.Id ? g with { ReviewId = null } : g)] });
            case SaveCategory c:
            {
                var v = Stamp(c.Value, s.Categories.SingleOrDefault(x => x.Id == c.Value.Id), now);
                return new(s with { Categories = s.Categories.Any(x => x.Id == v.Id) ? [.. s.Categories.Select(x => x.Id == v.Id ? v : x)] : s.Categories.Add(v) }, v.Id);
            }
            case ReorderCategories c:
            {
                var active = s.Categories.Where(x => !x.Archived).Select(x => x.Id).ToHashSet();
                if (c.Ids.IsDefault || c.Ids.Length != active.Count || c.Ids.Distinct().Count() != c.Ids.Length || c.Ids.Any(id => !active.Contains(id))) throw new PlannerException("InvalidCategoryOrder");
                var order = c.Ids.Select((id, index) => (id, key: index * 1024m)).ToDictionary(x => x.id, x => x.key);
                return new(s with { Categories = [.. s.Categories.Select(x => order.TryGetValue(x.Id, out var key) && x.SortKey != key ? x with { SortKey = key, ModifiedAt = now } : x)] });
            }
            case InitializeCategories:
            {
                foreach (var v in PlannerDefaults.Categories(s.Options.Language, now))
                    if (!s.Categories.Any(x => x.BuiltInKey == v.BuiltInKey)) s = s with { Categories = s.Categories.Add(v with { SortKey = s.Categories.Select(x => x.SortKey).DefaultIfEmpty(-1024).Max() + 1024 }) };
                return new(s);
            }
            case ArchiveCategory c:
            {
                var v = s.Categories.SingleOrDefault(x => x.Id == c.Id) ?? throw new PlannerException("CategoryNotFound");
                return new(s with { Categories = Put(s.Categories, v with { Archived = true, ModifiedAt = now }) }, v.Id);
            }
            case SaveAsset c:
            {
                var v = Stamp(c.Value, s.Assets.SingleOrDefault(x => x.Id == c.Value.Id), now);
                return new(s with { Assets = Put(s.Assets, v) }, v.Id);
            }
            case RemoveAsset c:
                if (!s.Assets.Any(a => a.Id == c.Id)) throw new PlannerException("AssetNotFound");
                if (s.Activities.Any(a => a.Notes.AssetIds.Contains(c.Id)) || s.Reviews.Any(r => r.AssetIds.Contains(c.Id))) throw new PlannerException("AssetInUse");
                return new(s with { Assets = [.. s.Assets.Where(a => a.Id != c.Id)] });
            case SaveDecoration c:
            {
                var old = s.Decorations.SingleOrDefault(d => c.Value.Id == Guid.Empty ? d.Date == c.Value.Date : d.Id == c.Value.Id);
                var d = Stamp(c.Value, old, now);
                return new(s with { Decorations = Put(s.Decorations, d) }, d.Id);
            }
            case DeleteDecoration c:
                if (!s.Decorations.Any(d => d.Id == c.Id)) throw new PlannerException("DecorationNotFound");
                return new(s with { Decorations = [.. s.Decorations.Where(d => d.Id != c.Id)] });
            default: throw new PlannerException("UnknownCommand");
        }
    }
    static ImmutableArray<Todo> PrepareTemplates(ImmutableArray<Todo> nodes, Guid series, DateTimeOffset now, ImmutableArray<Todo> previous)
    {
        if (nodes.IsDefaultOrEmpty || nodes.Any(t => t == null || t.Id == Guid.Empty)) throw new PlannerException("TemplateIdsRequired");
        if (nodes.Count(t => t.ParentId == null) != 1 || nodes.Select(t => t.Id).Distinct().Count() != nodes.Length) throw new PlannerException("InvalidTemplateTree");
        return [.. nodes.Select(t => Stamp(t, previous.SingleOrDefault(p => p.Id == t.Id), now) with
        { Kind = TodoKind.SeriesTemplate, SeriesId = series, Origin = null, Status = TodoStatus.Pending, CompletedAt = null, CompletionIntent = CompletionIntent.Automatic, CompletionSnapshot = null, CompletionSnapshotInvalidated = false, CarryDate = null, FrozenCarryDate = null, Moves = [] })];
    }
    static CommandChange Edit(PlannerState s, EditSeries c, DateTimeOffset now)
    {
        if (!Enum.IsDefined(c.Scope)) throw new PlannerException("InvalidEditScope");
        var series = s.Series.SingleOrDefault(x => x.Id == c.SeriesId) ?? throw new PlannerException("SeriesNotFound");
        if (series.Archived || series.SuppressedFrom <= c.From) throw new PlannerException("SeriesCancelled");
        var version = PlannerQueries.Version(series, c.From) ?? throw new PlannerException("RuleVersionNotFound");
        var nodes = PrepareTemplates(c.Templates.IsDefault ? version.Templates : c.Templates, series.Id, now, version.Templates);
        if (!nodes.Any(t => t.Id == series.RootTodoId && t.ParentId == null)) throw new PlannerException("SeriesRootCannotChange");
        if (c.Scope == EditScope.Single)
        {
            if (c.Rule != null) throw new PlannerException("SingleInstanceCannotChangeRule");
            foreach (var node in version.Templates)
            {
                var reference = TodoRef.For(new(series.Id, node.Id, c.From)); s = Materialize(s, reference); var old = Get(s, reference);
                if (old.Status == TodoStatus.Completed) continue;
                var draft = nodes.SingleOrDefault(t => t.Id == node.Id);
                if (draft == null) throw new PlannerException("SingleEditCannotChangeTree");
                var updated = (c.Templates.IsDefault ? old : PatchTemplate(old, node, draft, now)) with { ModifiedAt = now, PlannedDate = c.MoveTo ?? old.PlannedDate };
                s = s with { Todos = Put(s.Todos, updated) };
            }
            if (nodes.Length != version.Templates.Length) throw new PlannerException("SingleEditCannotChangeTree");
            return new(s, series.Id);
        }
        if (c.MoveTo != null) throw new PlannerException("MoveDateRequiresSingleScope");
        // Split rule/template history at the effective date. Completed snapshots always survive.
        var versions = c.Scope == EditScope.EntireSeries
            ? series.Versions.Select(v => v with { Rule = c.Rule ?? v.Rule, Templates = PrepareTemplates(c.Templates.IsDefault ? v.Templates : c.Templates, series.Id, now, v.Templates) }).ToImmutableArray()
            : series.Versions.Where(v => v.EffectiveFrom < c.From).Select(v => v.EffectiveThrough == null || v.EffectiveThrough >= c.From ? v with { EffectiveThrough = c.From.AddDays(-1) } : v)
                .Append(new RuleVersion(Guid.NewGuid(), c.From, null, c.Rule ?? version.Rule, nodes)).ToImmutableArray();
        if (c.Scope == EditScope.EntireSeries && c.Rule != null && c.Rule.Start < versions.Min(v => v.EffectiveFrom))
        { var first = versions.MinBy(v => v.EffectiveFrom)!; versions = [.. versions.Select(v => v.Id == first.Id ? v with { EffectiveFrom = c.Rule.Start } : v)]; }
        // Keep retired identity tombstones: deleting a template must not invalidate a completed snapshot.
        var known = versions.SelectMany(v => v.Templates).Select(t => t.Id).ToHashSet();
        var retired = series.RetiredNodeIds.Concat(series.Versions.SelectMany(v => v.Templates).Select(t => t.Id).Where(id => !known.Contains(id))).Distinct().Where(id => !known.Contains(id)).ToImmutableArray();
        var affected = s.Todos.Where(t => t.Origin?.SeriesId == series.Id && t.Status != TodoStatus.Completed && (c.Scope == EditScope.EntireSeries || t.Origin.Date >= c.From)).ToArray();
        var protectedIds = Protected(s, series.Id);
        var removed = new HashSet<Guid>();
        var excluded = new HashSet<Guid>();
        foreach (var old in affected)
        {
            var applicable = versions.SingleOrDefault(v => v.EffectiveFrom <= old.Origin!.Date && (v.EffectiveThrough == null || old.Origin.Date <= v.EffectiveThrough));
            var draft = applicable?.Templates.SingleOrDefault(t => t.Id == old.Origin!.NodeId);
            var prior = PlannerQueries.Version(series, old.Origin!.Date)?.Templates.SingleOrDefault(t => t.Id == old.Origin.NodeId);
            bool localException = prior == null && series.RetiredNodeIds.Contains(old.Origin.NodeId);
            if (applicable == null || !Recurrence.Matches(applicable.Rule, old.Origin.Date) || draft == null && !localException)
            { excluded.Add(old.Id); if (!protectedIds.Contains(old.Id)) removed.Add(old.Id); else s = s with { Todos = Put(s.Todos, old with { Status = TodoStatus.Cancelled, ModifiedAt = now }) }; continue; }
            // Existing instances keep their explicit tree/date exceptions; generated future instances use the new tree.
            s = s with { Todos = Put(s.Todos, c.Templates.IsDefault || draft == null ? old : PatchTemplate(old, prior, draft, now)) };
        }
        // Valid P2 data may contain ordinary, instance-local descendants of a recurring node.
        foreach (var child in s.Todos.Where(t => t.Origin == null && t.Status != TodoStatus.Completed).ToArray())
        {
            bool affectedChild = false;
            for (var parent = child.ParentId; parent is Guid id; parent = s.Todos.Single(t => t.Id == id).ParentId) if (excluded.Contains(id)) { affectedChild = true; break; }
            if (!affectedChild) continue;
            if (!protectedIds.Contains(child.Id)) removed.Add(child.Id); else s = s with { Todos = Put(s.Todos, child with { Status = TodoStatus.Cancelled, ModifiedAt = now }) };
        }
        s = Detach(s, removed, now);
        var updatedSeries = PreserveCompletedBranches(s, series, series with { Versions = versions, RetiredNodeIds = retired, ModifiedAt = now });
        return new(s with { Todos = [.. s.Todos.Where(t => !removed.Contains(t.Id))], TodoOrderings = [.. s.TodoOrderings.Where(o => o.ParentId == null || !removed.Contains(o.ParentId.Value))], Series = Put(s.Series, updatedSeries) }, series.Id);
    }
    static HashSet<Guid> Protected(PlannerState s, Guid series)
    {
        var ids = new HashSet<Guid>();
        foreach (var done in s.Todos.Where(t => t.Status == TodoStatus.Completed))
        {
            var chain = new List<Todo>(); var t = done;
            while (true) { chain.Add(t); if (t.ParentId is not Guid p) break; t = s.Todos.Single(x => x.Id == p); }
            if (chain.Any(x => x.Origin?.SeriesId == series)) foreach (var x in chain) ids.Add(x.Id);
        }
        return ids;
    }
    static CommandChange Cancel(PlannerState s, CancelSeries c, DateTimeOffset now)
    {
        if (!Enum.IsDefined(c.Scope)) throw new PlannerException("InvalidEditScope");
        var series = s.Series.SingleOrDefault(x => x.Id == c.SeriesId) ?? throw new PlannerException("SeriesNotFound");
        if (c.NodeId is Guid selected && selected != series.RootTodoId && c.Scope != EditScope.Single)
        {
            var version = PlannerQueries.Version(series, c.From) ?? throw new PlannerException("RuleVersionNotFound");
            if (!version.Templates.Any(t => t.Id == selected)) throw new PlannerException("TemplateNotFound");
            var subtree = Subtree(version.Templates, selected);
            return Edit(s, new(series.Id, c.From, c.Scope, Templates: [.. version.Templates.Where(t => !subtree.Contains(t.Id))]), now);
        }
        var sourceTemplates = PlannerQueries.Version(series, c.From)?.Templates ?? [];
        var selectedNodes = c.NodeId is Guid node && node != series.RootTodoId ? Subtree(sourceTemplates, node) : null;
        if (selectedNodes != null && !sourceTemplates.Any(t => t.Id == c.NodeId))
        {
            var local = s.Todos.SingleOrDefault(t => t.Origin == new OccurrenceKey(series.Id, c.NodeId!.Value, c.From));
            if (local == null || c.Scope != EditScope.Single) throw new PlannerException("TemplateNotFound");
            selectedNodes = s.Todos.Where(t => Subtree(s.Todos, local.Id).Contains(t.Id) && t.Origin?.SeriesId == series.Id).Select(t => t.Origin!.NodeId).ToHashSet();
        }
        bool Affects(Todo t)
        {
            for (Todo? node = t; node != null; node = node.ParentId is Guid p ? s.Todos.Single(x => x.Id == p) : null)
                if (node.Origin?.SeriesId == series.Id && (c.Scope switch { EditScope.EntireSeries => true, EditScope.Single => node.Origin.Date == c.From, _ => node.Origin.Date >= c.From }) && (selectedNodes == null || selectedNodes.Contains(node.Origin.NodeId))) return true;
            return false;
        }
        // Materialized completed items and their ancestors are protected; links to removed pending instances are detached.
        var protectedIds = Protected(s, series.Id);
        var remove = s.Todos.Where(t => Affects(t) && !protectedIds.Contains(t.Id)).Select(t => t.Id).ToHashSet();
        s = Detach(s, remove, now);
        s = s with { Todos = [.. s.Todos.Where(t => !remove.Contains(t.Id)).Select(t => Affects(t) && t.Status != TodoStatus.Completed ? t with { Status = TodoStatus.Cancelled, ModifiedAt = now } : t)], TodoOrderings = [.. s.TodoOrderings.Where(o => o.ParentId == null || !remove.Contains(o.ParentId.Value))] };
        series = c.Scope switch
        {
            EditScope.Single => series with { Cancelled = [.. series.Cancelled.Concat(sourceTemplates.Select(t => t.Id).Concat(s.Todos.Where(t => t.Origin?.SeriesId == series.Id && t.Origin.Date == c.From).Select(t => t.Origin!.NodeId)).Concat(selectedNodes ?? []).Distinct().Where(id => selectedNodes == null || selectedNodes.Contains(id)).Select(id => new OccurrenceKey(series.Id, id, c.From))).Distinct()] },
            EditScope.EntireSeries => series with { Archived = true },
            _ => series with { SuppressedFrom = series.SuppressedFrom is DateOnly previous && previous < c.From ? previous : c.From }
        };
        return new(s with { Series = Put(s.Series, series with { ModifiedAt = now }) }, series.Id);
    }
}
