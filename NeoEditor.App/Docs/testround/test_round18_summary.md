# 架构测试第18轮 — M11 ImageTools Plugin 完整迁移

> 日期：2026-07-29 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round17_summary.md](test_round17_summary.md) (M10 Phase 6-8)

## 本轮目标

完成 M11 ImageTools Plugin 完整迁移：将图片编辑功能从 App 完全迁移到 `NeoEditor.Plugins.ImageTools`。

---

## 迁移内容

### 新建的 Plugin 文件 (17 个)

| 文件 | 说明 |
|------|------|
| `ViewModels/ImageToolDocumentBase.cs` | Plugin 侧 Document 基类（DI 注入 ILocalizationService） |
| `ViewModels/ImageToolObservableObject.cs` | Plugin 侧 ObservableObject 基类 |
| `ViewModels/ImageEditorDocument.cs` | 图片编辑器 VM（从 App 迁移，改为 PluginDocumentBase 继承） |
| `ViewModels/ImageCropSelection.cs` | 裁剪选区结构体 |
| `ViewModels/ModImagesDocument.cs` | Mod 图片列表 VM（从 App 迁移，改 DI 注入） |
| `ViewModels/ImagePreviewContent.cs` | 图片预览 VM（从 App 迁移，改 DI 注入 IConfigService + IImageSearchService） |
| `Services/IImageSearchService.cs` | 图片搜索目录接口 |
| `Services/ImageSearchService.cs` | 图片搜索目录实现（从 App ImageService 内联） |
| `Services/IModImageListService.cs` | Mod 图片列表操作接口 |
| `Helper/CropSelectionInteraction.cs` | 裁剪交互逻辑 |
| `Helper/ImageSelectionOverlayPresenter.cs` | 裁剪覆盖层呈现器 |
| `Helper/ImageSelectionViewportMapper.cs` | 裁剪视口映射 |
| `Helper/ModImagePairDropHandler.cs` | 图片对拖放处理 |
| `Views/ImageEditorDocumentView.axaml/.cs` | 图片编辑器 View（XAML 中 x:DataType 指向 Plugin VM） |
| `Views/ModImagesDocumentView.axaml/.cs` | Mod 图片 View |
| `Views/ImagePreviewView.axaml/.cs` | 图片预览 View |

### App 侧清理

| 操作 | 文件 |
|------|------|
| 删除重复服务 | `Services/IImageEditorProcessingService.cs`, `Services/ImageEditorProcessingService.cs` |
| 删除重复 Helper | `Helper/ImageEditor/PixelArtOutputSizeCalculator.cs` |
| 删除旧 VM/View | `ImageEditorDocument.cs`, `ImageCropSelection.cs`, `ModImagesDocument.cs`, `ImagePreviewContent.cs` + 对应 6 个 View 文件 |
| 删除旧 Helper | 3 个 ImageEditor Helper + `ModImagePairDropHandler.cs` |
| 新建桥接 | `Services/ModImageListService.cs` — 包装 PhpParser + RenameImagePairDialog |
| 更新 DI | App.axaml.cs: 删除 `IImageEditorProcessingService` 注册 → 由 `AddImageToolsPlugin()` 接管 |
| 更新 DataTemplates | DocumentWorkspaceView.axaml: 指向 `itVm:ModImagesDocument` / `itVm:ImageEditorDocument` |
| 更新 RightPanelView | XAML + code-behind 指向 `itViews:ImagePreviewView` 和 Plugin 命名空间 |
| 更新 DocumentWorkspaceViewModel | 4 处创建语句改为 Plugin 命名空间 + 新构造参数 |

### Plugin csproj 新增依赖

```
Avalonia.Controls.DataGrid, FluentIcons.Avalonia, Irihi.Ursa, Xaml.Behaviors.Avalonia
```

### 新增测试项目

`Tests/NeoEditor.Plugins.ImageTools.Tests/` — 4 个测试：

| 测试文件 | 测试数 |
|----------|:------:|
| `Services/ImageEditorProcessingServiceTests.cs` | 1 |
| `Services/ImageSearchServiceTests.cs` | 3 |

---

## 架构合规验证

| 规则 | 检查项 | 结果 |
|------|--------|:--:|
| R18 | Plugin 不依赖 App | ✅ — ImageTools.csproj 0 对 NeoEditor.App 的 ProjectReference |
| R07 | 单向分层 | ✅ — Plugin → Core/Infra/UI.Common，不反向 |
| N01 | 无新增静态可变状态 | ✅ — 所有依赖走 DI 注入 |
| R14 | 文件夹+命名空间约定 | ✅ — `ViewModels/`, `Views/`, `Services/`, `Helper/` |

---

## 编译和测试

| 项目 | 结果 |
|------|:----:|
| `bash build.sh` (16 项目) | **0 Error, 6 Warning** (仅 NU1903, 0 CS) |
| DataViewer.Tests | 10/10 ✅ |
| EntityEditor.Tests | 9/9 ✅ |
| ImageTools.Tests | **4/4 ✅ [新]** |
| Messaging/Core/Infra/UI.Common/App.Tests | 11/11 ✅ |
| **总计** | **34/34 ✅** |

---

## 下一步

| # | 工作 | 说明 |
|---|------|------|
| **M12** | 收尾 & 清理 | Integration.Tests + 全局死代码清理 + 文档终稿 |
| M12.1 | `ViewServices.cs` 清理 | 20+ 文件仍需使用；过渡到全 DI |
| M12.2 | `DataTableService.Instance` | DataViewer.Tests 中仍在使用 |
| M12.3 | 删除 `NeoEditor.Tests` 旧项目 | 44 error，类型已迁出 |
