# 架构测试第7轮 — M9 DataViewer Plugin 收束 + 功能验收

> 日期：2026-07-26 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round6_summary.md](test_round6_summary.md) (M9 DataViewer Plugin 拆分)
> 后续：[test_round8_summary.md](test_round8_summary.md) (M10 EntityEditor Plugin 拆分)

## 本轮目标

M9 第 1 阶段（Service 层 + Converter 迁移）已完成。本轮完成第 2 阶段：**App 集成 + Views/ViewModels 迁移 + 功能验收**。

核心任务：

1. **App 引用 DataViewer Plugin** — 添加 ProjectReference，删除 App 中已迁移的旧文件，更新 ~50 处 using 引用
2. **迁移 Views 到 DataViewer** — `ModGameDataTabsView`（4 partial）、`SearchableDataGrid`、`PeekPanelView`、`IndexTableView`、`FindReplacePanel`、`SearchResultsView`
3. **迁移 ViewModels 到 DataViewer** — `ModGameDataTabsViewModel`→`DataTableViewModel`、`PeekPanelViewModel`、`IndexTableViewModel`、`ModDataToolViewModel`、从 `BottomToolsViewModel` 提取 `SearchResultViewModel`
4. **消除 ViewServices 依赖** — View code-behind 中的 `ViewServices.XXX` 改为构造注入或 `IPluginContext.Services` 解析
5. **功能验收** — 15 项 DataViewer 功能 + 4 项回归检查

> 本轮是 M9 的收束轮。完成后 DataViewer 作为独立 Plugin 运行，App 中不再保留 DataViewer 代码。

---

## 前置条件

- [x] `bash build.sh` 编译通过（12 项目，0 Error）
- [x] `dotnet test` 21/21 通过（6 个测试项目）
- [x] `NeoEditor.Plugins.DataViewer` 项目独立编译（0 引用 App）
- [x] GDH 拆分为 `DataTableService` + `ColumnTemplateFactory` + `InteractionHandler`
- [x] 5 个 Service + 5 个 Converter 已迁移，命名空间已更新
- [x] DataViewer.Tests 10/10 通过

---

## 任务 1：App 集成 DataViewer Plugin

### 1.1 添加 ProjectReference

```
NeoEditor.App.csproj 新增:
  <ProjectReference Include="..\NeoEditor.Plugins.DataViewer\NeoEditor.Plugins.DataViewer.csproj" />
```

### 1.2 删除 App 中已迁移的旧文件

| # | 文件 | 已在 DataViewer 中的对应 |
|---|------|------------------------|
| 1 | `Services/NavigationRouter.cs` | `Services/NavigationRouter.cs` |
| 2 | `Services/DataGridInteractionState.cs` | `Services/DataGridInteractionState.cs` |
| 3 | `Services/DataGridNavigationService.cs` | `Services/DataGridNavigationService.cs` |
| 4 | `Services/DataGridCellInteractionService.cs` | `Services/DataGridCellInteractionService.cs` |
| 5 | `Services/IDataGridCellInteractionService.cs` | `Services/IDataGridCellInteractionService.cs` |
| 6 | `Helper/Converter/FieldSourceConverter.cs` | `Converters/FieldSourceConverter.cs` |
| 7 | `Helper/Converter/FieldConflictBackgroundConverter.cs` | `Converters/FieldConflictBackgroundConverter.cs` |
| 8 | `Helper/Converter/EntityMergedIdConverter.cs` | `Converters/EntityMergedIdConverter.cs` |
| 9 | `Helper/Converter/ModNameColumnConverter.cs` | `Converters/ModNameColumnConverter.cs` |
| 10 | `Helper/Converter/OverlayChainConverter.cs` | `Converters/OverlayChainConverter.cs` |
| 11 | `Infra/Helper/ColumnVisibilityKeys.cs` | `Services/ColumnVisibilityKeys.cs` |

