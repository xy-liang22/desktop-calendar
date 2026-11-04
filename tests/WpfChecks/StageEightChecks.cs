using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static partial class StageThreeChecks
{
    static async Task<MainWindow> RunP8Checks(MainWindow window)
    {
        void Step(string text) => File.AppendAllText(Path.Combine(DirectoryPath, "p8-progress.log"), text + "\n");
        Step("begin");
        var session = window.Session; var c = session.Planner!; window.RestoreNormal();
        Step("normal");
        window.LanguageChoice.SelectedIndex = 1; await Idle(); window.ApplyAppearance(window.SnapshotPreferences() with { BackgroundColor = "#20323A", AccentColor = "#B394E8", FixedSixRows = true });
        Step("language appearance");
        window.EnterCompact(); session.Save(); string document = Digest(c.State!); string zip = Path.Combine(DirectoryPath, "p8-complete.zip");
        Step("preferences saved");
        var backup = new PlannerBackup(session.DataDirectory, ApplicationSettings.ValidateBackupFile); var info = await c.MaintenanceAsync(() => backup.Create(zip));
        Step("backup created");
        Check("GUI service backup includes real registered Markdown images and preferences", info.Manifest.Files.Any(f => f.Path.StartsWith("assets/images/")) && info.Manifest.Files.Any(f => f.Path == "window-preferences.json"));
        window.RestoreNormal(); var added = c.NewTodo("P8 restore must remove later item"); await c.ExecuteAsync(new SaveTodo(added)); session.Save(); string? point;
        using (session.SuspendPreferences()) { point = await c.MaintenanceAsync(() => backup.Restore(zip, info.Fingerprint, true)); await session.ReloadRestoredAsync(); }
        Step("restore and reload");
        window = session.Window; for (int i = 0; i < 80 && !window.Ready; i++) await Task.Delay(25); await Idle();
        Check("restored actual component replaces stale window and reloads original workspace", window.Ready && document == Digest(c.State!) && !c.State!.Todos.Any(t => t.Id == added.Id));
        Check("restored language appearance compact and week settings are applied", Localization.English && window.Mode == DisplayMode.Compact && window.Appearance.BackgroundColor == "#20323A" && window.Appearance.FixedSixRows && c.WeekStart == DayOfWeek.Sunday);
        var images = new LocalImages(c.DataDirectory, () => c.State); Check("restored Markdown images resolve from original relative references", c.State!.Assets.All(a => images.Read(a.RelativePath) != null));
        Check("restore keeps durable original data recovery point", Directory.Exists(point) && File.Exists(Path.Combine(point, "planner-data.json")));
        window.RestoreNormal(); window.OpenSettings(); Image(window.Surface, "p8-restored-english-settings.png"); window.SettingsPanel.Visibility = System.Windows.Visibility.Collapsed;
        await DriveAsync(() => ApplicationSettings.Backup(window), async s => { Check("backup UI has translated explicit restore action", Children<Button>(s).Any(b => b.Content?.ToString() == "Restore from ZIP")); Image(s, "p8-backup-panel.png"); Click(s.Actions.Children.OfType<Button>().First()); await Idle(); });
        string fixture = Path.Combine(DirectoryPath, "startup-fixture"), install = Path.Combine(fixture, "Programs"), startup = Path.Combine(fixture, "Startup"), version = Path.Combine(install, "versions", "v0123456789abcdef"); Directory.CreateDirectory(version);
        File.WriteAllText(Path.Combine(install, ".desktop-calendar-owned"), "desktop-calendar"); File.WriteAllText(Path.Combine(install, "installation.json"), "{\"SchemaVersion\":1,\"AppId\":\"desktop-calendar\",\"Version\":\"0.8.0\",\"Current\":\"versions\\\\v0123456789abcdef\"}"); File.WriteAllText(Path.Combine(version, "DesktopCalendar.exe"), "shortcut target fixture, never executed");
        var entry = new StartupEntry(install, startup); Check("startup opt-in default absent", !entry.Enabled); entry.Set(true); entry.Set(true); Check("isolated startup toggle creates one owned stable shortcut", entry.Enabled && Directory.GetFiles(startup, "*.lnk").Length == 1 && !entry.InstalledTarget().Contains("bin\\")); entry.Set(false); Check("startup disable removes only owned entry", !File.Exists(Path.Combine(startup, "Desktop Calendar.lnk")));
        string statePath = Path.Combine(install, "installation.json"), stateText = File.ReadAllText(statePath); File.WriteAllText(statePath, stateText.Replace("0.8.0", "0.7.0")); try { entry.Set(true); Check("old installed version cannot replace P8 on Windows login", false); } catch (InvalidOperationException) { Check("old installed version cannot replace P8 on Windows login", !entry.Enabled); } finally { File.WriteAllText(statePath, stateText); }
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!; dynamic foreign = shell.CreateShortcut(Path.Combine(startup, "Desktop Calendar.lnk")); foreign.TargetPath = Path.Combine(version, "DesktopCalendar.exe"); foreign.Description = "Unrelated fixture"; foreign.Save();
        try { entry.Set(true); Check("foreign startup entry protected", false); } catch (IOException) { Check("foreign startup entry protected", (string)shell.CreateShortcut(Path.Combine(startup, "Desktop Calendar.lnk")).Description == "Unrelated fixture"); }
        try { new StartupEntry(Path.Combine(fixture, "missing-install"), Path.Combine(fixture, "other-startup")).Set(true); Check("uninstalled temporary build cannot become startup target", false); } catch (InvalidOperationException) { Check("uninstalled temporary build cannot become startup target", true); }
        Check("startup tests never change actual system entry", !File.Exists(Path.Combine(session.DataDirectory, "Startup", "Desktop Calendar.lnk")));
        string journal = Path.Combine(c.DataDirectory, "restore-journal.json"), heldDocument = Digest(c.State!); File.WriteAllText(journal, "test-only blocked recovery marker");
        try { Check("pending restore blocks business writes instead of exposing partial graph", !c.Writable && !await c.ExecuteAsync(new SaveTodo(c.NewTodo("must not save"))) && Digest(c.State!) == heldDocument); await c.ReloadAsync(); Check("reload cannot bypass pending restore guard", !c.Writable && c.Diagnostic == "PendingRestoreRecoveryBlocked"); }
        finally { File.Delete(journal); }
        await c.ReloadAsync(); var blocked = new PlannerController(c.FilePath, window.SnapshotPreferences(), maintenanceBlocked: true); await blocked.InitializeAsync(); Check("startup recovery failure blocks initialization without empty overwrite", blocked.State == null && !blocked.Writable && blocked.Diagnostic == "PendingRestoreRecoveryBlocked"); blocked.Dispose();
        bool maintenanceFailed = false; try { await c.MaintenanceAsync<int>(() => throw new IOException("expected isolated backup failure")); } catch (IOException) { maintenanceFailed = true; } await c.Pending; Check("failed maintenance stays reported but lifecycle wait permits normal exit", maintenanceFailed && c.Writable && !c.Pending.IsFaulted);
        window.EnterCompact(); session.Save(); File.WriteAllText(Path.Combine(DirectoryPath, "expected-hash.txt"), Digest(c.State!)); return window;
    }
}
