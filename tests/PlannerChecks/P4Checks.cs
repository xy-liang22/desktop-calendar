using DesktopCalendar.Core;
using System.Collections.Immutable;

internal static partial class Checks
{
    static async Task P4Checks(string output)
    {
        var day = new DateOnly(2026, 11, 2); var clock = Now; string path = TestFile(output, "p4"); var w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => clock);
        bool nextCorrect = true;
        for (int i = 0; i < 40; i++)
        {
            var start = new DateOnly(2027, 12, 20).AddDays(i % 20); var from = start.AddDays(20);
            var rule = new RecurrenceRule { Start = start, Until = start.AddDays(180), Frequency = (Frequency)(i % 3), Interval = i % 4 + 1, Count = i % 5 == 0 ? 3 : null,
                WeekStart = (DayOfWeek)(i % 7), Weekdays = [DayOfWeek.Monday, DayOfWeek.Friday], MonthDays = [1, 15, 29, 31], IncludedDates = [start.AddDays(37)], ExcludedDates = [start.AddDays(3)] };
            var brute = Enumerable.Range(0, rule.Until.Value.DayNumber - from.DayNumber + 1).Select(n => from.AddDays(n)).Where(d => Recurrence.Matches(rule, d)).Cast<DateOnly?>().FirstOrDefault();
            nextCorrect &= Recurrence.Next(rule, from) == brute;
        }
        Check("next recurrence date agrees with bounded day-by-day oracle across forty rules", nextCorrect);
        Check("month31 skips February and leap month29 occurs", Recurrence.Next(new() { Start = new(2028, 2, 1), Frequency = Frequency.Monthly, MonthDays = [31] }, new(2028, 2, 1)) == new DateOnly(2028, 3, 31) && Recurrence.Next(new() { Start = new(2028, 2, 1), Frequency = Frequency.Monthly, MonthDays = [29] }, new(2028, 2, 1)) == new DateOnly(2028, 2, 29));
        Check("next date handles maximum date without arithmetic overflow", Recurrence.Next(new() { Start = DateOnly.MaxValue, Frequency = Frequency.Weekly, Weekdays = [DateOnly.MaxValue.DayOfWeek], Interval = 10000 }, DateOnly.MaxValue) == DateOnly.MaxValue);
        var root = Todo("series", day); var a = Todo("child A", day) with { ParentId = root.Id }; var b = Todo("child B", day) with { ParentId = root.Id };
        Guid series = Guid.NewGuid(); await Run(w, new CreateSeries(new() { Start = day }, [root, a, b], series));
        TodoRef RefNode(Guid node, int offset) => TodoRef.For(new(series, node, day.AddDays(offset)));
        Check("creating series does not pregenerate any occurrence records", w.Snapshot!.Todos.Length == 0);
        await Run(w, new SetTodoCompleted(RefNode(root.Id, 0), true));
        Check("completion materializes only needed tree and leaves another day independent", w.Snapshot!.Todos.Length == 3 && PlannerQueries.Resolve(w.Snapshot, RefNode(root.Id, 2)).Status == TodoStatus.Pending);
        var special = PlannerQueries.Resolve(w.Snapshot!, RefNode(root.Id, 2)) with { Title = "custom instance", Description = "keep", Color = "#123456", SortKey = 777, ManualProgress = 30, ProgressMode = ProgressMode.Manual };
        await Run(w, new SaveTodo(special));
        await Run(w, new EditSeries(series, day, EditScope.EntireSeries, Rule: new() { Start = day, Interval = 2 }));
        var edited = PlannerQueries.Resolve(w.Snapshot!, RefNode(root.Id, 2));
        Check("rule-only changes retain instance title/color/manual progress/base order", edited.Title == "custom instance" && edited.Color == "#123456" && edited.ManualProgress == 30 && edited.SortKey == 777);
        var newTemplate = Todo("new template child", day) with { ParentId = root.Id };
        await Run(w, new EditSeries(series, day, EditScope.EntireSeries, Templates: [root, a, b, newTemplate]));
        Check("bulk tree edit excludes new virtual children from completed history", PlannerQueries.TodosOn(w.Snapshot!, day, day).Count(t => t.Origin?.SeriesId == series) == 3 && PlannerQueries.Resolve(w.Snapshot!, RefNode(root.Id, 0)).Status == TodoStatus.Completed && w.Snapshot!.Series.Single(s => s.Id == series).Cancelled.Contains(new(series, newTemplate.Id, day)));
        Check("pending future instances receive new template without losing explicit ordering", PlannerQueries.Children(w.Snapshot!, PlannerQueries.Resolve(w.Snapshot!, RefNode(root.Id, 2))).Length == 3 && PlannerQueries.Resolve(w.Snapshot!, RefNode(root.Id, 2)).SortKey == 777);
        var local = Todo("only this child", day.AddDays(2)); await Run(w, new AddSeriesTodo(local, RefNode(root.Id, 2), EditScope.Single));
        var localRef = RefNode(local.Id, 2);
        Check("one-off recurring child has stable identity but no future template", PlannerQueries.Resolve(w.Snapshot!, localRef).ParentId == RefNode(root.Id, 2).Id && !PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(4), day).Any(t => t.Title == local.Title) && w.Snapshot!.Series.Single(s => s.Id == series).RetiredNodeIds.Contains(local.Id));
        await Run(w, new CancelSeries(series, day.AddDays(2), EditScope.Single, local.Id));
        Check("single local child cancellation does not remove sibling or parent", !PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(2), day).Any(t => t.Id == localRef.Id) && PlannerQueries.Resolve(w.Snapshot!, RefNode(root.Id, 2)).Status == TodoStatus.Pending);
        await w.UndoAsync();
        Check("undo restores local exception without creating other dates", PlannerQueries.Resolve(w.Snapshot!, localRef).Title == local.Title && !PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(4), day).Any(t => t.Title == local.Title));
        var copied = await Run(w, new CopyTodo(RefNode(root.Id, 2), day.AddDays(20)));
        var copiedNodes = w.Snapshot!.Todos.Where(t => t.CopiedFromId != null).ToArray();
        Check("copy occurrence includes virtual and one-off children as ordinary reset subtree", copiedNodes.Length == 5 && copiedNodes.All(t => t.Kind == TodoKind.Once && t.Origin == null && t.Status == TodoStatus.Pending && t.CompletionSnapshot == null));
        await w.UndoAsync(); Check("copy is one undo transaction", !w.Snapshot!.Todos.Any(t => t.Id == copied)); await w.RedoAsync();
        edited = PlannerQueries.Resolve(w.Snapshot!, RefNode(root.Id, 2));
        await Run(w, new EditOccurrence(edited with { Title = "moved single", PlannedDate = day.AddDays(10) }, EditScope.Single));
        Check("single instance move retains original key and does not duplicate it", PlannerQueries.Resolve(w.Snapshot!, RefNode(root.Id, 2)).Origin!.Date == day.AddDays(2) && !PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(2), day).Any(t => t.Id == RefNode(root.Id, 2).Id) && PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(10), day).Count(t => t.Id == RefNode(root.Id, 2).Id) == 1);
        edited = PlannerQueries.Resolve(w.Snapshot!, RefNode(root.Id, 2)); await Run(w, new EditOccurrence(edited with { Title = "range edit" }, EditScope.EntireSeries));
        Check("all-unfinished content edit protects completed title and keeps pending ordering", PlannerQueries.Resolve(w.Snapshot!, RefNode(root.Id, 0)).Title == "series" && PlannerQueries.Resolve(w.Snapshot!, RefNode(root.Id, 4)).Title == "range edit" && PlannerQueries.Resolve(w.Snapshot!, RefNode(root.Id, 2)).SortKey == 777);
        await Run(w, new CancelSeries(series, day.AddDays(4), EditScope.FromDate));
        Check("from-date stop preserves earlier moved exception by original occurrence date", PlannerQueries.Resolve(w.Snapshot!, RefNode(root.Id, 2)).PlannedDate == day.AddDays(10) && !PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(6), day).Any(t => t.Origin?.SeriesId == series));
        await Run(w, new CancelSeries(series, day.AddDays(4), EditScope.EntireSeries));
        Check("all-unfinished cancellation includes earlier pending dates and retains completed history", !w.Snapshot!.Todos.Any(t => t.Origin?.SeriesId == series && t.Status != TodoStatus.Completed) && PlannerQueries.Resolve(w.Snapshot!, RefNode(root.Id, 0)).Status == TodoStatus.Completed);
        await w.UndoAsync(); Check("undo all-range cancellation restores stopped-series exceptions", PlannerQueries.Resolve(w.Snapshot!, RefNode(root.Id, 2)).Title == "range edit"); await w.RedoAsync();
        var p = Todo("partial history", day); var pa = Todo("done branch", day) with { ParentId = p.Id }; var pb = Todo("pending branch", day) with { ParentId = p.Id };
        var partial = await Run(w, new CreateSeries(new() { Start = day }, [p, pa, pb]));
        var done = TodoRef.For(new(partial, pa.Id, day)); await Run(w, new SetTodoCompleted(done, true));
        await Run(w, new CancelSeries(partial, day, EditScope.Single));
        var tree = PlannerQueries.TodoTreeOn(w.Snapshot!, day, day).Single(t => t.Value.Origin?.SeriesId == partial);
        Check("partial completed branch remains navigable through cancelled context ancestor", tree.ContextOnly && tree.Value.Status == TodoStatus.Cancelled && tree.Children.Single().Value.Id == done.Id && tree.Children[0].Value.Status == TodoStatus.Completed);
        var history = await Run(w, new SaveTodo(Todo("carry", day) with { Kind = TodoKind.CarryHistory })); clock = Now.AddDays(3);
        Check("history carry extends across days with one shared ID", Enumerable.Range(0, 4).All(i => PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(i), day.AddDays(3)).Any(t => t.Id == history)) && w.Snapshot!.Todos.Count(t => t.Id == history) == 1);
        await Run(w, new SetTodoCompleted(Ref(history), true));
        Check("completed history freezes and remains crossed on prior dates", Enumerable.Range(0, 4).All(i => PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(i), day.AddDays(10)).Any(t => t.Id == history && t.Status == TodoStatus.Completed)) && !PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(4), day.AddDays(10)).Any(t => t.Id == history));
        await Run(w, new SetCarryMode(Ref(history), false));
        Check("carry display-mode edit preserves shared identity and frozen completion", !PlannerQueries.TodosOn(w.Snapshot!, day, day.AddDays(10)).Any(t => t.Id == history) && PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(3), day.AddDays(10)).Any(t => t.Id == history && t.Status == TodoStatus.Completed));
        await w.UndoAsync(); await Run(w, new SetTodoCompleted(Ref(history), false)); await Run(w, new MoveCarryTodo(Ref(history), day.AddDays(12))); await Run(w, new SetCarryMode(Ref(history), false)); clock = Now.AddDays(10);
        Check("explicit future carry date appears once without earlier phantom copies", PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(12), day.AddDays(10)).Any(t => t.Id == history) && !PlannerQueries.TodosOn(w.Snapshot!, day.AddDays(10), day.AddDays(10)).Any(t => t.Id == history));
        await Run(w, new SetTodoCompleted(Ref(history), true));
        Check("early completion does not move future planned carry into past", w.Snapshot!.Todos.Single(t => t.Id == history).FrozenCarryDate == day.AddDays(12));
        await Run(w, new SetTodoCompleted(Ref(history), false));
        var futureDay = day.AddDays(40); await Run(w, new SaveTodo(Todo("future ordinary", futureDay)));
        var roots = PlannerQueries.TodosOn(w.Snapshot!, futureDay, day.AddDays(10)).Where(t => t.ParentId == null).Reverse().Select(TodoRef.Of).ToImmutableArray();
        await Run(w, new ReorderTodos(roots, futureDay, day.AddDays(10)));
        Check("mixed future sorting uses actual as-of date without phantom carried siblings", PlannerQueries.TodosOn(w.Snapshot!, futureDay, day.AddDays(10)).Where(t => t.ParentId == null).Select(t => t.Id).SequenceEqual(roots.Select(r => r.Id)));
        var carryCopy = await Run(w, new CopyTodo(Ref(history), day.AddDays(44)));
        Check("copying a carried task creates ordinary data without frozen/move state", w.Snapshot!.Todos.Single(t => t.Id == carryCopy) is Todo plain && plain.Kind == TodoKind.Once && plain.CarryDate == null && plain.FrozenCarryDate == null && plain.Moves.Length == 0 && plain.CopiedFromId == history);
        var dstDay = new DateOnly(2026, 10, 31); var dstRoot = Todo("DST planned clock", dstDay) with { PlannedTime = new(1, 30) };
        var dstSeries = await Run(w, new CreateSeries(new() { Start = dstDay, Count = 3, TimeZoneId = "Eastern Standard Time" }, [dstRoot]));
        var dstRows = Enumerable.Range(0, 3).SelectMany(i => PlannerQueries.TodosOn(w.Snapshot!, dstDay.AddDays(i), dstDay).Where(t => t.Origin?.SeriesId == dstSeries)).ToArray();
        Check("DST fallback has one stable occurrence per local date with floating planned time", dstRows.Length == 3 && dstRows.Select(t => t.Id).Distinct().Count() == 3 && dstRows.All(t => t.PlannedTime == new TimeOnly(1, 30)) && PlannerTime.Day(new(2026, 11, 1), "Eastern Standard Time").Duration.TotalHours == 25);
        var mixedRoot = Todo("legacy mixed parent", day); var mixedTemplate = Todo("template branch", day) with { ParentId = mixedRoot.Id };
        var mixedSeries = await Run(w, new CreateSeries(new() { Start = day }, [mixedRoot, mixedTemplate])); var mixedRef = TodoRef.For(new(mixedSeries, mixedRoot.Id, day.AddDays(1)));
        await Run(w, new SaveTodo(PlannerQueries.Resolve(w.Snapshot!, mixedRef)));
        var ordinaryPending = await Run(w, new SaveTodo(Todo("legacy ordinary pending", day.AddDays(1)) with { ParentId = mixedRef.Id }));
        var ordinaryDone = await Run(w, new SaveTodo(Todo("legacy ordinary done", day.AddDays(1)) with { ParentId = mixedRef.Id })); await Run(w, new SetTodoCompleted(Ref(ordinaryDone), true));
        await Run(w, new CancelSeries(mixedSeries, day.AddDays(1), EditScope.Single));
        Check("range cancellation handles legacy ordinary children without orphaning completed history", !w.Snapshot!.Todos.Any(t => t.Id == ordinaryPending) && w.Snapshot.Todos.Single(t => t.Id == ordinaryDone).Status == TodoStatus.Completed && w.Snapshot.Todos.Single(t => t.Id == mixedRef.Id).Status == TodoStatus.Cancelled);
        await w.UndoAsync(); await Run(w, new EditSeries(mixedSeries, day, EditScope.EntireSeries, Rule: new() { Start = day, Interval = 2 }));
        Check("rule exclusion handles legacy ordinary children and protected context", !w.Snapshot!.Todos.Any(t => t.Id == ordinaryPending) && w.Snapshot.Todos.Single(t => t.Id == ordinaryDone).Status == TodoStatus.Completed && w.Snapshot.Todos.Single(t => t.Id == mixedRef.Id).Status == TodoStatus.Cancelled);
        var before = PlannerCodec.Encode(w.Snapshot!); w = await PlannerWorkspace.OpenAsync(new(path), Utc, () => clock);
        Check("full P4 graph reload preserves keys/exclusions/history/one-off identities", before.SequenceEqual(PlannerCodec.Encode(w.Snapshot!)));
    }
}
