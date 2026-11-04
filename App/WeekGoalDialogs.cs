using TextBlock = DesktopCalendar.EmojiTextBlock;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static class WeekGoalDialogs
{
    static string T(string key) => MainWindow.Text(key);
    internal static void Manage(MainWindow owner, PlannerController c, WeekKey week)
    { var s = new PlannerDialogs.Sheet(T("WeekManage"), 520); s.Body.Children.Add(new WeekSummaryView(owner, c, () => week)); s.Actions.Children.Add(PlannerView.Button("×", s.Close)); PlannerDialogs.Show(owner, s); }
    internal static bool Edit(MainWindow owner, PlannerController c, WeekKey week, WeekGoal? original = null, Guid? newId = null)
    {
        if (!c.Writable) return false; var value = original ?? new WeekGoal { Id = newId ?? Guid.NewGuid(), Week = week, ReviewId = c.State!.Reviews.FirstOrDefault(r => r.Week == week)?.Id, SortKey = c.State!.Goals.Where(g => g.Week == week).Select(g => g.SortKey).DefaultIfEmpty(-1).Max() + 1 };
        var s = new PlannerDialogs.Sheet(T(original == null ? "GoalNew" : "GoalEdit"), 560);
        s.Body.Children.Add(PlannerView.Text(value.Week.Start.ToString("yyyy-MM-dd") + " · " + CultureInfo.GetCultureInfo(MainWindow.Text("PlannerUiLanguage")).DateTimeFormat.GetDayName(value.Week.FirstDay) + " · " + value.Week.TimeZoneId, 11));
        TextBox Field(string key, string text) { var box = new TextBox { Text = text, MinHeight = 28 }; s.Field(key, box); return box; }
        var title = Field("PlannerTitle", value.Title); var description = Field("PlannerDescription", value.Description); description.AcceptsReturn = true; description.TextWrapping = TextWrapping.Wrap; description.Height = 48;
        var emoji = Field("ActivityEmoji", value.Emoji);
        var appearance = new TodoAppearance(c, value.CategoryId, null); appearance.Color.Visibility = Visibility.Collapsed; s.Field("PlannerCategory", appearance);
        var kind = new ComboBox { ItemsSource = new[] { T("GoalCount"), T("GoalDuration"), T("GoalCheck") }, SelectedIndex = (int)value.Kind, MinHeight = 28 }; s.Field("GoalKind", kind);
        var source = new ComboBox { ItemsSource = new[] { T("GoalNone"), T("GoalTodo"), T("GoalActivity") }, SelectedIndex = (int)value.Source, MinHeight = 28 }; s.Field("GoalSource", source);
        var semantics = PlannerView.Text(T("GoalCheckHint"), 10, "#A2AFBC"); semantics.TextWrapping = TextWrapping.Wrap; s.Body.Children.Add(semantics);
        decimal Display(decimal n) => value.Kind == GoalKind.Duration ? n / 3600 : n;
        var target = Field("GoalTarget", Display(value.Target).ToString(CultureInfo.InvariantCulture)); var unit = Field("GoalUnit", value.CountUnit); var over = Field("GoalOverride", value.ManualOverride is decimal m ? Display(m).ToString(CultureInfo.InvariantCulture) : "");
        void KindChanged() { bool check = kind.SelectedIndex == (int)GoalKind.Check; target.IsEnabled = !check; semantics.Visibility = check ? Visibility.Visible : Visibility.Collapsed; if (check) target.Text = "1"; } kind.SelectionChanged += (_, _) => KindChanged(); KindChanged();
        var checks = new StackPanel(); var todoBoxes = new Dictionary<TodoRef, CheckBox>(); var activityBoxes = new Dictionary<Guid, CheckBox>();
        var todoRows = c.State!.Todos.Concat(c.On(week.Start)).Concat(Enumerable.Range(1, 6).SelectMany(i => c.On(week.Start.AddDays(i)))).DistinctBy(t => t.Id).Where(t => t.Status != TodoStatus.Cancelled).ToList();
        foreach (var r in value.Todos) if (!todoRows.Any(t => t.Id == r.Id)) { try { todoRows.Add(PlannerQueries.Resolve(c.State, r)); } catch (PlannerException) { } }
        foreach (var t in todoRows.OrderBy(t => t.PlannedDate)) { var r = TodoRef.Of(t); var box = new CheckBox { Content = T("GoalTodo") + " · " + t.PlannedDate.ToString("MM-dd") + " " + t.Title, IsChecked = value.Todos.Contains(r), Margin = new Thickness(0, 3, 0, 3) }; todoBoxes[r] = box; checks.Children.Add(box); }
        foreach (var a in c.State.Activities) { var box = new CheckBox { Content = T("GoalActivity") + " · " + a.Title, IsChecked = value.Activities.Contains(a.Id), Margin = new Thickness(0, 3, 0, 3) }; activityBoxes[a.Id] = box; checks.Children.Add(box); }
        var links = new Expander { Header = T("GoalLinks"), Content = new ScrollViewer { Content = checks, MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } }; s.Body.Children.Add(links);
        string Signature() => JsonSerializer.Serialize(new { title.Text, Description = description.Text, Emoji = emoji.Text, appearance.CategoryId, Kind = kind.SelectedIndex, Source = source.SelectedIndex, Target = target.Text, Unit = unit.Text, Override = over.Text, Todos = todoBoxes.Where(x => x.Value.IsChecked == true).Select(x => x.Key).ToArray(), Activities = activityBoxes.Where(x => x.Value.IsChecked == true).Select(x => x.Key).ToArray() }); string initial = Signature();
        s.Save(async () =>
        {
            if (Signature() == initial && (original != null || s.CloseWithoutChanges && string.IsNullOrWhiteSpace(title.Text))) return true;
            if (!TodoDraftRules.Title(title.Text)) { s.Error.Text = T("PlannerTitleRequired"); return false; }
            var k = (GoalKind)kind.SelectedIndex; var from = (GoalSource)source.SelectedIndex;
            if (!decimal.TryParse(target.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal n) || n <= 0 || k == GoalKind.Duration && from == GoalSource.Todo) { s.Error.Text = T("GoalInvalid"); return false; }
            decimal? manual = null; if (over.Text.Trim().Length > 0) { if (!decimal.TryParse(over.Text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) || v < 0) { s.Error.Text = T("GoalInvalid"); return false; } manual = v; }
            if (k == GoalKind.Check && (n != 1 || manual is not null and not 0 and not 1) || k == GoalKind.Duration && (n > decimal.MaxValue / 3600 || manual > decimal.MaxValue / 3600)) { s.Error.Text = T("GoalInvalid"); return false; }
            if (original != null && JsonSerializer.Serialize(c.State!.Goals.FirstOrDefault(g => g.Id == original.Id)) != JsonSerializer.Serialize(original)) { s.Error.Text = T("PlannerStaleEdit"); return false; }
            bool changedType = original != null && (original.Kind != k || original.Source != from); if (changedType && !PlannerDialogs.Confirm(owner, T("GoalKind"), T("GoalChangeConfirm"))) return false;
            var goal = value with { Title = title.Text.Trim(), Description = description.Text, Emoji = emoji.Text, CategoryId = appearance.CategoryId, Kind = k, Source = from, Target = k == GoalKind.Duration ? n * 3600 : n, CountUnit = unit.Text, ManualOverride = k == GoalKind.Duration ? manual * 3600 : manual, Todos = [.. todoBoxes.Where(x => x.Value.IsChecked == true).Select(x => x.Key)], Activities = [.. activityBoxes.Where(x => x.Value.IsChecked == true).Select(x => x.Key)] };
            bool ok = await c.ExecuteAsync(new SaveGoal(goal, changedType)); if (!ok) s.Error.Text = T(c.NoticeKey); return ok;
        }, true);
        s.Loaded += (_, _) => title.Focus(); return PlannerDialogs.Show(owner, s) == true;
    }
}

