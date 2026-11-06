using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static partial class StageThreeChecks
{
    static ComboBox Choice(PlannerDialogs.Sheet sheet, string key) => Children<ComboBox>(sheet).Single(c => AutomationProperties.GetName(c) == MainWindow.Text(key));
    static void ApproveNext(PlannerDialogs.Sheet outer)
    {
        _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var confirmation = Application.Current.Windows.OfType<PlannerDialogs.Sheet>().LastOrDefault(s => s != outer);
            if (confirmation == null) { results.Add("FAIL missing scope confirmation"); return; }
            Check("broad scope requires a separate explicit confirmation", !confirmation.AutoSaveClose); Click(confirmation.Actions.Children.OfType<Button>().Last());
        }));
    }
    static async Task RunP4Checks(MainWindow window)
    {
        var c = window.Session.Planner!; var v = window.PlannerView!; var start = c.Today.AddDays(30); c.Select(start, true);
        await DriveAsync(() => v.Edit(null), async s =>
        {
            Field(s, "PlannerTitle").Text = "GUI periodic daily"; Field(s, "PlannerTime").Text = "09：45"; Choice(s, "RepeatType").SelectedIndex = 3; await Idle();
            var fields = Children<RecurrenceFields>(s).Single(); fields.IntervalInput.Text = "2"; fields.CountInput.Text = "3";
            fields.CountInput.Text = "-1"; Check("invalid recurrence creation retains the complete editor draft", !await s.SaveAndCloseAsync(false) && s.IsVisible && s.Error.Text == MainWindow.Text("RepeatInvalid")); fields.CountInput.Text = "3";
            await Idle(); Image(s, "p4-create-recurring.png"); Check("expanded recurring editor stays within monitor work area", NativeWindow.WorkArea(window.Handle).Contains(NativeWindow.Bounds(new System.Windows.Interop.WindowInteropHelper(s).Handle)));
            await s.SaveAndCloseAsync(false);
        });
        var series = c.State!.Series.Single(s => s.Versions.Any(version => version.Templates.Any(t => t.Title == "GUI periodic daily")));
        TodoRef Occ(int n) => TodoRef.For(new(series.Id, series.RootTodoId, start.AddDays(n)));
        Check("GUI creates interval/count/time rule without pre-generated tasks", c.State.Todos.All(t => t.Origin?.SeriesId != series.Id) && c.On(start.AddDays(2)).Any(t => t.Id == Occ(2).Id && t.PlannedTime == new TimeOnly(9, 45)) && !c.On(start.AddDays(6)).Any(t => t.Origin?.SeriesId == series.Id));
        c.Select(start, true); window.UpdateLayout();
        var first = Children<CheckBox>(v.Items).Single(x => AutomationProperties.GetName(x).EndsWith("GUI periodic daily", StringComparison.Ordinal)); first.IsChecked = true; first.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, first)); await c.Pending;
        Check("real recurring checkbox completes only selected occurrence", PlannerQueries.Resolve(c.State!, Occ(0)).Status == TodoStatus.Completed && PlannerQueries.Resolve(c.State!, Occ(2)).Status == TodoStatus.Pending);
        c.Select(start.AddDays(2), true); var current = PlannerQueries.Resolve(c.State!, Occ(2));
        await DriveAsync(() => v.Edit(null, current.Id), async s =>
        {
            Field(s, "PlannerTitle").Text = "GUI series child"; Choice(s, "RepeatScope").SelectedValue = EditScope.EntireSeries; ApproveNext(s); await s.SaveAndCloseAsync(false);
        });
        Check("GUI series child applies to pending dates and preserves completed history", c.On(start.AddDays(2)).Any(t => t.Title == "GUI series child") && !c.On(start).Any(t => t.Title == "GUI series child"));
        await DriveAsync(() => v.Edit(null, Occ(2).Id), async s =>
        {
            Field(s, "PlannerTitle").Text = "GUI one-off done child"; Choice(s, "RepeatType").SelectedIndex = 0; Children<CheckBox>(s).Single(x => Equals(x.Content, MainWindow.Text("PlannerCompleted"))).IsChecked = true; await s.SaveAndCloseAsync(false);
        });
        Check("GUI one-off child completion retains parent occurrence without repeating", c.On(start.AddDays(2)).Any(t => t.Title == "GUI one-off done child" && t.Status == TodoStatus.Completed && t.Origin == null && t.ParentId == Occ(2).Id) && !c.On(start.AddDays(4)).Any(t => t.Title == "GUI one-off done child"));
        await DriveAsync(() => v.Edit(PlannerQueries.Resolve(c.State!, Occ(2))), async s => { Field(s, "PlannerTitle").Text = "GUI single exception"; await s.SaveAndCloseAsync(false); });
        Check("single instance editing does not rename another date", PlannerQueries.Resolve(c.State!, Occ(2)).Title == "GUI single exception" && PlannerQueries.Resolve(c.State!, Occ(4)).Title == "GUI periodic daily");
        c.BeginInline(PlannerQueries.Resolve(c.State!, Occ(2)), start.AddDays(2)); window.UpdateLayout(); await Idle();
        var inline = Children<TextBox>(v).Single(t => AutomationProperties.GetName(t) == MainWindow.Text("CalendarInlineTitle")); inline.Text = "GUI inline instance"; SendKey(inline, System.Windows.Input.Key.Enter); await c.Pending; await Idle();
        Check("periodic calendar inline rename targets only one stable occurrence", PlannerQueries.Resolve(c.State!, Occ(2)).Title == "GUI inline instance" && PlannerQueries.Resolve(c.State!, Occ(4)).Title == "GUI periodic daily");
        Drive(() => PlannerDialogs.Delete(window, c, PlannerQueries.Resolve(c.State!, Occ(2))), s => Click(s.Actions.Children.OfType<Button>().Last())); await c.Pending;
        Check("GUI single cancellation excludes one date only", !c.On(start.AddDays(2)).Any(t => t.Id == Occ(2).Id) && c.On(start.AddDays(4)).Any(t => t.Id == Occ(4).Id)); await c.UndoAsync();
        await DriveAsync(() => v.Edit(PlannerQueries.Resolve(c.State!, Occ(2))), async s => { Field(s, "PlannerDescription").Text = "shared unfinished metadata"; Choice(s, "RepeatScope").SelectedValue = EditScope.EntireSeries; ApproveNext(s); await s.SaveAndCloseAsync(false); });
        Check("GUI all-unfinished edit preserves completed metadata", PlannerQueries.Resolve(c.State!, Occ(4)).Description == "shared unfinished metadata" && PlannerQueries.Resolve(c.State!, Occ(0)).Description == "");
        await DriveAsync(() => PlannerDialogs.EditRule(window, c, PlannerQueries.Resolve(c.State!, Occ(2))), async s =>
        { var fields = Children<RecurrenceFields>(s).Single(); fields.IntervalInput.Text = "3"; ApproveNext(s); await s.SaveAndCloseAsync(false); });
        Check("GUI future rule version changes cadence retaining full and partial completed history", c.On(start.AddDays(3)).Any(t => t.Origin?.SeriesId == series.Id) && c.On(start.AddDays(2)).Where(t => t.Origin?.SeriesId == series.Id).All(t => t.Status == TodoStatus.Completed) && c.On(start.AddDays(2)).Any(t => t.Title == "GUI one-off done child") && PlannerQueries.Resolve(c.State!, Occ(0)).Status == TodoStatus.Completed);
        c.Select(start.AddDays(20), true);
        await DriveAsync(() => v.Edit(null), async s =>
        {
            Field(s, "PlannerTitle").Text = "GUI weekly"; Choice(s, "RepeatType").SelectedIndex = 3; await Idle(); var fields = Children<RecurrenceFields>(s).Single(); fields.FrequencyBox.SelectedIndex = 1; fields.IntervalInput.Text = "2"; fields.CountInput.Text = "4";
            foreach (var day in Children<CheckBox>(fields).Where(x => x.Tag is DayOfWeek)) day.IsChecked = (DayOfWeek)day.Tag is DayOfWeek.Tuesday or DayOfWeek.Thursday;
            fields.EndEnabled.IsChecked = true; fields.Until.SelectedDate = start.AddDays(50).ToDateTime(TimeOnly.MinValue);
            await s.SaveAndCloseAsync(false);
        });
        var weekly = c.State!.Series.Single(s => s.Versions[0].Templates.Any(t => t.Title == "GUI weekly"));
        Check("weekly editor stores selected weekdays/N-week interval and optional inclusive end", weekly.Versions[0].Rule.Weekdays.SequenceEqual([DayOfWeek.Tuesday, DayOfWeek.Thursday]) && weekly.Versions[0].Rule.Interval == 2 && weekly.Versions[0].Rule.Until == start.AddDays(50));
        var firstWeek = Recurrence.Next(weekly.Versions[0].Rule, weekly.Versions[0].Rule.Start)!.Value;
        var secondWeek = Recurrence.Next(weekly.Versions[0].Rule, firstWeek.AddDays(1))!.Value; c.Select(secondWeek, true);
        var weeklySecond = c.On(secondWeek).Single(t => t.Origin?.SeriesId == weekly.Id);
        Drive(() => PlannerDialogs.Delete(window, c, weeklySecond), s => { Choice(s, "RepeatScope").SelectedValue = EditScope.FromDate; Click(s.Actions.Children.OfType<Button>().Last()); }); await c.Pending;
        Check("GUI from-date cancellation includes selected day and preserves earlier unfinished occurrence", c.On(firstWeek).Any(t => t.Origin?.SeriesId == weekly.Id) && !c.On(secondWeek).Any(t => t.Origin?.SeriesId == weekly.Id));
        c.Select(new(2028, 2, 1), true);
        await DriveAsync(() => v.Edit(null), async s =>
        {
            Field(s, "PlannerTitle").Text = "GUI monthly leap"; Choice(s, "RepeatType").SelectedIndex = 3; await Idle(); var fields = Children<RecurrenceFields>(s).Single(); fields.FrequencyBox.SelectedIndex = 2; fields.DaysInput.Text = "29，31"; fields.CountInput.Text = "3"; await s.SaveAndCloseAsync(false);
        });
        var monthly = c.State!.Series.Single(s => s.Versions[0].Templates.Any(t => t.Title == "GUI monthly leap")); var leapRef = TodoRef.For(new(monthly.Id, monthly.RootTodoId, new(2028, 2, 29)));
        Check("monthly editor skips missing day and navigates to leap-day occurrence", c.SelectedDate == new DateOnly(2028, 2, 29) && c.On(c.SelectedDate).Any(t => t.Id == leapRef.Id) && !c.On(new(2028, 4, 29)).Any(t => t.Origin?.SeriesId == monthly.Id));
        window.UpdateLayout(); Image(window.Surface, "p4-leap-month.png"); Check("calendar displays periodic marker at trailing edge", Children<TextBlock>(v).Any(t => t.Text == "↻" && DockPanel.GetDock(t) == Dock.Right));
        Drive(() => PlannerDialogs.Copy(window, c, PlannerQueries.Resolve(c.State!, leapRef)), s => { Children<DatePicker>(s).Single().SelectedDate = new DateTime(2028, 3, 10); Click(s.Actions.Children.OfType<Button>().Last()); }); await c.Pending;
        Check("GUI occurrence copy is independent ordinary data", c.State!.Todos.Any(t => t.CopiedFromId == leapRef.Id && t.Kind == TodoKind.Once && t.PlannedDate == new DateOnly(2028, 3, 10)));
        v.Quick.Text = "mixed ordinary"; await v.AddQuickAsync(); var ordinary = c.On(c.SelectedDate).Single(t => t.Title == "mixed ordinary");
        Check("mixed drop handler supports virtual recurring and ordinary siblings", await v.DropAsync(ordinary, PlannerQueries.Resolve(c.State!, leapRef), false) && c.On(c.SelectedDate).Where(t => t.ParentId == null).First().Id == ordinary.Id);
        c.Select(c.Today.AddDays(-4), true);
        await DriveAsync(() => v.Edit(null), async s => { Field(s, "PlannerTitle").Text = "GUI new latest"; Choice(s, "RepeatType").SelectedIndex = 2; await s.SaveAndCloseAsync(false); });
        var latest = c.State!.Todos.Single(t => t.Title == "GUI new latest");
        Check("new latest carry starting in past selects the actual visible day", c.SelectedDate == c.Today && c.On(c.Today).Any(t => t.Id == latest.Id) && !c.On(c.Today.AddDays(-4)).Any(t => t.Id == latest.Id));
        c.Select(c.Today.AddDays(-2), true);
        await DriveAsync(() => v.Edit(null), async s => { Field(s, "PlannerTitle").Text = "GUI carried"; Field(s, "PlannerTime").Text = "15:00"; Choice(s, "RepeatType").SelectedIndex = 1; await s.SaveAndCloseAsync(false); });
        var carry = c.State!.Todos.Single(t => t.Title == "GUI carried");
        Check("GUI creates one shared history-carry task across dates", c.On(c.Today).Any(t => t.Id == carry.Id) && c.On(c.Today.AddDays(-2)).Any(t => t.Id == carry.Id));
        c.Select(c.Today, true); c.BeginInline(carry, c.Today); window.UpdateLayout(); await Idle();
        Check("history carry opens only one inline box despite repeated date cells", Children<TextBox>(v).Count(t => AutomationProperties.GetName(t) == MainWindow.Text("CalendarInlineTitle")) == 1); c.CancelInline();
        var check = window.CompactRealList.Children.OfType<CheckBox>().Single(x => AutomationProperties.GetName(x) == carry.Title); check.IsChecked = true; check.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, check)); await c.Pending;
        Check("compact completion shares frozen carry status with month and day", c.On(c.Today.AddDays(-1)).Any(t => t.Id == carry.Id && t.Status == TodoStatus.Completed) && !c.On(c.Today.AddDays(1)).Any(t => t.Id == carry.Id));
        await DriveAsync(() => v.Edit(c.State!.Todos.Single(t => t.Id == carry.Id)), async s => { Choice(s, "RepeatType").SelectedIndex = 1; await s.SaveAndCloseAsync(false); });
        Check("GUI carry mode switch retains visible day, identity/completion and removes old dates", c.SelectedDate == c.Today && c.State!.Todos.Single(t => t.Id == carry.Id).Kind == TodoKind.CarryLatest && !c.On(c.Today.AddDays(-1)).Any(t => t.Id == carry.Id));
        await c.ExecuteAsync(new SetTodoCompleted(new(carry.Id), false));
        Drive(() => PlannerDialogs.MoveCarry(window, c, c.State!.Todos.Single(t => t.Id == carry.Id)), s => { Children<DatePicker>(s).Single().SelectedDate = c.Today.AddDays(3).ToDateTime(TimeOnly.MinValue); Click(s.Actions.Children.OfType<Button>().Last()); }); await c.Pending;
        Check("GUI explicit carry move preserves shared ID and latest display", c.On(c.Today.AddDays(3)).Any(t => t.Id == carry.Id) && !c.On(c.Today).Any(t => t.Id == carry.Id));
        var ordinaryParent = c.State!.Todos.Single(t => t.Title == "must not auto save via auxiliary popup");
        await DriveAsync(() => v.Edit(ordinaryParent), async s => { Children<CheckBox>(s).Single(x => Equals(x.Content, MainWindow.Text("PlannerCompleted"))).IsChecked = true; await s.SaveAndCloseAsync(false); });
        Check("ordinary full-editor completion cascades with durable snapshot", c.State!.Todos.Single(t => t.Id == ordinaryParent.Id).CompletionSnapshot != null && PlannerQueries.Children(c.State, c.State.Todos.Single(t => t.Id == ordinaryParent.Id)).All(t => t.Status == TodoStatus.Completed));
        await DriveAsync(() => v.Edit(c.State!.Todos.Single(t => t.Id == ordinaryParent.Id)), async s => { Children<CheckBox>(s).Single(x => Equals(x.Content, MainWindow.Text("PlannerCompleted"))).IsChecked = false; await s.SaveAndCloseAsync(false); });
        Check("ordinary full-editor cancellation restores original child state", c.State!.Todos.Single(t => t.Id == ordinaryParent.Id).Status == TodoStatus.Pending && PlannerQueries.Children(c.State, c.State.Todos.Single(t => t.Id == ordinaryParent.Id)).All(t => t.Status == TodoStatus.Pending));
        var stop = c.On(start.AddDays(3)).Single(t => t.Origin?.SeriesId == series.Id && t.ParentId == null);
        Drive(() => PlannerDialogs.Delete(window, c, stop), s => { Choice(s, "RepeatScope").SelectedValue = EditScope.EntireSeries; Click(s.Actions.Children.OfType<Button>().Last()); }); await c.Pending;
        Check("GUI all-unfinished stop retains completed occurrence and archives series", c.State!.Series.Single(s => s.Id == series.Id).Archived && PlannerQueries.Resolve(c.State, Occ(0)).Status == TodoStatus.Completed);
        c.Select(c.Today, true); File.WriteAllLines(Path.Combine(DirectoryPath, "p4-ids.txt"), [series.Id.ToString(), leapRef.Id.ToString(), carry.Id.ToString()]);
    }
}
