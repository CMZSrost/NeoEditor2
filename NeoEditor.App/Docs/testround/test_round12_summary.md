# 架构测试第12轮 — M9 DataViewer Plugin Views 迁移（第1轮）

> 日期：2026-07-26 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round11_summary.md](test_round11_summary.md) (M9 前置清理)

## 本轮目标

M9 DataViewer Plugin 的核心迁移工作 — SearchableDataGrid 解耦静态依赖并移至 Plugin、GenericDataGridHelper 轻量化为委托层、DataViewerPlugin.CreateToolView 实现。

---

## 最终结果

| # | 工作项 | 状态 | 说明 |
|---|--------|:--:|------|
| 1 | SearchableDataGrid 解耦 GDH/ViewServices | ✅ | 15+ 处静态引用全部替换为可注射属性 |
| 2 | Plugin 启用 XAML 编译 | ✅ | csproj 添加 AvaloniaUseCompiledBindingsByDefault |
| 3 | SearchableDataGrid 迁移到 Plugin | ✅ | 首个 Plugin View (.axaml)，App 旧副本已删除 |
| 4 | GenericDataGridHelper 轻量化 | ✅ | 839→167 行，全部委托到 DataTableService.Instance |
| 5 | DataViewerPlugin.CreateToolView 实现 | ✅ | 不再抛 NotImplementedException |
| 6 | 测试更新 | ✅ | 29/29 全部通过（含新 DataViewerPlugin 测试） |

---

## 前置条件

- [x] `bash build.sh` 编译通过（12 项目，0 Error）
- [x] 7 个测试项目 29/29 全部通过
- [x] M9 前置清理已完成（V6 static 删除、重复副本删除、DI 集中注册）

---

## 1. SearchableDataGrid 静态依赖解耦

### 现象

`SearchableDataGrid.axaml.cs` 中存在 15+ 处对 `GenericDataGridHelper` / `ViewServices` / `WeakReferenceMessenger.Default` 的静态引用，违反 N01（禁止静态可变状态）。这是 DataGrid 列自动生成、导航交互、单元格编辑的核心路径。

### 解耦方法

**新增可注射服务属性**（由父视图构造后设置）：

```csharp
public DataTableService? DataTable { get; set; }
public ColumnTemplateFactory? ColumnTemplateFactory { get; set; }
public InteractionHandler? InteractionHandler { get; set; }
public DataGridInteractionState? DataGridState { get; set; }
public IDataGridCellInteractionService? CellInteraction { get; set; }
public IDataGridNavigationService? DataGridNavigation { get; set; }
public ISelectionService? SelectionService { get; set; }
public ILocalizationService? Loc { get; set; }
public ILoggerFactory? LoggerFactory { get; set; }
public IConfigService? ConfigService { get; set; }
public IMessenger? Messenger { get; set; }
```

**构造函数与事件注册分离** — 原构造函数中注册的 `PointerPressed` 事件 / `WeakReferenceMessenger` 消息处理器需要服务属性已就绪，因此拆分为两步：
1. `SearchableDataGrid()` — 仅做 `InitializeComponent()` + 控件初始状态设置
2. `InitializeServices()` — 父视图设置全部服务属性后调用，注册消息和事件处理器

**替换清单**：

| 原调用 | 新调用 |
|--------|--------|
| `GenericDataGridHelper.ConfigureColumn(e, localizer, modelType)` | `ColumnTemplateFactory!.ConfigureColumn(e, modelType)` |
| `GenericDataGridHelper.SetActiveStores(ms, es)` | `DataTable!.SetActiveStores(ms, es)` |
| `GenericDataGridHelper.RaiseCellEditCommitted(...)` | `InteractionHandler!.RaiseCellEditCommitted(...)` |
| `GenericDataGridHelper.EditedCells` | `EditStore?.EditedCells`（本实例） 或 `DataTable!.EditedCells`（回退） |
| `GenericDataGridHelper.EntityMergedIds` | `DataTable!.EntityMergedIds` |
| `GenericDataGridHelper.EntityModNames` | `DataTable!.EntityModNames` |
| `ViewServices.DataGridState.*` | `DataGridState!.*` |
| `ViewServices.Loc` | `Loc!` |
| `ViewServices.SelectionService` | `SelectionService!` |
| `WeakReferenceMessenger.Default.*` | `Messenger!.*`（`WeakReferenceMessenger.Default` 仅做 null 回退） |

### 父视图注入

