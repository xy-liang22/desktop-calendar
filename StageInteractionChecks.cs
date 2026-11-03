using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopCalendar;
internal static partial class StageThreeChecks
{
    static async Task RunP51Checks(MainWindow window)
    {
        var c = window.Session.Planner!; var todo = c.State!.Todos.First(t => t.ParentId == null && t.Kind == Core.TodoKind.Once);
        await DriveAsync(() => window.PlannerView!.Edit(todo), async s =>
        {
            var hwnd = new WindowInteropHelper(s).Handle; var original = NativeWindow.Bounds(hwnd); var work = NativeWindow.WorkArea(hwnd); var parentBounds = NativeWindow.Bounds(window.Handle);
            NativeWindow.Place(hwnd, new Rect(work.X - original.Width - 100, work.Y - original.Height - 100, original.Width, original.Height)); WindowGestures.Fit(s);
            await Idle(); Check("editor dragged off-screen can be fitted back without changing root preferences", NativeWindow.WorkArea(hwnd).Contains(NativeWindow.Bounds(hwnd).TopLeft) && !s.ShowInTaskbar && s.Owner == window && NativeWindow.Bounds(window.Handle) == parentBounds);
            var title = Children<TextBox>(s).First(t => System.Windows.Automation.AutomationProperties.GetName(t) == MainWindow.Text("PlannerTitle")); title.Text = "retain when opening image";
            var bytes = new byte[800 * 600 * 4]; for (int i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
            var bitmap = BitmapSource.Create(800, 600, 96, 96, PixelFormats.Bgra32, null, bytes, 800 * 4); bitmap.Freeze();
            _ = s.Dispatcher.BeginInvoke(new Action(() =>
            {
                var viewer = Application.Current.Windows.OfType<ImageViewer>().Single(); viewer.UpdateLayout(); viewer.SetZoom(1, new Point(40, 40)); var dpi = VisualTreeHelper.GetDpi(viewer);
                Check("image 100 percent maps bitmap pixels to physical screen pixels", Math.Abs(viewer.Picture.Width * dpi.DpiScaleX - 800) < .1);
                viewer.SetZoom(2, new Point(80, 80)); Check("large image zoom expands scrollable content", viewer.Zoom == 2 && viewer.Picture.Width > viewer.View.ViewportWidth);
                viewer.Fit(); Check("fit image preserves aspect and fits both viewport dimensions", viewer.Picture.Width <= viewer.View.ViewportWidth + 1 && viewer.Picture.Height <= viewer.View.ViewportHeight + 1);
                Image(viewer, "p51-image-viewer.png"); viewer.Close();
            }));
            long revision = c.State!.Revision; ImageViewer.Open(title, bitmap); await Idle();
            Check("image viewer does not save or close parent draft", s.IsVisible && title.Text == "retain when opening image" && c.State.Revision == revision);
            Click(s.Actions.Children.OfType<Button>().First());
        });
        var below = ActivityDetailCard.Place(new Rect(30, 30, 100, 40), new Size(240, 180), new Size(700, 600), new Vector());
        var above = ActivityDetailCard.Place(new Rect(30, 500, 100, 40), new Size(240, 180), new Size(700, 600), new Vector());
        var dragged = ActivityDetailCard.Place(new Rect(30, 500, 100, 40), new Size(240, 180), new Size(700, 600), new Vector(900, -900));
        Check("anchored card chooses below or above without covering short target", below.Top >= 70 && above.Bottom <= 500);
        Check("dragged detail card remains inside client bounds", new Rect(0, 0, 700, 600).Contains(dragged));
        var tall = new Rect(550, 120, 100, 420); Check("tall event prefers free side rather than covering its block", !ActivityDetailCard.Place(tall, new Size(240, 350), new Size(700, 600), new Vector()).IntersectsWith(tall));
    }
}
