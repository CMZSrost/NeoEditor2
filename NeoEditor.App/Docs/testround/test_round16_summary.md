# 架构测试第16轮 — M10 Phase 5: Editor Views/VMs 迁移

> 日期：2026-07-29 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round15_summary.md](test_round15_summary.md) (25 Visualizer 迁移到 Plugin)

## 本轮目标

完成 M10 Phase 5：将 EntityEditor 相关的 8 个 View + 3 个 ViewModel + 5 个辅助文件从 `NeoEditor.App` 迁移到 `NeoEditor.Plugins.EntityEditor`。

---

## 最终结果

| # | 工作项 | 状态 | 说明 |
|---|--------|:--:|------|
| VM-1 | `EntityEditorDocument` → Plugin | ✅ | 改用 PluginDocumentBase（DI ILocalizationService），新增 INotificationService 依赖 |
| VM-2 | `KeyValueEditorViewModel` → Plugin | ✅ | 沿用 FieldSection/FieldRow/EditControlType，Infra IWorkspaceSession |
| VM-3 | `OverlayChainToolContent` → Plugin | ✅ | 新增 ILocalizationService 构造参数 |
| V-1 | `EntityEditorView` → Plugin | ✅ | ViewServices.VisualizerRegistry → DI 解析 |
| V-2 | `KeyValueEditorView` → Plugin | ✅ | ValueConverter via global using |
| V-3 | `EntityViewerView` → Plugin | ✅ | IBrowserIndexService + VisualizerRegistry via DI |
| V-4 | `OverlayChainToolView` → Plugin | ✅ | XAML data bindings 无改动 |
| V-5 | `XmlDiffView` → Plugin (806 行) | ✅ | 最复杂的 View；Loc/Logger 延迟 DI，AttachedProperties 重定向 |
| V-6 | `DiffPreviewTrack` → Plugin | ✅ | 附带自定义渲染控件 |
| Dialog-1 | `ModGameDataSavePreviewDialog` → Plugin | ✅ | ILocalizationService 属性改为延迟 DI |
| Dialog-2 | `MergeXmlExportDialog` → Plugin | ✅ | ExportItem 记录类型，ShowAsync 静态方法 |
| H-1 | `HighlightBackgroundRenderHelper` → Plugin | ✅ | 完整迁移 |
| H-2 | `XmlCompareHelper` → Plugin | ✅ | XMLDiffPatch 封装 |
| H-3 | `TextEditorScrollSyncAttached` → Plugin | ✅ | AttachedProperty 同步滚动 |
| Arch-1 | `PluginDocumentBase` 创建 | ✅ | 替代 App DocumentBase，DI 版 Loc |
| Arch-2 | `IDocumentBase` → Core | ✅ | 共享契约接口 |
| — | 旧 App 文件删除（17 个） | ✅ | 包含 Views + ViewModels + Helpers + Dialogs |
| — | App DataTemplates 更新 | ✅ | DocumentWorkspaceView.axaml 指向 Plugin 命名空间 |
| — | DI 参数补全 | ✅ | 3 处 `new EntityEditorDocument` 加 Loc + Notification 参数 |
| — | 编译验证 | ✅ | 13 项目 0 Error |

---

## 1. 迁移策略：PluginDocumentBase + IDocumentBase 契约

### 问题

App 侧 `DocumentBase` 使用 `ViewServices.Loc`（服务定位器）来获取本地化服务。Plugin 若直接引用会形成对 App 的反向依赖（违反 R18）。

### 方案

**App 侧保留 `DocumentBase` 不变**（仍用 `ViewServices.Loc`，兼容已有的 XmlDocument、ImageDocument 等）。

**Plugin 侧创建 `PluginDocumentBase`**，构造函数注入 `ILocalizationService`，暴露为 `public ILocalizationService Loc { get; }` 属性供 XAML 绑定：

```csharp
public abstract partial class PluginDocumentBase : ObservableObject, IDocumentBase
{
    public ILocalizationService Loc { get; }
    protected PluginDocumentBase(ILocalizationService loc) { Loc = loc; ... }
}
```

### IDocumentBase → Core

将 `IDocumentBase` 接口从 App 提升到 `NeoEditor.Core.Abstractions`，Plugin 和 App 共同实现：

| 位置 | 类型 |
|------|------|
| `Core/Abstractions/IDocumentBase.cs` | 接口定义（Title/CanClose/SetStaticTitle/…） |
| App `DocumentBase` | 实现 `Core.Abstractions.IDocumentBase` |
| Plugin `PluginDocumentBase` | 实现 `Core.Abstractions.IDocumentBase` |

