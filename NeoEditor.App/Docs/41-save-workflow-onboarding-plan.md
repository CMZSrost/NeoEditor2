# 41 — 保存工作流收敛 + 非侵入式新手引导

> v2.5 · 2026-08-04 · ✅ **已实施 + 增补（pending_export 持久化 / 字段级 diff / AI Chat 渲染 / MCP 评审实施 / 五项追修订正，647/647 测试通过）**
> 来源：工作流学习成本评审 + 用户补充设计（自动保存事件驱动化、高亮 = "已缓存未导出"）
> 原则：**引导强度分级** — 强度 0 默认路径做对（设计消除概念）＞ 强度 1 静态内嵌提示 ＞ 强度 2 行为触发一次性提示 ＞ 强度 3 强制向导（避免）。一切提示一次性 + 可关闭 + 可重置，中级用户零打扰。

---

## 增补记录（v2.4 → v2.5）

### 增补 G：验收追修五项（2026-08-04）

| # | 反馈 | 修复 |
|---|------|------|
| 1 | 改 N 个实体，外部始终 "1 dirty" | 外部计数按**实体数**：`GetDirtyEntityIdsAsync(modId)` = pending_export ∪ WAL 窗口实体（ModId=-1 追加 `("game",0)` 目标），HomePage / ModDatabase / ModIndex / WorkspaceHistory 四处全部改走实体计数 |
| 2 | 字段级 dirty 全部可改字段都亮 | `EditStore.EditedCells` 正常路径按**精确列名**写入（KV/XML 消息按 `EditRecord.ColumnName`、DataGrid 按 `[Column]` 名、WAL 恢复按命令 `GetEditedCells()`）；`"*"` 通配仅剩两个兜底场景——WAL 恢复的 Add/Delete 命令、旧数据（见增补 A/C 订正） |
| 3 | 修复前写入的旧 pending 行整行黄 | **一次性自动升级**：打开工作区时对无列名的旧行，用磁盘游戏 XML 原始值 vs DB 当前值做字段级 diff（`DiffEngine.ComputeChangedColumns`）还原列名并写回（删除 NULL 行）——自愈，之后不再执行；任何失败回退保留 `"*"` 标记 |
| 4 | .php 保存含空格/回车导致游戏加载失败 | `GenerateModsPhp` / `GenerateImagePhp` 改**单行纯 `&` 连接**（`nRows=N&strModName0=X&strModURL0=...` / `nRows=N&nCols=2&strImageURL0=...`），值 trim 首尾空白；写盘默认 UTF-8 无 BOM（游戏按 URL query-string 解析，空格/回车进值 → 加载失败） |
| 5 | XML 编辑"差异对比"对 dirty 项无变化 | diff 旧侧改取**磁盘 XML 原始值**（dirty 实体在文档打开前已编辑，内存快照=当前 → 无差异）；且 `XmlContent`（XML 编辑内容）必须始终=当前值，不能随旧侧走磁盘原始（否则 diff 两侧仍相同） |

### 增补 A 订正（pending_export 现状，2026-08-04）

- 表结构：`pending_export`（ModId/EntityId/**ColumnName**/IsNew，唯一索引 **(ModId,EntityId,ColumnName)**；ColumnName NULL = 实体级标记，如新建行 / 升级前旧行）
- 写入：自动/Quick 保存后按 mod 分组 upsert，**每编辑列一行**（`BuildPendingExportEntries` 取 EditStore 精确列）
- 清除：`ShowMergeSavePreviewAsync` 确认写盘后 `ClearPendingExportsAsync(affectedModIds)`（Save & Export）；单实体升级用 `RemovePendingExportEntityAsync`
- 恢复：`RestorePendingExportsAsync` 按列恢复（NULL → `"*"` 兜底；isNew → 绿）；**旧 NULL 行一次性升级**（见增补 G-3，磁盘 XML diff 还原列名写回）
- ⚠ 徽章：ModDatabase / HomePage / ModIndex / WorkspaceHistory = `GetDirtyEntityIdsAsync(modId)` **实体计数**（不再是 mod 布尔）
- 老库迁移：`RunEditorDbMigrations` 建表（含 ColumnName）→ 老库 `ALTER TABLE` 补列 + 换唯一索引（DROP 旧索引）
- 测试：`WorkspacePendingExportTests` 4 个（upsert 幂等 / 多 mod 清除 / 每列标记计数 distinct / 移除单实体）

### 增补 C 订正（字段级 diff 现状，2026-08-04）

