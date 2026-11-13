using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static partial class StageThreeChecks
{
    static async Task RunWorkflowChecks(MainWindow window)
    {
        var c = window.Session.Planner!; c.Select(c.Today.AddDays(40), true); window.SetPage(false);
        var first = c.NewTodo("Workflow selected parent"); await c.ExecuteAsync(new SaveTodo(first));
        var child = c.NewTodo("Workflow child", first.Id); await c.ExecuteAsync(new AddChildTodo(child, TodoRef.Of(first)));
        var target = c.NewTodo("Workflow target"); await c.ExecuteAsync(new SaveTodo(target));
        await DriveAsync(() => PlannerDialogs.Edit(window, c, c.FindTodo(first.Id)), async sheet =>
        {
            Children<Expander>(sheet).Single(e => Equals(e.Header, MainWindow.Text("Deadline"))).IsExpanded = true; await Idle();
            var fields = Children<DeadlineFields>(sheet).Single(); var kind = Children<ComboBox>(fields).First(); kind.SelectedIndex = 2;
            Field(sheet, "DeadlineTime").Text = "18：00"; await Idle();
            Check("deadline time input normalizes Chinese colon", Field(sheet, "DeadlineTime").Text == "18:00");
            Image(sheet, "workflow-deadline-editor.png"); await sheet.SaveAndCloseAsync(false);
            Check("deadline editor saves through existing transaction", !sheet.IsVisible && c.FindTodo(first.Id)!.Deadline?.Time == new TimeOnly(18, 0));
        });
        await DriveAsync(() => PlannerDialogs.Edit(window, c, c.FindTodo(first.Id)), async sheet =>
        {
            Children<Expander>(sheet).Single(e => Equals(e.Header, MainWindow.Text("Deadline"))).IsExpanded = true; await Idle();
            Field(sheet, "DeadlineTime").Text = "25:00";
            bool saved = await sheet.SaveAndCloseAsync(false); Check("invalid deadline keeps editor and stored value", !saved && sheet.IsVisible && c.FindTodo(first.Id)!.Deadline?.Time == new TimeOnly(18, 0));
        });
        await DriveAsync(() => PlannerDialogs.BatchEdit(window, c), async sheet =>
        {
            Children<CheckBox>(sheet).Single(x => Equals(x.Content, first.Title)).IsChecked = true;
            Choice(sheet, "RepeatType").SelectedIndex = 1;
            Image(sheet, "workflow-batch-preview.png"); ApproveNext(sheet); await sheet.SaveAndCloseAsync(false);
            Check("batch dialog saves selected entry only", !sheet.IsVisible && c.FindTodo(child.Id)!.Kind == TodoKind.Once);
        });
        await DriveAsync(() => PlannerDialogs.MoveHierarchy(window, c, c.FindTodo(first.Id)!, c.FindTodo(target.Id)), async sheet =>
        {
            Image(sheet, "workflow-hierarchy.png"); await sheet.SaveAndCloseAsync(false);
            Check("hierarchy dialog nests intact subtree", !sheet.IsVisible && c.FindTodo(first.Id)!.ParentId == target.Id && c.FindTodo(child.Id)!.ParentId == first.Id);
        });
        await c.UndoAsync(); Check("hierarchy undo restores root with deadline", c.FindTodo(first.Id)!.ParentId == null && c.FindTodo(first.Id)!.Deadline != null);
        var added = Enumerable.Range(0, 25).Select(i => c.NewTodo("Workflow long list " + i)).ToArray(); await c.ExecuteAsync(new BatchCommand([.. added.Select(t => new SaveTodo(t))])); await Idle();
        var v = window.PlannerView!; v.TodoScroll.ScrollToTop(); await Idle(); var revision = c.State!.Revision;
        v.BeginTodoDrag(c.FindTodo(first.Id)!, new Point(90, 30)); v.ContinueTodoDrag(new Point(90, 70));
        Check("in-list drag acquires capture without OLE modal loop", v.TodoDragging && v.TodoScroll.IsMouseCaptured);
        var targetRow = v.Items.Children.OfType<Border>().Single(b => b.Tag is Todo t && t.Id == target.Id);
        double rowHeight = targetRow.ActualHeight, viewport = v.TodoScroll.ActualHeight;
        v.ContinueTodoDrag(targetRow.TranslatePoint(new Point(30, rowHeight * .9), v.TodoScroll)); window.UpdateLayout();
        Check("edge feedback does not resize card or viewport", Math.Abs(targetRow.ActualHeight - rowHeight) < .1 && Math.Abs(v.TodoScroll.ActualHeight - viewport) < .1);
        v.ContinueTodoDrag(targetRow.TranslatePoint(new Point(30, rowHeight * .5), v.TodoScroll)); window.UpdateLayout();
        Check("nesting feedback names target without layout jump", v.DragHint.Text.Contains(target.Title) && Math.Abs(targetRow.ActualHeight - rowHeight) < .1 && Math.Abs(v.TodoScroll.ActualHeight - viewport) < .1);
        var wheel = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, -360) { RoutedEvent = Mouse.PreviewMouseWheelEvent }; v.TodoScroll.RaiseEvent(wheel); await Idle();
        Check("captured drag wheel moves actual list without saving", wheel.Handled && v.TodoScroll.VerticalOffset > 0 && c.State.Revision == revision);
        string observedKey = "none"; KeyEventHandler traceKey = (_, e) => observedKey = e.Key + "/" + e.Handled;
        window.AddHandler(Keyboard.PreviewKeyDownEvent, traceKey, true);
        SendKey(v.TodoScroll, Key.Escape); window.RemoveHandler(Keyboard.PreviewKeyDownEvent, traceKey);
        Check($"Esc releases drag and leaves stored order intact (key={observedKey}, drag={v.TodoDragging}, capture={v.TodoScroll.IsMouseCaptured}, rev={c.State.Revision}/{revision})", !v.TodoDragging && !v.TodoScroll.IsMouseCaptured && c.State.Revision == revision);
        Check("drop center/edge zones unambiguous", PlannerView.TodoDropZone(5, 80) == -1 && PlannerView.TodoDropZone(40, 80) == 0 && PlannerView.TodoDropZone(75, 80) == 1);
        v.BeginTodoDrag(c.FindTodo(first.Id)!, new Point(90, 30)); v.ContinueTodoDrag(new Point(90, 70)); c.Select(c.Today); await Idle(); Check("date switch cancels stale gesture", !v.TodoDragging);
        c.Select(first.PlannedDate, true); v.TodoScroll.ScrollToTop(); await Idle(); Image(window, "workflow-todos.png");
        Check("calendar and rows share deadline prefix without timezone text", PlannerView.Prefix(c.FindTodo(first.Id)!, c.State).StartsWith("ddl:") && !PlannerView.Prefix(c.FindTodo(first.Id)!, c.State).Contains("+08:00"));
        bool wasEnglish = Localization.English;
        foreach (string language in new[] { "en", "zh-CN" })
        {
            Localization.Apply(language);
            Check("workflow localized resources complete " + language, new[] { "BulkEdit", "HierarchyMove", "Deadline", "TodoDragHint", "DeadlineInvalid", "BulkSelectSubtree" }.All(k => Application.Current.TryFindResource(k) is string value && !string.IsNullOrWhiteSpace(value)));
        }
        Localization.Apply(wasEnglish ? "en" : "zh-CN");
    }
}
