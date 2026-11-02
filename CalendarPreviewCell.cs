using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopCalendar;
internal sealed record CalendarTodoEntry(string Title, string TimePrefix, bool Completed, string Color, bool Emphasis, Action Toggle);

// Fixed date/information rows cannot be displaced by the adaptive preview body.
internal sealed class CalendarPreviewCell : Grid
{
    internal CalendarTaskPanel Preview { get; }
    internal TextBlock HeaderMore { get; }
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
    internal CalendarPreviewCell(DateOnly date, CalendarTodoEntry[] tasks, bool currentMonth, bool backgroundColors, Action more)
    {
        Margin = new Thickness(4, 4, 2, 2); ClipToBounds = true;
        RowDefinitions.Add(new() { Height = new GridLength(21) }); RowDefinitions.Add(new() { Height = new GridLength(3) }); RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var header = new Grid(); header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = date.Day.ToString(CultureInfo.InvariantCulture), FontSize = 14, FontWeight = date == DateOnly.FromDateTime(DateTime.Today) ? FontWeights.Bold : FontWeights.Normal, Foreground = PrototypeViews.Brush(currentMonth ? "#EDF2F5" : "#6E7C89") });
        HeaderMore = new TextBlock { FontSize = 9, Foreground = PrototypeViews.Brush("#A3D9C4"), Cursor = System.Windows.Input.Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, ToolTip = MainWindow.Text("PlannerAllTasks") };
        HeaderMore.MouseLeftButtonUp += (_, e) => { e.Handled = true; more(); };
        SetColumn(HeaderMore, 1); header.Children.Add(HeaderMore); Children.Add(header);
        Preview = new CalendarTaskPanel(tasks, hidden => HeaderMore.Text = hidden == 0 ? "" : "+" + hidden.ToString(CultureInfo.InvariantCulture), backgroundColors, more);
        SetRow(Preview, 2); Children.Add(Preview);
    }
}

// Desired height is independent of preview count. Arrangement only clips/repositions
// fixed-height rows, without changing Visibility, rebuilding children or parent sizing.
internal sealed class CalendarTaskPanel : Panel
{
    internal const double RowHeight = 16;
    readonly int count;
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
    internal CalendarTaskPanel(CalendarTodoEntry[] tasks, Action<int> headerOverflow, bool backgrounds, Action onMore)
    {
        count = tasks.Length; this.headerOverflow = headerOverflow; ClipToBounds = true;
        foreach (var task in tasks)
        {
            var row = new DockPanel { Height = RowHeight, Opacity = task.Completed ? .6 : 1 };
            var check = new CheckBox { IsChecked = task.Completed, Margin = new Thickness(0, 0, 2, 0), Width = 13, VerticalAlignment = VerticalAlignment.Center, ToolTip = MainWindow.Text("PlannerToggle") };
            check.Click += (_, e) => { check.IsChecked = task.Completed; e.Handled = true; task.Toggle(); };
            row.Children.Add(check);
            var text = new TextBlock { Text = task.TimePrefix + task.Title, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = task.TimePrefix + task.Title,
                FontWeight = task.Emphasis ? FontWeights.SemiBold : FontWeights.Normal, Foreground = PrototypeViews.Brush(task.Completed ? "#76838E" : backgrounds ? "#EDF2F5" : task.Color), TextDecorations = task.Completed ? TextDecorations.Strikethrough : null };
            var color = (Color)ColorConverter.ConvertFromString(task.Color); color.A = 48;
            row.Children.Add(new Border { Background = backgrounds ? new SolidColorBrush(color) : Brushes.Transparent, CornerRadius = new CornerRadius(3), Child = text });
            Children.Add(row);
        }
        more = new TextBlock { FontSize = 10, Height = RowHeight, Foreground = PrototypeViews.Brush("#A2AFBC"), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = MainWindow.Text("PlannerAllTasks") };
        more.MouseLeftButtonUp += (_, e) => { e.Handled = true; onMore(); }; Children.Add(more);
    }
    internal static int Capacity(double height, int count)
    {
        int slots = Math.Max(0, (int)Math.Floor((height + .001) / RowHeight));
        return slots >= count ? count : Math.Max(0, slots - 1);
    }
    protected override Size MeasureOverride(Size available)
    {
        foreach (UIElement child in Children) child.Measure(new Size(available.Width, RowHeight));
        return new Size(0, 0);
    }
    protected override Size ArrangeOverride(Size final)
    {
        VisibleRows = Capacity(final.Height, count);
        MoreInHeader = Remaining > 0 && final.Height + .001 < RowHeight;
        headerOverflow(MoreInHeader ? Remaining : 0);
        for (int i = 0; i < count; i++) ArrangeRow((FrameworkElement)Children[i], i < VisibleRows, i * RowHeight, final.Width);
        more.Text = Remaining > 0 ? "+" + Remaining.ToString(CultureInfo.InvariantCulture) : "";
        ArrangeRow(more, Remaining > 0 && !MoreInHeader, VisibleRows * RowHeight, final.Width);
        return final;
    }
    static void ArrangeRow(FrameworkElement row, bool shown, double top, double width)
    {
        row.Clip = shown ? null : Geometry.Empty;
        row.Arrange(new Rect(0, shown ? top : 0, Math.Max(0, width), shown ? RowHeight : 0));
    }
}