- `EditStore.EditedCells`：DataGrid / KV / XML 正常编辑路径按**精确列名**；`"*"` 通配仅剩两个兜底场景（WAL 恢复的 Add/Delete 命令、升级前旧数据恢复）
- **主键锚点**：`key:` 前缀参数——行有**任意**编辑即亮（`EditedCells.Any(EntityId)`，不再依赖 `"*"` 命中）
- 列名一致性：DataGrid 提交按 `[Column]` 名入栈（原用 Header=属性名，与转换器列名不一致）；整行黄底移除（提交重渲染时单元格级高亮即时生效）

### 增补 H：加载校验 / 布局修复 / 跨 profile 覆盖标注（2026-08-04）

- **加载时 DB vs XML 对比校正**（用户建议）：`ValidatePendingMarkersAsync` 对全部 pending 实体做磁盘原始 vs DB 当前 diff——有差异按精确列重建（含 legacy 升级），**无差异（编辑已撤销/改回）清除失效标记**，解析失败/新建实体保守保留；按文件缓存解析一次
- **XML 编辑器"放缩"实为布局问题**：AvaloniaEdit 12.0.0 无任何滚轮缩放逻辑；XML tab 由 DockPanel（仅末子元素填满）改 `Grid`（两编辑器都填满、IsVisible 切换）；三处 XML 编辑器加 `WordWrap="True"`（长行自动换行，不再横向滚动）
- **跨 profile 保存覆盖 = 已知限制（上升讨论）**：dirty 按 profile 隔离不泄露；但同一实体跨 profile 编辑时 `SaveAllAsync` 写同一 game.db → 后保存者覆盖先保存者。涉及架构设计（DB 单份实体 vs 多 profile 工作区），待讨论

### 增补 I：多 profile 支持（2026-08-04，**B+C 已实施**）

**实施内容**：
- **B（WAL 按 profile 隔离）**：`GetPersistenceTarget` 单 mod profile 改 `("profile", profileId)`；`MigrateWalTargetAsync` 迁移遗留命令（未保存的移动+重排序号，已保存的丢弃）
- **C（per-profile 编辑覆盖层）**：`profile_edits` 表（列级覆盖 + IsNew/IsDeleted 标记）——保存写覆盖层（`PersistEntitiesAsync` 基线 diff）、加载合并（`ApplyProfileOverlayAsync`）、导出后 `AdvanceBaselineAsync` 推进基线 + 清覆盖层；`ExportModAsync` 合并当前 profile 覆盖；**实体表=共享基线，两 profile 编辑同一实体互不覆盖**
- **关联组件贯通（2026-08-05 追修）**：`IHostService.MergeProfileOverlay` 统一合并入口，接入搜索（`SearchEntitiesAsync`）、MCP（`EditorTools`、`EntityResourceProvider`）、CLI（`CliCommandHandler`）的实体读取；`DiscardAsync` 同步清覆盖层（否则编辑重启复活）
- 方案 A（冲突检测）不再需要——C 的列级覆盖天然共存；`entity_versions` 表未实施
- 已知边界：游戏数据（ModId=-1）仍走 `("game", 0)` 共享 WAL；测试 653/653 通过

---

## 增补记录（v2.3 → v2.4）

### 增补 C：DataTable 字段级 diff（用户反馈，2026-08-04）

> ⚠ 通配/锚点描述已被增补 G/C 订正（正常路径按精确列名，`"*"` 仅兜底；锚点=行有任意编辑即亮）——以下为 v2.4 原方案记录。

- **背景**：行级黄高亮 → 用户要求字段级；且主键不可改、不会自己亮，需作行锚点。
- **方案**：
  - `CellEditedHighlightConverter`（DataViewer）：每列 `CellTemplate` 统一包装 Grid，`Background` 绑 `EntityId + Converter + 列名` → 查 `EditStore.EditedCells`（含 `"*"` 通配）；每次 DataGrid 重载（RefreshActiveDataGrid/滚动）重新求值
  - **主键锚点**：`key:` 前缀参数——行有编辑（`*` 命中）→ 主键单元格同步亮黄
  - 行级保留：覆盖整行灰 / 新建整行绿；编辑行行背景 null（仅单元格级）
- 已知取舍：CheckBox 列（bool）因列类型无 CellTemplate 不参与单元格高亮（主键锚点仍可定位）

### 增补 D：AI Chat 渲染与主题（用户反馈，2026-08-04）

- **默认 Markdown 渲染**：assistant 气泡 `SelectableTextBlock` → `lm:MarkdownRenderer`（LiveMarkdown 1.9.2，AiChat 项目新增包引用）；`ChatMessageItem.MarkdownBuilder` 随 Content 流式同步（Clear+Append）
- **主题修复**：`MarkdownTheme.axaml` 从无条件 VSCode Dark+ → **ThemeDictionaries**（Light：白底深字 `#1F1F1F`；Dark：原 Dark+）——白主题黑底灰字问题消除，MD 文档与 AI Chat 同步修复
- **复制按钮**：气泡头部 📋 → `CopyCommand`（剪贴板）