`ModGameDataTabsView.axaml.cs` 构造函数中，`InitializeComponent()` 之后添加：

```csharp
var dataTableService = ViewServices.Get<DataTableService>();
var columnTemplateFactory = ViewServices.Get<ColumnTemplateFactory>();
columnTemplateFactory.Localizer = key => Loc[key] ?? key;
columnTemplateFactory.Messenger = _messenger;
SharedDataGrid.Loc = Loc;
SharedDataGrid.DataTable = dataTableService;
SharedDataGrid.ColumnTemplateFactory = columnTemplateFactory;
// ... 其余 9 个服务属性
SharedDataGrid.InitializeServices();
```

### 改动文件

| 文件 | 改动 |
|------|------|
| `NeoEditor.App/Views/UserControls/SearchableDataGrid.axaml.cs` | 15+ 处静态引用 → 服务属性；构造函数逻辑拆分 |
| `NeoEditor.App/Views/UserControls/ModGameDataTabsView.axaml.cs` | 构造函数中添加 SharedDataGrid 服务注入代码块 |

### 验证

- `SearchableDataGrid.axaml.cs` 中 `GenericDataGridHelper` 引用：**0**
- `SearchableDataGrid.axaml.cs` 中 `ViewServices.` 引用：**0**
- `SearchableDataGrid.axaml.cs` 中 `WeakReferenceMessenger.Default`：**2**（仅做 `Messenger ??` null 回退）

---

## 2. Plugin 启用 XAML 编译 + SearchableDataGrid 迁移

### Plugin csproj 改动

```xml
<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
<Using Include="NeoEditor.Infra.Services" />  <!-- 全局命名空间，解决 ILocalizationService/IConfigService 等类型引用 -->
```

关键点：Plugin 项目作为类库，不需要 `Avalonia.Desktop` 包（那是可执行文件用的）。仅需 `Avalonia` + `Avalonia.Controls.DataGrid` 即可编译 `.axaml` 文件。无需显式 `<AvaloniaResource>` — Avalonia SDK 自动包含。

### 迁移过程

1. **创建** `NeoEditor.Plugins.DataViewer/Views/SearchableDataGrid.axaml` — `x:Class` 指向 `NeoEditor.Plugins.DataViewer.Views.SearchableDataGrid`
2. **创建** `.axaml.cs` — namespace 改为 `NeoEditor.Plugins.DataViewer.Views`，其余代码与解耦后的 App 版本相同
3. **更新引用方**：
   - `ModGameDataTabsView.axaml`：加 `xmlns:dvViews`，`<userControls:SearchableDataGrid>` → `<dvViews:SearchableDataGrid>`
   - `ModGameDataTabsView.Tab.cs` / `.Data.cs`：加 `using SearchableDataGrid = NeoEditor.Plugins.DataViewer.Views.SearchableDataGrid;` 别名
   - `App.axaml.cs`：DI 注册改为完全限定名 `NeoEditor.Plugins.DataViewer.Views.SearchableDataGrid`
4. **删除** App 旧文件 `SearchableDataGrid.axaml` + `.axaml.cs`

### 遇到的编译错误

| 错误 | 原因 | 修复 |
|------|------|------|
| `CS0246: ISelectionService` 找不到 | 类型在 `NeoEditor.Services` 命名空间而非 `NeoEditor.Infra.Services` | 补 `using NeoEditor.Services;` |
| `CS0246: ILocalizationService/IConfigService` 找不到 | Plugin 无 `NeoEditor.Infra.Services` 全局 using | csproj 添加 `<Using Include="NeoEditor.Infra.Services" />` |
| `CS1503: 无法转换 SearchableDataGrid` | XAML 生成 Plugin 类型，但 code-behind 引用 App 类型 | 添加 using 别名指向 Plugin 类型 |
| `AVLN2002: Duplicate x:Class` | 显式 `<AvaloniaResource>` 与 SDK 自动包含冲突 | 删除显式 AvaloniaResource 项 |

### 改动文件

| 文件 | 改动 |
|------|------|
| `Plugin/Views/SearchableDataGrid.axaml` | **新建** |
| `Plugin/Views/SearchableDataGrid.axaml.cs` | **新建** |
| `Plugin/NeoEditor.Plugins.DataViewer.csproj` | 添加 XAML 编译 + global using |
| `App/Views/.../SearchableDataGrid.axaml` + `.cs` | **删除** |
| `App/Views/.../ModGameDataTabsView.axaml` | 新增 `xmlns:dvViews`，SearchableDataGrid 指向 Plugin |
| `App/Views/.../ModGameDataTabsView.Tab.cs` | 添加 `using SearchableDataGrid = ...` 别名 |
| `App/Views/.../ModGameDataTabsView.Data.cs` | 同上 |
| `App/App.axaml.cs` | DI 注册改为 Plugin 完全限定名 |