App 的 `ObservableCollection<IDocumentBase> Documents` 仍接受两种类型。

---

## 2. View 服务解析迁移

所有 Plugin View 从 `ViewServices.Xxx` 静态定位改为 `Application.Current.Resources["Services"]` DI 容器解析：

```csharp
// 旧（App）：
var registry = ViewServices.VisualizerRegistry;
var loc = ViewServices.Loc;

// 新（Plugin）：
private static T GetService<T>() where T : notnull
    => (Application.Current?.Resources["Services"] as IServiceProvider)!.GetRequiredService<T>();

var registry = GetService<EntityVisualizerRegistry>();
var loc = GetService<ILocalizationService>();
```

### XmlDiffView 特殊处理

XmlDiffView（806 行 code-behind）使用了 4 个不同的服务，通过延迟初始化属性避免构造时崩溃：

```csharp
private ILocalizationService Loc => _loc ??= GetService<ILocalizationService>();
private ILocalizationService? _loc;
private ILogger<XmlDiffView> Logger => _logger ??= GetService<ILoggerFactory>().CreateLogger<XmlDiffView>();
private ILogger<XmlDiffView>? _logger;
```

---

## 3. 编译修复记录

### 命名空间冲突

| 问题 | 根因 | 修复 |
|------|------|------|
| `Converters` 不明确 | `NeoEditor.UI.Common.Converters` 不存在，实际是 `NeoEditor.Helper.Converter` | Global using 改为 `NeoEditor.Helper.Converter` |
| `AttachedProperties` 不明确 | 同 — 在 UI.Common 中是 `NeoEditor.Helper.AttachedProperties` | Global using 改为 `NeoEditor.Helper.AttachedProperties` |
| `ReferenceParser` 找不到 | Plugin 的 `Helper` 命名空间不包含 `ReferenceParser`（在 Infra 的 `NeoEditor.Helper`） | 显式 `NeoEditor.Helper.ReferenceParser` |
| `IWorkspaceSession` 歧义 | Core 和 Infra 各有一个接口（Core 子集，Infra 完整版） | Plugin 用 `NeoEditor.Services.IWorkspaceSession`（Infra），删 `Core.Abstractions` using |

### IDocumentBase 兼容

| 问题 | 根因 | 修复 |
|------|------|------|
| `EntityEditorDocument` → `IDocumentBase` 不可转换 | Plugin 的 `IDocumentBase`（Core）与 App 的 `IDocumentBase`（App 内定义）是不同类型 | App `IDocumentBase` 改为空接口继承 Core 版，App `DocumentBase` 实现 Core `IDocumentBase` |

---

## 4. Plugin 结构（Phase 5 后）

```
NeoEditor.Plugins.EntityEditor/
├── EntityEditorPlugin.cs
├── ServiceCollectionExtensions.cs
├── Helper/
│   ├── AttachedProperties/
│   │   └── TextEditorScrollSyncAttached.cs    ← M10.5: 新增
│   ├── HighlightBackgroundRenderHelper.cs     ← M10.5: 新增
│   └── XmlCompareHelper.cs                   ← M10.5: 新增
├── Services/
│   ├── VisHelperService.cs
│   └── RefNode.cs
├── ViewModels/
│   ├── PluginDocumentBase.cs                 ← M10.5: 新增（DI 基类）
│   ├── EntityEditorDocument.cs              ← M10.5: 新增
│   ├── KeyValueEditorViewModel.cs           ← M10.5: 新增
│   └── OverlayChainToolContent.cs           ← M10.5: 新增
├── Views/
│   ├── DiffPreviewTrack.cs                  ← M10.5: 新增
│   ├── EntityEditorView.axaml/.cs           ← M10.5: 新增
│   ├── KeyValueEditorView.axaml/.cs         ← M10.5: 新增
│   ├── MergeXmlExportDialog.axaml/.cs       ← M10.5: 新增
│   ├── ModGameDataSavePreviewDialog.axaml/.cs ← M10.5: 新增
│   ├── OverlayChainToolView.axaml/.cs       ← M10.5: 新增
│   ├── XmlDiffView.axaml/.cs               ← M10.5: 新增
│   └── ZoomableImageView.axaml/.cs
└── Visualizers/ (25)
```

---

## 5. App 中已删除的文件

