# Test Round 20: M13+ Phase 5 & 6 全部完成 ✅

> 2026-07-30 · 199/199 测试通过 · 0 Error · 0 新 Warning

---

## 概述

本轮完成 M13+ 两个并行 Phase：
- **Phase 5** — ImageAssetManager Tool Dock（ImageTools Plugin）
- **Phase 6** — Plugin 分类体系（Core Abstractions + R23-R25 落地）

两个 Phase 无相互依赖，Phase 6 先行（轻量，6 新文件），Phase 5 后行（含 UI 组件）。

---

## Phase 6: Plugin 分类

### 新增文件 (6)

| 文件 | 说明 |
|------|------|
| `Core/Abstractions/PluginKind.cs` | `PluginKind` 枚举（Workbench/Service/Feature） |
| `Core/Abstractions/PluginKindAttribute.cs` | `[PluginKind]` 特性（单次、不可继承） |
| `Core/Abstractions/IServicePlugin.cs` | 后端插件标记接口（空，继承 IPlugin） |
| `Core/Abstractions/IExtensionPoint.cs` | 泛型扩展点 `IExtensionPoint<TContext>` |
| `Core/Abstractions/ExtensionContexts.cs` | PreSaveContext / PostLoadContext / PreExecuteContext |
| `Tests/Core.Tests/Spec/PluginArchitectureTests.cs` | 6 个架构合规测试 |

### 修改文件 (5)

| 文件 | 变更 |
|------|------|
| `Core/Abstractions/IHostService.cs` | +3 方法：RegisterPreSaveHook / RegisterPostLoadHook / RegisterPreExecuteHook |
| `Infra/Services/HostService.cs` | +3 字段（List<T>）+ 3 方法实现（存储，未调用） |
| `Plugins.DataViewer/DataViewerPlugin.cs` | + `[PluginKind(Workbench)]` |
| `Plugins.EntityEditor/EntityEditorPlugin.cs` | + `[PluginKind(Workbench)]` |
| `Plugins.ImageTools/ImageToolsPlugin.cs` | + `[PluginKind(Workbench)]` |
| `Tests/Core.Tests/NeoEditor.Core.Tests.csproj` | + 3 个 ProjectReference（Plugin 程序集） |

### 架构测试 (6/6 ✅)

| 测试 | 验证内容 |
|------|---------|
| R23_EveryPlugin_HasExactlyOnePluginKind | 3 个 Plugin 各有唯一 [PluginKind] |
| R23_WorkbenchPlugins_MustImplement_ToolOrDocumentInterface | Workbench 需实现 IToolPlugin/IDocumentPlugin |
| R23_ServicePlugins_MustImplement_IServicePlugin | Service 需实现 IServicePlugin（空，为未来预留） |
| R25_IHostService_Declares_ExtensionPoint_RegistrationMethods | 扩展点方法在接口上声明 |
| R23_PluginKindAttribute_IsSingleUse_And_NotInherited | AllowMultiple=false, Inherited=false |
| R23_PluginKindAttribute_Has_KindProperty | Kind 属性类型为 PluginKind |

---

## Phase 5: ImageAssetManager

### 新增文件 (3)

| 文件 | 说明 |
|------|------|
| `ImageTools/ViewModels/ImageAssetManagerViewModel.cs` | ViewModel（树构建 + 搜索过滤 + 预览 + Refresh/OpenImage 命令） |
| `ImageTools/Views/ImageAssetManagerView.axaml` | UI：TreeView + GridSplitter + 预览面板 + 工具栏 |
| `ImageTools/Views/ImageAssetManagerView.axaml.cs` | 双击 handler → OpenImageCommand |

### 修改文件 (4)

| 文件 | 变更 |
|------|------|
| `App/ViewModels/MainContent/Documents.cs` | + ImageAssetManagerTool (Dock.Model Tool wrapper) |
| `App/ViewModels/MainContent/DocumentWorkspaceViewModel.cs` | + 属性 + 构造函数 DI 解析 |
| `App/Views/UserControls/DocumentWorkspaceView.axaml` | RightToolPane + "Image Assets" Tab |
| `ImageTools/ServiceCollectionExtensions.cs` | + AddSingleton<ImageAssetManagerViewModel> |

### 功能

- **树状浏览**：扫描 `GameRootDir/Mods/*` 和 `img/`，解析 `getimages.php`，按 Mod 分组
- **搜索过滤**：实时递归过滤 tree 节点
- **预览**：选中图片 → 右侧显示缩略图 + 尺寸 + Mod 名 + x2 版本
- **双击打开**：→ 发送 OpenImageDocumentMessage → ImageDocument 在 Center Dock 打开
- **刷新**：重新扫描目录重建树

---

## 测试结果

| 测试项目 | 通过 | 变化 |
|---------|:--:|------|
| Messaging.Tests | 3/3 | — |
| Core.Tests | **33/33** | +6 (PluginArchitectureTests) |
| Infra.Tests | 113/113 | — |
| UI.Common.Tests | 1/1 | — |
| DataViewer.Tests | 9/9 | — |
| EntityEditor.Tests | 26/26 | — |
| ImageTools.Tests | 4/4 | — |
| Integration.Tests | 10/10 | — |
| **总计** | **199/199** | **+6** |

---

## 架构合规

- Spec R23 (Plugin 分类) ✅ — 3 个 Plugin 全部标注
- Spec R24 (统一写路径) ✅ — 已有（Phase 1）
- Spec R25 (扩展点接口) ✅ — 接口已定义，调用延后到 Phase 7
- 0 新 C# Warning
- 仅已知 NU1903/NU1701/CS0618（非本轮变更）
