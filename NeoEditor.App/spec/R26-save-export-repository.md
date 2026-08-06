# R26 — 保存/导出工作流：对称 Repository 契约 + 三动作

> 生效：2026-08-01 | 修订：2026-08-01 v2（对称契约）
> 来源：用户决策 Phase 9 议题 3
> 依从：R01 状态唯一所有者 · R24 数据修改必经 HostService · R25 扩展点

## 规则

领域模型（IEntity）之上，**DB 与 XML 都是持久化后端（Repository）**，二者是同一抽象的两个实现。
**契约完全对称**：CRUD、diff、dirty、save/export、load/import 五种能力，两个后端各实现一份，
接口上**一个能力一个函数**。禁止后端特判（NotSupported / 空返回 / 仅单端方法）。

### 1. 对称契约（IEntityRepository<T>）

```csharp
public interface IEntityRepository<T> : IDataRepository<T> where T : IEntity
{
    // ── CRUD：增删查改，四个显式函数 ──
    Task AddAsync(T entity);                  // 增
    Task UpdateAsync(T entity);               // 改
    Task DeleteAsync(string entityId);        // 删
    // 查：GetByIdAsync / GetAllAsync（继承自 IDataRepository<T>）

    // ── diff：行级 + 字段级，两个函数 ──
    Task<IReadOnlyList<RowDiff>> GetDiffAsync(IReadOnlyList<T> candidates);   // 行级/文件级
    Task<IReadOnlyList<DiffEntry>> GetFieldDiffAsync(T before, T after);      // 字段级（DiffEngine）

    // ── dirty：repository 暴露，session 持有 ──
    IReadOnlyCollection<string> DirtyIds { get; }
    void MarkDirty(IEnumerable<string> ids);
    void ClearDirty(IEnumerable<string> ids);

    // ── save/export：一个函数 ──
    Task SaveAsync(IEnumerable<T> entities);   // DB=upsert+delete；XML=写文件+删节点

    // ── load/import：一个函数 ──
    Task<IReadOnlyList<T>> LoadAsync();        // DB=读全部；XML=解析本 mod
}
```

- `IDataRepository<T>` 收敛为只读契约（`GetByIdAsync` / `GetAllAsync`）。
- `RowDiff`：`record RowDiff(string TargetId, DiffKind Kind, string? OldContent = null, string? NewContent = null)`
  — DB 填 `TargetId` + `Kind`（行级）；XML 填 `TargetId`(文件路径) + `Kind` + `OldContent/NewContent`（文件级快照）。
- 两端全实现五种能力，无 `NotSupportedException`、无空返回特判。

### 2. CRUD 经 HostService command（R24）

- 增/改/删**不直接写后端**：`AddAsync` → `AddEntityCommand`、`UpdateAsync` → `ReplaceEntityCommand`、
  `DeleteAsync` → `DeleteEntityCommand`，统一走 `HostService.ExecuteAsync`（undo 栈 + 标脏 + 缓存 + 事件）。
- HostService 的 `_entityCache` + `_session` 即工作集；repository 不持独立状态（R01/N01）。
- 删除落盘：`DeleteEntityCommand` 移除缓存并记录 tombstone，`SaveAsync` 时从后端删除。
- `PreExecuteHook` 在命令执行前触发（修复原空挂缺陷）。

### 3. XmlRepository 构造绑定 mod

- `XmlRepository<T>` 构造绑定一个 `modId`（每 mod 每实体类型一个实例），`LoadAsync()` 无参读该 mod。
- HostService 按 (实体类型, modId) 解析 XmlRepository 实例；`DbRepository` 绑定 game.db（工作库）。

### 4. HostService 三动作

```csharp
Task<SaveResult> SaveAsync(string? entityId = null);        // Save：内存 → DB
Task<SaveResult> SaveAllAsync();                             // 全部 dirty 落库
Task<IReadOnlyList<ExportResult>> ExportModAsync(int modId); // Export：DB → XML（计算 diff 供预览）
Task<IReadOnlyList<ExportResult>> ExportProfileAsync();
Task CommitExportAsync(IEnumerable<RowDiff> diffs);         // 写盘：确认后的 XML diff 唯一写入口（2026-08-03）
Task<PublishResult> PublishAsync();                          // 默认：Save + Export 事务
```

- 返回值 `SaveResult.PartialDiff` 驱动 dirty 清理（R01/R09）。
- 事务语义：diff 弹窗取消 = 整个 Publish 回滚（DB 也不落库）。
- hook（R25）：`PreSaveHook` 挂 DB 落库前；`PreExportHook` 挂 XML 写盘前；`PreExecuteHook` 在命令执行前。
- **写盘路径**：`ExportModAsync` 只计算 diff（`XmlRepository.GetDiffAsync`），**不写文件**；用户确认后的
  diff 由 `CommitExportAsync` 落盘（唯一写入口）。View 不得直接 `File.WriteAllText`（R24 收束，2026-08-03）。

### 5. dirty session 按 profile

一个 profile 一个 `IWorkspaceSession`（DirtyEntities 作用域收窄到当前 profile）。repository 的
`DirtyIds` / `MarkDirty` / `ClearDirty` 委托当前 profile 的 session（数据所有者仍是 session，R01）。

### 6. 收口

- ModManager（Import/Load/Create/Delete/ExportZip）并入 HostService。
- 删除保存管线的 Validation/Conflicts。
- XML 直接接入（XML-first）另立阶段评估；本阶段 DB 为源、XML 为导出产物。

## 禁止

- 禁止 View 层直接持有 `GameDbContext` / `EditorDbContext` 做写入（R24）
- 禁止在 HostService 之外另起一套 **写盘/导出提交** 逻辑（写盘只能走 `CommitExportAsync`；预览用 diff 可基于内存态计算，供弹窗展示，不落盘）
- 禁止 repository 契约出现后端特判（NotSupported / 空返回 / 仅单端方法）
- 禁止 repository 新增独立静态/全局状态（R01/N01）

## 理由

1. 领域模型之上，XML 与 DB 本质都是序列化格式，不应有"一等/二等"之分
2. 对称契约让调用方（HostService/CLI/MCP/UI）不感知后端差异，一个能力一个函数
3. diff 归 repository，HostService 与 View 都不再重复实现 diff，逻辑收敛
4. CRUD 走 command 使 undo/redo/dirty/hook 免费获得，R24 单写路径彻底化

## 验收

- Save/Export/Publish 三动作均有 Infra.Tests 覆盖（含取消回滚）
- DbRepository / XmlRepository 的 CRUD/diff/Save/Load 均有测试，验证对称性（无特判）
- 架构测试验证 View 层不引用 GameDbContext 写入路径
- `PreExecuteHook` 在 `ExecuteAsync` 中实际触发
