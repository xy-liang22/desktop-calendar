using DesktopCalendar.Core;

internal static partial class Checks
{
    static async Task CategoryOrderChecks(string output)
    {
        var path = TestFile(output, "category-order"); var w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now);
        var a = new Category { Name = "A", SortKey = 0 }; var b = new Category { Name = "B", SortKey = 1024 }; var c = new Category { Name = "C", SortKey = 2048 };
        await Run(w, new BatchCommand([new SaveCategory(a), new SaveCategory(b), new SaveCategory(c)]));
        a = w.Snapshot!.Categories.Single(x => x.Name == "A"); b = w.Snapshot.Categories.Single(x => x.Name == "B"); c = w.Snapshot.Categories.Single(x => x.Name == "C");
        var todo = Todo("category linked", new(2026, 11, 5)) with { CategoryId = a.Id }; await Run(w, new SaveTodo(todo));
        var activity = new Activity { Title = "category activity", CategoryId = b.Id, Intervals = [new(Guid.NewGuid(), Now, Now.AddHours(1))] }; await Run(w, new SaveActivity(activity));
        var original = PlannerQueries.OrderedCategories(w.Snapshot).Select(x => x.Id).ToArray();
        var reverse = original.Reverse().ToArray(); await Run(w, new ReorderCategories([.. reverse]));
        Check("category reorder persists exact visual order and retains entity IDs", PlannerQueries.OrderedCategories(w.Snapshot!).Select(x => x.Id).SequenceEqual(reverse));
        Check("category reorder leaves todo and activity associations intact", w.Snapshot!.Todos.Single().CategoryId == a.Id && w.Snapshot.Activities.Single().CategoryId == b.Id);
        await w.UndoAsync(); Check("category reorder undo restores previous order", PlannerQueries.OrderedCategories(w.Snapshot!).Select(x => x.Id).SequenceEqual(original));
        await w.RedoAsync(); Check("category reorder redo restores new order", PlannerQueries.OrderedCategories(w.Snapshot!).Select(x => x.Id).SequenceEqual(reverse));
        var reopened = await PlannerWorkspace.OpenAsync(new(path), Utc); Check("category order survives real atomic JSON reopen", PlannerQueries.OrderedCategories(reopened.Snapshot!).Select(x => x.Id).SequenceEqual(reverse));
        var before = File.ReadAllBytes(path);
        foreach (var bad in new[] { reverse[..^1], reverse.Concat([reverse[0]]).ToArray(), reverse.Select((id, i) => i == 0 ? Guid.NewGuid() : id).ToArray() })
        {
            var result = await w.ExecuteAsync(new ReorderCategories([.. bad]));
            Check("incomplete duplicate or unknown category order is rejected transactionally", !result.Success && before.SequenceEqual(File.ReadAllBytes(path)) && PlannerQueries.OrderedCategories(w.Snapshot!).Select(x => x.Id).SequenceEqual(reverse));
        }
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try { var result = await w.ExecuteAsync(new ReorderCategories([.. original])); Check("category order save failure preserves original memory and file", !result.Success && before.SequenceEqual(File.ReadAllBytes(path)) && PlannerQueries.OrderedCategories(w.Snapshot!).Select(x => x.Id).SequenceEqual(reverse)); }
        finally { File.SetAttributes(path, FileAttributes.Normal); }
        await Run(w, new ArchiveCategory(c.Id)); var archived = w.Snapshot!.Categories.Single(x => x.Id == c.Id);
        await Run(w, new ReorderCategories([a.Id, b.Id])); Check("archived category record is unchanged by active order", w.Snapshot!.Categories.Single(x => x.Id == c.Id) == archived && PlannerQueries.OrderedCategories(w.Snapshot).All(x => !x.Archived));
        Check("archived category cannot enter active reorder command", !(await w.ExecuteAsync(new ReorderCategories([a.Id, b.Id, c.Id]))).Success);
        var tie = await PlannerWorkspace.OpenAsync(new(TestFile(output, "category-legacy-ties")), Utc, () => Now);
        await Run(tie, new BatchCommand([new SaveCategory(new() { Name = "legacy A" }), new SaveCategory(new() { Name = "legacy B" }), new SaveCategory(new() { Name = "legacy C" })]));
        var ids = PlannerQueries.OrderedCategories(tie.Snapshot!).Select(x => x.Id).ToArray(); var middle = tie.Snapshot!.Categories.Single(x => x.Name == "legacy B");
        await Run(tie, new SaveCategory(middle with { Name = "renamed B" })); Check("editing legacy tied sort keys does not reorder categories", PlannerQueries.OrderedCategories(tie.Snapshot!).Select(x => x.Id).SequenceEqual(ids));
        await Run(tie, new InitializeCategories()); Check("adding missing defaults appends without reordering custom categories", PlannerQueries.OrderedCategories(tie.Snapshot!).Take(3).Select(x => x.Id).SequenceEqual(ids));
    }
}
