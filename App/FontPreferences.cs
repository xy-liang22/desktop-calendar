using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopCalendar;
internal static class FontPreferences
{
    internal static double Scale { get; private set; } = 1;
    sealed record Original(double Size);
    static readonly ConditionalWeakTable<TextBox, Original> originals = new();
    internal static double Size(double value) => value * Scale;
    internal static void Editor(TextBox box)
    {
        if (originals.TryGetValue(box, out var original)) box.SetCurrentValue(Control.FontSizeProperty, original.Size * Scale);
        else if (DependencyPropertyHelper.GetValueSource(box, Control.FontSizeProperty).BaseValueSource is BaseValueSource.Local or BaseValueSource.Style)
        { originals.Add(box, new(box.FontSize)); box.SetCurrentValue(Control.FontSizeProperty, box.FontSize * Scale); }
    }
    internal static void Apply(double scale)
    {
        Scale = Math.Clamp(scale, .85, 1.35);
        foreach (Window window in Application.Current.Windows) { window.FontSize = SystemFonts.MessageFontSize * Scale; Visit(window); }
    }
    static void Visit(DependencyObject parent)
    {
        if (parent is EmojiTextBlock text) text.CoerceValue(TextBlock.FontSizeProperty);
        if (parent is TextBox box) Editor(box);
        if (parent is FlowDocumentScrollViewer viewer) EmojiDocument.Scale(viewer.Document, Scale);
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) Visit(VisualTreeHelper.GetChild(parent, i));
    }
    internal static object Coerce(DependencyObject owner, object value) => DependencyPropertyHelper.GetValueSource(owner, TextBlock.FontSizeProperty).BaseValueSource == BaseValueSource.Inherited ? value : (double)value * Scale;
}
