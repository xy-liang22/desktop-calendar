using TextBlock = DesktopCalendar.EmojiTextBlock;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopCalendar;

// Keep normal ComboBox editing, keyboard selection and auxiliary-popup ownership.
internal class ChoicePicker : ComboBox
{
    public static readonly DependencyProperty PickerHeaderProperty = DependencyProperty.Register(nameof(PickerHeader), typeof(UIElement), typeof(ChoicePicker));
    public static readonly DependencyProperty PopupWidthProperty = DependencyProperty.Register(nameof(PopupWidth), typeof(double), typeof(ChoicePicker), new PropertyMetadata(340d));
    public UIElement? PickerHeader { get => (UIElement?)GetValue(PickerHeaderProperty); set => SetValue(PickerHeaderProperty, value); }
    public double PopupWidth { get => (double)GetValue(PopupWidthProperty); set => SetValue(PopupWidthProperty, value); }
    internal ChoicePicker()
    {
        MinHeight = 28; IsTextSearchEnabled = false;
        SetResourceReference(StyleProperty, "GridChoicePicker");
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && IsDropDownOpen) { IsDropDownOpen = false; e.Handled = true; } };
    }
}

internal sealed record EmojiGroup(string Key, string Mark, string SearchTerms, string Values);
internal static class EmojiCatalog
{
    // Curated Unicode text, not third-party artwork or an online emoji service.
    internal static readonly EmojiGroup[] Groups =
    [
        new("EmojiFaces", "😀", "笑脸 心情 表情 faces mood emotions smile", "😀 😃 😄 😁 😆 😅 😂 🤣 🥲 😊 😇 🙂 🙃 😉 😌 😍 🥰 😘 😗 😙 😚 😋 😛 😝 😜 🤪 🤨 🧐 🤓 😎 🥸 🤩 🥳 😏 😒 😞 😔 😟 😕 🙁 ☹️ 😣 😖 😫 😩 🥺 😢 😭 😤 😠 😡 🤬 🤯 😳 🥵 🥶 😶 😐 😑 😬 🫣 🤭 🫢 🤫 🤔 🫡 🤗 🫠 🤥 😶‍🌫️ 😮 😯 😲 😱 😨 😰 😥 😓 😪 😴 🥱 🤤 😵 😵‍💫 🤐 🥴 🤢 🤮 🤧 😷 🤒 🤕 🤑 🤠 😈 👿 👻 💀 ☠️ 👽 🤖 💩 🎃 😺 😸 😹 😻 😼 😽 🙀 😿 😾"),
        new("EmojiPeople", "👋", "人物 手势 职业 人 people hands gestures work", "👋 🤚 🖐️ ✋ 🖖 👌 🤌 🤏 ✌️ 🤞 🤟 🤘 🤙 👈 👉 👆 👇 ☝️ 👍 👎 ✊ 👊 🤛 🤜 👏 🙌 👐 🤲 🙏 🤝 ✍️ 💅 💪 🦾 🦵 🦶 👀 👁️ 👂 👃 🧠 🫀 🦷 👶 🧒 👦 👧 🧑 👨 👩 🧓 👴 👵 👨‍💻 👩‍💻 🧑‍💻 👨‍🍳 👩‍🍳 👨‍🎓 👩‍🎓 👩‍🏫 👨‍⚕️ 👩‍⚕️ 👨‍🌾 👩‍🔧 👨‍🚀 👩‍🎨 👮 🕵️ 💂 👷 🥷 👸 🤴 🧕 👰 🤵 🤱 👼 🧘 🏃 🚶 🧍 💃 🕺 🛀 🛌 👪 👫 👭 👬"),
        new("EmojiNature", "🌿", "动物 植物 天气 自然 宠物 animals plants nature weather pet", "🐶 🐱 🐭 🐹 🐰 🦊 🐻 🐼 🐨 🐯 🦁 🐮 🐷 🐸 🐵 🙈 🙉 🙊 🐔 🐧 🐦 🐤 🦆 🦉 🦅 🦇 🐺 🦄 🐝 🐛 🦋 🐌 🐞 🐢 🐍 🦎 🐙 🦑 🦐 🦀 🐠 🐟 🐬 🐳 🦈 🐘 🦒 🦓 🐎 🦌 🐈 🐕 🐾 🌵 🎄 🌲 🌳 🌴 🌱 🌿 ☘️ 🍀 🍃 🍂 🍁 🍄 🌾 💐 🌷 🌹 🌺 🌸 🌼 🌻 🌞 🌝 🌛 🌜 🌚 🌙 ⭐ 🌟 ✨ ⚡ ☄️ 💥 🔥 🌈 ☀️ 🌤️ ⛅ ☁️ 🌧️ ⛈️ 🌨️ ❄️ ☃️ 💨 💧 🌊"),
        new("EmojiFood", "🍎", "食物 饮料 做饭 水果 food drink cooking fruit meal", "🍏 🍎 🍐 🍊 🍋 🍌 🍉 🍇 🍓 🫐 🍈 🍒 🍑 🥭 🍍 🥥 🥝 🍅 🥑 🥦 🥬 🥒 🌶️ 🌽 🥕 🧄 🧅 🥔 🍠 🥐 🥯 🍞 🥖 🥨 🧀 🥚 🍳 🥞 🧇 🥓 🥩 🍗 🍖 🌭 🍔 🍟 🍕 🥪 🌮 🌯 🥗 🥘 🍝 🍜 🍲 🍛 🍣 🍱 🥟 🍙 🍚 🍘 🍥 🍢 🍡 🍧 🍨 🍦 🥧 🧁 🍰 🎂 🍮 🍭 🍬 🍫 🍿 🍩 🍪 🥜 🍯 🍼 🥛 ☕ 🍵 🧃 🥤 🧋 🍶 🍺 🍻 🥂 🍷 🥃 🍸 🍹 🍾 🧊 🥄 🍴 🍽️ 🥢"),
        new("EmojiTravel", "🚆", "旅行 交通 地点 建筑 travel transport places home car", "🚗 🚕 🚙 🚌 🚎 🏎️ 🚓 🚑 🚒 🚐 🛻 🚚 🚛 🚜 🛵 🏍️ 🚲 🛴 🚂 🚆 🚇 🚊 🚉 ✈️ 🛫 🛬 🛩️ 🚀 🛸 🚁 ⛵ 🚤 🛳️ 🚢 ⚓ ⛽ 🚦 🚧 🗺️ 🗿 🗽 🗼 🏰 🏯 🏟️ 🎡 🎢 🎠 ⛲ ⛱️ 🏖️ 🏝️ 🏜️ 🌋 ⛰️ 🏔️ 🗻 🏕️ ⛺ 🏠 🏡 🏘️ 🏚️ 🏗️ 🏭 🏢 🏬 🏣 🏤 🏥 🏦 🏨 🏪 🏫 🏛️ ⛪ 🕌 🕍 ⛩️ 🛤️ 🛣️ 🌅 🌄 🌠 🎇 🎆 🌇 🌆 🌃 🌌 🌉"),
        new("EmojiActivities", "🎯", "活动 运动 工作 学习 休息 艺术 游戏 activities sport study work rest art games", "⚽ 🏀 🏈 ⚾ 🥎 🎾 🏐 🏉 🥏 🎱 🏓 🏸 🏒 🏑 🏏 🥍 🏹 ⛳ 🪁 🎣 🤿 🥊 🥋 🎽 🛹 🛼 🛷 ⛸️ 🎿 ⛷️ 🏂 🏋️ 🤸 ⛹️ 🤺 🤼 🤽 🚴 🧗 🏄 🏊 🚣 🏆 🥇 🥈 🥉 🏅 🎖️ 🎗️ 🎫 🎟️ 🎪 🤹 🎭 🩰 🎨 🎬 🎤 🎧 🎼 🎹 🥁 🎷 🎺 🎸 🎻 🎲 ♟️ 🎯 🎳 🎮 🕹️ 🧩 🎰 🎉 🎊 🎈 🎁 🎀 🪄 🧶 🧵"),
        new("EmojiObjects", "💡", "物品 办公 学习 科技 工具 objects office study technology tools", "💼 🎒 👜 👓 🕶️ 🧢 👒 👟 👕 👗 👔 ⌚ 📱 💻 ⌨️ 🖥️ 🖨️ 🖱️ 💽 💾 💿 📀 📷 📸 📹 🎥 📺 📻 🎙️ 📞 ☎️ 📟 🔋 🔌 💡 🔦 🕯️ 🧯 🛢️ 💸 💵 💰 💳 💎 ⚖️ 🧰 🔧 🔨 ⚒️ 🛠️ ⛏️ 🔩 ⚙️ 🧱 🔑 🗝️ 🔒 🔓 🧪 🧫 🧬 🔬 🔭 📡 💉 💊 🩹 🩺 🚪 🛏️ 🛋️ 🪑 🚽 🚿 🛁 🧴 🧹 🧺 🧻 🧼 🪥 🧽 🪣 📦 ✉️ 📧 📩 📬 📮 📋 📁 📂 🗂️ 🗃️ 📅 🗓️ 📆 📈 📉 📊 📌 📍 📎 🖇️ 📏 📐 ✂️ 🖊️ 🖋️ ✏️ 🖍️ 📝 📖 📚 📒 📓 📔 📕 📗 📘 📙 🔖 🏷️ 📰 🗑️"),
        new("EmojiSymbols", "❤️", "符号 爱心 标记 心情 symbols hearts signs mood reaction", "❤️ 🧡 💛 💚 💙 💜 🖤 🤍 🤎 💔 ❣️ 💕 💞 💓 💗 💖 💘 💝 💟 ☮️ ✝️ ☪️ ☯️ ☸️ ✡️ ♈ ♉ ♊ ♋ ♌ ♍ ♎ ♏ ♐ ♑ ♒ ♓ 🆔 ⚛️ 🔯 ♾️ 🆚 💯 💢 💬 🗨️ 🗯️ 💭 💤 ✅ ☑️ ✔️ ❌ ❎ ➕ ➖ ➗ ✖️ ❗ ❓ ❕ ❔ ⚠️ 🚫 ⛔ 🔴 🟠 🟡 🟢 🔵 🟣 ⚫ ⚪ 🟤 🔺 🔻 🔸 🔹 🔶 🔷 🔘 🔲 🔳 🔔 🔕 🎵 🎶 🔊 🔈 🔇 🔀 🔁 🔂 ▶️ ⏸️ ⏹️ ⏺️ ⏭️ ⏮️ ⏩ ⏪ ⏰ ⏱️ ⏲️ ⌛ ⏳ 🔜 🔙 🔝 🔛 🔚 🏁 🚩 🎌 🏳️ 🏴 🏳️‍🌈")
    ];
    static readonly Dictionary<string, string> Aliases = new()
    {
        ["😊"]="开心 高兴 微笑 happy smile", ["😂"]="笑哭 laugh joy", ["🥰"]="喜欢 幸福 love", ["😍"]="喜欢 爱 love", ["😢"]="难过 哭 sad cry", ["😭"]="大哭 难过 cry sad", ["😴"]="睡觉 困 sleep tired", ["🤔"]="思考 think", ["😡"]="生气 angry", ["👍"]="赞 好 good like", ["🙏"]="感谢 thank pray", ["❤️"]="爱 喜欢 love heart", ["🔥"]="火 热情 fire", ["💻"]="电脑 工作 编程 computer work coding", ["📚"]="书 学习 读书 books study read", ["💼"]="工作 work", ["☕"]="咖啡 coffee", ["🍵"]="茶 tea", ["🐱"]="猫 cat", ["🐶"]="狗 dog", ["🏠"]="家 home", ["🏃"]="跑步 锻炼 run exercise", ["🎨"]="画画 艺术 art paint", ["🎮"]="游戏 game", ["🎂"]="生日 蛋糕 birthday cake", ["🎉"]="庆祝 派对 party", ["💊"]="药 健康 medicine health", ["🧘"]="冥想 瑜伽 meditation yoga", ["🍽️"]="吃饭 饭 meal eat", ["✅"]="完成 done complete"
    };
    internal static string[] Find(string? group, string search)
    {
        var terms = search.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Groups.Where(g => group == null || g.Key == group).SelectMany(g => g.Values.Split(' ').Select(value => (Group: g, Value: value)))
            .Where(e => terms.All(term => (e.Value + " " + e.Group.SearchTerms + " " + Aliases.GetValueOrDefault(e.Value, "")).Contains(term, StringComparison.OrdinalIgnoreCase)))
            .Select(e => e.Value).Distinct().ToArray();
    }
}

