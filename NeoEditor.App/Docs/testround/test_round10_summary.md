# 架构测试第10轮 — Dirty State 统一修复验证

> 日期：2026-07-26 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round9_summary.md](test_round9_summary.md) (Dirty State 统一 + DataViewer 功能验收)

## 本轮目标

test_round9 完成了 Dirty State 统一到 `IWorkspaceSession.DirtyEntities` 的架构改造，但验收发现 4 个问题。本轮对 4 个问题做根因分析和修复。

---

## 最终结果

| # | Bug | 状态 | 根因 |
|---|-----|:--:|------|
| P1 | Value Editor alert 不隔离 | ✅ 已修复 | `ApplyXmlToEntity` 无条件调用 `MarkDirty()` |
| P2 | DataTable 行黄底与 Tab ● 不一致 | ✅ 已修复 | 同 P1 — 每次打开实体都污染 DirtyEntities |
| P3 | 引用列 rawtext | ✅ 已修复 | 内存 `ReferenceIndex` 从未构建 |
| P4 | 重启后 Game entity 编辑丢失 | ⬜ 未测试 | 代码已完成，跳过验证 |

---

## 前置条件

- [x] `bash build.sh` 编译通过（10 项目，0 Error）
- [x] `dotnet test` 21/21 通过（6 个测试项目）
- [x] P1/P2/P3 全部修复

---

## P1 + P2 — 根因分析

### 现象

- **P1**: WAL 恢复后点**任意** entity（含无 WAL 编辑的），Value Editor 顶部黄色 banner 都显示
- **P2**: 双击打开 entity A → 打开 B → 双击打开 A，Center tab 标记 *

### 追踪过程

1. **Round 1** (静态分析): 怀疑 `LoadEntity` 未被调用、`PushEditStateToGrid` 时序问题。添加 `ClearDirtyEntities()` 和 `RefreshRowBackgrounds()` — 无效。

2. **Round 2** (静态分析): 怀疑 `EntityEditorDocument.MarkDirty()` 直接操作 `DirtyEntities.Add()` 绕过 `DirtyStateChanged` 事件。添加 `OnCurrentEntityChanged` 钩子、改用 `MarkEntityDirty()` — 无效。

3. **Round 3** (运行时日志诊断): 在 alert banner 中输出 `EntityId` 和 `IsCurrentEntityDirty` 值，配合 Serilog 日志分析。发现关键证据：

```
[RefreshBG] rows=14 editedIds=3   ← WAL 恢复后 3 个脏实体
[RefreshBG] rows=14 editedIds=4   ← 打开实体 A 后变成 4
[RefreshBG] rows=14 editedIds=5   ← 打开实体 B 后变成 5
[RefreshBG] rows=14 editedIds=6   ← 打开实体 C 后变成 6
```

每打开一个实体，`DirtyEntities` 就多一个。而每次都有 `[XML-Apply] Phase2 done: 0 diffs` 日志——XmlEditor 的 `TextChanged` → 150ms 防抖 → `ApplyXmlToEntity()` 被执行，但 **0 个 diff**。

### 根因

`EntityEditorDocument.ApplyXmlToEntity()` 第 278 行：

```csharp
if (edits.Count > 0) { ... }
else { Log("no diffs"); }

RefreshVisualizationCommand.Execute(null);
MarkDirty(); // ← 在 if 块之外，无条件执行！
```

每次 `ApplyXmlToEntity` 被触发（打开实体时 `XmlContent.Text` 赋值 → `TextChanged` → 防抖），即使 **零 diff**，`MarkDirty()` 也把实体加入 `DirtyEntities`。这同时导致：
- **P1**: 打开的实体都被标记脏 → Value Editor 显示 alert
- **P2**: 重开已打开的实体时 `IsDirty=true` → Center tab 显示 *

### 修复

`EntityEditorDocument.ApplyXmlToEntity()` — `MarkDirty()` 移入 `if (edits.Count > 0)` 内部：

