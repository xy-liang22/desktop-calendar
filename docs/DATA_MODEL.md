# 数据与服务接口：P2基础、P3接入与P3.1完成语义

状态：应用与Core为0.3.1，P3已接入真实普通待办，P3.1修订完成来源/快照及编辑体验。纯C# Core使用.NET10标准库，无WPF/数据库/联网/第三方依赖。重复/持续、Activity、周目标等完整管理UI仍留后续阶段。

## 实体和身份

P3.2：新用户五分类工作/家务/个人/家庭/健康，旧分类不自动迁移或重分类；仅用户明确初始化补齐缺失内置键。NewTodo子草稿捕获父分类/覆盖色，此后独立；不是Core每次Save自动继承。UI换行模式在独立窗口偏好，业务schema仍3。

- `PlannerState` 是不可变快照，文档 ID、递增 Revision 和实体数组；`Entity` 含 GUID 与 UTC 创建/修改时间。分类共享，归档保留历史引用。排序键 decimal，完成状态仅决定分组，不改基础键。
- `TodoOrdering` 记录根组的日期或子组的父ID，以及Manual/PlannedTime模式；默认时间优先，手动混排模式和键同时存JSON，重启不悄悄切回时间排序。`SetTodoOrderMode`恢复时间排序时仍保留原手动键。`DayDecoration`记录每天手选底色，单日期唯一；真实历法/假日参考数据仍待后续阶段。
- `Todo`：一次性、持续或物化周期实例；计划日期DateOnly、可选TimeOnly、优先级/描述/分类/颜色、父节点、进度、完成时间。`ProgressMode.Default`递归平均直接子分支，叶用ManualProgress；显式Manual不派生进度。完成来源规则如下，不依据“进度100%”直接决定完成。
- 周期使用 `TodoSeries -> RuleVersion -> Templates`。模板树按版本保存；实例身份是系列 ID＋节点 ID＋**原始出现日期**的稳定 SHA256 派生 GUID，不受系统文化、移动日期或重启影响。不预生成无限任务；查询按日期生成，只有修改/完成/建立关联时物化，并物化祖先。删除节点保留RetiredNodeIds墓碑，保护已完成实例的身份有效性。
- `RecurrenceRule`：日/周/月间隔、指定星期或月日、周首日、Count/Until、排除和追加日期、时区。每月31日遇短月跳过，不钳为月末；COUNT计算排除前的名义出现次数，追加日另计。规则和有效日期版本分离。
- `Activity`：真实时间记录，含多个各自有 ID 的 `ActivityPeriod`、emoji/摘要/reaction、文本或 Markdown 备注、可选 TodoRef、分类与图片资产引用。删除 Todo 保留 Activity 并保留旧标题；Activity 不修改 Todo 完成状态。
- `WeekGoal`：周身份、数量/秒数/勾选目标、单一自动来源、两种导航关联列表、独立手动覆盖。类型/来源变更必须显式确认。跨周关联 Todo 不静默丢弃，数量按显式关联完成项计，查询同时返回 `OutsideWeekTodos`，供后续 UI 警告。
- `WeeklyReview`：手写 Markdown、自定义小节，与独立自动摘要快照。`SaveReview` 不能覆盖统计；`AppendStatistics` 不能覆盖正文。
- `LocalAsset`：ID、受限 `assets/images/<文件名>` 路径、MIME、长度、SHA256；`PlannerQueries.AssetReference/ResolveAsset`提供`asset://<GUID>`引用，只解析注册信息，拒绝file/network/路径后缀。仅数据注册，不导入/读取图片，不渲染 Markdown。剪贴板、实际图片验证与 ZIP 全量恢复属于后续阶段。

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

`CopyTodo(TodoRef, TargetDate)`仅复制普通子树：新GUID/CopiedFromId，保留属性、相对日期和子组排序；重置完成时间、来源、快照、失效标识和手动进度，不复制Activity或周目标链接，单事务可撤销。P3.1新增`RenameTodo(TodoRef, Title, ExpectedTitle)`，只改标题，空值/陈旧标题拒绝，避免内联覆盖高级字段。

## P3.1 完成事务与迁移

`CompletionIntent`：Automatic按非取消直接子项全部完成而自然完成（叶默认未完成）；ManualComplete显式完成；ManualIncomplete显式保持未完成；CascadeComplete由祖先强制完成。取消自然完成不反向取消孩子，100%但未完成是合法覆盖状态。独立后代完成/进度/结构变化释放覆盖，文字/颜色/排序不释放。

`SetTodoCompleted(true)`物化所需实例及祖先，保存`CompletionSnapshot(Id, CapturedAt, Guard, Entries)`后级联活动后代。Entries保存原状态/来源/完成时间/持续冻结日。Guard为按ID排序的子树身份/父子关系/完成/进度字段SHA256，不含文字/颜色/排序/ModifiedAt。取消时校验Guard，仅恢复完成字段，保留元数据；独立子树变化使快照失效，不盲目恢复，标记CompletionSnapshotInvalidated。嵌套快照暂存；结构删除/提升释放孤立Cascade来源。所有归一、排序分组、快照与Undo/Redo在同一保存事务内完成；无UI状态循环。

