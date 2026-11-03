using System;
using System.Globalization;
using System.Windows;

namespace DesktopCalendar;
internal static class Localization
{
    internal static bool English { get; private set; }
    internal static CultureInfo Culture => CultureInfo.GetCultureInfo(English ? "en-US" : "zh-CN");
    static ResourceDictionary? english;
    internal static void Apply(string language)
    {
        English = language == "en"; var dictionaries = Application.Current.Resources.MergedDictionaries;
        if (english != null) { dictionaries.Remove(english); english = null; }
        if (English) { english = new ResourceDictionary { Source = new Uri("Strings.en.xaml", UriKind.Relative) }; dictionaries.Add(english); }
        CultureInfo.CurrentCulture = Culture; CultureInfo.CurrentUICulture = Culture;
        CultureInfo.DefaultThreadCurrentCulture = Culture; CultureInfo.DefaultThreadCurrentUICulture = Culture;
    }
    internal static string Category(DesktopCalendar.Core.Category? category)
    {
        if (category == null) return MainWindow.Text("PlannerUnclassified");
        string[] keys = { "work", "chores", "personal", "family", "health", "daily", "life" }, zh = { "工作", "家务", "个人", "家庭", "健康", "日常", "生活" }, en = { "Work", "Chores", "Personal", "Family", "Health", "Daily", "Life" };
        int i = Array.IndexOf(keys, category.BuiltInKey); return i >= 0 && (category.Name == zh[i] || category.Name == en[i]) ? English ? en[i] : zh[i] : category.Name;
    }
    internal static string Duration(double hours) => hours.ToString("0.##", Culture) + " " + MainWindow.Text("WeekHours");
}
