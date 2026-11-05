using DesktopCalendar.Core;
using System.Collections.Immutable;

internal static partial class Checks
{
    internal static readonly List<string> Results = [];
    internal static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    internal static void Check(string name, bool success)
    { Results.Add((success ? "PASS " : "FAIL ") + name); }
    internal static void Throws(string name, Action action, string? code = null)
    {
        try { action(); Check(name, false); }
        catch (PlannerException e) { Check(name, code == null || e.Code == code); }
    }
    internal static Todo Todo(string name, DateOnly day, decimal order = 0) => new() { Id = Guid.NewGuid(), Title = name, PlannedDate = day, SortKey = order, CreatedAt = Now, ModifiedAt = Now };
    internal static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--interrupt") return await InterruptChild(args);
        if (args.Length > 0 && args[0] == "--backup-interrupt") return BackupInterrupt(args);
        if (args.Length > 0 && args[0] is "--read" or "--verify") return await ReadChild(args);
        string output = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath("artifacts/p2-checks");
        Directory.CreateDirectory(output);
        try { ModelChecks(); await StorageChecks(output); await ServiceChecks(output); await BoundaryChecks(output); await CompletionChecks(output); await P4Checks(output); await P5Checks(output); await P6Checks(output); await P7Checks(output); await P8Checks(output); await ConversionChecks(output); await CategoryOrderChecks(output); }
        catch (Exception e) { Results.Add("FAIL unhandled " + e); }
        File.WriteAllLines(Path.Combine(output, "checks.txt"), Results);
        int failures = Results.Count(s => s.StartsWith("FAIL "));
        Console.WriteLine($"P2 PASS={Results.Count(s => s.StartsWith("PASS "))} FAIL={failures} NOT_VERIFIED={Results.Count(s => s.StartsWith("NOT VERIFIED "))}");
        foreach (var failed in Results.Where(s => s.StartsWith("FAIL "))) Console.WriteLine(failed);
        return failures == 0 ? 0 : 1;
    }
    static void ModelChecks()
    {
        var day = new DateOnly(2026, 11, 2);
        var a = Todo("A", day, 0); var b = Todo("B", day, 1); var c = Todo("C", day, 2); var d = Todo("D", day, 3);
        var state = PlannerState.Empty("UTC") with { Todos = [a, b with { Status = TodoStatus.Completed, CompletionIntent = CompletionIntent.ManualComplete, CompletedAt = Now }, c, d with { Status = TodoStatus.Completed, CompletionIntent = CompletionIntent.ManualComplete, CompletedAt = Now }] };
        PlannerIntegrity.Validate(state);
        Check("state validation and stable sibling grouping", string.Concat(PlannerQueries.Order(state.Todos, TodoSort.Manual).Select(t => t.Title)) == "ACBD");
        Throws("duplicate IDs rejected", () => PlannerIntegrity.Validate(state with { Todos = [a, a] }), "DuplicateOrEmptyId");
        Throws("parent cycle rejected", () => PlannerIntegrity.Validate(state with { Todos = [a with { ParentId = b.Id }, b with { ParentId = a.Id }] }), "ParentCycle");
        Throws("unknown category association rejected", () => PlannerIntegrity.Validate(state with { Todos = [a with { CategoryId = Guid.NewGuid() }] }), "CategoryNotFound");
        Throws("invalid override color rejected", () => PlannerIntegrity.Validate(state with { Todos = [a with { Color = "not-a-color" }] }), "InvalidColor");
        var history = a with { Kind = TodoKind.CarryHistory, CarryDate = day, FrozenCarryDate = day.AddDays(2), Status = TodoStatus.Completed, CompletionIntent = CompletionIntent.ManualComplete, CompletedAt = Now.AddDays(2) };
        var carryState = state with { Todos = [history] };
        PlannerIntegrity.Validate(carryState);
        Check("completed history appears crossed on all prior dates", Enumerable.Range(0, 3).All(i => PlannerQueries.TodosOn(carryState, day.AddDays(i), day.AddDays(8)).Single().Status == TodoStatus.Completed));
        Check("completed history stops extending", PlannerQueries.TodosOn(carryState, day.AddDays(3), day.AddDays(8)).IsEmpty);
        Check("latest carry freezes one effective date", PlannerQueries.TodosOn(carryState with { Todos = [history with { Kind = TodoKind.CarryLatest }] }, day, day.AddDays(8)).IsEmpty);
        var rule = new RecurrenceRule { Start = day, Frequency = Frequency.Daily, Interval = 2 };
        Recurrence.Validate(rule);
        Check("every N days", Recurrence.Matches(rule, day.AddDays(4)) && !Recurrence.Matches(rule, day.AddDays(3)));
        Check("recurrence Until is inclusive and blocks later dates", Recurrence.Matches(rule with { Until = day.AddDays(4) }, day.AddDays(4)) && !Recurrence.Matches(rule with { Until = day.AddDays(4) }, day.AddDays(6)));
        var weekly = rule with { Frequency = Frequency.Weekly, Interval = 2, Weekdays = [DayOfWeek.Monday, DayOfWeek.Friday], Count = 3 };
        Check("two-week cadence and count", Recurrence.Matches(weekly, day.AddDays(4)) && !Recurrence.Matches(weekly, day.AddDays(7)) && Recurrence.Matches(weekly, day.AddDays(14)) && !Recurrence.Matches(weekly, day.AddDays(18)));
        var monthly = rule with { Start = new DateOnly(2024, 1, 31), Frequency = Frequency.Monthly, Interval = 1, MonthDays = [31], Count = 2 };
        Check("monthly skips absent dates without clamping", !Recurrence.Matches(monthly, new(2024, 2, 29)) && Recurrence.Matches(monthly, new(2024, 3, 31)) && !Recurrence.Matches(monthly, new(2024, 5, 31)));
        var seriesId = Guid.NewGuid(); var rootId = Guid.NewGuid(); var key = new OccurrenceKey(seriesId, rootId, day);
        var template = a with { Id = rootId, Kind = TodoKind.SeriesTemplate, SeriesId = seriesId };
        var series = new TodoSeries { Id = seriesId, RootTodoId = rootId, CreatedAt = Now, ModifiedAt = Now, Versions = [new(Guid.NewGuid(), day, null, rule, [template])] };
        var recurring = state with { Todos = [], Series = [series] };
        PlannerIntegrity.Validate(recurring);
        Check("occurrence stable identity and lazy state", PlannerQueries.TodosOn(recurring, day, day).Single().Id == key.StableId() && recurring.Todos.IsEmpty && key.StableId() == new OccurrenceKey(seriesId, rootId, day).StableId());
        Check("week starts configurable", PlannerTime.Week(day, DayOfWeek.Sunday, "UTC").Start == day.AddDays(-1));
        var spring = PlannerTime.Day(new(2026, 3, 8), "Eastern Standard Time");
        var fall = PlannerTime.Day(new(2026, 11, 1), "Eastern Standard Time");
        Check("DST day durations are 23 and 25 actual hours", spring.Duration.TotalHours == 23 && fall.Duration.TotalHours == 25);
        Throws("DST gap local time rejected", () => PlannerTime.FromLocal(new(2026, 3, 8, 2, 30, 0), "Eastern Standard Time"), "InvalidLocalTime");
        Throws("ambiguous local time needs explicit offset", () => PlannerTime.FromLocal(new(2026, 11, 1, 1, 30, 0), "Eastern Standard Time"), "AmbiguousLocalTime");
        Check("explicit DST offsets distinguish repeated hour", PlannerTime.FromLocal(new(2026, 11, 1, 1, 30, 0), "Eastern Standard Time", TimeSpan.FromHours(-4)) != PlannerTime.FromLocal(new(2026, 11, 1, 1, 30, 0), "Eastern Standard Time", TimeSpan.FromHours(-5)));
        Check("interval union avoids overlap double count", PlannerTime.Seconds([new(Now, Now.AddHours(2)), new(Now.AddHours(1), Now.AddHours(3))]) == 10800);
        Throws("zero interval rejected", () => PlannerTime.Merge([new(Now, Now)]), "InvalidInterval");
        Check("cross midnight clipping", PlannerTime.Clip(new(Now.AddHours(11), Now.AddHours(19)), PlannerTime.Day(day, "UTC"))!.Duration.TotalHours == 1);
    }
}
