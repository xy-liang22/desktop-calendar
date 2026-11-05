using TextBlock = DesktopCalendar.EmojiTextBlock;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;

internal sealed class PeriodFields : Border
{
    internal readonly TextBox Start = new(), End = new(), StartOffset = new(), EndOffset = new();
    internal readonly Guid Id;
    readonly ActivityPeriod original;
    readonly string originalZone;
    readonly string initialStart, initialEnd, initialStartOffset, initialEndOffset;
    internal PeriodFields(ActivityPeriod value, string zone, Action remove)
    {
        Id = value.Id; original = value; originalZone = zone; var stack = new StackPanel(); Padding = new Thickness(7); Margin = new Thickness(0, 3, 0, 5); CornerRadius = new CornerRadius(6); Background = ThemeColors.Brush("#17232F");
        void Row(string key, DateTimeOffset instant, TextBox time, TextBox offset)
        {
            var local = TimeZoneInfo.ConvertTime(instant, PlannerTime.Zone(zone)); time.Text = local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); offset.Text = Offset(local.Offset); time.MinHeight = offset.MinHeight = 28;
            var row = new DockPanel(); var label = PlannerView.Text(MainWindow.Text(key), 11); label.Width = 34; row.Children.Add(label); offset.Width = 66; offset.Margin = new Thickness(5, 0, 0, 0); DockPanel.SetDock(offset, Dock.Right); row.Children.Add(offset); row.Children.Add(time); stack.Children.Add(row);
            System.Windows.Automation.AutomationProperties.SetName(time, MainWindow.Text(key));
        }
        Row("ActivityStart", value.Start, Start, StartOffset); Row("ActivityEnd", value.End, End, EndOffset);
        initialStart = Start.Text; initialEnd = End.Text; initialStartOffset = StartOffset.Text; initialEndOffset = EndOffset.Text;
        stack.Children.Add(PlannerView.Button(MainWindow.Text("ActivityRemovePeriod"), remove)); Child = stack;
    }
    internal static string Offset(TimeSpan offset) => (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    internal static DateTimeOffset Parse(string input, string offset, string zone)
    {
        string raw = input.Trim().Normalize(System.Text.NormalizationForm.FormKC).Replace('T', ' ');
        if (!DateTime.TryParseExact(raw, new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) throw new PlannerException("InvalidLocalTime");
        TimeSpan? preferred = null;
        if (offset.Trim().Length > 0)
        {
            var m = Regex.Match(offset.Trim(), @"^([+-])(\d{2}):(\d{2})$"); if (!m.Success || int.Parse(m.Groups[2].Value) > 14 || int.Parse(m.Groups[3].Value) > 59) throw new PlannerException("InvalidTimeOffset");
            var span = new TimeSpan(int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), 0); preferred = m.Groups[1].Value == "-" ? -span : span;
        }
        return PlannerTime.FromLocal(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone, preferred);
    }
    internal ActivityPeriod Read(string zone)
    { if (zone == originalZone && Start.Text == initialStart && End.Text == initialEnd && StartOffset.Text == initialStartOffset && EndOffset.Text == initialEndOffset) return original;
        var a = Parse(Start.Text, StartOffset.Text, zone); var b = Parse(End.Text, EndOffset.Text, zone); if (b <= a) throw new PlannerException("InvalidInterval"); return new(Id, a, b); }
}

