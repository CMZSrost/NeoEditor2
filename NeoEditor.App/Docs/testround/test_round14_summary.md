# 架构测试第14轮 — M9 收尾 + M10 EntityEditor Plugin 基础设施

> 日期：2026-07-28 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round13_summary.md](test_round13_summary.md) (M9 DataViewer Plugin 核心完成)

## 本轮目标

M9 延后项收尾 + M10 EntityEditor Plugin 骨架搭建。包含：DataTableService.Instance DI 注入、IEntityVisualizer 共享契约重定位、EntityEditor Plugin 项目创建、VisHelper 静态→DI 重构、RefNode Plugin 复制。

---

## 最终结果

| # | 工作项 | 状态 | 说明 |
|---|--------|:--:|------|
| M9-1 | DataTableService.Instance → DI（6 文件） | ✅ | BottomTools/SearchPane/EntityEditorDocument/DataExportService/ReferenceInspector/DocumentWorkspace |
| M9-2 | ReferenceIntegrityRule + ReferenceResolver.Instance | ⏭️ | 延后（需 ValidationService DI 化） |
| M9-3 | ModGameDataTabsView → Plugin DataTableView | ⏭️ | 延至 M12（4045 行，风险高） |
| 0a | IEntityVisualizer → UI.Common | ✅ | EntityVisualizerRegistry 随同迁移 |
| 0b | UI.Common 加 Core 引用 | ✅ | UI 工具箱可引用领域 IEntity 类型 |
| 0c | EditorUIFactory → UI.Common | ✅ | 纯 Avalonia UI 工厂，0 App 依赖 |
| 1a | EntityEditor Plugin 新项目 | ✅ | csproj + EntityEditorPlugin.cs + ServiceCollectionExtensions |
| 1b | 加入 .sln + App 引用 | ✅ | App.csproj ProjectReference + DI 注册 |
| 2a | VisHelperService 创建 | ✅ | 864 行 DI 单例，5 构造函数参数 |
| 2b | ZoomableImageView Plugin 副本 | ✅ | 双存（App + Plugin），后续 Phase 5 清理 |
| 3 | RefNode Plugin 副本 | ✅ | 双注册（旧 App 版 + 新 Plugin 版） |
| — | 编译 + 测试 | ✅ | 0 Error，6 Warning（已知 NU1903），10/10 测试通过 |

---

## 1. M9 收尾：DataTableService.Instance DI 迁移

### 已完成的 DI 注入

| 文件 | 改动 | 行数 |
|------|------|:--:|
| `ViewModels/MainContent/BottomToolsViewModel.cs` | 构造函数加 `DataTableService` 参数 | +3 |
| `ViewModels/ExplorerPane/SearchPaneViewModel.cs` | 构造函数加 `DataTableService` 参数 | +3 |
| `ViewModels/MainContent/EntityEditorDocument.cs` | 构造函数加 `DataTableService` 参数 | +3 |
| `Services/DataExportService.cs` | 构造函数加 `DataTableService` 参数 | +3 |
| `Views/UserControls/ReferenceInspectorView.axaml.cs` | `Instance?.NavigateToByEntityId` → `ViewServices.Get<>()` | 1 行 |
| `ViewModels/MainContent/DocumentWorkspaceViewModel.cs` | 3 处 `new EntityEditorDocument` 调用点加 `DataTableService` 参数 | +3 处 |

### 延后项

| 项 | 原因 |
|----|------|
| `ReferenceIntegrityRule.cs`（3 处） | `ValidationService` 用 `new` 创建规则，未注册 DI |
| `ReferenceResolver.cs`（`static Svc` 属性） | 需与 `ReferenceResolver.Instance` 静态模式一起重构 |
| `ModGameDataTabsView.Tab.cs`（1 处） | 文件本身待 M12 迁移到 Plugin |
| `VisHelper.cs` + 9 个 Visualizer（28 处） | 整个文件随 M10 迁移到 Plugin |

