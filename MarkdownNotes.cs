using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopCalendar.Core;

namespace DesktopCalendar;

// A deliberately local renderer: no HTML engine, script, network fetch or XAML loading.
internal static class MarkdownNotes
{
    static readonly Regex Inline = new(@"!\[([^\]]*)\]\(([^)]+)\)|\[([^\]]+)\]\(([^)]+)\)|`([^`]+)`|\*\*(.+?)\*\*|\*(.+?)\*", RegexOptions.NonBacktracking);
    internal static FlowDocument Render(string text, NoteFormat format, LocalImages images)
    {
        var doc = new FlowDocument { Foreground = ThemeColors.Brush("#EDF2F5"), Background = Brushes.Transparent, FontSize = 12, PagePadding = new Thickness(8), FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"), ColumnWidth = 10000 };
        if (text.Length > 100000) text = text[..100000] + "\n… " + MainWindow.Text("ActivityPreviewTruncated");
        if (format == NoteFormat.PlainText) { doc.Blocks.Add(new Paragraph(new Run(text))); return doc; }
        var lines = text.Replace("\r", "").Split('\n'); bool fence = false; var code = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal)) { if (fence) { doc.Blocks.Add(Code(code.ToString())); code.Clear(); } fence = !fence; continue; }
            if (fence) { code.AppendLine(line); continue; }
            if (i + 1 < lines.Length && line.Contains('|') && Regex.IsMatch(lines[i + 1], @"^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$"))
            {
                var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 8) }; var group = new TableRowGroup(); table.RowGroups.Add(group);
                void Row(string value, bool header)
                {
                    var row = new TableRow(); foreach (var cell in value.Trim().Trim('|').Split('|').Take(20))
                    { var p = new Paragraph { Margin = new Thickness(4) }; Fill(p.Inlines, cell.Trim(), images); row.Cells.Add(new TableCell(p) { BorderBrush = ThemeColors.Brush("#526474"), BorderThickness = new Thickness(.5), FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal }); } group.Rows.Add(row);
                }
                Row(line, true); i++; while (i + 1 < lines.Length && lines[i + 1].Contains('|') && lines[i + 1].Trim().Length > 0) Row(lines[++i], false); doc.Blocks.Add(table); continue;
            }
            var heading = Regex.Match(line, @"^(#{1,6})\s+(.+)$"); var bullet = Regex.Match(line, @"^\s*(?:[-+*]|\d+[.)])\s+(.+)$");
            var paragraph = new Paragraph { Margin = new Thickness(0, 2, 0, 7) };
            string content = line;
            if (heading.Success) { paragraph.FontSize = Math.Max(13, 23 - heading.Groups[1].Length * 2); paragraph.FontWeight = FontWeights.SemiBold; content = heading.Groups[2].Value; }
            else if (line.TrimStart().StartsWith('>')) { paragraph.BorderBrush = ThemeColors.Brush("#A3D9C4"); paragraph.BorderThickness = new Thickness(2, 0, 0, 0); paragraph.Padding = new Thickness(8, 0, 0, 0); content = line.TrimStart()[1..].TrimStart(); }
            else if (bullet.Success) { paragraph.Margin = new Thickness(10, 2, 0, 4); content = "• " + bullet.Groups[1].Value; }
            Fill(paragraph.Inlines, content, images); doc.Blocks.Add(paragraph);
        }
        if (code.Length > 0) doc.Blocks.Add(Code(code.ToString())); return doc;
    }
    static Paragraph Code(string text) => new(new Run(text)) { FontFamily = new FontFamily("Consolas"), Background = ThemeColors.Brush("#16212D"), Padding = new Thickness(8), FontSize = 11 };
    static void Fill(InlineCollection output, string text, LocalImages images)
    {
        int position = 0;
        foreach (Match m in Inline.Matches(text))
        {
            if (m.Index > position) output.Add(new Run(text[position..m.Index])); position = m.Index + m.Length;
            if (m.Groups[2].Success)
            {
                var bitmap = images.Read(m.Groups[2].Value);
                if (bitmap == null) output.Add(new Run("[" + MainWindow.Text("ActivityMissingImage") + ": " + m.Groups[1].Value + "]") { Foreground = ThemeColors.Brush("#E3BF89") });
                else
                {
                    var picture = new Image { Source = bitmap, Stretch = Stretch.Uniform, Cursor = Cursors.Hand, ToolTip = MainWindow.Text("ImageOpen") };
                    var frame = new ScrollViewer { Content = picture, MaxHeight = 440, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                    picture.MouseLeftButtonDown += (_, e) => { e.Handled = true; ImageViewer.Open(picture, bitmap); };
                    void FitWidth() { var viewer = Ancestor<FlowDocumentScrollViewer>(frame); if (viewer == null) return; double width = Math.Max(40, viewer.ActualWidth - 40); frame.Width = width; picture.Width = width; }
                    frame.Loaded += (_, _) => FitWidth(); frame.LayoutUpdated += (_, _) => { var viewer = Ancestor<FlowDocumentScrollViewer>(frame); if (viewer != null && Math.Abs(frame.Width - Math.Max(40, viewer.ActualWidth - 40)) > .5) FitWidth(); };
                    output.Add(new InlineUIContainer(frame));
                }
            }
            else if (m.Groups[4].Success)
            {
                string uri = m.Groups[4].Value; var link = new Hyperlink(new Run(m.Groups[3].Value)) { Foreground = ThemeColors.Brush("#79B8E9"), ToolTip = uri };
                link.Click += (_, _) => { try { Clipboard.SetText(uri); link.ToolTip = MainWindow.Text("ActivityLinkCopied"); } catch (System.Runtime.InteropServices.COMException) { } }; output.Add(link);
            }
            else if (m.Groups[5].Success) output.Add(new Run(m.Groups[5].Value) { FontFamily = new FontFamily("Consolas"), Background = ThemeColors.Brush("#16212D") });
            else if (m.Groups[6].Success) output.Add(new Bold(new Run(m.Groups[6].Value)));
            else output.Add(new Italic(new Run(m.Groups[7].Value)));
        }
        if (position < text.Length) output.Add(new Run(text[position..]));
    }
    static T? Ancestor<T>(DependencyObject source) where T : DependencyObject
    { for (DependencyObject? node = source; node != null; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node)) if (node is T found) return found; return null; }
}