internal sealed class GoalLinks : StackPanel
{
    readonly MainWindow owner;
    readonly PlannerController controller;
    readonly WeekKey week;
    readonly HashSet<Guid> selected;
    readonly StackPanel choices = new();
    readonly CheckBox all = new() { Content = MainWindow.Text("GoalAllWeeks") };
    internal GoalLinks(MainWindow window, PlannerController c, TodoRef? todo = null, Guid? activity = null)
    {
        owner = window; controller = c; week = c.Week(); selected = c.State!.Goals.Where(g => todo != null && g.Todos.Contains(todo) || activity != null && g.Activities.Contains(activity.Value)).Select(g => g.Id).ToHashSet();
        var hint = PlannerView.Text(week.Start.ToString("yyyy-MM-dd") + " · " + MainWindow.Text("GoalLinkHint"), 10, "#A2AFBC"); hint.TextWrapping = TextWrapping.Wrap; Children.Add(hint);
        Children.Add(PlannerView.Button(MainWindow.Text("GoalNew"), () => { var id = Guid.NewGuid(); if (WeekGoalDialogs.Edit(owner, controller, week, newId: id) && controller.State!.Goals.Any(g => g.Id == id)) selected.Add(id); Refresh(); }));
        Children.Add(all); Children.Add(choices); all.Click += (_, _) => Refresh(); Refresh();
        Loaded += (_, _) => controller.Changed += Refresh; Unloaded += (_, _) => controller.Changed -= Refresh;
    }
    void Refresh()
    {
        choices.Children.Clear(); foreach (var g in controller.State!.Goals.Where(g => all.IsChecked == true || g.Week == week || selected.Contains(g.Id)))
        { var box = new CheckBox { Content = g.Week.Start.ToString("yyyy-MM-dd") + " · " + g.Title, IsChecked = selected.Contains(g.Id), Margin = new Thickness(0, 3, 0, 3) }; box.Checked += (_, _) => selected.Add(g.Id); box.Unchecked += (_, _) => selected.Remove(g.Id); choices.Children.Add(box); }
    }
    internal string Signature => string.Join(',', selected.Order());
    internal bool Selected(Guid id) => selected.Contains(id);
    internal PlannerCommand[] Commands(TodoRef? todo = null, Guid? activity = null) => controller.State!.Goals.Where(g => selected.Contains(g.Id) != (todo != null ? g.Todos.Contains(todo) : activity != null && g.Activities.Contains(activity.Value))).Select(g => (PlannerCommand)new SaveGoal(todo != null ? g with { Todos = selected.Contains(g.Id) ? g.Todos.Add(todo) : g.Todos.Remove(todo) } : g with { Activities = selected.Contains(g.Id) ? g.Activities.Add(activity!.Value) : g.Activities.Remove(activity!.Value) })).ToArray();
}