### 1.3 更新 ~50 处 using 引用

涉及文件类型：

| 类别 | 文件数 | 典型改动 |
|------|:--:|------|
| View code-behind | 8 | `using NeoEditor.Services;` → 加 `using NeoEditor.Plugins.DataViewer.Services;` |
| ViewModel | 6 | 同上 |
| Visualizer (Editor 相关) | 14 | `GenericDataGridHelper.EntityModNames` → 待后续 M10 处理 |
| Converter 引用处 | 3 | `new FieldSourceConverter()` → 全限定名或 using 更新 |
| App 启动/DI | 3 | DI 注册改为 DataViewer 中的实现类 |
| 其他引用 | ~10 | 逐个排查 |

> 注意：`NeoEditor.Services` 命名空间下仍有 Infra 的类型（`IWorkspaceSession`、`EntityMergeStore` 等），不能删除此 using。只需**新增** `using NeoEditor.Plugins.DataViewer.Services;`。

### 1.4 App DI 注册更新

```csharp
// App.axaml.cs / ServiceCollectionExtensions — 新增：
services.AddSingleton<NeoEditor.Plugins.DataViewer.Services.DataGridInteractionState>();
services.AddSingleton<NeoEditor.Plugins.DataViewer.Services.IDataGridNavigationService,
    NeoEditor.Plugins.DataViewer.Services.DataGridNavigationService>();
services.AddSingleton<NeoEditor.Plugins.DataViewer.Services.IDataGridCellInteractionService,
    NeoEditor.Plugins.DataViewer.Services.DataGridCellInteractionService>();
services.AddSingleton<NeoEditor.Plugins.DataViewer.Services.DataTableService>();
services.AddSingleton<NeoEditor.Plugins.DataViewer.Services.ColumnTemplateFactory>();
services.AddSingleton<NeoEditor.Plugins.DataViewer.Services.InteractionHandler>();
// NavigationRouter 替换旧的 App 内实现
services.AddSingleton<INavigationRouter,
    NeoEditor.Plugins.DataViewer.Services.NavigationRouter>();
// Plugin 注册
services.AddPlugin<DataViewerPlugin>();
```

---

## 任务 2：迁移 Views 到 DataViewer

### 迁移清单

| # | 源文件（App） | 目标（DataViewer） | 行数 |
|---|-------------|-------------------|:---:|
| 1 | `Views/UserControls/SearchableDataGrid.axaml` + `.cs` | `Views/SearchableDataGrid.axaml` + `.cs` | 575 |
| 2 | `Views/UserControls/ModGameDataTabsView.axaml` | `Views/DataTableView.axaml` | ~80 |
| 3 | `Views/UserControls/ModGameDataTabsView.axaml.cs` | `Views/DataTableView.axaml.cs` | 1,542 |
| 4 | `Views/UserControls/ModGameDataTabsView.Data.cs` | `Views/DataTableView.Data.cs` | 1,276 |
| 5 | `Views/UserControls/ModGameDataTabsView.Operations.cs` | `Views/DataTableView.Operations.cs` | ~300 |
| 6 | `Views/UserControls/ModGameDataTabsView.Tab.cs` | `Views/DataTableView.Tab.cs` | 482 |
| 7 | `Views/UserControls/PeekPanelView.axaml` + `.cs` | `Views/PeekPanelView.axaml` + `.cs` | 76 |
| 8 | `Views/UserControls/IndexTableView.axaml` + `.cs` | `Views/IndexTableView.axaml` + `.cs` | 21 |
| 9 | `Views/UserControls/FindReplacePanel.axaml` + `.cs` | `Views/FindReplacePanel.axaml` + `.cs` | 361 |
| 10 | `Views/UserControls/SearchResultsView.axaml` + `.cs` | `Views/SearchResultsView.axaml` + `.cs` | 42 |

### 每文件改动要点