### 增补 E：MCP 工具评审实施（AI 评审建议，2026-08-04）

按投入产出比实施 4 项（工具数 16 → 19）：

| 工具 | 对应建议 |
|------|---------|
| `BatchEditEntity`（多字段一次编辑、原子 undo、校验前置） | 批量编辑（最痛） |
| `FindReferencingEntities`（反向索引：谁引用了我，删除前安全检查） | 反向引用（次痛） |
| `SearchAllTypes` query 改可选（空 query + filtersJson 纯过滤搜索） | 搜索增强 |
| `DiscardChanges`（清除单个实体暂存标记） | 工作流细节 |

未实施（后续）：GenerateImage 自定义 prompt/seed、ExportMod 完整 XML diff 预览、EditEntity 枚举值即时校验列表。

### 增补 F：验收修复（用户反馈，2026-08-04）

| # | 反馈 | 修复 |
|---|------|------|
| 1 | KV 编辑后 DataTable 不刷新、无高亮；Command Log / Session Dirty 空 | ① `RefreshEntityEditorMessage` 接收的 `if (ReadOnly) return;` **移除**（ReadOnly 只 gate CRUD 不 gate 刷新——底部 DataTable 实例 ReadOnly=true 是拦截根因）；② Command Log 空时提示"自动保存已落库并清 WAL（正常）"；③ Session Dirty 新增 **pending_export 摘要**（按 mod 统计未导出实体数） |
| 2 | 只读值（entity_id/file_path）过长截断 | 只读 TextBlock 加 `TextWrapping="Wrap"` |

---

## 增补记录（v2.2 → v2.3）

### 增补 A：pending_export 持久化（"未导出"状态重启不丢）

> ⚠ 部分描述已被增补 G/A 订正覆盖（表结构增加 ColumnName、恢复按列 + 旧行升级、徽章改实体计数、测试 4 个）——以下为 v2.3 原方案记录。

- **背景**：新语义下 dirty = "已存 DB 未导 XML"，但自动保存清 WAL 后该状态无任何持久化——重启后 ⚠/高亮消失（EditStore 会话级）。
- **方案**：新表 `pending_export`（ModId/EntityId/IsNew，唯一索引 (ModId,EntityId)）：
  - 写入：`AutoSaveAsync`/`QuickSaveAsync` 保存成功后按 mod 分组 upsert（IsNew 取自 `EditStore.NewEntityIds`）
  - 清除：`ShowMergeSavePreviewAsync` 确认写盘后 `ClearPendingExportsAsync(affectedModIds)`
  - 恢复：`RestorePendingExportsAsync`（加载完成后）→ 填 `EditStore`（`(eid,"*")` + 新建绿）——**不标 dirty**（已落库）
  - ⚠ 徽章：ModDatabase / HomePage / ModIndex / WorkspaceHistory 四处 = `HasUnsavedCommandsAsync || HasPendingExportsAsync`
  - 老库迁移：`RunEditorDbMigrations` 追加 `CREATE TABLE IF NOT EXISTS pending_export`
- 测试：`WorkspacePendingExportTests` 2 个（upsert 幂等 + 多 mod 清除）

### 增补 B：用户验收反馈四修（2026-08-04）

| # | 反馈 | 修复 |
|---|------|------|
| 1 | KV 只读字段重影（TextBlock 与编辑控件同时渲染） | `CtrlType` 强制 `ReadOnly`（`IsKey || IsMeta`），编辑控件不再渲染 |
| 2 | XML 不应显示 entity_id/file_path 与 `<?xml...?>` 声明；主键须显示但改后告警 | `GenerateXmlFragment` 过滤 IEntity 元数据列 + 去掉声明行；`ApplyXmlToEntity` 主键变更 → alert「Primary key cannot be changed」且不生效 |
| 3 | 列头说明应放 tooltip 而非列头本身 | 列头回退技术名；tooltip 保留说明；枚举 ≤6 时追加值域 |
| 4 | Debug Dock 应放 DataTable 旁 | `DefaultDock` Left → **Bottom** |

---

## 实施结果（相对 v2.1 的偏差）

