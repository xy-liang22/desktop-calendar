using DesktopCalendar.Core;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;

internal static partial class Checks
{
    static async Task BoundaryChecks(string output)
    {
        var day = new DateOnly(2026, 11, 2);
        // Compare ordinal math with bounded brute-force dates, including partial first weeks.
        bool recurrenceCorrect = true;
        foreach (var first in Enum.GetValues<DayOfWeek>())
        foreach (int interval in new[] { 1, 2, 3, 7 })
        foreach (int startOffset in Enumerable.Range(0, 7))
        {
            var rule = new RecurrenceRule { Frequency = Frequency.Weekly, Start = day.AddDays(startOffset), WeekStart = first, Interval = interval, Weekdays = [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Thursday] };
            var nominal = Enumerable.Range(0, 400).Select(i => rule.Start.AddDays(i)).Where(d => Recurrence.Matches(rule, d)).Take(13).ToHashSet();
            var counted = rule with { Count = 13 };
            recurrenceCorrect &= Enumerable.Range(0, 400).All(i => Recurrence.Matches(counted, rule.Start.AddDays(i)) == nominal.Contains(rule.Start.AddDays(i)));
        }
        Check("weekly COUNT matches brute force for 196 week/interval/start combinations", recurrenceCorrect);
        bool monthlyCorrect = true;
        foreach (int interval in new[] { 1, 2, 3, 12 })
        foreach (int month in Enumerable.Range(1, 12))
        {
            var rule = new RecurrenceRule { Frequency = Frequency.Monthly, Start = new(2024, month, 20), Interval = interval, MonthDays = [1, 20, 29, 31] };
            var nominal = Enumerable.Range(0, 1500).Select(i => rule.Start.AddDays(i)).Where(d => Recurrence.Matches(rule, d)).Take(19).ToHashSet();
            monthlyCorrect &= Enumerable.Range(0, 1500).All(i => Recurrence.Matches(rule with { Count = 19 }, rule.Start.AddDays(i)) == nominal.Contains(rule.Start.AddDays(i)));
        }
        Check("monthly COUNT matches leap/short-month brute force", monthlyCorrect);
        var exceptions = new RecurrenceRule { Start = day, Count = 2, ExcludedDates = [day], IncludedDates = [day.AddDays(5)] };
        Check("EXDATE does not extend COUNT and inclusion is explicit", !Recurrence.Matches(exceptions, day) && Recurrence.Matches(exceptions, day.AddDays(1)) && !Recurrence.Matches(exceptions, day.AddDays(2)) && Recurrence.Matches(exceptions, day.AddDays(5)));
        Throws("invalid recurrence interval rejected", () => Recurrence.Validate(exceptions with { Interval = 0 }), "InvalidRecurrence");
        Check("DateOnly extremes recurrence is bounded", Recurrence.Matches(new() { Start = DateOnly.MinValue, Interval = 2 }, DateOnly.MinValue) && Recurrence.Matches(new() { Start = DateOnly.MaxValue }, DateOnly.MaxValue));
        var springWeek = PlannerTime.Week(new(2026, 3, 8), DayOfWeek.Monday, "Eastern Standard Time");
        Check("DST week denominator is 167 hours", PlannerTime.WeekRange(springWeek).Duration.TotalHours == 167);
        var samoa = PlannerTime.Day(new(2011, 12, 30), "Samoa Standard Time");
        if (samoa.Duration == TimeSpan.Zero) Check("platform Samoa 2011 skipped-date historical rule", true);
        else Results.Add($"NOT VERIFIED Samoa 2011 actual historical skip: platform boundaries={samoa.Start:O}..{samoa.End:O}; no rule-data override");
        var midnightRule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new(2026, 1, 1), new(2026, 12, 31), TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 3, 1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 1, 0, 0), 10, 1));
        var midnightZone = TimeZoneInfo.CreateCustomTimeZone("probe-only", TimeSpan.Zero, "probe", "probe", "probe DST", [midnightRule]);
        Check("controlled midnight gap advances to first valid minute", PlannerTime.Boundary(new(2026, 3, 1), midnightZone) == new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        Check("controlled repeated midnight chooses earlier instant", PlannerTime.Boundary(new(2026, 10, 1), midnightZone) == new DateTimeOffset(2026, 9, 30, 23, 0, 0, TimeSpan.Zero));
        var adjacent = PlannerTime.Merge([new(Now, Now.AddHours(1)), new(Now.AddHours(1), Now.AddHours(2))]);
        Check("half-open adjacent periods merge without extra time", adjacent.Length == 1 && adjacent[0].Duration.TotalHours == 2);
        Check("end at midnight belongs only to prior day", PlannerTime.Clip(new(Now.AddHours(11), Now.AddHours(12)), PlannerTime.Day(day.AddDays(1), "UTC")) == null);
        Throws("local DateTime kind must be explicit unspecified", () => PlannerTime.FromLocal(DateTime.UtcNow, "UTC"), "LocalTimeMustBeUnspecified");
        var path = TestFile(output, "boundaries"); var w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now);
        var cat1 = await Run(w, new SaveCategory(new() { Name = "one" })); var cat2 = await Run(w, new SaveCategory(new() { Name = "two" }));
        var a1 = await Run(w, new SaveActivity(new() { Title = "a1", CategoryId = cat1, Intervals = [new(Guid.Empty, Now, Now.AddHours(2))] }));
        var a2 = await Run(w, new SaveActivity(new() { Title = "a2", CategoryId = cat1, Intervals = [new(Guid.Empty, Now.AddHours(1), Now.AddHours(3))] }));
        var a3 = await Run(w, new SaveActivity(new() { Title = "a3", CategoryId = cat2, Intervals = [new(Guid.Empty, Now, Now.AddHours(3))] }));
        var stats = PlannerQueries.CategoryStatistics(w.Snapshot!, PlannerTime.Week(day, DayOfWeek.Monday, "UTC"));
        Check("same-category union, different categories independent", stats.Length == 2 && stats.All(s => s.Seconds == 10800));
        var act = w.Snapshot!.Activities.First(a => a.Id == a1);
        await Run(w, new SaveActivity(act with { Title = "edited", Reaction = "🙂", Emoji = "📖", Summary = "summary", Notes = new() { Text = "initial note" } }));
        Check("activity edit preserves interval identities and notes", w.Snapshot!.Activities.First(a => a.Id == a1).Intervals[0].Id == act.Intervals[0].Id && w.Snapshot.Activities.First(a => a.Id == a1).Notes.Text == "initial note");
        var original = w.Snapshot;
        Check("invalid activity range cannot be committed", !(await w.ExecuteAsync(new SaveActivity(act with { Intervals = [new(Guid.NewGuid(), Now, Now)] }))).Success && w.Snapshot == original);
        var todo = await Run(w, new SaveTodo(Todo("outside", day.AddDays(-1)))); await Run(w, new SetTodoCompleted(Ref(todo), true));
        var week = PlannerTime.Week(day, DayOfWeek.Monday, "UTC");
        var goal = await Run(w, new SaveGoal(new() { Title = "count", Week = week, Source = GoalSource.Todo, Kind = GoalKind.Count, Target = 2, Todos = [Ref(todo)], Activities = [a1, a2] }));
        var gp = PlannerQueries.Goal(w.Snapshot!, w.Snapshot!.Goals.Single());
        Check("goal uses one source and reports cross-week link", gp.Automatic == 1 && gp.OutsideWeekTodos.Length == 1);
        var g = w.Snapshot!.Goals.Single();
        Check("goal source change needs confirmation", (await w.ExecuteAsync(new SaveGoal(g with { Source = GoalSource.Activity }))).Diagnostic == "GoalTypeChangeNeedsConfirmation");
        await Run(w, new SaveGoal(g with { Source = GoalSource.Activity }, true));
        Check("source change preserves navigation links", w.Snapshot!.Goals.Single().Todos.Length == 1 && PlannerQueries.Goal(w.Snapshot, w.Snapshot.Goals.Single()).Automatic == 2);
        await Run(w, new SaveGoal(w.Snapshot!.Goals.Single() with { Kind = GoalKind.Check, Target = 1 }, true));
        Check("automatic checkbox goal collapses count to one", PlannerQueries.Goal(w.Snapshot!, w.Snapshot!.Goals.Single()).Effective == 1);
        var manual = await Run(w, new SaveGoal(new() { Title = "manual", Week = week, Kind = GoalKind.Check, Source = GoalSource.None, Checked = true }));
        Check("manual checkbox has no fabricated source", PlannerQueries.Goal(w.Snapshot!, w.Snapshot!.Goals.Single(g => g.Id == manual)).Effective == 1);
        var parent = await Run(w, new SaveTodo(Todo("parent", day))); var child = await Run(w, new SaveTodo(Todo("child", day) with { ParentId = parent }));
        await Run(w, new DeleteTodo(Ref(parent), ChildDisposition.PromoteChildren));
        Check("explicit parent deletion can promote children", w.Snapshot!.Todos.Single(t => t.Id == child).ParentId == null);
        var root = Todo("series", day); var sub = Todo("child", day) with { ParentId = root.Id };
        var series = await Run(w, new CreateSeries(new() { Start = day }, [root, sub]));
        TodoRef O(Guid node, int days) => TodoRef.For(new(series, node, day.AddDays(days)));
        await Run(w, new SetTodoCompleted(O(sub.Id, 0), true));
        await Run(w, new CancelSeries(series, day, EditScope.EntireSeries, sub.Id));
        Check("deleting recurring child keeps completed historic node", PlannerQueries.Resolve(w.Snapshot!, O(sub.Id, 0)).Status == TodoStatus.Completed && w.Snapshot!.Series.Single().RetiredNodeIds.Contains(sub.Id));
        Throws("deleted recurring child no longer generated", () => PlannerQueries.Resolve(w.Snapshot!, O(sub.Id, 1)), "TemplateNotFound");
        Check("deleting child never deletes recurring root", PlannerQueries.Resolve(w.Snapshot!, O(root.Id, 1)).Title == "series");
        var linked = await Run(w, new SaveActivity(new() { Title = "linked", Todo = O(root.Id, 2), Intervals = [new(Guid.Empty, Now, Now.AddHours(1))] }));
        await Run(w, new EditSeries(series, day.AddDays(1), EditScope.FromDate, new() { Start = day.AddDays(1), Frequency = Frequency.Weekly, Weekdays = [DayOfWeek.Friday] }));
        Check("rule edit detaches removed pending instance but keeps actual work", w.Snapshot!.Activities.Single(a => a.Id == linked).Todo == null && !w.Snapshot.Todos.Any(t => t.Id == O(root.Id, 2).Id));
        Check("rule edit still preserves completed excluded history", PlannerQueries.Resolve(w.Snapshot!, O(sub.Id, 0)).Status == TodoStatus.Completed);
        var other = await PlannerWorkspace.OpenAsync(new(path), Utc, () => Now);
        await Run(other, new SaveTodo(Todo("external writer", day)));
        var old = w.Snapshot;
        var conflict = await w.ExecuteAsync(new SaveTodo(Todo("stale writer", day)));
        Check("workspace external conflict keeps in-memory data and blocks further edits", conflict.Status == StoreStatus.Conflict && w.Snapshot == old && !w.Storage.CanWrite && !(await w.UndoAsync()).Success);
        await w.ReloadAsync();
        Check("explicit reload restores editing without stale history", w.Storage.CanWrite && w.Snapshot!.Todos.Any(t => t.Title == "external writer") && !w.CanUndo);
        var writeFailPath = TestFile(output, "failed-transaction");
        var fail = await PlannerWorkspace.OpenAsync(new(writeFailPath, new() { Fault = s => { if (s == WriteStage.BeforeReplace) throw new IOException("Stop"); } }), Utc, () => Now);
        var empty = fail.Snapshot;
        var failed = await fail.ExecuteAsync(new SaveTodo(Todo("not saved", day)));
        Check("failed durable save publishes no state and no undo", failed.Status == StoreStatus.IoError && fail.Snapshot == empty && !fail.CanUndo && !File.Exists(writeFailPath));
        var hist = await PlannerWorkspace.OpenAsync(new(TestFile(output, "history")), Utc, () => Now);
        for (int i = 0; i < 103; i++) await Run(hist, new SaveCategory(new() { Name = "category" + i }));
        int undos = 0; while ((await hist.UndoAsync()).Success) undos++;
        Check("session undo history is bounded to 100 durable transactions", undos == 100 && hist.Snapshot!.Categories.Length == 3);
        await hist.RedoAsync(); await Run(hist, new SaveCategory(new() { Name = "new branch" }));
        Check("new edit invalidates redo branch", !hist.CanRedo);
        var tooLarge = new PlannerStore(TestFile(output, "limit"), new() { MaximumBytes = 100 });
        var small = await tooLarge.OpenAsync(Utc);
        Check("size limit rejects save before replacing primary", (await tooLarge.CommitAsync(small.State! with { Revision = 1 }, null)).Diagnostic == "FileTooLarge" && !File.Exists(tooLarge.FilePath));
        string backupNewer = TestFile(output, "future-backup"); await File.WriteAllTextAsync(backupNewer + ".bak", "{\"schemaVersion\":999}");
        Check("future backup prevents empty downgrade", (await new PlannerStore(backupNewer).OpenAsync(Utc)).Status == StoreStatus.NewerSchema);
        string pending = TestFile(output, "pending"); await File.WriteAllBytesAsync(pending + ".pending-complete", PlannerCodec.Encode(PlannerState.Empty("UTC")));
        var ps = new PlannerStore(pending); var pl = await ps.OpenAsync(Utc);
        Check("complete orphan requires explicit recovery", pl.Status == StoreStatus.RecoveryRequired && pl.Candidates.Length == 1 && !File.Exists(pending));
        Check("complete orphan can be recovered explicitly", (await ps.RecoverAsync(pl.Candidates[0], true)).CanWrite && File.Exists(pending));
        string malformed = TestFile(output, "malformed"); await File.WriteAllTextAsync(malformed, "{\"schemaVersion\":2}");
        var ms = new PlannerStore(malformed); var ml = await ms.OpenAsync(Utc);
        Check("missing required payload cannot become default entities", ml.Status == StoreStatus.RecoveryRequired && !ml.CanWrite);
        var defaults = await PlannerWorkspace.OpenAsync(new(TestFile(output, "defaults")), Utc, () => Now);
        await Run(defaults, new InitializeCategories()); var categoryIds = defaults.Snapshot!.Categories.Select(c => c.Id).Order().ToArray();
        await Run(defaults, new InitializeCategories());
        Check("built-in category initialization is idempotent", defaults.Snapshot!.Categories.Length == 3 && defaults.Snapshot.Categories.Select(c => c.Id).Order().SequenceEqual(categoryIds));
        Check("initial category labels support both languages", PlannerDefaults.Categories("en", Now).Select(c => c.Name).SequenceEqual(["Daily", "Work", "Life"]) && PlannerDefaults.Categories("zh-CN", Now).First().Name == "日常");
        var builtIn = defaults.Snapshot!.Categories.First();
        var inheritedTodo = await Run(defaults, new SaveTodo(Todo("inherit", day) with { CategoryId = builtIn.Id }));
        var inherited = await Run(defaults, new SaveActivity(new() { Title = "inherit actual", Todo = Ref(inheritedTodo), Intervals = [new(Guid.Empty, Now, Now.AddHours(1))] }));
        var independent = await Run(defaults, new SaveActivity(new() { Title = "explicit unclassified", Todo = Ref(inheritedTodo), Intervals = [new(Guid.Empty, Now, Now.AddHours(1))] }, false));
        Check("activity inherits linked category unless explicitly overridden", defaults.Snapshot!.Activities.Single(a => a.Id == inherited).CategoryId == builtIn.Id && defaults.Snapshot.Activities.Single(a => a.Id == independent).CategoryId == null);
        var local = PlannerTime.FromLocal(new(2026, 11, 1, 1, 30, 0), "Eastern Standard Time", TimeSpan.FromHours(-4));
        var offsetActivity = await Run(defaults, new SaveActivity(new() { Title = "offset", TimeZoneId = "Eastern Standard Time", Intervals = [new(Guid.Empty, local, local.AddHours(1))] }));
        var offsetRoundtrip = PlannerCodec.Decode(PlannerCodec.Encode(defaults.Snapshot!), Utc).State.Activities.Single(a => a.Id == offsetActivity);
        Check("explicit offsets and named zone survive persistence", offsetRoundtrip.Intervals[0].Start.Offset == TimeSpan.FromHours(-4) && offsetRoundtrip.TimeZoneId == "Eastern Standard Time");
        var decoration = await Run(defaults, new SaveDecoration(new() { Date = day, Color = "#AFC5D8" }));
        var sameDecoration = await Run(defaults, new SaveDecoration(new() { Date = day, Color = "#A3BDB0" }));
        Check("date decoration upsert retains stable identity", decoration == sameDecoration && defaults.Snapshot!.Decorations.Single().Color == "#A3BDB0");
        await Run(defaults, new DeleteDecoration(decoration));
        Check("date decoration delete has no todo side effect", defaults.Snapshot!.Decorations.IsEmpty && defaults.Snapshot.Todos.Any(t => t.Id == inheritedTodo));
        var legacyNode = JsonNode.Parse(PlannerCodec.EncodeLegacyV1ForMigration(defaults.Snapshot!))!;
        legacyNode["data"]!.AsObject().Remove("decorations");
        Check("v1 fixture with actually absent new fields migrates", PlannerCodec.Decode(Encoding.UTF8.GetBytes(legacyNode.ToJsonString()), Utc).State.Decorations.IsEmpty);
        // Real cancellation of an in-progress write retains its previous durable state.
        using var cancelled = new CancellationTokenSource();
        var cancelStore = new PlannerStore(TestFile(output, "cancel"), new() { Fault = stage => { if (stage == WriteStage.HalfWritten) cancelled.Cancel(); } });
        var cancelLoad = await cancelStore.OpenAsync(Utc);
        Check("cancellation during partial write does not create primary", (await cancelStore.CommitAsync(cancelLoad.State! with { Revision = 1 }, null, cancelled.Token)).Status == StoreStatus.Cancelled && !File.Exists(cancelStore.FilePath));
        // Required properties/unknown fields must not get silently normalized or discarded.
        string unknown = TestFile(output, "unknown"); var emptyEnvelope = JsonNode.Parse(PlannerCodec.Encode(PlannerState.Empty("UTC")))!; emptyEnvelope["unexpected"] = 1;
        await File.WriteAllTextAsync(unknown, emptyEnvelope.ToJsonString());
        Check("unknown same-version field is read-only corrupt state", (await new PlannerStore(unknown).OpenAsync(Utc)).Status == StoreStatus.RecoveryRequired);
        var historyPath = TestFile(output, "carry-latest"); var latest = await PlannerWorkspace.OpenAsync(new(historyPath), Utc, () => Now.AddDays(3));
        var latestId = await Run(latest, new SaveTodo(Todo("latest", day) with { Kind = TodoKind.CarryLatest }));
        await Run(latest, new SetTodoCompleted(Ref(latestId), true));
        Check("completed latest carry has exactly one frozen display", Enumerable.Range(0, 8).Count(i => PlannerQueries.TodosOn(latest.Snapshot!, day.AddDays(i), day.AddDays(10)).Any(t => t.Id == latestId)) == 1 && PlannerQueries.TodosOn(latest.Snapshot!, day.AddDays(3), day.AddDays(10)).Single().Status == TodoStatus.Completed);
        Check("empty series templates give diagnostic without crashing", (await defaults.ExecuteAsync(new CreateSeries(new() { Start = day }, []))).Diagnostic == "TemplateIdsRequired");
        Check("malformed command cannot mutate snapshot", !(await defaults.ExecuteAsync(new SaveTodo(null!))).Success);
        Check("invalid runtime options fail before data initialization", (await new PlannerStore(TestFile(output, "options")).OpenAsync(Utc with { Language = "invalid" })).Status == StoreStatus.InvalidData);
        var notFile = TestFile(output, "directory"); Directory.CreateDirectory(notFile);
        Check("directory at data path reports I/O instead of empty document", (await new PlannerStore(notFile).OpenAsync(Utc)).Status == StoreStatus.IoError);
        // Completed children remain navigable even if a cancelled parent is only a context row.
        var treePath = TestFile(output, "tree"); var tree = await PlannerWorkspace.OpenAsync(new(treePath), Utc, () => Now);
        var tr = Todo("tree root", day); var tc = Todo("tree child", day) with { ParentId = tr.Id };
        var ts = await Run(tree, new CreateSeries(new() { Start = day }, [tr, tc]));
        await Run(tree, new SetTodoCompleted(TodoRef.For(new(ts, tc.Id, day)), true));
        await Run(tree, new SetTodoCompleted(TodoRef.For(new(ts, tr.Id, day)), false)); // explicit parent override, children remain complete
        await Run(tree, new CancelSeries(ts, day, EditScope.EntireSeries));
        var nodes = PlannerQueries.TodoTreeOn(tree.Snapshot!, day, day);
        Check("cancelled ancestor is context only, completed descendant remains accessible", nodes.Length == 1 && nodes[0].ContextOnly && !nodes[0].Children[0].ContextOnly && nodes[0].Children[0].Value.Status == TodoStatus.Completed);
        var across = await Run(defaults, new SaveTodo(Todo("other date parent", day.AddDays(-1))));
        var acrossChild = await Run(defaults, new SaveTodo(Todo("today child", day) with { ParentId = across }));
        Check("date tree includes ancestor context without counting extra daily task", PlannerQueries.TodoTreeOn(defaults.Snapshot!, day, day).Single(n => n.Value.Id == across).ContextOnly && PlannerQueries.TodosOn(defaults.Snapshot!, day, day).All(t => t.Id != across));
        var beforeId = defaults.Snapshot!.Id;
        await defaults.ReloadAsync(Utc with { WeekStart = DayOfWeek.Sunday, Language = "en" });
        Check("runtime preference changes do not migrate document identity or historical dates", defaults.Snapshot!.Id == beforeId && defaults.Snapshot.Options.WeekStart == DayOfWeek.Sunday && defaults.Snapshot.Todos.Any(t => t.Id == acrossChild));
        var keysBefore = defaults.Snapshot!.Todos.ToDictionary(t => t.Id, t => t.SortKey);
        await Run(defaults, new SetTodoCompleted(Ref(inheritedTodo), true)); await Run(defaults, new SetTodoCompleted(Ref(inheritedTodo), false));
        Check("completion toggles preserve every sibling's exact original sort key", defaults.Snapshot!.Todos.All(t => keysBefore[t.Id] == t.SortKey));
        var assetId = await Run(defaults, new SaveAsset(new() { RelativePath = "assets/images/owned.png", MediaType = "image/png", Length = 10, Sha256 = new('f', 64) }));
        Check("local asset URI resolves by stable ID without opening any file", PlannerQueries.ResolveAsset(defaults.Snapshot!, PlannerQueries.AssetReference(assetId)).RelativePath == "assets/images/owned.png");
        Throws("file and network references are not asset resolver inputs", () => PlannerQueries.ResolveAsset(defaults.Snapshot!, "file:///C:/private.png"), "InvalidAssetReference");
        Throws("asset URI cannot contain traversal suffix", () => PlannerQueries.ResolveAsset(defaults.Snapshot!, "asset://" + assetId + "/../other"), "InvalidAssetReference");
        string orderPath = TestFile(output, "mixed-sort"); var mixed = await PlannerWorkspace.OpenAsync(new(orderPath), Utc, () => Now);
        var ta = await Run(mixed, new SaveTodo(Todo("A", day) with { PlannedTime = new(8, 30) }));
        var tb = await Run(mixed, new SaveTodo(Todo("B", day) with { PlannedTime = new(10, 0) }));
        var tcId = await Run(mixed, new SaveTodo(Todo("C", day)));
        await Run(mixed, new ReorderTodos([Ref(tcId), Ref(ta), Ref(tb)])); await Run(mixed, new SetTodoCompleted(Ref(tb), true));
        var mixedRestart = await PlannerWorkspace.OpenAsync(new(orderPath), Utc, () => Now);
        Check("manual timed/untimed mix and mode survive independent reopen", string.Concat(PlannerQueries.TodosOn(mixedRestart.Snapshot!, day, day).Select(t => t.Title)) == "CAB" && PlannerQueries.Ordering(mixedRestart.Snapshot!, null, day) == TodoSort.Manual);
        var originalKeys = mixedRestart.Snapshot!.Todos.ToDictionary(t => t.Id, t => t.SortKey);
        await Run(mixedRestart, new SetTodoCompleted(Ref(tb), false));
        await Run(mixedRestart, new SetTodoOrderMode(null, day, TodoSort.PlannedTime));
        Check("explicit restore time sort retains original manual keys", string.Concat(PlannerQueries.TodosOn(mixedRestart.Snapshot!, day, day).Select(t => t.Title)) == "ABC" && mixedRestart.Snapshot!.Todos.All(t => originalKeys[t.Id] == t.SortKey));
        var batchBefore = mixedRestart.Snapshot;
        var batch = await mixedRestart.ExecuteAsync(new BatchCommand([new SaveTodo(Todo("batch new", day)), new SaveTodo(Todo("", day))]));
        Check("failed command batch commits no partial state", !batch.Success && mixedRestart.Snapshot == batchBefore);
        var batchA = Todo("batch A", day.AddDays(1)); var batchB = Todo("batch B", day.AddDays(1));
        await Run(mixedRestart, new BatchCommand([new SaveTodo(batchA), new SaveTodo(batchB)]));
        Check("successful command batch increments one revision", mixedRestart.Snapshot!.Revision == batchBefore!.Revision + 1 && mixedRestart.Snapshot.Todos.Any(t => t.Id == batchA.Id));
        await mixedRestart.UndoAsync();
        Check("one durable undo reverses complete multi-command batch", mixedRestart.Snapshot!.Todos.All(t => t.Id != batchA.Id && t.Id != batchB.Id));
        Throws("minimum date conversion cannot underflow UTC", () => PlannerTime.FromLocal(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Unspecified), "Line Islands Standard Time"), "DateOutOfRange");
        Throws("invalid week identity cannot silently shift statistics", () => PlannerTime.WeekRange(new(day, DayOfWeek.Sunday, "UTC")), "InvalidWeek");
        Throws("clipping cannot silently accept reversed actual range", () => PlannerTime.Clip(new(Now, Now.AddHours(-1)), new(Now, Now.AddHours(1))), "InvalidInterval");
        var restructure = await PlannerWorkspace.OpenAsync(new(TestFile(output, "tree-exception")), Utc, () => Now);
        var rr = Todo("root", day); var rc = Todo("child", day) with { ParentId = rr.Id }; var middle = Todo("new middle", day) with { ParentId = rr.Id };
        var rs = await Run(restructure, new CreateSeries(new() { Start = day }, [rr, rc]));
        await Run(restructure, new SetTodoCompleted(TodoRef.For(new(rs, rc.Id, day)), false));
        await Run(restructure, new EditSeries(rs, day, EditScope.FromDate, Templates: [rr, middle, rc with { ParentId = middle.Id }]));
        var virtualMiddle = PlannerQueries.Resolve(restructure.Snapshot!, TodoRef.For(new(rs, middle.Id, day)));
        Check("template reparent cannot show materialized exception in two sibling groups", PlannerQueries.Children(restructure.Snapshot!, virtualMiddle).IsEmpty && PlannerQueries.Children(restructure.Snapshot!, PlannerQueries.Resolve(restructure.Snapshot!, TodoRef.For(new(rs, rr.Id, day)))).Count(t => t.Id == TodoRef.For(new(rs, rc.Id, day)).Id) == 1);
        var copy = await PlannerWorkspace.OpenAsync(new(TestFile(output, "copy")), Utc, () => Now);
        var cp = await Run(copy, new SaveTodo(Todo("source", day) with { Important = true, PlannedTime = new(9, 30), Description = "copy description" }));
        var cc1 = await Run(copy, new SaveTodo(Todo("one", day, 1) with { ParentId = cp, ManualProgress = 40 }));
        var cc2 = await Run(copy, new SaveTodo(Todo("two", day.AddDays(1), 2) with { ParentId = cp }));
        await Run(copy, new ReorderTodos([Ref(cc2), Ref(cc1)])); await Run(copy, new SetTodoCompleted(Ref(cc2), true));
        var copied = await Run(copy, new CopyTodo(Ref(cp), day.AddDays(4)));
        var copyRoot = copy.Snapshot!.Todos.Single(t => t.Id == copied); var copyChildren = PlannerQueries.Children(copy.Snapshot, copyRoot);
        Check("copy preserves properties with new root identity", copied != cp && copyRoot.CopiedFromId == cp && copyRoot.Important && copyRoot.PlannedTime == new TimeOnly(9, 30) && copyRoot.Description == "copy description");
        Check("copy preserves subtree identities and relative dates", copyChildren.Length == 2 && copyChildren.All(t => t.ParentId == copied && t.Id != cc1 && t.Id != cc2) && copyChildren.Single(t => t.CopiedFromId == cc2).PlannedDate == day.AddDays(5));
        Check("copy resets completed state and manual progress without changing source", copyChildren.All(t => t.Status == TodoStatus.Pending && t.ManualProgress == 0) && copy.Snapshot.Todos.Single(t => t.Id == cc2).Status == TodoStatus.Completed);
        Check("copy preserves manual child sorting mode", copyChildren[0].CopiedFromId == cc2 && PlannerQueries.Ordering(copy.Snapshot, copied, day.AddDays(4)) == TodoSort.Manual);
        await copy.UndoAsync(); Check("one undo reverses complete copied subtree", copy.Snapshot!.Todos.All(t => t.Id != copied && t.ParentId != copied) && copy.Snapshot.Todos.Length == 3);
    }
}
