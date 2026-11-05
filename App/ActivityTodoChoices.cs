using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;

internal static class ActivityTodoChoices
{
    internal static ActivityDialogs.TodoChoice[] On(PlannerController c, DateOnly date, TodoRef? selected)
    {
        var rows = new List<ActivityDialogs.TodoChoice> { new(null, MainWindow.Text("ActivityNoLink")) };
        void Add(TodoDateNode node, int depth)
        {
            var t = node.Value;
            if (!node.ContextOnly) rows.Add(new(TodoRef.Of(t), new string('　', depth) + (t.Status == TodoStatus.Completed ? "✓ " : "") + PlannerView.Prefix(t, c.State!, date) + t.Title));
            // Include all children: completion folding in the main view must not make a link unreachable.
            foreach (var child in node.Children) Add(child, depth + 1);
        }
        foreach (var node in c.Tree(date)) Add(node, 0);
        if (selected != null && rows.All(r => r.Reference != selected))
        {
            string title;
            try { title = PlannerQueries.Resolve(c.State!, selected).Title; }
            catch (PlannerException) { title = MainWindow.Text("ActivityFormerTodo"); }
            rows.Add(new(selected, MainWindow.Text("ActivityExistingLink") + " · " + title));
        }
        return rows.ToArray();
    }
    internal static ComboBox Add(PlannerDialogs.Sheet sheet, PlannerController c, Activity activity)
    {
        var initial = activity.Intervals.Length == 0 ? c.SelectedDate : PlannerTime.LocalDate(activity.Intervals.Min(p => p.Start), c.State!.Options.TimeZoneId);
        var day = new DatePicker { SelectedDate = initial.ToDateTime(TimeOnly.MinValue) }; sheet.Field("ActivityTodoDate", day);
        var linked = new ComboBox { DisplayMemberPath = nameof(ActivityDialogs.TodoChoice.Name), MinHeight = 28, MaxDropDownHeight = 290 };
        TodoRef? selection = activity.Todo; bool updating = false;
        void Refresh()
        {
            if (day.SelectedDate is not DateTime date) return;
            updating = true; var rows = On(c, DateOnly.FromDateTime(date), selection); linked.ItemsSource = rows;
            linked.SelectedItem = rows.FirstOrDefault(r => r.Reference == selection) ?? rows[0]; updating = false;
        }
        linked.SelectionChanged += (_, _) => { if (!updating) selection = (linked.SelectedItem as ActivityDialogs.TodoChoice)?.Reference; };
        day.SelectedDateChanged += (_, _) => Refresh(); day.DateValidationError += (_, e) => { e.ThrowException = false; sheet.Error.Text = MainWindow.Text("PlannerDateRequired"); };
        Refresh(); sheet.Field("ActivityTodo", linked); return linked;
    }
}
