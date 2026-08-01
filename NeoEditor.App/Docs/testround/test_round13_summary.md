# 架构测试第13轮 — M9 DataViewer Plugin Views 迁移第2轮 + 核心服务提取

> 日期：2026-07-28 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round12_summary.md](test_round12_summary.md) (Views 迁移第1轮)

## 本轮目标

完成 M9 DataViewer Plugin 的 #7-#10 工作项：迁移 4 个简单 View、提取 DataLoaderService、增强 DataTableViewModel、清理 GDH 双写、移除 Converter 中的 DataTableService.Instance。

---

## 最终结果

| # | 工作项 | 状态 | 说明 |
|---|--------|:--:|------|
| 7a | IndexTableView 迁移 | ✅ | 0 静态依赖，纯文件移动 |
| 7b | PeekPanelView 迁移 | ✅ | + IEntityVisualizer + EntityVisualizerRegistry 类型提取 |
| 7c | FindReplacePanel 迁移 | ✅ | 属性注入 Loc/Notification + FluentIcons 包引用 |
| 7d | SearchResultsView 迁移 | ✅ | BottomToolsViewModel → Plugin SearchResultViewModel |
| 8a | DataLoaderService 提取 | ✅ | 6 个 DB 方法 + BuildHeader + ResolveEntityKeyProperty 进 Plugin |
| 8b | DataTableViewModel 增强 | ✅ | Tabs/MergeStore/EditStore/ModInfo/ProfileInfo 所有权移到 VM |
| 9 | GDH 清理 | ✅ | MGDV 4 partial 清零，7 App 消费者迁移，Plugin 0 GDH 引用 |
| 10 | Converter Instance 移除 | ✅ | 5 Converter → ConverterServiceHelper（Application.Current 解析） |
| — | 编译 + 测试 | ✅ | 10 项目 0 Error，12/12 测试通过 |
| — | 人工验收 | ✅ | App 正常启动，功能正常 |

---

## 1. 简单 View 迁移（#7）

### IndexTableView
- **难度**: 极低。88 行，0 静态依赖
- **操作**: 纯文件移动 + `DocumentWorkspaceView.axaml` 命名空间更新
- **改动**: 2 新建 + 2 删除 + 1 XAML 更新

### PeekPanelView
- **难度**: 低。149 行，1 静态依赖（`ViewServices.VisualizerRegistry`）
- **阻塞**: Plugin 无法引用 App 的 `IEntityVisualizer` / `EntityVisualizerRegistry`
- **解决方案**: 将 `IEntityVisualizer` 移至 Plugin（Plugin 同时持有 Avalonia + Core 引用），`EntityVisualizerRegistry` 移至 Plugin/Services
- **DI fallback**: `Application.Current?.Resources["Services"]` 解析
- **附带影响**: App.axaml.cs 删除 `services.AddSingleton<EntityVisualizerRegistry>()`，ViewServices 更新命名空间，ValueEditorPanel using 更新

### FindReplacePanel
- **难度**: 中。416 行，无 ViewModel（`DataContext = this`），360 行 code-behind
- **静态依赖**: `ViewServices.Loc` → `InjectedLoc` 属性 + DI fallback；`ViewServices.Notification` → `InjectedNotification` 属性 + DI fallback
- **跨程序集访问**: `CommandHistory` / `OnDirtyChanged` 从 `internal` → `public`
- **新增依赖**: Plugin csproj 添加 `FluentIcons.Avalonia 2.0.319`（`<avalonia:SymbolIcon>` 需要）
- **父视图注入**: ModGameDataTabsView.axaml.cs 的 `ShowFindPanel()` 中设置 `InjectedLoc` / `InjectedNotification`

### SearchResultsView
- **难度**: 中。151 行，原绑定 `BottomToolsViewModel`（App）
- **重构**: 
  - `SearchResultViewModel` 添加 `ILocalizationService Loc` 构造参数 + `NavigateToResult()` 方法
  - XAML bindings: `BottomSearchText` → `SearchText`, `BottomSearchCommand` → `SearchCommand` 等
  - 代码: `BottomToolsViewModel` → `SearchResultViewModel`
