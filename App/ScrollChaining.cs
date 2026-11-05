using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopCalendar;

// Inner text/document viewers handle MouseWheel even at a boundary. Route each wheel
// to the nearest scrollable ancestor that can move, without replaying input or saving drafts.
internal static class ScrollChaining
{
    internal static IEnumerable<ScrollViewer> Ancestors(DependencyObject? source)
    {
        for (var node = source; node != null; node = node is FrameworkContentElement text ? text.Parent : node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
            if (node is ScrollViewer viewer) yield return viewer;
    }
    internal static bool CanMove(ScrollViewer viewer, int delta) => viewer.ScrollableHeight > .5 && (delta > 0 ? viewer.VerticalOffset > .5 : viewer.VerticalOffset < viewer.ScrollableHeight - .5);
    internal static bool Route(DependencyObject? source, ScrollViewer outer, int delta)
    {
        if (delta == 0) return false;
        foreach (var viewer in Ancestors(source))
        {
            if (CanMove(viewer, delta))
            {
                if (SystemParameters.WheelScrollLines == -1) { if (delta > 0) viewer.PageUp(); else viewer.PageDown(); }
                else if (viewer.CanContentScroll) { for (int i = 0; i < Math.Max(1, SystemParameters.WheelScrollLines * Math.Abs(delta) / 120); i++) { if (delta > 0) viewer.LineUp(); else viewer.LineDown(); } }
                else viewer.ScrollToVerticalOffset(viewer.VerticalOffset - delta / 120.0 * SystemParameters.WheelScrollLines * 16);
                return true;
            }
            if (viewer == outer) break;
        }
        return false;
    }
    internal static void Attach(ScrollViewer outer) => outer.PreviewMouseWheel += (_, e) =>
    {
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        e.Handled = Route(e.OriginalSource as DependencyObject, outer, e.Delta);
    };
}
