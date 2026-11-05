using TextBlock = DesktopCalendar.EmojiTextBlock;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using DesktopCalendar.Core;

namespace DesktopCalendar;
internal static class TodoDraftRules
{
    internal static bool Date(DatePicker picker, bool required, out DateOnly? value)
    {
        value = null; var input = picker.Template?.FindName("PART_TextBox", picker) as System.Windows.Controls.Primitives.DatePickerTextBox;
        string raw = (input?.Text ?? picker.Text).Trim(); if (raw.Length == 0) return !required;
        if (!DateTime.TryParse(raw, CultureInfo.GetCultureInfo(MainWindow.Text("PlannerUiLanguage")), DateTimeStyles.AllowWhiteSpaces, out var date)) return false;
        picker.SelectedDate = date.Date; value = DateOnly.FromDateTime(date); return true;
    }
    internal static bool Title(string text) => !string.IsNullOrWhiteSpace(text) && text.Trim().Length <= 10000;
    internal static string TimePunctuation(string text)
    {
        // Only the leading planned-time token belongs to us; punctuation in titles is user content.
        var match = Regex.Match(text, @"^\s*[0-9０-９]{1,2}\s*[:：﹕]");
        return match.Success ? text[..match.Length].Replace('：', ':').Replace('﹕', ':') + text[match.Length..] : text;
    }
    internal static void BindTimePunctuation(TextBox field, ImeInput ime)
    {
        bool queued = false;
        void Queue()
        {
            if (queued) return; queued = true;
            field.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                queued = false; if (ime.Composing) return;
                string normalized = TimePunctuation(field.Text); if (normalized == field.Text) return;
                int start = field.SelectionStart, length = field.SelectionLength;
                field.Text = normalized; field.Select(start, length);
            }));
        }
        field.TextChanged += (_, _) => Queue(); ime.Completed += Queue;
    }
    internal static bool Color(string? text) => text == null || Regex.IsMatch(text, "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$");
    internal static bool Time(string text, out TimeOnly? value)
    {
        value = null; if (string.IsNullOrWhiteSpace(text)) return true;
        string normalized = Regex.Replace(text.Normalize(NormalizationForm.FormKC).Replace('﹕', ':'), @"\s+", "");
        if (!TimeOnly.TryParseExact(normalized, ["H:m", "H:mm", "HH:m", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)) return false;
        value = result; return true;
    }
    internal static bool Quick(string text, out string title, out TimeOnly? time)
    {
        title = text.Trim(); time = null;
        var candidate = Regex.Match(title, @"^([0-9０-９]+\s*[:：﹕])");
        if (candidate.Success)
        {
            var match = Regex.Match(title, @"^([0-9０-９]{1,2}\s*[:：﹕]\s*[0-9０-９]{1,2})([^0-9０-９].*|$)");
            if (!match.Success || !Time(match.Groups[1].Value, out time)) return false;
            title = match.Groups[2].Value.Trim();
        }
        return Title(title);
    }
}
internal sealed record CategoryOption(Guid? Id, string Name, string Color)
{
    public Brush Swatch => ThemeColors.Brush(ThemeColors.Accent(Color));
    public override string ToString() => Name;
}
// The same category/color fields are used by quick creation and the full editor.
internal sealed class TodoAppearance : Grid
{
    readonly PlannerController controller;
    readonly bool keepHistorical;
    readonly ChoicePicker category = new() { SelectedValuePath = nameof(CategoryOption.Id), Margin = new Thickness(0, 0, 5, 0) };
    internal ColorField Color { get; }
    bool loading;
    internal Guid? CategoryId => (category.SelectedItem as CategoryOption)?.Id;
    internal event Action? Changed;
    internal TodoAppearance(PlannerController controller, Guid? id, string? color, bool compact = false)
    {
        this.controller = controller; keepHistorical = !compact;
        ColumnDefinitions.Add(new()); ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        Color = new ColorField(color, true, compact); SetColumn(Color, 1); Children.Add(category); Children.Add(Color);
        var row = new FrameworkElementFactory(typeof(StackPanel)); row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var dot = new FrameworkElementFactory(typeof(Ellipse)); dot.SetValue(Ellipse.WidthProperty, 8d); dot.SetValue(Ellipse.HeightProperty, 8d); dot.SetValue(Ellipse.MarginProperty, new Thickness(0, 0, 6, 0)); dot.SetBinding(Shape.FillProperty, new Binding(nameof(CategoryOption.Swatch))); row.AppendChild(dot);
        var label = new FrameworkElementFactory(typeof(TextBlock)); label.SetBinding(TextBlock.TextProperty, new Binding(nameof(CategoryOption.Name))); label.SetValue(TextBlock.ForegroundProperty, ThemeColors.Brush("#EDF2F5")); label.SetValue(TextBlock.MaxWidthProperty, 160d); label.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); label.SetBinding(TextBlock.ToolTipProperty, new Binding(nameof(CategoryOption.Name))); row.AppendChild(label); category.ItemTemplate = new DataTemplate { VisualTree = row };
        category.SizeChanged += (_, _) => category.PopupWidth = Math.Clamp(category.ActualWidth, 260, 400);
        AutomationProperties.SetName(category, MainWindow.Text("PlannerCategory")); Refresh(id);
        category.SelectionChanged += (_, _) => { UpdateDefault(); if (!loading) { Color.Value = null; Changed?.Invoke(); } };
        Color.Changed += () => { if (!loading) Changed?.Invoke(); };
    }
    void UpdateDefault() => Color.DefaultColor = (category.SelectedItem as CategoryOption)?.Color ?? "#A3D9C4";
    internal void Refresh(Guid? selected = null)
    {
        var wanted = selected ?? CategoryId; var raw = Color.Value; loading = true;
        category.ItemsSource = new[] { new CategoryOption(null, MainWindow.Text("PlannerUnclassified"), "#A3D9C4") }.Concat((controller.State == null ? [] : PlannerQueries.OrderedCategories(controller.State, true)).Where(c => !c.Archived && !PlannerDefaults.LegacyPriority(c) || keepHistorical && c.Id == wanted).Select(c => new CategoryOption(c.Id, Localization.Category(c) + (PlannerDefaults.LegacyPriority(c) ? " · " + MainWindow.Text("LegacyCategory") : ""), c.Color))).ToArray();
        category.SelectedValue = wanted; if (category.SelectedIndex < 0) category.SelectedIndex = 0;
        UpdateDefault(); Color.Value = raw; loading = false;
    }
}
