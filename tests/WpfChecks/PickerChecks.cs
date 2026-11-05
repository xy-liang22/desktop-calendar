using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static partial class StageThreeChecks
{
    static FrameworkElement PickerPopup(ChoicePicker picker) => (FrameworkElement)((Popup)picker.Template.FindName("PART_Popup", picker)).Child;
    static bool MultiColumn(ChoicePicker picker)
    {
        var first = picker.ItemContainerGenerator.ContainerFromIndex(0) as ComboBoxItem; var second = picker.ItemContainerGenerator.ContainerFromIndex(1) as ComboBoxItem;
        if (first == null || second == null) return false; var host = PickerPopup(picker);
        var a = first.TranslatePoint(new Point(), host); var b = second.TranslatePoint(new Point(), host);
        return Math.Abs(a.Y - b.Y) < 2 && b.X > a.X + 10;
    }
    static async Task RunPickerChecks(MainWindow window)
    {
        var c = window.Session.Planner!; var view = window.PlannerView!; window.SetPage(false);
        Check("offline emoji catalogue has over 650 unique choices and intact ZWJ sequences", EmojiCatalog.Find(null, "").Length > 650 && EmojiCatalog.Find(null, "").Contains("👩‍💻") && EmojiCatalog.Find(null, "").All(x => !string.IsNullOrWhiteSpace(x)));
        Check("emoji search matches Chinese English and literal emoji", EmojiCatalog.Find(null, "咖啡").Contains("☕") && EmojiCatalog.Find(null, "coffee").Contains("☕") && EmojiCatalog.Find(null, "👩‍💻").Contains("👩‍💻"));
        string language = window.Appearance.UiLanguage;
        foreach (string locale in new[] { "zh", "en" })
        {
            Localization.Apply(locale);
            await DriveAsync(() => ActivityDialogs.Edit(window, c), async sheet =>
            {
                Field(sheet, "PlannerTitle").Text = "Picker grid " + locale;
                var emoji = (EmojiPicker)Choice(sheet, "ActivityEmoji"); emoji.Text = "👩🏽‍💻"; emoji.IsDropDownOpen = true; await Idle();
                Check("emoji popup is auxiliary and prevents outside save " + locale, sheet.HasAuxiliary && !await sheet.OutsideCloseAsync() && sheet.IsVisible);
                Check("emoji popup has multiple square choices per row " + locale, MultiColumn(emoji) && ((ComboBoxItem)emoji.ItemContainerGenerator.ContainerFromIndex(0)).ActualWidth >= 34);
                Image(PickerPopup(emoji), "picker-emoji-" + locale + ".png");
                emoji.Filter(null, "coffee"); await Idle();
                Check("filtering leaves custom emoji input intact " + locale, emoji.Text == "👩🏽‍💻" && emoji.Items.Contains("☕"));
                emoji.SelectedItem = "☕"; emoji.IsDropDownOpen = false;
                var reaction = (EmojiPicker)Choice(sheet, "ActivityReaction"); reaction.Text = "🥰"; reaction.IsDropDownOpen = true; await Idle();
                Check("mood picker shares full grouped catalogue " + locale, MultiColumn(reaction) && reaction.Items.Count > 80);
                Image(PickerPopup(reaction), "picker-mood-" + locale + ".png");
                SendKey(reaction, Key.Escape); await Idle();
                Check("Escape closes only emoji popup and keeps editor draft " + locale, !reaction.IsDropDownOpen && sheet.IsVisible && Field(sheet, "PlannerTitle").Text == "Picker grid " + locale);
                reaction.Text = "自定义 🥰";
                var appearance = Children<TodoAppearance>(sheet).Single(); var category = Children<ChoicePicker>(appearance).Single(); category.IsDropDownOpen = true; await Idle();
                Check("activity categories wrap as tags and retain auxiliary ownership " + locale, MultiColumn(category) && sheet.HasAuxiliary);
                Image(PickerPopup(category), "picker-categories-" + locale + ".png"); category.SelectedIndex = 1; category.IsDropDownOpen = false;
                await sheet.SaveAndCloseAsync(false);
            });
            Check("grid selection and custom mood are persisted together " + locale, c.State!.Activities.Any(a => a.Title == "Picker grid " + locale && a.Emoji == "☕" && a.Reaction == "自定义 🥰" && a.CategoryId != null));
        }
        Localization.Apply(language);
        var categoryField = Children<ChoicePicker>(view.QuickAppearance).Single(); categoryField.IsDropDownOpen = true; await Idle();
        Check("quick todo uses same multi-tag category selector", MultiColumn(categoryField)); categoryField.IsDropDownOpen = false;
        var selected = c.State!.Categories.First(x => !x.Archived); var todo = c.NewTodo("Description preview") with { CategoryId = selected.Id, Description = "说明小字 📝 " + new string('长', 140) + "\n第二行原文", PlannedDate = c.SelectedDate };
        await c.ExecuteAsync(new SaveTodo(todo)); await Idle();
        var card = view.Items.Children.OfType<Border>().Single(b => b.Tag is Todo t && t.Id == todo.Id);
        var info = Children<System.Windows.Controls.TextBlock>(card).Single(t => Equals(t.ToolTip, todo.Description));
        Check("todo description appears in small metadata row without altering original text", info.FontSize <= 11 && info.Text.Contains("说明小字") && info.Text.Contains(Localization.Category(selected)) && info.TextTrimming == TextTrimming.CharacterEllipsis && c.State!.Todos.Single(t => t.Id == todo.Id).Description == todo.Description);
        card.BringIntoView(); await Idle(); Image(window.Surface, "picker-todo-description.png");
        await DriveAsync(() => PlannerDialogs.Edit(window, c, todo), async sheet =>
        {
            var choice = Children<ChoicePicker>(Children<TodoAppearance>(sheet).Single()).Single(); choice.IsDropDownOpen = true; await Idle();
            Check("full todo editor uses same tag popup without autosaving", MultiColumn(choice) && sheet.HasAuxiliary && !await sheet.OutsideCloseAsync());
            choice.IsDropDownOpen = false; Click(sheet.Actions.Children.OfType<Button>().First());
        });
        var sortCategory = new Category { Name = "Picker reorder category", Color = "#B394E8", SortKey = c.State!.Categories.Max(x => x.SortKey) + 1024 };
        await c.ExecuteAsync(new SaveCategory(sortCategory)); sortCategory = c.State!.Categories.Single(x => x.Name == sortCategory.Name);
        await DriveAsync(() => PlannerDialogs.ManageCategories(window, c), async sheet =>
        {
            var list = Children<CategoryOrderList>(sheet).Single(); list.SelectedItem = c.State!.Categories.Single(x => x.Id == sortCategory.Id);
            list.ScrollIntoView(list.Items[0]); await Idle();
            Check("category insertion near top padding targets first visible row", list.TargetAt(new Point(4, 1))?.Content is Category top && top.Id == ((Category)list.Items[0]).Id);
            Field(sheet, "PlannerCategoryName").Text = "Draft retained after reorder";
            var first = PlannerQueries.OrderedCategories(c.State!).First();
            Check("category list saves an insertion move", await list.MoveAsync(sortCategory.Id, first.Id, true)); await Idle();
            Check("category reorder retains unsaved name and selected category", Field(sheet, "PlannerCategoryName").Text == "Draft retained after reorder" && ((Category)list.SelectedItem).Id == sortCategory.Id);
            Check("category order propagates immediately to quick selection", ((CategoryOption[])Children<ChoicePicker>(view.QuickAppearance).Single().ItemsSource)[1].Id == sortCategory.Id);
            Check("category keyboard alternative moves selected item down", await list.MoveSelectedAsync(1)); await Idle();
            Check("category manager uses scoped dark list theme", list.Background.ToString() == "#FF17232F" && list.BorderBrush.ToString() == "#FF465968");
            Image(sheet, "picker-category-management.png");
            var order = PlannerQueries.OrderedCategories(c.State!).Select(x => x.Id).ToArray(); await sheet.SaveAndCloseAsync(false);
            Check("saving category metadata cannot restore stale sort key", PlannerQueries.OrderedCategories(c.State!).Select(x => x.Id).SequenceEqual(order) && c.State!.Categories.Single(x => x.Id == sortCategory.Id).Name == "Draft retained after reorder");
        });
        File.WriteAllLines(Path.Combine(DirectoryPath, "category-order-ids.txt"), PlannerQueries.OrderedCategories(c.State!).Select(x => x.Id.ToString()));
    }
}
