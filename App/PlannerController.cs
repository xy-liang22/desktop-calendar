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
    readonly bool maintenanceBlocked;
    DateOnly lastToday;
    internal PlannerState? State => workspace?.Snapshot;
    internal StoreLoad? Storage => workspace?.Storage;
    internal Task Pending { get; private set; } = Task.CompletedTask;
    internal bool Busy { get; private set; } = true;
    internal bool Stopping { get; private set; }
    internal bool MaintenancePending => maintenanceBlocked || System.IO.File.Exists(System.IO.Path.Combine(DataDirectory, "restore-journal.json"));
    internal bool Writable => !Busy && !Stopping && !MaintenancePending && Storage?.CanWrite == true;
    internal bool CanUndo => Writable && workspace?.CanUndo == true;
    internal bool CanRedo => Writable && workspace?.CanRedo == true;
    internal DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    internal DateOnly SelectedDate { get; private set; }
    internal DateOnly Month { get; private set; }
    internal bool ColorBackground { get; private set; }
    internal CalendarTextMode TextMode { get; private set; }
    internal DayOfWeek WeekStart { get; private set; }
    internal WeekPlanLayout WeekLayout { get; private set; }
    internal bool CollapseCompletedChildren { get; private set; }
    internal int ChildDisplayRevision { get; private set; }
    readonly System.Collections.Generic.Dictionary<Guid, bool> completedChildren = new();
    internal bool CompletedCollapsed(Guid parent) => completedChildren.TryGetValue(parent, out bool value) ? value : CollapseCompletedChildren;
    internal void ToggleCompletedChildren(Guid parent) { completedChildren[parent] = !CompletedCollapsed(parent); ChildDisplayRevision++; Changed?.Invoke(); }
    internal void SetCompletedChildren(bool collapse) { CollapseCompletedChildren = collapse; completedChildren.Clear(); ChildDisplayRevision++; Changed?.Invoke(); PreferencesChanged?.Invoke(); }
    internal int CompletedChildCount(TodoDateNode node) => node.Children.Count(c => c.Value.Status == TodoStatus.Completed && !c.ContextOnly);
    internal TodoDateNode[] DisplayChildren(TodoDateNode node) => node.Children.Where(c => !CompletedCollapsed(node.Value.Id) || c.Value.Status != TodoStatus.Completed || c.ContextOnly).ToArray();
    PlannerOptions Options => new() { TimeZoneId = TimeZoneInfo.Local.Id, WeekStart = WeekStart, Language = Localization.English ? "en" : "zh-CN" };
    internal WeekKey Week(DateOnly? date = null) => PlannerTime.Week(date ?? SelectedDate, WeekStart, State?.Options.TimeZoneId ?? TimeZoneInfo.Local.Id);
    internal void SetWeekLayout(WeekPlanLayout layout) { if (!Enum.IsDefined(layout) || WeekLayout == layout) return; WeekLayout = layout; Changed?.Invoke(); PreferencesChanged?.Invoke(); }
    internal Task SetWeekStartAsync(DayOfWeek day)
    { if (!Enum.IsDefined(day) || day == WeekStart || Busy) return Task.CompletedTask; return Pending = ChangeWeekStart(day); }
    async Task ChangeWeekStart(DayOfWeek day)
    {
        Busy = true; Changed?.Invoke();
        try { if (workspace != null) await workspace.ConfigureAsync(Options with { WeekStart = day }); WeekStart = day; }
        finally { Busy = false; Changed?.Invoke(); PreferencesChanged?.Invoke(); }
    }
    internal string QuickDraft { get; set; } = ""; // Unsubmitted UI text; never treated as saved planner data.
    internal Guid? QuickCategory { get; set; }
    internal string? QuickColor { get; set; }
    internal InlineTitleDraft? Inline { get; private set; }
    internal string NoticeKey { get; private set; } = "PlannerLoading";
    internal string Diagnostic { get; private set; } = "";
    internal string FilePath => store.FilePath;
    internal string DataDirectory => System.IO.Path.GetDirectoryName(FilePath)!;
    internal event Action? Changed;
    internal event Action? PreferencesChanged;
    internal PlannerController(string path, WindowPreferences preferences, StoreOptions? options = null, bool maintenanceBlocked = false)
    {
        this.maintenanceBlocked = maintenanceBlocked; store = new(path, options); lastToday = Today; WeekStart = preferences.WeekStart; WeekLayout = preferences.WeekPlanLayout; CollapseCompletedChildren = preferences.CollapseCompletedChildren;
        SelectedDate = preferences.PlannerDate ?? Today;
        Month = preferences.PlannerMonth ?? new(SelectedDate.Year, SelectedDate.Month, 1);
        Month = new(Month.Year, Month.Month, 1); ColorBackground = preferences.CalendarColorBackground; TextMode = preferences.CalendarTextMode;
        todayTimer.Tick += (_, _) => { if (lastToday != Today) { lastToday = Today; Changed?.Invoke(); } };
    }
    internal Task InitializeAsync() => Pending = InitializeCore();
    async Task InitializeCore()
    {
        try
        {
            if (maintenanceBlocked) { Diagnostic = "PendingRestoreRecoveryBlocked"; NoticeKey = "PlannerReadFailed"; return; }
            workspace = await PlannerWorkspace.OpenAsync(store, Options);
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
    internal void Report(string key, string diagnostic = "") { NoticeKey = key; Diagnostic = diagnostic; Changed?.Invoke(); }
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
            if (MaintenancePending) { NoticeKey = "PlannerReadFailed"; Diagnostic = "PendingRestoreRecoveryBlocked"; return; }
            if (workspace == null) { workspace = await PlannerWorkspace.OpenAsync(store, Options); }
            else await workspace.ReloadAsync(Options);
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
        try { var result = await workspace!.RecoverAsync(candidate, true); if (result.CanWrite) await workspace.ConfigureAsync(Options); SetStorageNotice(); if (!result.CanWrite) { NoticeKey = "PlannerReadFailed"; Diagnostic = result.Diagnostic; } return result.CanWrite; }
        finally { Busy = false; Changed?.Invoke(); }
    }
    internal async Task StopAsync() { Stopping = true; todayTimer.Stop(); await Pending; }
    internal Task<T> MaintenanceAsync<T>(Func<T> operation)
    {
        if (Busy || Stopping || workspace == null) throw new InvalidOperationException("PlannerBusy");
        var task = Maintain(operation); Pending = ObserveMaintenance(task); return task;
    }
    static async Task ObserveMaintenance(Task task) { try { await task; } catch { /* The operation caller receives the original failure; lifecycle waiting must still complete. */ } }
    async Task<T> Maintain<T>(Func<T> operation)
    {
        Busy = true; Changed?.Invoke();
        try { return await workspace!.MaintenanceAsync(() => Task.Run(operation)); }
        finally { Busy = false; if (MaintenancePending) { NoticeKey = "PlannerReadFailed"; Diagnostic = "PendingRestoreRecoveryBlocked"; } Changed?.Invoke(); }
    }
    internal async Task RestorePreferencesAsync(WindowPreferences p)
    {
        WeekStart = p.WeekStart; WeekLayout = p.WeekPlanLayout; TextMode = p.CalendarTextMode; ColorBackground = p.CalendarColorBackground; CollapseCompletedChildren = p.CollapseCompletedChildren; completedChildren.Clear();
        SelectedDate = p.PlannerDate ?? Today; Month = p.PlannerMonth ?? new(SelectedDate.Year, SelectedDate.Month, 1);
        await ReloadAsync();
    }
    internal void Dispose() { Stopping = true; todayTimer.Stop(); }
    internal void SetTextMode(CalendarTextMode mode) { if (!Enum.IsDefined(mode) || TextMode == mode) return; TextMode = mode; Changed?.Invoke(); PreferencesChanged?.Invoke(); }
    internal Todo? FindTodo(Guid id) => State?.Todos.FirstOrDefault(t => t.Id == id) ?? On(SelectedDate).FirstOrDefault(t => t.Id == id) ?? On(Today).FirstOrDefault(t => t.Id == id);
    internal Todo NewTodo(string title, Guid? parent = null)
    {
        var ancestor = parent == null ? null : FindTodo(parent.Value);
        return new() { Id = Guid.NewGuid(), Title = title.Trim(), PlannedDate = SelectedDate, ParentId = parent, CategoryId = ancestor?.CategoryId, Color = ancestor?.Color,
            SortKey = (ancestor != null && State != null ? PlannerQueries.Children(State, ancestor).Select(t => t.SortKey).DefaultIfEmpty(-1024).Max() : State?.Todos.Where(t => t.ParentId == parent && t.PlannedDate == SelectedDate).Select(t => t.SortKey).DefaultIfEmpty(-1024).Max() ?? -1024) + 1024 };
    }
    internal static bool QuickInput(string value, out string title, out TimeOnly? time)
        => TodoDraftRules.Quick(value, out title, out time);
    internal async void BeginInline(Todo todo, DateOnly? displayedDate = null)
    {
        var date = displayedDate ?? todo.PlannedDate;
        if (!Writable || todo.Status == TodoStatus.Cancelled || todo.Kind == TodoKind.SeriesTemplate || Inline?.Id == todo.Id && Inline.DisplayDate == date) return;
        if (Inline != null && !await CommitInlineAsync()) return;
        Inline = new(TodoRef.Of(todo), todo.Title, date); Changed?.Invoke();
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
            bool ok = await ExecuteAsync(new RenameTodo(draft.Reference, draft.Text.Trim(), draft.Original));
            if (ok) { if (Inline == draft) Inline = null; Changed?.Invoke(); }
            else { draft.Error = MainWindow.Text(NoticeKey); draft.Notify(); }
            return ok;
        }
        finally { draft.Saving = false; }
    }
}
internal sealed class InlineTitleDraft(TodoRef reference, string original, DateOnly displayedDate)
{
    internal TodoRef Reference { get; } = reference;
    internal Guid Id => Reference.Id;
    internal DateOnly DisplayDate { get; } = displayedDate;
    internal string Original { get; } = original;
    internal string Text { get; set; } = original;
    internal string Error { get; set; } = "";
    internal bool Saving { get; set; }
    internal event Action? Changed;
    internal void Notify() => Changed?.Invoke();
}
