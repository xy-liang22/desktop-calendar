# 阶段验证记录

## 2026-11-04：仓库整理与 main 基线准备

### 变更与安全范围

本轮不改业务逻辑、schema或桌面owner。根目录辅助C#文件原样归到App，同程序集检查原样归到tests/WpfChecks，XAML/项目入口仍在根目录；停止的DesktopHostProbe实验源码由Git 310a87b保留。旧研究和P1–P9过程移到history；README只列当前入口。所有默认构建/测试/发布配置统一Release，启动脚本改名Start-DesktopCalendar.ps1，生成的开发快捷方式改为Desktop Calendar.lnk。

清理217处旧生成目标约318.46MiB，另清除实验残留约2.31MiB；后续测试生成物不计作原有清理量。重新生成后，bin/obj/artifacts及Core/测试输出合计约18.84MiB，其中仍有用户运行的bin/InteractionFix（PID45220），脚本主动保留、不结束进程。此目录不在Git中；正常退出后重跑清理即可移除。默认只保留Release、当前发布包、最新已知报告；未知artifacts内容保留，拒绝跟踪文件和重解析路径。个人数据和用户级安装中的历史版本未清理。

### 本轮实际执行

收尾补充：用户确认从托盘退出后，代理核实没有DesktopCalendar进程，执行同一清理脚本，移除原先保留的bin/InteractionFix。**PASS**：bin仅剩Release，Release EXE清理前后SHA256一致；未改个人数据或main，无需重跑功能回归。上文PID45220及目录保留描述为清理前状态。

| 验证 | 结果 | 当前证据（均为忽略产物） |
|---|---|---|
| 离线Release重建 | **PASS，0警告/0错误** | bin/Release/net10.0-windows |
| 窗口/owner/托盘/缩放/恢复 | **374 PASS / 0 FAIL** | artifacts/p1-checks.txt，正常Windows桌面会话 |
| 完整数据/可靠性/类型转换 | **330 PASS / 0 FAIL / 1 NOT VERIFIED** | artifacts/checks/data/run-f0f977b20f3c49b0b9c7121a951cd3ac/checks.txt |
| WPF控件/编辑器/独立重启/损坏保护 | **392 PASS / 0 FAIL** | artifacts/checks/wpf/run-85a65be7f5374b1783b45640fa92fb42 |
| 清理的预览/源文件/活动程序/未知内容保护 | **7 PASS / 0 FAIL** | artifacts/checks/workspace/checks.txt：实际创建可丢弃产物，WhatIf保留，执行后删除，未知文件/Release/包保留，源码及XAML SHA256不变 |
| 隔离安装/更新/卸载 | **18 PASS / 0 FAIL** | artifacts/install-checks.txt；checks/install/run-dd4162ed8bcf422faed664766252e218 |
| 开发/包/实际安装/开始菜单 | **9 PASS / 0 FAIL** | artifacts/entry-checks-d6e365747ad6400497f189a211cfd33b |

数据层和数据测试源码本轮没有改动；所有13项ConversionChecks均执行通过。前次文档343项汇总本轮未复现，以本轮完整程序和checks.txt的实际330项为准，不将旧汇总复制为新证据。历史Samoa2011时区资料不足仍为NOT VERIFIED，不修改系统时区资料。

### 失败与复验

- 首次重建出现9个重复程序集特性错误：主项目取消旧实验排除项后，未清掉的experiments/DesktopHostProbe/obj被默认源码扫描。确认该目录没有跟踪文件、运行进程及重解析点后删除残留，重新构建和全部回归通过；没有禁用程序集特性或跳过测试。
- 首次在受限执行会话跑隔离安装得到17 PASS/1 FAIL（已安装组件桌面HWND烟测）；改在正常桌面会话执行原测试为18/0。未修改或删除断言。失败与会话条件相关，不能当首次通过；失败当轮汇总已记录，原细分JSON在成功轮按最新报告策略清理，不能据此断言唯一系统原因。
- 本轮没有重跑3分钟监测或物理GUI交互，因为功能逻辑未改变；上一轮结果在下节，不能冒充本轮新执行。电脑控制管道不可用的限制、Win10/混合DPI/Explorer实际重启/休眠/隔夜未验状态继续保留，无需重复人工验收已确认的桌面行为。

发布包仍为0.8.2，已更新既有当前用户安装至versions/v48826e591d2499c2，开始菜单目标/图标与Release五个运行文件SHA256一致；安装版只以隔离生命周期数据烟测，不读真实planner数据，不新建启动项或结束旧实例。旧实例仍须正常退出后才会使用新入口版本。

整理前已fetch origin，main是开发分支祖先（0落后、29领先），可快进，无须强行合并；待整理提交后复核。没有发现本轮阻碍main基线准备的失败，兼容性限制不等于全平台发布认证。main实际合并/推送仍等待明确批准，不创建Release、不改仓库可见性。历史原始artifacts已按本次授权清理，下节及归档中的旧路径仅为当时记录，当前证据以本节为准。

## 2026-11-04：0.8.2 连续交互修订

用户在a821d46之后连续授权类型互转、时间标点、完成子任务折叠、可编辑显示快捷键、彩色emoji、临时置顶边缘把手、固定周区间、字号和月历增亮。仍在dev/p7-p8，不是正式Release。数据schema3/偏好schema2兼容，未使用私人数据、安装新依赖或改全局设置。

| 验证 | 结果 | 证据 |
|---|---|---|
| 数据/可靠性及新类型转换 | **343 PASS / 0 FAIL / 1 NOT VERIFIED** | artifacts/interaction-data-final2/checks.txt；未验仍为历史Samoa2011时区资料 |
| 最终控件/真实WPF Window/独立重启 | **392 PASS / 0 FAIL** | artifacts/p3-checks/run-3a6478769e314246ad40a81322d80f57 |
| 窗口/owner/托盘/缩放/恢复回归 | **374 PASS / 0 FAIL** | artifacts/p1-checks.txt；0.8.2，正常用户桌面会话 |
| 3分钟字体/语言/视图资源监测 | **4 PASS / 0 FAIL** | artifacts/quality-04e2165678ce43f38057925e5095cc36；182.31秒，空闲CPU0.82%单逻辑核心，私有内存增12.36MiB，句柄减少7 |
| 隔离安装/更新/卸载/保护 | **18 PASS / 0 FAIL** | artifacts/install-checks.txt，0.8.2候选包 |
| 开发/发布包/实际安装/开始菜单 | **9 PASS / 0 FAIL** | artifacts/entry-checks-428b358c5043458d80fdd2f13c09ce63 |
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

结论：上述已执行范围通过；暂不进行正式发布/合并main。代码bf6fc80后已发布构建0.8.2（artifacts/publish-8f35d260b24c4026aa84e5c0e8d1c059），隔离安装18/0后更新当前用户Programs/DesktopCalendar及开始菜单，保留旧实例/旧版本。开发/包/安装5个运行文件SHA256一致，实际安装版用隔离生命周期目录烟测9/0，不读私人planner数据、不新增启动项。仍需用户退出旧实例后从开始菜单启动，未创建正式Release、未合并main。



历史阶段记录见 [P1–P9验证归档](history/TESTING_P1_P9.md)，桌面路线调查见 [研究归档](history/DESKTOP_MODE_RESEARCH.md)。
