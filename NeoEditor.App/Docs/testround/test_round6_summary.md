# 架构测试第6轮 — M9 DataViewer Plugin 拆分

> 日期：2026-07-26 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round5_summary.md](test_round5_summary.md) (M8 收束 + 2 Bug 修复 ✅)
> 后续：[test_round7_summary.md](test_round7_summary.md) (M10 EntityEditor Plugin 拆分)

## 本轮目标

M8 完成后进入 M9——将 DataViewer（数据表格浏览/导航/搜索/Peek）从 `NeoEditor.App` 拆分为独立 Plugin `NeoEditor.Plugins.DataViewer`。

核心任务：

1. **创建 `NeoEditor.Plugins.DataViewer` 项目** — 引用 Core / Infra / UI.Common，0 引用 App
2. **迁移 ~28 个文件**（~5,850 行 C#/XAML）从 App 到 DataViewer Plugin
3. **拆分 `GenericDataGridHelper`**（837 行静态类）→ `DataTableService` + `ColumnTemplateFactory` + `InteractionHandler`
4. **消除 ViewServices 过渡依赖** — View code-behind 改为构造注入或 Plugin DI 解析
5. **创建 `DataViewer.Tests`** — 独立运行，不依赖 App

> 本轮是 M9 的第 1 个 Plugin 拆分，验证插件架构可行性。

---

## 前置条件

- [x] `bash build.sh` 编译通过（0 Error）
- [x] `dotnet test` 19/19 通过（6 个测试项目）
- [x] 编辑器启动 + 基本功能正常
- [x] Settings 页持久化正常（Bug A 已修复）
- [x] DataGrid 脏数据高亮正常（Bug B 已修复）

---

## 任务 1：创建 DataViewer Plugin 项目

### 迁移文件清单（13 组，约 28 个文件）

| # | 当前 App 路径 | DataViewer 目标位置 | 说明 |
|---|--------------|-------------------|------|
| 1 | `Helper/GenericDataGridHelper.cs` | `Services/`（拆为 3 文件） | **拆分为** `DataTableService` + `ColumnTemplateFactory` + `InteractionHandler` |
| 2 | `Views/UserControls/SearchableDataGrid.axaml` + `.cs` | `Views/` | DataGrid 控件（574 行 code-behind） |
| 3 | `Views/UserControls/ModGameDataTabsView.axaml` + `.cs` + 3 partial | `Views/` | 主表格视图（4 文件 ~3,485 行） |
| 4 | `ViewModels/MainContent/ModGameDataTabsViewModel.cs` | `ViewModels/DataTableViewModel.cs` | 表格 VM |
| 5 | `ViewModels/MainContent/ModDataToolViewModel.cs` | `ViewModels/` | 工具栏上下文 VM |
| 6 | `Services/DataGridInteractionState.cs` | `Services/` | 交互状态 |
| 7 | `Services/IDataGridCellInteractionService.cs` + `DataGridCellInteractionService.cs` | `Services/` | 单元格交互 |
| 8 | `Services/NavigationRouter.cs` + `Infra/Helper/INavigationRouter.cs` | `Services/` | 导航路由 |
| 9 | `Services/IDataGridNavigationService.cs` + `DataGridNavigationService.cs` | `Services/` | DataGrid 导航 |
| 10 | `Views/UserControls/PeekPanelView.axaml` + `.cs` + `ViewModels/MainContent/PeekPanelViewModel.cs` | `Views/` + `ViewModels/` | Peek 面板 |
| 11 | `Views/UserControls/IndexTableView.axaml` + `.cs` + `ViewModels/MainContent/IndexTableViewModel.cs` | `Views/` + `ViewModels/` | 索引表 |
| 12 | `Views/UserControls/FindReplacePanel.axaml` + `.cs` | `Views/` | 查找替换 |
| 13 | `Views/UserControls/SearchResultsView.axaml` + `.cs` | `Views/` | 搜索结果 |
| 14 | `Infra/Helper/ColumnVisibilityKeys.cs` | `Services/` | 列可见性配置 |

### 项目结构目标

