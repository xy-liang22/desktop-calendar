using DesktopCalendar.Core;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;

internal static partial class Checks
{
    static async Task TimeZoneChecks(string output)
    {
        var date = new DateOnly(2026, 11, 5);
        var legacy = Todo("floating", date) with { PlannedTime = new(1, 0), ProgressMode = ProgressMode.Manual };
        var timed = Todo("zoned", date, 1024) with { PlannedTime = new(1, 0), PlannedTimeZoneId = "China Standard Time" };
        var plain = Todo("date only", date, 2048) with { PlannedTimeZoneId = "China Standard Time" };
        var state = PlannerState.Empty("UTC") with { Todos = [legacy, timed, plain] };
        Check("timezone conversion shifts date without altering stored planned clock", TodoTime.Display(state, timed) == new TodoClock(date.AddDays(-1), new(17, 0)) && timed.PlannedDate == date && timed.PlannedTime == new TimeOnly(1, 0));
        Check("floating legacy and date-only tasks retain dates", TodoTime.Display(state, legacy).Date == date && TodoTime.Display(state, plain).Date == date);
        Check("day query locates converted task on prior day only", PlannerQueries.TodosOn(state, date.AddDays(-1), date).Single().Id == timed.Id && !PlannerQueries.TodosOn(state, date, date).Any(t => t.Id == timed.Id));
        var child = Todo("child", date, 0) with { ParentId = legacy.Id, PlannedTime = timed.PlannedTime, PlannedTimeZoneId = timed.PlannedTimeZoneId };
        Check("converted child retains ancestor context and original identity", PlannerQueries.TodoTreeOn(state with { Todos = [legacy, child] }, date.AddDays(-1), date).Single() is { ContextOnly: true, Children.Length: 1 } tree && tree.Children[0].Value.Id == child.Id);
        var mixed = Todo("mixed", date, 0) with { PlannedTime = new(16, 0) };
        Check("time sorting compares converted times not source clocks", PlannerQueries.DisplayOrder(state, [timed, mixed], TodoSort.PlannedTime, date).Select(t => t.Id).SequenceEqual(new[] { mixed.Id, timed.Id }));
        Check("manual sorting still uses original keys", PlannerQueries.DisplayOrder(state, [timed, mixed], TodoSort.Manual, date).First().Id == mixed.Id);
        var carry = timed with { Kind = TodoKind.CarryHistory, CarryDate = date };
        Check("carry dates keep date workflow and convert time within each day", PlannerQueries.TodosOn(state with { Todos = [carry] }, date, date).Single().Id == carry.Id && TodoTime.Display(state, carry, date) == new TodoClock(date, new(17, 0)));
        var gap = timed with { PlannedDate = new(2026, 3, 8), PlannedTime = new(2, 30), PlannedTimeZoneId = "Eastern Standard Time" };
        Throws("new timed todo rejects nonexistent DST clock", () => TodoTime.Validate(gap, false), "InvalidLocalTime");
        var repeated = gap with { PlannedDate = new(2026, 11, 1), PlannedTime = new(1, 30) };
        Throws("new timed todo requires explicit repeated hour offset", () => TodoTime.Validate(repeated, false), "AmbiguousLocalTime");
        Check("two explicit DST offsets project to different times", TodoTime.Display(state, repeated with { PlannedOffset = TimeSpan.FromHours(-4) }).Time == new TimeOnly(5, 30) && TodoTime.Display(state, repeated with { PlannedOffset = TimeSpan.FromHours(-5) }).Time == new TimeOnly(6, 30));
        Check("unresolved recurrence DST is diagnostic rather than guessed", TodoTime.Display(state, gap).Issue == "InvalidLocalTime" && TodoTime.Display(state, repeated).Issue == "AmbiguousLocalTime");
        var path = TestFile(output, "timezone"); var w = await PlannerWorkspace.OpenAsync(new PlannerStore(path), state.Options);
        await Run(w, new SaveTodo(timed)); await Run(w, new SaveTodo(legacy));
        var originalBytes = File.ReadAllBytes(path);
        await w.ConfigureAsync(state.Options with { TimeZoneId = "Eastern Standard Time" });
        Check("display preference does not rewrite business JSON", originalBytes.SequenceEqual(File.ReadAllBytes(path)));
        await Run(w, new SetTodoCompleted(TodoRef.Of(timed), true)); await w.UndoAsync();
        Check("completion undo retains original zoned clock and sort key", w.Snapshot!.Todos.Single(t => t.Id == timed.Id).PlannedTimeZoneId == timed.PlannedTimeZoneId && w.Snapshot.Todos.Single(t => t.Id == timed.Id).SortKey == timed.SortKey);
        var reopened = await PlannerWorkspace.OpenAsync(new PlannerStore(path), state.Options);
        Check("restart restores zoned and floating identity", reopened.Snapshot!.Todos.Single(t => t.Id == timed.Id).PlannedTimeZoneId == timed.PlannedTimeZoneId && reopened.Snapshot.Todos.Single(t => t.Id == legacy.Id).PlannedTimeZoneId == null);
        var rule = new RecurrenceRule { Start = date, TimeZoneId = "China Standard Time" };
        var root = Todo("recurring zone", date) with { PlannedTime = new(1, 30) };
        var series = await Run(w, new CreateSeries(rule, [root]));
        await w.ConfigureAsync(state.Options);
        var occurred = PlannerQueries.TodosOn(w.Snapshot!, date.AddDays(-1), date).Single(t => t.Origin?.SeriesId == series);
        Check("recurrence queries neighboring source dates preserving occurrence identity", occurred.Origin!.Date == date && TodoTime.Display(w.Snapshot!, occurred).Time == new TimeOnly(17, 30));
        await Run(w, new SetTodoCompleted(TodoRef.Of(occurred), true));
        Check("materialized occurrence remains on converted display date", PlannerQueries.TodosOn(w.Snapshot!, date.AddDays(-1), date).Single(t => t.Id == occurred.Id).Status == TodoStatus.Completed);
        var copied = await Run(w, new CopyTodo(TodoRef.Of(occurred), date.AddDays(1)));
        Check("copy recurring task preserves source zone outside series", w.Snapshot!.Todos.Single(t => t.Id == copied).PlannedTimeZoneId == rule.TimeZoneId);
        // Produce an actual old-schema payload: remove new fields and recompute its wire hash.
        var old = JsonNode.Parse(PlannerCodec.Encode(state with { Todos = [legacy] }))!; old["schemaVersion"] = 3;
        foreach (var t in old["payload"]!["todos"]!.AsArray()) { t!.AsObject().Remove("plannedTimeZoneId"); t.AsObject().Remove("plannedOffset"); }
        old["payloadSha256"] = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(old["payload"])));
        var migrated = PlannerCodec.Decode(JsonSerializer.SerializeToUtf8Bytes(old), state.Options);
        Check("schema3 migration retains floating date and explicit manual progress", migrated.Migrated && migrated.State.Todos.Single().PlannedTimeZoneId == null && migrated.State.Todos.Single().ProgressMode == ProgressMode.Manual && migrated.State.Todos.Single().PlannedDate == date);
        Check("schema4 roundtrip keeps migration output intact", PlannerCodec.Decode(PlannerCodec.Encode(migrated.State), state.Options).State.Todos.Single() == migrated.State.Todos.Single());
        var cascadePath = TestFile(output, "timezone-legacy-cascade"); var cascade = await PlannerWorkspace.OpenAsync(new(cascadePath), state.Options);
        var p = await Run(cascade, new SaveTodo(Todo("legacy parent", date)));
        var a = await Run(cascade, new SaveTodo(Todo("legacy done child", date) with { ParentId = p }));
        var b = await Run(cascade, new SaveTodo(Todo("legacy pending child", date) with { ParentId = p }));
        await Run(cascade, new SetTodoCompleted(Ref(a), true)); await Run(cascade, new SetTodoCompleted(Ref(p), true));
        var oldCascade = JsonNode.Parse(PlannerCodec.Encode(cascade.Snapshot!))!; oldCascade["schemaVersion"] = 3;
        foreach (var t in oldCascade["payload"]!["todos"]!.AsArray()) { t!.AsObject().Remove("plannedTimeZoneId"); t.AsObject().Remove("plannedOffset"); }
        oldCascade["payloadSha256"] = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(oldCascade["payload"])));
        File.WriteAllBytes(cascadePath, JsonSerializer.SerializeToUtf8Bytes(oldCascade));
        cascade = await PlannerWorkspace.OpenAsync(new(cascadePath), state.Options);
        Check("schema3 migration retains completion snapshot and guard", cascade.Snapshot!.Todos.Single(t => t.Id == p).CompletionSnapshot is { } snap && snap.Guard == TodoCompletion.Guard(cascade.Snapshot, cascade.Snapshot.Todos.Single(t => t.Id == p)));
        await Run(cascade, new SetTodoCompleted(Ref(p), false));
        Check("migrated parent uncheck restores original completed and pending children", cascade.Snapshot!.Todos.Single(t => t.Id == a).Status == TodoStatus.Completed && cascade.Snapshot.Todos.Single(t => t.Id == b).Status == TodoStatus.Pending);
        Check("almanac bounded offline reference returns no out-of-range guesses", Almanac.On(Almanac.First.AddDays(-1)) == null && Almanac.On(Almanac.Last.AddDays(1)) == null && Almanac.On(Almanac.First) != null && Almanac.On(Almanac.Last) != null);
        Check("almanac leap day cross-year and 200-year monthly samples are valid", Enumerable.Range(1901, 200).All(y => Enumerable.Range(1, 12).All(m => Almanac.On(new(y, m, DateTime.DaysInMonth(y, m))) is { Suitable.Length: > 0, Avoid.Length: > 0 })));
        var first = Almanac.On(date)!; var again = Almanac.On(date)!;
        Check("almanac results are deterministic and do not mutate official calendar", first.Suitable.SequenceEqual(again.Suitable) && first.Avoid.SequenceEqual(again.Avoid) && new CalendarKnowledge().Data.Events.All(e => e.Kind != "Almanac"));
    }
}
