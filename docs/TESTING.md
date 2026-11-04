# 阶段验证记录

## 2026-11-04：0.8.2 连续交互修订

用户在a821d46之后连续授权类型互转、时间标点、完成子任务折叠、可编辑显示快捷键、彩色emoji、临时置顶边缘把手、固定周区间、字号和月历增亮。仍在dev/p7-p8，不是正式Release。数据schema3/偏好schema2兼容，未使用私人数据、安装新依赖或改全局设置。

| 验证 | 结果 | 证据 |
|---|---|---|
| 数据/可靠性及新类型转换 | **343 PASS / 0 FAIL / 1 NOT VERIFIED** | artifacts/interaction-data-final2/checks.txt；未验仍为历史Samoa2011时区资料 |
| 最终控件/真实WPF Window/独立重启 | **392 PASS / 0 FAIL** | artifacts/p3-checks/run-3a6478769e314246ad40a81322d80f57 |
| 窗口/owner/托盘/缩放/恢复回归 | **374 PASS / 0 FAIL** | artifacts/p1-checks.txt；0.8.2，正常用户桌面会话 |
| 3分钟字体/语言/视图资源监测 | **4 PASS / 0 FAIL** | artifacts/quality-04e2165678ce43f38057925e5095cc36；182.31秒，空闲CPU0.82%单逻辑核心，私有内存增12.36MiB，句柄减少7 |
| 本轮包/用户安装/开始菜单 | **待代码提交后发布构建及安装复验** | 尚未记为PASS，详见后续更新 |
| 构建 | **PASS，0警告/0错误** | bin/InteractionFix；仅既有SDK、离线恢复 |

### 实际覆盖与图像证据

- 类型：普通/持续保留身份、关联、完成、子树和排序；完成后的冻结日；部分子树完成后转系列；完成历史逐记录保留；未来取消与Activity/Goal迁移；Undo/Redo和重新打开JSON。GUI包含根/普通子项的更改类型、禁止将系列子项直接打散、新系列有限次数及明确确认；旧日期转仅最新后选择实际顺延日，避免看似消失。只读存储保护仍执行。
- 时间：快速输入/完整编辑的中文冒号可见地变为英文，保留光标选区、标题内标点、无效输入；组合输入期间不改，提交后转换；高级新增等待期间的规范化不再导致快速草稿清除失败。
- 已完成子任务：父/未完成保留，完成组数量及展开顺序，进度不变；中栏、月历、今日折叠同步，历史上下文父节点也遵循过滤；全局偏好保存，父级临时覆盖不写业务数据。
- 快捷键：真实RegisterHotKey竞争/冲突保留旧绑定、竞争者退出后重新注册、通过WM_HOTKEY分发找回托盘隐藏窗口、禁用释放、偏好保存；只读偏好文件时保留编辑器/错误并释放新绑定。WM_HOTKEY用自有窗口消息测试，**不是物理键盘按下证据**。
- 彩色：Windows原生DWrite/D2D/WIC得到😀、😴、❤、肤色/ZWJ与家庭组合的真实RGB像素；渲染图已核对标题、说明、原生TextBox、Markdown预览中的色彩，深色输入主题保留。原Unicode/选择替换/Markdown复制不含对象替代符；字体预览与还原不累乘。长备注滚动后只为可见区域建立字形，原文本不变。图为interaction-color-emoji.png，**RenderTargetBitmap而非桌面像素截图**。
- 外观：贴边把手原生TOPMOST为真且用户偏好仍为假；完整/今日折叠恢复后原生TOPMOST清除，回到desktop owner；固定1/3/6/12周、周日起始不同、跨年、极端偏移与上下界；起止周/数量联动、非法行数保留草稿；135%字号时日期头与待办行高同步、月历文字增亮与分类原色分离。查看interaction-appearance-lower.png、interaction-large-font-calendar.png和880×520的interaction-small-large-font.png。最小窗口/大字号允许省略和+N，无重叠；多周请增大高度。

### 迭代失败与处理

1. 首次旧P9输出被现有进程占用，构建复制失败。未结束用户进程，改用独立InteractionFix输出；后续构建0警告/错误。
2. 初次默认执行会话有owner/焦点断言失败，不能算产品通过；改在正常用户桌面会话执行后通过。快速新增高级编辑清空有真实回归：等待期间冒号标准化改变原字符串，已改按标准化前缀比较，原断言复验通过（run-e89520bd7374401dae3a3bab1ace5baa保留）。
3. 原生字体适配初稿尝试覆盖TextBlock.OnRender，编译证实该方法sealed；现用附属DrawingVisual，编辑框保留原TextBox及非命中Adorner。原生绘图/数据测试初稿的构造参数、控件声明错误已修正，不把失败构建记为通过。
4. run-e15a0d1f64404877930d882106a0bbd6、run-6dcdbc83b5af42d9bb44f5069013b950偶发P6空编辑器在驱动填入前消失，随后依赖断言失败。增加实际可见等待、保存状态诊断（p6-before-editor.json）以及**仅测试初始化阶段**的HoldOpen；填入前释放，外部点击/IME原断言继续执行。后续多次整套通过。具体外部失活触发未被屏幕工具观察，不能断言唯一根因为COM或系统焦点；并非删除/跳过失败。

### NOT VERIFIED / 最少人工补充

computer-use重新按技能初始化，list_windows仍返回native pipe unavailable / os error2，未反复触发审批或自造屏幕自动化。未进行物理组合键、实际其他应用遮挡把手和悬停、真实IME候选窗；窗口状态/自有消息与控件图不冒充屏幕操作。Win10、混合DPI实机、Explorer实际重启、休眠及小时/隔夜仍未验；部分新emoji/旗帜受系统字体覆盖限制，保留原回退。历史偶发轻微缩放抖动限制不变。

最终安装后只补一组恢复交互：退出旧实例，从开始菜单启动候选；设置组合键后实际按一次找回，再检查把手在普通窗口上可见并能悬停展开。展开后按原偏好回桌面层，完整组件仍允许普通应用覆盖。不要求重复数据编辑/备份或整套已确认的Win+D/任务栏测试。

结论：上述已执行范围通过；暂不进行正式发布/合并main。当前用户安装复验尚待下一步完成，本轮任务不因此提前结束。


## 2026-11-03：0.8.1 候选 / P9综合验收

基线d6bfa83/dev/p7-p8。用户授权月历三项修订及P9质量保证，未授权合并main或正式GitHub Release。候选版0.8.1；业务schema3、偏好schema2、Core农历算法/官方知识包不变。

| 范围 | 结果 | 证据 |
|---|---|---|
| 数据/周期/日程/统计/原子存储/ZIP | **330 PASS / 0 FAIL / 1 NOT VERIFIED** | artifacts/p2-checks/checks.txt；历史Samoa2011资料边界保留 |
| P3–P9真实Window/控件/独立重启 | **331 PASS / 0 FAIL** | artifacts/p3-checks/run-7ea0a482a80c4c7ab199ce71c6ec1f32 |
| 原窗口/owner/缩放/托盘/生命周期 | **374 PASS / 0 FAIL** | artifacts/p1-checks.txt，P9构建后执行 |
| 运行监测 | **4 PASS / 0 FAIL** | artifacts/quality-c8fd1141f3d04e53beb34270cef12f0c |
| 隔离安装更新/启动/卸载/保护 | **18 PASS / 0 FAIL** | artifacts/install-checks.txt，0.8.1候选包 |
| 开发包/实际安装/实际开始菜单 | **9 PASS / 0 FAIL** | artifacts/entry-checks-f13a495c8e77414783a306dd95674226 |
| 构建/本地候选包 | **PASS，0警告/0错误** | bin/P9、artifacts/package；无新依赖 |

### 三项修订与视觉证据

PASS：初一十月/十一月及闰六月/闰二月；非初一初二/初十/二十/三十；完整农历与中栏不变，越界仍空简写+详情明确提示。生产日期格只21DIP头与Star待办两行，公历Auto、辅助Star、+N Auto；长标签不抢公历或+N，0/1/3/10项、36/56/96/160DIP、37/70/120高度共48组测量和隐藏数量检查。优先节气/传统节日/具名假期及补班，内地/HK短标分色；全部事件在日期头悬停/详情保留。

PASS：普通Sunday是查询合成的通用HK事件，不是官方17个具名日期包。紧凑展示按精确身份过滤，未删除原始数据或改变On/统计；具名周日与观察补假不按weekday过滤，原包序列化与业务图摘要保持一致。

已查看p9-header-matrix、p9-en-1480、p9-zh-CN-880等控件图（中英各880/1240/1480窗口），日期与辅助信息确在同一行，窄格优先公历及+N、长节气英文省略；大窗口待办空间增加。源为真实WPF布局后的RenderTargetBitmap，**非桌面像素截图**。看不全的信息可悬停/查看中栏，不把长文本裁剪等同丢数据。

### P9发现并修复

- 语言切换重建页面原先会重置分栏。初次复验run-836063f965c9458cac69602754942598仍有1 FAIL：用ActualWidth当新Star权重重复施加最小宽度约束，实际比例仍改变。改为复制GridLength单位/权重，保留原断言；最终同实际尺寸分栏一致（p9-columns.json）。
- ActivityWeekView对owner.Closed使用匿名订阅，旧语言页面虽脱离Controller仍被窗口引用。改为可解除的具名事件；Detach同时解除Closed，GC/WeakReference检查旧页可回收。没有重写窗口系统。
- Test-P3整套上限由90扩至120秒，包含新测量、渲染与GC断言；没有降低UI刷新或跳过失败。3分钟监测为另一独立进程，不与其他GUI套件并行。

### 短时资源监测

完整控件夹具的独立副本，实际WPF进程连续182.15秒：前20秒切页、折叠、语言、置顶往返，后续空闲每30秒采样，最终正常退出且业务摘要不变。空闲段CPU **1.42%（折算一个逻辑核心）**；私有内存比30秒预热基线下降8.36MiB，句柄减少9；末次Private约322.8MiB、WorkingSet约210.5MiB、托管堆约18.6MiB。未见短时持续增长；这不是“小时级/隔夜稳定”或资源极小的承诺。

可复现：

```powershell
.\Test-Planner.ps1
.\Test-P3.ps1 -Configuration P9
.\build.ps1 -Configuration P9 -Test
.\Test-Quality.ps1 -FixtureRoot <已完成的项目artifacts控件目录> -Configuration P9
# 小时级/隔夜测试入口已提供，但本轮未执行这些时长：
.\Test-Quality.ps1 -FixtureRoot <同上> -Configuration P9 -DurationSeconds 14400
.\Publish-DesktopCalendar.ps1 -Configuration P9
.\scripts\Test-UserInstall.ps1
.\scripts\Test-EntryConsistency.ps1 -Configuration P9
```

### 安装一致性、数据与Git卫生

发现真实开始菜单仍指向0.1.1。先通过候选包隔离安装测试，再用既有脚本Update到当前用户Programs/DesktopCalendar，开始菜单指向新版本指纹目录；保留旧版本和运行中实例，不访问个人业务目录，不创建启动项/改注册表。真实安装EXE只用生命周期测试旗标和artifacts独立根启动烟测，不加载个人待办。

Git忽略规则用argv实测planner JSON、偏好、assets/images、ZIP、.env、bin、截图；不经PowerShell管道传git路径，避免CRLF被当作文件名字符而误判。已跟踪图片仅三项公开品牌资源（SVG/ICO/PNG64），无个人图片/备份。git diff --check与文件清单审查；有限令牌/私钥特征扫描无匹配，不冒称全面安全审计。测试图/JSON/日志/性能采样仅忽略的artifacts。

### NOT VERIFIED / 发布门槛

按computer-use技能检查list_windows，仍native pipe missing(os error2)，本轮不能注入物理输入/取桌面像素。owner/TOPMOST/任务栏工具窗口样式仅自动状态PASS；Win+D、Alt+Tab排除、普通应用覆盖沿用用户既有P1/P1.1–P6实机验收，**本轮未实际重按，不能标成新通过**。Win10、混合DPI硬件、Explorer真实重启/登录顺序/休眠、小时级或隔夜运行、物理满盘/断电仍NOT VERIFIED。已知偶发轻微缩放抖动为用户接受限制；日历官方覆盖与黄历缺源边界不变。

结论：**可供确认的候选版，已执行范围无未解决FAIL/代码级阻塞；尚非全平台无条件发布认证。** 正式发布仍须用户确认候选及兼容风险；不合并main、不创建Release、不改仓库可见性。

最少人工检查合并一组：从托盘退出旧实例，再从开始菜单打开Desktop Calendar；切换中英、宽/窄月历，看日期同行与周日无冗余HK标签，悬停看完整事件；按Win+D后用普通应用覆盖一次。此组只补新安装的屏幕/键鼠证据，不要求重复数据编辑、备份覆盖、排序或全部历史用例。其他平台需有对应硬件时再验。

## 2026-11-03：P7 + P8 最终开发验收记录

