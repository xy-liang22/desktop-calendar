using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal sealed partial class PlannerView
{
    internal ScrollViewer TodoScroll { get; } = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = true };
    internal EmojiTextBlock DragHint { get; } = Text(T("TodoDragHint"), 10, "#A2AFBC");
    Todo? dragSource, dragTarget;
    Point dragOrigin;
    bool dragging, acquiringCapture;
    int dropZone;
    internal bool TodoDragging => dragging;
    void SetupTodoDrag()
    {
        // Keep both the viewport and card geometry fixed while the preview text changes.
        DragHint.TextWrapping = TextWrapping.Wrap; DragHint.Height = 34; DragHint.MaxHeight = 34;
        TodoScroll.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(TodoDragKey), true); owner.Deactivated += TodoDragDeactivated;
        TodoScroll.PreviewMouseMove += (_, e) =>
        {
            // CaptureMouse can synchronously promote a new MouseMove while hit-testing changes.
            if (acquiringCapture) return;
            if (dragSource == null) return;
            if (e.LeftButton != MouseButtonState.Pressed) { CancelTodoDrag(); return; }
            ContinueTodoDrag(e.GetPosition(TodoScroll));
            if (dragging) e.Handled = true;
        };
        TodoScroll.PreviewMouseWheel += (_, e) => { if (!dragging) return; e.Handled = true; ScrollTodoDrag(e.Delta); };
        TodoScroll.PreviewMouseRightButtonDown += (_, e) => { if (dragging) { e.Handled = true; CancelTodoDrag(); } };
        TodoScroll.LostMouseCapture += (_, _) => { if (dragging) CancelTodoDrag(); };
        TodoScroll.PreviewMouseLeftButtonUp += async (_, e) =>
        {
            if (!dragging) { dragSource = null; return; }
            UpdateTodoDrop(e.GetPosition(TodoScroll)); var source = dragSource; var target = dragTarget; int zone = dropZone;
            e.Handled = true; CancelTodoDrag();
            if (source == null || target == null) return;
            if (zone == 0) PlannerDialogs.MoveHierarchy(owner, Controller, source, target);
            else if (source.ParentId != target.ParentId) PlannerDialogs.MoveHierarchy(owner, Controller, source, target.ParentId is Guid p ? Controller.FindTodo(p) : null, target.ParentId == null, target, zone > 0);
            else await DropAsync(source, target, zone > 0);
        };
    }
    void WireTodoDrag(Border row, Todo task, bool active)
    {
        row.AllowDrop = false;
        row.PreviewMouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount != 1 || !active || !Controller.Writable || InputOrigin(e.OriginalSource as DependencyObject, row)) return;
            BeginTodoDrag(task, e.GetPosition(TodoScroll));
        };
    }
    internal void BeginTodoDrag(Todo task, Point point) { if (!Controller.Writable) return; dragSource = task; dragOrigin = point; }
    internal void ContinueTodoDrag(Point point)
    {
        if (dragSource == null) return;
        if (!dragging && (Math.Abs(point.X - dragOrigin.X) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(point.Y - dragOrigin.Y) >= SystemParameters.MinimumVerticalDragDistance))
        {
            bool captured; acquiringCapture = true;
            try { captured = TodoScroll.CaptureMouse(); } finally { acquiringCapture = false; }
            // An independent close, refresh or button-up during capture still cancels the gesture.
            if (!captured || dragSource == null) { if (TodoScroll.IsMouseCaptured) TodoScroll.ReleaseMouseCapture(); CancelTodoDrag(); return; }
            dragging = true; TodoScroll.Focus();
        }
        if (dragging) UpdateTodoDrop(point);
    }
    void TodoDragKey(object sender, KeyEventArgs e) { if (dragSource != null && e.Key == Key.Escape) { CancelTodoDrag(); e.Handled = true; } }
    void TodoDragDeactivated(object? sender, EventArgs e) => CancelTodoDrag();
    internal bool ScrollTodoDrag(int delta)
    {
        if (!dragging) return false;
        bool moved = ScrollChaining.Route(TodoScroll, TodoScroll, delta);
        TodoScroll.UpdateLayout(); UpdateTodoDrop(Mouse.GetPosition(TodoScroll)); return moved;
    }
    internal static int TodoDropZone(double y, double height) => y < height * .25 ? -1 : y > height * .75 ? 1 : 0;
    void UpdateTodoDrop(Point p)
    {
        dragTarget = null; foreach (var row in Items.Children.OfType<Border>()) ResetDrop(row);
        DragHint.Text = T("TodoDragHint");
        if (dragSource == null || p.X < 0 || p.Y < 0 || p.X >= TodoScroll.ActualWidth - SystemParameters.VerticalScrollBarWidth || p.Y >= TodoScroll.ActualHeight) return;
        foreach (var row in Items.Children.OfType<Border>())
        {
            var q = TodoScroll.TranslatePoint(p, row);
            if (q.Y < 0 || q.Y > row.ActualHeight || row.Tag is not Todo target || target.Id == dragSource.Id || target.Status == TodoStatus.Cancelled) continue;
            int zone = TodoDropZone(q.Y, row.ActualHeight);
            if (zone != 0 && target.Status != dragSource.Status) { DragHint.Text = T("TodoDragSiblingOnly"); return; }
            // Reject self/descendant immediately, including a sibling insertion inside our subtree.
            for (var current = zone == 0 ? target : target.ParentId is Guid parent ? Controller.FindTodo(parent) : null; current != null; current = current.ParentId is Guid id ? Controller.FindTodo(id) : null)
                if (current.Id == dragSource.Id) { DragHint.Text = T("HierarchyCycle"); return; }
            dragTarget = target; dropZone = zone;
            // Changing border thickness rounds differently at 125/150% DPI. Color only.
            row.BorderBrush = zone == 0 ? ThemeColors.Brush("#C1AFE2") : new LinearGradientBrush(
                new GradientStopCollection { new(Color.FromRgb(163, 217, 196), 0), new(Color.FromRgb(163, 217, 196), .06), new(Color.FromRgb(48, 67, 82), .06), new(Color.FromRgb(48, 67, 82), 1) },
                zone < 0 ? new Point(0, 0) : new Point(0, 1), zone < 0 ? new Point(0, 1) : new Point(0, 0));
            DragHint.Text = T(zone == 0 ? "TodoDragChild" : zone < 0 ? "TodoDragBefore" : "TodoDragAfter") + " “" + target.Title + "”" + (zone != 0 && target.ParentId != dragSource.ParentId ? " · " + T("HierarchyMove") : ""); return;
        }
    }
    internal void CancelTodoDrag()
    {
        bool captured = dragging; dragging = false; dragSource = dragTarget = null;
        if (captured && TodoScroll.IsMouseCaptured) TodoScroll.ReleaseMouseCapture();
        foreach (var row in Items.Children.OfType<Border>()) ResetDrop(row);
        DragHint.Text = T("TodoDragHint");
    }
}