| 文件 | 备注 |
|------|------|
| `App/ViewModels/MainContent/EntityEditorDocument.cs` | → Plugin |
| `App/ViewModels/MainContent/KeyValueEditorViewModel.cs` | → Plugin |
| `App/ViewModels/MainContent/OverlayChainToolContent.cs` | → Plugin |
| `App/Views/UserControls/EntityEditorView.axaml/.cs` | → Plugin |
| `App/Views/UserControls/KeyValueEditorView.axaml/.cs` | → Plugin |
| `App/Views/UserControls/OverlayChainToolView.axaml/.cs` | → Plugin |
| `App/Views/UserControls/XmlDiffView.axaml/.cs` | → Plugin（806 行 code-behind） |
| `App/Views/UserControls/DiffPreviewTrack.cs` | → Plugin |
| `App/Views/Dialog/ModGameDataSavePreviewDialog.axaml/.cs` | → Plugin |
| `App/Views/Dialog/MergeXmlExportDialog.axaml/.cs` | → Plugin |
| `App/Helper/HighlightBackgroundRenderHelper.cs` | → Plugin |
| `App/Helper/AttachedProperties/TextEditorScrollSyncAttached.cs` | → Plugin |
| `App/Helper/XmlCompareHelper.cs` | → Plugin |

---

## 6. 剩余 App 文件变化

App 中仍有这些文件引用了迁移后的类型，已做兼容处理：

| App 文件 | 改动 |
|----------|------|
| `DocumentWorkspaceView.axaml` | DataTemplates 换为 `eeViews:`/`eeVm:` 命名空间 |
| `DocumentWorkspaceViewModel.cs` | `new EntityEditorDocument()` 加两个 DI 参数（ILocalizationService + INotificationService） |
| `Documents.cs` | `IDocumentBase` 改为空接口继承 `Core.Abstractions.IDocumentBase` |
| `MainWindowViewModel.cs` | `OpenedDocuments` 类型改为 `IDocumentBase`（Core 版） |
| `App.csproj` | 加 global using `NeoEditor.Plugins.EntityEditor.ViewModels` / `Views` / `Helper` |
| `ModGameDataTabsView.Data.cs` | 删除 `using NeoEditor.Views.Dialog`，类名自动解析（Core global using） |

---

## 7. 架构合规验证

| 规则 | 检查项 | 结果 |
|------|--------|:--:|
| R18 | Plugin 不引用 App | ✅ — 0 对 `NeoEditor.App` 的 ProjectReference |
| R04 | View 不写业务逻辑 | ✅ — EntityEditorView/KeyValueEditorView/XmlDiffView 仅做 UI 协调 |
| N03 | View 不放导航逻辑 | ✅ — 导航通过 Message 和 SelectionService |
| R07 | 单向分层 | ✅ — Plugin → Core/Infra/UI.Common，不反向 |
| N01 | 无新增静态可变状态 | ✅ — 所有 Plugin View 用延迟 DI 而非静态 |
| — | App 中 `PluginDocumentBase` 引用 | **0** — App 仍用自身 `DocumentBase` |
| — | Plugin 中 `ViewServices` 引用 | **0** — 全部改为 `GetService<T>()` DI |
| — | Plugin 中 `NeoEditor.App` 命名空间 | **0** |

---

## 编译和自动化测试

| 项目 | 错误 | 警告 | 备注 |
|------|:--:|:--:|------|
| `bash build.sh`（13 项目） | **0** | 12 (NU1903) | 13 src + 5 test 全部通过 |
| DataViewer.Tests | — | — | 10/10 ✅ |

> EntityEditor Plugin 暂**无独立测试项目**（Phase 8 创建）。

---

## 已知问题

| # | 问题 | 严重性 | 计划 |
|---|------|:--:|------|
| 1 | EntityEditor 仍引用 DataViewer Plugin（R17 例外） | 中 | Phase 6: DataTableService 数据部分提取到 Infra |
| 2 | App `ViewServices.cs` 仍被 20+ 文件使用 | 低 | M12: 全局清理 |
| 3 | App `VisHelper.cs` 仍存在（864 行） | 低 | Phase 6: 删除 |
| 4 | App `RefNode.cs` 仍存在 | 低 | Phase 6: 删除 |
| 5 | App `ZoomableImageView` 副本 | 低 | Phase 6: 删除 |
| 6 | 无 EntityEditor.Tests | 中 | Phase 8: 创建 |

---

## 下一步

| # | 工作 | 说明 |
|---|------|------|
| 6 | DI 简化 + App 清理 | 删除旧 `VisHelper.cs` / `RefNode.cs` / `ZoomableImageView` App 副本；DataTableService 数据部分提取到 Infra |
| 7 | DocumentWorkspaceViewModel 解耦 | `new EntityEditorDocument(...)` → `IDocumentPlugin` 工厂 |
| 8 | EntityEditor.Tests + 全链路验收 | EntityEditorDocument / KeyValueEditorViewModel / Visualizer 单测 + 人工验收场景 |
