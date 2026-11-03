using DesktopCalendar.Core;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static partial class Checks
{
    static int BackupInterrupt(string[] args)
    { new PlannerBackup(args[1], fault: s => { if (s == RestoreStage.FirstFileWritten) Environment.Exit(91); }).Restore(args[2], args[3], true); return 92; }
    static async Task P8Checks(string output)
    {
        string root = Path.Combine(output, "backup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        string json = Path.Combine(root, "planner-data.json"), preferences = Path.Combine(root, "window-preferences.json"), assets = Path.Combine(root, "assets", "images"); Directory.CreateDirectory(assets);
        var workspace = await PlannerWorkspace.OpenAsync(new(json)); var todo = Todo("Before backup", new(2026, 11, 3)); await workspace.ExecuteAsync(new SaveTodo(todo));
        byte[] image = Encoding.UTF8.GetBytes("test-owned binary image fixture"); string imageName = "assets/images/image.png"; var asset = new LocalAsset { Id = Guid.NewGuid(), RelativePath = imageName, MediaType = "image/png", Length = image.Length, Sha256 = Convert.ToHexStringLower(SHA256.HashData(image)), CreatedAt = Now, ModifiedAt = Now };
        File.WriteAllBytes(Path.Combine(assets, "image.png"), image); await workspace.ExecuteAsync(new SaveAsset(asset));
        File.WriteAllText(preferences, "{\"SchemaVersion\":2,\"Id\":\"desktop-calendar-window\"}"); CalendarKnowledge.Install(Path.Combine(root, "calendar-knowledge.json"), CalendarKnowledge.BuiltIn());
        var service = new PlannerBackup(root); string zip = Path.Combine(output, "backup-" + Guid.NewGuid().ToString("N") + ".zip");
        var info = await workspace.MaintenanceAsync(() => Task.FromResult(service.Create(zip))); byte[] originalJson = File.ReadAllBytes(json);
        Check("ZIP includes planner preferences images and offline calendar", info.Manifest.Version == 1 && info.Manifest.Files.Select(e => e.Path).Order().SequenceEqual(new[] { "assets/images/image.png", "calendar-knowledge.json", "planner-data.json", "window-preferences.json" }));
        Check("ZIP validates manifest hashes and graph", service.Inspect(zip).Fingerprint == info.Fingerprint);
        await workspace.ExecuteAsync(new SaveTodo(todo with { Title = "After backup" })); byte[] modifiedJson = File.ReadAllBytes(json);
        void Rejected(string name, Action op) { try { op(); Check(name, false); } catch (Exception e) when (e is InvalidDataException or IOException or PlannerException or JsonException or InvalidOperationException) { Check(name, true); } }
        Rejected("restore cannot occur without explicit confirmation", () => service.Restore(zip, info.Fingerprint, false)); Check("unconfirmed restore preserves current bytes", File.ReadAllBytes(json).SequenceEqual(modifiedJson));
        Rejected("changed ZIP token rejected after preview", () => service.Restore(zip, "bad-token", true));
        string point = await workspace.MaintenanceAsync(() => Task.FromResult(service.Restore(zip, info.Fingerprint, true))); await workspace.ReloadAsync();
        Check("restore reloads stable IDs title and verified image bytes", workspace.Snapshot!.Todos.Single().Id == todo.Id && workspace.Snapshot.Todos.Single().Title == "Before backup" && File.ReadAllBytes(json).SequenceEqual(originalJson) && File.ReadAllBytes(Path.Combine(assets, "image.png")).SequenceEqual(image));
        Check("current bytes preserved in durable recovery point", File.ReadAllBytes(Path.Combine(point, "planner-data.json")).SequenceEqual(modifiedJson) && File.Exists(Path.Combine(point, "recovery-manifest.json")) && !File.Exists(Path.Combine(root, "restore-journal.json")));
        await workspace.ExecuteAsync(new SaveTodo(todo with { Title = "Rollback baseline" })); byte[] baseline = File.ReadAllBytes(json);
        foreach (var stage in Enum.GetValues<RestoreStage>())
        {
            var faulty = new PlannerBackup(root, fault: s => { if (s == stage) throw new IOException("test interruption"); });
            Rejected("injected restore failure reported " + stage, () => faulty.Restore(zip, info.Fingerprint, true)); Check("injected failure preserves original JSON " + stage, File.ReadAllBytes(json).SequenceEqual(baseline));
        }
        Check("failed restore leaves complete image and no pending journal", File.ReadAllBytes(Path.Combine(assets, "image.png")).SequenceEqual(image) && !File.Exists(Path.Combine(root, "restore-journal.json")));
        int exit = await Child("--backup-interrupt", root, zip, info.Fingerprint); Check("real child exits during restore before rollback", exit == 91 && File.Exists(Path.Combine(root, "restore-journal.json")));
        Check("next startup journal recovers exact pre-restore planner", service.RecoverInterrupted() && File.ReadAllBytes(json).SequenceEqual(baseline) && !service.RecoverInterrupted());
        string imagePath = Path.Combine(assets, "image.png"); byte[] changedImage = Encoding.UTF8.GetBytes("pre-restore damaged image retained for rollback fixture"); File.WriteAllBytes(imagePath, changedImage);
        FileStream? blocker = null; var cannotRollback = new PlannerBackup(root, fault: s => { if (s == RestoreStage.BeforePlannerReplace) { blocker = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read); throw new IOException("blocked rollback fixture"); } });
        Rejected("blocked rollback reports failure and retains recovery journal", () => cannotRollback.Restore(zip, info.Fingerprint, true));
        Check("blocked rollback retains durable recovery evidence", File.Exists(Path.Combine(root, "restore-journal.json")));
        blocker?.Dispose(); Check("startup retries rollback after file share obstacle clears", service.RecoverInterrupted() && File.ReadAllBytes(imagePath).SequenceEqual(changedImage) && File.ReadAllBytes(json).SequenceEqual(baseline)); File.WriteAllBytes(imagePath, image);
        string Mutated(string id, Action<ZipArchive> edit)
        { string path = Path.Combine(output, "bad-backup-" + id + Guid.NewGuid().ToString("N") + ".zip"); File.Copy(zip, path); using (var archive = ZipFile.Open(path, ZipArchiveMode.Update)) edit(archive); return path; }
        foreach (string name in new[] { "../escape.txt", "/planner-data.json", "assets/images/../../escape.png", "assets\\images\\escape.png", "assets/images/con.png", "assets/images/x.png:stream", "DesktopCalendar.exe" })
        { string bad = Mutated("path", z => { using var s = z.CreateEntry(name).Open(); s.WriteByte(1); }); Rejected("ZIP path/executable entry rejected " + name, () => service.Inspect(bad)); }
        string duplicate = Mutated("duplicate", z => { using var s = z.CreateEntry("PLANNER-DATA.JSON").Open(); s.WriteByte(1); }); Rejected("ZIP case-alias duplicate rejected", () => service.Inspect(duplicate));
        string missing = Mutated("missing", z => z.GetEntry(imageName)!.Delete()); Rejected("missing registered image rejected", () => service.Inspect(missing));
        string damaged = Mutated("damage", z => { z.GetEntry(imageName)!.Delete(); using var s = z.CreateEntry(imageName).Open(); s.WriteByte(3); }); Rejected("damaged image hash rejected", () => service.Inspect(damaged));
        string future = Mutated("version", z => { z.GetEntry("manifest.json")!.Delete(); using var s = z.CreateEntry("manifest.json").Open(); JsonSerializer.Serialize(s, info.Manifest with { Version = 999 }); }); Rejected("future ZIP version rejected", () => service.Inspect(future));
        string nullPath = Mutated("nullpath", z => { z.GetEntry("manifest.json")!.Delete(); using var s = z.CreateEntry("manifest.json").Open(); JsonSerializer.Serialize(s, info.Manifest with { Files = info.Manifest.Files.Select((e, i) => i == 0 ? e with { Path = null! } : e).ToArray() }); }); Rejected("null manifest paths produce diagnostic instead of unhandled exception", () => service.Inspect(nullPath));
        string symlink = Mutated("link", z => { var e = z.GetEntry(imageName)!; e.ExternalAttributes = (0xA000 | 0x1FF) << 16; }); Rejected("ZIP symlink rejected", () => service.Inspect(symlink));
        string bomb = Mutated("size", z => { using var s = z.CreateEntry("assets/images/large.png").Open(); byte[] b = new byte[1024 * 1024]; for (int i = 0; i < 65; i++) s.Write(b); }); Rejected("decompression size ceiling rejects bomb", () => service.Inspect(bomb));
        File.WriteAllText(json, "{\"schemaVersion\":999}"); Rejected("newer current planner cannot be downgraded by backup", () => service.Restore(zip, info.Fingerprint, true)); Check("newer current bytes intact", File.ReadAllText(json).Contains("999"));
        File.WriteAllText(json, "BROKEN CURRENT TEST FIXTURE"); string rescue = service.Restore(zip, info.Fingerprint, true); Check("valid ZIP can rescue damaged current JSON while retaining evidence", File.ReadAllBytes(json).SequenceEqual(originalJson) && File.ReadAllText(Path.Combine(rescue, "planner-data.json")) == "BROKEN CURRENT TEST FIXTURE");
        File.Delete(Path.Combine(assets, "image.png")); Rejected("backup refuses missing referenced asset without harming JSON", () => service.Create(Path.Combine(output, "missing-assets.zip"))); Check("failed backup leaves planner bytes intact", File.ReadAllBytes(json).SequenceEqual(originalJson)); File.WriteAllBytes(Path.Combine(assets, "image.png"), image);
        using (var locked = new FileStream(json + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        { var lockedStore = new PlannerStore(json, new() { LockTimeout = TimeSpan.FromMilliseconds(70) }); var lockedLoad = await lockedStore.OpenAsync(); Check("store lease blocks competing maintenance/read", lockedLoad.Status == StoreStatus.Busy); }
        await workspace.ReloadAsync(); var held = new TaskCompletionSource(); var release = new TaskCompletionSource(); var work = workspace.MaintenanceAsync(async () => { held.SetResult(); await release.Task; return 1; }); await held.Task;
        var queuedSave = workspace.ExecuteAsync(new SaveTodo(todo with { Title = "Queued after maintenance" })); Check("save waits behind backup/restore maintenance gate", !queuedSave.IsCompleted); release.SetResult(); await work; Check("queued save completes after maintenance and stays coherent", (await queuedSave).Success && workspace.Snapshot!.Todos.Single().Title == "Queued after maintenance");
        Check("backup never contains program cache logs or other backups", !info.Manifest.Files.Any(e => e.Path.EndsWith(".exe") || e.Path.Contains("recovery-points") || e.Path.EndsWith(".log")));
        using (var updating = new FileStream(Path.Combine(root, "calendar-knowledge.json.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) Rejected("calendar update and backup share a nonoverlapping file lease", () => service.Create(Path.Combine(output, "calendar-busy.zip")));
        byte[] prefs = File.ReadAllBytes(preferences); File.WriteAllText(preferences, "{\"SchemaVersion\":999,\"Id\":\"desktop-calendar-window\"}"); Rejected("newer current preferences protected from restore downgrade", () => service.Restore(zip, info.Fingerprint, true)); File.WriteAllBytes(preferences, prefs);
        File.WriteAllText(Path.Combine(root, "restore-journal.json"), "{}"); byte[] unchanged = File.ReadAllBytes(json); Rejected("invalid interrupted journal fails closed without deleting data", () => service.RecoverInterrupted()); Check("invalid journal preserves planner and recovery evidence", File.ReadAllBytes(json).SequenceEqual(unchanged) && File.Exists(Path.Combine(root, "restore-journal.json"))); Rejected("pending recovery cannot be labelled a complete backup", () => service.Create(Path.Combine(output, "pending-recovery.zip"))); File.Delete(Path.Combine(root, "restore-journal.json"));
    }
}
