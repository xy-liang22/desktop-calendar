using System;
using System.Threading;
using System.Windows;

namespace DesktopCalendar;
public partial class App : Application
{
    DesktopSession? session;
    Mutex? instance;
    bool ownsInstance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        bool testing = Array.IndexOf(e.Args, "--self-test") >= 0;
        bool plannerCheck = Array.IndexOf(e.Args, "--planner-check") >= 0;
        bool restartCheck = Array.IndexOf(e.Args, "--restart-check") >= 0;
        bool visualChecks = Array.IndexOf(e.Args, "--ui-check") >= 0;
        string? lifecycle = LifecycleChecks.Phase(e.Args);
        if (!testing && !restartCheck && !plannerCheck || visualChecks)
        {
            instance = new Mutex(false, lifecycle != null ? "Local\\DesktopCalendar-LifecycleCheck" : visualChecks ? "Local\\DesktopCalendar-GuiCheck" : "Local\\DesktopCalendar");
            try { ownsInstance = instance.WaitOne(0); }
            catch (AbandonedMutexException) { ownsInstance = true; }
            if (!ownsInstance)
            {
                if (lifecycle != null) { LifecycleChecks.Duplicate(); Shutdown(23); }
                else { MessageBox.Show(DesktopCalendar.MainWindow.Text("AlreadyRunning"), DesktopCalendar.MainWindow.Text("AppName")); Shutdown(); }
                return;
            }
        }
        session = new DesktopSession(testing || restartCheck || lifecycle != null || plannerCheck, visualChecks, restartCheck, lifecycle != null, plannerCheck);
        SessionEnding += (_, _) => session?.Save();
        if (testing) session.Window.ContentRendered += async (_, _) => await PrototypeChecks.Run(session.Window);
        if (restartCheck) session.Window.ContentRendered += async (_, _) => await RevisionChecks.ObserveRestart(session.Window);
        if (lifecycle != null) session.Window.ContentRendered += async (_, _) => await LifecycleChecks.Observe(session.Window, lifecycle);
        if (plannerCheck) session.Window.ContentRendered += async (_, _) => await StageThreeChecks.Run(session.Window);
    }
    protected override void OnExit(ExitEventArgs e) { session?.Dispose(); if (ownsInstance) instance?.ReleaseMutex(); instance?.Dispose(); base.OnExit(e); }
}
