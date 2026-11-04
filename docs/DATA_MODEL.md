# 数据与服务接口：P2基础与P3–P8集成

状态：应用与Core为0.8.2，真实待办、周期、Activity与P6周目标/统计/总结界面已接入。纯C# Core使用.NET10标准库，无WPF/数据库/第三方依赖；仅用户主动的历法更新入口可联网，业务schema3不变。图片/备注呈现及偏好位于WPF层，实体/JSON仍不含WPF对象；P7/P8已授权并接入，见末节。

## 实体和身份

P3.2：新用户五分类工作/家务/个人/家庭/健康，旧分类不自动迁移或重分类；仅用户明确初始化补齐缺失内置键。NewTodo子草稿捕获父分类/覆盖色，此后独立；不是Core每次Save自动继承。UI换行模式在独立窗口偏好，业务schema仍3。

- `PlannerState` 是不可变快照，文档 ID、递增 Revision 和实体数组；`Entity` 含 GUID 与 UTC 创建/修改时间。分类共享，归档保留历史引用。排序键 decimal，完成状态仅决定分组，不改基础键。
- `TodoOrdering` 记录根组的日期或子组的父ID，以及Manual/PlannedTime模式；默认时间优先，手动混排模式和键同时存JSON，重启不悄悄切回时间排序。`SetTodoOrderMode`恢复时间排序时仍保留原手动键。`DayDecoration`记录每天手选底色，单日期唯一；真实历法/假日由独立知识模块提供，不写个人payload。
- `Todo`：一次性、持续或物化周期实例；计划日期DateOnly、可选TimeOnly、优先级/描述/分类/颜色、父节点、进度、完成时间。`ProgressMode.Default`递归平均直接子分支，叶用ManualProgress；显式Manual不派生进度。完成来源规则如下，不依据“进度100%”直接决定完成。
- 周期使用 `TodoSeries -> RuleVersion -> Templates`。模板树按版本保存；实例身份是系列 ID＋节点 ID＋**原始出现日期**的稳定 SHA256 派生 GUID，不受系统文化、移动日期或重启影响。不预生成无限任务；查询按日期生成，只有修改/完成/建立关联时物化，并物化祖先。删除节点保留RetiredNodeIds墓碑，保护已完成实例的身份有效性。
- `RecurrenceRule`：日/周/月间隔、指定星期或月日、周首日、Count/Until、排除和追加日期、时区。每月31日遇短月跳过，不钳为月末；COUNT计算排除前的名义出现次数，追加日另计。规则和有效日期版本分离。
- `Activity`：真实时间记录，含多个各自有 ID 的 `ActivityPeriod`、emoji/摘要/reaction、文本或 Markdown 备注、可选 TodoRef、分类与图片资产引用。删除 Todo 保留 Activity 并保留旧标题；Activity 不修改 Todo 完成状态。
- `WeekGoal`：周身份、数量/秒数/勾选目标、单一自动来源、两种导航关联列表、独立手动覆盖。类型/来源变更必须显式确认。跨周关联 Todo 不静默丢弃，数量按显式关联完成项计，查询同时返回 `OutsideWeekTodos`，供后续 UI 警告。
- `WeeklyReview`：手写 Markdown、自定义小节，与独立自动摘要快照。`SaveReview` 不能覆盖统计；`AppendStatistics` 不能覆盖正文。
- `LocalAsset`：ID、受限 `assets/images/<文件名>` 路径、MIME、长度、SHA256；`PlannerQueries.AssetReference/ResolveAsset`提供`asset://<GUID>`引用，只解析注册信息，拒绝file/network/路径后缀。P5 LocalImages进一步支持已注册相对路径的校验/导入/读取，Note.AssetIds记录引用。P8已实现完整ZIP；孤儿自动清理仍不启用。

## 时间与统计

计划日期/时间是当地墙钟值；Activity 起止为带原始偏移 DateTimeOffset，另存 TimeZoneId，比较/裁剪/合并按绝对时刻，派生区间归一UTC。`PlannerTime.FromLocal` 拒绝 DST 空洞，对重复小时要求显式偏移并保留选择。所有区间 `[start,end)`，零长/反向拒绝；按当地日/周边界裁剪，23/25小时日及 DST 周使用真实秒数。

同一 Activity、单个时长目标均做区间并集；分类分别合并同分类区间，不同分类允许各自贡献，比例分母是该周实际秒数。时长自动来源禁止 Todo。周身份保存首日和时区，后改偏好不会移动旧周总结。

