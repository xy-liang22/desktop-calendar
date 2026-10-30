using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;

namespace DesktopCalendar;
// Dependency-free executable checks; no test adapters or external package downloads.
internal static class PrototypeChecks
{
    private static bool started;
    internal static async Task Run(MainWindow window)
    {
        if (started) return;
        started = true;
        var results = new List<string>();
        int failed = 0;
        string output = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "artifacts");
        Directory.CreateDirectory(output);
        void Check(string name, bool pass)
        {
            results.Add((pass ? "PASS " : "FAIL ") + name);
            if (!pass) failed++;
        }
        bool Near(Rect a, Rect b) => Math.Abs(a.X - b.X) <= 3 && Math.Abs(a.Y - b.Y) <= 3 && Math.Abs(a.Width - b.Width) <= 3 && Math.Abs(a.Height - b.Height) <= 3;
        try
        {
            for (int i = 0; i < 100 && !window.Ready; i++) await Task.Delay(50);
            Check("startup restore completed before checks", window.Ready);
            // Start-Process -WindowStyle Hidden suppresses the very first native Show.
            // Re-show before checking styles that depend on a mapped desktop window.
            window.Hide(); window.Show();
            await Task.Delay(200);
            results.Add("P1 checks · " + DateTimeOffset.Now.ToString("O"));
            results.Add("Actual DPI: " + NativeWindow.GetDpiForWindow(window.Handle));
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                results.Add($"Monitor: {screen.Bounds}; work area: {screen.WorkingArea}");
            var start = new Rect(100, 100, 1000, 700);
            foreach (string edge in new[] { "N", "S", "E", "W", "NE", "NW", "SE", "SW" })
            {
                var r = WindowGeometry.Resize(start, new Vector(100, 80), edge, 880, 520);
                Check("resize " + edge, r.Width >= 880 && r.Height >= 520 && r != start);
                var limit = WindowGeometry.Resize(start, new Vector(edge.Contains('W') ? 5000 : -5000, edge.Contains('N') ? 5000 : -5000), edge, 880, 520);
                Check("minimum size " + edge, limit.Width >= 880 && limit.Height >= 520);
            }
            foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0 })
            {
                var work = new Rect(-2560, -400, 2560, 1400);
                var compact = WindowGeometry.Compact(work, scale);
                Check("compact geometry " + scale, work.Contains(compact) && Math.Abs(work.Right - compact.Right - 12 * scale) < 0.1);
                foreach (var side in new[] { DockSide.Left, DockSide.Right })
                    Check("edge geometry " + scale + " " + side, work.Contains(WindowGeometry.Handle(start, work, side, scale)));
                Check("removed monitor clamp " + scale, work.Contains(WindowGeometry.Clamp(new Rect(3000, 2000, 4000, 2000), work)));
            }
            Check("no native caption", (NativeWindow.GetWindowLong(window.Handle, -16) & 0x00C00000) == 0);
            Check("layered transparent HWND", (NativeWindow.GetWindowLong(window.Handle, -20) & 0x00080000) != 0);
            Check("transparent WPF surface", window.AllowsTransparency && window.WindowStyle == WindowStyle.None && window.BorderThickness == new Thickness(0));
            Check("eight resize grips", window.ResizeHandles.Children.Count == 8);
            Check("default desktop owner is top-level", window.DesktopMode.Layer == WindowLayer.Desktop && window.DesktopMode.Owner != IntPtr.Zero && (NativeWindow.GetWindowLong(window.Handle, -16) & 0x40000000) == 0);
            Check("desktop tool window styles", !window.ShowInTaskbar && (NativeWindow.GetWindowLong(window.Handle, -20) & 0x80) != 0 && (NativeWindow.GetWindowLong(window.Handle, -20) & 0x40000) == 0);
            Check("desktop starts non-topmost and activatable", !window.DesktopMode.NativeTopmost && !window.Topmost && (NativeWindow.GetWindowLong(window.Handle, -20) & 0x08000000) == 0);
            await Task.Delay(250);
            var original = NativeWindow.Bounds(window.Handle);
            foreach (double width in new[] { 1240.0, 880.0 })
            {
                window.Width = width; window.Height = width == 880 ? 520 : 760;
                window.FitToScreen();
                foreach (bool week in new[] { false, true })
                {
                    window.SetPage(week); window.UpdateLayout(); await Task.Delay(100);
                    Check($"page layout {width}/{week}", window.PageHost.ActualWidth >= 820 && window.PageHost.ActualHeight >= 360);
                    Check($"no invalid layout {width}/{week}", Descendants(window.PageHost).OfType<FrameworkElement>().All(e => !double.IsNaN(e.ActualWidth) && !double.IsNaN(e.ActualHeight)));
                    var activePage = window.PageHost.Children.OfType<Grid>().Single(g => g.Visibility == Visibility.Visible);
                    Check($"column minimums {width}/{week}", activePage.ColumnDefinitions.All(c => c.ActualWidth + 1 >= c.MinWidth));
                    Check($"columns fit page {width}/{week}", activePage.ColumnDefinitions.Sum(c => c.ActualWidth) <= activePage.ActualWidth + 1);
                    Capture(window, Path.Combine(output, $"{(week ? "week" : "month")}-{width}.png"), 1);
                }
            }
            NativeWindow.Place(window.Handle, original); window.SetPage(false); window.UpdateLayout();
            var monthGrid = window.PageHost.Children.OfType<Grid>().First();
            var splitter = Descendants(monthGrid).OfType<GridSplitter>().First();
            double firstColumn = monthGrid.ColumnDefinitions[0].ActualWidth;
            splitter.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(splitter)!, Environment.TickCount, Key.Right) { RoutedEvent = Keyboard.KeyDownEvent });
            window.UpdateLayout(); await Task.Delay(60);
            Check("built-in splitter changes column widths", Math.Abs(monthGrid.ColumnDefinitions[0].ActualWidth - firstColumn) > 1);
            var widths = monthGrid.ColumnDefinitions.Select(c => c.Width).ToArray();
            window.SetPinned(true); window.EnterCompact(); window.SetPinned(false); window.RestoreNormal(); window.UpdateLayout();
            Check("splitter widths survive mode and view changes", monthGrid.ColumnDefinitions.Select(c => c.Width).SequenceEqual(widths));
            // This validates raster scaling, NOT an actual Windows DPI setting change.
            foreach (double scale in new[] { 1.25, 1.5, 2.0 }) Capture(window, Path.Combine(output, $"render-{scale}.png"), scale);
            var corner = Capture(window, Path.Combine(output, "alpha-check.png"), 1);
            byte[] pixel = new byte[4]; corner.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 4, 0);
            Check("corner alpha is transparent", pixel[3] == 0);
            corner.CopyPixels(new Int32Rect(30, 15, 1, 1), pixel, 4, 0);
            Check("background is translucent", pixel[3] > 0 && pixel[3] < 255);
            var before = NativeWindow.Bounds(window.Handle);
            window.EnterCompact(); await Task.Delay(150);
            Check("compact hides full layout", window.Mode == DisplayMode.Compact && window.FullContent.Visibility == Visibility.Collapsed);
            Check("compact current work-area placement", Near(NativeWindow.Bounds(window.Handle), WindowGeometry.Compact(NativeWindow.WorkArea(window.Handle), window.Scale)));
            Check("compact contains today's date and fixture list", window.CompactDate.Text == DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) && window.CompactItems.Items.Count == DemoContent.Todos.Length && window.CompactContent.Visibility == Visibility.Visible);
            Check("compact inherits desktop and resizable grips", window.DesktopMode.Layer == WindowLayer.Desktop && !window.DesktopMode.NativeTopmost && window.ResizeHandles.Visibility == Visibility.Visible);
            window.UpdateLayout(); Capture(window, Path.Combine(output, "today-folded.png"), 1);
            bool done = DemoContent.Todos[0].Completed;
            window.UpdateLayout();
            var compactCheck = Descendants(window.CompactItems).OfType<CheckBox>().First();
            compactCheck.IsChecked = !done; await Task.Delay(60);
            Check("compact checkbox updates shared mock only", DemoContent.Todos[0].Completed == !done);
            DemoContent.Todos[0].Completed = done;
            window.RestoreNormal(); await Task.Delay(100);
            Check("compact restores bounds", Near(NativeWindow.Bounds(window.Handle), before));
            window.PinToggle.IsChecked = true;
            window.PinToggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            results.Add("Pin immediate style=" + NativeWindow.GetWindowLong(window.Handle, -20).ToString("X") + "; WPF=" + window.Topmost);
            await Task.Delay(150);
            results.Add("Pin settled style=" + NativeWindow.GetWindowLong(window.Handle, -20).ToString("X") + "; WPF=" + window.Topmost);
            Check("topmost native flag", (NativeWindow.GetWindowLong(window.Handle, -20) & 8) != 0); window.SetPinned(false);
            Check("topmost disabled flag", (NativeWindow.GetWindowLong(window.Handle, -20) & 8) == 0);
            var keptHandle = window.Handle;
            window.SetPage(true); window.OpacitySlider.Value = .85;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                window.SetPinned(true); window.EnterCompact(); window.SetPinned(false); window.RestoreNormal();
                Check("native TOPMOST cleared and desktop restored " + cycle, !window.DesktopMode.NativeTopmost && !window.Topmost && window.DesktopMode.Layer == WindowLayer.Desktop && window.DesktopMode.Owner != IntPtr.Zero);
                Check("mode switch preserves HWND/view/opacity/bounds " + cycle, window.Handle == keptHandle && window.IsWeek && window.OpacitySlider.Value == .85 && Near(NativeWindow.Bounds(window.Handle), before));
            }
            window.SetPage(false); window.OpacitySlider.Value = .96;
            int changes = window.DesktopMode.LayerChanges;
            await Task.Delay(700);
            Check("idle never repairs Z-order", window.DesktopMode.LayerChanges == changes);
            Check("simulated owner loss enters explicit fallback", !window.DesktopMode.CheckOwner(true) && window.DesktopMode.Layer == WindowLayer.Fallback && !window.DesktopMode.NativeTopmost && window.DesktopMode.Owner == IntPtr.Zero);
            window.Session.Locate();
            Check("explicit locate retries desktop without changing view", window.DesktopMode.Layer == WindowLayer.Desktop && Near(NativeWindow.Bounds(window.Handle), before));
            int gripIndex = 0;
            foreach (string edge in new[] { "N", "S", "W", "E", "NW", "NE", "SW", "SE" })
            {
                var grip = (Thumb)window.ResizeHandles.Children[gripIndex++];
                grip.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
                grip.RaiseEvent(new DragDeltaEventArgs(12, 8) { RoutedEvent = Thumb.DragDeltaEvent });
                grip.RaiseEvent(new DragCompletedEventArgs(12, 8, false) { RoutedEvent = Thumb.DragCompletedEvent });
                await Task.Delay(30);
                var expected = WindowGeometry.Resize(before, new Vector(12 * window.Scale, 8 * window.Scale), edge, window.MinWidth * window.Scale, window.MinHeight * window.Scale);
                Check("routed drag changes HWND " + edge, Near(NativeWindow.Bounds(window.Handle), expected));
                NativeWindow.Place(window.Handle, before); window.UpdateLayout();
            }
            foreach (var side in new[] { DockSide.Left, DockSide.Right })
            {
                window.CollapseToEdge(side); await Task.Delay(100);
                Check("edge handle " + side, window.Mode == DisplayMode.EdgeHandle && window.ActualWidth <= 32);
                window.HideToTray(); Check("edge to tray " + side, window.Mode == DisplayMode.Tray && !window.IsVisible);
                window.RestoreNormal(); await Task.Delay(100);
                Check("tray restores full bounds " + side, window.Mode == DisplayMode.Normal && Near(NativeWindow.Bounds(window.Handle), before));
            }
            window.EnterCompact(); window.HideToTray(); window.RestoreNormal(); await Task.Delay(100);
            Check("compact to tray to normal", Near(NativeWindow.Bounds(window.Handle), before) && window.Mode == DisplayMode.Normal);
            Check("tray icon registered", window.TrayVisible);
            window.HideToTray(); window.TestTrayShow();
            Check("tray menu show callback", window.IsVisible && window.Mode == DisplayMode.Normal);
            window.Session.TestTrayPin();
            Check("tray pin callback", window.DesktopMode.Layer == WindowLayer.Topmost && window.DesktopMode.NativeTopmost);
            window.Session.TestTrayPin();
            Check("tray returns to desktop and clears TOPMOST", window.DesktopMode.Layer == WindowLayer.Desktop && !window.DesktopMode.NativeTopmost);
            window.Session.TestTraySettings();
            Check("tray settings opens real settings panel", window.SettingsPanel.Visibility == Visibility.Visible);
            window.UpdateLayout(); Capture(window, Path.Combine(output, "settings.png"), 1);
            window.SettingsPanel.Visibility = Visibility.Collapsed;
            window.EnterCompact(); window.HideToTray(); window.TestTrayShow();
            Check("tray locate preserves collapsed view", window.Mode == DisplayMode.Compact && window.CompactContent.Visibility == Visibility.Visible);
            window.RestoreNormal();
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var a = screen.WorkingArea;
                var work = new Rect(a.X, a.Y, a.Width, a.Height);
                NativeWindow.Place(window.Handle, new Rect(a.X + 30, a.Y + 30, Math.Min(1600, a.Width - 60), Math.Min(1100, a.Height - 60)));
                await Task.Delay(300);
                results.Add("Monitor transition DPI=" + NativeWindow.GetDpiForWindow(window.Handle) + "; X=" + a.X);
                var moved = NativeWindow.Bounds(window.Handle);
                Check("monitor bounds " + a.X, work.Contains(moved));
                var compactPlacement = window.SnapshotPreferences().CompactPlacement!;
                window.EnterCompact(); await Task.Delay(80);
                var compactScreen = compactPlacement.Target(); var compactArea = compactScreen.WorkingArea;
                Check("monitor compact retains its saved display " + a.X, Near(NativeWindow.Bounds(window.Handle), compactPlacement.Resolve(new Rect(compactArea.X, compactArea.Y, compactArea.Width, compactArea.Height), NativeWindow.GetDpiForWindow(window.Handle), compactScreen.DeviceName == compactPlacement.Display, true)));
                window.RestoreNormal(); await Task.Delay(80);
                Check("monitor restore " + a.X, Near(NativeWindow.Bounds(window.Handle), moved));
            }
            window.EdgeToggle.IsChecked = true;
            foreach (var side in new[] { DockSide.Left, DockSide.Right })
            {
                var work = NativeWindow.WorkArea(window.Handle);
                var normal = NativeWindow.Bounds(window.Handle);
                var aligned = new Rect(side == DockSide.Left ? work.Left : work.Right - normal.Width, work.Top + 40, normal.Width, normal.Height);
                NativeWindow.Place(window.Handle, aligned); await Task.Delay(50);
                var now = DateTime.UtcNow.AddSeconds(4);
                var inside = new Point(aligned.Left + 30, aligned.Top + 30);
                window.EvaluateEdgeAt(now, inside);
                Check("edge stays open with pointer inside " + side, window.Mode == DisplayMode.Normal);
                var outside = new Point(work.Left - 100, work.Top - 100);
                window.EvaluateEdgeAt(now.AddMilliseconds(20), outside);
                window.EvaluateEdgeAt(now.AddMilliseconds(400), outside);
                Check("edge dwell does not flicker " + side, window.Mode == DisplayMode.Normal);
                window.EvaluateEdgeAt(now.AddMilliseconds(1100), outside);
                Check("edge auto-collapse after dwell " + side, window.Mode == DisplayMode.EdgeHandle);
                window.RestoreButton.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
                Check("edge hover expands " + side, window.Mode == DisplayMode.Normal);
            }
            window.EdgeToggle.IsChecked = false;
            string prefsDir = Path.Combine(output, "preferences-checks-" + Guid.NewGuid().ToString("N"));
            string prefsPath = Path.Combine(prefsDir, "window-preferences.json");
            var store = new WindowPreferencesStore(prefsPath);
            var settings = window.SnapshotPreferences() with { Collapsed = true, Week = true };
            Check("atomic first preferences save", store.Save(settings));
            settings.BackgroundOpacity = .8;
            Check("atomic replacement retains backup", store.Save(settings) && File.Exists(prefsPath + ".bak") && !File.Exists(prefsPath + ".tmp"));
            Check("preferences round trip stable schema/id", store.Load() is { SchemaVersion:2, Id:"desktop-calendar-window", Collapsed:true, Week:true, BackgroundOpacity:.8 });
            File.WriteAllText(prefsPath, "broken");
            Check("corrupt primary recovers backup", store.Load() is { Collapsed:true, Week:true, BackgroundOpacity:.96 } && store.Notice == "PreferencesRecovered");
            File.WriteAllText(prefsPath, JsonSerializer.Serialize(settings with { SchemaVersion = 99 }));
            var futureStore = new WindowPreferencesStore(prefsPath); futureStore.Load();
            Check("future schema is not overwritten", !futureStore.CanSave && !futureStore.Save(settings) && File.ReadAllText(prefsPath).Contains("99"));
            File.WriteAllText(prefsPath, "broken"); File.WriteAllText(prefsPath + ".bak", "broken");
            Check("corrupt primary and backup use defaults", new WindowPreferencesStore(prefsPath).Load() is { SchemaVersion:2, Collapsed:false, Week:false });
            Check("invalid geometry cannot overwrite preferences", !new WindowPreferencesStore(prefsPath).Save(settings with { Width = double.NaN }));
            Check("no mock business data in preferences JSON", !JsonSerializer.Serialize(settings).Contains(DemoContent.TodoNames[0]));
            await RevisionChecks.Run(window, Check);
            window.SetPage(true); window.EnterCompact();
            var session = window.Session;
            var previous = window;
            session.TestWindowLoss();
            for (int i = 0; i < 80 && (session.Window == previous || !session.Window.Ready); i++) await Task.Delay(100);
            window = session.Window;
            Check("component recreation keeps tray, collapsed preference and page", window != previous && window.LayerInitialized && window.Mode == DisplayMode.Compact && window.IsWeek && window.TrayVisible);
            Check("component recreation safely falls back without auto owner", window.DesktopMode.Layer == WindowLayer.Fallback && !window.DesktopMode.NativeTopmost && window.DesktopMode.Owner == IntPtr.Zero);
            session.Locate(); window.RestoreNormal();
            Check("recreated component can explicitly return to desktop", window.DesktopMode.Layer == WindowLayer.Desktop && window.FullContent.Visibility == Visibility.Visible);
            Check("no third-party assembly references", typeof(MainWindow).Assembly.GetReferencedAssemblies().All(a => a.Name!.StartsWith("System") || a.Name.StartsWith("Microsoft") || a.Name is "PresentationCore" or "PresentationFramework" or "WindowsBase"));
            results.Add("MANUAL: integrated Win+D, actual taskbar/AltTab, real owner-mode input/coverage/flicker and tray menu pixels; Windows 10, mixed DPI, Explorer restart, suspend/resume pending. P1.1-4 jitter/Snap and P1.1-5 row/zoom fixes NOT implemented. Raster scaling is not an OS DPI test.");
        }
        catch (Exception ex) { failed++; results.Add("FAIL unhandled check: " + ex); }
        results.Add("Failures: " + failed);
        File.WriteAllLines(Path.Combine(output, "p1-checks.txt"), results);
        window.ExitWithCode(failed == 0 ? 0 : 1);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static RenderTargetBitmap Capture(MainWindow window, string path, double scale)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.Surface.ActualWidth * scale), (int)Math.Ceiling(window.Surface.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(window.Surface);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file); return bitmap;
    }
}
