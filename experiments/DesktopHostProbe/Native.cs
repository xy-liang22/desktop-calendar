using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace DesktopHostProbe;

internal static class Native
{
    internal const int Style = -16, ExStyle = -20;
    internal const long Child = 0x40000000L, Popup = 0x80000000L, Tool = 0x80, AppWindow = 0x40000, Topmost = 8;
    internal const uint NoActivate = 0x10, NoZOrder = 4, FrameChanged = 0x20;
    internal static readonly nint Bottom = 1, Top = 0, TopMost = -1, NotTopMost = -2;
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; public Point(int x, int y) { X=x; Y=y; } }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right-Left;
        public readonly int Height => Bottom-Top;
        public override readonly string ToString() => $"{Left},{Top} {Width}x{Height}";
    }
    internal delegate bool EnumProc(nint h, nint p);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc cb, nint p);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern nint FindWindowEx(nint parent, nint after, string? cls, string? name);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(nint h, StringBuilder s, int count);
    [DllImport("user32.dll")] internal static extern nint GetShellWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint h, out uint pid);
    [DllImport("user32.dll")] internal static extern nint GetParent(nint h);
    [DllImport("user32.dll")] static extern nint GetWindow(nint h,uint command);
    [DllImport("gdi32.dll")] internal static extern int GetClipBox(nint dc,out Rect rect);
    [DllImport("user32.dll", SetLastError=true)] static extern nint SetParent(nint child, nint parent);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] internal static extern nint GetLong(nint h, int index);
    [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW", SetLastError=true)] static extern nint SetLong(nint h, int index, nint value);
    [DllImport("user32.dll", SetLastError=true)] internal static extern bool SetWindowPos(nint h,nint order,int x,int y,int w,int height,uint flags);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint h,out Rect rect);
    [DllImport("user32.dll")] internal static extern bool ScreenToClient(nint h,ref Point pt);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point pt);
    [DllImport("user32.dll")] internal static extern bool IsWindow(nint h);
    [DllImport("user32.dll")] internal static extern bool IsChild(nint parent,nint child);
    [DllImport("user32.dll",EntryPoint="SendMessageW")] internal static extern nint SendMessage(nint h,uint msg,nint wp,nint lp);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint h);
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint h);
    [DllImport("user32.dll")] internal static extern nint GetWindowDpiAwarenessContext(nint h);
    [DllImport("user32.dll")] internal static extern bool AreDpiAwarenessContextsEqual(nint a,nint b);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint h);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point p);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint h,uint flags);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(nint h,out Rect rect);
    [DllImport("user32.dll")] internal static extern bool ClientToScreen(nint h,ref Point pt);
    [DllImport("user32.dll")] internal static extern int GetSystemMetrics(int index);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(nint h,uint attribute,out int value,int size);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string name);
    internal static string Class(nint h) { var s=new StringBuilder(128); GetClassName(h,s,s.Capacity); return s.ToString(); }
    internal static uint Pid(nint h) { GetWindowThreadProcessId(h,out uint pid); return pid; }
    internal static string Cloaked(nint h) => DwmGetWindowAttribute(h,14,out int value,4)==0?value.ToString():"unknown";
    internal static string ClientBounds(nint h)
    {
        if(!GetClientRect(h,out var r)) return "unavailable";
        var p=new Point(0,0); if(!ClientToScreen(h,ref p))return "unavailable";
        return $"{p.X},{p.Y} {r.Width}x{r.Height}";
    }
    internal static string HostIntersection(nint h,nint parent)
    {
        if(parent==0)return "top-level (no host clipping)";
        if(!GetClientRect(parent,out var cr))return "unknown";
        var p=new Point(0,0); ClientToScreen(parent,ref p); var r=Bounds(h);
        int width=Math.Max(0,Math.Min(r.Right,p.X+cr.Width)-Math.Max(r.Left,p.X));
        int height=Math.Max(0,Math.Min(r.Bottom,p.Y+cr.Height)-Math.Max(r.Top,p.Y));
        return $"hostClient={p.X},{p.Y} {cr.Width}x{cr.Height}; intersect={width}x{height} (rectangular geometry only)";
    }
    internal static string Coverage(nint h)
    {
        var r=Bounds(h); int own=0;
        var others=new HashSet<string>();
        foreach(var p in new[]{new Point(r.Left+r.Width/2,r.Top+r.Height/2),new Point(r.Left+r.Width/4,r.Top+r.Height/4),new Point(r.Left+3*r.Width/4,r.Top+3*r.Height/4)})
        {
            nint hit=WindowFromPoint(p);
            if(hit==h || IsChild(h,hit)) own++;
            else others.Add($"{hit:X}/{Class(hit)}/PID={Pid(hit)}");
        }
        return $"ownHit={own}/3; possibleCover=[{string.Join(",",others)}] (hit samples, not pixel proof)";
    }
    internal static Rect Bounds(nint h) { if (!GetWindowRect(h,out var r)) throw new InvalidOperationException("窗口矩形不可用"); return r; }
    internal static void SetStyle(nint h,int index,long value)
    {
        Marshal.SetLastPInvokeError(0);
        if (SetLong(h,index,(nint)value)==0 && Marshal.GetLastPInvokeError()!=0) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    internal static void Parent(nint h,nint parent)
    {
        Marshal.SetLastPInvokeError(0);
        if (SetParent(h,parent)==0 && Marshal.GetLastPInvokeError()!=0) throw new Win32Exception(Marshal.GetLastPInvokeError());
        // While WS_CHILD is still set, detaching can report the desktop HWND until styles are restored.
        if (parent!=0 && GetParent(h)!=parent) throw new InvalidOperationException("父窗口验证失败");
    }
    internal static void Place(nint h,Rect r,nint order,uint flags=NoActivate)
    {
        var p=new Point(r.Left,r.Top);
        if (((long)GetLong(h,Style)&Child)!=0 && !ScreenToClient(GetParent(h),ref p)) throw new InvalidOperationException("屏幕坐标转换失败");
        if (!SetWindowPos(h,order,p.X,p.Y,r.Width,r.Height,flags)) throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    // Only enumerate existing Explorer windows; no undocumented 0x052C message or shell style mutation.
    internal static (nint Host,nint DefView,nint Wallpaper) Desktop()
    {
        nint host=0,def=0,wallpaper=0;
        uint shellPid=Pid(GetShellWindow());
        if (shellPid==0) return (0,0,0);
        EnumWindows((h,_)=> {
            string cls=Class(h);
            if (Pid(h)!=shellPid || (cls!="Progman" && cls!="WorkerW")) return true;
            nint d=FindWindowEx(h,0,"SHELLDLL_DefView",null);
            if(d!=0) { host=h; def=d; return false; }
            return true;
        },0);
        if(host!=0)
        {
            wallpaper=FindWindowEx(host,0,"WorkerW",null); // modern child layout
            if(wallpaper==0) wallpaper=FindWindowEx(0,host,"WorkerW",null); // legacy sibling layout
            if(Pid(wallpaper)!=shellPid || FindWindowEx(wallpaper,0,"SHELLDLL_DefView",null)!=0) wallpaper=0;
        }
        return(host,def,wallpaper);
    }
    internal static string Inventory()
    {
        var d=Desktop();
        return $"OS={Environment.OSVersion.Version}; shell={GetShellWindow():X}; host={d.Host:X}/{Class(d.Host)}; defView={d.DefView:X}; wallpaper={d.Wallpaper:X}; hostDpi={GetDpiForWindow(d.Host)}; hostContext={GetWindowDpiAwarenessContext(d.Host)}";
    }
    // Read-only shell metadata, no titles, pixel capture, messages, style or Z-order changes.
    internal static string ShellTopology()
    {
        var output=new StringBuilder(Inventory()); var desktop=Desktop();
        void Append(nint window,string prefix) {
            if(window==0 || !IsWindow(window))return;
            GetWindowRect(window,out var bounds);
            output.Append($"\n{prefix} HWND={window:X} class={Class(window)} PID={Pid(window)} parent={GetParent(window):X} STYLE={(long)GetLong(window,Style):X} EX={(long)GetLong(window,ExStyle):X} visible={IsWindowVisible(window)} cloaked={Cloaked(window)} bounds={bounds} DPI={GetDpiForWindow(window)} context={GetWindowDpiAwarenessContext(window)}");
        }
        void Children(nint parent,int depth) {
            if(depth>3)return;
            var seen=new HashSet<nint>(); int order=0;
            for(nint h=GetWindow(parent,5);h!=0 && order<40 && seen.Add(h);h=GetWindow(h,2)) {
                Append(h,$"depth={depth} z={order++}");
                // Only descend within Explorer; leave another process' controls/content private.
                if(Pid(h)==Pid(GetShellWindow()))Children(h,depth+1);
            }
        }
        Append(desktop.Host,"HOST"); if(desktop.Host!=0)Children(desktop.Host,1);
        if(desktop.Wallpaper!=0 && GetParent(desktop.Wallpaper)!=desktop.Host)Append(desktop.Wallpaper,"WALLPAPER");
        output.Append("\nMetadata/rectangle/Z-order evidence only; does not prove on-screen pixels or identify the black-screen root cause.");
        return output.ToString();
    }
}
