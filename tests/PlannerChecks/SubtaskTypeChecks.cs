using DesktopCalendar.Core;

internal static partial class Checks
{
    static async Task SubtaskTypeChecks(string output)
    {
        var date = new DateOnly(2026, 11, 6); var path = TestFile(output, "subtask-types");
        var w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now);
        foreach (var parentKind in new[] { TodoKind.Once, TodoKind.CarryHistory, TodoKind.CarryLatest })
        {
            var p = await Run(w, new SaveTodo(Todo("parent " + parentKind, date) with { Kind = parentKind }));
            foreach (var kind in new[] { TodoKind.Once, TodoKind.CarryHistory, TodoKind.CarryLatest })
            {
                var id = await Run(w, new AddChildTodo(Todo("child " + kind, date) with { Kind = kind }, Ref(p)));
                var child = w.Snapshot!.Todos.Single(t => t.Id == id);
                Check("mixed child keeps type and parent " + parentKind + "/" + kind, child.Kind == kind && child.ParentId == p && (kind == TodoKind.Once || child.CarryDate == date));
            }
        }
        var template = Todo("periodic parent", date); var series = await Run(w, new CreateSeries(new() { Start = date, Interval = 2, Count = 5 }, [template]));
        TodoRef Parent(int day) => TodoRef.For(new(series, template.Id, date.AddDays(day)));
        var ordinary = await Run(w, new AddChildTodo(Todo("local ordinary", date), Parent(0)));
        var carry = await Run(w, new AddChildTodo(Todo("local carry", date) with { Kind = TodoKind.CarryHistory }, Parent(0)));
        Check("local child materializes previously virtual parent and keeps ordinary identity", w.Snapshot!.Todos.Any(t => t.Id == Parent(0).Id) && w.Snapshot.Todos.Single(t => t.Id == ordinary) is { Origin: null, Kind: TodoKind.Once } localChild && localChild.ParentId == Parent(0).Id);
        Check("local carry extends without cloning into later parent instances", PlannerQueries.TodosOn(w.Snapshot!, date.AddDays(2), date.AddDays(2)).Any(t => t.Id == carry) && !PlannerQueries.Children(w.Snapshot!, PlannerQueries.Resolve(w.Snapshot!, Parent(2))).Any(t => t.Id == carry));
        await Run(w, new SetTodoCompleted(Ref(ordinary), true)); await Run(w, new SetTodoCompleted(Parent(0), true));
        w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now);
        await Run(w, new SetTodoCompleted(Parent(0), false));
        Check("mixed children restore completion snapshot across reload", w.Snapshot!.Todos.Single(t => t.Id == ordinary).Status == TodoStatus.Completed && w.Snapshot.Todos.Single(t => t.Id == carry).Status == TodoStatus.Pending);
        await w.UndoAsync(); await w.RedoAsync();
        Check("mixed type undo redo preserves parent and carry fields", w.Snapshot!.Todos.Single(t => t.Id == carry).ParentId == Parent(0).Id && w.Snapshot.Todos.Single(t => t.Id == carry).CarryDate == date);
        var follower = Todo("following child", date.AddDays(2)); await Run(w, new AddSeriesTodo(follower, Parent(2), EditScope.FromDate));
        Check("following child uses shared interval and stable per-date identity", !PlannerQueries.Children(w.Snapshot!, PlannerQueries.Resolve(w.Snapshot!, Parent(0))).Any(t => t.Title == follower.Title) && PlannerQueries.Children(w.Snapshot!, PlannerQueries.Resolve(w.Snapshot!, Parent(2))).Any(t => t.Origin?.NodeId == follower.Id) && PlannerQueries.Children(w.Snapshot!, PlannerQueries.Resolve(w.Snapshot!, Parent(4))).Any(t => t.Origin?.NodeId == follower.Id));
        var before = File.ReadAllBytes(path);
        var reject = await w.ExecuteAsync(new AddSeriesTodo(Todo("invalid carry template", date) with { Kind = TodoKind.CarryLatest }, Parent(2), EditScope.FromDate));
        Check("recurring command cannot silently discard selected carry type", !reject.Success && before.SequenceEqual(File.ReadAllBytes(path)));
        reject = await w.ExecuteAsync(new AddChildTodo(Todo("wrong parent", date) with { ParentId = Guid.NewGuid() }, Parent(0)));
        Check("mismatched child parent rejects atomically", !reject.Success && before.SequenceEqual(File.ReadAllBytes(path)));
        await Run(w, new CancelSeries(series, date, EditScope.Single));
        Check("cancel parent occurrence preserves completed local child and removes unfinished local carry", w.Snapshot!.Todos.Any(t => t.Id == ordinary && t.Status == TodoStatus.Completed) && !w.Snapshot.Todos.Any(t => t.Id == carry));
        reject = await w.ExecuteAsync(new AddChildTodo(Todo("cancelled parent child", date), Parent(0)));
        Check("new local child cannot attach to cancelled parent", !reject.Success);
        await w.UndoAsync();
        Check("undo cancel restores local carry and saved relationship", w.Snapshot!.Todos.Any(t => t.Id == carry && t.ParentId == Parent(0).Id));
        var roundtrip = PlannerCodec.Decode(PlannerCodec.Encode(w.Snapshot!), Utc).State;
        Check("mixed child types roundtrip existing schema without additional store", PlannerCodec.CurrentSchema == 4 && roundtrip.Todos.Single(t => t.Id == carry).Kind == TodoKind.CarryHistory);
    }
}
