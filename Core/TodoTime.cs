namespace DesktopCalendar.Core;

public sealed record TodoClock(DateOnly Date, TimeOnly? Time, string? Issue = null);

public static class TodoTime
{
    public static string? SourceZone(PlannerState state, Todo todo) => todo.PlannedTimeZoneId ?? (todo.Origin is OccurrenceKey k
        ? PlannerQueries.Version(state.Series.Single(s => s.Id == k.SeriesId), k.Date)?.Rule.TimeZoneId : null);
    public static TodoClock Display(PlannerState state, Todo todo, DateOnly? carryDay = null)
    {
        bool carry = todo.Kind is TodoKind.CarryHistory or TodoKind.CarryLatest;
        var day = carry ? carryDay ?? todo.PlannedDate : todo.PlannedDate;
        string? source = SourceZone(state, todo);
        if (todo.PlannedTime is not TimeOnly time || source == null) return new(day, todo.PlannedTime);
        try
        {
            var local = day.ToDateTime(time, DateTimeKind.Unspecified); var zone = PlannerTime.Zone(source);
            var instant = PlannerTime.FromLocal(local, source, zone.IsAmbiguousTime(local) ? todo.PlannedOffset : null);
            var shown = TimeZoneInfo.ConvertTime(instant, PlannerTime.Zone(state.Options.TimeZoneId));
            // Carry membership/frozen history is a date workflow, not a series of actual instants.
            return new(carry ? day : DateOnly.FromDateTime(shown.DateTime), TimeOnly.FromDateTime(shown.DateTime));
        }
        catch (PlannerException e) { return new(day, time, e.Code); }
    }
    public static void Validate(Todo todo, bool template)
    {
        if (todo.PlannedTimeZoneId != null) _ = PlannerTime.Zone(todo.PlannedTimeZoneId);
        if (todo.PlannedOffset is TimeSpan offset && (offset.Ticks % TimeSpan.TicksPerMinute != 0 || offset < TimeSpan.FromHours(-14) || offset > TimeSpan.FromHours(14))) throw new PlannerException("InvalidTimeOffset");
        if (!template && todo.Origin == null && todo.PlannedTimeZoneId != null && todo.PlannedTime is TimeOnly time)
        {
            var local = todo.PlannedDate.ToDateTime(time, DateTimeKind.Unspecified); var zone = PlannerTime.Zone(todo.PlannedTimeZoneId);
            _ = PlannerTime.FromLocal(local, zone.Id, zone.IsAmbiguousTime(local) ? todo.PlannedOffset : null);
        }
    }
}