| 项 | v2.1 计划 | 实际实施 |
|----|-----------|----------|
| P1.1 自动保存 | DirtyStateChanged 防抖 | ✅ `ScheduleAutoSave()`（挂 `SyncDirtyViewState`）+ `AutoSaveAsync()` 轻量落库（无 UI preparing 态）；`IsLoading` 作恢复期抑制标志（而非新增标志） |
| P1.2 高亮换源 | EditStore 派生 | ✅ `PushEditStateToGrid` → `EditedEntityIds = editStore.EditedCells.Select(EntityId)` |
| P1.3a QuickSave 清理后移 | 移除高亮清除块 | ✅ `Operations.cs` 移除；导出确认清理块（`Data.cs`）保留 |
| P1.3b SaveDocument | 移除 EditedCells.RemoveWhere | ✅ `EntityEditorDocument.cs` |
| P1.4 按钮/快捷键 | 删 Quick Save + Ctrl+Shift+S | ✅ `SaveAndExportRequestedMessage` → `DataTableViewModel` 注册 → View 挂 `ShowMergeSavePreviewAsync`；**无 `!_isDirty` 守卫**（自动保存清 dirty 后导出仍可达） |
| P3 first-merge-open | 首次进 Merge view 提示 | ❌ **裁剪**：B4 后 `_isMergeView` 恒 true（`axaml.cs:263`），该提示失去意义 |
| P3 first-game-entity-edit | 编辑 Game 实体提示 | ✅ 挂在 cell 编辑拦截分支（`OnCellEditCommitted`）；文案改为真实路径引导（Copy Row 复制到自己的 mod） |
| P3 first-export | 首次导出后提示 | ✅ `ShowMergeSavePreviewAsync` 成功路径 |
| P4 覆盖度审计 | 对照 Docs/38 补齐 | ✅ 由生成脚本保证（`field_descriptions.json` 由 `artifacts/gen-field-descriptions.js` 自 Docs/38 生成） |
| OnboardingHintService 单测 | 计划有 | ⚠️ 未建（App 层无测试项目）；语义简单，手工验证覆盖 |

---

## 一、目标

1. **消除"保存"概念**：编辑/增删自动落 DB（无感，如同缓存）；高亮表达"与游戏不一致"；用户唯一的显式动作 = Save & Export（写游戏）。
2. 空状态横幅升级为**三步叙事**，新手零文档走通。
3. 在**易错行为现场**给一次性提示（用错了才被教）。
4. 让已有的字段级文档（FieldDescriptions）**可见可发现**。

明确**不做**：Merge/Profile/Overlay 概念导览、强制向导、改变中级用户操作路径。

---

## 二、现状盘点（调研结论，决定改动范围）

**高亮机制已完整存在**，只缺语义/时机调整：

| 现状 | 位置 | 与新设计的关系 |
|------|------|------|
| 行高亮：淡绿 `(220,255,220)`=新建 / 淡黄 `(255,255,220)`=编辑 / 灰=被覆盖 | `SearchableDataGrid.axaml.cs:466-468` | ✅ 颜色即用户要的，不动 |
| 行高亮数据源 `EditedEntityIds = WorkspaceSession.DirtyEntities` | `ModGameDataTabsView.Tab.cs:64`（`PushEditStateToGrid`） | ❌ 自动保存会清 DirtyEntities → **改绑 EditStore 派生** |
| 单元格高亮：`EditStore.EditedCells` / 新行 `NewEntityIds` | `EditTrackingStore.cs`；`axaml.cs:510` | ✅ 保留；清除时机后移 |
| QuickSave 内清除高亮（`EditStore.RemoveWhere` / `NewEntityIds.Clear` / `PushEditStateToGrid` / `RefreshActiveDataGrid`） | `ModGameDataTabsView.Operations.cs:90-115` | ❌ **移除**（自动保存不再清高亮） |
| Save & Export 确认后清除高亮（`SetDirty(false)` / `ClearDirtyTabs` / `EditStore.Clear` / `RefreshActiveDataGrid`） | `ModGameDataTabsView.Data.cs:100-107`（`ShowMergeSavePreviewAsync`） | ✅ **已存在且位置正确**，保留 |
| 自动保存定时器（`AutoSaveTimer` + `AppConfig.AutoSaveInterval`，默认 0=关） | `DataTableViewModel.cs:139-148, 316-330` | ❌ 主机制改为**事件驱动**，定时器降为兜底 |
| Ctrl+S → `SaveRequestedMessage(CurrentTab)` | `MainWindow.axaml.cs:26-43` | ✅ 语义保留（缓存保存），不加导出 |
| WAL：命令级持久化 `command_log`，QuickSave 后 `ClearWorkspaceAsync` 清除 | `CommandHistory.cs:25-50`；`WorkspacePersistenceService.cs`；`Operations.cs:129` | ✅ 逻辑保留（自动保存后清 WAL 安全：已全部落库） |
| 所有编辑入口统一走 `ExecuteAsync → MarkEntitiesDirty → DirtyStateChanged` | `HostService.cs:105-142, 186-214` | ✅ **自动保存单挂点**：KV / XML / Add / Delete / Undo / Redo / CSV 导入全覆盖 |
| `SaveAllAsync` 落库 + 清 dirty 集（不清 undo 栈） | `HostService.cs:232-237, 822-868` | ✅ 架构零改动 |

