using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopCalendar;

// One scale drives the entire 24-hour layout. No data editing or business persistence.
internal sealed class TimelineView : ScrollViewer
{
    internal const double MinimumHour = 24, MaximumHour = 160, DefaultHour = 50;
    internal double HourHeight { get; private set; }
    internal event Action? ScaleChanged;
    internal Grid Track { get; } = new() { ClipToBounds = true };
    internal readonly List<(TextBlock Label, Border Line, int Hour)> Ticks = new();
    internal readonly List<(Border Card, Slot Slot)> Cards = new();
    readonly Canvas[] days;
    bool firstLoad = true;
    internal sealed record Slot(DemoContent.Block Block, int Lane, int Lanes, double Start, double End);
    internal TimelineView(bool week, int day, double hour = DefaultHour)
    {
        HourHeight = double.IsFinite(hour) ? Math.Clamp(hour, MinimumHour, MaximumHour) : DefaultHour;
        Track.ColumnDefinitions.Add(new() { Width = new GridLength(40) });
        var labels = new Canvas(); Track.Children.Add(labels);
        for (int h = 0; h < 24; h++)
        {
            var label = Text(h.ToString("00", CultureInfo.InvariantCulture) + ":00", 9, "#8FA0AE");
            var line = new Border { Height = 1, Background = PrototypeViews.Brush("#16FFFFFF"), VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumnSpan(line, week ? 8 : 2); Track.Children.Add(line); labels.Children.Add(label);
            Ticks.Add((label, line, h));
        }
        days = new Canvas[week ? 7 : 1];
        for (int i = 0; i < days.Length; i++)
        {
            Track.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            var canvas = days[i] = new Canvas { ClipToBounds = true };
            Grid.SetColumn(canvas, i + 1); Track.Children.Add(canvas);
            foreach (var slot in Layout(DemoContent.Blocks, week ? i : day))
            {
                var block = slot.Block;
                var text = new StackPanel { ClipToBounds = true };
                var title = Text(block.Title, week ? 11 : 12, DemoContent.Colors[block.Color]); title.FontWeight = FontWeights.SemiBold;
                text.Children.Add(title);
                string time = TimeSpan.FromHours(block.Start).ToString(@"hh\:mm", CultureInfo.InvariantCulture) + "–" + TimeSpan.FromHours(block.End).ToString(@"hh\:mm", CultureInfo.InvariantCulture);
                var timeLabel = Text(time, 9, "#C3CDD5"); timeLabel.Margin = new Thickness(0, 5, 0, 0); text.Children.Add(timeLabel);
                if (!week) { var note = Text(block.Note, 10, "#ABBCC5"); note.Margin = new Thickness(0, 8, 0, 0); text.Children.Add(note); }
                var card = new Border { Background = PrototypeViews.Brush(block.Color == 0 ? "#D1344D49" : block.Color == 1 ? "#D144405C" : "#D14F473C"),
                    BorderBrush = PrototypeViews.Brush(DemoContent.Colors[block.Color]), BorderThickness = new Thickness(2, 0, 0, 0),
                    CornerRadius = new CornerRadius(5), Padding = new Thickness(week ? 5 : 9, 5, 3, 3), Child = text, ClipToBounds = true,
                    ToolTip = block.Title + "\n" + time + "\n" + block.Note + "\n" + MainWindow.Text("TimelineTip") };
                Cards.Add((card, slot)); canvas.Children.Add(card);
            }
            canvas.SizeChanged += (_, _) => PositionCards();
        }
        Content = Track; VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
        ApplyScale();
        Loaded += (_, _) => { if (firstLoad) { firstLoad = false; ScrollToVerticalOffset(6 * HourHeight); } };
        PreviewMouseWheel += (_, e) =>
        {
            if (Wheel(e.Delta, (Keyboard.Modifiers & ModifierKeys.Control) != 0, e.GetPosition(this).Y)) e.Handled = true;
        };
        ToolTip = MainWindow.Text("TimelineZoomTip");
    }
    static TextBlock Text(string text, double size, string color) => new() { Text = text, FontSize = size,
        Foreground = PrototypeViews.Brush(color), TextTrimming = TextTrimming.CharacterEllipsis };
    internal bool Wheel(int delta, bool control, double pointerY)
    {
        if (!control) return false;
        UpdateLayout();
        double y = Math.Clamp(pointerY, 0, ViewportHeight), anchor = (VerticalOffset + y) / HourHeight;
        double next = Math.Clamp(HourHeight * Math.Pow(1.1, delta / 120.0), MinimumHour, MaximumHour);
        if (Math.Abs(next - HourHeight) < .00001) return true;
        HourHeight = next; ApplyScale(); UpdateLayout();
        ScrollToVerticalOffset(Math.Clamp(anchor * HourHeight - y, 0, Math.Max(0, Track.Height - ViewportHeight)));
        UpdateLayout(); ScaleChanged?.Invoke();
        return true;
    }
    void ApplyScale()
    {
        Track.Height = 24 * HourHeight;
        foreach (var tick in Ticks)
        {
            Canvas.SetTop(tick.Label, tick.Hour * HourHeight);
            tick.Line.Margin = new Thickness(40, tick.Hour * HourHeight, 0, 0);
        }
        PositionCards();
    }
    void PositionCards()
    {
        foreach (var (card, slot) in Cards)
        {
            var canvas = (Canvas)card.Parent;
            double lane = canvas.ActualWidth / slot.Lanes;
            card.Width = Math.Max(0, lane - 6);
            card.Height = (slot.End - slot.Start) * HourHeight;
            Canvas.SetLeft(card, slot.Lane * lane + 3); Canvas.SetTop(card, slot.Start * HourHeight);
        }
    }
    internal static Slot[] Layout(IEnumerable<DemoContent.Block> blocks, int day)
    {
        var result = new List<Slot>(); var group = new List<Slot>(); var ends = new List<double>(); double groupEnd = -1;
        void Finish() { foreach (var slot in group) result.Add(slot with { Lanes = ends.Count }); group.Clear(); ends.Clear(); }
        foreach (var block in blocks.Where(b => b.Day == day && b.Start < 24 && b.End > 0 && b.End > b.Start).OrderBy(b => b.Start).ThenBy(b => b.End))
        {
            double start = Math.Max(0, block.Start), end = Math.Min(24, block.End);
            if (start >= groupEnd) Finish();
            int lane = ends.FindIndex(e => e <= start);
            if (lane < 0) { lane = ends.Count; ends.Add(end); } else ends[lane] = end;
            group.Add(new(block, lane, 0, start, end)); groupEnd = Math.Max(groupEnd, end);
        }
        Finish(); return result.ToArray();
    }
}
