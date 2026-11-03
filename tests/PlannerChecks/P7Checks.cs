using DesktopCalendar.Core;
using System.Text.Json;

internal static partial class Checks
{
    static async Task P7Checks(string output)
    {
        var data = new CalendarKnowledge(); Check("bundled official calendar validates", data.Data.Events.Length == 387);
        Check("lunar lower and upper boundaries explicit", CalendarKnowledge.Lunar(new(1901, 2, 18)) == "" && CalendarKnowledge.Lunar(new(1901, 2, 19)) == "正月初一" && CalendarKnowledge.Lunar(new(2101, 1, 29)) == "");
        Check("known leap month and year crossing", CalendarKnowledge.Lunar(new(2023, 3, 22)) == "闰二月初一" && CalendarKnowledge.Lunar(new(2026, 2, 17)) == "正月初一" && CalendarKnowledge.Festivals(new(2026, 2, 16)).Single().Zh == "除夕");
        Check("term dates and midnight boundary retained", data.On(new(2026, 4, 5)).Any(e => e.Kind == "Term" && e.Zh == "清明") && !data.On(new(2026, 4, 4)).Any(e => e.Kind == "Term"));
        Check("mainland workday distinct from holiday", data.On(new(2026, 2, 14)).Any(e => e.Kind == "CNWork") && !data.On(new(2026, 2, 14)).Any(e => e.Kind == "CN"));
        Check("coincident mainland HK and traditional festival", data.On(new(2026, 6, 19)).Count(e => e.Kind is "CN" or "HK") == 2 && CalendarKnowledge.Festivals(new(2026, 6, 19)).Length == 1);
        Check("unpublished mainland year not guessed", !data.Data.MainlandYears.Contains(2027) && !data.On(new(2027, 10, 1)).Any(e => e.Kind is "CN" or "CNWork"));
        Check("HK Sunday also a general holiday", data.On(new(2026, 1, 4)).Any(e => e.Kind == "HK" && e.En.StartsWith("Sunday")));
        var path = Path.Combine(output, "calendar-" + Guid.NewGuid().ToString("N"), "calendar-knowledge.json"); CalendarKnowledge.Install(path, data.Data); var before = File.ReadAllBytes(path);
        try { await data.UpdateAsync(path, fetch: (_, _) => throw new IOException("offline fixture")); Check("offline update failure", false); } catch (IOException) { Check("offline update failure retains exact bytes", File.ReadAllBytes(path).SequenceEqual(before)); }
        void Invalid(string name, CalendarBundle b) { try { CalendarKnowledge.Validate(b); Check(name, false); } catch (InvalidDataException) { Check(name, true); } }
        Invalid("future calendar schema rejected", data.Data with { SchemaVersion = 999 }); Invalid("incomplete terms rejected", data.Data with { Events = data.Data.Events.Where(e => e.Kind != "Term" || e.Date != new DateOnly(2026, 4, 5)).ToArray() });
        Invalid("untrusted data source rejected", data.Data with { Events = data.Data.Events.Select((e, i) => i == 0 ? e with { Source = "https://example.com/data" } : e).ToArray() });
        string fixtures = Path.GetFullPath(Path.Combine(output, "..", "calendar-sources"));
        if (Directory.Exists(fixtures))
        {
            var hk = CalendarKnowledge.ParseHongKong(File.ReadAllText(Path.Combine(fixtures, "hk-en.json")), File.ReadAllText(Path.Combine(fixtures, "hk-sc.json")));
            Check("official bilingual HK download pairs 51 dates", hk.Length == 51 && hk.All(e => e.En != e.Zh));
            foreach (int year in data.Data.TermYears) Check("official HKO complete terms " + year, CalendarKnowledge.ParseTerms(year, File.ReadAllText(Path.Combine(fixtures, "T" + year + ".txt"))).SequenceEqual(data.Data.Events.Where(e => e.Kind == "Term" && e.Date.Year == year)));
            await data.UpdateAsync(path, fetch: (url, _) => Task.FromResult(File.ReadAllText(Path.Combine(fixtures, url.EndsWith("en.json") ? "hk-en.json" : url.EndsWith("sc.json") ? "hk-sc.json" : "T" + url.Split('/').Last()[1..5] + ".txt"))));
            Check("shared update validates and atomically replaces bundle", File.Exists(path + ".bak") && new CalendarKnowledge(path).Data.Events.Length == 387);
        }
        File.WriteAllText(path, "broken fixture"); var recovered = new CalendarKnowledge(path); Check("bad calendar falls back without touching original", recovered.Diagnostic.Length > 0 && recovered.Data.Events.Length == 387 && File.ReadAllText(path) == "broken fixture");
    }
}
