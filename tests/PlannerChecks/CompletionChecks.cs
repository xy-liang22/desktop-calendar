using DesktopCalendar.Core;
using System.Text;
internal static partial class Checks
{
    static async Task CompletionChecks(string output)
    {
        var day = new DateOnly(2026, 11, 2); string path = TestFile(output, "completion"); var w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now);
        var parent = await Run(w, new SaveTodo(Todo("parent", day)));
        var a = await Run(w, new SaveTodo(Todo("a", day, 1) with { ParentId = parent }));
        var b = await Run(w, new SaveTodo(Todo("b", day, 2) with { ParentId = parent }));
        var c = await Run(w, new SaveTodo(Todo("c", day, 3) with { ParentId = parent }));
        Todo Node(Guid id) => w.Snapshot!.Todos.Single(t => t.Id == id);
        await Run(w, new SetTodoCompleted(Ref(a), true)); await Run(w, new SetTodoCompleted(Ref(b), true));
        Check("default parent progress counts direct child branches once", Math.Abs(PlannerQueries.Progress(w.Snapshot!, Ref(parent)) - 200m / 3) < .001m && Node(parent).Status == TodoStatus.Pending);
        await Run(w, new SetTodoCompleted(Ref(parent), true));
        Check("manual parent completion cascades and saves provenance", new[] { a, b, c }.All(id => Node(id).Status == TodoStatus.Completed) && Node(parent).CompletionIntent == CompletionIntent.ManualComplete && Node(parent).CompletionSnapshot?.Entries.Length == 3);
        w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now);
        await Run(w, new SetTodoCompleted(Ref(parent), false));
        Check("snapshot restores original child states across process-style reload", Node(a).Status == TodoStatus.Completed && Node(b).Status == TodoStatus.Completed && Node(c).Status == TodoStatus.Pending && Node(parent).Status == TodoStatus.Pending);
        await Run(w, new SetTodoCompleted(Ref(c), true));
        Check("natural completion has automatic source without cascade snapshot", Node(parent).Status == TodoStatus.Completed && Node(parent).CompletionIntent == CompletionIntent.Automatic && Node(parent).CompletionSnapshot == null);
        await Run(w, new SetTodoCompleted(Ref(parent), false));
        Check("unchecking natural parent leaves completed children and suppresses immediate recompletion", Node(parent).Status == TodoStatus.Pending && new[] { a, b, c }.All(id => Node(id).Status == TodoStatus.Completed) && Node(parent).CompletionIntent == CompletionIntent.ManualIncomplete);
        await Run(w, new SaveTodo(Node(a) with { Title = "metadata edit" }));
        Check("metadata edit does not defeat explicit incomplete override", Node(parent).Status == TodoStatus.Pending);
        w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now);
        Check("explicit incomplete override survives reload with 100 percent progress", Node(parent).Status == TodoStatus.Pending && PlannerQueries.Progress(w.Snapshot!, Ref(parent)) == 100);
        await Run(w, new SetTodoCompleted(Ref(a), false)); await Run(w, new SetTodoCompleted(Ref(a), true));
        Check("later independent child status change releases stale incomplete override", Node(parent).Status == TodoStatus.Completed);
        await Run(w, new SetTodoCompleted(Ref(c), false)); await Run(w, new SetTodoCompleted(Ref(parent), true));
        await Run(w, new SaveTodo(Node(c) with { Description = "keep this new text" })); await Run(w, new SetTodoCompleted(Ref(parent), false));
        Check("metadata survives valid snapshot restore", Node(c).Status == TodoStatus.Pending && Node(c).Description == "keep this new text");
        await Run(w, new SetTodoCompleted(Ref(parent), true)); await Run(w, new SetTodoCompleted(Ref(a), false));
        Check("independent child toggle invalidates manual snapshot and derives parent", Node(parent).CompletionSnapshot == null && Node(parent).CompletionSnapshotInvalidated && Node(parent).Status == TodoStatus.Pending);
        await Run(w, new SetTodoCompleted(Ref(parent), false));
        Check("invalid snapshot never overwrites new child choices", Node(a).Status == TodoStatus.Pending && Node(c).Status == TodoStatus.Completed);
        await Run(w, new SetTodoCompleted(Ref(parent), true));
        var added = await Run(w, new SaveTodo(Todo("new child", day, 4) with { ParentId = parent }));
        Check("structure edit invalidates snapshot without completing new child", Node(added).Status == TodoStatus.Pending && Node(parent).CompletionSnapshot == null && Node(parent).Status == TodoStatus.Pending);
        await Run(w, new SetTodoCompleted(Ref(parent), true)); await w.UndoAsync();
        Check("Undo reverses entire completion cascade atomically", Node(added).Status == TodoStatus.Pending && Node(parent).Status == TodoStatus.Pending); await w.RedoAsync();
        Check("Redo reinstates valid durable cascade snapshot", Node(added).Status == TodoStatus.Completed && Node(parent).CompletionSnapshot != null);
        await Run(w, new SetTodoCompleted(Ref(parent), false));
        var branch = await Run(w, new SaveTodo(Todo("branch", day) with { ParentId = added }));
        await Run(w, new SetTodoCompleted(Ref(branch), true));
        Check("recursive progress does not count descendants twice", Node(added).Status == TodoStatus.Completed && PlannerQueries.Progress(w.Snapshot!, Ref(added)) == 100);
        await Run(w, new SetTodoCompleted(Ref(added), true)); // preserve this nested snapshot inside a parent cascade
        await Run(w, new SetTodoCompleted(Ref(parent), true)); await Run(w, new SetTodoCompleted(Ref(parent), false)); await Run(w, new SetTodoCompleted(Ref(added), false));
        Check("nested snapshots restore without recursive update loops", Node(added).Status == TodoStatus.Pending && Node(branch).Status == TodoStatus.Completed);
        var stored = PlannerCodec.Encode(w.Snapshot!); var reloaded = PlannerCodec.Decode(stored, Utc).State;
        Check("current schema preserves source and invalidation flags", reloaded.Todos.Single(t => t.Id == added).CompletionIntent == CompletionIntent.ManualIncomplete && PlannerCodec.CurrentSchema == 4);
        var old = PlannerState.Empty("UTC") with { Todos = [Todo("legacy", day) with { Status = TodoStatus.Completed, CompletedAt = Now, CompletionIntent = CompletionIntent.ManualComplete }] };
        var legacy = PlannerCodec.Decode(PlannerCodec.EncodeLegacyV2ForMigration(old), Utc);
        Check("actual schema2 missing new completion fields migrates preserving identity/status", legacy.Migrated && legacy.State.Id == old.Id && legacy.State.Todos[0].Status == TodoStatus.Completed && legacy.State.Todos[0].CompletionIntent == CompletionIntent.ManualComplete && legacy.State.Todos[0].CompletionSnapshot == null);
        Check("priority categories are removed only from defaults", PlannerDefaults.Categories("zh-CN", Now).Select(x => x.Name).SequenceEqual(["工作", "家务", "个人", "家庭", "健康"]) && PlannerDefaults.LegacyPriority(new Category { BuiltInKey = "important", Name = "重要" }) && !PlannerDefaults.LegacyPriority(new Category { Name = "重要" }) && !PlannerDefaults.LegacyPriority(new Category { BuiltInKey = "important", Name = "renamed custom" }));
        string legacyPath = TestFile(output, "schema2-real-file"); var legacyBytes = PlannerCodec.EncodeLegacyV2ForMigration(old); File.WriteAllBytes(legacyPath, legacyBytes);
        var migrated = await PlannerWorkspace.OpenAsync(new(legacyPath), Utc, () => Now);
        Check("opening schema2 does not rewrite original file", migrated.Snapshot?.Id == old.Id && legacyBytes.SequenceEqual(File.ReadAllBytes(legacyPath)));
        await Run(migrated, new SaveTodo(Todo("new schema3 edit", day)));
        Check("first post-migration atomic save retains byte-exact schema2 backup", legacyBytes.SequenceEqual(File.ReadAllBytes(legacyPath + ".bak")) && !PlannerCodec.Decode(File.ReadAllBytes(legacyPath), Utc).Migrated);
        path = TestFile(output, "completion-edge"); w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now);
        parent = await Run(w, new SaveTodo(Todo("edge parent", day)));
        a = await Run(w, new SaveTodo(Todo("edge a", day, 10) with { ParentId = parent }));
        b = await Run(w, new SaveTodo(Todo("edge b", day, 20) with { ParentId = parent }));
        var keys = w.Snapshot!.Todos.ToDictionary(t => t.Id, t => t.SortKey);
        await Run(w, new SetTodoCompleted(Ref(parent), true));
        await Run(w, new SaveTodo(Node(a) with { ManualProgress = 30 }));
        Check("independent progress edit invalidates snapshot and retains changed progress", Node(parent).CompletionSnapshot == null && Node(parent).CompletionSnapshotInvalidated && Node(a).ManualProgress == 30);
        await Run(w, new SetTodoCompleted(Ref(parent), false));
        Check("snapshot invalidation never changes base sort keys", w.Snapshot!.Todos.All(t => keys[t.Id] == t.SortKey));
        await Run(w, new SetTodoCompleted(Ref(a), false)); await Run(w, new SetTodoCompleted(Ref(parent), true));
        var beforeWriteFailure = w.Snapshot; File.SetAttributes(path, FileAttributes.ReadOnly);
        try { var failure = await w.ExecuteAsync(new SetTodoCompleted(Ref(parent), false)); Check("failed snapshot restore transaction changes neither in-memory nor durable snapshot", !failure.Success && w.Snapshot == beforeWriteFailure && PlannerCodec.Decode(File.ReadAllBytes(path), Utc).State.Todos.Single(t => t.Id == parent).CompletionSnapshot != null); }
        finally { File.SetAttributes(path, FileAttributes.Normal); }
        await Run(w, new DeleteTodo(Ref(a), ChildDisposition.DeleteSubtree));
        Check("deleting a descendant invalidates snapshot without dangling references", Node(parent).CompletionSnapshot == null && Node(parent).CompletionSnapshotInvalidated && !w.Snapshot!.Todos.Any(t => t.Id == a));
        await w.UndoAsync(); await Run(w, new SetTodoCompleted(Ref(parent), false));
        Check("Undo of structural invalidation reinstates valid restoration", Node(a).Status == TodoStatus.Pending && Node(b).Status == TodoStatus.Completed);
        long rev = w.Snapshot!.Revision; var renamed = await w.ExecuteAsync(new RenameTodo(Ref(a), "metadata-only rename", Node(a).Title));
        Check("inline rename command changes only title with same ordering and links", renamed.Success && Node(a).Title == "metadata-only rename" && Node(a).ParentId == parent && Node(a).SortKey == keys[a]);
        var stale = await w.ExecuteAsync(new RenameTodo(Ref(a), "lost update", "old stale title"));
        Check("stale and empty inline titles are rejected transactionally", !stale.Success && !(await w.ExecuteAsync(new RenameTodo(Ref(a), " ", Node(a).Title))).Success && w.Snapshot!.Revision == rev + 1 && Node(a).Title == "metadata-only rename");
        await Run(w, new SetTodoCompleted(Ref(parent), true)); await Run(w, new DeleteTodo(Ref(parent), ChildDisposition.PromoteChildren));
        Check("promoting former cascaded children releases orphaned cascade source", w.Snapshot!.Todos.All(t => t.ParentId == null && t.Status == TodoStatus.Completed && t.CompletionIntent != CompletionIntent.CascadeComplete));
    }
}
