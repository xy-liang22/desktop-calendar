using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Threading;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static partial class StageThreeChecks
{
    static async Task RunP6Checks(MainWindow window)
    {
        var c = window.Session.Planner!; var selected = c.SelectedDate; var startPref = c.WeekStart; c.Select(new(2027, 1, 12), true); await c.SetWeekStartAsync(DayOfWeek.Sunday); var week = c.Week();
        Check("week preference aligns month and weekly timeline", PlannerView.MonthDates(c.Month, c.WeekStart)[0]?.DayOfWeek == DayOfWeek.Sunday && window.WeeklyTimeline.Dates[0] == week.Start);
        var t1 = c.NewTodo("GUI P6 todo A"); var t2 = c.NewTodo("GUI P6 todo B"); await c.ExecuteAsync(new SaveTodo(t1)); await c.ExecuteAsync(new SaveTodo(t2)); await c.ExecuteAsync(new SetTodoCompleted(TodoRef.Of(t1), true));
        var category = c.State!.Categories.Single(x => x.BuiltInKey == "work"); var origin = PlannerTime.Boundary(week.Start, week.TimeZoneId);
        Activity A(string title, int from, int to) => new() { Id = Guid.NewGuid(), Title = title, CategoryId = category.Id, TimeZoneId = week.TimeZoneId, Intervals = [new(Guid.NewGuid(), origin.AddHours(from), origin.AddHours(to))] };
        var a = A("GUI P6 actual A", 8, 12); var b = A("GUI P6 actual B", 10, 15); await c.ExecuteAsync(new SaveActivity(a)); await c.ExecuteAsync(new SaveActivity(b));
        File.WriteAllText(Path.Combine(DirectoryPath, "p6-before-editor.json"), System.Text.Json.JsonSerializer.Serialize(new { c.Writable, c.Busy, c.Stopping, c.MaintenancePending, Status = c.Storage?.Status.ToString(), c.NoticeKey, c.Diagnostic, Pending = c.Pending.Status.ToString() }));
        await DriveAsync(() => WeekGoalDialogs.Edit(window, c, week), async s =>
        {
            Field(s, "PlannerTitle").Text = "GUI P6 goal"; Choice(s, "GoalSource").SelectedIndex = (int)GoalSource.Todo; Field(s, "GoalTarget").Text = "2"; Field(s, "GoalUnit").Text = "项";
            Children<Expander>(s).Single().IsExpanded = true; await Idle(); foreach (var box in Children<CheckBox>(s).Where(x => x.Content?.ToString()?.Contains("GUI P6 todo") == true)) box.IsChecked = true;
            Image(s, "p6-goal-editor.png"); await s.SaveAndCloseAsync(false);
        });
        var goal = c.State!.Goals.Single(g => g.Title == "GUI P6 goal"); Check("goal editor persists one source and multiple todo links", goal.Todos.Length == 2 && goal.Source == GoalSource.Todo && PlannerQueries.Goal(c.State, goal).Automatic == 1);
        await DriveAsync(() => WeekGoalDialogs.Edit(window, c, week, goal), async s => { Field(s, "GoalOverride").Text = "3"; await s.SaveAndCloseAsync(false); }); window.SetPage(true); await Idle();
        Check("GUI manual progress is effective", PlannerQueries.Goal(c.State!, c.State!.Goals.Single(g => g.Id == goal.Id)).Override == 3);
        Click(Children<Button>(window).Single(x => Equals(x.Content, MainWindow.Text("GoalRestore")))); await c.Pending; Check("restore auto button clears override", c.State!.Goals.Single(g => g.Id == goal.Id).ManualOverride == null);
        await DriveAsync(() => WeekGoalDialogs.Edit(window, c, week, c.State!.Goals.Single(g => g.Id == goal.Id)), async s =>
        {
            Choice(s, "GoalKind").SelectedIndex = (int)GoalKind.Duration; Check("duration rejects todo source and retains draft", !await s.SaveAndCloseAsync(false) && s.IsVisible);
            Choice(s, "GoalSource").SelectedIndex = (int)GoalSource.Activity; Field(s, "GoalTarget").Text = "8"; Children<Expander>(s).Single().IsExpanded = true; await Idle(); foreach (var box in Children<CheckBox>(s).Where(x => x.Content?.ToString()?.Contains("GUI P6 actual") == true)) box.IsChecked = true; ApproveNext(s); await s.SaveAndCloseAsync(false);
        });
        goal = c.State!.Goals.Single(g => g.Id == goal.Id); Check("confirmed type change keeps links and merges actual hours", goal.Kind == GoalKind.Duration && goal.Target == 8 * 3600 && goal.Todos.Length == 2 && goal.Activities.Length == 2 && PlannerQueries.Goal(c.State, goal).Automatic == 7 * 3600);
        Check("weekly category statistics merge shared source", PlannerQueries.CategoryStatistics(c.State, week).Single(x => x.CategoryId == category.Id).Seconds == 7 * 3600);
        await DriveAsync(() => WeekGoalDialogs.Edit(window, c, week), async s => { Field(s, "PlannerTitle").Text = "GUI P6 check"; Choice(s, "GoalKind").SelectedIndex = (int)GoalKind.Check; await s.SaveAndCloseAsync(false); });
        var check = Children<CheckBox>(window).Single(x => x.Content?.ToString()?.Contains("GUI P6 check") == true); check.IsChecked = true; check.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, check)); await c.Pending; Check("weekly check goal toggles through panel", c.State!.Goals.Single(g => g.Title == "GUI P6 check").Checked);
        var goalCard = Children<Border>(window).Single(x => x.Tag is WeekGoal g && g.Id == goal.Id); Drive(() => { var item = goalCard.ContextMenu.Items.OfType<MenuItem>().Single(x => Equals(x.Header, MainWindow.Text("GoalDelete"))); item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item)); }, s => Click(s.Actions.Children.OfType<Button>().Last())); await c.Pending;
        Check("deleting goal retains linked records", !c.State!.Goals.Any(g => g.Id == goal.Id) && c.State.Activities.Any(x => x.Id == a.Id) && c.State.Todos.Any(x => x.Id == t1.Id)); await c.UndoAsync(); window.SetPage(false);
        await DriveAsync(() => window.PlannerView!.Edit(null), async outer =>
        {
            Field(outer, "PlannerTitle").Text = "GUI P6 nested todo"; Children<Expander>(outer).Single(x => Equals(x.Header, MainWindow.Text("ActivityGoal"))).IsExpanded = true; await Idle();
            _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () => { var inner = Application.Current.Windows.OfType<PlannerDialogs.Sheet>().Last(x => x != outer); Field(inner, "PlannerTitle").Text = "GUI P6 nested goal"; Choice(inner, "GoalSource").SelectedIndex = (int)GoalSource.Todo; await inner.SaveAndCloseAsync(false); }));
            Click(Children<Button>(Children<GoalLinks>(outer).Single()).Single(x => Equals(x.Content, MainWindow.Text("GoalNew")))); await outer.SaveAndCloseAsync(false);
        });
        var nested = c.State!.Todos.Single(t => t.Title == "GUI P6 nested todo"); Check("nested new goal links after todo parent saves", c.State.Goals.Single(g => g.Title == "GUI P6 nested goal").Todos.Any(r => r.Id == nested.Id));
        await DriveAsync(() => window.PlannerView!.Edit(null), async s => { Field(s, "PlannerTitle").Text = "GUI P6 linked series"; Choice(s, "RepeatType").SelectedIndex = 3; Children<Expander>(s).Single(x => Equals(x.Header, MainWindow.Text("ActivityGoal"))).IsExpanded = true; await Idle(); Children<CheckBox>(s).Single(x => x.Content?.ToString()?.Contains("GUI P6 nested goal") == true).IsChecked = true; await s.SaveAndCloseAsync(false); });
        var linkedSeries = c.State!.Series.Single(x => x.Versions.Any(v => v.Templates.Any(t => t.Title == "GUI P6 linked series"))); Check("new recurring todo goal links stable first occurrence rather than template", c.State.Goals.Single(g => g.Title == "GUI P6 nested goal").Todos.Any(r => r.Occurrence?.SeriesId == linkedSeries.Id));
        await DriveAsync(() => ActivityDialogs.Edit(window, c), async outer =>
        {
            Field(outer, "PlannerTitle").Text = "GUI P6 nested activity"; Children<Expander>(outer).Single().IsExpanded = true; await Idle();
            _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () => { var inner = Application.Current.Windows.OfType<PlannerDialogs.Sheet>().Last(x => x != outer); Field(inner, "PlannerTitle").Text = "GUI P6 activity goal"; Choice(inner, "GoalSource").SelectedIndex = (int)GoalSource.Activity; await inner.SaveAndCloseAsync(false); }));
            Click(Children<Button>(Children<GoalLinks>(outer).Single()).Single(x => Equals(x.Content, MainWindow.Text("GoalNew")))); await outer.SaveAndCloseAsync(false);
        });
        var activity = c.State!.Activities.Single(x => x.Title == "GUI P6 nested activity"); Check("nested new goal links saved activity", c.State.Goals.Single(g => g.Title == "GUI P6 activity goal").Activities.Contains(activity.Id));
        await DriveAsync(() => WeeklyReviewDialog.Edit(window, c, week), async s =>
        {
            Children<NoteEditor>(s).Single().Source.Text = "# My real week\nHuman reflection"; Click(Children<Button>(s).Single(x => Equals(x.Content, MainWindow.Text("ReviewAddSection")))); var section = Children<WeeklyReviewDialog.Section>(s).Single(); section.Heading.Text = "What I learned"; section.Body.Text = "My own conclusions";
            Click(Children<Button>(s).Single(x => Equals(x.Content, MainWindow.Text("ReviewInsertStats")))); await Task.Delay(260); await Idle(); var preview = Children<NoteEditor>(s).Single().Preview.Document; Check("review Markdown live preview renders after debounce", new TextRange(preview.ContentStart, preview.ContentEnd).Text.Contains("My real week")); Image(s, "p6-review-editor.png"); section.Heading.Text = ""; Check("invalid review section retains draft and pending statistics", !await s.SaveAndCloseAsync(false) && s.IsVisible); section.Heading.Text = "What I learned"; await s.SaveAndCloseAsync(false);
        });
        var review = c.State!.Reviews.Single(r => r.Week == week); Check("review saves prose sections and separate snapshot atomically", review.Markdown.Contains("Human reflection") && review.Sections.Single().Markdown == "My own conclusions" && review.Statistics.Length == 1 && c.State.Goals.Where(g => g.Week == week).All(g => g.ReviewId == review.Id));
        await DriveAsync(() => WeeklyReviewDialog.Edit(window, c, week), async s => { Click(Children<Button>(s).Single(x => Equals(x.Content, MainWindow.Text("ReviewRemoveSection")))); await s.SaveAndCloseAsync(false); }); Check("section removal preserves free prose and statistics", c.State!.Reviews.Single(r => r.Id == review.Id).Sections.Length == 0 && c.State.Reviews.Single(r => r.Id == review.Id).Statistics.Length == 1); await c.UndoAsync(); Check("section removal can undo with original stable section identity", c.State!.Reviews.Single(r => r.Id == review.Id).Sections.Single().Id == review.Sections.Single().Id);
        long originalRevision = c.State.Revision; await DriveAsync(() => WeeklyReviewDialog.Edit(window, c, week), async s => { Click(Children<Button>(s).Single(x => Equals(x.Content, MainWindow.Text("ReviewInsertStats")))); Click(s.Actions.Children.OfType<Button>().First()); await Idle(); }); Check("cancelling new statistics does not append or write review", c.State.Revision == originalRevision && c.State.Reviews.Single(r => r.Id == review.Id).Statistics.Length == 1);
        await DriveAsync(() => WeeklyReviewDialog.Edit(window, c, week), async s => { Children<NoteEditor>(s).Single().Source.Text += "\nEdited later"; await s.SaveAndCloseAsync(false); }); Check("review editing keeps snapshots unchanged", c.State!.Reviews.Single(r => r.Id == review.Id).Statistics.SequenceEqual(review.Statistics)); await c.UndoAsync(); Check("review prose undo leaves statistics intact", c.State!.Reviews.Single(r => r.Id == review.Id).Markdown == review.Markdown); await c.RedoAsync();
        var before = c.State!.Reviews.Single(r => r.Id == review.Id); await c.ExecuteAsync(new SaveActivity(a with { Intervals = [a.Intervals[0] with { End = a.Intervals[0].End.AddHours(5) }] }, false)); Check("statistics refresh does not overwrite review text", c.State!.Reviews.Single(r => r.Id == review.Id) == before && PlannerQueries.CategoryStatistics(c.State, week).Single(x => x.CategoryId == category.Id).Seconds == 9 * 3600);
        await DriveAsync(() => WeeklyReviewDialog.Edit(window, c, week), async s => { var notes = Children<NoteEditor>(s).Single(); notes.Source.Text = "must survive failure"; File.SetAttributes(c.FilePath, FileAttributes.ReadOnly); try { Check("review failure retains draft", !await s.SaveAndCloseAsync(false) && s.IsVisible && notes.Source.Text == "must survive failure"); } finally { File.SetAttributes(c.FilePath, FileAttributes.Normal); } Click(s.Actions.Children.OfType<Button>().First()); });
        await c.ReloadAsync(); Check("reload retains local timezone and selected week option", c.State!.Options.WeekStart == DayOfWeek.Sunday && c.State.Options.TimeZoneId == week.TimeZoneId);
        c.SetWeekLayout(WeekPlanLayout.SummaryStrip); window.SetPage(false); window.UpdateLayout(); await Idle(); Check("calendar has one expandable strip per week", Children<ToggleButton>(window.PlannerView!).Count(x => x.Tag is WeekKey) == PlannerView.MonthDates(c.Month, c.WeekStart).Length / 7); Image(window.Surface, "p6-month-strips.png");
        double width = window.Width, height = window.Height; window.Width = 880; window.Height = 520; window.UpdateLayout(); await Idle(); window.UpdateLayout(); Check("minimum six-week calendar preserves complete date headers", Children<CalendarPreviewCell>(window.PlannerView!).All(x => x.ActualHeight >= 21));
        Check("compact summary strip respects padding and readable line height", Children<ToggleButton>(window.PlannerView!).Where(x => x.Tag is WeekKey).All(x => x.Height - x.Padding.Top - x.Padding.Bottom >= x.FontSize * 1.2)); Image(window.Surface, "p6-minimum.png"); window.Width = width; window.Height = height; window.UpdateLayout(); await Idle();
        c.SetWeekLayout(WeekPlanLayout.SideColumn); window.UpdateLayout(); await Idle(); Image(window.Surface, "p6-month-side.png"); window.SetPage(true); await Idle(); Image(window.Surface, "p6-week-plan-stats.png");
        var night = new Activity { Id = Guid.NewGuid(), Title = "GUI P6 overnight anchor", TimeZoneId = week.TimeZoneId, Intervals = [new(Guid.NewGuid(), origin.AddHours(23), origin.AddHours(31))] }; await c.ExecuteAsync(new SaveActivity(night));
        window.WeeklyTimeline.ScrollToVerticalOffset(0); window.UpdateLayout(); await Idle(); var secondDay = window.WeeklyTimeline.Cards.Last(x => x.Slot.Segment?.ActivityId == night.Id); window.ActivityDetails!.Show(night.Id, night.Intervals[0].Id, secondDay.Slot.Segment); await Idle();
        var expectedAnchor = secondDay.Card.TransformToAncestor(window.Surface).TransformBounds(new Rect(secondDay.Card.RenderSize)); Check("cross-day detail anchors the clicked day fragment rather than first period match", window.ActivityDetails.IsOpen && Math.Abs(window.ActivityDetails.AnchorBounds.X - expectedAnchor.X) < 1); Image(window.Surface, "p6-crossday-anchor.png"); window.ActivityDetails.Hide();
        var weekly = Children<ActivityWeekView>(window).Single(); Click(Children<Button>(weekly).Single(x => Equals(x.Content, "›"))); Check("week navigation keeps selected date and goals aligned", c.Week().Start == week.Start.AddDays(7)); Click(Children<Button>(weekly).Single(x => Equals(x.Content, "‹")));
        var sideScroll = Children<ScrollViewer>(weekly).Single(x => x.Content is WeekSummaryView); sideScroll.ScrollToBottom(); await Idle(); Check("weekly read view includes free text and separate snapshots", Children<FlowDocumentScrollViewer>(sideScroll).Any(x => new TextRange(x.Document.ContentStart, x.Document.ContentEnd).Text.Contains("Edited later"))); Image(window.Surface, "p6-week-review-view.png");
        File.WriteAllLines(Path.Combine(DirectoryPath, "p6-ids.txt"), [goal.Id.ToString(), review.Id.ToString()]); await c.SetWeekStartAsync(startPref); Check("changing week start preserves original review identity", c.State!.Reviews.Single(r => r.Id == review.Id).Week == week); await c.SetWeekStartAsync(DayOfWeek.Sunday); c.Select(selected, true); c.SetWeekLayout(WeekPlanLayout.SummaryStrip); window.SetPage(false);
    }
}
