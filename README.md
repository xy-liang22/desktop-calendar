# Desktop Calendar

面向 Windows 的本地日历、计划与实际时间记录工具。产品目标是融入桌面的组件：待办表示计划，Activity 表示实际发生的时间，两者可关联但状态独立。

**主程序仍为 P1 WPF 交互原型，使用固定模拟内容；尚未整合桌面组件模式。** P1.1-2 初版因测试组件不可见，人工验收未通过。2026-10-30 修复测试平台，改为先显示普通窗口；用户已确认新版组件可见、输入和计数可用。桌面嵌入仍未验收，不进入 P1.1-3。P1 的人工问题仍待修复。

## 当前功能与 Planned

| 功能 | 当前状态 |
|---|---|
| 深色半透明、透明圆角、无系统白框 | 已实现；背景不透明度可调，文字保持不透明 |
| 两页切换、三栏/七天时间轴布局 | 已实现，全部为模拟内容 |
| 移动、八方向缩放、分栏调整 | 已实现；特定方向缩小及跨 DPI 移动抖动待修复 |
| 右上角“待办”紧凑标签、恢复 | 已实现，仅标签，不是今日待办列表 |
| 普通窗口置顶、常驻托盘、隐藏/显示/退出 | 已实现，仍为 P1 旧语义 |
| 左右贴边收起 | 已有实现与内部事件检查；用户实测未触发，存在 Snap 冲突，不能视为可靠可用 |
| 默认桌面层、Win+D 后可见、免任务栏/Alt+Tab、可选托盘 | 主程序 **Planned**；独立宿主实验已提供，Win+D/真实输入及兼容性仍待人工验收 |
| 桌面/置顶切换、今日待办折叠及恢复记忆 | **Planned**：先做模拟数据与窗口偏好，不提前实现业务数据存储 |
| 月历按高度显示多条待办 | **Planned**：当前只显示一条或数量提示 |
| Ctrl+滚轮缩放时间轴 | **Planned**：当前每小时固定为 50 DIP；普通滚轮可滚动 |
| 正式 Logo、ICO、稳定部署及开始菜单入口 | **Planned**：先确认设计和用户级部署位置 |
| 真实待办、重复/顺延、Activity、周计划与统计 | **Planned**：P2–P8，当前不能编辑或保存真实记录 |
| Markdown 图片、JSON 迁移、原子保存、ZIP 备份恢复 | **Planned** |
| 农历/节气/内地及香港假日、手动资料更新 | **Planned** |
| 完整中英切换、开机启动 | **Planned**；当前仅中文，文案已集中管理 |

## 系统与开发要求

- 目标：Windows 10/11 x64。已实际检查 Windows build 26200、两块 150%（144 DPI）显示器；Win10 和混合 DPI 实机验收尚未完成。
- 运行：.NET Desktop Runtime 10 x64（SDK 已包含）。当前不是自包含安装包。
- 开发：Windows 本机 PowerShell、.NET 10 x64 SDK、Git；当前验证 SDK 版本为 10.0.403。可使用 VS Code，不要求完整 Visual Studio、Build Tools 或扩展。
- 不使用 WSL，不含第三方 NuGet 包。`NuGet.Config` 清空包源，使用已安装 SDK 的框架包离线构建。
- 缺少 SDK/运行时需自行从 Microsoft 官方渠道安装，项目脚本不会自动安装工具。

## 取得、构建与运行

仓库：<https://github.com/xy-liang22/desktop-calendar>。当前没有正式安装包、Release 下载或已验收发布标签；通过源码构建获得原型。不要覆盖已经存在的项目目录。

项目路径固定为 `D:\projects\desktop-calendar`。已有源码时，在 Windows PowerShell 执行：

```powershell
cd D:\projects\desktop-calendar
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

构建成功后，双击根目录生成的 **`Desktop Calendar P1.lnk`**，或双击 `bin\Release\net10.0-windows\DesktopCalendar.exe`。也可运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Start-Prototype.ps1
```

这是**当前开发原型路径**；开始菜单指向用户级稳定程序目录的方案为 Planned。快捷方式和构建产物不提交 Git。构建前先从托盘退出原型，避免程序文件被占用。多次启动可能产生多个实例；若原型已隐藏，请先从托盘恢复。