#### SearchableDataGrid.axaml.cs
```
ViewServices.Loc                  → 构造注入 ILocalizationService
ViewServices.LoggerFactory        → 构造注入 ILoggerFactory
ViewServices.ConfigService        → 构造注入 IConfigService
ViewServices.DataGridState        → 构造注入 DataGridInteractionState
ViewServices.SelectionService     → 构造注入 ISelectionService
GenericDataGridHelper.ConfigureColumn → ColumnTemplateFactory.ConfigureColumn
GenericDataGridHelper.SetActiveStores → DataTableService.SetActiveStores
GenericDataGridHelper.*           → DataTableService.Instance.* 或构造注入
```

#### ModGameDataTabsView → DataTableView（4 partial）
```
ViewServices.ConfigService        → 构造注入
ViewServices.GameDbFactory        → 构造注入
ViewServices.EditorDbFactory      → 构造注入
ViewServices.XmlParser            → 构造注入
ViewServices.WorkspacePersistence → 构造注入
ViewServices.NavigationRouter     → 构造注入 INavigationRouter
ViewServices.ProfileManager       → 构造注入
ViewServices.ModManager           → 构造注入
ViewServices.MergeService         → 构造注入
ViewServices.WorkspaceSession     → 构造注入 IWorkspaceSession
ViewServices.ReferenceResolver    → 构造注入 IReferenceResolver
ViewServices.Notification         → 构造注入 INotificationService
ViewServices.Loc                  → 构造注入 ILocalizationService
GenericDataGridHelper.*           → DataTableService.Instance.* 或构造注入
```

#### FindReplacePanel.axaml.cs
```
ViewServices.Loc                  → 构造注入
ViewServices.Notification         → 构造注入
```

#### SearchResultsView.axaml.cs / PeekPanelView.axaml.cs / IndexTableView.axaml.cs
```
ViewServices.NavigationRouter     → 构造注入 INavigationRouter
ViewServices.Loc                  → 构造注入
```

### 构造注入策略

Avalonia View 的构造注入有两种方式：

1. **通过 `IPluginContext.Services` 解析**（推荐）— View 构造时接收 `IServiceProvider`，手动 `GetRequiredService<T>()`
2. **App Shell 在创建 View 时注入** — `DocumentWorkspaceViewModel` 通过 DI 创建 View 实例

> 本轮优先用方案 1，与现有 `ViewServices` 模式最接近，改动最小。后续 M12 可优化为方案 2。

---

## 任务 3：迁移 ViewModels 到 DataViewer

| # | 源文件（App） | 目标（DataViewer） | 主要改动 |
|---|-------------|-------------------|---------|
| 1 | `ViewModels/MainContent/ModGameDataTabsViewModel.cs` | `ViewModels/DataTableViewModel.cs` | 类名改为 `DataTableViewModel` |
| 2 | `ViewModels/MainContent/ModDataToolViewModel.cs` | `ViewModels/ModDataToolViewModel.cs` | 仅命名空间 |
| 3 | `ViewModels/MainContent/PeekPanelViewModel.cs` | `ViewModels/PeekPanelViewModel.cs` | 移除 `ViewServices` 依赖 |
| 4 | `ViewModels/MainContent/IndexTableViewModel.cs` | `ViewModels/IndexTableViewModel.cs` | 移除 `ViewServices` 依赖 |
| 5 | `ViewModels/MainContent/BottomToolsViewModel.cs` | 拆分出 `ViewModels/SearchResultViewModel.cs` | 提取搜索结果相关逻辑 |

---

## 任务 4：消除 ViewServices 残余引用

迁移完成后，App 内残留的 View code-behind 不应再引用 DataViewer 专属的 `ViewServices` 属性：

