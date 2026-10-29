# P1.1-2：桌面宿主实验与测试平台修复

更新：2026-10-30（Asia/Shanghai）。**初版因组件不可见，人工验收未通过。新版普通诊断组件已由用户确认可见、输入和计数可用；桌面嵌入仍未验收，不批准主窗口整合。**

## 当前结论

先修复可观察、可操作、可找回的测试平台，再判断桌面方案。当前启动时完全使用普通 WPF 顶层窗口，不直接嵌入 Explorer；有系统标题栏、缩放边框、任务栏入口及不透明浅色内容。首次 Show、Loaded、ContentRendered 交由 WPF 完成，启动时不重写原生窗口样式。

用户在本次对话回复“能看到，输入和计数可用”。这证明新版普通窗口的这三项体验已通过人工检查，**不证明嵌入后仍然可见、可输入或 Win+D 后可见**。原先的 30 项自动检查不能推翻初版不可见的人工反馈。

现存 Explorer 图标宿主仍保留为待验证候选，尚不能称最可靠的最终桌面实现。普通窗口是测试与安全回退基线；是否接受它作为最终产品降级方案仍需用户确认，不悄悄替换默认桌面目标。当前普通诊断使用不透明渲染，**不代表主程序透明圆角渲染已通过宿主兼容性验证**。

## 不可见问题：已知与未知

旧平台在 Loaded 阶段即修改原生样式并 SetParent，没有记录 ContentRendered；窗口默认不进任务栏、不激活，找回仍复用同一透明 HWND。控制台只显示原生可见位、父句柄及模式，缺少逐条命令确认。这些是已确认的排障和恢复缺陷。

旧证据不足以将不可见唯一归因于某一种原因：父窗口裁剪、其他窗口遮挡、透明合成和初始化时序都不能仅凭原日志排除。本次通过普通不透明窗口、完整初始化记录和新进程重建路径解决测试入口与恢复路径问题，不宣称已找到或修复全部桌面嵌入渲染根因。

[IsWindowVisible 官方说明](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-iswindowvisible)明确指出，返回非零时窗口仍可能被完全遮挡或被父窗口裁剪。[ContentRendered](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.contentrendered?view=windowsdesktop-10.0)提供 WPF 内容渲染事件，仍不等于用户屏幕的可见性证明。

## 方案比较（候选结论，不是兼容保证）

| 方案 | Win+D / 应用覆盖 | 输入与桌面图标 | 多屏/DPI/恢复及兼容 | 当前决定 |
|---|---|---|---|---|
| 现存 Progman / 含 DefView 的 WorkerW 前方子窗口 | 可能随桌面保留；需要真实验证。原生父子关系不能证明可见 | 组件区域可能遮图标；嵌入后焦点、输入、右键待验 | 依赖 Explorer 内部结构；DPI 不一致拒绝挂载；失效回退 | 保留候选，不直接整合 |
| 图标后方的壁纸 WorkerW | 可能随桌面保留，普通应用可盖住 | 图标层可能先接收输入；能播放壁纸不代表可编辑文字 | 结构有版本差异；本机未找到符合条件的现存宿主 | 不发送内部消息生成宿主，缺失时回退；未验收 |
| 普通顶层窗口 + 单次 HWND_BOTTOM | 不保证 Win+D 可见；激活后层级可能变化 | 标准输入，不持续抢层级 | 使用公开 API，不依赖 Explorer 子窗口 | 仅作对照；人工确认普通窗口后才开放 |
| 普通非置顶诊断/回退窗口 | Win+D 可隐藏/覆盖它，普通应用可覆盖 | 新版可见、输入、计数已由用户确认；拖动缩放另验 | 生命周期与 Explorer 分离，新进程可重新创建 | 当前默认测试入口及安全回退，不冒充桌面模式 |
| 始终置顶 | 普通应用不能盖住，不符合默认桌面语义 | 用户主动选择时可用 | 公开窗口 API | 保留实验模式 |
| IDesktopWallpaper / Windows Widgets / AppBar | 壁纸、Win11 面板或边缘停靠用途 | 不能直接替代自由 WPF 编辑组件 | Widgets 不覆盖 Win10 目标；AppBar 有工作区语义 | 不实现，不改壁纸或工作区 |

## 实现与安全约束

