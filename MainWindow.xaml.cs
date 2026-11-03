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
using System.Collections.Generic;
using Forms = System.Windows.Forms;
using DesktopCalendar.Core;

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
    internal TimelineView DailyTimeline { get; }
    internal TimelineView WeeklyTimeline { get; }
    internal ActivityDetailCard? ActivityDetails { get; }
    internal PlannerView? PlannerView { get; private set; }
    internal WindowPreferences Appearance { get; private set; } = new();
    bool applyingLanguage;
    internal int PlannerDialogDepth { get; set; }
    private DockSide dockSide;
    private HwndSource? source;
    internal sealed record VisiblePlacement(long Milliseconds, Rect Bounds, DisplayMode Mode, string Stage);
    internal readonly List<VisiblePlacement> StartupVisible = new();
    internal bool VisibleBeforePrepared { get; private set; }
    readonly System.Diagnostics.Stopwatch startupClock = System.Diagnostics.Stopwatch.StartNew();
    bool startupPrepared;
    Rect gestureStart;
    Point gesturePointer;
    double gestureMinWidth, gestureMinHeight;
    string? gestureEdge;
    FrameworkElement? movingTitle;

    internal static string Text(string key) => (string)Application.Current.FindResource(key);
    internal MainWindow(DesktopSession session, WindowPreferences preferences)
    {
        Session = session;
        PreferredPinned = preferences.Pinned;
        fullPlacement = preferences.FullPlacement ?? (preferences.HasBounds ? WindowPlacement.Legacy(preferences.Bounds) : null);
        compactPlacement = preferences.CompactPlacement;
        InitializeComponent();
        Appearance = preferences with { }; applyingLanguage = true; LanguageChoice.SelectedIndex = preferences.UiLanguage == "en" ? 1 : 0; applyingLanguage = false;
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(Text("PlannerUiLanguage"));
        WeekStartChoice.ItemsSource = Localization.Culture.DateTimeFormat.DayNames;
        GuiNextMonitorButton.Visibility = session.VisualChecks ? Visibility.Visible : Visibility.Collapsed;
        DailyTimeline = new TimelineView(false, 3, preferences.DayHourHeight);
        WeeklyTimeline = new TimelineView(true, 0, preferences.WeekHourHeight);
        DailyTimeline.ScaleChanged += Remember; WeeklyTimeline.ScaleChanged += Remember;
        if (session.Planner == null) { monthPage = PrototypeViews.Month(DailyTimeline); CategorySettingsButton.Visibility = CalendarWrapping.Visibility = Visibility.Collapsed; }
        else { PlannerView = new PlannerView(this, session.Planner, DailyTimeline); monthPage = PlannerView; }
        weekPage = session.Planner == null ? PrototypeViews.Week(WeeklyTimeline) : new ActivityWeekView(this, session.Planner, WeeklyTimeline);
        if (session.Planner != null) { DailyTimeline.Bind(this, session.Planner); WeeklyTimeline.Bind(this, session.Planner); }
        if (session.Planner != null) ActivityDetails = new ActivityDetailCard(this, session.Planner);
        PageHost.Children.Add(monthPage);
        PageHost.Children.Add(weekPage);
        SetPage(preferences.Week);
        PinToggle.IsChecked = preferences.Pinned;
        EdgeToggle.IsChecked = preferences.EdgeAutoHide;
        OpacitySlider.Value = preferences.BackgroundOpacity;
        ApplyAppearance(preferences);
        CompactDate.Text = DateTime.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (session.Planner == null) CompactItems.ItemsSource = DemoTodo.View(DemoContent.Todos);
        else
        {
            CompactItems.Visibility = Visibility.Collapsed;
            session.Planner.Changed += RefreshPlanner;
            PreviewKeyDown += PlannerKey;
            RefreshPlanner();
        }
        RestoreColumns(monthPage, preferences.MonthColumns);
        RestoreColumns(weekPage, preferences.WeekColumns);
        BuildResizeHandles();
        PreviewMouseMove += MoveTitle;
        PreviewMouseLeftButtonUp += EndTitle;
        LostMouseCapture += (_, _) => { if (movingTitle != null) FinishPointerGesture(); };
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
        RestoreButton.MouseEnter += (_, _) => { if (Mode == DisplayMode.EdgeHandle) RestoreNormal(false); };
        Closing += OnClosing;
        Closed += (_, _) => { Ready = false; edgeTimer.Stop(); source?.RemoveHook(WindowMessage); PlannerView?.Detach(); DailyTimeline.Unbind(); WeeklyTimeline.Unbind(); if (session.Planner != null) session.Planner.Changed -= RefreshPlanner; };
        Surface.SizeChanged += (_, _) => Surface.Clip = new RectangleGeometry(new Rect(0, 0, Surface.ActualWidth, Surface.ActualHeight), Mode == DisplayMode.Normal ? 18 : 12, Mode == DisplayMode.Normal ? 18 : 12);
    }
    internal void ShowPlannerMonth() { if (Mode != DisplayMode.Normal) RestoreNormal(); SetPage(false); }
    async void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (applyingLanguage || !Ready || LanguageChoice.SelectedIndex < 0) return;
        if (Application.Current.Windows.OfType<PlannerDialogs.Sheet>().Any(w => w.IsVisible)) { applyingLanguage = true; LanguageChoice.SelectedIndex = Appearance.UiLanguage == "en" ? 1 : 0; applyingLanguage = false; return; }
        if (Session.Planner != null && !await Session.Planner.CommitInlineAsync()) return;
        Appearance.UiLanguage = LanguageChoice.SelectedIndex == 1 ? "en" : "zh-CN"; Localization.Apply(Appearance.UiLanguage); Session.RefreshLanguage();
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(Text("PlannerUiLanguage")); WeekStartChoice.ItemsSource = Localization.Culture.DateTimeFormat.DayNames; WeekStartChoice.SelectedIndex = (int)(Session.Planner?.WeekStart ?? DayOfWeek.Monday);
        if (Session.Planner != null)
        {
            ActivityDetails?.Hide(); PlannerView?.Detach(); (weekPage as ActivityWeekView)?.Detach(); DailyTimeline.Unbind(); WeeklyTimeline.Unbind();
            RemoveVisual(DailyTimeline); RemoveVisual(WeeklyTimeline); PageHost.Children.Clear();
            PlannerView = new PlannerView(this, Session.Planner, DailyTimeline); monthPage = PlannerView; weekPage = new ActivityWeekView(this, Session.Planner, WeeklyTimeline);
            DailyTimeline.Bind(this, Session.Planner); WeeklyTimeline.Bind(this, Session.Planner); PageHost.Children.Add(monthPage); PageHost.Children.Add(weekPage); SetPage(IsWeek); RefreshPlanner();
        }
        RefreshLayerStatus(); Remember();
    }
    static void RemoveVisual(FrameworkElement child) { if (child.Parent is Panel p) p.Children.Remove(child); else if (child.Parent is Decorator d) d.Child = null; }
    internal void ApplyAppearance(WindowPreferences p)
    {
        Appearance = p with { }; OpacitySlider.Value = p.BackgroundOpacity;
        ApplyShell(p.BackgroundOpacity);
        Application.Current.Resources["Accent"] = ThemeColors.Brush(ThemeColors.Accent(p.AccentColor));
        PlannerView?.InvalidateCalendar(); Remember();
    }
    void ApplyShell(double opacity)
    {
        byte alpha = (byte)(opacity * 255); opacity = alpha / 255d;
        var c = ThemeColors.Parse(Appearance.BackgroundColor); double mix = Math.Max(c.R, Math.Max(c.G, c.B)) > 80 ? .22 : 1;
        var safe = Color.FromRgb((byte)(c.R * mix + 25 * (1 - mix)), (byte)(c.G * mix + 35 * (1 - mix)), (byte)(c.B * mix + 46 * (1 - mix)));
        string Composite() => ThemeColors.Hex(Color.FromRgb((byte)(safe.R * opacity + 255 * (1 - opacity)), (byte)(safe.G * opacity + 255 * (1 - opacity)), (byte)(safe.B * opacity + 255 * (1 - opacity))));
        for (int i = 0; i < 30 && ThemeColors.Contrast(Composite(), "#EDF2F5") < 4.5; i++) safe = Color.FromRgb((byte)(safe.R * .85), (byte)(safe.G * .85), (byte)(safe.B * .85));
        Shell.Background = new SolidColorBrush(Color.FromArgb(alpha, safe.R, safe.G, safe.B));
        Application.Current.Resources["PanelBackground"] = ThemeColors.Brush(opacity < .85 ? "#C0101A24" : "#35101A24");
    }
    void AppearanceClick(object sender, RoutedEventArgs e) => ApplicationSettings.Appearance(this);
    void CalendarUpdateClick(object sender, RoutedEventArgs e) => ApplicationSettings.Calendar(this);
    void BackupClick(object sender, RoutedEventArgs e) => ApplicationSettings.Backup(this);
    void StartupChanged(object sender, RoutedEventArgs e)
    {
        try { if (Session.Testing) throw new InvalidOperationException("TestStartupUseIsolatedService"); new StartupEntry().Set(StartupToggle.IsChecked == true); }
        catch (Exception ex) when (ex is System.IO.IOException or System.IO.InvalidDataException or UnauthorizedAccessException or InvalidOperationException or System.Text.Json.JsonException or System.Runtime.InteropServices.COMException)
        { StartupToggle.IsChecked = !StartupToggle.IsChecked; SettingsNotice.Text = Text(ex.Message == "StartupUninstalled" ? "StartupUninstalled" : "StartupFailed"); }
    }
    async void PlannerKey(object sender, KeyEventArgs e)
    {
        if (Session.Planner == null || (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        if (e.OriginalSource is System.Windows.Controls.Primitives.TextBoxBase input && (input != PlannerView?.Quick || PlannerView.Quick.Text.Length > 0)) return;
        if (e.Key == Key.Z) { e.Handled = true; if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) await Session.Planner.RedoAsync(); else await Session.Planner.UndoAsync(); }
        if (e.Key == Key.Y) { e.Handled = true; await Session.Planner.RedoAsync(); }
    }
    void CategorySettingsClick(object sender, RoutedEventArgs e) { if (Session.Planner?.Writable == true) PlannerDialogs.ManageCategories(this, Session.Planner); }
    void CalendarWrappingChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { if (CalendarWrapping.SelectedIndex >= 0) Session.Planner?.SetTextMode((CalendarTextMode)CalendarWrapping.SelectedIndex); }
    async void WeekStartChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { if (WeekStartChoice.SelectedIndex >= 0 && Session.Planner != null) await Session.Planner.SetWeekStartAsync((DayOfWeek)WeekStartChoice.SelectedIndex); }
    void WeekLayoutChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { if (WeekLayoutChoice.SelectedIndex >= 0) Session.Planner?.SetWeekLayout((WeekPlanLayout)WeekLayoutChoice.SelectedIndex); }
    void RefreshPlanner()
    {
        var planner = Session.Planner!;
        CalendarWrapping.SelectedIndex = (int)planner.TextMode;
        WeekStartChoice.SelectedIndex = (int)planner.WeekStart; WeekLayoutChoice.SelectedIndex = (int)planner.WeekLayout;
        PlannerFooter.Text = CompactNotice.Text = Text(planner.NoticeKey);
        PlannerFooter.ToolTip = planner.Diagnostic.Length == 0 ? Text(planner.NoticeKey) : planner.Diagnostic;
        var noticeBrush = PrototypeViews.Brush(planner.NoticeKey is "PlannerLocal" or "PlannerLoading" or "PlannerSaving" ? "#A3D9C4" : "#E3BF89");
        PlannerFooter.Foreground = CompactNotice.Foreground = noticeBrush;
        CompactRealList.Children.Clear(); CompactRealList.IsEnabled = planner.Writable;
        CategorySettingsButton.IsEnabled = planner.Writable;
        CompactDate.Text = planner.Today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var rows = planner.Tree(planner.Today);
        if (rows.Length == 0) CompactRealList.Children.Add(PlannerView.Text(Text(planner.State == null ? planner.NoticeKey : "PlannerEmpty"), 12, "#A2AFBC"));
        void Row(TodoDateNode node, int depth)
        {
            var task = node.Value; var text = PlannerView.Text(""); text.Inlines.Add(new System.Windows.Documents.Run(PlannerView.Prefix(task)) { FontWeight = FontWeights.SemiBold, Foreground = PrototypeViews.Brush("#A3D9C4") }); text.Inlines.Add(new System.Windows.Documents.Run(task.Title)); text.TextWrapping = TextWrapping.Wrap; text.TextDecorations = task.Status == TodoStatus.Completed ? TextDecorations.Strikethrough : null; text.Opacity = task.Status == TodoStatus.Completed ? .55 : 1;
            var check = new CheckBox { Content = text, IsChecked = task.Status == TodoStatus.Completed, Margin = new Thickness(depth * 12, 0, 0, 12), IsEnabled = !node.ContextOnly };
            System.Windows.Automation.AutomationProperties.SetName(check, task.Title);
            check.Click += async (_, _) => { bool complete = check.IsChecked == true; check.IsChecked = task.Status == TodoStatus.Completed; await planner.ExecuteAsync(new SetTodoCompleted(TodoRef.Of(task), complete)); };
            CompactRealList.Children.Add(check); foreach (var child in node.Children) Row(child, Math.Min(depth + 1, 6));
        }
        foreach (var node in rows) Row(node, 0);
    }
    internal void PrepareStartup(WindowPreferences preferences, bool fallback)
    {
        // Create a hidden HWND at its real destination, not an off-screen parking position.
        // Manual startup lets WPF retain the native geometry before its first Show.
        new WindowInteropHelper(this).EnsureHandle();
        if (fullPlacement != null) ApplyPlacement(fullPlacement, false);
        else
        {
            var work = NativeWindow.WorkArea(Handle);
            NativeWindow.Place(Handle, WindowGeometry.Clamp(new Rect(work.X + (work.Width - 1240 * Scale) / 2,
                work.Y + (work.Height - 760 * Scale) / 2, 1240 * Scale, 760 * Scale), work));
        }
        fullPlacement = WindowPlacement.Capture(Handle); SavedBounds = fullPlacement.Bounds;
        if (preferences.Collapsed)
        {
            SmallMode(DisplayMode.Compact);
            CompactContent.Visibility = Visibility.Visible; ResizeHandles.Visibility = Visibility.Visible;
            if (compactPlacement != null) ApplyPlacement(compactPlacement, true);
            else
            {
                MinWidth = 320; MinHeight = 240;
                NativeWindow.Place(Handle, WindowGeometry.Compact(NativeWindow.WorkArea(Handle), Scale));
            }
            compactPlacement = WindowPlacement.Capture(Handle);
        }
        if (fallback) DesktopMode.Fallback(); else DesktopMode.Apply(preferences.Pinned);
        LayerInitialized = true;
        RefreshLayerStatus();
        startupPrepared = true;
    }
    internal void FinishStartup()
    {
        if (Ready) return;
        Ready = true;
        TraceVisible("ContentRendered");
    }
    void ApplyPlacement(WindowPlacement placement, bool compact)
    {
        var screen = placement.Target(); var area = screen.WorkingArea;
        var work = new Rect(area.X, area.Y, area.Width, area.Height);
        // Move onto the target monitor first so GetDpiForWindow reflects that monitor.
        // A first estimate already uses the saved relative destination. A hidden startup
        // HWND may need a second DPI calculation, never a visible default-center frame.
        NativeWindow.Place(Handle, placement.Resolve(work, NativeWindow.GetDpiForWindow(Handle), screen.DeviceName == placement.Display, compact));
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
        foreach (Window owned in OwnedWindows) if (owned is PlannerDialogs.Sheet) owned.Topmost = IsPinned;
        ModeStatus.Text = Text(DesktopMode.Layer switch { WindowLayer.Desktop => "DesktopMode", WindowLayer.Topmost => "PinnedMode", _ => "FallbackMode" });
        if (Session.VisualChecks) ModeStatus.Text = Text("GuiCheckTitle");
        SettingsNotice.Text = DesktopMode.Layer == WindowLayer.Fallback ? Text("FallbackNotice") : Text("SettingsNotice");
        if (Session.Notice.Length > 0) SettingsNotice.Text += "\n" + Text(Session.Notice);
    }
    internal WindowPreferences SnapshotPreferences()
    {
        if (!changingView) CapturePlacement();
        var p = Appearance with { Collapsed = Mode == DisplayMode.Compact || Mode == DisplayMode.Tray && beforeTray == DisplayMode.Compact,
            Week = IsWeek, Pinned = PreferredPinned, EdgeAutoHide = EdgeToggle.IsChecked == true, BackgroundOpacity = OpacitySlider.Value,
            FullPlacement = fullPlacement, CompactPlacement = compactPlacement,
            DayHourHeight = DailyTimeline.HourHeight, WeekHourHeight = WeeklyTimeline.HourHeight,
            PlannerDate = Session.Planner?.SelectedDate ?? Session.Preferences.PlannerDate, PlannerMonth = Session.Planner?.Month ?? Session.Preferences.PlannerMonth,
            CalendarColorBackground = Session.Planner?.ColorBackground ?? Session.Preferences.CalendarColorBackground,
            CalendarTextMode = Session.Planner?.TextMode ?? Session.Preferences.CalendarTextMode,
            WeekStart = Session.Planner?.WeekStart ?? Session.Preferences.WeekStart, WeekPlanLayout = Session.Planner?.WeekLayout ?? Session.Preferences.WeekPlanLayout };
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
        if (msg == 0x0047 || msg == 0x000F) TraceVisible(msg == 0x0047 ? "WINDOWPOSCHANGED" : "PAINT");
        // Our eight WPF grips own resizing. Do not let the invisible native frame
        // consume pointer input before Thumb can capture it on layered windows.
        if (msg == 0x0084) { handled = true; return new IntPtr(1); }
        if ((uint)msg == DesktopWindowMode.TaskbarCreated || msg == 0x0218 && (w.ToInt64() == 7 || w.ToInt64() == 18))
            Dispatcher.BeginInvoke(() => Session.ShellSignal());
        // WPF owns WM_DPICHANGED, including its suggested bounds. Re-evaluate after WPF.
        if (msg == 0x02E0 || msg == 0x007E)
            Dispatcher.BeginInvoke(() =>
            {
                if (!Ready || changingView || dragging) return;
                if (Mode == DisplayMode.Compact)
                {
                    var work = NativeWindow.WorkArea(Handle);
                    MinWidth = Math.Min(320, work.Width / Scale); MinHeight = Math.Min(240, work.Height / Scale);
                    NativeWindow.Place(Handle, WindowGeometry.Clamp(NativeWindow.Bounds(Handle), work));
                }
                else if (Mode == DisplayMode.EdgeHandle) RestoreNormal(false);
                else if (Mode == DisplayMode.Normal) FitToScreen();
                Remember();
            });
        return IntPtr.Zero;
    }
    void TraceVisible(string stage)
    {
        // Bounded native visibility history, not off-screen rendering or a visibility claim.
        if (startupClock.ElapsedMilliseconds > 5000 || StartupVisible.Count >= 256) return;
        if ((NativeWindow.GetWindowLong(Handle, -16) & 0x10000000) != 0)
        {
            if (!startupPrepared) VisibleBeforePrepared = true;
            StartupVisible.Add(new(startupClock.ElapsedMilliseconds, NativeWindow.Bounds(Handle), Mode, stage));
        }
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
        if (Mode is not (DisplayMode.Normal or DisplayMode.Compact) || e.ChangedButton != MouseButton.Left) return;
        for (DependencyObject? item = e.OriginalSource as DependencyObject; item != null && item != sender;
            item = item is Visual ? VisualTreeHelper.GetParent(item) : LogicalTreeHelper.GetParent(item))
            if (item is ButtonBase or TextBoxBase) return;
        // A client-area pointer gesture avoids invoking the system caption/Snap loop.
        // Coordinates are absolute physical pixels, independent of the moving visual.
        movingTitle = (FrameworkElement)sender;
        BeginPointerGesture(null, NativeWindow.Cursor());
        movingTitle.CaptureMouse(); e.Handled = true;
    }
    void MoveTitle(object sender, MouseEventArgs e)
    {
        if (movingTitle == null || e.LeftButton != MouseButtonState.Pressed) return;
        UpdatePointerGesture(NativeWindow.Cursor()); e.Handled = true;
    }
    void EndTitle(object sender, MouseButtonEventArgs e)
    {
        if (movingTitle == null) return;
        var title = movingTitle; movingTitle = null;
        FinishPointerGesture(); title.ReleaseMouseCapture(); e.Handled = true;
    }
    internal void BeginPointerGesture(string? edge, Point pointer)
    {
        dragging = true; gestureEdge = edge; gestureStart = NativeWindow.Bounds(Handle); gesturePointer = pointer;
        gestureMinWidth = MinWidth * Scale; gestureMinHeight = MinHeight * Scale;
    }
    internal void UpdatePointerGesture(Point pointer)
    {
        if (!dragging) return;
        var delta = pointer - gesturePointer;
        var bounds = gestureEdge == null ? new Rect(gestureStart.Location + delta, gestureStart.Size)
            : WindowGeometry.Resize(gestureStart, delta, gestureEdge, gestureMinWidth, gestureMinHeight);
        NativeWindow.Place(Handle, bounds);
    }
    internal void FinishPointerGesture()
    {
        if (!dragging) return;
        bool moved = gestureEdge == null;
        movingTitle = null; dragging = false;
        var bounds = NativeWindow.Bounds(Handle); var work = NativeWindow.WorkArea(Handle);
        if (moved && Mode == DisplayMode.Normal && EdgeToggle.IsChecked == true)
        {
            var side = WindowGeometry.Edge(bounds, work, Scale);
            if (side != DockSide.None) bounds.X = side == DockSide.Left ? work.Left : work.Right - bounds.Width;
        }
        NativeWindow.Place(Handle, WindowGeometry.Clamp(bounds, work));
        outsideSince = DateTime.MaxValue; suppressEdgeUntil = DateTime.UtcNow.AddMilliseconds(350); Remember();
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
    internal void OpenSettings() { outsideSince = DateTime.MaxValue; RefreshLayerStatus(); if (!Session.Testing) try { StartupToggle.IsChecked = new StartupEntry().Enabled; } catch { SettingsNotice.Text = Text("StartupFailed"); } SettingsPanel.Visibility = Visibility.Visible; SettingsCloseButton.Focus(); }
    private void EdgeChanged(object sender, RoutedEventArgs e)
    {
        outsideSince = DateTime.MaxValue;
        if (Mode == DisplayMode.EdgeHandle) RestoreNormal();
        Remember();
    }
    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Shell != null) ApplyShell(e.NewValue);
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
        Shell.Visibility = mode == DisplayMode.EdgeHandle ? Visibility.Collapsed : Visibility.Visible;
        RestoreButton.ApplyTemplate();
        if (RestoreButton.Template.FindName("EdgeMark", RestoreButton) is Border mark)
            mark.HorizontalAlignment = dockSide == DockSide.Left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
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
    internal void RestoreNormal(bool activate = true)
    {
        bool fromToday = Mode == DisplayMode.Compact || Mode == DisplayMode.Tray && beforeTray == DisplayMode.Compact;
        bool needsRestore = Mode != DisplayMode.Normal;
        CapturePlacement(); changingView = true;
        Mode = DisplayMode.Normal;
        Shell.Visibility = Visibility.Visible;
        FullContent.Visibility = Visibility.Visible;
        ResizeHandles.Visibility = Visibility.Visible;
        RestoreButton.Visibility = Visibility.Collapsed;
        CompactContent.Visibility = Visibility.Collapsed;
        if (fromToday) { Appearance.FixedWeekOffset = 0; Session.Planner?.Select(DateOnly.FromDateTime(DateTime.Now), true); PlannerView?.InvalidateCalendar(); SetPage(false); }
        SettingsPanel.Visibility = Visibility.Collapsed;
        Shell.CornerRadius = new CornerRadius(18);
        if (needsRestore)
        {
            ApplyPlacement(fullPlacement ?? WindowPlacement.Legacy(SavedBounds), false);
        }
        Show(); FitToScreen();
        if (activate) { Activate(); (IsWeek ? WeekButton : MonthButton).Focus(); }
        outsideSince = DateTime.MaxValue;
        suppressEdgeUntil = DateTime.UtcNow.AddSeconds(1.2);
        changingView = false;
        Remember();
    }
    internal void CollapseToEdge(DockSide side)
    {
        if (Mode != DisplayMode.Normal || side == DockSide.None) return;
        SaveNormalBounds(); CapturePlacement(); dockSide = side;
        var work = NativeWindow.WorkArea(Handle);
        var bounds = WindowGeometry.Handle(SavedBounds, work, side, Scale);
        changingView = true;
        SmallMode(DisplayMode.EdgeHandle);
        NativeWindow.Place(Handle, bounds);
        changingView = false; Remember();
    }
    private void EvaluateEdge()
    {
        NativeWindow.GetCursorPos(out var p);
        EvaluateEdgeAt(DateTime.UtcNow, new Point(p.X, p.Y));
    }
    internal void EvaluateEdgeAt(DateTime now, Point pointer)
    {
        if (EdgeToggle.IsChecked != true || dragging || Mouse.Captured != null) return;
        if (Mode == DisplayMode.EdgeHandle)
        {
            if (NativeWindow.Bounds(Handle).Contains(pointer)) RestoreNormal(false);
            return;
        }
        if (Mode != DisplayMode.Normal || now < suppressEdgeUntil || SettingsPanel.Visibility == Visibility.Visible || PlannerDialogDepth > 0) return;
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
            thumb.DragStarted += (_, _) => BeginPointerGesture(edge, NativeWindow.Cursor());
            thumb.DragDelta += (_, e) =>
            {
                // Thumb deltas use a visual-relative origin which itself moves on N/W
                // resizing. Use the fixed start rectangle and screen pointer instead.
                UpdatePointerGesture(NativeWindow.Cursor());
            };
            thumb.DragCompleted += (_, _) => FinishPointerGesture();
            ResizeHandles.Children.Add(thumb);
        }
    }
}
