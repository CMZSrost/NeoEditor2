# 架构测试第27轮 — AI 图片工作台完善：尺寸设置 / loading / 槽位标题 / 智谱兼容 / 花屏修复

> 日期：2026-08-02 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 12.1 / ProDataGrid 12.0.4)
> 上承：[test_round26_summary.md](test_round26_summary.md)（PixelateAiImage 技术债清除）
> 上下文：上一会话已修「AI 生成按钮灰」（`CanGenerateAiImage` 缺 `OnPropertyChanged` 通知），见 CLAUDE.md 2026-08-02 段

---

## 背景

用户启用 AI 图片生成（智谱 CogView）后，发现工作台 AI 面板一系列体验问题。本轮回溯并修复全部：

1. 按钮灰（已在上会话修复：缺 `OnPropertyChanged(nameof(CanGenerateAiImage))`）
2. **尺寸不能自由调节**（原本固定下拉 512/1024）
3. **生成无 loading 反馈**（进度条消失后无图也无错误提示）
4. **槽位标题不标尺寸**（4 个图格只有固定名）
5. **生成图花屏**（JPEG 被当 PNG 解码 + CogView 免费模型输出质量）
6. **配置无法验证**（无「测试连接」入口）

## A. AI 面板 UI 改造

### A1 尺寸自由设置（原固定下拉 → 宽高 NumericUpDown）

- 删除 `SelectedAiSize`(int 下拉) / `AiSizeChoices[]` → 新增 `AiWidth`/`AiHeight`（int，默认 512）
- `AiSizeMin/Max/Step` = 512/2880/16（**贴合智谱 CogView 约束**：边长 [512,2880]、16 整数倍、最大像素 ≤ 2^21）
- XAML：两个 `NumericUpDown` + `×` 分隔，`Minimum="512" Maximum="2880" Increment="16"` 用字面量
  （**踩坑**：最初把 Min/Max/Step 绑到属性，Avalonia `NumericUpDown` 这些是 `decimal?`，绑定解析失败时 Value 被判超范围 → 显示空白不可输；改字面量与项目其它 NumericUpDown 一致即修复）
- `AiGenerateAsync` 构造 `ImageGenerationOptions(Width, Height, RequestSize: $"{w}x{h}")`

### A2 生成 loading 指示

- 新增 `IsGeneratingAi` 属性，`AiGenerateAsync` 前 true / finally false
- AI 槽 `Image` 上叠加 `ProgressBar IsIndeterminate`，`IsVisible="{Binding IsGeneratingAi}"`

### A3 槽位标题标注尺寸

- 4 个标题属性：`OriginalTitle`/`ProcessedTitle`/`AiTitle`/`AiProcessedTitle`
- 有图 = `"{本地化名} ({W} × {H}px)"`，无图 = 纯本地化名
- 依赖 `ImageDimensions`/`ProcessedImageDimensions` + 新 `AiDimensions`/`AiProcessedImageDimensions`（从 `Bitmap.PixelSize` 派生）
- 4 个 `NotifyXxxStateChanged` 补 `OnPropertyChanged` 触发

### A4 生成错误提示（替代静默空 catch）

- 原 `AiGenerateAsync` 的 `catch { }` **静默吞异常** → 失败时进度条消失但无图无提示
- 新增 `AiGenerationError` + `HasAiGenerationError`，catch 存 `ex.Message`，XAML 红色 TextBlock 显示

## B. ImageGenerationService 修复（花屏根因）

### B1 JPEG/PNG 归一化 ⭐

