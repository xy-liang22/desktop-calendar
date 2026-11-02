using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DesktopCalendar.Core;

namespace DesktopCalendar;

internal sealed class PlannerView : Grid
{
    readonly MainWindow owner;
    internal readonly PlannerController Controller;
    internal TextBox Quick { get; } = new() { MinHeight = 30 };
    internal TodoAppearance QuickAppearance { get; }
    readonly TextBlock monthTitle, dateTitle, notice, empty;
    readonly Grid calendarHost = new();
    internal StackPanel Items { get; } = new();
    readonly Button add, quickAdd, undo, redo, reload, recover;
    readonly Dictionary<DateOnly, Button> days = [];
    readonly HashSet<Guid> collapsed = [];
    readonly HashSet<Guid> calendarExpanded = [];
    InlineTitleDraft? renderedInline;
    System.Collections.Immutable.ImmutableArray<Category> renderedCategories;
    PlannerState? rendered;
    DateOnly renderedMonth;
    bool renderedColors;
    DateOnly renderedToday;
    Point dragStart;
    Todo? dragTodo;
    bool dropping;
    internal static string T(string key) => MainWindow.Text(key);
    internal static TextBlock Text(string value, double size = 12, string color = "#EDF2F5") => new() { Text = value, FontSize = size, Foreground = PrototypeViews.Brush(color), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    internal static Button Button(string text, Action action, string? hint = null)
    {
        var b = new Button { Content = text, Padding = new Thickness(7, 5, 7, 5), ToolTip = hint ?? text, Margin = new Thickness(2) };
        AutomationProperties.SetName(b, hint ?? text); b.Click += (_, _) => action(); return b;
    }
    static Border Panel(UIElement child) => new() { Child = child, Padding = new Thickness(12), CornerRadius = new CornerRadius(12), Background = PrototypeViews.Brush("#35101A24") };
    static Grid RowGrid(params GridLength[] heights) { var g = new Grid(); foreach (var h in heights) g.RowDefinitions.Add(new() { Height = h }); return g; }
    static void At(Grid g, UIElement child, int row = 0, int col = 0) { SetRow(child, row); SetColumn(child, col); g.Children.Add(child); }
    static GridSplitter Splitter() => new() { Width = 8, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, Background = Brushes.Transparent, ResizeDirection = GridResizeDirection.Columns, ResizeBehavior = GridResizeBehavior.PreviousAndNext, ToolTip = T("SplitTip") };
    internal PlannerView(MainWindow owner, PlannerController controller, TimelineView timeline)
    {
        this.owner = owner; Controller = controller;
        Quick.Text = controller.QuickDraft; Quick.TextChanged += (_, _) => controller.QuickDraft = Quick.Text;
        foreach (var pair in new[] { (5.2, 340d), (0d, 0d), (2.7, 200d), (0d, 0d), (2.9, 220d) }) ColumnDefinitions.Add(new() { Width = pair.Item1 == 0 ? new GridLength(8) : new GridLength(pair.Item1, GridUnitType.Star), MinWidth = pair.Item2 });
        var calendar = RowGrid(GridLength.Auto, new GridLength(28), new GridLength(1, GridUnitType.Star), GridLength.Auto);
        var nav = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var navButtons = new StackPanel { Orientation = Orientation.Horizontal };
        navButtons.Children.Add(Button("‹", () => Controller.Navigate(-1), T("PlannerPrevious"))); navButtons.Children.Add(Button(T("PlannerToday"), () => Controller.Select(Controller.Today, true))); navButtons.Children.Add(Button("›", () => Controller.Navigate(1), T("PlannerNext")));
        DockPanel.SetDock(navButtons, Dock.Right); nav.Children.Add(navButtons);
        monthTitle = Text("", 20); nav.Children.Add(monthTitle); At(calendar, nav);
        var weekdays = new UniformGrid { Columns = 7 };
        foreach (var day in new[] { "PlannerMon", "PlannerTue", "PlannerWed", "PlannerThu", "PlannerFri", "PlannerSat", "PlannerSun" }) { var label = Text(T(day), 11, "#A2AFBC"); label.HorizontalAlignment = HorizontalAlignment.Center; weekdays.Children.Add(label); }
        At(calendar, weekdays, 1); At(calendar, calendarHost, 2);
        var colorMode = new CheckBox { Content = T("PlannerColorBackground"), IsChecked = Controller.ColorBackground, Margin = new Thickness(0, 8, 0, 0), FontSize = 10 };
        colorMode.Click += (_, _) => Controller.SetColors(colorMode.IsChecked == true); At(calendar, colorMode, 3);
        var todo = RowGrid(GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto);
        var heading = new StackPanel(); var toolbar = new DockPanel();
        var controls = new StackPanel { Orientation = Orientation.Horizontal }; undo = Button("↶", async () => await Controller.UndoAsync(), T("PlannerUndo")); redo = Button("↷", async () => await Controller.RedoAsync(), T("PlannerRedo")); add = Button("＋", () => Edit(null), T("PlannerNew"));
        controls.Children.Add(undo); controls.Children.Add(redo); controls.Children.Add(add); DockPanel.SetDock(controls, Dock.Right); toolbar.Children.Add(controls); toolbar.Children.Add(Text(T("TodoTitle"), 17)); heading.Children.Add(toolbar);
        dateTitle = Text("", 11, "#A3D9C4"); dateTitle.Margin = new Thickness(0, 7, 0, 10); heading.Children.Add(dateTitle); At(todo, heading);
        empty = Text(T("PlannerEmpty"), 12, "#A2AFBC"); empty.TextWrapping = TextWrapping.Wrap;
        var list = new StackPanel(); list.Children.Add(empty); list.Children.Add(Items); At(todo, new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, 1);
        var quick = new DockPanel { Margin = new Thickness(0, 8, 0, 0) }; quickAdd = Button("＋", async () => await AddQuickAsync(), T("PlannerQuickAdd")); DockPanel.SetDock(quickAdd, Dock.Right); quick.Children.Add(quickAdd); quick.Children.Add(Quick); AutomationProperties.SetName(Quick, T("PlannerQuickInput")); Quick.ToolTip = T("PlannerQuickHint");
        Quick.KeyDown += async (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; await AddQuickAsync(); } };
        var quickArea = new StackPanel();
        var options = new DockPanel(); var moreOptions = Button("⋯", MoreOptions, T("QuickMore")); DockPanel.SetDock(moreOptions, Dock.Right); options.Children.Add(moreOptions);
        QuickAppearance = new TodoAppearance(controller, controller.QuickCategory, controller.QuickColor, true); QuickAppearance.Changed += () => { controller.QuickCategory = QuickAppearance.CategoryId; controller.QuickColor = QuickAppearance.Color.Value; }; options.Children.Add(QuickAppearance);
        quickArea.Children.Add(options); quickArea.Children.Add(quick); At(todo, quickArea, 2);
        var status = new StackPanel { Margin = new Thickness(0, 8, 0, 0) }; notice = Text("", 10, "#A2AFBC"); notice.TextWrapping = TextWrapping.Wrap; status.Children.Add(notice);
        var recovery = new WrapPanel(); reload = Button(T("PlannerReload"), async () => await Controller.ReloadAsync()); recover = Button(T("PlannerRecover"), () => PlannerDialogs.Recover(owner, Controller)); recovery.Children.Add(reload); recovery.Children.Add(recover); status.Children.Add(recovery); At(todo, status, 3);
        var actual = RowGrid(GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star)); At(actual, Text(T("ActivityTitle"), 17)); var actualNote = Text(T("PlannerActivityDemo"), 10, "#A2AFBC"); actualNote.Margin = new Thickness(0, 7, 0, 10); At(actual, actualNote, 1); At(actual, timeline, 2);
        At(this, Panel(calendar)); At(this, Splitter(), 0, 1); At(this, Panel(todo), 0, 2); At(this, Splitter(), 0, 3); At(this, Panel(actual), 0, 4);
        Controller.Changed += Refresh;
        Refresh();
    }
    internal async Task<bool> AddQuickAsync()
    {
        if (!PlannerController.QuickInput(Quick.Text, out var title, out var time)) { notice.Text = T("PlannerQuickInvalid"); return false; }
        if (!Controller.Writable) return false;
        string draft = Quick.Text;
        if (!QuickAppearance.Color.Valid) { notice.Text = T("PlannerColorInvalid"); return false; }
        bool ok = await Controller.ExecuteAsync(new SaveTodo(Controller.NewTodo(title) with { PlannedTime = time, CategoryId = QuickAppearance.CategoryId, Color = QuickAppearance.Color.Value }));
        if (ok && Quick.Text == draft) Quick.Clear(); if (ok) Quick.Focus(); return ok;
    }
    internal void Edit(Todo? task, Guid? parent = null) => PlannerDialogs.Edit(owner, Controller, task, parent);
    void MoreOptions()
    {
        string raw = Quick.Text; string title = ""; TimeOnly? time = null;
        if (raw.Trim().Length > 0 && !PlannerController.QuickInput(raw, out title, out time)) { notice.Text = T("PlannerQuickInvalid"); return; }
        var draft = Controller.NewTodo(title) with { PlannedTime = time, CategoryId = QuickAppearance.CategoryId, Color = QuickAppearance.Color.Value };
        if (PlannerDialogs.Edit(owner, Controller, null, newDraft: draft) && Controller.State!.Todos.Any(t => t.Id == draft.Id) && Quick.Text == raw) Quick.Clear();
    }
    internal void FocusDate(DateOnly date) { Controller.Select(date); owner.ShowPlannerMonth(); Quick.Focus(); }
    internal void Refresh()
    {
        monthTitle.Text = Controller.Month.ToString(T("PlannerMonthFormat"), CultureInfo.CurrentCulture);
        dateTitle.Text = Controller.SelectedDate.ToString("yyyy-MM-dd dddd", CultureInfo.GetCultureInfo("zh-CN"));
        notice.Text = T(Controller.NoticeKey); notice.ToolTip = Controller.Diagnostic;
        add.IsEnabled = quickAdd.IsEnabled = Quick.IsEnabled = QuickAppearance.IsEnabled = Controller.Writable;
        if (Controller.State != null && renderedCategories != Controller.State.Categories) { QuickAppearance.Refresh(); renderedCategories = Controller.State.Categories; }
        undo.IsEnabled = Controller.CanUndo; redo.IsEnabled = Controller.CanRedo;
        reload.IsEnabled = !Controller.Busy; recover.IsEnabled = !Controller.Busy && Controller.Storage?.Status != StoreStatus.NewerSchema && Controller.Storage?.Candidates.Length > 0;
        if (rendered != Controller.State || renderedMonth != Controller.Month || renderedColors != Controller.ColorBackground || renderedToday != Controller.Today || renderedInline != Controller.Inline)
        { BuildCalendar(); rendered = Controller.State; renderedMonth = Controller.Month; renderedColors = Controller.ColorBackground; renderedToday = Controller.Today; renderedInline = Controller.Inline; }
        foreach (var day in days) day.Value.BorderBrush = PrototypeViews.Brush(day.Key == Controller.SelectedDate ? "#A3D9C4" : day.Key == Controller.Today ? "#607C9DB0" : "#00FFFFFF");
        Items.Children.Clear(); var rows = Controller.Tree(Controller.SelectedDate); empty.Visibility = rows.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        empty.Text = Controller.Storage?.CanWrite == true ? T("PlannerEmpty") : T(Controller.NoticeKey);
        foreach (var node in rows) AddNode(Items, node, 0);
        // Calendar checkboxes do not optimistically mutate data while a write is pending.
        calendarHost.IsEnabled = !Controller.Busy && !Controller.Stopping;
    }
    internal static DateOnly?[] MonthDates(DateOnly month)
    {
        var first = new DateOnly(month.Year, month.Month, 1); int offset = ((int)first.DayOfWeek + 6) % 7;
        int count = ((offset + DateTime.DaysInMonth(first.Year, first.Month) + 6) / 7) * 7;
        return Enumerable.Range(0, count).Select(i => first.DayNumber + i - offset).Select(n => n < 0 || n > DateOnly.MaxValue.DayNumber ? (DateOnly?)null : DateOnly.FromDayNumber(n)).ToArray();
    }
    void BuildCalendar()
    {
        calendarHost.Children.Clear(); days.Clear(); var dates = MonthDates(Controller.Month); var grid = new UniformGrid { Columns = 7, Rows = dates.Length / 7 };
        foreach (var value in dates)
        {
            if (value is not DateOnly date) { grid.Children.Add(new Border()); continue; }
            var tasks = Controller.On(date);
            var entries = new List<CalendarTodoEntry>();
            void AddCalendar(TodoDateNode node, int depth)
            {
                if (node.ContextOnly) { foreach (var child in node.Children) AddCalendar(child, depth); return; }
                var t = node.Value;
                entries.Add(new(t.Title, Prefix(t), t.Status == TodoStatus.Completed, Controller.Color(t), t.Important || t.Urgent,
                    async () => { if (Controller.Writable) await Controller.ExecuteAsync(new SetTodoCompleted(TodoRef.Of(t), t.Status != TodoStatus.Completed)); },
                    t.Id, t.ParentId, depth, node.Children.Length > 0, calendarExpanded.Contains(t.Id),
                    () => { if (!calendarExpanded.Add(t.Id)) calendarExpanded.Remove(t.Id); renderedMonth = default; Refresh(); },
                    t.Kind == TodoKind.Once ? () => Controller.BeginInline(t) : null,
                    Controller.Inline?.Id == t.Id ? Controller.Inline : null, Controller, Controller.Writable));
                if (calendarExpanded.Contains(t.Id)) foreach (var child in node.Children) AddCalendar(child, depth + 1);
            }
            foreach (var node in Controller.Tree(date)) AddCalendar(node, 0);
            var cell = new CalendarPreviewCell(date, entries.ToArray(), date.Month == Controller.Month.Month, Controller.ColorBackground, () => FocusDate(date));
            string? decoration = Controller.State?.Decorations.FirstOrDefault(d => d.Date == date)?.Color;
            var button = new Button { Content = cell, Tag = date, Padding = new Thickness(0), Margin = new Thickness(2), BorderThickness = new Thickness(1), Background = PrototypeViews.Brush(decoration == null ? date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? "#162A3D48" : "#0BFFFFFF" : "#38" + decoration.TrimStart('#')[^6..]), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch, ClipToBounds = true, ToolTip = date.ToString("yyyy-MM-dd") + "\n" + string.Join("\n", tasks.Select(t => Prefix(t) + t.Title)) };
            // Preserve the button through selection changes, so the second click can be recognized.
            button.Click += (_, e) => { if (ReferenceEquals(e.Source, button)) Controller.Select(date); };
            button.MouseDoubleClick += (_, e) => { if (!InputOrigin(e.OriginalSource as DependencyObject, button)) { e.Handled = true; FocusDate(date); } };
            var menu = new ContextMenu(); var colorItem = new MenuItem { Header = T("PlannerDateColor") }; colorItem.Click += (_, _) => PlannerDialogs.Decoration(owner, Controller, date); menu.Items.Add(colorItem); button.ContextMenu = menu;
            AutomationProperties.SetName(button, date.ToString("yyyy-MM-dd")); days.Add(date, button); grid.Children.Add(button);
        }
        calendarHost.Children.Add(grid);
    }
    internal static string Prefix(Todo t) => t.PlannedTime is TimeOnly time ? time.ToString("HH:mm", CultureInfo.InvariantCulture) + " " : "";
    static bool InputOrigin(DependencyObject? origin, DependencyObject stop)
    {
        for (var input = origin; input != null && input != stop; input = input is FrameworkContentElement content ? content.Parent : VisualTreeHelper.GetParent(input)) if (input is ButtonBase or TextBoxBase or ComboBox || input is FrameworkElement element && element.Tag is CalendarTodoEntry) return true;
        return false;
    }
    void AddNode(Panel parent, TodoDateNode node, int depth)
    {
        var task = node.Value;
        var outer = new Border { Padding = new Thickness(7, 7, 6, 8), Margin = new Thickness(Math.Min(depth, 6) * 12, 0, 0, 6), BorderThickness = new Thickness(1), BorderBrush = ThemeColors.Brush("#304352"), Background = ThemeColors.Brush("#151E2A"), CornerRadius = new CornerRadius(7), AllowDrop = true, Tag = task };
        var row = new Grid(); row.ColumnDefinitions.Add(new() { Width = new GridLength(4) }); row.ColumnDefinitions.Add(new() { Width = new GridLength(26) }); row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        At(row, new Border { Background = ThemeColors.Brush(ThemeColors.Accent(Controller.Color(task))), CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 2, 0, 2) });
        var check = new CheckBox { IsChecked = task.Status == TodoStatus.Completed, IsEnabled = Controller.Writable && !node.ContextOnly, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(6, 2, 3, 0) }; AutomationProperties.SetName(check, T("PlannerToggle") + " " + task.Title);
        check.Click += async (_, e) => { e.Handled = true; bool complete = check.IsChecked == true; check.IsChecked = task.Status == TodoStatus.Completed; await Controller.ExecuteAsync(new SetTodoCompleted(TodoRef.Of(task), complete)); }; At(row, check, 0, 1);
        var content = new StackPanel(); var line = new TextBlock { TextWrapping = TextWrapping.Wrap, TextDecorations = task.Status == TodoStatus.Completed ? TextDecorations.Strikethrough : null, Opacity = task.Status == TodoStatus.Completed ? .55 : 1 };
        line.Inlines.Add(new System.Windows.Documents.Run(Prefix(task)) { FontWeight = FontWeights.SemiBold, Foreground = PrototypeViews.Brush("#A3D9C4") }); line.Inlines.Add(new System.Windows.Documents.Run(task.Title) { FontWeight = task.Important || task.Urgent ? FontWeights.SemiBold : FontWeights.Normal }); content.Children.Add(line);
        string meta = string.Join(" · ", new[] { Controller.State?.Categories.FirstOrDefault(c => c.Id == task.CategoryId)?.Name, task.Important ? T("PlannerImportant") : null, task.Urgent ? T("PlannerUrgent") : null, task.Kind != TodoKind.Once ? T("PlannerAdvancedTask") : null }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (task.CompletionIntent == CompletionIntent.ManualIncomplete && node.Children.Length > 0) meta += (meta.Length > 0 ? " · " : "") + T("ParentPendingOverride");
        if (task.CompletionSnapshotInvalidated) meta += (meta.Length > 0 ? " · " : "") + T("ParentSnapshotInvalidated");
        if (meta.Length > 0) content.Children.Add(Text(meta, 10, "#A2AFBC"));
        decimal progress = Controller.State == null ? 0 : PlannerQueries.Progress(Controller.State, TodoRef.Of(task));
        if (progress > 0 || node.Children.Length > 0) { content.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = (double)progress, Height = 3, Margin = new Thickness(0, 5, 0, 3), Foreground = PrototypeViews.Brush("#A3D9C4"), Background = PrototypeViews.Brush("#263B45"), BorderThickness = new Thickness(0) }); content.Children.Add(Text(progress.ToString("0.#", CultureInfo.CurrentCulture) + "%", 10, "#A2AFBC")); }
        At(row, content, 0, 2);
        var menu = new ContextMenu();
        void Action(string key, Action handler, bool enabled = true) { var item = new MenuItem { Header = T(key), IsEnabled = enabled && Controller.Writable }; item.Click += (_, _) => handler(); menu.Items.Add(item); }
        Action("PlannerEdit", () => Edit(task), task.Kind == TodoKind.Once);
        Action("PlannerAddChild", () => Edit(null, task.Id), task.Kind == TodoKind.Once);
        Action("PlannerCopy", () => PlannerDialogs.Copy(owner, Controller, task), task.Kind == TodoKind.Once);
        Action("PlannerDelete", () => PlannerDialogs.Delete(owner, Controller, task), task.Kind == TodoKind.Once);
        Action("PlannerTimeOrder", async () => await Controller.ExecuteAsync(new SetTodoOrderMode(task.ParentId, task.ParentId == null ? Controller.SelectedDate : null, TodoSort.PlannedTime)));
        if (node.Children.Length > 0) Action(collapsed.Contains(task.Id) ? "PlannerExpandChildren" : "PlannerCollapseChildren", () => { if (!collapsed.Add(task.Id)) collapsed.Remove(task.Id); Refresh(); });
        var options = Button("⋯", () => { menu.PlacementTarget = outer; menu.IsOpen = true; }, T("PlannerTaskActions")); options.Padding = new Thickness(4, 2, 4, 2); options.VerticalAlignment = VerticalAlignment.Top; At(row, options, 0, 3); outer.ContextMenu = menu;
        outer.Child = row; parent.Children.Add(outer);
        content.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2 && task.Kind == TodoKind.Once) { e.Handled = true; Edit(task); } };
        outer.PreviewMouseLeftButtonDown += (_, e) => { dragStart = e.GetPosition(outer); dragTodo = task; };
        outer.PreviewMouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || dragTodo?.Id != task.Id || !Controller.Writable || node.ContextOnly || task.Kind != TodoKind.Once) return;
            if (InputOrigin(e.OriginalSource as DependencyObject, outer)) return;
            var p = e.GetPosition(outer); if (Math.Abs(p.X - dragStart.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(p.Y - dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            dragTodo = null; try { DragDrop.DoDragDrop(outer, new DataObject("DesktopCalendar.Todo", task.Id.ToString("D")), DragDropEffects.Move); }
            finally { foreach (var item in Items.Children.OfType<Border>()) ResetDrop(item); }
        };
        outer.DragOver += (_, e) => { bool valid = CanDrop(e, task, out var ignored); e.Effects = valid ? DragDropEffects.Move : DragDropEffects.None; outer.BorderThickness = new Thickness(0, e.GetPosition(outer).Y < outer.ActualHeight / 2 ? 2 : 0, 0, e.GetPosition(outer).Y >= outer.ActualHeight / 2 ? 2 : 0); outer.BorderBrush = valid ? PrototypeViews.Brush("#A3D9C4") : Brushes.Transparent; e.Handled = true; };
        outer.DragLeave += (_, _) => ResetDrop(outer);
        outer.Drop += async (_, e) => { e.Handled = true; ResetDrop(outer); if (CanDrop(e, task, out var source)) await DropAsync(source!, task, e.GetPosition(outer).Y >= outer.ActualHeight / 2); };
        if (!collapsed.Contains(task.Id)) foreach (var child in node.Children) AddNode(parent, child, depth + 1);
    }
    static void ResetDrop(Border row) { row.BorderThickness = new Thickness(1); row.BorderBrush = ThemeColors.Brush("#304352"); }
    bool CanDrop(DragEventArgs e, Todo target, out Todo? source)
    {
        source = DragId(e.Data) is Guid id ? Controller.State?.Todos.FirstOrDefault(t => t.Id == id) : null;
        return !dropping && Controller.Writable && source != null && source.Id != target.Id && source.Kind == TodoKind.Once && target.Kind == TodoKind.Once && source.ParentId == target.ParentId && source.Status == target.Status && (source.ParentId != null || source.PlannedDate == target.PlannedDate && source.PlannedDate == Controller.SelectedDate);
    }
    internal static Guid? DragId(IDataObject data)
    {
        // Only an inert canonical ID crosses OLE; never serialize a model or arbitrary object.
        try { if (data.GetDataPresent("DesktopCalendar.Todo", false) && data.GetData("DesktopCalendar.Todo", false) is string text && Guid.TryParseExact(text, "D", out var id)) return id; }
        catch (Exception e) when (e is System.Runtime.InteropServices.COMException or NotSupportedException or System.Runtime.Serialization.SerializationException) { }
        return null;
    }
    internal async Task<bool> DropAsync(Todo source, Todo target, bool after)
    {
        var actualSource = Controller.State?.Todos.SingleOrDefault(t => t.Id == source.Id); var actualTarget = Controller.State?.Todos.SingleOrDefault(t => t.Id == target.Id);
        if (actualSource == null || actualTarget == null) return false; source = actualSource; target = actualTarget;
        if (dropping || !Controller.Writable || source.ParentId != target.ParentId || source.Status != target.Status || source.Kind != TodoKind.Once || target.Kind != TodoKind.Once) return false;
        var siblings = source.ParentId is Guid parent ? PlannerQueries.Children(Controller.State!, Controller.State!.Todos.Single(t => t.Id == parent)).ToList() : Controller.On(Controller.SelectedDate).Where(t => t.ParentId == null).ToList();
        if (siblings.Any(t => t.Kind != TodoKind.Once)) return false;
        siblings.RemoveAll(t => t.Id == source.Id); int index = siblings.FindIndex(t => t.Id == target.Id); if (index < 0) return false; siblings.Insert(index + (after ? 1 : 0), source);
        dropping = true; try { return await Controller.ExecuteAsync(new ReorderTodos([.. siblings.Select(TodoRef.Of)], Controller.SelectedDate)); } finally { dropping = false; }
    }
    internal void Detach() => Controller.Changed -= Refresh;
}
