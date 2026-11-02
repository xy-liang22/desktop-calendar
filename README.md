# Desktop Calendar

![Desktop Calendar Logo](assets/branding/logo-64.png)

Windows 本地桌面日历：第一页是月历、计划待办与实际时间轴，第二页是七天时间轴、周计划和周总结。待办是计划，Activity 是实际发生的时间；两者可以关联，状态独立。

**P1.1：开发完成，已验证范围内通过。** 用户已验收 P1.1-4/5 的核心交互；本轮完成品牌、用户级开始菜单入口和综合回归。当前仍为模拟业务内容，不能用于保存正式待办或日程。没有已知阻碍 P2 数据模型开发的重大窗口问题；**尚未开始 P2，需另行批准**。

**P2 进行中**：真实数据基础位于独立 Core 项目；当前窗口继续模拟，不保存正式业务内容。接口与存储约定见 [DATA_MODEL](docs/DATA_MODEL.md)，离线测试入口 Test-Planner.ps1。dev/p2 由用户本轮授权建立，P1.1 分支与 main 保留。

## 已实现与 Planned

| 能力 | 状态 |
|---|---|
| 默认桌面 owner、Win+D 保留、免任务栏/Alt+Tab、普通应用覆盖 | 当前 Win11 环境用户人工通过；顶层透明 WPF，无 SetParent/WS_CHILD |
| 深色半透明、圆角无白框、置顶往返、八方向缩放 | 已实现；偶发轻微缩放抖动为用户接受的限制 |
| 两页、分栏拖动、今日待办折叠、托盘设置/找回/退出 | 已实现；窗口位置/大小/显示器/视图/置顶偏好可恢复 |
| 月历动态完整行、+N、完成划线 | 已实现，0/1/3/10条模拟内容；五/六周及临界高度测试 |
| Ctrl+滚轮缩放、重叠日程分列 | 已实现；两页比例独立记忆，完整24小时范围 |
| 同级未完成在前、完成在后，取消恢复基础排序 | 已实现，含子任务；勾选仅本次运行有效 |
| SVG、九尺寸 ICO、托盘/标题/EXE/快捷方式正式图标 | 已实现，方案 A；图标资源从 SVG 离线生成 |
| 当前用户安装、更新、卸载、开始菜单入口 | 已实现，不需要管理员，不写注册表或PATH |
| 真实Todo/Activity、重复/顺延、周目标计算、业务撤销 | **Planned：P2–P8**，当前不是正式持久化业务功能 |
| 真实JSON迁移、Markdown图片、ZIP备份/恢复 | **Planned**；现有备份仅是窗口偏好 |
| 农历/节气、内地/HK假日、黄历、手动资料更新 | **Planned**；当前无资料更新按钮或脚本 |
| 完整中英切换、主题扩展、开机启动 | **Planned**；当前中文文案集中管理 |

主界面仍使用2026年10月及固定样例周，选日期仅切换演示标题；没有完整月份导航、日程编辑或业务统计。范围以 [PRD](docs/PRD.md) 为准，未删除后续目标。

## 系统与开发环境

- 目标 Windows 10/11 x64；实际验证为 Windows 11 build 26200、两屏均144 DPI。Windows 10、真实混合DPI、Explorer重启和休眠恢复尚未全量验证。
- 运行需要 **Microsoft .NET Desktop Runtime 10 x64**。本机已有10.0.14，SDK也包含该运行时。选择框架依赖发布以减小体积，没有捆绑或自动下载运行时。
- 源码构建需现有 .NET 10 SDK x64（本机10.0.403）、Windows PowerShell和Git；可使用VS Code，不需要完整Visual Studio、Build Tools或额外扩展。
- 无第三方NuGet依赖。NuGet.Config清空包源，构建离线；脚本不安装软件、不改系统设置、不使用WSL。应用默认不联网。

## 安装、更新与卸载

