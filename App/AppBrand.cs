using System;
using System.Drawing;
using System.Windows;

namespace DesktopCalendar;

internal static class AppBrand
{
    internal const string IconResource = "/assets/branding/DesktopCalendar.ico";
    internal static Icon TrayIcon()
    {
        using var stream = Application.GetResourceStream(new Uri(IconResource, UriKind.Relative))!.Stream;
        using var icon = new Icon(stream, 32, 32);
        return (Icon)icon.Clone(); // Detach lifetime from the resource stream.
    }
}
