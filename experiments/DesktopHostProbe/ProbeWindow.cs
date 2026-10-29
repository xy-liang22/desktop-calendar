using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DesktopHostProbe;

internal sealed class ProbeWindow : Window
{
    internal string Mode { get; private set; } = "fallback";
    internal nint Handle => new WindowInteropHelper(this).Handle;
    internal nint Host { get; private set; }
    internal readonly TextBox Input = new() { Height=38, FontSize=18, Text="输入测试 / input test" };
    internal readonly TextBlock Status = new() { TextWrapping=TextWrapping.Wrap, FontSize=13 };
    readonly Action<string> report;
    readonly DispatcherTimer health = new() { Interval=TimeSpan.FromSeconds(1) };
    long originalStyle;
    nint shellAtAttach;
    uint hostPid;
    bool transitioning;
    int clicks;
    Native.Rect dragRect;
    Native.Point dragStart;
    string resizeEdge="";
    uint taskbarCreated;

    internal ProbeWindow(Action<string> report)
    {
        this.report=report;
        Title="Desktop Calendar — 桌面层实验组件";
        Width=610; Height=390; MinWidth=470; MinHeight=330; Left=760; Top=180;
        ShowInTaskbar=false; WindowStyle=WindowStyle.None; AllowsTransparency=true;
        Background=Brushes.Transparent; Foreground=Brushes.White; ShowActivated=false;
        var surface=new Border { CornerRadius=new CornerRadius(18), Background=new SolidColorBrush(Color.FromArgb(239,29,39,54)), Padding=new Thickness(16) };
        var root=new Grid(); Content=root; root.Children.Add(surface);
        var stack=new StackPanel(); surface.Child=stack;
        var title=new Border { Background=Brushes.Transparent, Padding=new Thickness(4,0,4,12), Cursor=Cursors.SizeAll,
            Child=new TextBlock { Text="桌面层实验 · 拖动这里移动", FontSize=21 } };
        title.MouseLeftButtonDown+=(_,e)=> { BeginDrag(""); title.CaptureMouse(); e.Handled=true; };
        title.MouseMove+=(_,_)=> { if(title.IsMouseCaptured) UpdateDrag(); };
        title.MouseLeftButtonUp+=(_,_)=> { title.ReleaseMouseCapture(); Snapshot("drag-complete"); };
        stack.Children.Add(title);
        stack.Children.Add(new TextBlock { Text="仅技术实验，不保存输入。四边四角可缩放。", Margin=new Thickness(0,0,0,10) });
        stack.Children.Add(Input);
        Input.TextChanged+=(_,_)=> { if(IsLoaded) report($"INPUT chars={Input.Text.Length}"); };
        var buttons=new WrapPanel { Margin=new Thickness(0,10,0,6) };
        Button Add(string text,Action action) { var b=new Button { Content=text, Margin=new Thickness(0,0,8,8), Padding=new Thickness(10,7,10,7) }; b.Click+=(_,_)=>action(); buttons.Children.Add(b); return b; }
        var counter=Add("点击计数：0",()=>{});
        counter.Click+=(_,_)=> { counter.Content=$"点击计数：{++clicks}"; report($"CLICK {clicks}"); };
        Add("始终置顶",()=>Switch("topmost"));
        Add("返回桌面",()=>Switch("desktop"));
        Add("安全回退",()=>Switch("fallback"));
        Add("退出组件",()=> { report("USER_EXIT"); Close(); });
        stack.Children.Add(buttons); stack.Children.Add(Status);
        foreach(string edge in new[]{"L","R","T","B","LT","RT","LB","RB"})
        {
            bool corner=edge.Length==2;
            var thumb=new Thumb { Width=edge.Contains('L')||edge.Contains('R')?10:double.NaN,
                Height=edge.Contains('T')||edge.Contains('B')?10:double.NaN,
                HorizontalAlignment=edge.Contains('L')?HorizontalAlignment.Left:edge.Contains('R')?HorizontalAlignment.Right:HorizontalAlignment.Stretch,
                VerticalAlignment=edge.Contains('T')?VerticalAlignment.Top:edge.Contains('B')?VerticalAlignment.Bottom:VerticalAlignment.Stretch,
                Cursor=corner?(edge is "LT" or "RB"?Cursors.SizeNWSE:Cursors.SizeNESW):(edge is "L" or "R"?Cursors.SizeWE:Cursors.SizeNS),
                Background=Brushes.Transparent };
            var template=new ControlTemplate(typeof(Thumb));
            var border=new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.BackgroundProperty,Brushes.Transparent); template.VisualTree=border; thumb.Template=template;
            thumb.DragStarted+=(_,_)=>BeginDrag(edge);
            thumb.DragDelta+=(_,_)=>UpdateDrag();
            thumb.DragCompleted+=(_,_)=>Snapshot("resize-complete");
            root.Children.Add(thumb);
        }
        SourceInitialized+=(_,_)=> {
            originalStyle=(long)Native.GetLong(Handle,Native.Style)|0x10000000L; // SourceInitialized precedes initial Show.
            Native.SetStyle(Handle,Native.ExStyle,((long)Native.GetLong(Handle,Native.ExStyle)|Native.Tool)&~Native.AppWindow);
            taskbarCreated=Native.RegisterWindowMessage("TaskbarCreated");
            HwndSource.FromHwnd(Handle)?.AddHook(Hook);
        };
        health.Tick+=(_,_)=>CheckHost();
        Loaded+=(_,_)=>health.Start();
        Closed+=(_,_)=>health.Stop();
    }
    nint Hook(nint hwnd,int message,nint wp,nint lp,ref bool handled)
    {
        if(message is 2 or 0x82) report($"HWND_DESTROY msg={message:X} hwnd={hwnd:X}");
        if(message==0x02E0) Dispatcher.BeginInvoke(()=>Snapshot("WM_DPICHANGED"));
        if((uint)message==taskbarCreated || message==0x007E || (message==0x0218 && (wp==7 || wp==18)))
            Dispatcher.BeginInvoke(()=> { CheckHost(); EnsureReachable(); Snapshot("shell/display/resume"); });
        return 0;
    }
    internal bool Switch(string mode)
    {
        if(transitioning) return false;
        transitioning=true;
        try
        {
            var bounds=Native.Bounds(Handle);
            Detach(bounds);
            if(mode is "desktop" or "wallpaper")
            {
                var desktop=Native.Desktop();
                nint candidate=mode=="desktop"?desktop.Host:desktop.Wallpaper;
                if(candidate==0 || !Native.IsWindowVisible(candidate)) throw new InvalidOperationException("未发现可用的现存桌面宿主；不发送内部消息创建宿主");
                if(!Native.AreDpiAwarenessContextsEqual(Native.GetWindowDpiAwarenessContext(Handle),Native.GetWindowDpiAwarenessContext(candidate)))
                    throw new InvalidOperationException("宿主与 WPF DPI awareness 不一致，拒绝跨进程强制重置");
                Native.SetStyle(Handle,Native.Style,(originalStyle|Native.Child)&~Native.Popup);
                Native.Parent(Handle,candidate);
                Host=candidate; hostPid=Native.Pid(candidate); shellAtAttach=Native.GetShellWindow();
                Native.Place(Handle,bounds,Native.Top,Native.NoActivate|Native.FrameChanged);
            }
            else Native.Place(Handle,bounds,mode=="topmost"?Native.TopMost:mode=="bottom"?Native.Bottom:Native.NotTopMost,Native.NoActivate|Native.FrameChanged);
            Mode=mode; EnsureReachable(); Snapshot("mode"); return true;
        }
        catch(Exception ex)
        {
            report("MODE_FAILED "+mode+" "+ex);
            try { Detach(Native.Bounds(Handle)); Mode="fallback"; EnsureReachable(); Snapshot("safe-fallback"); }
            catch(Exception fallback) { report("FALLBACK_FAILED "+fallback.Message); Close(); }
            return false;
        }
        finally { transitioning=false; }
    }
    void Detach(Native.Rect bounds)
    {
        // ShowInTaskbar=false can give WPF an owner; clear only our HWND's owner as well.
        bool wasChild=((long)Native.GetLong(Handle,Native.Style)&Native.Child)!=0;
        if(wasChild) Native.Parent(Handle,0);
        Native.SetStyle(Handle,Native.Style,originalStyle&~Native.Child);
        // Win32 can return ERROR_INVALID_WINDOW_HANDLE for a redundant zero-to-zero owner assignment.
        if(Native.GetLong(Handle,-8)!=0) Native.SetStyle(Handle,-8,0);
        if(Native.GetParent(Handle)!=0) throw new InvalidOperationException("脱离桌面宿主失败");
        Native.SetStyle(Handle,Native.ExStyle,((long)Native.GetLong(Handle,Native.ExStyle)|Native.Tool)&~Native.AppWindow);
        Host=0; hostPid=0;
        Native.Place(Handle,bounds,Native.NotTopMost,Native.NoActivate|Native.FrameChanged);
    }
    internal void CheckHost(bool simulateLoss=false)
    {
        if(transitioning || Host==0) return;
        if(simulateLoss || !Native.IsWindow(Host) || Native.Pid(Host)!=hostPid || Native.GetShellWindow()!=shellAtAttach || Native.GetParent(Handle)!=Host)
        {
            report(simulateLoss?"SIMULATED_HOST_LOSS (not Explorer restart)":"HOST_LOST");
            Switch("fallback"); // bounded fallback, no reattachment loop or focus stealing
        }
    }
    internal void NextMonitor()
    {
        var all=Forms.Screen.AllScreens; var current=Forms.Screen.FromHandle(Handle);
        int index=Array.FindIndex(all,s=>s.DeviceName==current.DeviceName);
        var target=all[(index+1)%all.Length].WorkingArea;
        var r=Native.Bounds(Handle); int w=Math.Min(r.Width,target.Width),h=Math.Min(r.Height,target.Height);
        r.Left=target.Left+(target.Width-w)/2; r.Top=target.Top+(target.Height-h)/2; r.Right=r.Left+w; r.Bottom=r.Top+h;
        Native.Place(Handle,r,0,Native.NoActivate|Native.NoZOrder); Snapshot("next-monitor");
    }
    void EnsureReachable()
    {
        var r=Native.Bounds(Handle); var area=Forms.Screen.FromHandle(Handle).WorkingArea;
        if(Forms.Screen.AllScreens.Any(s=>s.WorkingArea.IntersectsWith(new System.Drawing.Rectangle(r.Left,r.Top,r.Width,r.Height)))) return;
        r.Right=area.Left+Math.Min(r.Width,area.Width); r.Bottom=area.Top+Math.Min(r.Height,area.Height); r.Left=area.Left; r.Top=area.Top;
        Native.Place(Handle,r,0,Native.NoActivate|Native.NoZOrder);
    }
    void BeginDrag(string edge) { dragRect=Native.Bounds(Handle); Native.GetCursorPos(out dragStart); resizeEdge=edge; }
    void UpdateDrag()
    {
        Native.GetCursorPos(out var p); int dx=p.X-dragStart.X,dy=p.Y-dragStart.Y; var r=dragRect;
        double scale=Native.GetDpiForWindow(Handle)/96d; int minW=(int)Math.Ceiling(MinWidth*scale),minH=(int)Math.Ceiling(MinHeight*scale);
        if(resizeEdge=="") { r.Left+=dx; r.Right+=dx; r.Top+=dy; r.Bottom+=dy; }
        else
        {
            if(resizeEdge.Contains('L')) r.Left=Math.Min(dragRect.Left+dx,dragRect.Right-minW);
            if(resizeEdge.Contains('R')) r.Right=Math.Max(dragRect.Right+dx,dragRect.Left+minW);
            if(resizeEdge.Contains('T')) r.Top=Math.Min(dragRect.Top+dy,dragRect.Bottom-minH);
            if(resizeEdge.Contains('B')) r.Bottom=Math.Max(dragRect.Bottom+dy,dragRect.Top+minH);
        }
        Native.Place(Handle,r,0,Native.NoActivate|Native.NoZOrder);
    }
    internal void Snapshot(string reason)
    {
        if(!Native.IsWindow(Handle)) return;
        string state=$"mode={Mode}; parent={Native.GetParent(Handle):X}; dpi={Native.GetDpiForWindow(Handle)}; context={Native.GetWindowDpiAwarenessContext(Handle)}; rect={Native.Bounds(Handle)}; visible={Native.IsWindowVisible(Handle)}; iconic={Native.IsIconic(Handle)}; style={(long)Native.GetLong(Handle,Native.Style):X}; ex={(long)Native.GetLong(Handle,Native.ExStyle):X}";
        Status.Text=state; report(reason+" "+state);
    }
    internal void Command(string command)
    {
        switch(command)
        {
            case "exit": report("USER_EXIT"); Close(); break;
            case "snapshot": Snapshot("manual"); break;
            case "next-monitor": NextMonitor(); break;
            case "simulate-loss": CheckHost(true); break;
            case "simulate-destroy": report("SIMULATED_WIDGET_DESTRUCTION (not Explorer restart)"); Close(); break;
            default: if(command is "desktop" or "wallpaper" or "bottom" or "topmost" or "fallback") Switch(command); break;
        }
    }
}
