namespace DesktopCalendar;

// Deliberately static presentation fixtures, never persisted or presented as user records.
internal static class DemoContent
{
    internal static readonly string[] Weekdays = { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };
    internal static readonly string[] CalendarTasks = { "阅读与摘录", "整理设计思路", "散步 30 分钟", "完成方案初稿", "每周回顾", "准备晚餐", "留给自己的时间" };
    internal static readonly string[] TodoNames = { "完成桌面日历视觉方案", "梳理本周阅读笔记", "出门散步，放松一下", "整理工作区" };
    internal static readonly string[] TodoMeta = { "09:00 · 工作 · 重要", "14:00 · 学习", "无计划时间 · 生活", "已完成 · 生活" };
    internal static readonly string[] Subtasks = { "确定布局与留白", "检查小尺寸可读性" };
    internal static readonly string[] GoalNames = { "完成一个值得展示的作品", "阅读与思考", "给生活留一点空间" };
    internal static readonly string[] GoalProgress = { "2 / 3 项 · 演示", "4 / 6 小时 · 演示", "3 / 5 次 · 演示" };
    internal const string WeekRibbon = "本周的一个小目标";
    internal const string WeekRibbonBody = "专注于重要的事，完成视觉方案初稿。";
    internal const string ReviewBody = "这周做得不错\n\n把零散的想法整理成了清晰的方案，也给自己留出了散步的时间。\n\n下周想尝试\n\n减少同时进行的事情，让专注的时间更完整。";
    internal const string ReviewCaption = "自由记录 · 示例文字";
    internal const string TodoProgress = "子任务 1 / 2";
    internal const string LunarPending = "农历、节气与假日资料将在后续阶段接入";
    internal static readonly string[] StatLabels = { "工作  18h", "学习  6h", "生活  8h" };
    internal static readonly string[] Colors = { "#A3D9C4", "#B6B5E6", "#E3BF89" };
    internal record Block(int Day, double Start, double End, string Title, string Note, int Color);
    internal static readonly Block[] Blocks = {
        new(0,9,11,"专注工作","梳理本周方向",0), new(0,13,14,"阅读","摘录与思考",1), new(0,17,18,"散步","公园里的傍晚",2),
        new(1,8,9,"晨间阅读","给自己一点安静",1), new(1,10,12,"方案整理","信息与结构",0), new(1,15,17,"设计探索","尝试新的排版",0),
        new(2,9,11.5,"专注工作","打磨细节",0), new(2,12,13,"午餐与休息","放慢节奏",2), new(2,15,16.5,"学习笔记","整理阅读收获",1),
        new(3,8,9,"晨间阅读","读完一个小章节",1), new(3,9.5,12,"视觉方案","月历布局与细节",0), new(3,12,13,"午餐与休息","好好吃饭",2), new(3,14,16,"原型打磨","让交互自然一点",0), new(3,17,18,"户外散步","今天的晚霞很好看",2),
        new(4,9,11,"方案收尾","整理与检查",0), new(4,14,15.5,"一周回顾","记录这一周",1),
        new(5,10,12,"自由阅读","一本喜欢的书",1), new(5,15,17,"城市漫步","探索熟悉的街道",2),
        new(6,9,10.5,"整理房间","清爽的新开始",2), new(6,16,17.5,"下周计划","留出弹性",0)
    };
}
