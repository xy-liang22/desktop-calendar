using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static partial class PlannerDialogs
{
    static IEnumerable<(Todo Todo, int Depth)> WorkflowRows(IEnumerable<TodoDateNode> rows, int depth = 0)
    {
        foreach (var row in rows) { yield return (row.Value, depth); foreach (var child in WorkflowRows(row.Children, depth + 1)) yield return child; }
    }
    internal static void BatchEdit(MainWindow owner, PlannerController controller)
    {
        if (!controller.Writable) return;
        var sheet = new Sheet(T("BulkEdit"), 470); var before = controller.State!;
        var rows = WorkflowRows(controller.Tree(controller.SelectedDate)).Where(x => x.Todo.Status != TodoStatus.Cancelled).ToArray();
        var choices = new Dictionary<TodoRef, CheckBox>(); var list = new StackPanel();
        foreach (var row in rows)
        {
            var check = new CheckBox { Content = row.Todo.Title, Margin = new Thickness(row.Depth * 12, 5, 0, 5), ToolTip = PlannerView.Prefix(row.Todo, before) + row.Todo.Title };
            choices.Add(TodoRef.Of(row.Todo), check); list.Children.Add(check);
        }
        var all = new CheckBox { Content = T("BulkSelectAll"), Margin = new Thickness(0, 4, 0, 6) };
        all.Click += (_, _) => { foreach (var check in choices.Values) check.IsChecked = all.IsChecked; }; sheet.Body.Children.Add(all);
        sheet.Body.Children.Add(new ScrollViewer { Content = list, MaxHeight = 210, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var changeDate = new CheckBox { Content = T("BulkDate"), Margin = new Thickness(0, 8, 0, 4) }; sheet.Body.Children.Add(changeDate);
        var date = new DatePicker { SelectedDate = controller.SelectedDate.ToDateTime(TimeOnly.MinValue), IsEnabled = false }; sheet.Body.Children.Add(date); changeDate.Click += (_, _) => date.IsEnabled = changeDate.IsChecked == true;
        var type = new ComboBox { ItemsSource = new[] { "BulkKeepType", "RepeatOnce", "RepeatCarryHistory", "RepeatCarryLatest", "RepeatPeriodic" }.SelectText(), SelectedIndex = 0, MinHeight = 28 }; sheet.Field("RepeatType", type);
        var effective = new DatePicker { SelectedDate = controller.SelectedDate.ToDateTime(TimeOnly.MinValue) }; sheet.Field("TodoConversionDate", effective);
        var repeat = new RecurrenceFields(new() { Start = controller.SelectedDate, Weekdays = [controller.SelectedDate.DayOfWeek], WeekStart = before.Options.WeekStart, TimeZoneId = controller.DisplayZone }, effective);
        var section = new Expander { Header = T("RepeatRule"), Content = repeat, Visibility = Visibility.Collapsed }; sheet.Body.Children.Add(section);
        type.SelectionChanged += (_, _) => { section.Visibility = type.SelectedIndex == 4 ? Visibility.Visible : Visibility.Collapsed; };
        var hint = PlannerView.Text(T("BulkHint"), 11); hint.TextWrapping = TextWrapping.Wrap; sheet.Body.Children.Add(hint);
        sheet.Save(async () =>
        {
            if (controller.State != before) { sheet.Error.Text = T("PlannerStaleEdit"); return false; }
            var selected = choices.Where(p => p.Value.IsChecked == true).Select(p => p.Key).ToArray();
            if (selected.Length == 0 || type.SelectedIndex == 0 && changeDate.IsChecked != true) { sheet.Error.Text = T("BulkChoose"); return false; }
            DateOnly? day = null; if (changeDate.IsChecked == true && !TodoDraftRules.Date(date, true, out day) || !TodoDraftRules.Date(effective, true, out var from)) { sheet.Error.Text = T("PlannerDateRequired"); return false; }
            TodoKind? kind = type.SelectedIndex switch { 1 => TodoKind.Once, 2 => TodoKind.CarryHistory, 3 => TodoKind.CarryLatest, 4 => TodoKind.SeriesTemplate, _ => null };
            RecurrenceRule? rule = null; if (kind == TodoKind.SeriesTemplate) { if (!repeat.Read(out var read)) { sheet.Error.Text = T("RepeatInvalid"); return false; } rule = read with { Start = day ?? from!.Value }; }
            var command = new BulkEditTodos([.. selected], day, kind, from!.Value, rule);
            try { _ = PlannerWorkspace.Preview(before, command, DateTimeOffset.UtcNow); }
            catch (Exception e) when (e is PlannerException or ArgumentException or InvalidOperationException) { sheet.Error.Text = WorkflowText.Error(e); return false; }
            string preview = string.Format(Localization.Culture, T("BulkPreview"), selected.Length) + "\n" + (day == null ? "" : T("PlannerDate") + ": " + day + "\n") + T("RepeatType") + ": " + type.SelectedItem + "\n" + T("TodoConversionDate") + ": " + from + "\n\n" + string.Join("\n", selected.Select(r => "• " + PlannerQueries.Resolve(before, r).Title)) + "\n\n" + T("BulkHint");
            if (!Confirm(owner, T("BulkEdit"), preview)) return false;
            if (controller.State != before) { sheet.Error.Text = T("PlannerStaleEdit"); return false; }
            bool ok = await controller.ExecuteAsync(command); if (!ok) sheet.Error.Text = T(controller.NoticeKey); return ok;
        }); Show(owner, sheet);
    }
    sealed record ParentChoice(TodoRef? Reference, string Name);
    internal static bool MoveHierarchy(MainWindow owner, PlannerController controller, Todo task, Todo? target = null, bool root = false, Todo? sibling = null, bool after = false)
    {
        if (!controller.Writable) return false;
        var before = controller.State!; var forbidden = new HashSet<Guid> { task.Id };
        void Children(Todo node) { foreach (var child in PlannerQueries.Children(before, node)) { if (forbidden.Add(child.Id)) Children(child); } } Children(task);
        var options = new List<ParentChoice> { new(null, T("HierarchyRoot")) };
        options.AddRange(WorkflowRows(controller.Tree(controller.SelectedDate)).Where(x => !forbidden.Contains(x.Todo.Id) && x.Todo.Status != TodoStatus.Cancelled).Select(x => new ParentChoice(TodoRef.Of(x.Todo), new string('　', Math.Min(x.Depth, 6)) + x.Todo.Title)));
        var sheet = new Sheet(T("HierarchyMove"), 460);
        sheet.Body.Children.Add(PlannerView.Text(task.Title, 14));
        var parent = new ComboBox { ItemsSource = options, DisplayMemberPath = nameof(ParentChoice.Name), MinHeight = 30, MaxDropDownHeight = 260, SelectedItem = options.FirstOrDefault(x => x.Reference?.Id == (root ? null : target?.Id ?? task.ParentId)) ?? options[0] }; sheet.Field("PlannerParent", parent);
        if (sibling != null) { parent.IsEnabled = false; sheet.Body.Children.Add(PlannerView.Text(T(after ? "TodoDragAfter" : "TodoDragBefore") + " “" + sibling.Title + "”")); }
        var scope = new ComboBox { ItemsSource = new[] { new ScopeChoice(EditScope.Single, T("RepeatSingle")), new ScopeChoice(EditScope.FromDate, T("RepeatFrom")) }, DisplayMemberPath = nameof(ScopeChoice.Name), SelectedIndex = 0, MinHeight = 28 }; sheet.Field("RepeatScope", scope);
        var detach = new ComboBox { ItemsSource = new[] { "RepeatOnce", "RepeatCarryHistory", "RepeatCarryLatest" }.SelectText(), SelectedIndex = 0, MinHeight = 28 }; sheet.Field("HierarchyDetachType", detach);
        var hint = PlannerView.Text("", 11); hint.TextWrapping = TextWrapping.Wrap; sheet.Body.Children.Add(hint);
        bool NeedsDetach()
        {
            var p = (parent.SelectedItem as ParentChoice)?.Reference?.Occurrence;
            return task.Origin is OccurrenceKey k && (p == null || p.SeriesId != k.SeriesId || p.Date != k.Date);
        }
        void Update()
        {
            bool convert = NeedsDetach(); scope.IsEnabled = task.Origin != null && !convert;
            if (!scope.IsEnabled) scope.SelectedIndex = 0;
            detach.IsEnabled = convert; hint.Text = T(convert ? "HierarchyConversion" : "HierarchyHint");
        }
        parent.SelectionChanged += (_, _) => Update(); Update();
        sheet.Save(async () =>
        {
            if (controller.State != before) { sheet.Error.Text = T("PlannerStaleEdit"); return false; }
            var chosen = (ParentChoice)parent.SelectedItem;
            var move = new ReparentTodo(TodoRef.Of(task), chosen.Reference, ((ScopeChoice)scope.SelectedItem).Value,
                NeedsDetach() ? detach.SelectedIndex switch { 1 => TodoKind.CarryHistory, 2 => TodoKind.CarryLatest, _ => TodoKind.Once } : null);
            PlannerCommand command = sibling == null ? move : new PlaceTodoRelative(move, TodoRef.Of(sibling), after, controller.SelectedDate, controller.Today);
            try { _ = PlannerWorkspace.Preview(before, command, DateTimeOffset.UtcNow); }
            catch (Exception e) when (e is PlannerException or InvalidOperationException or ArgumentException) { sheet.Error.Text = WorkflowText.Error(e); return false; }
            if (NeedsDetach() && !Confirm(owner, T("HierarchyMove"), T("HierarchyConversion") + "\n" + task.Title + " → " + chosen.Name + "\n" + detach.SelectedItem)) return false;
            if (controller.State != before) { sheet.Error.Text = T("PlannerStaleEdit"); return false; }
            bool ok = await controller.ExecuteAsync(command); if (!ok) sheet.Error.Text = T(controller.NoticeKey); return ok;
        }); return Show(owner, sheet) == true;
    }
}
