using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DesktopCalendar;
internal sealed class FixedCalendarFields : StackPanel
{
    internal TextBox Count { get; } = new();
    internal DatePicker Start { get; } = new();
    internal DatePicker End { get; } = new();
    readonly DayOfWeek first;
    bool syncing;
    internal event Action? Changed;
    internal FixedCalendarFields(WindowPreferences p, DateOnly month, DayOfWeek first)
    {
        this.first = first; Count.Text = p.FixedWeekRows.ToString(CultureInfo.InvariantCulture);
        var date = CalendarViewport.Fixed(month, first, p.FixedWeekRows, p.FixedWeekStart, p.FixedWeekOffset).First(d => d != null)!.Value;
        Start.SelectedDate = date.ToDateTime(TimeOnly.MinValue); SetEnd();
        void Field(string key, FrameworkElement input) { var label = PlannerView.Text(MainWindow.Text(key), 11, "#A2AFBC"); label.Margin = new Thickness(0, 5, 0, 3); Children.Add(label); Children.Add(input); System.Windows.Automation.AutomationProperties.SetName(input, MainWindow.Text(key)); }
        Field("FixedRowCount", Count); Field("FixedFirstWeek", Start); Field("FixedLastWeek", End);
        Count.TextChanged += (_, _) => { if (!syncing) { SetEnd(); Changed?.Invoke(); } };
        Start.SelectedDateChanged += (_, _) => { if (!syncing) { SetEnd(); Changed?.Invoke(); } };
        End.SelectedDateChanged += (_, _) =>
        {
            if (syncing || Start.SelectedDate == null || End.SelectedDate == null) return;
            int weeks = (CalendarViewport.WeekStart(DateOnly.FromDateTime(End.SelectedDate.Value), first).DayNumber - CalendarViewport.WeekStart(DateOnly.FromDateTime(Start.SelectedDate.Value), first).DayNumber) / 7 + 1;
            syncing = true; Count.Text = weeks.ToString(CultureInfo.InvariantCulture); syncing = false; Changed?.Invoke();
        };
        Start.DateValidationError += (_, e) => e.ThrowException = false; End.DateValidationError += (_, e) => e.ThrowException = false;
    }
    void SetEnd()
    {
        if (!int.TryParse(Count.Text, out int count) || count is < 1 or > 12 || Start.SelectedDate == null) return;
        var day = CalendarViewport.WeekStart(DateOnly.FromDateTime(Start.SelectedDate.Value), first);
        if (day.DayNumber > DateOnly.MaxValue.DayNumber - (count - 1) * 7) return;
        syncing = true; End.SelectedDate = day.AddDays((count - 1) * 7).ToDateTime(TimeOnly.MinValue); syncing = false;
    }
    internal bool Read(out int rows, out DateOnly? anchor)
    {
        rows = 6; anchor = null;
        if (!int.TryParse(Count.Text, out int value) || value is < 1 or > 12) return false;
        bool previous = syncing; syncing = true;
        try
        {
            if (!TodoDraftRules.Date(Start, true, out var start) || !TodoDraftRules.Date(End, true, out var end)) return false;
            anchor = CalendarViewport.WeekStart(start!.Value, first); rows = value;
            return CalendarViewport.WeekStart(end!.Value, first).DayNumber - anchor.Value.DayNumber == (rows - 1) * 7;
        }
        finally { syncing = previous; }
    }
}
