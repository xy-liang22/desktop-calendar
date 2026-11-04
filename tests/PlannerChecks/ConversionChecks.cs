using DesktopCalendar.Core;
using System.Text.Json;

internal static partial class Checks
{
    static async Task ConversionChecks(string output)
    {
        var day = new DateOnly(2026, 11, 2); string path = TestFile(output, "conversion"); var w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now);
        var root = Todo("convert root", day, 1024); var done = Todo("completed child", day) with { ParentId = root.Id }; var pending = Todo("pending child", day, 2048) with { ParentId = root.Id };
        await Run(w, new BatchCommand([new SaveTodo(root), new SaveTodo(done), new SaveTodo(pending), new SetTodoCompleted(new(done.Id), true), new SetTodoOrderMode(root.Id, null, TodoSort.Manual)]));
        var a = new Activity { Id = Guid.NewGuid(), Title = "linked", Todo = new(root.Id), Intervals = [new(Guid.NewGuid(), Now, Now.AddHours(1))] };
        var goal = new WeekGoal { Id = Guid.NewGuid(), Title = "goal", Week = PlannerTime.Week(day, DayOfWeek.Monday, "UTC"), Todos = [new(root.Id), new(pending.Id)], Source = GoalSource.Todo, Target = 2 };
        await Run(w, new SaveActivity(a)); await Run(w, new SaveGoal(goal));
        foreach (var kind in new[] { TodoKind.CarryHistory, TodoKind.CarryLatest, TodoKind.Once })
        {
            await Run(w, new ConvertTodo(new(root.Id), kind, day)); var t = PlannerQueries.Resolve(w.Snapshot!, new(root.Id));
            Check("simple type conversion retains stable ID links children sorting " + kind, t.Kind == kind && t.SortKey == 1024 && w.Snapshot!.Activities.Single().Todo!.Id == root.Id && PlannerQueries.Children(w.Snapshot, t).Length == 2 && w.Snapshot.Goals.Single().Todos.Any(r => r.Id == pending.Id));
        }
        await Run(w, new SetTodoCompleted(new(root.Id), true)); string completed = JsonSerializer.Serialize(w.Snapshot!.Todos.Single(t => t.Id == done.Id));
        await Run(w, new ConvertTodo(new(root.Id), TodoKind.CarryHistory, day));
        Check("completed once to carry freezes on original completion day", w.Snapshot!.Todos.Single(t => t.Id == root.Id).FrozenCarryDate == day && PlannerQueries.TodosOn(w.Snapshot, day.AddDays(5), day.AddDays(5)).All(t => t.Id != root.Id));
        await Run(w, new SetTodoCompleted(new(root.Id), false));
        Check("conversion retains parent mixed completion restore snapshot", w.Snapshot!.Todos.Single(t => t.Id == done.Id).Status == TodoStatus.Completed && w.Snapshot.Todos.Single(t => t.Id == pending.Id).Status == TodoStatus.Pending);
        completed = JsonSerializer.Serialize(w.Snapshot.Todos.Single(t => t.Id == done.Id));
        var occurrenceId = await Run(w, new ConvertTodo(new(root.Id), TodoKind.SeriesTemplate, day.AddDays(1), new() { Start = day.AddDays(1), Count = 3 }));
        var series = w.Snapshot!.Series.Single(); var occurrence = PlannerQueries.Resolve(w.Snapshot, TodoRef.For(new(series.Id, series.RootTodoId, day.AddDays(1))));
        Check("convert to recurring returns first occurrence and does not generate unbounded tasks", occurrenceId == occurrence.Id && w.Snapshot.Todos.Count(t => t.Origin != null) < 4 && PlannerQueries.TodosOn(w.Snapshot, day.AddDays(4), day).All(t => t.Origin?.SeriesId != series.Id));
        Check("recurring conversion cancels pending source and preserves exact completed child history", w.Snapshot.Todos.Single(t => t.Id == root.Id).Status == TodoStatus.Cancelled && JsonSerializer.Serialize(w.Snapshot.Todos.Single(t => t.Id == done.Id)) == completed);
        Check("pending activity and goal references migrate to new first occurrence", w.Snapshot.Activities.Single().Todo!.Id == occurrence.Id && w.Snapshot.Goals.Single().Todos.Any(r => r.Id == occurrence.Id) && PlannerQueries.Order(PlannerQueries.Children(w.Snapshot, occurrence), TodoSort.Manual).Last().SortKey == 2048);
        await Run(w, new SetTodoCompleted(TodoRef.Of(occurrence), true)); var history = w.Snapshot!.Todos.Where(t => t.Origin?.SeriesId == series.Id && t.Status == TodoStatus.Completed).Select(t => JsonSerializer.Serialize(t)).ToArray();
        var future = PlannerQueries.Resolve(w.Snapshot, TodoRef.For(new(series.Id, series.RootTodoId, day.AddDays(2))));
        var second = a with { Id = Guid.NewGuid(), Title = "future link", Todo = TodoRef.Of(future), Intervals = [new(Guid.NewGuid(), Now, Now.AddHours(1))] }; await Run(w, new SaveActivity(second));
        Guid newId = await Run(w, new ConvertTodo(TodoRef.Of(future), TodoKind.CarryLatest, day.AddDays(2)));
        Check("series to carry stops future unfinished occurrences and preserves completed history", w.Snapshot!.Series.Single().SuppressedFrom == day.AddDays(2) && history.All(h => w.Snapshot.Todos.Any(t => JsonSerializer.Serialize(t) == h)) && !PlannerQueries.TodosOn(w.Snapshot, day.AddDays(3), day.AddDays(3)).Any(t => t.Origin?.SeriesId == series.Id));
        Check("series replacement links and history links remain independent", w.Snapshot.Activities.Single(t => t.Id == second.Id).Todo!.Id == newId && w.Snapshot.Activities.Single(t => t.Id == a.Id).Todo!.Id == occurrence.Id && PlannerQueries.Resolve(w.Snapshot, new(newId)).Kind == TodoKind.CarryLatest);
        await w.UndoAsync(); Check("conversion undo restores future schedule and prior refs", w.Snapshot!.Series.Single().SuppressedFrom == null && w.Snapshot.Activities.Single(t => t.Id == second.Id).Todo == TodoRef.Of(future));
        await w.RedoAsync(); var loaded = await PlannerWorkspace.OpenAsync(new(path), Utc);
        Check("conversion survives atomic JSON reopen with integrity", loaded.Snapshot != null && loaded.Snapshot.Todos.Any(t => t.Id == newId) && loaded.Snapshot.Activities.Single(t => t.Id == second.Id).Todo!.Id == newId);
        var failed = await w.ExecuteAsync(new ConvertTodo(new(newId), TodoKind.SeriesTemplate, day, new() { Start = day.AddDays(1) }));
        Check("invalid conversion is transactional and leaves saved graph unchanged", !failed.Success && w.Snapshot!.Todos.Any(t => t.Id == newId));
    }
}
