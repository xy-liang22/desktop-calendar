using System;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopCalendar.Core;

namespace DesktopCalendar;
// One app-local overlay, shared by both timelines. No Popup HWND or modal input loop.
internal sealed class ActivityDetailCard
{
    readonly MainWindow owner;
    readonly PlannerController controller;
    readonly Canvas layer = new() { ClipToBounds = true };
    internal Border Card { get; } = new() { Background = ThemeColors.Brush("#F5202E3B"), BorderBrush = ThemeColors.Brush("#526474"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Visibility = Visibility.Collapsed };
    internal Guid ActivityId { get; private set; }
    Guid periodId;
    ActivitySegment? preferred;
    string contentToken = "";
    Point? drag;
    Vector offset;
    Vector beforeDrag;
    internal bool IsOpen => Card.Visibility == Visibility.Visible;
    internal Rect AnchorBounds { get; private set; }
    internal ActivityDetailCard(MainWindow window, PlannerController c)
    {
        owner = window; controller = c; layer.Children.Add(Card); owner.Surface.Children.Add(layer);
        owner.PreviewMouseDown += Outside; owner.Surface.LayoutUpdated += Layout; c.Changed += Refresh;
        owner.Closed += (_, _) => { owner.PreviewMouseDown -= Outside; owner.Surface.LayoutUpdated -= Layout; c.Changed -= Refresh; };
    }
    static bool Within(DependencyObject? source, DependencyObject target)
    { for (var n = source; n != null; n = n is Visual ? VisualTreeHelper.GetParent(n) : n is FrameworkContentElement f ? f.Parent : LogicalTreeHelper.GetParent(n)) if (n == target) return true; return false; }
    void Outside(object sender, MouseButtonEventArgs e) { if (IsOpen && !Within(e.OriginalSource as DependencyObject, Card)) Hide(); }
    internal void Show(Guid id, Guid period, ActivitySegment? anchor = null)
    { ActivityId = id; periodId = period; preferred = anchor; offset = new(); contentToken = ""; Card.Visibility = Visibility.Visible; Refresh(); Position(); }
    internal void Hide() { Card.Visibility = Visibility.Collapsed; drag = null; if (Mouse.Captured is DependencyObject captured && Within(captured, Card)) Mouse.Capture(null); }
    void Refresh()
    {
        if (!IsOpen) return; var a = controller.State?.Activities.FirstOrDefault(x => x.Id == ActivityId); if (a == null) { Hide(); return; }
        string token = JsonSerializer.Serialize(a) + controller.DisplayZone + controller.Writable + string.Join(',', controller.State!.Goals.Where(g => g.Activities.Contains(a.Id)).Select(g => g.Id));
        if (token != contentToken) { contentToken = token; Build(a); }
        _ = owner.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Position));
    }
    void Layout(object? sender, EventArgs e) { if (IsOpen && drag == null) Position(); }
    internal static Rect Place(Rect anchor, Size card, Size client, Vector offset)
    {
        double top = anchor.Bottom + 8, left = anchor.X;
        if (client.Height - top < card.Height)
        {
            if (anchor.Top >= card.Height + 8) top = anchor.Top - card.Height - 8;
            else if (anchor.Left >= card.Width + 16) { left = anchor.Left - card.Width - 8; top = anchor.Top; }
            else if (client.Width - anchor.Right >= card.Width + 16) { left = anchor.Right + 8; top = anchor.Top; }
        }
        return new(Math.Clamp(left + offset.X, 8, Math.Max(8, client.Width - card.Width - 8)), Math.Clamp(top + offset.Y, 8, Math.Max(8, client.Height - card.Height - 8)), card.Width, card.Height);
    }
    void Position()
    {
        if (!IsOpen || owner.Mode != DisplayMode.Normal) { if (IsOpen) Hide(); return; }
        var timeline = owner.IsWeek ? owner.WeeklyTimeline : owner.DailyTimeline;
        var target = timeline.Cards.FirstOrDefault(x => x.Slot.Segment?.ActivityId == ActivityId && x.Slot.Segment.PeriodId == periodId && (preferred == null || x.Slot.Segment.Date == preferred.Date && x.Slot.Segment.Offset == preferred.Offset)).Card;
        if (target == null || !target.IsVisible) { Hide(); return; }
        try
        {
            var local = target.TransformToAncestor(timeline).TransformBounds(new Rect(target.RenderSize));
            if (!local.IntersectsWith(new Rect(timeline.RenderSize))) { Hide(); return; }
            AnchorBounds = target.TransformToAncestor(owner.Surface).TransformBounds(new Rect(target.RenderSize));
            double width = Math.Min(440, Math.Max(200, layer.ActualWidth - 24)), height = Math.Min(500, Math.Max(160, layer.ActualHeight - 28));
            if (Math.Abs(Card.Width - width) > .5 || double.IsNaN(Card.Width)) Card.Width = width; Card.MaxHeight = height;
            Card.Measure(new Size(width, height)); var bounds = Place(AnchorBounds, new Size(width, Math.Min(height, Card.DesiredSize.Height)), layer.RenderSize, offset);
            if (Math.Abs(Canvas.GetLeft(Card) - bounds.X) > .1 || double.IsNaN(Canvas.GetLeft(Card))) Canvas.SetLeft(Card, bounds.X);
            if (Math.Abs(Canvas.GetTop(Card) - bounds.Y) > .1 || double.IsNaN(Canvas.GetTop(Card))) Canvas.SetTop(Card, bounds.Y);
        }
        catch (InvalidOperationException) { Hide(); }
    }
    void Build(Activity a)
    {
        string T(string key) => MainWindow.Text(key); var root = new DockPanel(); var header = new DockPanel { Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
        var close = PlannerView.Button("×", Hide); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close); var title = PlannerView.Text(a.Emoji + " " + a.Title + (a.Reaction.Length > 0 ? " · " + a.Reaction : ""), 17); title.TextWrapping = TextWrapping.Wrap; header.Children.Add(title); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        header.PreviewMouseLeftButtonDown += (_, e) => { if (Within(e.OriginalSource as DependencyObject, close)) return; e.Handled = true; drag = e.GetPosition(layer); beforeDrag = offset; header.CaptureMouse(); };
        header.MouseMove += (_, e) => { if (drag is Point start && e.LeftButton == MouseButtonState.Pressed) { offset = beforeDrag + (e.GetPosition(layer) - start); var saved = drag; drag = null; Position(); drag = saved; } };
        header.MouseLeftButtonUp += (_, e) => { drag = null; header.ReleaseMouseCapture(); e.Handled = true; }; header.LostMouseCapture += (_, _) => drag = null;
        var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) }; DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
        void Action(string key, Action run, bool enabled = true) { var button = PlannerView.Button(T(key), () => { Hide(); run(); }); button.IsEnabled = enabled && controller.Writable; actions.Children.Add(button); }
        Action("ActivityEditAction", () => ActivityDialogs.Edit(owner, controller, a));
        Action("ActivityDelete", async () => { if (PlannerDialogs.Confirm(owner, T("ActivityDelete"), T("ActivityDeleteConfirm"))) await controller.ExecuteAsync(new DeleteActivity(a.Id)); });
        Action("ActivityDeletePeriod", async () => { if (!PlannerDialogs.Confirm(owner, T("ActivityDeletePeriod"), T("ActivityDeletePeriodConfirm"))) return; var current = controller.State!.Activities.FirstOrDefault(x => x.Id == a.Id); if (JsonSerializer.Serialize(current) != JsonSerializer.Serialize(a)) controller.Report("ActivityStale"); else await controller.ExecuteAsync(new SaveActivity(a with { Intervals = [.. a.Intervals.Where(p => p.Id != periodId)] }, false)); }, a.Intervals.Length > 1);
        Action("ActivityCopy", () => ActivityDialogs.Edit(owner, controller, draft: a with { Id = Guid.NewGuid(), Intervals = [.. a.Intervals.Select(p => p with { Id = Guid.NewGuid() })] }));
        var body = new StackPanel(); void Line(string text) { var t = PlannerView.Text(text, 11); t.TextWrapping = TextWrapping.Wrap; t.Margin = new Thickness(0, 4, 0, 4); body.Children.Add(t); }
        Line(a.CategoryId == null ? "" : Localization.Category(controller.State!.Categories.FirstOrDefault(c => c.Id == a.CategoryId)));
        foreach (var p in a.Intervals) Line(TimeZoneInfo.ConvertTime(p.Start, PlannerTime.Zone(controller.DisplayZone)).ToString("MM-dd HH:mm") + " → " + TimeZoneInfo.ConvertTime(p.End, PlannerTime.Zone(controller.DisplayZone)).ToString("MM-dd HH:mm") + (a.Summary.Length > 0 ? " · " + a.Summary.ReplaceLineEndings(" ") : ""));
        if (a.Todo != null) { try { Line(T("ActivityTodo") + " · " + PlannerQueries.Resolve(controller.State!, a.Todo).Title); } catch (PlannerException) { Line(a.FormerTodoTitle ?? ""); } }
        foreach (var goal in controller.State!.Goals.Where(g => g.Activities.Contains(a.Id))) Line(T("ActivityGoal") + " · " + goal.Title);
        body.Children.Add(new FlowDocumentScrollViewer { Document = MarkdownNotes.Render(a.Notes.Text, a.Notes.Format, new(controller.DataDirectory, () => controller.State)), IsToolBarVisible = false, MaxHeight = 270, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Card.Child = root;
    }
}
