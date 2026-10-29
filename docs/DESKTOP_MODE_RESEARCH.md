# P1.1-2：桌面宿主实验与测试平台修复

## 当前：A–D 人工失败后的原生 E/F 对照（2026-10-30）

用户本轮实测：**C 仅有可缩放的黑色矩形；B 透明顶层符合预期；D 完全不可见；A 重建找回有效。重复 A→C、B→D 未改善。桌面嵌入仍未通过，不进入 P1.1-3。** 第 4 项反馈没有明确是否勾选软件渲染并重建，已请求澄清；在收到答复前，不把重复操作记为 SoftwareOnly 人工对照结论。

据此，透明顶层本身在本轮用户环境可用；问题随桌面嵌入出现，非透明 C 也失败，因此不能仅归因于 AllowsTransparency。仍无法区分 WPF HWND 呈现、宿主合成/遮挡或远程输出；不能据此宣布 WPF 存在已证明的根本限制。

### 新增的最小定位实验

`NativePaintProbe.cs` 使用项目已经启用的 .NET Windows Forms（不是新增第三方依赖），建立独立原生窗口和原生 EDIT/BUTTON 控件。背景色块与标题直接使用 GDI FillRect/DrawText，在正常 WM_PAINT 内绘制，不使用 WPF 视觉树、DirectX 画布、layered、截图贴图或持续强制刷新。它仅是定位对照，**不代替最终 WPF 半透明圆角需求**。

| 模式 | 操作及目标 | 证据边界 |
|---|---|---|
| E 原生普通窗口 / 返回 E | 显示普通原生窗口；原 WPF 组件有意隐藏，保留控制台，因此正常仍是两个可见窗口 | 用户先确认色条、文字、输入与计数。独立 HWND，不是 WPF 截图 |
| F 原生桌面嵌入 | E 输入、计数、点确认后，从控制台选择 F；将同一个 E HWND 挂到与 C/D 相同的现存宿主，使用对应 WS_CHILD、无 POPUP、无 layered | 仅挂载和绘制回调通过不能证明屏幕可见；需要用户肉眼检查及输入 |
| 返回 E / 找回 | E 按钮将同一原生窗口脱离并移回主屏工作区；A 按钮销毁整个旧实验子进程并重建 WPF 普通组件 | A 不依赖原生窗口脱离成功；测试文字清空。退出整个实验清理两种组件 |

E 不要求先确认 A。F 必须在 E 内完成输入、点击计数及手动确认，不复用 WPF 的人工确认。控制台顶端标记“当前观察目标 E/F”和独立原生状态；下面的 WPF HWND/Visible 仅是有意隐藏的后台 WPF 窗口诊断，不能混作原生组件状态。用户观察记录、ACK 使用实际目标案例/模式。

原生诊断记录 PID、HWND、parent/host、样式、可见位、cloaked、矩形、宿主交集、命中遮挡线索、EDIT/BUTTON 类名、GDI 绘制回调次数、确认状态及错误。**绘制回调次数不证明用户屏幕像素正确。** 不记录输入正文，不操作用户应用或 Explorer。宿主失效时尝试回到 E；原生窗口关闭会显示原 WPF 组件，控制台保留 A 重建及退出。

### 本轮人工操作（先只测 E→F）

1. 关闭旧实验控制台，启动同一个 `experiments/DesktopHostProbe/bin/Release/net10.0-windows/DesktopHostProbe.exe`。仍从 A 和控制台两个窗口启动，不自动嵌入。
2. 点击控制台 **E 原生普通窗口 / 返回 E**。应出现标题为 **Desktop Calendar — E/F 原生 GDI 对照组件** 的浅色普通窗口，紫色标题区、红绿蓝条、输入框与按钮可见；A 暂时隐藏。
3. 在 E 手动输入无隐私测试文字，点计数，再点 **已看见、输入并计数，允许 F 嵌入**。确认色条、文字和控件正常。E 也失败时不继续 F，先记录现象。
4. 点控制台 **F 原生桌面嵌入**；最小化遮挡它的控制台/普通应用。检查色条、文字、输入、计数，以及拖紫色区域和四边四角。按实际结果点控制台“记录：可见可操作 / 黑屏 / 不可见”。
5. 点 **E 原生普通窗口 / 返回 E** 检查同一窗口能否恢复；若未恢复，点 **A 非透明顶层 / 重建找回**。最后“退出整个实验”应关闭所有实验窗口。

暂不要求重复 A–D、DPI、Explorer 重启或休眠。E/F 不受 WPF 软件渲染选项控制，不必切换它来重复 E/F。

### 自动结果与下一步判断

Release 构建 0 警告/错误。Default **43 PASS / 0 FAIL**、SoftwareOnly **43 PASS / 0 FAIL**、控制台 **13 PASS / 0 FAIL**；脚本退出码 0。本轮首次检查暴露原生控件类名大小写断言及聚焦输入框的程序赋值会启用确认入口的问题；已改为忽略类名大小写、仅输入消息期间的编辑触发门槛，复跑全部通过。没有把初次失败隐藏为通过。

