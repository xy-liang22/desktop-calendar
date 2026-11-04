using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DesktopCalendar;
internal static class EmojiDocument
{
    internal sealed record Glyph(string Text);
    static readonly DependencyProperty ScaleProperty = DependencyProperty.RegisterAttached("RenderedScale", typeof(double), typeof(EmojiDocument), new PropertyMetadata(1d));
    internal static void Scale(FlowDocument? document, double scale)
    {
        if (document == null) return; double previous = (double)document.GetValue(ScaleProperty); if (Math.Abs(previous - scale) < .0001) return;
        double ratio = scale / previous;
        for (var p = document.ContentStart; p != null && p.CompareTo(document.ContentEnd) < 0; p = p.GetNextContextPosition(LogicalDirection.Forward))
        {
            var element = p.GetAdjacentElement(LogicalDirection.Forward);
            if (p.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.ElementStart && element is TextElement text && text.ReadLocalValue(TextElement.FontSizeProperty) is double size) text.FontSize = size * ratio;
            if (p.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.EmbeddedElement && element is Image { Tag: Glyph } image) { image.Width *= ratio; image.Height *= ratio; }
        }
        document.FontSize *= ratio; document.SetValue(ScaleProperty, scale);
    }
    internal static FlowDocument Colorize(FlowDocument document)
    {
        document.SetValue(ScaleProperty, FontPreferences.Scale);
        foreach (var run in AllRuns(document.Blocks).ToArray())
        {
            var parts = EmojiText.Parts(run.Text); if (parts.Length == 0) continue;
            var span = new Span { FontFamily = run.FontFamily, FontSize = run.FontSize, FontWeight = run.FontWeight, FontStyle = run.FontStyle, FontStretch = run.FontStretch, Foreground = run.Foreground, Background = run.Background, TextDecorations = run.TextDecorations };
            int index = 0;
            foreach (var part in parts)
            {
                if (part.Start > index) span.Inlines.Add(new Run(run.Text[index..part.Start]));
                string text = run.Text.Substring(part.Start, part.Length); double height = run.FontSize * 1.05;
                var image = new Image { Source = part.Image, Height = height, Width = height * part.Image.PixelWidth / part.Image.PixelHeight, Stretch = Stretch.Uniform, Tag = new Glyph(text) };
                System.Windows.Automation.AutomationProperties.SetName(image, text);
                span.Inlines.Add(new InlineUIContainer(image) { BaselineAlignment = BaselineAlignment.Center }); index = part.Start + part.Length;
            }
            if (index < run.Text.Length) span.Inlines.Add(new Run(run.Text[index..]));
            var inlines = run.Parent switch { Paragraph p => p.Inlines, Span s => s.Inlines, _ => null };
            if (inlines == null) continue; inlines.InsertBefore(run, span); inlines.Remove(run);
        }
        return document;
    }
    static IEnumerable<Run> AllRuns(BlockCollection blocks)
    {
        foreach (var block in blocks)
        {
            if (block is Paragraph p) foreach (var run in InlineRuns(p.Inlines)) yield return run;
            else if (block is Section s) foreach (var run in AllRuns(s.Blocks)) yield return run;
            else if (block is Table t) foreach (var group in t.RowGroups) foreach (var row in group.Rows) foreach (var cell in row.Cells) foreach (var run in AllRuns(cell.Blocks)) yield return run;
            else if (block is System.Windows.Documents.List list) foreach (var item in list.ListItems) foreach (var run in AllRuns(item.Blocks)) yield return run;
        }
    }
    static IEnumerable<Run> InlineRuns(InlineCollection inlines)
    { foreach (var inline in inlines) { if (inline is Run run) yield return run; else if (inline is Span s) foreach (var nested in InlineRuns(s.Inlines)) yield return nested; } }
    internal static string SelectionText(TextPointer start, TextPointer end, out bool containsEmoji)
    {
        var text = new StringBuilder(); containsEmoji = false;
        for (var p = start; p != null && p.CompareTo(end) < 0;)
        {
            var context = p.GetPointerContext(LogicalDirection.Forward);
            if (context == TextPointerContext.Text)
            {
                string part = p.GetTextInRun(LogicalDirection.Forward); int count = Math.Min(part.Length, p.GetOffsetToPosition(end)); text.Append(part.AsSpan(0, count)); p = p.GetPositionAtOffset(count); continue;
            }
            if (context == TextPointerContext.EmbeddedElement && p.GetAdjacentElement(LogicalDirection.Forward) is Image { Tag: Glyph glyph }) { text.Append(glyph.Text); containsEmoji = true; }
            if (context == TextPointerContext.ElementEnd && p.Parent is Paragraph) text.AppendLine();
            p = p.GetNextContextPosition(LogicalDirection.Forward);
        }
        return text.ToString();
    }
    internal static void Copy(object sender, DataObjectCopyingEventArgs e)
    {
        if (sender is not FlowDocumentScrollViewer viewer || viewer.Selection.IsEmpty) return;
        string text = SelectionText(viewer.Selection.Start, viewer.Selection.End, out bool hasEmoji);
        if (!hasEmoji) return;
        e.DataObject.SetData(DataFormats.UnicodeText, text); e.DataObject.SetData(DataFormats.Text, text);
    }
}
