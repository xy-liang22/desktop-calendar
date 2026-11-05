using TextBlock = DesktopCalendar.EmojiTextBlock;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DesktopCalendar.Core;

namespace DesktopCalendar;

internal sealed class CategoryOrderList : ListBox
{
    const string Format = "DesktopCalendar.CategoryOrder";
    readonly PlannerController controller;
    readonly Action saved;
    readonly Action<string> error;
    Point down;
    Guid? candidate, dragging;
    ListBoxItem? indicator;
    internal CategoryOrderList(PlannerController controller, Action saved, Action<string> error)
    {
        this.controller = controller; this.saved = saved; this.error = error; Height = 180; AllowDrop = true;
        SetResourceReference(StyleProperty, "CategoryOrderListStyle");
        SetResourceReference(ItemContainerStyleProperty, "CategoryOrderItem");
        var row = new FrameworkElementFactory(typeof(StackPanel)); row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var grip = new FrameworkElementFactory(typeof(TextBlock)); grip.SetValue(TextBlock.TextProperty, "≡"); grip.SetValue(TextBlock.ForegroundProperty, ThemeColors.Brush("#A2AFBC")); grip.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 8, 0)); row.AppendChild(grip);
        var dot = new FrameworkElementFactory(typeof(Ellipse)); dot.SetValue(Ellipse.WidthProperty, 9d); dot.SetValue(Ellipse.HeightProperty, 9d); dot.SetValue(Ellipse.MarginProperty, new Thickness(0, 0, 8, 0)); dot.SetBinding(Shape.FillProperty, new Binding(nameof(Category.Color))); row.AppendChild(dot);
        var label = new FrameworkElementFactory(typeof(TextBlock)); label.SetBinding(TextBlock.TextProperty, new Binding(nameof(Category.Name))); label.SetBinding(TextBlock.ToolTipProperty, new Binding(nameof(Category.Name))); label.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); label.SetValue(TextBlock.MaxWidthProperty, 270d); row.AppendChild(label);
        ItemTemplate = new DataTemplate { VisualTree = row };
        PreviewMouseLeftButtonDown += (_, e) => { down = e.GetPosition(this); candidate = Row(e.OriginalSource as DependencyObject)?.Content is Category c ? c.Id : null; };
        PreviewMouseMove += (_, e) =>
        {
            if (candidate is not Guid id || e.LeftButton != MouseButtonState.Pressed || !controller.Writable) return;
            var delta = e.GetPosition(this) - down; if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            candidate = null; dragging = id;
            try { DragDrop.DoDragDrop(this, new DataObject(Format, id), DragDropEffects.Move); }
            finally { dragging = null; ClearIndicator(); }
            e.Handled = true;
        };
        DragOver += (_, e) =>
        {
            e.Handled = true; e.Effects = OwnDrag(e) ? DragDropEffects.Move : DragDropEffects.None; ClearIndicator(); if (e.Effects == DragDropEffects.None) return;
            var target = TargetAt(e.GetPosition(this));
            if (target == null) return; indicator = target; bool before = e.GetPosition(target).Y < target.ActualHeight / 2;
            target.BorderBrush = ThemeColors.Brush("#A3D9C4"); target.BorderThickness = new Thickness(0, before ? 2 : 0, 0, before ? 0 : 2);
            if (e.GetPosition(this).Y < 24 && Items.Count > 0) ScrollIntoView(Items[Math.Max(0, Items.IndexOf(target.Content) - 1)]);
            else if (e.GetPosition(this).Y > ActualHeight - 24) ScrollIntoView(Items[Math.Min(Items.Count - 1, Items.IndexOf(target.Content) + 1)]);
        };
        DragLeave += (_, _) => ClearIndicator();
        Drop += async (_, e) =>
        {
            e.Handled = true; ClearIndicator(); if (!OwnDrag(e)) return;
            var target = TargetAt(e.GetPosition(this)); var item = target?.Content as Category;
            await MoveAsync((Guid)e.Data.GetData(Format), item?.Id, target != null && e.GetPosition(target).Y < target.ActualHeight / 2);
        };
    }
    ListBoxItem? Row(DependencyObject? origin) => origin == null ? null : ItemsControl.ContainerFromElement(this, origin) as ListBoxItem;
    internal ListBoxItem? TargetAt(Point point) => Enumerable.Range(0, Items.Count).Select(i => ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem)
        .Where(row => row != null && row.IsVisible && row.ActualHeight > 0).OrderBy(row => Math.Abs(row!.TranslatePoint(new Point(0, row.ActualHeight / 2), this).Y - point.Y)).FirstOrDefault();
    bool OwnDrag(DragEventArgs e)
    {
        for (var node = e.OriginalSource as DependencyObject; node != null && node != this; node = node is Visual ? VisualTreeHelper.GetParent(node) : null) if (node is ScrollBar) return false;
        return controller.Writable && dragging != null && e.Data.GetDataPresent(Format) && e.Data.GetData(Format) is Guid id && id == dragging;
    }
    void ClearIndicator() { if (indicator == null) return; indicator.ClearValue(BorderBrushProperty); indicator.ClearValue(BorderThicknessProperty); indicator = null; }
    internal async Task<bool> MoveAsync(Guid id, Guid? target, bool before)
    {
        var ids = PlannerQueries.OrderedCategories(controller.State!).Select(c => c.Id).ToList(); if (!ids.Contains(id) || target == id) return false;
        var original = ids.ToArray(); ids.Remove(id); int index = target is Guid other ? ids.IndexOf(other) : ids.Count; if (index < 0) return false;
        ids.Insert(Math.Min(ids.Count, index + (target != null && !before ? 1 : 0)), id); if (original.SequenceEqual(ids)) return true;
        IsEnabled = false;
        try { bool ok = await controller.ExecuteAsync(new ReorderCategories([.. ids])); if (ok) saved(); else error(controller.NoticeKey); return ok; }
        finally { IsEnabled = true; }
    }
    internal Task<bool> MoveSelectedAsync(int direction)
    {
        var order = PlannerQueries.OrderedCategories(controller.State!); int index = SelectedItem is Category selected ? Array.FindIndex(order.ToArray(), c => c.Id == selected.Id) : -1;
        return index < 0 || index + direction < 0 || index + direction >= order.Length ? Task.FromResult(false) : MoveAsync(order[index].Id, order[index + direction].Id, direction < 0);
    }
}