P6 a865de1由用户验收；本轮dev/p7-p8连续开发，P7独立9137c93，P8为后续增量。应用/Core0.8.0，业务schema3、偏好schema2兼容追加。**开发完成，已执行范围内通过；不等同未测平台/真实输入全部通过，不进入最终综合验收或发布阶段。**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-Planner.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-P3.ps1 -Configuration P8
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Configuration P8 -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\Publish-DesktopCalendar.ps1 -Configuration P8
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-UserInstall.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Update-CalendarData.ps1 -Action Status -Configuration P8
```

| 验证范围 | 结果 | 证据边界 |
|---|---|---|
| 数据、可靠存储、历法、ZIP恢复 | 330 PASS / 0 FAIL / 1 NOT VERIFIED | artifacts/p2-checks/checks.txt；Samoa2011历史资料原NOT VERIFIED保留 |
| P3–P8控件/真实Window/独立重启 | 266 PASS / 0 FAIL | artifacts/p3-checks/run-2712f415fc9d47a4b5407c611a6b544a；正式checks-*.txt不包含progress-checks.txt |
| 原桌面owner/八向缩放/偏好/托盘基线 | 374 PASS / 0 FAIL | artifacts/p1-checks.txt |
| 最终发布包、隔离安装更新卸载 | 18 PASS / 0 FAIL | artifacts/install-checks.txt；不替换真实开始菜单或安装目录 |
| 构建与发布 | PASS：0警告/0错误 | bin/P8及artifacts/package，框架依赖，既有.NET10，无新依赖 |

### PASS / 历法与界面

- .NET农历下/上界、2023闰二月、春节/除夕跨年；天文台2020–2030每年24节气逐项与官方实际下载核对；内地2025/26官方连休与补班、HK2025–27每年17具名假期/周日、同日多事件及传统节日区分；未覆盖年份不虚构。黄历未启用。实际手动授权在线更新在artifacts/calendar-live-update成功，离线失败/坏包/未来版本/错误来源/缺项保留；独立脚本Status离线通过。
- 中英所有资源键齐备，主窗口实际重建/托盘文案更新、默认分类只翻译显示；私人正文/数据字节不变，周首日不被语言覆盖；英语窗口/偏好经独立进程恢复。系统文件选择器遵循Windows自身语言，不修改系统语言。
- 背景/强调/双休日色、透明度、两种月模式、取消回滚；白色背景输入柔化，最低透明度主文字对白底合成对比至少4.5，文字自身Opacity=1，低透明度内容面板加深。实际壁纸合成尚未目视验证，数学检查不冒充实机。
- 固定六行42格、逐周/六周分页、年月选择/今天重置；完整月保持原五/六周，动态待办/层级/+N继续原检查。年月选择器明确1901–2100，不等同农历/节气/官方假期覆盖完全相同。

### PASS / ZIP、状态及系统入口

- 真实业务图、实际Markdown相对图片、偏好与可选资料包进入带版本/时间/长度/hash清单的ZIP；图和资产核验，JSON重读；跨目录恢复保留ID与关联。
- 拒绝路径穿越、绝对路径、反斜杠、设备名、NTFS流、EXE、大小写重复、symlink、缺图、hash错误、空清单路径、未来schema和超大解压。恢复前预览/显式确认、包变化指纹拒绝；恢复当前损坏JSON时原字节保留，不降级新版本当前业务/偏好。
- 维护gate、store原跨进程lease及资料文件lease隔离保存/更新；失败任务仍将错误传给界面，生命周期等待不会重抛而阻止正常退出，pending恢复不能导出为完整备份；空闲写入不会越过备份事务。完整恢复点、JSON最后替换、各阶段故障回滚；真实测试子进程在恢复中退出91，下次读取journal精确回滚。
- 图片已经安装后制造真实FileShare.Read占用，阻挡回滚；保留journal及原始点，解除占用后恢复。UI有pending journal则停止业务写入/重载绕过/偏好保存，启动恢复受阻不初始化空文档。
- 主组件实际恢复后重建并重新读取，英语/背景色/固定行/折叠状态/周偏好与真实图片一致，不继续旧内存；原待办/周期/日程/周目标/备注回归保留。
- 在隔离安装/Startup目录实际创建COM快捷方式，默认无项、重复启用一个、禁用移除自有项、外来同名拒绝、无稳定安装/低于0.8.0拒绝临时bin或旧安装目标；安装更新跟随已有自有启动入口并连同开始菜单失败回滚，卸载保留个人JSON/图片。真实登录时序未执行。

### 初次失败、诊断及修复

- P7问题与修复见下方独立记录。P8首次两批仍沿用45秒全套GUI上限（run-d8fde4dc3f0e480badf9ab1c708adf8b、run-c95651a34aff4648b73b072eec468fdb），进程超时被自己的驱动停止；增加逐项进度记录及P8阶段记录，整套扩至90秒后全部阶段继续完成。没有跳过断言或延迟UI刷新。
- 初次进度文件命名checks-progress.txt被汇总脚本重复计数，275不是正式通过数；改为progress-checks.txt，并重新执行各进程，以最终表为准。
- 回滚受阻测试最初在“首个文件”时注入，但首个文件并非图片，原图尚未被替换，回滚自然成功；旧预期的2 FAIL是测试时机不正确。新增精确BeforePlannerReplace测试切点，图片已替换再占用，验证journal保留和后续恢复；未删除原正常回滚断言。写入也显式尊重ReadOnly。
- 一次发布误与仍运行的自检同配置并行，出现MSB3026/3027/3021文件占用；未结束正常应用、未绕过锁。待自检正常结束后串行重新发布0警告/错误，最终包及安装再测通过。

### 渲染与 NOT VERIFIED

已检查P7英文月历/编辑器/外观和P8恢复后的英语设置、备份面板、原周视图/总结控件图。图像由真实布局后的RenderTargetBitmap生成，**不是桌面像素截图，也不证明物理鼠标手感**。computer-use完整技能已读，本轮list_windows仍native pipe missing (os error2)，不能取得窗口或键鼠，不重复429审批。

NOT VERIFIED：物理鼠标与IME/系统文件选择器手感、不同壁纸实际合成；真实Win10、混合DPI硬件、Explorer重启、登录启动顺序、休眠、物理断电/满盘/长期性能。所有测试数据/ZIP/图片/恢复点都在忽略的artifacts或测试临时目录，不覆盖真实个人planner-data.json/设置，不修改真实Startup/开始菜单/系统显示配置。

最少人工体验仅一组：退出旧实例，启动bin/P8/net10.0-windows/DesktopCalendar.exe，在设置切中文/English、外观选择颜色/透明度后取消，观察文字与弹窗是否清晰；无需为验证而覆盖真实数据或重测Win+D/既有数据矩阵。Windows真实登录/跨机兼容留最终综合验收，不作为本轮已通过。安装更新命令和故障处理见README/BACKUP。


## 2026-11-03：P7 已执行验证

从 a865de1/dev/p6 建立 dev/p7-p8；P6已由用户验收。P7应用/Core0.7.0，业务schema3、窗口schema2加字段兼容。数据 **279 PASS / 0 FAIL / 1 NOT VERIFIED**（既有Samoa2011资料边界），控件/真实Window/独立进程 **244 PASS / 0 FAIL**，构建0警告错误。GUI报告 artifacts/p3-checks/run-974df7fa5b6e4262afa3809b721c549b。

PASS：全量原业务回归、闰月/农历上下界、2020–2030共264节气、内地/HK同日、补班区别、未知年份不猜测；损坏包/新版/错误来源/缺项拒绝；离线更新失败字节保留，实际官方白名单在线更新在 artifacts/calendar-live-update 成功。独立脚本与设置调用同一Core服务。首轮资料对照因生成脚本排序顺序错误失败，修正明确按Date/Kind排序后全部对照通过；重复测试复用损坏夹具失败改为每次唯一目录，未放宽产品校验。

PASS：中英资源键全覆盖、真实主视图重建/菜单语言、非破坏默认分类显示名、数据字节不变；英语月份最初仍为“六月”，渲染图发现后改显式Culture并新增断言。外观白色输入会柔化，文字Opacity始终1；预览取消还原、固定42格。查看p7-english-calendar/editor、p7-appearance控件渲染图，非桌面像素。

NOT VERIFIED：computer-use已读完整技能并尝试list_windows，native pipe不存在(os error2)，没有反复触发审批/429；实际鼠标手感、壁纸合成、中英文输入法和真实Win10/混合DPI/Explorer/休眠保持旧边界。月历知识行在极小格优先日期/+N，完整多事件可从日期详情/提示读取。未启用黄历宜忌。内地新年份不自动抓取未知公告，只接受经核验离线包，见CALENDAR_DATA。

本轮按授权直接继续P8，不进入最终发布阶段。

## 2026-11-03：P5.1 + P6最终交付

P5.1已独立提交/推送332b73e；P6为其后增量。应用/Core0.6.0，业务schema3不变；窗口偏好schema2兼容新增周首日/周计划布局。**已执行范围内通过，不把实际键鼠或未测兼容性称为通过；不进入P7。**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-Planner.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-P3.ps1 -Configuration P6
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Configuration P6Regression -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\Publish-DesktopCalendar.ps1 -Configuration P6
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-UserInstall.ps1
```

| 范围 | 最终结果 | 证据与限度 |
|---|---|---|
| 数据/存储/业务 | **253 PASS / 0 FAIL / 1 NOT VERIFIED** | artifacts/p2-checks/checks.txt，历史Samoa2011资料不完整仍保留NOT VERIFIED |
| P3–P6真实Window/控件/事务/新进程 | **234 PASS / 0 FAIL** | artifacts/p3-checks/run-523ecb2f3e9d44d4a303f03c0d8e0563；main/restart/corrupt/newer独立进程 |
| 桌面owner/窗口/偏好/缩放基线 | **374 PASS / 0 FAIL** | artifacts/p1-checks.txt，P6Regression，正常用户桌面会话 |
| 发布包/隔离安装更新卸载启动 | **15 PASS / 0 FAIL** | artifacts/install-checks.txt，只写项目artifacts，不替换真实安装入口 |
| 编译/框架依赖发布 | **PASS：0警告/0错误** | bin/P6/net10.0-windows与artifacts/package 0.6.0 |

执行在Windows原生、现有SDK、离线源下进行；无新包、管理员安装、系统/Explorer/显示配置变更。所有业务JSON、合成图片、只读测试、控件图均在忽略的artifacts；不访问真实个人planner-data.json或私有截图。GUI测试串行，纯Core测试可独立执行。

### 实际覆盖

- **PASS / 目标**：GUI新增数量、切换时长并确认来源变化、勾选完成、多个Todo/Activity关联、手动覆盖与恢复自动、删除确认/Undo；禁止Todo当实际时长，类型变更保留原导航关联。已有与当场新建目标从待办/日程编辑均可关联，父事项保存后才写引用；新系列关联稳定首发生实例。数据层额外覆盖来源单选、同目标区间并集、类型变更必须确认、跨类别百分比和大于100%。
- **PASS / 总结**：真实Sheet编辑Markdown、实时预览、增删小节、插入独立统计快照；非法空小节/真实只读文件失败均保留草稿，取消快照不增加Revision。保存正文不抹旧快照，重算/修改日程不覆盖人写文字，Undo/Redo整事务恢复。总结与同周目标ReviewId建立关联，新进程保留正文/小节/快照/目标引用。
- **PASS / 周与设置**：Sunday首日同时影响月历首列、七天轴和目标上下文；前后周按钮与SelectedDate同步。周首日、两种布局在偏好保存并经独立进程恢复；历史WeekKey不变。Core Configure不写JSON/Revision、不清Undo，后续Undo保留当前Options；Reload保留本地时区与首日。原先默认Reload可能回UTC的路径已修正。
- **PASS / 布局与图片**：窄栏和每周摘要条真实生成；最小880×520、六周日期头至少21DIP，低高度仅调整留白/条高，不循环重建日历。条内容浮动展开不撑高某周；目标状态/进度可见。P5.1大图100%物理像素计算、适应、缩放、父稿不保存、编辑器移位夹屏；跨天同段详情新增Date/Offset锚点断言，定位到被点的第二天，不误找第一天。
- **PASS / 渲染检查**：查看p6-goal-editor、p6-review-editor、p6-month-side、p6-month-strips、p6-minimum、p6-week-plan-stats、p6-week-review-view及p6-crossday-anchor图。源为实际WPF布局后的RenderTargetBitmap，**不是桌面像素截图**。已核对源码/预览双列、摘要文字、目标菜单/统计/小窗口日期；不以控件属性代替图像观察。

### 失败与修复记录

1. 最初编译调用了不存在的CategoryTimes，按真实API改为CategoryStatistics，最终无警告/错误；没有复制第二套统计算法。
2. 旧内联焦点异常在P5.1再次触发，保护迟到Loaded及重建失焦回调后多轮通过；保留原断言与P5.1报告。找回测试最初查原屏工作区，改查移动后的当前屏并等待布局。
3. P6图像显示摘要条文字裁剪，根因DarkToggle模板把Padding硬编码为6,4；改为默认值+TemplateBinding，让紧凑条Padding生效，非编辑下拉仍同样外观。删除按钮移入目标⋯菜单，空进度/分类条去掉默认白边。
4. 周总结第一张预览图空白是自动驱动未等220ms合并刷新；后续等待实际定时刷新、断言预览含正文再截图。只读失败检查后先显式Reload恢复状态再拍最终布局，避免把故意的保存错误提示误当正常状态。
5. 一次正常桌面测试审批审查超时未执行，按工具允许重试一次成功，无权限绕过或429循环。P6最终批次234/0、253/0，无未解决FAIL。
6. 增加小节删除/Undo检查时，连续打开新系列编辑器出现一次No modal（run-67074b343cc848bf92ad8e37dabba4a2）。初始激活还未稳定即触发失活保存；仅首次稳定激活后接受Deactivated提交，实际外部鼠标请求仍立即处理。驱动也明确激活并等布局后输入，保留原外部保存/取消断言，最终234/0；不简单重试或跳过失败步骤。

### NOT VERIFIED / 最少人工观察

computer-use技能已按要求读取并两次list_windows，均native pipe不存在（os error2）；无法获得可选窗口、物理输入或屏幕像素。实际标题拖动、块间单击切换、图片滚轮/平移手感仍**NOT VERIFIED**；WPF路由/方法、Native边界、控件图是各自独立证据。Win10、混合DPI硬件、Explorer重启、休眠、物理断电/满盘及长期资源消耗保留旧验证边界；偶发轻微主窗缩放抖动仍是用户已接受限制。

退出旧实例，从bin/P6/net10.0-windows/DesktopCalendar.exe启动，仅需两组：

1. 拖动待办/日程编辑器标题，单击另一日程切换锚定详情、滚动时间轴后看卡片是否跟随/关闭；点备注图试滚轮/平移/Esc返回，确认原输入与滚动保留。此组只验物理手感，不重复已通过的保存矩阵。
2. 在设置切月历周计划两布局、放大/缩小窗口，查看周侧栏目标/统计/总结及窄窗口摘要是否清楚可读。数据计数、去重、类型切换、周切换与重启已自动通过，无需再次逐项点击。

不要求重复旧Win+D/任务栏/Alt+Tab验收，不让用户重测Core数据边界。完整ZIP、孤儿图片清理和P7历法/完整双语尚未实现。P6结束停止，等待下一阶段授权。

## 2026-11-03：P5.1编辑窗口与图片交互

独立于随后P6提交；0.5.1/dev/p6，`Test-P3.ps1 -Configuration P51`最终 **202 PASS / 0 FAIL**，0警告/错误。报告artifacts/p3-checks/run-066e36b3b2454818ab798e156eccd245。旧P3–P5全部检查仍执行，新增真实owned Window移位/Fit、图片Viewer物理像素100%/适应/缩放、父草稿/Revision不变、详情上下/侧边避让与拖动夹入断言。已查看p5-local-image-preview、p5-detail及p51-image-viewer控件图；图片实际适应预览宽度，卡片在主窗口内，不是独立详情Window。

初次旧内联焦点再次失败，改为有效草稿/已加载可见控件才聚焦；重建同草稿迁移不提交，真实失焦延迟并验证仍在树上。保留旧断言，后续201/0、202/0。一次编辑器移位找回失败断言使用原显示器工作区，改为当前屏+布局完成后核对，无产品设置修改。工具list_windows仍native pipe不存在；实际鼠标标题拖动、块间切换和看图平移手感NOT VERIFIED，最终P6集中给最少两组人工观察，不重复旧owner测试。

## 2026-11-03：P4图标收尾 + P5实际日程

状态：0.5.0/dev/p5开发完成，已执行的自动/Window生命周期/控件渲染范围内通过，**实际物理拖拽、系统剪贴板与桌面像素NOT VERIFIED**；不进入P6/P7。沿用schema3、唯一业务JSON和已验收桌面owner；P4核心已由用户确认，本轮只调尾部图标，不重写周期系统。

### 执行方式与最终结果

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-Planner.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-P3.ps1 -Configuration P5
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Configuration P5Regression -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\Publish-DesktopCalendar.ps1 -Configuration P5
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-UserInstall.ps1
```

| 范围 | 结果 | 证据/限度 |
|---|---|---|
| 数据/存储/Core | **240 PASS / 0 FAIL / 1 NOT VERIFIED** | artifacts/p2-checks/checks.txt；原Samoa2011平台资料限制保留 |
| P3–P5编辑/控件/图片/独立进程恢复 | **195 PASS / 0 FAIL** | artifacts/p3-checks/run-b26dd13d0d0344d58e6f7205e243de1d；main/restart/corrupt/newer四类独立进程 |
| 桌面窗口/owner/缩放/偏好/安装资源基线 | **374 PASS / 0 FAIL** | artifacts/p1-checks.txt；P5Regression正常用户桌面会话 |
| 五文件发布包/隔离安装更新卸载启动 | **15 PASS / 0 FAIL** | artifacts/install-checks.txt；只写项目artifacts，不替换真实开始菜单安装版 |
| 最终构建/框架依赖发布 | **PASS：0警告/0错误** | bin/P5/net10.0-windows，artifacts/package 0.5.0 |

全程Windows原生PowerShell、现有SDK、离线包源，无安装依赖/系统设置/Explorer重启/显示器设置变更。所有业务及图片夹具在项目artifacts，未访问个人planner-data.json、剪贴板私有内容或用户图片。控制台日志/PNG/JSON均忽略提交；渲染图不包含真实私人桌面。

### 已执行的功能与边界

- **PASS / P4图标**：窄宽52DIP实测，‹/⌄和类型↻/←右停靠，正文仍计划时间+标题，标记独立宽度且不挤走勾选框。原父子展开、深度优先、三种测量换行、+N和五/六周检查继续执行；周期标记断言改为验证右侧结构，而非旧前缀文本。
- **PASS / 日期与时区**：跨午夜同PeriodId裁日、跨周查询基础、多个独立段；DST回拨3小时拆两偏移片、本地钟反向但绝对正时长仍可见；春季空洞保留墙钟空白/真实1小时。模糊无偏移与无效本地时间拒绝，移动保持真实时长，边缘只改一端，倒置拒绝。创建去掉鼠标小数坐标的秒/亚秒；已有时间未改则亚秒原样保留。
- **PASS / 编辑及数据**：创建即含备注/多段/emoji/reaction、未完成Todo关联、已有WeekGoal关联；不完成待办。表单非法时间保留草稿，外部关闭走同一校验保存，取消不改，真实只读文件保存失败不关。单段删除确认保留其余段/备注/关联，整条删除不删Todo；复制入口新ID。Core原JSON中断/并发/损坏保护、UndoRedo与图完整性全部仍执行。
- **PASS / 时间轴自动驱动**：两页均来自同一状态、无示例块；拖动控制器预览不保存，取消保持JSON摘要；释放移动/边缘调整一次事务，另一页即时更新，Undo恢复。保存失败保留可重试预览，恢复文件权限后同一次意图重试成功。重叠块独立Activity/Period标记与分列，缩放后刻度/块位置高度同一比例、指针锚点稳定，普通滚轮不被Ctrl逻辑拦截、两页比例独立。**这些调用生产手势入口/路由与真实布局，不是物理鼠标轨迹。**
- **PASS / 备注和图片**：源码/实时FlowDocument可渲染标题、列表、引用、代码、表格、链接与本地图；HTML作为文字，远程图片占位无网络。规范PNG内容去重/相对路径，不含Base64；坏图拒绝、文件占用/不安全引用返回占位。JSON+资产登记可Undo，物理文件保留以便Redo；共享图片不因删一日程丢失。新进程重新读取两段、emoji/关联与图片校验成功，非仅检查原文件内容。系统剪贴板入口和真实文件拖入尚无物理输入证据。
- **PASS / 渲染观察**：已查看p5-activity-editor.png、p5-notes-editor.png、p5-local-image-preview.png、p5-detail.png、p5-real-day.png和p5-real-week.png；截图式证据来自实际WPF控件RenderTargetBitmap，**不是桌面像素截图**。备注滚入编辑视口后检查，代码/表格/引用/缺图提示/本地图可见，emoji编辑值实际可见，深色文字对比正常。当前24小时/最小布局及月历原回归保留；极短块可放大时间轴或打开详情精确编辑。

### 本轮失败定位与修复

1. 初次构建有GridResizeDirection命名及匿名签名重复Text两处错误、剪贴板可空值一处警告，已修正；最终0警告/错误。
2. 首轮旧内联测试一次找不到输入框；增加焦点类型/日期/可写状态诊断，不删测试。后续多轮串行全部通过，未得到可稳定重现根因，保留首轮报告run-2d202f26941d46ccae09541ca02bb138；不宣称已证实某个产品根因。
3. 首次P5驱动在收起的周目标Expander里找CheckBox失败；按真实用户流程展开并等待布局后通过。P5选到次日后旧“找回必须有当日待办”的夹具假设失败；P5测试退出恢复原日期，保留原找回断言，非产品数据丢失。
4. 图像显示emoji控件空白而保存值正确，确认原全局ComboBox模板无PART_EditableTextBox。补标准编辑部件及IsEditable触发器，保留非编辑外观；新增可见文本断言/图像后通过。详情的旧“编辑待办”复用文案也改为“编辑”。
5. 曾同时运行P5与窗口基线，造成新编辑器失活、No modal测试竞争（run-7c80f6c455024b41b8b485f17fe5d19c）；停止并行GUI，独占桌面串行复跑191/0、194/0，最终195/0。独立测试目录仍保留，不把竞争记成产品通过。
6. 第一张直接渲染视口外NoteEditor为空，因为祖先滚动裁剪；后续将实际备注滚入编辑视口、预览滚到底，检查完整编辑器图，不用空图作视觉证据。

### NOT VERIFIED / 已知限制与仅需人工的两组

computer-use技能本轮已阅读并调用list_windows两次，均native pipe不存在（os error2），没有可选择窗口或像素/输入接口。没有用其他输入注入或额外权限绕过；没有反复429审批请求。Win10、真实混合DPI、Explorer重启/休眠、物理断电/满盘仍为历史未验证边界；用户接受的偶发轻微缩放抖动保留，不重测已验收Win+D/任务栏/Alt+Tab。

最终批次无FAIL；首轮内联焦点异常仍为未稳定复现的观察项，诊断保留，不宣称已定位根因。物理交互体验仍待确认；先从旧实例托盘退出，运行`bin/P5/net10.0-windows/DesktopCalendar.exe`：

1. 右栏拖空白创建，填标题保存；拖块移动、拖上下边缘，试Esc/右键取消、Ctrl缩放及Ctrl+Z；单击看详情、双击编辑，再切周视图核对同一日程。只观察鼠标命中、预览/单双击手感和视觉，不必再逐项测已通过的数据矩阵。
2. 编辑备注，试Ctrl+V截图及拖入一张测试图片；核对Markdown本地图、emoji自定义输入及外部点击/×/Esc保存，重新启动后图片可见。不要粘贴私人截图作反馈，也不需要把PNG/JSON提交Git。

Markdown为本地安全子集，链接仅复制、远程图不加载，超100000字预览明确截断但源文保留；取消/删除留下的孤儿图保守保留，自动清理/完整ZIP留后续。这些是明确边界，不冒充未实现功能。测试之外不自动更新已安装版本，P6等待批准。

## 2026-11-02：P3.2 + P4 最终验证

状态：P3.2/P4开发完成，已执行的自动、真实Window生命周期与控件渲染范围内通过；**不将物理键鼠/桌面像素标记通过，不进入P5**。P3.2单独提交74822bf，P4在其后dev/p4增量开发；应用/Core0.4.0、业务schema3、窗口schema2，无新依赖、owner源码不变。

### 最终执行与证据

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-Planner.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-P3.ps1 -Configuration P4
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Configuration P4Regression -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\Publish-DesktopCalendar.ps1 -Configuration P4
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-UserInstall.ps1
```

