using DesktopCalendar.Core;
using System.Collections.Immutable;

internal static partial class Checks
{
    static async Task<Guid> Run(PlannerWorkspace w, PlannerCommand command)
    {
        var result = await w.ExecuteAsync(command);
        if (!result.Success) throw new Exception($"{command.GetType().Name}: {result.Status}/{result.Diagnostic}");
        return result.EntityId ?? Guid.Empty;
    }
    static TodoRef Ref(Guid id) => new(id);
    static async Task ServiceChecks(string output)
    {
        var day = new DateOnly(2026, 11, 2); var clock = Now;
        string servicePath = TestFile(output, "service");
        var w = await PlannerWorkspace.OpenAsync(new(servicePath), Utc, () => clock);
        var ids = new List<Guid>();
        for (int i = 0; i < 4; i++) ids.Add(await Run(w, new SaveTodo(Todo(((char)('A' + i)).ToString(), day, i))));
        await Run(w, new SetTodoCompleted(Ref(ids[3]), true)); await Run(w, new SetTodoCompleted(Ref(ids[1]), true));
        Check("service D then B completion displays ACBD", string.Concat(PlannerQueries.TodosOn(w.Snapshot!, day, day).Select(t => t.Title)) == "ACBD");
        await Run(w, new SetTodoCompleted(Ref(ids[1]), false));
        Check("uncomplete restores original relative position", string.Concat(PlannerQueries.TodosOn(w.Snapshot!, day, day).Select(t => t.Title)) == "ABCD");
        await Run(w, new ReorderTodos([Ref(ids[2]), Ref(ids[0]), Ref(ids[3]), Ref(ids[1])]));
        await Run(w, new SetTodoCompleted(Ref(ids[1]), true));
        Check("manual mixed sorting survives completion grouping", string.Concat(PlannerQueries.TodosOn(w.Snapshot!, day, day).Select(t => t.Title)) == "CADB");
        var timed = w.Snapshot!.Todos.Single(t => t.Id == ids[2]) with { PlannedTime = new(9, 30) };
        await Run(w, new SaveTodo(timed));
        Check("time sort cannot put completed before incomplete", PlannerQueries.Order(w.Snapshot!.Todos, TodoSort.PlannedTime).Take(2).All(t => t.Status == TodoStatus.Pending));
        var parent = await Run(w, new SaveTodo(Todo("parent", day) with { ProgressMode = ProgressMode.Children }));
        var child1 = await Run(w, new SaveTodo(Todo("child1", day, 1) with { ParentId = parent }));
        var child2 = await Run(w, new SaveTodo(Todo("child2", day, 2) with { ParentId = parent, ManualProgress = 40 }));
        await Run(w, new SetTodoCompleted(Ref(child1), true));
        Check("derived child progress independent parent completion", PlannerQueries.Progress(w.Snapshot!, Ref(parent)) == 70 && w.Snapshot!.Todos.Single(t => t.Id == parent).Status == TodoStatus.Pending);
        Check("child siblings incomplete first", PlannerQueries.Children(w.Snapshot!, w.Snapshot!.Todos.Single(t => t.Id == parent)).Select(t => t.Id).SequenceEqual([child2, child1]));
        await Run(w, new SetTodoCompleted(Ref(child1), false));
        Check("child original order restored", PlannerQueries.Children(w.Snapshot!, w.Snapshot!.Todos.Single(t => t.Id == parent))[0].Id == child1);
        long revision = w.Snapshot!.Revision;
        var cycle = await w.ExecuteAsync(new SaveTodo(w.Snapshot.Todos.Single(t => t.Id == parent) with { ParentId = child1 }));
        Check("failed command is atomic in memory and disk", !cycle.Success && cycle.Diagnostic == "ParentCycle" && w.Snapshot!.Revision == revision);
        var carry = await Run(w, new SaveTodo(Todo("carry", day) with { Kind = TodoKind.CarryHistory }));
        clock = Now.AddDays(2); await Run(w, new SetTodoCompleted(Ref(carry), true));
        Check("carry completion freezes shared crossed-out history", Enumerable.Range(0, 3).All(i => PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(i), day.AddDays(10)).Single(t => t.Id == carry).Status == TodoStatus.Completed) && !PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(3), day.AddDays(10)).Any(t => t.Id == carry));
        await Run(w, new SetTodoCompleted(Ref(carry), false));
        await Run(w, new MoveCarryTodo(Ref(carry), day.AddDays(4)));
        Check("carry roll-forward records stable shared identity", PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(4), day).Any(t => t.Id == carry) && w.Snapshot!.Todos.Single(t => t.Id == carry).Moves.Length == 1);
        var category = await Run(w, new SaveCategory(new() { Name = "work" }));
        var asset = await Run(w, new SaveAsset(new() { RelativePath = "assets/images/test.png", MediaType = "image/png", Length = 42, Sha256 = new('a', 64) }));
        var activity = await Run(w, new SaveActivity(new() { Title = "actual", CategoryId = category, Todo = Ref(ids[0]), Intervals = [new(Guid.Empty, Now.AddHours(11), Now.AddHours(14)), new(Guid.Empty, Now.AddHours(12), Now.AddHours(15))], Notes = new() { Format = NoteFormat.Markdown, Text = "note ![](asset:test)", AssetIds = [asset] } }));
        Check("actual work never completes linked planned todo", w.Snapshot!.Todos.Single(t => t.Id == ids[0]).Status == TodoStatus.Pending);
        Check("multiple periods have stable unique IDs", w.Snapshot!.Activities.Single().Intervals.Select(i => i.Id).Distinct().Count() == 2);
        Check("cross-day query merges same-activity overlap", PlannerQueries.ActivitiesOn(w.Snapshot!, day).Single().Interval.Duration.TotalHours == 1 && PlannerQueries.ActivitiesOn(w.Snapshot!, day.AddDays(1)).Single().Interval.Duration.TotalHours == 3);
        var week = PlannerTime.Week(day, DayOfWeek.Monday, "UTC");
        var review = await Run(w, new SaveReview(new() { Week = week, Markdown = "user prose", Sections = [new(Guid.Empty, "section", "body")] }));
        await Run(w, new AppendStatistics(review, "generated"));
        var goal = await Run(w, new SaveGoal(new() { Title = "hours", Week = week, Kind = GoalKind.Duration, Source = GoalSource.Activity, Target = 14400, Activities = [activity], ReviewId = review }));
        Check("duration goal actual interval union", PlannerQueries.Goal(w.Snapshot!, w.Snapshot!.Goals.Single()).Automatic == 14400);
        await Run(w, new SaveGoal(w.Snapshot!.Goals.Single() with { ManualOverride = 18000 }));
        var progress = PlannerQueries.Goal(w.Snapshot!, w.Snapshot!.Goals.Single());
        Check("manual progress override leaves automatic value", progress.Automatic == 14400 && progress.Effective == 18000);
        var badGoal = await w.ExecuteAsync(new SaveGoal(w.Snapshot!.Goals.Single() with { Source = GoalSource.Todo }, true));
        Check("Todo cannot supply actual duration", badGoal.Diagnostic == "TodoIsNotActualDuration");
        await Run(w, new SaveReview(w.Snapshot!.Reviews.Single() with { Markdown = "edited", Statistics = [] }));
        Check("saving handwritten review cannot erase generated summary", w.Snapshot!.Reviews.Single().Markdown == "edited" && w.Snapshot.Reviews.Single().Statistics.Single().Markdown == "generated");
        Check("used image reference cannot be removed", (await w.ExecuteAsync(new RemoveAsset(asset))).Diagnostic == "AssetInUse");
        Check("image traversal rejected", !(await w.ExecuteAsync(new SaveAsset(new() { RelativePath = "assets/images/../private.png", MediaType = "image/png", Length = 1, Sha256 = new('b', 64) }))).Success);
        await Run(w, new DeleteTodo(Ref(ids[0]), ChildDisposition.DeleteSubtree));
        Check("deleting todo preserves actual activity and historic title", w.Snapshot!.Activities.Single().Todo == null && w.Snapshot.Activities.Single().FormerTodoTitle == "A");
        var undo = await w.UndoAsync();
        Check("durable undo restores links without revision rollback", undo.Success && w.Snapshot!.Todos.Any(t => t.Id == ids[0]) && w.Snapshot.Activities.Single().Todo!.Id == ids[0] && w.Snapshot.Revision > revision);
        Check("durable redo reapplies deletion", (await w.RedoAsync()).Success && !w.Snapshot!.Todos.Any(t => t.Id == ids[0]));
        await Run(w, new ArchiveCategory(category));
        Check("archive preserves category on historic activities", w.Snapshot!.Categories.Single().Archived && w.Snapshot.Activities.Single().CategoryId == category);
        await Run(w, new DeleteActivity(activity));
        Check("activity deletion clears goal links without deleting todo", w.Snapshot!.Goals.Single().Activities.IsEmpty && w.Snapshot.Todos.Any(t => t.Id == ids[1]));
        await Run(w, new DeleteReview(review));
        Check("review deletion clears goal link", w.Snapshot!.Goals.Single().ReviewId == null);
        await SeriesChecks(w, day);
        // Queue concurrent callers: no latest-write-wins loss or asynchronous reordering.
        int prior = w.Snapshot!.Todos.Length;
        var writes = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => w.ExecuteAsync(new SaveTodo(Todo("parallel" + i, day.AddMonths(1), i)))));
        Check("workspace serializes 12 concurrent edits without lost data", writes.All(r => r.Success) && w.Snapshot!.Todos.Length == prior + 12);
        // Persist a nonempty linked graph after exercising deletion cleanup; restarting an
        // empty navigation list would not prove that Activity/goal/review/asset edges survive.
        var durableReview = await Run(w, new SaveReview(new() { Week = week, Markdown = "persisted user prose" }));
        var durableActivity = await Run(w, new SaveActivity(new() { Title = "persisted actual", Todo = Ref(ids[1]), CategoryId = category,
            Intervals = [new(Guid.Empty, Now, Now.AddMinutes(30))], Notes = new() { Format = NoteFormat.Markdown, Text = "![local](" + PlannerQueries.AssetReference(asset) + ")", AssetIds = [asset] } }));
        await Run(w, new SaveGoal(w.Snapshot!.Goals.Single() with { Todos = [Ref(ids[1])], Activities = [durableActivity], ReviewId = durableReview }));
        var reopen = await new PlannerStore(servicePath).OpenAsync(Utc);
        Check("full service state survives restart with associations", reopen.Status == StoreStatus.Ready && reopen.State!.Revision == w.Snapshot!.Revision && PlannerCodec.Encode(reopen.State).SequenceEqual(PlannerCodec.Encode(w.Snapshot)));
        Check("independent process verifies complete service graph identity/revision/content", await Child("--verify", servicePath, w.Snapshot!.Id.ToString(), w.Snapshot.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture), Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(PlannerCodec.Encode(w.Snapshot)))) == 0);
        await w.ReloadAsync(); Check("reload clears obsolete session undo", !w.CanUndo && !w.CanRedo);
    }
    static async Task SeriesChecks(PlannerWorkspace w, DateOnly day)
    {
        var root = Todo("daily", day); var child = Todo("recurring child", day) with { ParentId = root.Id };
        var series = await Run(w, new CreateSeries(new() { Start = day, Frequency = Frequency.Daily }, [root, child]));
        TodoRef Occ(Guid node, int offset) => TodoRef.For(new(series, node, day.AddDays(offset)));
        var first = Occ(root.Id, 0); var second = Occ(root.Id, 1); var future = Occ(root.Id, 2);
        await Run(w, new SetTodoCompleted(first, true));
        Check("recurrence completion independent per date", PlannerQueries.Resolve(w.Snapshot!, first).Status == TodoStatus.Completed && PlannerQueries.Resolve(w.Snapshot!, second).Status == TodoStatus.Pending);
        await Run(w, new SetTodoCompleted(Occ(child.Id, 1), true));
        Check("materializing subtask also materializes parent", w.Snapshot!.Todos.Any(t => t.Id == second.Id));
        await Run(w, new EditSeries(series, day, EditScope.EntireSeries, Templates: [root with { Title = "edited series" }, child]));
        Check("series edit protects completed history", PlannerQueries.Resolve(w.Snapshot!, first).Title == "daily" && PlannerQueries.Resolve(w.Snapshot!, future).Title == "edited series");
        await Run(w, new EditSeries(series, day.AddDays(2), EditScope.Single, Templates: [root with { Title = "single" }, child], MoveTo: day.AddDays(4)));
        Check("single reschedule retains original identity and no duplicate", PlannerQueries.Resolve(w.Snapshot!, future).PlannedDate == day.AddDays(4) && !PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(2), day).Any(t => t.Id == future.Id) && PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(4), day).Any(t => t.Id == future.Id));
        await Run(w, new EditSeries(series, day.AddDays(3), EditScope.FromDate, Templates: [root with { Title = "future" }, child]));
        Check("future version preserves naturally completed prior history", PlannerQueries.Resolve(w.Snapshot!, Occ(root.Id, 1)).Title == "daily" && PlannerQueries.Resolve(w.Snapshot!, Occ(root.Id, 1)).Status == TodoStatus.Completed && PlannerQueries.Resolve(w.Snapshot!, Occ(root.Id, 3)).Title == "future");
        await Run(w, new CancelSeries(series, day.AddDays(3), EditScope.Single));
        Throws("single recurrence cancellation excludes only one date", () => PlannerQueries.Resolve(w.Snapshot!, Occ(root.Id, 3)), "OccurrenceNotFound");
        Check("single cancellation leaves next occurrence", PlannerQueries.Resolve(w.Snapshot!, Occ(root.Id, 4)).Title == "future");
        await Run(w, new CancelSeries(series, day.AddDays(2), EditScope.FutureUnfinished));
        Throws("future cancellation stops ungenerated instances", () => PlannerQueries.Resolve(w.Snapshot!, Occ(root.Id, 6)), "OccurrenceNotFound");
        Check("future cancellation protects completed prior child", PlannerQueries.Resolve(w.Snapshot!, Occ(child.Id, 1)).Status == TodoStatus.Completed);
        await Run(w, new CancelSeries(series, day, EditScope.EntireSeries));
        Check("series deletion protects completed snapshots and parent chain", PlannerQueries.Resolve(w.Snapshot!, first).Status == TodoStatus.Completed && PlannerQueries.Resolve(w.Snapshot!, Occ(child.Id, 1)).Status == TodoStatus.Completed);
    }
}
