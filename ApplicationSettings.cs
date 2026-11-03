using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static class ApplicationSettings
{
    static string T(string key) => MainWindow.Text(key);
    internal static void ChooseMonth(MainWindow owner)
    {
        var c = owner.Session.Planner; if (c == null) return; var sheet = new PlannerDialogs.Sheet(T("CalendarChooseMonth"));
        var year = new TextBox { Text = c.Month.Year.ToString(System.Globalization.CultureInfo.InvariantCulture) }; var month = new ComboBox { ItemsSource = Localization.Culture.DateTimeFormat.MonthNames.Take(12).ToArray(), SelectedIndex = c.Month.Month - 1 }; sheet.Field("CalendarYear", year); sheet.Field("CalendarMonth", month);
        sheet.Save(() => { if (!int.TryParse(year.Text, out int y) || y < 1901 || y > 2100 || month.SelectedIndex < 0) { sheet.Error.Text = T("PlannerDateRequired"); return System.Threading.Tasks.Task.FromResult(false); } owner.ApplyAppearance(owner.Appearance with { FixedWeekOffset = 0 }); c.Select(new(y, month.SelectedIndex + 1, 1), true); return System.Threading.Tasks.Task.FromResult(true); }); PlannerDialogs.Show(owner, sheet);
    }
    internal static void ValidateBackupFile(string name, byte[] bytes)
    {
        if (name != "window-preferences.json") return;
        var p = System.Text.Json.JsonSerializer.Deserialize<WindowPreferences>(bytes);
        if (p == null || p.SchemaVersion is not (1 or 2) || !p.Valid()) throw new InvalidDataException("InvalidWindowPreferences");
    }
    internal static void Backup(MainWindow owner)
    {
        var c = owner.Session.Planner;
        if (c == null || c.Busy || c.QuickDraft.Length > 0 || c.Inline != null || Application.Current.Windows.OfType<PlannerDialogs.Sheet>().Any(w => w.IsVisible)) { owner.SettingsNotice.Text = T("BackupBusy"); return; }
        var s = new PlannerDialogs.Sheet(T("BackupData"), 570); bool busy = false; s.Closing += (_, e) => { if (busy) e.Cancel = true; };
        var explanation = PlannerView.Text(T("BackupScope"), 12); explanation.TextWrapping = TextWrapping.Wrap; s.Body.Children.Add(explanation);
        s.Body.Children.Add(PlannerView.Button(T("BackupData"), async () =>
        {
            var picker = new Microsoft.Win32.SaveFileDialog { Filter = T("BackupData") + "|*.zip", DefaultExt = ".zip", FileName = "DesktopCalendar-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip", OverwritePrompt = true };
            if (picker.ShowDialog(s) != true) return;
            try
            {
                owner.Session.Save(); busy = true; s.IsEnabled = false;
                using (owner.Session.SuspendPreferences()) await c.MaintenanceAsync(() => new PlannerBackup(c.DataDirectory, ValidateBackupFile).Create(picker.FileName));
                s.Error.Text = T("BackupSaved");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or PlannerException or InvalidOperationException) { s.Error.Text = T("BackupFailed") + " (" + e.GetType().Name + ")"; }
            finally { busy = false; s.IsEnabled = true; }
        }));
        s.Body.Children.Add(PlannerView.Button(T("RestoreData"), async () =>
        {
            var picker = new Microsoft.Win32.OpenFileDialog { Filter = T("RestoreData") + "|*.zip" }; if (picker.ShowDialog(s) != true) return;
            try
            {
                busy = true; s.IsEnabled = false; var service = new PlannerBackup(c.DataDirectory, ValidateBackupFile); var info = await System.Threading.Tasks.Task.Run(() => service.Inspect(picker.FileName));
                s.IsEnabled = true; if (!PlannerDialogs.Confirm(owner, T("RestoreData"), string.Format(Localization.Culture, T("RestoreConfirm"), info.Manifest.Files.Length, info.Manifest.CreatedAt.ToLocalTime().ToString("g", Localization.Culture)) + "\n" + string.Join("\n", info.Manifest.Files.Take(12).Select(f => f.Path)) + (info.Manifest.Files.Length > 12 ? "\n…" : ""))) return;
                s.IsEnabled = false; owner.Session.Save();
                using (owner.Session.SuspendPreferences())
                {
                    string point = await c.MaintenanceAsync(() => service.Restore(picker.FileName, info.Fingerprint, true));
                    busy = false; s.IsEnabled = true; s.Close(); await owner.Session.ReloadRestoredAsync(); owner.Session.Window.OpenSettings(); owner.Session.Window.SettingsNotice.Text = string.Format(Localization.Culture, T("RestoreDone"), point);
                }
                owner.Session.Changed();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Text.Json.JsonException or PlannerException or InvalidOperationException) { s.Error.Text = T("BackupFailed") + " (" + e.GetType().Name + ")"; }
            finally { busy = false; s.IsEnabled = true; }
        }));
        s.Body.Children.Add(PlannerView.Button(T("DataFolder"), () => { Directory.CreateDirectory(c.DataDirectory); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(c.DataDirectory) { UseShellExecute = true }); }));
        s.Actions.Children.Add(PlannerView.Button(T("CloseSettings"), s.Close)); PlannerDialogs.Show(owner, s);
    }
    internal static void Appearance(MainWindow owner)
    {
        var before = owner.SnapshotPreferences(); var s = new PlannerDialogs.Sheet(T("AppearanceSetting"), 510);
        var hint = PlannerView.Text(T("AppearanceHint"), 11); hint.TextWrapping = TextWrapping.Wrap; s.Body.Children.Add(hint);
        var background = new ColorField(before.BackgroundColor, false); s.Field("BackgroundSetting", background);
        var accent = new ColorField(before.AccentColor, false); s.Field("AccentSetting", accent);
        var sat = new ColorField(before.SaturdayColor, false); var sun = new ColorField(before.SundayColor, false); s.Field("PlannerSat", sat); s.Field("PlannerSun", sun);
        var opacity = new Slider { Minimum = .65, Maximum = .98, Value = before.BackgroundOpacity }; s.Field("Opacity", opacity);
        var rows = new ComboBox { ItemsSource = new[] { T("MonthFull"), T("MonthFixed") }, SelectedIndex = before.FixedSixRows ? 1 : 0 }; s.Field("MonthModeSetting", rows);
        bool Good(ColorField field) => field.Valid && field.Value?.Length == 7;
        void Preview() { if (!Good(background) || !Good(accent) || !Good(sat) || !Good(sun)) return; owner.ApplyAppearance(before with { BackgroundColor = background.Value!, AccentColor = accent.Value!, SaturdayColor = sat.Value!, SundayColor = sun.Value!, BackgroundOpacity = opacity.Value, FixedSixRows = rows.SelectedIndex == 1 }); }
        background.Changed += Preview; accent.Changed += Preview; sat.Changed += Preview; sun.Changed += Preview; rows.SelectionChanged += (_, _) => Preview(); opacity.ValueChanged += (_, _) => Preview();
        s.Save(() => { bool ok = Good(background) && Good(accent) && Good(sat) && Good(sun); if (!ok) s.Error.Text = T("PlannerColorInvalid"); return System.Threading.Tasks.Task.FromResult(ok); }, true);
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
