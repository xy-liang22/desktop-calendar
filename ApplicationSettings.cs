using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static class ApplicationSettings
{
    static string T(string key) => MainWindow.Text(key);
    internal static void Appearance(MainWindow owner)
    {
        var before = owner.SnapshotPreferences(); var s = new PlannerDialogs.Sheet(T("AppearanceSetting"), 510);
        var hint = PlannerView.Text(T("AppearanceHint"), 11); hint.TextWrapping = TextWrapping.Wrap; s.Body.Children.Add(hint);
        var background = new ColorField(before.BackgroundColor, false); s.Field("BackgroundSetting", background);
        var accent = new ColorField(before.AccentColor, false); s.Field("AccentSetting", accent);
        var sat = new ColorField(before.SaturdayColor, false); var sun = new ColorField(before.SundayColor, false); s.Field("PlannerSat", sat); s.Field("PlannerSun", sun);
        var opacity = new Slider { Minimum = .65, Maximum = .98, Value = before.BackgroundOpacity }; s.Field("Opacity", opacity);
        var rows = new ComboBox { ItemsSource = new[] { T("MonthFull"), T("MonthFixed") }, SelectedIndex = before.FixedSixRows ? 1 : 0 }; s.Field("MonthModeSetting", rows);
        void Preview() { if (background.Value == null || accent.Value == null || sat.Value == null || sun.Value == null || !background.Valid || !accent.Valid || !sat.Valid || !sun.Valid) return; owner.ApplyAppearance(before with { BackgroundColor = background.Value!, AccentColor = accent.Value!, SaturdayColor = sat.Value!, SundayColor = sun.Value!, BackgroundOpacity = opacity.Value, FixedSixRows = rows.SelectedIndex == 1 }); }
        background.Changed += Preview; accent.Changed += Preview; sat.Changed += Preview; sun.Changed += Preview; rows.SelectionChanged += (_, _) => Preview(); opacity.ValueChanged += (_, _) => Preview();
        s.Save(() => { bool ok = background.Value != null && accent.Value != null && sat.Value != null && sun.Value != null && background.Valid && accent.Valid && sat.Valid && sun.Valid; if (!ok) s.Error.Text = T("PlannerColorInvalid"); return System.Threading.Tasks.Task.FromResult(ok); }, true);
        if (PlannerDialogs.Show(owner, s) != true) owner.ApplyAppearance(before);
    }
    internal static void Calendar(MainWindow owner)
    {
        var s = new PlannerDialogs.Sheet(T("CalendarUpdate"), 600); var status = PlannerView.Text(CalendarPresentation.Status(owner.Session.Calendar), 12); status.TextWrapping = TextWrapping.Wrap; s.Body.Children.Add(status);
        var update = PlannerView.Button(T("CalendarUpdate"), () => { });
        update.Click += async (_, _) =>
        {
            if (!PlannerDialogs.Confirm(owner, T("CalendarUpdate"), T("CalendarUpdateConfirm"))) return;
            update.IsEnabled = false; s.Error.Text = T("CalendarUpdating");
            try { await owner.Session.Calendar.UpdateAsync(Path.Combine(owner.Session.DataDirectory, "calendar-knowledge.json")); owner.PlannerView?.InvalidateCalendar(); status.Text = CalendarPresentation.Status(owner.Session.Calendar); s.Error.Text = T("CalendarUpdated"); }
            catch (Exception e) when (e is IOException or System.Net.Http.HttpRequestException or System.Threading.Tasks.TaskCanceledException or System.Text.Json.JsonException or InvalidDataException or UnauthorizedAccessException) { s.Error.Text = T("CalendarUpdateFailed") + " (" + e.GetType().Name + ")"; }
            finally { update.IsEnabled = true; }
        }; s.Body.Children.Add(update);
        s.Body.Children.Add(PlannerView.Button(T("CalendarImport"), () =>
        {
            var picker = new Microsoft.Win32.OpenFileDialog { Filter = "JSON|*.json" }; if (picker.ShowDialog(s) != true) return;
            try { var data = CalendarKnowledge.Decode(File.ReadAllText(picker.FileName)); if (!PlannerDialogs.Confirm(owner, T("CalendarImport"), T("CalendarImportConfirm"))) return; CalendarKnowledge.Install(Path.Combine(owner.Session.DataDirectory, "calendar-knowledge.json"), data); owner.Session.ReloadCalendar(); owner.PlannerView?.InvalidateCalendar(); status.Text = CalendarPresentation.Status(owner.Session.Calendar); s.Error.Text = T("CalendarUpdated"); }
            catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException) { s.Error.Text = T("CalendarUpdateFailed") + " (" + e.GetType().Name + ")"; }
        }));
        s.Actions.Children.Add(PlannerView.Button(T("CloseSettings"), s.Close)); PlannerDialogs.Show(owner, s);
    }
}