`-ExecutionPolicy Bypass` 仅作用于这一次 PowerShell 进程，不改变系统执行策略。脚本仅在当前进程禁用 .NET CLI 遥测和工作负载更新提示。

## 当前窗口操作

- **第一页**：较宽月历 + 当日待办示例 + 当日实际时间轴示例。点击日期只改变选中和标题，列表仍是固定示例。
- **第二页**：七天并排时间轴 + 周目标/周总结示例。目标进度和时长不是根据真实记录计算的。
- 拖动顶部空白区域移动窗口；四边四角缩放；竖向分隔线调整栏宽。较小时通过滚动查看部分区域。
- 时间轴默认从 06:00 显示，可向上滚到凌晨、向下滚到午夜；模拟时间块不可编辑。
- “始终置顶”目前只切换普通窗口置顶。**当前没有桌面模式入口，不保证 Win+D 后保留显示。**
- “—”折叠成当前屏工作区右上角的“待办”标签；点击或聚焦后按 Enter / Space 恢复。旧机制将在新折叠视图确认可用后替换。
- **“×”和 Alt+F4 当前均隐藏到托盘，不是退出。** 图标提示为 `Desktop Calendar`，可能在隐藏图标区；双击或右键“显示”恢复，右键“退出”才真正退出。
- 当前贴边逻辑需勾选开关、靠近左右边缘、鼠标离开后等待；用户实测存在失效与 Snap 冲突，暂不建议依赖它找回窗口。未全局关闭 Windows Snap。

| 输入 | 当前行为 |
|---|---|
| 普通滚轮 | 滚动当前时间轴或列表 |
| Ctrl + 滚轮 | 缩放尚未实现（Planned） |
| Tab / Enter / Space | 标准控件焦点及按钮操作；紧凑恢复键盘体验待人工复核 |
| Alt+F4 / × | 隐藏到常驻托盘 |
| Ctrl+Z | 业务编辑撤销尚未实现（Planned） |

默认桌面模式、默认关闭托盘和组件自身退出入口已写入 PRD，**文档确认不代表代码已切换为新行为**。

## 数据、图片、备份及隐私

当前原型不创建真实日程、不保存模拟待办或窗口偏好、不访问网络。模拟内容在 `DemoContent.cs`；检查结果/渲染图在 `artifacts/`，构建缓存在 `bin/`、`obj/` 及临时 CLI 目录。独立实验只记录窗口状态、点击数和输入字符数，不记录输入正文；日志在实验 `bin/` 内。这些不是用户记录，不上传 Git。

未来用户数据默认放在 `%LOCALAPPDATA%\DesktopCalendar`，独立于代码及部署目录。以下均为 **Planned**：

- `planner-data.json`：稳定 ID、schemaVersion、迁移、原子保存与损坏恢复。
- `assets/images/`：Markdown 本地图片与相对引用。
- `backups/`：ZIP 完整备份、校验、恢复预览及失败回滚。单独复制 JSON 不包含图片，不能算完整备份。
- `calendar-data/`：离线资料及来源/版本。仅用户主动更新时联网，不上传私人记录。

当前没有“导出备份”“恢复”“检查资料更新”按钮，也没有可运行的资料更新脚本。使用步骤将在对应功能实现后补齐，不提供虚构命令。未来手动复制完整数据目录应在应用退出后进行。

## 项目结构

```text
App.xaml / App.xaml.cs        应用资源、启动与检查入口
MainWindow.xaml / .xaml.cs    窗口外壳、模式、托盘及交互
WindowGeometry.cs            几何、DPI/原生窗口辅助
PrototypeViews.cs            两页模拟布局与固定时间轴
DemoContent.cs               模拟内容（不是用户数据）
Strings.zh-CN.xaml            中文界面文案
PrototypeChecks.cs           无第三方依赖的自动检查
DesktopCalendar.csproj       WPF / net10.0-windows
app.manifest                 PerMonitorV2 DPI 声明
NuGet.Config                 离线包源配置
build.ps1                    构建、快捷方式及可选检查
Start-Prototype.ps1           启动已构建原型
docs/PRD.md                  已确认需求与 Planned 目标
docs/DECISIONS.md            变更原因、授权及技术决定
docs/TESTING.md              历史检查、人工问题与阶段验证
docs/DESKTOP_MODE_RESEARCH.md 桌面方案比较、风险、证据及人工步骤
experiments/DesktopHostProbe/ 独立实验、控制台与无依赖检查器
```

