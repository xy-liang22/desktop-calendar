# Desktop Calendar

![Desktop Calendar Logo](assets/branding/logo-64.png)

Windows 本地桌面日历：第一页是月历、计划待办与实际时间轴，第二页是七天时间轴、周计划和周总结。待办是计划，Activity 是实际发生的时间；两者可以关联，状态独立。

**P6：周目标、分类统计与 Markdown 周总结已接入（0.6.0），自动与控件渲染验证范围内通过。** 保留全部待办、周期、真实日程及桌面组件基础，新增P5.1拖动编辑器、锚定详情和本地大图。P7离线历法/完整双语仍Planned。实际鼠标手感与桌面像素仍有工具验证边界，见[TESTING](docs/TESTING.md)；接口见[DATA_MODEL](docs/DATA_MODEL.md)。

## 已实现与 Planned

P5.1已独立提交332b73e（dev/p6）：待办/日程标题区可拖动并夹入当前屏；详情为主窗口内的锚定卡片，点击别处关闭，长事件优先避让。备注图按容器宽度显示，可点开大图，适应/100%/滚轮缩放/拖动平移，Esc关闭后保留草稿与滚动。跨天详情按所点击片段的日期/偏移锚定。

P3.2已实现编辑外部离开验证保存、IME上下文保护、行末展开及深度优先子项、三种实际测量换行（设置，默认两行）、子任务默认继承。新文档五类工作/家务/个人/家庭/健康及新色，旧分类/引用/颜色不替换；可主动补齐缺失预置。点击外部用于关闭的第一次点击不同时执行其他待办动作；确认框保持模态。当前可运行`bin/P6/net10.0-windows/DesktopCalendar.exe`，测试数据/报告在artifacts，真实开始菜单版本不自动更新。

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
| 周期规则、单次/未来/全部未完成范围、持续历史/仅最新 | **P4 GUI已实现**；按需生成，保护已完成历史，支持独立实例编辑/完成/取消、复制及Undo/Redo |
| Activity、多时间段、跨天/重叠、时间轴预览拖动/缩放/详情 | **P5 GUI已实现**；原JSON与事务撤销，精确时间输入、待办及已有周目标关联 |
| 周目标/统计/总结 | **P6 GUI已实现**；三种目标、单来源/关联/覆盖、分类去重、自由Markdown/小节/独立统计快照 |
| 业务JSON原子保存、schema迁移、损坏恢复、并发冲突保护 | **P2服务已实现**，包含真实文件/独立进程测试 |
| 月历分类文字/底色切换、日期底色 | **P3 GUI已实现**；低饱和色板与HEX，自定义分类可归档保留历史 |
| 业务Ctrl+Z/重做、损坏/新版只读与恢复确认 | **P3 GUI已接入**；100个会话事务，关闭后不保留撤销历史 |
| Markdown/纯文本备注、实时预览、本地图片、emoji/心情 | **P5已实现**；支持图片粘贴/选择/拖入，禁远程自动加载，链接只复制地址 |
| 完整ZIP备份恢复、孤儿图片清理管理 | **Planned**；不自动删图片，保留撤销与备份引用 |
| 农历/节气、内地/HK假日、黄历、手动资料更新 | **Planned**；当前无资料更新按钮或脚本 |
| 完整中英切换、主题扩展、开机启动 | **Planned**；当前中文文案集中管理 |

月历、当日日程、七天时间轴及周计划/总结均为真实数据，无模拟统计。范围以 [PRD](docs/PRD.md) 为准，未删除后续目标。

## 系统与开发环境

- 目标 Windows 10/11 x64；实际验证为 Windows 11 build 26200、两屏均144 DPI。Windows 10、真实混合DPI、Explorer重启和休眠恢复尚未全量验证。
- 运行需要 **Microsoft .NET Desktop Runtime 10 x64**。本机已有10.0.14，SDK也包含该运行时。选择框架依赖发布以减小体积，没有捆绑或自动下载运行时。
- 源码构建需现有 .NET 10 SDK x64（本机10.0.403）、Windows PowerShell和Git；可使用VS Code，不需要完整Visual Studio、Build Tools或额外扩展。
- 无第三方NuGet依赖。NuGet.Config清空包源，构建离线；脚本不安装软件、不改系统设置、不使用WSL。应用默认不联网。

## 安装、更新与卸载

