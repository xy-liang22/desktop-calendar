using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DesktopHostProbe;

internal static class OwnerChecks
{
    internal static int Run(Application app)
    {
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        var lines=new List<string>(); int pass=0,fail=0;
        void Check(bool ok,string name) { lines.Add((ok?"PASS ":"FAIL ")+name); if(ok)pass++;else fail++; }
        async Task Rendered(ProbeWindow w) {
            for(int i=0;i<60;i++) { if(w.Rendered)return; await Task.Delay(100); }
            throw new TimeoutException("ContentRendered not observed");
        }
        void Content(ProbeWindow w,string label) {
            w.SampleContent(); string evidence=w.State().SurfaceEvidence;
            var match=Regex.Match(evidence,@"RGB=(\d+)/(\d+)/(\d+), partialAlpha=(\d+)");
            Check(match.Success && int.Parse(match.Groups[1].Value)>100 && int.Parse(match.Groups[2].Value)>100 && int.Parse(match.Groups[3].Value)>100 && int.Parse(match.Groups[4].Value)>100,label+": WPF off-screen RGB/alpha retained; NOT screen proof");
        }
        var baseline=new ProbeWindow(lines.Add,false,true);
        baseline.Loaded+=async (_,_)=> {
            ProbeWindow? w=null; Window? cover=null;
            try {
                await Rendered(baseline);
                Check(!baseline.SwitchOwner("owner") && Native.Owner(baseline.Handle)==0,"owner gated until real baseline interaction or explicit automation-only bypass");
                baseline.Hide();
                w=new ProbeWindow(lines.Add,true,true); w.Show(); await Rendered(w);
                nint hwnd=w.Handle,shell=Native.GetShellWindow();
                var shellStyle=Native.GetLong(shell,Native.Style); var shellEx=Native.GetLong(shell,Native.ExStyle);
                string shellBounds=Native.Bounds(shell).ToString(),initialBounds=Native.Bounds(hwnd).ToString();
                int parentCalls=Native.ParentCalls;
                Check(w.State() is { Mode:"normal",Transparent:true,Layered:true,Child:false,HumanConfirmed:false },"transparent top-level baseline, no fabricated interaction confirmation");
                Content(w,"before owner");
                w.Input.Text="OwnerChecks temporary test"; w.Counter.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                Check(w.SwitchOwner("owner"),"owner command succeeds on this Shell");
                var s=w.State();
                Check(w.Handle==hwnd && Native.Owner(hwnd)==shell && Native.Class(shell)=="Progman","same HWND has exact checked Shell owner");
                Check(!s.Child && Native.GetAncestor(hwnd,2)==hwnd && Native.GetAncestor(hwnd,1)!=shell,"still top-level, not Explorer child; root/parent distinguished from owner");
                Check(!w.ShowInTaskbar && ((long)Native.GetLong(hwnd,Native.ExStyle)&Native.Tool)!=0 && ((long)Native.GetLong(hwnd,Native.ExStyle)&Native.AppWindow)==0,"taskbar/AltTab exclusion configuration; actual shell lists NOT asserted");
                Check(s.Layered && w.AllowsTransparency && ((long)Native.GetLong(hwnd,Native.ExStyle)&0x08000000)==0,"WPF layered rendering and normal input activation retained");
                Check(Native.Bounds(hwnd).ToString()==initialBounds && w.Input.Text=="OwnerChecks temporary test" && w.Counter.Content?.ToString()=="点击计数：1","owner preserves bounds and test control state");
                Check(w.OwnerModeZRequests==0 && Native.ParentCalls==parentCalls,"initial owner uses no explicit Z-order adjustment or SetParent");
                Content(w,"after owner");
                cover=new Window { Title="OwnerChecks ordinary cover",Width=700,Height=590,Background=Brushes.Coral };
                cover.Show(); Native.Place(new WindowInteropHelper(cover).Handle,Native.Bounds(hwnd),0,Native.NoZOrder|Native.NoActivate); cover.Activate();
                await Task.Delay(100);
                nint foreground=Native.GetForegroundWindow();
                w.Snapshot("READ_ONLY_OWNER_DIAGNOSTIC");
                Check(Native.GetForegroundWindow()==foreground,"diagnostic snapshot does not steal foreground");
                Check(((long)Native.GetLong(hwnd,Native.ExStyle)&Native.Topmost)==0,"owner is native non-topmost while ordinary cover is shown");
                var rect=Native.Bounds(hwnd); var hit=Native.WindowFromPoint(new Native.Point(rect.Left+rect.Width/2,rect.Top+rect.Height/2));
                nint coverHwnd=new WindowInteropHelper(cover).Handle;
                Check(Native.IsAbove(coverHwnd,hwnd),"ordinary cover is above owner in native Z-order; NOT pixel proof");
                lines.Add($"COVER centerHitIsCover={hit==coverHwnd || Native.IsChild(coverHwnd,hit)}; foregroundIsCover={foreground==coverHwnd}; other applications may occlude this background automatic test");
                cover.Close(); cover=null;
                for(int i=0;i<3;i++) {
                    Check(w.SwitchOwner("owner-topmost") && Native.Owner(hwnd)==0 && ((long)Native.GetLong(hwnd,Native.ExStyle)&Native.Topmost)!=0,"explicit topmost detaches owner, cycle "+i);
                    Check(w.SwitchOwner("owner") && Native.Owner(hwnd)==shell && ((long)Native.GetLong(hwnd,Native.ExStyle)&Native.Topmost)==0,"explicit return to owner removes topmost, cycle "+i);
                }
                int zRequests=w.OwnerModeZRequests;
                await Task.Delay(2300); w.Snapshot("OWNER_IDLE");
                Check(w.OwnerModeZRequests==zRequests && Native.Owner(hwnd)==shell,"health/diagnostic interval does not repair owner or adjust Z-order");
                Check(!w.Switch("desktop") && Native.ParentCalls==parentCalls,"owner session rejects mixing legacy child attachment");
                Check(w.SwitchOwner("owner-normal") && Native.Owner(hwnd)==0 && !w.State().Child && w.ShowInTaskbar,"explicit normal rescue clears external owner and restores taskbar entry");
                Check(w.Input.Text=="OwnerChecks temporary test" && w.Counter.Content?.ToString()=="点击计数：1" && w.Handle==hwnd,"mode round trips preserve input/counter/HWND");
                Check(Native.GetLong(shell,Native.Style)==shellStyle && Native.GetLong(shell,Native.ExStyle)==shellEx && Native.Bounds(shell).ToString()==shellBounds,"observed Shell styles and bounds unchanged");
                Check(Native.ParentCalls==parentCalls && !w.HumanConfirmed,"owner checks never call SetParent or fabricate human confirmation");
                Content(w,"normal rescue");
                lines.Add("DIAGNOSTIC "+w.OwnerEvidence());
                w.Close(); w=null;
                Check(!Native.IsWindow(hwnd),"normal close destroys experimental HWND");
                lines.Add("PENDING Win+D pixels/minimize history, actual taskbar/AltTab, real keyboard/mouse, flicker and coverage after editing require separate observation.");
            } catch(Exception ex) { fail++; lines.Add("FAIL "+ex); }
            finally {
                cover?.Close(); w?.Close(); baseline.Close();
                lines.Add($"RESULT {pass} PASS, {fail} FAIL; no physical visibility or Win+D claim");
                string dir=Path.Combine(Environment.CurrentDirectory,"artifacts"); Directory.CreateDirectory(dir);
                File.WriteAllLines(Path.Combine(dir,"p1.1-2-owner-checks.txt"),lines);
                app.Shutdown(fail==0?0:1);
            }
        };
        return app.Run(baseline);
    }
}
