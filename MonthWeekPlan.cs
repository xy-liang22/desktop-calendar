using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static class MonthWeekPlan
{
    internal static FrameworkElement Create(MainWindow owner, PlannerController c, WeekKey week, bool side)
    {
        var goals = c.State?.Goals.Where(g => g.Week == week).OrderBy(g => g.SortKey).ToArray() ?? [];
        string text = string.Join("\n", goals.Select(g => g.Title + " · " + WeekSummary.Progress(c.State!, g)));
        var body = new StackPanel(); foreach (var g in goals) { var row = PlannerView.Text(g.Title + " · " + WeekSummary.Progress(c.State!, g), 10, "#A3D9C4"); row.TextWrapping = TextWrapping.Wrap; row.Margin = new Thickness(2, 2, 2, 2); body.Children.Add(row); }
        Popup? popup = null; var manage = PlannerView.Button(MainWindow.Text("WeekManage"), () => { if (popup != null) popup.IsOpen = false; WeekGoalDialogs.Manage(owner, c, week); }); body.Children.Add(manage);
        if (side)
        {
            var view = new StackPanel(); var title = PlannerView.Button(week.Start.ToString("MM/dd"), () => WeekGoalDialogs.Manage(owner, c, week), MainWindow.Text("WeekManage")); title.Tag = week; title.FontSize = 10; title.Padding = new Thickness(1); view.Children.Add(title);
            foreach (var g in goals.Take(1))
            {
                view.Children.Add(new TextBlock { Text = g.Title + (goals.Length > 1 ? " +" + (goals.Length - 1) : ""), FontSize = 9, Foreground = ThemeColors.Brush("#A3D9C4"), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = text });
                var p = PlannerQueries.Goal(c.State!, g); var ratio = p.Effective >= g.Target ? 100 : (double)(p.Effective / g.Target * 100); view.Children.Add(PlannerView.Text(WeekSummary.Number((decimal)ratio) + "%", 9, "#A2AFBC")); view.Children.Add(new ProgressBar { Value = ratio, Height = 2, Foreground = ThemeColors.Brush("#A3D9C4") });
            }
            if (goals.Length == 0) view.Children.Add(PlannerView.Text(MainWindow.Text("GoalEmpty"), 9, "#A2AFBC"));
            return new Border { Child = view, Background = ThemeColors.Brush("#101E2A"), CornerRadius = new CornerRadius(4), Margin = new Thickness(2), Padding = new Thickness(2), ClipToBounds = true, ToolTip = text.Length > 0 ? text : MainWindow.Text("GoalEmpty") };
        }
        var header = new TextBlock { Text = "⌄ " + week.Start.ToString("MM/dd") + " · " + (goals.Length == 0 ? MainWindow.Text("GoalEmpty") : goals[0].Title + " · " + WeekSummary.Progress(c.State!, goals[0]) + (goals.Length > 1 ? " +" + (goals.Length - 1) : "")), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = text };
        var toggle = new ToggleButton { Content = header, Height = 20, Padding = new Thickness(4, 0, 4, 0), FontSize = 10, HorizontalContentAlignment = HorizontalAlignment.Left, Style = (Style)Application.Current.FindResource("DarkToggle"), Background = ThemeColors.Brush("#162B35"), Margin = new Thickness(2, 0, 2, 2), Tag = week };
        popup = new Popup { PlacementTarget = toggle, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, Child = new Border { Background = ThemeColors.Brush("#F5202E3B"), BorderBrush = ThemeColors.Brush("#526474"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(8), Child = new ScrollViewer { Content = body, MaxHeight = 220, Width = 300, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } } };
        popup.SetBinding(Popup.IsOpenProperty, new Binding(nameof(ToggleButton.IsChecked)) { Source = toggle, Mode = BindingMode.TwoWay }); manage.Click += (_, _) => popup.IsOpen = false; toggle.Unloaded += (_, _) => popup.IsOpen = false; return toggle;
    }
}
