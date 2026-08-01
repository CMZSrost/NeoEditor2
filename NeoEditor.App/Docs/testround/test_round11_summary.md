# 架构测试第11轮 — M9 DataViewer Plugin 前置清理

> 日期：2026-07-26 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round10_summary.md](test_round10_summary.md) (Dirty State 统一修复)

## 本轮目标

M9 DataViewer Plugin 迁移的前置清理工作 — 删除 V6 静态访问器、修复旧 NeoEditor.Tests、清除 App 中已迁移到 Plugin 的重复副本、集中 DI 注册。

---

## 最终结果

| # | 工作项 | 状态 | 说明 |
|---|--------|:--:|------|
| 1 | 删除 V6 静态访问器 | ✅ | `App.axaml.cs` 移除 5 个 `public static` 服务属性 |
| 2 | 修复旧 NeoEditor.Tests | ✅ | 编译 0 Error，8/8 测试通过 |
| 3 | 删除 App 重复副本 | ✅ | 5 Service + 5 Converter 删除，引用全部重定向到 Plugin |
| 4 | 集中 DI 注册 | ✅ | `services.AddDataViewerPlugin()` 一行替换 4 行分散注册 |
| 5 | App.axaml 转换器引用修复 | ✅ | `OverlayPanelConverter` XAML 命名空间更新为 Plugin 程序集 |

---

## 前置条件

- [x] `bash build.sh` 编译通过（10 项目，0 Error）
- [x] 6 个测试项目 19/19 全部通过
- [x] 人工验收：App 启动 → Profile 浏览 → DataTable 渲染 → 编辑保存 → 重启恢复

---

## 1. V6 静态访问器删除

### 现象

`App.axaml.cs` 中 `App` 类仍有 5 个 V6 架构遗留的 `public static` 服务属性（`ServiceProvider`、`Logger`、`ConfigService`、`Localizor`、`Notification`），违反 spec N01（禁止静态可变状态）。

### 清理过程

1. **引用分析** — 全局搜索发现外部代码已全部迁移至 `ViewServices`，无任何文件引用这些静态属性
2. **内部引用处理** — 唯一引用来自 `ImportGameDataOnStartupAsync()` 和 `ApplyStartupSettings()`，将所有引用改为通过 `_host.Services` DI 解析
3. **ImportGameDataOnStartupAsync 重构** — `static` → 实例方法，参数从静态属性改为局部 DI 解析
4. **OnFrameworkInitializationCompleted 清理** — 移除 5 个赋值语句，保留 `Resources["Services"]` 和 `Resources["Loc"]`

### 改动文件

| 文件 | 改动 |
|------|------|
| `NeoEditor.App/App.axaml.cs` | 删除 5 个 `public static` 属性；`ImportGameDataOnStartupAsync` static → instance；`ApplyStartupSettings`/`InitializeFieldDescriptions` 全部走 `_host.Services` |

---

## 2. 旧 NeoEditor.Tests 修复

### 现象

`NeoEditor.Tests/` 中 `TestStubs.cs` 和 `CoreFlowTests.cs` 编译报 4 个 unique 错误 — 类型 `IConfigService`、`AppConfig`、`INotificationService` 找不到。根因：M8 迁移后类型分布到 Core/Infra/App 不同程序集，但命名空间未同步更新。

### 修复

| 文件 | 改动 |
|------|------|
| `TestStubs.cs` | 补 `using NeoEditor.Infra.Services;` + `using NeoEditor.Core.Model;` |
| `CoreFlowTests.cs` | 补 `using NeoEditor.Data.Context;` + `using NeoEditor.Infra.Services;` |
| `CoreFlowTests.cs` | `ILocalizationService` DI 注册（`DocumentBase` 构造函数通过 `ViewServices.Loc` 依赖） |

### 测试结果

8/8 通过 — `Xml_Generates_Valid_Fragment` / `KV_Loads_Entity_Fields` / `KV_Apply_WritesBack_And_Sends_Refresh` / `Doc_Refresh_Xml_After_Entity_Change` / `Doc_Apply_Xml_Writes_To_Entity` / `ActiveEntityChanged_Trigger_KV_Load` / `EntitySelected_Message_Chain` / `KV_Revert_Restores_Values`