自动证据：E 收到 GDI 绘制回调，存在原生 Edit/Button HWND；F 与 C/D 使用同一个 Progman 候选，父子关系及非 layered 的 WS_CHILD 样式通过；同 HWND 脱离保留输入、模拟失效返回 E、关闭销毁原生 HWND、控制台 ACK 和回到 WPF/A 重建通过。**E/F 实际像素、键盘/鼠标、拖动缩放尚待人工；没有声称黑屏已修复。**

- 若 E 正常、F 也正常而 C/D 仍失败：增强 WPF 呈现与跨进程重挂相关线索，下一步才考虑创建时绑定宿主的 WPF/HwndSource 小实验；不能据此直接换框架或整合。
- 若 E 正常、F 也黑/消失：问题不限于 WPF，需要继续查宿主层级/裁剪/合成和远程路径，不继续堆叠 WPF 渲染开关。
- 若 F 色条正常但输入框/按钮不见或不能输入：分别记录 GDI 表面与原生子控件现象，不笼统写“通过”。
- 若 E 自身异常：先修复原生对照基线，不能将 F 的结果用于根因判断。

以下为 A–D 实验设计及先前证据，当前状态以本节为准。

## 当前：黑屏诊断 A–D 对照（2026-10-30）

**用户新增反馈：桌面宿主中有组件矩形且能缩放，但内容纯黑、不能读取或操作；任务栏和 Alt+Tab 排除符合预期。桌面模式人工验收仍失败。** 用户正通过远程桌面测试，本轮按要求暂不做混合 DPI、Explorer 重启或休眠。

已核对先前代码及本地日志：发生这次黑屏的组件是 `AllowsTransparency=false` 的非透明 WPF 窗口，不能将原因直接归为透明窗口。WorkerW 请求日志有 `MODE_ERROR 没有符合条件的现存宿主`，ACK 为 `Ok=false, Mode=normal`；用户看到普通窗口是**实际回退**，并非成功进入 WorkerW。

### 启动与本轮操作（优先于下方历史步骤）

