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
    // Find one date without pre-generating a series, jumping by recurrence periods.
    public static DateOnly? Next(RecurrenceRule rule, DateOnly from)
    {
        Validate(rule); if (from < rule.Start) from = rule.Start;
        DateOnly? extra = rule.IncludedDates.Where(d => d >= from && Matches(rule, d)).Order().Cast<DateOnly?>().FirstOrDefault();
        int last = (rule.Until ?? DateOnly.MaxValue).DayNumber;
        IEnumerable<DateOnly> Candidates()
        {
            if (rule.Frequency == Frequency.Daily)
            {
                long first = rule.Start.DayNumber + ((long)from.DayNumber - rule.Start.DayNumber + rule.Interval - 1) / rule.Interval * rule.Interval;
                for (long n = first; n <= last; n += rule.Interval) yield return DateOnly.FromDayNumber((int)n);
            }
            else if (rule.Frequency == Frequency.Weekly)
            {
                long anchor = rule.Start.DayNumber - ((int)rule.Start.DayOfWeek - (int)rule.WeekStart + 7) % 7;
                long period = rule.Interval * 7L, first = anchor + ((from.DayNumber - anchor) / period) * period;
                var offsets = rule.Weekdays.Select(d => ((int)d - (int)rule.WeekStart + 7) % 7).Order().ToArray();
                for (long week = first; week <= last; week += period)
                    foreach (int offset in offsets) { long n = week + offset; if (n >= from.DayNumber && n >= 0 && n <= last) yield return DateOnly.FromDayNumber((int)n); }
            }
            else
            {
                int anchor = (rule.Start.Year - 1) * 12 + rule.Start.Month - 1, target = (from.Year - 1) * 12 + from.Month - 1;
                int first = anchor + (target - anchor) / rule.Interval * rule.Interval;
                for (int month = first; month < 9999 * 12; month += rule.Interval)
                {
                    int year = month / 12 + 1, m = month % 12 + 1;
                    if (new DateOnly(year, m, 1).DayNumber > last) yield break;
                    foreach (int d in rule.MonthDays.Order()) if (d <= DateTime.DaysInMonth(year, m))
                    { var date = new DateOnly(year, m, d); if (date >= from && date.DayNumber <= last) yield return date; }
                }
            }
        }
        foreach (var date in Candidates())
        {
            if (extra is DateOnly included && included < date) return included;
            if (rule.Count is int count && Ordinal(rule, date) > count) break;
            if (Matches(rule, date)) return date;
        }
        return extra;
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
