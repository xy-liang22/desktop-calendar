# P2 数据与服务接口

状态：实现和可靠性验证进行中；业务 UI 仍是 P1.1 模拟内容。纯 C# `Core/DesktopCalendar.Core.csproj` 使用 .NET 10 标准库，无 WPF、数据库、联网或第三方依赖。主程序仅排除独立项目源码，不改变窗口与安装包。

## 实体和身份

- `PlannerState` 是不可变快照，文档 ID、递增 Revision 和实体数组；`Entity` 含 GUID 与 UTC 创建/修改时间。分类共享，归档保留历史引用。排序键 decimal，完成状态仅决定分组，不改基础键。
- `Todo`：一次性、历史/仅最新持续任务或已物化周期实例；计划日期 DateOnly、可选 TimeOnly、重要/紧急、描述/分类/颜色、父节点、手动/子节点进度、完成时间。父完成须显式操作，不自动跟随进度。
- 周期使用 `TodoSeries -> RuleVersion -> Templates`。模板树按版本保存；实例身份是系列 ID＋节点 ID＋**原始出现日期**的稳定 SHA256 派生 GUID，不受系统文化、移动日期或重启影响。不预生成无限任务；查询按日期生成，只有修改/完成/建立关联时物化，并物化祖先。
- `RecurrenceRule`：日/周/月间隔、指定星期或月日、周首日、Count/Until、排除和追加日期、时区。每月31日遇短月跳过，不钳为月末；COUNT计算排除前的名义出现次数，追加日另计。规则和有效日期版本分离。
- `Activity`：真实时间记录，含多个各自有 ID 的 `ActivityPeriod`、emoji/摘要/reaction、文本或 Markdown 备注、可选 TodoRef、分类与图片资产引用。删除 Todo 保留 Activity 并保留旧标题；Activity 不修改 Todo 完成状态。
- `WeekGoal`：周身份、数量/秒数/勾选目标、单一自动来源、两种导航关联列表、独立手动覆盖。类型/来源变更必须显式确认。跨周关联 Todo 不静默丢弃，数量按显式关联完成项计，查询同时返回 `OutsideWeekTodos`，供后续 UI 警告。
- `WeeklyReview`：手写 Markdown、自定义小节，与独立自动摘要快照。`SaveReview` 不能覆盖统计；`AppendStatistics` 不能覆盖正文。
- `LocalAsset`：ID、受限 `assets/images/<文件名>` 路径、MIME、长度、SHA256；仅数据注册，不导入/读取图片，不渲染 Markdown。剪贴板、图片验证与 ZIP 全量恢复属于后续阶段。

## 时间与统计

计划日期/时间是当地墙钟值；Activity 起止为带偏移 DateTimeOffset，保存为 UTC，另存 TimeZoneId。`PlannerTime.FromLocal` 拒绝 DST 空洞，对重复小时要求显式偏移。所有区间 `[start,end)`，零长/反向拒绝；按当地日/周边界裁剪，23/25小时日及 DST 周使用真实秒数。

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

`SaveTodo/SetTodoCompleted/MoveCarryTodo/ReorderTodos/DeleteTodo`、`CreateSeries/EditSeries/CancelSeries`、`SaveActivity/DeleteActivity`、`SaveGoal/DeleteGoal`、`SaveReview/AppendStatistics/DeleteReview`、分类归档与资产命令均经过整图校验、可靠保存，再发布新快照。UI 应调用命令，不直接修改集合。查询入口为 `PlannerQueries` 和 `PlannerTime`。创建模板树须事先分配节点 GUID 以表达父子关系。

单次周期修改不改规则，可用 MoveTo 改日期；FromDate/FutureUnfinished 分割未来规则版本，整系列更新各版本；各范围均保护已物化完成记录。取消本次记录取消键，未来用 SuppressedFrom，整系列归档。已完成记录及所需祖先保留，不能因范围取消删除已完成历史。撤销/重做保存最多100个会话内事务快照，每次仍提交新 Revision；重载清空历史，不做跨进程永久撤销日志。

## JSON 与恢复

默认 `%LOCALAPPDATA%\DesktopCalendar\planner-data.json`；窗口偏好仍是独立 `window-preferences.json`，未改旧 schema2。`PlannerOptions` 作为调用上下文传入，不写入业务 JSON；后续 UI 设置可持久化到独立设置。当前主 UI 不调用数据层，不会创建正式业务文件。

业务 schema2：`schemaVersion/documentId/revision/payload/payloadSha256`，独立 DTO 映射，不存 WPF 对象。严格字段/枚举/重复键/关联校验；未知同版本字段拒绝，避免悄悄丢失。v1 是本阶段定义的早期迁移合同（documentId/data，无 revision/校验和），不是声称 P1.1 曾有业务数据。加载迁移只在内存，下一次明确成功保存后写 schema2 并备份 v1。

同目录唯一临时文件，完整写入、Flush(true)、重新解码校验、File.Replace 保留 `.bak`；首次创建用同卷 Move。每个工作区串行队列＋跨进程排他锁文件＋文件指纹/Revision，防晚写覆盖和旧快照。旧锁文件不是持锁，进程退出由系统释放句柄。写入失败返回诊断；提交后异常先核实实际结果，不盲目重试。

损坏主文件只读阻断，绝不静默回空；`.bak` 与 `.pending-*` 提供经过验证的恢复候选，须显式 `RecoverAsync(candidate, confirmed:true)`，保留原件为 `.evidence-*`。存在更新 schema 的主文件或候选时禁止降级。完整主文件＋遗留临时文件仍用主文件，不擅自提升未提交事务；缺失主文件但有残留则阻断空初始化。恢复候选指纹也校验，避免恢复期间被替换。64MiB限额，错误码不含备注正文；SHA256用于完整性而非加密或身份认证。

不自动删图片/证据/备份。单个 JSON 不包含图片，后续完整备份必须包含整个资源目录。文件锁、I/O错误、权限错误、取消、新版schema和冲突均可诊断；冲突后调用 ReloadAsync，不直接强行覆盖。

## 验证

Windows PowerShell：`powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-Planner.ps1`。只使用项目忽略的 artifacts 隔离目录。测试包含真实 Windows 文件占用/只读、两个写入器、独立子进程重读及六个写入阶段的直接进程退出，详细结果见 TESTING；不使用个人数据文件。
