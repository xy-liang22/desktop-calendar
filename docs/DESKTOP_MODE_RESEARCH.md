# P1.1-2：独立桌面宿主实验与技术结论

日期：2026-10-29（Asia/Shanghai）。状态：**实验程序与自动验证已交付，人工验收待反馈；不批准主窗口整合。**

## 结论与边界

当前最值得继续人工验证的候选是：将独立 WPF 组件作为现存 Explorer 图标宿主（Progman 或含 `SHELLDLL_DefView` 的 WorkerW）的子窗口，放在该宿主内的前方，并保留独立控制台和普通窗口回退。它在本机确实建立了父子 HWND 关系，不是仅隐藏任务栏按钮。

**尚无证据证明任何候选已满足全部 Windows 10/11 桌面组件要求。现在不能安全地直接整合主窗口。** 本机自动检查确认父子关系、样式、模式转换、位置/内存输入保留及普通测试窗口的覆盖命中；无法替代 Win+D 后像素可见、真实输入、桌面图标及右键的人工测试。电脑控制工具连接失败，未完成这些实机交互。

若此候选人工测试失败，最稳妥的交付回退是普通非置顶工具窗口，并明确告知它不保证 Win+D 后可见。是否接受该产品妥协仍需用户确认，不能悄悄替换桌面目标。默认置顶或定时抢焦点不作为替代。

## 方案对比

| 方案 | Win+D / 普通应用覆盖 | 输入、拖动、缩放及桌面图标 | 系统与 DPI / 恢复 | 本阶段决定 |
|---|---|---|---|---|
| 现存图标宿主的子窗口，组件位于 DefView 前方 | 理论上随桌面保留；本机父子关系通过，Win+D 待人工；普通测试窗口覆盖命中通过 | 已有测试控件和八方向缩放；真实输入待验。组件矩形内会遮住其下图标，矩形外及透明角命中待验，不能称完全不影响图标 | 依赖未公开的 Explorer 窗口结构；本机 26200 同 DPI 双屏定位通过，Win10/混合 DPI 未验；宿主失效回退 | **推荐继续实验，不推荐立即整合** |
| 壁纸 WorkerW（通常位于图标之后） | 可能随桌面保留，普通应用可盖住；不是公开契约 | 图标层可能先收到输入；可渲染壁纸不等于能可靠输入文字。不能用全局钩子转发鼠标冒充可交互组件 | Explorer 布局随 Windows 更新变化；跨进程 DPI 及重启同样有风险 | 本机未找到符合条件的现存宿主，尝试后明确回退；未验证成功 |
| 普通顶层工具窗口 + 单次 HWND_BOTTOM | 不保证 Win+D 可见；普通应用可覆盖 | WPF 常规输入最直接；激活时层级可能变化，不能保证永远位于所有普通窗口下方 | 使用公开窗口 API，不依赖 Explorer 子窗口结构，原生 DPI 路径更简单 | 已提供“普通置底对照”，不能标为真正桌面模式 |
| 普通非置顶窗口（安全回退） | Win+D 可隐藏/覆盖它；普通应用可覆盖 | 常规交互，控制台可找回并退出 | 与 Explorer 生命周期分离，风险最低；无法满足全部桌面目标 | **可靠回退候选**，产品妥协须确认 |
| 始终置顶窗口 | 通常能留在上层，但普通应用无法覆盖 | 输入直接，但违反默认桌面语义 | 公开 API，不依赖 Explorer 嵌入 | 仅作为用户主动选择的模式 |
| IDesktopWallpaper / Windows Widgets / AppBar | 壁纸接口管理背景；Widgets 属于 Windows 11 面板；AppBar 是边缘停靠/工作区用途 | 均不能直接替代本需求的自由 WPF 桌面编辑组件 | Widgets 不覆盖 Win10 目标；AppBar 改变工作区语义 | 不实现，不修改壁纸或工作区 |

“普通置底”只是一次层级设置，不包含持续压底循环；未使用焦点劫持、周期置顶或 Win+D 后自动恢复伪装组件。

## 官方依据与推论

