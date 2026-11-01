using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopCalendar;

internal static class StageFiveChecks
{
    internal static async Task Run(MainWindow window, Action<string, bool> check, string output)
    {
        void Layout(FrameworkElement view, double width, double height)
        { view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout(); }
        foreach (int count in new[] { 0, 1, 3, 10 })
        {
            var tasks = Enumerable.Range(0, count).Select(i => new DemoTodo("cell-" + i, DemoContent.CalendarTasks[i % 7], i, i % 3 == 2)).ToArray();
            var cell = new CalendarPreviewCell(new DateTime(2026, 10, 29), tasks, true, "固定信息 · 示例");
            foreach (double height in new[] { 28d, 57.9, 58.1, 73.9, 74.1, 89.9, 90.1, 220 })
            {
                Layout(cell, 108, height);
                double body = cell.Preview.ActualHeight;
                int visible = CalendarTaskPanel.Capacity(body, count);
                check("calendar complete rows + reserved overflow count=" + count + "/height=" + height,
                    cell.Preview.VisibleRows == visible && cell.Preview.Remaining == count - visible &&
                    (count == visible || cell.Preview.MoreInHeader || (visible + 1) * CalendarTaskPanel.RowHeight <= body + .01));
                int before = cell.Preview.VisibleRows;
                for (int i = 0; i < 5; i++) cell.UpdateLayout();
                check("calendar repeat layout stable count=" + count + "/height=" + height, before == cell.Preview.VisibleRows && cell.IsMeasureValid && cell.IsArrangeValid);
            }
            Layout(cell, 108, 28);
            check("tiny calendar permits zero rows and header +N count=" + count, cell.Preview.VisibleRows == 0 && cell.HeaderMore.Text == (count > 0 ? "+" + count : ""));
            Layout(cell, 108, 220);
            check("tall cell shows all complete tasks count=" + count, cell.Preview.VisibleRows == count && cell.Preview.Remaining == 0);
            if (count >= 3) check("completed preview keeps strike and muted foreground", ((TextBlock)cell.Preview.Children[2]).TextDecorations == TextDecorations.Strikethrough);
        }
        foreach (int count in new[] { 1, 3, 10 })
        foreach (int slotCount in Enumerable.Range(0, count + 2))
        {
            int below = CalendarTaskPanel.Capacity(slotCount * 16 - .1, count), above = CalendarTaskPanel.Capacity(slotCount * 16 + .1, count);
            check("calendar threshold monotonic + overflow reservation " + count + "/" + slotCount, above >= below && above <= count && (slotCount == 0 || above == count || above + 1 <= slotCount));
        }
        foreach (int day in new[] { 0, 1, 2, 3 })
        {
            var tasks = DemoContent.CalendarTodos(day);
            check("actual calendar fixtures count/group " + day, tasks.Length == new[] { 0, 1, 3, 10 }[day] && tasks.SequenceEqual(tasks.OrderBy(t => t.Completed).ThenBy(t => t.OriginalOrder)));
        }
        foreach (int month in new[] { 10, 11 })
        {
            var days = PrototypeViews.CalendarDays(2026, month);
            days.Background = PrototypeViews.Brush("#19232E");
            check("calendar five/six week row count month=" + month, days.Rows == (month == 10 ? 5 : 6) && days.Children.Count == days.Rows * 7);
            foreach (double height in new[] { 300d, 610, 1050 })
            {
                Layout(days, 770, height);
                var cells = days.Children.OfType<Button>().Select(b => (CalendarPreviewCell)b.Content).ToArray();
                check("calendar equal dates/heights and no preview overflow " + month + "/" + height,
                    cells.Max(c => c.ActualHeight) - cells.Min(c => c.ActualHeight) < 1 && cells.All(c => c.Preview.VisibleRows == CalendarTaskPanel.Capacity(c.Preview.ActualHeight, c.Preview.VisibleRows + c.Preview.Remaining)));
                Render(days, Path.Combine(output, "calendar-" + month + "-" + height + ".png"));
            }
        }
        var slotsTest = new[] { new DemoContent.Block(0, -1, .5, "clipped", "", 0), new(0, 9, 11, "long", "", 0),
            new(0, 10, 10.5, "short", "", 1), new(0, 10.25, 12, "overlap", "", 2), new(0, 12, 13, "adjacent", "", 0), new(0, 23, 25, "end", "", 0) };
        var slots = TimelineView.Layout(slotsTest, 0);
        check("overlap cluster has three lanes and adjacency resets", slots.Where(s => s.Start >= 9 && s.Start < 12).All(s => s.Lanes == 3) && slots.Single(s => s.Start == 12).Lanes == 1);
        check("spanning blocks clipped to full 24 hours", slots.First().Start == 0 && slots.Last().End == 24);
        check("overlap lanes never intersect in time", slots.All(a => slots.All(b => a == b || a.Lane != b.Lane || a.End <= b.Start || b.End <= a.Start)));

        window.RestoreNormal(); window.SetPage(false); window.UpdateLayout(); await Task.Delay(100);
        var daily = window.DailyTimeline; var weekly = window.WeeklyTimeline;
        double oldWeek = weekly.HourHeight;
        daily.ScrollToVerticalOffset(7 * daily.HourHeight); daily.UpdateLayout(); await Task.Delay(50);
        double y = Math.Min(120, daily.ViewportHeight / 2), anchor = (daily.VerticalOffset + y) / daily.HourHeight;
        check("plain wheel keeps zoom and remains unhandled", !daily.Wheel(-120, false, y) && daily.HourHeight == 50);
        daily.Wheel(120, true, y); await Task.Delay(50);
        check("Ctrl wheel up enlarges with stable pointer time", daily.HourHeight > 50 && Math.Abs((daily.VerticalOffset + y) / daily.HourHeight - anchor) < .03);
        check("day zoom independent of week", weekly.HourHeight == oldWeek);
        for (int i = 0; i < 8; i++) daily.Wheel(12, true, y);
        await Task.Delay(50);
        check("rapid fractional wheel retains anchor", Math.Abs((daily.VerticalOffset + y) / daily.HourHeight - anchor) < .03);
        double keepHour = daily.HourHeight, keepOffset = daily.VerticalOffset;
        window.SetPage(true); window.UpdateLayout(); weekly.Wheel(-120, true, 100); await Task.Delay(50);
        window.SetPage(false); window.UpdateLayout(); await Task.Delay(50);
        check("page switch retains scale and vertical offset", daily.HourHeight == keepHour && Math.Abs(daily.VerticalOffset - keepOffset) < 1 && weekly.HourHeight < oldWeek);
        foreach (var timeline in new[] { daily, weekly })
        {
            window.SetPage(timeline == weekly); window.UpdateLayout();
            foreach (int delta in new[] { -12000, 12000, -120, -12000 })
            {
                timeline.Wheel(delta, true, 100); await Task.Delay(30);
                check("timeline unified 24h scale and limits " + (timeline == weekly) + "/" + delta,
                    timeline.HourHeight >= 24 && timeline.HourHeight <= 160 && Math.Abs(timeline.Track.Height - 24 * timeline.HourHeight) < .01 &&
                    timeline.Ticks.All(t => Math.Abs(Canvas.GetTop(t.Label) - t.Hour * timeline.HourHeight) < .01 && Math.Abs(t.Line.Margin.Top - t.Hour * timeline.HourHeight) < .01) &&
                    timeline.Cards.All(c => Math.Abs(Canvas.GetTop(c.Card) - c.Slot.Start * timeline.HourHeight) < .01 && Math.Abs(c.Card.Height - (c.Slot.End - c.Slot.Start) * timeline.HourHeight) < .01));
                Render(window.Surface, Path.Combine(output, "timeline-" + (timeline == weekly ? "week" : "day") + "-" + Math.Round(timeline.HourHeight) + ".png"));
            }
            timeline.ScrollToVerticalOffset(0); timeline.UpdateLayout(); await Task.Delay(20);
            double before = timeline.VerticalOffset;
            timeline.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.MouseWheelEvent });
            timeline.UpdateLayout(); await Task.Delay(50);
            check("ordinary routed wheel still scrolls without zoom " + (timeline == weekly), (timeline.ScrollableHeight == 0 || timeline.VerticalOffset > before) && timeline.HourHeight == TimelineView.MinimumHour);
        }
        var prefs = window.SnapshotPreferences();
        check("both zoom scales captured in window preferences", prefs.DayHourHeight == daily.HourHeight && prefs.WeekHourHeight == weekly.HourHeight);
        check("invalid timeline preference rejected", !(prefs with { DayHourHeight = double.NaN }).Valid() && !(prefs with { WeekHourHeight = 500 }).Valid());
        foreach (var side in new[] { DockSide.Left, DockSide.Right })
        {
            window.CollapseToEdge(side); window.UpdateLayout();
            var mark = (Border)window.RestoreButton.Template.FindName("EdgeMark", window.RestoreButton);
            double pixel = 1 / window.Scale;
            check("small edge visual retains large hit target " + side, Math.Abs(mark.ActualWidth - 9) <= pixel && Math.Abs(mark.ActualHeight - 48) <= pixel && window.RestoreButton.ActualWidth >= 28 - pixel && window.RestoreButton.ActualHeight >= 100 && window.Shell.Visibility == Visibility.Collapsed);
            var hit = window.Surface.InputHitTest(new Point(14, 10)) as DependencyObject;
            while (hit != null && hit != window.RestoreButton) hit = VisualTreeHelper.GetParent(hit);
            check("edge transparent margin still hits restore button " + side, hit == window.RestoreButton);
            Render(window.Surface, Path.Combine(output, "edge-small-" + side + ".png"));
            window.RestoreNormal();
        }
    }
    static void Render(FrameworkElement view, string path)
    {
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(view.ActualWidth)), Math.Max(1, (int)Math.Ceiling(view.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(view); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
}