```csharp
if (edits.Count > 0)
{
    Messenger.Send(new EntityFieldEditsMessage(Entity, edits));
    RefreshVisualizationCommand.Execute(null);
    MarkDirty(); // ← 仅在有实际 diff 时
}
else
{
    RefreshVisualizationCommand.Execute(null);
}
```

---

## P3 — 根因分析

### 现象

DataTable 中引用字段（如 `nComponentID`）显示原始数字/ID，而非解析后的引用目标名称。

### 根因

`ReferenceResolver.LookupSubject` 使用 `activeStore.Index`（内存 `ReferenceIndex`），但系统只构建了 `IndexService`（SQLite 版）。`BuildMergeViewIndexAsync` 调用 `IndexService.BuildAsync(entries)`，而内存索引 `ReferenceIndex.BuildAsync()` **从未被调用**。因此 `_nsIndex` 和 `_mergedIdIndex` 始终为空，所有引用查找返回 null。

### 修复

在 `ReloadTabsAsync` 和 `ReloadMergeTabsAsync` 中，`BuildMergeViewIndexAsync()` 之后增加：

```csharp
await MergeStore.Index.BuildAsync();
```

---

## P4 — 状态

P4 修复代码已完成（移除 `ModId==-1` 拦截 + `("game",0)` WAL 虚拟目标 + merge 恢复中处理 `game:0`），未做运行时验证。

---

## 改动文件清单（全 3 轮）

| 文件 | 改动 | 关联 Bug |
|------|------|:--:|
| `NeoEditor.App/ViewModels/MainContent/EntityEditorDocument.cs` | `ApplyXmlToEntity`: `MarkDirty()` 移入 `edits.Count > 0` 块内 | P1, P2 |
| `NeoEditor.App/ViewModels/MainContent/EntityEditorDocument.cs` | `MarkDirty`/`MarkClean`/ctor 走 `MarkEntityDirty` 触发 `DirtyStateChanged` | P2 |
| `NeoEditor.App/ViewModels/MainContent/KeyValueEditorViewModel.cs` | 新增 `OnCurrentEntityChanged` 自动重算 dirty | P1 |
| `NeoEditor.App/ViewModels/MainContent/DocumentWorkspaceViewModel.cs` | `OnEntitySelected` 加 `LoadEntity` | P1 |
| `NeoEditor.App/Views/UserControls/ModGameDataTabsView.Data.cs` | `ReloadTabsAsync` + `ReloadMergeTabsAsync` 加 `ClearDirtyEntities()` + `Index.BuildAsync()` | P1, P3 |
| `NeoEditor.App/Views/UserControls/ModGameDataTabsView.Tab.cs` | `OnTabChanged` + `PushEditStateToGrid` 末尾 `RefreshRowBackgrounds()` | P2 |
| `NeoEditor.App/Views/UserControls/ModGameDataTabsView.axaml.cs` | 移除 `ModId==-1` 拦截 + `("game",0)` WAL 恢复 | P4 |
| `NeoEditor.Infra/Services/EntityMergeStore.cs` | 恢复 `Index` 懒加载 + `Clear()` 中 `_index=null` | P3 |
| `NeoEditor.Plugins.DataViewer/ViewModels/DataTableViewModel.cs` | `OnCommandPersistAsync` 加 `ModId==-1` → `("game",0)` 分支 | P4 |

---

## 测试环境

- OS: Windows 10 Pro 22H2 (19045)
- .NET SDK: 10.0.301
- Avalonia: 11.3.12
- 分支: main

## 测试执行

| 时间 | 测试人 | 结果 | 备注 |
|------|--------|------|------|
| 2026-07-26 | | P1 ❌ P2 ❌ P3 ❌ P4 ⬜ | Round 1: 4 修复均未生效或未测试 |
| 2026-07-26 | | P1 ❌ P2 ❌ P3 ✅ P4 ⬜ | Round 2: P1+P2 再分析，P3 已验证 |
| 2026-07-26 | | P1 ✅ P2 ✅ P3 ✅ P4 ⬜ | Round 3: 日志诊断找到 P1/P2 真正根因并修复 |