---

## 2. Phase 0：共享契约重定位

### 问题

M9 将 `IEntityVisualizer` + `EntityVisualizerRegistry` 放在 **DataViewer Plugin**，但 M10 的 25 个 Visualizer 要移到 **EntityEditor Plugin**。若不做调整，EntityEditor 必须引用 DataViewer → 违反 R17。

### 解决方案

将两者移至 **UI.Common**（共享 UI 工具箱，非 Plugin）。

### 改动清单

| 文件 | 操作 |
|------|------|
| `NeoEditor.UI.Common.csproj` | 加 `<ProjectReference>` 到 Core |
| `UI.Common/Visualizers/IEntityVisualizer.cs` | **新建**，命名空间 `NeoEditor.UI.Common.Visualizers` |
| `UI.Common/Services/EntityVisualizerRegistry.cs` | **新建**，命名空间 `NeoEditor.UI.Common.Services` |
| `DataViewer/IEntityVisualizer.cs` | **删除** |
| `DataViewer/Services/EntityVisualizerRegistry.cs` | **删除** |
| `DataViewer/ServiceCollectionExtensions.cs` | 移除 `EntityVisualizerRegistry` 注册，加回 `using NeoEditor.Helper` |
| `App/App.axaml.cs` | 加 `services.AddSingleton<EntityVisualizerRegistry>()` |
| `App/Helper/ViewServices.cs` | `VisualizerRegistry` getter 更新命名空间 |
| `App/NeoEditor.App.csproj` | 加 global using `NeoEditor.UI.Common.Services` + `NeoEditor.UI.Common.Visualizers` |
| `DataViewer/NeoEditor.Plugins.DataViewer.csproj` | 加 global using `NeoEditor.UI.Common.Services` + `Visualizers` |
| `DataViewer/Views/PeekPanelView.axaml.cs` | 加 `using NeoEditor.UI.Common.Services` |

25 个 Visualizer + 4 个 View code-behind 通过全局 using 自动切换，无需逐个修改。

---

## 3. Phase 1：EntityEditor Plugin 骨架

### 新建项目

```
NeoEditor.Plugins.EntityEditor/
├── NeoEditor.Plugins.EntityEditor.csproj   ← 引用 Core + Infra + UI.Common + DataViewer
├── EntityEditorPlugin.cs                    ← IToolPlugin 实现
├── ServiceCollectionExtensions.cs           ← AddEntityEditorPlugin() 扩展方法
├── Services/
│   ├── VisHelperService.cs                 ← Phase 2（DI 版 VisHelper）
│   └── RefNode.cs                          ← Phase 3（Plugin 版 RefNode）
└── Views/
    ├── ZoomableImageView.axaml              ← Phase 2b（Plugin 副本）
    └── ZoomableImageView.axaml.cs
```

### csproj 依赖

| 引用 | 用途 |
|------|------|
| `NeoEditor.Core` | IEntity, Plugin 契约 |
| `NeoEditor.Infra` | IReferenceResolver, INavigationRouter, ILocalizationService |
| `NeoEditor.UI.Common` | IEntityVisualizer, EntityVisualizerRegistry, EditorUIFactory |
| `NeoEditor.Plugins.DataViewer` | DataTableService（**临时 R17 例外**，待 DataTableService 数据部分提取到 Infra 后解除） |

### NuGet 包

Avalonia, Avalonia.Controls.DataGrid, AvaloniaEdit.TextMate, CommunityToolkit.Mvvm, DiffPlex, FluentIcons.Avalonia, Microsoft.EntityFrameworkCore, XMLDiffPatch

### App 集成

- `App.csproj`：加 `<ProjectReference>` + global using `NeoEditor.Plugins.EntityEditor`
- `App.axaml.cs`：`services.AddEntityEditorPlugin()` + `VisHelperService` 独立注册