| ViewServices 属性 | M9 后状态 |
|-------------------|----------|
| `DataGridState` | 标记 `[Obsolete]`，改为从 DataViewer Plugin 解析 |
| `NavigationRouter` | 标记 `[Obsolete]`，改为从 DataViewer Plugin 解析 |
| `DataGridNavigationService` | 标记 `[Obsolete]` |
| `DataGridCellInteraction` | 标记 `[Obsolete]` |
| `Loc`, `ConfigService`, `LoggerFactory` 等 | 保留（App Shell 通用服务） |

---

## 任务 5：GenericDataGridHelper 清理

- [ ] `GenericDataGridHelper.cs` 所有方法改为 `[Obsolete("Use DataTableService / ColumnTemplateFactory / InteractionHandler instead.")]`
- [ ] 确认 App 内无编译引用 → 删除该文件
- [ ] 确认 Infra 内 `INavigationRouter` 接口保持不变（仍在 `NeoEditor.Helper` 命名空间）

---

## 结果汇总

| # | 验收项 | 结果 |
|---|--------|:--:|
| 1 | App 引用 DataViewer Plugin，编译 0 Error | ✅ |
| 2 | App 中 11 个已迁移文件删除 | ✅ |
| 3 | ~50 处 using 引用更新正确 | ✅ |
| 4 | 10 个 View 文件迁移 + 命名空间更新 | ✅ |
| 5 | 5 个 ViewModel 迁移/拆分 | ✅ |
| 6 | View code-behind 0 处 `ViewServices.DataGridState` 引用 | ✅ |
| 7 | View code-behind 0 处 `ViewServices.NavigationRouter` 引用 | ✅ |
| 8 | GenericDataGridHelper 删除或全量 [Obsolete] | ⬜ |
| 9 | `bash build.sh` — 0 Error（12 项目） | ✅ |
| 10 | `dotnet test` 全部通过（含 DataViewer.Tests） | ⬜ |
| 11 | 编辑器启动 + DataViewer 功能完整 | ⏳ 待功能验收 |

**代码通过率**：8 / 11 | **整体通过率**：启动正常，DI 问题已修复，待功能验收

---

## 编译/运行时 Bug 修复记录

### Bug 1: AXAML 类型解析失败（3 Error）

**症状**：
```
Error AVLN2000: Unable to resolve type DataTablePlaceholder from namespace NeoEditor.ViewModels.MainContent
Error AVLN2000: Unable to resolve type PeekBreadcrumb from namespace NeoEditor.ViewModels.MainContent
Error AVLN2000: Unable to resolve type PeekPanelViewModel from namespace NeoEditor.ViewModels.MainContent
```

**根因**：`DataTablePlaceholder`、`PeekPanelViewModel`、`PeekBreadcrumb` 已迁至 `NeoEditor.Plugins.DataViewer.ViewModels`，但 AXAML 中仍用 `clr-namespace:NeoEditor.ViewModels.MainContent`。

**修复**（5 文件）：
- `DocumentWorkspaceView.axaml:98` — `mainContent:DataTablePlaceholder` → `dv:DataTablePlaceholder`
- `PeekPanelView.axaml:6,9,12,24` — 替换 `mainContent`→`dv` 命名空间
- `PeekPanelView.axaml.cs`、`DocumentWorkspaceViewModel.cs`、`Documents.cs` — 添加 `using NeoEditor.Plugins.DataViewer.ViewModels;`

### Bug 2: PeekPanelViewModel.Loc 私有导致 AXAML 绑定失败（3 Error）

**症状**：
```
Error AVLN2000: Unable to resolve property or method of name 'Loc' on type 'PeekPanelViewModel'
```

**根因**：迁移到 DataViewer 后 `Loc` 属性误设为 `private`，AXAML 绑定 `{Binding Loc[...]}` 无法访问。

**修复**：`PeekPanelViewModel.cs:33` — `private ILocalizationService Loc` → `public ILocalizationService Loc`

### Bug 3: 具体类注入失败 — LocalizationService（7 FTL）

