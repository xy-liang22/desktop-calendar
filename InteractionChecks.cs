using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static partial class StageThreeChecks
{
    static async Task RunInteractionChecks(MainWindow window)
    {
        var c = window.Session.Planner!; window.RestoreNormal(false); window.SetPage(false); c.Select(c.Today, true);
        foreach (var emoji in new[] { "😀", "😴", "❤", "👩🏽‍💻", "👨‍👩‍👧‍👦" })
            Check("Windows native color glyph contains real colored pixels " + emoji, ColorEmojiRaster.Get(emoji) != null);
        var preview = new PlannerDialogs.Sheet("Emoji / 中文 🥰");
        var block = new EmojiTextBlock { Text = "任务 😀 做饭 🏠\n说明 👩🏽‍💻 中文 ❤ English", FontSize = 22, TextWrapping = TextWrapping.Wrap, Width = 320 };
        var edit = new TextBox { Text = block.Text, FontSize = 22, AcceptsReturn = true, Height = 120, TextWrapping = TextWrapping.Wrap };
        preview.Body.Children.Add(block); preview.Body.Children.Add(edit);
        var document = MarkdownNotes.Render("# Markdown 😀\n说明 **👩🏽‍💻** 与 ❤，原文本保留。", NoteFormat.Markdown, new LocalImages(c.DataDirectory, () => c.State));
        var note = new FlowDocumentScrollViewer { Document = document, Height = 135, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; preview.Body.Children.Add(note);
        preview.Owner = window; preview.Show(); await Idle(); await Idle();
        Image(preview, "interaction-color-emoji.png");
        Check("color glyph presentation leaves textbox text and selection semantics intact", edit.Text == block.Text && EmojiText.Parts(edit.Text).Length == 4);
        string plain = EmojiDocument.SelectionText(document.ContentStart, document.ContentEnd, out bool containsEmoji);
        Check("Markdown colored glyphs retain original Unicode when copied", containsEmoji && plain.Contains("Markdown 😀") && plain.Contains("说明 👩🏽‍💻 与 ❤") && !plain.Contains('\uFFFC'));
        edit.Select(3, 2); edit.SelectedText = "😴"; await Idle(); Check("emoji replacement remains native Unicode editable text", edit.Text.Contains("😴") && !edit.Text.Contains('\uFFFC'));
        double previousScale = FontPreferences.Scale; FontPreferences.Apply(1.25); await Idle();
        Check("existing editor and Markdown document track font preview without compounding", Math.Abs(edit.FontSize - 27.5) < .001 && Math.Abs(block.FontSize - 27.5) < .001 && Math.Abs(document.FontSize - 15) < .001);
        FontPreferences.Apply(previousScale); await Idle();
        Check("font preview reverts existing document and editor", Math.Abs(edit.FontSize - 22 * previousScale) < .001 && Math.Abs(document.FontSize - 12 * previousScale) < .001);
        string longText = string.Concat(Enumerable.Repeat("测试说明 😀\n", 2000)); edit.Text = longText; await Idle(); edit.ScrollToEnd(); await Idle();
        var effects = (System.Windows.Media.TextEffectCollection)edit.GetValue(System.Windows.Documents.TextElement.TextEffectsProperty);
        Check("long editable notes render color only near visible scroll range", effects.Count > 0 && effects.Count < 100 && effects.All(effect => effect.PositionStart > longText.Length - 5000) && edit.Text == longText);
        preview.Close();
        var root = c.NewTodo("Interaction parent 😀"); await c.ExecuteAsync(new SaveTodo(root));
        var childA = c.NewTodo("Interaction done", root.Id); var childB = c.NewTodo("Interaction pending", root.Id);
        await c.ExecuteAsync(new BatchCommand([new SaveTodo(childA), new SaveTodo(childB), new SetTodoCompleted(new(childA.Id), true)]));
        c.SetCompletedChildren(true); await Idle();
        await DriveAsync(() => PlannerDialogs.ChangeType(window, c, c.FindTodo(childB.Id)!), async s =>
        {
            var choice = Choice(s, "RepeatType"); Check("ordinary subtask allows carry conversion but not independent recurring series", !((ComboBoxItem)choice.Items[3]).IsEnabled);
            choice.SelectedIndex = 1; ApproveNext(s); await s.SaveAndCloseAsync(false);
        });
        Check("subtask type conversion preserves its parent and identity", c.FindTodo(childB.Id) is Todo child && child.ParentId == root.Id && child.Kind == TodoKind.CarryHistory);
        Check("completed child collapses while parent and pending child stay visible", window.PlannerView!.Items.Children.OfType<System.Windows.Controls.Border>().Any(b => b.Tag is Todo t && t.Id == childB.Id) && !window.PlannerView.Items.Children.OfType<System.Windows.Controls.Border>().Any(b => b.Tag is Todo t && t.Id == childA.Id));
        var group = window.PlannerView.Items.Children.OfType<Button>().Single(b => Equals(b.Tag, root.Id)); Click(group); await Idle();
        Check("completed group expands in original order without changing progress", window.PlannerView.Items.Children.OfType<System.Windows.Controls.Border>().Any(b => b.Tag is Todo t && t.Id == childA.Id) && PlannerQueries.Progress(c.State!, new(root.Id)) == 50);
        c.ToggleCompletedChildren(root.Id); window.EnterCompact(); await Idle();
        Check("compact view shares completed-child filter", !window.CompactRealList.Children.OfType<CheckBox>().Any(b => System.Windows.Automation.AutomationProperties.GetName(b) == childA.Title) && window.CompactRealList.Children.OfType<CheckBox>().Any(b => System.Windows.Automation.AutomationProperties.GetName(b) == childB.Title));
        window.RestoreNormal(false); c.SetCompletedChildren(false);
        await DriveAsync(() => PlannerDialogs.ChangeType(window, c, c.FindTodo(root.Id)!), async s =>
        {
            Choice(s, "RepeatType").SelectedIndex = 2; ApproveNext(s); await s.SaveAndCloseAsync(false);
        });
        Check("type conversion GUI keeps same ID and children for carry", c.FindTodo(root.Id)?.Kind == TodoKind.CarryLatest && PlannerQueries.Children(c.State!, c.FindTodo(root.Id)!).Length == 2);
        await DriveAsync(() => PlannerDialogs.ChangeType(window, c, c.FindTodo(root.Id)!), async s =>
        {
            Choice(s, "RepeatType").SelectedIndex = 3; await Idle(); var fields = Children<RecurrenceFields>(s).Single(); fields.CountInput.Text = "3"; ApproveNext(s); await s.SaveAndCloseAsync(false);
        });
        Check("type conversion GUI creates finite recurring rule with history protection", c.State!.Series.Any(s => s.Versions.Any(v => v.Templates.Any(t => t.Title == root.Title))) && c.State.Todos.Single(t => t.Id == childA.Id).Status == TodoStatus.Completed);
        c.SetCompletedChildren(true); await Idle();
        Check("calendar completed filter also covers history-only parent context", c.On(c.Today).Any(t => t.Id == childA.Id) && !Children<FrameworkElement>(window.PlannerView!).Any(e => e.Tag is CalendarTodoEntry entry && entry.Id == childA.Id));
        c.SetCompletedChildren(false);
        var oldTask = c.NewTodo("Old date converted to latest") with { PlannedDate = c.Today.AddDays(-5) }; await c.ExecuteAsync(new SaveTodo(oldTask)); c.Select(oldTask.PlannedDate, true);
        await DriveAsync(() => PlannerDialogs.ChangeType(window, c, c.FindTodo(oldTask.Id)!), async s => { Choice(s, "RepeatType").SelectedIndex = 2; ApproveNext(s); await s.SaveAndCloseAsync(false); });
        Check("past one-off converted to latest stays visible on actual carry date", c.SelectedDate == c.Today && c.On(c.Today).Any(t => t.Id == oldTask.Id && t.Kind == TodoKind.CarryLatest));
        Check("shortcut validation rejects unmodified keys and F12", !RevealShortcut.Valid("D") && !RevealShortcut.Valid("Ctrl+F12") && RevealShortcut.Valid("Ctrl+Alt+D"));
        string saved = window.Session.Hotkey.Value; string chord = "Ctrl+Alt+Shift+F10";
        using (var other = new RevealShortcut())
        {
            bool reserved = other.Apply(chord); Check("isolated native shortcut can be reserved", reserved);
            if (reserved) Check("shortcut conflict keeps prior setting and registration", !window.SetRevealHotkey(chord) && window.Session.Hotkey.Value == saved);
        }
        bool assigned = window.SetRevealHotkey(chord); Check("shortcut can be registered after competing owner exits", assigned);
        if (assigned)
        {
            window.HideToTray(); PostMessage(window.Session.Hotkey.Handle, 0x0312, new IntPtr(window.Session.Hotkey.Registration), IntPtr.Zero); await Idle();
            Check("WM_HOTKEY dispatch restores hidden component without forcing topmost", window.IsVisible && window.Mode != DisplayMode.Tray && window.Session.Hotkey.Value == chord);
        }
        window.SetRevealHotkey(saved); Check("shortcut disable/revert unregisters temporary test binding", window.Session.Hotkey.Value == saved);
        string preferencePath = Path.Combine(DirectoryPath, "window-preferences.json");
        await DriveAsync(() => ApplicationSettings.Shortcut(window), async s =>
        {
            Field(s, "ShortcutCapture").Text = chord; Children<CheckBox>(s).Single().IsChecked = true;
            File.SetAttributes(preferencePath, FileAttributes.ReadOnly);
            try { Check("failed shortcut preference save keeps editor and releases new registration", !await s.SaveAndCloseAsync(false) && s.IsVisible && s.Error.Text == MainWindow.Text("ShortcutSaveFailed") && window.Session.Hotkey.Value == saved); }
            finally { File.SetAttributes(preferencePath, FileAttributes.Normal); }
            Click(s.Actions.Children.OfType<Button>().First());
        });
        c.SetCompletedChildren(true); window.Session.Save();
        var p = System.Text.Json.JsonSerializer.Deserialize<WindowPreferences>(File.ReadAllBytes(Path.Combine(DirectoryPath, "window-preferences.json")))!;
        Check("new preference fields persist with existing schema", p.CollapseCompletedChildren && p.RevealHotkey == saved && p.SchemaVersion == 2);
        c.SetCompletedChildren(false); window.Session.Save();
        var before = window.SnapshotPreferences();
        window.SetPinned(false); window.CollapseToEdge(DockSide.Left);
        Check("edge handle floats while user topmost preference remains off", window.Mode == DisplayMode.EdgeHandle && window.DesktopMode.NativeTopmost && !window.PreferredPinned);
        window.RestoreNormal(false); Check("expanding handle clears temporary topmost and restores desktop owner", !window.DesktopMode.NativeTopmost && window.DesktopMode.Layer == WindowLayer.Desktop);
        window.CollapseToEdge(DockSide.Right); window.EnterCompact(); Check("edge to compact restores the user's desktop preference", !window.DesktopMode.NativeTopmost && window.Mode == DisplayMode.Compact); window.RestoreNormal(false);
        var anchor = new DateOnly(2026, 12, 30);
        foreach (int weeks in new[] { 1, 3, 6, 12 }) foreach (var first in new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Thursday })
        {
            var dates = CalendarViewport.Fixed(anchor, first, weeks, anchor, 1).OfType<DateOnly>().ToArray();
            Check($"fixed viewport is continuous aligned and inclusive {weeks} {first}", dates.Length == weeks * 7 && dates[0].DayOfWeek == first && dates[^1].DayNumber - dates[0].DayNumber == weeks * 7 - 1);
        }
        Check("fixed viewport clamps extreme offsets and year bounds", CalendarViewport.Fixed(DateOnly.MinValue, DayOfWeek.Sunday, 12, DateOnly.MinValue, -520000).Any(d => d == DateOnly.MinValue) && CalendarViewport.Fixed(DateOnly.MaxValue, DayOfWeek.Monday, 12, DateOnly.MaxValue, 520000).Last() == DateOnly.MaxValue);
        Check("short fixed view can always reveal a late-month today", CalendarViewport.Fixed(anchor, c.WeekStart, 1, CalendarViewport.TodayAnchor(anchor, 1), 0).Contains(anchor));
        await DriveAsync(() => ApplicationSettings.Appearance(window), async s =>
        {
            Children<ComboBox>(s).Last().SelectedIndex = 1; var fields = Children<FixedCalendarFields>(s).Single(); fields.Count.Text = "3"; fields.Start.SelectedDate = anchor.ToDateTime(TimeOnly.MinValue); await Idle();
            Check("editable first/last week fields track inclusive row count", fields.Read(out int rows, out var start) && rows == 3 && start == CalendarViewport.WeekStart(anchor, c.WeekStart));
            fields.End.SelectedDate = CalendarViewport.WeekStart(anchor, c.WeekStart).AddDays(21).ToDateTime(TimeOnly.MinValue); await Idle();
            Check("editing last week updates displayed week count", fields.Count.Text == "4");
            fields.Count.Text = "0"; Check("invalid fixed row count retains settings draft", !await s.SaveAndCloseAsync(false) && s.IsVisible); fields.Count.Text = "3";
            var textScale = Children<Slider>(s).Single(slider => System.Windows.Automation.AutomationProperties.GetName(slider) == MainWindow.Text("TextScaleSetting")); textScale.Value = 1.35;
            var brightness = Children<Slider>(s).Single(slider => System.Windows.Automation.AutomationProperties.GetName(slider) == MainWindow.Text("CalendarBrightnessSetting")); brightness.Value = .5; await Idle();
            Image(s, "interaction-appearance-controls.png"); Children<ScrollViewer>(s).Single(viewer => ReferenceEquals(viewer.Content, s.Body)).ScrollToEnd(); await Idle(); Image(s, "interaction-appearance-lower.png"); await s.SaveAndCloseAsync(false);
        });
        c.Select(anchor, true); await c.ExecuteAsync(new SaveTodo(c.NewTodo("Calendar brightness 😀 emoji") with { Color = "#E50016" })); c.SetColors(false);
        await Idle(); Image(window.Surface, "interaction-large-font-calendar.png");
        double width = window.Width, height = window.Height; window.Width = 880; window.Height = 520; await Idle(); Image(window.Surface, "interaction-small-large-font.png"); window.Width = width; window.Height = height;
        Check("custom fixed rows and font size apply to real calendar", Children<Button>(window.PlannerView!).Count(b => b.Tag is DateOnly) == 21 && Math.Abs(FontPreferences.Scale - 1.35) < .001 && Children<CalendarPreviewCell>(window.PlannerView!).All(cell => Math.Abs(cell.RowDefinitions[0].Height.Value - 28.35) < .01));
        window.ApplyAppearance(window.Appearance); await Idle(); Check("repeated font preview does not multiply explicit heading sizes", Children<System.Windows.Controls.TextBlock>(window.PlannerView!).Single(t => t.Text == MainWindow.Text("TodoTitle")).FontSize == 17 * 1.35);
        Check("calendar brightening increases contrast without altering category values", ThemeColors.Contrast(ThemeColors.CalendarAccent("#E50016", .5), "#19232E") > ThemeColors.Contrast(ThemeColors.CalendarAccent("#E50016", 0), "#19232E") && window.Appearance.CalendarTextBrightness == .5);
        window.ApplyAppearance(before); await Idle(); Check("text scaling reverts without cumulative font growth", Math.Abs(FontPreferences.Scale - before.TextScale) < .001);
    }
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window, int message, IntPtr w, IntPtr l);
}
