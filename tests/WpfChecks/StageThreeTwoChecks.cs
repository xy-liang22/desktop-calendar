using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static partial class StageThreeChecks
{
    static void Composition(UIElement target, bool start)
    {
        target.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, target, start ? "shi" : "是"))
        { RoutedEvent = start ? TextCompositionManager.PreviewTextInputStartEvent : TextCompositionManager.PreviewTextInputEvent });
    }
    static async Task RunP32Checks(MainWindow window)
    {
        var c = window.Session.Planner!; var v = window.PlannerView!; c.Select(c.Today.AddDays(6), true);
        var work = c.State!.Categories.Single(x => x.BuiltInKey == "work");
        var root = c.NewTodo("P32 outside") with { CategoryId = work.Id, Color = "#62A9E8" }; await c.ExecuteAsync(new SaveTodo(root)); root = c.State!.Todos.Single(t => t.Id == root.Id);
        var inherited = c.NewTodo("child draft", root.Id);
        Check("child draft inherits parent category and explicit color", inherited.CategoryId == root.CategoryId && inherited.Color == root.Color);
        await c.ExecuteAsync(new SaveTodo(inherited with { CategoryId = null, Color = "#57BD9C" }));
        await c.ExecuteAsync(new SaveTodo(root with { CategoryId = null, Color = "#B394E8" }));
        Check("parent later changes never overwrite child custom properties", c.State!.Todos.Single(t => t.Id == inherited.Id) is Todo child && child.CategoryId == null && child.Color == "#57BD9C");
        await DriveAsync(() => v.Edit(c.State!.Todos.Single(t => t.Id == root.Id)), async s =>
        {
            Field(s, "PlannerTitle").Text = "P32 activation saved"; window.Activate(); await Eventually(() => !s.IsVisible);
            Check("outside activation commits and closes actual editor window", !s.IsVisible && c.State!.Todos.Single(t => t.Id == root.Id).Title == "P32 activation saved");
        });
        await DriveAsync(() => v.Edit(c.State!.Todos.Single(t => t.Id == root.Id)), async s =>
        {
            Field(s, "PlannerTitle").Text = " "; Check("outside dismissal keeps invalid draft", !await s.OutsideCloseAsync() && s.IsVisible && s.Error.Text.Length > 0);
            foreach (var kind in new[] { 0, 1, 2 })
            {
                var combo = Children<ComboBox>(s).Single(); var date = Children<DatePicker>(s).Single(); var color = Children<ColorField>(s).Single();
                Field(s, "PlannerTitle").Text = "must not auto save via auxiliary popup";
                if (kind == 0) combo.IsDropDownOpen = true; else if (kind == 1) date.IsDropDownOpen = true; else color.Popup.IsOpen = true;
                await Idle(); Check("auxiliary popup is inside editor dismissal boundary " + kind, s.HasAuxiliary && !await s.OutsideCloseAsync() && s.IsVisible);
                combo.IsDropDownOpen = date.IsDropDownOpen = color.Popup.IsOpen = false;
            }
            var input = Field(s, "PlannerTitle"); input.Focus(); Composition(input, true); long rev = c.State!.Revision;
            SendKey(s, Key.Escape); Check("IME composition Escape does not save or close", s.IsVisible && c.State!.Revision == rev && !await s.OutsideCloseAsync());
            Composition(input, false); SendKey(s, Key.Escape); await Eventually(() => !s.IsVisible);
            Check("after composition ends normal Escape saves", !s.IsVisible && c.State!.Todos.Single(t => t.Id == root.Id).Title == "must not auto save via auxiliary popup");
        });
        Drive(() => PlannerDialogs.Confirm(window, "test", "important confirmation"), s =>
        {
            window.Activate(); Check("important confirmation retains native modal owner and no dismissal policy", (NativeWindow.GetWindowLong(window.Handle, -16) & 0x08000000) != 0 && s.IsVisible && !s.AutoSaveClose); Click(s.Actions.Children.OfType<Button>().First());
        });
        v.Quick.Text = "IME guarded quick"; Composition(v.Quick, true); long before = c.State!.Revision;
        v.Quick.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(v.Quick), 0, Key.Enter) { RoutedEvent = Keyboard.KeyDownEvent }); await Idle();
        Check("quick Enter during composition does not create task", c.State!.Revision == before && v.Quick.Text == "IME guarded quick"); Composition(v.Quick, false); v.Quick.Clear();
        foreach (CalendarTextMode mode in Enum.GetValues<CalendarTextMode>())
        {
            var entry = new CalendarTodoEntry("很长的任务标题，需要根据实际宽度换行并保持分类和完成状态清晰。", "09:30 ", false, "#62A9E8", false, () => { });
            var panel = new CalendarTaskPanel([entry], _ => { }, false, () => { }, mode); panel.Measure(new Size(100, 160)); panel.Arrange(new Rect(0, 0, 100, 160));
            double height = ((FrameworkElement)panel.Children[0]).ActualHeight;
            Check("measured calendar wrapping " + mode, panel.VisibleRows == 1 && (mode == CalendarTextMode.SingleLine ? height == 16 : mode == CalendarTextMode.TwoLines ? height == 32 : height > 32));
            for (int h = 0; h <= 96; h++) { panel.Measure(new Size(70, h)); panel.Arrange(new Rect(0, 0, 70, h)); if (panel.VisibleRows < 0 || panel.Remaining != 1 - panel.VisibleRows) throw new Exception("Unstable preview counts"); }
            var expanded = new CalendarTaskPanel([entry with { Id = root.Id, HasChildren = true, Expanded = true }, entry with { Title = "child", Id = inherited.Id, ParentId = root.Id, Depth = 1 }, entry with { Title = "sibling", Id = Guid.NewGuid() }, entry with { Title = "next", Id = Guid.NewGuid() }], _ => { }, false, () => { }, mode);
            expanded.Measure(new Size(100, 48)); expanded.Arrange(new Rect(0, 0, 100, 48));
            Check("expanded child precedes later sibling even when parent text needs wrapping " + mode, expanded.VisibleRows == 2 && expanded.Remaining == 2 && expanded.Children.OfType<DockPanel>().Where(x => x.Clip == null).Select(x => ((CalendarTodoEntry)x.Tag).Id).SequenceEqual([root.Id, inherited.Id]));
            c.SetTextMode(mode); window.UpdateLayout(); Image(window.Surface, "p32-wrap-" + mode + ".png");
        }
        Check("settings and controller share wrap mode without new business writes", window.CalendarWrapping.SelectedIndex == (int)CalendarTextMode.Full && new WindowPreferences().CalendarTextMode == CalendarTextMode.TwoLines);
    }
}
