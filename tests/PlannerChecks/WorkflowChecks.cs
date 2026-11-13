using DesktopCalendar.Core;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

internal static partial class Checks
{
    static async Task WorkflowChecks(string output)
    {
        var day = new DateOnly(2026, 11, 2); var path = TestFile(output, "workflow");
        var w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now);
        var a = await Run(w, new SaveTodo(Todo("A", day, 0))); var b = await Run(w, new SaveTodo(Todo("B", day, 1024)));
        var c = await Run(w, new AddChildTodo(Todo("child", day), Ref(a))); var g = await Run(w, new AddChildTodo(Todo("grandchild", day), Ref(c)));
        await Run(w, new SaveActivity(new() { Title = "linked", TimeZoneId = "UTC", Todo = Ref(a), Intervals = [new(Guid.NewGuid(), Now, Now.AddHours(1))] }));
        await Run(w, new ReparentTodo(Ref(a), Ref(b)));
        Check("reparent retains entire subtree and activity association", w.Snapshot!.Todos.Single(t => t.Id == a).ParentId == b && w.Snapshot.Todos.Single(t => t.Id == c).ParentId == a && w.Snapshot.Todos.Single(t => t.Id == g).ParentId == c && w.Snapshot.Activities.Single().Todo == Ref(a));
        var bytes = File.ReadAllBytes(path); var bad = await w.ExecuteAsync(new ReparentTodo(Ref(b), Ref(g)));
        Check("cycle reparent rejected without file mutation", !bad.Success && bad.Diagnostic == "ParentCycle" && bytes.SequenceEqual(File.ReadAllBytes(path)));
        await w.UndoAsync(); Check("one undo restores original hierarchy", w.Snapshot!.Todos.Single(t => t.Id == a).ParentId == null); await w.RedoAsync();
        await Run(w, new ReparentTodo(Ref(c), null)); Check("promote child to root keeps grandchildren", w.Snapshot!.Todos.Single(t => t.Id == c).ParentId == null && w.Snapshot.Todos.Single(t => t.Id == g).ParentId == c);
        await Run(w, new SetTodoCompleted(Ref(b), true)); await Run(w, new ReparentTodo(Ref(a), Ref(c)));
        Check("structural move invalidates previous completion snapshot", w.Snapshot!.Todos.Single(t => t.Id == b).CompletionSnapshot == null);
        await Run(w, new BulkEditTodos([Ref(a), Ref(c)], day.AddDays(3), TodoKind.CarryHistory, day));
        Check("bulk date and type affects selected only", w.Snapshot!.Todos.Where(t => t.Id == a || t.Id == c).All(t => t.PlannedDate == day.AddDays(3) && t.Kind == TodoKind.CarryHistory) && w.Snapshot.Todos.Single(t => t.Id == g).PlannedDate == day);
        await w.UndoAsync(); Check("bulk edit uses one undo frame", w.Snapshot!.Todos.Single(t => t.Id == a).Kind == TodoKind.Once && w.Snapshot.Todos.Single(t => t.Id == c).Kind == TodoKind.Once);
        bytes = File.ReadAllBytes(path); bad = await w.ExecuteAsync(new BulkEditTodos([Ref(c), Ref(Guid.NewGuid())], day.AddDays(4), null, day));
        Check("failed batch saves none", !bad.Success && bytes.SequenceEqual(File.ReadAllBytes(path)));
        var task = w.Snapshot!.Todos.Single(t => t.Id == g) with { Deadline = new(null, new(18, 0), "UTC") }; await Run(w, new SaveTodo(task));
        Check("time-only deadline follows planned day", TodoDeadlines.Instant(task) == new DateTimeOffset(2026, 11, 2, 18, 0, 0, TimeSpan.Zero));
        Check("deadline compact formats and zone date rollover", TodoDeadlines.Label(task, "UTC") == "ddl: 18:00" && TodoDeadlines.Label(task, "China Standard Time") == "ddl: 11.03 02:00");
        var dateOnly = task with { Deadline = new(day, null, "UTC") }; Check("date deadline ends at exclusive next boundary", TodoDeadlines.Instant(dateOnly) == new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero) && TodoDeadlines.Label(dateOnly, "UTC") == "ddl: 11.02");
        Check("deadline urgency boundaries do not change base order", TodoDeadlines.Urgency(task, Now) == DeadlineUrgency.Today && TodoDeadlines.Urgency(task, Now.AddDays(1)) == DeadlineUrgency.Overdue && TodoDeadlines.Urgency(task, Now.AddDays(-2)) == DeadlineUrgency.Soon && task.SortKey == w.Snapshot.Todos.Single(t => t.Id == g).SortKey);
        Throws("deadline DST gap rejected", () => TodoDeadlines.Validate(task with { Deadline = new(new(2026, 3, 8), new(2, 30), "Eastern Standard Time") }), "InvalidLocalTime");
        Throws("deadline ambiguous DST needs offset", () => TodoDeadlines.Validate(task with { Deadline = new(new(2026, 11, 1), new(1, 30), "Eastern Standard Time") }), "AmbiguousLocalTime");
        Check("date-only deadline follows 23-hour DST day", TodoDeadlines.Instant(task with { Deadline = new(new(2026, 3, 8), null, "Eastern Standard Time") }) - PlannerTime.Boundary(new(2026, 3, 8), "Eastern Standard Time") == TimeSpan.FromHours(23));
        await Run(w, new BulkEditTodos([Ref(g)], day.AddDays(1), null, day));
        Check("time-only deadline tracks batch date change", TodoDeadlines.Instant(w.Snapshot!.Todos.Single(t => t.Id == g)) == TodoDeadlines.Instant(task).AddDays(1));
        w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now);
        Check("reopen preserves deadline and hierarchy", w.Snapshot!.Todos.Single(t => t.Id == g).Deadline == task.Deadline && w.Snapshot.Todos.Single(t => t.Id == a).ParentId == c);
        var old = JsonNode.Parse(PlannerCodec.Encode(w.Snapshot!))!; old["schemaVersion"] = 4;
        foreach (var t in old["payload"]!["todos"]!.AsArray()) t!.AsObject().Remove("deadline");
        var compact = old["payload"]!.ToJsonString(new() { WriteIndented = false }); old["payloadSha256"] = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(compact)));
        var migrated = PlannerCodec.Decode(Encoding.UTF8.GetBytes(old.ToJsonString()), Utc);
        Check("real schema4 migration adds no artificial deadlines", migrated.Migrated && migrated.State.Todos.All(t => t.Deadline == null) && migrated.State.Todos.Select(t => t.Id).SequenceEqual(w.Snapshot.Todos.Select(t => t.Id)));

        var root = Todo("series", day); var one = Todo("one", day) with { ParentId = root.Id }; var two = Todo("two", day) with { ParentId = root.Id }; var leaf = Todo("leaf", day) with { ParentId = one.Id };
        var series = await Run(w, new CreateSeries(new() { Start = day, Count = 6 }, [root, one, two, leaf]));
        TodoRef Occ(Todo t, int delta = 0) => TodoRef.For(new(series, t.Id, day.AddDays(delta)));
        await Run(w, new ReparentTodo(Occ(one), Occ(two)));
        Check("single recurring reparent preserves IDs and future template", PlannerQueries.Resolve(w.Snapshot!, Occ(one)).ParentId == Occ(two).Id && PlannerQueries.Resolve(w.Snapshot!, Occ(one, 1)).ParentId == Occ(root, 1).Id && PlannerQueries.Resolve(w.Snapshot!, Occ(leaf)).ParentId == Occ(one).Id);
        await Run(w, new ReparentTodo(Occ(one, 1), Occ(two, 1), EditScope.FromDate));
        Check("future recurring reparent generates new hierarchy lazily", PlannerQueries.Resolve(w.Snapshot!, Occ(one, 3)).ParentId == Occ(two, 3).Id);
        bytes = File.ReadAllBytes(path); bad = await w.ExecuteAsync(new ReparentTodo(Occ(one), null));
        Check("leaving recurrence requires explicit conversion", bad.Diagnostic == "HierarchyConversionRequired" && bytes.SequenceEqual(File.ReadAllBytes(path)));
        await Run(w, new SaveActivity(new() { Title = "occ link", Todo = Occ(one), TimeZoneId = "UTC", Intervals = [new(Guid.NewGuid(), Now, Now.AddHours(1))] }));
        await Run(w, new ReparentTodo(Occ(one), Ref(c), DetachKind: TodoKind.CarryLatest));
        Check("detach occurrence retains identity subtree and links", w.Snapshot!.Todos.Single(t => t.Id == Occ(one).Id).Origin == null && w.Snapshot.Todos.Single(t => t.Id == Occ(leaf).Id).Kind == TodoKind.CarryLatest && w.Snapshot.Activities.Single(a => a.Title == "occ link").Todo == Ref(Occ(one).Id));
        Check("detached occurrences cannot reappear or duplicate", PlannerQueries.TodosOn(w.Snapshot!, day, day).Select(t => t.Id).Distinct().Count() == PlannerQueries.TodosOn(w.Snapshot!, day, day).Length && PlannerQueries.Children(w.Snapshot!, PlannerQueries.Resolve(w.Snapshot!, Occ(two))).All(t => t.Id != Occ(one).Id));
        await w.UndoAsync(); Check("undo detach restores original occurrence references", w.Snapshot!.Activities.Single(a => a.Title == "occ link").Todo == Occ(one));
        await w.RedoAsync(); w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now); PlannerIntegrity.Validate(w.Snapshot!);
        Check("detached identities survive restart and full integrity", w.Snapshot!.Todos.Single(t => t.Id == Occ(one).Id).ParentId == c);
        bytes = File.ReadAllBytes(path); bad = await w.ExecuteAsync(new BulkEditTodos([Occ(root, 2)], null, TodoKind.Once, day.AddDays(2)));
        Check("bulk cannot implicitly convert unchecked descendants", bad.Diagnostic == "BulkSelectSubtree" && bytes.SequenceEqual(File.ReadAllBytes(path)));
        var top = await Run(w, new SaveTodo(Todo("lift target", day)));
        var nested = await Run(w, new AddChildTodo(Todo("lift source", day), Ref(top)));
        var neighbor = await Run(w, new SaveTodo(Todo("lift neighbor", day)));
        await Run(w, new PlaceTodoRelative(new(Ref(nested), null), Ref(neighbor), false, day, day));
        var ordered = PlannerQueries.TodosOn(w.Snapshot!, day, day).Where(t => t.ParentId == null).ToArray();
        Check("edge cross-level lift and insertion are atomic", w.Snapshot!.Todos.Single(t => t.Id == nested).ParentId == null && Array.FindIndex(ordered, t => t.Id == nested) + 1 == Array.FindIndex(ordered, t => t.Id == neighbor));
        await w.UndoAsync(); Check("one undo restores hierarchy and ordering together", w.Snapshot!.Todos.Single(t => t.Id == nested).ParentId == top);
        bytes = File.ReadAllBytes(path); bad = await w.ExecuteAsync(new PlaceTodoRelative(new(Ref(top), Ref(nested)), Ref(g), false, day, day));
        Check("invalid cross-level edge cannot partially move", !bad.Success && bytes.SequenceEqual(File.ReadAllBytes(path)));
        var batchParent = await Run(w, new SaveTodo(Todo("batch series parent", day)));
        var batchChild = await Run(w, new AddChildTodo(Todo("batch series child", day), Ref(batchParent)));
        await Run(w, new BulkEditTodos([Ref(batchParent), Ref(batchChild)], null, TodoKind.SeriesTemplate, day, new() { Start = day, Count = 3 }));
        Check("explicit selected subtree can be batch-converted to series", w.Snapshot!.Series.Any(s => s.Versions.Any(v => v.Templates.Any(t => t.Title == "batch series parent"))) && w.Snapshot.Todos.Single(t => t.Id == batchParent).Status == TodoStatus.Cancelled);
        var relativeSeries = w.Snapshot.Series.Single(s => s.Versions[0].Templates.Any(t => t.Title == "batch series parent"));
        var virtualChild = TodoRef.For(new(relativeSeries.Id, relativeSeries.Versions[0].Templates.Single(t => t.Title == "batch series child").Id, day.AddDays(1)));
        await Run(w, new BulkEditTodos([virtualChild], day.AddDays(6), null, day));
        Check("bulk single date materializes parent and moves only selected child occurrence", PlannerQueries.Resolve(w.Snapshot!, virtualChild).PlannedDate == day.AddDays(6) && PlannerQueries.Resolve(w.Snapshot!, TodoRef.For(new(relativeSeries.Id, relativeSeries.RootTodoId, day.AddDays(1)))).PlannedDate == day.AddDays(1));
        var beforeFailure = w.Snapshot; bytes = File.ReadAllBytes(path);
        var failing = await PlannerWorkspace.OpenAsync(new(path, new() { Fault = stage => { if (stage == WriteStage.HalfWritten) throw new IOException("injected batch interruption"); } }), Utc, () => Now);
        bad = await failing.ExecuteAsync(new BulkEditTodos([Ref(top), Ref(nested)], day.AddDays(5), null, day));
        Check("interrupted bulk write retains both memory and original JSON", !bad.Success && failing.Snapshot!.Todos.Single(t => t.Id == top).PlannedDate == day && bytes.SequenceEqual(File.ReadAllBytes(path)));
        await Run(w, new SetTodoCompleted(Occ(one, 4), true));
        await Run(w, new ReparentTodo(Occ(one, 2), Occ(root, 2), EditScope.FromDate));
        Check("future hierarchy edit preserves completed occurrence and descendant history", PlannerQueries.Resolve(w.Snapshot!, Occ(one, 4)).ParentId == Occ(two, 4).Id && PlannerQueries.Resolve(w.Snapshot!, Occ(leaf, 4)).Status == TodoStatus.Completed && PlannerQueries.Resolve(w.Snapshot!, Occ(one, 3)).ParentId == Occ(root, 3).Id);
    }
}
