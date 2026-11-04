using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopCalendar.Core;
namespace DesktopCalendar;
internal static partial class StageThreeChecks
{
    static readonly string project = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
    internal static string DirectoryPath
    {
        get
        {
            string path = Path.GetFullPath(Environment.GetEnvironmentVariable("DESKTOP_CALENDAR_P3_CHECK_ROOT") ?? Path.Combine(project, "artifacts", "p3-checks"));
            if (!path.StartsWith(Path.Combine(project, "artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Checks must use project artifacts.");
            return path;
        }
    }
    static readonly List<string> results = [];
    static bool started;
    static void Check(string name, bool pass) { results.Add((pass ? "PASS " : "FAIL ") + name); File.WriteAllLines(Path.Combine(DirectoryPath, "progress-checks.txt"), results); }
    static IEnumerable<T> Children<T>(DependencyObject obj) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        { var child = VisualTreeHelper.GetChild(obj, i); if (child is T value) yield return value; foreach (var nested in Children<T>(child)) yield return nested; }
    }
    static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, b));
    static void Image(FrameworkElement element, string name)
    {
        element.UpdateLayout(); var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(element.ActualWidth)), Math.Max(1, (int)Math.Ceiling(element.ActualHeight)), 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(DirectoryPath, name)); encoder.Save(file);
    }
    static string Digest(PlannerState state) => Convert.ToHexStringLower(SHA256.HashData(PlannerCodec.Encode(state)));
    static TextBox Field(PlannerDialogs.Sheet sheet, string key) => Children<TextBox>(sheet).Single(c => AutomationProperties.GetName(c) == MainWindow.Text(key));
    static void Drive(Action open, Action<PlannerDialogs.Sheet> action)
    {
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = Application.Current.Windows.OfType<PlannerDialogs.Sheet>().LastOrDefault();
            try { if (dialog == null) throw new Exception("No modal"); action(dialog); }
            catch (Exception ex) { results.Add("FAIL modal driver " + ex); if (dialog != null) Click(dialog.Actions.Children.OfType<Button>().First()); }
        }));
        open();
    }
    internal static async Task Run(MainWindow window)
    {
        if (started) return; started = true;
        if (Environment.GetCommandLineArgs().Contains("--verify-soak")) { await RunP9Soak(window); return; }
        string phase = Environment.GetCommandLineArgs().Contains("--verify-restart") ? "restart" : Environment.GetCommandLineArgs().Contains("--verify-corrupt") ? "corrupt" : Environment.GetCommandLineArgs().Contains("--verify-newer") ? "newer" : "main";
        var session = window.Session; var controller = session.Planner!;
        try
        {
            await controller.Pending;
            window.Show(); window.UpdateLayout();
            if (phase == "restart")
            {
                Check("fresh WPF process loads entire prior committed planner graph", Digest(controller.State!) == File.ReadAllText(Path.Combine(DirectoryPath, "expected-hash.txt")));
                Check("fresh process restores English appearance and fixed row preferences", Localization.English && window.Appearance.UiLanguage == "en" && window.Appearance.BackgroundColor == "#20323A" && window.Appearance.FixedSixRows);
                Check("fresh WPF process restores collapsed view with real today items", window.Mode == DisplayMode.Compact && window.CompactRealList.Children.OfType<CheckBox>().Any());
                Check("startup has no visible default full layout", !window.VisibleBeforePrepared && window.StartupVisible.All(v => v.Mode == DisplayMode.Compact));
                Check("manual sibling mode survives actual app restart", controller.State!.TodoOrderings.Any(o => o.Mode == TodoSort.Manual));
                Check("calendar wrapping preference survives independent app restart", controller.TextMode == CalendarTextMode.Full);
                var p4Ids = File.ReadAllLines(Path.Combine(DirectoryPath, "p4-ids.txt")).Select(Guid.Parse).ToArray();
                Check("P4 series status, materialized occurrence and carry move survive independent restart", controller.State!.Series.Single(s => s.Id == p4Ids[0]).Archived && controller.State.Todos.Any(t => t.Id == p4Ids[1] && t.Origin != null) && controller.State.Todos.Single(t => t.Id == p4Ids[2]) is Todo carry && carry.Kind == TodoKind.CarryLatest && carry.Moves.Length == 1);
                var activityId = Guid.Parse(File.ReadAllText(Path.Combine(DirectoryPath, "p5-activity-id.txt"))); var activity = controller.State!.Activities.Single(a => a.Id == activityId); var assets = new LocalImages(controller.DataDirectory, () => controller.State);
                Check("independent restart restores actual periods notes emoji links and verified relative image", activity.Intervals.Length == 2 && activity.Notes.Text.Contains("assets/images/") && activity.Emoji == "😴" && activity.Todo != null && activity.Notes.AssetIds.All(id => assets.Read(controller.State.Assets.Single(a => a.Id == id).RelativePath) != null));
                var savedRoot = Guid.Parse(File.ReadAllText(Path.Combine(DirectoryPath, "p31-snapshot-id.txt")));
                var p6 = File.ReadAllLines(Path.Combine(DirectoryPath, "p6-ids.txt")).Select(Guid.Parse).ToArray(); var review = controller.State.Reviews.Single(r => r.Id == p6[1]); Check("fresh process restores goals and independent review text", controller.State.Goals.Single(g => g.Id == p6[0]).ReviewId == review.Id && review.Markdown.Contains("Edited later") && review.Sections.Length == 1 && review.Statistics.Length == 1 && controller.WeekLayout == WeekPlanLayout.SummaryStrip && controller.WeekStart == DayOfWeek.Sunday);
                var snapshot = controller.State!.Todos.Single(x => x.Id == savedRoot).CompletionSnapshot;
                Check("fresh process retains manual cascade source and child snapshot", snapshot?.Entries.Length == 2 && controller.State.Todos.Single(x => x.Id == savedRoot).CompletionIntent == CompletionIntent.ManualComplete);
                await controller.ExecuteAsync(new SetTodoCompleted(new(savedRoot), false));
                Check("fresh process cancellation restores mixed child completion", controller.State!.Todos.Single(x => x.Title == "P31 child A").Status == TodoStatus.Completed && controller.State.Todos.Single(x => x.Title == "P31 child B").Status == TodoStatus.Pending);
                window.RestoreNormal(false); window.SetPage(false); Image(window.Surface, "restart-real-data.png");
            }
            else if (phase == "newer")
            {
                var bytes = File.ReadAllBytes(controller.FilePath);
                Check("newer schema produces readonly GUI with recovery disabled", !controller.Writable && controller.Storage?.Status == StoreStatus.NewerSchema && Children<Button>(window.PlannerView!).Single(b => Equals(b.Content, MainWindow.Text("PlannerRecover"))).IsEnabled == false);
                window.PlannerView!.Quick.Text = "do not downgrade";
                Check("newer-schema GUI cannot add or recover over the file", !await window.PlannerView.AddQuickAsync() && !await controller.RecoverAsync(controller.Storage!.Candidates.First()) && bytes.SequenceEqual(File.ReadAllBytes(controller.FilePath)));
            }
            else if (phase == "corrupt")
            {
                string before = File.ReadAllText(controller.FilePath);
                Check("corrupt GUI is readonly and not an empty editable planner", !controller.Writable && controller.State == null && controller.Storage?.Status == StoreStatus.RecoveryRequired);
                window.PlannerView!.Quick.Text = "must not overwrite";
                Check("GUI quick add cannot overwrite corrupt document", !await window.PlannerView.AddQuickAsync() && File.ReadAllText(controller.FilePath) == before);
                Check("backup candidate surfaced without automatic restore", controller.Storage!.Candidates.Length > 0 && before == "BROKEN-P3-FIXTURE");
                var candidate = controller.Storage.Candidates.First();
                Check("explicit recovery service restores original GUI rows", await controller.RecoverAsync(candidate) && controller.State!.Todos.Any());
                Check("GUI recovery retains damaged original evidence", Directory.EnumerateFiles(DirectoryPath, "*.evidence-*").Any(f => File.ReadAllText(f) == before));
            }
            else
            {
                Check("real GUI starts empty with built-in categories and no demo todos", controller.State?.Todos.Length == 0 && controller.State.Categories.Length == 5 && window.PlannerView!.Items.Children.Count == 0 && window.CompactItems.Items.Count == 0);
                controller.Select(controller.Today, true); var view = window.PlannerView!;
                view.Quick.Text = "09:30 GUI alpha";
                view.Quick.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(view.Quick), 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent }); await controller.Pending; await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Check("Enter routed through real quick-input control saves title/time", controller.State!.Todos.Single().Title == "GUI alpha" && controller.State.Todos.Single().PlannedTime == new TimeOnly(9, 30) && view.Quick.Text.Length == 0);
                var alpha = controller.State.Todos.Single();
                long rev = controller.State.Revision; view.Quick.Text = "25:00 invalid"; Check("invalid quick input preserves durable state and draft", !await view.AddQuickAsync() && controller.State.Revision == rev && view.Quick.Text.Length > 0);
                Drive(() => view.Edit(alpha), sheet =>
                {
                    Image(sheet, "edit-dialog.png");
                    window.SetPinned(true); Check("open editor follows explicit topmost mode", sheet.Topmost);
                    window.SetPinned(false); Check("open editor clears topmost on desktop return", !sheet.Topmost && (NativeWindow.GetWindowLong(new System.Windows.Interop.WindowInteropHelper(sheet).Handle, -20) & 8) == 0);
                    Field(sheet, "PlannerTitle").Clear(); Click(sheet.Actions.Children.OfType<Button>().Last());
                    Check("empty editor does not delete or save the original", sheet.IsVisible && sheet.Error.Text.Length > 0 && controller.State!.Todos.Any(t => t.Id == alpha.Id));
                    Field(sheet, "PlannerTitle").Text = "GUI edited"; Field(sheet, "PlannerDescription").Text = "saved description"; Children<ColorField>(sheet).Single().Value = "#B6B5E6";
                    var auto = Children<CheckBox>(sheet).Single(c => Equals(c.Content, MainWindow.Text("PlannerDerivedProgress"))); auto.IsChecked = false; auto.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, auto));
                    Field(sheet, "PlannerProgress").Text = "35";
                    Children<CheckBox>(sheet).Single(c => Equals(c.Content, MainWindow.Text("PlannerImportant"))).IsChecked = true;
                    Click(sheet.Actions.Children.OfType<Button>().Last());
                });
                await controller.Pending; alpha = controller.State!.Todos.Single();
                Check("editor saves same identity, description, priority, color and progress", alpha.Title == "GUI edited" && alpha.Description == "saved description" && alpha.Important && alpha.Color == "#B6B5E6" && alpha.ManualProgress == 35);
                Drive(() => view.Edit(alpha), sheet => { Field(sheet, "PlannerTitle").Text = "cancelled draft"; Click(sheet.Actions.Children.OfType<Button>().First()); });
                Check("editor cancel preserves all persisted values", controller.State!.Todos.Single().Title == "GUI edited");
                Drive(() => view.Edit(null, alpha.Id), sheet => { Field(sheet, "PlannerTitle").Text = "GUI child"; Click(sheet.Actions.Children.OfType<Button>().Last()); }); await controller.Pending;
                var child = controller.State!.Todos.Single(t => t.ParentId == alpha.Id);
                Check("GUI creates independently persisted child", child.Title == "GUI child" && controller.State.Todos.Length == 2);
                Drive(() => view.Edit(alpha), sheet => { var auto = Children<CheckBox>(sheet).Single(c => Equals(c.Content, MainWindow.Text("PlannerDerivedProgress"))); auto.IsChecked = true; auto.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, auto)); Click(sheet.Actions.Children.OfType<Button>().Last()); }); await controller.Pending;
                var childCheck = Children<CheckBox>(view.Items).Single(b => AutomationProperties.GetName(b).EndsWith("GUI child", StringComparison.Ordinal)); childCheck.IsChecked = true; childCheck.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, childCheck)); await controller.Pending;
                Check("child checkbox derives progress and naturally completes parent", PlannerQueries.Progress(controller.State!, new(alpha.Id)) == 100 && controller.State!.Todos.Single(t => t.Id == alpha.Id).Status == TodoStatus.Completed && controller.State.Todos.Single(t => t.Id == alpha.Id).CompletionIntent == CompletionIntent.Automatic);
                Check("month/day/compact use identical committed IDs and completion state", controller.On(controller.Today).Any(t => t.Id == child.Id && t.Status == TodoStatus.Completed) && Children<CheckBox>(window.CompactRealList).Any(c => AutomationProperties.GetName(c) == "GUI child" && c.IsChecked == true));
                await controller.ExecuteAsync(new SetTodoCompleted(new(alpha.Id), false));
                foreach (var name in new[] { "GUI beta", "GUI gamma" }) { view.Quick.Text = name; await view.AddQuickAsync(); }
                var beta = controller.State!.Todos.Single(t => t.Title == "GUI beta"); var gamma = controller.State.Todos.Single(t => t.Title == "GUI gamma");
                Check("native drag payload uses canonical inert ID", PlannerView.DragId(new DataObject("DesktopCalendar.Todo", gamma.Id.ToString("D"))) == gamma.Id);
                Check("foreign or malformed native drag payload is rejected", PlannerView.DragId(new DataObject(DataFormats.Text, gamma.Id.ToString())) == null && PlannerView.DragId(new DataObject("DesktopCalendar.Todo", "not-an-id")) == null);
                Check("real drop handler persists sibling insertion", await view.DropAsync(gamma, beta, false) && controller.On(controller.Today).Where(t => t.ParentId == null).Select(t => t.Title).SequenceEqual(["GUI edited", "GUI gamma", "GUI beta"]));
                Check("drop handler rejects cross-parent moves", !await view.DropAsync(child, beta, false));
                await controller.UndoAsync(); Check("Undo restores prior sorting mode without losing tasks", controller.State!.Todos.Length == 4 && controller.State.TodoOrderings.Length == 0); await controller.RedoAsync();
                Drive(() => view.Edit(null, alpha.Id), sheet => { Field(sheet, "PlannerTitle").Text = "GUI child two"; Click(sheet.Actions.Children.OfType<Button>().Last()); }); await controller.Pending;
                var secondChild = controller.State!.Todos.Single(t => t.Title == "GUI child two");
                await controller.ExecuteAsync(new SetTodoCompleted(new(secondChild.Id), true));
                Check("child drop sorts only its sibling group", await view.DropAsync(secondChild, child, false) && PlannerQueries.Children(controller.State!, controller.State!.Todos.Single(t => t.Id == alpha.Id))[0].Id == secondChild.Id);
                await controller.ExecuteAsync(new SetTodoCompleted(new(child.Id), false)); await controller.ExecuteAsync(new SetTodoCompleted(new(secondChild.Id), false));
                Check("child uncomplete restores retained manual order", PlannerQueries.Children(controller.State!, controller.State!.Todos.Single(t => t.Id == alpha.Id)).Select(t => t.Id).SequenceEqual([secondChild.Id, child.Id]));
                Drive(() => PlannerDialogs.Copy(window, controller, alpha), sheet => { Children<DatePicker>(sheet).Single().SelectedDate = controller.Today.AddDays(1).ToDateTime(TimeOnly.MinValue); Click(sheet.Actions.Children.OfType<Button>().Last()); }); await controller.Pending;
                Check("copy dialog clones subtree with new IDs and reset state", controller.On(controller.Today.AddDays(1)).Length == 3 && controller.On(controller.Today.AddDays(1)).All(t => t.Status == TodoStatus.Pending) && controller.State!.Todos.Any(t => t.CopiedFromId == alpha.Id));
                Drive(() => PlannerDialogs.ManageCategories(window, controller), sheet => { Field(sheet, "PlannerCategoryName").Text = "GUI custom"; Children<ColorField>(sheet).Single().Value = "#AFC5D8"; Click(sheet.Actions.Children.OfType<Button>().Last()); }); await controller.Pending;
                Check("category dialog creates persistent custom category", controller.State!.Categories.Any(c => c.Name == "GUI custom" && c.Color == "#AFC5D8"));
                var custom = controller.State!.Categories.Single(c => c.Name == "GUI custom");
                Drive(() => view.Edit(controller.State!.Todos.Single(t => t.Id == beta.Id)), sheet => { Children<ComboBox>(sheet).Single().SelectedValue = custom.Id; Click(sheet.Actions.Children.OfType<Button>().Last()); }); await controller.Pending;
                window.UpdateLayout();
                Check("category selection persists and paints shared row metadata", controller.State!.Todos.Single(t => t.Id == beta.Id).CategoryId == custom.Id && controller.Color(controller.State.Todos.Single(t => t.Id == beta.Id)) == custom.Color && Children<TextBlock>(view.Items).Any(t => t.Text == "GUI custom"));
                Drive(() => PlannerDialogs.Decoration(window, controller, controller.Today), sheet => { Children<ColorField>(sheet).Single().Value = "#A3BDB0"; Click(sheet.Actions.Children.OfType<Button>().Last()); }); await controller.Pending;
                Check("date decoration dialog writes shared data", controller.State!.Decorations.Single().Date == controller.Today);
                controller.SetColors(true); window.UpdateLayout(); Check("calendar color mode changes existing task panel representation", controller.ColorBackground && Children<CalendarTaskPanel>(view).Any());
                int count = controller.State.Todos.Length;
                Drive(() => PlannerDialogs.Delete(window, controller, beta), sheet => Click(sheet.Actions.Children.OfType<Button>().First()));
                Check("delete cancel leaves record intact", controller.State.Todos.Length == count);
                Drive(() => PlannerDialogs.Delete(window, controller, beta), sheet => Click(sheet.Actions.Children.OfType<Button>().Last())); await controller.Pending;
                Check("confirmed GUI deletion immediately persists", controller.State!.Todos.All(t => t.Id != beta.Id)); await controller.UndoAsync();
                Check("GUI undo recovers deleted record", controller.State!.Todos.Any(t => t.Id == beta.Id));
                var tomorrow = controller.Today.AddDays(1); view.FocusDate(tomorrow); Check("date selection changes actual middle list", controller.SelectedDate == tomorrow && Children<CheckBox>(view.Items).Count() == 3); controller.Select(controller.Today, true);
                foreach (int countTasks in new[] { 0, 1, 3, 10 })
                {
                    var date = controller.Today.AddDays(10 + countTasks);
                    if (countTasks > 0) await controller.ExecuteAsync(new BatchCommand([.. Enumerable.Range(0, countTasks).Select(i => new SaveTodo(controller.NewTodo("layout " + countTasks + "/" + i) with { PlannedDate = date, SortKey = i }))]));
                    Check("real date query task count " + countTasks, controller.On(date).Length == countTasks);
                }
                // Existing P2 non-Todo sections are fixtures only, not a P5/P6 editing UI.
                var reviewId = Guid.NewGuid(); var activityId = Guid.NewGuid(); await controller.ExecuteAsync(new SaveReview(new() { Id = reviewId, Week = PlannerTime.Week(controller.Today, DayOfWeek.Monday, TimeZoneInfo.Local.Id), Markdown = "preserved review" }));
                await controller.ExecuteAsync(new SaveActivity(new() { Id = activityId, Title = "preserved actual", Todo = new(alpha.Id), Intervals = [new(Guid.Empty, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10))], Notes = new() { Text = "preserved note" } }));
                await controller.ExecuteAsync(new SaveGoal(new() { Title = "preserved goal", Week = PlannerTime.Week(controller.Today, DayOfWeek.Monday, TimeZoneInfo.Local.Id), Kind = GoalKind.Count, Source = GoalSource.Activity, Activities = [activityId], ReviewId = reviewId }));
                view.Quick.Text = "post-fixture task"; await view.AddQuickAsync();
                Check("GUI todo changes retain P2 Activity/goal/review sections and links", controller.State!.Activities.Single().Id == activityId && controller.State.Activities.Single().Todo!.Id == alpha.Id && controller.State.Goals.Single().ReviewId == reviewId && controller.State.Reviews.Single().Markdown == "preserved review");
                foreach (var month in new[] { new DateOnly(2026, 10, 1), new(2026, 11, 1) }) { controller.Select(month, true); window.UpdateLayout(); Check("five/six-week real month grid " + month.Month, PlannerView.MonthDates(month).Length == (month.Month == 10 ? 35 : 42)); Image(window.Surface, "month-" + month.Month + ".png"); }
                controller.Select(controller.Today, true);
                window.Width = 880; window.Height = 520; window.UpdateLayout(); Image(window.Surface, "minimum.png"); Check("minimum layout keeps quick input and list inside viewport", view.Quick.ActualWidth > 60 && view.Items.ActualWidth > 100);
                long selectionRevision = controller.State!.Revision; var overflowDate = controller.Today.AddDays(20);
                var overflowButton = Children<Button>(view).Single(b => b.Tag is DateOnly d && d == overflowDate); var overflowCell = (CalendarPreviewCell)overflowButton.Content;
                var more = overflowCell.Preview.MoreInHeader ? overflowCell.HeaderMore : (TextBlock)overflowCell.Preview.Children[overflowCell.Preview.Children.Count - 1];
                more.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
                Check("overflow click selects complete real day without duplicate creation", controller.SelectedDate == overflowDate && Children<CheckBox>(view.Items).Count() == 10 && controller.State.Revision == selectionRevision);
                overflowButton.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent });
                Check("date double-click focuses management without adding records", controller.State.Revision == selectionRevision);
                controller.Select(controller.Today, true); window.UpdateLayout();
                var smallCounts = Children<CalendarTaskPanel>(view).Select(p => p.VisibleRows).ToArray();
                window.Width = 1240; window.Height = 760; window.UpdateLayout(); Image(window.Surface, "full.png");
                Check("real adaptive panels expose more rows when taller", Children<CalendarTaskPanel>(view).Select(p => p.VisibleRows).Sum() > smallCounts.Sum());
                window.SetPinned(true); window.SetPinned(false); Check("real data UI retains owner/topmost roundtrip", window.DesktopMode.Layer == WindowLayer.Desktop && !window.IsPinned);
                await RunP31Checks(window);
                await RunP32Checks(window);
                await RunP4Checks(window);
                await RunP5Checks(window);
                await RunP51Checks(window);
                await RunP6Checks(window);
                view.Quick.Text = "recovery draft"; session.TestWindowLoss(); await Task.Delay(150); window = session.Window;
                window.Show();
                for (int i = 0; i < 100 && !window.Ready; i++) await Task.Delay(50);
                Check("recreated real component completes visible initialization", window.Ready);
                Check("recreated component reuses same workspace and current data", ReferenceEquals(controller, window.Session.Planner) && window.PlannerView!.Items.Children.Count > 0);
                Check("unsubmitted quick draft survives HWND recovery", window.PlannerView!.Quick.Text == "recovery draft"); window.PlannerView.Quick.Clear();
                window.EnterCompact(); window.UpdateLayout(); Image(window.Surface, "compact.png"); Check("compact uses real tasks without forcing topmost", window.Mode == DisplayMode.Compact && !window.IsPinned && window.CompactRealList.Children.OfType<CheckBox>().Any());
                session.Save();
                var savedPreferences = System.Text.Json.JsonSerializer.Deserialize<WindowPreferences>(File.ReadAllBytes(Path.Combine(DirectoryPath, "window-preferences.json")));
                Check("compact preference was durably captured before restart", savedPreferences?.Collapsed == true);
                File.WriteAllText(Path.Combine(DirectoryPath, "expected-hash.txt"), Digest(controller.State!));
                // A real Windows read-only replacement failure on this test-owned file only.
                var beforeFailure = controller.State; window.PlannerView.Quick.Text = "failed draft"; File.SetAttributes(controller.FilePath, FileAttributes.ReadOnly);
                try { Check("failed GUI save retains draft/state and shows compact error", !await window.PlannerView.AddQuickAsync() && window.PlannerView.Quick.Text == "failed draft" && controller.State == beforeFailure && controller.NoticeKey == "PlannerSaveFailed" && window.CompactNotice.Text == MainWindow.Text("PlannerSaveFailed")); }
                finally { File.SetAttributes(controller.FilePath, FileAttributes.Normal); }
                window.PlannerView.Quick.Clear(); await RunP7Checks(window); window = await RunP8Checks(window); await RunP9Checks(window);
            }
        }
        catch (Exception ex) { results.Add("FAIL unhandled " + ex); }
        Directory.CreateDirectory(DirectoryPath); results.Add("NOT VERIFIED physical mouse/keyboard, native drag insertion feedback, actual desktop pixels and Win+D; WPF routed events/renders are separate evidence.");
        File.WriteAllLines(Path.Combine(DirectoryPath, "checks-" + phase + ".txt"), results);
        session.Exit(results.Any(r => r.StartsWith("FAIL ")) ? 1 : 0);
    }
}
