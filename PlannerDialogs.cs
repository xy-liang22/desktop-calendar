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
using DesktopCalendar.Core;

namespace DesktopCalendar;

internal static class PlannerDialogs
{
    static string T(string key) => MainWindow.Text(key);
    internal sealed class Sheet : Window
    {
        bool saving;
        internal StackPanel Body { get; } = new();
        internal TextBlock Error { get; } = PlannerView.Text("", 11, "#E3BF89");
        internal StackPanel Actions { get; } = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        internal Sheet(string title, double width = 430)
        {
            Title = title; Language = System.Windows.Markup.XmlLanguage.GetLanguage(T("PlannerUiLanguage")); Width = width; MaxHeight = 740; SizeToContent = SizeToContent.Height; MinHeight = 160; MinWidth = 320; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize;
            var root = new Grid(); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 12), Background = Brushes.Transparent }; var close = PlannerView.Button("×", Close, T("PlannerCancel")); DockPanel.SetDock(close, Dock.Right); heading.Children.Add(close); heading.Children.Add(PlannerView.Text(title, 18)); heading.MouseLeftButtonDown += (_, e) => { if (e.Source == heading && e.LeftButton == MouseButtonState.Pressed) DragMove(); }; root.Children.Add(heading);
            var scroll = new ScrollViewer { Content = Body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 540 }; Grid.SetRow(scroll, 1); root.Children.Add(scroll);
            var footer = new StackPanel { Margin = new Thickness(0, 10, 0, 0) }; Error.TextWrapping = TextWrapping.Wrap; footer.Children.Add(Error); footer.Children.Add(Actions); Grid.SetRow(footer, 2); root.Children.Add(footer);
            Content = new Border { Background = PrototypeViews.Brush("#FF202E3B"), BorderBrush = PrototypeViews.Brush("#526474"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(18), Child = root };
            PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
            Closing += (_, e) => { if (saving) e.Cancel = true; };
        }
        internal void Field(string key, FrameworkElement control)
        {
            var label = PlannerView.Text(T(key), 11, "#A2AFBC"); label.Margin = new Thickness(0, 6, 0, 5); Body.Children.Add(label); Body.Children.Add(control); AutomationProperties.SetName(control, T(key));
        }
        internal Button Save(Func<Task<bool>> save)
        {
            var cancel = PlannerView.Button(T("PlannerCancel"), Close); Actions.Children.Add(cancel);
            var button = PlannerView.Button(T("PlannerSave"), async () => { saving = true; IsEnabled = false; try { bool ok = await save(); saving = false; if (ok && IsVisible) DialogResult = true; } finally { saving = false; IsEnabled = true; } }); Actions.Children.Add(button); return button;
        }
    }
    internal static bool? Show(MainWindow owner, Sheet sheet)
    {
        sheet.Owner = owner; sheet.Topmost = owner.IsPinned; sheet.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        sheet.MaxHeight = Math.Max(240, NativeWindow.WorkArea(owner.Handle).Height / owner.Scale - 32);
        owner.PlannerDialogDepth++;
        try { return sheet.ShowDialog(); } finally { owner.PlannerDialogDepth--; }
    }
    static TextBox Input(string value = "") => new() { Text = value, MinHeight = 28, Padding = new Thickness(6, 4, 6, 4) };
    static WrapPanel Palette(TextBox target)
    {
        var palette = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
        foreach (string color in new[] { "#A3D9C4", "#AFC5D8", "#B6B5E6", "#E3BF89", "#D1AAA6", "#B7C2AE" })
        {
            var button = PlannerView.Button("", () => target.Text = color, string.Format(CultureInfo.CurrentCulture, T("PlannerPickColor"), color));
            button.Width = 25; button.Height = 18; button.Padding = new Thickness(0); button.Background = PrototypeViews.Brush(color); palette.Children.Add(button);
        }
        return palette;
    }
    internal static bool Confirm(MainWindow owner, string title, string message)
    {
        var sheet = new Sheet(title); var body = PlannerView.Text(message); body.TextWrapping = TextWrapping.Wrap; sheet.Body.Children.Add(body);
        sheet.Actions.Children.Add(PlannerView.Button(T("PlannerCancel"), sheet.Close)); sheet.Actions.Children.Add(PlannerView.Button(T("PlannerConfirm"), () => sheet.DialogResult = true)); return Show(owner, sheet) == true;
    }
    internal static void Edit(MainWindow owner, PlannerController controller, Todo? original, Guid? parent = null)
    {
        if (!controller.Writable || original is { Kind: not TodoKind.Once }) return;
        var draft = original ?? controller.NewTodo("", parent);
        var sheet = new Sheet(original == null ? T("PlannerNew") : T("PlannerEdit"));
        var title = Input(draft.Title); title.MaxLength = 10000; sheet.Field("PlannerTitle", title);
        var description = Input(draft.Description); description.AcceptsReturn = true; description.TextWrapping = TextWrapping.Wrap; description.Height = 64; description.VerticalScrollBarVisibility = ScrollBarVisibility.Auto; sheet.Field("PlannerDescription", description);
        var dateTime = new Grid(); dateTime.ColumnDefinitions.Add(new()); dateTime.ColumnDefinitions.Add(new() { Width = new GridLength(92) });
        var date = new DatePicker { SelectedDate = draft.PlannedDate.ToDateTime(TimeOnly.MinValue), Margin = new Thickness(0, 0, 8, 0) };
        var time = Input(draft.PlannedTime?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? ""); time.ToolTip = T("PlannerTimeHint"); Grid.SetColumn(time, 1); dateTime.Children.Add(date); dateTime.Children.Add(time); sheet.Field("PlannerDateTime", dateTime); AutomationProperties.SetName(date, T("PlannerDate")); AutomationProperties.SetName(time, T("PlannerTime"));
        var categories = new ComboBox { MinHeight = 28, DisplayMemberPath = nameof(CategoryChoice.Name), SelectedValuePath = nameof(CategoryChoice.Id) };
        void Categories() { categories.ItemsSource = new[] { new CategoryChoice(null, T("PlannerUnclassified")) }.Concat(controller.State!.Categories.Where(c => !c.Archived || c.Id == draft.CategoryId).Select(c => new CategoryChoice(c.Id, c.Name))).ToArray(); categories.SelectedValue = draft.CategoryId; if (categories.SelectedIndex < 0) categories.SelectedIndex = 0; }
        Categories(); sheet.Field("PlannerCategory", categories);
        sheet.Body.Children.Add(PlannerView.Button(T("PlannerManageCategories"), () => { ManageCategories(owner, controller); Categories(); }));
        var color = Input(draft.Color ?? ""); color.ToolTip = T("PlannerColorHint"); sheet.Field("PlannerColor", color);
        sheet.Body.Children.Add(Palette(color));
        var priorities = new StackPanel { Orientation = Orientation.Horizontal }; var important = new CheckBox { Content = T("PlannerImportant"), IsChecked = draft.Important, Margin = new Thickness(0, 4, 16, 4) }; var urgent = new CheckBox { Content = T("PlannerUrgent"), IsChecked = draft.Urgent, Margin = new Thickness(0, 4, 0, 4) }; priorities.Children.Add(important); priorities.Children.Add(urgent); sheet.Field("PlannerPriority", priorities);
        var automatic = new CheckBox { Content = T("PlannerDerivedProgress"), IsChecked = draft.ProgressMode == ProgressMode.Children, Margin = new Thickness(0, 5, 0, 5) }; var progress = Input(draft.ManualProgress.ToString(CultureInfo.InvariantCulture)); sheet.Field("PlannerProgress", progress); sheet.Body.Children.Add(automatic); automatic.Click += (_, _) => progress.IsEnabled = automatic.IsChecked != true; progress.IsEnabled = automatic.IsChecked != true;
        var completed = new CheckBox { Content = T("PlannerCompleted"), IsChecked = draft.Status == TodoStatus.Completed, Margin = new Thickness(0, 6, 0, 6) }; sheet.Body.Children.Add(completed);
        if (draft.ParentId is Guid p) { var parentText = PlannerView.Text(T("PlannerParent") + " " + controller.State!.Todos.FirstOrDefault(t => t.Id == p)?.Title, 10, "#A2AFBC"); sheet.Body.Children.Add(parentText); }
        sheet.Save(async () =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) { sheet.Error.Text = T("PlannerTitleRequired"); return false; }
            if (date.SelectedDate == null) { sheet.Error.Text = T("PlannerDateRequired"); return false; }
            TimeOnly? planned = null;
            if (!string.IsNullOrWhiteSpace(time.Text)) { if (!TimeOnly.TryParseExact(time.Text.Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) { sheet.Error.Text = T("PlannerTimeInvalid"); return false; } planned = parsed; }
            string? hex = string.IsNullOrWhiteSpace(color.Text) ? null : color.Text.Trim();
            if (hex != null && !System.Text.RegularExpressions.Regex.IsMatch(hex, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")) { sheet.Error.Text = T("PlannerColorInvalid"); return false; }
            if (!decimal.TryParse(progress.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal percent) || percent is < 0 or > 100) { sheet.Error.Text = T("PlannerProgressInvalid"); return false; }
            if (original != null && JsonSerializer.Serialize(controller.State?.Todos.FirstOrDefault(t => t.Id == original.Id)) != JsonSerializer.Serialize(original)) { sheet.Error.Text = T("PlannerStaleEdit"); return false; }
            var value = draft with { Title = title.Text.Trim(), Description = description.Text, PlannedDate = DateOnly.FromDateTime(date.SelectedDate.Value), PlannedTime = planned, CategoryId = (categories.SelectedItem as CategoryChoice)?.Id, Color = hex, Important = important.IsChecked == true, Urgent = urgent.IsChecked == true, ProgressMode = automatic.IsChecked == true ? ProgressMode.Children : ProgressMode.Manual, ManualProgress = percent };
            bool ok = await controller.ExecuteAsync(new BatchCommand([new SaveTodo(value), new SetTodoCompleted(TodoRef.Of(value), completed.IsChecked == true)]));
            if (ok) controller.Select(value.PlannedDate, true); else sheet.Error.Text = T(controller.NoticeKey);
            return ok;
        });
        sheet.Loaded += (_, _) => title.Focus(); Show(owner, sheet);
    }
    sealed record CategoryChoice(Guid? Id, string Name);
    internal static void Delete(MainWindow owner, PlannerController controller, Todo task)
    {
        if (task.Kind != TodoKind.Once || !controller.Writable) return;
        if (Confirm(owner, T("PlannerDelete"), string.Format(CultureInfo.CurrentCulture, T("PlannerDeleteConfirm"), task.Title))) _ = controller.ExecuteAsync(new DeleteTodo(TodoRef.Of(task), ChildDisposition.DeleteSubtree));
    }
    internal static void Copy(MainWindow owner, PlannerController controller, Todo task)
    {
        var sheet = new Sheet(T("PlannerCopy")); var date = new DatePicker { SelectedDate = task.PlannedDate.ToDateTime(TimeOnly.MinValue) }; sheet.Field("PlannerCopyDate", date);
        var hint = PlannerView.Text(T("PlannerCopyHint"), 11, "#A2AFBC"); hint.TextWrapping = TextWrapping.Wrap; sheet.Body.Children.Add(hint);
        sheet.Save(async () => { if (date.SelectedDate == null) return false; bool ok = await controller.ExecuteAsync(new CopyTodo(TodoRef.Of(task), DateOnly.FromDateTime(date.SelectedDate.Value))); if (!ok) sheet.Error.Text = T(controller.NoticeKey); return ok; }); Show(owner, sheet);
    }
    internal static void ManageCategories(MainWindow owner, PlannerController controller)
    {
        var sheet = new Sheet(T("PlannerManageCategories"));
        var list = new ListBox { Height = 110, DisplayMemberPath = nameof(Category.Name) }; var name = Input(); var color = Input("#A3D9C4"); Category? selected = null;
        void Refresh() { list.ItemsSource = controller.State!.Categories.Where(c => !c.Archived).ToArray(); }
        Refresh(); sheet.Body.Children.Add(list); sheet.Field("PlannerCategoryName", name); sheet.Field("PlannerCategoryColor", color); sheet.Body.Children.Add(Palette(color));
        list.SelectionChanged += (_, _) => { selected = list.SelectedItem as Category; name.Text = selected?.Name ?? ""; color.Text = selected?.Color ?? "#A3D9C4"; };
        sheet.Body.Children.Add(PlannerView.Button(T("PlannerNewCategory"), () => { list.SelectedIndex = -1; selected = null; name.Clear(); color.Text = "#A3D9C4"; name.Focus(); }));
        sheet.Body.Children.Add(PlannerView.Button(T("PlannerArchiveCategory"), async () => { if (selected == null || !Confirm(owner, T("PlannerArchiveCategory"), T("PlannerArchiveHint"))) return; if (await controller.ExecuteAsync(new ArchiveCategory(selected.Id))) Refresh(); else sheet.Error.Text = T(controller.NoticeKey); }));
        sheet.Body.Children.Add(PlannerView.Button(T("PlannerDefaultCategories"), async () => { if (await controller.ExecuteAsync(new InitializeCategories())) Refresh(); else sheet.Error.Text = T(controller.NoticeKey); }));
        sheet.Save(async () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text) || !System.Text.RegularExpressions.Regex.IsMatch(color.Text.Trim(), "^#[0-9a-fA-F]{6}$")) { sheet.Error.Text = T("PlannerCategoryInvalid"); return false; }
            bool ok = await controller.ExecuteAsync(new SaveCategory((selected ?? new Category()) with { Name = name.Text.Trim(), Color = color.Text.Trim() })); if (!ok) sheet.Error.Text = T(controller.NoticeKey); return ok;
        }); Show(owner, sheet);
    }
    internal static void Decoration(MainWindow owner, PlannerController controller, DateOnly date)
    {
        if (!controller.Writable) return;
        var sheet = new Sheet(T("PlannerDateColor")); var old = controller.State!.Decorations.FirstOrDefault(d => d.Date == date); var color = Input(old?.Color ?? "#AFC5D8"); sheet.Field("PlannerCategoryColor", color); sheet.Body.Children.Add(Palette(color));
        if (old != null) sheet.Body.Children.Add(PlannerView.Button(T("PlannerClearColor"), async () => { if (await controller.ExecuteAsync(new DeleteDecoration(old.Id))) sheet.DialogResult = true; else sheet.Error.Text = T(controller.NoticeKey); }));
        sheet.Save(async () => { if (!System.Text.RegularExpressions.Regex.IsMatch(color.Text.Trim(), "^#[0-9a-fA-F]{6}$")) { sheet.Error.Text = T("PlannerColorInvalid"); return false; } bool ok = await controller.ExecuteAsync(new SaveDecoration((old ?? new DayDecoration { Date = date }) with { Color = color.Text.Trim() })); if (!ok) sheet.Error.Text = T(controller.NoticeKey); return ok; }); Show(owner, sheet);
    }
    internal static void Recover(MainWindow owner, PlannerController controller)
    {
        if (controller.Storage == null || controller.Storage.Status == StoreStatus.NewerSchema) return;
        var sheet = new Sheet(T("PlannerRecover")); var candidates = new ComboBox { ItemsSource = controller.Storage.Candidates, DisplayMemberPath = nameof(RecoveryCandidate.Name), SelectedIndex = 0, MinHeight = 28 }; sheet.Field("PlannerRecoveryCandidate", candidates);
        var hint = PlannerView.Text(T("PlannerRecoveryHint"), 11, "#A2AFBC"); hint.TextWrapping = TextWrapping.Wrap; sheet.Body.Children.Add(hint);
        sheet.Save(async () => { if (candidates.SelectedItem is not RecoveryCandidate candidate) return false; if (!Confirm(owner, T("PlannerRecover"), string.Format(CultureInfo.CurrentCulture, T("PlannerRecoveryConfirm"), candidate.State.Todos.Length, candidate.State.Activities.Length, candidate.State.Revision))) return false; bool ok = await controller.RecoverAsync(candidate); if (!ok) sheet.Error.Text = T(controller.NoticeKey); return ok; }); Show(owner, sheet);
    }
}
