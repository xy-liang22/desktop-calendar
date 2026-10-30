using System;
using System.Drawing;
using System.Windows;
using Forms=System.Windows.Forms;

namespace DesktopHostProbe;

// A visible, disposable host entirely owned by this process. Never changes Explorer styles.
internal sealed class LocalHostProbe : Forms.Form
{
    readonly bool noRedirection;
    internal LocalHostProbe(bool noRedirection)
    {
        this.noRedirection=noRedirection;
        Text=noRedirection?"Desktop Calendar — 本进程宿主 N（无重定向表面）":"Desktop Calendar — 本进程宿主 R（普通重定向）";
        StartPosition=Forms.FormStartPosition.CenterScreen; Size=new System.Drawing.Size(1150,1000);
        BackColor=Color.FromArgb(70,90,110);
    }
    protected override Forms.CreateParams CreateParams {
        get { var p=base.CreateParams; if(noRedirection)p.ExStyle|=0x200000; return p; }
    }
    internal static int Run(Application app,bool noRedirection)
    {
        app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        System.Windows.Forms.Integration.WindowsFormsHost.EnableWindowsFormsInterop();
        using var host=new LocalHostProbe(noRedirection); host.Show();
        var origin=new Native.Point(0,0); Native.ClientToScreen(host.Handle,ref origin);
        var bounds=new Native.Rect { Left=origin.X+40,Top=origin.Y+50,Right=origin.X+940,Bottom=origin.Y+840 };
        using var child=new NativePaintProbe(Console.WriteLine,()=>{},bounds);
        child.Shown+=(_,_)=> {
            if(!child.Attach(true,host)) { Console.Error.WriteLine(child.Error); app.Shutdown(1); return; }
            Console.WriteLine($"LOCAL_HOST noRedirection={noRedirection}; host={host.Handle:X}; ex={(long)Native.GetLong(host.Handle,Native.ExStyle):X}; {child.Evidence()}");
        };
        host.FormClosed+=(_,_)=> { child.Close(); app.Shutdown(); };
        child.FormClosed+=(_,_)=> { if(!host.IsDisposed)host.Close(); };
        child.Show();
        return app.Run();
    }
}