均Windows原生/离线；owner检查在正常用户桌面会话运行，无管理员、系统设置或Explorer重启。应用测试由自有进程实际显示Window/进入Dispatcher布局、控制草稿和路由事件、提交JSON、关闭并在新进程读取；所有数据仅项目artifacts，不访问真实个人文件，也未替换真实开始菜单旧版或结束用户旧实例。

| 范围 | 最终结果 | 证据 |
|---|---|---|
| Core数据/业务/存储 | **227 PASS / 0 FAIL / 1 NOT VERIFIED** | artifacts/p2-checks/checks.txt；新P4混合图isolated-p4-477b904c473540adadf63869a952b7a6 |
| P3/P3.1/P3.2/P4控件/编辑/重启/损坏保护 | **160 PASS / 0 FAIL** | artifacts/p3-checks/run-c0e0d96bf6b848a998a1353f819603c0，含main/restart/corrupt/newer独立进程 |
| 原窗口/owner/偏好/布局/进程回归 | **374 PASS / 0 FAIL** | artifacts/p1-checks.txt；P4Regression构建 |
| P4五文件发布包/隔离安装更新卸载启动 | **15 PASS / 0 FAIL** | artifacts/install-checks.txt，install-checks/cc3ff13878b74c47bdcf5a5c4c4dcbbd |
| 旧Core0.3.1读取P4 schema3 | **PASS** | 引用现有bin/P31的Core DLL，Decode后Encode与最新P4夹具逐字节一致；测试工具仅放忽略的tests/PlannerChecks/obj/compat-* |
| Release型构建/发布 | **PASS：0警告/0错误** | bin/P4/net10.0-windows；artifacts/package版本0.4.0 |

### 覆盖的行为

- **PASS / 数据**：每日间隔、每周指定星期/N周、月末/闰年/跨年/COUNT/UNTIL/最远日期跳转，不无限生成；原始实例键在移动日期后不变。单次字段/完成/取消独立，全部未完成包含更早待办，FromDate含原发生日且保留更早移动例外。规则独改不清单次标题/颜色/进度/排序，完成历史/部分完成子树及必要上下文保留；新增模板子项不污染完成历史，单次子项不进入未来模板。
- **PASS / 数据**：取消及规则排除兼容旧普通/周期混合子树，无孤立ParentId；周期/持续复制成普通子树、混排、完成分组、快照、Undo/Redo、完整图重新加载及JSON字节稳定。持续共享ID/历史每一天/仅最新、完成冻结/取消恢复、显式未来移动与asOf防未来幻影兄弟。Todo浮动计划钟在现代DST回拨日仍只有一次发生，Activity绝对区间旧测试保留。
- **PASS / Window与控件自动驱动**：从完整编辑器建周期、错误规则保留草稿、周/月控件/可选结束日、单次编辑及内联、单次/未来/全部取消、系列子项和本次子项、规则分割与历史保护、复制及虚拟兄弟拖排事件；持续类型两种新建/切换/顺延、历史同ID仅一处内联框、折叠完成同步/重启冻结。普通父任务在完整编辑中勾选/取消后快照还原，防新保存流程回归。新进程核对系列归档、已物化月实例和持续Moves。
- **PASS / P3.2回归**：五默认分类及旧分类不替换、继承不持续覆盖；外部失活保存/无效草稿保留、附属Popup/上下文菜单保护、模态确认不自动关闭、组合事件Enter/Esc保护、三种实测换行及0–96DIP容量/+N/深度优先。旧颜色、内联Esc取消、读写失败/损坏/新版阻断均继续执行。
- **PASS / 控件图像观察**：人工查看本轮RenderTargetBitmap输出p4-create-recurring.png、p4-leap-month.png及P3.2换行图；紫/灰深色、标题与输入可读，规则Expander标题改用白色、可选结束日改显式启用避免英文空日期水印。日期格不因内容撑高。图像是实际WPF控件渲染，**不是屏幕截图**；错误规则测试截图故意保留校验提示，不将提示掩盖。

### 定位失败及修复（保留事实）

1. 首轮P4驱动未等Expander完成布局即访问日期输入，产生一次测试失败；加Dispatcher空闲/布局等待后通过，未删除断言。
2. 后续2项失败暴露真实保存缺陷：Sheet保存时整窗禁用，读取完成CheckBox.IsEnabled会连带变false，导致本次子项及普通父完成意图被忽略。改用任务类型/选中作用域判断，再通过子项与普通父快照自动驱动，最终160/0。非仅更改测试预期。
3. 源码复核发现规则独改会重置单次字段、范围新增子项会改变已完成父历史、旧普通子节点可能孤立；已分别用差量字段补丁、有限历史排除及混合树保护修复，并添加实际持久化/撤销/重载断言，最终Core227/0。

### FAIL / NOT VERIFIED 与最小人工观察

- 当前无未解决的已执行FAIL。Core唯一NOT VERIFIED仍为Windows时区资料的Samoa2011整日跳跃；现代DST及可控午夜边界通过，未安装时区库。
- 本轮computer-use按技能初始化list_windows，重置后再试仍native pipe不存在（os error2），两次后停止；未使用其他权限/输入注入绕过。**物理鼠标、真实中文候选窗、外部应用点击及桌面像素 NOT VERIFIED**，自动WPF合成/激活与离屏图不能替代。没有429审批重试或为测试改显示/Snap设置。
- Windows10、混合DPI硬件、Explorer真实重启、休眠及物理断电/满盘未实测，沿用既有边界；偶发轻微缩放抖动为用户接受的限制。owner模块未改，不要求重复已确认Win+D/任务栏/Alt+Tab行为。

只保留两组界面手感检查，先从旧实例托盘退出，再启动bin/P4/net10.0-windows/DesktopCalendar.exe：

1. 编辑一个测试待办，切换分类/颜色/日期附属Popup并用中文候选输入，点外部应只提交一次；无效标题不关闭。完整编辑Esc保存，月历内联Esc取消；取消按钮放弃。此项核对物理事件边界，不要求重新测整套持久化。
2. 用长标题父子项切换三种换行/展开，核对小窗口文字和+N；从“更多选项”建短周期与持续任务，查看↻/↪及范围菜单是否清晰易用。范围/历史/重启数据矩阵已自动通过，无需再逐项手工勾选。

其余数据与常规回归无需用户重复操作。最终交付不等于所有兼容性人工验收通过，P5等待批准。

## 2026-11-02：P3.2 体验修订（P4继续授权中）

Core **197 PASS / 0 FAIL / 1 NOT VERIFIED**（原历史时区资料）；窗口基线 **374/0**。P3/P3.1/P3.2控件 **130 PASS / 0 FAIL**，构建0警告/错误。证据目录artifacts/p3-checks/run-b6581f0db4aa481188638bcbbea0ec10/。复现`Test-Planner.ps1`、`Test-P3.ps1 -Configuration P32`、`build.ps1 -Configuration P32Regression -Test`，均Windows原生、离线构建、项目artifacts隔离数据，不读写真实用户文件。

- PASS：新五分类准确颜色/双语初值；旧分类及引用重新打开不替换，主动补齐仅新增缺失键，改名/自定义同名不动。
- PASS：实际编辑Window外部激活主窗口后保存关闭；无效草稿不关闭；颜色/分类/日期Popup阻止外部关闭；原生确认窗口仍禁用owner且无自动关闭；IME合成事件期间Esc/Enter不提交，结束后正常提交。子草稿继承及父后改不覆盖。
- PASS：三种模式按真实TextBlock宽度测量，0–96 DIP逐高度检查；按展开DFS顺序选行，长父让出子行，溢出+N准确；换行设置经独立应用进程恢复。检查p32-wrap-*.png，无日期格被内容撑高。原P3.1所有断言仍执行。
- 首轮新增确认测试用WPF IsEnabled属性判断原生模态禁用，失败1项；改为检验原生WS_DISABLED，不修改确认功能。新增默认分类测试曾变量重名编译失败，修正后197/0。展开三行恰好全容纳的夹具不能期待+N，改为四条验证真实溢出，不删除断言。
- NOT VERIFIED：computer-use技能list_windows、重置后重连仍native pipe不存在（os error2）。真实鼠标跨应用点击/中文候选窗与键盘手感无法注入观察；WPF激活/合成事件、离屏渲染是替代证据，不冒称真实输入通过。P4末集中保留最少人工核对，不重复旧Win+D等验收。

## 2026-11-02：P3.1 待办交互与完成语义修订

状态：开发完成，自动及控件渲染验证范围内通过，物理键鼠/真实桌面像素 NOT VERIFIED。用户已确认P3核心新增/编辑/三视图/排序/撤销总体正常，本轮不要求重复旧Win+D/任务栏/Alt+Tab验收。仅P3.1，不开始P4。

### 可复现执行

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-Planner.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-P3.ps1 -Configuration P31
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Configuration P31Regression -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\Publish-DesktopCalendar.ps1 -Configuration P31
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-UserInstall.ps1
```

WPF/owner测试在正常用户桌面会话执行，未使用管理员或更改系统设置。所有业务夹具、只读属性试验、报告和渲染图均在忽略的artifacts/临时测试目录，未读写个人planner-data.json、未替换真实开始菜单安装版。四个独立WPF进程覆盖main/restart/corrupt/newer；退出后新进程恢复完成快照，再取消父完成核对原混合子状态。

| 范围 | 结果 | 实际证据 |
|---|---|---|
| 数据/存储/业务 | PASS：194，FAIL：0 | 包含P2原子写入/中断/并发/损坏/关联、P3复制，以及默认递归进度、自然完成与显式未完成覆盖、级联快照、独立状态/进度/结构失效、嵌套、删除/提升、UndoRedo、排序不变、标题并发校验、schema2原件不写/首次保存字节备份 |
| P3及P3.1 WPF | PASS：111，FAIL：0 | 真实模态窗口、控件路由事件/焦点及文件操作：中文冒号/空格/全角、快速分类覆盖色、更多选项复用、叶手动进度、保存/X/Esc/取消/无修改/无效日期/写失败、历史分类保留、颜色Popup及旧alpha HEX可读预览、月历层级与9个高度临界值、内联Enter/Esc/失焦、折叠回今天、独立进程快照恢复和只读保护 |
| 原窗口/owner/几何/时间轴 | PASS：374，FAIL：0 | 原371项保留并调整3项过时页面期望，新增3项“仅置顶往返保留周页面”。折叠展开按新要求回今日页；同HWND、矩形、透明度、原生TOPMOST清除、托盘、贴边、缩放、分栏、5/6周、DPI/显示器拓扑数学、启动可见时序、重启/单实例/异常终止保留 |
| 渲染图核对 | PASS（控件渲染范围） | 检查完整/最小窗口、编辑器、卡片层级、内联无效草稿、HSV/选色标记、分类下拉、菜单正常/高亮/勾选/禁用、日期Popup。三点菜单暗底亮字；颜色按钮与勾选色标显式前景；日期选择保留系统浅色模板、局部暗字隔离。不是桌面截图 |
| 历史时区 | NOT VERIFIED：1 | 沿用Windows资料下Samoa2011跳日差异，可控DST与日期边界测试通过，不修改系统资料 |
| 真实输入/实际屏幕 | NOT VERIFIED | computer-use技能初始化list_apps、重置后list_windows均native pipe unavailable / os error 2，停止重试。程序内RaiseEvent/焦点变化不是物理鼠标双击或Esc输入，RenderTargetBitmap不是桌面像素，不据此宣称完整GUI验收 |

0.3.1离线发布与隔离安装/更新/回滚/卸载 **15 PASS / 0 FAIL**，五文件包含Core；构建0警告/错误。最新WPF证据目录`artifacts/p3-checks/run-38c82ba7014c44bcaa8fd118da53a088/`，Core报告`artifacts/p2-checks/checks.txt`，窗口`artifacts/p1-checks.txt`；均忽略提交。Git交付在dev/p3.1，main保持不变，不标记未测环境已验收。

参考[Microsoft Calendar样式/模板说明](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/calendar)：日期选择仍用框架原有月/年/十年交互，本轮只调整主题资源和文字继承，未重写日期控件。

### 发现与修正（不隐藏失败）

- Core首轮2项旧断言仍假定父不会自然完成，改为验证新自然完成及历史保护，同时保留取消父的上下文导航用例。补测真实发现“删除父并提升子任务后遗留Cascade来源”，在事务归一中释放孤立来源，新增校验/用例后194/0。
- WPF首次编译HSV元组0整型、测试Key方法名遮蔽枚举及未await提示已修正，后续构建0警告/错误。首次分类测试夹具未预分配ID导致3项失败，修正夹具ID，不改生产分类规则绕过。
- 原窗口首轮368/3：全部为“折叠恢复仍保留周页”的旧期望，用户现要求回今天。保留HWND/矩形/透明度断言，改成今日页，并新增仅置顶往返不得换页检查；完整374/0。
- **实际渲染发现两项属性检查未能揭示的问题**：浅色颜色按钮/选色勾号受全局TextBlock样式覆盖而呈浅字；日期Popup结构检查通过但实际为浅底浅字。分别显式设置色块文字前景、对Calendar传入局部样式，并移除全局TextBlock强制前景，改由窗口/控件继承文字颜色（仅局部Calendar资源不能覆盖模板内的全局样式），重新生成并观察渲染图。分类下拉使用真实名称/色点而非对象描述；ContextMenu模板覆盖正常/高亮/选中/禁用。未把旧结构PASS冒充颜色可读PASS。
- Esc读取DatePicker实际编辑缓冲，避免尚未失焦时只校验旧SelectedDate；颜色Popup/下拉Esc不误关闭编辑器。内联仅RenameTodo，不覆盖其他字段；删除/完成与快照同事务且失败不发布。

### 最少人工核对（仅目前工具无法观察的新交互）

先从托盘退出旧版，运行`bin/P31/net10.0-windows/DesktopCalendar.exe`（项目开发快捷方式也指向最新构建；开始菜单旧安装不会被本轮测试替换）。

1. **上下文键盘手感**：完整编辑改标题按Esc应保存；月历双击该标题改字再按Esc应取消，Enter应保存；空标题应保留输入且不删除。
2. **实际屏幕弹层/选色**：打开⋯菜单、分类下拉、颜色HSV及日期选择，确认文字可读、色块选中清晰；这里只核对屏幕呈现与鼠标命中，保存/快照/重启等程序已自主验证，不要求重做数据矩阵或旧桌面行为。

Win10、真实混合DPI、Explorer重启/休眠及长期性能仍是历史边界；偶发轻微缩放抖动仍为用户接受限制。

## 2026-11-02：P3 真实月历/待办接入与验证

状态：实现已完成，自动验证范围内通过；真实输入与实际桌面视觉受工具限制待最小核对，不开始P4。最终结果：P3 WPF **56 PASS / 0 FAIL**、数据 **165 PASS / 0 FAIL**（沿用历史时区1项NOT VERIFIED）、原窗口回归 **371/0**、五文件安装/更新/卸载 **15/0**，构建0警告/错误。实现提交8c5364f已推送dev/p3，并对该提交重新执行窗口回归、离线发布及安装验证通过；随后仅更新最终文档。下面P2及更早“UI未接入”仅为历史。

### 复现与隔离

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-Planner.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-P3.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Configuration P3Regression -Test
powershell -NoProfile -ExecutionPolicy Bypass -File .\Publish-DesktopCalendar.ps1 -Configuration P3
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-UserInstall.ps1
```

