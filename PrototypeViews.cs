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
    internal static Grid Month()
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
        var days = new UniformGrid { Columns = 7, Rows = 5 };
        var todo = Rows(GridLength.Auto, Star, GridLength.Auto);
        var selectedDate = Label(string.Format(CultureInfo.InvariantCulture, T("SelectedDate"), 10, 29), 11, "#A3D9C4");
        var todoHeading = Heading(T("TodoTitle"), T("PlanCaption"), 18);
        todoHeading.Children.Add(selectedDate); selectedDate.Margin = new Thickness(0, 10, 0, 0);
        Add(todo, todoHeading);
        Add(todo, new ScrollViewer { Content = TodoList() }, 1);
        var disclaimer = Label(T("ReadOnly"), 11, "#A2AFBC"); disclaimer.TextWrapping = TextWrapping.Wrap; disclaimer.Margin = new Thickness(0, 15, 0, 4); Add(todo, disclaimer, 2);
        var actual = Rows(GridLength.Auto, Star);
        Add(actual, Heading(T("ActivityTitle"), T("ActualCaption"), 18));
        var daily = Timeline(false, 3); Add(actual, daily, 1);
        Button? selected = null;
        for (int i = 0; i < 35; i++)
        {
            var date = new DateTime(2026, 9, 28).AddDays(i);
            bool currentMonth = date.Month == 10;
            var content = new StackPanel { Margin = new Thickness(6, 4, 4, 2) };
            var number = Label(date.Day.ToString(CultureInfo.InvariantCulture), 14, currentMonth ? "#EDF2F5" : "#6E7C89");
            number.FontWeight = currentMonth && date.Day == 29 ? FontWeights.Bold : FontWeights.Normal;
            var dateHeader = new Grid(); dateHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = Star }); dateHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Add(dateHeader, number);
            bool hasTasks = currentMonth && i % 3 != 0;
            var smallCount = Label(hasTasks ? (i % 5 == 0 ? "+3" : "+1") : "", 9, "#A3D9C4");
            smallCount.Visibility = Visibility.Collapsed; Add(dateHeader, smallCount, 0, 1);
            content.Children.Add(dateHeader);
            var taskPreview = new StackPanel();
            if (currentMonth && i % 3 != 0)
            {
                var item = Label(DemoContent.CalendarTasks[i % 7], 10, DemoContent.Colors[i % 3]);
                item.Margin = new Thickness(0, 9, 0, 0); taskPreview.Children.Add(item);
                if (i % 5 == 0) { var more = Label("+2", 10, "#A2AFBC"); more.Margin = new Thickness(0, 4, 0, 0); taskPreview.Children.Add(more); }
            }
            content.Children.Add(taskPreview);
            var button = new Button { Content = content, ClipToBounds = true, Padding = new Thickness(0), Margin = new Thickness(2), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top, Background = Brush(i % 7 >= 5 ? "#162A3D48" : "#0BFFFFFF"), ToolTip = T("CalendarLabel") + " · " + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + (hasTasks ? "\n" + DemoContent.CalendarTasks[i % 7] : "") };
            button.SizeChanged += (_, _) =>
            {
                bool tight = button.ActualHeight < 72;
                taskPreview.Visibility = tight ? Visibility.Collapsed : Visibility.Visible;
                smallCount.Visibility = tight && hasTasks ? Visibility.Visible : Visibility.Collapsed;
            };
            // Date selection is a local visual preview only, with the same static fixture week.
            button.Click += (_, _) =>
            {
                if (selected != null) selected.Background = Brush("#0BFFFFFF");
                selected = button; button.Background = Brush("#48608275");
                selectedDate.Text = string.Format(CultureInfo.InvariantCulture, T("SelectedDate"), date.Month, date.Day);
            };
            if (date.Month == 10 && date.Day == 29) { selected = button; button.Background = Brush("#48608275"); }
            days.Children.Add(button);
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
    private static StackPanel TodoList()
    {
        var stack = new StackPanel();
        for (int i = 0; i < DemoContent.TodoNames.Length; i++)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 13) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(23) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = Star });
            var fixture = DemoContent.Todos[i];
            var marker = new CheckBox { DataContext = fixture, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 4, 0), ToolTip = T("CompactDemoNotice") };
            marker.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new System.Windows.Data.Binding(nameof(DemoTodo.Completed)) { Mode = System.Windows.Data.BindingMode.TwoWay });
            Add(row, marker);
            var content = new StackPanel();
            var title = Label(DemoContent.TodoNames[i], 13, i == 3 ? "#8998A6" : "#EDF2F5"); title.TextWrapping = TextWrapping.Wrap;
            title.ClearValue(TextBlock.ForegroundProperty);
            var titleStyle = new Style(typeof(TextBlock));
            titleStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush("#EDF2F5")));
            var completed = new DataTrigger { Binding = new System.Windows.Data.Binding(nameof(DemoTodo.Completed)) { Source = fixture }, Value = true };
            completed.Setters.Add(new Setter(TextBlock.TextDecorationsProperty, TextDecorations.Strikethrough));
            completed.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brush("#8998A6")));
            titleStyle.Triggers.Add(completed); title.Style = titleStyle;
            content.Children.Add(title);
            var meta = Label(DemoContent.TodoMeta[i], 10, "#A2AFBC"); meta.Margin = new Thickness(0, 7, 0, 0); content.Children.Add(meta);
            if (i == 0)
            {
                foreach (var sub in DemoContent.Subtasks) { var child = Label("· " + sub, 11, "#A2AFBC"); child.Margin = new Thickness(0, 9, 0, 0); content.Children.Add(child); }
                var progress = new ProgressBar { Value = 50, Height = 3, Margin = new Thickness(0, 12, 10, 6), Foreground = Brush("#A3D9C4"), Background = Brush("#263B45"), BorderThickness = new Thickness(0) }; content.Children.Add(progress);
                content.Children.Add(Label(DemoContent.TodoProgress, 10, "#A3D9C4"));
            }
            Add(row, content, 0, 1);
            stack.Children.Add(new Border { Child = row, BorderBrush = Brush("#17FFFFFF"), BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 0, 0, 15) });
        }
        return stack;
    }
    internal static Grid Week()
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
        Add(week, dayHeader, 1); Add(week, Timeline(true, 0), 2);
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
    internal static ScrollViewer Timeline(bool week, int day)
    {
        const double hour = 50;
        var grid = new Grid { Height = 24 * hour, ClipToBounds = true };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        for (int i = 0; i < (week ? 7 : 1); i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = Star });
        var labels = new Canvas();
        for (int h = 0; h < 24; h++)
        {
            var label = Label(h.ToString("00", CultureInfo.InvariantCulture) + ":00", 9, "#8FA0AE"); Canvas.SetTop(label, h * hour); labels.Children.Add(label);
            var line = new Border { Height = 1, Background = Brush("#16FFFFFF"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(40, h * hour + 7, 0, 0) }; Grid.SetColumnSpan(line, week ? 8 : 2); grid.Children.Add(line);
        }
        Add(grid, labels);
        foreach (var block in DemoContent.Blocks.Where(b => week || b.Day == day))
        {
            var text = new StackPanel { ClipToBounds = true };
            var title = Label(block.Title, week ? 11 : 12, DemoContent.Colors[block.Color]); title.FontWeight = FontWeights.SemiBold; text.Children.Add(title);
            string start = TimeSpan.FromHours(block.Start).ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            string end = TimeSpan.FromHours(block.End).ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            var time = Label(start + "–" + end, 9, "#C3CDD5"); time.Margin = new Thickness(0, 5, 0, 0); text.Children.Add(time);
            if (!week) { var note = Label(block.Note, 10, "#ABBCC5"); note.Margin = new Thickness(0, 8, 0, 0); text.Children.Add(note); }
            var card = new Border
            {
                Background = Brush(block.Color == 0 ? "#D1344D49" : block.Color == 1 ? "#D144405C" : "#D14F473C"),
                BorderBrush = Brush(DemoContent.Colors[block.Color]), BorderThickness = new Thickness(2, 0, 0, 0), CornerRadius = new CornerRadius(5),
                Padding = new Thickness(week ? 5 : 9, 7, 4, 4), Child = text, VerticalAlignment = VerticalAlignment.Top,
                Height = (block.End - block.Start) * hour - 3, Margin = new Thickness(3, block.Start * hour, 3, 0),
                ToolTip = block.Title + "\n" + start + "–" + end + "\n" + block.Note + "\n" + T("TimelineTip")
            };
            Add(grid, card, 0, week ? block.Day + 1 : 1);
        }
        var scroll = new ScrollViewer { Content = grid, VerticalScrollBarVisibility = ScrollBarVisibility.Visible };
        scroll.Loaded += (_, _) => scroll.ScrollToVerticalOffset(6 * hour);
        return scroll;
    }
}