- [SetParent](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent)：父子关系与窗口样式需分别处理；不同 DPI awareness 的跨进程挂载存在 DPI 重置风险。实验在调用前检查双方 context，不相等即拒绝。此 API 公开不代表 Explorer 的宿主结构受支持。
- [SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)：TOPMOST、NOTOPMOST、BOTTOM 定义的是窗口 Z 序；据此推论单次置底并不提供桌面成员身份。本实验只在用户切换/移动时调整位置，无常驻层级强制循环。
- [Extended Window Styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles)：TOOLWINDOW 与 APPWINDOW 影响任务栏和任务切换表现，不是桌面层 API。自动检查样式，实际任务栏/Alt+Tab 仍待人工。
- [High DPI desktop development](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)：父子窗口树的 DPI 一致性限制，说明跨进程嵌入不能按普通顶层窗口推定混合 DPI 可靠。尤其全虚拟桌面的宿主可能固定在一个 DPI，子组件跨屏后是否清晰正确需实测。
- [Microsoft：Show Desktop 与 Minimize All](https://devblogs.microsoft.com/oldnewthing/20040527-00/?p=39153)：这是历史行为解释，不是现代所有版本的保证；支持“无最小化按钮/隐藏任务栏不等于 Win+D 后仍在”的判断。
- [.NET WPF Window 源码](https://source.dot.net/PresentationFramework/System/Windows/Window.cs.html)：已有对 owner 从零重复设为零可能返回错误 1400 的说明；实验在原生 owner 清理前判空，避免误判后关闭窗口。
- [Lively 项目维护者发布记录](https://github.com/rocksdanister/lively/releases/tag/v2.2.0.0)：维护者报告为 Windows 11 24H2 重写核心以适配平台变化。这是兼容性风险的具体证据，不能当作本 WPF 组件已通过的证据；未复制其代码或安装该软件。
- [IDesktopWallpaper](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-idesktopwallpaper)、[Windows Widgets](https://learn.microsoft.com/en-us/windows/apps/design/widgets)、[Application Desktop Toolbars](https://learn.microsoft.com/en-us/windows/win32/shell/application-desktop-toolbars)：用于区分背景、Widgets 面板及停靠工具栏与本项目的桌面组件需求。

## 实现与安全限制

- 目录 `experiments/DesktopHostProbe/`，独立 .NET 10 WPF 可执行程序，无第三方 NuGet。主项目仅排除实验目录的编译/资源输入，主程序窗口源码不变。
- 默认启动控制台及一个组件子进程。**控制台故意保留任务栏/Alt+Tab 入口**，不是要验收的小组件；小组件使用 WS_CHILD/WS_EX_TOOLWINDOW 并去掉 APPWINDOW。退出控制台会退出本次启动的组件；组件不响应时，仅结束该组件进程，不结束 Explorer。
- 桌面宿主只从当前 Shell 进程中的 Progman/WorkerW 查找，并要求包含 DefView；壁纸候选排除含图标视图的 WorkerW。只使用现存窗口；没有发送未公开的 `0x052C`、没有隐藏或改写任何 Explorer 窗口样式。
- 挂载依赖内部窗口类/结构。独立进程限制影响范围，不意味着内部机制可靠。默认实验失败时进入普通回退，日志显示原因；不改注册表、Snap、任务栏、壁纸、全局设置或启动项。
- 桌面模式与置顶模式在同一个组件 HWND 上切换，保留本次运行中的文本及物理矩形。控件只用于测试，不存用户数据，不写业务 schema。
- 宿主句柄/进程/父子关系变化时，组件尝试脱离并回退；显示变化、恢复通知也触发检查。若整个组件窗口被销毁，独立控制台只自动重建一次普通回退实例；**这一路径不保留组件内的输入、位置或尺寸**，不能直接用于真实业务数据。用户可明确点“安全回退 / 找回”再次启动。
- 控制台通过匿名标准输入/输出管道与自己创建的子进程通信，无网络监听、无全局键盘/鼠标钩子。心跳只记录状态，不抢焦点、不自动重挂。控制台退出/管道 EOF 结束组件。
- 诊断只记窗口状态、输入字符数和点击计数，不记录输入正文；运行日志位于实验 `bin/.../probe-session.log`，超过 1 MiB 截断旧诊断，自动测试报告位于根 `artifacts/`。这些均被忽略提交。
- 未自动执行 Explorer 重启、休眠、系统 DPI 设置变更，也未测量长期资源消耗。恢复机制有模拟验证，不代表这些实机场景已验收。

## 启动和人工验收

从项目根目录 Windows PowerShell 启动（离线构建，不安装）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\experiments\DesktopHostProbe\Start-Probe.ps1
```

已构建时可直接双击 `experiments\DesktopHostProbe\bin\Release\net10.0-windows\DesktopHostProbe.exe`。不启动原日历主程序。关闭旧实验再重新构建，避免文件占用；不要重复启动多个控制台。

1. **桌面和覆盖**：确认状态为 `mode=desktop`，最小化控制台，手动按 Win+D，检查小组件仍可见；打开普通应用盖住组件，再回桌面。区分控制台的任务栏按钮与组件，检查 Alt+Tab。点击组件矩形外图标，右键空白桌面；组件矩形内遮挡图标是否可接受请反馈。
2. **输入和尺寸**：点计数按钮、输入无隐私测试文字（含中文输入法）、选择/复制文字；拖顶部移动，拖四边四角缩放。检查焦点、光标、透明角和右键菜单有无异常。自动输入赋值不替代这项。
3. **模式往返与找回**：记录位置/大小/文字，点“始终置顶”，确认普通应用不能盖住；点“返回桌面”，确认可被覆盖且状态保留。通过控制台试“普通置底对照”再 Win+D；再试“安全回退 / 找回”。“WorkerW 壁纸对照”本机预期提示无宿主并回退。
4. **恢复**：可先点“模拟宿主丢失”，确认进入普通回退；这不是 Explorer 重启。实际重启仅由你操作：保留控制台，回到桌面模式，在任务管理器选“Windows 资源管理器 → 重新启动”。观察控制台是否仍在，组件是否回退/可找回、图标和任务栏是否恢复；不要求它自动重新嵌入。测试后点“退出整个实验”确认两个进程都结束。
5. **显示器和休眠**：用“下一显示器”分别放置，再实际拖动跨屏；有混合 DPI 环境时检查尺寸、字体清晰度、鼠标命中和圆角。由你手动休眠/唤醒，再检查找回、输入及退出。没有对应硬件/Win10 时标记未验证，不为本次测试擅自更改系统缩放。

反馈请包含：方案按钮、Windows build、各显示器 DPI、操作顺序、实际现象。不要提交桌面截图、输入正文或私人日志到 Git。详细自动结果与验收状态见 [TESTING](TESTING.md)。

## 整合门槛

必须先确认：Win+D 可见且输入稳定；图标/右键影响可接受；桌面/置顶往返无丢失；Win10/Win11 和混合 DPI 不出现不可接受退化；Explorer 重启/休眠可找回和退出。若失败，先复现和说明，再决定候选调整或用户批准的降级，不自行进入 P1.1-3。最终实现还需可靠状态存储与恢复方案，本实验的一次性进程恢复不能替代它。