P3使用`artifacts/p3-checks/run-<GUID>/`：main/restart/corrupt/newer四个独立应用进程，报告`checks-*.txt`，渲染图`full/minimum/edit-dialog/compact/month-10/month-11/restart-real-data.png`。测试入口强制路径在项目artifacts内，自己的超时进程才可结束，不影响既有安装版、Explorer或个人业务文件。仅生成发布包和隔离安装，不自动更新真实开始菜单版本。正常生产运行才打开LocalAppData业务文件。

| 项目 | 结果 | 证据与边界 |
|---|---|---|
| 真实空状态/快速新增 | PASS | 新文档仅五分类、无模拟待办；真实Quick TextBox路由Enter事件保存09:30及标题，提交完成后清框；空/非法25:00拒绝，原状态与草稿保留 |
| 编辑/取消/子任务 | PASS | 打开真实模态编辑器，空标题不删、保存保持ID并写说明/优先级/颜色/进度；取消不变；新增子任务，勾选派生100%不自动完成父 |
| 三视图一致 | PASS | 月历查询、中栏控件与今日折叠读取相同ID/完成状态；折叠时间强调，今日与选择其他日期分别计算；不是重复业务集合 |
| 同级排序/撤销/复制 | PASS | 实际Drop处理器插入并保存Manual，跨父/状态限制；子任务单独排序、取消恢复原键；Undo/Redo恢复排序/删除；复制弹窗创建独立子树及重置状态。Core新增5项复制测试覆盖相对日期/元数据/子排序/整事务撤销 |
| 分类/颜色/日期底色 | PASS | 自定义分类对话框、分类选择持久化与实际行元数据/色条、日期底色弹窗、文字/底色模式；色板及HEX存在。归档保留历史由P2服务与接入菜单保证，不冒称测试了所有实际鼠标菜单路径 |
| 月历与布局 | PASS | 真实0/1/3/10条夹具、五/六周、880×520与1240×760；实际容量增高增加行，+N事件切到完整日列表，日期双击只聚焦管理不新增；检查上述渲染图，无明显重叠/溢出 |
| 非Todo实体保护 | PASS | 预置P2 Activity/Goal/Review关联夹具后通过GUI新增待办，所有ID/链接/正文保留；重新启动整图规范内容哈希相同，不清空其他实体 |
| 退出/重启/找回 | PASS | 窗口失效重建复用workspace和QuickDraft；等待新窗首次渲染后保存折叠；退出后的新进程读取相同业务图、Manual模式和真实折叠列表，原生可见时序无默认Full帧。此时序不冒充实际屏幕像素 |
| 写失败/只读保护 | PASS | 对自己隔离JSON设置真实只读属性，Quick保存失败后原快照/草稿保持，折叠显示失败提示，随后还原属性。损坏文件只读不回空，候选只显式恢复且原件留证据；更新schema主文件即使有旧备份也不降级，恢复按钮禁用 |
| 窗口与弹窗模式 | PASS | 原371项回归仍含owner/置顶/托盘/尺寸偏好/贴边/分栏/动态预览/时间轴；新真实视图置顶往返，编辑器打开时跟随置顶，回桌面时原生TOPMOST也清除。无SetParent/WS_CHILD、全局钩子、焦点轮询或系统设置改动 |
| 发布与安装 | PASS | 包含自有Core DLL的五文件哈希清单，版本0.3.0；安装器接受旧四文件管理，0.3.0缺Core拒绝；15项隔离安装更新/回滚/卸载/进程保护通过，不删数据/外来文件 |
| 真实输入/屏幕 | NOT VERIFIED | computer-use初始化、重连、重置后仍`native pipe unavailable / os error 2`。没有反复429审批。WPF控件事件不等同物理键鼠，RenderTargetBitmap不等同桌面截图；没有通过其他工具绕过限制注入Win键 |

### 初次问题与修正记录

- 首次编译5处错误（Thickness参数、Button方法名与类型模式重名、out变量被lambda下划线占用）修正后0警告/错误；新增测试夹具曾把bool当命令结果取EntityId，改为预分配测试ID再跑，不隐藏失败。
- 初轮Enter后清框与分类色板元素断言过早，分别等待UI回调/执行UpdateLayout后稳定；保留原断言，不删除测试。渲染图实际发现分类ComboBox显示CategoryChoice对象描述，改成显示其Text；复选框与多行标题对齐、紧凑计划时间强调、日期显示中文也已核对。
- 重启初次失败是测试在重建窗未完成ContentRendered前折叠并保存，偏好保护正确拒绝采样。测试改为等待Ready，并独立断言折叠偏好确已落盘，再退出/新进程验证；不改已验收窗口恢复机制，不凭文件内容单独判定GUI恢复。
- 新弹窗最初只在打开时继承置顶，会在托盘改模式后保留旧属性；已在主窗口层级刷新时同步自有弹窗，并断言原生TOPMOST清除。主体桌面owner实现未重写。

### 最少人工核对（仅工具无法覆盖）

先退出旧实例，从`bin/P3/net10.0-windows/DesktopCalendar.exe`或项目开发快捷方式启动，不用实验程序。

1. **输入与拖排手感**：在快速框实际输入并Enter；同父/同状态组拖动文字，观察前/后插入线，再在空快速框按Ctrl+Z。程序内已验证保存/排序/撤销语义，无需重做完整数据矩阵。
2. **实际视觉**：打开编辑器检查中文输入、分类下拉/色板与窄窗口文字，切今日折叠看计划时间/滚动。渲染图已检查，只需实际桌面画面与鼠标命中。无需重复旧Win+D/任务栏/Alt+Tab验收，owner路线未变。

Win10、混合DPI硬件、Explorer真实重启/休眠及偶发轻微缩放抖动保留P1.1边界；P4复杂重复/持续、P5日程、P6周目标/总结、P7历法/完整语言切换未开发。测试仅用示例夹具，不提交JSON、截图、日志或二进制。

## 2026-11-02：P2 最终数据/业务/存储验证

**结论：P2开发完成，已验证范围内通过，不开始P3。** 数据检查 **160 PASS / 0 FAIL / 1 NOT VERIFIED**；正常桌面用户会话WPF既有回归 **371 PASS / 0 FAIL**，构建0警告/错误。主窗口/桌面owner/安装源文件未改；根csproj仅增加Core/tests源码排除，避免独立项目被WPF递归编译。P2数据服务未接入模拟UI，不向用户伪装真实编辑。无需人工点击验收。

### 复现入口与隔离

```powershell
cd D:\projects\desktop-calendar
powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-Planner.ps1
# 正常用户桌面运行既有窗口回归；需要可访问当前Explorer的会话
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Configuration P2 -Test
```

数据报告`artifacts/p2-checks/checks.txt`，每项存储测试使用`isolated-<用途>-<GUID>/planner-data.json`。写入阶段子进程仅操作传入的隔离路径，使用Environment.Exit(73)模拟进程直接退出；没有结束Explorer/用户应用/机器。默认个人业务路径只定义在API中，本轮没有打开真实`%LOCALAPPDATA%\DesktopCalendar\planner-data.json`。WPF回归使用既有测试隔离偏好与自己创建的子进程，不改用户当前窗口状态；安装14项是P1.1历史基线，本轮未更改/重装安装入口。

### 结果矩阵

| 范围 | 状态 | 实际证据/边界 |
|---|---|---|
| 实体、ID、父子/关联完整性 | PASS | 重复ID、循环、缺失引用/非法颜色/资产路径拒绝；创建/编辑/删除操作经整图校验，失败不发布新快照 |
| 完成分组及稳定排序 | PASS | 先D后B得到ACBD，取消B得到ABCD；手动混排和有无时间任务、子任务独立分组，原键不变；独立重新打开JSON仍是Manual模式，恢复时间模式不抹键 |
| 子任务进度/删除 | PASS | 子完成与手动40%合成70%，不自动完成父；显式删除子树/提升子节点；取消系列保留完成子节点和祖先上下文，模板结构变化不把同一例外显示两次 |
| 重复/持续任务 | PASS | 每N天、间隔周指定星期、月末31跳短月、Count/Until/排除/包含基础；196组周边界与48组月/闰年组合对照有界枚举；独立实例完成、单次移动、未来版本、子模板删除/身份墓碑、未来取消/整系列归档保护完成历史；历史/仅最新完成冻结与顺延记录 |
| Activity和备注 | PASS | 多区间独立ID、跨午夜按日裁剪、同活动重叠去重、关联不完成Todo；删除Todo保留Activity/旧标题；创建/修改备注、emoji/reaction；原始偏移和命名时区重读保留 |
| 时间和周统计 | PASS | DST23/25小时日、167小时周、无效/重复小时/显式偏移、可控午夜跳跃和重复、半开端点、零长反向拒绝、UTC下溢诊断；不同周首日，同分类并集/跨分类独立；目标秒数并集、数量单来源、跨周警告、手动覆盖和类型变更确认 |
| 周总结/基础配置数据 | PASS | 手写正文保存不抹统计快照、统计追加不抹正文；日期底色同日upsert保持ID；五内置分类中英初始名、幂等初始化、归档保留引用、Activity分类继承/显式独立覆盖 |
| 资产引用 | PASS | 注册元数据/安全相对路径，asset://GUID查询；拒绝file/network/穿越后缀，已引用资产不可删除。不代表已读取图片、验证文件像素或实现粘贴/Markdown渲染 |
| 保存/进程重启 | PASS | 实际完整写入/替换/上一版备份；独立子进程重新解码并核对完整服务图的文档ID/Revision/规范内容哈希；服务对象重新打开核对全部实体、关联、状态与排序 |
| 原子中断 | PASS | TempCreated/HalfWritten/Flushed/Validated/BeforeReplace/AfterReplace六阶段独立进程直接退出，重开均读到完整旧版或完整新版；完整pending候选可列出且不擅自提升；部分孤儿＋缺失主文件禁止空初始化；普通异常/取消清理自己的临时文件；提交后异常重新核实已提交结果 |
| 损坏/迁移/新版保护 | PASS | 校验和、重复JSON键、必填字段/未知同版本字段阻断；坏主文件不覆盖、显式恢复需确认且保留原件证据；v1迁移后安全写v2且备份仍为v1；新版主/备份拒绝降级恢复或保存；候选指纹校验 |
| 文件占用/并发/失败 | PASS | 真实FileShare.None锁竞争Busy；主文件被独占打开得到IoError；只读目标替换失败但旧内容完整；两个独立store同时写仅一个成功、另一个Conflict；工作区12个并发命令全保留；外部工作区写入使旧工作区阻断，Reload后可继续；大小限制、错误目录目标、非法选项均可诊断 |
| 撤销/重做/事务 | PASS | 可靠保存后才更新内存和历史；一次Undo恢复删除及关联、Redo再删除，Revision不回退；100次会话上限；新命令清Redo，Reload清旧历史；批量失败不部分保存，成功单Revision且一次Undo全撤销 |
| 原WPF窗口基础 | PASS | 本轮重新运行371项，真实WPF生命周期、owner/置顶状态、偏好重启/异常进程恢复、分栏/折叠/贴边、动态月历/时间轴和品牌断言均保留。没有主UI变更；自动状态/离屏图仍不能当新一轮实际桌面像素/鼠标验收 |
| Samoa2011历史跳日资料 | NOT VERIFIED | 原测试预期跳过日期0小时，本机Windows TimeZoneInfo返回-10偏移、2011-12-30T10Z到31T10Z为24小时。初次124 PASS/1 FAIL保留为测试环境资料/假设差异；没有冒称修复算法或覆盖系统资料。补充可控时区午夜空洞/重复验证通过，但不证明该真实历史转换已验证 |

### 失败记录及限制

- 首轮v1匿名对象被IgnoreReadOnlyProperties忽略，导致MissingSchema；改显式Legacy DTO后通过。初次测试代码Span跨await编译失败，改用安全枚举比较后通过，不隐藏初次失败。
- Samoa差异经同机Windows API检查确认，不能据预期值判定唯一算法错误，也不能把该历史场景算PASS。当前依赖系统时区资料，未安装/下载时区库。极端历史政令变更准确性保留上述NOT VERIFIED；现代美国DST及自有午夜规则实际执行通过。
- 二次检查发现只保存手动键不保存模式会让未来UI重启切回时间排序，已增同级组TodoOrdering及真实重读断言。补查模板重挂后旧实例可能在两组查询重复，已按实际ParentId过滤并增加复现断言；最终全部复跑160/0。
- 没有用物理断电、实际满盘、硬盘损坏或恶意修改文件替代测试；异常注入/直接进程退出不代表所有文件系统/硬件故障已保证。个人数据不进入Git；单JSON不能替代含图片的全量备份。
- 主UI未接入Core，数据设置通过Options注入，真实编辑、恢复对话框、Ctrl+Z绑定、图片导入/渲染和ZIP功能仍后续阶段。P1.1的Win10、混合DPI硬件、Explorer重启、休眠和偶发轻抖限制不扩大声称；无需用户重复已接受的GUI。

### 工作包历史

第一工作包已在dev/p2提交/推送28db08d。最终边界与文档作为后续提交；不修改main/dev/p1.1，不开始P3。以下工作包一记录保留原进度，已被上方最终结果覆盖。

## 2026-11-02：P2 工作包一（历史进度）

PASS：91项数据/服务/存储检查，独立Core与测试项目编译0警告/错误。覆盖真实文件共享冲突、只读替换失败、两写入器冲突、v1迁移、新schema保护、损坏原件保留、六阶段子进程直接退出和进程重读；初跑发现v1匿名DTO被只读属性设置忽略，改为显式DTO后复跑通过。初次测试代码跨await Span编译错误已修正。所有文件在artifacts隔离目录，不碰个人数据。P2仍在补充边界与回归，不标完成。


## 2026-11-01：P1.1-6/7 合并交付与最终技术基线

**结论：P1.1 开发完成，已验证范围内通过；无已知阻碍P2数据模型开发的重大窗口问题，未开始P2。** 用户已补齐P1.1-5人工验收：月历动态显示、两页Ctrl滚轮、切页和小把手均符合要求；P1.1-4的恢复/无明显启动闪烁/贴边已验收，偶发轻抖接受。以下早期“待验”是历史，不删除失败记录，也不推定未测系统全部通过。

### 本轮实现和实际部署

- 方案A：圆角日历、石墨蓝底、雾蓝轮廓与灰绿/雾蓝/淡紫时间块。`logo.svg`为源，离线WPF原语生成九尺寸PNG帧ICO（16/20/24/32/40/48/64/128/256）、PNG64及预览。检查16/32/48/256视觉层级；ICO逐帧解码核对尺寸、EXE Win32图标抽取核对品牌像素、托盘Icon32独立加载、WPF标题/窗口Icon资源加载均通过。重复生成ICO/PNG64的SHA256相同；不用默认图标，无第三方代码/依赖/设计软件。
- 正式资源为明确授权的公共应用资产（SVG、ICO、PNG64），提交Git；预览在忽略的`artifacts/branding/logo-preview.png`，不是私人图片。构建刷新原项目P1启动快捷方式的IconLocation；未创建桌面快捷方式。
- 框架依赖Windows x64发布，四个运行文件约0.4MB，本机Desktop Runtime10.0.14可用，不下载自包含运行时。安装器使用schema1/AppId/内容指纹、白名单路径、文件长度/SHA256；准备不可变版本目录后原子替换唯一开始菜单入口和安装记录，失败回滚入口，不覆盖运行文件。旧版本保留；同名外来入口/目录或重解析路径拒绝；卸载只移除未修改的管理文件，陌生文件和数据保留。
- 实际正常交互用户已执行**安装和更新**：应用根为`%LOCALAPPDATA%\Programs\DesktopCalendar`，入口为当前用户`Start Menu\Programs\Desktop Calendar.lnk`。最终版本为`versions\v116afdaefd0c51f7\DesktopCalendar.exe`，四文件哈希与包一致；快捷方式目标/IconLocation和项目快捷方式IconLocation均核对。实际用户数据目录未被安装器访问；没有注册表/PATH/Snap/显示器/开机启动修改。
- 从正式快捷方式解析出的最终EXE，在隔离`artifacts/final-deployment-smoke`偏好下真实启动、创建WPF窗口并正常退出：Ready/Visible/Brand/DesktopOwner均true，实际进程路径为上述稳定安装目录。它证明部署可运行及原生owner状态，不证明用户屏幕像素或Windows搜索已收录；未擅自退出仍运行的源码旧实例，未运行真实用户目录的卸载。
- 综合图检查发现最小880×520窗口中两位日期被固定28DIP计数列挤成省略号。局部改计数列18DIP及横向留白，增加5/6周最窄日期字形宽度断言，重新检查`month-880.png`，28/29/30/31等与+N都完整；未重构owner、缩放或贴边。
- 单实例由“命名对象是否新建”改为非阻塞获取互斥体并正确接管abandoned状态，退出显式释放。测试持有额外句柄、终止**自己启动的**hold进程，再重启验证锁和折叠/页面/置顶/实际矩形恢复；没有结束用户实例或Explorer。尚未刷盘的最后一瞬间偏好不能据此保证保存，业务事务仍是P2以后范围。

