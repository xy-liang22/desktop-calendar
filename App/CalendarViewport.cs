using System;
using System.Linq;

namespace DesktopCalendar;
internal static class CalendarViewport
{
    internal static DateOnly? TodayAnchor(DateOnly today, int rows) => rows < 6 ? today : null;
    internal static DateOnly WeekStart(DateOnly date, DayOfWeek first)
        => DateOnly.FromDayNumber(Math.Max(0, date.DayNumber - ((int)date.DayOfWeek - (int)first + 7) % 7));
    internal static DateOnly?[] Fixed(DateOnly month, DayOfWeek first, int rows, DateOnly? anchor, int offset)
    {
        int count = Math.Clamp(rows, 1, 12) * 7; var basis = anchor ?? new DateOnly(month.Year, month.Month, 1);
        long start = basis.DayNumber - ((int)basis.DayOfWeek - (int)first + 7) % 7 + (long)offset * 7;
        start = Math.Clamp(start, -6, DateOnly.MaxValue.DayNumber - count + 1);
        return Enumerable.Range(0, count).Select(i => start + i >= 0 ? (DateOnly?)DateOnly.FromDayNumber((int)start + i) : null).ToArray();
    }
}
