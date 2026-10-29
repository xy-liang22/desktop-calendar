using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Interop;

namespace DesktopHostProbe;

internal sealed record CommandReply(long Id,string Command,bool Ok,string Mode,string Error);

internal static class Program
{
    [STAThread] static int Main(string[] args)
    {
        try
        {
            if(Array.IndexOf(args,"--software")>=0) RenderOptions.ProcessRenderMode=RenderMode.SoftwareOnly;
            var app=new Application { ShutdownMode=ShutdownMode.OnMainWindowClose };
            if(Array.IndexOf(args,"--self-test")>=0) return Checks.Run(app);
            if(Array.IndexOf(args,"--widget")>=0)
            {
                Console.OutputEncoding=new UTF8Encoding(false);
                void Report(string text) { Console.WriteLine(text); Console.Out.Flush(); }
                Report($"BOOT pid={Environment.ProcessId}; startup=normal; no desktop attach");
                var widget=new ProbeWindow(Report,Array.IndexOf(args,"--test-commands")>=0,Array.IndexOf(args,"--transparent")>=0);
                _=Task.Run(async ()=> {
                    try
                    {
                        string? line;
                        while((line=await Console.In.ReadLineAsync())!=null)
                        {
                            int split=line.IndexOf('|');
                            if(split<1 || !long.TryParse(line[..split],out long id)) { Report("PROTOCOL_ERROR invalid request"); continue; }
                            string command=line[(split+1)..];
                            await app.Dispatcher.InvokeAsync(()=> {
                                Report($"RECEIVED id={id} command={command}");
                                bool ok=false; string error="";
                                try { ok=widget.Command(command); error=ok?"":widget.LastError; }
                                catch(Exception ex) { error=ex.Message; }
                                Report("ACK "+JsonSerializer.Serialize(new CommandReply(id,command,ok,widget.ActiveMode,error)));
                                if(command=="exit") widget.Close();
                            });
                        }
                        if(!app.Dispatcher.HasShutdownStarted) await app.Dispatcher.InvokeAsync(()=>app.Shutdown());
                    }
                    catch(TaskCanceledException) {}
                    catch(IOException) { if(!app.Dispatcher.HasShutdownStarted) _=app.Dispatcher.BeginInvoke(()=>app.Shutdown()); }
                });
                return app.Run(widget); // WPF owns the complete initial Show/Loaded/ContentRendered sequence.
            }
            var controller=new Controller(Array.IndexOf(args,"--supervisor-test")>=0);
            app.Exit+=(_,e)=>e.ApplicationExitCode=controller.TestExitCode;
            return app.Run(controller);
        }
        catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

internal sealed class Controller : Window
{
    Process? child;
    ProbeState? state;
    bool closing,intentionalExit,recovered,rebuilding;
    readonly bool selfTest;
    long nextId;
    readonly Dictionary<long,TaskCompletionSource<CommandReply>> pending=new();
    readonly TextBlock summary=new() { TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8) };
    readonly TextBlock result=new() { TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8),Text="尚无控制命令；等待组件启动及渲染报告。" };
    readonly TextBox log=new() { IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
    readonly List<Button> experimentalButtons=new();
    readonly CheckBox software=new() { Content="软件渲染对照（仅新建 A/B 子进程生效；不改系统）",Margin=new Thickness(0,4,10,8) };
    bool transparentChild;
    readonly string logPath=Path.Combine(AppContext.BaseDirectory,"probe-session.log");
    readonly DispatcherTimer pulse=new() { Interval=TimeSpan.FromSeconds(3) };
    internal int TestExitCode { get; private set; }

    internal Controller(bool selfTest=false)
    {
        this.selfTest=selfTest;
        Title="Desktop Calendar — 实验控制台（保留找回入口）";
        Width=940; Height=920; Left=40; Top=40;
        var grid=new Grid { Margin=new Thickness(16) }; Content=grid;
        for(int i=0;i<4;i++) grid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition());
        var intro=new TextBlock { Text="本轮先测 E 原生普通窗口→输入、计数并确认→F 原生桌面嵌入；原 WPF 组件暂时隐藏。\n保留 A→确认→C、B→确认→D。应见红绿蓝色条、文字和控件；黑屏/消失时点 A 重建找回。\nA/B 重建会清空测试文字。软件选项只影响新建 A/B 的 WPF 渲染偏好，不影响原生 E/F。",TextWrapping=TextWrapping.Wrap };
        grid.Children.Add(intro);
        var buttons=new WrapPanel { Margin=new Thickness(0,10,0,0) }; Grid.SetRow(buttons,1); grid.Children.Add(buttons);
        void Add(string text,string command,bool gate=false)
        {
            var b=new Button { Content=text,Padding=new Thickness(10,7,10,7),Margin=new Thickness(0,0,8,8) };
            b.Click+=async (_,_)=>await Send(command); buttons.Children.Add(b);
            if(gate) { b.IsEnabled=false; experimentalButtons.Add(b); }
        }
        var rescue=new Button { Content="A 非透明顶层 / 重建找回",Padding=new Thickness(10),Margin=new Thickness(0,0,8,8) };
        rescue.Click+=async (_,_)=>await Rebuild(); buttons.Children.Add(rescue);
        var transparentButton=new Button { Content="B 透明顶层 / 重建",Padding=new Thickness(10),Margin=new Thickness(0,0,8,8) };
        transparentButton.Click+=async (_,_)=>await Rebuild(true,true); buttons.Children.Add(transparentButton);
        Add("恢复当前顶层","normal"); Add("C 非透明嵌入","desktop",true); experimentalButtons[^1].Tag=false;
        Add("D 透明嵌入","desktop",true); experimentalButtons[^1].Tag=true;
        Add("E 原生普通窗口 / 返回 E","native-normal");
        Add("F 原生桌面嵌入","native-desktop",true); experimentalButtons[^1].Tag="native";
        Add("WorkerW 对照","wallpaper",true); Add("普通置底对照","bottom",true);
        Add("始终置顶","topmost"); Add("下一显示器","next-monitor"); Add("完整诊断","snapshot");
        Add("模拟宿主丢失","simulate-loss");
        Add("采样 WPF 内容（非截图）","sample");
        foreach(string observation in new[]{"可见可操作","黑屏","不可见"}) {
            var b=new Button { Content="记录："+observation,Padding=new Thickness(8),Margin=new Thickness(0,0,8,8) };
            b.Click+=(_,_)=> { string message=$"USER_OBSERVATION case={state?.ActiveCase} mode={state?.ActiveMode} render={state?.RenderPreference} pid={state?.Pid}: {observation}"; Append(message); result.Text=message; };
            buttons.Children.Add(b);
        }
        buttons.Children.Add(software);
        var exit=new Button { Content="退出整个实验",Padding=new Thickness(10),Margin=new Thickness(0,0,8,8) };
        exit.Click+=(_,_)=>Close(); buttons.Children.Add(exit);
        var details=new ScrollViewer { Content=summary,MaxHeight=245,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        Grid.SetRow(details,2); grid.Children.Add(details);
        Grid.SetRow(result,3); grid.Children.Add(result);
        Grid.SetRow(log,4); grid.Children.Add(log);
        Loaded+=async (_,_)=> {
            Append("A–D 渲染对照：先 A 顶层，用户确认后嵌入。状态与离屏采样均不证明屏幕可见。");
            await Start(); pulse.Start();
            if(selfTest) await TestSupervisor();
        };
        pulse.Tick+=async (_,_)=> { if(!closing && !rebuilding && pending.Count==0 && child is { HasExited:false }) await Send("snapshot",false); };
        Closing+=async (_,e)=> {
            if(closing) return;
            e.Cancel=true; closing=true; pulse.Stop(); await StopChild();
            _=Dispatcher.BeginInvoke(Close);
        };
    }
    void Append(string text)
    {
        string line=$"{DateTimeOffset.Now:HH:mm:ss} {text}";
        if(log.Text.Length>26000) log.Text=log.Text[^12000..];
        log.AppendText(line+Environment.NewLine); log.ScrollToEnd();
        try {
            if(File.Exists(logPath)&&new FileInfo(logPath).Length>1024*1024) File.WriteAllText(logPath,"Old diagnostic log truncated.\n");
            File.AppendAllText(logPath,line+Environment.NewLine,Encoding.UTF8);
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { result.Text="诊断日志无法写入："+ex.Message; }
    }
    void UpdateSummary()
    {
        string process=child==null?"未启动":child.HasExited?$"已退出 PID={child.Id}":$"存活 PID={child.Id}";
        summary.Text=$"进程：{process}\n当前观察目标：{state?.ActiveCase} / {state?.ActiveMode}\n原生独立对照：{state?.NativeEvidence}\n以下为 WPF 窗口诊断（E/F 期间有意隐藏）：\n"+
            (state==null?"HWND/WPF：尚无组件报告":$"HWND={state.Hwnd} | 模式={state.Mode} | WPF Loaded={state.Loaded} / ContentRendered={state.ContentRendered}\n内容={state.ContentWidth:0}×{state.ContentHeight:0} | parent={state.Parent} / host={state.Host} / 匹配={state.ParentMatches}\nWS_VISIBLE={state.NativeVisible} / WPF Visible={state.WpfVisible} / 最小化={state.Minimized} / DWM cloaked={state.Cloaked}\n矩形={state.Bounds} / DPI={state.Dpi}\n遮挡采样：{state.Coverage}\n错误：{(state.Error==""?"无已报告错误":state.Error)}")+
            (state==null?"":$"\n实际案例={state.Case} / 请求模式={state.RequestedMode} / AllowsTransparency={state.Transparent}\nSTYLE={state.Style} EX={state.ExStyle} | CHILD={state.Child} POPUP={state.Popup} LAYERED={state.Layered}\nclient={state.ClientBounds}; {state.HostIntersection}\n渲染偏好={state.RenderPreference} HWND偏好={state.HwndRenderMode} tier={state.RenderTier} | RemoteSession={state.RemoteSession} RDP加速配置={state.RdpAcceleration}\n{state.SurfaceEvidence}")+
            "\n人工可见性："+(state?.HumanConfirmed==true?"用户已确认当前顶层；嵌入仍需另验。":"未确认，不能据以上状态判为可见。");
        foreach(var b in experimentalButtons) b.IsEnabled=!rebuilding && (b.Tag is string
            ?state is { NativeConfirmed:true }
            :state is { HumanConfirmed:true,ContentRendered:true,NativeEvidence:"inactive" } && (b.Tag is not bool expected || expected==state.Transparent));
    }
    void Receive(Process process,string line)
    {
        if(process!=child || closing) return;
        try {
            if(line.StartsWith("STATE ")) { state=JsonSerializer.Deserialize<ProbeState>(line[6..]); UpdateSummary(); }
            else if(line.StartsWith("ACK "))
            {
                var reply=JsonSerializer.Deserialize<CommandReply>(line[4..])!;
                if(pending.TryGetValue(reply.Id,out var waiter)) waiter.TrySetResult(reply);
            }
            else if(line=="USER_EXIT") intentionalExit=true;
            Append(line);
        }
        catch(JsonException ex) { result.Text="组件诊断格式错误："+ex.Message; Append("PROTOCOL_ERROR "+ex.Message); }
    }
    async Task Start()
    {
        intentionalExit=false; state=null;
        var process=new Process {
            StartInfo=new ProcessStartInfo(Environment.ProcessPath!,"--widget"+(selfTest?" --test-commands":"")+(transparentChild?" --transparent":"")+(software.IsChecked==true?" --software":"")) {
                UseShellExecute=false,WindowStyle=ProcessWindowStyle.Normal,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
                StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8 },
            EnableRaisingEvents=true
        };
        child=process;
        process.OutputDataReceived+=(_,e)=> { if(e.Data!=null) _=Dispatcher.BeginInvoke(()=>Receive(process,e.Data)); };
        process.ErrorDataReceived+=(_,e)=> { if(e.Data!=null) _=Dispatcher.BeginInvoke(()=> { if(process==child) { result.Text="子进程错误："+e.Data; Append(result.Text); } }); };
        process.Exited+=(_,_)=>_=Dispatcher.BeginInvoke(async ()=> {
            await Task.Delay(250);
            if(process!=child || closing || rebuilding) return;
            UpdateSummary(); Append($"PROCESS_EXIT pid={process.Id} code={process.ExitCode}");
            if(!intentionalExit && !recovered) { recovered=true; Append("意外退出：只重建一次普通窗口，不自动嵌入。"); await Rebuild(false); }
        });
        try {
            if(!process.Start()) throw new InvalidOperationException("Process.Start 返回 false");
            process.BeginOutputReadLine(); process.BeginErrorReadLine();
            Append($"PROCESS_STARTED pid={process.Id}; waiting for HWND_CREATED / WPF_LOADED / CONTENT_RENDERED");
            UpdateSummary();
            await Wait(()=>state is { ContentRendered:true,Loaded:true } || process.HasExited,10000);
            if(process!=child) return;
            if(state is not { ContentRendered:true,Loaded:true }) throw new InvalidOperationException("组件在首次内容渲染前退出");
            result.Text="组件已报告 WPF 内容渲染；请亲眼检查并操作浅色普通窗口。这不是屏幕可见性认证。";
        }
        catch(Exception ex) { result.Text="启动/渲染未完成："+ex.Message+"；可点“重建普通窗口 / 找回”。"; Append(result.Text); }
    }
    async Task<CommandReply?> Send(string command,bool showResult=true)
    {
        if(child is not { HasExited:false } process) { if(showResult) result.Text="组件未运行；请重建普通窗口。"; return null; }
        long id=++nextId; var waiter=new TaskCompletionSource<CommandReply>(TaskCreationOptions.RunContinuationsAsynchronously); pending[id]=waiter;
        try {
            if(showResult) result.Text=$"发送 #{id} {command}，等待组件确认…";
            Append($"SEND id={id} command={command}");
            await process.StandardInput.WriteLineAsync($"{id}|{command}").WaitAsync(TimeSpan.FromSeconds(2));
            await process.StandardInput.FlushAsync().WaitAsync(TimeSpan.FromSeconds(2));
            var reply=await waiter.Task.WaitAsync(TimeSpan.FromSeconds(8));
            if(showResult) result.Text=$"#{id} {command}: {(reply.Ok?"已执行（非屏幕可见性证明）":"失败")}；mode={reply.Mode}；{reply.Error}";
            return reply;
        }
        catch(Exception ex) when(ex is IOException or InvalidOperationException or TimeoutException) {
            result.Text=$"#{id} {command} 未收到成功确认：{ex.Message}。请重建普通窗口。"; Append(result.Text); return null;
        }
        finally { pending.Remove(id); }
    }
    async Task StopChild()
    {
        if(child is not { HasExited:false } process) return;
        intentionalExit=true;
        try {
            await process.StandardInput.WriteLineAsync($"{++nextId}|exit").WaitAsync(TimeSpan.FromSeconds(1));
            process.StandardInput.Close();
            await Task.WhenAny(process.WaitForExitAsync(),Task.Delay(2500));
        }
        catch(Exception ex) when(ex is IOException or InvalidOperationException or TimeoutException) { Append("STOP "+ex.Message); }
        if(!process.HasExited) {
            Append($"结束本控制台启动的实验组件 PID={process.Id}；不操作 Explorer。");
            process.Kill(); await process.WaitForExitAsync();
        }
    }
    async Task Rebuild(bool explicitRequest=true,bool transparent=false)
    {
        if(rebuilding || closing) return;
        rebuilding=true;
        try {
            if(explicitRequest) recovered=false;
            result.Text="正在销毁旧测试实例并新建普通窗口；测试文字清空。";
            await StopChild();
            transparentChild=transparent;
            await Start();
        }
        finally { rebuilding=false; UpdateSummary(); }
    }
    static async Task Wait(Func<bool> condition,int ms=6000)
    {
        for(int i=0;i<ms/100;i++) { if(condition())return; await Task.Delay(100); }
        throw new TimeoutException("等待状态超时");
    }
    async Task TestSupervisor()
    {
        var results=new List<string>(); int failures=0;
        void Check(bool ok,string name) { results.Add((ok?"PASS ":"FAIL ")+name); if(!ok) failures++; }
        try {
            Check(state is { Mode:"normal",ContentRendered:true,Loaded:true,Parent:"0",HumanConfirmed:false },"new child reports ordinary HWND + loaded content + rendered, without claiming human visibility");
            int first=child!.Id;
            var ack=await Send("snapshot");
            Check(ack is { Ok:true,Command:"snapshot" },"command ID round-trip acknowledged by child");
            var bad=await Send("unknown-test-command");
            Check(bad is { Ok:false } && bad.Error!="","unknown command has explicit error acknowledgement");
            await Rebuild(true,true); await Wait(()=>state is { ContentRendered:true,Case:"B" });
            Check(state is { Transparent:true,Layered:true,HumanConfirmed:false } && child!.Id!=first,"B selector starts fresh layered top-level without carrying human confirmation");
            software.IsChecked=true; await Rebuild(true,true); await Wait(()=>state is { ContentRendered:true,Case:"B" });
            Check(state?.RenderPreference=="SoftwareOnly","software option affects only new test child process");
            var sample=await Send("sample");
            Check(sample is { Ok:true } && state!.SurfaceEvidence.StartsWith("offscreen="),"explicit content sample returns acknowledgement and labels off-screen evidence");
            var native=await Send("native-normal");
            Check(native is { Ok:true,Mode:"native-normal" } && state is { ActiveCase:"E",WpfVisible:false } && state.NativeEvidence.Contains("HWND="),"E opens separate native HWND and labels hidden WPF diagnostics");
            var embedded=await Send("native-desktop");
            Check(embedded!=null && (embedded.Ok?state is { ActiveCase:"F",ActiveMode:"native-desktop" }:state is { ActiveCase:"E" }),"F command acknowledges actual native attachment or explicit normal fallback");
            var normalReturn=await Send("normal");
            Check(normalReturn is { Ok:true } && state is { NativeEvidence:"inactive",WpfVisible:true,ActiveCase:"B" },"normal command closes native control and restores WPF baseline");
            software.IsChecked=false;
            await Rebuild(); await Wait(()=>state is { ContentRendered:true,Mode:"normal" });
            Check(child!.Id!=first && state is { Parent:"0",HumanConfirmed:false },"rescue starts fresh non-embedded process and resets human gate");
            int second=child!.Id;
            _=Send("simulate-destroy",false);
            await Wait(()=>child!.Id!=second && state is { Mode:"normal",ContentRendered:true },10000);
            Check(recovered,"unexpected window loss gets one ordinary restart");
            int third=child!.Id;
            _=Send("simulate-destroy",false);
            await Wait(()=>child!.HasExited); await Task.Delay(500);
            Check(child!.Id==third,"second unexpected exit does not restart endlessly");
            await Rebuild(); await Wait(()=>state is { Mode:"normal",ContentRendered:true });
            var quit=await Send("exit"); await Wait(()=>child!.HasExited); await Task.Delay(500);
            Check(quit is { Ok:true,Command:"exit" } && intentionalExit,"normal exit acknowledged before closing and does not respawn");
        }
        catch(Exception ex) { failures++; results.Add("FAIL "+ex); }
        results.Add($"RESULT {results.Count-failures} PASS, {failures} FAIL; on-screen visibility still requires user");
        string dir=Path.Combine(Environment.CurrentDirectory,"artifacts"); Directory.CreateDirectory(dir);
        File.WriteAllLines(Path.Combine(dir,"p1.1-2-supervisor-checks.txt"),results);
        TestExitCode=failures==0?0:1; Close();
    }
}