---

## 3. GenericDataGridHelper 轻量化

### 策略

不逐个修改 22 个消费者文件，而是让 `GenericDataGridHelper` 本身变成 `DataTableService.Instance` 的纯委托层。这样所有消费者零改动即自动使用 Plugin 服务。

### 改写原则

- 所有数据访问属性（`EditedCells`、`EntityModNames`、`ReferenceLookups` 等）→ 委托给 `Svc?.Xxx ?? []`
- 所有实体查询方法（`GetEntities<T>`、`GetCompositeEntities<T>` 等）→ 委托给 `Svc?.Xxx() ?? []`
- 导航方法（`NavigateTo`、`PeekEntity` 等）→ 委托给 `Svc?.Xxx()`
- `ConfigureColumn` 保留但委托给 `ColumnTemplateFactory`
- `NavigateToReference` 保留 `ViewServices`（无直接 passthrough）
- `RaiseCellEditCommitted` 等消息方法保留（轻量，仅一行 `WeakReferenceMessenger.Default.Send`）

### 行数变化

| 指标 | 前 | 后 |
|------|:--:|:--:|
| 总行数 | 839 | 167 |
| 有效代码行 | ~700 | ~90 |
| 静态空集合回退 | 10 个 `_emptyXxx` | 全部移除（Plugin 服务用 `[]` 回退） |
| `ConfigureColumn` 方法 | ~530 行（内联模板构建） | ~30 行（委托给 ColumnTemplateFactory） |

### 改动文件

| 文件 | 改动 |
|------|------|
| `NeoEditor.App/Helper/GenericDataGridHelper.cs` | 全部重写为委托层 |

---

## 4. DataViewerPlugin.CreateToolView 实现

### 原状

```csharp
throw new NotImplementedException(
    "DataViewer views are registered via DI. Use IPluginContext.Services to resolve views.");
```

### 修改

```csharp
public object CreateToolView()
{
    return new ContentControl
    {
        DataContext = new ViewModels.ModDataToolViewModel()
    };
}
```

> `ContentControl` 返回后，App Shell 的 `DocumentWorkspaceView.axaml` 中的 DataTemplate 会匹配 `ModDataToolViewModel` 并渲染完整 DataTable 视图。这是当前架构下最简洁的实现。

### 测试更新

`DataViewerPluginTests.CreateToolView_ThrowsNotImplementedException` → `CreateToolView_ReturnsContentControl`：
```csharp
var view = plugin.CreateToolView();
Assert.NotNull(view);
Assert.IsType<Avalonia.Controls.ContentControl>(view);
```

---

## 改动文件完整清单

| 文件 | 改动 | 关联工作 |
|------|------|:--:|
| `App/Views/.../SearchableDataGrid.axaml.cs` | 15+ 静态引用 → 服务属性；构造函数拆分 | 1 |
| `App/Views/.../ModGameDataTabsView.axaml.cs` | 添加 SharedDataGrid 服务注入 | 1 |
| `Plugin/Views/SearchableDataGrid.axaml` | **新建** — 首个 Plugin View | 2, 3 |
| `Plugin/Views/SearchableDataGrid.axaml.cs` | **新建** — namespace 改为 Plugin | 2, 3 |
| `Plugin/NeoEditor.Plugins.DataViewer.csproj` | 添加 XAML 编译 + global using | 2 |
| `App/Views/.../SearchableDataGrid.axaml` + `.cs` | **删除** — 已迁移到 Plugin | 3 |
| `App/Views/.../ModGameDataTabsView.axaml` | 新增 `xmlns:dvViews`，SearchableDataGrid → Plugin 程序集 | 3 |
| `App/Views/.../ModGameDataTabsView.Tab.cs` | `using SearchableDataGrid = ...` 类型别名 | 3 |
| `App/Views/.../ModGameDataTabsView.Data.cs` | 同上 | 3 |
| `App/App.axaml.cs` | DI 注册改为 Plugin 完全限定名 | 3 |
| `App/Helper/GenericDataGridHelper.cs` | 839→167 行，全部委托到 Plugin 服务 | 4 |
| `Plugin/DataViewerPlugin.cs` | CreateToolView() 不再抛异常 | 5 |
| `Tests/.../DataViewerPluginTests.cs` | 测试更新匹配新行为 | 6 |