- **BottomToolsViewModel 瘦身**: 删除搜索属性/命令(BottomSearchText, IsBottomSearching, SearchResultGroups, SearchSummary, RecentSearches, NavigateToResult)，仅保留 Conflicts + Validation
- **BottomToolsView 瘦身**: 删除搜索 TabItem，仅保留 Conflicts + Validation 两个 Tab
- **DocumentWorkspaceViewModel**: `SearchResultsTool` 改用新建的 `SearchResultViewModel` 实例

---

## 2. DataLoaderService 提取（#8a）

### 提取的方法

| 方法 | 来源文件 | 行数 |
|------|----------|:--:|
| `LoadEntitiesByTypeAsync` + typed 变体 | Data.cs | ~30 |
| `LoadEntitiesByModAsync` + typed 变体 | Data.cs | ~20 |
| `LoadEntitiesByModIdsAsync` + typed 变体 | Data.cs | ~20 |
| `ResolveEntityKeyProperty` | Operations.cs | ~16 |
| `BuildHeader` | Data.cs | ~5 |

### 架构收益
- View 不再直接创建 `GameDbContext` — 由 DataLoaderService 内部管理生命周期
- DB 访问全部集中到 Plugin 的 Singleton 服务
- ~120 行死代码从 App 中删除
- `DataLoaderService` 注册为 Singleton，构造函数注入 `IDbContextFactory<GameDbContext>` + `ILocalizationService` + `ILogger`

---

## 3. DataTableViewModel 增强（#8b）

### 状态所有权迁移（View → VM）

| 状态 | 迁移前（View 字段） | 迁移后（VM 属性） |
|------|---------------------|-------------------|
| Tabs | `public ObservableCollection<GameDataTypeTabItem> Tabs { get; } = []` | `_vm.Tabs` |
| MergeStore | `internal EntityMergeStore MergeStore { get; private set; } = new()` | `_vm.MergeStore` |
| EditStore | `internal EditTrackingStore EditStore { get; private set; } = new()` | `_vm.EditStore` |
| ModInfo | View 回调 `_vm.GetModInfo = () => ModInfo` | `_vm.ModInfo` 属性 |
| ProfileInfo | View 回调 `_vm.GetProfileInfo = () => ProfileInfo` | `_vm.ProfileInfo` 属性 |

### 策略
- View 保留同名属性委托到 VM（如 `internal EntityMergeStore MergeStore => _vm.MergeStore;`）
- 所有现有 partial 代码无需修改引用
- TabSnapshotCache 恢复时通过 `_vm.ReplaceStores()` 替换 VM 内 stores

---

## 4. GDH 清理（#9）

### ModGameDataTabsView 4 partial 全部清零

**Data.cs** (最多 GDH 引用):
- `GenericDataGridHelper.ClearSubjectCache()` → `MergeStore.SubjectCache.Clear()`
- `GenericDataGridHelper.EntityMergedIds[x] = y` → 删除（已直写 MergeStore）
- `GenericDataGridHelper.EditedCells/.NewEntityIds` → `EditStore.EditedCells/.NewEntityIds`
- 合并视图双写（793-836 行）：删除 11 处 GDH 写（MergeStore 已有相同数据）
- `GenericDataGridHelper.NamespaceToModName` 参数 → `MergeStore.NamespaceToModName`

**axaml.cs**: `OverlayChainDisplay` / `OverriddenEntityIds` / `NewEntityIds` → MergeStore/EditStore

**Tab.cs**: `SetActiveStores` → `DataTableService.Instance?.SetActiveStores`；`EditedCells` / `NewEntityIds` → EditStore

**Operations.cs**: `EditedCells.RemoveWhere` / `NewEntityIds.Clear` / `NewEntityIds.Add` → EditStore；`EntityModNames` / `OverlayChainDisplay` → MergeStore

### 批量迁移的 App 消费者

