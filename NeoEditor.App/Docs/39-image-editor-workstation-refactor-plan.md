# 39 — 图像编辑工作站重构方案（完成版：创建/编辑双 Document）

> 日期：2026-08-02 | 状态：**已完成 ✅** | 分支：main
> 范围：`NeoEditor.Plugins.ImageTools` 的图像编辑工作站
> 上承：[33-image-generation-plan.md](33-image-generation-plan.md) · [30-post-m12-development-plan.md](30-post-m12-development-plan.md)
> 目标（用户确认）：**瘦身解耦 · 为插件化铺路 · 统一管线 · 提升可测试性 · 语义收敛**
>
> **最终形态**：创建/编辑**双 Document**。创建图像 Document 负责素材获取
> （导入 / AI 生成，生成即入待编辑列表），编辑图像 Document 负责单图加工
> （素材→裁剪→像素化→保存）。编辑已有图片只走编辑 Document。
> 全量测试 **577/577 通过**（ImageTools 91/91）。

---

## 〇、用户场景（设计基准）

| 场景 | 流程 | 经过的 Document |
|------|------|----------------|
| 1. 修改已有图片 | 资产管理器双击 → 裁剪/像素化/拉伸 → 保存 | **仅编辑** |
| 2. 创建图片（有素材） | 创建文档导入 → 待编辑列表 → Open in Editor → 保存 | **创建 → 编辑** |
| 3. 创建图片（无素材） | 创建文档 AI 生成 → 自动入列 → 选择 → Open in Editor → 保存 | **创建 → 编辑** |

三个场景共用同一套编辑语义；区别只在前置的「素材获取」阶段。

---

## 一、现状诊断（重构前）

| 文件 | 规模 | 问题 |
|------|------|------|
| `ViewModels/ImageEditorDocument.cs` | 953 行 | 上帝类：4 个位图槽 + 文件 IO + 裁剪 + 尺寸联动 + 像素化选项 + AI 生成 |
| `Views/ImageEditorDocumentView.axaml` | 420 行 | 4 个格子复制粘贴，~300 行重复；空态与 AI 槽与「编辑」职责无关 |
| `Views/ImageEditorDocumentView.axaml.cs` | 276 行 | 裁剪交互（指针/视口映射），本应属于格子自身的视图 |
| 双像素化管线 | `:309` vs `:349` | `PixelateImage` 走服务层（带裁剪），`PixelateAiImage` 绕过服务层直调 `PixelArtConversionService` |
| App 直接 `new` 插件 VM | `DocumentWorkspaceViewModel.cs` 三处 | 手工拼构造函数 |
| Bitmap 生命周期 | — | 文档关闭时 4 个 Bitmap 从不释放（泄漏） |

---

## 二、最终架构

```
┌─ ImageCreateDocument（创建图像, 单例, ~250 行）────────────────────────┐
│   左列(20%): [导入图片] 按钮 + 待编辑列表(多选) + [Open in Editor]     │
│   右列(80%): 顶部 AI 生成表单(prompt/尺寸/数量/进度)                    │
│              底部 Preview 大图(单击列表项 → 预览)                      │
│   AI 生成 → 每张候选落盘(StageAiCandidate) → 自动入待编辑列表          │
└──────────────────────────────────────────────────────────────────────┘
                                │ 每素材一个编辑文档
                                ▼
┌─ ImageEditorDocument（编辑图像, 306 行）───────────────────────────────┐
│   Source(素材+裁剪) → [应用] → Result(产物) · 过期标记 · [保存...]     │
│   （构造: IImageEditorProcessingService, IImageFileService, Loc）      │
└──────────────────────────────────────────────────────────────────────┘
```

### 2.1 创建图像 Document UI（最终）

```
┌──────────────────────┬──────────────────────────────────────────────┐
│ 20%                  │ 80%                                          │
│ [导入图片]            │ AI 生成: [prompt...................] [生成]  │
│ ──────────────────── │ Size: [512] × [512]  Count: [4]             │
│ ☐ sword.png          │ ──────────────────────────────────────────── │
│ ☑ ai_candidate_1.png │ Preview 大图                                 │
│ ☐ shield.png         │ (单击列表项 → 此处显示大图 + 名称/尺寸)       │
│ [Open in Editor]     │                                              │
└──────────────────────┴──────────────────────────────────────────────┘
```

### 2.2 编辑图像 Document UI（最终）

