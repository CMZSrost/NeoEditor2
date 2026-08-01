# 架构测试第9轮 — Dirty State 统一 + DataViewer 功能验收

> 日期：2026-07-26 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round8_summary.md](test_round8_summary.md) (M9 接口提取 + ViewModel 迁移)
> 上承：[test_round7_summary.md](test_round7_summary.md) (M9 App 集成 + DI 修复)

## 本轮目标

test_round7 修复了编译和 DI 启动问题，test_round8 完成了接口提取和 ViewModel 迁移。本轮在此基础上完成两项工作：

1. **Dirty State 统一到 SSoT** — 将 DataViewer 4 套互不相干的 dirty 追踪体系收敛到 `IWorkspaceSession.DirtyEntities`
2. **DataViewer 完整功能验收** — 15 项核心功能 + dirty 专项 + 回归检查

> **本轮是 M9 的功能验收轮**。完成后 DataViewer 作为独立 Plugin 运行，dirty 状态所有组件一致。

---

## 前置条件

- [x] `bash build.sh` 编译通过（12 项目，0 Error）
- [x] `dotnet test` 21/21 通过（6 个测试项目）
- [x] DataViewer.Tests 10/10 通过
- [x] 启动正常，无 DI 解析失败（LocalizationService / BrowserIndexService 接口注入已修复）
- [x] Dirty State 统一架构改造完成

---

## 本轮改动背景：Dirty State 统一

### 问题

M9 迁移前，dirty 追踪存在 **4 套互不相干的体系**：

| 体系 | 位置 | 作用 |
|------|------|------|
| `WorkspaceSession.DirtyEntities` | Infra | Entity 级 dirty（KV Editor 黄色 banner 唯一使用方） |
| `EditStore.EditedCells` → `SearchableDataGrid.EditedEntityIds` | App View | DataGrid 行黄色背景 |
| `GameDataTypeTabItem._dirtyWasSet` | DataViewer VM | Tab 标题 "● " 前缀 |
| `DataTableViewModel._isDirty` / `_dirtyTabs` | DataViewer VM | VM 级 dirty flag（死代码，从未真正被调用） |

它们由不同代码路径设置，设置时机基本同步，但**清除时机不同**：
- `EntityEditorDocument.SaveDocument()` → 只清除 `DirtyEntities`（via `MarkClean`），不清除 tab dirty
- `QuickSaveAsync` → 清除 `DirtyEntities` + tab dirty + EditedCells，但三条路径可能不同步
- KV Editor 编辑 → 触发 `MarkTabDirty` + `MarkEntityDirty`，同步设置
- XML Editor 编辑 → 同上

根本矛盾：**没有单一真相源**，4 套体系各自的 Set/Clear 路径有细微差异，容易漂移。

### 修复

1. **`IWorkspaceSession` 新增 `DirtyStateChanged` 事件**（Core + Infra 两个接口）
2. **`WorkspaceSession` 实现**——在 `MarkEntityDirty` / `MarkEntitiesDirty` / `ClearDirtyEntities` / `RemoveDirtyEntities` 中触发，仅当 set 真正变更时触发
3. **`ModGameDataTabsView.SyncDirtyViewState()`**——订阅 `DirtyStateChanged`，遍历所有 tab 对照 `DirtyEntities` 同步：
   - Tab 标题 dirty：`tabHasDirty = tab.SourceCollection.Any(e => DirtyEntities.Contains(e.EntityId))`
   - VM dirty flag：`SetDirty(DirtyEntities.Count > 0)`
   - DataGrid 行高亮：`SharedDataGrid.EditedEntityIds = DirtyEntities`
4. **`PushEditStateToGrid`**——`EditedEntityIds` 改用 `WorkspaceSession.DirtyEntities` 而非 `EditStore.EditedCells.Select(...)`
5. **`KeyValueEditorViewModel`**——订阅 `DirtyStateChanged`，实时更新 `IsCurrentEntityDirty`

### 改动文件列表

| 文件 | 改动 |
|------|------|
| `NeoEditor.Core/Abstractions/IWorkspaceSession.cs` | 新增 `event EventHandler? DirtyStateChanged` |
| `NeoEditor.Infra/Services/IWorkspaceSession.cs` | 同上 |
| `NeoEditor.Infra/Services/WorkspaceSession.cs` | 4 个 dirty 修改方法均触发事件（仅在真正变更时） |
| `NeoEditor.App/Views/UserControls/ModGameDataTabsView.axaml.cs` | 订阅 `DirtyStateChanged` → `SyncDirtyViewState()`；新增 `SyncDirtyViewState()` 方法 |
| `NeoEditor.App/Views/UserControls/ModGameDataTabsView.Tab.cs` | `PushEditStateToGrid` 中 `EditedEntityIds` 改用 `DirtyEntities` |
| `NeoEditor.App/ViewModels/MainContent/KeyValueEditorViewModel.cs` | 订阅 `DirtyStateChanged` 实时刷新 `IsCurrentEntityDirty` |
| `Tests/NeoEditor.Plugins.DataViewer.Tests/Services/DataTableServiceTests.cs` | Stub 实现 `DirtyStateChanged` 事件 |
| `NeoEditor.Tests/TestStubs.cs` | FakeWorkspaceSession 实现 `DirtyStateChanged` 事件 |

