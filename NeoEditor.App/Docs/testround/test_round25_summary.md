# 架构测试第25轮 — 图像工作台重设计 + Image Browser 右键 + 崩溃修复

> 日期：2026-08-02 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 12.1 / ProDataGrid 12.0.4)
> 上承：[test_round24_summary.md](test_round24_summary.md)（Profile Tool 崩溃修复 + Round23 八项）
> 订正对象：[test_round22_summary.md](test_round22_summary.md)（§6 Orchestration 行模板补普通列规则）

---

## 本轮内容

### A. Image Orchestration 修复（列头拉伸 + 普通列 DataContext 陷阱）⭐

- **A1 列头可拉伸**：`DataGrid` 显式 `CanUserResizeColumns="False"` → `"True"`，Name / x2 / Status 三列可拖拽。
- **A2 普通列 DataContext 陷阱（Status 列「✓✗✓✗」根因）**：
  - 层级模式每行 DataContext 是 `HierarchicalNode`（`HierarchicalModel.Flattened` 暴露 node，`node.Item` 才是真 item）；
  - **只有 `DataGridHierarchicalColumn` 解包**——列上 `Binding="{Binding Item}"`（`BindContent` 设 `presenter.Content = dataItem` + ContentTemplate），其模板 DataContext = item；
  - **普通 `DataGridTemplateColumn`（x2、Status）不走解包**：`CellTemplate.Build(dataItem)` 的 dataItem 就是 node → 模板里所有 item 属性绑定失败，`IsVisible`（bool 失败回退默认 `true`）→ Status 列 4 个符号全亮（每行 ✓✗✓✗），x2 列空白；
  - **修复**：普通列模板绑定加 `Item.` 前缀（`{Binding Item.X2Text}`、`{Binding Item.NormalExists}`…）。即：**层级列直接绑 item 属性，普通列必须 `Item.xxx`**。

### B. 合并视图崩溃修复（TabSnapshotCache 自我修改集合）

- **现象**：profile A 加载过 → detached 切到 B（`_loadPending=true` 但 reload 跳过）→ 切回 A 再 attach → 缓存命中路径闪退：
  `System.InvalidOperationException: Collection was modified; enumeration operation may not execute.` at `ModGameDataTabsView.Tab.cs:370`（`OnAttachedToVisualTree`）。
- **根因**：`Tabs => _vm.Tabs`（ViewModel 活引用），而 `TabSnapshotCache[key] = (Tabs, MergeStore, EditStore)` 存的是**同一活引用**不是快照。缓存命中路径 `foreach (var tab in cached.Tabs) Tabs.Add(tab)` 枚举的集合与添加的集合**同一个** → 自我修改必崩。
- **修复**：删除该冗余 foreach（`cached.Tabs` 恒等于 `Tabs`，tabs 本就在集合里；若为空循环本来就什么都不做）。缓存的价值（跳过 DB reload、恢复 MergeStore/EditStore）不受影响。

### C. Image Browser 右键入口

- **C1「新增图片」**（`AddImageCommand`）：右键 Mod 目录节点 → 文件选择器（多选）→ 拷贝进 `<ContentRoot>/img/` → 刷新树 → **每个新增图片打开一个工作台编辑页**。
  - `ModImageTreeNode` 加 `IsGame`（Base Game 只读，`CanExecute` 禁止写游戏安装目录）；
  - 右键命中检测：code-behind `ContextRequested` → `TryGetPosition` + `InputHitTest` → 找 `TreeViewItem` → `SelectedNode`。
- **C2「AI 生成图片」**（`GenerateImageCommand`）：`CanExecute = IImageGenerationService.IsAvailable`（**未配置禁用**）→ 发 `OpenAiImageWorkbenchMessage` → App shell 打开空白工作台。

### D. 图像编辑工作台重设计（4 槽模型 + 每图保存 + AI 生成 + 尺寸堆叠）⭐

- **D1 4 槽独立模型**：`SelectedImage`（原图，可空）/ `ProcessedImage`（原图像素处理图）/ `AiImage`（AI 生成图，新）/ `AiProcessedImage`（AI 图像素处理图，新）。
  - **2×2 网格**布局：原图（含裁剪 overlay）| 原图像素 ／ AI生成图 | AI图像素；
  - `LoadGeneratedImage` **只设 AiImage**，不再污染原图/处理图槽（原实现把 AI 图同时塞进 SelectedImage+ProcessedImage）；
  - `PixelateAiImage` 命令：`ToImageSharp`（Avalonia Bitmap→PNG→ImageSharp）→ `PixelArtConversionService.ConvertToPixelArtAsync` → `ToAvaloniaBitmap`。