[仓库](https://github.com/xy-liang22/desktop-calendar) 当前在 `dev/p1.1` 开发，没有公开的安装包Release。获取源码后，在 Windows PowerShell 执行：

```powershell
cd D:\projects\desktop-calendar
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-DesktopCalendar.ps1 -Action Install
```

这会离线构建/发布，然后仅写入：

- `%LOCALAPPDATA%\Programs\DesktopCalendar\versions\v<包指纹>\`：四个运行文件及包清单。
- `%LOCALAPPDATA%\Programs\DesktopCalendar\installation.json`：当前版本信息。
- 当前用户开始菜单 `Programs\Desktop Calendar.lnk`：唯一入口，目标与图标都指向上述稳定版本目录的EXE。

不会创建桌面快捷方式、开机启动项或系统级安装，不修改PATH、注册表、任务栏、Snap和数据目录。项目根已有的P1快捷方式仍供开发使用，构建会更新其正式图标；它与正式开始菜单入口不同。

**先从旧实例托盘/设置退出，再从开始菜单搜索 `Desktop Calendar` 启动新版。** 安装/更新不擅自结束或重启现有程序。搜索尚未收录时，可直接打开当前用户开始菜单中的同名快捷方式；不需要改索引设置。

```powershell
# 更新到当前源码；重复执行不会重复创建入口
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-DesktopCalendar.ps1 -Action Update

# 查看当前版本（无构建、无下载）
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-DesktopCalendar.ps1 -Action Status

# 先从托盘退出安装版，再卸载
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-DesktopCalendar.ps1 -Action Uninstall
```

更新先校验包指纹/长度/SHA256，准备新版本，再原子切换快捷方式及当前版本记录；损坏包拒绝，旧入口保留。旧版本目录保留，避免覆盖运行文件。卸载仅删除本安装器管理的入口和未修改的程序文件；外来/修改文件保留，**不访问或删除 `%LOCALAPPDATA%\DesktopCalendar` 的个人数据、图片或备份**。运行中的安装版会阻止卸载，不强制结束进程。遇到同名外来快捷方式、陌生安装目录或重解析路径时拒绝覆盖。

如已准备好本机包，可免SDK安装，不重新发布：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Publish-DesktopCalendar.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-DesktopCalendar.ps1 -Action Install -PackagePath .\artifacts\package
```

`artifacts/package` 是本地发布产物，不提交Git。只选用自己从可信源码构建的包；包内哈希用于完整性校验，并非发行者数字签名。ExecutionPolicy Bypass仅作用本次PowerShell进程，不更改系统策略。

## 从源码构建、运行与测试

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\Start-Prototype.ps1
```

也可运行 `bin\Release\net10.0-windows\DesktopCalendar.exe`。构建前退出使用该目录的实例，避免文件占用。可指定 `-Configuration P117` 隔离构建；对应启动 `Start-Prototype.ps1 -Configuration P117`，本轮构建生成的根目录P1快捷方式也指向该配置。

```powershell
# 先发布，再做项目 artifacts 下的安装/卸载隔离测试
powershell -NoProfile -ExecutionPolicy Bypass -File .\Publish-DesktopCalendar.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-UserInstall.ps1
```

图标维护：编辑 [logo.svg](assets/branding/logo.svg)，运行 `scripts/New-BrandAssets.ps1`（构建也会运行）。仅支持当前所需rect/line原语，未支持的新原语会报错，不能静默丢失。ICO含16/20/24/32/40/48/64/128/256；PNG64用于窗口标题，其他尺寸/预览保留在忽略的 `artifacts/branding`。不要手改生成的ICO/PNG。图标资产是明确授权的公共程序资源，不是用户图片。

## 桌面模式与日常操作

- 首次启动默认桌面模式：WPF顶层窗口以Explorer桌面为owner，普通应用可覆盖；不进入任务栏/Alt+Tab。不是Explorer子窗口，不靠持续置顶或抢焦点模拟桌面。
- 设置/托盘“始终置顶”开启后成为置顶组件；关闭时清除原生TOPMOST并恢复桌面owner，保留当前HWND、尺寸、页面和状态。
- 顶部空白区拖动，四边四角缩放；页面分隔线调整栏宽。第一页面左侧月历优先较宽，第二页七天时间轴及右侧周总结。
- “—”切为今日待办，显示系统今日日期与带计划时间的模拟列表；可以缩放/滚动，“恢复完整视图”返回独立保存的完整矩形。不自动置顶。
- 开启贴边后，拖完整窗口到当前工作区左右边缘，松手并移开约900ms变为小把手；悬停展开。可见区域约9×48 DIP，实际命中保留28×110 DIP。设置打开时暂停收起，折叠/托盘隐藏不参与贴边。
- 月历按剩余高度显示完整待办，并为+N预留空间；极小时计数进入日期头。已完成划线、弱化；无时间事项不显示虚假时间占位。
- 普通滚轮滚动；在日程区域 **Ctrl+滚轮向上放大、向下缩小**，默认50、范围24–160 DIP/小时。刻度与块共享比例，重叠分列；靠近滚动边界时锚点安全夹入。切页保留各自比例/当前偏移，比例跨重启保存。
- 未完成始终在同级完成组之前，两组内部使用相同基础排序。完成先后不参与排序，取消恢复原本相对位置；子任务独立，父子状态不自动互改。模拟勾选退出后重置。
- **× / Alt+F4隐藏到托盘，并不退出。** 托盘双击显示/定位，右键有置顶、设置、安全找回和退出。设置/托盘“退出”才结束程序。Tab、Enter、Space遵循标准按钮/勾选行为；Ctrl+Z业务撤销尚未实现。

## 数据、隐私、JSON、图片与备份

当前只持久化 `%LOCALAPPDATA%\DesktopCalendar\window-preferences.json`，稳定ID为 `desktop-calendar-window`，schemaVersion=2，兼容v1。完整/折叠物理矩形、显示器/工作区/DPI、页面/分栏、置顶、贴边、透明度及两页时间轴比例分别保存。临时文件刷盘后原子替换，保留 `.bak`；损坏尝试备份，未知schema禁止覆盖。缺失显示器回退可用工作区，重连不擅自跳回。

窗口偏好不包含待办/Activity正文或完成状态。需手动备份时先退出程序，再复制该JSON与 `.bak`；这不是业务导入/导出。安装目录、源码与数据目录分离，卸载不删除个人资料。

以下仍为 **Planned**，不是现有可用功能：

- `planner-data.json`：真实业务的稳定ID、可迁移schema、原子保存与恢复。
- `assets/images/`：Markdown图片及相对引用；当前没有导入界面。
- `backups/`：完整ZIP备份、校验、恢复预览与失败回滚；单独JSON不能包含未来图片。
- `calendar-data/`：离线历法、假日来源及版本；未来仅主动手动更新时联网，不上传私人记录。当前不能把固定模拟日期当作真实历法资料。

## 已知限制、恢复与常见问题

| 问题 | 处理 / 边界 |
|---|---|
| 找不到组件 | 托盘可能在隐藏图标区；双击或选择显示/定位。安全找回会退到普通非置顶工具窗口，不保证Win+D保留 |
| 更新后仍见旧图标/旧版本 | 先从旧实例托盘退出，再用开始菜单启动；不会强行替换运行中的版本 |
| 重复启动提示已运行 | 正常单实例。异常退出后的遗留互斥体使用非阻塞获取/abandoned恢复，不凭对象存在直接误判 |
| Explorer owner失效 | 回退并保留托盘找回；组件HWND丢失最多自动重建一次。实际Explorer重启/休眠尚未全量验证 |
| 无法安装/运行 | 检查Desktop Runtime 10 x64；无SDK时使用预构建包参数。脚本不会自动下载工具 |
| 同名入口、修改的程序文件、重解析目录 | 安装器拒绝覆盖或删除，不以强制方式修复陌生内容；保留个人数据，检查提示后处理 |
| 轻微缩放抖动 | 用户已接受的偶发限制，无高频补偿或窗口系统重构 |
| Win10/混合DPI/休眠兼容 | 尚未实机全量验证；纯数学DPI/拓扑通过不能等同硬件兼容通过 |
| 托盘/搜索新Logo的实际画面 | 元数据、资源与部署启动已验证；本轮电脑控制管道不可用，Windows搜索收录/托盘表面仍需实际观察 |

## 项目结构与验证基线

```text
App.xaml / .cs               启动、单实例、窗口生命周期
MainWindow.xaml / .cs        外壳、折叠、设置、拖动缩放
DesktopWindowMode.cs         已验收的桌面owner / 置顶 / 回退
DesktopSession.cs            托盘及窗口偏好生命周期
WindowPreferences.cs         原子偏好、备份与兼容读取
WindowGeometry.cs            物理坐标/DPI几何
CalendarPreviewCell.cs       固定信息与动态完整待办行
TimelineView.cs              比例、滚动锚点、重叠布局
PrototypeViews.cs / DemoContent.cs  两页模拟布局与数据
AppBrand.cs / assets/branding/     品牌资源与SVG源
PrototypeChecks / RevisionChecks / StageFiveChecks / LifecycleChecks.cs
                             无外部测试框架的回归与进程检查
Install-DesktopCalendar.ps1  当前用户安装/更新/卸载/状态
Publish-DesktopCalendar.ps1  离线框架依赖发布
scripts/                    图标生成与安装隔离测试
AGENTS.md / docs/            长期规范、PRD、决定、测试、研究
experiments/DesktopHostProbe/     旧实验，未复制失败的子窗口路线
```

综合回归：应用 **371 PASS / 0 FAIL**，安装 **14 PASS / 0 FAIL**，构建0警告/错误；实际用户目录的部署版也已启动验证并正常退出。自动检查涵盖负坐标、缺失屏/分辨率/DPI数学、最小布局、5/6周、缩放、排序、托盘/置顶、真实进程重启/重复启动/异常退出、ICO和安装边界。核心真实GUI证据包括用户对O、P1.1-4、P1.1-5的验收；代理新增渲染图不冒充桌面截图。细节、初次失败与未验证矩阵见 [TESTING](docs/TESTING.md)。

`main` 保持原基线，开发提交在 `dev/p1.1`，不强推、不标记未测环境全部通过。P1.1作为后续开发技术基础已具备条件；P2–P8仍待逐阶段批准，目标均保留在PRD。
