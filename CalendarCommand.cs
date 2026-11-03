using System;
using System.IO;
using System.Linq;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static class CalendarCommand
{
    internal static bool TryRun(string[] args, out int code)
    {
        code = 0; int pos = Array.IndexOf(args, "--calendar-action"); if (pos < 0) return false;
        try
        {
            string action = args[pos + 1]; int rootPos = Array.IndexOf(args, "--data-root"); string root = rootPos < 0 ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopCalendar") : Path.GetFullPath(args[rootPos + 1]);
            string path = Path.Combine(root, "calendar-knowledge.json"); var data = new CalendarKnowledge(path);
            if (action == "update") { if (!args.Contains("--online")) throw new InvalidOperationException("Explicit --online required"); data.UpdateAsync(path).GetAwaiter().GetResult(); }
            else if (action == "import") { int from = Array.IndexOf(args, "--package"); CalendarKnowledge.Install(path, CalendarKnowledge.Decode(File.ReadAllText(args[from + 1]))); data = new CalendarKnowledge(path); }
            else if (action != "status") throw new ArgumentException("Unknown action");
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { data.Data.UpdatedAt, data.Data.TermYears, data.Data.MainlandYears, data.Data.HongKongYears, data.Diagnostic }));
        }
        catch (Exception e) { code = 1; Console.Error.WriteLine("Calendar action failed; previous data retained: " + e.GetType().Name); }
        return true;
    }
}