持续任务共享 ID/完成状态，未完成时按查询 asOf 向后展示；显式顺延保留移动记录。完成冻结最后有效日期：历史模式保留原始日至冻结日的所有划线展示，仅最新模式只留冻结日。取消完成重新延展，不重建副本。

## 事务接口

```csharp
var store = new PlannerStore(); // 或注入测试/自定义路径
var workspace = await PlannerWorkspace.OpenAsync(store,
    new PlannerOptions { TimeZoneId = TimeZoneInfo.Local.Id,
                         WeekStart = DayOfWeek.Sunday });
if (workspace.Storage.CanWrite) {
    var result = await workspace.ExecuteAsync(new SaveTodo(new Todo {
        Title = "示例", PlannedDate = new DateOnly(2026, 11, 2)
    })); // 返回成功状态和新 ID；失败不能当保存成功
}
```

`SaveTodo/SetTodoCompleted/MoveCarryTodo/ReorderTodos/SetTodoOrderMode/DeleteTodo`、`CreateSeries/EditSeries/CancelSeries`、`SaveActivity/DeleteActivity`、`SaveGoal/DeleteGoal`、`SaveReview/AppendStatistics/DeleteReview`、分类初始化/归档、资产和日期底色命令均经过整图校验、可靠保存，再发布新快照。`BatchCommand`将多个合法步骤一次提交/撤销（最多1000项、嵌套16层）；任一步失败不发布部分状态。UI 应调用命令，不直接修改集合。查询入口为 `PlannerQueries` 和 `PlannerTime`；`TodosOn`仅实际当日项，`TodoTreeOn`额外标记祖先上下文，避免已完成子任务因父取消而无法导航。创建模板树须事先分配节点 GUID 以表达父子关系。

`CopyTodo(TodoRef, TargetDate)`复制活动普通、持续或某次周期子树为独立普通子树：新GUID/CopiedFromId，保留属性、相对日期和子组排序；重置系列/Origin/完成时间、来源、快照、失效标识、顺延字段和手动进度，不复制Activity或周目标链接，单事务可撤销。取消节点不复制，其活动后代提升到最近被复制祖先下。P3.1新增`RenameTodo(TodoRef, Title, ExpectedTitle)`，只改标题，空值/陈旧标题拒绝，避免内联覆盖高级字段；P4内联草稿使用TodoRef（含原始发生键）和展示日期，持续同ID不会在多格同时出现输入框。

## P3.1 完成事务与迁移

`CompletionIntent`：Automatic按非取消直接子项全部完成而自然完成（叶默认未完成）；ManualComplete显式完成；ManualIncomplete显式保持未完成；CascadeComplete由祖先强制完成。取消自然完成不反向取消孩子，100%但未完成是合法覆盖状态。独立后代完成/进度/结构变化释放覆盖，文字/颜色/排序不释放。

`SetTodoCompleted(true)`物化所需实例及祖先，保存`CompletionSnapshot(Id, CapturedAt, Guard, Entries)`后级联活动后代。Entries保存原状态/来源/完成时间/持续冻结日。Guard为按ID排序的子树身份/父子关系/完成/进度字段SHA256，不含文字/颜色/排序/ModifiedAt。取消时校验Guard，仅恢复完成字段，保留元数据；独立子树变化使快照失效，不盲目恢复，标记CompletionSnapshotInvalidated。嵌套快照暂存；结构删除/提升释放孤立Cascade来源。所有归一、排序分组、快照与Undo/Redo在同一保存事务内完成；无UI状态循环。

schema3严格校验来源、快照作用域、Guard与关联。v2先验旧校验和，再将旧完成项映射ManualComplete（无虚构快照）、旧未完成映射Automatic；旧Manual/0进度改Default，非零Manual和Children保留。按新规则归一旧父状态，稳定ID/Revision/其他实体不变。读取迁移不写磁盘，下一成功修改才保存3并备份原字节；仅认识v2的旧程序必须拒绝3而非降级。P3.2新默认五分类见上文；历史重要/紧急分类不删除、不转优先级。只在UI新建选择中过滤标准历史默认分类，改名/自定义不变。

## P3界面边界

