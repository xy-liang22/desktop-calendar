using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace DesktopCalendar.Core;

public sealed record BackupEntry(string Path, long Length, string Sha256);
public sealed record BackupManifest(int Version, string AppId, int PlannerSchema, DateTimeOffset CreatedAt, BackupEntry[] Files);
public sealed record BackupInspection(BackupManifest Manifest, string Fingerprint);
public enum RestoreStage { RecoveryPointReady, JournalWritten, FirstFileWritten, BeforePlannerReplace, PlannerReplaced }
// The host must hold its workspace/store maintenance lease while using this service.
public sealed class PlannerBackup
{
    public const long MaximumTotal = 512L * 1024 * 1024;
    const long MaximumFile = 64L * 1024 * 1024;
    readonly string root;
    readonly Action<string, byte[]>? validateExtra;
    readonly Action<RestoreStage>? fault;
    public PlannerBackup(string directory, Action<string, byte[]>? validateExtra = null, Action<RestoreStage>? fault = null)
    { root = Path.GetFullPath(directory); this.validateExtra = validateExtra; this.fault = fault; SafePath(root); }
    static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    static string HashFile(string path) { using var s = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(s)); }
    static void Space(string path, long needed)
    { var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))!); if (drive.AvailableFreeSpace < needed + 8 * 1024 * 1024) throw new IOException("InsufficientSpaceForSafeBackupOrRestore"); }
    FileStream KnowledgeLease() { Directory.CreateDirectory(root); string p = Path.Combine(root, "calendar-knowledge.json.lock"); SafePath(p); return new FileStream(p, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
    public static void SafePath(string path)
    { for (string? p = Path.GetFullPath(path); p != null; p = Path.GetDirectoryName(p)) if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new IOException("ReparsePointRejected"); }
    static bool Managed(string name, bool auxiliary = false)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (name is "planner-data.json" or "window-preferences.json" or "calendar-knowledge.json") return true;
        if (name.StartsWith("assets/images/", StringComparison.Ordinal) && name.Count(c => c == '/') == 2)
        { string tail = name[14..]; return tail.Length is > 0 and < 240 && tail != "." && !tail.Contains("..") && !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileNameWithoutExtension(tail), "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", System.Text.RegularExpressions.RegexOptions.IgnoreCase) && !tail.Any(c => c is ':' or '\\' or '%' or '?' or '#' || char.IsControl(c)) && new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp" }.Contains(Path.GetExtension(tail).ToLowerInvariant()); }
        return auxiliary && (name is "planner-data.json.bak" or "window-preferences.json.bak" or "calendar-knowledge.json.bak" || name.StartsWith("planner-data.json.pending-", StringComparison.Ordinal) || name.StartsWith("planner-data.json.evidence-", StringComparison.Ordinal)) && !name.Any(c => c is '/' or '\\' or ':' || char.IsControl(c));
    }
    string Target(string name, bool auxiliary = false)
    { if (!Managed(name, auxiliary)) throw new InvalidDataException("UnsafeBackupPath"); string path = Path.GetFullPath(Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar))); if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("BackupPathEscape"); SafePath(path); return path; }
    Dictionary<string, byte[]> ReadCurrent(bool auxiliary)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase); long total = 0;
        if (!Directory.Exists(root)) return result;
        string images = Path.Combine(root, "assets", "images"); SafePath(images);
        var files = Directory.EnumerateFiles(root).Where(f => Managed(Path.GetFileName(f), auxiliary)).Concat(Directory.Exists(images) ? Directory.EnumerateFiles(images) : []);
        foreach (string file in files)
        {
            string name = Path.GetRelativePath(root, file).Replace('\\', '/'); if (!Managed(name, auxiliary)) continue; SafePath(file);
            long size = new FileInfo(file).Length; if (size > MaximumFile || (total += size) > MaximumTotal || result.Count >= 10000) throw new InvalidDataException("BackupSizeLimit");
            byte[] bytes = File.ReadAllBytes(file); if (bytes.LongLength != size) throw new IOException("FileChangedDuringBackup"); result.Add(name, bytes);
        }
        return result;
    }
    void ValidateFiles(Dictionary<string, byte[]> files)
    {
        if (!files.TryGetValue("planner-data.json", out var planner)) throw new InvalidDataException("MissingPlanner");
        var state = PlannerCodec.Decode(planner).State;
        if (!files.ContainsKey("window-preferences.json")) throw new InvalidDataException("MissingPreferences");
        foreach (var (name, bytes) in files)
        {
            if (name == "window-preferences.json") { using var d = JsonDocument.Parse(bytes); var e = d.RootElement; if (!e.TryGetProperty("SchemaVersion", out var v) || v.GetInt32() is not (1 or 2) || e.GetProperty("Id").GetString() != "desktop-calendar-window") throw new InvalidDataException("PreferenceSchema"); }
            if (name == "calendar-knowledge.json") CalendarKnowledge.Decode(System.Text.Encoding.UTF8.GetString(bytes));
            validateExtra?.Invoke(name, bytes);
        }
        foreach (var a in state.Assets)
            if (!files.TryGetValue(a.RelativePath, out var bytes) || bytes.LongLength != a.Length || Hash(bytes) != a.Sha256.ToLowerInvariant()) throw new InvalidDataException("MissingOrDamagedAsset");
    }
    static int Schema(Dictionary<string, byte[]> files) { try { using var d = JsonDocument.Parse(files["planner-data.json"]); return d.RootElement.GetProperty("schemaVersion").GetInt32(); } catch { return 0; } }
    static BackupManifest Manifest(Dictionary<string, byte[]> files) => new(1, "desktop-calendar", Schema(files), DateTimeOffset.UtcNow, files.Select(x => new BackupEntry(x.Key, x.Value.LongLength, Hash(x.Value))).OrderBy(e => e.Path, StringComparer.Ordinal).ToArray());
    public BackupInspection Create(string destination)
    {
        using var knowledgeLease = KnowledgeLease();
        if (File.Exists(JournalPath)) throw new IOException("ResolvePendingRecoveryFirst");
        SafePath(destination); if (Path.GetExtension(destination).ToLowerInvariant() != ".zip" || Managed(Path.GetRelativePath(root, destination).Replace('\\', '/'), true)) throw new InvalidDataException("UnsafeBackupDestination");
        var files = ReadCurrent(false); ValidateFiles(files); var manifest = Manifest(files); Space(destination, files.Values.Sum(b => b.LongLength)); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!); string temporary = destination + ".pending-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            { using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true)) { foreach (var (name, bytes) in files) { using var s = zip.CreateEntry(name, CompressionLevel.Optimal).Open(); s.Write(bytes); } using var m = zip.CreateEntry("manifest.json").Open(); JsonSerializer.Serialize(m, manifest); } output.Flush(true); }
            Inspect(temporary); if (File.Exists(destination)) File.Replace(temporary, destination, destination + ".bak"); else File.Move(temporary, destination);
            return new(manifest, HashFile(destination));
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    (BackupInspection Info, Dictionary<string, byte[]> Files) ReadArchive(string path)
    {
        SafePath(path); if (new FileInfo(path).Length > MaximumTotal + 8 * 1024 * 1024) throw new InvalidDataException("ZipSizeLimit");
        using var stream = File.OpenRead(path); string fingerprint = Convert.ToHexStringLower(SHA256.HashData(stream)); stream.Position = 0; using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        if (zip.Entries.Count > 10001) throw new InvalidDataException("ZipEntryLimit"); var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase); byte[]? manifestBytes = null; long total = 0;
        foreach (var e in zip.Entries)
        {
            string name = e.FullName;
            if (name != "manifest.json" && !Managed(name) || (e.ExternalAttributes & 0x400) != 0 || (e.ExternalAttributes >> 16 & 0xF000) == 0xA000 || e.Length > (name == "manifest.json" ? 4_000_000 : MaximumFile) || e.Length < 0 || (total += e.Length) > MaximumTotal) throw new InvalidDataException("UnsafeZipEntry");
            using var s = e.Open(); using var buffer = new MemoryStream(); byte[] chunk = new byte[8192]; int n; while ((n = s.Read(chunk)) > 0) { if (buffer.Length + n > e.Length) throw new InvalidDataException("ZipLengthMismatch"); buffer.Write(chunk, 0, n); } if (buffer.Length != e.Length) throw new InvalidDataException("ZipLengthMismatch");
            if (name == "manifest.json") { if (manifestBytes != null) throw new InvalidDataException("DuplicateManifest"); manifestBytes = buffer.ToArray(); } else if (!files.TryAdd(name, buffer.ToArray())) throw new InvalidDataException("DuplicateZipEntry");
        }
        if (manifestBytes == null) throw new InvalidDataException("MissingManifest");
        var m = JsonSerializer.Deserialize<BackupManifest>(manifestBytes) ?? throw new InvalidDataException("ManifestEmpty");
        if (m.Version != 1 || m.AppId != "desktop-calendar" || m.PlannerSchema is < 1 or > 3 || m.PlannerSchema != Schema(files) || m.CreatedAt == default || m.Files == null || m.Files.Any(e => e == null || string.IsNullOrEmpty(e.Path) || e.Sha256 == null || e.Length < 0 || e.Length > MaximumFile) || m.Files.Length != files.Count || m.Files.Select(e => e.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count) throw new InvalidDataException("BackupVersionOrManifest");
        foreach (var entry in m.Files) if (!files.TryGetValue(entry.Path, out var bytes) || bytes.LongLength != entry.Length || Hash(bytes) != entry.Sha256) throw new InvalidDataException("BackupChecksumMismatch");
        ValidateFiles(files); return (new(m, fingerprint), files);
    }
    public BackupInspection Inspect(string path) => ReadArchive(path).Info;
    sealed record Journal(int Version, string Point, BackupEntry[] Originals, BackupEntry[] Incoming);
    string JournalPath => Path.Combine(root, "restore-journal.json");
    static void WriteAtomic(string path, byte[] bytes)
    {
        SafePath(path); if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0) throw new UnauthorizedAccessException("ReadOnlyRestoreTarget"); Directory.CreateDirectory(Path.GetDirectoryName(path)!); string temp = path + ".restore-" + Guid.NewGuid().ToString("N");
        try { using (var f = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { f.Write(bytes); f.Flush(true); } if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    void Rollback(Journal journal)
    {
        if (journal.Version != 1 || !System.Text.RegularExpressions.Regex.IsMatch(journal.Point ?? "", "^recovery-points/[a-f0-9]{32}$") || journal.Originals == null || journal.Incoming == null || journal.Originals.Length > 10000 || journal.Incoming.Length > 10000 || journal.Originals.Concat(journal.Incoming).Any(e => e == null || e.Length < 0 || e.Length > MaximumFile || e.Sha256 == null || !System.Text.RegularExpressions.Regex.IsMatch(e.Sha256, "^[a-f0-9]{64}$"))) throw new InvalidDataException("UnsafeRecoveryJournal");
        string point = Path.Combine(root, journal.Point!); SafePath(point);
        foreach (var original in journal.Originals) { _ = Target(original.Path, true); string file = Path.Combine(point, original.Path); SafePath(file); if (!File.Exists(file) || new FileInfo(file).Length != original.Length || HashFile(file) != original.Sha256) throw new InvalidDataException("RecoveryPointDamaged"); }
        foreach (var incoming in journal.Incoming.Where(e => !journal.Originals.Any(o => o.Path == e.Path)))
        { string path = Target(incoming.Path); if (File.Exists(path)) { if (HashFile(path) != incoming.Sha256) throw new IOException("UnexpectedFileDuringRollback"); File.Delete(path); } }
        foreach (var original in journal.Originals.OrderBy(e => e.Path == "planner-data.json")) { string path = Target(original.Path, true); if (!File.Exists(path) || HashFile(path) != original.Sha256) WriteAtomic(path, File.ReadAllBytes(Path.Combine(point, original.Path))); }
        File.Delete(JournalPath);
    }
    public bool RecoverInterrupted()
    { SafePath(JournalPath); if (!File.Exists(JournalPath)) return false; string lockPath = Path.Combine(root, "planner-data.json.lock"); SafePath(lockPath); using var lease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); using var knowledgeLease = KnowledgeLease(); if (!File.Exists(JournalPath)) return false; if (new FileInfo(JournalPath).Length > 4_000_000) throw new InvalidDataException("RecoveryJournalTooLarge"); var journal = JsonSerializer.Deserialize<Journal>(File.ReadAllBytes(JournalPath)) ?? throw new InvalidDataException("RecoveryJournalEmpty"); Rollback(journal); return true; }
    public string Restore(string zip, string expectedFingerprint, bool confirmed)
    {
        if (!confirmed) throw new InvalidOperationException("RestoreConfirmationRequired");
        using var knowledgeLease = KnowledgeLease();
        if (File.Exists(JournalPath)) throw new IOException("ResolvePendingRecoveryFirst");
        var archive = ReadArchive(zip); if (archive.Info.Fingerprint != expectedFingerprint) throw new InvalidDataException("BackupChangedAfterPreview");
        foreach (var (name, property, supported) in new[] { ("window-preferences.json", "SchemaVersion", 2), ("calendar-knowledge.json", "SchemaVersion", 1) })
        { string p = Target(name); if (File.Exists(p)) try { if (new FileInfo(p).Length > MaximumFile) throw new IOException("CurrentFileTooLarge"); using var d = JsonDocument.Parse(File.ReadAllBytes(p)); if (d.RootElement.TryGetProperty(property, out var v) && v.TryGetInt32(out int n) && n > supported) throw new InvalidDataException("NewerCurrentSettings"); } catch (JsonException) { } }
        // Never downgrade a newer current planner document, even with a compatible old ZIP.
        string currentPlanner = Target("planner-data.json"); if (File.Exists(currentPlanner)) try { if (new FileInfo(currentPlanner).Length > MaximumFile) throw new IOException("CurrentPlannerTooLarge"); PlannerCodec.Decode(File.ReadAllBytes(currentPlanner)); } catch (PlannerException e) when (e.Code != "NewerSchema") { } catch (JsonException) { }
        var originals = ReadCurrent(true); Space(root, originals.Values.Sum(b => b.LongLength) + archive.Files.Values.Sum(b => b.LongLength) + archive.Files.Values.Max(b => b.LongLength)); string relative = "recovery-points/" + Guid.NewGuid().ToString("N"), point = Path.Combine(root, relative); Directory.CreateDirectory(point);
        foreach (var (name, bytes) in originals) WriteAtomic(Path.Combine(point, name), bytes);
        var originalManifest = Manifest(originals); WriteAtomic(Path.Combine(point, "recovery-manifest.json"), JsonSerializer.SerializeToUtf8Bytes(originalManifest)); fault?.Invoke(RestoreStage.RecoveryPointReady);
        var journal = new Journal(1, relative, originalManifest.Files, archive.Info.Manifest.Files); WriteAtomic(JournalPath, JsonSerializer.SerializeToUtf8Bytes(journal));
        try
        {
            fault?.Invoke(RestoreStage.JournalWritten); int count = 0;
            // Quarantine old store recovery candidates so they cannot supersede the restored document.
            foreach (var name in originals.Keys.Where(n => !Managed(n) || n == "calendar-knowledge.json" && !archive.Files.ContainsKey(n))) File.Delete(Target(name, true));
            foreach (var (name, bytes) in archive.Files.OrderBy(x => x.Key == "planner-data.json")) { if (name == "planner-data.json") fault?.Invoke(RestoreStage.BeforePlannerReplace); WriteAtomic(Target(name), bytes); if (++count == 1) fault?.Invoke(RestoreStage.FirstFileWritten); }
            fault?.Invoke(RestoreStage.PlannerReplaced); File.Delete(JournalPath); return point;
        }
        catch { Rollback(journal); throw; }
    }
}
