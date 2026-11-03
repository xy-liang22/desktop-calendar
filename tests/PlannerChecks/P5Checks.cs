using DesktopCalendar.Core;

internal static partial class Checks
{
    static async Task P5Checks(string output)
    {
        string path = TestFile(output, "p5"); var w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now); var day = new DateOnly(2026, 11, 3);
        DateTimeOffset At(int offset, int hour) => new(2026, 11, 3 + offset, hour, 0, 0, TimeSpan.Zero);
        var todo = Todo("unfinished associated", day); await Run(w, new SaveTodo(todo));
        var a = new Activity { Id = Guid.NewGuid(), Title = "sleep and work", Emoji = "😴", Summary = "two periods", Reaction = "😊", Todo = TodoRef.Of(todo), Notes = new() { Format = NoteFormat.Markdown, Text = "# created with notes" },
            Intervals = [new(Guid.NewGuid(), At(0, 23), At(1, 7)), new(Guid.NewGuid(), At(1, 14), At(1, 16))] };
        await Run(w, new SaveActivity(a)); var first = ActivityTimeline.On(w.Snapshot!, day, "UTC"); var next = ActivityTimeline.On(w.Snapshot!, day.AddDays(1), "UTC");
        Check("activity display clips a cross-midnight period without duplicating identity", first.Length == 1 && first[0].StartHour == 23 && first[0].EndHour == 24 && next.Length == 2 && next[0].PeriodId == first[0].PeriodId && next[0].StartHour == 0 && next[0].EndHour == 7);
        Check("activity creation with notes emoji reaction leaves associated todo pending", w.Snapshot!.Todos.Single(t => t.Id == todo.Id).Status == TodoStatus.Pending && w.Snapshot.Activities.Single().Notes.Text == "# created with notes" && w.Snapshot.Activities.Single().Reaction == "😊");
        var period = a.Intervals[0]; var moved = ActivityTimeline.Transform(period, TimeSpan.FromHours(2), "UTC", "move");
        Check("move preserves period ID and elapsed duration across midnight", moved.Id == period.Id && moved.Duration == period.Duration && moved.Start == At(1, 1));
        var shortened = ActivityTimeline.Transform(period, TimeSpan.FromHours(-2), "UTC", "end"); Check("resize changes only selected endpoint", shortened.Start == period.Start && shortened.End == At(1, 5));
        bool invalid = false; try { _ = ActivityTimeline.Transform(period, TimeSpan.FromHours(9), "UTC", "start"); } catch (PlannerException) { invalid = true; } Check("inverted resize is rejected without storage writes", invalid);
        string zone = "Eastern Standard Time"; var start = new DateTimeOffset(2026, 11, 1, 0, 30, 0, TimeSpan.FromHours(-4)); var end = new DateTimeOffset(2026, 11, 1, 2, 30, 0, TimeSpan.FromHours(-5));
        var fall = new Activity { Id = Guid.NewGuid(), Title = "repeated hour", TimeZoneId = zone, Intervals = [new(Guid.NewGuid(), start, end)] };
        var state = PlannerState.Empty(zone) with { Activities = [fall] }; var split = ActivityTimeline.On(state, new(2026, 11, 1), zone);
        Check("DST repeated hour splits display by offsets and preserves actual total duration", split.Length == 2 && split[0].Offset != split[1].Offset && split.Sum(s => (s.End - s.Start).TotalHours) == 3 && split.All(s => s.EndHour > s.StartHour));
        var reverseClock = fall with { Intervals = [new(Guid.NewGuid(), new(2026, 11, 1, 1, 50, 0, TimeSpan.FromHours(-4)), new(2026, 11, 1, 1, 10, 0, TimeSpan.FromHours(-5)))] };
        split = ActivityTimeline.On(state with { Activities = [reverseClock] }, new(2026, 11, 1), zone);
        Check("valid elapsed interval with locally reversed clock is still displayed as two fragments", split.Length == 2 && Math.Abs(split.Sum(s => (s.End - s.Start).TotalMinutes) - 20) < .001);
        var spring = fall with { Intervals = [new(Guid.NewGuid(), new(2026, 3, 8, 1, 30, 0, TimeSpan.FromHours(-5)), new(2026, 3, 8, 3, 30, 0, TimeSpan.FromHours(-4)))] };
        split = ActivityTimeline.On(state with { Activities = [spring] }, new(2026, 3, 8), zone);
        Check("DST missing hour leaves a wall-clock gap and counts only real elapsed time", split.Length == 2 && split.Sum(s => (s.End - s.Start).TotalHours) == 1 && split[0].EndHour == 2 && split[1].StartHour == 3);
        invalid = false; try { _ = ActivityTimeline.Local(new(2026, 3, 8, 2, 30, 0), zone); } catch (PlannerException) { invalid = true; } Check("gesture cannot silently create an invalid DST local time", invalid);
        invalid = false; try { _ = ActivityTimeline.Local(new(2026, 11, 1, 1, 30, 0), zone); } catch (PlannerException) { invalid = true; } Check("ambiguous gesture needs an explicit offset rather than silently choosing", invalid);
        var goal = new WeekGoal { Id = Guid.NewGuid(), Title = "existing goal", Week = PlannerTime.Week(day, DayOfWeek.Sunday, "UTC"), Source = GoalSource.Activity, Kind = GoalKind.Duration, Target = 3600, Activities = [a.Id] }; await Run(w, new SaveGoal(goal));
        var before = PlannerCodec.Encode(w.Snapshot!); await Run(w, new SaveActivity(a with { Intervals = [moved, a.Intervals[1]] }, false)); await w.UndoAsync();
        Check("activity move undo restores original intervals and goal association", w.Snapshot!.Activities.Single().Intervals[0] == period && w.Snapshot.Goals.Single().Activities.Contains(a.Id));
        await w.RedoAsync(); await Run(w, new DeleteActivity(a.Id)); Check("deleting activity detaches goals but retains unfinished todo", w.Snapshot!.Goals.Single().Activities.Length == 0 && w.Snapshot.Todos.Single().Status == TodoStatus.Pending); await w.UndoAsync();
        w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now); Check("P5 activity periods notes and links survive storage reload", w.Snapshot!.Activities.Single().Intervals[0] == moved && w.Snapshot.Activities.Single().Emoji == "😴" && w.Snapshot.Goals.Single().Activities.Contains(a.Id));
    }
}
