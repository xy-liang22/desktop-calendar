namespace DesktopCalendar.Core;

public static class Recurrence
{
    public static void Validate(RecurrenceRule rule)
    {
        if (rule == null) throw new PlannerException("InvalidRecurrence");
        if (!Enum.IsDefined(rule.Frequency) || !Enum.IsDefined(rule.WeekStart) || rule.Interval is < 1 or > 10000 || rule.Count is <= 0 || rule.Until < rule.Start ||
            rule.Weekdays.IsDefault || rule.MonthDays.IsDefault || rule.ExcludedDates.IsDefault || rule.IncludedDates.IsDefault ||
            rule.Weekdays.Distinct().Count() != rule.Weekdays.Length || rule.MonthDays.Distinct().Count() != rule.MonthDays.Length ||
            rule.Weekdays.Any(d => !Enum.IsDefined(d)) || rule.MonthDays.Any(d => d is < 1 or > 31) ||
            rule.Frequency == Frequency.Weekly && rule.Weekdays.Length == 0 || rule.Frequency == Frequency.Monthly && rule.MonthDays.Length == 0)
            throw new PlannerException("InvalidRecurrence");
        _ = PlannerTime.Zone(rule.TimeZoneId);
    }
    public static bool Matches(RecurrenceRule rule, DateOnly date)
    {
        Validate(rule);
        if (date < rule.Start || rule.Until is DateOnly until && date > until || rule.ExcludedDates.Contains(date)) return false;
        if (rule.IncludedDates.Contains(date)) return true;
        if (!Nominal(rule, date)) return false;
        if (rule.Count == null) return true;
        return Ordinal(rule, date) <= rule.Count;
    }
    static bool Nominal(RecurrenceRule r, DateOnly d)
    {
        int days = d.DayNumber - r.Start.DayNumber;
        return r.Frequency switch
        {
            Frequency.Daily => days % r.Interval == 0,
            Frequency.Weekly => ((days + ((int)r.Start.DayOfWeek - (int)r.WeekStart + 7) % 7) / 7) % r.Interval == 0 && r.Weekdays.Contains(d.DayOfWeek),
            Frequency.Monthly => ((d.Year - r.Start.Year) * 12 + d.Month - r.Start.Month) % r.Interval == 0 && r.MonthDays.Contains(d.Day),
            _ => false
        };
    }
    // COUNT counts nominal dates (before EXDATE); explicit inclusions are additions.
    static int Ordinal(RecurrenceRule r, DateOnly d)
    {
        if (r.Frequency == Frequency.Daily) return (d.DayNumber - r.Start.DayNumber) / r.Interval + 1;
        if (r.Frequency == Frequency.Weekly)
        {
            int offset = ((int)r.Start.DayOfWeek - (int)r.WeekStart + 7) % 7;
            int week = (d.DayNumber - r.Start.DayNumber + offset) / 7;
            int count = 0;
            foreach (var day in r.Weekdays)
            {
                int position = ((int)day - (int)r.WeekStart + 7) % 7;
                int firstWeek = position < offset ? r.Interval : 0;
                int lastWeek = position > ((int)d.DayOfWeek - (int)r.WeekStart + 7) % 7 ? week - 1 : week;
                if (lastWeek >= firstWeek) count += (lastWeek - firstWeek) / r.Interval + 1;
            }
            return count;
        }
        int total = 0, month = (d.Year - r.Start.Year) * 12 + d.Month - r.Start.Month;
        for (int i = 0; i <= month; i += r.Interval)
        {
            var start = new DateOnly(r.Start.Year, r.Start.Month, 1).AddMonths(i);
            foreach (int day in r.MonthDays)
                if (day <= DateTime.DaysInMonth(start.Year, start.Month))
                { var candidate = new DateOnly(start.Year, start.Month, day); if (candidate >= r.Start && candidate <= d) total++; }
        }
        return total;
    }
}
