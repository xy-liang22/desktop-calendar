using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;

namespace DesktopHostProbe;

internal static class Checks
{
    internal static int Run(Application app)
    {
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        var lines=new List<string>(); int pass=0,fail=0;
        bool software=RenderOptions.ProcessRenderMode==RenderMode.SoftwareOnly;
        string dir=Path.Combine(Environment.CurrentDirectory,"artifacts"); Directory.CreateDirectory(dir);
        void Assert(bool ok,string name) { lines.Add((ok?"PASS ":"FAIL ")+name); if(ok)pass++;else fail++; }
        void CheckColors(ProbeWindow w,string label) {
            w.SampleContent(); var s=w.State(); var rgb=Regex.Match(s.SurfaceEvidence,@"RGB=(\d+)/(\d+)/(\d+)");
            Assert(rgb.Success && int.Parse(rgb.Groups[1].Value)>100 && int.Parse(rgb.Groups[2].Value)>100 && int.Parse(rgb.Groups[3].Value)>100,label+" off-screen RGB content exists (not screen proof)");
            lines.Add($"CASE {label}: render={s.RenderPreference}; tier={s.RenderTier}; remote={s.RemoteSession}; RDP acceleration={s.RdpAcceleration}; transparent={s.Transparent}; layered={s.Layered}; style={s.Style}/{s.ExStyle}; parent={s.Parent}; {s.HostIntersection}; {s.Coverage}; {s.SurfaceEvidence}");
        }
        async Task Rendered(ProbeWindow w) {
            for(int i=0;i<60;i++) { if(w.Rendered)return; await Task.Delay(100); }
            throw new TimeoutException("WPF ContentRendered was not observed");
        }
        var normal=new ProbeWindow(lines.Add);
        normal.Loaded+=async (_,_)=> {
            ProbeWindow? experimental=null,transparent=null;
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
                Assert(!s.Transparent && !s.Layered && !s.Child && s.Parent=="0","A: opaque non-layered top-level");
                CheckColors(normal,"A");
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
                using(var output=File.Create(Path.Combine(dir,software?"probe-software-content.png":"probe-normal-content.png"))) encoder.Save(output);
                lines.Add("Image is WPF content rendering ONLY; not a desktop screenshot or proof user can see the window.");
                Assert(normal.Switch("normal") && normal.State().Cloaked=="0","normal restore path remains ordinary and non-cloaked");

                // Explicit automation-only bypass: never sets HumanConfirmed and never used by interactive startup.
                normal.Hide(); // Do not let another probe's ordinary window obscure C/D diagnostics.
                experimental=new ProbeWindow(lines.Add,true);
                experimental.Show(); await Rendered(experimental);
                lines.Add(Native.Inventory());
                bool desktop=experimental.Switch("desktop"); await Task.Delay(150);
                Assert(desktop || experimental.Mode=="normal","desktop native transition succeeds or restores ordinary mode");
                if(desktop)
                {
                    CheckColors(experimental,"C");
                    Assert(!experimental.State().Layered && experimental.State().Child && !experimental.State().Popup,"C: non-layered WS_CHILD without WS_POPUP");
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
                experimental.Close(); experimental=null;
                transparent=new ProbeWindow(lines.Add,true,true);
                transparent.Show(); await Rendered(transparent);
                var b=transparent.State();
                Assert(b.Case=="B" && b.Transparent && b.Layered && !b.Child && b.Parent=="0","B: true WPF transparent layered top-level");
                CheckColors(transparent,"B");
                Assert(!transparent.State().SurfaceEvidence.Contains("partialAlpha=0,"),"B: semi-transparent background retained");
                bool transparentAttached=transparent.Switch("desktop"); await Task.Delay(150);
                Assert(transparentAttached || transparent.Mode=="normal","D: attach or explicit normal fallback");
                if(transparentAttached) {
                    var d=transparent.State();
                    Assert(d.Case=="D" && d.Layered && d.Child && !d.Popup && d.ParentMatches,"D: layered child with verified host, without manually toggling layered flag");
                    CheckColors(transparent,"D");
                }
                Assert(transparent.Switch("normal") && transparent.State().Case=="B" && transparent.State().Layered,"transparent detach preserves B transparency");
                bool worker=transparent.Switch("wallpaper");
                Assert(worker?transparent.State().ParentMatches:transparent.Mode=="normal" && transparent.LastError!="","WorkerW result distinguishes actual attachment from failed normal fallback");
                lines.Add($"WORKERW attached={worker}; actual={transparent.Mode}; requested={transparent.State().RequestedMode}; error={transparent.LastError}");
                lines.Add("PENDING user must see two windows, type text, click counter, resize and explicitly confirm before desktop experiment.");
                lines.Add("PENDING actual desktop visibility, Explorer restart, sleep and mixed-DPI remain unverified.");
            }
            catch(Exception ex) { fail++; lines.Add("FAIL "+ex); }
            finally
            {
                transparent?.Close(); experimental?.Close();
                lines.Add($"RESULT {pass} PASS, {fail} FAIL; human visibility NOT asserted");
                File.WriteAllLines(Path.Combine(dir,software?"p1.1-2-software-checks.txt":"p1.1-2-checks.txt"),lines);
                app.Shutdown(fail==0?0:1);
            }
        };
        return app.Run(normal);
    }
}
