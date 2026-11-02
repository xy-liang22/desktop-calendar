using System;
using System.Linq;
using System.Text.Json;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static partial class PlannerDialogs
{
    internal static bool CanEditSeries(PlannerState state, OccurrenceKey key)
    {
        var series = state.Series.SingleOrDefault(s => s.Id == key.SeriesId);
        return series != null && !series.Archived && !(series.SuppressedFrom <= key.Date) && PlannerQueries.Version(series, key.Date)?.Templates.Any(t => t.Id == key.NodeId) == true;
    }
    internal static void EditRule(MainWindow owner, PlannerController controller, Todo task)
    {
        if (!controller.Writable || task.Origin is not OccurrenceKey key || !CanEditSeries(controller.State!, key)) return;
        var series = controller.State!.Series.Single(s => s.Id == key.SeriesId); var rule = PlannerQueries.Version(series, key.Date)!.Rule;
        var sheet = new Sheet(T("RepeatRule")); var context = PlannerView.Text(T("RepeatRuleContext") + "\n" + task.Title + " · " + key.Date.ToString("yyyy-MM-dd")); context.TextWrapping = System.Windows.TextWrapping.Wrap; sheet.Body.Children.Add(context);
        var fields = new RecurrenceFields(rule); sheet.Body.Children.Add(fields); var scope = RecurrenceFields.Scopes(false, key.Date); sheet.Field("RepeatScope", scope);
        sheet.Save(async () =>
        {
            if (!fields.Read(out var value)) { sheet.Error.Text = T("RepeatInvalid"); return false; }
            if (JsonSerializer.Serialize(value) == JsonSerializer.Serialize(rule)) return true;
            if (JsonSerializer.Serialize(controller.State!.Series.SingleOrDefault(s => s.Id == series.Id)) != JsonSerializer.Serialize(series)) { sheet.Error.Text = T("PlannerStaleEdit"); return false; }
            if (!Confirm(owner, T("RepeatScope"), T("RepeatApplyConfirm") + "\n" + ((ScopeChoice)scope.SelectedItem).Name)) return false;
            bool ok = await controller.ExecuteAsync(new EditSeries(key.SeriesId, key.Date, ((ScopeChoice)scope.SelectedItem).Value, Rule: value));
            if (!ok) sheet.Error.Text = T(controller.NoticeKey); else controller.Select(Recurrence.Next(value, key.Date) ?? key.Date, true); return ok;
        }, true); Show(owner, sheet);
    }
    internal static void CancelOccurrence(MainWindow owner, PlannerController controller, Todo task)
    {
        if (task.Origin is not OccurrenceKey key) return;
        var sheet = new Sheet(T("RepeatCancel")); var scope = RecurrenceFields.Scopes(true, key.Date);
        if (!CanEditSeries(controller.State!, key)) { scope.ItemsSource = new[] { new ScopeChoice(EditScope.Single, T("RepeatSingle")) }; scope.SelectedIndex = 0; }
        var hint = PlannerView.Text(T("RepeatCancelHint") + "\n" + task.Title + " · " + key.Date.ToString("yyyy-MM-dd")); hint.TextWrapping = System.Windows.TextWrapping.Wrap; sheet.Body.Children.Add(hint); sheet.Field("RepeatScope", scope);
        var apply = sheet.Save(async () =>
        {
            var selected = ((ScopeChoice)scope.SelectedItem).Value;
            if (selected == EditScope.Single && task.Status == TodoStatus.Completed) { sheet.Error.Text = T("RepeatCompletedProtected"); return false; }
            bool ok = await controller.ExecuteAsync(new CancelSeries(key.SeriesId, key.Date, selected, key.NodeId)); if (!ok) sheet.Error.Text = T(controller.NoticeKey); return ok;
        }); apply.Content = T("RepeatCancelApply"); Show(owner, sheet);
    }
    internal static void MoveCarry(MainWindow owner, PlannerController controller, Todo task)
    {
        if (!controller.Writable || task.Status != TodoStatus.Pending) return;
        var sheet = new Sheet(T("RepeatMove")); var picker = new DatePicker { SelectedDate = (task.CarryDate > controller.Today ? task.CarryDate.Value : controller.Today).ToDateTime(TimeOnly.MinValue) };
        sheet.Field("PlannerDate", picker); var hint = PlannerView.Text(T("RepeatCarryHint"), 11, "#A2AFBC"); hint.TextWrapping = System.Windows.TextWrapping.Wrap; sheet.Body.Children.Add(hint);
        sheet.Save(async () =>
        {
            if (!TodoDraftRules.Date(picker, true, out var day) || day <= task.CarryDate) { sheet.Error.Text = T("RepeatMoveInvalid"); return false; }
            bool ok = await controller.ExecuteAsync(new MoveCarryTodo(TodoRef.Of(task), day!.Value)); if (!ok) sheet.Error.Text = T(controller.NoticeKey); else controller.Select(day.Value, true); return ok;
        }); Show(owner, sheet);
    }
}