---

## 验收清单

### 验收 A：编译与测试（自动化，3 项）

| 步骤 | 操作 | 预期 | 结果 |
|:--:|------|------|:--:|
| 1 | `bash build.sh` | 12 项目全部编译通过，0 Error | ⬜ |
| 2 | `dotnet test`（全部 6 个测试项目） | 21+ 测试通过，0 Failure | ⬜ |
| 3 | `dotnet build NeoEditor.Plugins.DataViewer` | 0 Error，确认 0 引用 App | ⬜ |

### 验收 B：Dirty State 一致性（核心专项，8 项）

> **这是本轮核心验收**。验证 4 个组件在**所有编辑路径**下 dirty 状态完全一致。

**测试方法**：对每个编辑路径（DataGrid cell 编辑 / KV 编辑 / XML 编辑），执行编辑操作后检查以下 4 个指标是否全部一致：

| 指标 | 组件 | 检查点 |
|:--:|------|--------|
| D1 | Value Editor（左侧） | 顶部黄色 banner "⚠ This entity has unsaved changes" 显示/隐藏 |
| D2 | DataTable Tab 标题 | "● " 前缀出现/消失 |
| D3 | DataTable 行背景 | 被编辑行显示黄色背景（#FFFFDC） |
| D4 | Ctrl+S / Save 按钮 | 工具栏 Save 按钮 enabled / disabled 状态 |

**编辑路径 × 一致性矩阵**：

| 步骤 | 操作 | D1 | D2 | D3 | D4 | 结果 |
|:--:|------|:--:|:--:|:--:|:--:|:--:|
| B1 | DataGrid 直接修改单元格值 | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ |
| B2 | 左侧 KV Editor 修改字段值 | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ |
| B3 | Center EntityEditor XML Tab 修改 → Apply | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ |
| B4 | 从 Filter 搜索后修改单元格 | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ |

**清除路径 × 一致性矩阵**：

| 步骤 | 操作 | D1 | D2 | D3 | D4 | 结果 |
|:--:|------|:--:|:--:|:--:|:--:|:--:|
| B5 | Ctrl+S (Save All) 后 | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ |
| B6 | 单 EntityEditorDocument 工具栏 Save 后 | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ |
| B7 | Undo (Ctrl+Z) 撤销所有编辑后 | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ |
| B8 | 切换 Tab 再切回，dirty 状态保持 | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ |

### 验收 C：DataViewer 基本功能（15 项）

| 步骤 | 操作 | 检查点 | 结果 |
|:--:|------|--------|:--:|
| 1 | 启动编辑器 | 启动正常，欢迎页完整，无启动异常 / crash | ⬜ |
| 2 | 打开 Profile → Browse Game Data | DataTable 打开，列头显示正确，数据行渲染 | ⬜ |
| 3 | 表切换 | 切换 ItemType / Recipe / Creature 等，表格内容正确刷新 | ⬜ |
| 4 | 排序 | 点击列头排序，升序/降序切换正常 | ⬜ |
| 5 | 过滤 | Filter 输入框输入关键字，行过滤正常 | ⬜ |
| 6 | 搜索（Ctrl+F） | FindReplace 面板打开，搜索关键词，结果高亮 | ⬜ |
| 7 | 单击行 | 行高亮，Bottom 面板显示详情（KV Editor + Peek Panel） | ⬜ |
| 8 | 双击行 | EntityEditorDocument 在 Center 区域打开 | ⬜ |
| 9 | Ctrl+Click 引用字段 | DataTable 跳转到引用实体行，Center 打开对应 Tab | ⬜ |
| 10 | Ctrl+RMB 引用字段 | Peek 面板弹出，显示目标实体信息 | ⬜ |
| 11 | 修改字段 → Ctrl+S | 保存成功，脏标记清除（全部 4 个指标回到 clean） | ⬜ |
| 12 | KV 编辑 | Bottom KeyValueEditor 字段编辑正常，值即时写入 Entity | ⬜ |
| 13 | 合并视图 | Merge View 下拉切换，覆盖链 / 字段来源 tooltip 正确 | ⬜ |
| 14 | 列可见性 | Settings → Column Visibility 勾选/取消 → DataGrid 列显隐生效 | ⬜ |
| 15 | Index 表 | 侧边栏 Index Tab 切换，正向/反向索引数据正确 | ⬜ |

