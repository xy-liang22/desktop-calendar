using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Forms=System.Windows.Forms;

namespace DesktopHostProbe;

// Independent GDI/native-control baseline. It does not host any WPF visual or use layered windows.
internal sealed class NativePaintProbe : Forms.Form
{
    internal string Mode { get; private set; }="native-normal";
    internal string Case => Mode=="native-desktop"?"F":Mode=="native-local"?"L":"E";
    internal nint Host { get; private set; }
    internal bool HumanConfirmed { get; private set; }
    internal int PaintCount { get; private set; }
    internal string Error { get; private set; }="";
    internal readonly TestInput Input=new() { Text="请手动输入无隐私测试文字",Multiline=false };
    internal readonly Forms.Button Counter=new() { Text="点击计数：0" };
    internal readonly Forms.Button Confirm=new() { Text="已看见、输入并计数，允许 F 嵌入",Enabled=false };
    readonly Action<string> report;
    readonly Action snapshot;
    readonly Forms.Button recover=new() { Text="返回 E 普通窗口" };
    readonly Forms.Button exit=new() { Text="退出原生对照，返回 A/B" };
    long originalStyle;
    bool inputEdited,transitioning;
    int clicks;
    string paintClip="not painted";
    uint shellPid;
    nint shell;
    Native.Point dragStart;
    Native.Rect dragRect;
    string? dragEdge;
    readonly Font uiFont=new("Microsoft YaHei UI",11);
    [DllImport("user32.dll")] static extern int FillRect(nint dc,ref Native.Rect r,nint brush);
    [DllImport("gdi32.dll")] static extern nint CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] static extern int SetBkMode(nint dc,int mode);
    [DllImport("gdi32.dll")] static extern uint SetTextColor(nint dc,uint color);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int DrawText(nint dc,string text,int length,ref Native.Rect rect,uint format);

    internal NativePaintProbe(Action<string> report,Action snapshot,Native.Rect initial)
    {
        this.report=report; this.snapshot=snapshot;
        Text="Desktop Calendar — E/F 原生 GDI 对照组件";
        StartPosition=Forms.FormStartPosition.Manual;
        AutoScaleMode=Forms.AutoScaleMode.None;
        Bounds=new Rectangle(initial.Left,initial.Top,Math.Max(700,initial.Width),Math.Max(560,initial.Height));
        MinimumSize=new Size(640,500); BackColor=Color.FromArgb(222,233,245); Font=uiFont;
        // Keep the baseline comparable to C: opaque, non-layered, own HWND and native EDIT/BUTTON controls.
        SetStyle(Forms.ControlStyles.OptimizedDoubleBuffer,false);
        Controls.AddRange(new Forms.Control[]{Input,Counter,Confirm,recover,exit});
        Input.TextChanged+=(_,_)=> { if(Input.EditingByInputMessage) inputEdited=true; UpdateConfirm(); };
        Counter.Click+=(_,_)=> { Counter.Text=$"点击计数：{++clicks}"; UpdateConfirm(); report($"NATIVE_CLICK count={clicks}"); };
        Confirm.Click+=(_,_)=> {
            if(!Confirm.Enabled)return;
            HumanConfirmed=true; Confirm.Enabled=false; Confirm.Text="E 已由用户确认；请从控制台选择 F";
            report("NATIVE_HUMAN_CONFIRMED"); snapshot();
        };
        recover.Click+=(_,_)=> { Restore(); snapshot(); };
        exit.Click+=(_,_)=>Close();
        FormClosed+=(_,_)=> { report("NATIVE_CLOSED"); uiFont.Dispose(); };
        Shown+=(_,_)=> { originalStyle=(long)Native.GetLong(Handle,Native.Style); report($"NATIVE_CREATED hwnd={Handle:X}; EDIT={Input.Handle:X}; BUTTON={Counter.Handle:X}"); LayoutControls(); };
        Resize+=(_,_)=>LayoutControls();
        MouseDown+=(_,e)=> {
            if(e.Button!=Forms.MouseButtons.Left)return;
            int edge=12;
            string sides=(e.X<edge?"L":e.X>=ClientSize.Width-edge?"R":"")+(e.Y<edge?"T":e.Y>=ClientSize.Height-edge?"B":"");
            if(sides=="" && e.Y>90)return;
            dragEdge=sides; Native.GetCursorPos(out dragStart); dragRect=Native.Bounds(Handle); Capture=true;
        };
        MouseMove+=(_,_)=>Drag();
        MouseUp+=(_,_)=> { dragEdge=null; Capture=false; };
        MouseCaptureChanged+=(_,_)=> { if(!Capture)dragEdge=null; };
    }
    void UpdateConfirm() => Confirm.Enabled=!HumanConfirmed && inputEdited && clicks>0;
    protected override void WndProc(ref Forms.Message m)
    {
        base.WndProc(ref m);
        // E has a real top-level frame: let Windows move it from the painted title area.
        // Preserve native caption buttons/borders and leave F's unverified child input path unchanged.
        if(m.Msg!=0x84 || Mode!="native-normal" || m.Result!=1)return;
        long packed=(long)m.LParam;
        var point=PointToClient(new Point(unchecked((short)packed),unchecked((short)(packed>>16))));
        if(point.X>=12 && point.X<ClientSize.Width-12 && point.Y>=12 && point.Y<90)m.Result=2;
    }
    void LayoutControls()
    {
        int width=Math.Max(200,ClientSize.Width-48);
        Input.SetBounds(24,210,width,32); Counter.SetBounds(24,255,width,38);
        Confirm.SetBounds(24,305,width,42); recover.SetBounds(24,360,width,36); exit.SetBounds(24,410,width,36);
    }
    protected override void OnPaint(Forms.PaintEventArgs e)
    {
        base.OnPaint(e);
        nint dc=e.Graphics.GetHdc();
        try
        {
            int clipType=Native.GetClipBox(dc,out var clip);
            paintClip=$"lastPaintMode={Mode}; GDI clipType={clipType} box={clip}; paintUpdate={e.ClipRectangle} (not screen proof)";
            void Fill(int x,int y,int w,int h,uint color) {
                var rect=new Native.Rect { Left=x,Top=y,Right=x+w,Bottom=y+h }; nint brush=CreateSolidBrush(color);
                try { FillRect(dc,ref rect,brush); } finally { if(brush!=0)DeleteObject(brush); }
            }
            Fill(0,0,ClientSize.Width,90,0x8B3D48);
            int w=Math.Max(1,(ClientSize.Width-48)/3);
            Fill(24,108,w,40,0x0045FF); Fill(24+w,108,w,40,0x32CD32); Fill(24+2*w,108,w,40,0xFF901E);
            SetBkMode(dc,1); SetTextColor(dc,0xFFFFFF);
            var title=new Native.Rect { Left=24,Top=24,Right=ClientSize.Width-24,Bottom=86 };
            string heading=$"{Case} · Win32 / GDI · {Mode} · 拖动紫色区域";
            DrawText(dc,heading,-1,ref title,0x10|0x20);
            SetTextColor(dc,0); var info=new Native.Rect { Left=24,Top=165,Right=ClientSize.Width-24,Bottom=203 };
            DrawText(dc,"应见红绿蓝色条、原生输入框及按钮；不是 WPF 内容。",-1,ref info,0x10);
            PaintCount++;
        }
        finally { e.Graphics.ReleaseHdc(dc); }
    }
    void Drag()
    {
        if(!Capture || dragEdge==null)return;
        Native.GetCursorPos(out var p); var r=dragRect; int dx=p.X-dragStart.X,dy=p.Y-dragStart.Y;
        if(dragEdge=="") { r.Left+=dx;r.Right+=dx;r.Top+=dy;r.Bottom+=dy; }
        else {
            if(dragEdge.Contains('L'))r.Left=Math.Min(r.Left+dx,r.Right-MinimumSize.Width);
            if(dragEdge.Contains('R'))r.Right=Math.Max(r.Right+dx,r.Left+MinimumSize.Width);
            if(dragEdge.Contains('T'))r.Top=Math.Min(r.Top+dy,r.Bottom-MinimumSize.Height);
            if(dragEdge.Contains('B'))r.Bottom=Math.Max(r.Bottom+dy,r.Top+MinimumSize.Height);
        }
        Native.Place(Handle,r,0,Native.NoActivate|Native.NoZOrder);
    }
    internal bool Attach(bool automated=false,Forms.Form? localHost=null)
    {
        Error="";
        if(originalStyle==0) { Error="普通原生窗口尚未完成 Shown，拒绝挂载"; return false; }
        if(!automated && !HumanConfirmed) { Error="请先在 E 中输入、计数并确认；F 未执行"; return false; }
        transitioning=true;
        try
        {
            if(Host!=0)Restore();
            nint host=localHost?.Handle??Native.Desktop().Host;
            if(localHost!=null && Native.Pid(host)!=Environment.ProcessId)throw new InvalidOperationException("对照宿主必须由本实验进程拥有");
            if(host==0 || !Native.IsWindowVisible(host))throw new InvalidOperationException("无可用桌面宿主");
            if(!Native.AreDpiAwarenessContextsEqual(Native.GetWindowDpiAwarenessContext(Handle),Native.GetWindowDpiAwarenessContext(host)))
                throw new InvalidOperationException("DPI awareness 不一致，拒绝原生挂载");
            var rect=Native.Bounds(Handle);
            if(localHost!=null) {
                Native.GetClientRect(host,out var client); var origin=new Native.Point(0,0); Native.ClientToScreen(host,ref origin);
                int width=Math.Min(rect.Width,Math.Max(1,client.Width-48)),height=Math.Min(rect.Height,Math.Max(1,client.Height-48));
                rect.Left=origin.X+24; rect.Top=origin.Y+24; rect.Right=rect.Left+width; rect.Bottom=rect.Top+height;
            }
            Native.SetStyle(Handle,Native.Style,(originalStyle|Native.Child)&~(Native.Popup|0x00C00000L|0x00040000L));
            Native.SetStyle(Handle,Native.ExStyle,((long)Native.GetLong(Handle,Native.ExStyle)|Native.Tool)&~Native.AppWindow);
            Native.Parent(Handle,host); Host=host; shell=Native.GetShellWindow(); shellPid=Native.Pid(host);
            Native.Place(Handle,rect,Native.Top,Native.NoActivate|Native.FrameChanged);
            Mode=localHost==null?"native-desktop":"native-local"; Invalidate(); // One ordinary content change, never a refresh loop.
            return true;
        }
        catch(Exception ex) { Error=ex.Message; try { Restore(); } catch(Exception fallback) { Error+="; restore="+fallback.Message; } return false; }
        finally { transitioning=false; report("NATIVE_ATTACH "+Evidence()); if(localHost==null)report("SHELL_TOPOLOGY "+Native.ShellTopology()); }
    }
    internal void Restore()
    {
        var rect=Native.Bounds(Handle);
        if(((long)Native.GetLong(Handle,Native.Style)&Native.Child)!=0)Native.Parent(Handle,0);
        Native.SetStyle(Handle,Native.Style,originalStyle&~Native.Child);
        Native.SetStyle(Handle,Native.ExStyle,((long)Native.GetLong(Handle,Native.ExStyle)|Native.AppWindow)&~Native.Tool);
        Host=0; Mode="native-normal";
        var area=Forms.Screen.PrimaryScreen!.WorkingArea;
        int width=Math.Min(rect.Width,area.Width),height=Math.Min(rect.Height,area.Height);
        rect.Left=area.Left+(area.Width-width)/2; rect.Top=area.Top+(area.Height-height)/2; rect.Right=rect.Left+width;rect.Bottom=rect.Top+height;
        Native.Place(Handle,rect,Native.NotTopMost,Native.FrameChanged);
        Show(); Activate(); Invalidate();
    }
    internal void CheckHost(bool simulate=false)
    {
        if(transitioning || Host==0 || IsDisposed)return;
        if(simulate || !Native.IsWindow(Host) || Native.Pid(Host)!=shellPid || Native.GetShellWindow()!=shell || Native.GetParent(Handle)!=Host)
        {
            try { Restore(); report("NATIVE_HOST_LOST safe normal fallback"); }
            catch(Exception ex) { Error="宿主丢失找回失败："+ex.Message+"；请点 A 重建"; report(Error); }
        }
    }
    internal string Evidence() => IsDisposed?"closed":$"case={Case}; mode={Mode}; PID={Environment.ProcessId}; HWND={Handle:X}; parent={Native.GetParent(Handle):X}; host={Host:X}; match={Host!=0 && Native.GetParent(Handle)==Host}; style={(long)Native.GetLong(Handle,Native.Style):X}; ex={(long)Native.GetLong(Handle,Native.ExStyle):X}; visible={Native.IsWindowVisible(Handle)}; cloaked={Native.Cloaked(Handle)}; bounds={Native.Bounds(Handle)}; {Native.HostIntersection(Handle,Host)}; {Native.Coverage(Handle)}; GDI paint callbacks={PaintCount} (NOT screen proof); {paintClip}; EDIT={Native.Class(Input.Handle)}; BUTTON={Native.Class(Counter.Handle)}; humanConfirmed={HumanConfirmed}; error={Error}";

    internal sealed class TestInput : Forms.TextBox
    {
        internal bool EditingByInputMessage { get; private set; }
        protected override void WndProc(ref Forms.Message m)
        {
            bool prior=EditingByInputMessage;
            // Ordinary typing, IME results, paste/cut/clear and Delete. Text= alone is not manual input.
            EditingByInputMessage=prior || Focused && (m.Msg is 0x102 or 0x300 or 0x302 or 0x303 ||
                m.Msg==0x10F && ((long)m.LParam&0x800)!=0 || m.Msg==0x100 && m.WParam==0x2E);
            try { base.WndProc(ref m); } finally { EditingByInputMessage=prior; }
        }
    }
}
