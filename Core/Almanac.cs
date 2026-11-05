namespace DesktopCalendar.Core;

public sealed record AlmanacDay(DateOnly Date, string[] Suitable, string[] Avoid);

// Traditional calendar reference, independent of official holidays and personal data.
// Pin the upstream convention (sect 2). Never invent entries outside the tested range.
public static class Almanac
{
    public static readonly DateOnly First = new(1901, 1, 1), Last = new(2100, 12, 31);
    static readonly Dictionary<DateOnly, AlmanacDay> cache = new();
    public static AlmanacDay? On(DateOnly date)
    {
        if (date < First || date > Last) return null;
        lock (cache)
        {
            if (!cache.TryGetValue(date, out var value))
            {
                var lunar = Lunar.Solar.FromYmdHms(date.Year, date.Month, date.Day, 12, 0, 0).Lunar;
                value = new(date, lunar.GetDayYi(2).ToArray(), lunar.GetDayJi(2).ToArray());
                if (cache.Count >= 512) cache.Clear(); cache.Add(date, value);
            }
            return value with { Suitable = [.. value.Suitable], Avoid = [.. value.Avoid] };
        }
    }
}
