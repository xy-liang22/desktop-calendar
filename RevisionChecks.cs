using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Forms = System.Windows.Forms;

namespace DesktopCalendar;

// Separate real-process restart checks; all files stay in ignored artifacts, never user preferences.
internal static class RevisionChecks
{
    internal static string RestartDirectory => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "artifacts", "restart-check-preferences"));
    static bool observed;
    internal sealed record Observation
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public uint Dpi { get; set; }
        public string Display { get; set; } = "";
        public bool Collapsed { get; set; }
        public bool Pinned { get; set; }
        public bool Ready { get; set; }
        public bool Visible { get; set; }
        public bool DesktopOwner { get; set; }
        internal Rect Bounds => new(X, Y, Width, Height);
    }
    internal static async Task ObserveRestart(MainWindow window)
    {
        if (observed) return; observed = true;
        for (int i = 0; i < 100 && !window.Ready; i++) await Task.Delay(50);
        await Task.Delay(250);
        var r = NativeWindow.Bounds(window.Handle);
        var report = new Observation { X = r.X, Y = r.Y, Width = r.Width, Height = r.Height,
            Dpi = NativeWindow.GetDpiForWindow(window.Handle), Display = Forms.Screen.FromHandle(window.Handle).DeviceName,
            Collapsed = window.Mode == DisplayMode.Compact, Pinned = window.DesktopMode.NativeTopmost,
            Ready = window.Ready, Visible = window.IsVisible,
            DesktopOwner = window.DesktopMode.Layer == WindowLayer.Desktop && window.DesktopMode.Owner != IntPtr.Zero };
        File.WriteAllText(Path.Combine(RestartDirectory, "observed.json"), JsonSerializer.Serialize(report));
        window.Session.Exit(0);
    }
    internal static async Task Run(MainWindow window, Action<string, bool> check)
    {
        bool Near(Rect a, Rect b) => Math.Abs(a.X - b.X) < 4 && Math.Abs(a.Y - b.Y) < 4 && Math.Abs(a.Width - b.Width) < 4 && Math.Abs(a.Height - b.Height) < 4;
        // Live views reorder only presentation; completion never rewrites sibling keys.
        var siblings = new[] { new DemoTodo("a", "A", 10, false, new TimeSpan(14, 0, 0)),
            new DemoTodo("b", "B", 20), new DemoTodo("c", "C", 30, true, new TimeSpan(9, 30, 0)) };
        var manual = DemoTodo.View(siblings); var timed = DemoTodo.View(siblings, true);
        string Order(System.ComponentModel.ICollectionView v) => string.Concat(v.Cast<DemoTodo>().Select(x => x.Id));
        check("manual mixed tasks still incomplete first", Order(manual) == "abc");
        check("time sort cannot move completed before incomplete", Order(timed) == "abc");
        siblings[0].Completed = true; await Task.Delay(100);
        check("completion appends after existing completed without changing original keys", Order(manual) == "bca" && siblings[0].OriginalOrder == 10);
        siblings[0].Completed = false; await Task.Delay(100);
        check("unchecking restores original manual position", Order(manual) == "abc");
        siblings[2].Completed = false; await Task.Delay(100);
        check("time sort applies only within completion group", Order(timed) == "cab");
        siblings[2].Completed = true; await Task.Delay(100);
        check("manual order independent of time sorting", Order(manual) == "abc" && Order(timed) == "abc");
        var parent = DemoContent.Todos[0]; var children = parent.Children; var childView = parent.ChildrenView;
        children[0].Completed = true; children[1].Completed = false; await Task.Delay(100);
        check("child sibling live completion grouping", childView.Cast<DemoTodo>().First() == children[1] && parent.ChildProgress == 50);
        children[0].Completed = false; children[1].Completed = true; await Task.Delay(100);
        check("child uncheck restores original position", childView.Cast<DemoTodo>().First() == children[0]);
        check("scheduled time prefix and no fake placeholder", DemoContent.Todos[0].TimePrefix == "09:30 " && DemoContent.Todos[2].TimePrefix == "" && children[0].TimePrefix == "");

        var saved = window.SnapshotPreferences();
        window.EnterCompact();
        var work = NativeWindow.WorkArea(window.Handle);
        NativeWindow.Place(window.Handle, new Rect(work.X + 90, work.Y + 100, 390 * window.Scale, 310 * window.Scale));
        await Task.Delay(100);
        var compact = NativeWindow.Bounds(window.Handle);
        window.RestoreNormal(); window.EnterCompact(); await Task.Delay(100);
        check("fold round trip keeps independent compact position and size", Near(compact, NativeWindow.Bounds(window.Handle)));
        window.HideToTray(); window.Session.Save();
        check("hidden snapshot keeps compact state and rectangle", window.Session.Preferences.Collapsed && Near(window.Session.Preferences.CompactPlacement!.Bounds, compact));
        window.ShowComponent(); window.RestoreNormal();

        Directory.CreateDirectory(RestartDirectory);
        string path = Path.Combine(RestartDirectory, "window-preferences.json");
        var store = new WindowPreferencesStore(path);
        // Real HWND startup on each available display, including negative physical coordinates.
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var area = screen.WorkingArea;
            var placement = WindowPlacement.Capture(window.Handle) with { Display = screen.DeviceName,
                X = area.X + 70, Y = area.Y + 60, WorkX = area.X, WorkY = area.Y };
            foreach (bool collapsed in new[] { false, true })
            foreach (bool pinned in new[] { false, true })
            {
                var folded = placement with { Width = 400 * window.Scale, Height = 320 * window.Scale, X = area.X + 150, Y = area.Y + 160 };
                var p = saved with { Pinned = pinned, Collapsed = collapsed, FullPlacement = placement, CompactPlacement = folded };
                store.Save(p);
                string report = Path.Combine(RestartDirectory, "observed.json");
                if (File.Exists(report)) File.Delete(report);
                using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--restart-check") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden })!;
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
                var actual = File.Exists(report) ? JsonSerializer.Deserialize<Observation>(File.ReadAllText(report)) : null;
                var source = collapsed ? folded : placement;
                var expected = source.Resolve(new Rect(area.X, area.Y, area.Width, area.Height), actual?.Dpi ?? 96, true, collapsed);
                check("real process restore HWND/display/state " + screen.DeviceName + "/fold=" + collapsed + "/pin=" + pinned,
                    process.ExitCode == 0 && actual is { Ready:true, Visible:true } && actual.Display == screen.DeviceName && actual.Collapsed == collapsed && actual.Pinned == pinned && (pinned || actual.DesktopOwner) && Near(actual.Bounds, expected));
                var again = store.Load();
                check("shutdown save preserves full and compact rectangles " + screen.DeviceName + "/fold=" + collapsed + "/pin=" + pinned,
                    again.Collapsed == collapsed && again.Pinned == pinned && again.FullPlacement != null && again.CompactPlacement != null);
            }
        }
        var changedDpi = new WindowPlacement { X = -2000, Y = -100, Width = 1000, Height = 700, Dpi = 96, WorkX = -2200, WorkY = -200, Display = "test" };
        var changedWork = new Rect(-3300, -300, 3300, 1800);
        check("DPI scales size and monitor-relative offset", Near(changedDpi.Resolve(changedWork, 144, true, false), new Rect(-3000, -150, 1500, 1050)));
        var missing = changedDpi with { Display = "disconnected-test-display" };
        check("unavailable display chooses a connected fallback", Forms.Screen.AllScreens.Contains(missing.Target()));
        check("fallback fits tiny work area without invalid sizes", new Rect(0, 0, 600, 400).Contains(missing.Resolve(new Rect(0, 0, 600, 400), 192, false, false)));
        File.WriteAllText(path, "{\"SchemaVersion\":1,\"HasBounds\":true,\"X\":-1200,\"Y\":100,\"Width\":1000,\"Height\":700,\"Collapsed\":true,\"Pinned\":true}");
        var migrated = new WindowPreferencesStore(path).Load();
        check("schema 1 migrates preserving full bounds and mode", migrated is { SchemaVersion:2, Collapsed:true, Pinned:true, FullPlacement.X:-1200 } && migrated.CompactPlacement == null);
    }
}
