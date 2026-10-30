# P1.1-2：桌面宿主实验与测试平台修复

## 最新：开源源码复核与候选路线（2026-10-30，等待实验批准）

**本轮只完成公开源码、许可证和 issue/PR 调研，不运行参考软件，不复制实现代码，不增加实验或依赖，不修改主日历窗口。推荐先批准候选 1；候选 2 只作为有边界的后备，需要单独批准事件监听及临时层级调整。** 当前没有新方案通过本机视觉验收，旧 C/F 黑屏、D 不可见的结果继续有效；不重跑 A–F，不进入 P1.1-3。下方旧记录中的“下一步跨进程自有宿主对照”不再是当前优先路线。

### 需求与证据边界

- 验收以行为为准，不要求某种宿主技术：无任务栏按钮、无 Alt+Tab、Win+D 后持续可见且不被最小化、默认非置顶且普通应用能覆盖、可直接点击/输入/编辑/移动/缩放。点击组件可以正常取得输入焦点；普通应用激活后，组件不能持续压在其上。
- 托盘可以保留，安全隐藏只是软偏好，不是硬性门槛；可靠的找回、设置、退出入口不能省略。当前默认常驻管理入口仍是可用的产品规划，不为隐藏图标增加风险。Windows 10/11 都是最终目标，本轮不宣称完成全平台兼容。
- 区分四层证据：README 主张、源码机制、外部 issue/PR 的反馈、我们自己的实机验证。前三层不能替代第四层；本轮仅前三层。没有新建“自动通过”或“人工通过”结果。
- 阅读的是下表固定提交，不保证以后版本相同；未下载发布 EXE、未安装 Qt/Electron/Rainmeter。只读取源码并用自己的文字描述，没有将第三方源文件或片段纳入仓库。

| 项目 | 本次核对提交 | 仓库 LICENSE |
|---|---|---|
| VaticUI/DeskWidgets | `e37bf4eb3427e2dc5c76937f3c6bf1bd27255ad3` | MIT |
| tanxestudio/Windows-desktop-Tasks-Widget | `32322d2e649a7271722a17a4eac44ac9189e1ca0` | MIT |
| Felix-au/DeskX-Wallpaper-Engine | `2a78908b99ea7a1a8dcacf36aa672a44f6e13ca6` | MIT |
| rainmeter/rainmeter | `54df7737c615ebe5a6b1d63e539aec171b54aec7` | GNU GPL v2 |

### 1. DeskWidgets：顶层窗口的 owner，不是 SetParent 子窗口

**README 主张**：Win10/11 桌面组件、位于普通应用下方、Win+D 可见、可编辑便签和提醒。

