using System.Collections.Immutable;

namespace DesktopCalendar.Core;

public static class PlannerTime
{
    public static TimeZoneInfo Zone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) { throw new PlannerException("InvalidTimeZone"); }
    }
    public static DateTimeOffset FromLocal(DateTime local, string zoneId, TimeSpan? ambiguousOffset = null)
    {
        if (local.Kind != DateTimeKind.Unspecified) throw new PlannerException("LocalTimeMustBeUnspecified");
        var zone = Zone(zoneId);
        if (zone.IsInvalidTime(local)) throw new PlannerException("InvalidLocalTime");
        var offsets = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local) : [zone.GetUtcOffset(local)];
        if (offsets.Length > 1 && ambiguousOffset == null) throw new PlannerException("AmbiguousLocalTime");
        var offset = ambiguousOffset ?? offsets[0];
        if (!offsets.Contains(offset)) throw new PlannerException("InvalidTimeOffset");
        try { return new DateTimeOffset(local, offset); }
        catch (ArgumentOutOfRangeException) { throw new PlannerException("DateOutOfRange"); }
    }
    public static DateOnly LocalDate(DateTimeOffset instant, string zoneId) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone(zoneId)).DateTime);
    public static DateTimeOffset Boundary(DateOnly date, string zoneId)
        => Boundary(date, Zone(zoneId));
    public static DateTimeOffset Boundary(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        // Some zones skip midnight or an entire date. Calendar boundaries advance to
        // the first valid local minute; repeated midnight uses the earlier instant.
        for (int i = 0; zone.IsInvalidTime(local); i++)
        { if (i >= 2880 || local >= DateTime.MaxValue.AddMinutes(-1)) throw new PlannerException("InvalidDateBoundary"); local = local.AddMinutes(1); }
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        try { return new DateTimeOffset(local, offset).ToUniversalTime(); }
        catch (ArgumentOutOfRangeException) { throw new PlannerException("DateOutOfRange"); }
    }
    public static TimeInterval Day(DateOnly date, string zoneId)
    {
        if (date == DateOnly.MaxValue) throw new PlannerException("DateOutOfRange");
        return new(Boundary(date, zoneId), Boundary(date.AddDays(1), zoneId));
    }
    public static WeekKey Week(DateOnly date, DayOfWeek firstDay, string zoneId)
    {
        if (!Enum.IsDefined(firstDay)) throw new PlannerException("InvalidWeekStart");
        _ = Zone(zoneId);
        int back = ((int)date.DayOfWeek - (int)firstDay + 7) % 7;
        if (date.DayNumber < back || date.DayNumber - back > DateOnly.MaxValue.DayNumber - 7) throw new PlannerException("DateOutOfRange");
        return new(date.AddDays(-back), firstDay, zoneId);
    }
    public static TimeInterval WeekRange(WeekKey week)
    {
        if (week == null || !Enum.IsDefined(week.FirstDay) || week.Start.DayOfWeek != week.FirstDay || week.Start.DayNumber > DateOnly.MaxValue.DayNumber - 7) throw new PlannerException("InvalidWeek");
        return new(Boundary(week.Start, week.TimeZoneId), Boundary(week.Start.AddDays(7), week.TimeZoneId));
    }
    public static TimeInterval? Clip(TimeInterval interval, TimeInterval range)
    {
        if (interval == null || range == null || interval.End <= interval.Start || range.End < range.Start) throw new PlannerException("InvalidInterval");
        var start = interval.Start > range.Start ? interval.Start : range.Start;
        var end = interval.End < range.End ? interval.End : range.End;
        return end > start ? new(start.ToUniversalTime(), end.ToUniversalTime()) : null;
    }
    public static ImmutableArray<TimeInterval> Merge(IEnumerable<TimeInterval> intervals)
    {
        var result = new List<TimeInterval>();
        foreach (var item in intervals.OrderBy(i => i.Start).ThenBy(i => i.End))
        {
            if (item.End <= item.Start) throw new PlannerException("InvalidInterval");
            var next = new TimeInterval(item.Start.ToUniversalTime(), item.End.ToUniversalTime());
            if (result.Count > 0 && next.Start <= result[^1].End)
                result[^1] = result[^1] with { End = next.End > result[^1].End ? next.End : result[^1].End };
            else result.Add(next);
        }
        return [.. result];
    }
    public static decimal Seconds(IEnumerable<TimeInterval> intervals) => Merge(intervals).Sum(i => (decimal)i.Duration.Ticks / TimeSpan.TicksPerSecond);
}