`DesktopSession`持有一个`PlannerController`，重建HWND也复用；月历、中栏和今日折叠只读取此工作区快照。编辑器使用独立草稿，旧记录发生变化则拒绝覆盖。保存按钮以BatchCommand提交字段与完成操作，保存成功才刷新记录；失败旧快照/草稿保留，状态在完整/折叠均可见。加载未知/损坏版本只读，不初始化空业务数据；新文件只初始化分类，不填DemoTodo。

同级拖放仅同父/同状态组；完成不改基础键。月历默认根、显式展开子项，P3.2改为深度优先紧随父项，测量文字及有限空间，+N不含折叠项。`QuickDraft`和`InlineTitleDraft`仅UI未提交草稿，业务仍单一Workspace。完整编辑外部点击/×/Esc验证保存，明确取消放弃；无修改不写入，失败保留草稿。内联Enter/有效失焦保存、Esc取消，使用期望旧标题命令。正常退出先处理编辑器/内联，再确认放弃快速草稿并等待提交；强制终止未提交文字不保证恢复。

日期/月/分类文字或底色偏好仅新增到原window-preferences schema2，未把UI状态写入业务文件。根项目排除Core/tests的递归Compile仍保留，避免把独立库重复编译；窗口显式fixture回归才用DemoTodo，普通运行不使用它。P4已沿用同一controller/workspace；P5/P6后续也应如此，不另造JSON仓库。

单次周期修改不改规则，可用 MoveTo 改日期；FromDate/FutureUnfinished 都以命令From为界分割未来规则版本，整系列更新各版本；各范围均保护已物化完成记录。取消本次记录取消键，未来用 SuppressedFrom，整系列归档；CancelSeries的可选NodeId限定子树。已完成记录及所需祖先保留，不能因范围取消删除已完成历史。已保存实例保留自己的树/移动日期例外，尚未生成的未来实例使用新模板。撤销/重做保存最多100个会话内事务快照，每次仍提交新 Revision；实体历史字段按事务快照恢复；重载清空历史，不做跨进程永久撤销日志。

## P4 服务增量与UI调用约定

- `CreateSeries(Rule, Templates, SeriesId = null)`可预分配系列GUID，空GUID拒绝。`Recurrence.Next(rule, from)`按日/周/月周期跳转寻找单个有效日，支持COUNT/UNTIL/排除与追加日，不无限预生成。每月不存在的日跳过；实例的TimeOnly仍为当地计划墙钟，DST重复小时不会生成两个Todo，Activity绝对区间语义不变。
- `EditOccurrence(Value, Scope)`：Single走SaveTodo，只改变对应发生；FromDate/EntireSeries映射选中节点的可编辑字段至版本模板，禁止把单次移动日期当系列移动。范围按Origin.Date而非PlannedDate；UI默认Single，FromDate含原发生日，EntireSeries应用所有未完成而保留已完成。
- `EditSeries`只将模板前后实际变化的字段应用于未完成已保存实例；Rule-only保留所有单次元数据、原始键和日期。已保存树关系保留，未生成实例使用新树。新增模板在已完成历史分支用现有Cancelled键作有限排除，避免补入新的未完成子项。已完成子树的必要祖先保留为取消上下文；旧合法普通子项也参与保护/清理，无孤立ParentId。
- `AddSeriesTodo(Value, Parent, Scope)`：Single等价`AddOccurrenceTodo`；其他范围将新节点加入父模板。`AddOccurrenceTodo`的Value.Id作为一次性节点身份，真正实体ID是系列+该节点ID+父的原发生日哈希；节点注册到既有RetiredNodeIds（非活动模板身份注册表），不出现在未来模板，后续只支持单次作用域。无新增DTO字段，严格校验、完成快照、Undo/Redo仍走原事务路径。
- `CancelSeries`复用Single/FromDate/EntireSeries，保留完成节点/祖先与关联安全处理。UI拒绝单次取消仍完成的目标，提示先取消完成；范围取消保留完成历史并停止相应未来，不将历史抹除。取消后的上下文只读。
- `SetCarryMode(TodoRef, KeepHistory)`仅切换两种持续展示类型，不换ID、不抹Moves/冻结日。`MoveCarryTodo`显式前移到更晚日，自动延续仍通过TodosOn的asOf参数而非每天写文件；完成冻结原有显示、取消再延续。
- `ReorderTodos(Siblings, DisplayDate = null, AsOf = null)`调用方应传实际Today；查询未来视图时不能把DisplayDate误用为asOf，造成未显示的持续兄弟进入排序集合。保留时间/手动模式及完成分组；虚拟实例仅在需要持久修改时物化。
- UI `RecurrenceFields`共用验证/字段，`PlannerRecurrenceDialogs`提供规则/取消/顺延，重要确认仍模态。普通编辑Window用受控外部保存；编辑器提交期间禁用整窗，完成意图按类型/Scope判断，不依赖暂时的IsEnabled。

