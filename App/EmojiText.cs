using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DesktopCalendar;
internal sealed record EmojiPart(int Start, int Length, BitmapSource Image);
internal static class EmojiText
{
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBox, EmojiTextBoxSurface> editors = new();
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FlowDocumentScrollViewer, object> documents = new();
    static bool initialized;
    internal static void Initialize()
    {
        if (initialized) return; initialized = true;
        EventManager.RegisterClassHandler(typeof(TextBox), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, e) =>
        { if (sender is TextBox box && ReferenceEquals(e.OriginalSource, box)) editors.GetValue(box, b => new EmojiTextBoxSurface(b)).Refresh(); }), true);
        EventManager.RegisterClassHandler(typeof(FlowDocumentScrollViewer), FrameworkElement.LoadedEvent, new RoutedEventHandler((sender, e) =>
        { if (sender is FlowDocumentScrollViewer viewer && ReferenceEquals(e.OriginalSource, viewer)) documents.GetValue(viewer, v => { DataObject.AddCopyingHandler(v, EmojiDocument.Copy); return new(); }); }), true);
    }
    internal static EmojiPart[] Parts(string text)
    {
        var parts = new List<EmojiPart>(); var enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            int at = enumerator.ElementIndex; char first = text[at];
            if (!char.IsHighSurrogate(first) && first is not (>= '\u2600' and <= '\u27BF') && !(at + 1 < text.Length && text[at + 1] == '\uFE0F')) continue;
            string element = enumerator.GetTextElement();
            if (element.Contains('\uFE0E') || !element.EnumerateRunes().Any(r => r.Value is >= 0x1F000 and <= 0x1FAFF or >= 0x2600 and <= 0x27BF || r.Value == 0xFE0F)) continue;
            var image = ColorEmojiRaster.Get(element); if (image != null) parts.Add(new(enumerator.ElementIndex, element.Length, image));
        }
        return parts.ToArray();
    }
    internal static void Draw(DrawingContext dc, EmojiPart part, Rect start, Rect end, double fontSize)
    {
        if (start.IsEmpty || end.IsEmpty || Math.Abs(start.Top - end.Top) > Math.Max(start.Height, end.Height) * .5) return;
        double width = Math.Max(start.Width, end.Right - start.Left); if (width < .5) width = fontSize;
        double maxHeight = Math.Min(start.Height, fontSize * 1.12); if (maxHeight <= 0) return;
        double scale = Math.Min(width / part.Image.PixelWidth, maxHeight / part.Image.PixelHeight);
        double w = part.Image.PixelWidth * scale, h = part.Image.PixelHeight * scale;
        dc.DrawImage(part.Image, new Rect(start.Left + (width - w) / 2, start.Top + (start.Height - h) / 2, w, h));
    }
}
// Preserve native text, layout/trimming, bindings, accessibility and hit-testing.
// TextEffects suppress only supported monochrome emoji glyphs; the same glyph positions receive color pixels.
public class EmojiTextBlock : TextBlock
{
    readonly DrawingVisual overlay = new();
    bool queued;
    static EmojiTextBlock()
    {
        TextProperty.OverrideMetadata(typeof(EmojiTextBlock), new FrameworkPropertyMetadata("", (d, _) => ((EmojiTextBlock)d).Queue()));
        FontSizeProperty.OverrideMetadata(typeof(EmojiTextBlock), new FrameworkPropertyMetadata(SystemFonts.MessageFontSize, FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => ((EmojiTextBlock)d).Queue(), FontPreferences.Coerce));
    }
    public EmojiTextBlock()
    {
        AddVisualChild(overlay); Loaded += (_, _) => { CoerceValue(FontSizeProperty); Queue(); }; SizeChanged += (_, _) => Queue();
    }
    protected override int VisualChildrenCount => base.VisualChildrenCount + 1;
    protected override Visual GetVisualChild(int index) => index == base.VisualChildrenCount ? overlay : base.GetVisualChild(index);
    void Queue()
    {
        if (queued || !IsLoaded) return; queued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            queued = false; Prepare();
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(Draw));
        }));
    }
    string? snapshot;
    Run[] runs = [];
    readonly List<(Run Run, EmojiPart Part)> glyphs = [];
    static IEnumerable<Run> Runs(InlineCollection inlines)
    { foreach (var inline in inlines) { if (inline is Run run) yield return run; else if (inline is Span span) foreach (var child in Runs(span.Inlines)) yield return child; } }
    void Prepare()
    {
        if (glyphs.Count == 0 && !Text.EnumerateRunes().Any(r => r.Value is >= 0x1F000 and <= 0x1FAFF or >= 0x2600 and <= 0x27BF || r.Value == 0xFE0F)) return;
        var current = Runs(Inlines).ToArray(); string signature = string.Join("\0", current.Select(r => r.Text));
        if (snapshot != signature || !current.SequenceEqual(runs))
        {
            snapshot = signature; foreach (var old in runs) old.ClearValue(TextElement.TextEffectsProperty); runs = current; glyphs.Clear();
            foreach (var run in runs)
            {
                var effects = new TextEffectCollection();
                foreach (var part in EmojiText.Parts(run.Text))
                {
                    int offset = ContentStart.GetOffsetToPosition(run.ContentStart) + part.Start;
                    effects.Add(new TextEffect { Foreground = Brushes.Transparent, PositionStart = offset, PositionCount = part.Length }); glyphs.Add((run, part));
                }
                if (effects.Count > 0) run.TextEffects = effects;
            }
        }
    }
    void Draw()
    {
        using var dc = overlay.RenderOpen(); if (glyphs.Count == 0) return;
        dc.PushClip(new RectangleGeometry(new Rect(RenderSize)));
        foreach (var pair in glyphs)
        {
            var start = pair.Run.ContentStart.GetPositionAtOffset(pair.Part.Start); var end = start?.GetPositionAtOffset(pair.Part.Length);
            if (start != null && end != null && start.HasValidLayout && end.HasValidLayout)
                EmojiText.Draw(dc, pair.Part, start.GetCharacterRect(LogicalDirection.Forward), end.GetCharacterRect(LogicalDirection.Backward), pair.Run.FontSize);
        }
        dc.Pop();
    }
}
internal sealed class EmojiTextBoxSurface
{
    EmojiPart[] glyphs = [];
    readonly ImeInput ime;
    GlyphAdorner? overlay;
    AdornerLayer? overlayLayer;
    bool queued;
    readonly TextBox box;
    internal EmojiTextBoxSurface(TextBox box)
    {
        this.box = box;
        ime = new ImeInput(box); ime.Completed += Queue;
        box.TextChanged += (_, _) => Queue(); box.Loaded += (_, _) => Queue();
        box.SizeChanged += (_, _) => Queue();
        box.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => Queue()));
        box.Unloaded += (_, _) => { if (overlay != null) overlayLayer?.Remove(overlay); overlay = null; overlayLayer = null; };
    }
    internal void Refresh() { FontPreferences.Editor(box); Queue(); }
    void Queue()
    {
        if (queued) return; queued = true;
        box.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            queued = false;
            if (ime.Composing) { glyphs = []; box.ClearValue(TextElement.TextEffectsProperty); overlay?.InvalidateVisual(); return; }
            // Long Markdown sources keep native text editing; rasterize only the visible range plus cluster context.
            int a = box.GetCharacterIndexFromPoint(new Point(2, 2), true), b = box.GetCharacterIndexFromPoint(new Point(Math.Max(2, box.ActualWidth - 2), Math.Max(2, box.ActualHeight - 2)), true);
            int first = a < 0 || b < 0 ? 0 : Math.Max(0, Math.Min(a, b) - 64), last = a < 0 || b < 0 ? Math.Min(box.Text.Length, 10000) : Math.Min(box.Text.Length, Math.Max(a, b) + 64);
            if (first > 0 && first < box.Text.Length && char.IsLowSurrogate(box.Text[first])) first--;
            var next = EmojiText.Parts(box.Text[first..last]).Select(p => p with { Start = p.Start + first }).ToArray();
            bool unchanged = next.SequenceEqual(glyphs); glyphs = next; var layer = AdornerLayer.GetAdornerLayer(box);
            if (layer == null) { box.ClearValue(TextElement.TextEffectsProperty); return; }
            if (unchanged && overlay != null) { overlay.InvalidateVisual(); return; }
            if (overlay == null && glyphs.Length > 0) { overlay = new GlyphAdorner(box, this) { IsHitTestVisible = false }; overlayLayer = layer; layer.Add(overlay); }
            var effects = new TextEffectCollection(); foreach (var part in glyphs) effects.Add(new TextEffect { Foreground = Brushes.Transparent, PositionStart = part.Start, PositionCount = part.Length });
            box.SetValue(TextElement.TextEffectsProperty, effects); overlay?.InvalidateVisual();
        }));
    }
    sealed class GlyphAdorner(TextBox box, EmojiTextBoxSurface surface) : Adorner(box)
    {
        protected override void OnRender(DrawingContext dc)
        {
            IsHitTestVisible = false;
            dc.PushClip(new RectangleGeometry(new Rect(box.BorderThickness.Left + 1, box.BorderThickness.Top + 1, Math.Max(0, box.ActualWidth - box.BorderThickness.Left - box.BorderThickness.Right - 2), Math.Max(0, box.ActualHeight - box.BorderThickness.Top - box.BorderThickness.Bottom - 2))));
            foreach (var part in surface.glyphs)
                EmojiText.Draw(dc, part, box.GetRectFromCharacterIndex(part.Start), box.GetRectFromCharacterIndex(part.Start + part.Length - 1, true), box.FontSize);
            dc.Pop();
        }
    }
}