```
NeoEditor.Plugins.DataViewer/
├── NeoEditor.Plugins.DataViewer.csproj
├── DataViewerPlugin.cs                  (IPlugin 入口)
├── Services/
│   ├── DataTableService.cs              (from GDH: 列配置 + Store 管理)
│   ├── ColumnTemplateFactory.cs         (from GDH: ADF 模板生成)
│   ├── InteractionHandler.cs            (from GDH: Ctrl+Click/导航/Peek)
│   ├── NavigationRouter.cs              (from App/Services/)
│   ├── DataGridInteractionState.cs      (from App/Services/)
│   ├── DataGridNavigationService.cs     (from App/Services/)
│   ├── DataGridCellInteractionService.cs(from App/Services/)
│   └── ColumnVisibilityKeys.cs          (from Infra/Helper/)
├── ViewModels/
│   ├── DataTableViewModel.cs            (ModGameDataTabsViewModel 改名)
│   ├── ModDataToolViewModel.cs
│   ├── PeekPanelViewModel.cs
│   ├── IndexTableViewModel.cs
│   └── SearchResultViewModel.cs         (从 BottomToolsViewModel 提取)
└── Views/
    ├── DataTableView.axaml              (ModGameDataTabsView 改名 + 合并 SearchableDataGrid)
    └── (其余 Views 保持原名)
```

---

## 任务 2：拆分 GenericDataGridHelper

### 拆分为 3 个可注入 Service

| 新类 | 职责 | GDH 原始方法 |
|------|------|-------------|
| **DataTableService** | 列配置生成、Store 读写、引用查找、EntityMergedIds | `ConfigureColumn`, `SetActiveStores`, `ReferenceLookups`, `EntityMergedIds` 等 |
| **ColumnTemplateFactory** | DataGrid 列模板生成（ADF 绑定、编辑器选择器） | 模板构建逻辑 |
| **InteractionHandler** | 单元格交互：Ctrl+Click 导航、Ctrl+RMB Peek | 事件 handler 注册 + 分发逻辑 |

### 转换要点

- 静态类 → `sealed class`，`static` 方法 → 实例方法
- `App.*` 引用 → 构造注入对应接口
- 24 个 `Converter` 对 GDH 的静态引用 → 改为引用 `DataTableService`（构造注入或通过 PluginContext 获取）

---

## 任务 3：消除 ViewServices 过渡依赖

DataViewer 内 View code-behind 当前使用 `ViewServices.XXX`：

| 文件 | ViewServices 引用 | M9 处理 |
|------|------------------|---------|
| `SearchableDataGrid.axaml.cs` | `ViewServices.Loc`, `ViewServices.NavigationRouter` 等 | 改为 Plugin DI / 构造注入 |
| `FindReplacePanel.axaml.cs` | `ViewServices.*` | 同上 |
| `ModGameDataTabsView.axaml.cs` | `ViewServices.*` | 同上 |

> `ViewServices` 作为 App Shell 的服务定位器，Plugin 层不应依赖。M9 Plugin 需走 `IPluginContext` 获取服务。

---

## 任务 4：创建 DataViewer.Tests

| # | 项目 | 引用 |
|---|------|------|
| 1 | `Tests/NeoEditor.Plugins.DataViewer.Tests/` | `NeoEditor.Plugins.DataViewer` + Core/Infra/UI.Common |

**测试覆盖**（初始目标 ~5-8 个）：
- `DataTableService` 列配置生成正确性
- `ColumnTemplateFactory` 模板输出验证
- `InteractionHandler` 导航解析
- `DataViewerPlugin` 注册/激活
- (后续补充) DataTableViewModel 集成测试

---

## 结果汇总

| # | 验收项 | 结果 |
|---|--------|:--:|
| 1 | DataViewer 项目创建 + 0 Error | ⬜ |
| 2 | ~28 文件迁移 + 命名空间/using 更新 | ⬜ |
| 3 | GenericDataGridHelper 拆分为 3 Service | ⬜ |
| 4 | 24 Converter 对 GDH 引用改为注入 | ⬜ |
| 5 | View code-behind 去 ViewServices 依赖 | ⬜ |
| 6 | DataViewer.Tests 独立编译 | ⬜ |
| 7 | App 内原有文件删除（迁移后） | ⬜ |
| 8 | `bash build.sh` — 0 Error（含 Plugin 项目） | ⬜ |
| 9 | `dotnet test` 全部通过 | ⬜ |
| 10 | 编辑器启动 + DataViewer 功能正常 | ⬜ |
| 11 | 19->N 个新测试通过（含 DataViewer.Tests） | ⬜ |