**症状**：
```
System.InvalidOperationException: No service for type 'NeoEditor.Services.LocalizationService' has been registered.
```

**根因**：DI 只注册了 `ILocalizationService` 接口映射 (`services.AddSingleton<ILocalizationService, LocalizationService>()`)，但多处构造函数直接注入具体类 `GetRequiredService<LocalizationService>()`。

**修复**（9 处，7 文件）：
- `MainWindowViewModel.cs:58` — `GetRequiredService<LocalizationService>()` → `GetRequiredService<ILocalizationService>()`
- `DocumentWorkspaceViewModel.cs:105` — 同上
- `CreateModDialogViewModel.cs`、`DataBrowserViewModel.cs`、`ModDatabaseViewModel.cs`、`ModIndexViewModel.cs`、`HomePageViewModel.cs` — 构造函数参数 `LocalizationService` → `ILocalizationService`

### Bug 4: 具体类注入失败 — BrowserIndexService（1 FTL + 2 预修）

**症状**：
```
System.InvalidOperationException: Unable to resolve service for type 'NeoEditor.Services.BrowserIndexService' while attempting to activate 'NeoEditor.Services.ModManager'.
```

**根因**：`BrowserIndexService` 只注册为 `IBrowserIndexService`，但 `ModManager` 构造函数注入具体类。

**修复**（3 文件）：
- `ModManager.cs` — 构造函数参数 + 私有字段 `BrowserIndexService` → `IBrowserIndexService`
- `DataBrowserViewModel.cs` — 同上
- `Documents.cs` — `EntityBrowserDocument` + `BrowserEntityRow` 两处 `_bis` 字段 + 构造函数参数

---

## 改动统计

| 类别 | 文件数 | 说明 |
|------|:--:|------|
| AXAML 命名空间修复 | 2 | `DocumentWorkspaceView.axaml`、`PeekPanelView.axaml` |
| C# using 新增 (DataViewer) | 3 | `PeekPanelView.axaml.cs`、`DocumentWorkspaceViewModel.cs`、`Documents.cs` |
| DI 注入修复 (LocalizationService→ILocalizationService) | 7 | `MainWindowViewModel`、`DocumentWorkspaceViewModel`、`CreateModDialogViewModel`、`DataBrowserViewModel`、`ModDatabaseViewModel`、`ModIndexViewModel`、`HomePageViewModel` |
| DI 注入修复 (BrowserIndexService→IBrowserIndexService) | 3 | `ModManager`、`DataBrowserViewModel`、`Documents` |
| DataViewer Plugin 修复 (Loc public) | 1 | `PeekPanelViewModel.cs` |
| **合计修改文件** | **16** | 含 AXAML + C# + Plugin |

> App 代码行原来已经有大量 M9 Phase 1 产物（11 旧文件删除、~50 using 更新、DI 注册等），本轮在此基础上修复编译和运行时 DI 问题。

---

## 残留项（M10 后续处理）

- 14 个 EntityVisualizer 文件中的 `GenericDataGridHelper.EntityModNames` / `EntityNamespaces` 引用 → M10 EntityEditor Plugin 拆分时一并处理
- `ReferenceInspectorView.axaml.cs` 中的 `GenericDataGridHelper.NavigateToByEntityId` → M10 处理
- `SearchPaneViewModel.cs` 中的 `GenericDataGridHelper.NavigateToByEntityId` → M10 处理
- `DataExportService.cs` 中的 `GenericDataGridHelper.GetDedupedEntities` → 改为注入 `DataTableService`
- `ReferenceResolver.cs` 中的 `EntityNamespaces` 查找 → 改为通过 `IWorkspaceSession` 获取
- Converter 目录下剩余与 DataViewer 无关的 Converter 保留在 App

---

## 功能验收清单

### 验收 A：DataViewer 基本功能（15 项）

