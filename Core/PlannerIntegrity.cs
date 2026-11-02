using System.Text.RegularExpressions;

namespace DesktopCalendar.Core;

public static partial class PlannerIntegrity
{
    [GeneratedRegex("^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")]
    private static partial Regex ColorPattern();
    [GeneratedRegex("^[a-fA-F0-9]{64}$")]
    private static partial Regex HashPattern();
    static void Require(bool condition, string code) { if (!condition) throw new PlannerException(code); }
    public static void ValidateOptions(PlannerOptions options)
    {
        Require(options != null && Enum.IsDefined(options.WeekStart) && Enum.IsDefined(options.TodoSort) && options.Language is "zh-CN" or "en", "InvalidOptions");
        _ = PlannerTime.Zone(options!.TimeZoneId);
    }
    public static void Validate(PlannerState state)
    {
        Require(state != null && state.Id != Guid.Empty && state.Revision >= 0, "InvalidDocument");
        ValidateOptions(state!.Options);
        Require(!state.Categories.IsDefault && !state.Todos.IsDefault && !state.Series.IsDefault && !state.Activities.IsDefault && !state.Goals.IsDefault && !state.Reviews.IsDefault && !state.Assets.IsDefault && !state.Decorations.IsDefault && !state.TodoOrderings.IsDefault, "InvalidCollections");
        Require(state.Categories.All(x => x != null) && state.Todos.All(x => x != null) && state.Series.All(x => x != null) && state.Activities.All(x => x != null) && state.Goals.All(x => x != null) && state.Reviews.All(x => x != null) && state.Assets.All(x => x != null) && state.Decorations.All(x => x != null) && state.TodoOrderings.All(x => x != null), "InvalidCollections");
        var ids = new HashSet<Guid> { state.Id };
        void Id(Guid id) => Require(id != Guid.Empty && ids.Add(id), "DuplicateOrEmptyId");
        void Stamp(Entity e) { Id(e.Id); Require(e.CreatedAt != default && e.ModifiedAt >= e.CreatedAt, "InvalidTimestamps"); }
        void Title(string text) => Require(!string.IsNullOrWhiteSpace(text) && text.Length <= 10000, "InvalidTitle");
        void Color(string? color) => Require(color == null || ColorPattern().IsMatch(color), "InvalidColor");
        void Category(Guid? id) => Require(id == null || state.Categories.Any(c => c.Id == id), "CategoryNotFound");
        void Assets(IEnumerable<Guid> refs)
        { Require(refs.Distinct().Count() == refs.Count() && refs.All(id => state.Assets.Any(a => a.Id == id)), "AssetNotFoundOrDuplicated"); }
        void Week(WeekKey week)
        {
            Require(week != null && Enum.IsDefined(week.FirstDay) && week.Start.DayOfWeek == week.FirstDay && week.Start.DayNumber <= DateOnly.MaxValue.DayNumber - 7, "InvalidWeek");
            _ = PlannerTime.Zone(week!.TimeZoneId);
        }
        void Task(Todo t, bool template)
        {
            Title(t.Title); Color(t.Color); Category(t.CategoryId);
            Require(t.Description != null && t.Description.Length <= 4_000_000 && Enum.IsDefined(t.Kind) && Enum.IsDefined(t.Status) && Enum.IsDefined(t.ProgressMode) && !t.Moves.IsDefault && t.ManualProgress is >= 0 and <= 100, "InvalidTodo");
            Require((t.Status == TodoStatus.Completed) == (t.CompletedAt != null), "InvalidCompletion");
            Require(template == (t.Kind == TodoKind.SeriesTemplate), "InvalidTemplateKind");
            Require((t.Kind == TodoKind.Occurrence) == (t.Origin != null) && (template ? t.SeriesId != null : t.SeriesId == null), "InvalidOccurrenceIdentity");
            if (t.Origin is OccurrenceKey key) Require(key.StableId() == t.Id && state.Series.Any(s => s.Id == key.SeriesId && (s.RetiredNodeIds.Contains(key.NodeId) || s.Versions.Any(v => v.Templates.Any(n => n.Id == key.NodeId)))), "InvalidOccurrenceIdentity");
            bool carry = t.Kind is TodoKind.CarryHistory or TodoKind.CarryLatest;
            Require(carry || t.CarryDate == null && t.FrozenCarryDate == null && t.Moves.Length == 0, "InvalidCarryFields");
            if (carry)
            {
                Require(t.CarryDate >= t.PlannedDate && (t.Status == TodoStatus.Completed ? t.FrozenCarryDate >= t.PlannedDate : t.FrozenCarryDate == null), "InvalidCarryDates");
                DateOnly previous = t.PlannedDate;
                foreach (var move in t.Moves) { Require(move != null && move.From == previous && move.To > move.From && move.At != default, "InvalidCarryHistory"); previous = move!.To; }
                Require(t.CarryDate == previous, "InvalidCarryHistory");
            }
        }
        void Forest(IEnumerable<Todo> tasks)
        {
            var map = tasks.ToDictionary(t => t.Id);
            foreach (var task in map.Values)
            {
                var seen = new HashSet<Guid> { task.Id }; var parent = task.ParentId;
                while (parent is Guid p) { Require(map.TryGetValue(p, out var ancestor), "ParentNotFound"); Require(seen.Add(p), "ParentCycle"); Require(seen.Count <= 128, "TaskTreeTooDeep"); parent = ancestor!.ParentId; }
            }
        }
        foreach (var c in state.Categories) { Stamp(c); Title(c.Name); Require(c.Color != null, "InvalidColor"); Color(c.Color); }
        Require(state.Decorations.Select(d => d.Date).Distinct().Count() == state.Decorations.Length, "DuplicateDateDecoration");
        foreach (var d in state.Decorations) { Stamp(d); Require(d.Color != null, "InvalidColor"); Color(d.Color); }
        Require(state.Categories.Where(c => c.BuiltInKey != null).Select(c => c.BuiltInKey).Distinct().Count() == state.Categories.Count(c => c.BuiltInKey != null), "DuplicateBuiltInCategory");
        var nodeOwners = new Dictionary<Guid, Guid>();
        foreach (var s in state.Series)
        {
            Stamp(s); Require(!s.Versions.IsDefaultOrEmpty && !s.Cancelled.IsDefault && !s.RetiredNodeIds.IsDefault && s.RetiredNodeIds.Distinct().Count() == s.RetiredNodeIds.Length && s.Cancelled.Distinct().Count() == s.Cancelled.Length, "InvalidSeries");
            Require(s.Versions.All(v => v != null) && s.Cancelled.All(k => k != null), "InvalidSeries");
            foreach (var retired in s.RetiredNodeIds) { Id(retired); nodeOwners.Add(retired, s.Id); }
            DateOnly? previousEnd = null; bool first = true;
            foreach (var v in s.Versions.OrderBy(v => v.EffectiveFrom))
            {
                Id(v.Id); Recurrence.Validate(v.Rule);
                Require(v.EffectiveThrough == null || v.EffectiveThrough >= v.EffectiveFrom, "InvalidRuleVersion");
                if (!first) Require(previousEnd != null && v.EffectiveFrom > previousEnd, "OverlappingRuleVersions");
                first = false; previousEnd = v.EffectiveThrough;
                Require(!v.Templates.IsDefaultOrEmpty && v.Templates.All(t => t != null) && v.Templates.Count(t => t.ParentId == null) == 1 && v.Templates.Any(t => t.Id == s.RootTodoId && t.ParentId == null), "InvalidTemplateTree");
                Require(v.Templates.Select(t => t.Id).Distinct().Count() == v.Templates.Length, "DuplicateTemplateId");
                foreach (var node in v.Templates)
                {
                    if (nodeOwners.TryGetValue(node.Id, out var owner)) Require(owner == s.Id, "DuplicateTemplateId");
                    else { Id(node.Id); nodeOwners.Add(node.Id, s.Id); }
                    Require(node.CreatedAt != default && node.ModifiedAt >= node.CreatedAt && node.SeriesId == s.Id && node.Status == TodoStatus.Pending && node.Origin == null, "InvalidTemplate"); Task(node, true);
                }
                Forest(v.Templates);
            }
            Require(s.Cancelled.All(k => k.SeriesId == s.Id && nodeOwners.TryGetValue(k.NodeId, out var owner) && owner == s.Id), "InvalidCancellation");
        }
        foreach (var t in state.Todos) { Stamp(t); Task(t, false); }
        Forest(state.Todos);
        Require(state.TodoOrderings.Select(o => (o.ParentId, o.Date)).Distinct().Count() == state.TodoOrderings.Length, "DuplicateOrderingGroup");
        foreach (var o in state.TodoOrderings)
        {
            Stamp(o); Require(Enum.IsDefined(o.Mode) && (o.ParentId == null) != (o.Date == null) && (o.ParentId == null || state.Todos.Any(t => t.Id == o.ParentId)), "InvalidOrderingGroup");
        }
        foreach (var a in state.Assets)
        {
            Stamp(a);
            Require(a.RelativePath != null && a.MediaType != null && a.Sha256 != null && a.Length > 0 && HashPattern().IsMatch(a.Sha256) && a.RelativePath.StartsWith("assets/images/", StringComparison.Ordinal) &&
                a.RelativePath.Count(c => c == '/') == 2 && !a.RelativePath.Contains("..") && !a.RelativePath.Any(c => c is ':' or '\\' or '%' or '?' or '#') &&
                new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" }.Contains(Path.GetExtension(a.RelativePath).ToLowerInvariant()) && a.MediaType.StartsWith("image/", StringComparison.Ordinal), "UnsafeAssetReference");
        }
        foreach (var a in state.Activities)
        {
            Stamp(a); Title(a.Title); Category(a.CategoryId); Color(a.Color); _ = PlannerTime.Zone(a.TimeZoneId);
            Require(a.Emoji != null && a.Summary != null && a.Reaction != null && !a.Intervals.IsDefaultOrEmpty && a.Notes != null && Enum.IsDefined(a.Notes.Format) && a.Notes.Text != null && !a.Notes.AssetIds.IsDefault, "InvalidActivity");
            Require(a.Intervals.All(i => i != null), "InvalidInterval");
            foreach (var interval in a.Intervals) { Id(interval.Id); Require(interval.End > interval.Start, "InvalidInterval"); }
            Assets(a.Notes!.AssetIds);
            if (a.Todo != null) _ = PlannerQueries.Resolve(state, a.Todo);
        }
        Require(state.Reviews.Select(r => r.Week).Distinct().Count() == state.Reviews.Length, "DuplicateWeekReview");
        foreach (var review in state.Reviews)
        {
            Stamp(review); Week(review.Week); Require(review.Markdown != null && !review.Sections.IsDefault && !review.Statistics.IsDefault && !review.AssetIds.IsDefault, "InvalidReview");
            Require(review.Sections.All(s => s != null) && review.Statistics.All(s => s != null), "InvalidReview");
            Assets(review.AssetIds);
            foreach (var section in review.Sections) { Id(section.Id); Require(section.Heading != null && section.Markdown != null, "InvalidReviewSection"); }
            foreach (var stat in review.Statistics) { Id(stat.Id); Require(stat.GeneratedAt != default && stat.Markdown != null, "InvalidStatisticsSnapshot"); }
        }
        foreach (var goal in state.Goals)
        {
            Stamp(goal); Week(goal.Week); Title(goal.Title); Category(goal.CategoryId);
            Require(Enum.IsDefined(goal.Kind) && Enum.IsDefined(goal.Source) && goal.Target > 0 && goal.ManualOverride is not < 0 && goal.Description != null && goal.Emoji != null && goal.CountUnit != null && !goal.Todos.IsDefault && !goal.Activities.IsDefault, "InvalidGoal");
            Require(goal.Kind != GoalKind.Duration || goal.Source != GoalSource.Todo, "TodoIsNotActualDuration");
            Require(goal.Kind != GoalKind.Check || goal.Target == 1 && goal.ManualOverride is null or 0 or 1, "InvalidCheckGoal");
            Require(goal.Todos.Distinct().Count() == goal.Todos.Length && goal.Activities.Distinct().Count() == goal.Activities.Length, "DuplicateGoalLink");
            foreach (var reference in goal.Todos) _ = PlannerQueries.Resolve(state, reference);
            Require(goal.Activities.All(id => state.Activities.Any(a => a.Id == id)), "ActivityNotFound");
            Require(goal.ReviewId == null || state.Reviews.Any(r => r.Id == goal.ReviewId && r.Week == goal.Week), "ReviewWeekMismatch");
        }
    }
}
