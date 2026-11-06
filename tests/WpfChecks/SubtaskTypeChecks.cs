using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static partial class StageThreeChecks
{
    static async Task RunSubtaskTypeChecks(MainWindow window)
    {
        var c = window.Session.Planner!; var day = c.Today; c.Select(day, true); window.SetPage(false);
        foreach (var kind in new[] { TodoKind.Once, TodoKind.CarryHistory, TodoKind.CarryLatest })
        {
            var p = c.NewTodo("Subtask test parent " + kind) with { Kind = kind }; await c.ExecuteAsync(new SaveTodo(p));
            await DriveAsync(() => PlannerDialogs.Edit(window, c, null, p.Id), async s =>
            {
                var type = Choice(s, "RepeatType"); int expected = kind == TodoKind.Once ? 0 : kind == TodoKind.CarryHistory ? 1 : 2;
                Check("new subtask inherits selected parent type " + kind, type.SelectedIndex == expected && type.Items.Count == 3);
                Field(s, "PlannerTitle").Text = "Subtask inherited " + kind; await s.SaveAndCloseAsync(false); Check("inherited subtask saves and closes " + kind, !s.IsVisible);
            });
            Check("inherited type persists as a child " + kind, c.State!.Todos.Any(t => t.ParentId == p.Id && t.Kind == kind));
            await DriveAsync(() => PlannerDialogs.Edit(window, c, null, p.Id), async s =>
            {
                Choice(s, "RepeatType").SelectedIndex = kind == TodoKind.Once ? 2 : 0; Field(s, "PlannerTitle").Text = "Subtask override " + kind;
                if (kind == TodoKind.CarryHistory) { await Idle(); Image(s, "subtask-type-override.png"); }
                await s.SaveAndCloseAsync(false);
            });
            Check("subtask override does not change parent kind " + kind, c.State!.Todos.Single(t => t.Id == p.Id).Kind == kind && c.State.Todos.Any(t => t.ParentId == p.Id && t.Title == "Subtask override " + kind && t.Kind == (kind == TodoKind.Once ? TodoKind.CarryLatest : TodoKind.Once)));
        }
        var node = c.NewTodo("Subtask shared cycle parent"); var series = Guid.NewGuid(); await c.ExecuteAsync(new CreateSeries(new() { Start = day, Interval = 2, Count = 4, TimeZoneId = c.DisplayZone }, [node], series));
        var parentRef = TodoRef.For(new(series, node.Id, day));
        await DriveAsync(() => PlannerDialogs.Edit(window, c, null, parentRef.Id), async s =>
        {
            Check("recurring parent defaults to shared cycle from selected date", Choice(s, "RepeatType").SelectedIndex == 3 && (EditScope)Choice(s, "RepeatScope").SelectedValue == EditScope.FromDate && !Children<RecurrenceFields>(s).Any());
            Field(s, "PlannerTitle").Text = "Subtask following"; await Idle(); Image(s, "subtask-type-follow.png"); ApproveNext(s); await s.SaveAndCloseAsync(false); Check("following subtask commits after explicit scope confirmation", !s.IsVisible);
        });
        var occurrence = c.On(day).Single(t => t.Title == "Subtask following");
        Check("shared cycle child appears next occurrence with a separate ID", c.On(day.AddDays(2)).Any(t => t.Title == occurrence.Title && t.Id != occurrence.Id && t.Origin?.NodeId == occurrence.Origin?.NodeId));
        foreach (int typeIndex in new[] { 0, 1, 2 })
        {
            await DriveAsync(() => PlannerDialogs.Edit(window, c, null, parentRef.Id), async s =>
            {
                Choice(s, "RepeatType").SelectedIndex = typeIndex; Field(s, "PlannerTitle").Text = "Subtask attached " + typeIndex;
                Check("local child hides series scope and permits independent completion " + typeIndex, Choice(s, "RepeatScope").Visibility == Visibility.Collapsed && Children<CheckBox>(s).Single(b => Equals(b.Content, MainWindow.Text("PlannerCompleted"))).IsEnabled);
                await s.SaveAndCloseAsync(false); Check("local mixed-type child saves " + typeIndex, !s.IsVisible);
            });
            var child = c.State!.Todos.Single(t => t.Title == "Subtask attached " + typeIndex);
            Check("local child has selected type and same parent occurrence " + typeIndex, child.ParentId == parentRef.Id && child.Origin == null && child.Kind == (typeIndex == 0 ? TodoKind.Once : typeIndex == 1 ? TodoKind.CarryHistory : TodoKind.CarryLatest));
        }
        await c.ExecuteAsync(new SetTodoCompleted(parentRef, true)); long revision = c.State!.Revision;
        await DriveAsync(() => PlannerDialogs.Edit(window, c, null, parentRef.Id), async s => { Field(s, "PlannerTitle").Text = "must preserve completed history"; Check("completed occurrence does not silently acquire new recurring child", !await s.SaveAndCloseAsync(false) && s.IsVisible && s.Error.Text == MainWindow.Text("ChildCompletedParent") && c.State!.Revision == revision); Click(s.Actions.Children.OfType<Button>().First()); });
        await c.UndoAsync();
        var loaded = await PlannerWorkspace.OpenAsync(new(c.FilePath), c.State!.Options);
        Check("subtask types and occurrence attachment reload unchanged", loaded.Snapshot!.Todos.Where(t => t.Title.StartsWith("Subtask attached ")).All(t => t.ParentId == parentRef.Id) && loaded.Snapshot.Todos.Count(t => t.Title.StartsWith("Subtask attached ")) == 3);
    }
}