### 验收 D：基础功能回归（6 项）

| 步骤 | 操作 | 检查点 | 结果 |
|:--:|------|--------|:--:|
| 1 | 语言切换 | 菜单 Language → English / 中文，UI 文本即时切换 | ⬜ |
| 2 | 通知 | 执行保存 / 导出 / 报错操作，Toast 通知正常弹出 | ⬜ |
| 3 | Settings 持久化 | 修改 GameRootDir / 语言 / 主题 / AutoSaveInterval → 重启 → 配置保留 | ⬜ |
| 4 | Profile 管理 | 创建 Profile → 添加 Mod → 保存 → 重新打开 Profile，数据正确 | ⬜ |
| 5 | `dotnet test` 全部 | 所有测试项目通过，0 Failure | ⬜ |
| 6 | 日志无异常 | 启动和运行期间无 FTL / Error 日志（Serilog 输出） | ⬜ |

---

## 结果汇总

| # | 验收项 | 结果 |
|---|--------|:--:|
| A1 | `bash build.sh` — 0 Error | ⬜ |
| A2 | `dotnet test` — 全部通过 | ⬜ |
| A3 | DataViewer 独立编译 — 0 引用 App | ⬜ |
| B1-B8 | Dirty State 一致性（8 项） | ⬜ |
| C1-C15 | DataViewer 基本功能（15 项） | ⬜ |
| D1-D6 | 基础功能回归（6 项） | ⬜ |

**自动化通过率**：0 / 3 | **Dirty 专项通过率**：0 / 8 | **功能通过率**：0 / 15 | **回归通过率**：0 / 6

**整体通过率**：0 / 32

---

## 已知残留项（后续处理）

- `GenericDataGridHelper` 仍被 14 个 EntityVisualizer + ReferenceInspector + DataExportService 引用 → M10 EntityEditor Plugin 拆分时处理
- `ViewServices.DataGridState` / `NavigationRouter` / `DataGridNavigationService` / `DataGridCellInteraction` 4 个 [Obsolete] 属性仍有引用 → M10 清零
- `ModGameDataTabsView`（View）仍在 App 中未迁至 DataViewer → 后续 Views 迁移轮次处理
- `DataTableViewModel._dirtyTabs` 和 `MarkTabDirty` / `ClearDirtyTabs` 方法为死代码（View 已有自己的实现）→ 后续清理

---

## 附录：Dirty State 架构 — 修复前后对比

### 修复前

```
编辑事件 (CellEdit / KVEdit / XmlEdit)
    ├── MarkTabDirty()         → tab._dirtyWasSet = true     (Tab 标题)
    ├── EditStore.EditedCells  → EditedEntityIds             (DataGrid 黄行)
    ├── WorkspaceSession.MarkEntityDirty()  → DirtyEntities  (KV banner)
    └── _vm.SetDirty(true)    → _vm._isDirty                (VM flag)

保存 (QuickSaveAsync)
    ├── ClearDirtyTabs()       → tab._dirtyWasSet = false
    ├── EditedCells.RemoveWhere(...) → PushEditStateToGrid
    ├── WorkspaceSession.ClearDirtyEntities()
    └── _vm.SetDirty(false)

单文档保存 (EntityEditorDocument.SaveDocument)
    └── MarkClean()            → DirtyEntities.Remove()      ← 只清除 DirtyEntities！
                                    tab dirty / EditedCells / VM flag 全部残留！
```

### 修复后

```
编辑事件 (CellEdit / KVEdit / XmlEdit)
    └── WorkspaceSession.MarkEntityDirty(id)
            ├── DirtyEntities.Add(id)
            └── Fire DirtyStateChanged
                    └── SyncDirtyViewState()
                            ├── Tab dirty ← DirtyEntities ∩ Tab.Entities
                            ├── EditedEntityIds ← DirtyEntities
                            └── _vm.SetDirty(hasDirty)

保存
    └── WorkspaceSession.ClearDirtyEntities() / RemoveDirtyEntities(...)
            └── Fire DirtyStateChanged → SyncDirtyViewState() → 所有 UI 同步清除
```

**Single Source of Truth**: `IWorkspaceSession.DirtyEntities`

---

## 测试环境

- OS: Windows 10 Pro 22H2 (19045)
- .NET SDK: 10.0.301
- Avalonia: 11.3.12
- 分支: main

## 测试执行

| 时间 | 测试人 | 结果 | 备注 |
|------|--------|------|------|
| | | | |