| 文件 | 引用数 | 方式 |
|------|:--:|------|
| BottomToolsViewModel.cs | 1 | `FieldConflicts` → `DataTableService.Instance?.FieldConflicts` |
| EntityEditorDocument.cs | 1 | `EditedCells.RemoveWhere` → `DataTableService.Instance?.EditedCells.RemoveWhere` |
| SearchPaneViewModel.cs | 1 | `NavigateToByEntityId` → `DataTableService.Instance?.NavigateToByEntityId` |
| DataExportService.cs | 1 | `GetEntityMergedId` → `DataTableService.Instance?.GetEntityMergedId` |
| ReferenceInspectorView.axaml.cs | 1 | `NavigateToByEntityId` → `DataTableService.Instance?.NavigateToByEntityId` |
| ReferenceIntegrityRule.cs | 3 | 批量 sed + `?.` → `?? false` 修正 |
| App.axaml.cs | 1 | `FieldDescriptions` — 保留 GDH（静态 setter，仅此处使用） |

### 暂未迁移（复杂模式，需逐文件处理）

| 文件 | 引用数 | 原因 |
|------|:--:|------|
| ReferenceResolver.cs | 22 | `TryGetValue` with `out var` + null-coalescing 链 |
| VisHelper.cs | 7 | 同上 |
| 7 个 EntityVisualizers | 各 1-5 | 同上 |

> 这些文件的 GDH 使用模式（`TryGetValue(out var)`, `?. ??` 链）无法用批量 sed 安全替换。后续逐文件迁移。

---

## 5. Converter Instance 移除（#10）

### 新建 `ConverterServiceHelper`

```csharp
internal static class ConverterServiceHelper
{
    public static DataTableService? DataTable =>
        (Application.Current?.Resources["Services"] as IServiceProvider)
            ?.GetService(typeof(DataTableService)) as DataTableService;
}
```

### 迁移的 Converter

| Converter | 原调用 | 新调用 |
|-----------|--------|--------|
| EntityMergedIdConverter | `Services.DataTableService.Instance?.GetEntityMergedId` | `ConverterServiceHelper.DataTable?.GetEntityMergedId` |
| FieldSourceConverter | `Services.DataTableService.Instance?.FieldSources` | `ConverterServiceHelper.DataTable?.FieldSources` |
| FieldConflictBackgroundConverter | `Services.DataTableService.Instance?.FieldConflicts` | `ConverterServiceHelper.DataTable?.FieldConflicts` |
| OverlayChainConverter | `Services.DataTableService.Instance` | `ConverterServiceHelper.DataTable` |
| ModNameColumnConverter | `Services.DataTableService.Instance?.EntityModNames` | `ConverterServiceHelper.DataTable?.EntityModNames` |

**注意**: `DataTableService.Instance` 静态属性暂时保留——App 代码中 `ReferenceResolver.Svc`、`MGDV.Tab.cs`、`EntityEditorDocument.cs` 等仍通过它访问。完全移除需将这些消费者改为 DI 注入模式。

---

## 改动文件清单