### 综合验证矩阵

| 项目 | 结果与证据 | 边界 |
|---|---|---|
| 原桌面owner、Win+D、任务栏/Alt+Tab排除、普通应用覆盖、输入与置顶往返 | PASS：用户O/后续阶段人工反馈；本轮原生样式/owner/TOPMOST回归 | 当前Win11环境，不代表Win10通用保证 |
| 恢复、缩放、分栏、两页、折叠、贴边及小把手 | PASS：P1.1-4/5用户验收＋本轮回归/渲染图 | 偶发轻抖为已接受限制 |
| 模拟待办分组/取消恢复、月历0/1/3/10条与5/6周临界高度 | PASS：自动布局/稳定键/原子偏好检查，用户P1.1-5反馈 | 仍非真实业务持久化 |
| 两页独立缩放、锚点、重叠布局、普通滚轮 | PASS：用户P1.1-5实际输入，自动比例/区间/滚动及8次真实进程恢复 | 边界夹入允许锚点偏移 |
| 负坐标/两屏重启 | PASS：实际两块144DPI显示器、8个模式组合的真实进程矩形 | 不涉及拔插或改变缩放 |
| 缺失显示器、分辨率变化、100/125/150/200% DPI | PASS：纯几何/拓扑模拟、可见工作区夹入与新位置保留 | **NOT VERIFIED**：真实混合DPI/拔插 |
| 多次启动、异常退出与重启 | PASS：真实独立测试进程、重复实例拒绝、abandoned接管、实际保存矩形恢复 | 仅终止自建隔离进程 |
| 宿主不可用/组件丢失 | PASS：模拟失效回退、托盘保留、有限重建/显式找回 | **NOT VERIFIED**：真实Explorer重启、休眠 |
| 正式图标 | PASS：SVG源/9帧/EXE/托盘资源/标题资源与离屏预览 | **NOT VERIFIED**：新托盘/搜索界面的实际像素 |
| 用户级安装/更新/卸载 | PASS：隔离完整流程及负面边界；真实用户目录安装/更新/启动通过 | 卸载在隔离目录验证，正式入口保留 |
| Windows10、长期运行资源、休眠恢复 | **NOT VERIFIED** | 没有相应实机/长期测试，不作为P2前全部完成的虚假保证 |

正常桌面账户 `build.ps1 -Configuration P117 -Test`：**371 PASS / 0 FAIL，构建0警告/错误，退出0**。新增九帧/EXE品牌、单实例/进程恢复、最小日期优先等断言全部执行，未删除旧13项owner断言。`scripts/Test-UserInstall.ps1`：**14 PASS / 0 FAIL**，见`artifacts/install-checks.txt`（正常安装、重复更新、不同包切换、损坏拒绝、路径越界、运行中卸载保护、部署启动、外来入口/目录保护、卸载数据/图片保留、后置失败回滚）。后置失败是把隔离安装记录位置临时替换为空目录，保证原子提交失败，断言快捷方式退回原目标后还原测试记录；只操作自建fixture。`Publish-DesktopCalendar.ps1`离线发布及正式部署检查通过；所有日志/测试偏好/故障注入与快照都在忽略的artifacts，未提交。

初次安装测试暴露：发布脚本对OrderedDictionary排序与反序列化对象不一致导致包ID不匹配，改为PSCustomObject统一排序，未取消哈希校验；Windows PowerShell5.1把File.Replace空备份参数变成空路径，改用NullString.Value后重复安装通过；测试框架对预期损坏包的原生stderr过早终止，改为明确记录退出码后检验拒绝条件。初次失败后均复跑，不把负面用例的预期拒绝计为产品通过以外的GUI证据。

### GUI工具与仅需的人工观察

本轮computer-use读技能后轻量连接/重试/JS重置仍为`native pipe unavailable (os error 2)`，没有反复刷429、注入替代输入或自动Win键。Logo预览与窗口图由程序自己的WPF视觉生成并查看，**不是电脑桌面截图**。既有真实GUI通过的依据是用户验收，部署检查的Visible/owner位也不替代像素。

唯一新增必要观察：先从旧实例托盘退出，按Win搜索`Desktop Calendar`启动，核对正式图标/标题及托盘可显示、设置、退出。若搜索尚未出现，直接打开已创建的当前用户开始菜单快捷方式即可；不要求重复已经验收的月历/缩放/恢复/Win+D等功能。Win10/混合DPI/Explorer/休眠保留将来具备环境后的补验，不擅自执行。

## 2026-11-01：P1.1-5 动态月历、时间轴缩放与小把手

**授权与前提**：用户本轮亲自确认 P1.1-4 基本通过：启动无明显初始化闪烁、恢复正确、左右贴边正常；偶发轻抖可接受。此反馈补齐前一轮必要人工验收，不重复要求 Win+D/任务切换/旧启动测试。仅进行P1.1-5与把手视觉小改，不开始P1.1-6/7/P2。owner、托盘、原生缩放控制器和Snap方案未重写。

### 实现与测试边界

- 月历固定日期行、可选固定信息行、独立自适应预览体。每行16 DIP，有剩余先留一行 +N；不足一行时在日期头显示计数。只排列/裁剪已有行，不因条数改变父级期望高度，不反复增删控件或切Visibility。0/1/3/10条真实样例含完成划线/弱化，原始键在各完成组内稳定。主界面仍是2026年10月样例，六周11月使用同一CalendarDays生成器离屏验证；未新增农历/假日数据或正式月导航。
- 单一 HourHeight 驱动24小时轨道、刻度、日程块起止/高度；默认50、最小24、最大160 DIP。Ctrl滚轮1.1倍/120 delta，支持小delta；换算旧时间锚点后调整新偏移，触及边界夹入。重叠连通组分列，空闲列复用，相邻端点不重叠；短卡片按实际时长保留高度，文字裁剪/提示，不人为增高。两页各自持有控件，只首次Loaded滚到06:00，切页不重置位置。
- schema2兼容新增两个比例字段，默认50；八次真实新进程用64/93两个不同值验证展开/折叠、桌面/置顶、两屏重启恢复，记录实际控件比例而非仅反序列化配置。
- 把手视觉9×48 DIP、提示线2×18，原命中28×110 DIP保留；关闭大Shell背景，左右对称。WPF InputHitTest 检查视觉标记之外(14,10)仍命中RestoreButton，悬停/键盘/找回路径未重写。144 DPI的实际尺寸允许一个物理像素取整，不用DIP严格相等冒充无效失败。
- 轻抖：固定起点物理坐标、相同矩形去重、拖动时贴边/工作区修正保护已存在。未发现新可复现根因，没有额外补偿或刷新节流；保留用户接受的偶发轻抖限制。

### PASS：自主验证

正常Windows桌面账户执行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Configuration P115 -Test`：**最终359 PASS / 0 FAIL，构建0警告/错误，脚本退出0**。离线构建无新增第三方依赖；结果在 `artifacts/p1-checks.txt`，日志、偏好和渲染图均忽略提交。

- 各0/1/3/10条，在28、57.9/58.1、73.9/74.1、89.9/90.1、220 DIP单格高度验证（含固定信息行、行数临界、零正文、计数预留、重复UpdateLayout稳定、全部显示与完成样式）。额外纯容量边界逐槽上下0.1验证单调性；真实样例数量/完成分组检查。
- 五/六周月份300/610/1050 DIP网格高度，所有日期同高、计数正确；生成 `calendar-10-*.png` / `calendar-11-*.png`。代理直接检查最终深色WPF渲染图：低高度只有+N或一条完整待办，高高度能显示十条，日期和计数不被待办遮住，各周布局一致。初始独立网格PNG透明背景在图片查看器下显白，已给测试网格加入与主界面一致的深色底后重新核对；实际主窗口未出现白色日期格。
- 两页缩放上/下限、连续小delta、指针时间锚点、切页保持比例/偏移；普通Routed MouseWheel仍滚动且比例不变。所有刻度/卡片/轨道按同一比例定位；重叠三列、相邻块回单列、区间裁剪至00:00–24:00验证。生成并查看 `timeline-day-*` / `timeline-week-*`：24/145/160 DIP比例、重叠并列、短块裁剪及长块高度符合预期。
- 生成并查看 `edge-small-Left.png` / `edge-small-Right.png`：小标记贴左右边对称，原大侧栏底消失；命中尺寸及非视觉标记区域命中检查。原窗口状态、页切换、分栏、八方向、圆角透明、TOPMOST清除、托盘恢复、原子偏好/迁移等全部回归仍执行。
- 初次构建因测试局部变量slots重名失败，改名后通过；首轮348/5中的3项为零空间时错误要求“必须预留一整行”，实际应走头部计数，另2项为144DPI取整的严格尺寸等式，均修正测试假设后复跑353/0。最终又增加真实样例分组、实际临界点和透明命中检查；不隐去初始失败，也不把断言放宽替代产品功能修复。

### NOT VERIFIED 与唯一人工检查组

已读computer-use技能，本轮轻量连接、重试及JS重置后仍为 `native pipe is unavailable / os error 2`，停止GUI请求；不是功能失败，未反复刷429或改用其他输入注入。上述图是**真实WPF视觉树的离屏RenderTargetBitmap**，不是桌面截图；自动Ctrl缩放是调用同一处理方法，普通滚轮为WPF路由事件，都不是物理鼠标输入。无法据此证明真实Ctrl键识别、滚轮手感、实际屏幕无闪烁和小把手悬停易用性。

只保留一组用户检查：从托盘退出旧版，打开 `D:\projects\desktop-calendar\bin\P115\net10.0-windows\DesktopCalendar.exe`（根目录P1快捷方式也指向本次配置）；纵向调整窗口看月历条数与+N，再分别在两页Ctrl+滚轮/普通滚轮、来回切页检查比例/时间位置，最后将窗口贴边并悬停新小把手。无需重复启动恢复/Win+D等已验收行为。Win10、真实混合DPI、Explorer重启/休眠仍留后续综合验证；本轮交付不标记全面实机验收。

## 2026-11-01：P1.1-4 续验（开发快照，实际 GUI 尚未验收）

保留全部未提交工作，没有重写、丢弃或绕过原失败断言。已重读 AGENTS/PRD/DECISIONS；仅补充 owner 分步骤错误诊断（ResolveDesktop、DetachOwner、ConfigureToolWindow、SetTopmost、AssignDesktopOwner，原生错误附 Win32 码），未更改 owner 技术行为。

### Owner 失败复核

**PASS：正常桌面账户运行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test`：Release 构建 0 警告/错误，完整 221 PASS / 0 FAIL，进程退出0。** 没有跳过或删除测试；两屏均144 DPI，包含负坐标。以下是原13项逐项对应结果，不能把受限环境失败直接改记通过；本次 PASS 依据是新的正常会话执行。

| 原失败项 | 失败的共同依赖 / 本轮判断 | 正常会话复测 |
|---|---|---|
| default desktop owner is top-level | owner 失败后进入 Fallback，无法满足 desktop 断言 | PASS |
| compact inherits desktop and resizable grips | 折叠继承了 Fallback；不是已证实的折叠控件回归 | PASS |
| native TOPMOST cleared and desktop restored 0 | 关闭置顶后的 desktop 恢复断言依赖 owner；不能据此称 TOPMOST 清除失败 | PASS |
| native TOPMOST cleared and desktop restored 1 | 同上，第2次往返 | PASS |
| native TOPMOST cleared and desktop restored 2 | 同上，第3次往返 | PASS |
| simulated owner loss enters explicit fallback | 先决条件 desktop 未建立，CheckOwner 在 Fallback 返回，模拟丢失分支未被触发 | PASS |
| explicit locate retries desktop without changing view | 重试仍需访问桌面 owner | PASS |
| tray returns to desktop and clears TOPMOST | 托盘关闭置顶后仍需 owner；复合断言不能独立归罪托盘或清除位 | PASS |
| real process restore DISPLAY1 / fold=False / pin=False | 首屏展开非置顶分支要求 DesktopOwner=true | PASS |
| real process restore DISPLAY1 / fold=True / pin=False | 首屏折叠非置顶同上 | PASS |
| real process restore DISPLAY2 / fold=False / pin=False | 次屏展开非置顶同上 | PASS |
| real process restore DISPLAY2 / fold=True / pin=False | 次屏折叠非置顶同上 | PASS |
| recreated component can explicitly return to desktop | 安全回退后显式找回仍需 owner | PASS |

环境对照：只读 `whoami` 确认普通执行为受限沙箱账户，正常桌面执行为交互用户（不保存账户名到仓库）。同一 Release 二进制、同一隔离偏好、同一 `--restart-check`：受限执行报告 **DesktopOwner=false、`ResolveDesktop; DesktopUnavailable`**；正常桌面执行报告 **DesktopOwner=true、LayerError为空**。两者均 Ready/Visible=true、Collapsed=true、DPI144。这支持“测试环境不具备交互用户桌面 owner”而非13个独立产品缺陷；不能推断任意受限环境可用。历史87错误没有分步骤日志，本轮未精确复现其具体 API 返回点，**不声称已查清历史87的唯一内核原因**。测试仍保留严格 owner 断言，未自动忽略环境失败。

证据在忽略目录：`artifacts/p1-checks-normal-2026-11-01.txt`、`owner-restricted-2026-11-01.json`、`owner-normal-2026-11-01.json`；历史208/13原始报告已被本轮正常执行覆盖，原失败列表保留在下方2026-10-31记录及本表，不能声称原始失败日志仍完整存在。不提交日志或个人配置。

### 三项窗口功能与工具限制

- **PASS（原生几何）/ NOT VERIFIED（像素闪烁）**：8次新进程正常启动覆盖两屏×展开/折叠×桌面/置顶；准备完成前未见 WS_VISIBLE，首次可见后的 WINDOWPOSCHANGED/PAINT/ContentRendered 所记录矩形和视图均为目标值，无默认中央/默认大小跳转。没有实际逐帧启动画面，不将此称为肉眼无闪烁验收。
- **PASS（控制器/真实 HWND）/ NOT VERIFIED（实际指针平滑性）**：八方向各30步固定起点连续缩小、重复绝对坐标输入、最小尺寸与原生矩形检查通过；特别包括 NE/SW。测试直接调用同一手势控制器，没有注入真实鼠标，也不验证 Thumb 路由或远程帧流平滑。
- **PASS（状态/矩形）/ NOT VERIFIED（实际拖拽、悬停与 Snap）**：左右工作区靠边、延时收起、窄条保持可见、悬停恢复、关闭开关不收起、置顶保留、折叠/托盘独立等自动检查通过。标题代码不再调用 DragMove，不改全局 Snap，但不能把程序几何检查称作鼠标实测避免 Snap。
- **工具不可用而非功能失败**：本轮正常桌面构建/测试、Git fetch 已可执行，未再出现429。computer-use 的 `list_windows` 返回 **`native pipe is unavailable ... os error 2`**；按技能轻量重试、重置 JS 会话后最后重连仍失败，停止输入尝试，没有新截图/实际拖动，不改用 SendInput/UIAutomation 或自建通道绕过。未操作 Codex UI、系统设置或 Explorer。历史429曾阻止 GUI、正常执行与 Git 升级请求，不代表项目内读写永久受限；本轮未重复刷429请求。

### 最少人工检查与交付边界

本轮需用户补两组实际画面验收（工具恢复前无法代做），不重复 Win+D/任务栏/Alt+Tab：

1. 双击 `D:\projects\desktop-calendar\bin\Release\net10.0-windows\DesktopCalendar.exe`（已重新构建）；移动并折叠后从设置/托盘退出、重开，观察首次出现即为保存的位置/大小/折叠状态，是否仍有跳变。
2. 展开后匀速从右上/左下角缩小；打开贴边开关，将完整窗口分别拖到左右工作区边缘，松手移开约1秒应为可见窄条，悬停展开；关闭开关及进入今日待办折叠不误收起。

状态：未验收开发快照，不能标记 P1.1-4 全面通过。Win10、真实混合 DPI、Explorer 重启、休眠仍待后续综合验证。不进入 P1.1-5/P2。Git 仅提交源码/脚本/文档，不提交上述 artifacts；提交/推送结果以实际 Git 执行及阶段反馈为准。

## 2026-10-31：P1.1-3 收尾 + P1.1-4（验证未闭环，不能标记全面通过）

授权：用户批准两阶段连续推进，不中途请求验收；尚未授权 P1.1-5/P2。AGENTS.md 已重读，owner 技术模块未修改。下方 2026-10-30 的启动后恢复/完成追加属于历史，已被本轮需求覆盖。

