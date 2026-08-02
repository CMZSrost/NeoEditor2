# 39 — 图像编辑工作站重构方案（v3：创建/编辑双 Document）

> 日期：2026-08-02 | 状态：**草案 v3（待评审）** | 分支：main
> 范围：`NeoEditor.Plugins.ImageTools` 的图像编辑工作站
> 上承：[33-image-generation-plan.md](33-image-generation-plan.md) · [30-post-m12-development-plan.md](30-post-m12-development-plan.md)
> 目标（用户确认）：**瘦身解耦 · 为插件化铺路 · 统一管线 · 提升可测试性 · 语义收敛**
>
> **v3 变化**：v2 的「单文档空态/编辑态切换」升级为**两个 Document**——
> `创建图像 Document`（素材获取：导入 / AI 备选）与 `编辑图像 Document`（单图加工）。
> 创建流程先走创建文档 → 素材落盘 → 打开编辑文档；编辑已有图片只走编辑文档。
> Dock 并行：等待 AI 生成时可切去编辑其他图，互不阻塞。

---

## 〇、用户场景（v3 的设计基准）

| 场景 | 流程 | 经过的 Document |
|------|------|----------------|
| 1. 修改已有图片 | 资产管理器打开 → 裁剪/像素化/拉伸 → 保存 | **仅编辑** |
| 2. 创建图片（有素材） | 创建文档导入 → 待编辑列表 → 打开编辑 → 保存 | **创建 → 编辑** |
| 3. 创建图片（无素材） | 创建文档 AI 生成备选 → 勾选入列 → 打开编辑 → 保存 | **创建 → 编辑** |

三个场景共用同一套编辑语义；区别只在前置的「素材获取」阶段。

---

## 一、现状诊断（文件级）

| 文件 | 当前规模 | 问题 |
|------|---------|------|
| `ViewModels/ImageEditorDocument.cs` | 953 行 | 上帝类：4 个位图槽 + 文件 IO + 裁剪 + 尺寸联动 + 像素化选项 + AI 生成，9 类职责混居 |
| `Views/ImageEditorDocumentView.axaml` | 420 行 | 4 个格子复制粘贴，~300 行重复；空态与 AI 槽与「编辑」职责无关 |
| `Views/ImageEditorDocumentView.axaml.cs` | 276 行 | 裁剪交互（指针/视口映射），本应属于格子自身的视图 |
| 双像素化管线 | `:309` vs `:349` | `PixelateImage` 走服务层（带裁剪），`PixelateAiImage` 绕过服务层直调 `PixelArtConversionService` |
| App 直接 `new` 插件 VM | `DocumentWorkspaceViewModel.cs:557/849/871` | 三处手工拼构造函数 |
| Bitmap 生命周期 | — | 文档关闭时 4 个 Bitmap 从不释放（替换时才 dispose），泄漏 |

可复用模式：`IModImagesDocumentFactory`（Core 接口 + 插件实现 + DI + App GetRequiredService）、`PixelArtConversionService` 零构造依赖（headless 可测）、`CropSelectionInteraction` 等纯逻辑 Helper 类。

---

## 二、目标架构（v3）

```
┌─ ImageCreateDocument（创建图像, 单例工具型）──────────────────────────┐
│   左: ① 导入 card（拖拽/点击多选）                                     │
│       ③ 待编辑列表（导入图 + AI 勾选备选 汇合, 多选 → [打开编辑]）      │
│   右: ② AiGenerationPanel（prompt/尺寸/数量/备选画廊, 勾选入列）        │
│   └─ 打开编辑: 素材落盘 → 发 OpenImageDocumentMessage(path)            │
└──────────────────────────────────────────────────────────────────────┘
                                │ 每素材一个编辑文档
                                ▼
┌─ ImageEditorDocument（编辑图像, 永远带素材打开, ~200 行）──────────────┐
│   SourcePane(素材+裁剪) → [应用] → ResultPane(产物)                    │
│   参数/尺寸 + 过期标记 → [保存...]（始终弹框, normal + x2_）            │
│   砍掉: 空态 / AI 槽 / 来源选择 / 更换素材                              │
└──────────────────────────────────────────────────────────────────────┘
```

### 2.1 创建图像 Document UI

