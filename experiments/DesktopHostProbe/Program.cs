using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace DesktopHostProbe;

internal static class Program
{
    [STAThread] static int Main(string[] args)
    {
        var app=new Application { ShutdownMode=ShutdownMode.OnMainWindowClose };
        app.Exit+=(_,e)=> { if(Array.IndexOf(args,"--supervisor-test")>=0) e.ApplicationExitCode=Environment.ExitCode; };
        if(Array.IndexOf(args,"--self-test")>=0) return Checks.Run(app);
        if(Array.IndexOf(args,"--widget")>=0)
        {
            Console.OutputEncoding=new UTF8Encoding(false);
            var widget=new ProbeWindow(s=>Console.WriteLine(s));
            widget.Loaded+=async (_,_)=> {
                Console.WriteLine(Native.Inventory());
                widget.Switch(Array.IndexOf(args,"--fallback")>=0?"fallback":"desktop");
                // EOF also ends the child if its controller disappears.
                await Task.Run(async ()=> {
                    string? line;
                    while((line=await Console.In.ReadLineAsync())!=null) await app.Dispatcher.InvokeAsync(()=>widget.Command(line));
                    await app.Dispatcher.InvokeAsync(()=>app.Shutdown());
                });
            };
            return app.Run(widget);
        }
        return app.Run(new Controller(Array.IndexOf(args,"--supervisor-test")>=0));
    }
}