**实际源码**：[core.py](https://github.com/VaticUI/DeskWidgets/blob/e37bf4eb3427e2dc5c76937f3c6bf1bd27255ad3/core.py#L412-L483) 的 `WidgetWindow` 使用 Qt 无框工具窗口、WindowStaysOnBottomHint、透明背景和显示时不激活；`pin()` 找 Progman，再对**自己的顶层 HWND** 调用 `SetWindowLongPtrW(GWLP_HWNDPARENT)`，并用 `GW_OWNER` 检查拥有者。这里没有把组件改成 WS_CHILD，也不是把整个窗口 SetParent 给 Explorer。`showEvent` 延后执行 pin；tick 每隔超过五秒检查 owner，只有不符才重设 owner/置底；`WM_WINDOWPOSCHANGING` 又会将层级变更改成 HWND_BOTTOM。它不是单纯“设置一次 owner 就全部解决”。

**输入与视觉**：[kinds.py](https://github.com/VaticUI/DeskWidgets/blob/e37bf4eb3427e2dc5c76937f3c6bf1bd27255ad3/kinds.py#L775-L878) 的便签用 QPlainTextEdit，编辑时主动 `activateWindow()` / `setFocus()`；因此 ShowWithoutActivating 不等于永久禁止输入激活。Qt/QPainter 和透明背景证明其实现路径，不能证明相同 owner 对 WPF 的呈现、中文输入法或八方向缩放已经兼容。尺寸是预设档位，并非我们需要的连续缩放。

**实际外部证据**：[issue #1](https://github.com/VaticUI/DeskWidgets/issues/1) 在 2026-10-23 报告其他虚拟桌面中可见但不能点击，2026-10-24 已关闭；本次接口返回没有评论，未取得可核对的修复说明，不能把“closed”当作问题已解决或输入全面通过。也没有取得覆盖本项目全部核心要求的可复现实机矩阵。

**对本项目的价值**：值得独立验证 owner 与 parent 的差异，可保留已经可见的透明顶层 WPF 呈现路径。不能原样采用它的定期修正和每次层级变更强制置底，这与本项目的行为约束不符；删去这些机制后是否仍能通过 Win+D，必须重新作为新候选验证。

### 2. Desktop Tasks Widget：事件监听可借鉴，锁定语义不适用

**README 主张**：无需用户开启 Always on Top 也能抵抗 Win+D；锁定时只读/穿透，解锁后拖动、缩放及编辑。

**实际源码**：[DesktopOverlay.xaml.cs](https://github.com/tanxestudio/Windows-desktop-Tasks-Widget/blob/32322d2e649a7271722a17a4eac44ac9189e1ca0/Windows/DesktopOverlay.xaml.cs#L108-L200) 调用 `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, WINEVENT_OUTOFCONTEXT)`，将前台窗口类名为 WorkerW 视为 Show Desktop，临时设 TOPMOST；前台换成其他类后取消。只有 `_isLocked && !_isAlwaysOnTop` 才执行；它还拦截锁定状态的 SC_MINIMIZE，并在 StateChanged 中把最小化改回 Normal。仅凭 WorkerW 类名不是可靠的 Win+D 状态识别，当前机器此前实际宿主是 Progman。

**输入与透明限制**：[ApplyAllStates](https://github.com/tanxestudio/Windows-desktop-Tasks-Widget/blob/32322d2e649a7271722a17a4eac44ac9189e1ca0/Windows/DesktopOverlay.xaml.cs#L243-L337) 在锁定时设 NOACTIVATE、TRANSPARENT、禁止命中和缩放；解锁虽恢复交互，却退出上述 Show Desktop 事件分支。解锁还手动清除 LAYERED，而 XAML 使用 AllowsTransparency=True；不把这种样式变更当作可维护的 WPF 透明方案。文件虽声明 SetParent，核对的实现没有实际调用，不能据声明说它使用了桌面嵌入。

**托盘与证据**：[App.xaml.cs](https://github.com/tanxestudio/Windows-desktop-Tasks-Widget/blob/32322d2e649a7271722a17a4eac44ac9189e1ca0/App.xaml.cs#L18-L90) 确实创建常驻 NotifyIcon，提供锁定、置顶、最简和退出；不是本项目完整的找回/设置菜单。本次仓库 issue 查询无结果，这既不是通过证据，也不表示没有问题。没有证据证明其“始终可直接编辑 + Win+D 可见”同时成立。

**对本项目的价值**：事件订阅和清理方式与 WPF 可配合；不采纳锁定才能抵抗 Win+D、禁用编辑、最小化后反复恢复或清除 WPF LAYERED 的实现。

### 3. DeskX：壁纸与交互层分离有价值，维护策略不可照搬

**README 主张**：WorkerW 壁纸、独立透明交互 overlay；组件可分处普通应用下、任务栏上或所有窗口上方。

**实际源码**：[wallpaper-manager.js](https://github.com/Felix-au/DeskX-Wallpaper-Engine/blob/2a78908b99ea7a1a8dcacf36aa672a44f6e13ca6/src/main/wallpaper-manager.js#L38-L333) 将非交互壁纸 BrowserWindow 与透明、focusable 的 overlay 分开；后者是独立顶层窗口，不随壁纸一同 SetParent。`win32-wallpaper.js` 的 [壁纸路径](https://github.com/Felix-au/DeskX-Wallpaper-Engine/blob/2a78908b99ea7a1a8dcacf36aa672a44f6e13ca6/src/main/win32-wallpaper.js#L142-L283) 会发送私有 0x052C、SetParent 到 WorkerW，失败则每 500ms 压底；overlay 每 30 秒重设层级，focus/blur 后又延迟 50ms 重设。以上都不作为本项目候选实现。

**Win+D 与输入**：[overlay helpers](https://github.com/Felix-au/DeskX-Wallpaper-Engine/blob/2a78908b99ea7a1a8dcacf36aa672a44f6e13ca6/src/main/win32-wallpaper.js#L369-L568) 设 TOOLWINDOW/NOACTIVATE/LAYERED，前台类为 Progman/WorkerW 时，200ms 后恢复并重排；另有 minimize 后 100ms 恢复。任务栏层通过 TOPMOST→NOTOPMOST 配对调用，注释声称的精确相对层级不是 Windows API 的保证。[index.js](https://github.com/Felix-au/DeskX-Wallpaper-Engine/blob/2a78908b99ea7a1a8dcacf36aa672a44f6e13ca6/src/main/index.js#L221-L265) 的命中 IPC 切换鼠标穿透，编辑 IPC 会 focus/blur overlay；这证明存在输入意图，不能证明 NOACTIVATE、异步压底、键盘焦点和 Win+D 往返在本机稳定。

**证据与 WPF 适配**：本次仓库 issue 查询无结果，未取得核心行为的外部测试矩阵。架构上“交互窗口不必嵌入壁纸宿主”有参考意义；Electron/Koffi 不是当前依赖，也没有必要为了这个分层概念引入它们。私有消息、定时层级维护及恢复链路超出本轮可采用边界。

### 4. Rainmeter：最有价值的兼容性证据，不能当作 WPF 现成模块

**README 主张**：Windows 桌面定制工具；README 本身不提供本项目所有行为的保证。

**实际源码**：[Skin.cpp](https://github.com/rainmeter/rainmeter/blob/54df7737c615ebe5a6b1d63e539aec171b54aec7/Library/Skin.cpp#L292-L306) 创建 `WS_POPUP + WS_EX_LAYERED + WS_EX_TOOLWINDOW` 顶层 skin，所查皮肤桌面层级路径不通过 SetParent。透明绘制使用 UpdateLayeredWindow，并非 WPF 视觉树。[ChangeZPos / OnWindowPosChanging](https://github.com/rainmeter/rainmeter/blob/54df7737c615ebe5a6b1d63e539aec171b54aec7/Library/Skin.cpp#L999-L1118) 根据桌面状态在辅助窗口附近或底部排序；桌面/底部模式还在 WindowPosChanging 中阻止自动改变层级（该处理在同文件 L4702–4725）。自定义输入 meter 点击后 SetFocus（L5066–5096），并非所有模式永久 NOACTIVATE。

[System.cpp](https://github.com/rainmeter/rainmeter/blob/54df7737c615ebe5a6b1d63e539aec171b54aec7/Library/System.cpp#L60-L130) 有两个自建工具辅助窗口、全桌面 OUTOFCONTEXT 前台事件监听；还保留 250/100ms 桌面状态检测计时器。`CheckDesktopState` 只在状态改变时重排，不是每次计时器回调都强制压底；`PrepareHelperWindow` 在 Show Desktop 状态可能使用 topmost 辅助层，退出后回到底部。不能把它简化成“无需 Shell 依赖、纯事件、从不碰 topmost”。

**明确的历史问题和修复**：[issue #377](https://github.com/rainmeter/rainmeter/issues/377) 报告 Win11 24H2 Win+D 隐藏 skin；维护者说明 Show Desktop 与 Restore Windows 必须区分，单纯强制恢复可能破坏第二次 Win+D 的还原行为。[PR #413](https://github.com/rainmeter/rainmeter/pull/413) 于 **2025-04-09** 合并，提交 `b1128fb21ee21a0370997f3fabf050915ff911a1`，将新构建的桌面参考从 WorkerW 调整为 Shell 窗口，并更改事件分支。当前代码用 `GetCurrentMonitorTopologyId` 导出是否存在作为分支启发式，仍依赖 Explorer 层级/未公开实现细节，不是微软支持的桌面固定契约。这是具体风险与修复证据，不是我们 WPF 程序已通过的证据。

**对本项目的价值**：优先借鉴“保留顶层渲染 + 明确桌面状态 + 层级与输入分开管理”的思路；不直接移植其计时器、层级拦截或 GPL 代码，也不使用那个未公开导出做本项目的版本承诺。

### 官方语义：需要避免的三种误判

1. [SetWindowLongPtrW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowlongptrw) 的 GWLP_HWNDPARENT 对**顶层窗口设置 owner**，不是对 child 设置 parent。检查新候选必须分别记录 WS_CHILD、`GetWindow(GW_OWNER)`、`GetAncestor(GA_PARENT/GA_ROOT)`；[GetParent](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getparent) 对某些顶层 popup 会返回 owner，不能沿用旧实验“GetParent 非零就是嵌入”的判定。所谓 desktop-owned 是一种所有权尝试，并非新的受支持桌面组件 API；[GetShellWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getshellwindow) 与 GetDesktopWindow 的语义也不能混用。
2. [Owned Windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#owned-windows) 规定 owned 窗口在 owner 上方，owner 最小化时随之隐藏、owner 销毁时随之销毁。它**没有保证普通应用总能盖住组件、Win+D 排除或 Explorer 重启不丢 HWND**；特别要防 owner 变动/置顶带来的层级传播。[SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos) 的 TOPMOST/NOTOPMOST、NOACTIVATE、NOOWNERZORDER 是层级/激活控制，不是桌面成员身份。
3. [SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook) OUTOFCONTEXT 是异步事件回调、不将 DLL 注入目标进程；PID/TID 为 0 会监听当前桌面所有进程/线程，仍属于全桌面事件订阅，不能偷偷当作无钩子方案。[EVENT_SYSTEM_FOREGROUND](https://learn.microsoft.com/en-us/windows/win32/winauto/event-constants) 只表示前台改变，既不是专用 Win+D 通知，也不能区分 Win+D、普通点击桌面与还原。事件回调需防重入、过期事件和反馈循环；仅凭窗口类名判断不能获得可靠桌面状态。

### 最多两个候选及推荐顺序（尚未实现）

| 候选 | 拟验证机制 | 主要收益 | 主要风险 / 失败条件 |
|---|---|---|---|
| **1，优先：桌面 owner 的 WPF 顶层工具窗口** | 保留透明圆角顶层 WPF，WS_CHILD=false；经 Shell HWND/PID 校验，仅设置自身 GWLP_HWNDPARENT；显示/模式切换时单次置底，普通应用激活导致自身 Deactivated 时至多一次不激活的层级调整；不安装全桌面事件钩子 | 改动最小，绕开当前子窗口跨进程绘制路径；直接保留 TextBox/按钮、键盘焦点与连续缩放 | owner/Win+D 没有兼容保证；删去 Qt 的定期修正/层级拦截后可能失败；Shell 提升/隐藏及 owner 生命周期有耦合。若需要持续压底、抢焦点或禁止输入才能工作，就判失败 |
| **2，后备：无 Explorer owner 的顶层 WPF + 事件驱动桌面层级** | 保持自己的窗口/辅助窗口，不 SetParent、不设置外部 owner；只订阅前台事件并结合 Shell 身份及可观察层级推断状态，在进入/退出桌面状态时做有界层级切换 | 避开 Explorer 子窗口绘制和外部 owner 销毁关系；参考 Rainmeter 的独立呈现思路，可保留正常输入 | 检测仍受 Explorer 实现影响；事件延迟可能闪烁、压住新应用或损坏 Win+D 双向切换；Rainmeter 还有状态轮询，删除后能否可靠检测尚无证据。必须显式批准钩子和临时层级调整后才能实验 |

候选 1 的启动/失焦排序必须带不激活及不移动 owner 的限制；用户在输入框点击时允许正常激活，不配置永久 NOACTIVATE，不在 Activated 时立即反复压底。普通应用再次激活后检查它确实能覆盖组件；原生 topmost 状态及焦点都要记录，不能只看 WPF 属性。独立控制台/托盘的存活不依赖组件 owner；找回可重建普通窗口，默认不再挂回实验模式。owner 生命周期风险保留到后续恢复门槛。

候选 2 的授权边界：只允许 `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, WINEVENT_OUTOFCONTEXT)` 的非注入观察，不读键盘/鼠标、不记录他人标题或内容、不操作他人窗口、不发 Explorer 私有消息；退出必须 Unhook。外部前台事件与本组件激活/失焦分开处理，以防忽略自身事件后输入状态失配。回调只排入本进程 UI 线程，合并重复事件；新的普通应用前台事件须取消过期的桌面动作，不抢回前台。

**事件驱动不等于稳定保证**：拟限定每个确认的桌面状态转换最多一组层级操作，没有常驻刷新/置底计时器。若需要临时 TOPMOST，只允许转换中的一次提升与立即降级，并记录实际 TOPMOST 位；不得为了保持 Win+D 可见而在整个桌面状态维持 topmost。若降级即被遮挡、需要每次窗口变动强制纠正，或需要最小化后自动 Restore 循环，则候选 2 在这些约束下失败，不能悄悄放宽条件。即使不调用 SetForegroundWindow，异步层级变化仍可能闪烁，必须看真实画面。

这里推荐的是**值得先试的最小假设**，不是四个参考项目已经验证过的删减版本。两种候选都保留半透明圆角要求，不用取消透明度、换渲染开关、反复重绘、GDI 参数调整来继续试错；暂不考虑更换 WPF。

### 获批后的最小验证与停止条件（本轮不执行）

每次只实现一个获批候选，复用独立实验的透明顶层测试表面、TextBox、计数按钮、拖动与八方向缩放、安全控制台和托盘找回/退出。使用自己的覆盖测试窗口，不操作用户文档或关闭用户应用。先确认普通透明窗口可见，再切入新模式，不复用 C/D/F 的子窗口嵌入流程。

| 顺序 | 观察与预期 | 判失败 / 必须记录 |
|---|---|---|
| 1 | 透明圆角、文字/色条可见；点击输入中英文，计数、移动与八方向缩放正常 | 黑/不可见、失焦、输入到别的应用或需要锁定穿透才能显示 |
| 2 | 实际任务栏与 Alt+Tab 中无组件；控制台属于实验找回入口，分别记录 | 不能仅以 TOOLWINDOW/ShowInTaskbar 属性断言；控制台不作为组件混淆 |
| 3 | 覆盖测试窗口已打开时按 Win+D，组件持续可见、没有最小化；再按 Win+D，原应用正常还原并盖住组件；至少往返 5 组 | 记录 pixels、IsIconic、foreground、owner、TOPMOST 位、事件/层级调用时间；短暂消失后恢复也不标“不会被最小化”通过 |
| 4 | 点击组件编辑，再激活覆盖窗口；关闭并重新打开覆盖窗口，重复至少 3 组 | 组件不得保持 topmost 或抢回焦点；检查正在输入的对象、应用覆盖、闪烁及事件反馈循环 |
| 5 | 托盘/控制台明确找回为普通透明窗口；正常退出后无残留实验窗口、子进程或钩子 | 不用 visible=True 代替看见；不依赖 Explorer 组件 HWND 仍存活才能退出 |

优先用现有 computer-use 对可选择的顶层窗口验证画面与鼠标/键盘；任务栏、Alt+Tab、Win+D 桌面像素若工具不能观察，只针对缺口给出简短人工步骤，不把 API 状态冒充视觉通过。本轮没有启动新的测试程序，没有新操作要求。混合 DPI、真实 Explorer 重启、休眠和 Windows 10 全量验证继续作为获批整合前的后续门槛，不擅自执行。

**研究停止线**：候选 1 若失败，先给出事件/画面证据与归因范围，再由用户决定是否批准候选 2；两者都失败后停止新增路线。阶段性建议是保留 WPF 和可靠普通非置顶工具窗口作为需用户接受的过渡模式（明确 Win+D 不满足），把桌面模式隔离为独立模块。由于 WPF 与原生 GDI 在旧宿主都失败，仅凭此没有充分证据证明更换 UI 框架能解决问题；若以后确需换窗口实现，必须另提证据与迁移影响并获批准。

### 许可证与本轮交付检查

已读取固定提交的 [DeskWidgets LICENSE](https://github.com/VaticUI/DeskWidgets/blob/e37bf4eb3427e2dc5c76937f3c6bf1bd27255ad3/LICENSE)、[Desktop Tasks LICENSE](https://github.com/tanxestudio/Windows-desktop-Tasks-Widget/blob/32322d2e649a7271722a17a4eac44ac9189e1ca0/LICENSE)、[DeskX LICENSE](https://github.com/Felix-au/DeskX-Wallpaper-Engine/blob/2a78908b99ea7a1a8dcacf36aa672a44f6e13ca6/LICENSE) 与 [Rainmeter LICENSE](https://github.com/rainmeter/rainmeter/blob/54df7737c615ebe5a6b1d63e539aec171b54aec7/LICENSE)。前三者 MIT 的代码复用涉及保留版权及许可声明；Rainmeter 是 GPL v2，不能当成 MIT 片段直接移入。**本轮无代码复制，未来复用任何实现仍先获得用户批准并核对具体文件许可；不会用改写/翻译来规避要求。** 本节仅记录来源与采用边界，不决定本项目发布许可。

本轮只修改此研究文档，检查固定提交/路径、PR 合并状态及文档差异；不运行 A–F 或新增自动窗口测试。Git 提交与推送记录以本次阶段反馈及 Git 历史为准，`main` 不变。等待用户批准**候选 1 的独立最小实验**，不自动进入候选 2 或 P1.1-3。

---

以下保留历史实验记录，后续路线选择以上节为准。

## 最新：F 人工确认黑屏与宿主对照（2026-10-30）

**用户最新结果：F 画面黑屏，输入和计数因不可见而无法判断；移动和缩放正常；返回 E 成功。** 原生 GDI 也未通过桌面视觉验收。与 C 非透明 WPF 黑屏、D 透明 WPF 不可见、B 透明顶层正常合并判断：黑屏现象不限于 WPF/DirectX 或透明度。不能据此断言所有 WPF 原因都被排除，也不能将输入记为失败或通过；准确状态是未能验证。

### 本轮进一步检查（代理自行执行，无需重复人工操作）

新增只读 `--host-inventory`，记录当前 Shell 进程内 Progman/WorkerW、直接子窗口及至多三层的 Explorer 子树：HWND、类名、PID、父窗口、兄弟 Z 序、样式、矩形、可见位、cloaked 和 DPI。最多每层 40 项，不取其他应用标题/内容，不发送 Explorer 私有消息，不改变宿主样式/层级。F 挂载也输出同类诊断。

本机 Windows build 26200：实际宿主是 Progman，EX=0x200080（含 WS_EX_NOREDIRECTIONBITMAP 和 TOOLWINDOW）；其 SHELLDLL_DefView 的 EX=0x80000（LAYERED）。未发现可用壁纸 WorkerW。自动挂载时 F 是 Progman 的 z=0 子窗口，DefView 在 z=1；几何范围与宿主客户区完整相交。**这削弱了“F 只是放在 DefView 后面/完全落在宿主之外”的解释，不证明真实像素可见或排除合成遮挡。**

GDI 诊断新增最后一次 OnPaint 的模式、GetClipBox 结果及无效区域；本轮 F 的一次回调为 NULLREGION/0×0。其他普通应用确有遮挡采样，N 本地宿主也会出现空更新区域；因此空 clip 是绘制时机/可见区域线索，不能独立认定黑屏根因。没有反复重绘、抢焦点或强制抬高窗口。

为检验“宿主 NOREDIRECTIONBITMAP 样式本身是否足够造成黑屏”，增加 **完全属于实验进程** 的 R/N 最小宿主，GDI 内容仍使用同一 NativePaintProbe：

| 对照 | 实际观察 | 限度 |
|---|---|---|
| R：普通自有宿主 | 代理通过宿主窗口实际截图看见子窗口红绿蓝色条、标题、输入框和按钮 | 仅验证像素内容；本轮未在 R/N 中另做键盘、移动、Win+D 验收 |
| N：自有宿主创建时带 NOREDIRECTIONBITMAP | 代理实际截图同样看见上述内容，EX=0x250100 含指定标志 | 该标志单独在此同进程对照未复现黑屏；不能推定与 Explorer 跨进程环境等价 |
| F：实际 Progman 宿主 | 用户已确认黑屏；返回 E 可恢复 | 仍失败，不将 R/N 可见冒充 F 修复 |

R/N 只通过自有窗口的 CreateParams 设置样式，不修改 Explorer。初始化检查曾发现 Show/Shown 前挂载会拿到未完成显示的样式；现明确拒绝 Shown 前挂载，独立对照在 Shown 后执行。另修正自有宿主位于不同屏幕时子窗口必须落入其客户区的问题，加入几何断言。上述定位工具修复不被当成用户 F 黑屏已修复的证据。

### 复现与本阶段结论

关闭已有实验再从根目录运行 `Start-Probe.ps1 -Test`，或只构建；默认 EXE 仍先普通 A/控制台，不自动嵌入。新增定位入口（不需要用户重复测试）：

```powershell
$probeExe = 'D:\projects\desktop-calendar\experiments\DesktopHostProbe\bin\Release\net10.0-windows\DesktopHostProbe.exe'
# 只读层级元数据；WinExe 输出需显式重定向。
Start-Process -FilePath $probeExe -ArgumentList '--host-inventory' -Wait -RedirectStandardOutput '.\artifacts\shell-topology.txt'
# 各自仅创建自有宿主及子窗口，用宿主右上角 × / Alt+F4 正常退出。
Start-Process -FilePath $probeExe -ArgumentList '--local-host-check'
Start-Process -FilePath $probeExe -ArgumentList '--local-host-check --no-redirection'
```

最新自动检查：Default **53 PASS / 0 FAIL**、SoftwareOnly **53 PASS / 0 FAIL**、控制台 **13 PASS / 0 FAIL**。新增确认 Shown 前拒绝、R/N 创建标志、同 HWND 挂载、客户区定位与输入保留。最终构建 0 警告/错误；测试报告/元数据日志仍在被忽略的 artifacts，不提交截图或输入正文。初次对照有一处 Size 类型歧义编译错误及旧实验 EXE 占用重试，均修正后重新构建；不作为通过结果隐去。

构建诊断说明：一次临时沙箱构建未完整设置 SDK 的禁首次证书变量，SDK 输出了开发证书安装提示；只读检查当前执行用户证书库未发现当日新增 ASP.NET 证书，无法据该输出断言用户交互账户证书库改变。后续构建均使用禁证书/禁 PATH 添加等变量，没有信任或删除证书，也未修改系统证书设置。

**目前推荐结论：暂停将当前 Progman 挂载方案作为可整合候选；只保留普通窗口的安全回退和显式实验。** R/N 结果排除了“此样式单独必然黑屏”的简单解释，不能定位唯一根因；尚未区分 Explorer 特有宿主合成、跨进程挂载与远程输出。下一步若继续研究，应先做可控的跨进程自有宿主对照，保持只修改实验；涉及改变 Explorer 结构/私有消息的新方案仍须事先说明并获批准。现阶段不改 Explorer，不改主窗口，不进入 P1.1-3；无需用户重复 C/F 测试。

官方定义见文末来源中的 Extended Window Styles；GetClipBox 的 NULLREGION 仅表示该 DC 当前剪裁区域为空，不能替代屏幕视觉验收。补充来源：[GetClipBox](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-getclipbox)、[GetWindow 的子窗口及兄弟 Z 序](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindow)。

## 最新：代理直接操作电脑的验证与限制（2026-10-30）

用户授权可由代理自行操作电脑验证，减少重复人工测试。本轮 computer-use 已成功连接，不再沿用此前“连接失败”的状态。仅通过已提供的窗口选择、截图、鼠标和键盘接口操作实验；未修改主程序、系统设置或 Explorer，未安装依赖。实际操作发现并修复了 E 普通窗口的紫色标题区移动问题；桌面嵌入黑屏仍未解决。

| 操作 | 代理实际观察 |
|---|---|
| 打开 E 普通原生组件 | 激活后截图可见紫色标题、红绿蓝色条、浅色表面、输入框和按钮 |
| E 输入与计数 | 先点击输入框并核对焦点，再经键盘接口输入无隐私测试内容；截图/控件值均显示输入，鼠标点计数从 0 到 1 |
| E 交互确认 | 代理在实际看见并操作后点击确认，F 入口启用。UI 现有文案“用户确认”及 HumanConfirmed 字段此处表示交互确认，本次操作者是代理，**不得将这次记录写成用户亲自验收** |
| E→F | 命令应答为 native-desktop，实际 parent/host 匹配；挂载后组件从电脑控制工具返回的可选顶层窗口列表消失。使用先前由工具返回的窗口 ID 重新绑定失败，报 window id was not found，不能继续对该子窗口截图或注入输入 |
| F 的颜色、内容与输入 | **未验证**。工具无法选择嵌入子窗口，不等于该子窗口已黑屏或不可见；也不能据 visible=True、GDI 绘制次数或父子关系判为可见。本轮没有桌面全景像素证据 |
| F→E | 通过仍可操作的控制台返回 E，同一个原生窗口重新进入可选列表；截图显示原输入和计数 1 保留，色条与控件恢复可见 |
| E 系统标题栏移动 / 右下角缩小 | 已用鼠标拖动并核对截图位置/尺寸变化，色条和控件保留。只覆盖这两个动作，不宣称八方向已全部通过 |
| E 紫色区域拖动 | 修复前同一工具动作未观察到位移，而系统标题栏可移动。现仅在 E 普通模式将紫色标题区的 WM_NCHITTEST 从 HTCLIENT 映射为 HTCAPTION，使用原生移动流程；重建后相同鼠标拖动使截图原点由 (1404,601) 移至 (1464,639)，尺寸保留 688×584，实际画面内容完整。只证明该普通模式拖动恢复，不归因 WPF 黑屏，也不推定 F 拖动通过 |
| A 重建找回 | 控制台重建后出现新的 A 普通 WPF 组件，实际截图可见彩色内容，计数归零、确认重置，原 E 关闭 |

修复后完整自动检查：Default **44 PASS / 0 FAIL**、SoftwareOnly **44 PASS / 0 FAIL**、控制台 **13 PASS / 0 FAIL**，脚本退出码 0，Release 构建 0 警告/错误。新增原生命中检查仅验证 E 的彩色标题区；F 输入与移动路径未改。当前保留控制台和普通 E 组件，A 按钮可重建找回。

当前已经能自行完成普通窗口检查和控制台找回；F 桌面子窗口的可见性与交互仍受工具选择能力限制。后续优先执行可自动操作的项目，不重复要求用户测已验证的普通窗口；仅在确实无法观察的桌面行为上保留验收缺口。未通过验收，不进入 P1.1-3。下面的 E/F 实验设计及 A–D 历史仍保留，状态以本节为准。

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