internal sealed class EmojiPicker : ChoicePicker
{
    readonly TextBox search = new() { MinHeight = 28, Margin = new Thickness(0, 4, 0, 7), Padding = new Thickness(6) };
    readonly Dictionary<string, Button> groups = new();
    readonly TextBlock empty = PlannerView.Text(MainWindow.Text("PickerNoResults"), 11, "#A2AFBC");
    string? group;
    internal EmojiPicker(string current, bool reaction = false)
    {
        IsEditable = true; StaysOpenOnEdit = true; Text = current; PopupWidth = 360;
        ItemContainerStyle = (Style)FindResource("EmojiGridItem");
        var label = new FrameworkElementFactory(typeof(TextBlock)); label.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding()); label.SetValue(TextBlock.FontSizeProperty, 22d);
        ItemTemplate = new DataTemplate { VisualTree = label };
        var header = new StackPanel(); var tabs = new WrapPanel(); var all = Tab("", "PickerAll", "∗"); tabs.Children.Add(all);
        foreach (var item in EmojiCatalog.Groups) tabs.Children.Add(Tab(item.Key, item.Key, item.Mark));
        header.Children.Add(tabs); header.Children.Add(PlannerView.Text(MainWindow.Text("PickerSearch"), 10, "#A2AFBC")); AutomationProperties.SetName(search, MainWindow.Text("PickerSearch")); search.ToolTip = MainWindow.Text("PickerSearch"); header.Children.Add(search);
        search.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) e.Handled = true; };
        var clear = PlannerView.Button(MainWindow.Text("PickerClear"), () => { Text = ""; SelectedIndex = -1; IsDropDownOpen = false; }); clear.HorizontalAlignment = HorizontalAlignment.Left; header.Children.Add(clear); header.Children.Add(empty); PickerHeader = header;
        search.TextChanged += (_, _) => { if (search.Text.Length > 0) group = null; Refresh(); };
        group = reaction ? "EmojiFaces" : "EmojiActivities"; Refresh(); Text = current;
    }
    Button Tab(string key, string title, string mark)
    {
        var button = new Button { Content = new TextBlock { Text = mark, FontSize = 17 }, Width = 32, Height = 30, Margin = new Thickness(2), ToolTip = MainWindow.Text(title) };
        button.SetResourceReference(StyleProperty, "ColorSwatch"); AutomationProperties.SetName(button, MainWindow.Text(title)); groups.Add(key, button);
        button.Click += (_, _) => { group = key.Length == 0 ? null : key; search.Text = ""; Refresh(); }; return button;
    }
    internal void Filter(string? key, string text = "") { group = key; search.Text = text; Refresh(); }
    void Refresh()
    {
        string value = Text; ItemsSource = EmojiCatalog.Find(group, search.Text); Text = value;
        empty.Visibility = Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var pair in groups) { bool active = pair.Key == (group ?? ""); pair.Value.Background = ThemeColors.Brush(active ? "#3A5662" : "#263543"); pair.Value.BorderBrush = ThemeColors.Brush(active ? "#A3D9C4" : "#465968"); pair.Value.BorderThickness = new Thickness(1); }
    }
}
