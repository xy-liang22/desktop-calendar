using System.Collections.Immutable;

namespace DesktopCalendar.Core;

public sealed record ActivitySegment(Guid ActivityId, Guid PeriodId, DateOnly Date, DateTimeOffset Start, DateTimeOffset End,
    double StartHour, double EndHour, TimeSpan Offset);

// Display fragments retain the original period identity. Repeated DST hours overlap in
// wall-clock coordinates and are separately selectable, with their UTC offsets shown.
public static class ActivityTimeline
{
    public static ImmutableArray<ActivitySegment> On(PlannerState state, DateOnly date, string zoneId)
    {
        var range = PlannerTime.Day(date, zoneId); var zone = PlannerTime.Zone(zoneId); var result = new List<ActivitySegment>();
        foreach (var a in state.Activities)
        foreach (var p in a.Intervals)
        {
            var clip = PlannerTime.Clip(p, range); if (clip == null) continue;
            var start = clip.Start; var cursor = start;
            while (cursor < clip.End)
            {
                var next = cursor.AddMinutes(15); if (next > clip.End) next = clip.End;
                var offset = zone.GetUtcOffset(cursor);
                if (zone.GetUtcOffset(next.AddTicks(-1)) != offset)
                {
                    long low = cursor.UtcTicks, high = next.UtcTicks;
                    while (high - low > 1) { long mid = low + (high - low) / 2; if (zone.GetUtcOffset(new DateTimeOffset(mid, TimeSpan.Zero)) == offset) low = mid; else high = mid; }
                    var transition = new DateTimeOffset(high, TimeSpan.Zero); Add(start, transition, offset); start = cursor = transition;
                }
                else { cursor = next; if (cursor == clip.End) Add(start, cursor, offset); else if (zone.GetUtcOffset(cursor) != offset) { Add(start, cursor, offset); start = cursor; } }
            }
            void Add(DateTimeOffset from, DateTimeOffset to, TimeSpan offset)
            {
                double h1 = (from.ToOffset(offset).DateTime - date.ToDateTime(TimeOnly.MinValue)).TotalHours;
                double h2 = (to.ToOffset(offset).DateTime - date.ToDateTime(TimeOnly.MinValue)).TotalHours;
                if (to > from && h2 > h1) result.Add(new(a.Id, p.Id, date, from, to, Math.Clamp(h1, 0, 24), Math.Clamp(h2, 0, 24), offset));
            }
        }
        return [.. result.OrderBy(s => s.StartHour).ThenBy(s => s.EndHour).ThenBy(s => s.ActivityId).ThenBy(s => s.PeriodId)];
    }
    public static DateTimeOffset Local(DateTime local, string zoneId, TimeSpan? preferredOffset = null)
    {
        var zone = PlannerTime.Zone(zoneId); local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return PlannerTime.FromLocal(local, zoneId, zone.IsAmbiguousTime(local) ? preferredOffset : null);
    }
    public static ActivityPeriod Transform(ActivityPeriod period, TimeSpan wallDelta, string zoneId, string edge)
    {
        var start = TimeZoneInfo.ConvertTime(period.Start, PlannerTime.Zone(zoneId)); var end = TimeZoneInfo.ConvertTime(period.End, PlannerTime.Zone(zoneId));
        DateTimeOffset a = period.Start, b = period.End;
        if (edge == "move") { a = Local(start.DateTime.Add(wallDelta), zoneId, start.Offset); b = period.End.Add(a - period.Start); }
        else if (edge == "start") a = Local(start.DateTime.Add(wallDelta), zoneId, start.Offset);
        else if (edge == "end") b = Local(end.DateTime.Add(wallDelta), zoneId, end.Offset);
        else throw new PlannerException("InvalidPeriodOperation");
        if (b <= a) throw new PlannerException("InvalidInterval"); return period with { Start = a, End = b };
    }
}