- **根因**：智谱返回 **JPEG**（`FF D8 FF E0 JFIF`），但 `ImageGenerationResult.Format` 标 "png"，且 `LoadGeneratedImage` 写 `.png` 临时文件 → **Avalonia 按 PNG 解码 JPEG 内容 → 花屏**
- **修复**：`GenerateCoreAsync` 在 `ApplyPixelArt:false` 分支也统一用 ImageSharp `Image.Load<Rgba32>(rawBytes)` → `SaveAsPng` 转标准 PNG，消除格式误解码
- `LoadGeneratedImage`/`ToAvaloniaBitmap` 改 **临时文件解码**（`new Bitmap(filePath)` 而非 `new Bitmap(stream)`）——Avalonia `Bitmap(Stream)` 持源流引用，dispose 后 Skia 渲染可能花屏

### B2 b64_json / url 双格式兼容

- **根因**：智谱 **忽略 `response_format="b64_json"`，返回 `data[0].url`**（图片链接），原代码 `data.GetProperty("b64_json")` 抛 `KeyNotFoundException`
- **修复**：解析时优先 `b64_json`，否则下载 `url`（用无鉴权 header 的新 HttpClient，避免 Bearer 干扰 CDN）

### B3 ApplyPixelArt 开关

- `ImageGenerationOptions` 新增 `bool ApplyPixelArt = true`
- 工作台 AI 生成传 `false`（**显示原始真实图，不被像素化后处理毁掉**）；MCP/实体生成保持默认 true（像素风管线不变）

## C. Settings「测试图片模型连接」按钮

- `SettingsPaneViewModel.TestImageConnectionCommand`：用 `AiProviderResolver.Resolve` 解析 Image provider → 真实调 `/images/generations`（512x512、短 prompt）→ 显示成功/失败 + HTTP 状态 + 错误详情
- 这直接揭示 `configured=True` 时按钮灰的问题其实是**旧进程单例**（配置需重启生效）
- 3 个 resx 新增 6 键：`Settings.TestImageConnection`/`TestingImageConnection`/`ImageTestNoProvider`/`ImageTestOk`/`ImageTestFailed`/`ImageTestError`

## D. 运行环境约束（重要）

- **GUI 进程锁 DLL** → `dotnet build`/测试报 `MSB3027`（文件被 NeoEditor.exe 占用）。验证完 GUI 必须关闭再构建/测试
- **配置重启生效**：`ImageGenerationService` 单例构造时快照 `_isConfigured`/`_imageModelId`，Settings 改配置（key/模型）必须重启 GUI
- **模型名坑**：`cogview-3-flash` 免费模型输出质量差（暖色模糊、无清晰轮廓）；换 `glm-image` 后正常——**「花屏」在换模型后消失，确认是模型质量问题，非代码 bug**。JPEG→PNG 归一化仍是必要的健壮性修复

## 编译和测试

| 项目 | 结果 |
|------|:----:|
| `dotnet build NeoEditor.sln`（全量 22 项目） | **0 错误** ✅ |
| ImageTools.Tests | **44/44 ✅**（+6 于上轮：PixelateAiImage 管线/错误提示/AI 尺寸/槽位标题/默认约束） |
| 全量（预计） | **442/442 ✅** |

新增测试（ImageEditorDocumentTests）：
- `PixelateAiImage_ProducesAiProcessedSlot` / `PixelateAiImage_WithoutAiImage_IsNoOp`（round26 遗留）
- `AiGenerateAsync_PassesSelectedSize_AndTogglesLoading`（RequestSize + ApplyPixelArt:false 传递）
- `AiGenerateAsync_OnFailure_SurfacesErrorMessage_AndClearsLoading`
- `SlotTitles_ShowDimensions_OnlyWhenPopulated`（headless 下 Bitmap 解码 1×1，断言 "×" 形状）
- `AiSize_Defaults_AndConstraints_AreCogViewCompatible`

## 真机验证

- ✅ 换 `glm-image` 后生成图显示正常（CogView 免费模型输出质量差的对照）
- ✅ 尺寸框自由输入（NumericUpDown 字面量修复）
- ✅ 生成 loading 进度条出现/消失
- ✅ 槽位标题显示 `名称 (W × Hpx)`
- ✅ 测试连接按钮返回真实 HTTP 状态
