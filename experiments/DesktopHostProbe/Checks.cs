using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopHostProbe;

internal static class Checks
{
    internal static int Run(Application app)
    {
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        var lines=new List<string>(); int pass=0,fail=0;
        string dir=Path.Combine(Environment.CurrentDirectory,"artifacts"); Directory.CreateDirectory(dir);
        void Assert(bool ok,string name) { lines.Add((ok?"PASS ":"FAIL ")+name); if(ok)pass++;else fail++; }
        async Task Rendered(ProbeWindow w) {
            for(int i=0;i<60;i++) { if(w.Rendered)return; await Task.Delay(100); }
            throw new TimeoutException("WPF ContentRendered was not observed");
        }
        var normal=new ProbeWindow(lines.Add);
        normal.Loaded+=async (_,_)=> {
            ProbeWindow? experimental=null;
            try
            {
                await Rendered(normal);
                var s=normal.State();
                Assert(s.Pid==Environment.ProcessId && s.Hwnd!="0" && Native.IsWindow(normal.Handle),"own process has real HWND");
                Assert(s.Mode=="normal" && s.Parent=="0" && s.Host=="0","startup stays ordinary top-level, no Explorer parent");
                Assert(s.Loaded && s.ContentRendered && s.ContentWidth>200 && s.ContentHeight>200,"WPF loaded, content rendered and measured");
                Assert(normal.ShowInTaskbar && !normal.AllowsTransparency && normal.WindowStyle==WindowStyle.SingleBorderWindow,"ordinary opaque captioned diagnostic window with taskbar entry");
                Assert(normal.ResizeMode==ResizeMode.CanResizeWithGrip,"standard resize border and grip available");
                Assert(s.NativeVisible && s.WpfVisible && !s.Minimized && s.Cloaked=="0","visibility flags + non-cloaked (not a physical screen assertion)");
                Assert(!normal.HumanConfirmed && !normal.Confirm.IsEnabled,"no fabricated human confirmation");
                Assert(!normal.Switch("desktop") && normal.Mode=="normal" && Native.GetParent(normal.Handle)==0,"desktop blocked until user confirms interaction");
                Assert(!normal.Switch("bottom") && Native.GetParent(normal.Handle)==0,"potentially hiding bottom mode also gated");

                var content=(FrameworkElement)normal.Content;
                var bitmap=new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),96,96,PixelFormats.Pbgra32);
                bitmap.Render(content);
                int stride=bitmap.PixelWidth*4; byte[] pixels=new byte[stride*bitmap.PixelHeight]; bitmap.CopyPixels(pixels,stride,0);
                int opaque=0,dark=0;
                for(int i=0;i<pixels.Length;i+=4) { if(pixels[i+3]>200) opaque++; if(pixels[i+3]>200 && pixels[i]+pixels[i+1]+pixels[i+2]<300) dark++; }
                Assert(opaque>10000 && dark>100,"off-screen WPF render contains background and text, not an empty surface");
                var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using(var output=File.Create(Path.Combine(dir,"probe-normal-content.png"))) encoder.Save(output);
                lines.Add("Image is WPF content rendering ONLY; not a desktop screenshot or proof user can see the window.");
                var initial=Native.Bounds(normal.Handle); normal.NextMonitor(); await Task.Delay(150);
                Assert(Native.GetParent(normal.Handle)==0 && normal.Mode=="normal","ordinary monitor placement does not embed");
                Assert(normal.Switch("normal") && normal.State().Cloaked=="0","normal restore path remains ordinary and non-cloaked");

                // Explicit automation-only bypass: never sets HumanConfirmed and never used by interactive startup.
                experimental=new ProbeWindow(lines.Add,true);
                experimental.Show(); await Rendered(experimental);
                lines.Add(Native.Inventory());
                bool desktop=experimental.Switch("desktop"); await Task.Delay(150);
                Assert(desktop || experimental.Mode=="normal","desktop native transition succeeds or restores ordinary mode");
                if(desktop)
                {
                    Assert(experimental.State().ParentMatches && ((long)Native.GetLong(experimental.Handle,Native.Style)&Native.Child)!=0,"desktop report matches actual parent and WS_CHILD");
                    experimental.Input.Text="temporary non-personal test";
                    Assert(experimental.Switch("topmost"),"detach from desktop into topmost");
                    Assert(Native.GetParent(experimental.Handle)==0 && ((long)Native.GetLong(experimental.Handle,Native.ExStyle)&Native.Topmost)!=0,"topmost native state");
                    Assert(experimental.Input.Text=="temporary non-personal test","same-HWND detach preserves input");
                    Assert(experimental.Switch("desktop"),"explicit experimental reattach");
                    experimental.CheckHost(true);
                    Assert(experimental.Mode=="normal" && Native.GetParent(experimental.Handle)==0,"host-loss simulation returns ordinary mode");
                }
                Assert(experimental.Switch("normal") && !experimental.HumanConfirmed,"restoration does not invent human visibility confirmation");
                lines.Add("PENDING user must see two windows, type text, click counter, resize and explicitly confirm before desktop experiment.");
                lines.Add("PENDING actual desktop visibility, Explorer restart, sleep and mixed-DPI remain unverified.");
            }
            catch(Exception ex) { fail++; lines.Add("FAIL "+ex); }
            finally
            {
                experimental?.Close();
                lines.Add($"RESULT {pass} PASS, {fail} FAIL; human visibility NOT asserted");
                File.WriteAllLines(Path.Combine(dir,"p1.1-2-checks.txt"),lines);
                app.Shutdown(fail==0?0:1);
            }
        };
        return app.Run(normal);
    }
}
