using System.Collections.Immutable;

namespace DesktopCalendar.Core;
internal static partial class PlannerCommands
{
    static TodoSeries PreserveCompletedBranches(PlannerState state, TodoSeries before, TodoSeries next)
    {
        var exclusions = next.Cancelled.ToHashSet();
        foreach (var group in state.Todos.Where(t => t.Origin?.SeriesId == before.Id && t.Status == TodoStatus.Completed).GroupBy(t => t.Origin!.Date))
        {
            var version = PlannerQueries.Version(next, group.Key); if (version == null) continue;
            var previous = PlannerQueries.Version(before, group.Key); var complete = group.Select(t => t.Origin!.NodeId).ToHashSet();
            IEnumerable<Guid> Ancestors(Todo node, ImmutableArray<Todo> tree)
            { for (var parent = node.ParentId; parent is Guid id; parent = tree.Single(t => t.Id == id).ParentId) yield return id; }
            foreach (var node in version.Templates)
            {
                var key = new OccurrenceKey(next.Id, node.Id, group.Key); if (state.Todos.Any(t => t.Origin == key)) continue;
                var frozen = Ancestors(node, version.Templates).FirstOrDefault(complete.Contains); if (frozen == Guid.Empty) continue;
                var old = previous?.Templates.SingleOrDefault(t => t.Id == node.Id);
                bool wasThere = old != null && Recurrence.Matches(previous!.Rule, group.Key) && !before.Cancelled.Contains(key) && Ancestors(old, previous.Templates).Contains(frozen);
                if (!wasThere) exclusions.Add(key);
            }
        }
        return next with { Cancelled = [.. exclusions] };
    }
    static Todo Editable(Todo target, Todo fields) => target with { Title = fields.Title, Description = fields.Description, CategoryId = fields.CategoryId, Color = fields.Color,
        Important = fields.Important, Urgent = fields.Urgent, PlannedTime = fields.PlannedTime, PlannedTimeZoneId = fields.PlannedTimeZoneId, PlannedOffset = fields.PlannedOffset, ManualProgress = fields.ManualProgress, ProgressMode = fields.ProgressMode };
    static CommandChange EditOccurrenceFields(PlannerState s, EditOccurrence c, DateTimeOffset now)
    {
        var original = Get(s, TodoRef.Of(c.Value));
        if (original.Origin is not OccurrenceKey key) throw new PlannerException("OccurrenceRequired");
        if (c.Scope == EditScope.Single) return Apply(s, new SaveTodo(c.Value), now);
        if (c.Value.PlannedDate != original.PlannedDate) throw new PlannerException("MoveDateRequiresSingleScope");
        var series = s.Series.Single(x => x.Id == key.SeriesId); var version = PlannerQueries.Version(series, key.Date) ?? throw new PlannerException("RuleVersionNotFound");
        if (!version.Templates.Any(t => t.Id == key.NodeId)) throw new PlannerException("LocalExceptionRequiresSingleScope");
        var templates = version.Templates.Select(t => t.Id == key.NodeId ? Editable(t, c.Value) : t).ToImmutableArray();
        return Edit(s, new(key.SeriesId, key.Date, c.Scope, Templates: templates), now);
    }
    static CommandChange AddSeriesChild(PlannerState s, AddSeriesTodo c, DateTimeOffset now)
    {
        if (c.Value.Kind != TodoKind.Once) throw new PlannerException("SeriesChildMustFollowRule");
        var parent = Get(s, c.Parent); if (parent.Origin is not OccurrenceKey key) throw new PlannerException("OccurrenceParentRequired");
        if (c.Scope == EditScope.Single) return AddOccurrence(s, new(c.Value, c.Parent), now);
        var series = s.Series.Single(x => x.Id == key.SeriesId); var version = PlannerQueries.Version(series, key.Date) ?? throw new PlannerException("RuleVersionNotFound");
        if (!version.Templates.Any(t => t.Id == key.NodeId)) throw new PlannerException("LocalExceptionRequiresSingleScope");
        var child = c.Value with { Id = c.Value.Id == Guid.Empty ? Guid.NewGuid() : c.Value.Id, ParentId = key.NodeId, PlannedDate = version.Rule.Start };
        return Edit(s, new(key.SeriesId, key.Date, c.Scope, Templates: version.Templates.Add(child)), now);
    }
    static CommandChange AddOccurrence(PlannerState s, AddOccurrenceTodo c, DateTimeOffset now)
    {
        s = Materialize(s, c.Parent); var parent = Get(s, c.Parent);
        if (parent.Origin is not OccurrenceKey origin || parent.Status == TodoStatus.Cancelled) throw new PlannerException("OccurrenceParentRequired");
        var node = c.Value.Id == Guid.Empty ? Guid.NewGuid() : c.Value.Id; var key = new OccurrenceKey(origin.SeriesId, node, origin.Date);
        if (c.Value.Status != TodoStatus.Pending || c.Value.CompletionIntent != CompletionIntent.Automatic || c.Value.CompletionSnapshot != null) throw new PlannerException("UseExplicitTodoCommand");
        var series = s.Series.Single(x => x.Id == origin.SeriesId);
        // Non-template identities already live in this registry; a local exception never
        // creates a recurring template or an infinite set of future child instances.
        if (series.RetiredNodeIds.Contains(node) || series.Versions.Any(v => v.Templates.Any(t => t.Id == node))) throw new PlannerException("DuplicateOrEmptyId");
        var child = Stamp(c.Value with { Id = key.StableId(), Origin = key, Kind = TodoKind.Occurrence, SeriesId = null, ParentId = parent.Id, CarryDate = null, FrozenCarryDate = null, Moves = [] }, null, now);
        return new(s with { Todos = s.Todos.Add(child), Series = Put(s.Series, series with { RetiredNodeIds = series.RetiredNodeIds.Add(node), ModifiedAt = now }) }, child.Id);
    }
    static Todo PatchTemplate(Todo value, Todo? before, Todo next, DateTimeOffset now)
    {
        if (before == null) return value; // a saved instance-only exception
        return value with
        {
            Title = before.Title != next.Title ? next.Title : value.Title,
            Description = before.Description != next.Description ? next.Description : value.Description,
            CategoryId = before.CategoryId != next.CategoryId ? next.CategoryId : value.CategoryId,
            Color = before.Color != next.Color ? next.Color : value.Color,
            Important = before.Important != next.Important ? next.Important : value.Important,
            Urgent = before.Urgent != next.Urgent ? next.Urgent : value.Urgent,
            PlannedTime = before.PlannedTime != next.PlannedTime ? next.PlannedTime : value.PlannedTime,
            PlannedTimeZoneId = before.PlannedTimeZoneId != next.PlannedTimeZoneId ? next.PlannedTimeZoneId : value.PlannedTimeZoneId,
            PlannedOffset = before.PlannedOffset != next.PlannedOffset ? next.PlannedOffset : value.PlannedOffset,
            ProgressMode = before.ProgressMode != next.ProgressMode ? next.ProgressMode : value.ProgressMode,
            ManualProgress = before.ManualProgress != next.ManualProgress ? next.ManualProgress : value.ManualProgress,
            ModifiedAt = now
        };
    }
}