### 根因、修复与证据

- **启动**：旧代码在 ContentRendered 后才设置 owner、恢复完整矩形，空闲时复核，再折叠；必然允许默认中央/默认尺寸先显示。改为 Manual + EnsureHandle，在首次 Show 前准备真实目标位置、大小、折叠控件和原 owner 模式；ContentRendered 只开始正常保存。无屏外停泊、降低透明度或定时修正。记录本窗口 WS_VISIBLE 后的 WINDOWPOSCHANGED/PAINT/ContentRendered 矩形与模式，另检测是否准备完成前已可见（5 秒/256 条上限）。
- **排序**：旧 completionSequence 使完成组按勾选先后排列，现删除；Completed、可选 TimeSortKey、OriginalOrder 构成两个状态组共享的排序。完成/取消不修改任何基础键，同级子任务独立。包括用户 A/B/C/D 示例、两种勾选顺序、取消、全部完成、手动混排、不同时间排序及子任务进度。
- **缩放**：Thumb 相对自身的增量在 N/W 边移动后会改变参考系；原实现在不断变化的矩形上累加，形成反馈条件。八方向改为固定起点的物理屏幕指针差；重复绝对位置输入幂等，原生矩形相同则不再 SetWindowPos；拖动中跳过另一路工作区/DPI 修正，手势结束后夹入一次。不禁方向、不卡刷新率。
- **贴边**：标题 DragMove 启动系统 caption/Snap 流程，改为本窗口标题鼠标捕获，不更改全局 Snap。完整窗口松手靠近左/右工作区边界时对齐；离开约 900ms 收起，28 DIP 可见条留在工作区，悬停恢复且不激活抢焦点。设置打开暂停，折叠/托盘隐藏不参与，置顶状态不变。保存收起前完整矩形，显示变化/托盘找回可恢复。
- **拓扑**：schema 2 兼容增加工作区宽高；模拟负坐标、工作区缩小、96/120/144/192 DPI、缺失屏和重连后保留新位置。真实拔插或混合 DPI 本轮未执行。Windows 显示名称不是永久硬件 ID。

### PASS / FAIL：本轮实际执行

1. 正常桌面会话、独立 `P114` 构建：收尾第一轮 **184 PASS / 0 FAIL**；加入缩放/贴边回归后 **216 PASS / 0 FAIL**，构建 0 警告/错误。8 次新进程覆盖两屏（含负坐标、均144 DPI）×展开/折叠×桌面/置顶，实际 HWND 最终和首次可见起的全部原生矩形/模式均为目标值。八方向30步连续缩小/重复指针输入、靠左/右、桌面/置顶、折叠独立、关闭开关等状态检查通过。这些是真实自有 HWND + 程序输入坐标检查，不是鼠标实测。
2. 后续增加“准备完成前可见”检测、不同基础排序与重连断言后，最新 `P114Final` 在受限执行会话离线构建 **0 警告/错误**；最新自测 **208 PASS / 13 FAIL**，脚本明确以失败退出。13 项均涉及 desktop owner/切回 owner：实际层级为 Fallback，错误 **`The parameter is incorrect.`**；首次可见几何、排序及缩放/贴边状态断言仍通过。不能把它冒充全通过，也不能仅凭正常会话前一轮通过就排除回归；正常桌面会话复跑被下述工具故障挡住，目前仅有“执行会话差异相关”的线索，没有确认唯一原因。
3. `--ui-check` 实际打开同一主界面的普通透明诊断窗口并成功激活/截图：深色圆角、时间前缀、两个页面入口及八个缩放控件可见；截图为1240×760，物理原点(-3750,90)。这只证明当次正常绘制，不证明缩放、排序点击或桌面行为。
4. 随后的右上角鼠标拖动请求被自动审批服务拒绝执行：**`429 Too Many Requests / approval review could not be completed`**。再次截图、只读 GUI 连接、正常桌面会话测试以及清理本轮诊断进程的请求也遭同类服务错误；这是审批基础设施故障，不是安全风险判定，没有尝试改用其他输入注入绕过。因此本轮 GUI 动作没有发生，不可记为通过。原有 Release 实例仍运行且锁定 EXE；没有结束用户实例。隔离 P114 GUI 检查窗口也未能通过界面正常退出，不将其算作退出验收。
5. 初次编译漏改 RestoreNormal 参数，修正后编译通过；Release 构建遇旧进程文件占用，后续改独立配置避免覆盖运行文件。`build.ps1` / `Start-Prototype.ps1` 支持可选 Configuration（默认 Release），便于复现隔离构建；没有安装软件或依赖、联网恢复、改全局设置或重启 Explorer。

### NOT VERIFIED 与最少人工检查

- 不能宣布“启动闪烁已被逐帧肉眼证实消除”：目前是首次可见起的原生几何无跳变证据，无完整启动视频；最新正常会话 owner 回归尚待补跑。
- 本轮真实鼠标 NE/SW 缩小平滑性、标题拖动/贴边避免 Snap、窄条悬停及完成/取消后的实际画面未完成；程序状态断言不能替代它们。原先用户认可的 Win+D/任务栏/Alt+Tab不重复要求人工检查。
- 若工具恢复，优先由代理继续；若由用户检查，最多两组：①从托盘退出旧版，用 `bin\P114Final\net10.0-windows\DesktopCalendar.exe` 打开，移动/折叠后退出重启，观察首次出现是否仍跳变，并确认设置中的模式没有回退；②匀速缩小右上/左下角，开启贴边后分别拖至左右工作区边缘、松手移开约1秒、悬停窄条展开，同时切换折叠和关闭贴边确认不误收起。无需现在拔插屏幕、改变DPI或重测旧 Win+D 项。
- 自动复现：正常 Windows PowerShell 运行 `.\build.ps1 -Configuration P114Final -Test`。最新报告 `artifacts/p1-checks.txt`，8份启动时序 `artifacts/restart-check-preferences/startup-*.json`；仅窗口元数据，不含待办正文或桌面图像，全部忽略提交。
- Win10、真实混合 DPI、Explorer 重启、休眠仍保留后续综合门槛；P1.1-5 动态行数和时间轴缩放未开始。

### Git 与交付状态

已检查14个变更文件及 `git diff --check`，仅源码、构建脚本与四份必要文档；无图片、构建产物、凭证或用户数据。当前仍在 `dev/p1.1`，`main` 为 `d0e76d510d1052e7095b8a0e825d69601c5ea068` 未动。受限执行无法创建 `.git/index.lock`；已授权的暂存升级请求又被自动审批服务429挡住。因此本轮**尚未暂存、提交或推送**；不创建“已验收”标签。恢复工具后应先在正常桌面会话复跑最新版本，补 GUI 检查，再提交/推送，不进入 P1.1-5。

## 当前 P1.1-3 修订：恢复、时间和完成沉底（2026-10-30）

**范围：用户批准的三项补丁，不进入 P1.1-4/P2。** 根目录用户提供的 AGENTS.md 已读并保持原文。已通过的 owner 方向保留，不重新验收 Win+D、任务栏或 Alt+Tab。

### 修复依据

原实现确定没有持久化折叠矩形，折叠必定重算右上角；完整矩形在 Loaded 阶段恢复，后续 owner/初始布局还可能变更窗口，启动期间缺少完整采样隔离。新实现先完成呈现和 owner，再定位目标显示器、读取实际窗口 DPI、复核布局后启用保存；完整/折叠分别保留矩形和屏幕信息。退出同步保存，隐藏/最小化不采样无效矩形，临时安全回退不覆盖桌面/置顶偏好。schema 1→2 迁移保留旧完整矩形/模式，旧文件未保存的折叠位置只能首次使用默认值。未声称查明用户所有环境下恢复失败的唯一原因。

### PASS：自动和真实进程检查

- 最终 `build.ps1 -Test` 为 **155 PASS / 0 FAIL**，Release 构建 **0 警告/错误**，退出码 0。报告 `artifacts/p1-checks.txt`，无第三方依赖。
- 八次独立进程真实启动/退出：两块实际显示器分别测试展开/折叠 × 桌面/置顶全部组合，读取新进程实际 HWND 矩形、当前显示器、DPI、视图和原生 TOPMOST；桌面案例同时检查 owner。不是只反序列化配置。显示器含负坐标，当前两块均为 144 DPI；Visible 是 WPF 位，不作为像素通过依据。
- 窗口：独立折叠矩形、隐藏后保存、完整恢复、页面/分栏、八方向事件、三次 TOPMOST 清除、托盘回调、有限组件重建和原有透明布局回归。
- 数据：v1 迁移、v2 原子保存/备份、损坏恢复、未来 schema 拒绝覆盖；数学模拟 DPI 变化后的尺寸/工作区偏移、消失屏幕选择和极小工作区安全夹入。
- 排序：完成追加到已有完成项之后；取消恢复原键；手动混排/时间排序都先未完成后完成；子任务同样实时排序及进度同步；时间前缀正确，无时间无占位。模拟完成状态仍不持久化。

### PASS：代理实际 GUI 操作及像素观察

computer-use 不能选择正常 owner 工具窗口，且 drag 拒绝窗口边界外坐标。使用显式 `--ui-check` 运行**同一主界面与恢复逻辑**，仅临时改为可选择的普通透明窗口，偏好隔离在 artifacts；不把它作为 owner/Win+D 证据。下面均通过实际点击、截图及工具返回窗口位置观察，不是离屏渲染。未保存或提交桌面截图。

| 实际操作 | 观察 |
|---|---|
| 完整页时间 | 09:30、14:00 前缀呈浅绿色半粗，任务文字可读；无时间任务无占位 |
| 完整页完成/取消 | 09:30 项从第一项移至已有完成项“整理工作区”之后；取消后回第一项 |
| 子任务完成/取消 | “确定布局与留白”完成后排在已有完成子项之后，进度 1/2→2/2；取消后原顺序及 1/2 恢复 |
| 折叠时间/排序 | 今日日期、计划时间、无时间任务均可见；完成追加最后、取消回第一项 |
| 独立矩形 | 折叠移动到第二屏后，展开仍恢复此前主屏完整矩形 (990,474)，图像 1240×760 |
| 折叠跨屏/缩放/重启 | 通过仅诊断可见的“移到下一显示器”实际移到左侧屏，拖右下角将图像 360×350 缩至 334×324；设置滚动至退出，确认进程窗口消失，再重启并激活截图。前后 origin=(-3750,90)、图像334×324完全一致，仍折叠且时间/列表正常 |
| 完整置顶跨屏/重启 | 实际勾选置顶、移到第二屏，设置退出后重启。前后 origin=(-3750,90)、图像1240×760一致，仍完整；重开设置实际看见置顶勾选保留 |
| 清理 | 通过设置正常退出隔离 GUI 实例，无强行结束用户进程；全部 GUI 偏好、进程报告与图像在忽略目录，未覆盖真实用户偏好 |

工具 origin 为窗口屏幕原点，图像 width/height 是工具截图尺寸；144 DPI 时不等同 GetWindowRect 的物理宽高，不把两种单位混用。少数操作后 accessibility 树更新晚于截图，本轮以重新观察后的画面及新树核对，不使用旧索引重试未知操作。

### 修复中的 FAIL 及最终边界

- 初轮实际启动出现只读 ChildProgress / Run.TimePrefix 的默认双向绑定异常，进程未生成新报告；脚本拒绝旧报告。已对只读进度、时间、标题明确 OneWay，随后真实初始化及完整检查通过。
- 一轮 146 PASS / 1 FAIL 的旧断言假定折叠永远跟随当前屏右上角，与新“独立记住折叠位置”需求冲突；改为核对保存的折叠显示器及矩形，另用真实重启/GUI 证明恢复，并非降低要求。
- 一轮构建因诊断 EXE 尚运行发生复制占用失败；未算通过，之后通过界面正常退出再构建成功。GUI 首次退出点击未关闭时重新观察入口并正常退出，未强杀。一次只读进程检查的自动权限审核超时，重试成功，非测试失败。
- **NOT VERIFIED**：混合 DPI 实机切换、实际拔插/重排显示器、Win10、Explorer 重启、休眠；没有修改显示设置来制造测试。几何模拟不能替代这些环境。
- **NOT VERIFIED**：正常 owner 模式窗口重启后的肉眼像素（工具无法选取），由八次真实 owner/置顶进程的几何与配置检查、同一恢复逻辑的 GUI 像素结果交叉支持，不标成正常 owner 的 GUI 验收通过。

最少人工补充仅一组：正常打开 `bin\Release\net10.0-windows\DesktopCalendar.exe`，折叠并移到所需位置，从设置/托盘“退出”后重开，确认仍在原屏原位置且折叠尺寸正常。无需重复 Win+D、任务栏或 Alt+Tab；排序及时间显示已自主操作，不要求再测。混合 DPI 等仍保留后续综合门槛。

下方为 P1.1-3 初版及更早历史，当前结果以本节为准。

---

## 当前 P1.1-3：桌面 owner 主程序整合（2026-10-30）

**状态：实现和自动回归完成，主程序人工验收待反馈。** 用户已明确验收独立 O 的正常绘制、Win+D 可见、任务栏/Alt+Tab 排除、移动缩放、普通应用覆盖及置顶往返，并批准本阶段。下方 P1.1-2 的“待验收/不整合”是历史状态，由此记录更新；不重跑 A–F，不进入 P1.1-4、P1.1-5 或 P2。

| 验证 | 结果与边界 |
|---|---|
| 构建与自动检查 | `build.ps1 -Test`：123 PASS / 0 FAIL，Release 构建 0 警告/错误，脚本退出码 0；报告 `artifacts/p1-checks.txt` |
| 桌面模式 | 主 HWND 保持顶层、透明 LAYERED、TOOLWINDOW、无 APPWINDOW/NOACTIVATE/WS_CHILD；owner 指向校验过的 Progman。只证明配置，不代替主程序 Win+D、任务切换列表及视觉验收 |
| 置顶往返 | 三次往返后原生 TOPMOST 与 WPF Topmost 均为 false，owner 恢复；位置、尺寸、页面、分栏、不透明度及相同 HWND 保留。空闲检查没有层级操作 |
| 折叠与布局回归 | 今日日期、四条模拟待办、勾选与完整页共享、恢复完整视图、继承非置顶、折叠八方向手柄；两页布局、最小尺寸、分栏键盘调整与八方向 Thumb 事件检查通过。不是实际鼠标八方向或跨 DPI 测试 |
| 托盘与找回 | 菜单回调覆盖显示/定位、置顶开关、设置；定位保留折叠。模拟 owner 丢失安全回退；受控关闭组件后托盘仍在，最多一次重建回退窗口，保存的折叠/页面恢复，可显式重试桌面 owner。不是实际 Explorer 重启或原生 HWND 强制销毁测试 |
| 窗口偏好 | schema=1、稳定 ID、原子替换及 `.bak`；往返、损坏主文件恢复备份、两者损坏默认回退、未知 schema 禁止覆盖、非法尺寸拒绝保存。测试仅写隔离临时目录，无模拟业务文字或勾选状态落盘 |
| 渲染图检查 | 代理已查看折叠及设置面板生成图，日期、勾选、恢复/设置/退出入口无明显重叠；报告图仅为 WPF 离屏渲染，不是桌面实际像素证据 |
| 主程序实际启动 | 通过 computer-use 显式启动当前 EXE；工具报告 `launched app did not expose a targetable window`，刷新仍无可选目标，故停止注入输入。独立进程查询确认 DesktopCalendar 存活且 Responding=True；未声称由此证明可见、可点或 Win+D 通过 |

失败与修复记录：首轮构建中 `App.MainWindow` 属性遮蔽类型名导致 2 处编译错误，已使用完整类型名；第一轮偏好非法数据检查暴露 InvalidDataException 未被原异常过滤器捕获，加入明确处理后完整复跑通过。最终结果只取最新报告，构建脚本现在拒绝过期报告及失败退出码。没有隐去失败，也没有联网恢复、安装工具或修改系统设置。

复现：先从托盘或设置真正退出已运行原型，再从项目根的 Windows PowerShell 执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

实际启动双击 `D:\projects\desktop-calendar\bin\Release\net10.0-windows\DesktopCalendar.exe`。本轮已启动一个正常实例；重复启动只会提示通过托盘找回。请只检查以下三组：

1. **桌面与置顶**：Win+D 后看见深色透明组件，任务栏/Alt+Tab 无组件；普通应用可覆盖。设置开启置顶再关闭，检查位置/尺寸/页面保留且普通应用重新可盖住。
2. **折叠与原交互**：第一页调整分栏、切第二页、四边四角缩放；点“—”看今日列表，勾选后恢复完整视图，检查模拟状态同步、完整矩形恢复、不自动置顶。
3. **管理与重启**：组件设置和托盘均能打开设置/退出；× 隐藏后托盘显示可找回，折叠后真正退出再启动应仍折叠。必要时用托盘“安全找回（普通窗口）”；该回退不保证 Win+D 可见。

已知待办保持原状：特定方向缩小抖动、Snap/贴边冲突、月历动态条数和时间轴缩放未修复。本阶段不要求 Win10、混合 DPI、实际 Explorer 重启或休眠恢复；这些仍是后续整合验收门槛，不能记为通过。模拟数据仍不可作为真实日程使用。

---

以下为独立实验历史，最新状态以本节及用户人工反馈为准。