**结论：HostService / WAL / Repository / Session 架构全部不动**（R24/R26 契约不变）。改动全部在 UI 层（View + 少量 VM/AppConfig）。

---

## 三、P1 — 保存语义重构（核心）

### 新心智模型（用户设计）

```
编辑/增删 ──自动保存(防抖)──→ DB（缓存，无感）
        └──→ 行/单元格高亮：黄=修改，绿=新建（"与游戏不一致"指示）
Save & Export（唯一按钮 / Ctrl+Shift+S）──→ DB + XML 写盘 + 清除全部高亮
```

### 3.1 自动保存：事件驱动（单挂点）

- **触发**：监听 `WorkspaceSession.DirtyStateChanged`，dirty 集非空时启动**防抖定时器（~800ms）**，到期执行 `SaveAllAsync()`（复用 `QuickSaveAsync` 的落库路径）。
- **单挂点覆盖全部入口**（都经 `ExecuteAsync` 标脏）：
  | 入口 | 现状 | 覆盖 |
  |------|------|:--:|
  | Left KV 失焦提交 | `axaml.cs:510` 附近 | ✅ |
  | Center XML Apply | `Operations.cs:513`（经 `_commandHistory`） | ✅ |
  | Add / Clone | `Operations.cs:452`（`ExecuteAsync`） | ✅ |
  | Delete | `Operations.cs:563`（`ExecuteAsync`） | ✅ |
  | **Undo / Redo**（undo 后 `MarkAffectedDirty`） | `HostService.cs:195, 210` | ✅ 自动保存自动补上 undo 后的 DB 同步 |
  | CSV 导入（`ExecuteBatchAsync`） | `ModDatabaseViewModel.cs:665` | ✅ |
- **守卫**：
  - `_isSavePreviewOpen`（导出预览期间不自动保存）；
  - WAL 恢复期抑制（见 4.3）；
  - **不加 `ReadOnly` 守卫**（订正 v1/v2.0 表述）——`ReadOnly` 只隐藏保存按钮（`axaml:157-167`），不禁止编辑路径；Data Browser 下编辑 Game 数据照常标脏落库，与现状 QuickSave 行为一致（现状 ReadOnly 时编辑也会写 WAL）。
- **兜底**：现有 `AutoSaveTimer`（`DataTableViewModel.cs:139-148`）保留（`AutoSaveInterval` 默认 60s），与防抖钩子**共用 `QuickSaveAsync` 落库路径**（`_vm.SaveRequested` → `QuickSaveAsync`，`axaml.cs:380`），防抖 + 定时器并存不冲突（都有 `_isSavePreviewOpen` 守卫）。
- **反馈**：无 toast（无感）；状态栏沿用现有 `Saved at HH:mm` 类提示（可降为 `Auto-saved`）；高亮本身即反馈。

### 3.2 高亮语义与数据源

- 语义改为 **"已缓存、未导出"**：修改 → 淡黄；创建 → 淡绿（颜色已存在，`SearchableDataGrid.axaml.cs:466-468`）。
- 数据源改绑：`PushEditStateToGrid` 中 `EditedEntityIds = WorkspaceSession.DirtyEntities`（`Tab.cs:64`）→
  `EditedEntityIds = editStore.EditedCells.Select(c => c.EntityId).ToHashSet()`（+ 与 `NewEntityIds` 语义分离，新建行绿）。
  - 理由：自动保存后 `DirtyEntities` 被 `SaveAllAsync` 清空（R26 契约不变），高亮若仍绑它会在自动保存后消失——必须换源。
  - `EditTrackingStore` 是 per-view 实例（`EditTrackingStore.cs:7`），会话级，导出成功后才 `Clear()`，语义吻合。

### 3.3 清除时机（只发生在 Save & Export 确认后）

- `ShowMergeSavePreviewAsync` 确认提交后（`Data.cs:100-107`）已有完整清理块 → **保留**。
- `QuickSaveAsync`（`Operations.cs:90-115`）中删除以下 UI 清除（仅保留落库 + `SaveCompletedMessage` + 清 WAL + `_dirtyTabs` 清理）：
  - `EditStore.EditedCells.RemoveWhere(...)` / `EditStore.NewEntityIds.Clear()`（:108-109）
  - `PushEditStateToGrid` / `RefreshActiveDataGrid`（:112-113）
