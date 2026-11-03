using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static partial class StageThreeChecks
{
    static async System.Threading.Tasks.Task RunP7Checks(MainWindow window)
    {
        window.RestoreNormal(); var c = window.Session.Planner!; var document = Digest(c.State!); var firstDay = c.WeekStart;
        var keys = Application.Current.Resources.MergedDictionaries.SelectMany(d => d.Keys.Cast<object>().OfType<string>()).Distinct().Where(k => Application.Current.TryFindResource(k) is string).ToArray();
        var english = new ResourceDictionary { Source = new Uri("Strings.en.xaml", UriKind.Relative) };
        Check("English resource covers every Chinese string key", keys.All(k => english.Contains(k)));
        window.LanguageChoice.SelectedIndex = 1; await Idle();
        Check("instant language switch rebuilds real views without data mutation", Localization.English && window.MonthButton.Content.ToString() == "Calendar & today" && document == Digest(c.State!) && c.WeekStart == firstDay);
        Check("English tray commands refresh", window.Session.TrayMenuText(0) == "Show / locate component");
        c.Select(new(2026, 6, 19), true); window.UpdateLayout(); Image(window.Surface, "p7-english-calendar.png");
        Check("English month and built-in display names use explicit culture without rewriting categories", Children<TextBlock>(window.PlannerView!).Any(t => t.Text == "June 2026") && Localization.Category(c.State!.Categories.Single(x => x.BuiltInKey == "work")) == "Work" && c.State.Categories.Single(x => x.BuiltInKey == "work").Name == "工作");
        Check("date details keep lunar but translate regions and holiday names", CalendarPresentation.Detail(window.Session.Calendar, c.SelectedDate).Contains("Hong Kong general holiday") && CalendarPresentation.Detail(window.Session.Calendar, c.SelectedDate).Contains("五月初五"));
        await DriveAsync(() => PlannerDialogs.Edit(window, c, null), async s => { Check("English editor and validation translated", s.Title == "New task" && Field(s, "PlannerTitle") != null); Image(s, "p7-english-editor.png"); Click(s.Actions.Children.OfType<Button>().First()); await Idle(); });
        var before = window.SnapshotPreferences();
        await DriveAsync(() => ApplicationSettings.Appearance(window), async s => { var fields = Children<ColorField>(s).ToArray(); fields[0].Value = "#FFFFFF"; var sliders = Children<Slider>(s).ToArray(); sliders.Last().Value = .65; var rows = Children<ComboBox>(s).Last(); rows.SelectedIndex = 1; await Idle();
            var surface = ((System.Windows.Media.SolidColorBrush)window.Shell.Background).Color; Check("appearance preview constrains bright background and preserves text opacity", surface.R < 100 && window.Opacity == 1 && window.Appearance.FixedSixRows); Image(s, "p7-appearance.png"); Click(s.Actions.Children.OfType<Button>().First()); await Idle(); });
        Check("appearance cancel restores original colors rows opacity", window.Appearance.BackgroundColor == before.BackgroundColor && window.Appearance.FixedSixRows == before.FixedSixRows && Math.Abs(window.OpacitySlider.Value - before.BackgroundOpacity) < .001);
        window.ApplyAppearance(window.SnapshotPreferences() with { FixedSixRows = true }); c.Select(new(2026, 2, 12), true); await Idle();
        Check("fixed six row calendar contains 42 date buttons", Children<Button>(window.PlannerView!).Count(b => b.Tag is DateOnly) == 42);
        window.LanguageChoice.SelectedIndex = 0; await Idle(); Check("Chinese switch retains business bytes", !Localization.English && window.MonthButton.Content.ToString() == "月历与今日" && Digest(c.State!) == document);
        window.ApplyAppearance(before with { UiLanguage = "zh-CN" }); c.Select(c.Today, true); window.EnterCompact(); window.Session.Save();
        File.WriteAllText(Path.Combine(DirectoryPath, "expected-hash.txt"), Digest(c.State!));
    }
}
