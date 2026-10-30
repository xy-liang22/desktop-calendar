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
        bool restartCheck = Array.IndexOf(e.Args, "--restart-check") >= 0;
        bool visualChecks = Array.IndexOf(e.Args, "--ui-check") >= 0;
        if (!testing && !restartCheck || visualChecks)
        {
            instance = new Mutex(true, visualChecks ? "Local\\DesktopCalendar-GuiCheck" : "Local\\DesktopCalendar", out bool created);
            if (!created) { MessageBox.Show(DesktopCalendar.MainWindow.Text("AlreadyRunning"), DesktopCalendar.MainWindow.Text("AppName")); Shutdown(); return; }
        }
        session = new DesktopSession(testing || restartCheck, visualChecks, restartCheck);
        SessionEnding += (_, _) => session?.Save();
        if (testing) session.Window.ContentRendered += async (_, _) => await PrototypeChecks.Run(session.Window);
        if (restartCheck) session.Window.ContentRendered += async (_, _) => await RevisionChecks.ObserveRestart(session.Window);
    }
    protected override void OnExit(ExitEventArgs e) { session?.Dispose(); instance?.Dispose(); base.OnExit(e); }
}