- **订正：`EntityEditorDocument.SaveDocument` 也要改**——其单实体保存命令在 `MarkClean()` 后执行 `_dataTable.EditedCells.RemoveWhere(c => c.EntityId == entityId)`（`EntityEditorDocument.cs:110-113`），会提前抹掉单元格高亮。新语义下该行**移除**（单元格高亮只由导出后统一清）；`MarkClean()`（title `*`）保留。
- 删除实体无高亮需求（行消失）；其"未导出"态由 WAL 兜底（4.2）。

### 3.4 按钮与快捷键

| 项 | 行为 |
|----|------|
| 工具栏 | 删除 Quick Save 按钮（`ModGameDataTabsView.axaml:157-162`），只留 **Save & Export**（ToolTip 注明 Ctrl+Shift+S） |
| `Ctrl+S` | 保留现语义（`SaveRequestedMessage(CurrentTab)` → 缓存保存，不清高亮） |
| `Ctrl+Shift+S` | 新增：`MainWindow.axaml.cs` switch 先判 Shift → 新消息 `SaveAndExportRequestedMessage`（`ModGameDataMessages.cs`）→ `ModGameDataTabsView` 接收 → `ShowMergeSavePreviewAsync()` |

---

## 四、P1 联动调整（增删改查 + 保存全链路）

> 用户明确要求：command 回退等都要相应调整。以下为逐环节核对结论。

### 4.1 Undo / Redo（command 回退）

| 环节 | 现状 | 新语义下行为 |
|------|------|------|
| undo 后标脏 | `HostService.UndoAsync` → `MarkAffectedDirty`（`HostService.cs:195`） | ✅ 触发自动保存 → DB 同步 undo 结果（内存态与 DB 一致） |
| undo 后高亮 | `EditStore` 不清 | ✅ 保留黄（相对导出态仍有差异的近似指示）；undo 新建 → 行消失；undo 删除 → 行恢复（DB 未删，相对导出态无差异，可接受不精确） |
| undo 栈跨自动保存 | `SaveAllAsync` 只清 dirty 不清栈（`HostService.cs:866`） | ✅ 无需改动 |
| redo | 同 undo（`HostService.cs:201-214`） | ✅ 同上 |

### 4.2 WAL（command_log）与崩溃恢复

| 环节 | 现状 | 新语义下行为 |
|------|------|------|
| 命令写 WAL | `CommandHistory.Execute` 同步写（`CommandHistory.cs:35-49`） | ✅ 不变（防抖窗口内未落库命令仍有恢复保障） |
| 自动保存后清 WAL | `QuickSaveAsync` 内 `ClearWorkspaceAsync()`（`Operations.cs:129`） | ✅ 保留：SaveAllAsync 已落库全部 dirty，WAL 冗余，清掉防重启重放（现有注释同理由） |
| 崩溃恢复 | WAL 重放 `sequence > snapshot` 命令（R09） | ✅ 窗口缩小到防抖期（<1s）；重放后标脏 → 状态栏/⚠ 指示 → 用户 Save & Export 落盘 |
| ⚠ 徽章语义 | `HasUnsavedCommandsAsync`（WAL 非空） | ✅ 语义自然变为"有未落库编辑"（防抖窗口内短暂出现），合理 |

### 4.3 WAL 恢复抑制（新设计的唯一架构风险点）

- **风险**：WAL 重放填充 `DirtyEntities`（R09 要求）会触发 `DirtyStateChanged` → 自动保存钩子可能把**恢复出来的编辑**立刻落库（用户尚未查看）。
- **处理**：自动保存钩子设置**启用标志**——WAL 恢复完成后才置位（恢复发生在打开 Mod 的 `LoadModDataContextAsync` 链路，落地时定位确切完成回调/消息挂点）；恢复期 `DirtyStateChanged` 只更新 UI（tab `*` 号），不触发保存。

### 4.4 dirty 视觉指示语义分层（R09 更新）

| 指示器 | 旧语义 | 新语义 | 调整 |
|--------|--------|--------|:--:|
| 行/单元格高亮（黄/绿） | 未保存 | **未导出**（与游戏不一致） | 数据源改 EditStore 派生（3.2） |
| tab `*` 号 / KV 黄条 | 未保存 | 未落库（自动保存窗口内短暂出现） | 现有逻辑自动吻合（自动保存后 `SaveCompletedMessage` 清 `*`） |
| ⚠ 徽章（Sidebar/HomePage） | WAL 有未保存命令 | 未落库 | 自动吻合（4.2），无改动 |
| Override 灰（Merge） | 被覆盖 | 不变 | 无改动 |

### 4.5 删除（tombstone）

- `DeleteEntityCommand` 移除缓存 + 记录 tombstone；`SaveAsync` 时从后端删除（R26 §2）→ 自动保存后删除已落库，undo 恢复再自动保存 → DB 恢复。**无需改动**。