[仓库](https://github.com/xy-liang22/desktop-calendar) 当前开发在 `dev/p6`，P5保留于`dev/p5`，暂无公开Release包。不会强行替换旧实例；源码安装：

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

也可运行 `bin\Release\net10.0-windows\DesktopCalendar.exe`。本轮直接启动 `bin\P6\net10.0-windows\DesktopCalendar.exe`，或 `Start-Prototype.ps1 -Configuration P6`。先从托盘退出旧实例；根目录历史名称“Desktop Calendar P1”的快捷方式指向最近构建配置，不代表仍用模拟数据。

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
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-P3.ps1 -Configuration P6
```

P3/P4报告、隔离数据及渲染图在`artifacts/p3-checks/run-<GUID>/`，不写个人数据。发布包含Core DLL，安装器兼容旧四文件包。业务schema3继续保存完成来源/快照及周期版本/例外，不增加第二套存储；兼容v1/v2读取迁移，窗口偏好仍schema2。

图标维护：编辑 [logo.svg](assets/branding/logo.svg)，运行 `scripts/New-BrandAssets.ps1`（构建也会运行）。仅支持当前所需rect/line原语，未支持的新原语会报错，不能静默丢失。ICO含16/20/24/32/40/48/64/128/256；PNG64用于窗口标题，其他尺寸/预览保留在忽略的 `artifacts/branding`。不要手改生成的ICO/PNG。图标资产是明确授权的公共程序资源，不是用户图片。

## 桌面模式与日常操作

- 首次启动默认桌面模式：WPF顶层窗口以Explorer桌面为owner，普通应用可覆盖；不进入任务栏/Alt+Tab。不是Explorer子窗口，不靠持续置顶或抢焦点模拟桌面。
- 设置/托盘“始终置顶”开启后成为置顶组件；关闭时清除原生TOPMOST并恢复桌面owner，保留当前HWND、尺寸、页面和状态。
- 顶部空白区拖动，四边四角缩放；页面分隔线调整栏宽。月历用‹/›切月，“今天”回当日；单击选日期，双击或点击+N将焦点移到中栏完整待办管理，不自动创建记录。
- “—”显示今日真实待办和计划时间，可缩放/滚动；恢复完整视图返回保存矩形并选中今天、当前月份和第一页，不自动置顶。
- 开启贴边后，拖完整窗口到当前工作区左右边缘，松手并移开约900ms变为小把手；悬停展开。可见区域约9×48 DIP，实际命中保留28×110 DIP。设置打开时暂停收起，折叠/托盘隐藏不参与贴边。
- 月历按剩余高度显示完整待办，并为+N预留空间；极小时计数进入日期头。已完成划线、弱化；无时间事项不显示虚假时间占位。
- 普通滚轮滚动；在日程区域 **Ctrl+滚轮向上放大、向下缩小**，默认50、范围24–160 DIP/小时。刻度与块共享比例，重叠分列；靠近滚动边界时锚点安全夹入。切页保留各自比例/当前偏移，比例跨重启保存。
- 快速框可选带色点分类/覆盖色，输入任务或`09：30 任务名称`，Enter/＋保存；⋯更多选项填写日期、说明和优先级，无需先建后改。完整编辑的外部点击、保存、×、Esc均验证保存，明确取消才放弃；无修改不写、失败留输入。新文档默认工作/家务/个人/家庭/健康，重要/紧急是优先级；旧分类引用保留。
- 月历默认根项，行末‹展开/⌄折叠子项，类型标记固定在文字右侧；双击月历文字原位改标题，Enter/有效失焦保存，**Esc取消**，空文本不删除。+N统计展开候选行的溢出项；高级属性仍用中栏双击/⋯完整编辑。颜色支持HSV/色相/24预设/HEX，强调色自动提亮、大背景柔化。
- 月历设置可选单行省略、最多两行（默认）、尽量完整换行；展开子项紧随父项并缩进，占用日期格已有空间，不撑高某一周。长内容可经提示、+N或中栏完整查看。
- 同级未完成在前、已完成在后，两组基础规则相同；完成先后不改键。拖文字仅同父/同状态组插入，不能变为别人的子任务，菜单可恢复时间排序。
- 默认进度递归平均直接子分支，叶可设手动值；子任务全完成令父自然完成。手动完成父会完成整个子树并保存原状态快照；取消仅在快照有效时恢复。独立修改子树完成/进度/结构后快照失效，不覆盖新选择；文字/颜色/排序不使快照失效。取消自然完成保留子任务，父保持未完成（即使100%），下次子树状态/结构变化恢复自动判定；整事务可撤销。
- ↶/↷撤销重做，Ctrl+Z / Ctrl+Y（或Ctrl+Shift+Z）；在有文字的输入框中保留文本本身的撤销，快速框为空时可撤销业务。撤销历史限当前会话100事务；复制子树与编辑多字段一次撤销。
- **× / Alt+F4隐藏到托盘，并不退出。** 托盘双击显示/定位，右键有置顶、设置、安全找回和退出。设置/托盘“退出”才结束程序，并等待当前提交；快速框未提交文字在退出前确认放弃。折叠/隐藏与同会话HWND找回保留草稿，不把草稿当保存成功。

## 周期与持续待办（P4）

- 新增的“更多选项”中选择普通、持续保留历史、持续仅最新或周期性；快速Enter仍创建普通待办。周期规则支持每天/间隔N天、指定星期/间隔N周、指定每月日期、开始日、可选结束日（含）与次数。每月不存在的日期跳过，次数从规则开始日计算，不在中途修改时重新计数。
- 周期标记为↻，持续标记为←，均在正文右侧、展开按钮左侧。双击/⋯编辑周期实例，默认“仅本次”；标题、时间、完成等不会修改其他发生。改变规则用⋯“编辑重复规则”。范围选项明确区分“仅本次”“从本次原发生日开始（含）”“全部未完成（保留已完成）”；全部也包含所选日之前未完成的实例，不按屏幕上移动后的日期界定。
- 周期取消/删除会打开专门范围确认；单次已完成须先取消完成再取消本次，较大范围保留已完成历史及必要上下文。规则范围修改前再次确认，单次例外保留未被明确改动的属性。单次子任务仅在本次存在；范围新增子项进入模板，但不向已完成的历史分支补入新待办。
- 持续未完成项每天按当前日期自动延续；保留历史显示原日至当前日，仅最新只显示最新日，两种模式共享同一个ID/完成状态。完成后冻结原有显示日期、划线；取消完成继续延续。编辑可切换显示模式；⋯“顺延日期”可显式移到更晚一天。今日折叠和主界面使用同一查询。
- 复制周期实例或持续子树会生成独立普通待办，清空完成/顺延/系列状态，不另建系列。当前不提供已有普通任务与系列间的原地类型转换；请在新增时选择类型。实例日期修改仅作用于所选节点，独立子项的日期不随之改写。所有操作沿用原子JSON、事务撤销与失败反馈。

## 实际日程与备注（P5）

1. 当日右栏或周视图按＋新增，或在待办⋯选择“记录实际时间”。空白时间轴拖出范围会打开预填草稿。标题、摘要、emoji、心情、分类色及备注可一次填写；完整编辑外部点击/×/Esc验证保存，取消放弃，错误保留输入。创建实际记录不会勾选完成待办。
2. 时间段输入`yyyy-MM-dd HH:mm`（可带秒），起止可跨日；＋时间段增加不连续记录，移除此段只改草稿。时区使用Windows/受支持的时区ID，右侧UTC偏移可明确夏令时重复小时；非法/倒置时间不会自动修正。已有亚秒时间未改则保留。周目标可在展开区关联或当场新建，完整管理见下节。
3. 单击块打开小型详情卡，双击完整编辑；详情区分删除整条与仅删除所选段，后者最后一段时禁用。复制生成新Activity与新时间段ID。跨日/跨周显示裁片，编辑仍回到原记录；多段与重叠分别可选。
4. 拖块移动所选段，拖上/下边缘调整该端点，先预览后松开提交；默认5分钟网格，按Shift为1分钟。Esc/右键/捕获丢失或视口外松开取消；Ctrl缩放取消活动预览。保存失败保留预览，Enter重试、Esc取消；可Ctrl+Z撤销成功操作。普通滚轮/两页独立缩放仍保留。
5. 备注选普通文本或Markdown，左侧源码、右侧实时预览；支持标题、强调、列表、引用、代码块、表格、链接、本地图片。HTML只显示文字，远程图片显示占位，链接点击只复制地址。预览超100000字会明确提示截断，原文保留；不是完整CommonMark或Typora编辑器。
6. 在源码框Ctrl+V粘贴截图，或选“插入本地图片”、拖入图片文件；验证后规范PNG并插入`assets/images/<hash>.png`相对引用。单图限制20MiB、边长8192、2400万像素；重复内容复用。图片失败不清输入，缺失/损坏/不安全引用显示占位。取消草稿、删备注或删日程不会物理删图，避免损坏其他笔记、撤销及备份；后续提供清理与完整ZIP。

显示时区沿用工作区当地时区，单条记录保留自己的时区供编辑/详情使用。DST重复小时分列并显示UTC偏移，跳时留空；统计按真实绝对秒数计算，不按图形高度猜时长。

## 周计划、统计与总结（P6）

- 第二页右侧“新建周目标”，可选数量、累计实际时长、勾选三类型。时长输入单位为小时，内部存秒；可关联多个待办/日程，但自动来源只选一种。实际时长禁止Todo来源；勾选的自动来源判断至少一个匹配对象，需要全部完成请用数量目标。类型/来源变更会明确确认，保留原关联。
- ⋯编辑/删除目标。手动覆盖留空或点“恢复自动统计”即返回自动值；界面标明自动/手动。无自动来源的勾选目标可直接点击。显式关联的跨周待办保留，并提示数量仍按所有关联完成项计算；不静默丢弃。
- 待办/日程编辑的“周目标关联”可选本周或显示其他周，支持当场新建。新目标独立保存，当前事项保存后才关联，取消事项不会删除新目标；周期仅关联当前实例，新建系列关联首个有效发生，不把模板当Todo。
- 设置可改每周首日及月历周计划布局：周右侧窄栏显示摘要/进度，或每周摘要条。条可上下展开浮动摘要，不挤高某周；窄窗口自动使用条保护日期，窄高度压缩留白。点击周入口管理整周；七天视图前后周与月历所选日期联动。
- 分类时长按本周实际边界裁剪，同类别合并重叠、不同类别独立累计，百分比总和允许超过100%。分母明确为本周自然实际小时数（DST可能167/169），另列全局去重总时长，图形与文字实时刷新。
- “编辑周总结”提供自由Markdown、可增删小节及统计摘要。点“插入当前统计摘要”生成独立快照，保存时与正文/小节一次事务提交，不覆盖手写正文；重新计算日程不会改旧快照。明确取消放弃草稿，失败保留内容；成功编辑可用业务Ctrl+Z撤销。总结也支持原本地图与大图查看。
- 周身份包含创建时首日和时区，改偏好不迁移旧目标/总结；切回原首日可看原周记录，其他周目标也可从关联列表查看。新目标自动关联该周已有总结，首次保存总结为同周目标补上引用，不改变统计来源。周首日/布局保存在原偏好文件，业务仍唯一schema3 JSON。

## 数据、隐私、JSON、图片与备份

窗口/界面偏好保存在 `%LOCALAPPDATA%\DesktopCalendar\window-preferences.json`，稳定ID为 `desktop-calendar-window`，schemaVersion=2，兼容v1。完整/折叠矩形、显示器/工作区/DPI、页面/分栏、置顶/贴边/透明度、时间轴比例及P3所选日期/月份/月历颜色模式单独保存。临时文件刷盘后原子替换，保留 `.bak`；损坏尝试备份、未知schema禁止覆盖。缺失屏回退，重连不擅自跳回。

窗口偏好不包含待办/Activity正文或完成状态。需手动备份时先退出程序，再复制该JSON与 `.bak`；这不是业务导入/导出。安装目录、源码与数据目录分离，卸载不删除个人资料。

真实业务文件 `%LOCALAPPDATA%\DesktopCalendar\planner-data.json` 使用schema3，窗口偏好独立。v1/v2校验后内存迁移，读取不改原件；下次成功修改才写3并备份旧字节`.bak`，不降级覆盖。P4复用已有周期版本/排除键/节点身份注册，无新JSON字段；P3.1旧Core读取P4夹具并逐字节往返通过，但旧GUI不提供新管理入口。全新目录初始化上述五类，不填示例。完整临时写入、刷盘校验和替换、锁/队列/指纹仍保留。旧分类/引用不删，旧非零手动进度保留、零值转默认派生。所有测试仅隔离数据。

保存期间显示状态；失败保留旧显示和输入草稿，完整/折叠均提示，不显示成功。损坏时只读，不能用空数据覆盖；可“重新读取”，或从经过验证的候选确认恢复，原件保留为`.evidence-*`。较新schema禁止写入与恢复降级；外部修改提示冲突，先重新读取。恢复界面预览待办/实际日程数量及Revision并再次确认，不自动恢复。诊断不含用户正文，错误详情在提示的工具提示中；API边界见DATA_MODEL。

图片现已在同一数据根目录的`assets/images/`，JSON仅登记元数据，不含Base64。整套手动备份请先退出程序，复制整个`%LOCALAPPDATA%\DesktopCalendar`目录；仅JSON不能包含图片。图片路径与资产ID跨备份位置保持稳定，渲染只读取注册且校验通过的本地文件，拒绝任意路径/网络/重解析点。

仍为 **Planned** 的完整备份能力：

- 孤儿图片清理管理：当前保守保留，暂不提供自动回收，不能按当前一篇备注的引用直接删图。
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
| P5真实键鼠/屏幕 | computer-use原生管道缺失；WPF事件/渲染非物理输入/桌面截图。仅需两组观察：实际拖拽/单双击，以及系统剪贴板粘图/文件拖入；不重复旧Win+D验收 |
| 数据恢复与撤销 | 明确确认恢复；Undo/Redo仅当前会话100次事务。ZIP恢复、孤儿图片清理、跨会话撤销仍Planned |

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
ActivityTimelineView.cs / ActivityWeekView.cs  两页真实时间段、预览手势与周导航
ActivityDialogs.cs           多时间段编辑、详情及关联
ActivityDetailCard.cs / ImageViewer.cs / WindowGestures.cs  锚定详情、大图与编辑器标题拖动
WeekGoalDialogs.cs / WeekSummaryView.cs / WeeklyReviewDialog.cs  周目标、统计与总结
MonthWeekPlan.cs             月历两种周计划布局
MarkdownNotes.cs / LocalImages.cs  离线备注预览与可靠本地图片
PrototypeViews.cs / DemoContent.cs  显式测试fixture中的历史模拟布局
AppBrand.cs / assets/branding/     品牌资源与SVG源
PrototypeChecks / RevisionChecks / StageFiveChecks / LifecycleChecks.cs
                             无外部测试框架的回归与进程检查
Install-DesktopCalendar.ps1  当前用户安装/更新/卸载/状态
Publish-DesktopCalendar.ps1  离线框架依赖发布
scripts/                    图标生成与安装隔离测试
Core/                       独立net10.0不可变实体、查询/命令、JSON存储与工作区
PlannerController.cs        单工作区、加载/保存状态、UI草稿、退出等待
PlannerView.cs / PlannerDialogs.cs  真实月历/待办、同级拖排与编辑/恢复弹窗
RecurrenceFields.cs / PlannerRecurrenceDialogs.cs  周期规则与明确范围/顺延
Strings.P3.zh-CN.xaml        新界面集中资源（完整语言切换P7）
TodoFields.cs / ColorField.cs / DarkPopups.xaml  共用字段、HSV与派生色、深色弹层
Core/TodoCompletion.cs       事务完成来源、递归归一与恢复快照
StageThreeChecks.cs / StageThreeOneChecks.cs / Test-P3.ps1  WPF及重启/保护测试
StageThreeTwoChecks.cs / StageFourChecks.cs / StageActivityChecks.cs  换行/周期/日程与图片检查
tests/PlannerChecks/        无第三方框架的数据、故障注入、并发/重启测试
Test-Planner.ps1             P2离线测试入口（仅artifacts隔离目录）
AGENTS.md / docs/            长期规范、PRD、决定、测试、研究
experiments/DesktopHostProbe/     旧实验，未复制失败的子窗口路线
```

最新P6综合回归：数据 **253 PASS / 0 FAIL / 1 NOT VERIFIED**（历史Samoa资料），P3–P6控件/编辑/独立进程重启 **234/0**，原窗口 **374/0**，隔离安装 **15/0**；构建0警告/错误。覆盖目标来源/类型/覆盖、周总结分离与重启、真实Activity/多段/DST、图片保护、负坐标/缺屏/DPI数学、最小布局、5/6周、缩放、排序、托盘/置顶、进程恢复及安装边界。核心桌面仍有用户历史验收，新增控件图不冒充桌面截图。初次失败与未验证矩阵见 [TESTING](docs/TESTING.md)。

已核对目标编辑器、月历两布局、最小窗口、统计图、总结源码/实时预览、锚定详情与大图，非桌面截图。computer-use管道不可用，最终只留两组必要观察：编辑器/详情/大图的实际拖动手感，以及月历周计划/周侧栏的阅读体验；不重复旧Win+D和数据矩阵。发布安装测试隔离执行，未替换真实开始菜单旧版。

`main`与历史阶段分支保留，当前`dev/p6`；P5.1与P6分别提交，不强推，不标记未测环境全部通过。P6结束后停止，P7须另行批准。