P4继续编码schema3，无读写迁移。测试中实际引用旧`bin/P31/.../DesktopCalendar.Core.dll`（0.3.1）解码P4混合图，再Encode与原字节一致；这证明格式/既有校验器兼容，不表示旧GUI具有P4管理入口。P4当时未提供类型转换；0.8.2增补显式ConvertTodo，见末节。复制仍生成普通树，新增时明确选类型。界面支持单次/系列不同作用域，不能从UI直接改底层数组。

## P5显示查询、编辑与图片边界

- 仍用`SaveActivity/DeleteActivity`和原`PlannerWorkspace`。界面保存多字段/资产登记/周目标关联采用同一`BatchCommand`，成功后才发布状态；TodoRef允许指向未完成或周期实例，一Todo可有多个Activity，保存实际时间不完成Todo。周关联保存在`WeekGoal.Activities`，没有新增冗余Activity.GoalIds。复制用新Activity和Period GUID，备注共享合法资产，整条删除只清周关联、不删Todo/文件。
- `ActivityTimeline.On(state,date,zoneId)`返回只读`ActivitySegment(ActivityId,PeriodId,Date,Start,End,StartHour,EndHour,Offset)`。用PlannerTime.Day/Clip裁原区间，再按时区偏移转折分片；同Activity多段不在显示查询中合并，以保留独立编辑身份。统计仍调用P2 Merge/Seconds，不重复计时。DST回拨小时可以两个并列片，显示偏移；本地结束钟早于开始但绝对时长正的区间也不会消失。跳时留墙钟空白。
- `ActivityTimeline.Local`调用原FromLocal，空洞拒绝、重复小时没有明确offset则拒绝。`Transform(period,wallDelta,displayZone,"move"/"start"/"end")`：移动按显示墙钟确定新起点、保持真实持续长度；边缘只改一个端点，反向拒绝，ID不变。存储仍原ActivityPeriod，不保存裁片。UI时间字段未改时保留原始DateTimeOffset及亚秒，不用格式化文本重建。
- `ActivityTimelineView`绑定Controller后覆盖所有示例块，跨页使用同一快照/日期，周首日读Options；Slot.Segment仅显示DTO。鼠标预览不保存，成功松开一次SaveActivity即可Undo；创建则打开预填草稿，取消不写。默认5分钟/Shift1分钟；创建取整到完整分钟，不将鼠标小数坐标写入秒数。失去捕获/外部松开/Esc/右键取消；缩放先取消活动手势。保存失败保留待重试预览，陈旧快照拒绝覆盖。
- `ActivityDialogs`复用Sheet外部关闭语义、IME保护、分类色和原工作区。PeriodFields提供ISO日期时间、时区及每端offset，草稿移除一段与整条删除清楚区分；最后一段保留，整条必须单独确认。`Sheet.HoldOpen`只用于自有文件选择器等附属交互，避免Deactivated自动提交；重要确认仍模态。
- `LocalImages`以实际业务文件目录为根，剪贴板BitmapSource/本地图片先校验20MiB、8192边、2400万像素，再PNG编码/验证。SHA256文件名，已有同字节文件/资产复用；拒绝危险相对路径/祖先重解析点，读图核对登记长度与哈希。写入同目录唯一pending、Flush(true)、解码验证后Move；JSON提交前文件必须可校验，失败不发布新引用。Notes.AssetIds和新SaveAsset一起事务保存；相对`assets/images/<hash>.png`与既有`asset://ID`都可解析，不自动读取任意file:/远程图片。
- 文件与JSON不假装跨文件系统事务：图片先落盘，失败或取消可能留下未登记文件；这是安全孤儿，暂不删除。已登记但当前无引用、备份或Undo中仍使用的图片也保留，避免损害恢复。Undo登记后文件仍在，可Redo；多备注引用不因一条删除丢图。P8的ZIP携带全部图片，暂不自动清理。
- `MarkdownNotes`渲染FlowDocument安全子集，纯文本模式不解释语法，无HTML执行/WebView/远程下载；链接只复制地址。Markdown支持标题、强调、列表/引用、围栏代码、简单表格、链接及本地图片。实时预览合并220ms，超过100000字有明确截断提示，源文不截断；不声称完整CommonMark兼容。图片缺失/损坏/不安全引用显示占位。

