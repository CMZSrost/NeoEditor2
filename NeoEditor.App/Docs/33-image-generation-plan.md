# 33 — 像素风格图像生成计划

> v2.1 · 2026-07-31 · **✅ G1-G3 全部完成 + G1/G2 集成收尾**
> 上承: [30-post-m12-development-plan.md](30-post-m12-development-plan.md) §九
> 目标: AI 根据实体 XML 生成符合游戏数据设计的像素风格图片
> 实现: 见 [CLAUDE.md](../../../CLAUDE.md) · 12 新文件 · +16 ImageTools +1 MCP = +17 测试
>
> **2026-07-31 收尾更新**：
> - `ImageGenerationService.GenerateForEntityAsync` 现在在 AI 生成后自动调用 `PixelArtConversionService` 做像素化后处理（G1↔G2 集成）
> - `OPENAI_IMAGE_MODEL` 环境变量支持可配置图片生成模型（默认 `dall-e-3`）
> - 修复 `BuildPrompt` 中的 sync-over-async 潜在死锁

---

## 〇、动机

NeoScavenger 是 2D 像素风游戏，modder 常需要为新物品/生物制作对应图片。当前流程是：
1. 在编辑器创建实体（XML 数据）
2. 退出编辑器
3. 手动用像素绘图软件画图
4. 回到编辑器导入图片

**目标流程**：在编辑器内选中实体 → AI 读取 XML → 生成像素风图片 → modder 微调或直接用。

---

## 一、功能设计

### 1.1 两种生成路径

```
路径 A: AI 文生图（质量优先）
  实体 XML → 结构化 prompt → DALL·E / Stable Diffusion → 写实图
      → 像素化后处理（像素算法） → 像素图

路径 B: 本地像素化（离线可用）
  实体 XML → 模板匹配 → 像素图合成（组合预置元素）
      → 适合简单物品（武器/物品图标）
```

### 1.2 输入

| 输入 | 来源 | 格式 |
|------|------|------|
| 实体 XML | 当前选中的实体（EntityEditor 或 DataGrid） | IEntity 对象 → 序列化为结构化描述 |
| 实体类型 | ItemType / Creature / 等 | 决定生成策略（物品图标 vs 生物精灵） |
| 参考图片（可选） | ImageTools 中已存在的同类图片 | 作为 img2img 的参考 |
| 尺寸约束 | 游戏图片标准 | NeoScavenger 物品图标: 64×64 (x2: 128×128) |

### 1.3 输出

| 输出 | 格式 |
|------|------|
| 生成图片 | PNG，符合游戏尺寸 |
| 可选 x2 版本 | PNG，双倍分辨率 |
| 自动命名 | 基于实体 ID（如 `item_sword.png`） |
| 自动注册 | 可选添加到 Mod 的 getimages.php |

---

## 二、实体 → Prompt 转换

### 2.1 XML 摘要提取

从 IEntity 生成结构化 prompt 描述：

```
Entity: ItemType / item_weapon_sword
Category: Weapon
Properties:
  - Name: Iron Sword
  - Weight: 3.5 kg
  - Value: 100 gold
  - Damage: 15 (slash)
  - Durability: 80
Visual hints from references:
  - Material: metal (from weaponRef→attack_slash)
  - Style: medieval
```

### 2.2 Prompt 模板

```
"A pixel art {category} sprite for a game. {description}.
 Single object on transparent background, {width}x{height} pixels,
 limited color palette (16-32 colors), pixel-perfect edges, no anti-aliasing."
```

示例：
```
"A pixel art weapon sprite for a game. An iron sword with a simple crossguard,
 silver blade, dark leather-wrapped handle. Single object on transparent background,
 64x64 pixels, limited color palette (24 colors), pixel-perfect edges, no anti-aliasing."
```

### 2.3 按实体类型的 Prompt 策略

| 实体类型 | Prompt 侧重 | 尺寸 |
|---------|------------|:--:|
| ItemType (武器/防具) | 物品外观、材质、颜色 | 64×64 |
| ItemType (消耗品) | 容器形状、内容物暗示 | 32×32 |
| Creature | 生物形态、姿势、大小 | 128×128 |
| 其他 | 通用图标 | 64×64 |