### 4.6 按钮可用性（已核实，无隐患）

- `CanStartSavePreview = !_isSavePreviewOpen && !IsPreparingSavePreview`（`Data.cs:279`）——**不依赖 dirty**，自动保存清 dirty 后 Save & Export 按钮保持可用 ✅（v2.0 曾担忧，核实后排除）。
- `SetDirty` → `_vm.SetDirty`（`DataTableViewModel.cs:156-160`）只驱动 Merge view dirty 指示，不影响按钮。自动保存（SaveScope.All）后 `SetDirty(false)` / `ClearDirtyTabs()`（`Operations.cs:105-107`）保留——清 `*` 号，符合 4.4 语义。

### 4.6 性能

- 每次编辑动作一次 `SaveAllAsync`（单/少量实体 upsert，SQLite 本地，~ms 级）；防抖 800ms 合并连续编辑；导出预览期间抑制。**无需额外优化**。

### 4.7 R11 兼容

- `SaveScope.CurrentTab`（Ctrl+S）语义保留：只落当前 tab 实体，不清任何高亮。

---

## 五、P2 — 空状态三步卡片（强度 1）

### 现状

`EmptyModBanner` 仅标题 + 提示 + `+ Add First Entity`（`ModGameDataTabsView.axaml:190-225`）。

### 改动

```
你的 Mod 还是空的 —— 三步完成第一个实体
  ①  + 添加第一个实体（选类型 + 选目标 XML 文件）
  ②  在左侧面板编辑字段（悬停字段名可查看含义）
  ③  点右上角 Save & Export 写入游戏
  编辑会自动保存，黄/绿高亮 = 改动还没写入游戏
[不再显示]
```

- `[不再显示]` → `AppConfig.EmptyModHintDismissed = true`（新增字段）；横幅 `IsVisible = IsEmptyMod && !Dismissed`。
- 文案走三个 resx。

---

## 六、P3 — 行为触发一次性提示（强度 2）

### 机制：`IOnboardingHintService`（App 层，DI 注入，禁静态状态 N01）

```csharp
public interface IOnboardingHintService
{
    bool TryShow(string hintKey);   // 未关闭过 → 标记并返回 true（调用方弹 toast）
    void Dismiss(string hintKey);
    void ResetAll();                // Settings 里"重置新手提示"
}
```

- 状态存 `AppConfig.DismissedHints`（`HashSet<string>`）；显示走 `ViewServices.Notification`。

### 提示表（自动保存无感化后删除了 first-autosave）

| key | 触发条件 | 文案 |
|-----|----------|------|
| `first-export` | 首次 Save & Export 成功后（`Data.cs:110` 附近） | "已写入游戏 XML ✅ 可点 ▶ 直接进游戏验证" |
| `first-merge-open` | 首次进入 Merge view | "同一实体可来自多个 Mod——表格显示合并结果，黄色 = 被覆盖的旧值" |
| `first-game-entity-edit` | 首次选中 ModId==-1（Game 来源）实体编辑 | "此实体来自游戏基础数据，保存将创建覆盖写入你的 Mod" |

> `first-merge-open` / `first-game-entity-edit` 文案落地时核对 `OverlayChainToolView` 实际视觉语义。

---

## 七、P4 — 字段级文档可见化（已部分存在）

### 现状（已核实）

`FieldDescriptions`（`NeoEditor.Core/Model`，嵌入 Docs/38）→ `KeyValueEditorViewModel.cs:145` 填充 Description → `KeyValueEditorView.axaml:78` hover ToolTip（300ms）。**机制已通，缺可发现性**。

### 改动

1. KV 每行字段名前加 `?` 图标（Description 非空时可见），ToolTip 挂图标（`KeyValueEditorView.axaml`）。
2. 对照 `Docs/38` 24 表审计 `FieldDescriptions` 覆盖度，补齐缺失。
3. `AddRowDialog` XML 路径选择旁加说明行："实体按 XML 文件分组存储，NeoScavenger 通过文件名覆盖游戏数据"。

---

## 八、P5 — 状态可见性确认

评审结论：现状已达标（Dirty 标记 / ⚠ 徽章 / Saved 时间戳 / Undo/Redo / 模式一致性），无开发项；唯一新增"可撤销"提示并入 P2。

---

## 九、实现顺序与任务清单

