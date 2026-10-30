using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Linq;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;

namespace DesktopCalendar;
public partial class MainWindow : Window
{
    internal DisplayMode Mode { get; private set; } = DisplayMode.Normal;
    internal IntPtr Handle { get; private set; }
    internal double Scale => Handle == IntPtr.Zero ? 1 : NativeWindow.GetDpiForWindow(Handle) / 96.0;
    internal Rect SavedBounds { get; private set; }
    internal bool IsWeek { get; private set; }
    internal DesktopWindowMode DesktopMode { get; private set; } = null!;
    internal DesktopSession Session { get; }
    internal bool Ready { get; private set; }
    internal bool LayerInitialized { get; set; }
    internal bool IsPinned => DesktopMode?.Layer == WindowLayer.Topmost;
    internal bool PreferredPinned { get; private set; }
    WindowPlacement? fullPlacement, compactPlacement;
    bool changingView;
    private DisplayMode beforeTray = DisplayMode.Normal;
    private readonly DispatcherTimer edgeTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private DateTime outsideSince = DateTime.MaxValue;
    private DateTime suppressEdgeUntil;
    private bool exiting, dragging;
    private Grid monthPage = null!, weekPage = null!;
    private DockSide dockSide;
    private HwndSource? source;

    internal static string Text(string key) => (string)Application.Current.FindResource(key);
    internal MainWindow(DesktopSession session, WindowPreferences preferences)
    {
        Session = session;
        PreferredPinned = preferences.Pinned;
        fullPlacement = preferences.FullPlacement ?? (preferences.HasBounds ? WindowPlacement.Legacy(preferences.Bounds) : null);
        compactPlacement = preferences.CompactPlacement;
        InitializeComponent();
        GuiNextMonitorButton.Visibility = session.VisualChecks ? Visibility.Visible : Visibility.Collapsed;
        monthPage = PrototypeViews.Month();
        weekPage = PrototypeViews.Week();
        PageHost.Children.Add(monthPage);
        PageHost.Children.Add(weekPage);
        SetPage(preferences.Week);
        PinToggle.IsChecked = preferences.Pinned;
        EdgeToggle.IsChecked = preferences.EdgeAutoHide;
        OpacitySlider.Value = preferences.BackgroundOpacity;
        CompactDate.Text = DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        CompactItems.ItemsSource = DemoTodo.View(DemoContent.Todos);
        RestoreColumns(monthPage, preferences.MonthColumns);
        RestoreColumns(weekPage, preferences.WeekColumns);
        BuildResizeHandles();
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            DesktopMode = new DesktopWindowMode(this, Handle, session.VisualChecks);
            source = HwndSource.FromHwnd(Handle);
            source.AddHook(WindowMessage);
        };
        Loaded += (_, _) =>
        {
            edgeTimer.Start();
        };
        LocationChanged += (_, _) => Remember();
        SizeChanged += (_, _) => Remember();
        edgeTimer.Tick += (_, _) => EvaluateEdge();
        RestoreButton.MouseEnter += (_, _) => { if (Mode == DisplayMode.EdgeHandle) RestoreNormal(); };
        Closing += OnClosing;
        Closed += (_, _) => { Ready = false; edgeTimer.Stop(); source?.RemoveHook(WindowMessage); };
        Surface.SizeChanged += (_, _) => Surface.Clip = new RectangleGeometry(new Rect(0, 0, Surface.ActualWidth, Surface.ActualHeight), Mode == DisplayMode.Normal ? 18 : 12, Mode == DisplayMode.Normal ? 18 : 12);
    }
    internal async Task RestoreStartup(WindowPreferences preferences)
    {
        // ContentRendered and owner initialization must finish before taking any snapshots.
        // WPF can apply its initial placement / WM_DPICHANGED after Loaded.
        if (fullPlacement != null) ApplyPlacement(fullPlacement, false);
        else FitToScreen();
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
        if (fullPlacement != null) ApplyPlacement(fullPlacement, false);
        fullPlacement = WindowPlacement.Capture(Handle); SavedBounds = fullPlacement.Bounds;
        if (preferences.Collapsed)
        {
            EnterCompact();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ContextIdle);
            ApplyPlacement(compactPlacement!, true);
            compactPlacement = WindowPlacement.Capture(Handle);
        }
        Ready = true;
    }
    void ApplyPlacement(WindowPlacement placement, bool compact)
    {
        var screen = placement.Target(); var area = screen.WorkingArea;
        var work = new Rect(area.X, area.Y, area.Width, area.Height);
        // Move onto the target monitor first so GetDpiForWindow reflects that monitor.
        NativeWindow.Place(Handle, WindowGeometry.Clamp(new Rect(work.X + 24, work.Y + 24, placement.Width, placement.Height), work));
        double dpi = NativeWindow.GetDpiForWindow(Handle);
        MinWidth = Math.Min(compact ? 320 : 880, work.Width * 96 / dpi);
        MinHeight = Math.Min(compact ? 240 : 520, work.Height * 96 / dpi);
        NativeWindow.Place(Handle, placement.Resolve(work, dpi, screen.DeviceName == placement.Display, compact));
    }
    private void Remember()
    {
        if (Ready && !dragging && !changingView) { CapturePlacement(); Session.Changed(); }
    }
    void CapturePlacement()
    {
        if (!IsVisible || WindowState != WindowState.Normal || !DesktopWindowMode.IsWindow(Handle)) return;
        var placement = WindowPlacement.Capture(Handle);
        if (!placement.Valid()) return;
        if (Mode == DisplayMode.Normal) { fullPlacement = placement; SavedBounds = placement.Bounds; }
        else if (Mode == DisplayMode.Compact) compactPlacement = placement;
    }
    internal void RefreshLayerStatus()
    {
        if (DesktopMode == null) return;
        PinToggle.IsChecked = IsPinned;
        ModeStatus.Text = Text(DesktopMode.Layer switch { WindowLayer.Desktop => "DesktopMode", WindowLayer.Topmost => "PinnedMode", _ => "FallbackMode" });
        if (Session.VisualChecks) ModeStatus.Text = Text("GuiCheckTitle");
        SettingsNotice.Text = DesktopMode.Layer == WindowLayer.Fallback ? Text("FallbackNotice") : Text("SettingsNotice");
        if (Session.Notice.Length > 0) SettingsNotice.Text += "\n" + Text(Session.Notice);
    }
    internal WindowPreferences SnapshotPreferences()
    {
        if (!changingView) CapturePlacement();
        var p = Session.Preferences with { Collapsed = Mode == DisplayMode.Compact || Mode == DisplayMode.Tray && beforeTray == DisplayMode.Compact,
            Week = IsWeek, Pinned = PreferredPinned, EdgeAutoHide = EdgeToggle.IsChecked == true, BackgroundOpacity = OpacitySlider.Value,
            FullPlacement = fullPlacement, CompactPlacement = compactPlacement };
        p.SetBounds(fullPlacement?.Bounds ?? SavedBounds);
        if (monthPage.IsVisible) p.MonthColumns = ColumnWidths(monthPage);
        if (weekPage.IsVisible) p.WeekColumns = ColumnWidths(weekPage);
        return p;
    }
    static double[] ColumnWidths(Grid grid) => grid.ColumnDefinitions.Where((_, i) => i % 2 == 0).Select(c => c.ActualWidth > 0 ? c.ActualWidth : c.Width.Value).ToArray();
    static void RestoreColumns(Grid grid, double[] widths)
    {
        for (int i = 0; i < widths.Length; i++) grid.ColumnDefinitions[i * 2].Width = new GridLength(widths[i], GridUnitType.Star);
    }
    private IntPtr WindowMessage(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        // Our eight WPF grips own resizing. Do not let the invisible native frame
        // consume pointer input before Thumb can capture it on layered windows.
        if (msg == 0x0084) { handled = true; return new IntPtr(1); }
        if ((uint)msg == DesktopWindowMode.TaskbarCreated || msg == 0x0218 && (w.ToInt64() == 7 || w.ToInt64() == 18))
            Dispatcher.BeginInvoke(() => Session.ShellSignal());
        // WPF owns WM_DPICHANGED, including its suggested bounds. Re-evaluate after WPF.
        if (msg == 0x02E0 || msg == 0x007E)
            Dispatcher.BeginInvoke(() =>
            {
                if (!Ready || changingView) return;
                if (Mode == DisplayMode.Compact)
                {
                    var work = NativeWindow.WorkArea(Handle);
                    MinWidth = Math.Min(320, work.Width / Scale); MinHeight = Math.Min(240, work.Height / Scale);
                    NativeWindow.Place(Handle, WindowGeometry.Clamp(NativeWindow.Bounds(Handle), work));
                }
                else if (Mode == DisplayMode.EdgeHandle) RestoreNormal();
                else if (Mode == DisplayMode.Normal) FitToScreen();
                Remember();
            });
        return IntPtr.Zero;
    }
    internal void FitToScreen()
    {
        var work = NativeWindow.WorkArea(Handle);
        MinWidth = Math.Min(880, work.Width / Scale);
        MinHeight = Math.Min(520, work.Height / Scale);
        NativeWindow.Place(Handle, WindowGeometry.Clamp(NativeWindow.Bounds(Handle), work));
    }
    internal void SetPage(bool week)
    {
        Remember(); // Capture outgoing page's splitter widths before collapsing its layout.
        IsWeek = week;
        monthPage.Visibility = week ? Visibility.Collapsed : Visibility.Visible;
        weekPage.Visibility = week ? Visibility.Visible : Visibility.Collapsed;
        MonthButton.Background = PrototypeViews.Brush(week ? "#17FFFFFF" : "#355E786C");
        WeekButton.Background = PrototypeViews.Brush(week ? "#355E786C" : "#17FFFFFF");
        Remember();
    }
    private void ShowMonth(object sender, RoutedEventArgs e) => SetPage(false);
    private void ShowWeek(object sender, RoutedEventArgs e) => SetPage(true);
    private void DragWindow(object sender, MouseButtonEventArgs e)
    {
        if (Mode is not (DisplayMode.Normal or DisplayMode.Compact) || e.ChangedButton != MouseButton.Left || e.OriginalSource is ButtonBase) return;
        dragging = true;
        try { DragMove(); } catch (InvalidOperationException) { /* Mouse released before native move starts. */ }
        finally { dragging = false; suppressEdgeUntil = DateTime.UtcNow.AddSeconds(1); Remember(); }
    }
    private void CompactClick(object sender, RoutedEventArgs e) => EnterCompact();
    private void TrayClick(object sender, RoutedEventArgs e) => HideToTray();
    private void RestoreClick(object sender, RoutedEventArgs e) => RestoreNormal();
    private void PinChanged(object sender, RoutedEventArgs e) => SetPinned(PinToggle.IsChecked == true);
    internal void SetPinned(bool enabled)
    {
        PreferredPinned = enabled;
        DesktopMode.Apply(enabled);
        RefreshLayerStatus(); Remember();
    }
    private void SettingsClick(object sender, RoutedEventArgs e) => OpenSettings();
    private void SettingsClose(object sender, RoutedEventArgs e) => SettingsPanel.Visibility = Visibility.Collapsed;
    private void ExitClick(object sender, RoutedEventArgs e) => Exit();
    private void SafeLocateClick(object sender, RoutedEventArgs e) => Session.Locate(true);
    private void GuiNextMonitorClick(object sender, RoutedEventArgs e)
    {
        if (!Session.VisualChecks) return;
        var screens = Forms.Screen.AllScreens;
        int current = Array.FindIndex(screens, s => s.DeviceName == Forms.Screen.FromHandle(Handle).DeviceName);
        var work = screens[(current + 1) % screens.Length].WorkingArea;
        var bounds = NativeWindow.Bounds(Handle);
        NativeWindow.Place(Handle, WindowGeometry.Clamp(new Rect(work.X + 90, work.Y + 90, bounds.Width, bounds.Height), new Rect(work.X, work.Y, work.Width, work.Height)));
        Remember();
    }
    internal void OpenSettings() { RefreshLayerStatus(); SettingsPanel.Visibility = Visibility.Visible; SettingsCloseButton.Focus(); }
    private void EdgeChanged(object sender, RoutedEventArgs e)
    {
        outsideSince = DateTime.MaxValue;
        if (Mode == DisplayMode.EdgeHandle) RestoreNormal();
        Remember();
    }
    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Shell != null) Shell.Background = new SolidColorBrush(Color.FromArgb((byte)(e.NewValue * 255), 25, 35, 46));
        Remember();
    }
    private void SaveNormalBounds()
    {
        if (Mode == DisplayMode.Normal) SavedBounds = NativeWindow.Bounds(Handle);
    }
    private void SmallMode(DisplayMode mode)
    {
        Mode = mode;
        FullContent.Visibility = Visibility.Collapsed;
        ResizeHandles.Visibility = Visibility.Collapsed;
        CompactContent.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility = Visibility.Collapsed;
        RestoreButton.Visibility = mode == DisplayMode.EdgeHandle ? Visibility.Visible : Visibility.Collapsed;
        RestoreButton.ToolTip = Text("HandleTip");
        RestoreButton.Content = dockSide == DockSide.Left ? "›" : "‹";
        Shell.CornerRadius = new CornerRadius(12);
        MinWidth = 20; MinHeight = 20;
    }
    internal void EnterCompact()
    {
        if (Mode == DisplayMode.Compact) return;
        CompactDate.Text = DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        SaveNormalBounds();
        CapturePlacement(); changingView = true;
        SmallMode(DisplayMode.Compact);
        CompactContent.Visibility = Visibility.Visible;
        ResizeHandles.Visibility = Visibility.Visible;
        MinWidth = 320; MinHeight = 240;
        if (compactPlacement != null) ApplyPlacement(compactPlacement, true);
        else NativeWindow.Place(Handle, WindowGeometry.Compact(NativeWindow.WorkArea(Handle), Scale));
        Show(); RestoreFullButton.Focus();
        compactPlacement = WindowPlacement.Capture(Handle); changingView = false; Remember();
    }
    internal void HideToTray()
    {
        SaveNormalBounds();
        CapturePlacement();
        if (Mode != DisplayMode.Tray) beforeTray = Mode == DisplayMode.Compact ? DisplayMode.Compact : DisplayMode.Normal;
        SettingsPanel.Visibility = Visibility.Collapsed;
        Mode = DisplayMode.Tray;
        Hide(); Remember(); if (Ready) Session.Save();
    }
    internal void ShowComponent()
    {
        if (Mode == DisplayMode.Tray && beforeTray == DisplayMode.Compact) { Mode = DisplayMode.Compact; Show(); }
        else if (Mode is DisplayMode.Tray or DisplayMode.EdgeHandle) RestoreNormal();
        else Show();
        NativeWindow.Place(Handle, WindowGeometry.Clamp(NativeWindow.Bounds(Handle), NativeWindow.WorkArea(Handle)));
    }
    internal void RestoreNormal()
    {
        bool needsRestore = Mode != DisplayMode.Normal;
        CapturePlacement(); changingView = true;
        Mode = DisplayMode.Normal;
        FullContent.Visibility = Visibility.Visible;
        ResizeHandles.Visibility = Visibility.Visible;
        RestoreButton.Visibility = Visibility.Collapsed;
        CompactContent.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility = Visibility.Collapsed;
        Shell.CornerRadius = new CornerRadius(18);
        if (needsRestore)
        {
            ApplyPlacement(fullPlacement ?? WindowPlacement.Legacy(SavedBounds), false);
        }
        Show(); FitToScreen(); Activate(); (IsWeek ? WeekButton : MonthButton).Focus();
        outsideSince = DateTime.MaxValue;
        suppressEdgeUntil = DateTime.UtcNow.AddSeconds(1.2);
        changingView = false;
        Remember();
    }
    internal void CollapseToEdge(DockSide side)
    {
        if (Mode != DisplayMode.Normal || side == DockSide.None) return;
        SaveNormalBounds(); dockSide = side;
        var work = NativeWindow.WorkArea(Handle);
        var bounds = WindowGeometry.Handle(SavedBounds, work, side, Scale);
        SmallMode(DisplayMode.EdgeHandle);
        NativeWindow.Place(Handle, bounds);
        Remember();
    }
    private void EvaluateEdge()
    {
        NativeWindow.GetCursorPos(out var p);
        EvaluateEdgeAt(DateTime.UtcNow, new Point(p.X, p.Y));
    }
    internal void EvaluateEdgeAt(DateTime now, Point pointer)
    {
        if (Mode != DisplayMode.Normal || EdgeToggle.IsChecked != true || dragging || Mouse.Captured != null || now < suppressEdgeUntil) return;
        var bounds = NativeWindow.Bounds(Handle);
        var side = WindowGeometry.Edge(bounds, NativeWindow.WorkArea(Handle), Scale);
        if (side == DockSide.None || bounds.Contains(pointer)) { outsideSince = DateTime.MaxValue; return; }
        if (outsideSince == DateTime.MaxValue) outsideSince = now;
        else if (now - outsideSince > TimeSpan.FromMilliseconds(900)) CollapseToEdge(side);
    }
    internal bool TrayVisible => Session.TrayVisible;
    internal void TestTrayShow() => Session.TestTrayShow();
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!exiting) { e.Cancel = true; HideToTray(); }
    }
    internal void Exit()
    {
        ExitWithCode(0);
    }
    internal void ExitWithCode(int code) => Session.Exit(code);
    internal void CloseForRecovery() { exiting = true; Close(); }
    private void BuildResizeHandles()
    {
        foreach (string edge in new[] { "N", "S", "W", "E", "NW", "NE", "SW", "SE" })
        {
            bool corner = edge.Length == 2;
            var thumb = new Thumb
            {
                Width = edge is "N" or "S" ? double.NaN : corner ? 16 : 7,
                Height = edge is "W" or "E" ? double.NaN : corner ? 16 : 7,
                HorizontalAlignment = edge.Contains('W') ? HorizontalAlignment.Left : edge.Contains('E') ? HorizontalAlignment.Right : HorizontalAlignment.Stretch,
                VerticalAlignment = edge.Contains('N') ? VerticalAlignment.Top : edge.Contains('S') ? VerticalAlignment.Bottom : VerticalAlignment.Stretch,
                Cursor = edge is "N" or "S" ? Cursors.SizeNS : edge is "W" or "E" ? Cursors.SizeWE : edge is "NW" or "SE" ? Cursors.SizeNWSE : Cursors.SizeNESW,
                Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
                Focusable = false
            };
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)));
            thumb.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = factory };
            thumb.DragStarted += (_, _) => { dragging = true; };
            thumb.DragDelta += (_, e) =>
            {
                // Thumb supplies deltas in device-independent units, including injected
                // pointer events. Use that event stream rather than polling a second cursor.
                var current = NativeWindow.Bounds(Handle);
                NativeWindow.Place(Handle, WindowGeometry.Resize(current, new Vector(e.HorizontalChange * Scale, e.VerticalChange * Scale), edge, MinWidth * Scale, MinHeight * Scale));
            };
            thumb.DragCompleted += (_, _) => { dragging = false; suppressEdgeUntil = DateTime.UtcNow.AddSeconds(1); Remember(); };
            ResizeHandles.Children.Add(thumb);
        }
    }
}
