using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DesktopCalendar;
public partial class MainWindow : Window
{
    internal DisplayMode Mode { get; private set; } = DisplayMode.Normal;
    internal IntPtr Handle { get; private set; }
    internal double Scale => Handle == IntPtr.Zero ? 1 : NativeWindow.GetDpiForWindow(Handle) / 96.0;
    internal Rect SavedBounds { get; private set; }
    internal bool IsWeek { get; private set; }
    private Forms.NotifyIcon? tray;
    private readonly DispatcherTimer edgeTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private DateTime outsideSince = DateTime.MaxValue;
    private DateTime suppressEdgeUntil;
    private bool exiting, dragging;
    private Grid monthPage = null!, weekPage = null!;
    private DockSide dockSide;
    private HwndSource? source;

    internal static string Text(string key) => (string)Application.Current.FindResource(key);
    public MainWindow()
    {
        InitializeComponent();
        monthPage = PrototypeViews.Month();
        weekPage = PrototypeViews.Week();
        PageHost.Children.Add(monthPage);
        PageHost.Children.Add(weekPage);
        SetPage(false);
        BuildResizeHandles();
        SourceInitialized += (_, _) =>
        {
            Handle = new WindowInteropHelper(this).Handle;
            source = HwndSource.FromHwnd(Handle);
            source.AddHook(WindowMessage);
        };
        Loaded += (_, _) =>
        {
            FitToScreen();
            SavedBounds = NativeWindow.Bounds(Handle);
            CreateTray();
            edgeTimer.Start();
        };
        edgeTimer.Tick += (_, _) => EvaluateEdge();
        RestoreButton.MouseEnter += (_, _) => { if (Mode == DisplayMode.EdgeHandle) RestoreNormal(); };
        Closing += OnClosing;
        Closed += (_, _) => { edgeTimer.Stop(); tray?.Dispose(); source?.RemoveHook(WindowMessage); };
        Surface.SizeChanged += (_, _) => Surface.Clip = new RectangleGeometry(new Rect(0, 0, Surface.ActualWidth, Surface.ActualHeight), Mode == DisplayMode.Normal ? 18 : 12, Mode == DisplayMode.Normal ? 18 : 12);
    }
    private IntPtr WindowMessage(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        // Our eight WPF grips own resizing. Do not let the invisible native frame
        // consume pointer input before Thumb can capture it on layered windows.
        if (msg == 0x0084) { handled = true; return new IntPtr(1); }
        // WPF owns WM_DPICHANGED, including its suggested bounds. Re-evaluate after WPF.
        if (msg == 0x02E0 || msg == 0x007E)
            Dispatcher.BeginInvoke(() =>
            {
                if (Mode == DisplayMode.Compact) NativeWindow.Place(Handle, WindowGeometry.Compact(NativeWindow.WorkArea(Handle), Scale));
                else if (Mode == DisplayMode.EdgeHandle) RestoreNormal();
                else if (Mode == DisplayMode.Normal) FitToScreen();
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
    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Text("Show"), null, (_, _) => Dispatcher.Invoke(RestoreNormal));
        menu.Items.Add(Text("Exit"), null, (_, _) => Dispatcher.Invoke(Exit));
        tray = new Forms.NotifyIcon { Text = Text("AppName"), Icon = System.Drawing.SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreNormal);
    }
    internal void SetPage(bool week)
    {
        IsWeek = week;
        monthPage.Visibility = week ? Visibility.Collapsed : Visibility.Visible;
        weekPage.Visibility = week ? Visibility.Visible : Visibility.Collapsed;
        MonthButton.Background = PrototypeViews.Brush(week ? "#17FFFFFF" : "#355E786C");
        WeekButton.Background = PrototypeViews.Brush(week ? "#355E786C" : "#17FFFFFF");
    }
    private void ShowMonth(object sender, RoutedEventArgs e) => SetPage(false);
    private void ShowWeek(object sender, RoutedEventArgs e) => SetPage(true);
    private void DragWindow(object sender, MouseButtonEventArgs e)
    {
        if (Mode != DisplayMode.Normal || e.ChangedButton != MouseButton.Left) return;
        dragging = true;
        try { DragMove(); } catch (InvalidOperationException) { /* Mouse released before native move starts. */ }
        finally { dragging = false; suppressEdgeUntil = DateTime.UtcNow.AddSeconds(1); }
    }
    private void CompactClick(object sender, RoutedEventArgs e) => EnterCompact();
    private void TrayClick(object sender, RoutedEventArgs e) => HideToTray();
    private void RestoreClick(object sender, RoutedEventArgs e) => RestoreNormal();
    private void PinChanged(object sender, RoutedEventArgs e) => SetPinned(PinToggle.IsChecked == true);
    internal void SetPinned(bool enabled)
    {
        Topmost = enabled;
        NativeWindow.Pin(Handle, enabled);
    }
    private void EdgeChanged(object sender, RoutedEventArgs e)
    {
        outsideSince = DateTime.MaxValue;
        if (Mode == DisplayMode.EdgeHandle) RestoreNormal();
    }
    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Shell != null) Shell.Background = new SolidColorBrush(Color.FromArgb((byte)(e.NewValue * 255), 25, 35, 46));
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
        RestoreButton.Visibility = Visibility.Visible;
        RestoreButton.ToolTip = Text(mode == DisplayMode.Compact ? "CompactTip" : "HandleTip");
        RestoreButton.Content = mode == DisplayMode.Compact ? Text("Compact") : (dockSide == DockSide.Left ? "›" : "‹");
        Shell.CornerRadius = new CornerRadius(12);
        MinWidth = 20; MinHeight = 20;
    }
    internal void EnterCompact()
    {
        if (Mode == DisplayMode.Compact) return;
        SaveNormalBounds();
        SmallMode(DisplayMode.Compact);
        NativeWindow.Place(Handle, WindowGeometry.Compact(NativeWindow.WorkArea(Handle), Scale));
        Show(); Activate(); RestoreButton.Focus();
    }
    internal void HideToTray()
    {
        SaveNormalBounds();
        Mode = DisplayMode.Tray;
        Hide();
    }
    internal void RestoreNormal()
    {
        bool needsRestore = Mode != DisplayMode.Normal;
        Mode = DisplayMode.Normal;
        FullContent.Visibility = Visibility.Visible;
        ResizeHandles.Visibility = Visibility.Visible;
        RestoreButton.Visibility = Visibility.Collapsed;
        Shell.CornerRadius = new CornerRadius(18);
        if (needsRestore)
        {
            var center = new System.Drawing.Point((int)(SavedBounds.X + SavedBounds.Width / 2), (int)(SavedBounds.Y + SavedBounds.Height / 2));
            var area = Forms.Screen.FromPoint(center).WorkingArea;
            NativeWindow.Place(Handle, WindowGeometry.Clamp(SavedBounds, new Rect(area.X, area.Y, area.Width, area.Height)));
        }
        Show(); FitToScreen(); Activate(); (IsWeek ? WeekButton : MonthButton).Focus();
        outsideSince = DateTime.MaxValue;
        suppressEdgeUntil = DateTime.UtcNow.AddSeconds(1.2);
    }
    internal void CollapseToEdge(DockSide side)
    {
        if (Mode != DisplayMode.Normal || side == DockSide.None) return;
        SaveNormalBounds(); dockSide = side;
        var work = NativeWindow.WorkArea(Handle);
        var bounds = WindowGeometry.Handle(SavedBounds, work, side, Scale);
        SmallMode(DisplayMode.EdgeHandle);
        NativeWindow.Place(Handle, bounds);
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
    internal bool TrayVisible => tray?.Visible == true;
    internal void TestTrayShow() => tray?.ContextMenuStrip?.Items[0].PerformClick();
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!exiting) { e.Cancel = true; HideToTray(); }
    }
    internal void Exit()
    {
        ExitWithCode(0);
    }
    internal void ExitWithCode(int code) { exiting = true; Close(); Application.Current.Shutdown(code); }
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
            thumb.DragCompleted += (_, _) => { dragging = false; suppressEdgeUntil = DateTime.UtcNow.AddSeconds(1); };
            ResizeHandles.Children.Add(thumb);
        }
    }
}
