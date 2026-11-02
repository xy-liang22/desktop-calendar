using DesktopCalendar.Core;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

internal static partial class Checks
{
    static readonly PlannerOptions Utc = new() { TimeZoneId = "UTC" };
    static string TestFile(string output, string name)
    { var dir = Path.Combine(output, "isolated-" + name + "-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); return Path.Combine(dir, "planner-data.json"); }
    static async Task<int> InterruptChild(string[] args)
    {
        var stage = Enum.Parse<WriteStage>(args[2]);
        var store = new PlannerStore(args[1], new() { Fault = s => { if (s == stage) Environment.Exit(73); } });
        var load = await store.OpenAsync(Utc);
        await store.CommitAsync(load.State! with { Revision = load.State!.Revision + 1, Todos = [Todo("new", new(2026, 11, 2))] }, load.Fingerprint);
        return 74;
    }
    static async Task<int> ReadChild(string[] args)
    {
        var load = await new PlannerStore(args[1]).OpenAsync(Utc);
        return load.Status == StoreStatus.Ready && load.State?.Todos.Single().Title == "restart" ? 0 : 75;
    }
    static async Task<int> Child(params string[] args)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        // `dotnet run` launches this test as its generated apphost, not an arbitrary shell.
        foreach (var a in args) info.ArgumentList.Add(a);
        using var p = Process.Start(info)!; using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try { await p.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { p.Kill(); await p.WaitForExitAsync(); return -1; }
        return p.ExitCode;
    }
    static async Task StorageChecks(string output)
    {
        var day = new DateOnly(2026, 11, 2); var original = PlannerState.Empty("UTC") with { Todos = [Todo("restart", day)] };
        var bytes = PlannerCodec.Encode(original); var decoded = PlannerCodec.Decode(bytes, Utc);
        Check("codec roundtrip stable identity and stamps", decoded.State.Todos.Single() == original.Todos.Single() && decoded.State.Id == original.Id);
        Check("runtime settings are not stored with planner data", !Encoding.UTF8.GetString(bytes).Contains("weekStart") && decoded.State.Options == Utc);
        var legacy = PlannerCodec.Decode(PlannerCodec.EncodeLegacyV1ForMigration(original), Utc);
        Check("explicit v1 to v2 migration", legacy.Migrated && legacy.State.Id == original.Id && legacy.State.Revision == 0);
        var node = JsonNode.Parse(bytes)!; node["payload"]!["todos"]![0]!["title"] = "tampered";
        Throws("checksum rejects semantic file tampering", () => PlannerCodec.Decode(Encoding.UTF8.GetBytes(node.ToJsonString()), Utc), "ChecksumMismatch");
        Throws("duplicate JSON fields rejected", () => PlannerCodec.Decode(Encoding.UTF8.GetBytes("{\"schemaVersion\":2,\"schemaVersion\":2}"), Utc), "DuplicateJsonProperty");
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        var key = new OccurrenceKey(Guid.NewGuid(), Guid.NewGuid(), day); var id = key.StableId();
        try { System.Globalization.CultureInfo.CurrentCulture = new("ar-SA"); Check("occurrence identity culture independent", key.StableId() == id); }
        finally { System.Globalization.CultureInfo.CurrentCulture = culture; }
        string path = TestFile(output, "restart"); var store = new PlannerStore(path); var load = await store.OpenAsync(Utc);
        Check("empty path yields nonpersisted new document", load.Status == StoreStatus.Missing && !File.Exists(path));
        original = original with { Id = load.State!.Id, Revision = 1 };
        var saved = await store.CommitAsync(original, null);
        Check("first durable save", saved.Success && File.Exists(path));
        Check("separate process restart restores actual JSON", await Child("--read", path) == 0);
        var second = await store.CommitAsync(original with { Revision = 2, Todos = [original.Todos[0] with { Title = "second" }] }, saved.Fingerprint);
        Check("replacement keeps previous verified backup", second.Success && PlannerCodec.Decode(await File.ReadAllBytesAsync(path + ".bak"), Utc).State.Todos.Single().Title == "restart");
        var stale = await store.CommitAsync(original with { Revision = 3 }, saved.Fingerprint);
        Check("stale fingerprint cannot overwrite", stale.Status == StoreStatus.Conflict);
        using (var lockFile = new FileStream(path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var busy = await new PlannerStore(path, new() { LockTimeout = TimeSpan.FromMilliseconds(80) }).OpenAsync(Utc);
            Check("real file lease contention reports busy", busy.Status == StoreStatus.Busy);
        }
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Check("real Windows data-file sharing violation diagnosed", (await new PlannerStore(path).OpenAsync(Utc)).Status == StoreStatus.IoError);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        try { Check("read-only destination leaves valid old file", (await store.CommitAsync(original with { Revision = 3 }, second.Fingerprint)).Status == StoreStatus.IoError); }
        finally { File.SetAttributes(path, FileAttributes.Normal); }
        Check("failed replace preserves content", PlannerCodec.Decode(await File.ReadAllBytesAsync(path), Utc).State.Revision == 2);
        await File.WriteAllTextAsync(path, "broken-original");
        load = await store.OpenAsync(Utc);
        Check("corrupt primary is blocked with backup candidate", load.Status == StoreStatus.RecoveryRequired && load.State == null && load.Candidates.Length == 1);
        Check("corrupt primary cannot become empty on save", !(await store.CommitAsync(original with { Revision = 3 }, load.Fingerprint)).Success && await File.ReadAllTextAsync(path) == "broken-original");
        Check("recovery requires explicit confirmation", (await store.RecoverAsync(load.Candidates[0], false)).Status == StoreStatus.ConfirmationRequired);
        var recovered = await store.RecoverAsync(load.Candidates[0], true);
        Check("backup recovery restores valid state", recovered.Status == StoreStatus.Ready && recovered.State!.Todos.Single().Title == "restart");
        Check("recovery preserves corrupt primary evidence", Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.evidence-*").Any(p => File.ReadAllText(p) == "broken-original"));
        await File.WriteAllTextAsync(path, "{\"schemaVersion\":999,\"privateFutureField\":1}");
        load = await store.OpenAsync(Utc); var futureBytes = await File.ReadAllBytesAsync(path);
        Check("newer schema blocks write and recovery", load.Status == StoreStatus.NewerSchema && (await store.RecoverAsync(load.Candidates[0], true)).Status == StoreStatus.NewerSchema && !(await store.CommitAsync(original, load.Fingerprint)).Success && futureBytes.AsEnumerable().SequenceEqual(await File.ReadAllBytesAsync(path)));
        string migratedPath = TestFile(output, "migration"); await File.WriteAllBytesAsync(migratedPath, PlannerCodec.EncodeLegacyV1ForMigration(original));
        var migrationStore = new PlannerStore(migratedPath); var migration = await migrationStore.OpenAsync(Utc);
        var migrationSave = await migrationStore.CommitAsync(migration.State! with { Revision = 1 }, migration.Fingerprint);
        Check("migration committed atomically retaining v1 backup", migration.Migrated && migrationSave.Success && JsonNode.Parse(await File.ReadAllBytesAsync(migratedPath))!["schemaVersion"]!.GetValue<int>() == 2 && JsonNode.Parse(await File.ReadAllBytesAsync(migratedPath + ".bak"))!["schemaVersion"]!.GetValue<int>() == 1);
        foreach (var stage in Enum.GetValues<WriteStage>())
        {
            var faultPath = TestFile(output, stage.ToString()); var seed = new PlannerStore(faultPath); var seedLoad = await seed.OpenAsync(Utc);
            await seed.CommitAsync(seedLoad.State! with { Revision = 1, Todos = [Todo("old", day)] }, null);
            int exit = await Child("--interrupt", faultPath, stage.ToString());
            var reopened = await new PlannerStore(faultPath).OpenAsync(Utc);
            Check($"abrupt process exit at {stage}: valid old or new primary", exit == 73 && reopened.Status == StoreStatus.Ready && reopened.State!.Revision == (stage == WriteStage.AfterReplace ? 2 : 1));
            if (stage is WriteStage.Flushed or WriteStage.Validated or WriteStage.BeforeReplace)
                Check($"{stage} complete pending write is recoverable without auto promotion", reopened.PendingFound && reopened.Candidates.Any(c => c.State.Revision == 2) && reopened.State!.Revision == 1);
        }
        string concurrent = TestFile(output, "concurrent"); var one = new PlannerStore(concurrent); var two = new PlannerStore(concurrent);
        var l1 = await one.OpenAsync(Utc); var l2 = await two.OpenAsync(Utc);
        var commits = await Task.WhenAll(one.CommitAsync(l1.State! with { Revision = 1 }, null), two.CommitAsync(l2.State! with { Revision = 1 }, null));
        Check("two independent writers: exactly one commits", commits.Count(c => c.Success) == 1 && commits.Count(c => c.Status == StoreStatus.Conflict) == 1);
        var interrupted = new PlannerStore(TestFile(output, "exception"), new() { Fault = stage => { if (stage == WriteStage.HalfWritten) throw new IOException("Injected"); } });
        var empty = await interrupted.OpenAsync(Utc); var failed = await interrupted.CommitAsync(empty.State! with { Revision = 1 }, null);
        Check("write exception removes only own temporary and keeps missing primary", failed.Status == StoreStatus.IoError && !File.Exists(interrupted.FilePath) && !Directory.EnumerateFiles(Path.GetDirectoryName(interrupted.FilePath)!, "*.pending-*").Any());
        var after = new PlannerStore(TestFile(output, "after"), new() { Fault = stage => { if (stage == WriteStage.AfterReplace) throw new IOException("InjectedAfter"); } });
        var beforeAfter = await after.OpenAsync(Utc);
        Check("post-replace exception reconciles committed outcome", (await after.CommitAsync(beforeAfter.State! with { Revision = 1 }, null)).Success && (await new PlannerStore(after.FilePath).OpenAsync(Utc)).State!.Revision == 1);
        string orphan = TestFile(output, "orphan"); await File.WriteAllTextAsync(orphan + ".pending-test", "partial");
        Check("missing primary with partial orphan blocks empty initialization", (await new PlannerStore(orphan).OpenAsync(Utc)).Status == StoreStatus.RecoveryRequired);
    }
}