新增界面文字集中在Strings.P5.zh-CN.xaml；emoji/reaction可自定义，标准ComboBox可编辑模板补齐，非编辑分类选择不变。Core/存储DTO没有增加字段，旧schema3格式无需迁移；图片资源能力的增加不意味着旧GUI具备Markdown或图片管理。

## P6目标、总结与运行偏好

- `WeekGoalDialogs`编辑原WeekGoal三类型/来源/目标/手动值。UI时长输入小时，保存乘3600，勾选Target=1且Override仅null/0/1；类型/来源变更以`SaveGoal(...,ConfirmTypeChange:true)`明确确认。`GoalLinks`从原`Todos/Activities`读取关联，按父事项保存批事务增删引用，不另建映射表。新目标可独立先保存，父事项取消后目标仍在；周期引用是TodoRef.For(OccurrenceKey)，从新系列首有效发生建立，不关联模板。
- `WeekSummaryView`统一调用`PlannerQueries.Goal/CategoryStatistics`；目标时长对关联区间做并集，分类各自并集并裁WeekRange，类别之间可重叠；全局并集另列。百分比基准为真实周秒数，DST不假定168h。跨周显式Todo返回OutsideWeekTodos，UI警告，P2原计数规则不变。自动勾选是来源至少一项满足；无来源用Checked，覆盖值有优先级。
- `WeeklyReviewDialog`独立草稿，SaveReview继续禁止覆盖Statistics。用户插入摘要时暂存生成Markdown，保存以Batch(SaveReview, SaveGoal补ReviewId, AppendStatistics...)一次提交/撤销，取消无写；旧快照不随统计刷新。正文、ReviewSection和StatisticsSnapshot分别保存，图片复用同目录/资产登记。已有同周总结被新目标引用，首次总结为同周目标补空ReviewId。
- `PlannerWorkspace.ConfigureAsync(options)`校验后仅更新运行Snapshot.Options，保持Fingerprint/Revision、业务字节及Undo/Redo；Commit仍保持当前Options。WindowPreferences兼容新增WeekStart（默认Monday）、WeekPlanLayout（默认SideColumn），不进业务JSON；初始化/重载/恢复均传当前时区/首日。历史WeekKey从不随设置变化搬迁。
- 月历MonthDates可传任意首日，日期与七天轴/周侧栏共用Controller.Week。MonthWeekPlan按原WeekKey查询，窄栏/摘要条仅布局偏好，空间不足可响应式退到摘要条；浮动展开不撑高日期行。所有UI仍一个Controller/Workspace。
- P5.1详情改为根Surface上的唯一Canvas卡片，无独立详情窗口；定位键包含Activity/Period/裁片日期与偏移。Markdown图片自适应容器，ImageViewer只接收LocalImages验证后的Bitmap；Owned Window/HoldOpen与父滚动还原保护草稿，不触发额外保存或网络读取。

## JSON 与恢复

默认 `%LOCALAPPDATA%\DesktopCalendar\planner-data.json`；窗口偏好仍是独立 `window-preferences.json`，兼容旧schema2。`PlannerOptions`作为调用上下文传入，不写入业务JSON。P3正常启动读取真实业务文件；只有主/备份/残留都不存在才允许首次初始化，现有P2其他实体与关联保留。

业务schema3：`schemaVersion/documentId/revision/payload/payloadSha256`，DTO不存WPF对象，严格字段/枚举/重复键/关联检查；未知同版本字段拒绝。v1为早期迁移合同（documentId/data，无revision/校验和），不是P1.1真实业务文件；兼容v1/v2读取迁移，下一次明确成功保存才写3并备份旧文件。

同目录唯一临时文件，完整写入、Flush(true)、重新解码校验、File.Replace 保留 `.bak`；首次创建用同卷 Move。每个工作区串行队列＋跨进程排他锁文件＋文件指纹/Revision，防晚写覆盖和旧快照。旧锁文件不是持锁，进程退出由系统释放句柄。写入失败返回诊断；提交后异常先核实实际结果，不盲目重试。

