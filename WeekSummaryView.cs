using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static class WeekSummary
{
    internal static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    internal static string Progress(PlannerState state, WeekGoal goal)
    { var p = PlannerQueries.Goal(state, goal); return Number(goal.Kind == GoalKind.Duration ? p.Effective / 3600 : p.Effective) + " / " + Number(goal.Kind == GoalKind.Duration ? goal.Target / 3600 : goal.Target) + " " + (goal.Kind == GoalKind.Duration ? MainWindow.Text("WeekHours") : goal.CountUnit) + " · " + MainWindow.Text(p.Override == null ? "GoalAutomatic" : "GoalManual"); }
    internal static decimal Total(PlannerState state, WeekKey week)
    { var range = PlannerTime.WeekRange(week); return PlannerTime.Seconds(state.Activities.SelectMany(a => a.Intervals).Select(p => PlannerTime.Clip(p, range)).OfType<TimeInterval>()); }
    internal static string Markdown(PlannerState state, WeekKey week)
    {
        string Clean(string text) => text.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
        var lines = new System.Collections.Generic.List<string> { "## " + MainWindow.Text("WeekStatsTitle") + " · " + week.Start.ToString("yyyy-MM-dd"), MainWindow.Text("WeekDenominator") + " " + Number((decimal)PlannerTime.WeekRange(week).Duration.TotalHours) + " " + MainWindow.Text("WeekHours"), "| " + MainWindow.Text("PlannerCategory") + " | " + MainWindow.Text("WeekHours") + " | % |", "| --- | --- | --- |" };
        foreach (var t in PlannerQueries.CategoryStatistics(state, week).OrderByDescending(t => t.Seconds)) lines.Add("| " + Clean(state.Categories.FirstOrDefault(c => c.Id == t.CategoryId)?.Name ?? MainWindow.Text("WeekUncategorized")) + " | " + Number(t.Seconds / 3600) + " | " + Number(t.FractionOfWeek * 100) + "% |");
        lines.Add(MainWindow.Text("WeekGlobal") + ": " + Number(Total(state, week) / 3600) + " " + MainWindow.Text("WeekHours")); lines.Add(MainWindow.Text("WeekOverlapHint"));
        foreach (var g in state.Goals.Where(g => g.Week == week).OrderBy(g => g.SortKey)) lines.Add("- " + Clean(g.Title) + ": " + Progress(state, g)); return string.Join("\n", lines);
    }
    internal static string ReviewText(WeeklyReview review) => review.Markdown + "\n\n" + string.Join("\n\n", review.Sections.Select(s => "## " + s.Heading + "\n" + s.Markdown));
}
internal sealed class WeekSummaryView : StackPanel
{
    readonly MainWindow owner;
    readonly PlannerController c;
    readonly Func<WeekKey> week;
    internal WeekSummaryView(MainWindow window, PlannerController controller, Func<WeekKey>? identity = null)
    { owner = window; c = controller; week = identity ?? (() => c.Week()); Loaded += (_, _) => { c.Changed += Refresh; Refresh(); }; Unloaded += (_, _) => c.Changed -= Refresh; Refresh(); }
    void Text(string text, double size = 12, string color = "#EDF2F5") { var label = PlannerView.Text(text, size, color); label.TextWrapping = TextWrapping.Wrap; label.Margin = new Thickness(0, 4, 0, 6); Children.Add(label); }
    internal void Refresh()
    {
        Children.Clear(); if (c.State == null) { Text(MainWindow.Text(c.NoticeKey)); return; } var key = week();
        Text(MainWindow.Text("WeekPlan"), 17); Text(key.Start.ToString("yyyy-MM-dd") + " – " + key.Start.AddDays(6).ToString("MM-dd"), 11, "#A2AFBC");
        var add = PlannerView.Button(MainWindow.Text("GoalNew"), () => WeekGoalDialogs.Edit(owner, c, key)); add.IsEnabled = c.Writable; Children.Add(add);
        var goals = c.State.Goals.Where(g => g.Week == key).OrderBy(g => g.SortKey).ToArray(); if (goals.Length == 0) Text(MainWindow.Text("GoalEmpty"), 12, "#A2AFBC");
        foreach (var g in goals)
        {
            var content = new StackPanel(); var progress = PlannerQueries.Goal(c.State, g);
            var menu = new ContextMenu(); var heading = new DockPanel(); var edit = PlannerView.Button("⋯", () => { menu.IsOpen = true; }); edit.IsEnabled = c.Writable; menu.PlacementTarget = edit; DockPanel.SetDock(edit, Dock.Right); heading.Children.Add(edit);
            var editItem = new MenuItem { Header = MainWindow.Text("GoalEdit") }; editItem.Click += (_, _) => { menu.IsOpen = false; WeekGoalDialogs.Edit(owner, c, key, g); }; menu.Items.Add(editItem);
            var deleteItem = new MenuItem { Header = MainWindow.Text("GoalDelete") }; deleteItem.Click += async (_, _) => { menu.IsOpen = false; if (PlannerDialogs.Confirm(owner, MainWindow.Text("GoalDelete"), MainWindow.Text("GoalDeleteConfirm"))) await c.ExecuteAsync(new DeleteGoal(g.Id)); }; menu.Items.Add(deleteItem);
            var check = new CheckBox { Content = g.Emoji + " " + g.Title, IsChecked = progress.Complete, IsEnabled = c.Writable && g.Kind == GoalKind.Check && g.Source == GoalSource.None, Margin = new Thickness(0, 4, 0, 4) }; check.Click += async (_, _) => await c.ExecuteAsync(new SaveGoal(g with { Checked = check.IsChecked == true, ManualOverride = null })); heading.Children.Add(check); content.Children.Add(heading);
            var status = PlannerView.Text(WeekSummary.Progress(c.State, g), 10, "#A3D9C4"); status.TextWrapping = TextWrapping.Wrap; content.Children.Add(status);
            content.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = progress.Effective >= g.Target ? 100 : (double)(progress.Effective / g.Target * 100), Height = 4, Margin = new Thickness(0, 5, 0, 6), Foreground = ThemeColors.Brush("#A3D9C4"), Background = ThemeColors.Brush("#263B45"), BorderThickness = new Thickness(0) });
            if (progress.OutsideWeekTodos.Length > 0) { var warning = PlannerView.Text(MainWindow.Text("GoalOutside"), 10, "#E3BF89"); warning.TextWrapping = TextWrapping.Wrap; content.Children.Add(warning); }
            var actions = new WrapPanel();
            if (g.ManualOverride != null) actions.Children.Add(PlannerView.Button(MainWindow.Text("GoalRestore"), async () => await c.ExecuteAsync(new SaveGoal(g with { ManualOverride = null })))); content.Children.Add(actions);
            Children.Add(new Border { Tag = g, ContextMenu = menu, Child = content, CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1), BorderBrush = ThemeColors.Brush("#304352"), Padding = new Thickness(6), Margin = new Thickness(0, 7, 0, 0) });
        }
        Text(MainWindow.Text("WeekStatsTitle"), 17); Text(MainWindow.Text("WeekDenominator") + " " + WeekSummary.Number((decimal)PlannerTime.WeekRange(key).Duration.TotalHours) + " " + MainWindow.Text("WeekHours"), 10, "#A2AFBC");
        foreach (var duration in PlannerQueries.CategoryStatistics(c.State, key).OrderByDescending(t => t.Seconds))
        {
            var category = c.State.Categories.FirstOrDefault(x => x.Id == duration.CategoryId); Text((category?.Name ?? MainWindow.Text("WeekUncategorized")) + " · " + WeekSummary.Number(duration.Seconds / 3600) + "h · " + WeekSummary.Number(duration.FractionOfWeek * 100) + "%", 11);
            Children.Add(new ProgressBar { Value = (double)(duration.FractionOfWeek * 100), Maximum = 100, Height = 4, Foreground = ThemeColors.Brush(ThemeColors.Accent(category?.Color ?? "#A3D9C4")), Background = ThemeColors.Brush("#263B45"), BorderThickness = new Thickness(0) });
        }
        Text(MainWindow.Text("WeekGlobal") + " " + WeekSummary.Number(WeekSummary.Total(c.State, key) / 3600) + "h", 11); Text(MainWindow.Text("WeekOverlapHint"), 10, "#A2AFBC");
        Text(MainWindow.Text("WeekReview"), 17); var review = c.State.Reviews.FirstOrDefault(r => r.Week == key); var reviewButton = PlannerView.Button(MainWindow.Text("ReviewEdit"), () => WeeklyReviewDialog.Edit(owner, c, key)); reviewButton.IsEnabled = c.Writable; Children.Add(reviewButton);
        if (review == null) Text(MainWindow.Text("ReviewEmpty"), 11, "#A2AFBC");
        else
        {
            Children.Add(new FlowDocumentScrollViewer { Document = MarkdownNotes.Render(WeekSummary.ReviewText(review), NoteFormat.Markdown, new(c.DataDirectory, () => c.State)), IsToolBarVisible = false, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            foreach (var snapshot in review.Statistics) { var expander = new Expander { Header = MainWindow.Text("ReviewSnapshots") + " · " + snapshot.GeneratedAt.ToLocalTime().ToString("MM-dd HH:mm"), Content = new FlowDocumentScrollViewer { Document = MarkdownNotes.Render(snapshot.Markdown, NoteFormat.Markdown, new(c.DataDirectory, () => c.State)), IsToolBarVisible = false, MaxHeight = 250 } }; Children.Add(expander); }
        }
    }
}
