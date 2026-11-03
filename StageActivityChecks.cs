using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static partial class StageThreeChecks
{
    static async Task RunP5Checks(MainWindow window)
    {
        var c = window.Session.Planner!; var savedDate = c.SelectedDate; var day = new DateOnly(2027, 1, 5); c.Select(day, true); window.SetPage(false); await Idle();
        Check("real activity timelines start empty without demo blocks", window.DailyTimeline.Cards.Count == 0 && window.WeeklyTimeline.Cards.Count == 0);
        var marked = new CalendarTaskPanel([new("long task text", "09:30 ", false, "#62A9E8", false, () => { }, HasChildren: true, StatusMark: "↻")], _ => { }, false, () => { }); marked.Measure(new Size(52, 48)); marked.Arrange(new Rect(0, 0, 52, 48));
        Check("calendar trailing markers reserve space after text without shifting checkboxes", Children<Button>(marked).Single().Content?.ToString() == "‹" && Children<TextBlock>(marked).Any(t => t.Text == "↻" && DockPanel.GetDock(t) == Dock.Right && t.ActualWidth >= 14) && Children<TextBlock>(marked).Any(t => t.Text == "09:30 long task text"));
        var todo = c.NewTodo("GUI unfinished associated"); await c.ExecuteAsync(new SaveTodo(todo));
        var goal = new WeekGoal { Title = "GUI existing goal", Week = PlannerTime.Week(day, DayOfWeek.Monday, c.State!.Options.TimeZoneId), Kind = GoalKind.Duration, Source = GoalSource.Activity, Target = 3600 };
        await c.ExecuteAsync(new SaveGoal(goal)); goal = c.State!.Goals.Single(g => g.Title == goal.Title);
        var pixels = Enumerable.Repeat((byte)180, 16 * 12 * 4).ToArray(); var bitmap = BitmapSource.Create(16, 12, 96, 96, PixelFormats.Bgra32, null, pixels, 16 * 4); bitmap.Freeze();
        Guid assetId = Guid.Empty;
        await DriveAsync(() => ActivityDialogs.Edit(window, c, draft: ActivityDialogs.Draft(c, todo: todo)), async s =>
        {
            Field(s, "PlannerTitle").Text = "GUI activity sleep"; Field(s, "ActivitySummary").Text = "two real periods"; Field(s, "ActivityZone").Text = "UTC";
            Choice(s, "ActivityEmoji").Text = "😴"; Choice(s, "ActivityReaction").Text = "😊";
            var period = Children<PeriodFields>(s).Single(); period.Start.Text = "2027-01-05 23:00"; period.End.Text = "2027-01-06 07:00"; period.StartOffset.Text = period.EndOffset.Text = "+00:00";
            Click(Children<Button>(s).Single(b => Equals(b.Content, MainWindow.Text("ActivityAddPeriod")))); await Idle(); var second = Children<PeriodFields>(s).Last(); second.Start.Text = "2027-01-06 14:00"; second.End.Text = "2027-01-06 16:00"; second.StartOffset.Text = second.EndOffset.Text = "+00:00";
            Children<Expander>(s).Single(e => Equals(e.Header, MainWindow.Text("ActivityGoal"))).IsExpanded = true; await Idle();
            Children<CheckBox>(s).Single(b => b.Content?.ToString()?.Contains(goal.Title) == true).IsChecked = true;
            var notes = Children<NoteEditor>(s).Single(); notes.Format.SelectedIndex = (int)NoteFormat.Markdown;
            notes.Source.Text = "# Created with notes\n- item\n> quote\n```cs\nvar x = 1;\n```\n| A | B |\n| --- | --- |\n| 1 | 2 |\n[link](https://example.invalid)\n![remote](https://example.invalid/a.png)\n<script>alert(1)</script>\n";
            notes.Source.CaretIndex = notes.Source.Text.Length; var imported = notes.Images.Import(bitmap); assetId = imported.Id; notes.Insert(imported); var again = notes.Images.Import(bitmap);
            Check("image imports deduplicate validated bytes and use relative references", again.Id == imported.Id && notes.Source.Text.Contains("assets/images/") && !notes.Source.Text.Contains("base64"));
            Check("markdown renders table code and local image without executing HTML or fetching remote", notes.Preview.Document.Blocks.OfType<Table>().Any() && notes.Preview.Document.Blocks.OfType<Paragraph>().SelectMany(p => p.Inlines.OfType<InlineUIContainer>()).Any(i => i.Child is System.Windows.Controls.Image) && new TextRange(notes.Preview.Document.ContentStart, notes.Preview.Document.ContentEnd).Text.Contains("<script>") && new TextRange(notes.Preview.Document.ContentStart, notes.Preview.Document.ContentEnd).Text.Contains(MainWindow.Text("ActivityMissingImage")));
            await Idle(); Check("emoji custom text has an actual editable visible template part", Choice(s, "ActivityEmoji").Template.FindName("PART_EditableTextBox", Choice(s, "ActivityEmoji")) is TextBox edit && edit.IsVisible && edit.Text == "😴"); Image(s, "p5-activity-editor.png"); Children<ScrollViewer>(s).First().ScrollToBottom(); await Idle(); Image(s, "p5-notes-editor.png");
            Children<ScrollViewer>(notes.Preview).First().ScrollToBottom(); await Idle(); Image(s, "p5-local-image-preview.png");
            second.End.Text = "invalid"; Check("invalid precise activity time keeps full draft open", !await s.SaveAndCloseAsync(false) && s.IsVisible && notes.Source.Text.Contains("Created with notes")); second.End.Text = "2027-01-06 16:00";
            await s.SaveAndCloseAsync(false);
        });
        var activity = c.State!.Activities.Single(a => a.Title == "GUI activity sleep"); File.WriteAllText(Path.Combine(DirectoryPath, "p5-activity-id.txt"), activity.Id.ToString());
        Check("activity editor commits all periods notes emoji reaction and optional links", activity.Intervals.Length == 2 && activity.Notes.AssetIds.Contains(assetId) && activity.Todo?.Id == todo.Id && activity.Emoji == "😴" && activity.Reaction == "😊" && c.State.Goals.Single(g => g.Id == goal.Id).Activities.Contains(activity.Id));
        Check("recording actual time does not complete associated todo", c.State.Todos.Single(t => t.Id == todo.Id).Status == TodoStatus.Pending);
        var savedAsset = c.State.Assets.Single(a => a.Id == assetId); var imagePath = Path.Combine(c.DataDirectory, savedAsset.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        await c.UndoAsync(); Check("undo new activity and asset registration retains safe image bytes for redo", !c.State!.Activities.Any(a => a.Id == activity.Id) && !c.State.Assets.Any(a => a.Id == assetId) && File.Exists(imagePath)); await c.RedoAsync();
        var subsecond = new ActivityPeriod(Guid.NewGuid(), activity.Intervals[0].Start.AddTicks(1234567), activity.Intervals[0].End.AddTicks(2345678)); var precise = new PeriodFields(subsecond, "UTC", () => { }); Check("editing metadata preserves unedited subsecond period precision", precise.Read("UTC") == subsecond);
        // Current UI zone may differ from UTC. Compare the exact core display query.
        Check("daily and weekly real cards use shared period identities", window.DailyTimeline.Cards.All(p => p.Slot.Segment != null) && window.WeeklyTimeline.Cards.Any(p => p.Slot.Segment?.ActivityId == activity.Id));
        var zone = c.State.Options.TimeZoneId; var visibleDate = PlannerTime.LocalDate(activity.Intervals[0].Start, zone); c.Select(visibleDate, true); await Idle();
        var timeline = window.DailyTimeline; var segment = timeline.Cards.First(x => x.Slot.Segment?.ActivityId == activity.Id).Slot.Segment!; var pointer = visibleDate.ToDateTime(new TimeOnly(12, 0)); var before = Digest(c.State);
        timeline.BeginGesture(segment, pointer, "move"); Check("drag preview does not persist before release", timeline.PreviewGesture(pointer.AddHours(1)) && Digest(c.State) == before && timeline.PreviewPeriod!.Duration == activity.Intervals[0].Duration); timeline.CancelGesture(); Check("escape or capture-loss cancellation preserves persisted data", timeline.CurrentGesture == null && Digest(c.State) == before);
        timeline.BeginGesture(segment, pointer, "move"); timeline.PreviewGesture(pointer.AddHours(1)); Check("drag release saves one undoable move transaction", await timeline.CommitGestureAsync());
        var moved = c.State!.Activities.Single(a => a.Id == activity.Id); Check("move changes selected period only and synchronizes both timelines", moved.Intervals[0].Start == activity.Intervals[0].Start.AddHours(1) && moved.Intervals[1] == activity.Intervals[1] && window.WeeklyTimeline.Cards.Any(x => x.Slot.Segment?.PeriodId == moved.Intervals[0].Id));
        await c.UndoAsync(); segment = timeline.Cards.First(x => x.Slot.Segment?.ActivityId == activity.Id).Slot.Segment!;
        timeline.BeginGesture(segment, pointer, "end"); timeline.PreviewGesture(pointer.AddHours(-1)); await timeline.CommitGestureAsync(); Check("resize release preserves start and adjusts only selected end", c.State!.Activities.Single(a => a.Id == activity.Id).Intervals[0].End == activity.Intervals[0].End.AddHours(-1)); await c.UndoAsync();
        timeline.BeginGesture(segment, pointer, "move"); timeline.PreviewGesture(pointer.AddMinutes(30)); var unchanged = Digest(c.State!); File.SetAttributes(c.FilePath, FileAttributes.ReadOnly);
        try { Check("failed drag save retains retryable preview without changing persisted state", !await timeline.CommitGestureAsync() && timeline.FailedGesture && timeline.PreviewPeriod != null && Digest(c.State!) == unchanged); }
        finally { File.SetAttributes(c.FilePath, FileAttributes.Normal); }
        Check("retained drag preview can retry as one transaction", await timeline.CommitGestureAsync()); await c.UndoAsync();
        double hour = timeline.HourHeight; timeline.UpdateLayout(); double anchor = (timeline.VerticalOffset + 90) / hour; timeline.Wheel(120, true, 90); Check("real activity scale aligns cards and keeps mouse time anchor", timeline.Cards.All(x => Math.Abs(Canvas.GetTop(x.Card) - x.Slot.Start * timeline.HourHeight) < .01 && Math.Abs(x.Card.Height - (x.Slot.End - x.Slot.Start) * timeline.HourHeight) < .01) && Math.Abs((timeline.VerticalOffset + 90) / timeline.HourHeight - anchor) < .02);
        Check("ordinary wheel remains scrolling and pages retain independent scales", !timeline.Wheel(120, false, 90) && window.WeeklyTimeline.HourHeight != timeline.HourHeight);
        var overlap = activity with { Id = Guid.NewGuid(), Title = "GUI overlap", Todo = null, Intervals = [new(Guid.NewGuid(), activity.Intervals[0].Start.AddMinutes(15), activity.Intervals[0].Start.AddMinutes(45))] }; await c.ExecuteAsync(new SaveActivity(overlap, false));
        Check("overlap lanes expose separate individually tagged activity blocks", timeline.Cards.Where(x => x.Slot.Segment?.ActivityId == activity.Id || x.Slot.Segment?.ActivityId == overlap.Id).Any(x => x.Slot.Lanes >= 2) && timeline.Cards.Select(x => x.Card.Tag).OfType<ActivitySegment>().Select(s => s.ActivityId).Distinct().Count() >= 2);
        window.UpdateLayout(); Image(window.Surface, "p5-real-day.png"); window.SetPage(true); await Idle(); Image(window.Surface, "p5-real-week.png"); window.SetPage(false);
        await DriveAsync(() => ActivityDialogs.Edit(window, c, c.State!.Activities.Single(a => a.Id == activity.Id)), async s => { Field(s, "PlannerTitle").Text = "discard this"; Click(s.Actions.Children.OfType<Button>().First()); await Idle(); }); Check("explicit activity cancel retains original data", c.State!.Activities.Single(a => a.Id == activity.Id).Title == activity.Title);
        await DriveAsync(() => ActivityDialogs.Edit(window, c, c.State!.Activities.Single(a => a.Id == activity.Id)), async s => { Field(s, "ActivitySummary").Text = "saved by external close"; Check("activity outside-close uses shared validated editor semantics", await s.OutsideCloseAsync()); });
        await DriveAsync(() => ActivityDialogs.Detail(window, c, activity.Id, activity.Intervals[0].Id), async s => { Check("detail is a transient editable card with complete notes document", Children<FlowDocumentScrollViewer>(s).Any(d => new TextRange(d.Document.ContentStart, d.Document.ContentEnd).Text.Contains("Created with notes"))); Image(s, "p5-detail.png"); await s.SaveAndCloseAsync(false); });
        Drive(() => ActivityDialogs.Detail(window, c, activity.Id, activity.Intervals[0].Id), s => { ApproveNext(s); Click(s.Actions.Children.OfType<Button>().Single(b => Equals(b.Content, MainWindow.Text("ActivityDeletePeriod")))); }); await c.Pending;
        Check("detail removes only selected period and keeps activity todo and note links", c.State!.Activities.Single(a => a.Id == activity.Id).Intervals.Length == 1 && c.State.Activities.Single(a => a.Id == activity.Id).Todo?.Id == todo.Id); await c.UndoAsync();
        await DriveAsync(() => ActivityDialogs.Edit(window, c, c.State!.Activities.Single(a => a.Id == activity.Id)), async s =>
        {
            var before = Digest(c.State!); Field(s, "ActivitySummary").Text = "cannot-save-draft"; File.SetAttributes(c.FilePath, FileAttributes.ReadOnly);
            try { Check("activity failed save retains editor note fields and original state", !await s.SaveAndCloseAsync(false) && s.IsVisible && Field(s, "ActivitySummary").Text == "cannot-save-draft" && Digest(c.State!) == before); }
            finally { File.SetAttributes(c.FilePath, FileAttributes.Normal); } Click(s.Actions.Children.OfType<Button>().First());
        });
        var images = new LocalImages(c.DataDirectory, () => c.State); Check("registered image remains readable after note transaction", images.Read(c.State!.Assets.Single(a => a.Id == assetId).RelativePath)?.PixelWidth == 16);
        var sourcePath = Path.Combine(c.DataDirectory, c.State.Assets.Single(a => a.Id == assetId).RelativePath.Replace('/', Path.DirectorySeparatorChar)); using (var held = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.None)) Check("occupied image read is a safe placeholder", images.Read(c.State.Assets.Single(a => a.Id == assetId).RelativePath) == null);
        Check("unsafe external image paths never load", images.Read("../../private.png") == null && images.Read("https://example.invalid/a.png") == null);
        var malformed = Path.Combine(DirectoryPath, "bad-image.png"); File.WriteAllText(malformed, "invalid image fixture"); bool refused = false; try { images.ImportFile(malformed); } catch (Exception ex) when (NoteEditor.ImageFailure(ex)) { refused = true; } Check("malformed imported image is rejected before registration", refused);
        await c.ExecuteAsync(new SaveActivity(overlap with { Notes = c.State.Activities.Single(a => a.Id == activity.Id).Notes }, false));
        await c.ExecuteAsync(new DeleteActivity(activity.Id)); Check("deleting activity never deletes shared image bytes or second note", File.Exists(sourcePath) && c.State!.Activities.Single(a => a.Id == overlap.Id).Notes.AssetIds.Contains(assetId)); await c.UndoAsync();
        timeline.BeginGesture(null, pointer.AddSeconds(13.5), "create"); timeline.PreviewGesture(pointer.AddSeconds(13.5).AddMinutes(40));
        Check("empty-area creation snaps away fractional mouse coordinate seconds", timeline.PreviewPeriod != null && timeline.PreviewPeriod.Start.Ticks % TimeSpan.TicksPerMinute == 0 && timeline.PreviewPeriod.End.Ticks % TimeSpan.TicksPerMinute == 0);
        await DriveAsync(() => _ = timeline.CommitGestureAsync(), async s => { Field(s, "PlannerTitle").Text = "GUI drag-created"; await s.SaveAndCloseAsync(false); }); Check("empty-area preview opens prefilled editor and persists on confirmation", c.State!.Activities.Any(a => a.Title == "GUI drag-created" && a.Intervals.Single().Duration.TotalMinutes == 40));
        c.Select(savedDate, true);
    }
}
