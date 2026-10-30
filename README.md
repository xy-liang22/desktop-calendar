# Desktop Calendar

Windows 本地桌面日历、计划与实际时间记录工具。待办表示计划，Activity 表示实际发生的时间；两者可关联，状态独立。

**当前为 P1.1-3 修订原型，使用模拟业务内容。** 本次仅修复窗口恢复、计划时间显示及同级完成沉底。已验收的 O 方案仍为 Explorer 桌面 owner + 透明顶层 WPF，没有使用 SetParent、WS_CHILD、Explorer 私有消息或持续层级纠正。

## 当前功能与 Planned

| 功能 | 当前状态 |
|---|---|
| 深色半透明、圆角、无系统白框 | 已实现；设置可调整背景不透明度 |
| 两页、三栏月历及七天时间轴、可调整分栏 | 已实现，内容为模拟数据 |
| 默认桌面 owner、免任务栏/Alt+Tab、置顶往返 | 已整合，主程序实际桌面行为待人工验收；独立 O 实验核心行为已验收 |
| 移动、八方向缩放 | 保留；特定方向缩小和跨 DPI 抖动尚待后续修复 |
| 今日待办折叠视图 | 已实现，日期、模拟勾选列表、设置和恢复入口；继承桌面/置顶模式 |
| 托盘、显示定位、安全找回、设置、退出 | 已实现，默认常驻托盘；正式图标 Planned |
| 窗口偏好记忆 | 已实现，分别保存完整/折叠矩形、显示器与 DPI，并恢复页面、分栏、视图及桌面/置顶偏好 |
| 模拟待办时间与完成沉底 | 已实现，完整与折叠均显示计划时间；子任务同样先未完成后已完成，取消完成恢复原顺序 |
| 左右贴边收起 | 保留旧实现；用户实测失效及 Snap 冲突未修复，不能依赖它找回窗口 |
| 月历按高度显示更多待办、Ctrl+滚轮时间轴缩放 | **Planned：P1.1-5**，当前每小时固定 50 DIP |
| 正式 Logo、ICO、稳定部署和开始菜单入口 | **Planned：P1.1-6**，需另行确认设计与部署位置 |
| 真实待办、Activity、重复/顺延、周计划与统计 | **Planned：P2–P8**；当前勾选仅改变内存中的模拟状态 |
| 业务 JSON 迁移、Markdown 图片、ZIP 备份恢复 | **Planned**；窗口偏好备份不等于业务备份 |
| 农历/节气、内地及香港假日、手动资料更新 | **Planned** |
| 完整中英切换、开机启动 | **Planned**；当前中文文案集中管理 |

## 系统及开发要求

- 目标 Windows 10/11 x64；当前验证机为 Windows 11 build 26200，两屏均 144 DPI。Win10、混合 DPI、Explorer 重启及休眠恢复保留后续综合验收。
- 运行需要 .NET Desktop Runtime 10 x64，SDK 已包含；当前不是自包含安装包。
- 开发使用 Windows 本机 PowerShell、.NET 10 x64 SDK 和 Git；已验证 SDK 10.0.403。可用 VS Code，不要求完整 Visual Studio、Build Tools 或扩展。
- 不使用 WSL，无第三方 NuGet 包。`NuGet.Config` 清空包源，使用现有 SDK 框架包离线构建。脚本不会安装工具或修改系统设置。

## 获取、构建及运行

仓库为 `https://github.com/xy-liang22/desktop-calendar`。目前没有正式安装包、Release 下载或已验收发布标签，通过源码构建获得原型。已有项目请勿覆盖。

在 Windows PowerShell 执行：

```powershell
cd D:\projects\desktop-calendar
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Start-Prototype.ps1
```

也可双击 `bin\Release\net10.0-windows\DesktopCalendar.exe`，或构建生成的根目录 `Desktop Calendar P1.lnk`。这仍是开发路径，不是开始菜单安装入口。构建前从设置或托盘真正退出程序，避免 EXE 被占用。