## 测试与已知限制

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

检查器会短暂显示窗口，结束后退出，失败返回非零码。结果为 `artifacts/p1-checks.txt`，渲染图只含模拟内容。历史 P1 结果为 91 PASS / 0 FAIL；P1.1-1 只整理 Git 和文档，不把历史结果当作新增功能验收。

人工问题：月历增高仍只显示一条；右上/左下角缩小抖动；跨 DPI 移动轻微抖动；贴边未触发且可能与 Snap 冲突。主窗口行为未改。独立实验新版普通窗口的可见、输入和计数获用户确认；嵌入后可见性、Win+D、Explorer 重启及休眠仍待人工检查。详见 [TESTING](docs/TESTING.md)。

## 独立桌面模式实验（P1.1-2）

本实验不会启动或替换原日历。**默认只显示普通顶层诊断窗口，不嵌入 Explorer。** 普通组件采用浅色不透明内容、标准标题栏和缩放边框，故意保留任务栏入口。只有在组件内输入文字、点计数并点击确认按钮后，才能从控制台选择桌面实验。详细步骤见 [研究报告](docs/DESKTOP_MODE_RESEARCH.md#启动和人工验收)。

```powershell
cd D:\projects\desktop-calendar
powershell -NoProfile -ExecutionPolicy Bypass -File .\experiments\DesktopHostProbe\Start-Probe.ps1
```

已有构建可直接双击 `experiments\DesktopHostProbe\bin\Release\net10.0-windows\DesktopHostProbe.exe`。加 `-Test` 运行两组自动检查；加 `-BuildOnly` 仅构建。均使用现有 SDK 离线构建，不安装依赖。

应出现两个窗口：①“Desktop Calendar — 实验控制台（保留找回入口）”，含操作按钮、诊断与日志；②“Desktop Calendar — 普通窗口诊断组件”，浅色背景，紫色标题文字、输入框、计数和确认按钮。两个普通窗口均可在任务栏找回；仅嵌入模式移除组件任务栏样式。退出控制台同时结束其组件，组件“退出组件”只结束自身。

若组件消失，点控制台的 **“重建普通窗口 / 找回”**：结束本控制台启动的旧组件进程，新建从未挂入 Explorer 的普通窗口。测试文字及位置会重置，也需重新做可见性确认。“恢复普通模式”是同 HWND 脱离路径；重建按钮不依赖这一路径成功。

当前自动结果为 **20 + 7 项通过、0 失败**，覆盖普通启动、内容渲染、人工确认门槛、命令编号/应答/错误以及新进程找回；不据此认定肉眼可见。控制台分别显示 PID、HWND、Loaded、ContentRendered、父子关系、最小化、DWM cloaked、遮挡采样和命令结果。2026-10-30 用户已确认普通组件可见、输入和计数可用；嵌入后的视觉、找回、Win+D 和混合 DPI 等仍待验。两屏实际均为 144 DPI。当前普通诊断使用不透明渲染，不能代表原主窗口透明渲染兼容性已通过。

## 分支与后续计划

- `main`：项目基线，纳入现有 P1 源码及确认后的需求文档，不代表所有功能已验收。
- `dev/p1.1`：后续开发分支；未经人工验收不标记为已验收版本。
- 远程历史变化或分支冲突时停止说明，不强推、不覆盖、不自动合并无关历史。GitHub 认证由用户本人完成。

P1.1-1 基线/Git/文档 → P1.1-2 独立桌面层验证（当前等待人工反馈）→ P1.1-3 模式与今日待办折叠 → P1.1-4 抖动与贴边 → P1.1-5 动态行数及时间轴缩放 → P1.1-6 Logo/开始菜单 → P1.1-7 综合验收。**每步单独批准，当前不进入 P1.1-3 或 P2。**

P2–P8 的数据可靠性、待办/Activity、周计划与总结、Markdown 图片、离线历法、完整双语及备份恢复仍保留在 [PRD](docs/PRD.md)，未删减。