- **D2 每图独立保存按钮**（请求 1）：`SaveOriginalImage` / `SaveProcessedImage` / `SaveAiImage` / `SaveAiProcessedImage` 各管各的图格，归属清晰。通用 `SaveBitmapPairAsync` 写 PNG + 2× 版本（`x2_` 前缀 NearestNeighbor 放大），替换旧 pair 重新处理保存（`TryCreateOutputPairPaths` 删除）。
- **D3 AI 生成面板**（请求 2 的入口）：工作台顶部 prompt 输入 + 「AI 生成图片」按钮；`AiGenerateCommand` 调 `IImageGenerationService.GenerateAsync(prompt)`（新 API，`GenerateCoreAsync` 共享管线）；`IsAiUnavailable` 显示未配置提示。
- **D4 尺寸调整**（请求 3）：宽/高从底部挤在一起的窄输入 → 右侧独立区块**上下堆叠**，输入框加宽到 **130px**，带锁定宽高比。
- **D5 本地化**：新增 `OriginalImage`/`PixelatedImage`/`AiGeneratedImage`/`AiPixelatedImage`/`SaveImage`/`PixelateAi`/`AiGenerate`/`AiPromptPlaceholder`/`AiUnavailableHint` 键（中英）。

### E. 移除实体右键 Generate Image

- 删除 `EntityImageGenActionProvider` + `IEntityContextActionProvider` 扩展点（EntityEditorDocument/Fatory/Plugin/View 接线、ImageTools DI 注册、`EntityEditorDocumentFactoryTests` stub 分支）+ 死消息 `ImageGeneratedMessage`（只发不收，N04）。
- EntityEditor 右键菜单 `BuildContextMenu` 删除（`Root.ContextMenu` 不再设置）。
- AI 生成整体迁往工作台（§D3），实体版不再存在。

### F. 测试基建：Avalonia.Headless

- ImageTools.Tests 新增 `Avalonia.Headless` + `Avalonia.Skia` 包 + `TestApp.cs`（`TestApp.EnsureAvaloniaInitialized` 手动初始化 headless 平台，`UseSkia().UseHeadless().SetupWithoutStarting()`）。
- **不用 `Avalonia.Headless.XUnit`**：12.x 依赖 xunit v3，与项目 xunit 2.9 冲突（`FactAttribute` CS0433 二义）。
- 用途：`ImageEditorDocumentTests` 里解码 Avalonia `Bitmap`（`LoadGeneratedImage`）的测试可跑。注意 headless 下 `Bitmap.Save`（编码）不可靠，`PixelateAiImage` 的 Avalonia↔ImageSharp 管线不在单测覆盖（已有 `EntityToPromptConverterTests`/`ImageEditorProcessingServiceTests` 覆盖转换逻辑），留给真机。

---

## 订正说明（对 round22 文档）

| round22 章节 | 原描述 | 订正为 |
|------|--------|--------|
| §6 Orchestration 行模板 | 「CellTemplate DataContext = item，直接绑属性」 | 该规则**仅限 `DataGridHierarchicalColumn`**；**普通 `DataGridTemplateColumn` 的单元格 DataContext 是 `HierarchicalNode`**，必须 `Item.` 前缀绑定（§A2） |

---

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
| ImageTools.Tests | **38/38 ✅（+3：ImageEditorDocument 工作台；+2 于上轮 AddImage/GenerateImage CanExecute）** |
| Integration.Tests | 12/12 ✅ |
| **总计** | **437/437 ✅** |

新增测试（`Tests/NeoEditor.Plugins.ImageTools.Tests/ViewModels/ImageEditorDocumentTests.cs`）：
- `AiGenerateCommand_CanExecute_FollowsAvailabilityAndPrompt` — 可用性 + prompt 双条件；
- `LoadGeneratedImage_SetsAiSlotOnly_NotOriginalOrProcessed` — **4 槽分离**（headless 下验证 AI 图不污染原图/处理图）；
- `SaveCommands_CanExecute_FollowTheirSlotState` — 每图保存按槽位启停。

---

## 真机验证（待人工回填）

- [ ] **Image Orchestration**：三列可拖拽拉伸；Status 列 pair 行最多 2 个符号（normal + x2 各自 ✓/✗），source 行空白（§A）
- [ ] **合并视图**：profile A 加载 → 切走 → 切回 → 合并视图正常加载，**不再闪退**（§B）
- [ ] **Image Browser 右键「新增图片」**：选图 → 文件进 `Mods/X/img/` → 树刷新 → 编辑页打开；右键 Base Game 项菜单置灰（§C1）
- [ ] **Image Browser 右键「AI 生成图片」**：未配 API Key 时禁用；配好后点开工作台（§C2）
- [ ] **工作台 2×2**：4 个图格各自独立；AI 生成图不覆盖原图/处理图（§D1）
- [ ] **工作台 AI 生成**：prompt → 生成 → AI 槽出现图；AI 像素化 → AI图像素槽；未配置时顶部显示提示（§D3）
- [ ] **工作台保存**：4 个保存按钮各自保存对应图 + x2_ 版本（§D2）
- [ ] **尺寸调整**：宽/高上下堆叠、输入框加宽可用（§D4）
- [ ] **EntityEditor**：右键实体不再有「Generate Image」菜单（§E）

## 剩余项（技术债）

| 项 | 说明 |
|---|------|
| `PixelateAiImage` 自动单测 | Avalonia Bitmap 编码在 headless 不可靠，暂靠转换逻辑测试 + 真机覆盖（§F） |
| 真机验收 | 上表 9 项需人工逐项确认 |
| CLAUDE.md 历史轮次计数 | round21/22 摘要里的旧计数（419/430）未回填为本轮最终态，仅 round25 为准 |
