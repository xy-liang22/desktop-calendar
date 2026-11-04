using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

namespace DesktopCalendar;

// Explicit test entry, isolated from normal preferences and the production instance mutex.
internal static class LifecycleChecks
{
    internal const string RootVariable = "DESKTOP_CALENDAR_CHECK_ROOT";
    internal static string DirectoryPath => Environment.GetEnvironmentVariable(RootVariable) is string root && root.Length > 0
        ? Path.GetFullPath(root) : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "artifacts", "lifecycle-checks"));
    static bool observed;
    internal static string? Phase(string[] args)
    {
        var arg = args.FirstOrDefault(a => a.StartsWith("--lifecycle-check=", StringComparison.Ordinal));
        if (arg == null) return null;
        string phase = arg[18..];
        if (phase is not ("hold" or "duplicate" or "recover" or "installed")) throw new ArgumentException("Invalid lifecycle check phase.");
        return phase;
    }
    internal static void Duplicate() => Write("duplicate", new { Duplicate = true, Pid = Environment.ProcessId });
    static void Write(string name, object value)
    {
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(Path.Combine(DirectoryPath, name + ".json"), JsonSerializer.Serialize(value));
    }
    internal static async Task Observe(MainWindow window, string phase)
    {
        if (observed) return; observed = true;
        window.Hide(); window.Show(); // Counter Start-Process Hidden, as in the existing self-test.
        if (phase == "hold") { window.SetPage(true); window.EnterCompact(); window.SetPinned(true); }
        await Task.Delay(200); window.Session.Save();
        var bounds = NativeWindow.Bounds(window.Handle);
        Write(phase, new { Pid = Environment.ProcessId, window.Ready, window.IsVisible,
            Collapsed = window.Mode == DisplayMode.Compact, Week = window.IsWeek, Pinned = window.IsPinned,
            DesktopOwner = window.DesktopMode.Layer == WindowLayer.Desktop && window.DesktopMode.Owner != IntPtr.Zero,
            bounds.X, bounds.Y, bounds.Width, bounds.Height, Executable = Environment.ProcessPath,
            Brand = window.Icon != null && window.BrandLogo.Source != null });
        if (phase == "hold") await Task.Delay(30000); // Watchdog if the parent check fails.
        window.Session.Exit(0);
    }
    internal static async Task Run(MainWindow window, Action<string, bool> check, string output)
    {
        check("window and header carry official brand", window.Icon != null && window.BrandLogo.Source != null);
        using (var icon = AppBrand.TrayIcon()) check("official tray icon is independently loaded at 32px", icon.Width == 32 && icon.Height == 32);
        using (var stream = Application.GetResourceStream(new Uri(AppBrand.IconResource, UriKind.Relative))!.Stream)
        using (var reader = new BinaryReader(stream))
        {
            check("embedded ICO contains nine sizes", reader.ReadUInt16() == 0 && reader.ReadUInt16() == 1 && reader.ReadUInt16() == 9);
            bool framesValid = true; int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            for (int i = 0; i < sizes.Length; i++)
            {
                stream.Position = 6 + 16 * i;
                int width = reader.ReadByte(), height = reader.ReadByte(); reader.ReadUInt16();
                int planes = reader.ReadUInt16(), bits = reader.ReadUInt16(), length = reader.ReadInt32(), offset = reader.ReadInt32();
                stream.Position = offset;
                using var frame = new MemoryStream(reader.ReadBytes(length));
                var bitmap = new PngBitmapDecoder(frame, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                framesValid &= (width == 0 ? 256 : width) == sizes[i] && (height == 0 ? 256 : height) == sizes[i] && planes == 1 && bits == 32 && bitmap.PixelWidth == sizes[i] && bitmap.PixelHeight == sizes[i];
            }
            check("all ICO frames decode at their declared sizes", framesValid);
        }
        using (var exeIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)!)
        using (var bitmap = exeIcon.ToBitmap())
        {
            var pixel = bitmap.GetPixel(bitmap.Width / 8, bitmap.Height / 2);
            check("EXE Win32 icon uses graphite brand palette", pixel.R == 27 && pixel.G == 40 && pixel.B == 53);
        }
        string root = Path.Combine(output, "lifecycle-checks"); Directory.CreateDirectory(root);
        var store = new WindowPreferencesStore(Path.Combine(root, "window-preferences.json"));
        store.Save(new WindowPreferences { FullPlacement = WindowPlacement.Capture(window.Handle) });
        foreach (string name in new[] { "hold", "duplicate", "recover" })
            if (File.Exists(Path.Combine(root, name + ".json"))) File.Delete(Path.Combine(root, name + ".json"));
        Process Start(string phase)
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!, "--lifecycle-check=" + phase) { UseShellExecute = false, CreateNoWindow = true };
            start.Environment[RootVariable] = Path.GetFullPath(root);
            return Process.Start(start)!;
        }
        using var first = Start("hold");
        try
        {
            for (int i = 0; i < 100 && !File.Exists(Path.Combine(root, "hold.json")); i++) await Task.Delay(100);
            check("lifecycle child reached real ready window", File.Exists(Path.Combine(root, "hold.json")) && !first.HasExited);
            using var duplicate = Start("duplicate");
            await duplicate.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            check("second instance refuses duplicate without changing first", duplicate.ExitCode == 23 && File.Exists(Path.Combine(root, "duplicate.json")) && !first.HasExited);
            using var retainedMutex = Mutex.OpenExisting("Local\\DesktopCalendar-LifecycleCheck");
            first.Kill(); // Keep an unowned handle alive to exercise the abandoned-mutex path.
            await first.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            check("owned test process terminated abruptly", first.HasExited);
            using var recovered = Start("recover");
            await recovered.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "recover.json")));
            var r = report.RootElement;
            check("abrupt-exit restart reclaims abandoned mutex and restores folded/week/topmost state", recovered.ExitCode == 0 && r.GetProperty("Ready").GetBoolean() && r.GetProperty("Collapsed").GetBoolean() && r.GetProperty("Week").GetBoolean() && r.GetProperty("Pinned").GetBoolean() && store.Load().Valid());
            using var previous = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "hold.json")));
            check("abrupt-exit restart restores actual saved rectangle", new[] { "X", "Y", "Width", "Height" }.All(p => Math.Abs(previous.RootElement.GetProperty(p).GetDouble() - r.GetProperty(p).GetDouble()) <= 3));
        }
        finally { if (!first.HasExited) { first.Kill(); await first.WaitForExitAsync(); } }
    }
}
