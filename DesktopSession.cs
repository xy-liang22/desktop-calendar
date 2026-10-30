using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DesktopCalendar;

// Tray/recovery outlive the component HWND; Explorer never owns this manager's window.
internal sealed class DesktopSession : IDisposable
{
    readonly WindowPreferencesStore store;
    readonly Forms.NotifyIcon tray;
    readonly Forms.ToolStripMenuItem pin;
    readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    bool closing, recovered, disposed;
    internal MainWindow Window { get; private set; } = null!;
    internal WindowPreferences Preferences { get; private set; }
    internal bool Testing { get; }
    internal bool VisualChecks { get; }
    internal bool TrayVisible => tray.Visible;
    internal string Notice => store.Notice;
    internal DesktopSession(bool testing, bool visualChecks = false, bool restartCheck = false)
    {
        Testing = testing;
        VisualChecks = visualChecks;
        string directory = restartCheck ? RevisionChecks.RestartDirectory
            : visualChecks ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "artifacts", "ui-check-preferences"))
            : testing ? Path.Combine(Path.GetTempPath(), "DesktopCalendar-Checks-" + Guid.NewGuid().ToString("N"))
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCalendar");
        store = new WindowPreferencesStore(Path.Combine(directory, "window-preferences.json"));
        Preferences = store.Load();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(MainWindow.Text("Locate"), null, (_, _) => Dispatch(() => Locate()));
        pin = new Forms.ToolStripMenuItem(MainWindow.Text("Topmost"));
        pin.Click += (_, _) => Dispatch(() => Window.SetPinned(!Window.IsPinned)); menu.Items.Add(pin);
        menu.Items.Add(MainWindow.Text("Settings"), null, (_, _) => Dispatch(() => { Locate(); Window.OpenSettings(); }));
        menu.Items.Add(MainWindow.Text("SafeLocate"), null, (_, _) => Dispatch(() => Locate(true)));
        menu.Items.Add(MainWindow.Text("Exit"), null, (_, _) => Dispatch(() => Exit(0)));
        tray = new Forms.NotifyIcon { Text = MainWindow.Text("AppName"), Icon = System.Drawing.SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Dispatch(() => Locate());
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Save(); };
        CreateWindow(false);
    }
    static void Dispatch(Action action) => Application.Current.Dispatcher.Invoke(action);
    void CreateWindow(bool fallback)
    {
        var settings = Preferences;
        var window = new MainWindow(this, settings);
        if (VisualChecks) window.Title = MainWindow.Text("GuiCheckTitle");
        Window = window; Application.Current.MainWindow = window;
        window.ContentRendered += async (_, _) =>
        {
            if (window.DesktopMode == null || window.LayerInitialized) return;
            window.LayerInitialized = true;
            if (fallback) window.DesktopMode.Fallback();
            else window.DesktopMode.Apply(settings.Pinned);
            await window.RestoreStartup(settings);
            window.RefreshLayerStatus(); Changed();
        };
        window.Closed += (_, _) =>
        {
            if (closing || window != Window) return;
            // A single recovery after owned HWND loss. No Explorer operation or reattach loop.
            if (!recovered) { recovered = true; Application.Current.Dispatcher.BeginInvoke(() => CreateWindow(true)); }
        };
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
    internal void Exit(int code)
    {
        if (closing) return;
        Save(); closing = true; Dispose(); Window.CloseForRecovery(); Application.Current.Shutdown(code);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; saveTimer.Stop(); tray.ContextMenuStrip?.Dispose(); tray.Dispose();
    }
}
