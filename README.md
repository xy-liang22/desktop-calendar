# Desktop Calendar

![Desktop Calendar Logo](assets/branding/logo-64.png)

Windows 本地桌面日历：第一页是月历、计划待办与实际时间轴，第二页是七天时间轴、周计划和周总结。待办是计划，Activity 是实际发生的时间；两者可以关联，状态独立。

**P3.1：真实待办编辑与视觉交互修订，自动验证范围内通过。** 快速分类/颜色、完整编辑关闭保存、月历层级/内联、HSV及深色菜单、父子完成快照已实现。三视图共享唯一可靠JSON。P4重复/持续管理、P5日程、P6周计划/总结、P7历法/完整双语仍Planned。接口见[DATA_MODEL](docs/DATA_MODEL.md)，证据及限制见[TESTING](docs/TESTING.md)。

## 已实现与 Planned

P3.2已增量实现（dev/p4，P4仍在后续授权范围）：编辑外部离开验证保存、IME上下文保护、行末展开及深度优先子项、三种实际测量换行（设置，默认两行）、子任务默认继承。新文档五类工作/家务/个人/家庭/健康及新色，旧分类/引用/颜色不替换；可主动补齐缺失预置。点击外部用于关闭的第一次点击不同时执行其他待办动作；确认框保持模态。当前可运行`bin/P32/net10.0-windows/DesktopCalendar.exe`，测试数据/报告在artifacts，真实开始菜单版本不自动更新。

| 能力 | 状态 |
|---|---|
| 默认桌面 owner、Win+D 保留、免任务栏/Alt+Tab、普通应用覆盖 | 当前 Win11 环境用户人工通过；顶层透明 WPF，无 SetParent/WS_CHILD |
| 深色半透明、圆角无白框、置顶往返、八方向缩放 | 已实现；偶发轻微缩放抖动为用户接受的限制 |
| 两页、分栏拖动、今日待办折叠、托盘设置/找回/退出 | 已实现；窗口位置/大小/显示器/视图/置顶偏好可恢复 |
| 真实月份导航、补位日期、动态完整行、+N、划线 | P3已实现，0/1/3/10条真实服务夹具、五/六周及小窗口检查 |
| Ctrl+滚轮缩放、重叠日程分列 | 已实现；两页比例独立记忆，完整24小时范围 |
| 同级状态分组、基础键/手动模式、子任务拖排 | P3 GUI已接入；完成先后不改变键，重启保留，拒绝跨父/跨状态组拖放 |
| SVG、九尺寸 ICO、托盘/标题/EXE/快捷方式正式图标 | 已实现，方案 A；图标资源从 SVG 离线生成 |
| 当前用户安装、更新、卸载、开始菜单入口 | 已实现，不需要管理员，不写注册表或PATH |
| 普通待办/子任务完整编辑、复制、进度、分类/优先级/颜色 | **P3 GUI已实现**；空状态不填示例，取消/空标题不误删 |
| 重复/持续、Activity、周目标/总结 | **P2服务已实现**；对应复杂管理UI仍Planned |
| 业务JSON原子保存、schema迁移、损坏恢复、并发冲突保护 | **P2服务已实现**，包含真实文件/独立进程测试 |
| 月历分类文字/底色切换、日期底色 | **P3 GUI已实现**；低饱和色板与HEX，自定义分类可归档保留历史 |
| 业务Ctrl+Z/重做、损坏/新版只读与恢复确认 | **P3 GUI已接入**；100个会话事务，关闭后不保留撤销历史 |
| Markdown图片导入/渲染、完整ZIP备份恢复 | **Planned**；已有安全资产引用数据API，无图片导入/ZIP界面 |
| 农历/节气、内地/HK假日、黄历、手动资料更新 | **Planned**；当前无资料更新按钮或脚本 |
| 完整中英切换、主题扩展、开机启动 | **Planned**；当前中文文案集中管理 |