---

## 三、像素后处理

### 3.1 写实图 → 像素图算法

当 AI 生成写实图时，用 ImageSharp 做像素化后处理：

```
输入图片 (512×512)
  → 缩放到目标尺寸 (64×64) — NearestNeighbor 保持硬边缘
  → 颜色量化 (MedianCut / K-Means) — 减少到 16-32 色
  → 可选：边缘检测 + 轮廓加重
  → 可选：透明度处理（背景色 → Alpha）
  → 输出 PNG
```

### 3.2 像素化参数

| 参数 | 默认值 | 说明 |
|------|:--:|------|
| TargetWidth/Height | 64/64 | 目标尺寸 |
| ColorCount | 24 | 量化后颜色数 |
| Dithering | None | 像素风不应有抖动 |
| EdgeEnhancement | true | 加重轮廓线 |
| TransparentBackground | true | 背景色→透明 |

### 3.3 实现位置

ImageTools Plugin 新增：
- `Services/PixelArtConversionService.cs` — 像素化算法
- `Services/ImageGenerationService.cs` — 生成编排（调 AI + 后处理）

---

## 四、架构设计

### 4.1 组件关系

```
AiChat Plugin                    ImageTools Plugin
┌──────────────────┐            ┌──────────────────────────────┐
│ ChatService      │            │ ImageGenerationService        │
│                  │            │  ├── PromptBuilder            │
│  tool: generate  │──R17──→   │  ├── PixelArtConversionService│
│  _image          │  MCP Tool  │  └── ImageSaveService         │
│                  │            │                               │
└──────────────────┘            └──────────────────────────────┘
         │                                │
         ▼                                ▼
  OpenAI Image API              ImageSharp 本地处理
  (DALL·E / Stable Diff.)       (像素化 / 缩放 / 量化)
```

### 4.2 MCP 工具定义

在 ImageTools Plugin 中新增 MCP Tool Provider：

```csharp
// ImageTools/Services/ImageMcpToolProvider.cs
[McpServerTool("generate_image",
    "Generate a pixel art image for a game entity based on its XML data.")]
public async Task<string> GenerateImage(
    string entityType,    // ItemType, Creature, etc.
    string entityId,      // entity identifier
    int? width,           // optional: override default size
    int? height,
    string? style         // optional: "pixel" | "realistic" | "sketch"
);
```

> 或者作为 AiChat Plugin 自身的 MCP 工具（简单路径，不跨 Plugin）。

**推荐方案**：generate_image 放在 AiChat Plugin 内（避免 ImageTools 依赖 AI SDK），通过 IHostService 读取实体数据，生成后通过 ImageTools Plugin 的 IImageSaveService 保存。

### 4.3 服务注册（R17 合规）

```csharp
// Core/Abstractions/IImageGenerationService.cs
public interface IImageGenerationService
{
    Task<ImageGenerationResult> GenerateAsync(
        IEntity entity, ImageGenerationOptions options, CancellationToken ct = default);
}

// AiChat Plugin 调用 IImageGenerationService（Core 接口，R17）
// ImageTools Plugin 实现 IImageGenerationService
```

---

## 五、分阶段计划

### Phase G1: 像素化后处理 ✅ 已完成 (2026-07-30)

| 步骤 | 内容 | 涉及文件 |
|:--:|------|---------|
| G1.1 | `PixelArtConversionService` — K-Means 颜色量化 + Sobel 边缘增强 + Floyd-Steinberg 抖动 | ImageTools/Services/PixelArtConversionService.cs + PixelArtConversionOptions.cs |
| G1.2 | ImageEditorView 已有 "Pixelate" 按钮，新增参数控件（Slider + CheckBoxes） | ImageTools/Views/ImageEditorDocumentView.axaml |
| G1.3 | 像素化参数属性（ColorCount / EdgeEnhancement / DitheringEnabled / TransparentBackground） | ViewModels/ImageEditorDocument.cs |
| G1.4 | 单元测试 7 个 | ImageTools.Tests |
| **交付** | 16/16 ImageTools 测试通过 | |

### Phase G2: AI 图片生成接入 ✅ 已完成 (2026-07-30)

