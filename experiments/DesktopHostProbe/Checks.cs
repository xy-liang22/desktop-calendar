using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Forms=System.Windows.Forms;

namespace DesktopHostProbe;

internal static class Checks
{
    internal static int Run(Application app)
    {
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        string traceDir=Path.Combine(Environment.CurrentDirectory,"artifacts"); Directory.CreateDirectory(traceDir);
        string tracePath=Path.Combine(traceDir,"p1.1-2-trace.log");
        File.WriteAllText(tracePath,"Started self-test"+Environment.NewLine);
        var lines=new List<string>(); int pass=0,fail=0,skip=0;
        void Assert(bool ok,string name) { lines.Add((ok?"PASS ":"FAIL ")+name); if(ok)pass++;else fail++; }
        void Skip(string name) { skip++; lines.Add("PENDING "+name); }
        var window=new ProbeWindow(s=> { lines.Add(s); File.AppendAllText(tracePath,s+Environment.NewLine); });
        window.Closed+=(_,_)=>File.AppendAllText(tracePath,"WINDOW CLOSED"+Environment.NewLine);
        window.Loaded+=async (_,_)=> {
            try
            {
                await Task.Delay(350);
                lines.Add(Native.Inventory());
                nint shell=Native.GetShellWindow(); uint pid=Native.Pid(shell);
                var desktop=Native.Desktop();
                bool attached=window.Switch("desktop"); await Task.Delay(250);
                Assert(attached || window.Mode=="fallback","desktop attach or explicit safe fallback");
                if(attached)
                {
                    Assert(Native.GetParent(window.Handle)==desktop.Host,"actual Explorer parent HWND");
                    Assert(((long)Native.GetLong(window.Handle,Native.Style)&Native.Child)!=0,"WS_CHILD applied");
                    Assert(((long)Native.GetLong(window.Handle,Native.ExStyle)&Native.Topmost)==0,"desktop is not topmost");
                    Assert(Native.AreDpiAwarenessContextsEqual(Native.GetWindowDpiAwarenessContext(window.Handle),Native.GetWindowDpiAwarenessContext(desktop.Host)),"DPI awareness matches host");
                    Assert(Native.IsWindowVisible(window.Handle),"native visible flag (not proof of on-screen pixels)");
                }
                else Skip("desktop attachment unsupported in current shell/DPI state");
                Assert(!window.ShowInTaskbar && ((long)Native.GetLong(window.Handle,Native.ExStyle)&Native.Tool)!=0 && ((long)Native.GetLong(window.Handle,Native.ExStyle)&Native.AppWindow)==0,"component taskbar/Alt+Tab exclusion flags");
                window.Input.Text="temporary test 123";
                var before=Native.Bounds(window.Handle);
                Assert(window.Switch("topmost"),"switch to topmost"); await Task.Delay(200);
                Assert(Native.GetParent(window.Handle)==0 && ((long)Native.GetLong(window.Handle,Native.Style)&Native.Child)==0,"topmost detached from Explorer");
                Assert(((long)Native.GetLong(window.Handle,Native.ExStyle)&Native.Topmost)!=0,"topmost native flag");
                var after=Native.Bounds(window.Handle);
                Assert(before.Equals(after),"physical bounds preserved when detaching");
                Assert(window.Input.Text=="temporary test 123","in-memory input preserved across detach");
                if(attached)
                {
                    Assert(window.Switch("desktop"),"return to desktop"); await Task.Delay(200);
                    Assert(Native.Bounds(window.Handle).Equals(before),"physical bounds preserved on desktop return");
                    window.CheckHost(true); await Task.Delay(150);
                    Assert(window.Mode=="fallback" && Native.GetParent(window.Handle)==0,"simulated host loss falls back without reattach loop");
                    Assert(window.Input.Text=="temporary test 123","same-process fallback preserves input");
                }
                Assert(window.Switch("bottom"),"documented bottom z-order comparison available");
                Assert(Native.GetParent(window.Handle)==0 && ((long)Native.GetLong(window.Handle,Native.ExStyle)&Native.Topmost)==0,"bottom comparison remains ordinary non-topmost window");
                bool wallpaper=window.Switch("wallpaper");
                Assert(wallpaper || window.Mode=="fallback","WorkerW attach or explicit fallback");
                lines.Add("WorkerW attached="+wallpaper+" (input/visibility require physical testing)");
                if(!wallpaper) Skip("WorkerW wallpaper host absent: no claim of successful wallpaper embedding");
                window.Switch("fallback");
                foreach(var screen in Forms.Screen.AllScreens)
                {
                    window.NextMonitor(); await Task.Delay(250);
                    var r=Native.Bounds(window.Handle);
                    Assert(Forms.Screen.FromHandle(window.Handle).WorkingArea.IntersectsWith(new System.Drawing.Rectangle(r.Left,r.Top,r.Width,r.Height)),"fallback reachable on "+Forms.Screen.FromHandle(window.Handle).DeviceName);
                    lines.Add($"MONITOR bounds={r} dpi={Native.GetDpiForWindow(window.Handle)}");
                }
                if(attached)
                {
                    window.Switch("desktop");
                    foreach(var screen in Forms.Screen.AllScreens)
                    {
                        window.NextMonitor(); await Task.Delay(200);
                        Assert(Native.GetParent(window.Handle)==desktop.Host && Native.IsWindowVisible(window.Handle),"desktop HWND retained across monitor placement "+Forms.Screen.FromHandle(window.Handle).DeviceName);
                        lines.Add($"DESKTOP_MONITOR {Native.Bounds(window.Handle)} dpi={Native.GetDpiForWindow(window.Handle)}");
                    }
                }
                var region=Native.Bounds(window.Handle);
                var cover=new Window { Title="Probe ordinary coverage test", Width=350,Height=230,Left=region.Left/(Native.GetDpiForWindow(window.Handle)/96d),Top=region.Top/(Native.GetDpiForWindow(window.Handle)/96d),Content=new TextBlock { Text="Ordinary window coverage",FontSize=24 } };
                cover.Show(); cover.Activate(); await Task.Delay(350);
                var cr=Native.Bounds(new WindowInteropHelper(cover).Handle);
                nint hit=Native.WindowFromPoint(new Native.Point(cr.Left+cr.Width/2,cr.Top+cr.Height/2));
                Assert(Native.GetAncestor(hit,2)==new WindowInteropHelper(cover).Handle,"ordinary test window receives hit at overlapping point");
                cover.Close(); window.Switch("fallback");
                Assert(Native.GetShellWindow()==shell && Native.Pid(shell)==pid,"Explorer identity unchanged by checks");
                Assert(Native.IsWindow(desktop.DefView),"desktop icon view still exists");
                Skip("physical Win+D visibility; taskbar/Alt+Tab presentation; desktop icon/right-click interaction");
                Skip("physical mouse click, text/IME input, eight-direction drag/resize");
                Skip("user-controlled Explorer restart; sleep/resume; Windows 10 and mixed-DPI hardware");
            }
            catch(Exception ex) { fail++; lines.Add("FAIL unexpected exception "+ex); }
            finally
            {
                lines.Add($"RESULT {pass} PASS, {fail} FAIL, {skip} PENDING groups");
                string dir=Path.Combine(Environment.CurrentDirectory,"artifacts"); Directory.CreateDirectory(dir);
                File.WriteAllLines(Path.Combine(dir,"p1.1-2-checks.txt"),lines);
                app.Shutdown(fail==0?0:1);
            }
        };
        return app.Run(window);
    }
}