**代码通过率**：0 / 11 | **整体通过率**：0 / 11

---

## 改动统计

| 类别 | 文件数（预估） | 说明 |
|------|:--:|------|
| 新建 Plugin 项目 | 1 | `NeoEditor.Plugins.DataViewer.csproj` |
| 新建 Plugin 入口 | 1 | `DataViewerPlugin.cs` |
| 迁移 ViewModel | 5 | DataTableViewModel, ModDataToolViewModel, PeekPanelViewModel, IndexTableViewModel, SearchResultViewModel |
| 迁移 View (axaml+cs) | ~12 | DataTableView, SearchableDataGrid, PeekPanelView, IndexTableView, FindReplacePanel, SearchResultsView |
| 迁移/拆分 Service | 7 | GDH→3 + NavigationRouter + DataGridInteractionState + DataGridNavigationService + DataGridCellInteractionService |
| 迁移杂项 | 1 | ColumnVisibilityKeys |
| Converter 适配 | ~24 | GDH 静态引用 → DataTableService 注入 |
| App 侧删除/清理 | ~30 | 迁移走的所有原文件 + using 清理 |
| 新建测试项目 | 1 | `Tests/NeoEditor.Plugins.DataViewer.Tests/` |
| App.sln 更新 | 1 | 加 Plugin + Tests 项目引用 |
| App DI 注册更新 | 1 | 注册 Plugin + 适配 App 残留引用 |

**预估修改文件 ~80，新建 ~2。**

---

## 残留项（M10 后续处理）

- `GenericDataGridHelper` 拆分后，原静态类删除或标记 `[Obsolete]`
- `ViewServices` 中 DataViewer 相关属性（`DataGridState`, `NavigationRouter`）标记废弃
- `BottomToolsViewModel` — `SearchResultViewModel` 提取后，原搜索结果逻辑清理
- `App.axaml.cs` 5 个 static 属性 — 继续保留（纯 App Shell 内部使用）
- Converter 目录 — `OverlayChainConverter` / `ModNameColumnConverter` / `FieldSourceConverter` 等与 DataViewer 耦合的 Converter，后续视需要移入 Plugin 或通过抽象接口解耦

---

## 下轮验收清单（test_round6 验收 10 + 11）

### 验收 10：DataViewer 基本功能

| 步骤 | 操作 | 检查点 |
|:--:|------|--------|
| 1 | 启动编辑器 | 启动正常，欢迎页完整 |
| 2 | 打开 Profile → Browse Game Data | DataTable 打开，列头显示，数据行显示 |
| 3 | 表切换 | 不同数据类表正常切换 |
| 4 | 排序 | 点击列头排序正常 |
| 5 | 过滤 | Filter 输入框过滤正常 |
| 6 | 搜索 | Ctrl+F 打开 FindReplace，搜索正常 |
| 7 | 单击行 | 行高亮，Bottom 详情正常 |
| 8 | 双击行 | EntityEditorDocument 打开（M10 目标） |
| 9 | Ctrl+Click 字段 | 跳转到引用实体（导航正常） |
| 10 | Ctrl+RMB 行 | Peek 面板弹出 |
| 11 | 修改字段 → Ctrl+S | 保存成功，脏标记一致 |
| 12 | KV 编辑 | 正常 |
| 13 | 合并视图 | Merge View 正常 |
| 14 | 列可见性 | Settings → Column Visibility 配置生效 |
| 15 | Index 表 | 侧边栏 Index 切换正常 |

### 验收 11：回归检查

| 步骤 | 操作 | 检查点 |
|:--:|------|--------|
| 1 | Settings 持久化 | 改 GameRootDir → 重启 → 路径保留 |
| 2 | 脏数据一致性 | DataGrid 高亮 ↔ Value Editor Alert ↔ Ctrl+S 状态一致 |
| 3 | 旧 NeoEditor.Tests | 8/8 通过 |
| 4 | 所有其他测试 | N/N 全部通过 |
