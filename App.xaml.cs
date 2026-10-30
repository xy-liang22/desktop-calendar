using System;
using System.Threading;
using System.Windows;

namespace DesktopCalendar;
public partial class App : Application
{
    DesktopSession? session;
    Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool testing = Array.IndexOf(e.Args, "--self-test") >= 0;
        if (!testing)
        {
            instance = new Mutex(true, "Local\\DesktopCalendar", out bool created);
            if (!created) { MessageBox.Show(DesktopCalendar.MainWindow.Text("AlreadyRunning"), DesktopCalendar.MainWindow.Text("AppName")); Shutdown(); return; }
        }
        session = new DesktopSession(testing);
        if (testing) session.Window.ContentRendered += async (_, _) => await PrototypeChecks.Run(session.Window);
    }
    protected override void OnExit(ExitEventArgs e) { session?.Dispose(); instance?.Dispose(); base.OnExit(e); }
}