---

## 4. Phase 2：VisHelper → VisHelperService

### 改造对比

| 方面 | 旧（App/VisHelper.cs） | 新（Plugin/VisHelperService.cs） |
|------|----------------------|--------------------------------|
| 类型 | `internal static class` | `public class` |
| 依赖注入 | `SetServices(img, resolver, router)` | 构造函数注入 5 参数 |
| DataTableService | `DataTableService.Instance?.Xxx` | `_dataTable.Xxx`（直接注入） |
| 图片查找 | `IImageService.FindImage()` | `Func<string, string?>` 委托（R18 边界桥接） |
| 本地化 | `ViewServices.Loc[key]` | `_loc[key]`（ILocalizationService 注入） |
| 命名空间 | `NeoEditor.Views.UserControls.Editors` | `NeoEditor.Plugins.EntityEditor.Services` |

### App→Plugin 边界桥接

**问题**：`IImageService` 在 App 项目，Plugin 不能引用 App（R18）。

**方案**：VisHelperService 构造函数接收 `Func<string, string?>` 委托代替 `IImageService`。App.axaml.cs 注册时传入 `imgSvc.FindImage` 方法引用。

```csharp
// App.axaml.cs
services.AddSingleton<VisHelperService>(sp =>
    new VisHelperService(
        sp.GetRequiredService<IImageService>().FindImage,  // 委托桥接
        sp.GetRequiredService<IReferenceResolver>(),
        sp.GetRequiredService<INavigationRouter>(),
        sp.GetRequiredService<DataTableService>(),
        sp.GetRequiredService<ILocalizationService>()));
```

### 旧 VisHelper.cs 保留

App 中 `VisHelper.cs`（864 行）**暂时保留**，25 个未迁移的 Visualizer 仍在使用。Phase 4 迁移完成后删除。

---

## 5. Phase 3：RefNode Plugin 副本

### 双注册策略

| 实例 | 注册位置 | 消费者 |
|------|---------|--------|
| `Helper.RefNode`（App 旧版） | App.axaml.cs `services.AddSingleton<Helper.RefNode>()` | 25 个 App 内 Visualizer |
| `Plugin.Services.RefNode`（Plugin 新版） | App.axaml.cs 独立注册 | 后续迁移到 Plugin 的 Visualizer |

旧版 RefNode 将在全部 Visualizer 迁移完成后删除。

### 旧注释订正

原注释 "MUST stay in the App layer" 已过时。RefNode 仅依赖 `IReferenceResolver` + `INavigationRouter`（Infra 接口）+ Avalonia 类型，可安全驻留 Plugin。

---

## 6. 迁移后的 EditorUIFactory

| 属性 | 旧 | 新 |
|------|----|----|
| 项目 | NeoEditor.App | NeoEditor.UI.Common |
| 位置 | `App/Helper/EditorUIFactory.cs` | `UI.Common/Controls/EditorUIFactory.cs` |
| 命名空间 | `NeoEditor.Helper` | `NeoEditor.Helper`（不变） |
| 依赖 | Avalonia | Avalonia（无新增） |

App 中旧文件已删除。命名空间不变，所有调用方无需修改。

---

## 编译和自动化测试

| 项目 | 错误 | 警告 | 备注 |
|------|:--:|:--:|------|
| `bash build.sh` | 0 | 6 (NU1903) | 11 src + 5 test 全部通过 |
| DataViewer.Tests | — | — | 10/10 ✅ |
| **总计** | **0** | **已知** | **10/10 ✅** |

> EntityEditor Plugin 暂无独立测试项目（Phase 8 创建）。

---

## 架构合规验证