```
┌──────────────────────────────────────────────────────────────────┐
│ 创建图像                                                           │
├────────────────────────────┬─────────────────────────────────────┤
│ ① 导入                      │ ② AI 生成工作台                     │
│ ┌────────────────────────┐ │ prompt: [一把像素风剑...........]   │
│ │  拖拽 / 点击选择多张    │ │ 尺寸 [512]×[512]   数量 [4]  [生成] │
│ └────────────────────────┘ │ ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐ │
│                            │ │ ☑备选1│ │ 备选2 │ │ ☑备选3│ │ 备选4│ │
│ ③ 待编辑列表               │ └──────┘ └──────┘ └──────┘ └──────┘ │
│ ☑ sword.png   (导入)       │ 勾选 = 加入列表（可取消）            │
│ ☑ ai_c1.png   (AI 备选)    │ 生成失败: 行内错误提示               │
│ ☐ shield.png  (导入)       │                                    │
│              [打开编辑]    │                                    │
└────────────────────────────┴─────────────────────────────────────┘
```

### 2.2 编辑图像 Document UI

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

### 2.3 关键设计决策（v3 已确认）

| # | 决策 | 结论 |
|---|------|------|
| D1 | 裁剪交互代码去向 | ✅ 随素材区槽走（`ImageSlotView.axaml.cs`，IsCropEnabled 开关） |
| D2 | AI 面板结果回传 | ✅ 事件（`AiPanel.Selected`），面板零感知工作台 |
| D4 | Bitmap 泄漏 | ✅ 本次修（slot VM `IDisposable`，Workspace 关闭钩子调） |
| D6 | 参数/裁剪变更后产物更新 | ✅ **手动应用**：变更 → 过期标记 → 点 [应用] 重算 |
| D7 | 保存方式 | ✅ **始终弹对话框**（无覆盖写回分支） |
| D8 | AI 生成形态 | ✅ **N 个备选（数量可配 1-8，持久化 IConfigService）→ 勾选入列** |
| D9 | 架构形态 | ✅ **创建/编辑双 Document**；创建流程两个都走，编辑流程只走编辑 |
| D10 | 素材汇总 | ✅ **统一待编辑列表**：导入图 + AI 勾选备选汇合，多选 → [打开编辑] |
| D11 | AI 备选进入编辑 | ✅ **先落盘到临时会话目录 → 按路径打开**（编辑文档永远面向文件路径，通道统一） |

---

## 三、重构步骤（Phase 0-5，每步独立可回退）

### Phase 1：文件服务 + 编辑文档工厂（零行为变化）

**1a. `IImageEditorDocumentFactory`（Core/Abstractions）+ `ImageEditorDocumentFactory`（插件）**
- 照抄 `IModImagesDocumentFactory` 模式；`CreateDocument()` / `CreateDocument(string path)`
- 替换 `DocumentWorkspaceViewModel.cs:557/849/871` 三处 `new ImageEditorDocument(...)`

**1b. `IImageFileService` + `ImageFileService`（插件 Services）**
- 选图 picker（支持多选）、`SaveAsync(Bitmap, suggestedName)`（始终弹框, normal + x2_）
- 命名约定（`GetSuggestedFileName` / `GetSuggestedX2FileName` / `NormalizeNormalOutputFileName`,原样搬移 `:674-703`）
- 位图编解码（临时文件 workaround 集中）
- **`StageAiCandidate(byte[] png, string name)`** → 落盘 `%TEMP%/NeoEditor/AiStaging/{guid}.png`，返回路径（D11）

**验收**：`dotnet test` 全量通过（原测试签名未变）。

### Phase 2：编辑文档瘦身（953 → ~200，XAML 420 → ~120，cs 276 → ~30）

**2a. `ImageSlotViewModel`（新）**：`Bitmap? Image`（dispose-on-replace）、`byte[]? SourceBytes`、`Title`、`HasImage`、`EmptyHint`、`SaveCommand`（注入 nameProvider）、`SetCropBounds`/`CropRect`（素材区启用）、**`IDisposable`**（D4）。

**2b. `ImageSlotView`（新 UserControl）**：标题 + Image + 空态提示 + 保存按钮；`IsCropEnabled` 时渲染裁剪 overlay + 交互 code-behind 迁入（原 276 行主体）。

