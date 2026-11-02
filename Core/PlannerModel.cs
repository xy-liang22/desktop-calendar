using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;

namespace DesktopCalendar.Core;

public sealed class PlannerException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
public enum TodoKind { Once, CarryHistory, CarryLatest, SeriesTemplate, Occurrence }
public enum TodoStatus { Pending, Completed, Cancelled }
public enum ProgressMode { Manual, Children }
public enum TodoSort { Manual, PlannedTime }
public enum Frequency { Daily, Weekly, Monthly }
public enum EditScope { Single, FromDate, FutureUnfinished, EntireSeries }
public enum ChildDisposition { DeleteSubtree, PromoteChildren }
public enum NoteFormat { PlainText, Markdown }
public enum GoalKind { Count, Duration, Check }
public enum GoalSource { None, Todo, Activity }

public abstract record Entity
{
    public Guid Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ModifiedAt { get; init; }
}
public sealed record Category : Entity
{
    public string Name { get; init; } = "";
    public string Color { get; init; } = "#A3D9C4";
    public string? BuiltInKey { get; init; }
    public decimal SortKey { get; init; }
    public bool Archived { get; init; }
}
public sealed record OccurrenceKey(Guid SeriesId, Guid NodeId, DateOnly Date)
{
    public Guid StableId()
    {
        var day = Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"desktop-calendar/occurrence/{SeriesId:D}/{NodeId:D}/{day}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
public sealed record TodoRef(Guid Id, OccurrenceKey? Occurrence = null)
{
    public static TodoRef Of(Todo todo) => new(todo.Id, todo.Origin);
    public static TodoRef For(OccurrenceKey key) => new(key.StableId(), key);
}
public sealed record TodoMove(DateOnly From, DateOnly To, DateTimeOffset At);
public sealed record Todo : Entity
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public Guid? CategoryId { get; init; }
    public string? Color { get; init; }
    public bool Important { get; init; }
    public bool Urgent { get; init; }
    public DateOnly PlannedDate { get; init; }
    public TimeOnly? PlannedTime { get; init; }
    public TodoKind Kind { get; init; }
    public TodoStatus Status { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public DateOnly? FrozenCarryDate { get; init; }
    public DateOnly? CarryDate { get; init; }
    public ImmutableArray<TodoMove> Moves { get; init; } = [];
    public Guid? ParentId { get; init; }
    public decimal SortKey { get; init; }
    public ProgressMode ProgressMode { get; init; }
    public decimal ManualProgress { get; init; }
    public Guid? SeriesId { get; init; }
    public OccurrenceKey? Origin { get; init; }
    public Guid? CopiedFromId { get; init; }
}
public sealed record RecurrenceRule
{
    public Frequency Frequency { get; init; }
    public int Interval { get; init; } = 1;
    public DateOnly Start { get; init; }
    public DateOnly? Until { get; init; }
    public int? Count { get; init; }
    public DayOfWeek WeekStart { get; init; } = DayOfWeek.Monday;
    public ImmutableArray<DayOfWeek> Weekdays { get; init; } = [];
    public ImmutableArray<int> MonthDays { get; init; } = [];
    public string TimeZoneId { get; init; } = "UTC";
    public ImmutableArray<DateOnly> ExcludedDates { get; init; } = [];
    public ImmutableArray<DateOnly> IncludedDates { get; init; } = [];
}
public sealed record RuleVersion(Guid Id, DateOnly EffectiveFrom, DateOnly? EffectiveThrough, RecurrenceRule Rule, ImmutableArray<Todo> Templates);
public sealed record TodoSeries : Entity
{
    public Guid RootTodoId { get; init; }
    public ImmutableArray<RuleVersion> Versions { get; init; } = [];
    public DateOnly? SuppressedFrom { get; init; }
    public bool Archived { get; init; }
    public ImmutableArray<OccurrenceKey> Cancelled { get; init; } = [];
}
public sealed record Note
{
    public NoteFormat Format { get; init; }
    public string Text { get; init; } = "";
    public ImmutableArray<Guid> AssetIds { get; init; } = [];
}
public record TimeInterval(DateTimeOffset Start, DateTimeOffset End)
{
    public TimeSpan Duration => End - Start;
}
public sealed record ActivityPeriod(Guid Id, DateTimeOffset Start, DateTimeOffset End) : TimeInterval(Start, End);
public sealed record Activity : Entity
{
    public string Title { get; init; } = "";
    public string Emoji { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Reaction { get; init; } = "";
    public Guid? CategoryId { get; init; }
    public string? Color { get; init; }
    public string TimeZoneId { get; init; } = "UTC";
    public ImmutableArray<ActivityPeriod> Intervals { get; init; } = [];
    public Note Notes { get; init; } = new();
    public TodoRef? Todo { get; init; }
    public string? FormerTodoTitle { get; init; }
}
// Historical week identity captures its first day and zone; later settings do not move it.
public sealed record WeekKey(DateOnly Start, DayOfWeek FirstDay, string TimeZoneId);
public sealed record WeekGoal : Entity
{
    public WeekKey Week { get; init; } = new(new DateOnly(2000, 1, 3), DayOfWeek.Monday, "UTC");
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Emoji { get; init; } = "";
    public Guid? CategoryId { get; init; }
    public GoalKind Kind { get; init; }
    public GoalSource Source { get; init; }
    public decimal Target { get; init; } = 1;
    public string CountUnit { get; init; } = "";
    public decimal? ManualOverride { get; init; }
    public bool Checked { get; init; }
    public decimal SortKey { get; init; }
    public ImmutableArray<TodoRef> Todos { get; init; } = [];
    public ImmutableArray<Guid> Activities { get; init; } = [];
    public Guid? ReviewId { get; init; }
}
public sealed record ReviewSection(Guid Id, string Heading, string Markdown);
public sealed record StatisticsSnapshot(Guid Id, DateTimeOffset GeneratedAt, string Markdown);
public sealed record WeeklyReview : Entity
{
    public WeekKey Week { get; init; } = new(new DateOnly(2000, 1, 3), DayOfWeek.Monday, "UTC");
    public string Markdown { get; init; } = "";
    public ImmutableArray<ReviewSection> Sections { get; init; } = [];
    public ImmutableArray<StatisticsSnapshot> Statistics { get; init; } = [];
    public ImmutableArray<Guid> AssetIds { get; init; } = [];
}
public sealed record LocalAsset : Entity
{
    public string RelativePath { get; init; } = "";
    public string MediaType { get; init; } = "";
    public long Length { get; init; }
    public string Sha256 { get; init; } = "";
}
public sealed record PlannerOptions
{
    public DayOfWeek WeekStart { get; init; } = DayOfWeek.Monday;
    public string TimeZoneId { get; init; } = "UTC";
    public string Language { get; init; } = "zh-CN";
    public TodoSort TodoSort { get; init; }
}
public sealed record PlannerState
{
    public Guid Id { get; init; }
    public long Revision { get; init; }
    public PlannerOptions Options { get; init; } = new();
    public ImmutableArray<Category> Categories { get; init; } = [];
    public ImmutableArray<Todo> Todos { get; init; } = [];
    public ImmutableArray<TodoSeries> Series { get; init; } = [];
    public ImmutableArray<Activity> Activities { get; init; } = [];
    public ImmutableArray<WeekGoal> Goals { get; init; } = [];
    public ImmutableArray<WeeklyReview> Reviews { get; init; } = [];
    public ImmutableArray<LocalAsset> Assets { get; init; } = [];
    public static PlannerState Empty(string? zone = null) => new() { Id = Guid.NewGuid(), Options = new() { TimeZoneId = zone ?? TimeZoneInfo.Local.Id } };
}
