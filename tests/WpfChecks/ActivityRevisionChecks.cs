using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static partial class StageThreeChecks
{
    static async Task RunActivityRevisionChecks(MainWindow window)
    {
        var c = window.Session.Planner!; var oldZone = c.DisplayTimeZoneId; var day = new DateOnly(2028, 2, 15); c.Select(day, true); window.SetPage(false);
        await c.SetTimeZoneAsync("China Standard Time");
        var parent = c.NewTodo("TZ hierarchy parent") with { PlannedTime = new(10, 0) }; await c.ExecuteAsync(new SaveTodo(parent));
        var child = c.NewTodo("TZ hierarchy child", parent.Id); await c.ExecuteAsync(new SaveTodo(child));
        var unrelated = c.NewTodo("TZ not this day") with { PlannedDate = day.AddDays(5) }; await c.ExecuteAsync(new SaveTodo(unrelated));
        await c.ExecuteAsync(new SetTodoCompleted(TodoRef.Of(child), true));
        var choices = ActivityTodoChoices.On(c, day, null); int rootIndex = Array.FindIndex(choices, x => x.Reference?.Id == parent.Id);
        Check("activity association picker filters date and keeps child hierarchy", choices.All(x => x.Reference?.Id != unrelated.Id) && choices[rootIndex + 1].Reference?.Id == child.Id && choices[rootIndex + 1].Name.StartsWith("　✓ "));
        Check("linked off-day task remains explicit without silently unlinking", ActivityTodoChoices.On(c, day, TodoRef.Of(unrelated)).Last().Reference?.Id == unrelated.Id);
        var start = PlannerTime.FromLocal(day.ToDateTime(new TimeOnly(10, 5), DateTimeKind.Unspecified), "China Standard Time");
        var activity = new Activity { Id = Guid.NewGuid(), Title = "TIMEZONE PREVIEW", Emoji = "🗓️", Reaction = "😊", Summary = "Inline summary wraps only when it needs more space", TimeZoneId = "China Standard Time", Intervals = [new(Guid.NewGuid(), start, start.AddMinutes(40))] };
        await c.ExecuteAsync(new SaveActivity(activity)); await Idle();
        var card = window.DailyTimeline.Cards.Single(x => x.Slot.Segment?.ActivityId == activity.Id).Card;
        var labels = Children<EmojiTextBlock>(card).ToArray();
        Check("activity title includes reaction while time and summary share one label", labels.Length == 2 && labels[0].Text.EndsWith(" · 😊") && labels[1].Text.StartsWith("10:05-10:45 · ") && !labels[1].Text.Contains("+08:00"));
        window.DailyTimeline.ScrollToVerticalOffset(9.7 * window.DailyTimeline.HourHeight); await Idle(); Image(window.Surface, "revision-activity-inline.png");
        await c.SetTimeZoneAsync("UTC"); await Idle();
        Check("timezone setting converts both timelines from same stored activity", window.DailyTimeline.Cards.Single(x => x.Slot.Segment?.ActivityId == activity.Id).Slot.Start == 2 + 5 / 60.0 && window.WeeklyTimeline.Cards.Any(x => x.Slot.Segment?.ActivityId == activity.Id && x.Slot.Start == 2 + 5 / 60.0));
        Check("todo display converts without exposing zone or modifying source", PlannerView.Prefix(parent, c.State!, day) == "02:00 " && c.State!.Todos.Single(t => t.Id == parent.Id).PlannedTime == new TimeOnly(10, 0));
        var crossing = c.NewTodo("TZ edit cross date") with { PlannedTimeZoneId = "China Standard Time", PlannedTime = new(1, 0) }; await c.ExecuteAsync(new SaveTodo(crossing));
        crossing = c.State!.Todos.Single(t => t.Id == crossing.Id);
        await DriveAsync(() => PlannerDialogs.Edit(window, c, crossing), async s =>
        {
            Check("todo editor loads the source timezone and source clock", TimeZoneChoices.Value(Choice(s, "TodoTimeZone")) == "China Standard Time" && Field(s, "PlannerTime").Text == "01:00");
            Field(s, "PlannerTitle").Text = "TZ edit cross date saved"; await s.SaveAndCloseAsync(false); Check("zoned todo editor commits valid fields and closes", !s.IsVisible);
        });
        Check("saving zoned task selects converted day without losing source clock", c.SelectedDate == day.AddDays(-1) && c.On(c.SelectedDate).Any(t => t.Id == crossing.Id) && c.State!.Todos.Single(t => t.Id == crossing.Id).PlannedTime == new TimeOnly(1, 0));
        c.Select(day, true);
        await DriveAsync(() => ActivityDialogs.Edit(window, c, activity), async s =>
        {
            Check("activity editor has direct shared category manager action", Children<Button>(s).Any(b => Equals(b.Content, MainWindow.Text("PlannerManageCategories"))));
            var date = Children<DatePicker>(s).Single(p => System.Windows.Automation.AutomationProperties.GetName(p) == MainWindow.Text("ActivityTodoDate"));
            Check("association date defaults to activity day in display zone", DateOnly.FromDateTime(date.SelectedDate!.Value) == day);
            date.SelectedDate = day.AddDays(5).ToDateTime(TimeOnly.MinValue); await Idle();
            var linked = Choice(s, "ActivityTodo"); Check("changing link date loads only chosen day's tasks", linked.Items.Cast<ActivityDialogs.TodoChoice>().Any(x => x.Reference?.Id == unrelated.Id) && linked.Items.Cast<ActivityDialogs.TodoChoice>().All(x => x.Reference?.Id != parent.Id));
            var notes = Children<NoteEditor>(s).Single(); var outer = Children<ScrollViewer>(s).First();
            notes.Source.Text = string.Join("\n", Enumerable.Range(1, 90).Select(i => "scroll draft " + i)); await Idle();
            outer.ScrollToVerticalOffset(outer.ScrollableHeight / 2); notes.Source.ScrollToEnd(); await Idle();
            var inner = Children<ScrollViewer>(notes.Source).Single(); double before = outer.VerticalOffset;
            Check("notes at bottom hands wheel to editor", ScrollChaining.Route(inner, outer, -120)); await Idle();
            Check("outer editor actually scrolls without losing notes", outer.VerticalOffset > before && notes.Source.Text.Contains("scroll draft 90"));
            inner.ScrollToTop(); outer.ScrollToVerticalOffset(outer.ScrollableHeight / 2); await Idle(); before = outer.VerticalOffset;
            ScrollChaining.Route(inner, outer, -120); await Idle(); Check("notes with room scrolls internally instead of outer", inner.VerticalOffset > 0 && Math.Abs(outer.VerticalOffset - before) < .1);
            Image(s, "revision-activity-notes-scroll.png"); Click(s.Actions.Children.OfType<Button>().First());
        });
        Check("cancel activity editor retains original notes", c.State!.Activities.Single(a => a.Id == activity.Id).Notes.Text == "");
        await DriveAsync(() => PlannerDialogs.ManageCategories(window, c), async s =>
        {
            var save = Children<Button>(s.Body).Single(b => Equals(b.Content, MainWindow.Text("PlannerSave")));
            Field(s, "PlannerCategoryName").Text = "Revision category one"; Click(save); await c.Pending; await Idle();
            Check("category create saves without closing manager", s.IsVisible && c.State!.Categories.Any(x => x.Name == "Revision category one"));
            Click(Children<Button>(s.Body).Single(b => Equals(b.Content, MainWindow.Text("PlannerNewCategory")))); Field(s, "PlannerCategoryName").Text = "Revision category two";
            File.SetAttributes(c.FilePath, FileAttributes.ReadOnly);
            try { Click(save); await c.Pending; await Idle(); Check("category failed save retains draft and existing committed category", s.IsVisible && Field(s, "PlannerCategoryName").Text == "Revision category two" && !c.State!.Categories.Any(x => x.Name == "Revision category two")); }
            finally { File.SetAttributes(c.FilePath, FileAttributes.Normal); }
            Click(save); await c.Pending; await Idle(); Check("category retry commits second item in same window", c.State!.Categories.Any(x => x.Name == "Revision category two"));
            Field(s, "PlannerCategoryName").Text = "discard this final edit"; Check("unsaved category draft does not retain saved success message", s.Error.Text != MainWindow.Text("CategorySaved")); Image(s, "revision-category-batch.png"); s.Close();
        });
        Check("closing category manager retains both saved edits only", c.State!.Categories.Any(x => x.Name == "Revision category one") && c.State.Categories.Any(x => x.Name == "Revision category two") && !c.State.Categories.Any(x => x.Name == "discard this final edit"));
        await DriveAsync(() => ApplicationSettings.TimeZone(window), async s =>
        {
            var combo = Choice(s, "DisplayTimeZone"); combo.SelectedValue = "Tokyo Standard Time"; await Idle(); Image(s, "revision-timezone-settings.png"); await s.SaveAndCloseAsync(false);
        });
        Check("timezone settings save to separate window preferences", window.SnapshotPreferences().DisplayTimeZoneId == "Tokyo Standard Time" && c.State!.Options.TimeZoneId == "Tokyo Standard Time");
        var fresh = new PlannerController(c.FilePath, window.SnapshotPreferences()); await fresh.InitializeAsync();
        Check("fresh controller restores selected zone and same data", fresh.DisplayZone == "Tokyo Standard Time" && fresh.State!.Activities.Single(a => a.Id == activity.Id).Intervals[0].Start == start); fresh.Dispose();
        await DriveAsync(() => ApplicationSettings.TimeZone(window), async s => { Choice(s, "DisplayTimeZone").SelectedValue = "UTC"; Click(s.Actions.Children.OfType<Button>().First()); await Idle(); });
        Check("cancel timezone selection does not change preference", c.DisplayZone == "Tokyo Standard Time");
        try { await c.SetTimeZoneAsync("DesktopCalendar invalid zone fixture"); Check("invalid display zone is rejected", false); }
        catch (PlannerException) { Check("invalid display zone preserves preference and healthy pending state", c.DisplayZone == "Tokyo Standard Time" && c.Pending.IsCompletedSuccessfully); }
        foreach (string zone in new[] { "Dateline Standard Time", "Line Islands Standard Time" })
        {
            await c.SetTimeZoneAsync(zone); window.EnterCompact(); await Idle();
            Check("compact date follows selected display zone " + zone, window.CompactDate.Text == c.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            window.RestoreNormal(); await Idle();
            Check("restore selects display today and matching fixed week anchor " + zone, c.SelectedDate == c.Today && window.Appearance.FixedWeekStart == CalendarViewport.TodayAnchor(c.Today, window.Appearance.FixedWeekRows));
        }
        await c.SetTimeZoneAsync(oldZone); c.Select(day, true);
        Check("almanac has visible source and suitable avoid detail", CalendarPresentation.Detail(window.Session.Calendar, day).Contains(MainWindow.Text("AlmanacSuitable")) && CalendarPresentation.Detail(window.Session.Calendar, day).Contains("1.6.8"));
        foreach (var lang in new[] { "zh-CN", "en" })
        {
            Localization.Apply(lang);
            await DriveAsync(() => ApplicationSettings.DateDetails(window, day), async s => { await Idle(); Image(s, "revision-almanac-" + lang + ".png"); s.Close(); });
        }
        Check("almanac English glossary covers upstream vocabulary", typeof(Lunar.Util.LunarUtil).GetField("YI_JI", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!.GetValue(null) is string[] terms && terms.All(t => CalendarPresentation.AlmanacTerm(t) != t));
        File.WriteAllLines(Path.Combine(DirectoryPath, "category-order-ids.txt"), PlannerQueries.OrderedCategories(c.State!).Select(x => x.Id.ToString()));
    }
}