```
┌─────────────────────────────────────────────────────────────┐
│ 编辑图像: sword.png (128×128)                                 │
├───────────────────────────────┬─────────────────────────────┤
│   素材 + 裁剪框               │   产物（像素化后）           │
│   ┌───────────┐               │   ⚠ 参数已变更, 结果过期    │
│   │░░裁剪框░░░│               │                             │
│   └───────────┘               │   256×256 (2× 预览)         │
├───────────────────────────────┴─────────────────────────────┤
│ 像素化: [色数══●══] [Edges] [Dither] [透明]     [应用]      │
│ 目标尺寸: [宽 100] [高 100] [锁比例]                         │
│ [保存...]（始终弹框, 写 normal + x2_）                      │
└─────────────────────────────────────────────────────────────┘
```

---

## 三、实施记录（实际实现 vs 方案差异）

| # | 方案(v1-v3 设想) | 实际实现（用户迭代确认） |
|---|------------------|------------------------|
| 1 | AI 备选画廊网格 + 勾选入列 | **画廊网格删除**：生成后每张候选自动落盘入待编辑列表；查看靠右侧 Preview（用户明确"画廊位置不需要 preview"） |
| 2 | 创建文档左导入右 AI | **两列布局**：左 20% = 导入按钮 + 待编辑列表 + Open；右 80% = AI 表单(顶) + Preview 大图(底) |
| 3 | 点击备选打开编辑 | **单击列表项 = 选中 + 右侧预览**；勾选 = 批量；[Open in Editor] 按钮打开编辑；打开后列表项保留（可重复打开） |
| 4 | 右键添加图片直接复制到 mod img | **改为发 `OpenCreateImageDocumentMessage`**：文件只入待编辑列表，保存发生在编辑文档（不复制、不注册） |
| 5 | OpenAiImageWorkbenchMessage 语义升级 | 新增 `OpenCreateImageDocumentMessage`；`AddImage`/`OpenAiImageWorkbenchMessage` 均打开创建文档 |
| 6 | 过期标记 + 手动应用 | 一致（素材/裁剪/参数变更 → ⚠ → [应用] 重算并清除） |
| 7 | 统一管线（ImageSource） | 一致（`ImageSource.FromPath`/`FromBytes` 双源一致性测试锁定） |
| 8 | 裁剪交互随槽走 | 一致（`ImageSlotView.axaml.cs` 258 行, 工作台 code-behind 仅 11 行） |
| 9 | Bitmap 泄漏修复 | 一致（槽 VM `IDisposable` + `ConfirmCloseDockableAsync` 关闭时 dispose） |

### 3.1 踩坑记录（Avalonia 12 特有）

1. **`ListBox.SelectionMode` 已移除**（Avalonia 12），选中模型不可靠 → 用 `PointerPressed` 手动设置 `SelectedItem`。
2. **按钮 `IsEnabled` 显式绑定属性时必须手动发 `PropertyChanged`**：`CanGenerate`/`CanOpenPending` 变化时只通知 Command 的 CanExecute 不会刷新显式绑定 → AI 生成按钮、Open in Editor 按钮"一直 disabled"的根因。
3. **行内元素吞点击**：整行是 CheckBox 时点击不冒泡到列表选中 → 行拆为 `TextBlock(点击选中) + CheckBox(点击勾选)`。
4. **headless 测试平台把 PNG 解码为 1×1 placeholder**：裁剪/尺寸类测试用 `WriteableBitmap` 构造真实尺寸。

---

## 四、文件清单（最终）

**新增（13）**

| 文件 | 内容 |
|------|------|
| `NeoEditor.Core/Abstractions/IImageEditorDocumentFactory.cs` | 编辑文档工厂接口 |
| `NeoEditor.Plugins.ImageTools/Services/ImageEditorDocumentFactory.cs` | 工厂实现（`CreateDocument()` / `CreateDocument(path)`） |
| `NeoEditor.Plugins.ImageTools/Services/IImageFileService.cs` + `ImageFileService.cs` | 选图(多选)/ 保存 normal+x2_ / 命名约定 / 位图编解码 / StageAiCandidate / CleanupStagedCandidates |
| `NeoEditor.Plugins.ImageTools/ViewModels/ImageSlotViewModel.cs` + `Views/ImageSlotView.axaml(.cs)` | 素材/产物共用槽（含裁剪交互迁移） |
| `NeoEditor.Plugins.ImageTools/ViewModels/AiGenerationPanelViewModel.cs` + `Views/AiGenerationPanelView.axaml(.cs)` | AI 生成表单（CandidateGenerated 事件逐张回传） |
| `NeoEditor.Plugins.ImageTools/ViewModels/ImageCreateDocument.cs` + `Views/ImageCreateDocumentView.axaml(.cs)` | 创建图像文档 |
| `NeoEditor.Plugins.ImageTools/ViewModels/PendingImageItem.cs` | 待编辑列表项（Imported / AiGenerated） |
| 测试文件 6 个 | ImageEditorDocument / ImageSlot / AiGenerationPanel / ImageCreateDocument / ImageCropSelection / ImageEditorProcessingService / ImageFileService |