## 当前 P1.1-2：候选 1，透明顶层 WPF 的桌面 owner（2026-10-30）

**状态：独立实验已实现，核心桌面行为待人工验收；不进入 P1.1-3。** 只改实验和研究/测试文档，主程序不变。新路径保留 AllowsTransparency/LAYERED 和顶层 WPF，不调用 SetParent、不开启 WS_CHILD、不发 Explorer 私有消息；没有事件监听/失焦纠正/自动压底或 Win+D 后恢复。只有显式置顶/返回命令可变更组件的 TOPMOST 状态。未安装依赖或修改系统设置，未重跑历史 A–F。

| 证据类型 | 本轮结果与范围 |
|---|---|
| 最终离线 Release 构建 | 0 警告 / 0 错误 |
| owner 自动检查 | 28 PASS / 0 FAIL：owner 与 parent 区分、透明/输入激活样式、同 HWND/状态/尺寸保留、置顶往返、无 SetParent/首次无显式 Z-order 调整、空闲诊断不纠正、普通恢复、原生相对覆盖顺序。离屏 RGB/alpha 不能证明屏幕像素 |
| owner 控制台自动检查 | 11 PASS / 0 FAIL：透明普通启动、命令 ACK、置顶/owner/普通往返、新 PID 找回且确认重置、意外销毁后只恢复一次普通透明窗口、二次损失不循环、正常退出 |
| 代理实际电脑操作通过 | 普通透明组件可见、真实键盘输入含中文、鼠标计数 0→1、确认；标题空白区移动、右下角缩放；owner/置顶往返后恢复普通窗口，截图仍有原文字/计数和彩色透明内容；通过控制台正常退出后无实验进程残留 |
| 代理实际有限覆盖观察 | 普通非置顶珊瑚红测试窗口画面可见；O 的 3 个命中点均属于该窗口且其为前台，O 非 TOPMOST。窗口捕获不是完整桌面合成证据；未完成 O 编辑后覆盖、关闭再打开循环 |
| 仅配置/原生状态通过 | O 的 GW_OWNER 指向校验过的 Progman，GA_ROOT 为自身、CHILD=false、LAYERED=true、TOOLWINDOW=true、APPWINDOW=false；首次 explicitModeZRequests=0、SetParentCalls=0；显式置顶往返 ACK/样式通过。不能据此标记任务栏/Alt+Tab 或 Win+D 通过 |
| 仍待用户实际检查 | **设置 owner 后内容是否可见可编辑、Win+D、真实任务栏/Alt+Tab、O 模式拖动/缩放、编辑后普通应用覆盖、置顶往返画面/闪烁/焦点、托盘菜单找回和退出** |
| 工具限制 | O 与 owner-topmost 不在可选窗口列表，重绑先前工具窗口 ID 失败，故不能截图或注入组件输入；不等于黑屏/成功。当前 computer-use 不支持 Win 键，未通过替代途径注入。混合 DPI、Explorer 重启、休眠和 Win10 全量测试按本轮要求暂缓 |

失败记录：首轮 27 PASS / 1 FAIL，中心命中覆盖窗口的断言受到已有普通应用遮挡（实际前台不是覆盖窗口）影响。现拆分原生相对 Z-order 自动断言和实际覆盖观察，保留 hit/foreground 不匹配日志，不宣称自动捕获了覆盖像素。后续最终 28+11；没有强制焦点/层级修正掩盖失败。普通模式第一次标题文字区拖动未见位移，重新从标题空白区操作观察到位置改变；仅该动作及右下角缩放通过，不扩展成八方向全部通过。

复现自动检查（项目根 Windows PowerShell）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\experiments\DesktopHostProbe\Start-Probe.ps1 -OwnerTest
```

脚本退出码 0。报告在被忽略的 `artifacts/p1.1-2-owner-checks.txt`、`artifacts/p1.1-2-owner-supervisor-checks.txt`，未提交截图或输入正文。默认启动变更为 owner 专用控制台，必须先真实交互确认再显式设置 owner；自动测试专用 bypass 不设置 HumanConfirmed。代理本次点击确认不是用户验收。

人工检查：双击 `experiments\DesktopHostProbe\bin\Release\net10.0-windows\DesktopHostProbe.exe`，应见“owner 独立实验控制台”和“B 透明对照组件”两个窗口；组件输入/计数/确认→控制台 O→手动 Win+D 与任务栏/Alt+Tab→普通覆盖窗口、编辑及重开→置顶再 O→普通恢复/必要时重建→退出。具体预期、限制和安全找回步骤见 [当前研究记录](DESKTOP_MODE_RESEARCH.md#当前候选-1桌面-owner--透明顶层-wpf2026-10-30)。若 O 失败，不自行增加候选 2 或合并主程序。

---

以下保留历史测试结果，不作为本轮候选 1 的验收证据。

## P1.1-2：F 黑屏人工结论与自有 R/N 宿主对照（2026-10-30）

**用户确认：F 黑屏；输入及计数因黑屏无法判断；移动、缩放正常；返回 E 成功。** 此结果优先于先前 F“未观察到”的工具限制记录。当前 Progman 挂载方案验收失败，不满足主窗口整合门槛，不进入 P1.1-3。

只改实验：增加有界只读 Shell 元数据、GDI 最后绘制模式/clip、独立自有 R/N 宿主。R 普通重定向，N 仅在自有宿主创建时加 NOREDIRECTIONBITMAP；均未改 Explorer 样式/图标/壁纸/系统设置。代理实际截图看到 R/N 内的色条、文字及控件，不能推定 F 通过或跨进程呈现已验证。本轮 R/N 输入、移动未另验。

| 验证 | 结果及限度 |
|---|---|
| 最终构建 | 0 警告/错误；首轮 Size 类型歧义及 EXE 占用已修正后重建 |
| 完整自动 | Default 53 PASS / 0 FAIL；SoftwareOnly 53 PASS / 0 FAIL；控制台 13 PASS / 0 FAIL |
| R/N 初始化及范围 | Shown 前挂载被拒绝；样式按创建参数建立；同 HWND 挂载/脱离保留输入；即使宿主跨屏也将子窗口置于其客户区 |
| Explorer 层级 | Progman EX=0x200080，DefView layered，未发现 WorkerW；F 为前方子窗口，客户区相交。元数据不是屏幕像素 |
| GDI clip | F 有 NULLREGION 回调；N 也出现空更新区且实机内容可见，因此不把空 clip 单独认作根因 |
| 根因判断 | 不限于 WPF；同进程 NOREDIRECTIONBITMAP 单独未复现。跨进程/Explorer 合成/远程输出仍未区分 |
| 清理 | R/N 已通过各自正常关闭入口退出；更新 Release 前正常退出旧控制台及组件，更新后保留普通 A/控制台找回入口 |

测试经一次自动权限审核超时后重试执行成功，不是测试失败；未自动重启 Explorer、休眠或改 DPI。定位中使用的另一输出目录也是实验 bin 内被忽略的构建产物；未改主程序。复现命令和 SDK 首次证书提示的检查边界详见 [研究报告最新节](DESKTOP_MODE_RESEARCH.md)。

暂停当前 Progman 路径的整合，不宣称黑屏已修复；不要求用户重复 C/F。后续若获准继续研究，优先可控跨进程自有宿主对照，改变 Explorer 内部结构的方案须单独批准。

## P1.1-2 代理实机交互记录（2026-10-30）

computer-use 本轮已连接成功，代理通过目标窗口截图、鼠标和键盘执行下列检查；不使用属性赋值冒充键盘交互，不把代理操作归为用户亲自验收。本轮发现 E 普通模式紫色标题区移动异常后，只修复这一独立实验行为并复跑构建/自动套件，不修改主程序。

- **通过，代理目视及实际输入：**E 普通原生组件彩色色条、标题、文本框和按钮可见；核对输入焦点后输入无隐私文字，计数由 0 到 1，交互确认开放 F。
- **仅原生挂载证据：**E→F 有成功 ACK、parent/host 匹配。F 成为 Explorer 子窗口后不再出现在工具可选顶层列表，旧窗口绑定报 window id was not found；因此无法用该工具继续截图/点击 F。此项不是视觉通过，也不是新确认的 F 黑屏。
- **通过，恢复实机检查：**控制台 F→E 后原生窗口重新可选，截图显示文字与计数 1 保留；A 重建后新的普通 WPF 组件可见、内容清空和确认门槛重置。
- **部分动作通过：**E 系统标题栏拖动及右下角缩小有截图位置/尺寸变化且内容保留；未覆盖八方向。紫色区域拖动初次未见位移，改为 E 专用的原生标题栏命中后，代理同样拖动已实际移动，截图原点 (1404,601)→(1464,639)、尺寸 688×584 不变、内容保留。F 移动路径未改，不由此推定通过。
- **未测：**F 实际桌面像素、嵌入输入及拖动；Win+D、Explorer 重启、休眠、混合 DPI。没有修改系统设置、安装工具或操控其他应用。

修复后自动结果：Default **44 PASS / 0 FAIL**、SoftwareOnly **44 PASS / 0 FAIL**、控制台 **13 PASS / 0 FAIL**，脚本退出码 0；Release 0 警告/错误。新增断言验证 E 紫色区域的原生命中，不用断言代替鼠标移动的截图验证。

运行结束保留控制台与普通 E 组件，A 可重建找回。本轮不保存/提交桌面截图或输入正文；详见 [研究报告最新记录](DESKTOP_MODE_RESEARCH.md)。主程序未修改，仍停在 P1.1-2。

## P1.1-2 人工失败跟进：原生 E/F 定位（2026-10-30）

**最新人工结果：C 可缩放但内部纯黑；B 透明顶层符合预期；D 不可见；A 找回有效；重复操作未改善。桌面嵌入验收失败。** 用户第 4 项未明确软件选项是否勾选并重建，暂记未知，不能宣称软件渲染已被人工排除。混合 DPI、Explorer 重启和休眠继续暂缓。

只新增实验原生 GDI/Windows Forms 对照及必要诊断，不改主程序，不安装依赖或修改系统设置。E 是独立普通原生窗口，F 是同一 HWND 挂到 C/D 所用宿主。E/F 期间 WPF 窗口有意隐藏；控制台分别展示当前观察目标与 WPF 后台诊断。E 手动输入、计数和确认后才允许 F；不把 WPF 确认传给 E。

| 验证项 | 本轮结果 |
|---|---|
| 构建 | Release 0 警告、0 错误 |
| 完整自动检查 | Default 43 PASS / 0 FAIL；SoftwareOnly 43 PASS / 0 FAIL；控制台 13 PASS / 0 FAIL，脚本退出码 0 |
| E 绘制及控件 | GDI WM_PAINT 回调次数 >0，原生 Edit/Button HWND 存在；仅回调和元数据证据，不是屏幕像素证明 |
| F 宿主及样式 | 实际父窗口与 C/D 同为现存 Progman；同 HWND、WS_CHILD、非 layered；挂载或错误回退明确应答 |
| 找回和退出 | 同 HWND 返回 E 保留输入、模拟宿主失效返回 E、关闭销毁原生 HWND、控制台恢复 WPF、A 新进程重建及整个实验退出通过 |
| 确认门槛 | F 在 E 未确认时拒绝；聚焦状态下程序 Text 赋值/PerformClick 不伪造人工确认 |
| 人工待验 | E/F 色条文字是否可见；原生输入和计数；拖动缩放；回退的肉眼可见性 |

首次自动运行有 2 个失败：原生类名为 Edit/Button 而断言用了大写；输入框聚焦时 Text 程序赋值也会开启确认入口。已修正类名大小写检查、限定输入消息期间的编辑后全量复跑通过。没有用自动测试否定用户 C/D 失败反馈。

复现自动检查仍用 `powershell -NoProfile -ExecutionPolicy Bypass -File .\experiments\DesktopHostProbe\Start-Probe.ps1 -Test`，报告位于被忽略的 `artifacts/`，原生日志不含输入正文。手动只需：启动 EXE → E → 输入/计数/确认 → F → 记录 → 返回 E 或 A 找回 → 退出整个实验。预期色条、文字、输入框及按钮仍可见可操作；F 失败时分别记录“整个矩形黑/不可见”与“仅控件缺失/输入失败”。详见 [研究说明顶部](DESKTOP_MODE_RESEARCH.md)。

本轮仅提供进一步定位工具，未证明 WPF 根本不兼容，未解决或验收桌面半透明视觉，不进入 P1.1-3。

## P1.1-2 黑屏：A–D 渲染对照（2026-10-30）

**人工结论：前版桌面宿主出现可缩放的纯黑组件；内容不可操作，验收失败。用户确认任务栏/Alt+Tab 排除符合预期。** 用户当前远程连接，混合 DPI、Explorer 重启及休眠按要求暂缓。本轮只改实验及本测试记录/研究说明，不修改主程序。

已确认前版组件是非透明 WPF；WorkerW 请求实际返回 `Ok=false / normal / 没有符合条件的现存宿主`，并非 WorkerW 成功。当前用 A 非透明顶层、B 透明顶层、C 非透明嵌入、D 透明嵌入控制变量对照；每轮 A/B 重建，C/D 复用对应 HWND。B/D 保留半透明圆角、WPF 自动 layered，禁止定时重绘或手动 layered 翻转掩盖问题。

自动结果：Default **31 PASS / 0 FAIL**、SoftwareOnly **31 PASS / 0 FAIL**、控制台 **10 PASS / 0 FAIL**，整套脚本退出码 0。Release 构建 0 警告/错误，无依赖安装。

| 证据 | 当前结果与限度 |
|---|---|
| A/B HWND | parent=0；A 非 layered，B AllowsTransparency=true 且 layered |
| C/D HWND | 实际父窗口为 Progman；CHILD=true、POPUP=false；C 非 layered、D layered 保留 |
| RGB/透明采样 | 默认及软件 A–D 离屏红绿蓝均非零，B/D 半透明像素保留；只证明 WPF 内容可以生成，不证明屏幕显示 |
| 几何与遮挡 | C/D 与宿主 client 矩形有交集；三个命中点有浏览器遮挡线索，不能据此认定整个黑色矩形都是遮挡 |
| 会话 | 自动进程报告 RemoteSession=false、tier=2、RDP 加速配置 unset；用户远程会话单独记录，不能冒充已在相同 RDP 条件下完成视觉验证 |
| 命令/找回 | B 新进程透明样式、SoftwareOnly 新进程隔离、内容采样 ACK、新进程找回与退出通过；保留人工确认门槛 |
| 用户观察 | 增加可见可操作/黑屏/不可见记录按钮，附实际案例、模式、PID和偏好；当前新 A–D 视觉结果仍待用户反馈 |

前一轮自动矩阵曾同时显示普通对照组件，遮挡采样受到测试自身影响；已改为测试 C/D 前隐藏/关闭其他对照组件后复验。仍有外部浏览器覆盖采样点，未移动/关闭用户应用。没有抓取桌面截图或记录输入正文。

复现：`powershell -NoProfile -ExecutionPolicy Bypass -File .\experiments\DesktopHostProbe\Start-Probe.ps1 -Test`。报告为 `artifacts/p1.1-2-checks.txt`、`p1.1-2-software-checks.txt`、`p1.1-2-supervisor-checks.txt`，均忽略提交。运行日志在实验 bin 内；最终黑屏是否改善以人工结果为准，未宣称修复。

人工步骤：启动同一个 DesktopHostProbe.exe，先 A 输入/计数/确认后选 C；重建 A 找回，再 B 输入/计数/确认后选 D。记录各案例颜色、文字、输入是否可用；勾软件选项后重新创建 A/B，再比较 C/D。WorkerW 看请求/实际模式与失败原因，不能把普通回退算通过。详细解释和来源见 [研究报告](DESKTOP_MODE_RESEARCH.md)。不进入 P1.1-3。

## P1.1-2 修复：先普通窗口，再嵌入（2026-10-30）

**人工结论：初版不可见、验收失败；新版普通组件已由用户确认“能看到，输入和计数可用”。仅此三项通过，不等于桌面模式验收通过。** 本次只改实验和文档，不改主程序或其构建文件。

| 项目 | 当前证据 / 状态 |
|---|---|
| 进程与 HWND | 启动日志独立记录 BOOT / PROCESS_STARTED / HWND_CREATED；普通启动返回两个独立窗口标题，组件 parent=0 |
| WPF 初始化 | WPF_LOADED 和 WPF_CONTENT_RENDERED 分开报告；普通内容实测约 635×473 DIP，非零；启动阶段没有原生样式重写或嵌入 |
| 普通窗口可见、输入与计数 | **用户于本次对话确认通过**；非代理截图推测 |
| 视觉检查辅助 | NativeVisible、WpfVisible、最小化、DWM cloaked、矩形、DPI、三个点的 WindowFromPoint 命中线索；只用于排障，不等同屏幕像素证明 |
| 嵌入门槛 | 默认 normal；确认前 desktop/bottom 被拒绝，控制台入口禁用。组件内输入且计数后才能手动确认；自动测试不设置 HumanConfirmed |
| 命令协议 | SEND / RECEIVED / ACK 使用相同编号；测试验证成功应答、未知命令错误应答；超过时限显示未确认，不能伪报成功。超时分支尚未用挂死进程实测 |
| 新进程找回 | 自动检查确认 PID 更换、回到 normal、parent=0、ContentRendered=true、重新要求人工确认；不依赖旧 HWND 脱离成功 |
| 模式与失效 | 只在显式自动检查中测试桌面原生父子关系、置顶/脱离和模拟丢失；这些检查不能推翻之前的不可见反馈 |
| 普通窗口拖动、八方向缩放 | 使用 WPF 标准标题栏、边框和缩放角；**待用户人工验证** |
| 桌面嵌入后的可见、输入、找回及 Win+D | **仍待人工验证**；不标为已修复通过 |
| Explorer 重启、休眠、混合 DPI、Win10 | 未验证；不自动执行 Explorer 重启或系统设置变更 |

自动检查：`Start-Probe.ps1 -Test` 最终退出码 0，**20 PASS / 0 FAIL**（普通初始化、内容非空渲染、确认门槛及原生转换）、**7 PASS / 0 FAIL**（控制台协议、新进程找回、有限重启和正常退出）。Release 构建 0 警告、0 错误，无新增依赖。测试数量与上一版不同，因为改为优先检查可见测试平台的前提，而非继续累计原生样式断言。

`artifacts/probe-normal-content.png` 是 WPF 自身内容离屏渲染，确认有背景及文字，**不是桌面截图，不能证明当时用户看得见**。报告仍为 `artifacts/p1.1-2-checks.txt`、`artifacts/p1.1-2-supervisor-checks.txt`，本地控制台日志在实验 bin 下；均忽略提交。当前日志不记录输入正文。

电脑操作工具本次仍在首次、等待重试、重置后返回 native pipe unavailable（os error 2）。未用猜测坐标替代。可见性结论来源于用户明确反馈；进程标题和状态只作辅助证据。

复现：退出旧实验后，在项目根目录执行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\experiments\DesktopHostProbe\Start-Probe.ps1 -Test`。重新启动不加 `-Test`，或双击同目录 `bin/Release/net10.0-windows/DesktopHostProbe.exe`。

