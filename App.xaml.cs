using System;
using System.Windows;

namespace DesktopCalendar;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
        if (Array.IndexOf(e.Args, "--self-test") >= 0)
            window.ContentRendered += async (_, _) => await PrototypeChecks.Run(window);
    }
}