| 顺序 | 任务 | 依赖 | 估量 |
|:--:|------|------|:--:|
| 1 | **P1.1+P1.2 合并一步落地**：DirtyStateChanged 防抖自动保存 + 高亮数据源改 EditStore 派生（`Tab.cs:64`）——必须同步提交，否则中间态出现"自动保存一触发（清 DirtyEntities）→ 行高亮消失"的回归 | 无 | M |
| 2 | P1.3a QuickSaveAsync 移除高亮清除块（`Operations.cs:108-113`） | 1 | S |
| 3 | P1.3b `EntityEditorDocument.SaveDocument` 移除 `EditedCells.RemoveWhere`（`EntityEditorDocument.cs:113`） | 1 | S |
| 4 | P1.4 按钮/快捷键：删 Quick Save、加 Ctrl+Shift+S、新消息 | 无 | S |
| 5 | **验证编辑→高亮即时刷新链路**：确认编辑发生后 `OnPushEditState` / `RefreshRowBackgrounds` 的调用时机（`axaml.cs:372`、`SearchableDataGrid.axaml.cs:475`），确保换源后 KV/XML 编辑即时反映到行/单元格高亮 | 1 | S（验证为主） |
| 6 | P3 机制 `IOnboardingHintService` + AppConfig 字段 | 无 | S |
| 7 | P2 空状态三步卡片 | 6 | S |
| 8 | P3 三条触发点接线 | 1、4、6 | S |
| 9 | P4 `?` 图标 + 覆盖度审计 + AddRowDialog 说明 | 无 | S-M |
| 10 | Settings 重置提示按钮 + resx 全量文案 | 6 | S |

> 顺序理由：任务 1 是同一语义闭环（自动保存启用 + 高亮换源必须原子落地）；2-3 清除后移；5 是换源后的回归验证（现有链路 `EditedCells.Add → SetDirty` 已即时，重点确认行背景刷新仍走通）。

## 十、测试与验收

- **单元测试**：
  - `OnboardingHintService`：TryShow 首次 true 且仅一次 / Dismiss / ResetAll
  - `AppConfig` 序列化往返含新字段（`DismissedHints` / `EmptyModHintDismissed`）
  - 快捷键消息映射（提取 `ResolveSaveGesture(Key, KeyModifiers)` 可测）：Ctrl+S / Ctrl+Shift+S / 其他
- **集成/手工验证**（沿用 testround 惯例）：
  | 场景 | 期望 |
  |------|------|
  | KV 改字段失焦 | 黄高亮出现；~1s 后无 toast、状态栏 Auto-saved；高亮**保留** |
  | 新建实体 | 绿高亮；自动落库 |
  | 删除实体 | 行消失；自动落库（重启不复活） |
  | Undo（修改/新建/删除） | 高亮不消失；DB 同步 undo 结果（重启验证） |
  | 防抖窗口内连续编辑 | 只触发一次保存 |
  | Ctrl+S（当前 tab 粒度） | 只落当前 tab，**不清任何高亮**（含 Center 文档 title `*` 清除但行/单元格高亮保留） |
  | **Center 文档保存路径**（若入口可达） | 同 Ctrl+S：title `*` 清，单元格高亮**保留**（验证 `SaveDocument` 改动） |
  | 自动保存后 Save & Export 按钮 | **保持可用**（CanStartSavePreview 不依赖 dirty） |
  | Ctrl+Shift+S / Save & Export 按钮 | diff 预览 → 确认 → 全部高亮清除 → 首次导出 toast 一次 |
  | 取消 diff 预览 | 不落库、不清高亮、不写 XML（R26 事务语义） |
  | 编辑后立即强杀进程 | 重启：防抖窗口内命令经 WAL 恢复（tab `*` 号），自动保存未误触发（恢复期抑制） |
  | **重启后未导出修改** | 行高亮丢失（EditStore 会话级，已知边界）；但 Save & Export 的 diff 预览仍能检出并导出（ExportModAsync 从 DB 算 diff）——能力不丢 |
  | Data Browser（ReadOnly）编辑 | 与现状一致：编辑标脏落库，无按钮可点 |
  | Merge view | 覆盖行灰黄绿语义正确；Game 实体编辑首次提示一次 |
  | 切换 mod / 重开应用 | 高亮重置（EditStore 会话级），⚠ 按 WAL 判定 |
- **回归**：`dotnet test NeoEditor.sln`（重点：`AutoSaveInterval` 默认值变更对既有断言的检查；HostService 保存管线测试——应全绿，架构未动）

## 十一、不做的事（边界）

- ❌ 不做 Merge/Profile/Overlay 概念教学导览
- ❌ 不改 R24/R26 架构契约（HostService / Repository / WAL / Session 零改动）
- ❌ 不为高亮引入持久化（EditStore 会话级）。**已知边界**：重启后"未导出"行高亮丢失；但导出能力不丢——`ExportModAsync` 从 DB 计算 diff，未导出的修改在 Save & Export 预览中仍会检出
- ❌ 不做 undo 后的精确 diff 高亮（黄色近似保留，导出后统一清）
- ❌ 不做服务器端/跨机器提示同步