- 独立目录 `experiments/DesktopHostProbe/`，现有 .NET 10 WPF，无第三方依赖。本次不修改主程序的源码或构建配置。
- 默认两个普通窗口：控制台与组件子进程。组件浅色、不透明、标准标题栏；这是诊断选择，不改变最终产品的深色半透明要求。
- 先在组件输入文字、点计数，再点击“我已看见并操作组件，允许桌面实验”。只有收到组件 HumanConfirmed 报告后，控制台才开放桌面、WorkerW 和置底入口；组件再次检查门槛。自动检查的显式测试开关不设置 HumanConfirmed，不能成为人工确认记录。
- 控制台分别显示：进程存活/PID；HWND；Loaded；ContentRendered 与内容尺寸；当前模式；parent/host 及嵌入关系；WS_VISIBLE/WPF Visible；最小化；DWM cloaked；屏幕矩形/DPI；三点命中采样及可能遮挡窗口的句柄、类名和 PID。采样不记录其他窗口标题，不自动认定可见。
- 控制命令带递增编号，日志有 SEND、RECEIVED、ACK；状态显示已执行、失败及错误信息。等待应答超时会报未确认，不伪报成功；心跳诊断不会抢焦点或反复恢复窗口。
- **“重建普通窗口 / 找回”**：退出本控制台启动的旧组件，必要时只结束该组件进程，然后创建全新普通进程/窗口。它不依赖旧 HWND 成功脱离；测试文字、位置、计数及人工确认会清空。没有业务数据持久化。
- “恢复普通模式”仍提供同一 HWND 的脱离路径；若无效，使用上述独立重建。新组件保持普通模式，不自动再嵌入。
- Explorer 宿主仅枚举现存结构，检查宿主进程和双方 DPI context；不发送 0x052C、不隐藏或改写 Explorer/图标窗口样式、不用全局输入钩子、不修改注册表、Snap、任务栏、壁纸或系统设置。
- 意外子窗口销毁只自动重建一次普通组件，避免重启循环。控制台退出会清理其子进程，组件正常退出不会自动复活。挂死时强制清理分支还未做人工验证。
- 诊断只记录输入字符数、点击数及窗口元数据，不保存输入正文；日志在实验 `bin/.../probe-session.log`，超过 1 MiB 截断旧诊断。自动报告和自身内容渲染在根 `artifacts/`，全部忽略提交。
- 未自动重启 Explorer、休眠或修改 DPI。默认普通窗口可能出现在任务栏和 Alt+Tab，便于找回；桌面模式才应用子窗口/工具窗口样式，其任务切换表现仍待人工检查。

## 启动和人工验收

先从旧实验控制台退出，避免混淆多个版本；不用启动原日历主程序。双击：

`experiments\DesktopHostProbe\bin\Release\net10.0-windows\DesktopHostProbe.exe`

或从项目根目录 Windows PowerShell 构建并启动：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\experiments\DesktopHostProbe\Start-Probe.ps1
```

正常应有 **两个窗口**：

1. **Desktop Calendar — 实验控制台（保留找回入口）**：模式和找回按钮、状态区、日志。
2. **Desktop Calendar — 普通窗口诊断组件**：浅色背景、紫色大标题、白色输入框、计数按钮、确认按钮，带系统标题栏和缩放边框。

操作顺序：

1. 在浅色组件输入无隐私文字，点击计数，再拖标题栏、四边四角和右下角缩放。用户已确认可见/输入/计数；缩放仍请单独核对。
2. 点组件的确认按钮，再点控制台 **“开始桌面嵌入”**。查看对应命令 ACK，同时亲眼检查组件是否仍可见、可输入；mode=desktop/parent 有值不是通过依据。
3. 如果消失，从任务栏返回控制台，点 **“重建普通窗口 / 找回”**。应见新 PID 和新的普通组件；文字清空是明确的实验行为。不要仅依赖“恢复普通模式”或 visible=True。
4. 确认嵌入可见后，再手动测试 Win+D、普通应用覆盖、桌面图标及空白处右键，以及置顶/桌面往返。组件矩形内图标遮挡是否可接受需反馈。
5. Explorer 重启、休眠、混合 DPI 由用户手动做：先保留控制台；需要重启 Explorer 时在任务管理器选择“Windows 资源管理器 → 重新启动”。不要求自动重新嵌入，但必须能找回和退出。没有相应环境则记未测。
6. **“退出整个实验”**结束控制台及其组件；组件“退出组件”只关自身，控制台仍可找回。

若普通组件再次不可见，请反馈控制台 PID、HWND、Loaded/ContentRendered、模式、cloaked、矩形、遮挡采样和最后命令 ACK/错误；不要提供或提交私人输入、桌面截图、凭证。详细记录见 [TESTING](TESTING.md)。

## 自动证据与人工边界

2026-10-30：普通初始化/确认门槛/内容非空渲染及原生转换 **20 PASS / 0 FAIL**；控制台应答/错误应答/新进程恢复/有限重启/正常退出 **7 PASS / 0 FAIL**，脚本退出码 0。离屏内容图仅验证 WPF 渲染输出非空，不是屏幕截图。电脑交互工具重试及重置后仍不可用；普通窗口人工通过的依据是用户明确反馈。

旧版本的 26+4 项记录保留为历史，已被“组件不可见”的人工结论限制。当前待验：嵌入后视觉及输入、恢复后的肉眼可见、实际拖动缩放、Win+D、图标/右键、Explorer 重启、休眠、Win10、混合 DPI，以及主程序透明渲染。**不据编译或自动数量认定整合可行，不进入 P1.1-3。**

## 参考依据

- [SetParent](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent)及[高 DPI 桌面开发](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)：跨进程挂载的 DPI 一致性风险；公开 API 不代表 Explorer 内部结构有兼容契约。
- [SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)及[扩展窗口样式](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles)：层级与任务栏样式不能替代桌面身份。
- [DwmGetWindowAttribute](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmgetwindowattribute)：读取合成器属性作为诊断辅助。
- [Microsoft 对 Show Desktop 的历史说明](https://devblogs.microsoft.com/oldnewthing/20040527-00/?p=39153)、[WPF Window 源码](https://source.dot.net/PresentationFramework/System/Windows/Window.cs.html)、[Lively 24H2 适配发布说明](https://github.com/rocksdanister/lively/releases/tag/v2.2.0.0)：分别帮助理解显示桌面、owner 清理与 Explorer 版本风险，不作为当前组件已验收的证明。
- [IDesktopWallpaper](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-idesktopwallpaper)、[Windows Widgets](https://learn.microsoft.com/en-us/windows/apps/design/widgets)、[AppBar](https://learn.microsoft.com/en-us/windows/win32/shell/application-desktop-toolbars)：区分背景管理、面板和停靠工具栏用途。