---

## 3. App 重复副本删除

### 现象

DataViewer Plugin 项目已包含 Services + ViewModels + Converters 的精简版（消除了 `GenericDataGridHelper` 静态依赖），但 App 中保留了旧副本。两个版本命名空间不同（`NeoEditor.Services` vs `NeoEditor.Plugins.DataViewer.Services`），形成代码冗余。

### 删除清单

**Services（5 个）**：
| 文件 | 旧命名空间 | 新命名空间（Plugin） | 差异 |
|------|----------|-------------------|------|
| `DataGridInteractionState.cs` | `NeoEditor.Services` | `NeoEditor.Plugins.DataViewer.Services` | 完全相同 |
| `DataGridNavigationService.cs` | `NeoEditor.Services` | `NeoEditor.Plugins.DataViewer.Services` | Plugin 版 `FindBestMatch` 走 session stores，非 static `GenericDataGridHelper` |
| `DataGridCellInteractionService.cs` | `NeoEditor.Services` | `NeoEditor.Plugins.DataViewer.Services` | Plugin 版 `SuppressNextSelectionChanged` 走 `_state` 实例，非 static |
| `IDataGridCellInteractionService.cs` | `NeoEditor.Services` | `NeoEditor.Plugins.DataViewer.Services` | 完全相同 |
| `NavigationRouter.cs` | `NeoEditor.Services` | `NeoEditor.Plugins.DataViewer.Services` | 完全相同 |

**Converters（5 个）**：
| 文件 | 旧命名空间 | 新命名空间（Plugin） |
|------|----------|-------------------|
| `EntityMergedIdConverter.cs` | `NeoEditor.Helper.Converter` | `NeoEditor.Plugins.DataViewer.Converters` |
| `FieldConflictBackgroundConverter.cs` | `NeoEditor.Helper.Converter` | `NeoEditor.Plugins.DataViewer.Converters` |
| `FieldSourceConverter.cs` | `NeoEditor.Helper.Converter` | `NeoEditor.Plugins.DataViewer.Converters` |
| `ModNameColumnConverter.cs` | `NeoEditor.Helper.Converter` | `NeoEditor.Plugins.DataViewer.Converters` |
| `OverlayChainConverter.cs` | `NeoEditor.Helper.Converter` | `NeoEditor.Plugins.DataViewer.Converters` |

### 引用重定向

| 文件 | 改动 |
|------|------|
| `ViewServices.cs` | 废弃访问器 `Services.DataGridInteractionState` → `DataGridInteractionState`（Plugin using） |
| `App.axaml.cs` | DI 注册 `Services.xxx` → `xxx`（Plugin using） |
| `GenericDataGridHelper.cs` | `Converter.FieldSourceConverter` → `FieldSourceConverter`（Plugin using） |
| `SearchableDataGrid.axaml.cs` | `Helper.Converter.EntityMergedIdConverter` → `EntityMergedIdConverter`（Plugin using） |
| `App.axaml` | 新增 `xmlns:dvconv` → Plugin Converters；`conv:OverlayPanelConverter` → `dvconv:OverlayPanelConverter` |

---

## 4. 集中 DI 注册

### 新建文件

`NeoEditor.Plugins.DataViewer/ServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection AddDataViewerPlugin(this IServiceCollection services)
{
    services.AddSingleton<IToolPlugin, DataViewerPlugin>();
    services.AddSingleton<DataGridInteractionState>();
    services.AddSingleton<INavigationRouter, NavigationRouter>();
    services.AddSingleton<IDataGridNavigationService, DataGridNavigationService>();
    services.AddSingleton<IDataGridCellInteractionService, DataGridCellInteractionService>();
    services.AddSingleton<DataTableService>();
    services.AddSingleton<ColumnTemplateFactory>();
    services.AddSingleton<InteractionHandler>();
    return services;
}
```

### App.axaml.cs DI 简化

