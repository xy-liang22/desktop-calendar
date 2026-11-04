using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace DesktopCalendar;
internal static class WindowGestures
{
    internal static void Header(Window window, FrameworkElement header, Func<IDisposable>? hold = null)
    {
        header.Cursor = Cursors.SizeAll;
        header.PreviewMouseLeftButtonDown += (_, e) =>
        {
            for (var node = e.OriginalSource as DependencyObject; node != null && node != header; node = node is Visual ? VisualTreeHelper.GetParent(node) : null)
                if (node is ButtonBase or TextBoxBase or System.Windows.Controls.ComboBox) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;
            e.Handled = true; using var guard = hold?.Invoke();
            try { window.DragMove(); } catch (InvalidOperationException) { }
            Fit(window);
        };
    }
    internal static void Fit(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle; if (hwnd == IntPtr.Zero) return;
        var bounds = NativeWindow.Bounds(hwnd); var fit = WindowGeometry.Clamp(bounds, NativeWindow.WorkArea(hwnd));
        NativeWindow.Place(hwnd, new Rect(fit.X, fit.Y, bounds.Width, bounds.Height));
    }
}
