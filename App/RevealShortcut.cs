using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace DesktopCalendar;
// RegisterHotKey is process-scoped, without a keyboard hook or Windows configuration changes.
internal sealed class RevealShortcut : IDisposable
{
    readonly HwndSource source = new(new HwndSourceParameters("DesktopCalendar.RevealShortcut") { ParentWindow = new IntPtr(-3), WindowStyle = 0 });
    int registered;
    internal string Value { get; private set; } = "";
    internal event Action? Invoked;
    internal IntPtr Handle => source.Handle;
    internal int Registration => registered;
    internal RevealShortcut() => source.AddHook(Message);
    internal static bool Parse(string? value, out uint modifiers, out uint key)
    {
        modifiers = key = 0; if (value == null) return false; if (value == "") return true;
        string[] parts = value.Split('+'); if (parts.Length < 2 || parts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != parts.Length) return false;
        foreach (string part in parts[..^1]) { uint m = part switch { "Ctrl" => 2, "Alt" => 1, "Shift" => 4, _ => 0 }; if (m == 0) return false; modifiers |= m; }
        if ((modifiers & 3) == 0 || !Enum.TryParse<Key>(parts[^1], out var parsed)) return false;
        if (parsed.ToString() != parts[^1] || !(parsed >= Key.A && parsed <= Key.Z || parsed >= Key.D0 && parsed <= Key.D9 || parsed >= Key.F1 && parsed <= Key.F24 && parsed != Key.F12)) return false;
        key = (uint)KeyInterop.VirtualKeyFromKey(parsed); return true;
    }
    internal static bool Valid(string? value) => Parse(value, out _, out _);
    internal static string Capture(Key key, ModifierKeys modifiers) => string.Join("+", new[] { (modifiers & ModifierKeys.Control) != 0 ? "Ctrl" : null, (modifiers & ModifierKeys.Alt) != 0 ? "Alt" : null, (modifiers & ModifierKeys.Shift) != 0 ? "Shift" : null, key.ToString() }.Where(s => s != null));
    internal bool Apply(string value)
    {
        if (!Parse(value, out uint modifiers, out uint key)) return false;
        if (value == Value) return true;
        if (value.Length > 0 && Value.Length > 0 && Parse(Value, out uint oldModifiers, out uint oldKey) && oldModifiers == modifiers && oldKey == key) { Value = value; return true; }
        if (value.Length == 0) { if (registered != 0) UnregisterHotKey(source.Handle, registered); registered = 0; Value = ""; return true; }
        int next = registered == 5001 ? 5002 : 5001;
        if (!RegisterHotKey(source.Handle, next, modifiers | 0x4000, key)) return false;
        if (registered != 0) UnregisterHotKey(source.Handle, registered); registered = next; Value = value; return true;
    }
    IntPtr Message(IntPtr hwnd, int message, IntPtr w, IntPtr l, ref bool handled)
    { if (message == 0x0312 && registered != 0 && w.ToInt32() == registered) { handled = true; Invoked?.Invoke(); } return IntPtr.Zero; }
    public void Dispose() { if (registered != 0) UnregisterHotKey(source.Handle, registered); registered = 0; source.RemoveHook(Message); source.Dispose(); }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool RegisterHotKey(IntPtr h, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool UnregisterHotKey(IntPtr h, int id);
}
