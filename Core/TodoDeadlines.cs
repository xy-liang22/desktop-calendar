using System.Globalization;

namespace DesktopCalendar.Core;

public enum DeadlineUrgency { None, Normal, Soon, Today, Overdue, Completed }
public static class TodoDeadlines
{
    public static DateTimeOffset Instant(Todo todo)
    {
        var d = todo.Deadline ?? throw new PlannerException("DeadlineMissing");
        var day = d.Date ?? todo.PlannedDate;
        if (d.Time == null) return PlannerTime.Day(day, d.TimeZoneId).End;
        var local = day.ToDateTime(d.Time.Value, DateTimeKind.Unspecified);
        var zone = PlannerTime.Zone(d.TimeZoneId);
        return PlannerTime.FromLocal(local, zone.Id, zone.IsAmbiguousTime(local) ? d.Offset : null);
    }
    public static void Validate(Todo todo, bool template = false)
    {
        if (todo.Deadline is not TodoDeadline d) return;
        _ = PlannerTime.Zone(d.TimeZoneId);
        if (d.Date == null && d.Time == null || d.Offset is TimeSpan o && (o.Ticks % TimeSpan.TicksPerMinute != 0 || o.TotalHours is < -14 or > 14)) throw new PlannerException("InvalidDeadline");
        // Future relative recurring deadlines are checked when displayed, not infinitely generated.
        if (!template && todo.Origin == null || d.Date != null) _ = Instant(todo);
    }
    public static DeadlineUrgency Urgency(Todo todo, DateTimeOffset now)
    {
        if (todo.Deadline == null) return DeadlineUrgency.None;
        if (todo.Status == TodoStatus.Completed) return DeadlineUrgency.Completed;
        var left = Instant(todo) - now;
        return left <= TimeSpan.Zero ? DeadlineUrgency.Overdue : left <= TimeSpan.FromHours(24) ? DeadlineUrgency.Today : left <= TimeSpan.FromDays(3) ? DeadlineUrgency.Soon : DeadlineUrgency.Normal;
    }
    public static string Label(Todo todo, string displayZone, bool full = false)
    {
        if (todo.Deadline is not TodoDeadline d) return "";
        var instant = Instant(todo);
        // A date-only deadline is an exclusive next-day boundary. Show its last included minute.
        var shown = TimeZoneInfo.ConvertTime(d.Time == null ? instant.AddTicks(-1) : instant, PlannerTime.Zone(displayZone));
        bool sameDate = DateOnly.FromDateTime(shown.DateTime) == todo.PlannedDate;
        string format = full ? "yyyy.MM.dd HH:mm" : d.Time == null ? "MM.dd" : d.Date == null && sameDate ? "HH:mm" : "MM.dd HH:mm";
        return "ddl: " + shown.ToString(format, CultureInfo.InvariantCulture);
    }
}
