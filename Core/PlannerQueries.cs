using System.Collections.Immutable;

namespace DesktopCalendar.Core;

public sealed record ActivitySlice(Guid ActivityId, TimeInterval Interval);
public sealed record GoalProgress(decimal Automatic, decimal? Override, decimal Effective, bool Complete, ImmutableArray<TodoRef> OutsideWeekTodos);
public sealed record CategoryDuration(Guid? CategoryId, decimal Seconds, decimal FractionOfWeek);
public sealed record TodoDateNode(Todo Value, bool ContextOnly, ImmutableArray<TodoDateNode> Children);

public static class PlannerQueries
{
    public static ImmutableArray<Category> OrderedCategories(PlannerState state, bool includeArchived = false) =>
        [.. state.Categories.Where(c => includeArchived || !c.Archived).OrderBy(c => c.SortKey)];
    public static string AssetReference(Guid id) => id != Guid.Empty ? "asset://" + id.ToString("D") : throw new PlannerException("InvalidAssetReference");
    public static LocalAsset ResolveAsset(PlannerState state, string reference)
    {
        const string prefix = "asset://";
        if (reference == null || !reference.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(reference[prefix.Length..], "D", out var id)) throw new PlannerException("InvalidAssetReference");
        return state.Assets.SingleOrDefault(a => a.Id == id) ?? throw new PlannerException("AssetNotFound");
    }
    public static RuleVersion? Version(TodoSeries series, DateOnly date) => series.Versions.SingleOrDefault(v => v.EffectiveFrom <= date && (v.EffectiveThrough == null || date <= v.EffectiveThrough));
    public static Todo Resolve(PlannerState state, TodoRef reference)
    {
        if (reference == null) throw new PlannerException("InvalidTodoReference");
        var saved = state.Todos.SingleOrDefault(t => t.Id == reference.Id);
        if (saved != null)
        {
            if (saved.Origin != reference.Occurrence) throw new PlannerException("TodoReferenceMismatch");
            return saved;
        }
        if (reference.Occurrence is not OccurrenceKey key || key.StableId() != reference.Id) throw new PlannerException("TodoNotFound");
        var series = state.Series.SingleOrDefault(s => s.Id == key.SeriesId) ?? throw new PlannerException("SeriesNotFound");
        var version = Version(series, key.Date);
        if (series.Archived || series.SuppressedFrom <= key.Date || series.Cancelled.Contains(key) || version == null || !Recurrence.Matches(version.Rule, key.Date)) throw new PlannerException("OccurrenceNotFound");
        var template = version.Templates.SingleOrDefault(t => t.Id == key.NodeId) ?? throw new PlannerException("TemplateNotFound");
        return template with { Id = reference.Id, Kind = TodoKind.Occurrence, SeriesId = null, Origin = key, PlannedDate = key.Date,
            ParentId = template.ParentId is Guid parent ? new OccurrenceKey(series.Id, parent, key.Date).StableId() : null };
    }
    public static DateOnly CarryEnd(Todo todo, DateOnly asOf)
    {
        if (todo.Status == TodoStatus.Completed) return todo.FrozenCarryDate ?? throw new PlannerException("CarryFreezeMissing");
        return new[] { todo.PlannedDate, todo.CarryDate ?? todo.PlannedDate, asOf }.Max();
    }
    public static ImmutableArray<Todo> TodosOn(PlannerState state, DateOnly date, DateOnly asOf, TodoSort? sort = null)
    {
        var rows = state.Todos.Where(t => t.Status != TodoStatus.Cancelled && (t.Kind switch
        {
            TodoKind.Once or TodoKind.Occurrence => t.PlannedDate == date,
            TodoKind.CarryHistory => date >= t.PlannedDate && date <= CarryEnd(t, asOf),
            TodoKind.CarryLatest => date == CarryEnd(t, asOf), _ => false
        })).ToList();
        foreach (var series in state.Series)
        {
            var version = Version(series, date);
            if (version == null) continue;
            foreach (var node in version.Templates)
            {
                var key = new OccurrenceKey(series.Id, node.Id, date);
                if (state.Todos.Any(t => t.Origin == key)) continue; // moved/completed override wins on every query date
                try { rows.Add(Resolve(state, TodoRef.For(key))); }
                catch (PlannerException e) when (e.Code is "OccurrenceNotFound") { }
            }
        }
        // Flat results retain ParentId; a UI can request/sort each sibling group.
        return [.. rows.GroupBy(t => t.ParentId).SelectMany(g => Order(g, sort ?? Ordering(state, g.Key, date)))];
    }
    public static TodoSort Ordering(PlannerState state, Guid? parent, DateOnly date) => state.TodoOrderings.SingleOrDefault(o => o.ParentId == parent && (parent != null || o.Date == date))?.Mode ?? state.Options.TodoSort;
    public static IOrderedEnumerable<Todo> Order(IEnumerable<Todo> siblings, TodoSort sort) => siblings
        .OrderBy(t => t.Status == TodoStatus.Completed)
        .ThenBy(t => sort == TodoSort.PlannedTime ? t.PlannedTime?.Ticks ?? long.MaxValue : 0)
        .ThenBy(t => t.SortKey).ThenBy(t => t.Id);
    // Context ancestors keep completed children reachable when a series/parent is cancelled
    // or the child's planned day differs. They are not additional tasks counted for that day.
    public static ImmutableArray<TodoDateNode> TodoTreeOn(PlannerState state, DateOnly date, DateOnly asOf, TodoSort? sort = null)
    {
        var visible = TodosOn(state, date, asOf, sort); var originals = visible.Select(t => t.Id).ToHashSet();
        var map = visible.ToDictionary(t => t.Id);
        foreach (var row in visible)
        {
            var t = row;
            while (t.ParentId is Guid p)
            {
                if (map.ContainsKey(p)) break;
                var parent = state.Todos.SingleOrDefault(x => x.Id == p);
                if (parent == null && t.Origin is OccurrenceKey k)
                {
                    var template = Version(state.Series.Single(s => s.Id == k.SeriesId), k.Date)!.Templates.Single(x => new OccurrenceKey(k.SeriesId, x.Id, k.Date).StableId() == p);
                    parent = Resolve(state, TodoRef.For(new(k.SeriesId, template.Id, k.Date)));
                }
                t = parent ?? throw new PlannerException("ParentNotFound"); map.Add(t.Id, t);
            }
        }
        TodoDateNode Node(Todo t) => new(t, !originals.Contains(t.Id), [.. Order(map.Values.Where(x => x.ParentId == t.Id), sort ?? Ordering(state, t.Id, date)).Select(Node)]);
        return [.. Order(map.Values.Where(x => x.ParentId == null), sort ?? Ordering(state, null, date)).Select(Node)];
    }
    public static decimal Progress(PlannerState state, TodoRef reference)
    {
        var todo = Resolve(state, reference);
        if (todo.Status == TodoStatus.Completed) return 100;
        if (todo.ProgressMode == ProgressMode.Manual) return todo.ManualProgress;
        var children = Children(state, todo).Where(t => t.Status != TodoStatus.Cancelled).ToArray();
        return children.Length == 0 ? todo.ManualProgress : children.Average(t => Progress(state, TodoRef.Of(t)));
    }
    public static ImmutableArray<Todo> Children(PlannerState state, Todo parent)
    {
        var children = state.Todos.Where(t => t.ParentId == parent.Id).ToList();
        if (parent.Origin is OccurrenceKey key)
        {
            var version = Version(state.Series.Single(s => s.Id == key.SeriesId), key.Date);
            if (version != null) foreach (var template in version.Templates.Where(t => t.ParentId == key.NodeId))
            {
                var childKey = new OccurrenceKey(key.SeriesId, template.Id, key.Date);
                if (children.Any(c => c.Origin == childKey)) continue;
                try { var resolved = Resolve(state, TodoRef.For(childKey)); if (resolved.ParentId == parent.Id) children.Add(resolved); } catch (PlannerException e) when (e.Code == "OccurrenceNotFound") { }
            }
        }
        return [.. Order(children, Ordering(state, parent.Id, parent.PlannedDate))];
    }
    public static ImmutableArray<ActivitySlice> ActivitiesIn(PlannerState state, TimeInterval range)
    {
        if (range.End <= range.Start) throw new PlannerException("InvalidInterval");
        return [.. state.Activities.SelectMany(a => PlannerTime.Merge(a.Intervals).Select(i => (a.Id, Clip: PlannerTime.Clip(i, range))))
            .Where(x => x.Clip != null).Select(x => new ActivitySlice(x.Id, x.Clip!)).OrderBy(x => x.Interval.Start).ThenBy(x => x.ActivityId)];
    }
    public static ImmutableArray<ActivitySlice> ActivitiesOn(PlannerState state, DateOnly date, string? zone = null)
    {
        var day = PlannerTime.Day(date, zone ?? state.Options.TimeZoneId);
        return day.End == day.Start ? [] : ActivitiesIn(state, day);
    }
    public static ImmutableArray<CategoryDuration> CategoryStatistics(PlannerState state, WeekKey week)
    {
        var range = PlannerTime.WeekRange(week); decimal denominator = (decimal)range.Duration.Ticks / TimeSpan.TicksPerSecond;
        return [.. state.Activities.GroupBy(a => a.CategoryId).Select(g =>
        {
            decimal seconds = PlannerTime.Seconds(g.SelectMany(a => a.Intervals).Select(i => PlannerTime.Clip(i, range)).OfType<TimeInterval>());
            return new CategoryDuration(g.Key, seconds, denominator > 0 ? seconds / denominator : 0);
        }).Where(s => s.Seconds > 0)];
    }
    public static GoalProgress Goal(PlannerState state, WeekGoal goal)
    {
        var range = PlannerTime.WeekRange(goal.Week);
        decimal automatic = 0;
        var outside = goal.Todos.Where(r => { var t = Resolve(state, r); return t.PlannedDate < goal.Week.Start || t.PlannedDate >= goal.Week.Start.AddDays(7); }).ToImmutableArray();
        if (goal.Source == GoalSource.Todo)
            automatic = goal.Todos.Select(r => Resolve(state, r)).Count(t => t.Status == TodoStatus.Completed);
        if (goal.Source == GoalSource.Activity)
        {
            var activities = state.Activities.Where(a => goal.Activities.Contains(a.Id));
            automatic = goal.Kind == GoalKind.Duration
                ? PlannerTime.Seconds(activities.SelectMany(a => a.Intervals).Select(i => PlannerTime.Clip(i, range)).OfType<TimeInterval>())
                : activities.Count(a => a.Intervals.Any(i => PlannerTime.Clip(i, range) != null));
        }
        if (goal.Kind == GoalKind.Check)
            automatic = goal.Source == GoalSource.None ? (goal.Checked ? 1 : 0) : automatic > 0 ? 1 : 0;
        decimal effective = goal.ManualOverride ?? automatic;
        return new(automatic, goal.ManualOverride, effective, effective >= goal.Target, outside);
    }
}
