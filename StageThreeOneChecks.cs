using System;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static partial class StageThreeChecks
{
    static async Task Idle() => await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    static async Task Eventually(Func<bool> condition) { for (int i = 0; i < 100 && !condition(); i++) await Task.Delay(20); }
    static void SendKey(UIElement target, Key key) => target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    static async Task DriveAsync(Action open, Func<PlannerDialogs.Sheet, Task> action)
    {
        var done = new TaskCompletionSource();
        _ = Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
        {
            var dialog = Application.Current.Windows.OfType<PlannerDialogs.Sheet>().LastOrDefault();
            try { if (dialog == null) throw new Exception("No modal"); await action(dialog); }
            catch (Exception ex) { results.Add("FAIL P3.1 modal " + ex); }
            finally { if (dialog?.IsVisible == true) Click(dialog.Actions.Children.OfType<Button>().First()); done.SetResult(); }
        }));
        open(); await done.Task;
    }
    static async Task RunP31Checks(MainWindow window)
    {
        var c = window.Session.Planner!; var v = window.PlannerView!; var testDate = c.Today.AddDays(4); c.Select(testDate, true);
        foreach (string input in new[] { "11：30 写作业", "11 : 30 写作业", "１１：３０　写作业", "11﹕30 写作业", "11:30写作业" })
            Check("normalized quick time " + input, PlannerController.QuickInput(input, out var title, out var time) && title == "写作业" && time == new TimeOnly(11, 30));
        foreach (string input in new[] { "24：00 title", "11：60 title", "1：300 title", "11：30 " })
            Check("invalid quick time retains input " + input, !PlannerController.QuickInput(input, out _, out _));
        Check("untimed text retains literal punctuation", PlannerController.QuickInput("会议：讨论", out var literal, out var noTime) && literal == "会议：讨论" && noTime == null);
        var work = c.State!.Categories.Single(x => x.BuiltInKey == "work");
        var category = Children<ComboBox>(v.QuickAppearance).Single(); category.SelectedValue = work.Id;
        Check("quick category applies default color without override", v.QuickAppearance.CategoryId == work.Id && v.QuickAppearance.Color.Value == null && v.QuickAppearance.Color.DefaultColor == work.Color);
        v.QuickAppearance.Color.Value = "#637584"; v.Quick.Text = "11：30 P31 parent"; await v.AddQuickAsync();
        var root = c.State!.Todos.Single(x => x.Title == "P31 parent");
        Check("quick category/override/time save together", root.CategoryId == work.Id && root.Color == "#637584" && root.PlannedTime == new TimeOnly(11, 30));
        v.Quick.Text = "14：45 More fields";
        await DriveAsync(() => Click(Children<Button>(v).Single(x => Equals(x.ToolTip, MainWindow.Text("QuickMore")))), async s =>
        {
            Check("more options reuses quick title/time/category/override before creation", Field(s, "PlannerTitle").Text == "More fields" && Field(s, "PlannerTime").Text == "14:45" && Children<TodoAppearance>(s).Single().CategoryId == work.Id && Children<ColorField>(s).Single().Value == "#637584");
            Check("ordinary leaf manual progress remains editable with default automatic mode", Field(s, "PlannerProgress").IsEnabled);
            Field(s, "PlannerProgress").Text = "45"; Field(s, "PlannerDescription").Text = "advanced without preliminary save";
            await s.SaveAndCloseAsync(false);
        });
        Check("advanced creation is one durable todo with shared validation", c.State!.Todos.Single(x => x.Title == "More fields") is Todo more && more.ManualProgress == 45 && more.ProgressMode == ProgressMode.Default && more.Description == "advanced without preliminary save" && v.Quick.Text.Length == 0);
        long revision = c.State.Revision; var bytes = File.ReadAllBytes(c.FilePath);
        await DriveAsync(() => v.Edit(root), async s => { s.Close(); await Eventually(() => !s.IsVisible); Check("unchanged X closes without revision or file write", !s.IsVisible && c.State!.Revision == revision && bytes.SequenceEqual(File.ReadAllBytes(c.FilePath))); });
        await DriveAsync(() => v.Edit(c.State!.Todos.Single(x => x.Id == root.Id)), async s =>
        {
            Field(s, "PlannerDescription").Text = "saved by X"; s.Close(); await Eventually(() => !s.IsVisible);
            Check("X validates and commits editor", !s.IsVisible && c.State!.Todos.Single(x => x.Id == root.Id).Description == "saved by X");
        });
        await DriveAsync(() => v.Edit(c.State!.Todos.Single(x => x.Id == root.Id)), async s =>
        {
            var title = Field(s, "PlannerTitle"); title.Text = "P31 Escape saved"; title.Focus(); SendKey(s, System.Windows.Input.Key.Escape); await Eventually(() => !s.IsVisible);
            Check("editor Escape saves including title and original ID", !s.IsVisible && c.State!.Todos.Single(x => x.Id == root.Id).Title == "P31 Escape saved");
        });
        root = c.State!.Todos.Single(x => x.Id == root.Id);
        await DriveAsync(() => v.Edit(root), async s =>
        {
            Field(s, "PlannerTitle").Text = " "; s.Close(); await Idle();
            Check("invalid X preserves editor and reports error", s.IsVisible && s.Error.Text.Length > 0 && c.State!.Todos.Single(x => x.Id == root.Id).Title == root.Title);
            Field(s, "PlannerTitle").Focus(); SendKey(s, System.Windows.Input.Key.Escape); await Idle();
            Check("invalid Escape also preserves unsaved text", s.IsVisible && Field(s, "PlannerTitle").Text == " ");
        });
        await DriveAsync(() => v.Edit(root), async s =>
        {
            Field(s, "PlannerTitle").Text = "save failure retained"; File.SetAttributes(c.FilePath, FileAttributes.ReadOnly);
            try { s.Close(); await Eventually(() => c.NoticeKey == "PlannerSaveFailed" && s.IsEnabled); Check("failed editor close keeps input/error and original persisted record", s.IsVisible && Field(s, "PlannerTitle").Text == "save failure retained" && s.Error.Text.Length > 0 && c.State!.Todos.Single(x => x.Id == root.Id).Title == root.Title); }
            finally { File.SetAttributes(c.FilePath, FileAttributes.Normal); }
        });
        await DriveAsync(() => v.Edit(root), async s =>
        {
            var date = Children<DatePicker>(s).Single(); var text = (System.Windows.Controls.Primitives.DatePickerTextBox)date.Template.FindName("PART_TextBox", date); text.Text = "invalid-date"; text.Focus(); SendKey(s, System.Windows.Input.Key.Escape); await Idle();
            Check("Escape validates live date text before closing", s.IsVisible && s.Error.Text == MainWindow.Text("PlannerDateRequired") && text.Text == "invalid-date");
        });
        await DriveAsync(() => v.Edit(root), async s =>
        {
            var choices = Children<ComboBox>(s).Single(); choices.IsDropDownOpen = true; await Idle();
            var choicePopup = (System.Windows.Controls.Primitives.Popup)choices.Template.FindName("PART_Popup", choices); Image((FrameworkElement)choicePopup.Child, "p31-category-popup.png");
            Check("category popup renders colored names instead of object descriptions", Children<TextBlock>(choicePopup.Child).Any(x => x.Text == work.Name) && Children<TextBlock>(choicePopup.Child).All(x => !x.Text.Contains("CategoryOption {")));
            choices.IsDropDownOpen = false;
            var picker = Children<DatePicker>(s).Single(); picker.IsDropDownOpen = true; await Idle();
            var datePopup = (System.Windows.Controls.Primitives.Popup)picker.Template.FindName("PART_Popup", picker); Image((FrameworkElement)datePopup.Child, "p31-date-popup.png");
            Check("date picker popup calendar structure remains rendered", datePopup.IsOpen && Children<System.Windows.Controls.Primitives.CalendarDayButton>(datePopup.Child).Count() >= 28);
            picker.IsDropDownOpen = false;
            var color = Children<ColorField>(s).Single(); color.Popup.IsOpen = true; await Idle(); Image((FrameworkElement)color.Popup.Child, "p31-hsv-colors.png");
            Check("visual color popup has HSV, hue and 24 accessible preset buttons", color.Plane.ActualHeight > 100 && color.Hue.Maximum == 359 && Children<Button>(color.Popup.Child).Count(x => x.Tag is string) == 24);
            color.Value = "#283C4C"; Check("low contrast primary derives readable accent and soft background", ThemeColors.Contrast(ThemeColors.Accent(color.Value!), "#19232E") >= 4.5 && ThemeColors.Soft(color.Value!) != color.Value);
            var chip = Children<Button>(color.Popup.Child).Single(x => Equals(x.Tag, "#A3D9C4")); Click(chip); Check("preset selected mark and stored primary agree", color.Value == "#A3D9C4" && chip.Content is TextBlock mark && mark.Text == "✓" && ThemeColors.Contrast(((System.Windows.Media.SolidColorBrush)mark.Foreground).Color.ToString(), color.Value) >= 4.5);
            string prior = color.Value!; color.Plane.Focus(); SendKey(color.Plane, System.Windows.Input.Key.Left); color.Hue.Value = 280;
            Check("HSV plane keyboard and hue slider update same HEX value", color.Value != prior && color.Valid);
            color.Value = "#00FFFFFF"; var preview = Children<Button>(color).Single();
            Check("legacy alpha HEX keeps a readable RGB primary preview without mutating stored value", color.Value == "#00FFFFFF" && ((System.Windows.Media.SolidColorBrush)preview.Background).Color.A == 255 && preview.Content is TextBlock label && ThemeColors.Contrast(((System.Windows.Media.SolidColorBrush)label.Foreground).Color.ToString(), ((System.Windows.Media.SolidColorBrush)preview.Background).Color.ToString()) >= 4.5);
            SendKey(color.Plane, System.Windows.Input.Key.Escape); await Idle(); Check("color-popup Escape closes popup without closing full editor", !color.Popup.IsOpen && s.IsVisible);
            color.Popup.IsOpen = false; Click(s.Actions.Children.OfType<Button>().First());
        });
        Check("cancel color changes does not persist", c.State!.Todos.Single(x => x.Id == root.Id).Color == root.Color);
        var oldPriority = new Category { Id = Guid.NewGuid(), BuiltInKey = "urgent", Name = "紧急", Color = "#C66868" };
        await c.ExecuteAsync(new SaveCategory(oldPriority));
        Check("legacy priority excluded from new choices without deleting stored category", !((CategoryOption[])category.ItemsSource).Any(x => x.Id == oldPriority.Id) && c.State!.Categories.Any(x => x.Id == oldPriority.Id));
        await c.ExecuteAsync(new SaveTodo(c.State!.Todos.Single(x => x.Id == root.Id) with { CategoryId = oldPriority.Id }));
        await DriveAsync(() => v.Edit(c.State!.Todos.Single(x => x.Id == root.Id)), s =>
        {
            Check("old category reference can still be retained in editor", Children<ComboBox>(s).Single().SelectedValue is Guid id && id == oldPriority.Id);
            Click(s.Actions.Children.OfType<Button>().First()); return Task.CompletedTask;
        });
        foreach (string name in new[] { "P31 child A", "P31 child B" }) await c.ExecuteAsync(new SaveTodo(c.NewTodo(name, root.Id)));
        var a = c.State!.Todos.Single(x => x.Title == "P31 child A"); var b = c.State.Todos.Single(x => x.Title == "P31 child B");
        await c.ExecuteAsync(new SetTodoCompleted(new(a.Id), true));
        Check("default parent progress is automatic in GUI data", PlannerQueries.Progress(c.State!, new(root.Id)) == 50);
        window.UpdateLayout();
        CalendarTaskPanel DayPanel() => ((CalendarPreviewCell)Children<Button>(v).Single(x => x.Tag is DateOnly d && d == testDate).Content).Preview;
        Check("calendar starts with roots instead of flat children", !Children<TextBlock>(DayPanel()).Any(x => x.Text.Contains("P31 child")));
        var expand = Children<Button>(DayPanel()).Single(x => Equals(x.ToolTip, MainWindow.Text("CalendarExpand"))); Click(expand); window.UpdateLayout();
        Check("calendar explicit expansion shows indented direct children", Children<TextBlock>(DayPanel()).Count(x => x.Text.Contains("P31 child")) == 2 && Children<DockPanel>(DayPanel()).Any(x => x.Margin.Left > 0));
        var entries = new[] { new CalendarTodoEntry("root", "", false, "#AFC5D8", false, () => { }, root.Id), new("child A", "", false, "#AFC5D8", false, () => { }, a.Id, root.Id, 1), new("child B", "", false, "#AFC5D8", false, () => { }, b.Id, root.Id, 1), new("other root", "", false, "#AFC5D8", false, () => { }, Guid.NewGuid()) };
        var adaptive = new CalendarTaskPanel(entries, _ => { }, false, () => { });
        foreach (double h in new[] { 0d, 15.9, 16, 31.9, 32, 47.9, 48, 64, 80 })
        {
            adaptive.Measure(new Size(120, h)); adaptive.Arrange(new Rect(0, 0, 120, h));
            var visible = adaptive.Children.OfType<DockPanel>().Where(x => x.Clip == null).Select(x => (CalendarTodoEntry)x.Tag).ToArray();
            Check("hierarchical calendar depth-first capacity and +N boundary " + h, visible.Length == CalendarTaskPanel.Capacity(h, 4) && adaptive.Remaining == 4 - visible.Length && visible.SequenceEqual(entries.Take(visible.Length)) && visible.All(x => x.Depth == 0 || visible.Any(p => p.Id == x.ParentId)));
        }
        Image(window.Surface, "p31-hierarchy-cards.png");
        var inlineFocus = new System.Collections.Generic.List<string>();
        System.Windows.Input.KeyboardFocusChangedEventHandler trace = (_, e) => { if (c.Inline != null) inlineFocus.Add((e.OldFocus?.GetType().Name ?? "null") + "->" + (e.NewFocus?.GetType().Name ?? "null")); };
        window.AddHandler(System.Windows.Input.Keyboard.LostKeyboardFocusEvent, trace, true);
        c.BeginInline(c.State!.Todos.Single(x => x.Id == root.Id)); window.UpdateLayout(); await Idle();
        if (!Children<TextBox>(v).Any(x => AutomationProperties.GetName(x) == MainWindow.Text("CalendarInlineTitle"))) results.Add("FAIL inline diagnostics: draft=" + (c.Inline != null) + ", writable=" + c.Writable + ", selected=" + c.SelectedDate + ", planned=" + c.State!.Todos.Single(x => x.Id == root.Id).PlannedDate + ", focus=" + string.Join(";", inlineFocus));
        window.RemoveHandler(System.Windows.Input.Keyboard.LostKeyboardFocusEvent, trace);
        TextBox Inline() => Children<TextBox>(v).Single(x => AutomationProperties.GetName(x) == MainWindow.Text("CalendarInlineTitle"));
        Inline().Text = "discard inline"; SendKey(Inline(), System.Windows.Input.Key.Escape); await Idle();
        Check("inline Escape cancels without changing title", c.Inline == null && c.State!.Todos.Single(x => x.Id == root.Id).Title == root.Title);
        c.BeginInline(c.State!.Todos.Single(x => x.Id == root.Id)); window.UpdateLayout(); await Idle(); Inline().Text = " "; SendKey(Inline(), System.Windows.Input.Key.Enter); await Idle();
        Check("empty inline Enter retains editor without deletion", c.Inline?.Error.Length > 0 && c.State!.Todos.Any(x => x.Id == root.Id));
        Image(window.Surface, "p31-inline-invalid.png");
        Inline().Text = "P31 inline saved"; SendKey(Inline(), System.Windows.Input.Key.Enter); await c.Pending; await Idle();
        Check("inline Enter changes title only and retains advanced fields", c.Inline == null && c.State!.Todos.Single(x => x.Id == root.Id) is Todo renamed && renamed.Title == "P31 inline saved" && renamed.CategoryId == oldPriority.Id && renamed.PlannedTime == root.PlannedTime && renamed.Description == root.Description);
        c.BeginInline(c.State!.Todos.Single(x => x.Id == root.Id)); window.UpdateLayout(); await Idle(); Inline().Text = "P31 focus saved"; v.Quick.Focus(); await c.Pending; await Idle();
        Check("inline focus loss commits valid title", c.Inline == null && c.State!.Todos.Single(x => x.Id == root.Id).Title == "P31 focus saved");
        var card = v.Items.Children.OfType<Border>().Single(x => x.Tag is Todo t && t.Id == root.Id); var menu = card.ContextMenu!;
        menu.PlacementTarget = card; menu.IsOpen = true; await Idle();
        var first = menu.Items.OfType<MenuItem>().First(); first.Focus(); Image(menu, "p31-dark-menu.png");
        Check("real context menu uses opaque dark surface and readable foreground", menu.IsOpen && ThemeColors.Contrast(((System.Windows.Media.SolidColorBrush)menu.Foreground).Color.ToString(), ((System.Windows.Media.SolidColorBrush)menu.Background).Color.ToString()) > 7);
        first.IsChecked = true; var disabled = menu.Items.OfType<MenuItem>().Last(); disabled.IsEnabled = false; menu.UpdateLayout();
        Check("disabled menu item remains readable without stacked opacity", disabled.Opacity == 1 && ThemeColors.Contrast(((System.Windows.Media.SolidColorBrush)disabled.Foreground).Color.ToString(), ((System.Windows.Media.SolidColorBrush)menu.Background).Color.ToString()) >= 4.5);
        Image(menu, "p31-dark-menu-states.png"); menu.IsOpen = false;
        c.Select(c.Today.AddDays(-10), true); window.EnterCompact(); window.RestoreNormal(false);
        Check("compact expansion selects today/month/day and keeps desktop mode", c.SelectedDate == c.Today && c.Month.Month == c.Today.Month && window.Mode == DisplayMode.Normal && !window.IsWeek && !window.IsPinned);
        // Leave a real manual cascade for the next independent app-process restart phase.
        await c.ExecuteAsync(new SetTodoCompleted(new(root.Id), true));
        Check("GUI-created subtree has durable cascade snapshot", c.State!.Todos.Single(x => x.Id == root.Id).CompletionSnapshot != null && c.State.Todos.Single(x => x.Id == b.Id).Status == TodoStatus.Completed);
        File.WriteAllText(Path.Combine(DirectoryPath, "p31-snapshot-id.txt"), root.Id.ToString());
    }
}