**2c. 编辑文档重组**
- 构造：`CreateDocument(path)`（永远有素材，无空态）
- 布局：SourcePane（IsCropEnabled）+ ResultPane + 参数/尺寸 + [应用] + [保存...]
- **删除**：`AiImage`/`AiProcessedImage`/`HasAiImage`/`CanPixelateAiImage`/`AiGenerateCommand` 等 AI 槽全部（归创建文档）；空态/来源选择/更换素材；双 Pixelate 命令 → 单一 `ApplyCommand`
- **过期标记**：素材/裁剪/参数变更 → `HasStaleResult = true`（产物区 ⚠ 提示）；[应用] 重算并清除（替换原「变更即清空」语义）

**验收**：裁剪/拖拽/缩放冒烟一致；过期标记→应用→刷新链路验证；原 AI 相关测试迁出后全绿。

### Phase 3：创建图像 Document（新功能）

**3a. `AiGenerationPanelViewModel` + `AiGenerationPanelView`（新）**
- 输入：`AiPrompt` / `AiWidth` / `AiHeight`（512-2880, step 16）/ `CandidateCount`（1-8, 默认 4, IConfigService 持久化）
- 状态：`IsGenerating`（逐张进度）/ `Candidates: ObservableCollection<AiCandidate>` / `GenerationError`
- `GenerateCommand`：优先 API `n` 参数（gpt-image-1），不支持时并行调用 Count 次（面板无感）
- 勾选：`SelectedCandidates`（多选）→ 事件 `CandidatePicked(AiCandidate)`（D2）
- `AiCandidate`：`record(byte[] PngBytes, string Name)`

**3b. `ImageCreateDocument` + `ImageCreateDocumentView`（新）**
- 左：导入 card（拖拽/点击多选 → `ImageFileService` picker）→ 入列
- 右：`AiGenerationPanel`；勾选备选 → 入列（落盘 `StageAiCandidate` → 列表项持路径，D11）
- 底部/左下列表：`PendingImageItem(Path, DisplayName, Kind)`（可取消、多选）
- `[打开编辑]` 命令（CanExecute = 有勾选）→ 逐项发 `OpenImageDocumentMessage(path)`（现有 App 通道：查重 → 新开编辑文档）
- 临时文件清理：列表项移除时删；应用启动时懒清理 `AiStaging/` 残留
- 打开入口：现有 `OpenAiImageWorkbenchMessage` 语义升级为「打开创建图像文档」（EntityEditor 的 AI 生成命令自动受益）

**验收**：导入 3 张 → 列表就位；AI 生成 4 备选 → 勾 2 张入列 → [打开编辑] → 2 个编辑文档带图打开；`AiGenerationPanelViewModelTests` 覆盖数量配置/逐张落位/勾选回传/失败提示。

### Phase 4：统一管线（消除双入口）

- `ImageEditorProcessingRequest(ImageSource Source, int NormalWidth, int NormalHeight, PixelRect? CropRect)`，`ImageSource` = `record(byte[]? Bytes, string? FilePath)`，静态工厂 `FromPath`/`FromBytes`
- `ImageEditorProcessingService` 统一：解码 → crop → downscale → pixelart → x2 → Bitmap
- VM 删除对 `PixelArtConversionService` 的直接依赖；`ApplyCommand` 走唯一服务入口

**验收**：双源一致性测试（path 源与 bytes 源同选项产出一致）。

### Phase 5：测试补齐 + 全量回归

| 新测试 | 覆盖 |
|--------|------|
| `ImageSlotViewModelTests` | dispose-on-replace（替换+文档释放）、Title、SaveCommand、SourceBytes |
| `AiGenerationPanelViewModelTests` | 数量配置、逐张落位、勾选回传、失败提示、尺寸约束 |
| `ImageCreateDocumentTests` | 导入入列、AI 入列（落盘路径存在）、列表多选→打开编辑消息序列、取消清理 |
| `ImageEditorProcessingServiceTests` 扩充 | bytes 源管线、crop+像素化、双源一致性 |
| `ImageFileServiceTests` | 命名约定、StageAiCandidate 落盘+清理 |
| `CropSelectionInteractionTests`（新） | Move 边界 clamp、8 向 handle 最小尺寸（纯逻辑） |
| `ImageEditorDocumentTests` 重写 | 素材→应用→产物链路、过期标记→应用、保存 CanExecute |

**验收**：全量 `dotnet test` 通过；手动冒烟：导入→编辑→保存；AI 4 备选→勾选→编辑→保存；Dock 并行（生成中切编辑其他文档）。

---

## 四、文件清单（v3）

**新增（9）**