| 文件 | 改动 | 关联工作 |
|------|------|:--:|
| `Plugin/Views/IndexTableView.axaml` + `.cs` | **新建** | 7a |
| `Plugin/Views/PeekPanelView.axaml` + `.cs` | **新建** | 7b |
| `Plugin/IEntityVisualizer.cs` | **新建** — 从 App 提取 | 7b |
| `Plugin/Services/EntityVisualizerRegistry.cs` | **新建** — 从 App 提取 | 7b |
| `Plugin/Views/FindReplacePanel.axaml` + `.cs` | **新建** | 7c |
| `Plugin/Views/SearchResultsView.axaml` + `.cs` | **新建** | 7d |
| `Plugin/Services/DataLoaderService.cs` | **新建** | 8a |
| `Plugin/Converters/ConverterServiceHelper.cs` | **新建** | 10 |
| `Plugin/ServiceCollectionExtensions.cs` | +DataLoaderService +EntityVisualizerRegistry 注册 | 8a, 7b |
| `Plugin/NeoEditor.Plugins.DataViewer.csproj` | +FluentIcons.Avalonia 包引用 | 7c |
| `Plugin/ViewModels/SearchResultViewModel.cs` | +Loc 构造参数 +NavigateToResult() | 7d |
| `Plugin/ViewModels/DataTableViewModel.cs` | +Tabs/MergeStore/EditStore/ModInfo/ProfileInfo 所有权 | 8b |
| `App/Views/.../IndexTableView.axaml` + `.cs` | **删除** | 7a |
| `App/Views/.../PeekPanelView.axaml` + `.cs` | **删除** | 7b |
| `App/Helper/IEntityVisualizer.cs` | **删除** | 7b |
| `App/Services/EntityVisualizerRegistry.cs` | **删除** | 7b |
| `App/Views/.../FindReplacePanel.axaml` + `.cs` | **删除** | 7c |
| `App/Views/.../SearchResultsView.axaml` + `.cs` | **删除** | 7d |
| `App/Views/.../DocumentWorkspaceView.axaml` | 所有 View 引用 → dvViews 命名空间 | 7a-d |
| `App/Views/.../ModGameDataTabsView.axaml` | FindReplacePanel → dvViews | 7c |
| `App/Views/.../ModGameDataTabsView.axaml.cs` | +_dataLoader 字段；ShowFindPanel 注入；ResolveEntityKeyProperty → DataLoaderService | 8a, 7c |
| `App/Views/.../ModGameDataTabsView.Data.cs` | GDH 双写删除；使用 _dataLoader；死代码清理 | 8a, 9 |
| `App/Views/.../ModGameDataTabsView.Tab.cs` | GDH 替换为 DataTableService.Instance/EditStore | 9 |
| `App/Views/.../ModGameDataTabsView.Operations.cs` | GDH 替换；ResolveEntityKeyProperty → DataLoaderService；死代码删除 | 8a, 9 |
| `App/Views/.../BottomToolsView.axaml` + `.cs` | 删除搜索 Tab；简化 code-behind | 7d |
| `App/ViewModels/.../BottomToolsViewModel.cs` | 删除搜索属性/命令；简化构造函数 | 7d |
| `App/ViewModels/.../DocumentWorkspaceViewModel.cs` | SearchResultsTool → SearchResultViewModel | 7d |
| `App/ViewModels/.../Documents.cs` | SearchResultsTool 构造参数类型 → SearchResultViewModel | 7d |
| `App/ViewModels/.../EntityEditorDocument.cs` | GDH → DataTableService.Instance | 9 |
| `App/ViewModels/.../SearchPaneViewModel.cs` | GDH → DataTableService.Instance | 9 |
| `App/Helper/ViewServices.cs` | VisualizerRegistry 命名空间更新 | 7b |
| `App/Services/DataExportService.cs` | GDH → DataTableService.Instance | 9 |
| `App/Views/.../ValueEditorPanel.axaml.cs` | using 更新 | 7b |
| `App/Views/.../ReferenceInspectorView.axaml.cs` | GDH → DataTableService.Instance | 9 |
| `App/Data/.../ReferenceIntegrityRule.cs` | GDH → DataTableService.Instance | 9 |
| `App/App.axaml.cs` | 删除 EntityVisualizerRegistry 注册 | 7b |

---

## 编译和自动化测试

| 项目 | 错误 | 警告 | 备注 |
|------|:--:|:--:|------|
| `bash build.sh` | 0 | 已知 NU1903 + AVLN3001 | 10 项目全部通过 |
| NeoEditor.App.Tests | — | — | 2/2 ✅ |
| NeoEditor.Plugins.DataViewer.Tests | — | — | 10/10 ✅ |
| **总计** | **0** | **已知** | **12/12 ✅** |

## 架构合规验证

| 规则 | 检查项 | 结果 |
|------|--------|:--:|
| N01 | Plugin 无静态可变状态 | ✅ |
| R17 | Plugin 互不引用 | ✅ |
| R18 | Plugin 只依赖 Core + Infra + UI.Common | ✅ |
| N03 | Plugin View code-behind 无业务逻辑 | ✅（FindReplacePanel 是已知例外，N03 需进一步处理） |
| — | Plugin 中 GDH 引用 | **0** |
| — | Plugin 中 ViewServices 引用 | **0** |
| — | Plugin 中 DataTableService.Instance 引用 | **0**（Converter 已迁到 ConverterServiceHelper） |
| — | Plugin 中 NeoEditor.App 命名空间引用 | **0** |
| — | MGDV 4 partial 中 GDH 引用 | **0** |