人工操作（不输入私人资料）：

1. 启动后应见控制台和浅色“普通窗口诊断组件”两个窗口；输入文字、点计数，再拖标题栏和四边四角。
2. 点组件内“我已看见并操作组件，允许桌面实验”，再点控制台“开始桌面嵌入”；单独观察是否仍可见、能否输入、能否被普通应用覆盖。
3. 消失时从任务栏返回控制台，点“重建普通窗口 / 找回”；应产生新 PID、新普通组件，测试文字清空。不要只看 visible=True。
4. 点“退出整个实验”检查两个进程结束。后续 Win+D、Explorer 重启/休眠/混合 DPI 按研究报告执行；完成后反馈，不进入 P1.1-3。

## 历史：P1.1-2 初版（2026-10-29，随后人工验收失败）

下列为旧版本执行记录；2026-10-30 用户报告组件不可见，旧的“找回”也无效。保留历史证据，不能将其作为当前可见性或整合通过的结论。

状态：**26 项原生窗口检查 + 4 项控制台恢复检查通过，0 FAIL；4 组未满足/待人工事项，不是完整桌面验收通过。** 主窗口没有整合或改变。方案、来源、限制与人工步骤见 [DESKTOP_MODE_RESEARCH](DESKTOP_MODE_RESEARCH.md)。

环境：Windows build 26200，.NET SDK 10.0.403，Windows 本机 PowerShell；两块显示器（包含负 X），实际 DPI 均为 144。没有修改系统缩放、注册表、Snap 或 Explorer 设置。实验与主项目 Release 构建均成功，0 编译警告/错误；无新增依赖。主项目只增加实验目录排除项，本阶段不重复运行 P1 的 91 项 GUI 检查。

| 验证项 | 本阶段结果及证据边界 |
|---|---|
| 桌面宿主 | 本机找到 Progman / SHELLDLL_DefView；真实 SetParent 后父 HWND 与目标相等，WS_CHILD 生效，非 TOPMOST |
| WorkerW 壁纸路径 | 未发现符合条件的现存 WorkerW，返回明确失败并安全回退；未验证成功嵌入 |
| Win+D 后可见 | **待人工**；native visible=true 不证明屏幕像素可见，不自动发送 Win+D |
| 普通应用覆盖 | 自动创建普通 WPF 对照窗口，重叠点 WindowFromPoint 命中该普通窗口；外部应用实际视觉覆盖仍待人工 |
| 点击、输入、拖动、八方向缩放 | 实验提供真实控件和输入事件，但**物理操作待人工**；测试中的 TextBox 赋值不算键盘/IME 测试 |
| 桌面 → 置顶 → 桌面 | 真实 HWND 父子/样式变换通过；物理矩形及内存测试文字保持；可视及焦点体验待人工 |
| 任务栏 / Alt+Tab | 组件 TOOLWINDOW、非 APPWINDOW、ShowInTaskbar=false 断言通过；实际 UI 待人工。控制台故意有正常入口 |
| 多显示器 | 桌面和回退模式分别在两块真实显示器原生定位通过；均为 144 DPI，不能代表混合 DPI 字体/命中/拖动通过 |
| 宿主失效回退 | 模拟检测异常后脱离为普通窗口通过，未破坏本次内存文字；这不是 Explorer 重启 |
| 子窗口销毁恢复 | 真的关闭实验子窗口（不动 Explorer），控制台自动重建一次回退；第二次丢失不循环重启；明确找回及正常退出不误重启，4 项通过 |
| 退出 | 自动检查组件正常退出及控制台正常关闭；实际按钮与任务栏找回体验待人工；未验证挂死强制清理分支 |
| 桌面图标 / 右键 | 自动确认原 DefView 仍存在、Explorer 身份不变；不能证明图标与菜单交互不受影响。组件区域内会遮住下方图标 |
| Explorer 重启 / 休眠恢复 | **待用户手动测试**；程序有检查与回退路径，未宣称实机通过 |
| Win10、其他 Win11 build、混合 DPI、长期运行 | **未验证** |

4 组未满足/待人工事项：①现存 WorkerW 缺失；②Win+D/任务栏/图标右键；③真实输入/拖动/缩放；④Explorer 重启/休眠/Win10/混合 DPI。不能用通过数量抵消这些核心缺口。

实际发现并修正的实验问题：

- 初次 SourceInitialized 捕获的窗口样式尚无 WS_VISIBLE，重新应用会隐藏组件；恢复可见位后 native visible 检查通过。
- 脱离后把已为零的原生 owner 再设零，会返回 1400；与 WPF 官方源码注释相符，改为仅在非零时清除。最早的 7 PASS/4 FAIL、8 PASS/3 FAIL 已被后续修正复验覆盖，不能当作最终成功记录。
- 控制台在 Closing 回调同步再次 Close 导致重入；改为事件结束后排队关闭，最终整套脚本退出码 0。只看四个恢复断言通过而忽略进程异常退出是不合格的。
- 测试显式关闭应用，并检查报告更新时间及退出码，防止提前退出却复用旧报告。

电脑交互验证阻碍：computer-use 的 `list_windows` 初次、等待重试及重置后重试均返回 `Computer Use native pipe is unavailable ... os error 2`。停止该交互路径，没有用猜测坐标或其他脚本替代；因此未提供或声称看过当前实验屏幕截图。

复现（先退出其他实验实例）：

```powershell
cd D:\projects\desktop-calendar
powershell -NoProfile -ExecutionPolicy Bypass -File .\experiments\DesktopHostProbe\Start-Probe.ps1 -Test
```

输出：`artifacts/p1.1-2-checks.txt`（26 PASS / 0 FAIL / 4 PENDING groups）、`artifacts/p1.1-2-supervisor-checks.txt`（4 PASS / 0 FAIL）、`artifacts/p1.1-2-trace.log`；控制台日志在实验 `bin/Release/net10.0-windows/probe-session.log`。均忽略提交，输入正文不记录。重跑需交互式 Windows 会话，不是无桌面的 CI 测试。

人工检查五组操作：[启动和人工验收](DESKTOP_MODE_RESEARCH.md#启动和人工验收)。先检查 Win+D 与普通覆盖，再输入/拖动/八方向、模式往返，最后由用户做 Explorer 重启及休眠/混合 DPI。每项记录通过/失败/未测；本阶段等待反馈，不进入 P1.1-3。

## P1.1-1：基线与文档（2026-10-29）

范围：仅 Git/忽略规则/README/PRD/DECISIONS/本记录；不改窗口代码，不进入 P1.1-2。下文 91 项结果属于历史 P1，本阶段不重新启动 GUI 或声称已修复人工问题。

已执行：14 个源码/构建文件的 SHA-256 与修改前完全一致；首次清单为 19 个项目文本文件；24 个敏感/产物路径应忽略、6 个源码/文档/未来 Logo 路径应保留的检查通过；19 个文件 UTF-8 严格解码及常见凭证特征扫描通过，未发现匹配。文件清单已人工审查，未纳入截图、个人 JSON、图片附件、备份或账户凭证。认证由用户通过 Git Credential Manager 浏览器流程完成。

检查过程中：PowerShell 管道传入 `check-ignore --stdin` 的数量断言失败，改为逐个路径参数检查后 30 个案例均通过；这是检查方式问题，不是忽略规则失效。首次 `git diff --cached --check` 发现 PRD 末尾多余空行，已清理并在提交前复查。不修改任何产品源码来消除文档检查问题。

复查命令：`git diff --cached --check` 检查提交空白；`git diff --cached --name-only` 查看首次清单；`git check-ignore -v -- bin/Release/test.exe artifacts/month.png assets/images/pasted.png planner-data.json .env.local` 检查排除规则。提交后用 `git ls-files` 检查同一清单，用 `git show --format= --check HEAD` 复查提交；用 `git ls-remote --heads origin` 比对两个远程分支与 `git rev-parse HEAD`。Git/文档检查不等同 GUI 回归，未新增或声称修复窗口功能。

人工检查步骤：

1. 阅读 README 的功能状态表及当前操作，对照现有原型：当前仍是模拟数据、旧紧凑标签、常驻托盘，桌面模式和时间轴缩放标为 Planned。
2. 阅读 PRD 第 6、11 节及 DECISIONS，核对新桌面定位、折叠/托盘规则和逐项审批；本阶段没有选择 WorkerW 方案。
3. 查看 `git status --short --branch`、`git log -1 --oneline` 及 GitHub 的 `main` / `dev/p1.1`；检查仓库没有 bin/obj、artifacts、快捷方式或个人数据。

## P1 人工反馈：未解决问题

| 问题 | 当前状态 / 后续验收 |
|---|---|
| 月历增高仍只显示一条待办 | 已确认现实现限制；待 P1.1-5 按实际高度及 0/1/3/10 条验证 |
| 左下、右上角匀速缩小明显抖动 | 用户实测；历史尺寸变化检查不证明平滑，待 P1.1-4 根因定位及八方向连续缩放 |
| 不同 DPI 显示器间拖动轻微抖动 | 用户反馈；此前两块同为 144 DPI，不能当作混合 DPI 通过 |
| 贴边未触发、容易触发 Windows Snap | 用户实测；内部指针事件测试不能代替真实拖动，待 P1.1-4 重现及修复 |
| 桌面层及 Explorer/休眠恢复 | 主程序尚未实现；P1.1-2 已提供独立实验及模拟回退检查，真实 Explorer/休眠恢复仍未验证，见本文件上方矩阵 |

本阶段不修复上述问题；保留历史测试证据，但不以“91 PASS”推翻人工反馈。

## P1 历史验证记录

日期：2026-10-29（Asia/Shanghai）。当时状态：**原型可运行，自动检查通过，等待用户视觉验收。** 用户随后完成测试并提出上面的缺陷与 P1.1 调整；未进入 P2。

## 环境与结果

- Windows x64 build 26200，PowerShell 5.1，.NET SDK 10.0.403 / Desktop Runtime 10.0.14。
- 两块 3840×2160 显示器；工作区均为 3840×2088，一块位于负 X 坐标。两块实际检测到的 DPI 均为 144（150%）。没有修改显示器缩放或其他系统设置。
- Release 构建：0 警告、0 错误。使用 SDK 自带框架包离线恢复，无第三方 NuGet 依赖。
- 最终自动检查：**91 PASS、0 FAIL**，详细结果在 `artifacts/p1-checks.txt`。不是 `dotnet test` 或第三方测试框架，而是同一可执行程序的 `--self-test` 入口。
- 默认窗口与 880×520 DIP 下的两页已渲染并逐图检查；小月历单元格改为数量提示，避免截出半行任务文字。原型背景默认 96%，可在界面调节。
- 一次交互运行采样：工作集约 52.9 MiB、私有内存约 343.5 MiB。该值不是长期内存泄漏或空闲 CPU 达标结论；本阶段没有制定或宣称性能阈值通过。

## 自动覆盖范围

1. 八方向缩放及最小尺寸；使用实际 Thumb 路由拖拽事件改变 HWND，防止仅几何函数通过但真实事件未生效。
2. 100% / 125% / 150% / 200% 的几何计算，负坐标屏幕、显示器移除回退；两块真实显示器上的定位、紧凑位置及展开矩形恢复。
3. 原生窗口无 Caption、Layered 标志、零边框；渲染图角像素 alpha=0、背景像素半透明。
4. 1240×760 与 880×520 的两页布局，栏最小宽度及总宽度，100% / 125% / 150% / 200% 栅格渲染。
5. 紧凑、贴边、托盘之间转换和恢复；置顶开启/关闭原生标志；托盘图标对象及菜单显示回调。
6. 左右贴边的内部指针输入与计时：鼠标在窗口内不收起；短暂离开不闪烁；延时后收起；MouseEnter 事件展开。

**栅格渲染缩放不能代替实际 Windows DPI 切换；内部指针/悬停事件测试不能代替真实鼠标悬停。**

## 已执行的电脑交互检查

使用 computer-use 技能在实际桌面窗口操作，未修改系统设置：

- 两页切换及展开内容显示；在浅色和深色应用背景前检查圆角透明、半透明表面及无系统白框。
- 四边及四角均已实际拖动并观察尺寸变化：右边 1240→1120，下边 760→720，左边 1120→1080，上边 720→690；四个角随后分别改变两个维度。
- 第一页竖向分隔线拖动有效，月历与待办栏宽随之改变。
- “—”进入仅有“待办”的紧凑标签；点击标签恢复完整窗口。
- 置顶复选框可操作；置顶的原生标志由自动检查验证。
- 过程中用户误按 Esc 停止电脑控制；按用户后续授权重试后恢复窗口并继续验证。检测到用户实时输入时，按工具要求重新获取窗口状态，未使用旧坐标继续操作。

## 尚待人工验收

- Windows 10 实机；与当前 150% 不同的系统缩放；不同 DPI 显示器之间的真实拖拽、拔插屏幕。
- 顶部拖动完整体验、紧凑标签 Enter / Space 恢复。
- 系统托盘的实际双击、右键显示、右键退出，以及图标位于隐藏图标区时的易发现性。
- 真实鼠标靠近左右边缘、离开后收起、悬停展开；置顶打开/关闭下与托盘、紧凑模式的组合体验。
- 长时间空闲、反复拖拽的资源走势；未宣称无内存泄漏或已通过性能验收。

## 用户验收：五组操作

1. **外观与缩放**：双击根目录 `Desktop Calendar P1.lnk`。把窗口放在浅/深背景前，拖四边和四角直到最小尺寸，再放大；应无白框、无跨栏覆盖，圆角外露出桌面。小格子显示 `+N`，悬停可读示例任务。顶部空白区域应能拖动窗口。
2. **两页布局**：切换“月历与今日 / 一周回顾”，拖动分隔线；滚动时间轴和周总结。左月历默认更宽，周视图七天保持并列。示例数据只读，无真实新增、完成或保存行为。
3. **紧凑恢复**：先把窗口移到另一显示器，记下大小与位置；点“—”，标签应位于该屏工作区右上角。分别点击、按 Enter、按 Space 恢复，页面、位置和尺寸应保留。
4. **托盘**：点“×”，窗口隐藏但进程仍在；打开系统托盘（必要时“显示隐藏的图标”），找到提示为 `Desktop Calendar` 的图标。双击恢复，再分别检查右键“显示”和“退出”；退出应移除图标并结束进程。
5. **置顶与贴边**：开启置顶，再切换到其他普通窗口确认层级；勾选贴边，拖到左/右边缘，鼠标移开约 0.9 秒；应只剩把手。悬停展开，期间反复进入紧凑、托盘并恢复，不应丢失窗口或恢复成狭窄布局。

任何一项不符，请记录：Windows 版本、缩放比例、所在显示器、操作顺序、窗口状态。P1 不保存真实数据，关闭原型不会删除个人记录。

## 复现命令与产物

先从托盘退出原型，再在 Windows PowerShell 中执行：

```powershell
cd D:\projects\desktop-calendar
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

产物：`artifacts/p1-checks.txt`、`month-1240.png`、`month-880.png`、`week-1240.png`、`week-880.png`、其他 DPI 栅格图。图片由应用自身渲染，只含模拟内容，不保存用户桌面截图。所有测试产物已忽略提交。

已修复的检查问题：关闭按钮因内边距被截断；默认浅色滚动条；小月历格子文字裁切；拖拽时轮询系统鼠标导致与事件位移不一致；隐藏启动造成原生置顶断言失真。

P1 未实现：真实日历/任务/日程逻辑、农历节气/假日、完整英语、备份恢复、持久化、开机自启动和安装包。均保留在 PRD 后续交付范围内。