| 步骤 | 内容 | 涉及文件 |
|:--:|------|---------|
| G2.1 | `IImageGenerationService` 接口 + `ImageGenerationOptions` + `ImageGenerationResult` record | Core/Abstractions/IImageGenerationService.cs |
| G2.2 | `ImageGenerationService` — HttpClient 调 OpenAI Images API | ImageTools/Services/ImageGenerationService.cs |
| G2.3 | `EntityToPromptConverter` — 实体属性反射 → 结构化 prompt | ImageTools/Services/EntityToPromptConverter.cs |
| G2.4 | `generate_image` MCP 工具（EditorTools 第 12 个工具） | Mcp/Tools/EditorTools.cs |
| G2.5 | 单元测试 5 个 prompt + MCP 工具计数 11→12 | ImageTools.Tests + Mcp.Tests |
| **交付** | 22/22 MCP 测试 · 16/16 ImageTools 测试通过 | |

### Phase G3: 编辑器集成 ✅ 已完成 (2026-07-30)

> ⚠️ **2026-08-02 移除**：G3 实体右键「Generate Image」已整体移除——`EntityImageGenActionProvider` + `IEntityContextActionProvider` 扩展点（EntityEditorDocument/Fatory/Plugin/View 接线、DI）+ 死消息 `ImageGeneratedMessage` 全删，EntityEditor 右键菜单清空（N04 无死代码）。AI 生成迁往**图像编辑工作台**：`IImageGenerationService` 新增 `GenerateAsync(prompt)`（`GenerateCoreAsync` 共享管线），工作台 AI 面板 prompt→生成→保存/编辑，Image Browser 右键「AI 生成图片」入口。详见 [test_round25](testround/test_round25_summary.md)。

| 步骤 | 内容 | 涉及文件 |
|:--:|------|---------|
| G3.1 | EntityEditor 右键菜单 "Generate Image" | EntityEditor/Views/EntityEditorView.axaml.cs + EntityEditorDocument.cs |
| G3.2 | `IEntityContextActionProvider` 扩展点接口（Core）+ `EntityImageGenActionProvider` 实现 | Core/Abstractions/ + ImageTools/Services/ |
| G3.3 | `ImageGeneratedMessage` — 生成后通知 App Shell 打开 ImageEditor | Core/Messages/ModMessages.cs |
| G3.4 | 自动命名 + 保存到 Mod img/ 目录 + x2 版本生成 | EntityImageGenActionProvider |
| G3.5 | DI 注册：`IEnumerable<IEntityContextActionProvider>` 自动发现 | ImageTools + EntityEditor ServiceCollectionExtensions |
| **交付** | 26/26 EntityEditor 测试 · 10/10 Integration 测试通过 | |

---

## 六、技术选型

### 6.1 图片生成 API

| 方案 | 优点 | 缺点 |
|------|------|------|
| **OpenAI DALL·E 3** | 质量最高，自然语言理解好 | 费用高，不支持像素风 fine-tune |
| **Stable Diffusion (API)** | 可自部署，开源模型可选 | 需要 GPU 服务器或付费 API |
| **本地 SD + ComfyUI** | 完全离线，像素风 LoRA 可控 | 部署复杂，用户需 GPU |
| **Ollama + 视觉模型** | 统一 API，本地运行 | 图片生成能力弱 |

> **推荐方案**：优先复用 OpenAI 兼容 API（用户在 `OPENAI_ENDPOINT` 里配置什么用什么），+
> Phase G1 像素化后处理作为质量兜底。

### 6.2 像素风格 LoRA（可选）

如果用户常用，可以指向社区像素风 LoRA：
- `pixel-art-xl-v1` (CivitAI) — SDXL 像素风
- `PixelSprites` (CivitAI) — 精灵图专用

> 仅文档指引，不内置模型文件。

---

## 七、时间估算

| Phase | 工作量 | 说明 |
|:-----:|:------:|------|
| G1 像素后处理 | 2-3h | 纯 ImageSharp 本地算法 |
| G2 AI 接入 | 2-3h | 接口 + prompt + API 调用 |
| G3 编辑器集成 | 2-3h | 右键菜单 + 自动保存 |
| **合计** | **6-9h** | 约 1 个工作日 |
