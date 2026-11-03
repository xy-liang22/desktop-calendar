using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DesktopCalendar.Core;

public sealed record CalendarEvent(DateOnly Date, string Kind, string Zh, string En, string Source);
public sealed record CalendarBundle(int SchemaVersion, DateTimeOffset UpdatedAt, int[] TermYears, int[] MainlandYears, int[] HongKongYears, CalendarEvent[] Events);
// Official knowledge is deliberately separate from the personal planner document.
public sealed class CalendarKnowledge
{
    public const string Hko = "https://www.hko.gov.hk/en/gts/time/calendar/text/files/T{0}e.txt";
    public const string HkEnglish = "https://www.1823.gov.hk/common/ical/en.json";
    public const string HkChinese = "https://www.1823.gov.hk/common/ical/sc.json";
    public CalendarBundle Data { get; private set; }
    public string Diagnostic { get; private set; } = "";
    public static CalendarBundle BuiltIn()
    { using var s = typeof(CalendarKnowledge).Assembly.GetManifestResourceStream("DesktopCalendar.Core.calendar.json")!; return Decode(new StreamReader(s).ReadToEnd()); }
    public CalendarKnowledge(string? path = null)
    {
        Data = BuiltIn(); if (path == null || !File.Exists(path)) return;
        try { Data = Decode(File.ReadAllText(path)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException) { Diagnostic = e.GetType().Name; try { Data = Decode(File.ReadAllText(path + ".bak")); } catch { /* Embedded, verified data remains available. */ } }
    }
    public static CalendarBundle Decode(string json)
    {
        if (json.Length > 4_000_000) throw new InvalidDataException("CalendarTooLarge");
        var b = JsonSerializer.Deserialize<CalendarBundle>(json) ?? throw new InvalidDataException("CalendarEmpty"); Validate(b); return b;
    }
    public static void Validate(CalendarBundle b)
    {
        if (b.SchemaVersion != 1 || b.Events == null || b.TermYears == null || b.MainlandYears == null || b.HongKongYears == null || b.UpdatedAt == default || b.Events.Length > 20000) throw new InvalidDataException("CalendarVersion");
        foreach (var years in new[] { b.TermYears, b.MainlandYears, b.HongKongYears }) if (years.Length != years.Distinct().Count() || years.Any(y => y < 1901 || y > 2100)) throw new InvalidDataException("CalendarYears");
        foreach (var e in b.Events)
            if (e == null || e.Kind is not ("Term" or "CN" or "CNWork" or "HK") || string.IsNullOrWhiteSpace(e.Zh) || string.IsNullOrWhiteSpace(e.En) || e.Zh.Length > 100 || e.En.Length > 160 || !Trusted(e.Source) || !(e.Kind == "Term" ? b.TermYears : e.Kind == "HK" ? b.HongKongYears : b.MainlandYears).Contains(e.Date.Year)) throw new InvalidDataException("CalendarEntry");
        if (b.Events.DistinctBy(x => (x.Date, x.Kind, x.Zh)).Count() != b.Events.Length) throw new InvalidDataException("CalendarDuplicate");
        foreach (int y in b.TermYears) { var terms = b.Events.Where(e => e.Kind == "Term" && e.Date.Year == y).OrderBy(e => e.Date).ToArray(); if (terms.Length != 24 || terms.Select(e => e.En).Distinct().Count() != 24 || terms.Where((e, i) => e.Date.Month != i / 2 + 1).Any()) throw new InvalidDataException("IncompleteTerms"); }
        foreach (int y in b.HongKongYears) if (b.Events.Count(e => e.Kind == "HK" && e.Date.Year == y) != 17) throw new InvalidDataException("IncompleteHongKongYear");
        foreach (int y in b.MainlandYears) if (b.Events.Count(e => e.Kind == "CN" && e.Date.Year == y) < 28 || b.Events.Count(e => e.Kind == "CNWork" && e.Date.Year == y) < 4) throw new InvalidDataException("IncompleteMainlandYear");
    }
    static bool Trusted(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == "https" && u.Port == 443 && u.UserInfo == "" && u.Host is "www.hko.gov.hk" or "www.1823.gov.hk" or "www.gov.cn";
    static readonly string[] TermNames = "小寒|大寒|立春|雨水|惊蛰|春分|清明|谷雨|立夏|小满|芒种|夏至|小暑|大暑|立秋|处暑|白露|秋分|寒露|霜降|立冬|小雪|大雪|冬至".Split('|');
    public static CalendarEvent[] ParseTerms(int year, string text)
    {
        var matches = Regex.Matches(text, @"(?m)^\s*(\d{4})/(\d{1,2})/(\d{1,2})\s+.+?\s+(?:Monday|Tuesday|Wednesday|Thursday|Friday|Saturday|Sunday)[ \t]+([A-Za-z][^\r\n]*)");
        if (matches.Count != 24) throw new InvalidDataException("HkoFormatChanged");
        return matches.Select((m, i) => { var d = new DateOnly(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value)); if (d.Year != year) throw new InvalidDataException("HkoYearMismatch"); return new CalendarEvent(d, "Term", TermNames[i], m.Groups[4].Value.Trim(), string.Format(CultureInfo.InvariantCulture, Hko, year)); }).ToArray();
    }
    public static CalendarEvent[] ParseHongKong(string english, string chinese)
    {
        JsonElement[] Parse(string json) { using var d = JsonDocument.Parse(json.TrimStart('\uFEFF')); return d.RootElement.GetProperty("vcalendar")[0].GetProperty("vevent").EnumerateArray().Select(e => e.Clone()).ToArray(); }
        var en = Parse(english); var zh = Parse(chinese).ToDictionary(e => e.GetProperty("dtstart")[0].GetString()!);
        return en.Select(e => { string day = e.GetProperty("dtstart")[0].GetString()!; var d = DateOnly.ParseExact(day, "yyyyMMdd", CultureInfo.InvariantCulture); if (!zh.TryGetValue(day, out var z) || DateOnly.ParseExact(e.GetProperty("dtend")[0].GetString()!, "yyyyMMdd", CultureInfo.InvariantCulture) != d.AddDays(1)) throw new InvalidDataException("HongKongMismatch"); return new CalendarEvent(d, "HK", z.GetProperty("summary").GetString()!, e.GetProperty("summary").GetString()!, HkEnglish); }).ToArray();
    }
    public CalendarEvent[] On(DateOnly date) => Data.Events.Where(e => e.Date == date).Concat(Data.HongKongYears.Contains(date.Year) && date.DayOfWeek == DayOfWeek.Sunday ? new[] { new CalendarEvent(date, "HK", "星期日（公众假期）", "Sunday (general holiday)", HkEnglish) } : []).ToArray();
    public static string Lunar(DateOnly date)
    {
        var calendar = new ChineseLunisolarCalendar(); var dt = date.ToDateTime(TimeOnly.MinValue);
        if (dt < calendar.MinSupportedDateTime || dt > calendar.MaxSupportedDateTime) return "";
        int year = calendar.GetYear(dt), month = calendar.GetMonth(dt), leap = calendar.GetLeapMonth(year), day = calendar.GetDayOfMonth(dt); bool isLeap = leap > 0 && month == leap; if (leap > 0 && month >= leap) month--;
        string[] months = "正 二 三 四 五 六 七 八 九 十 冬 腊".Split(' '), digits = "一 二 三 四 五 六 七 八 九 十".Split(' ');
        string name = day <= 10 ? "初" + digits[day - 1] : day < 20 ? "十" + digits[day - 11] : day == 20 ? "二十" : day < 30 ? "廿" + digits[day - 21] : "三十";
        return (isLeap ? "闰" : "") + months[month - 1] + "月" + name;
    }
    public static CalendarEvent[] Festivals(DateOnly date)
    {
        string lunar = Lunar(date); if (lunar.Length == 0 || lunar.StartsWith("闰", StringComparison.Ordinal)) return [];
        string? zh = null, en = null;
        foreach (var pair in new[] { ("正月初一", "春节", "Spring Festival"), ("正月十五", "元宵节", "Lantern Festival"), ("五月初五", "端午节", "Dragon Boat Festival"), ("七月初七", "七夕", "Qixi Festival"), ("八月十五", "中秋节", "Mid-Autumn Festival"), ("九月初九", "重阳节", "Double Ninth Festival") }) if (lunar == pair.Item1) { zh = pair.Item2; en = pair.Item3; }
        if (date < DateOnly.MaxValue && Lunar(date.AddDays(1)) == "正月初一") { zh = "除夕"; en = "Lunar New Year's Eve"; }
        return zh == null ? [] : [new(date, "Festival", zh, en!, "")];
    }
    // An explicit action only. Redirects are disabled; no planner content is sent.
    public async Task UpdateAsync(string path, CancellationToken ct = default, Func<string, CancellationToken, Task<string>>? fetch = null)
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
        async Task<string> Get(string url) { if (fetch != null) return await fetch(url, ct).ConfigureAwait(false); using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false); response.EnsureSuccessStatusCode(); using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false); using var output = new MemoryStream(); byte[] buf = new byte[8192]; int n; while ((n = await stream.ReadAsync(buf, ct).ConfigureAwait(false)) > 0) { if (output.Length + n > 1_000_000) throw new InvalidDataException("SourceTooLarge"); output.Write(buf, 0, n); } return System.Text.Encoding.UTF8.GetString(output.ToArray()); }
        var events = Data.Events.Where(e => e.Kind is "CN" or "CNWork").ToList();
        foreach (int y in Data.TermYears) events.AddRange(ParseTerms(y, await Get(string.Format(CultureInfo.InvariantCulture, Hko, y)).ConfigureAwait(false)));
        events.AddRange(ParseHongKong(await Get(HkEnglish).ConfigureAwait(false), await Get(HkChinese).ConfigureAwait(false)));
        var next = Data with { UpdatedAt = DateTimeOffset.UtcNow, HongKongYears = events.Where(e => e.Kind == "HK").Select(e => e.Date.Year).Distinct().Order().ToArray(), Events = events.OrderBy(e => e.Date).ThenBy(e => e.Kind).ToArray() }; Validate(next);
        Install(path, next); Data = next;
    }
    public static void Install(string path, CalendarBundle data)
    {
        Validate(data); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); string temp = path + ".pending-" + Guid.NewGuid().ToString("N");
        try { using (var s = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { s.Write(JsonSerializer.SerializeToUtf8Bytes(data)); s.Flush(true); } Decode(File.ReadAllText(temp)); if (File.Exists(path)) { Decode(File.ReadAllText(path)); File.Replace(temp, path, path + ".bak"); } else File.Move(temp, path); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