损坏主文件只读阻断，绝不静默回空；`.bak` 与 `.pending-*` 提供经过验证的恢复候选，须显式 `RecoverAsync(candidate, confirmed:true)`，保留原件为 `.evidence-*`。存在更新 schema 的主文件或候选时禁止降级。完整主文件＋遗留临时文件仍用主文件，不擅自提升未提交事务；缺失主文件但有残留则阻断空初始化。恢复候选指纹也校验，避免恢复期间被替换。64MiB限额，错误码不含备注正文；SHA256用于完整性而非加密或身份认证。

不自动删图片/证据/备份。单个 JSON 不包含图片，后续完整备份必须包含整个资源目录。文件锁、I/O错误、权限错误、取消、新版schema和冲突均可诊断；冲突后调用 ReloadAsync，不直接强行覆盖。

## 验证

Windows PowerShell：`powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-Planner.ps1`，330 PASS/0 FAIL/1 NOT VERIFIED；`Test-P3.ps1 -Configuration P8`验证331项P3–P9控件、事务和独立进程重启。只用忽略的artifacts，不使用个人文件。测试包括原子写入故障/并发、周期保护、DST片、日程/图片以及目标来源/覆盖/类型、区间统计、运行选项不改磁盘、总结快照与文字分离及重启。Samoa2011整日跳跃本机资料未重现，物理断电/满盘、Win10及真实输入设备仍未冒称通过，详见TESTING。

## P7 / P8 集成边界（0.8.0）

- 业务实体/JSON schema仍3，窗口偏好仍2追加UiLanguage、BackgroundColor/AccentColor/SaturdayColor/SundayColor、FixedSixRows/FixedWeekOffset。P7语言重建仅替换主视图控件，复用Controller/Workspace；用户正文不翻译，未改标准内置分类只显示本地化名称。农历使用.NET已装框架，节气/假期为独立带来源年份知识包，不写个人payload。
- `PlannerWorkspace.MaintenanceAsync`和`PlannerStore.MaintenanceAsync`使用已有工作区gate、store gate和原跨进程lease，并检查当前文件指纹。Controller暴露维护任务到Pending，UI暂停偏好计时保存，维护期间业务只读；不是第二个JSON来源。恢复后重新读取、清空会话Undo、按偏好重建组件。
- `PlannerBackup.Create/Inspect/Restore/RecoverInterrupted`详见BACKUP：ZIP清单schema1记录实际业务schema、长度/hash；白名单相对路径/限额/图与资产验证；原始恢复点、刷盘journal、JSON最后替换、失败回滚。待恢复journal或启动恢复受阻时不允许空初始化、业务写入、普通重新读取绕过或偏好写盘。
- ZIP带全部完成写入的本地图片，包括当前孤儿；不删除旧安全图片/恢复点。manifest哈希不是签名或加密。恢复不建立系统Startup入口；入口属本机用户主动选择，更新脚本只跟随原已启用项。
- 自启动不写注册表，托盘/单实例/Explorer未就绪安全回退继续沿用旧模块，未更换owner技术方案。
P9仅在展示层简写农历/过滤通用Sunday，Core.On、完整Lunar、官方包、JSON/关联/统计均未更改；候选包与用户级入口版本一致性另见TESTING。

## 0.8.2 现有待办类型转换与显示偏好

ConvertTodo(TodoRef, TodoKind, EffectiveDate, Rule?)复用原事务/原子存储与Undo/Redo。普通/持续互转保留ID和关联；持续转普通的显示日可选。涉及系列只允许根，指定生效日；新模板/发生使用新身份，旧完成历史保留，生效日起的未完成发生取消。对应未完成引用迁移到后继/新首个发生；无映射的旧关联留在保留的取消记录，保持可解析。新周期模板包括原结构但重置完成；系列转单一后继只带未完成子树。没有无限预生成；没有schema变化。

窗口偏好schema2兼容追加：CollapseCompletedChildren、RevealHotkey、FixedWeekRows（默认6）、FixedWeekStart、TextScale（默认1）、CalendarTextBrightness（默认0.2）。旧字段FixedSixRows仍表示启用固定行数，确保读取旧设置；缺少新字段采用默认值。每父已完成分支的临时展开覆盖只保留会话，不写入业务实体。

彩色emoji为系统字体派生的运行时字形缓存，不属于个人图片资产、不写JSON/ZIP。Markdown只读预览使用带原Unicode标记的内联图形，复制回文本保留原Unicode；编辑源码和业务正文保持原字符串。