internal sealed class NoteEditor : Grid
{
    internal readonly TextBox Source = new() { AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 160, MaxLength = 1000000, Padding = new Thickness(7) };
    internal readonly ComboBox Format = new() { MinHeight = 28 };
    internal readonly FlowDocumentScrollViewer Preview = new() { IsToolBarVisible = false, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    internal readonly LocalImages Images;
    readonly TextBlock notice = PlannerView.Text("", 10, "#E3BF89");
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(220) };
    internal NoteEditor(PlannerController controller, Note note, PlannerDialogs.Sheet sheet)
    {
        Images = new(controller.DataDirectory, () => controller.State);
        RowDefinitions.Add(new() { Height = GridLength.Auto }); RowDefinitions.Add(new()); RowDefinitions.Add(new() { Height = GridLength.Auto });
        ColumnDefinitions.Add(new()); ColumnDefinitions.Add(new()); Height = 320;
        Format.ItemsSource = new[] { MainWindow.Text("ActivityPlain"), MainWindow.Text("ActivityMarkdown") }; Format.SelectedIndex = (int)note.Format;
        var toolbar = new DockPanel(); var add = PlannerView.Button(MainWindow.Text("ActivityImage"), () =>
        {
            using var hold = sheet.HoldOpen(); var picker = new Microsoft.Win32.OpenFileDialog { Filter = MainWindow.Text("ActivityImageFilter"), Multiselect = false };
            if (picker.ShowDialog(sheet) == true) ImportFile(picker.FileName);
        }); DockPanel.SetDock(add, Dock.Right); toolbar.Children.Add(add); toolbar.Children.Add(Format); SetColumnSpan(toolbar, 2); Children.Add(toolbar);
        Source.Text = note.Text; SetRow(Source, 1); Source.Margin = new Thickness(0, 5, 5, 0); Children.Add(Source); SetRow(Preview, 1); SetColumn(Preview, 1); Children.Add(Preview);
        notice.Text = MainWindow.Text("ActivityImageHint"); notice.TextWrapping = TextWrapping.Wrap; SetRow(notice, 2); SetColumnSpan(notice, 2); Children.Add(notice);
        timer.Tick += (_, _) => { timer.Stop(); Refresh(); }; Source.TextChanged += (_, _) => { timer.Stop(); timer.Start(); }; Format.SelectionChanged += (_, _) => Refresh(); Unloaded += (_, _) => timer.Stop();
        Source.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.V || (Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
            try { if (Clipboard.ContainsImage() && Clipboard.GetImage() is { } bitmap) { e.Handled = true; Insert(Images.Import(bitmap)); } } catch (Exception ex) when (ImageFailure(ex)) { e.Handled = true; notice.Text = MainWindow.Text("ActivityImageError"); }
        };
        Source.AllowDrop = true;
        Source.PreviewDragOver += (_, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) { e.Effects = DragDropEffects.Copy; e.Handled = true; } };
        Source.PreviewDrop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] files) { e.Handled = true; foreach (var file in files.Take(10)) ImportFile(file); } };
        Refresh();
    }
    internal static bool ImageFailure(Exception ex) => ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Runtime.InteropServices.COMException;
    internal void ImportFile(string file) { try { Insert(Images.ImportFile(file)); } catch (Exception ex) when (ImageFailure(ex)) { notice.Text = MainWindow.Text("ActivityImageError"); } }
    internal void Insert(LocalAsset asset) { Format.SelectedIndex = (int)NoteFormat.Markdown; Source.SelectedText = "![image](" + asset.RelativePath + ")"; Source.CaretIndex = Source.SelectionStart + Source.SelectionLength; notice.Text = MainWindow.Text("ActivityImageHint"); Refresh(); }
    internal Note Value => new() { Format = (NoteFormat)Format.SelectedIndex, Text = Source.Text, AssetIds = [.. Images.Referenced(Source.Text)] };
    internal void Refresh() => Preview.Document = MarkdownNotes.Render(Source.Text, (NoteFormat)Format.SelectedIndex, Images);
}
