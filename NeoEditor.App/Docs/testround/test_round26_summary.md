# 架构测试第26轮 — PixelateAiImage 技术债清除 + headless 像素路径根因定位

> 日期：2026-08-02 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 12.1 / ProDataGrid 12.0.4)
> 上承：[test_round25_summary.md](test_round25_summary.md)（图像工作台重设计 + Image Browser 右键 + 崩溃修复）
> 清债对象：round25 剩余技术债「PixelateAiImage 无自动单测（headless 下 Avalonia Bitmap 编码不可靠，留真机）」

---

## 背景：round25 技术债

round25 §F 记录：`PixelateAiImage` 的 Avalonia↔ImageSharp 管线不在单测覆盖——headless 下
`Bitmap.Save`（编码）不可靠，留给真机。本轮回溯该根因并根治。

## A. 根因探测（5 个探针，全部证实同一结论）⭐

| 探针 | 验证目标 | 结果 |
|------|---------|------|
| `Probe_AvaloniaPngEncodeUnderHeadless` | `Bitmap.Save(ms)` → ImageSharp `Image.Load` | ❌ **编码产垃圾字节**，`Image.Load` 抛 `UnknownImageFormatException`（非 1×1，是无效 PNG） |
| `Probe_ToAvaloniaBitmapDirection` | ImageSharp `SaveAsPng` → `new Bitmap(ms)` | ❌ **解码返 1×1 占位**（不抛异常，静默尺寸错误） |
| `Probe_WriteableBitmapPixelCopyUnderHeadless` | `WriteableBitmap.Lock()` framebuffer 写/读回 | ❌ 写 0/1/2/3 读回 `48`（'0'）/'Ava' 字符串残留 → **内存也是假的** |
| `Probe_ReplicatePixelateAiImageSteps` | 逐段复现管线 | ❌ Step2 证实 `Bitmap.Save` 编码崩 |
| `Probe_FramebufferIsRealMemory` | 整块填充模式后重 Lock 校验 | ❌ 写 0/1/2/3 读回 48 → framebuffer 不持久 |

**结论**：headless 下 Avalonia 的**三条像素路径全部不可靠**——
1. PNG **编码**（`Bitmap.Save`）→ 垃圾字节，ImageSharp 抛异常；
2. PNG **解码**（`new Bitmap(stream)`）→ 1×1 占位（不抛）；
3. `WriteableBitmap.Lock()` framebuffer → 假内存。

因此 round25 的 `LoadGeneratedImage_SetsAiSlotOnly` 等测试**只验证了状态流**（`AiImage != null`、
槽位分离），从未对真实像素做断言——headless 下 Bitmap 解码返回 1×1 占位不抛异常，测试通过是「假绿」。

## B. 修复：像素管线改从 PNG 源字节走 ImageSharp ⭐

**设计原则**：不让像素处理依赖 Avalonia 的 Bitmap 编解码（headless 不可靠），而是
**直接从源 PNG 字节走 ImageSharp 管线**（纯托管代码，headless 可靠），Avalonia 只负责显示层。

| 变更 | 文件 | 说明 |
|------|------|------|
| 新增字段 `_aiSourceBytes` | `ImageEditorDocument.cs` | 保存 AI 图 PNG 源字节，注释说明 headless 可靠性动机 |
| `LoadGeneratedImage` 存字节 | 同上 | 收到 `pngBytes` 时同步保存 `_aiSourceBytes` |
| `PixelateAiImage` 改字节流 | 同上 | `Image.Load<Rgba32>(_aiSourceBytes)` → `ConvertToPixelArtAsync` → `ToAvaloniaBitmap`，删除 `ToImageSharp`（`bitmap.Save` 往返） |
| `ClearAiImage` 清字节 | 同上 | 防泄漏 |
| **`SavePng` 保留** | 同上 | 仍被 `SaveBitmapPairAsync`/`SaveX2VersionAsync` 用于**磁盘保存**（真实用户操作，不可删） |

**生产收益**：`PixelateAiImage` 原实现 `Bitmap.Save`（编码）→ ImageSharp（解码）是一次无谓往返；
改字节流后省掉一次 Avalonia 编码 + 一次 ImageSharp 解码，更快且行为不变。

## C. 新增测试（2 个，ImageTools 38→40）

| 测试 | 验证 |
|------|------|
| `PixelateAiImage_ProducesAiProcessedSlot` | **真实管线**：加载 → `PixelateAiImageCommand` 执行 → `HasAiProcessedImage`/`AiProcessedImage` 落位 → 不污染原图/处理图槽 |
| `PixelateAiImage_WithoutAiImage_IsNoOp` | 空槽 no-op，`HasAiProcessedImage`/`CanPixelateAiImage` 均 false |

探针测试（5 个 `Probe_*`）已全部删除——它们是定位手段，非交付物。csproj 的 `AllowUnsafeBlocks`
（仅探针需要）已移除。

## 编译和测试

| 项目 | 结果 |
|------|:----:|
| `dotnet build NeoEditor.sln`（全量 22 项目） | **0 错误** ✅ |
| Messaging.Tests | 3/3 ✅ |
| Core.Tests | 47/47 ✅ |
| UI.Common.Tests | 1/1 ✅ |
| Infra.Tests | 150/150 ✅ |
| DataViewer.Tests | 61/61 ✅ |
| EntityEditor.Tests | 28/28 ✅ |
| Mcp.Tests | 25/25 ✅ |
| Cli.Tests | 40/40 ✅ |
| AiChat.Tests | 32/32 ✅ |
| ImageTools.Tests | **40/40 ✅（+2：PixelateAiImage 管线 / no-op）** |
| Integration.Tests | 12/12 ✅ |
| **总计** | **439/439 ✅** |

## 真机冒烟（2026-08-02）

Rider run configuration `NeoEditor.App` 启动 GUI（waitForExit=false），20s 后确认：

| 检查项 | 结果 |
|--------|:----:|
| 进程存活 | ✅ `NeoEditor.exe` PID 41812 存活，354MB 内存正常 |
| 启动日志 411 行 | ✅ 无 `ERR`/`Exception`/`Unhandled`/崩溃 |
| 数据管线 | ✅ `PhpParser` 解析 7 mods / 2326 images，Help 10 项；profile_info/实体查询正常执行 |
| 唯一 WRN | EF Core 引用集合 ValueComparer 警告（既有，非本轮引入） |

round25 真机验收 9 项中，**启动无崩溃 / 数据加载**已由冒烟自动确认；其余纯交互项
（Orchestration 拖拽拉伸、Browser 右键、AI 生成、4 保存按钮、尺寸调整）需人工 GUI 逐项确认。

## 后续（未完成项）

| 项 | 说明 |
|---|------|
| round25 真机验收 9 项 | 纯交互项（拖拽、右键、AI 生成）仍需人工 GUI 确认；本轮回溯确认 headless 无法替代 |
| `ImageEditorProcessingService.CreateBitmap` 同步路径 | 与 `PixelateAiImage` 同走 PNG 编解码，headless 下同样 1×1；因仅磁盘保存/显示用且已由转换逻辑测试覆盖，本轮未改（留待需要 headless 断言时） |
