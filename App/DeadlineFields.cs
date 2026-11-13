using System;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal sealed class DeadlineFields : StackPanel
{
    readonly ComboBox kind = new() { MinHeight = 28 };
    readonly DatePicker date = new();
    readonly TextBox time = new() { MinHeight = 28 }, offset = new() { MinHeight = 28 };
    readonly ComboBox zone;
    internal string Signature => JsonSerializer.Serialize(new { kind.SelectedIndex, date.SelectedDate, DateText = date.Text, TimeText = time.Text, Zone = TimeZoneChoices.Value(zone), Offset = offset.Text });
    internal DeadlineFields(Todo draft, string displayZone)
    {
        kind.ItemsSource = new[] { "DeadlineNone", "DeadlineDate", "DeadlineTime", "DeadlineBoth" }.SelectText();
        var d = draft.Deadline; kind.SelectedIndex = d == null ? 0 : d.Time == null ? 1 : d.Date == null ? 2 : 3;
        date.SelectedDate = (d?.Date ?? draft.PlannedDate).ToDateTime(TimeOnly.MinValue);
        time.Text = d?.Time?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "";
        offset.Text = d?.Offset is TimeSpan o ? PeriodFields.Offset(o) : "";
        zone = TimeZoneChoices.Create(d?.TimeZoneId ?? displayZone, "TimeZoneSystem");
        Children.Add(kind); Children.Add(date); Children.Add(time); Children.Add(zone); Children.Add(offset);
        time.ToolTip = MainWindow.Text("PlannerTimeHint"); offset.ToolTip = MainWindow.Text("TodoTimeOffset");
        System.Windows.Automation.AutomationProperties.SetName(kind, MainWindow.Text("Deadline"));
        System.Windows.Automation.AutomationProperties.SetName(time, MainWindow.Text("DeadlineTime"));
        TodoDraftRules.BindTimePunctuation(time, new ImeInput(time));
        void Update() { date.Visibility = kind.SelectedIndex is 1 or 3 ? Visibility.Visible : Visibility.Collapsed; time.Visibility = kind.SelectedIndex is 2 or 3 ? Visibility.Visible : Visibility.Collapsed; zone.Visibility = kind.SelectedIndex > 0 ? Visibility.Visible : Visibility.Collapsed; offset.Visibility = time.Visibility; }
        kind.SelectionChanged += (_, _) => Update(); Update();
        var hint = PlannerView.Text(MainWindow.Text("DeadlineHint"), 10); hint.TextWrapping = TextWrapping.Wrap; Children.Add(hint);
    }
    internal bool Read(Todo draft, out TodoDeadline? deadline)
    {
        deadline = null; if (kind.SelectedIndex == 0) return true;
        DateOnly? day = null; TimeOnly? clock = null;
        if (kind.SelectedIndex is 1 or 3 && !TodoDraftRules.Date(date, true, out day)) return false;
        if (kind.SelectedIndex is 2 or 3 && (!TodoDraftRules.Time(time.Text, out clock) || clock == null)) return false;
        try
        {
            string zoneId = TimeZoneChoices.Resolve(TimeZoneChoices.Value(zone)); TimeSpan? ambiguous = null;
            if (clock != null && !string.IsNullOrWhiteSpace(offset.Text)) ambiguous = PeriodFields.Parse((day ?? draft.PlannedDate).ToString("yyyy-MM-dd") + " " + clock.Value.ToString("HH:mm"), offset.Text, zoneId).Offset;
            deadline = new(day, clock, zoneId, ambiguous); TodoDeadlines.Validate(draft with { Deadline = deadline }); _ = TodoDeadlines.Instant(draft with { Deadline = deadline }); return true;
        }
        catch (PlannerException) { return false; }
    }
}
internal static class WorkflowText
{
    internal static string[] SelectText(this string[] keys) => System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(keys, MainWindow.Text));
    internal static string Error(Exception error) => MainWindow.Text(error is PlannerException p ? p.Code switch
    {
        "BulkSelectSubtree" => "BulkSelectSubtree", "BulkSelectSeriesRoot" or "ConvertRootOnly" => "BulkSelectSeriesRoot",
        "BulkCompletedHistory" => "BulkCompletedHistory", "ParentCycle" => "HierarchyCycle", "InvalidDeadline" or "InvalidLocalTime" or "AmbiguousLocalTime" or "InvalidTimeOffset" => "DeadlineInvalid",
        "HierarchyConversionRequired" => "HierarchyConversion", "HierarchyDetachSingle" => "HierarchyDetachSingle",
        _ => "WorkflowInvalid"
    } : "WorkflowInvalid");
    internal static string Deadline(Todo todo, string zone, bool full = false)
    { try { return TodoDeadlines.Label(todo, zone, full); } catch (PlannerException) { return "ddl: ⚠"; } }
    internal static string DeadlineColor(Todo todo)
    {
        try { return TodoDeadlines.Urgency(todo, DateTimeOffset.UtcNow) switch { DeadlineUrgency.Overdue => "#F49B9B", DeadlineUrgency.Today => "#F2C078", DeadlineUrgency.Soon => "#DAD598", DeadlineUrgency.Completed => "#8796A5", _ => "#ADC8D8" }; }
        catch (PlannerException) { return "#F2C078"; }
    }
}
