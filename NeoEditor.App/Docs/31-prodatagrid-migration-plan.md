# 31 — ProDataGrid 迁移计划

> v1.1 · 2026-07-31 · ✅ **已完成**
> 基于 2026-07-30 DataGrid 全量使用分析
> 上承: [30-post-m12-development-plan.md](30-post-m12-development-plan.md)
> 目标: 将 DataViewer Plugin 中的 Avalonia DataGrid 替换为 ProDataGrid ✅ 已于 2026-07-31 完成

---

## 〇、动机

### 为什么换

| 维度 | Avalonia DataGrid | ProDataGrid |
|------|:---:|:---:|
| **列过滤** | 无内置 | ✅ 每列自带下拉过滤 |
| **列分组** | 无内置 | ✅ 拖拽分组 |
| **行分组/汇总** | 无内置 | ✅ GroupRow + SummaryRow |
| **单元格样式条件** | 需手写 LoadingRow | ✅ CellStyleSelector |
| **虚拟化滚动** | 基础虚拟化 | ✅ 高性能虚拟化（万行无压力） |
| **Excel 导出** | 需手写 | ✅ 内置 Excel/CSV 导出 |
| **列固定 (Frozen)** | 有限支持 | ✅ 左右固定列 |
| **复制粘贴** | 基础 ClipboardCopyMode | ✅ 增强选区 + 跨单元格复制 |
| **编辑体验** | 基础 | ✅ 下拉列表编辑、日期选择、 NumericUpDown 内嵌 |
| **主题** | Semi.Avalonia.DataGrid | ✅ Semi.Avalonia.ProDataGrid（同系列） |

NeoEditor 的 DataViewer 核心需求——列过滤、高性能虚拟化、引用列样式区分——ProDataGrid 都原生支持，可删除大量手写代码。

### NuGet 包

```
ProDataGrid 12.0.4                    (核心库, wieslawsoltes)
Semi.Avalonia.ProDataGrid 12.0.0.1   (Semi 主题, irihiTech)
```

> NeoEditor 已用 Semi.Avalonia 主题，ProDataGrid 的 Semi 主题直接兼容。

---

## 一、影响范围分析

### 1.1 需迁移的文件（按改动程度分组）

#### 🔴 重度改动（~5 文件）

| 文件 | 当前 | 迁移后 |
|------|------|--------|
| `DataViewer/Views/SearchableDataGrid.axaml` | `<DataGrid>` 元素 | `<ProDataGrid>` 元素，列过滤/分组自带 |
| `DataViewer/Views/SearchableDataGrid.axaml.cs` (580行) | DataGrid API + 手动排序/过滤/行样式 | 大量删除：排序→内置、过滤→内置、行背景→CellStyleSelector |
| `DataViewer/Services/ColumnTemplateFactory.cs` | DataGridTemplateColumn / DataGridTextColumn / DataGridCheckBoxColumn | ProDataGrid 列类型适配 |
| `App/Views/UserControls/ModGameDataTabsView.axaml.cs` | DataGrid 类型引用、FindActiveDataGrid()、SwitchTabItemsSource | ProDataGrid API 适配 |
| `DataViewer/Views/FindReplacePanel.axaml.cs` | DataGrid 类型引用、视觉树遍历 OfType<DataGridRow/Cell> | ProDataGrid 行/单元格类型适配 |

#### 🟡 中度改动（~4 文件）

| 文件 | 改动内容 |
|------|---------|
| `DataViewer/Services/DataGridInteractionState.cs` | `ColumnMetaCache` Key 从 DataGrid 改为 ProDataGrid |
| `App/Views/UserControls/ModGameDataTabsView.Data.cs` | `PushEditStateToGrid` 属性赋值适配 |
| `App/Views/UserControls/ModGameDataTabsView.Tab.cs` | DataGrid 交互适配 |
| `DataViewer/Views/IndexTableView.axaml` | 两个 DataGrid → ProDataGrid，手动列定义保留 |

#### 🟢 轻度改动（~6 文件）

| 文件 | 改动内容 |
|------|---------|
| `App/App.axaml` | `DataGridSemiTheme` → `ProDataGridSemiTheme`，`Fluent.xaml` → 移除或换 ProDataGrid 主题 |
| `App/Views/Dialog/ProfileDiffDialog.axaml` | DataGrid → ProDataGrid（只读对话框，简单绑定） |
| `App/Views/Dialog/CsvImportDiffDialog.axaml` | 同上 |
| `App/Views/Dialog/DependencyListDialog.axaml` | 同上 |
| `App/Views/Dialog/ConflictListDialog.axaml` | 同上 |
| `App/Views/UserControls/EditProfileView.axaml` | 拖拽行为需 ProDataGrid 兼容 DnD |