internal static class ActivityDialogs
{
    static string T(string key) => MainWindow.Text(key);
    internal sealed record TodoChoice(TodoRef? Reference, string Name);
    internal static Activity Draft(PlannerController c, TimeInterval? interval = null, Todo? todo = null)
    {
        var zone = c.State!.Options.TimeZoneId;
        var local = c.SelectedDate.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Unspecified);
        var start = interval?.Start ?? ActivityTimeline.Local(local, zone);
        return new() { Id = Guid.NewGuid(), Title = todo?.Title ?? "", CategoryId = todo?.CategoryId, Color = todo?.Color, Todo = todo == null ? null : TodoRef.Of(todo), TimeZoneId = zone,
            Intervals = [new(Guid.NewGuid(), start, interval?.End ?? start.AddHours(1))] };
    }
    internal static bool Edit(MainWindow owner, PlannerController c, Activity? original = null, Activity? draft = null)
    {
        if (!c.Writable) return false; var value = original ?? draft ?? Draft(c); var sheet = new PlannerDialogs.Sheet(T(original == null ? "ActivityNew" : "ActivityEdit"), 740);
        TextBox Field(string key, string text, bool multiline = false) { var input = new TextBox { Text = text, MinHeight = 28, Padding = new Thickness(5), AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, Height = multiline ? 50 : double.NaN }; sheet.Field(key, input); return input; }
        var title = Field("PlannerTitle", value.Title); var summary = Field("ActivitySummary", value.Summary, true);
        var emoji = new EmojiPicker(value.Emoji); sheet.Field("ActivityEmoji", emoji);
        var reaction = new EmojiPicker(value.Reaction, true); sheet.Field("ActivityReaction", reaction);
        var appearance = new TodoAppearance(c, value.CategoryId, value.Color); sheet.Field("PlannerCategory", appearance);
        var zone = Field("ActivityZone", value.TimeZoneId); var hint = PlannerView.Text(T("ActivityTimeHint"), 10, "#A2AFBC"); hint.TextWrapping = TextWrapping.Wrap; sheet.Body.Children.Add(hint);
        var periods = new StackPanel(); sheet.Field("ActivityPeriods", periods); var rows = new List<PeriodFields>();
        void Add(ActivityPeriod p)
        {
            PeriodFields row = null!; row = new(p, zone.Text.Trim(), () => { if (rows.Count == 1) sheet.Error.Text = T("ActivityAtLeastOne"); else { rows.Remove(row); periods.Children.Remove(row); } }); rows.Add(row); periods.Children.Add(row);
        }
        foreach (var p in value.Intervals) Add(p);
        sheet.Body.Children.Add(PlannerView.Button(T("ActivityAddPeriod"), () => { try { var last = rows.Last().Read(zone.Text.Trim()); Add(new(Guid.NewGuid(), last.End.AddHours(1), last.End.AddHours(2))); } catch (Exception ex) when (ex is PlannerException or ArgumentOutOfRangeException) { sheet.Error.Text = T("ActivityInvalidTime"); } }));
        var todos = c.State!.Todos.Concat(c.On(c.SelectedDate)).Concat(c.On(c.Today)).DistinctBy(t => t.Id).Where(t => t.Status != TodoStatus.Cancelled).ToList();
        if (value.Todo != null && !todos.Any(t => t.Id == value.Todo.Id)) { try { todos.Add(PlannerQueries.Resolve(c.State, value.Todo)); } catch (PlannerException) { } }
        var choices = new[] { new TodoChoice(null, T("ActivityNoLink")) }.Concat(todos.Select(t => new TodoChoice(TodoRef.Of(t), t.PlannedDate.ToString("yyyy-MM-dd") + " · " + t.Title))).ToArray();
        var linked = new ComboBox { ItemsSource = choices, DisplayMemberPath = nameof(TodoChoice.Name), SelectedItem = choices.FirstOrDefault(x => x.Reference == value.Todo) ?? choices[0], MinHeight = 28 }; sheet.Field("ActivityTodo", linked);
        linked.SelectionChanged += (_, _) => { if ((linked.SelectedItem as TodoChoice)?.Reference is TodoRef r) { try { var t = PlannerQueries.Resolve(c.State!, r); if (appearance.CategoryId == null) appearance.Refresh(t.CategoryId); } catch (PlannerException) { } } };
        var goals = new GoalLinks(owner, c, activity: value.Id); sheet.Body.Children.Add(new Expander { Header = T("ActivityGoal"), Content = goals });
        var notes = new NoteEditor(c, value.Notes, sheet); sheet.Field("ActivityNotes", notes);
        string Signature() => JsonSerializer.Serialize(new { Title = title.Text, Summary = summary.Text, Emoji = emoji.Text, Reaction = reaction.Text, Zone = zone.Text, appearance.CategoryId, Color = appearance.Color.Value, Periods = rows.Select(r => new { r.Id, Start = r.Start.Text, End = r.End.Text, A = r.StartOffset.Text, B = r.EndOffset.Text }), Todo = (linked.SelectedItem as TodoChoice)?.Reference, Goals = goals.Signature, Text = notes.Source.Text, Format = notes.Format.SelectedIndex });
        string initial = Signature();
        sheet.Save(async () =>
        {
            if (Signature() == initial && (original != null || sheet.CloseWithoutChanges && string.IsNullOrWhiteSpace(title.Text))) return true;
            if (!TodoDraftRules.Title(title.Text)) { sheet.Error.Text = T("ActivityTitleRequired"); return false; }
            if (!appearance.Color.Valid) { sheet.Error.Text = T("PlannerColorInvalid"); return false; }
            ActivityPeriod[] parsed;
            try { _ = PlannerTime.Zone(zone.Text.Trim()); parsed = rows.Select(r => r.Read(zone.Text.Trim())).ToArray(); }
            catch (Exception ex) when (ex is PlannerException or ArgumentOutOfRangeException) { sheet.Error.Text = T("ActivityInvalidTime"); return false; }
            if (original != null && JsonSerializer.Serialize(c.State!.Activities.FirstOrDefault(a => a.Id == original.Id)) != JsonSerializer.Serialize(original)) { sheet.Error.Text = T("ActivityStale"); return false; }
            var saved = value with { Title = title.Text.Trim(), Summary = summary.Text, Emoji = emoji.Text, Reaction = reaction.Text, CategoryId = appearance.CategoryId, Color = appearance.Color.Value, TimeZoneId = zone.Text.Trim(), Intervals = [.. parsed], Notes = notes.Value, Todo = (linked.SelectedItem as TodoChoice)?.Reference };
            var commands = new List<PlannerCommand>();
            foreach (var asset in notes.Images.Pending.Where(a => saved.Notes.AssetIds.Contains(a.Id) && !c.State!.Assets.Any(x => x.Id == a.Id))) { if (notes.Images.Read(asset.RelativePath) == null) { sheet.Error.Text = T("ActivityImageError"); return false; } commands.Add(new SaveAsset(asset)); }
            commands.Add(new SaveActivity(saved, false));
            commands.AddRange(goals.Commands(activity: saved.Id));
            bool ok = await c.ExecuteAsync(new BatchCommand([.. commands])); if (!ok) sheet.Error.Text = T(c.NoticeKey); return ok;
        }, true);
        sheet.Loaded += (_, _) => title.Focus(); return PlannerDialogs.Show(owner, sheet) == true;
    }
    internal static void Detail(MainWindow owner, PlannerController c, Guid id, Guid periodId, ActivitySegment? anchor = null) => owner.ActivityDetails?.Show(id, periodId, anchor);
}