---

## 编译和自动化测试

| 项目 | 错误 | 警告 | 备注 |
|------|:--:|:--:|------|
| `bash build.sh` | 0 | 已知 NU1903 | 12 项目全部通过 |
| NeoEditor.Messaging.Tests | — | — | 3/3 ✅ |
| NeoEditor.Core.Tests | — | — | 3/3 ✅ |
| NeoEditor.Infra.Tests | — | — | 2/2 ✅ |
| NeoEditor.UI.Common.Tests | — | — | 1/1 ✅ |
| NeoEditor.App.Tests | — | — | 2/2 ✅ |
| NeoEditor.Plugins.DataViewer.Tests | — | — | 10/10 ✅ |
| NeoEditor.Tests (旧) | — | — | 8/8 ✅ |
| **总计** | **0** | **已知 NU1903** | **29/29 ✅** |

## 架构合规验证

| 规则 | 检查项 | 结果 |
|------|--------|:--:|
| N01 | Plugin 无静态可变状态 | ✅ |
| R17 | Plugin 互不引用 | ✅（仅 DataViewer 一个 Plugin） |
| R18 | Plugin 只依赖 Core + Infra + UI.Common | ✅ |
| R04/N03 | SearchableDataGrid View 不写业务逻辑 | ✅（委托给注入服务） |
| — | Plugin 中 ViewServices 引用 | **0** |
| — | Plugin 中 GDH 运行时引用 | **0**（仅文档注释） |
| — | Plugin 中 NeoEditor.App 命名空间引用 | **0**（仅文档注释） |

## 当前架构

```
NeoEditor.Plugins.DataViewer/
├── Views/
│   └── SearchableDataGrid.axaml + .cs    ← 首个 View ✅
├── ViewModels/ (6)                       ← 已迁移 ✅
├── Services/ (9)                         ← 已迁移 ✅
├── Converters/ (5)                       ← 已迁移 ✅
├── DataViewerPlugin.cs                   ← CreateToolView ✅
└── ServiceCollectionExtensions.cs        ← DI 注册 ✅

NeoEditor.App/
├── Views/UserControls/
│   ├── ModGameDataTabsView (4 partials)  ← 仍在此，待拆分迁移
│   ├── PeekPanelView                     ← 待迁移
│   ├── IndexTableView                    ← 待迁移
│   ├── FindReplacePanel                  ← 待迁移
│   └── SearchResultsView                 ← 待迁移
└── Helper/
    └── GenericDataGridHelper.cs          ← 167行纯委托（22消费者零改动）
```

---

## 人工验收（启动 App）

> ⚠️ 本轮未做人工验收。以下为下一轮对话需执行的验收场景：

| 场景 | 预期 | 结果 |
|------|------|:--:|
| 启动 App | 日志无异常 | ⬜ |
| 打开 Profile | DataTable 正常加载，列正确渲染 | ⬜ |
| 引用列（teal 链接） | 显示正常、Ctrl+Click 跳转、Ctrl+Hover 悬浮提示 | ⬜ |
| 双击行打开编辑 | Visual / XML / KV 正常 | ⬜ |
| 编辑字段 + Ctrl+S | 保存成功提示 | ⬜ |
| 编辑字段 + Undo/Redo | 正常撤销/重做 | ⬜ |
| 合并视图 | 字段冲突高亮、→Id 列、Mod 列正常 | ⬜ |
| 列管理器 | 显示/隐藏列正常 | ⬜ |
| 重启 App | 编辑内容持久化恢复 | ⬜ |
| 设置页切换语言/主题 | 正常生效 | ⬜ |

---

## 下一步 (M9 继续)

| # | 工作 | 说明 |
|---|------|------|
| 7 | 迁移其余 Views (PeekPanel/IndexTable/FindReplace/SearchResults) | 每个 View 类似 SearchableDataGrid 的解耦+移动流程 |
| 8 | 拆分 ModGameDataTabsView | 4136 行 4 partial → DataLoaderService（数据加载）+ DataTableViewModel 增强 + 瘦 Plugin View |
| 9 | 删除 GenericDataGridHelper.cs | 22 消费者逐个确认 → 移除静态委托层 |
| 10 | 移除 DataTableService.Instance | GDH 删除后不再需要静态桥接 |
| 11 | Plugin Views 单元测试 | Avalonia.Headless 或纯 ViewModel 测试 |
