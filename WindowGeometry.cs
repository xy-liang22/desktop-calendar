using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace DesktopCalendar;
internal enum DisplayMode { Normal, Compact, EdgeHandle, Tray }
internal enum DockSide { None, Left, Right }

// All rectangles here are physical pixels; WPF dimensions are converted at the boundary.
internal static class WindowGeometry
{
    internal static Rect Clamp(Rect bounds, Rect work)
    {
        double w = Math.Min(bounds.Width, work.Width), h = Math.Min(bounds.Height, work.Height);
        return new Rect(Math.Clamp(bounds.X, work.Left, work.Right - w), Math.Clamp(bounds.Y, work.Top, work.Bottom - h), w, h);
    }
    internal static Rect Compact(Rect work, double scale) => new(work.Right - 110 * scale - 12 * scale, work.Top + 12 * scale, 110 * scale, 44 * scale);
    internal static DockSide Edge(Rect window, Rect work, double scale)
    {
        if (Math.Abs(window.Left - work.Left) <= 18 * scale) return DockSide.Left;
        if (Math.Abs(window.Right - work.Right) <= 18 * scale) return DockSide.Right;
        return DockSide.None;
    }
    internal static Rect Handle(Rect window, Rect work, DockSide side, double scale)
    {
        double w = 28 * scale, h = Math.Min(110 * scale, work.Height);
        return new Rect(side == DockSide.Left ? work.Left : work.Right - w, Math.Clamp(window.Top + 64 * scale, work.Top, work.Bottom - h), w, h);
    }
    internal static Rect Resize(Rect start, Vector delta, string edge, double minWidth, double minHeight)
    {
        double l = start.Left, t = start.Top, r = start.Right, b = start.Bottom;
        if (edge.Contains('W')) l = Math.Min(l + delta.X, r - minWidth);
        if (edge.Contains('E')) r = Math.Max(r + delta.X, l + minWidth);
        if (edge.Contains('N')) t = Math.Min(t + delta.Y, b - minHeight);
        if (edge.Contains('S')) b = Math.Max(b + delta.Y, t + minHeight);
        return new Rect(l, t, r - l, b - t);
    }
}

internal static class NativeWindow
{
    [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] internal static extern int GetWindowLong(IntPtr hwnd, int index);
    internal static Rect Bounds(IntPtr hwnd)
    {
        GetWindowRect(hwnd, out var r);
        return new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
    }
    internal static Rect WorkArea(IntPtr hwnd)
    {
        var r = System.Windows.Forms.Screen.FromHandle(hwnd).WorkingArea;
        return new Rect(r.X, r.Y, r.Width, r.Height);
    }
    internal static void Place(IntPtr hwnd, Rect r)
    {
        if (!SetWindowPos(hwnd, IntPtr.Zero, (int)Math.Round(r.X), (int)Math.Round(r.Y), (int)Math.Round(r.Width), (int)Math.Round(r.Height), 0x0014))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
    internal static void Pin(IntPtr hwnd, bool enabled)
    {
        if (!SetWindowPos(hwnd, new IntPtr(enabled ? -1 : -2), 0, 0, 0, 0, 0x0013))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
}