| 文件 | 内容 |
|------|------|
| `NeoEditor.Core/Abstractions/IImageEditorDocumentFactory.cs` | 工厂接口 |
| `NeoEditor.Plugins.ImageTools/Services/ImageEditorDocumentFactory.cs` | 工厂实现 |
| `NeoEditor.Plugins.ImageTools/Services/IImageFileService.cs` + `ImageFileService.cs` | 文件 IO / 命名 / 编解码 / StageAiCandidate |
| `NeoEditor.Plugins.ImageTools/ViewModels/ImageSlotViewModel.cs` + `Views/ImageSlotView.axaml(.cs)` | 素材/产物共用槽（含裁剪模式） |
| `NeoEditor.Plugins.ImageTools/ViewModels/AiGenerationPanelViewModel.cs` + `Views/AiGenerationPanelView.axaml(.cs)` | AI 备选画廊 |
| `NeoEditor.Plugins.ImageTools/ViewModels/ImageCreateDocument.cs` + `Views/ImageCreateDocumentView.axaml(.cs)` | 创建图像文档（导入 + AI + 列表） |
| `NeoEditor.Plugins.ImageTools/ViewModels/AiCandidate.cs` | `record(byte[] PngBytes, string Name)` |
| `NeoEditor.Plugins.ImageTools/ViewModels/PendingImageItem.cs` | 待编辑列表项（Path/DisplayName/Kind） |
| 对应新测试文件 | 见 §三 Phase 5 |

**修改（8）**

| 文件 | 改动 |
|------|------|
| `ImageEditorDocument.cs` | 953 → ~200 行：SourcePane/ResultPane + 尺寸/选项/过期标记/应用/保存 |
| `ImageEditorDocumentView.axaml` | 420 → ~120 行；`.cs` 276 → ~30 行 |
| `ImageEditorProcessingService.cs` + `IImageEditorProcessingService.cs` | `ImageSource` 泛化 |
| `ServiceCollectionExtensions.cs` | 注册工厂 / 文件服务 / 创建文档单例 |
| `DocumentWorkspaceViewModel.cs` | 3 处 `new` → 工厂；`OpenAiImageWorkbenchMessage` → 打开创建文档；关闭文档 dispose |
| `IConfigService`/配置 | `AiCandidateCount` 持久化字段 |
| 现有测试 | 适配签名 / AI 测试迁出 / 槽语义重写 |
| 消息语义 | `OpenAiImageWorkbenchMessage` 用途升级（名字可保留） |

**删除（语义层面）**：AI 槽位属性/命令、空态/来源选择/更换素材、双 Pixelate 命令、`InvalidateProcessedPreview` 清空语义（→ 过期标记）。

---

## 五、风险与开放点

| # | 风险/开放点 | 处理 |
|---|------------|------|
| R1 | AI 多备选 API 限制（dall-e-3 不支持 `n>1`） | 优先 `n` 参数，否则并行调用 Count 次；抽象在 `IImageGenerationService` 内，面板无感 |
| R2 | Phase 2 裁剪交互迁移（唯一行为敏感区） | `CropSelectionInteractionTests` + UI 冒烟双保险 |
| R3 | 过期标记替换自动清空是行为变化 | 已确认设计（D6），Phase 2 验收明确列出 |
| R4 | 临时落盘文件生命周期 | 列表项移除/文档关闭即删；启动时懒清理残留 |
| R5 | 原 4 槽测试大量重写 | Phase 5 单独安排 |
| O1 | 创建文档单例 vs 可多开 | 默认单例（工具型，重复打开激活已有）；实施时视体验定 |
| O2 | 画廊勾选即入列 vs 需点「加入列表」按钮 | 默认勾选即入列（可取消）；若误触率高实施时加确认 |

---

## 六、执行顺序与验证

```
Phase 1 文件服务+工厂  ──► dotnet test 全绿（行为零变化）
Phase 2 编辑文档瘦身   ──► dotnet test 全绿 + 裁剪/过期标记 UI 冒烟
Phase 3 创建文档       ──► dotnet test 全绿（AI 测试迁入 + 创建文档新测试）
Phase 4 统一管线       ──► dotnet test 全绿 + 双源一致性测试
Phase 5 测试补齐+回归   ──► 全量 dotnet test + 双文档全链路冒烟
```

每 Phase 独立提交、可单独回退；用户可随时在任意 Phase 后叫停评审。
