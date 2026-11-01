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
        public string LayerError { get; set; } = "";
        public MainWindow.VisiblePlacement[] StartupVisible { get; set; } = Array.Empty<MainWindow.VisiblePlacement>();
        public bool VisibleBeforePrepared { get; set; }
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
            DesktopOwner = window.DesktopMode.Layer == WindowLayer.Desktop && window.DesktopMode.Owner != IntPtr.Zero,
            StartupVisible = window.StartupVisible.ToArray(), VisibleBeforePrepared = window.VisibleBeforePrepared, LayerError = window.DesktopMode.Error };
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
        check("completed group uses base order rather than completion time", Order(manual) == "bac" && siblings[0].OriginalOrder == 10);
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
        foreach (bool reverse in new[] { false, true })
        {
            var four = new[] { new DemoTodo("A", "A", 30, plannedTime:TimeSpan.FromHours(14)), new DemoTodo("B", "B", 10, plannedTime:TimeSpan.FromHours(9)),
                new DemoTodo("C", "C", 40), new DemoTodo("D", "D", 20, plannedTime:TimeSpan.FromHours(11)) };
            var mixed = DemoTodo.View(four); var timeView = DemoTodo.View(four, true);
            four[reverse ? 1 : 3].Completed = true; four[reverse ? 3 : 1].Completed = true;
            await Task.Delay(80);
            check("completion order independent manual/time groups reverse=" + reverse, Order(mixed) == "ACBD" && Order(timeView) == "ACBD");
            four[1].Completed = false; await Task.Delay(80);
            check("uncheck restores manual/time keys reverse=" + reverse, Order(mixed) == "BACD" && Order(timeView) == "BACD");
            foreach (var todo in four) todo.Completed = true;
            await Task.Delay(80);
            check("all completed retain manual and time orders reverse=" + reverse, Order(mixed) == "BDAC" && Order(timeView) == "BDAC");
            var isolated = new DemoTodo("parent", "Parent", 0) { Children = four };
            check("child sibling view has identical stable grouping", Order(isolated.ChildrenView) == "BDAC" && !isolated.Completed);
        }
        var abc = new[] { new DemoTodo("A", "A", 0), new DemoTodo("B", "B", 1), new DemoTodo("C", "C", 2), new DemoTodo("D", "D", 3) };
        var abcView = DemoTodo.View(abc);
        abc[3].Completed = true; abc[1].Completed = true; await Task.Delay(80);
        check("user example A C / B D independent of D then B clicks", Order(abcView) == "ACBD");
        abc[1].Completed = false; await Task.Delay(80);
        check("user example uncheck B gives A B C / D", Order(abcView) == "ABCD");
        var different = new[] { new DemoTodo("late", "late", 0, plannedTime:TimeSpan.FromHours(16)),
            new DemoTodo("none", "none", 1), new DemoTodo("early", "early", 2, plannedTime:TimeSpan.FromHours(8)) };
        var differentManual = DemoTodo.View(different); var differentTime = DemoTodo.View(different, true);
        foreach (var task in different.Reverse()) task.Completed = true;
        await Task.Delay(80);
        check("completed manual and scheduled groups use their own base sort", Order(differentManual) == "latenoneearly" && Order(differentTime) == "earlylatenone");

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
        // Exercise the same anchored physical-coordinate controller used by all eight grips.
        var baseline = NativeWindow.Bounds(window.Handle);
        foreach (string edge in new[] { "N", "S", "W", "E", "NW", "NE", "SW", "SE" })
        {
            var initial = new Rect(work.X + 200, work.Y + 150, 1700, 1200);
            NativeWindow.Place(window.Handle, initial); window.UpdateLayout();
            initial = NativeWindow.Bounds(window.Handle);
            window.BeginPointerGesture(edge, new Point(700, 600));
            bool stable = true;
            for (int i = 1; i <= 30; i++)
            {
                var delta = new Vector(edge.Contains('W') ? i * 2 : -i * 2, edge.Contains('N') ? i : -i);
                window.UpdatePointerGesture(new Point(700 + delta.X, 600 + delta.Y));
                stable &= Near(NativeWindow.Bounds(window.Handle), WindowGeometry.Resize(initial, delta, edge, window.MinWidth * window.Scale, window.MinHeight * window.Scale));
                // Identical absolute input must be idempotent, never compound the last delta.
                window.UpdatePointerGesture(new Point(700 + delta.X, 600 + delta.Y));
                stable &= Near(NativeWindow.Bounds(window.Handle), WindowGeometry.Resize(initial, delta, edge, window.MinWidth * window.Scale, window.MinHeight * window.Scale));
            }
            window.FinishPointerGesture();
            check("30-step shrink has fixed opposite edge/no cumulative feedback " + edge, stable);
        }
        NativeWindow.Place(window.Handle, baseline);
        window.EdgeToggle.IsChecked = true;
        foreach (var side in new[] { DockSide.Left, DockSide.Right })
        {
            foreach (bool pinned in new[] { false, true })
            {
                window.SetPinned(pinned);
                var area = NativeWindow.WorkArea(window.Handle); var r = NativeWindow.Bounds(window.Handle);
                r.X = side == DockSide.Left ? area.Left + 4 : area.Right - r.Width - 4;
                r.Y = area.Top + 50;
                window.BeginPointerGesture(null, new Point(100, 100));
                NativeWindow.Place(window.Handle, r); window.FinishPointerGesture();
                var aligned = NativeWindow.Bounds(window.Handle);
                check("client move docks on work area without native Snap " + side + "/pin=" + pinned,
                    Math.Abs(side == DockSide.Left ? aligned.Left - area.Left : aligned.Right - area.Right) < 1);
                var now = DateTime.UtcNow.AddSeconds(4); var outside = new Point(area.Left - 100, area.Top - 100);
                window.EvaluateEdgeAt(now, outside); window.EvaluateEdgeAt(now.AddSeconds(1), outside);
                check("docked leave collapses and preserves pin " + side + "/pin=" + pinned, window.Mode == DisplayMode.EdgeHandle && window.IsPinned == pinned);
                var strip = NativeWindow.Bounds(window.Handle);
                check("strip stays entirely in current work area", area.Contains(strip));
                window.EvaluateEdgeAt(now.AddSeconds(2), new Point(strip.X + strip.Width / 2, strip.Y + strip.Height / 2));
                check("pointer strip hover restores without changing desktop/topmost", window.Mode == DisplayMode.Normal && window.IsPinned == pinned);
                window.EnterCompact(); window.EvaluateEdgeAt(now.AddSeconds(3), outside); window.EvaluateEdgeAt(now.AddSeconds(5), outside);
                check("compact view is independent from edge hide", window.Mode == DisplayMode.Compact);
                window.RestoreNormal();
                window.EdgeToggle.IsChecked = false;
                window.EvaluateEdgeAt(now.AddSeconds(6), outside); window.EvaluateEdgeAt(now.AddSeconds(8), outside);
                check("disabled edge never collapses", window.Mode == DisplayMode.Normal);
                window.EdgeToggle.IsChecked = true;
            }
        }
        window.EdgeToggle.IsChecked = false; window.SetPinned(false); NativeWindow.Place(window.Handle, baseline);

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
                using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--restart-check") { UseShellExecute = false, CreateNoWindow = true })!;
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
                var actual = File.Exists(report) ? JsonSerializer.Deserialize<Observation>(File.ReadAllText(report)) : null;
                var source = collapsed ? folded : placement;
                var expected = source.Resolve(new Rect(area.X, area.Y, area.Width, area.Height), actual?.Dpi ?? 96, true, collapsed);
                check("real process restore HWND/display/state " + screen.DeviceName + "/fold=" + collapsed + "/pin=" + pinned,
                    process.ExitCode == 0 && actual is { Ready:true, Visible:true } && actual.Display == screen.DeviceName && actual.Collapsed == collapsed && actual.Pinned == pinned && (pinned || actual.DesktopOwner) && Near(actual.Bounds, expected));
                check("first visible and every startup native placement is final " + screen.DeviceName + "/fold=" + collapsed + "/pin=" + pinned,
                    actual != null && !actual.VisibleBeforePrepared && actual.StartupVisible.Length > 0 && actual.StartupVisible.All(s => Near(s.Bounds, expected) && s.Mode == (collapsed ? DisplayMode.Compact : DisplayMode.Normal)));
                if (actual != null) File.WriteAllText(Path.Combine(RestartDirectory, "startup-" + screen.DeviceName.Replace("\\", "").Replace(".", "") + "-" + collapsed + "-" + pinned + ".json"), JsonSerializer.Serialize(actual));
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
        var resolution = new WindowPlacement { X = -2200, Y = 400, Width = 1000, Height = 700, Dpi = 96, WorkX = -3000, WorkY = 0, WorkWidth = 3000, WorkHeight = 1500, Display = "test" };
        var smaller = new Rect(-2000, -100, 2000, 1100);
        check("resolution change preserves available-space fraction on negative monitor", Near(resolution.Resolve(smaller, 96, true, false), new Rect(-1600, 100, 1000, 700)));
        check("unchanged work area keeps exact saved physical rectangle", Near(resolution.Resolve(new Rect(-3000, 0, 3000, 1500), 96, true, false), resolution.Bounds));
        var fallbackWork = new Rect(0, 0, 1920, 1040);
        var fallbackBounds = missing.Resolve(fallbackWork, 144, false, false);
        var recapturedFallback = new WindowPlacement { X = fallbackBounds.X, Y = fallbackBounds.Y, Width = fallbackBounds.Width, Height = fallbackBounds.Height,
            Display = "available", Dpi = 144, WorkWidth = fallbackWork.Width, WorkHeight = fallbackWork.Height };
        check("saving fallback makes later reconnect retain the current screen rectangle", Near(recapturedFallback.Resolve(fallbackWork, 144, true, false), fallbackBounds));
        foreach (double dpi in new[] { 96d, 120, 144, 192 })
        foreach (var area in new[] { new Rect(-3840, -800, 3840, 2100), new Rect(0, 0, 1366, 720), new Rect(2200, -1200, 800, 600) })
            check("simulated topology always recoverable dpi=" + dpi + "/area=" + area, area.Contains(resolution.Resolve(area, dpi, false, false)) && area.Contains(resolution.Resolve(area, dpi, true, true)));
        File.WriteAllText(path, "{\"SchemaVersion\":1,\"HasBounds\":true,\"X\":-1200,\"Y\":100,\"Width\":1000,\"Height\":700,\"Collapsed\":true,\"Pinned\":true}");
        var migrated = new WindowPreferencesStore(path).Load();
        check("schema 1 migrates preserving full bounds and mode", migrated is { SchemaVersion:2, Collapsed:true, Pinned:true, FullPlacement.X:-1200 } && migrated.CompactPlacement == null);
    }
}
