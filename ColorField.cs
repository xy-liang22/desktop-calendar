using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
namespace DesktopCalendar;

internal static class ThemeColors
{
    internal static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    internal static SolidColorBrush Brush(string hex) => new(Parse(hex));
    internal static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    static double L(Color c) { double F(byte b) { double x = b / 255d; return x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4); } return .2126 * F(c.R) + .7152 * F(c.G) + .0722 * F(c.B); }
    internal static double Contrast(string a, string b) { double x = L(Parse(a)), y = L(Parse(b)); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05); }
    internal static string Foreground(string background) => Contrast(background, "#EDF2F5") >= Contrast(background, "#101820") ? "#EDF2F5" : "#101820";
    internal static string Soft(string hex) { var c = Parse(hex); var b = Parse("#19232E"); return Hex(Color.FromRgb((byte)(b.R * .78 + c.R * .22), (byte)(b.G * .78 + c.G * .22), (byte)(b.B * .78 + c.B * .22))); }
    internal static string Accent(string hex)
    {
        var c = Parse(hex);
        for (int i = 0; i < 20 && Contrast(Hex(c), "#19232E") < 4.5; i++) c = Color.FromRgb((byte)Math.Min(255, c.R + (255 - c.R) * .12 + 1), (byte)Math.Min(255, c.G + (255 - c.G) * .12 + 1), (byte)Math.Min(255, c.B + (255 - c.B) * .12 + 1));
        return Hex(c);
    }
    internal static Color Hsv(double hue, double s, double v)
    {
        double h = ((hue % 360) + 360) % 360 / 60, c = v * s, x = c * (1 - Math.Abs(h % 2 - 1)), m = v - c;
        (double r, double g, double b) = h switch { < 1 => (c, x, 0d), < 2 => (x, c, 0d), < 3 => (0d, c, x), < 4 => (0d, x, c), < 5 => (x, 0d, c), _ => (c, 0d, x) };
        return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
    }
    internal static (double H, double S, double V) ToHsv(Color color)
    {
        double r = color.R / 255d, g = color.G / 255d, b = color.B / 255d, max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        double h = d == 0 ? 0 : max == r ? 60 * (((g - b) / d + 6) % 6) : max == g ? 60 * ((b - r) / d + 2) : 60 * ((r - g) / d + 4);
        return (h, max == 0 ? 0 : d / max, max);
    }
}
internal sealed class HsvPlane : FrameworkElement
{
    internal double Hue, Saturation = .5, Value = .8;
    internal event Action? Changed;
    internal HsvPlane() { Width = 224; Height = 118; Focusable = true; Cursor = Cursors.Cross; AutomationProperties.SetName(this, MainWindow.Text("ColorPlane")); }
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(new SolidColorBrush(ThemeColors.Hsv(Hue, 1, 1)), null, new Rect(RenderSize));
        dc.DrawRectangle(new LinearGradientBrush(Colors.White, Colors.Transparent, 0), null, new Rect(RenderSize));
        dc.DrawRectangle(new LinearGradientBrush(Colors.Transparent, Colors.Black, 90), null, new Rect(RenderSize));
        dc.DrawEllipse(null, new Pen(Brushes.Black, 3), new Point(Saturation * ActualWidth, (1 - Value) * ActualHeight), 4, 4);
        dc.DrawEllipse(null, new Pen(Brushes.White, 1), new Point(Saturation * ActualWidth, (1 - Value) * ActualHeight), 4, 4);
    }
    void Set(Point p) { Saturation = Math.Clamp(p.X / Math.Max(1, ActualWidth), 0, 1); Value = 1 - Math.Clamp(p.Y / Math.Max(1, ActualHeight), 0, 1); InvalidateVisual(); Changed?.Invoke(); }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { Focus(); CaptureMouse(); Set(e.GetPosition(this)); e.Handled = true; }
    protected override void OnMouseMove(MouseEventArgs e) { if (IsMouseCaptured) Set(e.GetPosition(this)); }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { if (IsMouseCaptured) { Set(e.GetPosition(this)); ReleaseMouseCapture(); e.Handled = true; } }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right) Saturation = Math.Clamp(Saturation + (e.Key == Key.Right ? .01 : -.01), 0, 1);
        else if (e.Key is Key.Up or Key.Down) Value = Math.Clamp(Value + (e.Key == Key.Up ? .01 : -.01), 0, 1); else return;
        InvalidateVisual(); Changed?.Invoke(); e.Handled = true;
    }
}
internal sealed class ColorField : StackPanel
{
    internal readonly TextBox HexInput = new() { MinHeight = 26, Padding = new Thickness(4) };
    internal readonly HsvPlane Plane = new();
    internal readonly Slider Hue = new() { Minimum = 0, Maximum = 359, Height = 24 };
    internal Popup Popup { get; }
    readonly Button button;
    readonly TextBlock warning;
    readonly bool compact;
    readonly System.Collections.Generic.List<Button> chips = [];
    bool updating;
    string defaultColor = "#A3D9C4";
    internal event Action? Changed;
    internal string DefaultColor { get => defaultColor; set { defaultColor = value; UpdateVisual(); } }
    internal string? Value { get => string.IsNullOrWhiteSpace(HexInput.Text) ? null : HexInput.Text.Trim().ToUpperInvariant(); set { HexInput.Text = value ?? ""; UpdateVisual(); } }
    internal bool Valid => TodoDraftRules.Color(Value);
    internal ColorField(string? value, bool inherit = true, bool compact = false)
    {
        this.compact = compact;
        button = PlannerView.Button("", () => Popup!.IsOpen = !Popup.IsOpen, MainWindow.Text("ColorChoose")); button.MinWidth = compact ? 30 : 100;
        button.Style = (Style)Application.Current.FindResource("ColorSwatch"); button.BorderBrush = ThemeColors.Brush("#526474"); button.BorderThickness = new Thickness(1);
        var body = new StackPanel { Width = 224 }; body.Children.Add(Plane);
        var hueBrush = new LinearGradientBrush(); foreach (var stop in Enumerable.Range(0, 7)) hueBrush.GradientStops.Add(new(ThemeColors.Hsv(stop * 60, 1, 1), stop / 6d)); Hue.Background = hueBrush; body.Children.Add(Hue); AutomationProperties.SetName(Hue, MainWindow.Text("ColorHue"));
        var swatches = new WrapPanel();
        foreach (string color in new[] { "#C66868", "#D08B5C", "#C4A34F", "#9CAE52", "#66AF78", "#4DAC94", "#50ADB3", "#559DC6", "#6B8BD2", "#8B7DCE", "#AA78C4", "#C179AA", "#E4A9A3", "#E3BF89", "#DDD0A0", "#B7C2AE", "#A3D9C4", "#AFC5D8", "#B6B5E6", "#C5B2CC", "#C7CCD1", "#EDF2F5", "#637584", "#283C4C" })
        { var chip = PlannerView.Button("", () => Value = color, color); chip.Style = (Style)Application.Current.FindResource("ColorSwatch"); chip.Tag = color; chip.Width = 27; chip.Height = 23; chip.Padding = new Thickness(0); chip.Background = ThemeColors.Brush(color); chip.Foreground = ThemeColors.Brush(ThemeColors.Foreground(color)); chip.BorderBrush = chip.Foreground; chip.BorderThickness = new Thickness(0); swatches.Children.Add(chip); chips.Add(chip); }
        body.Children.Add(swatches); var advanced = new Expander { Header = MainWindow.Text("ColorHex"), Content = HexInput, Margin = new Thickness(0, 5, 0, 4) }; body.Children.Add(advanced); AutomationProperties.SetName(HexInput, MainWindow.Text("PlannerColor"));
        warning = PlannerView.Text("", 10, "#E3BF89"); warning.TextWrapping = TextWrapping.Wrap; body.Children.Add(warning);
        if (inherit) body.Children.Add(PlannerView.Button(MainWindow.Text("ColorInherit"), () => Value = null));
        body.Children.Add(PlannerView.Button(MainWindow.Text("ColorDone"), () => Popup!.IsOpen = false));
        var surface = new Border { Child = body, Background = ThemeColors.Brush("#202E3B"), BorderBrush = ThemeColors.Brush("#526474"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(10) };
        Popup = new Popup { Child = surface, PlacementTarget = button, Placement = PlacementMode.Bottom, AllowsTransparency = true, StaysOpen = false };
        Unloaded += (_, _) => Popup.IsOpen = false;
        surface.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Popup.IsOpen = false; e.Handled = true; } };
        Children.Add(button); HexInput.Text = value ?? "";
        HexInput.TextChanged += (_, _) => { UpdateVisual(); Changed?.Invoke(); };
        Plane.Changed += () => { if (!updating) Value = ThemeColors.Hex(ThemeColors.Hsv(Plane.Hue, Plane.Saturation, Plane.Value)); };
        Hue.ValueChanged += (_, _) => { if (!updating) { Plane.Hue = Hue.Value; Plane.InvalidateVisual(); Value = ThemeColors.Hex(ThemeColors.Hsv(Plane.Hue, Plane.Saturation, Plane.Value)); } };
        UpdateVisual();
    }
    void UpdateVisual()
    {
        if (button == null || warning == null) return;
        string? value = Value; bool valid = TodoDraftRules.Color(value); string effective = valid ? value ?? defaultColor : defaultColor;
        button.Background = ThemeColors.Brush(ThemeColors.Hex(ThemeColors.Parse(effective))); button.Foreground = ThemeColors.Brush(ThemeColors.Foreground(effective));
        // The app's implicit TextBlock style otherwise overrides inherited button foreground.
        button.Content = new TextBlock { Text = compact ? "●" : value ?? MainWindow.Text("ColorInherit"), Foreground = button.Foreground, VerticalAlignment = VerticalAlignment.Center };
        warning.Text = !valid ? MainWindow.Text("PlannerColorInvalid") : ThemeColors.Contrast(effective, "#19232E") < 4.5 ? MainWindow.Text("ColorContrastHint") : "";
        foreach (var chip in chips) { bool selected = value == (string)chip.Tag; chip.Content = new TextBlock { Text = selected ? "✓" : "", Foreground = chip.Foreground }; chip.BorderThickness = new Thickness(selected ? 2 : 0); }
        if (valid && !updating)
        { updating = true; var hsv = ThemeColors.ToHsv(ThemeColors.Parse(effective)); Plane.Hue = hsv.H; Plane.Saturation = hsv.S; Plane.Value = hsv.V; Hue.Value = hsv.H; Plane.InvalidateVisual(); updating = false; }
    }
}
