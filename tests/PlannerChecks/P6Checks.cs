using DesktopCalendar.Core;

internal static partial class Checks
{
    static async Task P6Checks(string output)
    {
        string path = TestFile(output, "p6"); var w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now); var day = new DateOnly(2026, 11, 2); var week = PlannerTime.Week(day, DayOfWeek.Monday, "UTC");
        var c1 = new Category { Id = Guid.NewGuid(), Name = "C1", Color = "#62A9E8" }; var c2 = c1 with { Id = Guid.NewGuid(), Name = "C2" }; await Run(w, new SaveCategory(c1)); await Run(w, new SaveCategory(c2));
        DateTimeOffset At(int d, int h) => new(2026, 11, 2 + d, h, 0, 0, TimeSpan.Zero);
        Activity A(string title, Guid category, DateTimeOffset from, DateTimeOffset to) => new() { Id = Guid.NewGuid(), Title = title, CategoryId = category, Intervals = [new(Guid.NewGuid(), from, to)] };
        var a = A("a", c1.Id, At(0, 8), At(0, 12)); var b = A("b", c1.Id, At(0, 10), At(0, 15)); var other = A("other category", c2.Id, At(0, 9), At(0, 14));
        foreach (var activity in new[] { a, b, other }) await Run(w, new SaveActivity(activity));
        var stats = PlannerQueries.CategoryStatistics(w.Snapshot!, week); Check("P6 same category overlap merges while different category contributes independently", stats.Single(t => t.CategoryId == c1.Id).Seconds == 7 * 3600 && stats.Single(t => t.CategoryId == c2.Id).Seconds == 5 * 3600);
        var goal = new WeekGoal { Id = Guid.NewGuid(), Week = week, Title = "actual hours", Kind = GoalKind.Duration, Source = GoalSource.Activity, Target = 8 * 3600, Activities = [a.Id, b.Id] }; await Run(w, new SaveGoal(goal));
        Check("goal duration uses union of associated periods", PlannerQueries.Goal(w.Snapshot!, goal).Automatic == 7 * 3600);
        await Run(w, new SaveGoal(goal with { ManualOverride = 9 * 3600 })); Check("manual progress overrides automatic without discarding source value", PlannerQueries.Goal(w.Snapshot!, w.Snapshot!.Goals.Single()).Complete && PlannerQueries.Goal(w.Snapshot!, w.Snapshot.Goals.Single()).Automatic == 7 * 3600);
        await Run(w, new SaveGoal(goal)); Check("clearing manual override restores automatic progress", PlannerQueries.Goal(w.Snapshot!, w.Snapshot!.Goals.Single()).Override == null && !PlannerQueries.Goal(w.Snapshot!, w.Snapshot.Goals.Single()).Complete);
        var changed = goal with { Kind = GoalKind.Count, Target = 2 }; var denied = await w.ExecuteAsync(new SaveGoal(changed)); Check("changing goal type requires explicit confirmation", !denied.Success); await Run(w, new SaveGoal(changed, true)); Check("confirmed goal type change retains existing activities", w.Snapshot!.Goals.Single().Activities.SequenceEqual(goal.Activities) && PlannerQueries.Goal(w.Snapshot, w.Snapshot.Goals.Single()).Automatic == 2);
        var review = new WeeklyReview { Id = Guid.NewGuid(), Week = week, Markdown = "human text", Sections = [new(Guid.NewGuid(), "reflection", "my own words")] }; await Run(w, new BatchCommand([new SaveReview(review), new SaveGoal(changed with { ReviewId = review.Id }), new AppendStatistics(review.Id, "generated one")]));
        await Run(w, new SaveReview(w.Snapshot!.Reviews.Single() with { Markdown = "updated human text", Statistics = [] }));
        Check("saving free prose cannot erase automatic snapshots or sections", w.Snapshot!.Reviews.Single().Statistics.Single().Markdown == "generated one" && w.Snapshot.Reviews.Single().Sections.Single().Markdown == "my own words");
        byte[] before = File.ReadAllBytes(path); long revision = w.Snapshot.Revision; await w.ConfigureAsync(Utc with { WeekStart = DayOfWeek.Sunday });
        Check("runtime week preference preserves data bytes revision and undo history", before.SequenceEqual(File.ReadAllBytes(path)) && w.Snapshot!.Revision == revision && w.CanUndo && w.Snapshot.Options.WeekStart == DayOfWeek.Sunday && w.Snapshot.Reviews.Single().Week == week);
        await w.UndoAsync(); Check("undo after runtime week change preserves current option and original week identities", w.Snapshot!.Options.WeekStart == DayOfWeek.Sunday && w.Snapshot.Reviews.Single().Week == week && w.Snapshot.Reviews.Single().Markdown == "human text"); await w.RedoAsync();
        bool invalid = false; try { await w.ConfigureAsync(Utc with { WeekStart = (DayOfWeek)99 }); } catch (PlannerException) { invalid = true; } Check("invalid runtime options are rejected without changing state", invalid && w.Snapshot!.Options.WeekStart == DayOfWeek.Sunday);
        var saved = PlannerCodec.Encode(w.Snapshot!); w = await PlannerWorkspace.OpenAsync(new(path), Utc with { WeekStart = DayOfWeek.Sunday }); Check("week goals prose sections snapshots and links reload intact", saved.SequenceEqual(PlannerCodec.Encode(w.Snapshot!)) && w.Snapshot!.Goals.Single().ReviewId == review.Id);
        var zone = "Eastern Standard Time"; var dstWeek = PlannerTime.Week(new(2026, 11, 1), DayOfWeek.Sunday, zone); Check("weekly percentage denominator accounts for DST fallback", PlannerTime.WeekRange(dstWeek).Duration.TotalHours == 169);
        var fullA = A("full1", c1.Id, PlannerTime.WeekRange(week).Start, PlannerTime.WeekRange(week).End); var fullB = A("full2", c2.Id, fullA.Intervals[0].Start, fullA.Intervals[0].End);
        var state = w.Snapshot! with { Activities = [fullA, fullB], Goals = [], Reviews = [] }; Check("independent category percentages may sum above one hundred", PlannerQueries.CategoryStatistics(state, week).Sum(t => t.FractionOfWeek) == 2);
    }
}
