namespace DesktopCalendar.Core;

public sealed record CommandResult(StoreStatus Status, string Diagnostic, Guid? EntityId = null)
{
    public bool Success => Status == StoreStatus.Ready;
}

// Every successful operation is durably committed before becoming observable in memory.
// One queue per workspace + cross-process file lease + content token prevent lost/late writes.
public sealed class PlannerWorkspace
{
    readonly PlannerStore store;
    readonly Func<DateTimeOffset> clock;
    readonly SemaphoreSlim gate = new(1, 1);
    readonly List<PlannerState> undo = [], redo = [];
    volatile StoreLoad loaded;
    public PlannerState? Snapshot => loaded.State;
    public StoreLoad Storage => loaded;
    public bool CanUndo => undo.Count > 0 && loaded.CanWrite;
    public bool CanRedo => redo.Count > 0 && loaded.CanWrite;
    PlannerWorkspace(PlannerStore store, StoreLoad loaded, Func<DateTimeOffset>? clock)
    { this.store = store; this.loaded = loaded; this.clock = clock ?? (() => DateTimeOffset.UtcNow); }
    public static async Task<PlannerWorkspace> OpenAsync(PlannerStore store, PlannerOptions? options = null, Func<DateTimeOffset>? clock = null, CancellationToken ct = default)
        => new(store, await store.OpenAsync(options, ct).ConfigureAwait(false), clock);
    public async Task<StoreLoad> ReloadAsync(PlannerOptions? options = null, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try { loaded = await store.OpenAsync(options, ct).ConfigureAwait(false); undo.Clear(); redo.Clear(); return loaded; }
        finally { gate.Release(); }
    }
    public async Task<StoreLoad> RecoverAsync(RecoveryCandidate candidate, bool confirmed, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try { var result = await store.RecoverAsync(candidate, confirmed, ct).ConfigureAwait(false); if (result.CanWrite) { loaded = result; undo.Clear(); redo.Clear(); } return result; }
        finally { gate.Release(); }
    }
    async Task<CommandResult> Commit(PlannerState next, Guid? entity, CancellationToken ct)
    {
        var before = loaded.State!;
        next = next with { Id = before.Id, Revision = checked(before.Revision + 1), Options = before.Options };
        PlannerIntegrity.Validate(next);
        var result = await store.CommitAsync(next, loaded.Fingerprint, ct).ConfigureAwait(false);
        if (result.Success) loaded = new(StoreStatus.Ready, next, result.Fingerprint, result.Diagnostic, []);
        else if (result.Status is StoreStatus.Conflict or StoreStatus.NewerSchema) loaded = loaded with { Status = result.Status, Diagnostic = result.Diagnostic };
        return new(result.Status, result.Diagnostic, entity);
    }
    public async Task<CommandResult> ExecuteAsync(PlannerCommand command, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!loaded.CanWrite || loaded.State == null) return new(loaded.Status, "StorageNotWritable");
            var before = loaded.State;
            var change = PlannerCommands.Apply(before, command, clock().ToUniversalTime());
            var result = await Commit(change.State, change.EntityId, ct).ConfigureAwait(false);
            if (result.Success) { undo.Add(before); if (undo.Count > 100) undo.RemoveAt(0); redo.Clear(); }
            return result;
        }
        catch (PlannerException e) { return new(StoreStatus.InvalidData, e.Code); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or NullReferenceException or OverflowException)
        { return new(StoreStatus.InvalidData, "InvalidCommand:" + e.GetType().Name); }
        finally { gate.Release(); }
    }
    public Task<CommandResult> UndoAsync(CancellationToken ct = default) => History(false, ct);
    public Task<CommandResult> RedoAsync(CancellationToken ct = default) => History(true, ct);
    async Task<CommandResult> History(bool forward, CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var source = forward ? redo : undo; var destination = forward ? undo : redo;
            if (!loaded.CanWrite || source.Count == 0) return new(StoreStatus.InvalidData, "NoHistory");
            var before = loaded.State!;
            var result = await Commit(source[^1], null, ct).ConfigureAwait(false);
            if (result.Success) { destination.Add(before); source.RemoveAt(source.Count - 1); }
            return result;
        }
        catch (PlannerException e) { return new(StoreStatus.InvalidData, e.Code); }
        finally { gate.Release(); }
    }
}