**修改（9）**

| 文件 | 改动 |
|------|------|
| `ImageEditorDocument.cs` | 953 → 306 行：Source/Result 双槽 + 尺寸/选项/过期标记/应用/保存 |
| `ImageEditorDocumentView.axaml` + `.cs` | 420+276 → 114+11 行 |
| `ImageEditorProcessingService.cs` + `IImageEditorProcessingService.cs` | `ImageSource(Bytes\|Path)` 统一入口 |
| `ServiceCollectionExtensions.cs` | 注册工厂 / 文件服务 / 创建文档与 AI 面板单例 |
| `DocumentWorkspaceViewModel.cs` | 3 处 `new` → 工厂；`AddImage`/`OpenAiImageWorkbenchMessage` → 创建文档；新增 `OpenCreateImageDocumentMessage` 处理；关闭文档 dispose |
| `DocumentWorkspaceView.axaml` | 注册 `ImageCreateDocument` DataTemplate |
| `ImageAssetManagerViewModel.cs` | 右键添加图片：多选 → 入创建文档列表（不再复制） |
| `NeoEditor.Core/Model/AppConfig.cs` | `AiCandidateCount`（默认 4, 持久化） |
| `NeoEditor.Core/Messages/ModMessages.cs` | 新增 `OpenCreateImageDocumentMessage` |

**删除**：`AiCandidate.cs`（画廊网格取消后不再需要）、编辑文档 AI 槽全部属性/命令、`InvalidateProcessedPreview` 清空语义（→ 过期标记）、4 槽相关测试。

---

## 五、最终决策表

| # | 决策 | 结论 |
|---|------|------|
| D1 | 裁剪交互代码去向 | ✅ 随素材区槽走（`ImageSlotView.axaml.cs`，IsCropEnabled 开关） |
| D2 | AI 面板结果回传 | ✅ 事件（`AiPanel.CandidateGenerated` 逐张），面板零感知工作台 |
| D4 | Bitmap 泄漏 | ✅ 本次修（槽 VM `IDisposable`，Workspace 关闭钩子调） |
| D6 | 参数/裁剪变更后产物更新 | ✅ **手动应用**：变更 → 过期标记 → 点 [应用] 重算 |
| D7 | 保存方式 | ✅ **始终弹对话框**（无覆盖写回分支） |
| D8 | AI 生成形态 | ✅ **N 个候选（数量可配 1-8, 持久化）→ 自动落盘入待编辑列表**（画廊勾选环节取消） |
| D9 | 架构形态 | ✅ **创建/编辑双 Document** |
| D10 | 素材汇总 | ✅ **统一待编辑列表**：导入 + AI 生成自动汇合 |
| D11 | 素材进入编辑 | ✅ **AI 先落盘临时目录 → 按路径打开**（编辑文档永远面向文件路径） |
| D12 | 交互模型 | ✅ 单击=选中+右侧预览；勾选=批量；[Open in Editor] 打开；打开后列表项保留 |

---

## 六、测试（577/577 通过）

| 测试 | 覆盖 |
|------|------|
| `ImageEditorDocumentTests` (9) | 素材加载/保存/过期标记/参数传递/裁剪 stale/dispose |
| `ImageSlotViewModelTests` (9) | dispose-on-replace、SourceBytes、裁剪 clamp、SaveCommand |
| `AiGenerationPanelViewModelTests` (7) | 数量配置、逐张事件、部分失败、属性通知 |
| `ImageCreateDocumentTests` (9) | 入列去重、AI 落盘、打开消息、选中预览、属性通知 |
| `ImageCropSelectionTests` (6) | Normalize clamp/最小尺寸/反转坐标 |
| `ImageEditorProcessingServiceTests` (4) | path/bytes 双源一致性、crop、无效请求 |
| `ImageFileServiceTests` (3) | 命名约定、StageAiCandidate 落盘/清理 |
| 其余项目 | 无回归 |

---

## 七、已知限制 / 后续

- **AI 落盘文件生命周期**：打开编辑后临时文件保留（编辑文档在读），下次应用启动创建文档构建时 `CleanupStagedCandidates` 懒清理。
- **创建文档为单例**：重复打开激活已有实例。
- **AI API 未配置时**：生成按钮禁用 + "AI not configured" 提示（配置在 Settings / 环境变量）。
- 后续可做：AI 面板按 R25 扩展点做成可插拔 Feature；编辑文档支持从剪贴板粘贴素材。