```
- services.AddSingleton<DataGridInteractionState>();
- services.AddSingleton<Helper.INavigationRouter, NavigationRouter>();
- services.AddSingleton<IDataGridNavigationService, DataGridNavigationService>();
- services.AddSingleton<IDataGridCellInteractionService, DataGridCellInteractionService>();
+ services.AddDataViewerPlugin();
```

---

## 改动文件完整清单

| 文件 | 改动 | 关联工作 |
|------|------|:--:|
| `NeoEditor.App/App.axaml.cs` | 删除 5 static 属性 + DI 注册简化 | 1, 4 |
| `NeoEditor.App/App.axaml` | 新增 `xmlns:dvconv` + `OverlayPanelConverter` 引用修复 | 3 |
| `NeoEditor.App/Helper/ViewServices.cs` | 废弃访问器类型重定向到 Plugin | 3 |
| `NeoEditor.App/Helper/GenericDataGridHelper.cs` | Converter 引用改为 Plugin using | 3 |
| `NeoEditor.App/Views/UserControls/SearchableDataGrid.axaml.cs` | Converter 引用改为 Plugin using | 3 |
| `NeoEditor.App/Services/` (5 files) | **删除** — DataGridInteractionState / DataGridNavigationService / DataGridCellInteractionService / IDataGridCellInteractionService / NavigationRouter | 3 |
| `NeoEditor.App/Helper/Converter/` (5 files) | **删除** — EntityMergedIdConverter / FieldConflictBackgroundConverter / FieldSourceConverter / ModNameColumnConverter / OverlayChainConverter | 3 |
| `NeoEditor.Plugins.DataViewer/ServiceCollectionExtensions.cs` | **新建** — `AddDataViewerPlugin()` 集中 DI 注册 | 4 |
| `NeoEditor.Tests/TestStubs.cs` | using 语句修复 | 2 |
| `NeoEditor.Tests/CoreFlowTests.cs` | using 语句修复 + ILocalizationService DI 注册 | 2 |

---

## 测试环境

- OS: Windows 10 Pro 22H2 (19045)
- .NET SDK: 10.0.301
- Avalonia: 11.3.12
- 分支: main

## 编译和自动化测试

| 项目 | 错误 | 警告 | 备注 |
|------|:--:|:--:|------|
| `bash build.sh` | 0 | 8 (已知 NU1903) | 10 项目全部通过 |
| NeoEditor.Messaging.Tests | — | — | 3/3 ✅ |
| NeoEditor.Core.Tests | — | — | 3/3 ✅ |
| NeoEditor.Infra.Tests | — | — | 2/2 ✅ |
| NeoEditor.UI.Common.Tests | — | — | 1/1 ✅ |
| NeoEditor.App.Tests | — | — | 2/2 ✅ |
| NeoEditor.Tests (旧) | — | — | 8/8 ✅ |
| **总计** | **0** | **8** | **19/19 ✅** |

## 人工验收（启动 App）

| 场景 | 预期 | 结果 |
|------|------|:--:|
| 启动 App | 日志无异常 | ✅ |
| 打开 Profile | DataTable 正常加载 | ✅ |
| 双击行打开编辑 | Visual / XML / KV 正常 | ✅ |
| 编辑字段 + Ctrl+S | 保存成功提示 | ✅ |
| 重启 App | 编辑内容持久化恢复 | ✅ |
| 设置页切换语言/主题 | 正常生效 | ✅ |

---

## 下一步 (M9 继续)

| # | 工作 | 说明 |
|---|------|------|
| 5 | 迁移 Views 到 Plugin | code-behind 需先重构解除 App 耦合（ViewServices/GenericDataGridHelper） |
| — | 重构 GenericDataGridHelper | 825 行 static → Plugin Services（DataTableService / ColumnTemplateFactory / InteractionHandler） |
| — | 拆分 ModGameDataTabsView | 3500 行 4 个 partial → Plugin Views + ViewModels |
| — | DataViewerPlugin.CreateToolView | 当前抛 NotImplementedException，View 迁移后可实现 |
