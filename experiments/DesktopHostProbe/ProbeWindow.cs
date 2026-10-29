using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace DesktopHostProbe;

internal sealed record ProbeState(int Pid,string Hwnd,string Mode,bool Loaded,bool ContentRendered,
    double ContentWidth,double ContentHeight,bool HumanConfirmed,string Parent,string Host,
    bool ParentMatches,bool NativeVisible,bool WpfVisible,bool Minimized,string Cloaked,
    string Bounds,uint Dpi,string Coverage,string Error,string Case,bool Transparent,string Style,string ExStyle,
    bool Layered,bool Child,bool Popup,string RequestedMode,string ClientBounds,string HostIntersection,
    string RenderPreference,int RenderTier,bool RemoteSession,string RdpAcceleration,string HwndRenderMode,string SurfaceEvidence);

internal sealed class ProbeWindow : Window
{
    internal string Mode { get; private set; }="normal";
    internal nint Handle => new WindowInteropHelper(this).Handle;
    internal nint Host { get; private set; }
    internal bool Rendered { get; private set; }
    internal bool HumanConfirmed { get; private set; }
    internal string LastError { get; private set; }="";
    internal readonly TextBox Input=new() { Height=42,FontSize=19,Text="请在这里输入测试文字",Foreground=Brushes.Black,Background=Brushes.White };
    internal readonly Button Counter=new() { Content="点击计数：0",Padding=new Thickness(16,10,16,10) };
    internal readonly Button Confirm=new() { Content="我已看见并操作组件，允许桌面实验",IsEnabled=false,Padding=new Thickness(12),Margin=new Thickness(0,10,0,10) };
    readonly TextBlock status=new() { TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Black };
    readonly Action<string> report;
    readonly DispatcherTimer health=new() { Interval=TimeSpan.FromSeconds(2) };
    readonly bool automated;
    long originalStyle;
    nint shellAtAttach;
    uint hostPid;
    bool transitioning,inputEdited;
    int clicks;
    uint taskbarCreated;
    string requestedMode="normal",surfaceEvidence="not sampled";
    Native.Rect dragRect;
    Native.Point dragStart;
    string resizeEdge="";
    readonly TextBlock heading=new() { FontSize=26,Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap };
    internal string Case => Mode=="desktop"?(AllowsTransparency?"D":"C"):(Mode=="normal"?(AllowsTransparency?"B":"A"):"other");

