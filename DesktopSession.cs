using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DesktopCalendar;

// Tray/recovery outlive the component HWND; Explorer never owns this manager's window.
internal sealed class DesktopSession : IDisposable
{
    readonly WindowPreferencesStore store;
    readonly Forms.NotifyIcon tray;
    readonly System.Drawing.Icon trayIcon;
    readonly Forms.ToolStripMenuItem pin;
    readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    bool closing, recovered, disposed;
    internal MainWindow Window { get; private set; } = null!;
    internal WindowPreferences Preferences { get; private set; }
    internal bool Testing { get; }
    internal bool VisualChecks { get; }
    internal bool TrayVisible => tray.Visible;
    internal string Notice => store.Notice;
    internal PlannerController? Planner { get; }
    internal DesktopSession(bool testing, bool visualChecks = false, bool restartCheck = false, bool lifecycleCheck = false, bool plannerCheck = false)
    {
        Testing = testing;
        VisualChecks = visualChecks;
        string directory = plannerCheck ? StageThreeChecks.DirectoryPath : lifecycleCheck ? LifecycleChecks.DirectoryPath : restartCheck ? RevisionChecks.RestartDirectory
            : visualChecks ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "artifacts", "ui-check-preferences"))
            : testing ? Path.Combine(Path.GetTempPath(), "DesktopCalendar-Checks-" + Guid.NewGuid().ToString("N"))
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCalendar");
        store = new WindowPreferencesStore(Path.Combine(directory, "window-preferences.json"));
        Preferences = store.Load();
        if (!testing || plannerCheck) Planner = new PlannerController(Path.Combine(directory, "planner-data.json"), Preferences);
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(MainWindow.Text("Locate"), null, (_, _) => Dispatch(() => Locate()));
        pin = new Forms.ToolStripMenuItem(MainWindow.Text("Topmost"));
        pin.Click += (_, _) => Dispatch(() => Window.SetPinned(!Window.IsPinned)); menu.Items.Add(pin);
        menu.Items.Add(MainWindow.Text("Settings"), null, (_, _) => Dispatch(() => { Locate(); Window.OpenSettings(); }));
        menu.Items.Add(MainWindow.Text("SafeLocate"), null, (_, _) => Dispatch(() => Locate(true)));
        menu.Items.Add(MainWindow.Text("Exit"), null, (_, _) => Dispatch(() => Exit(0)));
        trayIcon = AppBrand.TrayIcon();
        tray = new Forms.NotifyIcon { Text = MainWindow.Text("AppName"), Icon = trayIcon, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Dispatch(() => Locate());
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Save(); };
        CreateWindow(false);
        if (Planner != null) { Planner.PreferencesChanged += Changed; _ = Planner.InitializeAsync(); }
    }
    static void Dispatch(Action action) => Application.Current.Dispatcher.Invoke(action);
    void CreateWindow(bool fallback)
    {
        var settings = Preferences;
        var window = new MainWindow(this, settings);
        if (VisualChecks) window.Title = MainWindow.Text("GuiCheckTitle");
        Window = window; Application.Current.MainWindow = window;
        window.ContentRendered += (_, _) =>
        {
            window.FinishStartup(); Changed();
        };
        window.Closed += (_, _) =>
        {
            if (closing || window != Window) return;
            // A single recovery after owned HWND loss. No Explorer operation or reattach loop.
            if (!recovered) { recovered = true; Application.Current.Dispatcher.BeginInvoke(() => CreateWindow(true)); }
        };
        window.PrepareStartup(settings, fallback);
        window.Show();
    }
    internal void Changed()
    {
        if (closing || !Window.Ready || !Window.LayerInitialized) return;
        Preferences = Window.SnapshotPreferences();
        pin.Checked = Window.IsPinned;
        saveTimer.Stop(); saveTimer.Start();
    }
    internal void Save()
    {
        if (!Window.Ready) return;
        if (DesktopWindowMode.IsWindow(Window.Handle)) Preferences = Window.SnapshotPreferences();
        if (!store.Save(Preferences)) Window.RefreshLayerStatus();
    }
    internal void Locate(bool safe = false)
    {
        if (!DesktopWindowMode.IsWindow(Window.Handle)) { recovered = false; CreateWindow(true); return; }
        Window.ShowComponent();
        if (safe) Window.DesktopMode.Fallback();
        else Window.DesktopMode.Apply(Window.PreferredPinned);
        Window.RefreshLayerStatus(); Window.Activate();
        Changed();
    }
    internal void ShellSignal()
    {
        if (closing) return;
        Window.DesktopMode.CheckOwner();
        Window.RefreshLayerStatus();
        // NotifyIcon also handles TaskbarCreated itself. Refresh on this shell event only.
        tray.Visible = false; tray.Visible = true;
    }
    internal void TestTrayShow() => tray.ContextMenuStrip!.Items[0].PerformClick();
    internal void TestTrayPin() => pin.PerformClick();
    internal void TestTraySettings() => tray.ContextMenuStrip!.Items[2].PerformClick();
    internal void TestWindowLoss() { Save(); Window.CloseForRecovery(); }
    internal async void Exit(int code)
    {
        if (closing) return;
        if (Planner != null && !Testing)
        {
            // A tray exit follows the editor's save-on-close contract, including failures.
            var sheets = Application.Current.Windows.OfType<PlannerDialogs.Sheet>().Reverse().ToArray();
            foreach (var sheet in sheets)
            {
                if (!sheet.AutoSaveClose || !await sheet.SaveAndCloseAsync(true)) { sheet.Activate(); return; }
            }
            if (!await Planner.CommitInlineAsync()) { Window.Activate(); return; }
        }
        if (Planner != null && !Testing && Planner.QuickDraft.Length > 0 && !PlannerDialogs.Confirm(Window, MainWindow.Text("Exit"), MainWindow.Text("PlannerExitDraft"))) return;
        closing = true; if (Planner != null) await Planner.StopAsync();
        Save(); Dispose(); Window.CloseForRecovery(); Application.Current.Shutdown(code);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Planner?.Dispose(); saveTimer.Stop(); tray.ContextMenuStrip?.Dispose(); tray.Dispose(); trayIcon.Dispose();
    }
}
