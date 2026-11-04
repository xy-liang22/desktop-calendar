using TextBlock = DesktopCalendar.EmojiTextBlock;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopCalendar;
internal sealed record CalendarTodoEntry(string Title, string TimePrefix, bool Completed, string Color, bool Emphasis, Action Toggle,
    Guid Id = default, Guid? ParentId = null, int Depth = 0, bool HasChildren = false, bool Expanded = false,
    Action? Expand = null, Action? BeginInline = null, InlineTitleDraft? Draft = null, PlannerController? Controller = null, bool Editable = true, string StatusMark = "");

// One fixed production header; auxiliary text shares its width with the date and overflow.
internal sealed class CalendarPreviewCell : Grid
{
    internal CalendarTaskPanel Preview { get; }
    internal TextBlock HeaderMore { get; }
    readonly Grid? knowledgeHeader;
    internal TextBlock? CalendarInfo { get; private set; }
    internal void AddKnowledge(DesktopCalendar.Core.CalendarKnowledge knowledge, DateOnly date)
    {
        if (knowledgeHeader == null) return;
        if (CalendarInfo != null) knowledgeHeader.Children.Remove(CalendarInfo);
        var events = CalendarPresentation.CompactEvents(knowledge, date); var primary = events.OrderBy(CalendarPresentation.Priority).FirstOrDefault();
        var info = new TextBlock { FontSize = 9, MinWidth = 0, Margin = new Thickness(3, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, ClipToBounds = true, ToolTip = CalendarPresentation.Detail(knowledge, date) };
        foreach (string region in new[] { "CN", "HK" })
            if (events.Any(e => region == "CN" ? e.Kind is "CN" or "CNWork" : e.Kind == "HK")) info.Inlines.Add(new System.Windows.Documents.Run(MainWindow.Text(region == "CN" ? "CalendarCNShort" : "CalendarHKShort") + " ") { Foreground = ThemeColors.Brush(CalendarPresentation.EventColor(region)) });
        info.Inlines.Add(new System.Windows.Documents.Run(primary == null ? CalendarPresentation.ShortLunar(date) : Localization.English ? primary.En : primary.Zh) { Foreground = ThemeColors.Brush(primary == null ? "#A2AFBC" : CalendarPresentation.EventColor(primary.Kind)) });
        CalendarInfo = info; knowledgeHeader.ToolTip = CalendarPresentation.Detail(knowledge, date); SetColumn(info, 1); knowledgeHeader.Children.Add(info);
    }
    internal CalendarPreviewCell(DateTime date, DemoTodo[] tasks, bool currentMonth, string information = "")
    {
        Margin = new Thickness(4, 4, 2, 2); ClipToBounds = true;
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        RowDefinitions.Add(new() { Height = GridLength.Auto });
        RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var header = new Grid { Height = 21 };
        header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = new GridLength(18) });
        header.Children.Add(new TextBlock { Text = date.Day.ToString(CultureInfo.InvariantCulture), FontSize = 14,
            FontWeight = currentMonth && date.Day == 29 ? FontWeights.Bold : FontWeights.Normal,
            Foreground = PrototypeViews.Brush(currentMonth ? "#EDF2F5" : "#6E7C89") });
        HeaderMore = new TextBlock { FontSize = 9, Foreground = PrototypeViews.Brush("#A3D9C4"), VerticalAlignment = VerticalAlignment.Center };
        SetColumn(HeaderMore, 1); header.Children.Add(HeaderMore); Children.Add(header);
        var info = new TextBlock { Text = information, FontSize = 9, Height = information.Length == 0 ? 3 : 15,
            TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = information };
        SetRow(info, 1); Children.Add(info);
        Preview = new CalendarTaskPanel(tasks, hidden => HeaderMore.Text = hidden == 0 ? "" : "+" + hidden.ToString(CultureInfo.InvariantCulture));
        SetRow(Preview, 2); Children.Add(Preview);
    }
    internal CalendarPreviewCell(DateOnly date, CalendarTodoEntry[] tasks, bool currentMonth, bool backgroundColors, Action more, CalendarTextMode textMode = CalendarTextMode.SingleLine, double brightness = .2)
    {
        Margin = new Thickness(4, 4, 2, 2); ClipToBounds = true;
        RowDefinitions.Add(new() { Height = new GridLength(FontPreferences.Size(21)) }); RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var header = knowledgeHeader = new Grid { ClipToBounds = true }; header.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); header.ColumnDefinitions.Add(new() { MinWidth = 0 }); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = date.Day.ToString(CultureInfo.InvariantCulture), FontSize = 14, FontWeight = date == DateOnly.FromDateTime(DateTime.Today) ? FontWeights.Bold : FontWeights.Normal, Foreground = PrototypeViews.Brush(currentMonth ? "#EDF2F5" : "#6E7C89") });
        HeaderMore = new TextBlock { FontSize = 9, Foreground = PrototypeViews.Brush("#A3D9C4"), Cursor = System.Windows.Input.Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, ToolTip = MainWindow.Text("PlannerAllTasks") };
        HeaderMore.MouseLeftButtonUp += (_, e) => { e.Handled = true; more(); };
        SetColumn(HeaderMore, 2); header.Children.Add(HeaderMore); Children.Add(header);
        Preview = new CalendarTaskPanel(tasks, hidden => HeaderMore.Text = hidden == 0 ? "" : "+" + hidden.ToString(CultureInfo.InvariantCulture), backgroundColors, more, textMode, brightness);
        SetRow(Preview, 1); Children.Add(Preview);
    }
}