    internal ProbeWindow(Action<string> report,bool automated=false,bool transparent=false)
    {
        this.report=report; this.automated=automated;
        Title=transparent?"Desktop Calendar — B 透明对照组件":"Desktop Calendar — A 非透明对照组件";
        Width=700; Height=590; MinWidth=540; MinHeight=460;
        WindowStartupLocation=WindowStartupLocation.CenterScreen;
        ShowInTaskbar=true; ShowActivated=true;
        WindowStyle=transparent?WindowStyle.None:WindowStyle.SingleBorderWindow; ResizeMode=ResizeMode.CanResizeWithGrip;
        AllowsTransparency=transparent; Background=transparent?Brushes.Transparent:Brushes.WhiteSmoke; Foreground=Brushes.Black;
        var stack=new StackPanel { Margin=new Thickness(24) };
        var root=new Grid(); Content=root;
        var surface=new Border { CornerRadius=new CornerRadius(20),Background=new SolidColorBrush(Color.FromArgb(transparent?(byte)205:(byte)255,222,233,245)),
            Child=new ScrollViewer { Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto } };
        root.Children.Add(surface);
        root.SizeChanged+=(_,_)=>root.Clip=new RectangleGeometry(new Rect(0,0,root.ActualWidth,root.ActualHeight),20,20);
        var title=new Border { Background=Brushes.DarkSlateBlue,Padding=new Thickness(12),Margin=new Thickness(0,0,0,12),Cursor=Cursors.SizeAll,Child=heading };
        title.MouseLeftButtonDown+=(_,e)=> { BeginDrag(""); title.CaptureMouse(); e.Handled=true; };
        title.MouseMove+=(_,_)=> { if(title.IsMouseCaptured)UpdateDrag(); };
        title.MouseLeftButtonUp+=(_,_)=>title.ReleaseMouseCapture();
        stack.Children.Add(title);
        heading.Text=$"{(transparent?"B":"A")} · {(transparent?"透明":"非透明")}顶层 WPF · 拖这里移动";
        var stripes=new Grid { Height=40,Margin=new Thickness(0,0,0,10) };
        foreach(var brush in new[]{Brushes.OrangeRed,Brushes.LimeGreen,Brushes.DodgerBlue}) {
            int col=stripes.ColumnDefinitions.Count; stripes.ColumnDefinitions.Add(new ColumnDefinition());
            var block=new Border { Background=brush }; Grid.SetColumn(block,col); stripes.Children.Add(block);
        }
        stack.Children.Add(stripes);
        stack.Children.Add(new TextBlock { Text="应见红、绿、蓝色块与文字；先输入、点击计数并确认，再从控制台嵌入。\n若变成纯黑矩形，请从控制台记录“黑屏”，再重建 A 找回。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12) });
        stack.Children.Add(Input);
        Input.TextChanged+=(_,_)=> {
            if(IsLoaded) {
                if(Input.IsKeyboardFocusWithin) inputEdited=true;
                UpdateConfirmation();
                report($"INPUT chars={Input.Text.Length}; keyboardFocus={Input.IsKeyboardFocusWithin}");
            }
        };
        Counter.Margin=new Thickness(0,12,0,0);
        Counter.Click+=(_,_)=> { Counter.Content=$"点击计数：{++clicks}"; UpdateConfirmation(); report($"CLICK count={clicks}"); };
        stack.Children.Add(Counter);
        Confirm.Click+=(_,_)=> {
            if(!Confirm.IsEnabled) return;
            HumanConfirmed=true; Confirm.Content="已由用户确认；请从控制台选择桌面实验";
            Confirm.IsEnabled=false; Snapshot("HUMAN_CONFIRMED");
        };
        stack.Children.Add(Confirm);
        stack.Children.Add(new TextBlock { Text="若嵌入后消失：回到控制台点“重建普通窗口 / 找回”。\n重建会清空测试文字；不要输入个人资料。",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12) });
        var exit=new Button { Content="退出组件",Padding=new Thickness(12,6,12,6),HorizontalAlignment=HorizontalAlignment.Left };
        exit.Click+=(_,_)=> { report("USER_EXIT"); Close(); };
        stack.Children.Add(exit); stack.Children.Add(status);
        foreach(string edge in new[]{"L","R","T","B","LT","RT","LB","RB"}) {
            var thumb=new Thumb { Width=edge.Contains('L')||edge.Contains('R')?10:double.NaN,Height=edge.Contains('T')||edge.Contains('B')?10:double.NaN,
                HorizontalAlignment=edge.Contains('L')?HorizontalAlignment.Left:edge.Contains('R')?HorizontalAlignment.Right:HorizontalAlignment.Stretch,
                VerticalAlignment=edge.Contains('T')?VerticalAlignment.Top:edge.Contains('B')?VerticalAlignment.Bottom:VerticalAlignment.Stretch,
                Cursor=edge.Length==2?(edge is "LT" or "RB"?Cursors.SizeNWSE:Cursors.SizeNESW):(edge is "L" or "R"?Cursors.SizeWE:Cursors.SizeNS) };
            var template=new ControlTemplate(typeof(Thumb)); var border=new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty,Brushes.Transparent); template.VisualTree=border; thumb.Template=template;
            thumb.DragStarted+=(_,_)=>BeginDrag(edge); thumb.DragDelta+=(_,_)=>UpdateDrag(); root.Children.Add(thumb);
        }
        SourceInitialized+=(_,_)=> {
            taskbarCreated=Native.RegisterWindowMessage("TaskbarCreated");
            HwndSource.FromHwnd(Handle)?.AddHook(Hook);
            report($"HWND_CREATED pid={Environment.ProcessId} hwnd={Handle:X}; no native style/parent changes at startup");
        };
        Loaded+=(_,_)=> { report($"WPF_LOADED hwnd={Handle:X} content={Content.GetType().Name}"); health.Start(); };
        health.Tick+=(_,_)=>CheckHost();
        ContentRendered+=(_,_)=> {
            bool first=!Rendered; Rendered=true;
            if(first) { originalStyle=(long)Native.GetLong(Handle,Native.Style); report($"WPF_CONTENT_RENDERED hwnd={Handle:X}"); Snapshot("CONTENT_RENDERED"); }
        };
        Closed+=(_,_)=> { health.Stop(); report("WINDOW_CLOSED"); };
    }
    void UpdateConfirmation() { if(!HumanConfirmed) Confirm.IsEnabled=clicks>0 && inputEdited && Rendered; }
    nint Hook(nint hwnd,int message,nint wp,nint lp,ref bool handled)
    {
        if(message is 2 or 0x82) report($"HWND_DESTROY msg={message:X} hwnd={hwnd:X}");
        if(message==0x02E0) _=Dispatcher.BeginInvoke(()=>Snapshot("WM_DPICHANGED"));
        if((uint)message==taskbarCreated || message==0x007E || (message==0x0218 && (wp==7 || wp==18)))
            _=Dispatcher.BeginInvoke(()=> { CheckHost(); Snapshot("shell/display/resume"); });
        return 0;
    }
    internal bool Switch(string requested)
    {
        requestedMode=requested;
        string mode=requested=="fallback"?"normal":requested;
        if(!Rendered) { LastError="WPF ContentRendered 尚未到达，拒绝改变宿主"; Snapshot("REJECTED"); return false; }
        if(mode is "desktop" or "wallpaper" or "bottom")
        {
            if(!HumanConfirmed && !automated) { LastError="请先在普通组件内输入文字、点计数并确认看见；本次没有嵌入"; Snapshot("REJECTED"); return false; }
        }
        if(transitioning) { LastError="上一次模式转换尚未结束"; return false; }
        transitioning=true; LastError="";
        try
        {
            var bounds=Native.Bounds(Handle);
            Detach(bounds);
            if(mode is "desktop" or "wallpaper")
            {
                var desktop=Native.Desktop();
                nint candidate=mode=="desktop"?desktop.Host:desktop.Wallpaper;
                if(candidate==0 || !Native.IsWindowVisible(candidate)) throw new InvalidOperationException("没有符合条件的现存宿主");
                if(!Native.AreDpiAwarenessContextsEqual(Native.GetWindowDpiAwarenessContext(Handle),Native.GetWindowDpiAwarenessContext(candidate)))
                    throw new InvalidOperationException("DPI awareness 不一致，拒绝挂载");
                // Preserve WPF's own layered flag; do not toggle WS_EX_LAYERED or force repaints.
                Native.SetStyle(Handle,Native.Style,(originalStyle|Native.Child)&~(Native.Popup|0x00C00000L|0x00040000L));
                Native.SetStyle(Handle,Native.ExStyle,((long)Native.GetLong(Handle,Native.ExStyle)|Native.Tool)&~Native.AppWindow);
                Native.Parent(Handle,candidate);
                Host=candidate; hostPid=Native.Pid(candidate); shellAtAttach=Native.GetShellWindow();
                Native.Place(Handle,bounds,Native.Top,Native.NoActivate|Native.FrameChanged);
            }
            else Native.Place(Handle,bounds,mode=="topmost"?Native.TopMost:mode=="bottom"?Native.Bottom:Native.NotTopMost,Native.NoActivate|Native.FrameChanged);
            Mode=mode;
            heading.Text=$"{Case} · {(AllowsTransparency?"透明":"非透明")} WPF · {Mode} · 拖这里移动";
            if(mode=="normal") { WindowState=WindowState.Normal; Show(); BringIntoWorkArea(); Activate(); }
            Snapshot("MODE_CHANGED"); return true;
        }
        catch(Exception ex)
        {
            LastError=ex.Message; report("MODE_ERROR "+ex.Message);
            try { Detach(Native.Bounds(Handle)); Mode="normal"; heading.Text=$"{Case} · 普通回退（{requested} 未成功）"; WindowState=WindowState.Normal; Show(); BringIntoWorkArea(); Activate(); Snapshot("SAFE_NORMAL"); }
            catch(Exception fallback) { LastError+="; detach failed: "+fallback.Message; report("REBUILD_REQUIRED "+LastError); }
            return false;
        }
        finally { transitioning=false; }
    }
    void Detach(Native.Rect bounds)
    {
        if(((long)Native.GetLong(Handle,Native.Style)&Native.Child)!=0) Native.Parent(Handle,0);
        Native.SetStyle(Handle,Native.Style,originalStyle&~Native.Child);
        if(Native.GetLong(Handle,-8)!=0) Native.SetStyle(Handle,-8,0);
        if(Native.GetParent(Handle)!=0) throw new InvalidOperationException("脱离宿主失败；请重建普通组件");
        Native.SetStyle(Handle,Native.ExStyle,((long)Native.GetLong(Handle,Native.ExStyle)|Native.AppWindow)&~Native.Tool);
        Host=0; hostPid=0;
        Native.Place(Handle,bounds,Native.NotTopMost,Native.NoActivate|Native.FrameChanged);
    }
    internal void BringIntoWorkArea()
    {
        var area=Forms.Screen.PrimaryScreen!.WorkingArea;
        var r=Native.Bounds(Handle);
        int w=Math.Min(r.Width,area.Width),h=Math.Min(r.Height,area.Height);
        r.Left=area.Left+(area.Width-w)/2; r.Top=area.Top+(area.Height-h)/2;
        r.Right=r.Left+w; r.Bottom=r.Top+h;
        Native.Place(Handle,r,Native.NotTopMost,Native.FrameChanged);
    }
    internal void CheckHost(bool simulateLoss=false)
    {
        if(transitioning || Host==0) return;
        if(simulateLoss || !Native.IsWindow(Host) || Native.Pid(Host)!=hostPid || Native.GetShellWindow()!=shellAtAttach || Native.GetParent(Handle)!=Host)
        { report(simulateLoss?"SIMULATED_HOST_LOSS":"HOST_LOST"); Switch("normal"); }
    }
    internal void NextMonitor()
    {
        var all=Forms.Screen.AllScreens; var current=Forms.Screen.FromHandle(Handle);
        int index=Array.FindIndex(all,s=>s.DeviceName==current.DeviceName);
        var target=all[(index+1)%all.Length].WorkingArea; var r=Native.Bounds(Handle);
        int w=Math.Min(r.Width,target.Width),h=Math.Min(r.Height,target.Height);
        r.Left=target.Left+(target.Width-w)/2; r.Top=target.Top+(target.Height-h)/2; r.Right=r.Left+w; r.Bottom=r.Top+h;
        Native.Place(Handle,r,0,Native.NoActivate|Native.NoZOrder); Snapshot("NEXT_MONITOR");
    }
    void BeginDrag(string edge) { dragRect=Native.Bounds(Handle); Native.GetCursorPos(out dragStart); resizeEdge=edge; }
    void UpdateDrag()
    {
        Native.GetCursorPos(out var p); int dx=p.X-dragStart.X,dy=p.Y-dragStart.Y; var r=dragRect;
        double scale=Native.GetDpiForWindow(Handle)/96d; int w=(int)Math.Ceiling(MinWidth*scale),h=(int)Math.Ceiling(MinHeight*scale);
        if(resizeEdge=="") { r.Left+=dx;r.Right+=dx;r.Top+=dy;r.Bottom+=dy; }
        else {
            if(resizeEdge.Contains('L'))r.Left=Math.Min(dragRect.Left+dx,dragRect.Right-w);
            if(resizeEdge.Contains('R'))r.Right=Math.Max(dragRect.Right+dx,dragRect.Left+w);
            if(resizeEdge.Contains('T'))r.Top=Math.Min(dragRect.Top+dy,dragRect.Bottom-h);
            if(resizeEdge.Contains('B'))r.Bottom=Math.Max(dragRect.Bottom+dy,dragRect.Top+h);
        }
        Native.Place(Handle,r,0,Native.NoActivate|Native.NoZOrder);
    }
    internal void SampleContent()
    {
        var content=(FrameworkElement)Content;
        int w=Math.Clamp((int)Math.Ceiling(content.ActualWidth),1,2048),h=Math.Clamp((int)Math.Ceiling(content.ActualHeight),1,2048);
        var bitmap=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32); bitmap.Render(content);
        int stride=w*4; var pixels=new byte[stride*h]; bitmap.CopyPixels(pixels,stride,0);
        int red=0,green=0,blue=0,alpha=0,clear=0;
        for(int i=0;i<pixels.Length;i+=4) {
            int b=pixels[i],g=pixels[i+1],r=pixels[i+2],a=pixels[i+3];
            if(a==0)clear++; else if(a<255)alpha++;
            if(r>180 && r>g*2 && r>b*2)red++;
            if(g>140 && g>r*2 && g>b*2)green++;
            if(b>180 && b>r*2 && b>g)blue++;
        }
        surfaceEvidence=$"offscreen={w}x{h}, RGB={red}/{green}/{blue}, partialAlpha={alpha}, clear={clear}; NOT screen pixels";
        report("CONTENT_SAMPLE "+surfaceEvidence);
    }
    internal ProbeState State()
    {
        bool exists=Native.IsWindow(Handle); var content=Content as FrameworkElement;
        return new(Environment.ProcessId,$"{Handle:X}",Mode,IsLoaded,Rendered,content?.ActualWidth??0,content?.ActualHeight??0,
            HumanConfirmed,$"{Native.GetParent(Handle):X}",$"{Host:X}",Host!=0 && Native.GetParent(Handle)==Host,
            exists&&Native.IsWindowVisible(Handle),IsVisible,exists&&Native.IsIconic(Handle),exists?Native.Cloaked(Handle):"unknown",
            exists?Native.Bounds(Handle).ToString():"unavailable",Native.GetDpiForWindow(Handle),
            exists?Native.Coverage(Handle):"unavailable",LastError,Case,AllowsTransparency,
            $"{(long)Native.GetLong(Handle,Native.Style):X}",$"{(long)Native.GetLong(Handle,Native.ExStyle):X}",
            ((long)Native.GetLong(Handle,Native.ExStyle)&0x80000)!=0,((long)Native.GetLong(Handle,Native.Style)&Native.Child)!=0,
            ((long)Native.GetLong(Handle,Native.Style)&Native.Popup)!=0,requestedMode,Native.ClientBounds(Handle),Native.HostIntersection(Handle,Host),
            RenderOptions.ProcessRenderMode.ToString(),RenderCapability.Tier>>16,Native.GetSystemMetrics(0x1000)!=0,
            AppContext.TryGetSwitch("Switch.System.Windows.Media.EnableHardwareAccelerationInRdp",out bool enabled)?enabled.ToString():"unset (runtime default)",
            HwndSource.FromHwnd(Handle)?.CompositionTarget?.RenderMode.ToString()??"unavailable",surfaceEvidence);
    }
    internal void Snapshot(string reason)
    {
        if(!Native.IsWindow(Handle)) { report("HWND_MISSING "+reason); return; }
        status.Text=$"PID {Environment.ProcessId} | HWND {Handle:X} | {Mode}\n"+(HumanConfirmed?"普通窗口已由用户确认；嵌入可见性另验。":"屏幕可见性：尚待用户亲眼确认。");
        report("STATE "+System.Text.Json.JsonSerializer.Serialize(State()));
    }
    internal bool Command(string command)
    {
        switch(command)
        {
            case "exit": report("USER_EXIT"); return true;
            case "snapshot": Snapshot("COMMAND"); return true;
            case "sample": SampleContent(); Snapshot("OFFSCREEN_SAMPLE"); return true;
            case "next-monitor": NextMonitor(); return true;
            case "simulate-loss": CheckHost(true); Snapshot("SIMULATED_LOSS"); return true;
            case "simulate-destroy": if(automated) { Close(); return true; } LastError="仅自动检查可模拟销毁"; return false;
            default:
                if(command is "normal" or "fallback" or "desktop" or "wallpaper" or "bottom" or "topmost") return Switch(command);
                LastError="未知命令"; return false;
        }
    }
}
