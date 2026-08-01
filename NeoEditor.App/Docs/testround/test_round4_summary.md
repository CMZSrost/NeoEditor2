# 架构测试第4轮 — M8 收尾：脏数据指示修复

> 日期：2026-07-25 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round3_summary.md](test_round3_summary.md)
> 后续：[test_round5_summary.md](test_round5_summary.md) (M9 前清理 — 删除 23 个 App.* 静态访问器 + 修复旧 NeoEditor.Tests)

## 本轮目标

修复 test_round3 发现的唯一回归 Bug：**脏数据视觉指示消失**（R09）。

---

## 前置条件

- [x] `bash build.sh` 编译通过（0 Error）
- [x] `dotnet test` 11/11 通过

---

## Bug 描述

| 症状 | 根因 |
|------|------|
| DataTable 修改行无黄色背景 | `EditStore.EditedCells` 未被正确填充 → `PushEditStateToGrid` 创建空的 `EditedEntityIds` |
| DataTable Tab 无 ● 标记 | 同上，`MarkTabDirty` → `PushEditStateToGrid` 从空 `EditStore` 读取 |
| Value Editor 顶部无 Alert 提示 | `IsCurrentEntityDirty` 双重检查（`EditedCells` + `DirtyEntities`）过于严格 |

---

## 根因分析

三个独立但相关的缺陷：

### A. WAL 恢复不填充 EditStore

`ReloadTabsAsync` → `EditStore.Clear()` 清空 `EditedCells`，随后 WAL 命令重放只设置属性值（不更新 edit-tracking）。`MarkTabsDirtyFromEditedCells` 读空集合 → 跳过 → `DirtyEntities` 不填充。

### B. IsCurrentEntityDirty 双重检查过严

`KeyValueEditorViewModel.LoadEntity` 要求 `EditedCells.Any() && DirtyEntities.Contains()` 同时满足。KV/XML 编辑器编辑走命令系统（不经过 `EditStore`），导致 `false && true` = `false`。

### C. 运行时编辑不填充 EditStore（多视图 Bug）

`SearchableDataGrid.OnCellEditEnding` 走 `GenericDataGridHelper.EditedCells` → `Session.ActiveEditStore`（全局单例）。多视图布局（Center + Bottom）下 `ActiveEditStore` 可能指向**其他视图的 EditStore** — 编辑写入 View A 的 store，但 `PushEditStateToGrid` 读取 View B 的 store。同时 `OnCellEditCommitted` 和 `OnEntityFieldEditsFromXml` 完全没有向 `EditStore.EditedCells` 写入。

---

## 修复内容

### Batch 1 — 基础设施 + Alert

| # | 文件 | 修改 |
|---|------|------|
| 1 | `IEditorCommand.cs` (Infra) | 新增 `GetAffectedEntityIds()` 默认接口方法 |
| 2 | `EditCellCommand.cs` (Infra) | 实现 → `{_entity.EntityId}` |
| 3 | `BatchEditCommand.cs` (Infra) | 实现 → 所有 `EditRecord.Entity.EntityId` |
| 4 | `AddEntityCommand.cs` (Infra) | 实现 → `{_entity.EntityId}` |
| 5 | `DeleteEntityCommand.cs` (Infra) | 实现 → `{_entity.EntityId}` |
| 6 | `ModGameDataTabsView.axaml.cs` | WAL 恢复后调用 `GetAffectedEntityIds()` 填充 `EditStore.EditedCells`（单 mod + 合并视图） |
| 7 | `KeyValueEditorViewModel.cs` | `IsCurrentEntityDirty` → 仅检查 `DirtyEntities` |

### Batch 2 — 运行时 EditStore 填充（用户验收反馈）

| # | 文件 | 修改 |
|---|------|------|
| 8 | `SearchableDataGrid.axaml.cs` | `OnCellEditEnding` 优先用 `this.EditStore?.EditedCells`（本控件自己的 store），fallback 到 GDH 全局路径 |
| 9 | `ModGameDataTabsView.axaml.cs` | `OnCellEditCommitted` 命令执行后 `EditStore.EditedCells.Add((entityId, colName))` |
| 10 | `ModGameDataTabsView.axaml.cs` | `OnEntityFieldEditsFromXml` 命令执行后遍历 `GetAffectedEntityIds()` → `EditStore.EditedCells.Add((eid, "*"))` |

### 关键设计原则

- `EditStore.EditedCells` 属于**当前视图**，不依赖全局 `Session.ActiveEditStore`（多视图下不可靠）
- `DirtyEntities` 是实体级脏状态的**单一真相源**（所有编辑路径都通过 `MarkEntityDirty`）
- 三条编辑路径（DataGrid 直接编辑 / CellEditCommitted / KV-XML 编辑器）**全部**填充 `EditStore.EditedCells`

---

## 结果汇总

| # | 验收项 | 结果 |
|---|--------|:--:|
| 1 | DataTable 黄色行背景 | ✅ 人工验收通过 |
| 2 | DataTable Tab ● 标记 | ✅ 人工验收通过 |
| 3 | Value Editor Alert | ✅ 人工验收通过 |
| 4 | 基础回归（编译 + 单测） | ✅ 0 Error / 11/11 |

**通过 / 总计**：4 / 4

```bash
bash build.sh          # 0 Error / 6 Warning (全为 NU1903 已知)
dotnet test (5项目)     # 11/11 通过 ✅
```

- 依赖方向审计：Core / Infra 无 Avalonia，UI.Common 无 Core/Infra/App 引用 ✅
- 架构合规：R09 / R01 / N01 / R03 / R07 ✅

---

## 下一轮预告 (test_round5)

M9 前清理：删除 `App.*` V6 静态访问器 + 旧 `NeoEditor.Tests` 项目引用修复。