关闭旧实验后，双击 `experiments\DesktopHostProbe\bin\Release\net10.0-windows\DesktopHostProbe.exe`；或在项目根执行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\experiments\DesktopHostProbe\Start-Probe.ps1`。默认仍先显示控制台和 A 顶层组件两个窗口。

| 选项 | 实际实验 | 操作顺序 |
|---|---|---|
| A 非透明顶层 / 重建找回 | 非透明 WPF，标准标题栏，parent=0，非 LAYERED | 新进程启动；输入、点击计数、点确认 |
| B 透明顶层 / 重建 | WPF `AllowsTransparency=true`、无系统标题栏、WPF 自建圆角半透明表面，parent=0、LAYERED | 新进程启动；输入、计数、点确认；紫色标题区域可拖动，四边四角可缩放 |
| C 非透明嵌入 | A 的同一个 HWND 挂入现存桌面宿主；CHILD=true、POPUP=false、非 LAYERED | 先确认 A，再点 C；不重建，不更改渲染偏好 |
| D 透明嵌入 | B 的同一个 HWND 挂入相同宿主；CHILD=true、POPUP=false、保留 WPF 自己设置的 LAYERED | 先确认 B，再点 D；不手动翻转 LAYERED |
| 软件渲染对照 | 在新组件启动前设置该子进程 `RenderOptions.ProcessRenderMode=SoftwareOnly` | 勾选后重新点 A/B，再对应进入 C/D；设置不会改变已经运行的组件或系统 |

全部使用相同的红绿蓝色条、紫色标题、输入框、计数与退出按钮。**B/D 继续验证半透明和圆角要求，非透明路径只用于定位。** 透明组件不会自动进入嵌入；C/D 入口只在相应顶层组件被用户确认后启用。

建议先不勾软件选项，按 **A→C→重建 A→B→D** 观察；然后勾选软件选项，重新走 **A→C、B→D**。在每个模式点控制台“记录：可见可操作 / 黑屏 / 不可见”；记录附带案例、模式、PID、渲染偏好，不含输入正文。嵌入黑屏时无需等待，点 **A 非透明顶层 / 重建找回**。测试文字会清空；退出整个实验继续清理其自身组件。

“WorkerW 对照”是额外宿主实验；本机未发现可用宿主时，控制台明确显示请求 `wallpaper`、实际 `normal`、失败 ACK 和原因。不要把普通回退算作 WorkerW 成功。软件选项的 Default 代表默认偏好，**不是强制 GPU**；实际渲染可能受运行时及会话条件影响。

### 诊断证据及其边界

- 新增记录实际 STYLE/EXSTYLE、AllowsTransparency、LAYERED/CHILD/POPUP、请求模式与实际模式、组件 client 屏幕矩形、宿主 client 矩形和相交尺寸、HwndTarget 渲染偏好、进程渲染偏好、WPF tier、SM_REMOTESESSION 及 RDP 加速开关；继续保留 PID/HWND、Loaded、ContentRendered、cloaked、父子关系、命令编号/ACK/错误。
- “采样 WPF 内容”只在显式点击时执行一次 RenderTargetBitmap，报告 RGB 色块数量与透明像素；**不截图桌面、不证明真实 HWND 已成功合成或 RDP 已显示**。没有周期重绘、强制焦点、LAYERED 反复切换或重挂循环。
- 本机最终自动检查：Default **31 PASS / 0 FAIL**，SoftwareOnly **31 PASS / 0 FAIL**，控制台 **10 PASS / 0 FAIL**；完整脚本退出码 0，构建 0 警告/错误。测试报告在根 `artifacts/`，运行日志在实验 bin 下，均不提交。
- A/B parent=0；C/D 确实挂到 Progman，C 非 LAYERED，D 保留 LAYERED，均 CHILD=true、POPUP=false。A–D 的离屏 RGB 数量均非零，B/D 有大量非全透明 alpha 像素。由此仅能排除“本次测试的 WPF 视觉树完全没有彩色内容”和“D 实际未启用 layered”的情况，**不能排除真实呈现链路失败**。
- 当前自动进程报告 `SM_REMOTESESSION=false`、tier=2，RDP 加速开关未显式设置；用户报告远程连接，二者分别记录。该指标只描述运行进程所见会话，不能据此否认远程传输、认定等同用户 RDP 环境或比较已完成。
- 本次 C/D 的窗口矩形与宿主 client 矩形有完整矩形交集；三个命中采样点中有两个命中浏览器子窗口，另一个命中组件。已在检查器中隐藏/关闭其他普通对照组件，减少测试自身遮挡。几何相交只能排除完全落在宿主矩形外，不能排除其他兄弟层、区域裁剪、DWM/DirectX 或远程编码问题。WindowFromPoint 是命中线索，不是像素采样。
- 暂未捕获到托管异常或命令错误能解释黑屏；“无异常”不等于 GPU、合成器或远程路径健康。默认/软件对照仍需用户逐项看见并操作，再判断是否存在偏好相关差异。

### 如何解释下一轮结果与替代方向

- A/B 都正常而 C/D 黑：优先研究宿主子窗口呈现及跨进程重挂；不能仅归因透明度。
- C 正常、D 黑：增加 layered/per-pixel alpha 与宿主合成路径的证据；仍需同会话软件对照。
- 软件 C/D 正常、默认黑：是渲染路径相关线索；不直接证明驱动有错，且应评估 CPU 与 RDP 性能后再决定是否采用。
- B 顶层也黑：先排查透明顶层及远程显示路径，不继续把问题限定为 Explorer。
- 离屏 RGB 非空但屏幕黑：说明内容可生成，尚未区分 HWND 呈现、宿主合成、遮挡或远程输出失败。

**目前没有证据支持“WPF 透明嵌入存在已证明的根本限制”，也没有证据支持“黑屏已经修复”。** 若 A–D 人工结果仍不能区分，再在同宿主加入独立 Win32/GDI 子窗口对照：GDI 可见而 WPF 黑才增强 WPF 呈现链路线索，两者均黑则增强宿主/遮挡线索。本轮未实现该额外路径，避免在 A–D 人工结果到来前扩大实验。

可维护替代候选包括创建时就绑定宿主的 HwndSource（避免修改现存顶层 HWND 的呈现契约），或 Win32 宿主加可交互的 WPF 子内容；它们的透明合成和键盘焦点仍需独立验证。不能把截图贴图、不可编辑壁纸或取消半透明圆角当作最终方案。未选择或整合替代框架。

### 本轮查证来源

- [WPF 互操作渲染区域](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/technology-regions-overview)：WPF 使用 DirectX，layered 与其他 HWND 互操作需遵循 Win32 呈现规则；不是对本项目具体黑屏的根因证明。
- [WPF 硬件/软件管线](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-taking-advantage-of-hardware)与[ProcessRenderMode](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.renderoptions.processrendermode?view=windowsdesktop-10.0)：软件偏好仅作用当前进程；tier/偏好不能直接说明每帧实际用了 GPU。
- [现代 .NET WPF 的 RDP 配置](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/wpf)：.NET 8 起有 RDP 硬件加速开关，未设置时 RDP 默认软件渲染。本实验只读取并记录，不改配置或系统策略。
- [WPF 渲染线程故障排查](https://learn.microsoft.com/en-us/troubleshoot/developer/dotnet/framework/general/wpf-render-thread-failures)：软件渲染可作为排查手段，不能单凭黑色矩形判定驱动缺陷。
- [Win32 窗口特性](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features)：Windows 8 起支持 layered 子窗口，故不能笼统说 Win10/11 完全不允许 CHILD+LAYERED；WPF/Explorer/RDP 组合仍须实测。
- [.NET WPF issue #7724](https://github.com/dotnet/wpf/issues/7724)记录的是共享窗口时透明内容黑屏，属于不同场景的已有报告；不能直接当作本次 RDP+Explorer 的已知根因。

## 以下为前一轮普通入口修复记录（历史）

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
