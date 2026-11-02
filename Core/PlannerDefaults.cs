using System.Collections.Immutable;

namespace DesktopCalendar.Core;

// Initial localized names only. Once edited, names remain user-owned content.
public static class PlannerDefaults
{
    public static ImmutableArray<Category> Categories(string language, DateTimeOffset now)
    {
        if (language is not ("zh-CN" or "en")) throw new PlannerException("InvalidOptions");
        var names = language == "zh-CN" ? new[] { "工作", "家务", "个人", "家庭", "健康" } : ["Work", "Chores", "Personal", "Family", "Health"];
        string[] keys = ["work", "chores", "personal", "family", "health"];
        string[] colors = ["#62A9E8", "#E6A65E", "#B394E8", "#E38DA8", "#57BD9C"];
        return [.. keys.Select((k, i) => new Category { Id = Guid.NewGuid(), BuiltInKey = k, Name = names[i], Color = colors[i], SortKey = i * 1024m, CreatedAt = now, ModifiedAt = now })];
    }
    public static bool LegacyPriority(Category c) => c.BuiltInKey == "important" && c.Name is "重要" or "Important" || c.BuiltInKey == "urgent" && c.Name is "紧急" or "Urgent";
}
