using System.Collections.Immutable;

namespace DesktopCalendar.Core;
internal static partial class PlannerCommands
{
    static CommandChange Convert(PlannerState s, ConvertTodo c, DateTimeOffset now)
    {
        var root = Get(s, c.Todo);
        if (root.Status == TodoStatus.Cancelled || c.Kind is not (TodoKind.Once or TodoKind.CarryHistory or TodoKind.CarryLatest or TodoKind.SeriesTemplate)) throw new PlannerException("InvalidTodoConversion");
        bool toSeries = c.Kind == TodoKind.SeriesTemplate, fromSeries = root.Origin != null;
        if (!toSeries && c.Rule != null || toSeries && c.Rule == null) throw new PlannerException("InvalidTodoConversion");
        if (!fromSeries && !toSeries)
        {
            bool carry = c.Kind is TodoKind.CarryHistory or TodoKind.CarryLatest;
            bool wasCarry = root.Kind is TodoKind.CarryHistory or TodoKind.CarryLatest;
            var day = carry || !wasCarry ? root.PlannedDate : c.EffectiveDate;
            var changed = root with { Kind = c.Kind, PlannedDate = day, ModifiedAt = now,
                CarryDate = carry ? root.CarryDate ?? day : null, Moves = carry ? root.Moves : [],
                FrozenCarryDate = carry && root.Status == TodoStatus.Completed ? root.FrozenCarryDate ?? new[] { day, PlannerTime.LocalDate(root.CompletedAt!.Value, s.Options.TimeZoneId) }.Max() : null };
            return new(s with { Todos = Put(s.Todos, changed) }, root.Id);
        }
        if (root.ParentId != null || fromSeries && root.Origin!.NodeId != s.Series.Single(x => x.Id == root.Origin.SeriesId).RootTodoId) throw new PlannerException("ConvertRootOnly");
        var tree = TodoCompletion.Descendants(s, root).Prepend(root).Where(t => t.Status != TodoStatus.Cancelled).ToArray();
        DateOnly first = c.EffectiveDate;
        if (toSeries)
        {
            Recurrence.Validate(c.Rule!);
            if (c.Rule!.Start != c.EffectiveDate) throw new PlannerException("ConversionStartMismatch");
            first = Recurrence.Next(c.Rule, c.EffectiveDate) ?? throw new PlannerException("ConversionNoOccurrence");
        }
        // A new series gets new template/occurrence identities. Completed old records never change identity.
        var selected = tree.Where(t => toSeries || t.Id == root.Id || t.Status == TodoStatus.Pending).ToArray();
        var ids = selected.ToDictionary(t => t.Id, _ => Guid.NewGuid());
        Guid? Parent(Todo t)
        {
            if (t.Id == root.Id) return null;
            var parent = t.ParentId; while (parent is Guid p && !ids.ContainsKey(p)) parent = tree.Single(n => n.Id == p).ParentId;
            return parent is Guid id ? ids[id] : null;
        }
        var replacement = selected.Select(t => t with { Id = ids[t.Id], ParentId = Parent(t), PlannedDate = first,
            Kind = toSeries ? TodoKind.Once : c.Kind, Origin = null, SeriesId = null, Status = TodoStatus.Pending, CompletedAt = null,
            CompletionIntent = CompletionIntent.Automatic, CompletionSnapshot = null, CompletionSnapshotInvalidated = false,
            CarryDate = null, FrozenCarryDate = null, Moves = [], ManualProgress = 0, CopiedFromId = t.Id, CreatedAt = now, ModifiedAt = now }).ToArray();
        Guid seriesId = Guid.NewGuid();
        var refs = selected.ToDictionary(t => t.Id, t => toSeries ? TodoRef.For(new(seriesId, ids[t.Id], first)) : new TodoRef(ids[t.Id]));
        var before = s;
        var affected = new HashSet<Guid>();
        if (root.Origin is OccurrenceKey key)
        {
            foreach (var t in s.Todos)
            {
                var node = t;
                while (true)
                {
                    if (node.Origin?.SeriesId == key.SeriesId && node.Origin.Date >= c.EffectiveDate) { affected.Add(t.Id); break; }
                    if (node.ParentId is not Guid parent) break; node = s.Todos.Single(n => n.Id == parent);
                }
            }
            s = Cancel(s, new(key.SeriesId, c.EffectiveDate, EditScope.FromDate), now).State;
            // Keep cancelled materialized records as resolvable history, including local-only branches and their links.
            s = s with { Todos = s.Todos.AddRange(before.Todos.Where(t => affected.Contains(t.Id) && !s.Todos.Any(n => n.Id == t.Id)).Select(t => t with { Status = TodoStatus.Cancelled, ModifiedAt = now })) };
        }
        else
        {
            affected = tree.Select(t => t.Id).ToHashSet();
            s = s with { Todos = [.. s.Todos.Select(t => affected.Contains(t.Id) && t.Status == TodoStatus.Pending ? t with { Status = TodoStatus.Cancelled, ModifiedAt = now } : t)] };
        }
        if (toSeries) s = ApplyCore(s, new CreateSeries(c.Rule!, [.. replacement], seriesId), now, 0).State;
        else foreach (var t in replacement) s = ApplyCore(s, new SaveTodo(t), now, 0).State;
        var nodes = selected.Where(t => t.Origin != null).ToDictionary(t => t.Origin!.NodeId, t => t.Id);
        TodoRef Remap(TodoRef reference)
        {
            var old = before.Todos.SingleOrDefault(t => t.Id == reference.Id);
            if (old == null || old.Status != TodoStatus.Pending || !affected.Contains(old.Id)) return reference;
            Guid id = old.Id;
            if (old.Origin != null && nodes.TryGetValue(old.Origin.NodeId, out var selectedId)) id = selectedId;
            return refs.GetValueOrDefault(id, reference);
        }
        s = s with {
            Activities = [.. before.Activities.Select(a => a.Todo != null && Remap(a.Todo) is TodoRef r && r != a.Todo ? a with { Todo = r, ModifiedAt = now } : a)],
            Goals = [.. before.Goals.Select(g => { var mapped = g.Todos.Select(Remap).DistinctBy(r => r.Id).ToImmutableArray(); return mapped.SequenceEqual(g.Todos) ? g : g with { Todos = mapped, ModifiedAt = now }; })]
        };
        // Materialize replacement refs used by links or orderings, using the normal identity mechanism.
        foreach (var reference in refs.Values.Where(r => s.Activities.Any(a => a.Todo == r) || s.Goals.Any(g => g.Todos.Contains(r)))) s = Materialize(s, reference);
        foreach (var order in before.TodoOrderings.Where(o => o.ParentId is Guid p && ids.ContainsKey(p)))
        {
            var reference = refs[order.ParentId!.Value]; s = Materialize(s, reference);
            s = s with { TodoOrderings = s.TodoOrderings.Add(order with { Id = Guid.NewGuid(), ParentId = reference.Id, CreatedAt = now, ModifiedAt = now }) };
        }
        return new(s, refs[root.Id].Id);
    }
}
