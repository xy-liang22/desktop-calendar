using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;

internal sealed class ActivityWeekView : Grid
{
    readonly PlannerController controller;
    readonly TextBlock title = PlannerView.Text("", 20);
    readonly TextBlock[] dates = new TextBlock[7];
    internal ActivityWeekView(MainWindow owner, PlannerController c, TimelineView timeline)
    {
        controller = c; ColumnDefinitions.Add(new() { Width = new GridLength(4, GridUnitType.Star), MinWidth = 570 }); ColumnDefinitions.Add(new() { Width = new GridLength(8) }); ColumnDefinitions.Add(new() { Width = new GridLength(1.2, GridUnitType.Star), MinWidth = 230 });
        var main = new Grid(); main.RowDefinitions.Add(new() { Height = GridLength.Auto }); main.RowDefinitions.Add(new() { Height = new GridLength(40) }); main.RowDefinitions.Add(new());
        var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 12) }; var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        void Move(int days) { int day = c.SelectedDate.DayNumber + days; if (day >= 0 && day <= DateOnly.MaxValue.DayNumber - 7) c.Select(DateOnly.FromDayNumber(day), true); }
        buttons.Children.Add(PlannerView.Button("‹", () => Move(-7))); buttons.Children.Add(PlannerView.Button(MainWindow.Text("PlannerToday"), () => c.Select(c.Today, true))); buttons.Children.Add(PlannerView.Button("›", () => Move(7))); buttons.Children.Add(PlannerView.Button("＋", () => ActivityDialogs.Edit(owner, c)));
        DockPanel.SetDock(buttons, Dock.Right); heading.Children.Add(buttons); heading.Children.Add(title); main.Children.Add(heading);
        var header = new Grid { Margin = new Thickness(40, 0, 17, 0) }; for (int i = 0; i < 7; i++) { header.ColumnDefinitions.Add(new()); dates[i] = PlannerView.Text("", 10, "#A2AFBC"); dates[i].HorizontalAlignment = HorizontalAlignment.Center; SetColumn(dates[i], i); header.Children.Add(dates[i]); } SetRow(header, 1); main.Children.Add(header); SetRow(timeline, 2); main.Children.Add(timeline);
        Border Panel(UIElement child) => new() { Background = ThemeColors.Brush("#35101A24"), CornerRadius = new CornerRadius(12), Padding = new Thickness(14), Child = child };
        Children.Add(Panel(main)); var splitter = new GridSplitter { Width = 8, HorizontalAlignment = HorizontalAlignment.Stretch, Background = System.Windows.Media.Brushes.Transparent, ResizeDirection = GridResizeDirection.Columns, ResizeBehavior = GridResizeBehavior.PreviousAndNext }; SetColumn(splitter, 1); Children.Add(splitter);
        var side = new ScrollViewer { Content = new WeekSummaryView(owner, c), VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; var panel = Panel(side); SetColumn(panel, 2); Children.Add(panel);
        c.Changed += Refresh; owner.Closed += (_, _) => Detach(); Refresh();
    }
    internal void Detach() => controller.Changed -= Refresh;
    void Refresh()
    {
        var date = controller.SelectedDate; int back = controller.State == null ? 0 : ((int)date.DayOfWeek - (int)controller.State.Options.WeekStart + 7) % 7;
        var start = DateOnly.FromDayNumber(Math.Clamp(date.DayNumber - back, 0, DateOnly.MaxValue.DayNumber - 7));
        title.Text = MainWindow.Text("ActivityWeek") + " · " + start.ToString("M/d", CultureInfo.InvariantCulture);
        for (int i = 0; i < 7; i++) dates[i].Text = start.AddDays(i).ToString("MM-dd ddd", CultureInfo.GetCultureInfo(MainWindow.Text("PlannerUiLanguage")));
    }
}
