using System.Collections.Immutable;

namespace DesktopCalendar.Core;

// Initial localized names only. Once edited, names remain user-owned content.
public static class PlannerDefaults
{
    public static ImmutableArray<Category> Categories(string language, DateTimeOffset now)
    {
        if (language is not ("zh-CN" or "en")) throw new PlannerException("InvalidOptions");
        var names = language == "zh-CN" ? new[] { "日常", "重要", "紧急", "工作", "生活" }
            : ["Daily", "Important", "Urgent", "Work", "Life"];
        string[] keys = ["daily", "important", "urgent", "work", "life"];
        string[] colors = ["#AFC5D8", "#C2B4D9", "#D1AAA6", "#A3BDB0", "#B7C2AE"];
        return [.. keys.Select((k, i) => new Category { Id = Guid.NewGuid(), BuiltInKey = k, Name = names[i], Color = colors[i], SortKey = i * 1024m, CreatedAt = now, ModifiedAt = now })];
    }
}
