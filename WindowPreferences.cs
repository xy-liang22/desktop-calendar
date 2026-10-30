using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Forms = System.Windows.Forms;

namespace DesktopCalendar;

// Window preferences only. No Todo/Activity data, text, screenshots or account information.
internal sealed record WindowPreferences
{
    public int SchemaVersion { get; set; } = 2;
    public string Id { get; set; } = "desktop-calendar-window";
    public bool HasBounds { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 1240;
    public double Height { get; set; } = 760;
    public WindowPlacement? FullPlacement { get; set; }
    public WindowPlacement? CompactPlacement { get; set; }
    public bool Collapsed { get; set; }
    public bool Week { get; set; }
    public bool Pinned { get; set; }
    public bool EdgeAutoHide { get; set; }
    public double BackgroundOpacity { get; set; } = .96;
    public double[] MonthColumns { get; set; } = Array.Empty<double>();
    public double[] WeekColumns { get; set; } = Array.Empty<double>();
    internal Rect Bounds => new(X, Y, Width, Height);
    internal void SetBounds(Rect r) { HasBounds = true; X = r.X; Y = r.Y; Width = r.Width; Height = r.Height; }
    internal bool Valid() => Id == "desktop-calendar-window" &&
        new[] { X, Y, Width, Height, BackgroundOpacity }.All(double.IsFinite) &&
        Math.Abs(X) < 100000 && Math.Abs(Y) < 100000 && Width is >= 200 and <= 20000 && Height is >= 200 and <= 20000 &&
        BackgroundOpacity is >= .65 and <= .98 && ValidColumns(MonthColumns, 3) && ValidColumns(WeekColumns, 2) &&
        (FullPlacement == null || FullPlacement.Valid()) && (CompactPlacement == null || CompactPlacement.Valid());
    static bool ValidColumns(double[]? a, int count) => a != null && (a.Length == 0 || a.Length == count && a.All(x => double.IsFinite(x) && x > 0 && x < 20000));
}

// Physical pixels plus the original work area/DPI. No display configuration is changed.
internal sealed record WindowPlacement
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Display { get; set; } = "";
    public double Dpi { get; set; }
    public double WorkX { get; set; }
    public double WorkY { get; set; }
    internal Rect Bounds => new(X, Y, Width, Height);
    internal bool Valid() => new[] { X, Y, Width, Height, Dpi, WorkX, WorkY }.All(double.IsFinite) &&
        Math.Abs(X) < 100000 && Math.Abs(Y) < 100000 && Math.Abs(WorkX) < 100000 && Math.Abs(WorkY) < 100000 &&
        Width is >= 200 and <= 20000 && Height is >= 200 and <= 20000 && (Dpi == 0 || Dpi is >= 48 and <= 960) && Display != null && Display.Length <= 256;
    internal static WindowPlacement Legacy(Rect r) => new() { X = r.X, Y = r.Y, Width = r.Width, Height = r.Height };
    internal static WindowPlacement Capture(IntPtr hwnd)
    {
        var r = NativeWindow.Bounds(hwnd); var screen = Forms.Screen.FromHandle(hwnd);
        return new() { X = r.X, Y = r.Y, Width = r.Width, Height = r.Height, Display = screen.DeviceName,
            Dpi = NativeWindow.GetDpiForWindow(hwnd), WorkX = screen.WorkingArea.X, WorkY = screen.WorkingArea.Y };
    }
    internal Forms.Screen Target()
    {
        var screens = Forms.Screen.AllScreens;
        var saved = screens.FirstOrDefault(s => s.DeviceName == Display);
        if (saved != null) return saved;
        if (Display.Length > 0) return Forms.Screen.PrimaryScreen ?? screens[0];
        return Forms.Screen.FromPoint(new System.Drawing.Point((int)(X + Width / 2), (int)(Y + Height / 2)));
    }
    internal Rect Resolve(Rect work, double dpi, bool sameDisplay, bool compact)
    {
        double ratio = Dpi > 0 ? dpi / Dpi : 1;
        double x = Dpi > 0 && sameDisplay ? work.X + (X - WorkX) * ratio : X;
        double y = Dpi > 0 && sameDisplay ? work.Y + (Y - WorkY) * ratio : Y;
        if (!sameDisplay && Display.Length > 0) { x = work.X + 24 * dpi / 96; y = work.Y + 24 * dpi / 96; }
        double w = Math.Max(Width * ratio, (compact ? 320 : 880) * dpi / 96);
        double h = Math.Max(Height * ratio, (compact ? 240 : 520) * dpi / 96);
        return WindowGeometry.Clamp(new Rect(x, y, w, h), work);
    }
}

internal sealed class WindowPreferencesStore
{
    readonly string path;
    internal string Notice { get; private set; } = "";
    internal bool CanSave { get; private set; } = true;
    internal WindowPreferencesStore(string path) { this.path = path; }
    WindowPreferences Read(string file)
    {
        if (new FileInfo(file).Length > 65536) throw new InvalidDataException();
        var p = JsonSerializer.Deserialize<WindowPreferences>(File.ReadAllBytes(file)) ?? throw new InvalidDataException();
        if (p.SchemaVersion is not (1 or 2)) { CanSave = false; throw new NotSupportedException(); }
        if (p.SchemaVersion == 1)
        {
            if (!p.Valid()) throw new InvalidDataException();
            p.SchemaVersion = 2;
            if (p.HasBounds) p.FullPlacement = WindowPlacement.Legacy(p.Bounds);
        }
        if (!p.Valid()) throw new InvalidDataException();
        return p;
    }
    internal WindowPreferences Load()
    {
        try { if (File.Exists(path)) return Read(path); }
        catch (NotSupportedException) { Notice = "PreferencesNewer"; return new(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        { Notice = "PreferencesRecovered"; }
        try { if (File.Exists(path + ".bak")) { Notice = "PreferencesRecovered"; return Read(path + ".bak"); } }
        catch (NotSupportedException) { Notice = "PreferencesNewer"; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) { Notice = "PreferencesReset"; }
        return new();
    }
    internal bool Save(WindowPreferences p)
    {
        if (!CanSave) return false;
        string temporary = path + ".tmp";
        try
        {
            if (p.SchemaVersion != 2 || !p.Valid()) throw new InvalidDataException();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(p, new JsonSerializerOptions { WriteIndented = true });
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { stream.Write(bytes); stream.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        { Notice = "PreferencesSaveFailed"; return false; }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