月历使用实际日期与真实待办；日程时间轴和第二页仍是明确标注的固定示例，不代表真实记录或业务统计。现有周期/持续任务可读取和勾选，但系列/顺延管理不冒用普通删除，留给P4。范围以 [PRD](docs/PRD.md) 为准，未删除后续目标。

## 系统与开发环境

- 目标 Windows 10/11 x64；实际验证为 Windows 11 build 26200、两屏均144 DPI。Windows 10、真实混合DPI、Explorer重启和休眠恢复尚未全量验证。
- 运行需要 **Microsoft .NET Desktop Runtime 10 x64**。本机已有10.0.14，SDK也包含该运行时。选择框架依赖发布以减小体积，没有捆绑或自动下载运行时。
- 源码构建需现有 .NET 10 SDK x64（本机10.0.403）、Windows PowerShell和Git；可使用VS Code，不需要完整Visual Studio、Build Tools或额外扩展。
- 无第三方NuGet依赖。NuGet.Config清空包源，构建离线；脚本不安装软件、不改系统设置、不使用WSL。应用默认不联网。

## 安装、更新与卸载

[仓库](https://github.com/xy-liang22/desktop-calendar) 当前开发在 `dev/p3.1`，P3保留于`dev/p3`，暂无公开Release包。不会强行替换旧实例；源码安装：

```powershell
cd D:\projects\desktop-calendar
powershell -NoProfile -ExecutionPolicy Bypass -File .\Install-DesktopCalendar.ps1 -Action Install
```

这会离线构建/发布，然后仅写入：

- `%LOCALAPPDATA%\Programs\DesktopCalendar\versions\v<包指纹>\`：五个运行文件（含自有DesktopCalendar.Core.dll）及包清单。
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

也可运行 `bin\Release\net10.0-windows\DesktopCalendar.exe`。本轮直接启动 `bin\P31\net10.0-windows\DesktopCalendar.exe`，或 `Start-Prototype.ps1 -Configuration P31`。先从托盘退出旧实例；根目录历史名称“Desktop Calendar P1”的快捷方式指向最近构建配置，不代表仍用模拟待办。

```powershell
# 先发布，再做项目 artifacts 下的安装/卸载隔离测试
powershell -NoProfile -ExecutionPolicy Bypass -File .\Publish-DesktopCalendar.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-UserInstall.ps1
```

P2数据层独立构建与可靠性测试（无GUI，无新增依赖，不写个人数据）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-Planner.ps1
# 或只编译可供后续WPF项目引用的库
dotnet build .\Core\DesktopCalendar.Core.csproj -c Release
```

测试明细与隔离数据在忽略的 `artifacts/p2-checks/`；P3通过ProjectReference引用Core。新增WPF控件/弹窗/退出重启测试：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-P3.ps1 -Configuration P31
```

P3.1报告、隔离数据及渲染图在`artifacts/p3-checks/run-<GUID>/`，不写个人数据。新包含Core DLL，安装器兼容旧四文件包。业务schema3保存完成来源/快照，兼容v1/v2读取迁移；窗口偏好仍schema2。

图标维护：编辑 [logo.svg](assets/branding/logo.svg)，运行 `scripts/New-BrandAssets.ps1`（构建也会运行）。仅支持当前所需rect/line原语，未支持的新原语会报错，不能静默丢失。ICO含16/20/24/32/40/48/64/128/256；PNG64用于窗口标题，其他尺寸/预览保留在忽略的 `artifacts/branding`。不要手改生成的ICO/PNG。图标资产是明确授权的公共程序资源，不是用户图片。

## 桌面模式与日常操作

- 首次启动默认桌面模式：WPF顶层窗口以Explorer桌面为owner，普通应用可覆盖；不进入任务栏/Alt+Tab。不是Explorer子窗口，不靠持续置顶或抢焦点模拟桌面。
- 设置/托盘“始终置顶”开启后成为置顶组件；关闭时清除原生TOPMOST并恢复桌面owner，保留当前HWND、尺寸、页面和状态。
- 顶部空白区拖动，四边四角缩放；页面分隔线调整栏宽。月历用‹/›切月，“今天”回当日；单击选日期，双击或点击+N将焦点移到中栏完整待办管理，不自动创建记录。
- “—”显示今日真实待办和计划时间，可缩放/滚动；恢复完整视图返回保存矩形并选中今天、当前月份和第一页，不自动置顶。
- 开启贴边后，拖完整窗口到当前工作区左右边缘，松手并移开约900ms变为小把手；悬停展开。可见区域约9×48 DIP，实际命中保留28×110 DIP。设置打开时暂停收起，折叠/托盘隐藏不参与贴边。
- 月历按剩余高度显示完整待办，并为+N预留空间；极小时计数进入日期头。已完成划线、弱化；无时间事项不显示虚假时间占位。
- 普通滚轮滚动；在日程区域 **Ctrl+滚轮向上放大、向下缩小**，默认50、范围24–160 DIP/小时。刻度与块共享比例，重叠分列；靠近滚动边界时锚点安全夹入。切页保留各自比例/当前偏移，比例跨重启保存。
- 快速框可选带色点分类/覆盖色，输入任务或`09：30 任务名称`，Enter/＋保存；⋯更多选项填写日期、说明和优先级，无需先建后改。完整编辑的保存、×、Esc均验证保存，明确取消才放弃；无修改不写、失败留输入。默认分类日常/工作/生活，重要/紧急是优先级；旧分类引用保留。
- 月历默认根项，▸展开子项；双击月历文字原位改标题，Enter/有效失焦保存，**Esc取消**，空文本不删除。+N统计展开候选行的溢出项；高级属性仍用中栏双击/⋯完整编辑。颜色支持HSV/色相/24预设/HEX，强调色自动提亮、大背景柔化。
- 同级未完成在前、已完成在后，两组基础规则相同；完成先后不改键。拖文字仅同父/同状态组插入，不能变为别人的子任务，菜单可恢复时间排序。
- 默认进度递归平均直接子分支，叶可设手动值；子任务全完成令父自然完成。手动完成父会完成整个子树并保存原状态快照；取消仅在快照有效时恢复。独立修改子树完成/进度/结构后快照失效，不覆盖新选择；文字/颜色/排序不使快照失效。取消自然完成保留子任务，父保持未完成（即使100%），下次子树状态/结构变化恢复自动判定；整事务可撤销。
- ↶/↷撤销重做，Ctrl+Z / Ctrl+Y（或Ctrl+Shift+Z）；在有文字的输入框中保留文本本身的撤销，快速框为空时可撤销业务。撤销历史限当前会话100事务；复制子树与编辑多字段一次撤销。
- **× / Alt+F4隐藏到托盘，并不退出。** 托盘双击显示/定位，右键有置顶、设置、安全找回和退出。设置/托盘“退出”才结束程序，并等待当前提交；快速框未提交文字在退出前确认放弃。折叠/隐藏与同会话HWND找回保留草稿，不把草稿当保存成功。

## 数据、隐私、JSON、图片与备份

窗口/界面偏好保存在 `%LOCALAPPDATA%\DesktopCalendar\window-preferences.json`，稳定ID为 `desktop-calendar-window`，schemaVersion=2，兼容v1。完整/折叠矩形、显示器/工作区/DPI、页面/分栏、置顶/贴边/透明度、时间轴比例及P3所选日期/月份/月历颜色模式单独保存。临时文件刷盘后原子替换，保留 `.bak`；损坏尝试备份、未知schema禁止覆盖。缺失屏回退，重连不擅自跳回。

窗口偏好不包含待办/Activity正文或完成状态。需手动备份时先退出程序，再复制该JSON与 `.bak`；这不是业务导入/导出。安装目录、源码与数据目录分离，卸载不删除个人资料。

真实业务文件 `%LOCALAPPDATA%\DesktopCalendar\planner-data.json` 使用schema3，窗口偏好独立。v1/v2校验后内存迁移，读取不改原件；下次成功修改才写3并备份旧字节`.bak`，旧程序禁止降级覆盖。全新目录初始化日常/工作/生活，不填示例。完整临时写入、刷盘校验和替换、锁/队列/指纹仍保留。旧分类/引用不删，旧非零手动进度保留、零值转默认派生。所有测试仅隔离数据。

保存期间显示状态；失败保留旧显示和输入草稿，完整/折叠均提示，不显示成功。损坏时只读，不能用空数据覆盖；可“重新读取”，或从经过验证的候选确认恢复，原件保留为`.evidence-*`。较新schema禁止写入与恢复降级；外部修改提示冲突，先重新读取。恢复界面预览待办/实际日程数量及Revision并再次确认，不自动恢复。诊断不含用户正文，错误详情在提示的工具提示中；API边界见DATA_MODEL。

仍为 **Planned** 的界面与完整备份能力：

- `assets/images/`：已有资产ID注册和`asset://<GUID>`解析，仅解析注册信息，不读取任意路径；粘贴/导入/实际图片校验和Markdown渲染待后续接入。
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
| 托盘/搜索新Logo的实际画面 | 元数据、资源与部署启动已验证；P1.1时管道不可用，P2未修改图标/入口且未重复观察搜索收录/托盘表面 |
| P2日期与历史时区资料 | 使用Windows提供的TimeZoneInfo；现代DST及可控午夜跳跃测试通过，本机Samoa 2011历史资料未给出预期整日跳跃，保留NOT VERIFIED，不自行安装时区库或覆盖系统资料 |
| P3.1真实键鼠/屏幕 | computer-use重置后native pipe仍缺失；WPF事件/渲染非物理输入/桌面截图。仅需核对编辑Esc保存与内联Esc取消手感、菜单/颜色实际屏幕；不重复旧Win+D验收 |
| P3恢复与撤销 | 明确确认恢复；Undo/Redo仅当前会话100次事务。复杂重复/持续、日程/周目标UI、图片导入、ZIP恢复、跨会话撤销仍Planned |

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
Core/                       独立net10.0不可变实体、查询/命令、JSON存储与工作区
PlannerController.cs        单工作区、加载/保存状态、UI草稿、退出等待
PlannerView.cs / PlannerDialogs.cs  真实月历/待办、同级拖排与编辑/恢复弹窗
Strings.P3.zh-CN.xaml        新界面集中资源（完整语言切换P7）
TodoFields.cs / ColorField.cs / DarkPopups.xaml  共用字段、HSV与派生色、深色弹层
Core/TodoCompletion.cs       事务完成来源、递归归一与恢复快照
StageThreeChecks.cs / StageThreeOneChecks.cs / Test-P3.ps1  WPF及重启/保护测试
tests/PlannerChecks/        无第三方框架的数据、故障注入、并发/重启测试
Test-Planner.ps1             P2离线测试入口（仅artifacts隔离目录）
AGENTS.md / docs/            长期规范、PRD、决定、测试、研究
experiments/DesktopHostProbe/     旧实验，未复制失败的子窗口路线
```

综合回归：应用 **371 PASS / 0 FAIL**，安装 **14 PASS / 0 FAIL**，构建0警告/错误；实际用户目录的部署版也已启动验证并正常退出。自动检查涵盖负坐标、缺失屏/分辨率/DPI数学、最小布局、5/6周、缩放、排序、托盘/置顶、真实进程重启/重复启动/异常退出、ICO和安装边界。核心真实GUI证据包括用户对O、P1.1-4、P1.1-5的验收；代理新增渲染图不冒充桌面截图。细节、初次失败与未验证矩阵见 [TESTING](docs/TESTING.md)。

P3.1数据**194/0**（另1项历史Samoa资料NOT VERIFIED）、WPF编辑/颜色/层级/内联及独立重启/只读保护**111/0**、原窗口**374/0**、隔离安装**15/0**。已核对菜单正常/选中/禁用、HSV、分类/日期Popup、编辑器和完整/最小窗口渲染图，非桌面截图。发布包及安装测试隔离执行，未替换真实开始菜单旧版。

`main`与历史阶段分支保留，当前`dev/p3.1`；不强推，不标记未测环境全部通过。P4及以后须另行批准。