schema3严格校验来源、快照作用域、Guard与关联。v2先验旧校验和，再将旧完成项映射ManualComplete（无虚构快照）、旧未完成映射Automatic；旧Manual/0进度改Default，非零Manual和Children保留。按新规则归一旧父状态，稳定ID/Revision/其他实体不变。读取迁移不写磁盘，下一成功修改才保存3并备份原字节；旧程序必须拒绝3而非降级。新默认分类仅日常/工作/生活；历史重要/紧急分类不删除、不转优先级。只在UI新建选择中过滤标准历史默认分类，改名/自定义不变。

## P3界面边界

`DesktopSession`持有一个`PlannerController`，重建HWND也复用；月历、中栏和今日折叠只读取此工作区快照。编辑器使用独立草稿，旧记录发生变化则拒绝覆盖。保存按钮以BatchCommand提交字段与完成操作，保存成功才刷新记录；失败旧快照/草稿保留，状态在完整/折叠均可见。加载未知/损坏版本只读，不初始化空业务数据；新文件只初始化分类，不填DemoTodo。

同级拖放仅同父/同状态组；完成不改基础键。月历默认根、显式展开子项，容量先根/父再子，+N不含折叠项。`QuickDraft`和`InlineTitleDraft`仅UI未提交草稿，业务仍单一Workspace。完整编辑×/Esc验证保存，明确取消放弃；无修改不写入，失败保留草稿。内联Enter/有效失焦保存、Esc取消，使用期望旧标题命令。正常退出先处理编辑器/内联，再确认放弃快速草稿并等待提交；强制终止未提交文字不保证恢复。

日期/月/分类文字或底色偏好仅新增到原window-preferences schema2，未把UI状态写入业务文件。根项目排除Core/tests的递归Compile仍保留，避免把独立库重复编译；旧371项显式fixture回归才用DemoTodo，普通运行不使用它。P4/P5/P6后续也应沿用同一controller/workspace，不另造JSON仓库。

单次周期修改不改规则，可用 MoveTo 改日期；FromDate/FutureUnfinished 都以命令From为界分割未来规则版本，整系列更新各版本；各范围均保护已物化完成记录。取消本次记录取消键，未来用 SuppressedFrom，整系列归档；CancelSeries的可选NodeId限定子树。已完成记录及所需祖先保留，不能因范围取消删除已完成历史。已保存实例保留自己的树/移动日期例外，尚未生成的未来实例使用新模板。撤销/重做保存最多100个会话内事务快照，每次仍提交新 Revision；实体历史字段按事务快照恢复；重载清空历史，不做跨进程永久撤销日志。

## JSON 与恢复

默认 `%LOCALAPPDATA%\DesktopCalendar\planner-data.json`；窗口偏好仍是独立 `window-preferences.json`，兼容旧schema2。`PlannerOptions`作为调用上下文传入，不写入业务JSON。P3正常启动读取真实业务文件；只有主/备份/残留都不存在才允许首次初始化，现有P2其他实体与关联保留。

业务schema3：`schemaVersion/documentId/revision/payload/payloadSha256`，DTO不存WPF对象，严格字段/枚举/重复键/关联检查；未知同版本字段拒绝。v1为早期迁移合同（documentId/data，无revision/校验和），不是P1.1真实业务文件；兼容v1/v2读取迁移，下一次明确成功保存才写3并备份旧文件。

同目录唯一临时文件，完整写入、Flush(true)、重新解码校验、File.Replace 保留 `.bak`；首次创建用同卷 Move。每个工作区串行队列＋跨进程排他锁文件＋文件指纹/Revision，防晚写覆盖和旧快照。旧锁文件不是持锁，进程退出由系统释放句柄。写入失败返回诊断；提交后异常先核实实际结果，不盲目重试。

损坏主文件只读阻断，绝不静默回空；`.bak` 与 `.pending-*` 提供经过验证的恢复候选，须显式 `RecoverAsync(candidate, confirmed:true)`，保留原件为 `.evidence-*`。存在更新 schema 的主文件或候选时禁止降级。完整主文件＋遗留临时文件仍用主文件，不擅自提升未提交事务；缺失主文件但有残留则阻断空初始化。恢复候选指纹也校验，避免恢复期间被替换。64MiB限额，错误码不含备注正文；SHA256用于完整性而非加密或身份认证。

不自动删图片/证据/备份。单个 JSON 不包含图片，后续完整备份必须包含整个资源目录。文件锁、I/O错误、权限错误、取消、新版schema和冲突均可诊断；冲突后调用 ReloadAsync，不直接强行覆盖。

## 验证

Windows PowerShell：`powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-Planner.ps1`，194 PASS/0 FAIL/1 NOT VERIFIED；`Test-P3.ps1 -Configuration P31`验证P3.1控件、快照和进程重启。只用忽略的artifacts，不使用个人文件。测试包含真实Windows占用/只读、两个写入器、六写入阶段直接进程退出、复制子树排序状态及P3不覆盖其他实体。历史时区以Windows资料为准；Samoa2011整日跳跃本机未重现，不能保证所有历史政令资料完整。未安装时区库；物理断电、满盘、Win10实机及真实输入设备未冒称通过，详见TESTING。
