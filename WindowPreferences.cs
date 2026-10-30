using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace DesktopCalendar;

// Window preferences only. No Todo/Activity data, text, screenshots or account information.
internal sealed record WindowPreferences
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "desktop-calendar-window";
    public bool HasBounds { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 1240;
    public double Height { get; set; } = 760;
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
        BackgroundOpacity is >= .65 and <= .98 && ValidColumns(MonthColumns, 3) && ValidColumns(WeekColumns, 2);
    static bool ValidColumns(double[]? a, int count) => a != null && (a.Length == 0 || a.Length == count && a.All(x => double.IsFinite(x) && x > 0 && x < 20000));
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
        if (p.SchemaVersion != 1) { CanSave = false; throw new NotSupportedException(); }
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
            if (p.SchemaVersion != 1 || !p.Valid()) throw new InvalidDataException();
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
