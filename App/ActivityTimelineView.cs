using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopCalendar.Core;

namespace DesktopCalendar;

internal sealed partial class TimelineView
{
    PlannerController? planner;
    MainWindow? owner;
    internal DateOnly[] Dates { get; private set; } = [];
    readonly List<Border> ghosts = new();
    internal sealed record Gesture(Activity? Original, ActivityPeriod? Period, DateTime Pointer, string Edge);
    internal Gesture? CurrentGesture { get; private set; }
    internal ActivityPeriod? PreviewPeriod { get; private set; }
    internal bool FailedGesture { get; private set; }
    DateTime lastPointer;
    int lastStep = 5;
    bool dragged, doubleClick;
    Point down;
    readonly DispatcherTimer singleClick = new() { Interval = TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime + 30) };
    ActivitySegment? clicked;
    internal void Bind(MainWindow window, PlannerController controller)
    {
        owner = window; planner = controller; Focusable = true; planner.Changed += RefreshActivities;
        foreach (var canvas in days) canvas.Background = Brushes.Transparent;
        Track.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (FailedGesture) { e.Handled = true; Focus(); return; }
            if (owner.PlannerDialogDepth > 0) return; singleClick.Stop(); var point = e.GetPosition(Track); var date = PointerDate(point); if (date == null) return;
            var card = AncestorCard(e.OriginalSource as DependencyObject); var segment = card?.Tag as ActivitySegment;
            if (e.ClickCount == 2 && segment != null) { singleClick.Stop(); doubleClick = true; clicked = segment; e.Handled = true; return; }
            doubleClick = false; down = point; clicked = segment;
            string edge = "create";
            if (segment != null && card != null) { double y = e.GetPosition(card).Y; edge = card.ActualHeight >= 14 && y <= 5 ? "start" : card.ActualHeight >= 14 && y >= card.ActualHeight - 5 ? "end" : "move"; }
            if (planner.Writable) { BeginGesture(segment, date.Value, edge); if (!Mouse.Capture(Track)) CancelGesture(); }
            e.Handled = true;
        };
        Track.PreviewMouseMove += (_, e) =>
        {
            if (CurrentGesture == null || e.LeftButton != MouseButtonState.Pressed) return; var p = e.GetPosition(Track);
            if (!dragged && (Math.Abs(p.X - down.X) > SystemParameters.MinimumHorizontalDragDistance || Math.Abs(p.Y - down.Y) > SystemParameters.MinimumVerticalDragDistance)) { dragged = true; singleClick.Stop(); }
            if (dragged && PointerDate(p) is DateTime time) PreviewGesture(time, (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 1 : 5);
            e.Handled = true;
        };
        PreviewMouseLeftButtonUp += async (_, e) =>
        {
            if (doubleClick && clicked != null) { doubleClick = false; var activity = planner.State?.Activities.FirstOrDefault(a => a.Id == clicked.ActivityId); if (activity != null) ActivityDialogs.Edit(owner, planner, activity); e.Handled = true; return; }
            bool changed = dragged; var target = clicked;
            if (changed && !new Rect(RenderSize).Contains(e.GetPosition(this))) { CancelGesture(); e.Handled = true; return; }
            if (CurrentGesture != null) { if (changed) { if (PointerDate(e.GetPosition(Track)) is DateTime time) PreviewGesture(time, (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 1 : 5); await CommitGestureAsync(); } else CancelGesture(); }
            if (!changed && target != null) { clicked = target; singleClick.Stop(); singleClick.Start(); } e.Handled = target != null || changed;
        };
        singleClick.Tick += (_, _) => { singleClick.Stop(); if (clicked != null && !dragged && owner.PlannerDialogDepth == 0) ActivityDialogs.Detail(owner, planner, clicked.ActivityId, clicked.PeriodId, clicked); };
        PreviewMouseRightButtonDown += (_, e) => { if (CurrentGesture != null) { CancelGesture(); e.Handled = true; } };
        PreviewKeyDown += async (_, e) => { if (e.Key == Key.Escape && CurrentGesture != null) { CancelGesture(); e.Handled = true; } else if (e.Key == Key.Enter && FailedGesture) { e.Handled = true; await CommitGestureAsync(); } };
        LostMouseCapture += (_, _) => { if (CurrentGesture != null) CancelGesture(); };
        RefreshActivities();
    }
    internal void Unbind() { singleClick.Stop(); CancelGesture(); if (planner != null) planner.Changed -= RefreshActivities; }
    static Border? AncestorCard(DependencyObject? source)
    {
        for (var node = source; node != null; node = node is Visual ? VisualTreeHelper.GetParent(node) : null) if (node is Border card && card.Tag is ActivitySegment) return card;
        return null;
    }
    DateTime? PointerDate(Point point)
    {
        if (Dates.Length == 0 || point.X < 40) return null;
        int index = 0; for (int i = 0; i < days.Length; i++) { var p = Track.TranslatePoint(point, days[i]); if (p.X >= 0 && p.X <= days[i].ActualWidth) { index = i; break; } if (p.X > days[i].ActualWidth) index = i; }
        return Dates[index].ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified).AddMinutes(Math.Clamp(point.Y / HourHeight * 60, 0, 1440));
    }
    internal void RefreshActivities()
    {
        if (planner == null || CurrentGesture != null) return; Cards.Clear(); foreach (var canvas in days) canvas.Children.Clear();
        var date = planner.SelectedDate; int back = planner.State == null ? 0 : ((int)date.DayOfWeek - (int)planner.State.Options.WeekStart + 7) % 7;
        int first = days.Length == 1 ? Math.Min(date.DayNumber, DateOnly.MaxValue.DayNumber - 1) : Math.Clamp(date.DayNumber - back, 0, DateOnly.MaxValue.DayNumber - 7);
        Dates = Enumerable.Range(0, days.Length).Select(i => DateOnly.FromDayNumber(first + i)).ToArray();
        for (int i = 0; i < days.Length; i++)
        {
            if (planner.State == null || Dates[i] == DateOnly.MaxValue) continue;
            var slots = LayoutActivities(planner.State, Dates[i], planner.State.Options.TimeZoneId, i);
            foreach (var slot in slots)
            {
                var a = planner.State.Activities.Single(a => a.Id == slot.Segment!.ActivityId); string color = a.Color ?? planner.State.Categories.FirstOrDefault(c => c.Id == a.CategoryId)?.Color ?? "#62A9E8";
                var body = new StackPanel { ClipToBounds = true }; var title = Text((a.Emoji.Length > 0 ? a.Emoji + " " : "") + a.Title, days.Length == 1 ? 12 : 10, ThemeColors.Foreground(ThemeColors.Soft(color))); title.FontWeight = FontWeights.SemiBold; body.Children.Add(title);
                string time = slot.Segment!.Start.ToOffset(slot.Segment.Offset).ToString("HH:mm zzz") + "–" + slot.Segment.End.ToOffset(slot.Segment.Offset).ToString("HH:mm");
                body.Children.Add(Text(time, 9, "#C3CDD5")); if (days.Length == 1 && a.Summary.Length > 0) body.Children.Add(Text(a.Summary, 10, "#C3CDD5"));
                var card = new Border { Tag = slot.Segment, Child = body, Background = ThemeColors.Brush(ThemeColors.Soft(color)), BorderBrush = ThemeColors.Brush(ThemeColors.Accent(color)), BorderThickness = new Thickness(2, 0, 0, 0), CornerRadius = new CornerRadius(5), Padding = new Thickness(4, 3, 2, 2), ClipToBounds = true, ToolTip = a.Emoji + " " + a.Title + "\n" + time + "\n" + a.Summary + "\n" + MainWindow.Text("ActivityHint") };
                card.MouseMove += (_, e) => { double y = e.GetPosition(card).Y; card.Cursor = card.ActualHeight >= 14 && (y < 5 || y > card.ActualHeight - 5) ? Cursors.SizeNS : Cursors.Hand; };
                Cards.Add((card, slot)); days[i].Children.Add(card);
            }
            if (slots.Length == 0) { var label = Text(MainWindow.Text(days.Length == 1 ? "ActivityEmpty" : "ActivityEmptyShort"), 10, "#8FA0AE"); label.TextWrapping = TextWrapping.Wrap; label.Width = Math.Max(20, days[i].ActualWidth - 8); Canvas.SetTop(label, 8 * HourHeight); days[i].Children.Add(label); }
        }
        PositionCards();
    }
    internal static Slot[] LayoutActivities(PlannerState state, DateOnly date, string zone, int day)
    {
        var result = new List<Slot>(); var group = new List<Slot>(); var ends = new List<double>(); double groupEnd = -1;
        void Finish() { foreach (var slot in group) result.Add(slot with { Lanes = ends.Count }); group.Clear(); ends.Clear(); }
        foreach (var s in ActivityTimeline.On(state, date, zone))
        {
            if (s.StartHour >= groupEnd) Finish(); int lane = ends.FindIndex(e => e <= s.StartHour); if (lane < 0) { lane = ends.Count; ends.Add(s.EndHour); } else ends[lane] = s.EndHour;
            var a = state.Activities.Single(a => a.Id == s.ActivityId); group.Add(new(new(day, s.StartHour, s.EndHour, a.Title, a.Summary, 0), lane, 0, s.StartHour, s.EndHour, s)); groupEnd = Math.Max(groupEnd, s.EndHour);
        }
        Finish(); return result.ToArray();
    }
    internal void BeginGesture(ActivitySegment? segment, DateTime pointer, string edge)
    {
        if (planner?.Writable != true) return; var a = segment == null ? null : planner.State!.Activities.Single(a => a.Id == segment.ActivityId);
        CurrentGesture = new(a, a?.Intervals.Single(p => p.Id == segment!.PeriodId), pointer, edge); PreviewPeriod = null; FailedGesture = false; dragged = false; Focus();
    }
    internal bool PreviewGesture(DateTime pointer, int step = 5)
    {
        if (CurrentGesture == null || planner?.State == null) return false; var gesture = CurrentGesture; lastPointer = pointer; lastStep = step;
        foreach (var ghost in ghosts) ((Canvas)ghost.Parent).Children.Remove(ghost); ghosts.Clear();
        try
        {
            double minutes = Math.Round((pointer - gesture.Pointer).TotalMinutes / step) * step;
            if (gesture.Period != null) PreviewPeriod = ActivityTimeline.Transform(gesture.Period, TimeSpan.FromMinutes(minutes), planner.State.Options.TimeZoneId, gesture.Edge);
            else
            {
                var a = gesture.Pointer.Date.AddMinutes(Math.Floor(gesture.Pointer.TimeOfDay.TotalMinutes / step) * step); var b = a.AddMinutes(minutes);
                if (b == a) b = a.AddMinutes(step); if (b < a) (a, b) = (b, a);
                PreviewPeriod = new(Guid.NewGuid(), ActivityTimeline.Local(a, planner.State.Options.TimeZoneId), ActivityTimeline.Local(b, planner.State.Options.TimeZoneId));
            }
            var preview = (gesture.Original ?? new Activity { Id = Guid.NewGuid(), Title = "" }) with { Intervals = [PreviewPeriod] };
            var state = planner.State with { Activities = [preview] };
            for (int i = 0; i < Dates.Length; i++) foreach (var part in ActivityTimeline.On(state, Dates[i], planner.State.Options.TimeZoneId))
            {
                var ghost = new Border { Width = Math.Max(0, days[i].ActualWidth - 6), Height = (part.EndHour - part.StartHour) * HourHeight, Background = ThemeColors.Brush("#99557982"), BorderBrush = ThemeColors.Brush("#D8EAF2"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), IsHitTestVisible = false, Child = Text(part.Start.ToOffset(part.Offset).ToString("HH:mm") + "–" + part.End.ToOffset(part.Offset).ToString("HH:mm"), 10, "#FFFFFF") };
                Canvas.SetTop(ghost, part.StartHour * HourHeight); Canvas.SetLeft(ghost, 3); days[i].Children.Add(ghost); ghosts.Add(ghost);
            }
            ToolTip = MainWindow.Text("ActivityGestureCancel"); return true;
        }
        catch (Exception ex) when (ex is PlannerException or ArgumentOutOfRangeException) { PreviewPeriod = null; ToolTip = MainWindow.Text("ActivityGestureInvalid"); return false; }
    }
    internal async System.Threading.Tasks.Task<bool> CommitGestureAsync()
    {
        var g = CurrentGesture; var period = PreviewPeriod; var pointer = lastPointer; var step = lastStep; CancelGesture(); if (g == null || period == null || planner == null || owner == null) return false;
        if (g.Original == null) return ActivityDialogs.Edit(owner, planner, draft: ActivityDialogs.Draft(planner, period));
        var current = planner.State!.Activities.SingleOrDefault(a => a.Id == g.Original.Id);
        if (JsonSerializer.Serialize(current) != JsonSerializer.Serialize(g.Original)) { CurrentGesture = g; PreviewGesture(pointer, step); FailedGesture = true; planner.Report("ActivityStale"); return false; }
        bool ok = await planner.ExecuteAsync(new SaveActivity(current! with { Intervals = [.. current!.Intervals.Select(p => p.Id == period.Id ? period : p)] }, false));
        if (!ok) { CurrentGesture = g; PreviewGesture(pointer, step); FailedGesture = true; planner.Report("ActivityDragSaveFailed", planner.Diagnostic); ToolTip = MainWindow.Text("ActivityDragSaveFailed"); Focus(); }
        return ok;
    }
    internal void CancelGesture()
    {
        CurrentGesture = null; PreviewPeriod = null; FailedGesture = false; dragged = false; foreach (var ghost in ghosts) if (ghost.Parent is Canvas canvas) canvas.Children.Remove(ghost); ghosts.Clear(); if (Mouse.Captured == Track) Mouse.Capture(null); if (planner != null) RefreshActivities();
    }
}
