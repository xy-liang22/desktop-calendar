using TextBlock = DesktopCalendar.EmojiTextBlock;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopCalendar;
internal sealed class ImageViewer : Window
{
    internal readonly ScrollViewer View = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = ThemeColors.Brush("#111922") };
    internal readonly Image Picture;
    internal double Zoom { get; private set; } = 1;
    readonly BitmapSource bitmap;
    readonly TextBlock label = PlannerView.Text("", 11);
    Point? pan;
    Point origin;
    internal ImageViewer(BitmapSource source, Window parent)
    {
        bitmap = source; Owner = parent; Topmost = parent.Topmost; ShowInTaskbar = false; WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Width = Math.Min(960, Math.Max(320, parent.ActualWidth + 80)); Height = Math.Min(720, Math.Max(300, parent.ActualHeight)); MinWidth = 300; MinHeight = 220; Title = MainWindow.Text("ImageViewerTitle"); WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel(); var header = new DockPanel { Background = Brushes.Transparent, Margin = new Thickness(0, 0, 0, 10) }; WindowGestures.Header(this, header);
        var close = PlannerView.Button("×", Close); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        header.Children.Add(PlannerView.Button(MainWindow.Text("ImageFit"), Fit)); header.Children.Add(PlannerView.Button("100%", () => SetZoom(1, new Point(View.ViewportWidth / 2, View.ViewportHeight / 2)))); header.Children.Add(label);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header); root.Children.Add(View);
        Picture = new Image { Source = source, Stretch = Stretch.Fill, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top }; RenderOptions.SetBitmapScalingMode(Picture, BitmapScalingMode.HighQuality); View.Content = Picture;
        Content = new Border { Background = ThemeColors.Brush("#F5202E3B"), BorderBrush = ThemeColors.Brush("#526474"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(12), Child = root };
        Loaded += (_, _) => { WindowGestures.Fit(this); Fit(); }; PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
        View.PreviewMouseWheel += (_, e) => { SetZoom(Zoom * Math.Pow(1.15, e.Delta / 120.0), e.GetPosition(View)); e.Handled = true; };
        View.PreviewMouseLeftButtonDown += (_, e) => { pan = e.GetPosition(View); origin = new Point(View.HorizontalOffset, View.VerticalOffset); View.CaptureMouse(); e.Handled = true; };
        View.PreviewMouseMove += (_, e) => { if (pan is Point start && e.LeftButton == MouseButtonState.Pressed) { var p = e.GetPosition(View); View.ScrollToHorizontalOffset(origin.X + start.X - p.X); View.ScrollToVerticalOffset(origin.Y + start.Y - p.Y); } };
        View.PreviewMouseLeftButtonUp += (_, e) => { pan = null; View.ReleaseMouseCapture(); e.Handled = true; }; View.LostMouseCapture += (_, _) => pan = null;
    }
    internal void Fit()
    {
        View.UpdateLayout(); var dpi = VisualTreeHelper.GetDpi(this); SetZoom(Math.Min(View.ViewportWidth * dpi.DpiScaleX / bitmap.PixelWidth, View.ViewportHeight * dpi.DpiScaleY / bitmap.PixelHeight), new Point()); View.ScrollToHome();
    }
    internal void SetZoom(double value, Point pointer)
    {
        double next = Math.Clamp(double.IsFinite(value) ? value : 1, .02, 16); var dpi = VisualTreeHelper.GetDpi(this);
        double x = (View.HorizontalOffset + pointer.X) / Zoom, y = (View.VerticalOffset + pointer.Y) / Zoom;
        Zoom = next; Picture.Width = bitmap.PixelWidth * Zoom / dpi.DpiScaleX; Picture.Height = bitmap.PixelHeight * Zoom / dpi.DpiScaleY;
        View.UpdateLayout(); View.ScrollToHorizontalOffset(Math.Max(0, x * Zoom - pointer.X)); View.ScrollToVerticalOffset(Math.Max(0, y * Zoom - pointer.Y)); label.Text = "  " + (Zoom * 100).ToString("0") + "%";
    }
    internal static void Open(FrameworkElement origin, BitmapSource bitmap)
    {
        if (Window.GetWindow(origin) is not Window parent) return;
        var positions = Scrolls(parent).Select(s => (View: s, X: s.HorizontalOffset, Y: s.VerticalOffset)).ToArray();
        using var hold = (parent as PlannerDialogs.Sheet)?.HoldOpen();
        var main = parent as MainWindow; if (main != null) main.PlannerDialogDepth++;
        try { new ImageViewer(bitmap, parent).ShowDialog(); }
        finally
        {
            if (main != null) main.PlannerDialogDepth--;
            if (parent.IsVisible) parent.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => { foreach (var p in positions) if (p.View.IsLoaded) { p.View.ScrollToHorizontalOffset(p.X); p.View.ScrollToVerticalOffset(p.Y); } }));
        }
    }
    static System.Collections.Generic.IEnumerable<ScrollViewer> Scrolls(DependencyObject node)
    { if (node is ScrollViewer scroll) yield return scroll; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) foreach (var child in Scrolls(VisualTreeHelper.GetChild(node, i))) yield return child; }
}