| 步骤 | 操作 | 检查点 |
|:--:|------|--------|
| 1 | 启动编辑器 | 启动正常，欢迎页完整，无启动异常 |
| 2 | 打开 Profile → Browse Game Data | DataTable 打开，列头显示正确，数据行渲染 |
| 3 | 表切换 | 切换 ItemType / Recipe / Creature 等，表格内容正确刷新 |
| 4 | 排序 | 点击列头排序，升序/降序切换正常 |
| 5 | 过滤 | Filter 输入框输入关键字，行过滤正常 |
| 6 | 搜索 | Ctrl+F 打开 FindReplace 面板，搜索关键词，结果高亮 |
| 7 | 单击行 | 行高亮，Bottom 面板显示详情 |
| 8 | 双击行 | EntityEditorDocument 在 Center 区域打开（M10 目标） |
| 9 | Ctrl+Click 引用字段 | DataTable 跳转到引用实体行，Center 打开对应 Tab |
| 10 | Ctrl+RMB 引用字段 | Peek 面板弹出，显示目标实体信息 |
| 11 | 修改字段 → Ctrl+S | 保存成功，脏标记清除，数据持久化 |
| 12 | KV 编辑 | Bottom KeyValueEditor 字段编辑正常 |
| 13 | 合并视图 | Merge View 下拉切换，覆盖链 / 字段来源 tooltip 正确 |
| 14 | 列可见性 | Settings → Column Visibility 勾选/取消 → DataGrid 列显隐生效 |
| 15 | Index 表 | 侧边栏 Index Tab 切换，正向/反向索引数据正确 |

### 验收 B：回归检查（4 项）

| 步骤 | 操作 | 检查点 |
|:--:|------|--------|
| 1 | Settings 持久化 | 修改 GameRootDir / 语言 / 主题 → 重启编辑器 → 配置保留 |
| 2 | 脏数据一致性 | DataGrid 黄色高亮 ↔ Value Editor Alert 图标 ↔ Ctrl+S 状态三者一致 |
| 3 | `dotnet test` 全部 | 21+ 测试全部通过，0 Failure |
| 4 | 旧 NeoEditor.Tests | 8/8 通过（若无变动） |

---

## 附录：当前 M9 第一阶段产物

```
NeoEditor.Plugins.DataViewer/
├── NeoEditor.Plugins.DataViewer.csproj     ✅
├── DataViewerPlugin.cs                      ✅
├── Services/
│   ├── DataTableService.cs                  ✅ (new — GDH 数据访问替代)
│   ├── ColumnTemplateFactory.cs             ✅ (new — GDH ConfigureColumn 替代)
│   ├── InteractionHandler.cs                ✅ (new — GDH 事件触发替代)
│   ├── NavigationRouter.cs                  ✅ (migrated)
│   ├── DataGridInteractionState.cs          ✅ (migrated)
│   ├── DataGridNavigationService.cs         ✅ (migrated)
│   ├── DataGridCellInteractionService.cs    ✅ (migrated)
│   ├── IDataGridCellInteractionService.cs   ✅ (migrated)
│   └── ColumnVisibilityKeys.cs              ✅ (migrated from Infra)
├── Converters/
│   ├── FieldSourceConverter.cs              ✅ (migrated)
│   ├── FieldConflictBackgroundConverter.cs  ✅ (migrated)
│   ├── EntityMergedIdConverter.cs           ✅ (migrated)
│   ├── ModNameColumnConverter.cs            ✅ (migrated)
│   └── OverlayChainConverter.cs             ✅ (migrated)
├── ViewModels/                              ⏳ (本轮迁移)
└── Views/                                   ⏳ (本轮迁移)

Tests/NeoEditor.Plugins.DataViewer.Tests/
├── NeoEditor.Plugins.DataViewer.Tests.csproj ✅
├── DataViewerPluginTests.cs                  ✅ (3 tests)
└── Services/DataTableServiceTests.cs         ✅ (7 tests)
```