#### ⚪ 不直接依赖 DataGrid（无需改动）

- `DataTableService.cs` / `DataGridNavigationService.cs` / `DataGridCellInteractionService.cs` — 不直接持有 DataGrid 引用
- `GameDataTypeTabItem.cs` — ItemsSource 是 IEnumerable，类型无关

### 1.2 需移除的 NuGet 包

| 包 | 原因 |
|----|------|
| `Avalonia.Controls.DataGrid` 11.3.12 | 被 ProDataGrid 替代 |
| `Semi.Avalonia.DataGrid` 11.3.7.3 | 换 `Semi.Avalonia.ProDataGrid` |
| `Xaml.Behaviors.Interactions.DragAndDrop.DataGrid` 11.3.9.5 | 仅 EditProfileView 用，评估 ProDataGrid 内置 DnD 或替代 |

### 1.3 需新增的 NuGet 包

| 包 | 版本 | 用途 |
|----|------|------|
| `ProDataGrid` | 12.0.4 | 核心 DataGrid 控件 |
| `Semi.Avalonia.ProDataGrid` | 12.0.0.1 | Semi 主题适配 |

---

## 二、分阶段迁移策略

### Phase D1: 基础设施切换（先跑通编译）

| 步骤 | 内容 | 涉及文件 |
|:--:|------|---------|
| D1.1 | 添加 ProDataGrid + Semi.Avalonia.ProDataGrid NuGet 包 | DataViewer.csproj, App.csproj |
| D1.2 | App.axaml: 替换 DataGrid 主题引用 | App.axaml |
| D1.3 | SearchableDataGrid.axaml: `<DataGrid>` → `<ProDataGrid>` | 1 axaml |
| D1.4 | SearchableDataGrid.axaml.cs: 适配编译（API 差异最小集） | 1 cs |
| D1.5 | 5 个 Dialog: 替换 DataGrid 类型 | 5 axaml + 5 cs |
| **目标** | `dotnet build` 0 Error（功能可能不完整，但结构就位） | |

### Phase D2: 核心功能迁移

| 步骤 | 内容 | 涉及文件 |
|:--:|------|---------|
| D2.1 | **列过滤**：删除手写过滤逻辑，启用 ProDataGrid 内置 `ColumnFilter` | SearchableDataGrid.axaml + .cs |
| D2.2 | **排序**：删除 `OnSorting` 手动排序，换 ProDataGrid 内置排序 | SearchableDataGrid.axaml.cs |
| D2.3 | **行高亮**：`OnLoadingRow` / `RefreshRowBackgrounds` → CellStyleSelector | SearchableDataGrid.axaml.cs，新建 StyleSelector 类 |
| D2.4 | **列生成**：`OnAutoGeneratingColumn` → ProDataGrid 等价事件 | SearchableDataGrid.axaml.cs |
| D2.5 | **复制粘贴**：用 ProDataGrid 增强选区 + 内置剪切板 | ModGameDataTabsView.axaml.cs |
| **目标** | 核心 DataGrid 功能正常工作（排序/过滤/高亮/复制） | |

### Phase D3: 高级功能 & 清理

| 步骤 | 内容 | 涉及文件 |
|:--:|------|---------|
| D3.1 | **FindReplacePanel**：适配 ProDataGrid 行/单元格视觉树 | FindReplacePanel.axaml.cs |
| D3.2 | **ColumnManager**：列可见性切换适配 ProDataGrid 列 API | ModGameDataTabsView.axaml.cs |
| D3.3 | **IndexTableView**：两个只读 DataGrid 迁移 | IndexTableView.axaml + .cs |
| D3.4 | **EditProfileView DnD**：拖拽排序适配（ProDataGrid RowDrag 或替代） | EditProfileView.axaml + .cs |
| D3.5 | 移除旧 NuGet 包：Avalonia.Controls.DataGrid, Semi.Avalonia.DataGrid, Xaml.Behaviors DnD | 各 csproj |
| D3.6 | ColumnTemplateFactory: 重构为 ProDataGrid 列工厂 | ColumnTemplateFactory.cs |
| **目标** | 全部功能正常，旧代码清理完毕 | |

### Phase D4: 测试 & 文档

| 步骤 | 内容 |
|:--:|------|
| D4.1 | 更新 DataViewer.Tests（如涉及） |
| D4.2 | 更新 Integration.Tests |
| D4.3 | 更新 spec 规则（如有接口变化） |
| D4.4 | 性能基准测试（万行数据滚动帧率对比） |

