using System.Collections.Immutable;

namespace DesktopCalendar.Core;

// Initial localized names only. Once edited, names remain user-owned content.
public static class PlannerDefaults
{
    public static ImmutableArray<Category> Categories(string language, DateTimeOffset now)
    {
        if (language is not ("zh-CN" or "en")) throw new PlannerException("InvalidOptions");
        var names = language == "zh-CN" ? new[] { "日常", "工作", "生活" } : ["Daily", "Work", "Life"];
        string[] keys = ["daily", "work", "life"];
        string[] colors = ["#AFC5D8", "#A3BDB0", "#B7C2AE"];
        return [.. keys.Select((k, i) => new Category { Id = Guid.NewGuid(), BuiltInKey = k, Name = names[i], Color = colors[i], SortKey = i * 1024m, CreatedAt = now, ModifiedAt = now })];
    }
    public static bool LegacyPriority(Category c) => c.BuiltInKey == "important" && c.Name is "重要" or "Important" || c.BuiltInKey == "urgent" && c.Name is "紧急" or "Urgent";
}
