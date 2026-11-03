using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static partial class StageThreeChecks
{
    static async Task RunP9Soak(MainWindow window)
    {
        var samples = new System.Collections.Generic.List<object>(); var session = window.Session; var c = session.Planner!;
        try
        {
            int duration = int.TryParse(Environment.GetEnvironmentVariable("DESKTOP_CALENDAR_SOAK_SECONDS"), out int requested) && requested is >= 180 and <= 86400 ? requested : 180;
            await c.Pending; window.RestoreNormal(false); string digest = Digest(c.State!); using var process = Process.GetCurrentProcess(); var clock = Stopwatch.StartNew();
            long idleMemory = 0, idleHandles = 0; TimeSpan idleCpu = TimeSpan.Zero; double idleStart = 0;
            for (int second = 1; second <= duration; second++)
            {
                await Task.Delay(1000);
                if (second <= 20)
                {
                    if (second % 2 == 0) window.SetPage(!window.IsWeek);
                    if (second == 4) window.EnterCompact(); if (second == 6) window.RestoreNormal(false);
                    if (second == 10) window.SetPinned(true); if (second == 12) window.SetPinned(false);
                    if (second == 14) window.LanguageChoice.SelectedIndex = 0; if (second == 16) window.LanguageChoice.SelectedIndex = 1;
                    if (second == 20) { window.RestoreNormal(false); window.SetPage(false); }
                }
                if (second % 30 == 0)
                {
                    process.Refresh(); if (second == 30) { idleMemory = process.PrivateMemorySize64; idleHandles = process.HandleCount; idleCpu = process.TotalProcessorTime; idleStart = clock.Elapsed.TotalSeconds; }
                    samples.Add(new { Seconds = clock.Elapsed.TotalSeconds, CpuSeconds = process.TotalProcessorTime.TotalSeconds, PrivateBytes = process.PrivateMemorySize64, WorkingSet = process.WorkingSet64, ManagedBytes = GC.GetTotalMemory(false), Handles = process.HandleCount });
                    File.WriteAllText(Path.Combine(DirectoryPath, "p9-soak-samples.json"), JsonSerializer.Serialize(samples));
                }
            }
            process.Refresh(); double idlePercent = (process.TotalProcessorTime - idleCpu).TotalSeconds / (clock.Elapsed.TotalSeconds - idleStart) * 100; long growth = process.PrivateMemorySize64 - idleMemory, handleGrowth = process.HandleCount - idleHandles;
            Check("bounded component soak exits without business mutation", digest == Digest(c.State!) && window.Ready);
            Check("post-warm idle CPU under five percent of one logical core", idlePercent < 5);
            Check("post-warm private memory growth under 64 MiB", growth < 64L * 1024 * 1024);
            Check("post-warm handle growth under 64", handleGrowth < 64);
            File.WriteAllText(Path.Combine(DirectoryPath, "p9-soak-summary.json"), JsonSerializer.Serialize(new { DurationSeconds = clock.Elapsed.TotalSeconds, IdleCpuOneCorePercent = idlePercent, PrivateGrowthBytes = growth, HandleGrowth = handleGrowth, Limit = "Evidence applies only to the measured duration" }));
            Image(window.Surface, "p9-soak-last.png");
        }
        catch (Exception e) { results.Add("FAIL soak " + e); }
        File.WriteAllLines(Path.Combine(DirectoryPath, "checks-soak.txt"), results); session.Exit(results.Any(r => r.StartsWith("FAIL ")) ? 1 : 0);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static WeakReference WeekReference(MainWindow window) => new(window.PageHost.Children.OfType<ActivityWeekView>().Single());
    static async Task RunP9Checks(MainWindow window)
    {
        var c = window.Session.Planner!; window.RestoreNormal(); var business = Digest(c.State!); var data = window.Session.Calendar; var raw = JsonSerializer.Serialize(data.Data);
        Check("calendar short lunar uses month only on first day", CalendarPresentation.ShortLunar(new(2026, 11, 9)) == "十月" && CalendarPresentation.ShortLunar(new(2026, 12, 9)) == "十一月");
        Check("calendar short lunar preserves ordinary and special day names", CalendarPresentation.ShortLunar(new(2026, 11, 10)) == "初二" && CalendarPresentation.ShortLunar(new(2026, 11, 18)) == "初十" && CalendarPresentation.ShortLunar(new(2026, 11, 28)) == "二十" && CalendarPresentation.ShortLunar(new(2026, 12, 8)) == "三十");
        Check("leap first day preserves correct month", CalendarPresentation.ShortLunar(new(2025, 7, 25)) == "闰六月" && CalendarPresentation.ShortLunar(new(2023, 3, 22)) == "闰二月");
        Check("full lunar detail and unsupported range stay unchanged", CalendarKnowledge.Lunar(new(2026, 11, 10)) == "十月初二" && CalendarPresentation.Detail(data, new(2026, 11, 10)).Contains("十月初二") && CalendarPresentation.ShortLunar(new(1901, 2, 18)) == "");
        var sunday = data.On(new(2026, 11, 1)).Single(e => CalendarPresentation.OrdinarySunday(e));
        Check("only generic Sunday removed from compact information", !CalendarPresentation.CompactEvents(data, sunday.Date).Any(CalendarPresentation.OrdinarySunday) && data.On(sunday.Date).Contains(sunday));
        Check("named Sunday holiday is never filtered by weekday", !CalendarPresentation.OrdinarySunday(sunday with { Zh = "某具名公众假期", En = "A named public holiday" }));
        Check("observed HK holiday and coincident region events retained", CalendarPresentation.CompactEvents(data, new(2027, 12, 27)).Any(e => e.Kind == "HK") && CalendarPresentation.CompactEvents(data, new(2026, 6, 19)).Any(e => e.Kind == "HK") && CalendarPresentation.CompactEvents(data, new(2026, 6, 19)).Any(e => e.Kind == "CN"));
        var entries = Enumerable.Range(0, 10).Select(i => new CalendarTodoEntry("Preview title " + i + " / long wrapping text", "09:30 ", i % 2 == 0, "#62A9E8", false, () => { })).ToArray();
        var sheet = new Grid { Width = 720, Height = 520, Background = ThemeColors.Brush("#19232E") }; for (int i = 0; i < 4; i++) { sheet.ColumnDefinitions.Add(new()); sheet.RowDefinitions.Add(new()); }
        int sample = 0;
        foreach (int count in new[] { 0, 1, 3, 10 }) foreach (int width in new[] { 36, 56, 96, 160 }) foreach (int height in new[] { 37, 70, 120 })
        {
            var cell = new CalendarPreviewCell(new DateOnly(2026, 6, 19), entries.Take(count).ToArray(), true, false, () => { }, CalendarTextMode.TwoLines) { Width = width, Height = height }; cell.AddKnowledge(data, new(2026, 6, 19)); cell.Measure(new(width, height)); cell.Arrange(new(0, 0, width, height)); cell.UpdateLayout();
            var header = (Grid)cell.CalendarInfo!.Parent; var dateText = header.Children.OfType<TextBlock>().First(); var moreRect = cell.HeaderMore.TransformToAncestor(cell).TransformBounds(new Rect(cell.HeaderMore.RenderSize));
            Check($"single-row calendar geometry count={count} width={width} height={height}", cell.RowDefinitions.Count == 2 && Math.Abs(cell.Preview.ActualHeight - (cell.ActualHeight - 21)) < 1 && Grid.GetColumn(cell.CalendarInfo) == 1 && dateText.ActualWidth > 0 && moreRect.Right <= cell.ActualWidth + .5 && cell.Preview.Remaining + cell.Preview.VisibleRows == count);
            if (height == 120) { cell.Height = 115; Grid.SetColumn(cell, sample % 4); Grid.SetRow(cell, sample / 4); sheet.Children.Add(cell); sample++; }
        }
        sheet.Measure(new(720, 520)); sheet.Arrange(new(0, 0, 720, 520)); Image(sheet, "p9-header-matrix.png");
        var solar = new CalendarPreviewCell(new DateOnly(2026, 4, 5), [], true, false, () => { }); solar.AddKnowledge(data, new(2026, 4, 5));
        Check("solar term priority and full multi-event tooltip", new TextRange(solar.CalendarInfo!.ContentStart, solar.CalendarInfo.ContentEnd).Text.Contains(Localization.English ? "Bright" : "清明") && solar.CalendarInfo.ToolTip!.ToString() == CalendarPresentation.Detail(data, new(2026, 4, 5)));
        foreach (string language in new[] { "zh-CN", "en" })
        {
            window.LanguageChoice.SelectedIndex = language == "en" ? 1 : 0; await Idle(); c.Select(c.Today, true);
            foreach (var size in new[] { (880d, 520d), (1240d, 760d), (1480d, 940d) }) { window.Width = size.Item1; window.Height = size.Item2; await Idle(); window.UpdateLayout(); Image(window.Surface, $"p9-{language}-{size.Item1}.png"); Check($"localized calendar fits {language} {size.Item1}", Children<CalendarPreviewCell>(window.PlannerView!).All(cell => cell.CalendarInfo != null && cell.Preview.ActualHeight >= 0 && cell.RowDefinitions[0].ActualHeight >= 20)); }
        }
        window.Width = 1240; window.Height = 760; var view = window.PlannerView!; view.ColumnDefinitions[0].Width = new GridLength(6); view.ColumnDefinitions[2].Width = new GridLength(2); view.ColumnDefinitions[4].Width = new GridLength(3); await Idle();
        double before = view.ColumnDefinitions[0].ActualWidth / view.ActualWidth; var beforeWidths = view.ColumnDefinitions.Select(d => new { d.ActualWidth, Weight = d.Width.ToString() }).ToArray(); var reference = WeekReference(window); window.LanguageChoice.SelectedIndex = 0; await Idle();
        File.WriteAllText(Path.Combine(DirectoryPath, "p9-columns.json"), JsonSerializer.Serialize(new { Before = beforeWidths, After = window.PlannerView!.ColumnDefinitions.Select(d => new { d.ActualWidth, Weight = d.Width.ToString() }).ToArray() }));
        Check("language switch retains user column widths", Math.Abs(window.PlannerView!.ColumnDefinitions[0].ActualWidth / window.PlannerView.ActualWidth - before) < .015);
        for (int i = 0; i < 3; i++) { await Idle(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
        Check("detached weekly view is not retained by owner closed event", !reference.IsAlive);
        Check("presentation fixes do not mutate official data or business graph", JsonSerializer.Serialize(data.Data) == raw && Digest(c.State!) == business);
        window.LanguageChoice.SelectedIndex = 1; await Idle(); window.EnterCompact(); window.Session.Save(); File.WriteAllText(Path.Combine(DirectoryPath, "expected-hash.txt"), Digest(c.State!));
    }
}
