using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopCalendar.Core;

namespace DesktopCalendar;

internal static partial class PlannerDialogs
{
    static string T(string key) => MainWindow.Text(key);
    internal sealed class Sheet : Window
    {
        bool saving;
        bool allowClose;
        Func<Task<bool>>? saveAction;
        readonly ImeInput ime;
        bool editorModeless, outsidePending, outsideByPointer;
        bool outsideReady;
        int auxiliaryDepth;
        internal IDisposable HoldOpen() { auxiliaryDepth++; return new Auxiliary(() => auxiliaryDepth--); }
        sealed class Auxiliary(Action release) : IDisposable { Action? pending = release; public void Dispose() { pending?.Invoke(); pending = null; } }
        internal bool? Result { get; private set; }
        internal bool AutoSaveClose { get; private set; }
        internal bool CloseWithoutChanges { get; private set; }
        readonly Button closeButton;
        internal StackPanel Body { get; } = new();
        internal TextBlock Error { get; } = PlannerView.Text("", 11, "#E3BF89");
        internal StackPanel Actions { get; } = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        internal Sheet(string title, double width = 430)
        {
            Foreground = ThemeColors.Brush("#EDF2F5");
            Title = title; Language = System.Windows.Markup.XmlLanguage.GetLanguage(T("PlannerUiLanguage")); Width = width; MaxHeight = 740; SizeToContent = SizeToContent.Height; MinHeight = 160; MinWidth = 320; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize;
            var root = new Grid(); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 12), Background = Brushes.Transparent }; closeButton = PlannerView.Button("×", Close, T("PlannerCancel")); DockPanel.SetDock(closeButton, Dock.Right); heading.Children.Add(closeButton); heading.Children.Add(PlannerView.Text(title, 18)); WindowGestures.Header(this, heading, HoldOpen); root.Children.Add(heading);
            var scroll = new ScrollViewer { Content = Body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 540 }; Grid.SetRow(scroll, 1); root.Children.Add(scroll);
            var footer = new StackPanel { Margin = new Thickness(0, 10, 0, 0) }; Error.TextWrapping = TextWrapping.Wrap; footer.Children.Add(Error); footer.Children.Add(Actions); Grid.SetRow(footer, 2); root.Children.Add(footer);
            Content = new Border { Background = PrototypeViews.Brush("#F5202E3B"), BorderBrush = PrototypeViews.Brush("#526474"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(18), Child = root };
            ime = new ImeInput(this); ime.Completed += () => { if (outsidePending) QueueOutsideClose(); };
            Activated += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => { if (editorModeless && IsVisible && IsActive) outsideReady = true; }));
            Deactivated += (_, _) => { if (editorModeless && outsideReady) QueueOutsideClose(); };
            Closed += (_, _) => { if (editorModeless) InputManager.Current.PreProcessInput -= OutsideInput; };
            SizeChanged += (_, _) => _ = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                if (!IsVisible) return; var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle; if (handle == IntPtr.Zero) return;
                var bounds = NativeWindow.Bounds(handle); var fit = WindowGeometry.Clamp(bounds, NativeWindow.WorkArea(handle));
                if (Math.Abs(fit.X - bounds.X) > 1 || Math.Abs(fit.Y - bounds.Y) > 1) NativeWindow.Place(handle, new Rect(fit.X, fit.Y, bounds.Width, bounds.Height));
            }));
            PreviewKeyDown += async (_, e) =>
            {
                if (ime.Consumes(e)) return;
                if (e.Key != Key.Escape) return;
                if (Keyboard.FocusedElement is DependencyObject focus && PresentationSource.FromDependencyObject(focus) != PresentationSource.FromVisual(this)) return;
                if (FindCombos(this).Any(c => c.IsDropDownOpen)) return;
                e.Handled = true; if (AutoSaveClose) await SaveAndCloseAsync(true); else Close();
            };
            Closing += (_, e) => { if (saving) { e.Cancel = true; return; } if (AutoSaveClose && !allowClose) { e.Cancel = true; Dispatcher.BeginInvoke(new Action(async () => await SaveAndCloseAsync(true))); } };
        }
        internal void Field(string key, FrameworkElement control)
        {
            var label = PlannerView.Text(T(key), 11, "#A2AFBC"); label.Margin = new Thickness(0, 6, 0, 5); Body.Children.Add(label); Body.Children.Add(control); AutomationProperties.SetName(control, T(key));
        }
        static System.Collections.Generic.IEnumerable<ComboBox> FindCombos(DependencyObject obj)
        { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++) { var child = VisualTreeHelper.GetChild(obj, i); if (child is ComboBox combo) yield return combo; foreach (var nested in FindCombos(child)) yield return nested; } }
        static System.Collections.Generic.IEnumerable<T> Find<T>(DependencyObject obj) where T : DependencyObject
        { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++) { var child = VisualTreeHelper.GetChild(obj, i); if (child is T value) yield return value; foreach (var nested in Find<T>(child)) yield return nested; } }
        internal bool HasAuxiliary => auxiliaryDepth > 0 || FindCombos(this).Any(c => c.IsDropDownOpen) || Find<DatePicker>(this).Any(p => p.IsDropDownOpen) || Find<ColorField>(this).Any(c => c.Popup.IsOpen) || Application.Current.Windows.OfType<Sheet>().Any(w => w != this && w.IsVisible);
        internal bool ContainsInput(DependencyObject? target)
        {
            for (var node = target; node != null; node = node is FrameworkContentElement text ? text.Parent : node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
                if (node == this || node is ContextMenu menu && menu.PlacementTarget != null && Window.GetWindow(menu.PlacementTarget) == this) return true;
            return target != null && Window.GetWindow(target) == this;
        }
        void OutsideInput(object sender, PreProcessInputEventArgs e)
        {
            if (!editorModeless || saving || !IsVisible || HasAuxiliary) return;
            if (e.StagingItem.Input is not MouseButtonEventArgs mouse || mouse.ButtonState != MouseButtonState.Pressed || mouse.RoutedEvent != Mouse.PreviewMouseDownEvent) return;
            var target = mouse.OriginalSource as DependencyObject ?? Mouse.DirectlyOver as DependencyObject;
            if (target == null || ContainsInput(target)) return;
            // Consume the dismissal click so it cannot also add/delete/reorder another task.
            e.Cancel(); QueueOutsideClose(true);
        }
        void QueueOutsideClose(bool pointer = false)
        {
            if (!editorModeless || !IsVisible || HasAuxiliary) return;
            outsidePending = true; outsideByPointer |= pointer;
            _ = Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(async () =>
            {
                if (!outsidePending || !IsVisible || HasAuxiliary || ime.Composing || saving) return;
                // IME/candidate windows may briefly deactivate an editor and return focus.
                if (!outsideByPointer && IsActive) { outsidePending = false; return; }
                outsidePending = outsideByPointer = false; await SaveAndCloseAsync(true);
            }));
        }
        internal async Task<bool> OutsideCloseAsync() => !HasAuxiliary && !ime.Composing && await SaveAndCloseAsync(true);
        internal bool? ShowEditor()
        {
            editorModeless = true;
            var frame = new DispatcherFrame(); Closed += (_, _) => frame.Continue = false;
            InputManager.Current.PreProcessInput += OutsideInput;
            try { Show(); if (IsVisible) Dispatcher.PushFrame(frame); return Result; }
            finally { InputManager.Current.PreProcessInput -= OutsideInput; }
        }
        internal async Task<bool> SaveAndCloseAsync(bool closing)
        {
            if (saving || saveAction == null) return false;
            saving = true; CloseWithoutChanges = closing; IsEnabled = false;
            try { bool ok = await saveAction(); if (ok && IsVisible) { allowClose = true; saving = false; Result = true; if (editorModeless) Close(); else DialogResult = true; } return ok; }
            finally { saving = false; IsEnabled = true; }
        }
        internal Button Save(Func<Task<bool>> save, bool saveOnClose = false)
        {
            saveAction = save; AutoSaveClose = saveOnClose;
            if (saveOnClose) closeButton.ToolTip = T("SaveClose");
            var cancel = PlannerView.Button(T("PlannerCancel"), () => { allowClose = true; Close(); }); Actions.Children.Add(cancel);
            var button = PlannerView.Button(T("PlannerSave"), async () => await SaveAndCloseAsync(false)); Actions.Children.Add(button); return button;
        }
    }
    internal static bool? Show(MainWindow owner, Sheet sheet)
    {
        sheet.Owner = owner; sheet.Topmost = owner.IsPinned; sheet.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        sheet.MaxHeight = Math.Max(240, NativeWindow.WorkArea(owner.Handle).Height / owner.Scale - 32);
        owner.PlannerDialogDepth++;
        try { return sheet.AutoSaveClose ? sheet.ShowEditor() : sheet.ShowDialog(); } finally { owner.PlannerDialogDepth--; }
    }
    static TextBox Input(string value = "") => new() { Text = value, MinHeight = 28, Padding = new Thickness(6, 4, 6, 4) };
    internal static bool Confirm(MainWindow owner, string title, string message)
    {
        var sheet = new Sheet(title); var body = PlannerView.Text(message); body.TextWrapping = TextWrapping.Wrap; sheet.Body.Children.Add(body);
        sheet.Actions.Children.Add(PlannerView.Button(T("PlannerCancel"), sheet.Close)); sheet.Actions.Children.Add(PlannerView.Button(T("PlannerConfirm"), () => sheet.DialogResult = true)); return Show(owner, sheet) == true;
    }
    internal static bool Edit(MainWindow owner, PlannerController controller, Todo? original, Guid? parent = null, Todo? newDraft = null)
    {
        if (!controller.Writable || original is { Status: TodoStatus.Cancelled } or { Kind: TodoKind.SeriesTemplate }) return false;
        var draft = original ?? newDraft ?? controller.NewTodo("", parent);
        var parentTodo = original == null && draft.ParentId is Guid parentId ? controller.FindTodo(parentId) : null;
        var occurrence = original?.Origin ?? parentTodo?.Origin;
        var sheet = new Sheet(original == null ? T("PlannerNew") : T("PlannerEdit"));
        ComboBox? type = null, scope = null;
        bool newRoot = original == null && draft.ParentId == null, carry = original?.Kind is TodoKind.CarryHistory or TodoKind.CarryLatest;
        if (newRoot || carry)
        {
            type = new ComboBox { MinHeight = 28, ItemsSource = newRoot ? new[] { T("RepeatOnce"), T("RepeatCarryHistory"), T("RepeatCarryLatest"), T("RepeatPeriodic") } : new[] { T("RepeatCarryHistory"), T("RepeatCarryLatest") }, SelectedIndex = newRoot || original!.Kind == TodoKind.CarryHistory ? 0 : 1 }; sheet.Field("RepeatType", type);
        }
        if (occurrence != null)
        {
            scope = RecurrenceFields.Scopes(true, occurrence.Date);
            if (!CanEditSeries(controller.State!, occurrence)) scope.ItemsSource = new[] { new ScopeChoice(EditScope.Single, T("RepeatSingle")) };
            scope.SelectedIndex = 0; sheet.Field("RepeatScope", scope);
            var context = PlannerView.Text(T("RepeatInstanceContext") + " " + occurrence.Date.ToString("yyyy-MM-dd"), 11, "#A3D9C4"); context.TextWrapping = TextWrapping.Wrap; sheet.Body.Children.Add(context);
        }
        var title = Input(draft.Title); title.MaxLength = 10000; sheet.Field("PlannerTitle", title);
        var description = Input(draft.Description); description.AcceptsReturn = true; description.TextWrapping = TextWrapping.Wrap; description.Height = 64; description.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; sheet.Field("PlannerDescription", description);
        var dateTime = new Grid(); dateTime.ColumnDefinitions.Add(new()); dateTime.ColumnDefinitions.Add(new() { Width = new GridLength(92) });
        var date = new DatePicker { SelectedDate = draft.PlannedDate.ToDateTime(TimeOnly.MinValue), Margin = new Thickness(0, 0, 8, 0) };
        if (carry) date.IsEnabled = false;
        bool invalidDate = false;
        date.DateValidationError += (_, e) => { e.ThrowException = false; invalidDate = true; sheet.Error.Text = T("PlannerDateRequired"); };
        date.SelectedDateChanged += (_, _) => invalidDate = false;
        var time = Input(draft.PlannedTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? ""); time.ToolTip = T("PlannerTimeHint"); Grid.SetColumn(time, 1); dateTime.Children.Add(date); dateTime.Children.Add(time); sheet.Field("PlannerDateTime", dateTime); AutomationProperties.SetName(date, T("PlannerDate")); AutomationProperties.SetName(time, T("PlannerTime"));
        RecurrenceFields? repeat = null; Expander? repeatSection = null;
        if (newRoot)
        {
            repeat = new(new RecurrenceRule { Start = draft.PlannedDate, Weekdays = [draft.PlannedDate.DayOfWeek], TimeZoneId = controller.State!.Options.TimeZoneId, WeekStart = controller.State.Options.WeekStart }, date);
            repeatSection = new Expander { Header = T("RepeatRule"), Content = repeat, IsExpanded = true, Visibility = Visibility.Collapsed }; sheet.Body.Children.Add(repeatSection);
        }
        var appearance = new TodoAppearance(controller, draft.CategoryId, draft.Color); sheet.Field("PlannerCategory", appearance);
        sheet.Body.Children.Add(PlannerView.Button(T("PlannerManageCategories"), () => { ManageCategories(owner, controller); appearance.Refresh(); }));
        var priorities = new StackPanel { Orientation = Orientation.Horizontal }; var important = new CheckBox { Content = T("PlannerImportant"), IsChecked = draft.Important, Margin = new Thickness(0, 4, 16, 4) }; var urgent = new CheckBox { Content = T("PlannerUrgent"), IsChecked = draft.Urgent, Margin = new Thickness(0, 4, 0, 4) }; priorities.Children.Add(important); priorities.Children.Add(urgent); sheet.Field("PlannerPriority", priorities);
        bool initialAuto = draft.ProgressMode != ProgressMode.Manual, autoTouched = false;
        bool hasChildren = original != null && PlannerQueries.Children(controller.State!, original).Length > 0;
        var automatic = new CheckBox { Content = T("PlannerDerivedProgress"), IsChecked = initialAuto, Margin = new Thickness(0, 5, 0, 5) }; var progress = Input(draft.ManualProgress.ToString(CultureInfo.InvariantCulture)); sheet.Field("PlannerProgress", progress); sheet.Body.Children.Add(automatic); automatic.Click += (_, _) => { autoTouched = true; progress.IsEnabled = !hasChildren || automatic.IsChecked != true; }; progress.IsEnabled = !hasChildren || automatic.IsChecked != true;
        var completed = new CheckBox { Content = T("PlannerCompleted"), IsChecked = draft.Status == TodoStatus.Completed, Margin = new Thickness(0, 6, 0, 6) }; sheet.Body.Children.Add(completed);
        void KindChanged()
        {
            bool periodic = newRoot && type?.SelectedIndex == 3, single = scope == null || (scope.SelectedItem as ScopeChoice)?.Value == EditScope.Single;
            if (repeatSection != null) repeatSection.Visibility = periodic ? Visibility.Visible : Visibility.Collapsed;
            completed.IsEnabled = !periodic && single; if (periodic) completed.IsChecked = false;
            if (occurrence != null) date.IsEnabled = single;
        }
        if (type != null) type.SelectionChanged += (_, _) => KindChanged(); if (scope != null) scope.SelectionChanged += (_, _) => KindChanged(); KindChanged();
        if (hasChildren) { var hint = PlannerView.Text(T("ParentCompletionHint"), 10, "#A2AFBC"); hint.TextWrapping = TextWrapping.Wrap; sheet.Body.Children.Add(hint); }
        if (draft.ParentId is Guid p) { var parentText = PlannerView.Text(T("PlannerParent") + " " + controller.FindTodo(p)?.Title, 10, "#A2AFBC"); sheet.Body.Children.Add(parentText); }
        var goals = new GoalLinks(owner, controller, TodoRef.Of(draft)); sheet.Body.Children.Add(new Expander { Header = T("ActivityGoal"), Content = goals });
        string Signature() => JsonSerializer.Serialize(new { title.Text, Description = description.Text, Date = date.SelectedDate, Time = time.Text, appearance.CategoryId, Color = appearance.Color.Value, Important = important.IsChecked, Urgent = urgent.IsChecked, Progress = progress.Text, Auto = automatic.IsChecked, Complete = completed.IsChecked, Type = type?.SelectedIndex, Repeat = newRoot && type?.SelectedIndex == 3 ? repeat?.DraftSignature : null, Goals = goals.Signature });
        string initial = Signature();
        sheet.Save(async () =>
        {
            // Esc need not move focus out of DatePicker's editable text box first.
            var dateInput = date.Template?.FindName("PART_TextBox", date) as System.Windows.Controls.Primitives.DatePickerTextBox;
            if (!DateTime.TryParse(dateInput?.Text ?? date.Text, CultureInfo.GetCultureInfo(T("PlannerUiLanguage")), DateTimeStyles.AllowWhiteSpaces, out var parsedDate)) { sheet.Error.Text = T("PlannerDateRequired"); return false; }
            date.SelectedDate = parsedDate.Date;
            if (!invalidDate && Signature() == initial && (original != null || sheet.CloseWithoutChanges && string.IsNullOrWhiteSpace(title.Text))) return true;
            if (!TodoDraftRules.Title(title.Text)) { sheet.Error.Text = T("PlannerTitleRequired"); return false; }
            if (invalidDate || date.SelectedDate == null) { sheet.Error.Text = T("PlannerDateRequired"); return false; }
            if (!TodoDraftRules.Time(time.Text, out var planned)) { sheet.Error.Text = T("PlannerTimeInvalid"); return false; }
            if (!appearance.Color.Valid) { sheet.Error.Text = T("PlannerColorInvalid"); return false; }
            decimal percent = draft.ManualProgress;
            if ((!hasChildren || automatic.IsChecked != true) && (!decimal.TryParse(progress.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out percent) || percent is < 0 or > 100)) { sheet.Error.Text = T("PlannerProgressInvalid"); return false; }
            if (original != null)
            {
                try { if (JsonSerializer.Serialize(PlannerQueries.Resolve(controller.State!, TodoRef.Of(original))) != JsonSerializer.Serialize(original)) { sheet.Error.Text = T("PlannerStaleEdit"); return false; } }
                catch (PlannerException) { sheet.Error.Text = T("PlannerStaleEdit"); return false; }
            }
            var mode = !autoTouched && automatic.IsChecked == initialAuto ? draft.ProgressMode : automatic.IsChecked == true ? ProgressMode.Children : ProgressMode.Manual;
            var value = draft with { Title = title.Text.Trim(), Description = description.Text, PlannedDate = DateOnly.FromDateTime(date.SelectedDate.Value), PlannedTime = planned, CategoryId = appearance.CategoryId, Color = appearance.Color.Value, Important = important.IsChecked == true, Urgent = urgent.IsChecked == true, ProgressMode = mode, ManualProgress = percent };
            var commands = new System.Collections.Generic.List<PlannerCommand>(); var selectedScope = (scope?.SelectedItem as ScopeChoice)?.Value ?? EditScope.Single;
            DateOnly showDate = value.PlannedDate; TodoRef completionRef = TodoRef.Of(value);
            if (newRoot && type?.SelectedIndex == 3)
            {
                if (!repeat!.Read(out var rule)) { sheet.Error.Text = T("RepeatInvalid"); return false; }
                var seriesId = Guid.NewGuid(); commands.Add(new CreateSeries(rule, [value], seriesId)); showDate = Recurrence.Next(rule, rule.Start)!.Value; completionRef = TodoRef.For(new(seriesId, value.Id, showDate));
            }
            else if (original?.Origin != null) commands.Add(new EditOccurrence(value, selectedScope));
            else if (parentTodo?.Origin is OccurrenceKey parentOccurrence)
            {
                commands.Add(new AddSeriesTodo(value, TodoRef.Of(parentTodo), selectedScope));
                completionRef = TodoRef.For(new(parentOccurrence.SeriesId, value.Id, parentOccurrence.Date));
            }
            else
            {
                if (newRoot) value = value with { Kind = type?.SelectedIndex switch { 1 => TodoKind.CarryHistory, 2 => TodoKind.CarryLatest, _ => TodoKind.Once } };
                commands.Add(new SaveTodo(value));
                if (carry && (type!.SelectedIndex == 0) != (original!.Kind == TodoKind.CarryHistory)) commands.Add(new SetCarryMode(TodoRef.Of(value), type.SelectedIndex == 0));
            }
            if (selectedScope != EditScope.Single && !Confirm(owner, T("RepeatScope"), T("RepeatApplyConfirm") + "\n" + (scope?.SelectedItem as ScopeChoice)?.Name)) return false;
            bool allowCompletion = selectedScope == EditScope.Single && !(newRoot && type?.SelectedIndex == 3);
            if (allowCompletion && completed.IsChecked != (draft.Status == TodoStatus.Completed)) commands.Add(new SetTodoCompleted(completionRef, completed.IsChecked == true));
            commands.AddRange(goals.Commands(todo: completionRef));
            bool ok = await controller.ExecuteAsync(new BatchCommand([.. commands]));
            if (ok)
            {
                var saved = controller.State!.Todos.FirstOrDefault(t => t.Id == value.Id);
                if (saved?.Kind == TodoKind.CarryLatest) showDate = PlannerQueries.CarryEnd(saved, controller.Today);
                else if (original != null && saved?.Kind == TodoKind.CarryHistory && controller.On(controller.SelectedDate).Any(t => t.Id == saved.Id)) showDate = controller.SelectedDate;
                controller.Select(showDate, true);
            }
            else sheet.Error.Text = T(controller.NoticeKey);
            return ok;
        }, true);
        sheet.Loaded += (_, _) => title.Focus(); return Show(owner, sheet) == true;
    }
    internal static void Delete(MainWindow owner, PlannerController controller, Todo task)
    {
        if (!controller.Writable) return;
        if (task.Origin != null) { CancelOccurrence(owner, controller, task); return; }
        if (Confirm(owner, T("PlannerDelete"), string.Format(CultureInfo.CurrentCulture, T("PlannerDeleteConfirm"), task.Title))) _ = controller.ExecuteAsync(new DeleteTodo(TodoRef.Of(task), ChildDisposition.DeleteSubtree));
    }
    internal static void Copy(MainWindow owner, PlannerController controller, Todo task)
    {
        var sheet = new Sheet(T("PlannerCopy")); var date = new DatePicker { SelectedDate = task.PlannedDate.ToDateTime(TimeOnly.MinValue) }; sheet.Field("PlannerCopyDate", date);
        var hint = PlannerView.Text(T(task.Kind == TodoKind.Once ? "PlannerCopyHint" : "RepeatCopyHint"), 11, "#A2AFBC"); hint.TextWrapping = TextWrapping.Wrap; sheet.Body.Children.Add(hint);
        sheet.Save(async () => { if (!TodoDraftRules.Date(date, true, out var chosen)) { sheet.Error.Text = T("PlannerDateRequired"); return false; } bool ok = await controller.ExecuteAsync(new CopyTodo(TodoRef.Of(task), chosen!.Value)); if (!ok) sheet.Error.Text = T(controller.NoticeKey); return ok; }); Show(owner, sheet);
    }
    internal static void ManageCategories(MainWindow owner, PlannerController controller)
    {
        var sheet = new Sheet(T("PlannerManageCategories"));
        var list = new ListBox { Height = 110, DisplayMemberPath = nameof(Category.Name) }; var name = Input(); var color = new ColorField("#A3D9C4", false); Category? selected = null;
        void Refresh() { list.ItemsSource = controller.State!.Categories.Where(c => !c.Archived).ToArray(); }
        Refresh(); sheet.Body.Children.Add(list); sheet.Field("PlannerCategoryName", name); sheet.Field("PlannerCategoryColor", color);
        list.SelectionChanged += (_, _) => { selected = list.SelectedItem as Category; name.Text = selected?.Name ?? ""; color.Value = selected?.Color ?? "#A3D9C4"; };
        sheet.Body.Children.Add(PlannerView.Button(T("PlannerNewCategory"), () => { list.SelectedIndex = -1; selected = null; name.Clear(); color.Value = "#A3D9C4"; name.Focus(); }));
        sheet.Body.Children.Add(PlannerView.Button(T("PlannerArchiveCategory"), async () => { if (selected == null || !Confirm(owner, T("PlannerArchiveCategory"), T("PlannerArchiveHint"))) return; if (await controller.ExecuteAsync(new ArchiveCategory(selected.Id))) Refresh(); else sheet.Error.Text = T(controller.NoticeKey); }));
        sheet.Body.Children.Add(PlannerView.Button(T("PlannerDefaultCategories"), async () => { if (await controller.ExecuteAsync(new InitializeCategories())) Refresh(); else sheet.Error.Text = T(controller.NoticeKey); }));
        sheet.Save(async () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text) || !System.Text.RegularExpressions.Regex.IsMatch(color.Value ?? "", "^#[0-9a-fA-F]{6}$")) { sheet.Error.Text = T("PlannerCategoryInvalid"); return false; }
            bool ok = await controller.ExecuteAsync(new SaveCategory((selected ?? new Category()) with { Name = name.Text.Trim(), Color = color.Value! })); if (!ok) sheet.Error.Text = T(controller.NoticeKey); return ok;
        }); Show(owner, sheet);
    }
    internal static void Decoration(MainWindow owner, PlannerController controller, DateOnly date)
    {
        if (!controller.Writable) return;
        var sheet = new Sheet(T("PlannerDateColor")); var old = controller.State!.Decorations.FirstOrDefault(d => d.Date == date); var color = new ColorField(old?.Color ?? "#AFC5D8", false); sheet.Field("PlannerCategoryColor", color);
        if (old != null) sheet.Body.Children.Add(PlannerView.Button(T("PlannerClearColor"), async () => { if (await controller.ExecuteAsync(new DeleteDecoration(old.Id))) sheet.DialogResult = true; else sheet.Error.Text = T(controller.NoticeKey); }));
        sheet.Save(async () => { if (!System.Text.RegularExpressions.Regex.IsMatch(color.Value ?? "", "^#[0-9a-fA-F]{6}$")) { sheet.Error.Text = T("PlannerColorInvalid"); return false; } bool ok = await controller.ExecuteAsync(new SaveDecoration((old ?? new DayDecoration { Date = date }) with { Color = color.Value! })); if (!ok) sheet.Error.Text = T(controller.NoticeKey); return ok; }); Show(owner, sheet);
    }
    internal static void Recover(MainWindow owner, PlannerController controller)
    {
        if (controller.Storage == null || controller.Storage.Status == StoreStatus.NewerSchema) return;
        var sheet = new Sheet(T("PlannerRecover")); var candidates = new ComboBox { ItemsSource = controller.Storage.Candidates, DisplayMemberPath = nameof(RecoveryCandidate.Name), SelectedIndex = 0, MinHeight = 28 }; sheet.Field("PlannerRecoveryCandidate", candidates);
        var hint = PlannerView.Text(T("PlannerRecoveryHint"), 11, "#A2AFBC"); hint.TextWrapping = TextWrapping.Wrap; sheet.Body.Children.Add(hint);
        sheet.Save(async () => { if (candidates.SelectedItem is not RecoveryCandidate candidate) return false; if (!Confirm(owner, T("PlannerRecover"), string.Format(CultureInfo.CurrentCulture, T("PlannerRecoveryConfirm"), candidate.State.Todos.Length, candidate.State.Activities.Length, candidate.State.Revision))) return false; bool ok = await controller.RecoverAsync(candidate); if (!ok) sheet.Error.Text = T(controller.NoticeKey); return ok; }); Show(owner, sheet);
    }
}