---

## 三、关键风险 & 缓解

| 风险 | 影响 | 缓解 |
|------|:--:|------|
| **ProDataGrid API 不兼容** | 某些 Column API 不同 | Phase D1 先最小适配，D2 再逐功能替换 |
| **Semi 主题不完整** | 样式走样 | 保留 fallback 到 ProDataGrid 默认主题 |
| **DnD 不支持** | EditProfileView 拖拽失效 | 评估用 ProDataGrid 内置 `RowDrag` 或 Avalonia 通用 DragDrop |
| **引用列渲染** | 自定义 TemplateColumn 行为不同 | ColumnTemplateFactory 逐列适配，保留现有 FuncDataTemplate |
| **DataGridInteractionState 缓存失效** | ColumnMetaCache 键类型变化 | D2.4 同步更新 Key 类型 |

---

## 四、可删除的代码（预估）

| 文件 | 可删除行数 | 原因 |
|------|:--:|------|
| `SearchableDataGrid.axaml.cs` | ~200 行 | OnSorting 手动排序、OnLoadingRow 手动行样式、SortItems 反射排序 |
| `ModGameDataTabsView.axaml.cs` | ~50 行 | SwitchTabItemsSource 列重建逻辑 |
| `ColumnTemplateFactory.cs` | ~30 行 | 简化列类型处理 |
| `FindReplacePanel.axaml.cs` | ~20 行 | 视觉树遍历简化 |
| **合计** | **~300 行** | 维护负担大幅降低 |

---

## 五、时间估算

| Phase | 工作量 | 说明 |
|:-----:|:------:|------|
| D1 基础设施 | 1-2h | 包安装 + 编译适配 |
| D2 核心迁移 | 4-6h | 排序/过滤/高亮/复制/列生成 |
| D3 高级+清理 | 3-4h | 搜索面板/列管理/DnD/清理 |
| D4 测试 | 2-3h | 测试更新 + 性能验证 |
| **合计** | **10-15h** | 1.5-2 个工作日 |

---

## 六、实施记录（2026-07-31 完成）

### 关键修正

原始计划假设 ProDataGrid 有全新的 XAML 元素名和列类型，需大量改写。实际**ProDataGrid 是硬 fork**，保持完整 API 兼容：

- XAML `<DataGrid>` 元素名不变，列类型（`DataGridTextColumn` / `DataGridTemplateColumn` / `DataGridCheckBoxColumn`）不变
- Assembly 名仍为 `Avalonia.Controls.DataGrid.dll`
- 因此 **Dialog、EditProfileView、IndexTableView 等无需任何改动**

### 实际改动

| 文件 | 改动 |
|------|------|
| `NeoEditor.Plugins.DataViewer.csproj` | `Avalonia.Controls.DataGrid 11.3.12` → `ProDataGrid 11.3.11` |
| `NeoEditor.App.csproj` | `Avalonia.Controls.DataGrid 11.3.12` → `ProDataGrid 11.3.11`；`Semi.Avalonia.DataGrid 11.3.7.3` → `Semi.Avalonia.ProDataGrid 11.3.9-beta.1` |
| `App.axaml` | `DataGridSemiTheme` → `ProDataGridSemiTheme`；移除 Fluent DataGrid theme（`avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml` 在 ProDataGrid 中不存在，Semi 主题已覆盖） |
| `ModGameDataTabsView.Tab.cs` | `SwitchTabItemsSource`: ~55 行（AutoGenerateColumns hack + 恢复逻辑）→ 3 行直接赋值；`OnTabChanged`: 移除事件重绑定 |
| `SearchableDataGrid.axaml.cs` | `OnSorting`: 移除 Dispatcher + AutoGenerateColumns hack（~12 行） |

### 版本选择

| 包 | 版本 | 原因 |
|----|------|------|
| `ProDataGrid` | 11.3.11 | 兼容 Avalonia 11.3（12.0.4 需 Avalonia 12） |
| `Semi.Avalonia.ProDataGrid` | 11.3.9-beta.1 | 唯一兼容 11.3 的版本（12.0.0.1 需 Avalonia 12） |

### 结果

- ✅ **296/296 测试全过**
- ✅ **0 Error 0 Warning（排除 NU1903）**
- ✅ **净删 ~60 行 hack 代码**
- ✅ **ProDataGrid 修复了列生命周期 bug**——SwitchTabItemsSource 不再需要 AutoGenerateColumns 开关