正常模式限制单实例；重复启动会提示使用托盘找回。首次启动默认桌面模式，以后恢复已保存的置顶和折叠偏好。托盘可能位于系统隐藏图标区。脚本的 ExecutionPolicy Bypass 仅作用于当前进程，不改变系统策略。

## 窗口与主要操作

- 第一页为较宽月历、当日模拟待办及实际时间轴；选日期改变标题，仍展示固定样例。第二页为七天时间轴及周目标/总结样例，没有真实计算或保存。
- 顶部空白区拖动，四边四角缩放，分隔线调整栏宽；时间轴普通滚轮滚动，初始显示 06:00。模拟时间块不可编辑。
- 设置中的“始终置顶”开启后脱离桌面 owner；关闭先清除原生 TOPMOST 再恢复桌面 owner。切换保留当前 HWND、位置、大小、页面和内存 UI 状态。
- “—”折叠为今日待办，显示真实今日日期与固定模拟列表；勾选与完整页同步，但重新启动会重置模拟勾选。折叠约 360×350 DIP，可缩放并滚动；“恢复完整视图”返回原完整矩形。
- 完整与折叠视图分别保存最后有效位置、尺寸及显示器；首次无折叠历史才默认右上角。重启恢复最后展开/折叠和桌面/置顶偏好；显示器不可用时回退主屏并确保窗口在工作区内。旧 v1 偏好未记录折叠矩形，首次迁移无法恢复从未保存的位置。
- 计划时间以“09:30 任务名称”显示，时间为浅绿色半粗体，无时间任务不加占位。新完成任务追加到同级完成组末尾，取消完成按原排序恢复；子任务独立分组，父子状态互不自动覆盖。本轮没有新增手动拖排或时间排序开关；相应排序约束由共享视图及自动检查覆盖。
- 组件和折叠视图均有设置入口。设置含置顶、原有贴边开关、不透明度、安全找回及退出。
- **× / Alt+F4 隐藏到托盘，不是退出。** 托盘双击或“显示 / 定位组件”找回；保留折叠状态。设置或托盘“退出”才真正退出。
- 托盘还提供置顶切换、设置及“安全找回（普通窗口）”。安全找回清除 owner/置顶并将窗口放回工作区；这种回退不保证 Win+D 后可见，可再用显示/定位重试桌面模式。
- owner 失效时回退为普通非置顶工具窗口；组件窗口意外关闭时最多自动重建一次回退窗口。托盘管理器独立于组件 HWND，仍可显式找回或退出。真实 Explorer 重启仍待验收。

| 输入 | 当前行为 |
|---|---|
| 普通滚轮 | 滚动列表或时间轴 |
| Ctrl + 滚轮 | 时间轴缩放尚未实现（Planned） |
| Tab / Enter / Space | 标准控件焦点、按钮及勾选操作 |
| Alt+F4 / × | 隐藏到托盘 |
| Ctrl+Z | 业务撤销尚未实现（Planned） |

## 本地数据、隐私、图片及备份

程序默认不联网，不保存真实日程。本阶段只在 `%LOCALAPPDATA%\DesktopCalendar\window-preferences.json` 保存窗口偏好，稳定 ID 为 `desktop-calendar-window`，schemaVersion 为 2，兼容 v1 读取迁移。写入先刷盘临时文件，再原子替换，保留上一版 `.bak`；主文件损坏尝试备份，无法恢复则提示并使用默认值。不认识的 schema 禁止覆盖并提示，不冒充已经实现业务迁移。

窗口偏好不包含模拟待办文字或完成状态；这些只存在进程内。手动保留偏好时，先退出程序，再复制 JSON 及 `.bak`；当前没有业务导入/导出界面。自动检查使用隔离临时目录，不覆盖正常偏好。截图、报告、日志、构建产物和个人备份不提交 Git。

以下均为 **Planned**，未来默认放在同一用户本地数据目录，独立于项目与程序部署位置：