## 人工验收

| 场景 | 预期 | 结果 |
|------|------|:--:|
| 启动 App | 日志无异常 | ✅ |
| 打开 Profile | DataTable 正常加载，列正确渲染 | ✅ |
| 引用列（teal 链接） | 显示正常、Ctrl+Click 跳转 | ✅ |
| Peek 面板 | 实体详情显示、面包屑导航 | ✅ |
| Index 表（正向/反向） | 数据显示正常 | ✅ |
| 搜索（Ctrl+F） | 搜索/替换功能正常 | ✅ |
| 底部搜索 | 搜索结果显示、双击导航 | ✅ |
| 编辑 + 保存 | 正常持久化 | ✅ |

---

## 当前架构

```
NeoEditor.Plugins.DataViewer/
├── IEntityVisualizer.cs              ← 从 App 提取
├── DataViewerPlugin.cs
├── ServiceCollectionExtensions.cs     ← 11 服务注册
├── Converters/ (5 + 1 helper)
│   └── ConverterServiceHelper.cs      ← 替换 DataTableService.Instance
├── Services/ (11)
│   ├── DataLoaderService.cs           ← 实体 DB 加载
│   ├── EntityVisualizerRegistry.cs    ← 从 App 提取
│   └── ... (9 more)
├── ViewModels/ (6)
└── Views/ (5 .axaml)

NeoEditor.App/
├── Views/UserControls/
│   ├── ModGameDataTabsView (5 partial) ← 仍在此，GDH 已清零
│   ├── BottomToolsView                 ← 搜索 Tab 已移除
│   ├── Editors/ (25+ EntityVisualizers)
│   └── ... (其余 View)
└── Helper/
    └── GenericDataGridHelper.cs        ← 167行纯委托（部分消费者仍在用）
```

---

---

## 6. GDH 最终删除（#11，第13轮补充）

### GDH 剩余消费者迁移

| 文件 | 引用数 | 处理方式 |
|------|:--:|------|
| EncounterEntityVisualizer.cs | 4 TryGetValue | `(DataTableService.Instance?.ReferenceLookups ?? []).TryGetValue(...)` |
| TreasureTableEntityVisualizer.cs | 4 TryGetValue | 同上 |
| VisHelper.cs | 7 | TryGetValue / FindBestMatch / store access — 全部手动迁移 |
| ReferenceResolver.cs | 22 | 添加 `private static Svc` 属性 + TryGetValue `?? []` 包装 |
| BarterHex/ContainerType/CreatureSource/Faction/CampType/ItemType/Recipe Visualizers | 各 1-3 | 批量 sed 替换 |
| FactionEntityVisualizer.cs | 1 GetEntities | 遗漏→手动修正 |

### FieldDescriptions 迁移

- `GenericDataGridHelper.FieldDescriptions` → `ColumnTemplateFactory.FieldDescriptionProvider` 委托
- App.axaml.cs 直接设置 `FieldDescriptionProvider = (table, prop) => fieldDescService.GetDescription(table, prop)`
- 无需在 Plugin 中引用 App 的 `FieldDescriptionService` 类型

### GDH.cs 删除

`NeoEditor.App/Helper/GenericDataGridHelper.cs` — **已删除** ✅

所有 167 行委托代码已无消费者，`FieldDescriptions` 已迁到 `ColumnTemplateFactory`。

### 最终验证

- 编译：10 项目 0 CS Error
- 测试：12/12 通过
- 全局搜索 `GenericDataGridHelper.`：**0 结果**（代码引用清零）

---

## 下一步

| # | 工作 | 说明 |
|---|------|------|
| 12 | DataTableService.Instance 完全移除 | App 剩余消费者（ReferenceResolver.Svc、MGDV.Tab.cs 等）→ DI 注入 |
| 13 | ModGameDataTabsView → Plugin DataTableView | 5 partial 4153 行拆分为瘦 Plugin View。基础已就绪 |
| 14 | Plugin Views 单元测试 | Avalonia.Headless 或纯 ViewModel 测试 |
