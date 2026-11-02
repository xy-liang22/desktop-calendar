using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal sealed record ScopeChoice(EditScope Value, string Name);
internal sealed class RecurrenceFields : StackPanel
{
    internal readonly ComboBox FrequencyBox = new() { MinHeight = 28 };
    internal readonly TextBox IntervalInput = new() { Text = "1", MinHeight = 28 };
    internal readonly TextBox DaysInput = new() { MinHeight = 28 };
    internal readonly TextBox CountInput = new() { MinHeight = 28 };
    internal readonly DatePicker Start, Until;
    internal readonly CheckBox EndEnabled;
    readonly CheckBox[] days;
    readonly StackPanel week = new(), month = new();
    readonly RecurrenceRule original;
    internal string DraftSignature => System.Text.Json.JsonSerializer.Serialize(new { FrequencyBox.SelectedIndex, Interval = IntervalInput.Text, Days = DaysInput.Text, Count = CountInput.Text, EndEnabled.IsChecked, Until = Until.SelectedDate, Start = Start.SelectedDate, Week = days.Select(d => d.IsChecked).ToArray() });
    static string T(string key) => MainWindow.Text(key);
    internal RecurrenceFields(RecurrenceRule rule, DatePicker? sharedStart = null)
    {
        original = rule; Start = sharedStart ?? new DatePicker { SelectedDate = rule.Start.ToDateTime(TimeOnly.MinValue) };
        Until = new() { SelectedDate = (rule.Until ?? rule.Start).ToDateTime(TimeOnly.MinValue), IsEnabled = rule.Until != null };
        EndEnabled = new() { Content = T("RepeatUseUntil"), IsChecked = rule.Until != null, Margin = new Thickness(0, 0, 8, 0) };
        void EndChanged() { Until.IsEnabled = EndEnabled.IsChecked == true; if (Until.IsEnabled && Until.SelectedDate < Start.SelectedDate) Until.SelectedDate = Start.SelectedDate; }
        EndEnabled.Checked += (_, _) => EndChanged(); EndEnabled.Unchecked += (_, _) => EndChanged();
        if (sharedStart == null) Field("RepeatStart", Start);
        FrequencyBox.ItemsSource = new[] { T("RepeatDaily"), T("RepeatWeekly"), T("RepeatMonthly") }; FrequencyBox.SelectedIndex = (int)rule.Frequency; Field("RepeatFrequency", FrequencyBox);
        IntervalInput.Text = rule.Interval.ToString(CultureInfo.InvariantCulture); Field("RepeatInterval", IntervalInput);
        var row = new WrapPanel(); DayOfWeek[] order = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
        string[] keys = ["PlannerMon", "PlannerTue", "PlannerWed", "PlannerThu", "PlannerFri", "PlannerSat", "PlannerSun"];
        days = order.Select((d, i) => new CheckBox { Tag = d, Content = T(keys[i]), IsChecked = rule.Weekdays.Contains(d), Margin = new Thickness(0, 4, 7, 4), FontSize = 11 }).ToArray();
        foreach (var day in days) row.Children.Add(day); week.Children.Add(row); Children.Add(week);
        DaysInput.Text = string.Join(",", rule.MonthDays.Length > 0 ? rule.MonthDays : [rule.Start.Day]); month.Children.Add(PlannerView.Text(T("RepeatMonthDays"), 11, "#A2AFBC")); month.Children.Add(DaysInput); var hint = PlannerView.Text(T("RepeatMonthHint"), 10, "#A2AFBC"); hint.TextWrapping = TextWrapping.Wrap; month.Children.Add(hint); Children.Add(month);
        var end = new DockPanel(); end.Children.Add(EndEnabled); end.Children.Add(Until); Field("RepeatUntil", end); System.Windows.Automation.AutomationProperties.SetName(Until, T("RepeatUntil"));
        CountInput.Text = rule.Count?.ToString(CultureInfo.InvariantCulture) ?? ""; Field("RepeatCount", CountInput);
        void FrequencyChanged() { week.Visibility = FrequencyBox.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed; month.Visibility = FrequencyBox.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed; }
        FrequencyBox.SelectionChanged += (_, _) => FrequencyChanged(); FrequencyChanged();
    }
    void Field(string key, FrameworkElement input) { Children.Add(PlannerView.Text(T(key), 11, "#A2AFBC")); input.Margin = new Thickness(0, 5, 0, 8); Children.Add(input); System.Windows.Automation.AutomationProperties.SetName(input, T(key)); }
    internal bool Read(out RecurrenceRule value)
    {
        value = original;
        if (!TodoDraftRules.Date(Start, true, out var start) || !int.TryParse(IntervalInput.Text.Trim(), out int interval)) return false;
        DateOnly? until = null; if (EndEnabled.IsChecked == true && !TodoDraftRules.Date(Until, true, out until)) return false;
        int? count = null; if (CountInput.Text.Trim().Length > 0) { if (!int.TryParse(CountInput.Text.Trim(), out int n)) return false; count = n; }
        int[] monthDays = [];
        if (FrequencyBox.SelectedIndex == 2)
        {
            var parts = DaysInput.Text.Normalize(NormalizationForm.FormKC).Replace('、', ',').Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Any(p => !int.TryParse(p, out _))) return false; monthDays = parts.Select(int.Parse).Distinct().Order().ToArray();
        }
        value = original with { Start = start!.Value, Until = until, Count = count, Frequency = (Frequency)FrequencyBox.SelectedIndex, Interval = interval,
            Weekdays = [.. days.Where(d => d.IsChecked == true).Select(d => (DayOfWeek)d.Tag)], MonthDays = [.. monthDays] };
        try { Recurrence.Validate(value); return Recurrence.Next(value, value.Start) != null; } catch (PlannerException) { return false; }
    }
    internal static ComboBox Scopes(bool single, DateOnly date)
    {
        var items = new[] { new ScopeChoice(EditScope.Single, T("RepeatSingle")), new(EditScope.FromDate, T("RepeatFrom") + " " + date.ToString("yyyy-MM-dd")), new(EditScope.EntireSeries, T("RepeatAll")) };
        return new ComboBox { MinHeight = 28, DisplayMemberPath = nameof(ScopeChoice.Name), SelectedValuePath = nameof(ScopeChoice.Value), ItemsSource = single ? items : items.Skip(1).ToArray(), SelectedIndex = 0 };
    }
}