- `planner-data.json`：真实数据的稳定 ID、可迁移 schema、原子保存与恢复。
- `assets/images/`：Markdown 本地图片与相对引用。
- `backups/`：ZIP 完整备份、校验、恢复预览及失败回滚；单独 JSON 不包含图片。
- `calendar-data/`：离线历法资料、来源及版本；仅用户主动更新时联网，不上传私人记录。

当前没有“检查资料更新”、图片导入或业务备份恢复按钮，也没有可运行的资料更新脚本。对应阶段实现后补充真实步骤，不提供虚构命令。

## 项目结构

```text
App.xaml / .cs              启动、单实例及检查入口
MainWindow.xaml / .cs       窗口外壳、折叠、设置与交互
DesktopWindowMode.cs        桌面 owner / 置顶 / 安全回退
DesktopSession.cs           独立托盘、窗口生命周期及找回
WindowPreferences.cs        窗口偏好验证、原子保存及备份读取
WindowGeometry.cs          几何、DPI 和原生窗口辅助
PrototypeViews.cs          两页模拟布局与固定时间轴
DemoContent.cs             内存模拟内容，不是业务数据模型
Strings.zh-CN.xaml          中文界面资源
PrototypeChecks.cs         无第三方依赖的自动检查
RevisionChecks.cs          同级排序、真实进程重启与窗口恢复回归
AGENTS.md                  用户提供的长期开发工作规则
DesktopCalendar.csproj     WPF / net10.0-windows
app.manifest               PerMonitorV2 声明
NuGet.Config               离线包源
build.ps1 / Start-Prototype.ps1  构建、检查及启动
docs/                      需求、决定、测试及研究记录
experiments/DesktopHostProbe/   已验收 O 的独立实验及历史对照
```

## 测试与已知限制

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

本轮 **155 PASS / 0 FAIL，构建 0 警告/错误**。除既有回归外，包含两块显示器 × 展开/折叠 × 桌面/置顶的八次真实进程重启，检查实际 HWND 矩形、显示器及模式；包含 v1 迁移、DPI/屏幕缺失回退和同级/子任务排序。报告为 `artifacts/p1-checks.txt`。原生几何不能替代实际像素，具体 GUI 证据见 [TESTING](docs/TESTING.md)。

可显式运行 `DesktopCalendar.exe --ui-check`：使用同一主界面、独立 `artifacts/ui-check-preferences`，临时呈现普通透明窗口供电脑工具选择；设置中提供仅诊断可见的“移到下一显示器”。这不改变正常启动方案，不用于证明桌面 owner、Win+D 或任务切换。代理已实际点击完整/折叠任务及子任务，观察时间、完成沉底/取消恢复；跨屏调整折叠窗口后退出重启，截图矩形一致；完整窗口和置顶偏好重启也一致。真实 owner 模式由八次进程原生检查补充；不重复已验收的 Win+D/任务切换项目。

尚未修复：月历增高仍只有一条待办、右上/左下缩小时抖动、跨 DPI 轻微抖动、贴边与 Snap 冲突。Win10、混合 DPI、Explorer 重启和休眠为后续门槛。本轮不新增时间轴缩放、不更换框架或实现业务数据。

独立实验的失败子窗口 A–F 历史与成功 O 方案见 [研究记录](docs/DESKTOP_MODE_RESEARCH.md)。O 没有 SetParent，不代表旧黑屏根因已查明；无需重做 A–F。

## 分支与后续计划

`main` 保持项目基线，`dev/p1.1` 存放当前开发；未人工验收的主程序不标记已验收版本。不强推，不覆盖远程历史，认证由用户本人完成。

P1.1-1 基线完成 → P1.1-2 O 核心人工通过 → **P1.1-3 本轮整合待验收** → P1.1-4 抖动/贴边 → P1.1-5 动态行数/缩放 → P1.1-6 Logo/开始菜单 → P1.1-7 综合验收。每个子阶段另行批准。P2–P8 的待办/Activity、周计划/总结、完整双语、历法、图片及备份目标全部保留在 [PRD](docs/PRD.md)。