// Desired height is independent of preview count. Arrangement only clips/repositions
// fixed-height rows, without changing Visibility, rebuilding children or parent sizing.
internal sealed class CalendarTaskPanel : Panel
{
    internal const double RowHeight = 16;
    readonly double rowUnit = FontPreferences.Size(RowHeight);
    readonly int count;
    readonly CalendarTodoEntry[]? entries;
    double[] heights = [];
    readonly Action<int> headerOverflow;
    readonly TextBlock more;
    internal int VisibleRows { get; private set; }
    internal int Remaining => count - VisibleRows;
    internal bool MoreInHeader { get; private set; }
    internal CalendarTaskPanel(DemoTodo[] tasks, Action<int> headerOverflow)
    {
        count = tasks.Length; this.headerOverflow = headerOverflow; ClipToBounds = true;
        foreach (var task in tasks)
        {
            var text = new TextBlock { Text = task.TimePrefix + task.Title, FontSize = 10, Height = RowHeight,
                TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = task.Title };
            void Refresh()
            {
                text.Foreground = PrototypeViews.Brush(task.Completed ? "#76838E" : "#A3D9C4");
                text.TextDecorations = task.Completed ? TextDecorations.Strikethrough : null;
            }
            Refresh(); task.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(DemoTodo.Completed)) Refresh(); };
            Children.Add(text);
        }
        more = new TextBlock { FontSize = 10, Height = RowHeight, Foreground = PrototypeViews.Brush("#A2AFBC") };
        Children.Add(more);
    }
    internal CalendarTaskPanel(CalendarTodoEntry[] tasks, Action<int> headerOverflow, bool backgrounds, Action onMore, CalendarTextMode textMode = CalendarTextMode.SingleLine, double brightness = .2)
    {
        count = tasks.Length; entries = tasks; this.headerOverflow = headerOverflow; ClipToBounds = true;
        foreach (var task in tasks)
        {
            var row = new DockPanel { Tag = task, MinHeight = rowUnit, Margin = new Thickness(Math.Min(task.Depth, 6) * 7, 0, 0, 0), Opacity = task.Completed ? .6 : 1, ClipToBounds = true };
            if (task.HasChildren)
            {
                var expand = PlannerView.Button(task.Expanded ? "⌄" : "‹", () => task.Expand?.Invoke(), MainWindow.Text("CalendarExpand")); expand.Width = 12; expand.Height = rowUnit; expand.VerticalAlignment = VerticalAlignment.Top; expand.FontSize = 12; expand.Margin = new Thickness(2, 0, 0, 0); expand.Padding = new Thickness(0); DockPanel.SetDock(expand, Dock.Right); row.Children.Add(expand);
            }
            if (task.StatusMark.Length > 0) { var mark = new TextBlock { Text = task.StatusMark, MinWidth = 14, Height = rowUnit, Margin = new Thickness(2, 0, 0, 0), Foreground = ThemeColors.Brush("#A3D9C4"), FontSize = 11, TextAlignment = TextAlignment.Center, ToolTip = task.StatusMark == "↻" ? MainWindow.Text("RepeatPeriodicMark") : MainWindow.Text("RepeatLatestMark") }; DockPanel.SetDock(mark, Dock.Right); row.Children.Add(mark); }
            var check = new CheckBox { IsChecked = task.Completed, Margin = new Thickness(0, 0, 2, 0), Width = 17, IsEnabled = task.Editable, Foreground = ThemeColors.Brush(ThemeColors.Accent(task.Color)), Style = (Style)Application.Current.FindResource("CalendarCheck"), VerticalAlignment = VerticalAlignment.Top, ToolTip = MainWindow.Text("PlannerToggle") };
            check.Click += (_, e) => { check.IsChecked = task.Completed; e.Handled = true; task.Toggle(); };
            row.Children.Add(check);
            var text = new TextBlock { Tag = task, Text = task.TimePrefix + task.Title, FontSize = 10, TextWrapping = textMode == CalendarTextMode.SingleLine ? TextWrapping.NoWrap : TextWrapping.Wrap, MaxHeight = textMode == CalendarTextMode.SingleLine ? rowUnit : textMode == CalendarTextMode.TwoLines ? rowUnit * 2 : double.PositiveInfinity, LineHeight = rowUnit, LineStackingStrategy = LineStackingStrategy.BlockLineHeight, VerticalAlignment = VerticalAlignment.Top, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = task.TimePrefix + task.Title,
                FontWeight = task.Emphasis ? FontWeights.SemiBold : FontWeights.Normal, Foreground = ThemeColors.Brush(task.Completed ? "#93A0AC" : backgrounds ? ThemeColors.Foreground(ThemeColors.Soft(task.Color)) : ThemeColors.CalendarAccent(task.Color, brightness)), TextDecorations = task.Completed ? TextDecorations.Strikethrough : null };
            if (task.BeginInline != null) text.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { e.Handled = true; task.BeginInline(); } };
            UIElement body = text;
            if (task.Draft is InlineTitleDraft draft && task.Controller is PlannerController controller)
            {
                var input = new TextBox { Tag = draft, Text = draft.Text, FontSize = 10, Padding = new Thickness(0), BorderThickness = new Thickness(1), Height = rowUnit, MaxLength = 10000, ToolTip = draft.Error.Length > 0 ? draft.Error : MainWindow.Text("CalendarInlineHint") };
                System.Windows.Automation.AutomationProperties.SetName(input, MainWindow.Text("CalendarInlineTitle"));
                input.TextChanged += (_, _) => draft.Text = input.Text;
                var ime = new ImeInput(input);
                input.PreviewKeyDown += async (_, e) => { if (ime.Consumes(e)) return; if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; controller.CancelInline(); } else if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; await controller.CommitInlineAsync(); } };
                input.LostKeyboardFocus += (_, e) =>
                {
                    if (e.NewFocus is FrameworkElement replacement && ReferenceEquals(replacement.Tag, draft)) return;
                    _ = input.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(async () => { if (input.IsLoaded && !input.IsKeyboardFocusWithin && controller.Inline == draft && !draft.Saving) await controller.CommitInlineAsync(); }));
                };
                void Error() { input.ToolTip = draft.Error.Length > 0 ? draft.Error : MainWindow.Text("CalendarInlineHint"); input.BorderBrush = ThemeColors.Brush(draft.Error.Length > 0 ? "#E3BF89" : "#A3D9C4"); }
                draft.Changed += Error; input.Unloaded += (_, _) => draft.Changed -= Error;
                input.Loaded += (_, _) => { if (input.IsLoaded && input.IsVisible && controller.Inline == draft && !draft.Saving) { input.Focus(); input.SelectAll(); } }; Error(); body = input;
            }
            row.Children.Add(new Border { Background = backgrounds ? ThemeColors.Brush(ThemeColors.Soft(task.Color)) : Brushes.Transparent, CornerRadius = new CornerRadius(3), Child = body });
            Children.Add(row);
        }
        more = new TextBlock { FontSize = 10, Height = rowUnit, Foreground = PrototypeViews.Brush("#A2AFBC"), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = MainWindow.Text("PlannerAllTasks") };
        more.MouseLeftButtonUp += (_, e) => { e.Handled = true; onMore(); }; Children.Add(more);
    }
    internal static int Capacity(double height, int count)
    {
        int slots = Math.Max(0, (int)Math.Floor((height + .001) / RowHeight));
        return slots >= count ? count : Math.Max(0, slots - 1);
    }
    protected override Size MeasureOverride(Size available)
    {
        heights = new double[count];
        for (int i = 0; i < Children.Count; i++)
        {
            Children[i].Measure(new Size(Math.Max(0, available.Width), entries == null || i == count ? rowUnit : double.PositiveInfinity));
            if (i < count) heights[i] = entries == null ? rowUnit : Math.Max(rowUnit, Math.Ceiling((Children[i].DesiredSize.Height - .001) / rowUnit) * rowUnit);
        }
        return new Size(0, 0);
    }
    protected override Size ArrangeOverride(Size final)
    {
        double budget = Math.Max(0, final.Height);
        bool allFit = heights.Length == count && heights.Sum() <= budget + .001;
        if (!allFit) budget = Math.Max(0, budget - rowUnit);
        var sizes = new double[count]; VisibleRows = 0;
        // Entries are already depth-first. An expanded child immediately follows its parent.
        for (int i = 0; i < count && budget + .001 >= rowUnit; i++)
        {
            double height = heights.Length == count ? heights[i] : rowUnit;
            if (entries != null && entries[i].Expanded && i + 1 < count && entries[i + 1].Depth > entries[i].Depth && budget + .001 >= rowUnit * 2)
                height = Math.Min(height, Math.Floor((budget - rowUnit + .001) / rowUnit) * rowUnit);
            sizes[i] = Math.Min(height, Math.Floor((budget + .001) / rowUnit) * rowUnit);
            VisibleRows++; budget -= sizes[i];
            if (sizes[i] + .001 < height) break;
        }
        if (VisibleRows == count && count > 0)
        {
            double spare = Math.Floor((final.Height - sizes.Sum() + .001) / rowUnit) * rowUnit;
            for (int i = 0; i < count && spare > 0; i++) { double add = Math.Max(0, Math.Min(heights[i] - sizes[i], spare)); sizes[i] += add; spare -= add; }
        }
        MoreInHeader = Remaining > 0 && final.Height + .001 < rowUnit;
        headerOverflow(MoreInHeader ? Remaining : 0);
        double top = 0;
        for (int i = 0; i < count; i++) { ArrangeRow((FrameworkElement)Children[i], i < VisibleRows, top, final.Width, sizes[i]); top += sizes[i]; }
        more.Text = Remaining > 0 ? "+" + Remaining.ToString(CultureInfo.InvariantCulture) : "";
        ArrangeRow(more, Remaining > 0 && !MoreInHeader, top, final.Width, rowUnit);
        return final;
    }
    static void ArrangeRow(FrameworkElement row, bool shown, double top, double width, double height)
    {
        row.Clip = shown ? null : Geometry.Empty;
        row.Arrange(new Rect(0, shown ? top : 0, Math.Max(0, width), shown ? height : 0));
    }
}