| 规则 | 检查项 | 结果 |
|------|--------|:--:|
| N01 | 无新增静态可变状态 | ✅ |
| R01 | IWorkspaceSession 单所有者 | ✅ |
| R17 | Plugin 互不引用 | ⚠️ EntityEditor 临时引用 DataViewer（DataTableService），待 Phase 6 提取接口后解除 |
| R18 | Plugin 不依赖 App | ✅（IImageService 通过委托桥接，DataTableService 通过 DI 注入） |
| R03 | 引用解析走注入 IReferenceResolver | ✅（VisHelperService 构造函数注入） |
| N02 | 无 ReferenceResolver.Instance 新增 | ✅ |
| — | UI.Common 0 业务逻辑 | ✅（仅 UI 类型 + 简单注册表） |
| — | VisHelperService 0 静态可变字段 | ✅ |

---

## 架构变更总结

### 新增项目
- `NeoEditor.Plugins.EntityEditor`（11 个 src 项目 → 12 个）

### UI.Common 职责扩展
```
NeoEditor.UI.Common/
├── Visualizers/IEntityVisualizer.cs     ← M10: 从 DataViewer Plugin 移入
├── Services/EntityVisualizerRegistry.cs  ← M10: 从 DataViewer Plugin 移入
├── Controls/EditorUIFactory.cs          ← M10: 从 App 移入
├── Converters/                           ← M8: 原有
├── Behaviors/                            ← M8: 原有
└── Themes/                               ← M8: 原有
```

### 依赖方向
```
EntityEditor Plugin
  ├── Core          ✅ 正式
  ├── Infra         ✅ 正式
  ├── UI.Common     ✅ 正式
  └── DataViewer    ⚠️ 临时（R17 例外），待 DataTableService 数据部分提取到 Infra
```

---

## 已知问题

| # | 问题 | 严重性 | 计划 |
|---|------|:--:|------|
| 1 | EntityEditor 引用 DataViewer Plugin（R17 例外） | 中 | Phase 6 将 DataTableService 数据访问部分提取到 Infra |
| 2 | App 中 ZoomableImageView 副本 | 低 | Phase 5 清理 App 副本，DocumentWorkspaceView 改用 Plugin 版本 |
| 3 | App 中 VisHelper.cs 仍存在 | 低 | Phase 4 Visualizer 迁移完成后删除 |
| 4 | App 中 RefNode.cs 仍存在 | 低 | Phase 4 Visualizer 迁移完成后删除 |
| 5 | 无 EntityEditor.Tests | 中 | Phase 8 创建 |

---

## 当前 EntityEditor Plugin 结构

```
NeoEditor.Plugins.EntityEditor/
├── EntityEditorPlugin.cs               ← IToolPlugin（文档型，Center Dock）
├── ServiceCollectionExtensions.cs       ← 插件 DI 注册入口
├── Services/
│   ├── VisHelperService.cs             ← DI 版 VisHelper（864 行）
│   └── RefNode.cs                      ← Plugin 版引用节点渲染器
└── Views/
    ├── ZoomableImageView.axaml          ← 可缩放图片控件
    └── ZoomableImageView.axaml.cs
```

---

## 下一步

| # | 工作 | 说明 |
|---|------|------|
| 4 | 迁移 25 个 Visualizer 到 Plugin | 分 3 批：7 简单 + 9 中等 + 9 复杂。每个模式：加 VisHelperService/RefNode/DataTableService 构造参数，VisHelper.Xxx → _vis.Xxx |
| 5 | 迁移 Editor Views/ViewModels | EntityEditorView, KeyValueEditorView, XmlDiffView, EntityEditorDocument, 对话框等 |
| 6 | DI 注册简化 + App 清理 | 删除旧 VisHelper.cs / RefNode.cs / ZoomableImageView，Visualizer 注册移到 Plugin |
| 7 | DocumentWorkspaceViewModel 解耦 | new EntityEditorDocument → IDocumentPlugin 工厂 |
| 8 | EntityEditor.Tests + 全链路验收 | VisHelperService, EntityEditorDocument, KeyValueEditorViewModel, Visualizer 单测 |
