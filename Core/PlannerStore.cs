using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace DesktopCalendar.Core;

public enum StoreStatus { Ready, Missing, RecoveryRequired, NewerSchema, IoError, Busy, Conflict, InvalidData, Cancelled, ConfirmationRequired }
public enum WriteStage { TempCreated, HalfWritten, Flushed, Validated, BeforeReplace, AfterReplace }
public sealed record RecoveryCandidate(string Name, string Fingerprint, PlannerState State);
public sealed record StoreLoad(StoreStatus Status, PlannerState? State, string? Fingerprint, string Diagnostic,
    ImmutableArray<RecoveryCandidate> Candidates, bool Migrated = false, bool PendingFound = false)
{
    public bool CanWrite => Status is StoreStatus.Ready or StoreStatus.Missing;
}
public sealed record StoreCommit(StoreStatus Status, string? Fingerprint, string Diagnostic)
{
    public bool Success => Status == StoreStatus.Ready;
}
public sealed record StoreOptions
{
    public long MaximumBytes { get; init; } = 64 * 1024 * 1024;
    public TimeSpan LockTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public Action<WriteStage>? Fault { get; init; } // Explicit test seam, never enabled by the app.
}

public sealed class PlannerStore
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCalendar", "planner-data.json");
    public string FilePath { get; }
    public string AssetDirectory => Path.Combine(Path.GetDirectoryName(FilePath)!, "assets", "images");
    readonly StoreOptions options;
    readonly SemaphoreSlim gate = new(1, 1);
    StoreLoad? lastLoad;
    PlannerOptions context = new() { TimeZoneId = TimeZoneInfo.Local.Id };
    public PlannerStore(string? path = null, StoreOptions? options = null)
    {
        FilePath = Path.GetFullPath(path ?? DefaultPath); this.options = options ?? new();
        if (this.options.MaximumBytes < 100 || this.options.LockTimeout <= TimeSpan.Zero) throw new ArgumentException("Invalid store options.");
    }
    static string Fingerprint(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    void CheckPath(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("ReparsePointRejected");
    }
    async Task<FileStream> Lease(CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        for (var ancestor = new DirectoryInfo(directory); ancestor != null; ancestor = ancestor.Parent) CheckPath(ancestor.FullName);
        if (Directory.Exists(FilePath)) throw new IOException("DestinationIsDirectory");
        Directory.CreateDirectory(directory);
        string path = FilePath + ".lock"; CheckPath(path);
        var started = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (started.Elapsed < options.LockTimeout) { await Task.Delay(25, ct).ConfigureAwait(false); }
            catch (IOException) { throw new PlannerException("StoreBusy"); }
        }
    }
    async Task<byte[]> Read(string path, CancellationToken ct)
    {
        CheckPath(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        if (stream.Length > options.MaximumBytes) throw new PlannerException("FileTooLarge");
        using var memory = new MemoryStream(); var buffer = new byte[16384]; int count;
        while ((count = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) != 0)
        { if (memory.Length + count > options.MaximumBytes) throw new PlannerException("FileTooLarge"); memory.Write(buffer, 0, count); }
        return memory.ToArray();
    }
    static bool IsDataError(Exception e) => e is JsonException or PlannerException or ArgumentException or InvalidOperationException or KeyNotFoundException or NullReferenceException or OverflowException;
    static StoreStatus Failure(Exception e) => e switch { OperationCanceledException => StoreStatus.Cancelled, PlannerException { Code: "StoreBusy" } => StoreStatus.Busy,
        PlannerException { Code: "NewerSchema" } => StoreStatus.NewerSchema, _ when IsDataError(e) => StoreStatus.InvalidData, _ => StoreStatus.IoError };
    static string Diagnostic(Exception e) => e is PlannerException p ? p.Code : e is IOException or UnauthorizedAccessException ? $"{e.GetType().Name}:0x{e.HResult:X8}" : e.GetType().Name;
    IEnumerable<string> CandidatePaths() => new[] { FilePath + ".bak" }.Concat(Directory.EnumerateFiles(Path.GetDirectoryName(FilePath)!, Path.GetFileName(FilePath) + ".pending-*")).Where(File.Exists);
    public async Task<StoreLoad> OpenAsync(PlannerOptions? context = null, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            this.context = context ?? this.context;
            PlannerIntegrity.ValidateOptions(this.context);
            using var lease = await Lease(ct).ConfigureAwait(false);
            string? token = null; PlannerState? state = null; bool migrated = false; string error = "";
            StoreStatus status = StoreStatus.Missing;
            if (File.Exists(FilePath))
            {
                var bytes = await Read(FilePath, ct).ConfigureAwait(false); token = Fingerprint(bytes);
                try { var decoded = PlannerCodec.Decode(bytes, this.context); state = decoded.State; migrated = decoded.Migrated; status = StoreStatus.Ready; }
                catch (Exception e) when (IsDataError(e)) { status = e is PlannerException { Code: "NewerSchema" } ? StoreStatus.NewerSchema : StoreStatus.RecoveryRequired; error = Diagnostic(e); }
            }
            var candidates = new List<RecoveryCandidate>(); bool pending = false;
            foreach (var path in CandidatePaths())
            {
                pending |= path != FilePath + ".bak";
                try { var bytes = await Read(path, ct).ConfigureAwait(false); candidates.Add(new(Path.GetFileName(path), Fingerprint(bytes), PlannerCodec.Decode(bytes, this.context).State)); }
                catch (PlannerException e) when (e.Code == "NewerSchema") { status = StoreStatus.NewerSchema; error = "NewerRecoverySchema"; }
                catch (Exception e) when (IsDataError(e) || e is IOException) { }
            }
            if (status == StoreStatus.Missing && (pending || File.Exists(FilePath + ".bak"))) status = StoreStatus.RecoveryRequired;
            if (status == StoreStatus.Missing) state = PlannerState.Empty(this.context.TimeZoneId) with { Options = this.context };
            return lastLoad = new(status, state, token, error, [.. candidates], migrated, pending);
        }
        catch (Exception e) when (IsDataError(e) || e is IOException or UnauthorizedAccessException or OperationCanceledException)
        { return lastLoad = new(Failure(e), null, null, Diagnostic(e), []); }
        finally { gate.Release(); }
    }
    async Task<string?> DiskToken(CancellationToken ct) => File.Exists(FilePath) ? Fingerprint(await Read(FilePath, ct).ConfigureAwait(false)) : null;
    public async Task<T> MaintenanceAsync<T>(string? expectedFingerprint, Func<Task<T>> action, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try { using var lease = await Lease(ct).ConfigureAwait(false); if (await DiskToken(ct).ConfigureAwait(false) != expectedFingerprint) throw new PlannerException("FileChangedExternally"); return await action().ConfigureAwait(false); }
        finally { gate.Release(); }
    }
    async Task WriteFlushed(string path, byte[] bytes, bool fault, CancellationToken ct)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough);
        if (fault) options.Fault?.Invoke(WriteStage.TempCreated);
        int half = bytes.Length / 2;
        await stream.WriteAsync(bytes.AsMemory(0, half), ct).ConfigureAwait(false);
        if (fault) options.Fault?.Invoke(WriteStage.HalfWritten);
        await stream.WriteAsync(bytes.AsMemory(half), ct).ConfigureAwait(false); await stream.FlushAsync(ct).ConfigureAwait(false); stream.Flush(true);
        if (fault) options.Fault?.Invoke(WriteStage.Flushed);
    }
    async Task Preserve(string path, CancellationToken ct)
    {
        var bytes = await Read(path, ct).ConfigureAwait(false);
        await WriteFlushed(FilePath + ".evidence-" + Guid.NewGuid().ToString("N"), bytes, false, ct).ConfigureAwait(false);
    }
    public async Task<StoreCommit> CommitAsync(PlannerState state, string? expectedFingerprint, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        string? temporary = null; bool replaced = false; byte[]? encoded = null;
        try
        {
            if (lastLoad == null || !lastLoad.CanWrite) return new(lastLoad?.Status ?? StoreStatus.InvalidData, null, "OpenWritableDocumentFirst");
            using var lease = await Lease(ct).ConfigureAwait(false);
            if (await DiskToken(ct).ConfigureAwait(false) != expectedFingerprint || lastLoad.Fingerprint != expectedFingerprint) return new(StoreStatus.Conflict, null, "FileChangedExternally");
            if (state.Id != lastLoad.State!.Id || state.Revision != lastLoad.State.Revision + 1) return new(StoreStatus.Conflict, null, "RevisionMismatch");
            encoded = PlannerCodec.Encode(state);
            if (encoded.Length > options.MaximumBytes) return new(StoreStatus.InvalidData, null, "FileTooLarge");
            // A writer must not erase a newer or corrupt backup unnoticed.
            string backup = FilePath + ".bak";
            if (File.Exists(backup))
            {
                try { _ = PlannerCodec.Decode(await Read(backup, ct).ConfigureAwait(false), context); }
                catch (PlannerException e) when (e.Code == "NewerSchema") { return new(StoreStatus.NewerSchema, null, "NewerBackupSchema"); }
                catch (Exception e) when (IsDataError(e)) { await Preserve(backup, ct).ConfigureAwait(false); }
            }
            temporary = FilePath + ".pending-" + Guid.NewGuid().ToString("N");
            await WriteFlushed(temporary, encoded, true, ct).ConfigureAwait(false);
            _ = PlannerCodec.Decode(await Read(temporary, ct).ConfigureAwait(false), context); options.Fault?.Invoke(WriteStage.Validated);
            ct.ThrowIfCancellationRequested(); options.Fault?.Invoke(WriteStage.BeforeReplace);
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, backup); else File.Move(temporary, FilePath);
            replaced = true; temporary = null; options.Fault?.Invoke(WriteStage.AfterReplace);
            var token = Fingerprint(encoded);
            lastLoad = new(StoreStatus.Ready, state, token, "", [], false);
            return new(StoreStatus.Ready, token, "");
        }
        catch (Exception e) when (IsDataError(e) || e is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            bool verified = false;
            if (replaced && encoded != null)
            {
                try { using var lease = await Lease(CancellationToken.None).ConfigureAwait(false); verified = await DiskToken(CancellationToken.None).ConfigureAwait(false) == Fingerprint(encoded); }
                catch (Exception check) when (IsDataError(check) || check is IOException or UnauthorizedAccessException) { }
            }
            if (verified && encoded != null)
            {
                var token = Fingerprint(encoded); lastLoad = new(StoreStatus.Ready, state, token, "VerifiedAfterCommitError", []);
                return new(StoreStatus.Ready, token, "VerifiedAfterCommitError");
            }
            if (replaced) { lastLoad = lastLoad! with { Status = StoreStatus.Conflict, Diagnostic = "CommitOutcomeRequiresReload" }; return new(StoreStatus.Conflict, null, "CommitOutcomeRequiresReload"); }
            return new(Failure(e), null, Diagnostic(e));
        }
        finally { if (temporary != null) { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } } gate.Release(); }
    }
    public async Task<StoreLoad> RecoverAsync(RecoveryCandidate candidate, bool confirmed, CancellationToken ct = default)
    {
        if (!confirmed) return new(StoreStatus.ConfirmationRequired, null, null, "ExplicitRecoveryRequired", []);
        await gate.WaitAsync(ct).ConfigureAwait(false);
        string? temporary = null;
        try
        {
            if (lastLoad == null || lastLoad.Status == StoreStatus.NewerSchema) return new(StoreStatus.NewerSchema, null, null, "RecoveryBlocked", []);
            using var lease = await Lease(ct).ConfigureAwait(false);
            if (await DiskToken(ct).ConfigureAwait(false) != lastLoad.Fingerprint) return new(StoreStatus.Conflict, null, null, "FileChangedExternally", []);
            bool allowed = candidate.Name == Path.GetFileName(FilePath) + ".bak" || candidate.Name.StartsWith(Path.GetFileName(FilePath) + ".pending-", StringComparison.Ordinal);
            if (!allowed || Path.GetFileName(candidate.Name) != candidate.Name) throw new PlannerException("UnsafeRecoveryPath");
            var bytes = await Read(Path.Combine(Path.GetDirectoryName(FilePath)!, candidate.Name), ct).ConfigureAwait(false);
            if (Fingerprint(bytes) != candidate.Fingerprint) return new(StoreStatus.Conflict, null, null, "RecoveryCandidateChanged", []);
            var decoded = PlannerCodec.Decode(bytes, context);
            if (File.Exists(FilePath)) await Preserve(FilePath, ct).ConfigureAwait(false);
            temporary = FilePath + ".pending-" + Guid.NewGuid().ToString("N");
            await WriteFlushed(temporary, bytes, false, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null); else File.Move(temporary, FilePath);
            temporary = null;
            return lastLoad = new(StoreStatus.Ready, decoded.State, Fingerprint(bytes), "OriginalPreserved", [], decoded.Migrated);
        }
        catch (Exception e) when (IsDataError(e) || e is IOException or UnauthorizedAccessException or OperationCanceledException)
        { return new(Failure(e), null, null, Diagnostic(e), []); }
        finally { if (temporary != null) { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } } gate.Release(); }
    }
}
