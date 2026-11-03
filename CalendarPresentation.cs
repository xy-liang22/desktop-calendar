using System;
using System.Linq;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static class CalendarPresentation
{
    static string T(string key) => MainWindow.Text(key);
    internal static string Label(CalendarEvent e) => T(e.Kind switch { "CN" => "CalendarCN", "CNWork" => "CalendarWork", "HK" => "CalendarHK", "Term" => "CalendarTerm", _ => "CalendarFestival" }) + ": " + (Localization.English ? e.En : e.Zh);
    internal static string Detail(CalendarKnowledge data, DateOnly date)
    {
        string lunar = CalendarKnowledge.Lunar(date); var lines = new[] { date.ToString("D", Localization.Culture), lunar.Length == 0 ? T("CalendarUnsupported") : lunar }
            .Concat(data.On(date).Concat(CalendarKnowledge.Festivals(date)).Select(Label));
        if (!data.Data.TermYears.Contains(date.Year)) lines = lines.Append(T("CalendarNoTerms"));
        if (!data.Data.MainlandYears.Contains(date.Year)) lines = lines.Append(T("CalendarNoCN"));
        if (!data.Data.HongKongYears.Contains(date.Year)) lines = lines.Append(T("CalendarNoHK"));
        return string.Join("\n", lines);
    }
    internal static string Status(CalendarKnowledge data) => string.Format(Localization.Culture, T("CalendarStatus"), string.Join(",", data.Data.TermYears), string.Join(",", data.Data.MainlandYears), string.Join(",", data.Data.HongKongYears), data.Data.UpdatedAt.ToLocalTime().ToString("g", Localization.Culture)) + "\n" + T("CalendarSources") + "\n" + T("CalendarAlmanac") + (data.Diagnostic.Length == 0 ? "" : "\n" + T("CalendarUpdateFailed"));
}
