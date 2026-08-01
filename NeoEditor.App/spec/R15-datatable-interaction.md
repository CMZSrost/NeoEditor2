# [R15] DataTable 交互矩阵

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 基石(DO) |
| **创建来源** | open-questions Q4b / 2026-06-29 用户确认 |
| **创建时间** | 2026-06-29 |

**是什么**
> Bottom DataTable 的交互按下列矩阵处理（区分点到「数据项行」还是「引用单元格」）：

| 操作 | 点在数据项行 | 点在引用 |
|------|-------------|---------|
| 单击 | 选中行（仅浏览高亮，**不**打开标签页、不改当前实体） | 选中行 |
| 双击 | Center 打开/跳转该实体标签页 | — |
| Ctrl+LMB | Center 打开该实体标签页（Navigate） | 跳转到引用目标（Navigate） |
| Ctrl+RMB | Peek 该数据项到右侧面板 | Peek 引用目标到右侧面板 |

**为什么**
> DataTable 主要用于数据浏览，浏览需求频繁。单击若直接打开标签页或改当前实体会打断浏览。
> 因此单击只做轻量选中（[R12](R12-selection-service.md) 当前实体仍以 Center 焦点为主），
> 需要进入编辑/查看时才用双击或 Ctrl 操作。Ctrl+LMB=Navigate / Ctrl+RMB=Peek 与全局
> 引用交互一致（[R04](R04-view-assembles-only.md)）。

**决策边界**
> 适用：Bottom DataTable（`ModGameDataTabsView` 等）。
> 不适用：Left KV / Center 内的引用交互（沿用 Ctrl+LMB Navigate / Ctrl+RMB Peek，无单击浏览语义）。
> 约束：单击选中**不**触发 [R12](R12-selection-service.md) 的当前实体变更，仅双击/Ctrl+LMB 打开标签页才会。
> 相关：[R08](R08-edit-entry-points.md) Bottom 只读；[N05](N05-no-bottom-editing.md) 禁原地编辑。

**实现要点**
> - 引用列处理通过 `GenericDataGridHelper.ConfigureColumn` 注册为 **Tunnel 阶段 +
>   `handledEventsToo:true`**，确保优先于 DataGrid 内部 `OnPointerPressed` 和行级
>   Bubble handler。
> - `SearchableDataGrid` 构造器额外注册 UserControl 级 Tunnel handler，在 DataGrid
>   内部处理**之前**提前设 `SuppressNextSelectionChanged=true`，防止 `SelectionChanged`
>   过早发送 `EntitySelectedMessage`（DataGrid 内部 `OnPointerPressed` 同为 Tunnel
>   阶段但晚于 UserControl 的 Tunnel handler）。
> - **`SuppressNextSelectionChanged` 必须在每次新点击周期开始时重置为 `false`**：
>   上次 Ctrl+LMB 引用列处理残留的标志会阻塞后续 Ctrl+RMB 数据行的行级 Peek。
>   Tunnel handler 对**所有点击**（含非 Ctrl）都执行重置以清理残留状态。
