using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;
using DesktopCalendar.Core;

namespace DesktopCalendar;

// One workspace per app session, shared by month, selected day and compact views.
// UI drafts never modify a snapshot. All observed changes follow a successful commit.
internal sealed class PlannerController
{
    readonly PlannerStore store;
    readonly DispatcherTimer todayTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    PlannerWorkspace? workspace;
    DateOnly lastToday;
    internal PlannerState? State => workspace?.Snapshot;
    internal StoreLoad? Storage => workspace?.Storage;
    internal Task Pending { get; private set; } = Task.CompletedTask;
    internal bool Busy { get; private set; } = true;
    internal bool Stopping { get; private set; }
    internal bool Writable => !Busy && !Stopping && Storage?.CanWrite == true;
    internal bool CanUndo => Writable && workspace?.CanUndo == true;
    internal bool CanRedo => Writable && workspace?.CanRedo == true;
    internal DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    internal DateOnly SelectedDate { get; private set; }
    internal DateOnly Month { get; private set; }
    internal bool ColorBackground { get; private set; }
    internal string QuickDraft { get; set; } = ""; // Unsubmitted UI text; never treated as saved planner data.
    internal Guid? QuickCategory { get; set; }
    internal string? QuickColor { get; set; }
    internal InlineTitleDraft? Inline { get; private set; }
    internal string NoticeKey { get; private set; } = "PlannerLoading";
    internal string Diagnostic { get; private set; } = "";
    internal string FilePath => store.FilePath;
    internal event Action? Changed;
    internal event Action? PreferencesChanged;
    internal PlannerController(string path, WindowPreferences preferences, StoreOptions? options = null)
    {
        store = new(path, options); lastToday = Today;
        SelectedDate = preferences.PlannerDate ?? Today;
        Month = preferences.PlannerMonth ?? new(SelectedDate.Year, SelectedDate.Month, 1);
        Month = new(Month.Year, Month.Month, 1); ColorBackground = preferences.CalendarColorBackground;
        todayTimer.Tick += (_, _) => { if (lastToday != Today) { lastToday = Today; Changed?.Invoke(); } };
    }
    internal Task InitializeAsync() => Pending = InitializeCore();
    async Task InitializeCore()
    {
        try
        {
            workspace = await PlannerWorkspace.OpenAsync(store, new() { TimeZoneId = TimeZoneInfo.Local.Id });
            if (workspace.Storage.Status == StoreStatus.Missing)
            {
                var result = await workspace.ExecuteAsync(new InitializeCategories());
                if (!result.Success) { Diagnostic = result.Diagnostic; NoticeKey = "PlannerSaveFailed"; return; }
            }
            SetStorageNotice();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or PlannerException)
        { Diagnostic = ex.GetType().Name; NoticeKey = "PlannerReadFailed"; }
        finally { Busy = false; todayTimer.Start(); Changed?.Invoke(); }
    }
    void SetStorageNotice()
    {
        Diagnostic = Storage?.Diagnostic ?? "";
        NoticeKey = Storage?.CanWrite == true ? "PlannerLocal" : Storage?.Status switch
        { StoreStatus.NewerSchema => "PlannerNewer", StoreStatus.RecoveryRequired => "PlannerCorrupt", StoreStatus.Conflict => "PlannerConflict", _ => "PlannerReadFailed" };
    }
    internal void Select(DateOnly date, bool revealMonth = false)
    {
        SelectedDate = date; if (revealMonth) Month = new(date.Year, date.Month, 1);
        Changed?.Invoke(); PreferencesChanged?.Invoke();
    }
    internal void Navigate(int months)
    {
        int absolute = (Month.Year - 1) * 12 + Month.Month - 1 + months;
        if (absolute is < 0 or >= 119988) return;
        Month = Month.AddMonths(months);
        SelectedDate = new(Month.Year, Month.Month, Math.Min(SelectedDate.Day, DateTime.DaysInMonth(Month.Year, Month.Month)));
        Changed?.Invoke(); PreferencesChanged?.Invoke();
    }
    internal void SetColors(bool background) { ColorBackground = background; Changed?.Invoke(); PreferencesChanged?.Invoke(); }
    internal string Color(Todo todo) => todo.Color ?? State?.Categories.FirstOrDefault(c => c.Id == todo.CategoryId)?.Color ?? "#A3D9C4";
    internal ImmutableArray<Todo> On(DateOnly date) => State == null ? [] : PlannerQueries.TodosOn(State, date, Today);
    internal ImmutableArray<TodoDateNode> Tree(DateOnly date) => State == null ? [] : PlannerQueries.TodoTreeOn(State, date, Today);
    internal Task<bool> ExecuteAsync(PlannerCommand command) => Start(() => workspace!.ExecuteAsync(command));
    internal Task<bool> UndoAsync() => Start(() => workspace!.UndoAsync());
    internal Task<bool> RedoAsync() => Start(() => workspace!.RedoAsync());
    Task<bool> Start(Func<Task<CommandResult>> action)
    {
        if (!Writable) return Task.FromResult(false);
        var task = Run(action); Pending = task; return task;
    }
    async Task<bool> Run(Func<Task<CommandResult>> action)
    {
        Busy = true; NoticeKey = "PlannerSaving"; Changed?.Invoke();
        try
        {
            var result = await action();
            if (result.Success) { NoticeKey = "PlannerLocal"; Diagnostic = ""; return true; }
            Diagnostic = result.Diagnostic;
            NoticeKey = result.Status switch { StoreStatus.Conflict => "PlannerConflict", StoreStatus.NewerSchema => "PlannerNewer", _ => "PlannerSaveFailed" };
            return false;
        }
        finally { Busy = false; Changed?.Invoke(); }
    }
    internal Task ReloadAsync() { if (Busy || Stopping) return Task.CompletedTask; return Pending = ReloadCore(); }
    async Task ReloadCore()
    {
        Busy = true; Changed?.Invoke();
        try
        {
            if (workspace == null) { workspace = await PlannerWorkspace.OpenAsync(store); }
            else await workspace.ReloadAsync();
            SetStorageNotice();
        }
        finally { Busy = false; Changed?.Invoke(); }
    }
    internal Task<bool> RecoverAsync(RecoveryCandidate candidate)
    {
        if (Busy || Stopping || workspace == null || Storage?.Status == StoreStatus.NewerSchema) return Task.FromResult(false);
        var task = RecoverCore(candidate); Pending = task; return task;
    }
    async Task<bool> RecoverCore(RecoveryCandidate candidate)
    {
        Busy = true; Changed?.Invoke();
        try { var result = await workspace!.RecoverAsync(candidate, true); SetStorageNotice(); if (!result.CanWrite) { NoticeKey = "PlannerReadFailed"; Diagnostic = result.Diagnostic; } return result.CanWrite; }
        finally { Busy = false; Changed?.Invoke(); }
    }
    internal async Task StopAsync() { Stopping = true; todayTimer.Stop(); await Pending; }
    internal void Dispose() { Stopping = true; todayTimer.Stop(); }
    internal Todo NewTodo(string title, Guid? parent = null) => new()
    {
        Id = Guid.NewGuid(), Title = title.Trim(), PlannedDate = SelectedDate, ParentId = parent,
        SortKey = (State?.Todos.Where(t => t.ParentId == parent && (parent != null || t.PlannedDate == SelectedDate)).Select(t => t.SortKey).DefaultIfEmpty(-1024).Max() ?? -1024) + 1024
    };
    internal static bool QuickInput(string value, out string title, out TimeOnly? time)
        => TodoDraftRules.Quick(value, out title, out time);
    internal async void BeginInline(Todo todo)
    {
        if (!Writable || todo.Kind != TodoKind.Once || Inline?.Id == todo.Id) return;
        if (Inline != null && !await CommitInlineAsync()) return;
        Inline = new(todo.Id, todo.Title); Changed?.Invoke();
    }
    internal void CancelInline() { Inline = null; Changed?.Invoke(); }
    internal async Task<bool> CommitInlineAsync()
    {
        var draft = Inline; if (draft == null) return true; if (draft.Saving) return false;
        if (!TodoDraftRules.Title(draft.Text)) { draft.Error = MainWindow.Text("PlannerTitleRequired"); draft.Notify(); return false; }
        if (draft.Text.Trim() == draft.Original) { CancelInline(); return true; }
        draft.Saving = true;
        try
        {
            bool ok = await ExecuteAsync(new RenameTodo(new(draft.Id), draft.Text.Trim(), draft.Original));
            if (ok) { if (Inline == draft) Inline = null; Changed?.Invoke(); }
            else { draft.Error = MainWindow.Text(NoticeKey); draft.Notify(); }
            return ok;
        }
        finally { draft.Saving = false; }
    }
}
internal sealed class InlineTitleDraft(Guid id, string original)
{
    internal Guid Id { get; } = id;
    internal string Original { get; } = original;
    internal string Text { get; set; } = original;
    internal string Error { get; set; } = "";
    internal bool Saving { get; set; }
    internal event Action? Changed;
    internal void Notify() => Changed?.Invoke();
}
