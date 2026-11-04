using System;
using System.Linq;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static class CalendarPresentation
{
    static string T(string key) => MainWindow.Text(key);
    // Display-only abbreviation; the full lunar calculation and detail text stay intact.
    internal static string ShortLunar(DateOnly date)
    {
        string full = CalendarKnowledge.Lunar(date); int monthEnd = full.IndexOf('月'); if (monthEnd < 0) return "";
        string day = full[(monthEnd + 1)..]; return day == "初一" ? full[..(monthEnd + 1)].Replace("冬月", "十一月", StringComparison.Ordinal).Replace("腊月", "十二月", StringComparison.Ordinal) : day;
    }
    // On() synthesizes this one generic Sunday entry. Never filter named holidays by weekday.
    internal static bool OrdinarySunday(CalendarEvent e) => e.Kind == "HK" && e.Date.DayOfWeek == DayOfWeek.Sunday && e.Zh == "星期日（公众假期）" && e.En == "Sunday (general holiday)" && e.Source == CalendarKnowledge.HkEnglish;
    internal static CalendarEvent[] CompactEvents(CalendarKnowledge data, DateOnly date) => data.On(date).Concat(CalendarKnowledge.Festivals(date)).Where(e => !OrdinarySunday(e)).ToArray();
    internal static int Priority(CalendarEvent e) => e.Kind switch { "Term" => 0, "Festival" => 1, "HK" => 2, "CNWork" => 3, _ => 4 };
    internal static string EventColor(string kind) => kind switch { "HK" => "#BBA8DE", "CN" or "CNWork" => "#E0B17C", "Term" => "#A3D9C4", _ => "#E3BF89" };
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
