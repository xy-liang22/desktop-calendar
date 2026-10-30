using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace DesktopCalendar;

internal enum WindowLayer { Desktop, Topmost, Fallback }

// Owns only our top-level HWND. No SetParent, child styles, hooks or layer repair timer.
internal sealed class DesktopWindowMode
{
    readonly Window window;
    readonly IntPtr hwnd;
    IntPtr desktopOwner;
    uint desktopPid;
    internal WindowLayer Layer { get; private set; } = WindowLayer.Fallback;
    internal string Error { get; private set; } = "";
    internal static readonly uint TaskbarCreated = RegisterWindowMessage("TaskbarCreated");
    internal IntPtr Owner => GetWindow(hwnd, 4);
    internal bool NativeTopmost => (NativeWindow.GetWindowLong(hwnd, -20) & 8) != 0;
    internal int LayerChanges { get; private set; }
    internal DesktopWindowMode(Window window, IntPtr hwnd) { this.window = window; this.hwnd = hwnd; }

    internal bool Apply(bool topmost)
    {
        try
        {
            IntPtr owner = IntPtr.Zero;
            if (!topmost)
            {
                owner = GetShellWindow();
                if (!IsWindow(owner) || Class(owner) != "Progman" || Pid(owner) == 0)
                    throw new InvalidOperationException("DesktopUnavailable");
            }
            SetOwner(IntPtr.Zero); // Detach before changing TOPMOST to avoid owner propagation.
            ConfigureToolWindow();
            SetTopmost(topmost);
            SetOwner(owner);
            desktopOwner = owner; desktopPid = Pid(owner);
            Layer = topmost ? WindowLayer.Topmost : WindowLayer.Desktop;
            Error = "";
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Fallback(ex.Message);
            return false;
        }
    }
    internal void Fallback(string reason = "DesktopUnavailable")
    {
        Error = reason; Layer = WindowLayer.Fallback;
        if (!IsWindow(hwnd)) return;
        SetOwner(IntPtr.Zero); ConfigureToolWindow(); SetTopmost(false);
        desktopOwner = IntPtr.Zero; desktopPid = 0;
    }
    internal bool CheckOwner(bool simulateLoss = false)
    {
        if (Layer != WindowLayer.Desktop) return true;
        if (!simulateLoss && IsWindow(desktopOwner) && Pid(desktopOwner) == desktopPid &&
            GetShellWindow() == desktopOwner && Owner == desktopOwner) return true;
        Fallback(); // One transition on loss, never automatic reattachment or foreground activation.
        return false;
    }
    void ConfigureToolWindow()
    {
        window.ShowInTaskbar = false;
        SetLongChecked(-20, (NativeWindow.GetWindowLong(hwnd, -20) | 0x80L) & ~0x40000L);
    }
    void SetTopmost(bool enabled)
    {
        window.Topmost = enabled;
        // WPF and USER32 must agree, including a native flag left over from an earlier mode.
        if (NativeTopmost != enabled)
        {
            if (!NativeWindow.SetWindowPos(hwnd, new IntPtr(enabled ? -1 : -2), 0, 0, 0, 0, 0x213))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        LayerChanges++;
        if (NativeTopmost != enabled) throw new InvalidOperationException("TopmostMismatch");
    }
    void SetOwner(IntPtr owner)
    {
        if (Pid(hwnd) != Environment.ProcessId || (NativeWindow.GetWindowLong(hwnd, -16) & 0x40000000) != 0)
            throw new InvalidOperationException("ExpectedOwnTopLevelWindow");
        if (Owner != owner) SetLongChecked(-8, owner.ToInt64());
        if (Owner != owner) throw new InvalidOperationException("OwnerMismatch");
    }
    void SetLongChecked(int index, long value)
    {
        Marshal.SetLastPInvokeError(0);
        if (SetWindowLongPtr(hwnd, index, new IntPtr(value)) == IntPtr.Zero && Marshal.GetLastPInvokeError() != 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    static uint Pid(IntPtr h) { GetWindowThreadProcessId(h, out uint pid); return pid; }
    static string Class(IntPtr h) { var s = new StringBuilder(128); GetClassName(h, s, s.Capacity); return s.ToString(); }
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint command);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] static extern IntPtr SetWindowLongPtr(IntPtr h, int index, IntPtr value);
}
