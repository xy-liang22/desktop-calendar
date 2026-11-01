using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DesktopCalendar;
internal static class PrototypeViews
{
    internal static SolidColorBrush Brush(string color) => (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
    private static string T(string key) => MainWindow.Text(key);
    private static TextBlock Label(string text, double size = 13, string color = "#EDF2F5") => new()
    {
        Text = text, FontSize = size, Foreground = Brush(color), TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center, ToolTip = text
    };
    private static Border Panel(UIElement child) => new() { Background = Brush("#35101A24"), CornerRadius = new CornerRadius(12), Padding = new Thickness(14), Child = child };
    private static Grid Rows(params GridLength[] heights)
    {
        var grid = new Grid();
        foreach (var h in heights) grid.RowDefinitions.Add(new RowDefinition { Height = h });
        return grid;
    }
    private static readonly GridLength Star = new(1, GridUnitType.Star);
    private static void Add(Grid grid, UIElement element, int row = 0, int col = 0)
    {
        Grid.SetRow(element, row); Grid.SetColumn(element, col); grid.Children.Add(element);
    }
    private static StackPanel Heading(string title, string subtitle, double size = 20)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 4, 0, 18) };
        var heading = Label(title, size); heading.FontWeight = FontWeights.SemiBold;
        stack.Children.Add(heading);
        var sub = Label(subtitle, 11, "#A2AFBC"); sub.Margin = new Thickness(0, 7, 0, 0); stack.Children.Add(sub);
        return stack;
    }
    private static GridSplitter Splitter() => new()
    {
        Width = 8, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch,
        Background = Brushes.Transparent, ResizeDirection = GridResizeDirection.Columns,
        ResizeBehavior = GridResizeBehavior.PreviousAndNext, ShowsPreview = false,
        ToolTip = T("SplitTip"), Focusable = true
    };
    internal static Grid Month(TimelineView? timeline = null)
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5.2, GridUnitType.Star), MinWidth = 340 });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.7, GridUnitType.Star), MinWidth = 200 });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.9, GridUnitType.Star), MinWidth = 220 });
        var calendar = Rows(GridLength.Auto, new GridLength(30), Star, GridLength.Auto);
        var header = Heading(T("MonthTitle"), T("MonthSubtitle"), 25);
        Add(calendar, header);
        var weekdays = new UniformGrid { Columns = 7 };
        foreach (string day in DemoContent.Weekdays) { var label = Label(day, 11, "#A2AFBC"); label.HorizontalAlignment = HorizontalAlignment.Center; weekdays.Children.Add(label); }
        Add(calendar, weekdays, 1);
        var days = CalendarDays(2026, 10);
        var todo = Rows(GridLength.Auto, Star, GridLength.Auto);
        var selectedDate = Label(string.Format(CultureInfo.InvariantCulture, T("SelectedDate"), 10, 29), 11, "#A3D9C4");
        var todoHeading = Heading(T("TodoTitle"), T("PlanCaption"), 18);
        todoHeading.Children.Add(selectedDate); selectedDate.Margin = new Thickness(0, 10, 0, 0);
        Add(todo, todoHeading);
        Add(todo, new ScrollViewer { Content = TodoList() }, 1);
        var disclaimer = Label(T("ReadOnly"), 11, "#A2AFBC"); disclaimer.TextWrapping = TextWrapping.Wrap; disclaimer.Margin = new Thickness(0, 15, 0, 4); Add(todo, disclaimer, 2);
        var actual = Rows(GridLength.Auto, Star);
        Add(actual, Heading(T("ActivityTitle"), T("ActualCaption"), 18));
        Add(actual, timeline ?? new TimelineView(false, 3), 1);
        Button? selected = null;
        foreach (Button button in days.Children)
        {
            var date = (DateTime)button.Tag;
            button.Click += (_, _) =>
            {
                if (selected != null) selected.Background = Brush("#0BFFFFFF");
                selected = button; button.Background = Brush("#48608275");
                selectedDate.Text = string.Format(CultureInfo.InvariantCulture, T("SelectedDate"), date.Month, date.Day);
            };
            if (date.Month == 10 && date.Day == 29) { selected = button; button.Background = Brush("#48608275"); }
        }
        Add(calendar, days, 2);
        var ribbon = new StackPanel { Margin = new Thickness(8, 13, 8, 4) };
        ribbon.Children.Add(Label(DemoContent.WeekRibbon, 11, "#A3D9C4"));
        var ribbonBody = Label(DemoContent.WeekRibbonBody, 12); ribbonBody.Margin = new Thickness(0, 6, 0, 0); ribbon.Children.Add(ribbonBody);
        var lunar = Label(DemoContent.LunarPending, 10, "#8998A6"); lunar.Margin = new Thickness(0, 10, 0, 0); ribbon.Children.Add(lunar);
        Add(calendar, ribbon, 3);
        Add(root, Panel(calendar)); Add(root, Splitter(), 0, 1); Add(root, Panel(todo), 0, 2); Add(root, Splitter(), 0, 3); Add(root, Panel(actual), 0, 4);
        return root;
    }
    private static ItemsControl TodoList() => new()
    {
        ItemsSource = DemoTodo.View(DemoContent.Todos),
        ItemTemplate = (DataTemplate)Application.Current.FindResource("FullTodoTemplate")
    };
    internal static Grid Week(TimelineView? timeline = null)
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Star), MinWidth = 570 });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star), MinWidth = 230 });
        var week = Rows(GridLength.Auto, new GridLength(42), Star);
        Add(week, Heading(T("WeekTitle"), T("WeekSubtitle"), 23));
        var dayHeader = new Grid(); dayHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        for (int i = 0; i < 7; i++)
        {
            dayHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = Star });
            var text = Label(DemoContent.Weekdays[i] + "  " + (i < 6 ? (26 + i).ToString(CultureInfo.InvariantCulture) : "1"), 11, i == 3 ? "#A3D9C4" : "#A2AFBC"); text.HorizontalAlignment = HorizontalAlignment.Center; Add(dayHeader, text, 0, i + 1);
        }
        dayHeader.Margin = new Thickness(0, 0, 10, 0);
        Add(week, dayHeader, 1); Add(week, timeline ?? new TimelineView(true, 0), 2);
        var side = new StackPanel(); side.Children.Add(Heading(T("WeekPlan"), DemoContent.GoalProgress[0], 18));
        for (int i = 0; i < 3; i++)
        {
            var name = Label(DemoContent.GoalNames[i], 13); name.TextWrapping = TextWrapping.Wrap; side.Children.Add(name);
            var progress = new ProgressBar { Value = i == 2 ? 60 : 67, Height = 4, Margin = new Thickness(0, 10, 0, 7), Foreground = Brush(DemoContent.Colors[i]), Background = Brush("#263B45"), BorderThickness = new Thickness(0) }; side.Children.Add(progress);
            var detail = Label(DemoContent.GoalProgress[i], 10, "#A2AFBC"); detail.Margin = new Thickness(0, 0, 0, 20); side.Children.Add(detail);
        }
        var statsTitle = Label(T("WeekStats"), 13); statsTitle.Margin = new Thickness(0, 7, 0, 10); side.Children.Add(statsTitle);
        var stats = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < 3; i++) { var stat = Label(DemoContent.StatLabels[i], 10, DemoContent.Colors[i]); stat.Margin = new Thickness(0, 0, 12, 0); stats.Children.Add(stat); } side.Children.Add(stats);
        var review = Heading(T("WeekReview"), DemoContent.ReviewCaption, 18); review.Margin = new Thickness(0, 25, 0, 14); side.Children.Add(review);
        var body = Label(DemoContent.ReviewBody, 12, "#C2CCD5"); body.TextWrapping = TextWrapping.Wrap; body.LineHeight = 22; side.Children.Add(body);
        Add(root, Panel(week)); Add(root, Splitter(), 0, 1); Add(root, Panel(new ScrollViewer { Content = side }), 0, 2);
        return root;
    }
    internal static UniformGrid CalendarDays(int year, int month)
    {
        var first = new DateTime(year, month, 1);
        int offset = ((int)first.DayOfWeek + 6) % 7;
        int rows = (offset + DateTime.DaysInMonth(year, month) + 6) / 7;
        var days = new UniformGrid { Columns = 7, Rows = rows };
        for (int i = 0; i < rows * 7; i++)
        {
            var date = first.AddDays(i - offset); bool current = date.Month == month;
            var tasks = current ? DemoContent.CalendarTodos(date.Day) : Array.Empty<DemoTodo>();
            var cell = new CalendarPreviewCell(date, tasks, current);
            var button = new Button { Content = cell, Tag = date, ClipToBounds = true, Padding = new Thickness(0), Margin = new Thickness(2),
                HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
                Background = Brush(i % 7 >= 5 ? "#162A3D48" : "#0BFFFFFF"),
                ToolTip = T("CalendarLabel") + " · " + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "\n" + string.Join("\n", tasks.Select(t => t.Title)) };
            days.Children.Add(button);
        }
        return days;
    }
}