internal sealed class Controller : Window
{
    Process? child;
    bool closing,intentionalExit,recovered;
    string latestMode="";
    readonly TextBox log = new() { IsReadOnly=true, AcceptsReturn=true, TextWrapping=TextWrapping.Wrap, VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
    readonly string logPath=Path.Combine(AppContext.BaseDirectory,"probe-session.log");
    readonly DispatcherTimer pulse=new() { Interval=TimeSpan.FromSeconds(2) };
    internal Controller(bool selfTest=false)
    {
        Title="Desktop Calendar — 实验控制台（保留找回入口）";
        Width=690; Height=510; Left=60; Top=100;
        var grid=new Grid { Margin=new Thickness(16) }; Content=grid;
        grid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.Children.Add(new TextBlock { Text="控制台是独立普通窗口；不代表组件任务栏行为。\n请手动测试 Win+D、Explorer 重启和混合 DPI；不输入个人资料。", TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,10) });
        var buttons=new WrapPanel(); Grid.SetRow(buttons,1); grid.Children.Add(buttons);
        void Add(string label,string command) { var b=new Button { Content=label, Padding=new Thickness(10,6,10,6), Margin=new Thickness(0,0,8,8) }; b.Click+=(_,_)=>Send(command); buttons.Children.Add(b); }
        Add("桌面宿主（默认）","desktop"); Add("WorkerW 壁纸对照","wallpaper"); Add("普通置底对照","bottom");
        Add("始终置顶","topmost"); Add("安全回退 / 找回","fallback"); Add("下一显示器","next-monitor");
        Add("记录状态","snapshot"); Add("模拟宿主丢失","simulate-loss");
        var exit=new Button { Content="退出整个实验", Padding=new Thickness(10,6,10,6), Margin=new Thickness(0,0,8,8) }; exit.Click+=(_,_)=>Close(); buttons.Children.Add(exit);
        Grid.SetRow(log,2); grid.Children.Add(log);
        Loaded+=async (_,_)=> { Append("日志仅含窗口状态，不记录输入正文。" ); Start(false); pulse.Start(); if(selfTest) await TestSupervisor(); };
        pulse.Tick+=(_,_)=>Send("snapshot",false);
        Closing+=async (_,e)=> {
            if(closing) return;
            e.Cancel=true;
            closing=true; pulse.Stop();
            if(child is { HasExited:false } process)
            {
                try
                {
                    process.StandardInput.WriteLine("exit"); process.StandardInput.Close();
                    await Task.WhenAny(process.WaitForExitAsync(),Task.Delay(3000));
                    if(!process.HasExited)
                    {
                        // Only our own launched test child; never Explorer or another application.
                        Append("实验组件未响应退出，结束本控制台启动的组件进程。"); process.Kill(); await process.WaitForExitAsync();
                    }
                }
                catch(InvalidOperationException) {} catch(IOException) { if(!process.HasExited) process.Kill(); }
            }
            _=Dispatcher.BeginInvoke(Close); // do not reenter Close while its Closing event is still running
        };
    }
    void Append(string text)
    {
        string line=$"{DateTimeOffset.Now:HH:mm:ss} {text}";
        if(log.Text.Length>24000) log.Text=log.Text[^12000..];
        log.AppendText(line+Environment.NewLine); log.ScrollToEnd();
        try
        {
            if(File.Exists(logPath) && new FileInfo(logPath).Length>1024*1024) File.WriteAllText(logPath,"Log capped at 1 MiB; older diagnostics removed.\n");
            File.AppendAllText(logPath,line+Environment.NewLine,Encoding.UTF8);
        }
        catch(IOException) { /* UI remains available if diagnostic storage fails. */ }
    }
    void Start(bool fallback)
    {
        intentionalExit=false;
        latestMode="";
        var start=new ProcessStartInfo(Environment.ProcessPath!,"--widget"+(fallback?" --fallback":"")) {
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
            StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8 };
        var process=new Process { StartInfo=start,EnableRaisingEvents=true }; child=process;
        process.OutputDataReceived+=(_,e)=> { if(e.Data!=null) Dispatcher.BeginInvoke(()=> {
            if(process!=child)return;
            if(e.Data=="USER_EXIT") intentionalExit=true;
            if(e.Data.StartsWith("mode mode=") || e.Data.StartsWith("safe-fallback mode=")) latestMode=e.Data.Contains("mode=fallback")?"fallback":"other";
            Append(e.Data);
        }); };
        process.ErrorDataReceived+=(_,e)=> { if(e.Data!=null) Dispatcher.BeginInvoke(()=>Append("CHILD_ERROR "+e.Data)); };
        process.Exited+=(_,_)=>Dispatcher.BeginInvoke(async ()=> {
            await Task.Delay(250); // allow final stdout notification to drain
            if(closing || process!=child) return;
            Append($"组件进程已结束 code={process.ExitCode}");
            if(!intentionalExit && !recovered) { recovered=true; Append("只自动重建一次普通回退窗口；不自动重挂 Explorer。输入内容不恢复。"); Start(true); }
        });
        try { process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine(); Append($"组件 PID={process.Id}"); }
        catch(Exception ex) { Append("启动失败："+ex.Message); child=null; }
    }
    void Send(string command,bool restart=true)
    {
        if(child==null || child.HasExited)
        {
            if(restart) { recovered=false; Start(true); Append("已启动安全窗口；请再次选择所需模式。"); }
            return;
        }
        try { child.StandardInput.WriteLine(command); child.StandardInput.Flush(); }
        catch(IOException ex) { Append("控制通道已关闭："+ex.Message); }
    }
    async Task TestSupervisor()
    {
        var results=new System.Collections.Generic.List<string>(); int failures=0;
        async Task Wait(Func<bool> condition)
        {
            for(int i=0;i<60;i++) { if(condition())return; await Task.Delay(100); }
            throw new TimeoutException("Supervisor test timeout");
        }
        void Check(bool ok,string name) { results.Add((ok?"PASS ":"FAIL ")+name); if(!ok)failures++; }
        try
        {
            await Wait(()=>latestMode!=""); int first=child!.Id;
            Send("simulate-destroy");
            await Wait(()=>child!=null && child.Id!=first && latestMode=="fallback");
            Check(recovered && child!.Id!=first,"unexpected child window loss starts one independent fallback process");
            int second=child!.Id; Send("simulate-destroy");
            await Wait(()=>child!.HasExited); await Task.Delay(700);
            Check(child!.Id==second && child.HasExited,"second loss does not cause respawn loop");
            Send("fallback"); await Wait(()=>latestMode=="fallback" && child!=null && !child.HasExited);
            Check(child!.Id!=second,"explicit rescue restarts a reachable ordinary window");
            int third=child!.Id; Send("exit");
            await Wait(()=>child!.HasExited); await Task.Delay(700);
            Check(child!.Id==third && intentionalExit,"normal child exit does not trigger automatic restart");
        }
        catch(Exception ex) { failures++; results.Add("FAIL "+ex); }
        results.Add($"RESULT {results.Count-failures} PASS, {failures} FAIL; simulation only, not Explorer restart");
        string dir=Path.Combine(Environment.CurrentDirectory,"artifacts"); Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir,"p1.1-2-supervisor-checks.txt"),results);
        Environment.ExitCode=failures==0?0:1;
        Close();
    }
}
