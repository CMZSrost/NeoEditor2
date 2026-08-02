# 架构测试第22轮 — 七项改造（AI Chat 可视化 / HostService 搜索 / Profile Tool 树形网格 / 图片布局 / MD 主题 / Orchestration 树形网格）

> 日期：2026-08-02 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 12.1 / ProDataGrid 12.0.4)
> 上承：[test_round21_summary.md](test_round21_summary.md) (UI 六项改造)
> 计划：`C:\Users\Cromzst\.claude\plans\sunny-dazzling-garden.md`

> ⚠️ **本文件描述的实现已被 round24 订正**（2026-08-02）：
> - §3 Profile Tool 懒加载 `ChildrenSelectorAsync` → **同步 `ChildrenSelector` + 后台预取 stats**（原实现引发展开线程亲和崩溃）；
> - §6 Orchestration `ContentControl` 按类型选 DataTemplate → **统一属性 + 单模板直接绑 item**（`DataGridHierarchicalColumn` 的 CellTemplate DataContext 是 item，原类型分派运行时失效回退类名）；
> - §2 `SearchEntitiesAsync` 签名 → **加 `entityType`/`modId` 过滤 + 搜索所有 string 属性**。
> 详见 [test_round24_summary.md](test_round24_summary.md)。

## 本轮目标（7 项）

1. **AI Chat 可视化**：assistant 回复气泡化 + 工具调用独立成块，不再混进正文
2. **HostService 提供 search 方法**，MCP `SearchAllTypes` 重构走它（工具数保持 12）
3. **Profile Tool**：修「缺游戏本体」+ 修「XML 无法展开数据类叶子」+ 换 **ProDataGrid 层级网格** + 右键打开目录/文件 + 双击 XML 打开只读页
4. **Image Browser** 预览改为上下分栏（预览在下）
5. **帮助文档 MD 渲染**换 VSCode Dark+ 风格主题
6. **Image Orchestration** 改 ProDataGrid 层级网格（source→pairs）

> 已确认决策：树形网格用 **ProDataGrid 自带 Hierarchical**（`DataGridHierarchicalColumn` + `HierarchicalModel<T>`，12.0.4 已带；官方 TreeDataGrid 包本机只有 11.3.0 不兼容 Avalonia 12）；MCP 搜索**重构现有 SearchAllTypes**；AI Chat 历史**弃用 DataGrid ChatMimic 改 ItemsControl**。

---

## 完成的工作

### 1. AI Chat 气泡 + 工具调用块
- **`ChatMessageItem.cs`**：新增 `ChatMessageKind` 枚举（User/Assistant/Tool/System）+ `Kind` 属性，`(role, content)` 构造按 role 映射；`IsUser/IsAssistant/IsTool/IsSystem` 由 Kind 派生。`IsThinking` 保留。
- **`AiChatViewModel.cs`**：流式循环重写——`[tool:` 标记（`\n[tool: executing X]\n`，注意原 `StartsWith("[tool:")` 因前导 `\n` 从未命中）用 `[GeneratedRegex]` 解析工具名，**收尾当前 assistant 气泡 → 新增独立 `Kind=Tool` 消息 → 后续正文进新 assistant 气泡**；空 assistant 占位被移除。
- **`AiChatView.axaml`**：`DataGrid` → `ScrollViewer + ItemsControl`，单个 `DataTemplate` 内 4 个 `Border` 按 Kind 显隐：User 右蓝气泡 / Assistant 左气泡（**加可见边框**，解决白底白气泡看不见）+ "AI" 标签 + IsThinking「…」/ Tool **VSCode 深色条**（⚙ + 工具名等宽字体）/ System 居中灰条。
- **`AiChatView.axaml.cs`**：滚底改 `MessageScroll.ScrollToEnd()`。
- 删 `Converters/BubbleConverters.cs`；`AiChat.csproj` 移除 ProDataGrid 引用（ChatMimic 不再用）；`Watermark`→`PlaceholderText`（obsolete 警告）。
- 测试 +3：`ChatMessageItem_MapsRoleToKind`、`SendMessage_InterleavesToolCall_AsSeparateToolBlock`、`SendMessage_ToolOnlyTurn_DropsEmptyAssistantPlaceholder`。

### 2. HostService.SearchEntitiesAsync + MCP 重构
- **`IHostService.cs`**：新增 `Task<IReadOnlyList<IEntity>> SearchEntitiesAsync(string query, int limit = 50)`。
  > 🔧 订正（round24）：签名加 `string? entityType = null, int? modId = null`，搜索范围扩为 Subject/ID + **所有 string 属性**。
- **`HostService.cs`**：实现——遍历 `Constants.GameTypes`，反射调 `Repository<T>().GetAllAsync()` 全表加载 + `$"{Subject} {EntityId}"` 子串过滤（StringComparison.OrdinalIgnoreCase），取 limit。
- **`EditorTools.cs`**：`SearchAllTypes` 改调 `_hostService.SearchEntitiesAsync(query, limit)`，格式化复用。工具数不变（12）。
- 同步更新 **3 处 IHostService stub**（`McpToolExecutorTests`/`McpServerHostTests`/`Infra.Tests StubHostService`）。
- 测试 +5：Mcp `SearchAllTypes_DelegatesToHostServiceSearch` +1；Infra `HostServiceSearchTests` 4 例（大小写/limit/空 query/无匹配）。

### 3. Profile Tool 重构
- **`ProfileToolTreeNodes.cs`**：`ProfileModNode/ProfileXmlNode/ProfileDataTypeNode` 三节点 → **单一 `ProfileTreeItem`**（Kind: Mod|Xml|DataType + Name/Path/ModId/Count/IsGame/TypesLoaded/Children + DisplayName/Icon）。理由：`HierarchicalModel<T>` 需单一 T。
- **`ProfileToolViewModel.cs`**：
  - **游戏本体**：`BuildRootItems` 无条件首个 `Game` 节点，扫 `gameRoot/data/*.xml`（ModId=-1）；ModLoadInfos 里 ModId==-1 的条目跳过防重复。
  - **懒加载**：`TreeModel = HierarchicalModel<ProfileTreeItem>` + `ChildrenSelectorAsync = LoadChildrenAsync`，替代 `TreeViewItem.Expanded → EventTriggerBehavior`（删掉 XAML 里的 Interaction.Behaviors）。XML 节点首展开才加载数据类叶子。
  > 🔧 订正（round24）：`ChildrenSelectorAsync`（真实异步 DB I/O）配合 ProDataGrid **同步阻塞的 `Expand()`** 会导致 `SetNodeExpandedState` 落线程池 → `VerifyAccess` 展开崩溃。改为 **同步 `ChildrenSelector = LoadChildren`** + `RebuildTreeAsync` 的 `Task.Run` 后台**预取** stats（`PrewarmEntityStats`，含 Game `ModId=-1`），展开时纯内存命中。
  - **叶子根因**：`ModEntityStats.LoadModEntityStats(db, modId, gameRoot)` 增加 gameRoot 参数，**非 rooted DB FilePath 先 `Path.Combine(gameRoot, path)` 再 Normalize**（游戏数据存 `data/*.xml` 相对路径，`Path.GetFullPath` 相对 CWD 导致不匹配）；basename 兜底保留；`LoadXmlTypesAsync`/`LoadModStatsAsync` catch 改 Serilog 日志（不再静默）。
  - **选择**：`SelectedRow`(object) 绑 DataGrid.SelectedItem → `SelectedItem` 解包 `HierarchicalNode.Item`。
  - **命令**：`OpenXmlCommand`（双击 XML 发 `OpenXmlDocumentMessage`，复用现有只读 XmlDocument 机制）/ `OpenDirectoryCommand`（explorer 定位 dir/文件）/ `OpenFileCommand`（默认程序打开）。
- **`ProfileToolView.axaml`**：`TreeView` → **ProDataGrid 层级 DataGrid**（`HierarchicalModel` + `HierarchicalRowsEnabled` + `Classes="hierarchical"` + `DataGridHierarchicalColumn` 单列，CellTemplate 绑 `ProfileTreeItem`）+ `ContextMenu`（Open in Explorer / Open file）+ 双击事件。
- **`ProfileToolView.axaml.cs`**：`DoubleTapped` → `vm.OpenXmlCommand.Execute(null)`（R04 输入→VM 命令）。
- 测试：VM 在 App 项目（无独立测试项目），靠真机验证。

### 4. Image Browser 预览上下分栏
- **`ImageAssetManagerView.axaml`**：`Grid ColumnDefinitions="*,4,Auto"`（左右）→ `RowDefinitions="Auto,*,4,Auto,Auto"`（上下：工具栏/树/splitter/预览/页脚）；splitter 改横置 `Height="4"`；预览 `Border` 移到下方整行、`MinHeight=140`，内部 `StackPanel` 改横向（图左、信息右）。VM 无改动。

### 5. 帮助文档 MD VSCode 主题
- **新增 `NeoEditor.App/Assets/MarkdownTheme.axaml`**：覆盖 LiveMarkdown 默认资源 token（`BorderColor`/`ForegroundColor`/`CardBackgroundColor`/`SecondaryCardBackgroundColor`/`CodeInlineColor`/`QuoteBorderColor` 用 VSCode Dark+ 配色 + 字体大小）+ 自定义 `MarkdownBackgroundBrush`/`MarkdownTextBrush`/`MarkdownCodeBlockBrush`/`MarkdownLinkBrush`。
- **`App.axaml`**：`Defaults.axaml` 之后追加 `<ResourceInclude Source="avares://NeoEditor/Assets/MarkdownTheme.axaml"/>`（注意 App 程序集名是 **NeoEditor**，非 NeoEditor.App）。
- **`DocumentWorkspaceView.axaml`**：MarkdownRenderer 加 `CodeBlockColorTheme="DarkPlus"` + `Margin`，ScrollViewer 背景 `MarkdownBackgroundBrush`。

### 6. Image Orchestration 树形网格
- **`ImageOrchestrationViewModel.cs`**：
  - `TreeModel = HierarchicalModel<object>`，`ChildrenSelector = o => o is SourceNode s ? s.Pairs : null`、`IsLeafSelector = o => o is not SourceNode`；`ApplySources` 时 `TreeModel.SetRoots(sources)`。
  - 选中改 `SelectedRow`(object) 绑 DataGrid → `OnSelectedRowChanged` 同步 `SelectedSource`/`SelectedPair`（**选中 source 仍自动选其首对**，与旧双栏行为一致；选中 pair 解析归属 source）。
  - 命令（Save/Add/MoveUp/MoveDown/Delete）语义不变，`SelectedPair = ...` 赋值保留。
  - `ImageOrchestrationSourceNode` 加 `MissingSummary`。
- **`ImageOrchestrationView.axaml`**：左右双 ListBox → **单一层级 DataGrid**（Name 树列 source 名+摘要/pair normal / x2 列 / Status 列 ✓✗；行模板用 `ContentControl` 按 Source/Pair 类型选 DataTemplate）+ 顶部动作工具栏（↑↓ + - 💾 刷新）+ 页脚图例。`ImageTools.csproj` 补引 ProDataGrid 12.0.4。
  > 🔧 订正（round24）：`DataGridHierarchicalColumn` 的 CellTemplate **DataContext 是 item 本身**（非 node），`ContentControl` + `x:DataType` 类型分派运行时匹配失败回退 `ToString()` 类名。改为两节点类型暴露统一属性（`RowTitle/RowSubtitle/X2Text/IsPair/...`）+ **单模板直接绑 item 属性**（`{Binding RowTitle}`，无 `Item.` 前缀）。
- 测试 +3：`TreeModel_IsBuiltWithSourcesAsRoots`、`Selection_OnSourceRow_SelectsSourceAndFirstPair`、`Selection_OnPairRow_ResolvesOwningSource`（原有测试不改动全过）。

---

## 编译和测试

| 项目 | 结果 |
|------|:----:|
| `dotnet build NeoEditor.sln`（全量 22 项目） | **0 错误** ✅（Rider build_solution 中途无诊断失败，以 dotnet 为准） |
| Messaging.Tests | 3/3 ✅ |
| Core.Tests | 47/47 ✅ |
| UI.Common.Tests | 1/1 ✅ |
| Infra.Tests | **148/148 ✅（+4）** |
| DataViewer.Tests | 61/61 ✅ |
| EntityEditor.Tests | 28/28 ✅ |
| Mcp.Tests | **25/25 ✅（+1）** |
| Cli.Tests | 40/40 ✅ |
| AiChat.Tests | **32/32 ✅（+3）** |
| ImageTools.Tests | **33/33 ✅（+3）** |
| Integration.Tests | 12/12 ✅ |
| **总计** | **430/430 ✅（+11）** |

> 修过的编译/分析问题：ImageTools 补 ProDataGrid 引用；`StringFormat='{0} missing'` 以 `{` 开头触发 AVLN2000 → 改用 `MissingSummary` 属性；`avares://NeoEditor.App/...` 程序集名应为 **NeoEditor**；`HierarchicalNode<T>` 打包版无 public `Children`（测试改用 `FindNode`/`Root` 非空断言）；未用参数 `_ = value` 清零。

## 真机冒烟

- 从 `NeoEditor.App/bin/Debug/net10.0` 启动 `NeoEditor.exe`，进程 18s+ 稳定存活（~352MB），日志无 Error/Exception（`[PhpParser] parsed 2326 images`、Help 加载 10 项）。
- **ImageOrchestrationViewModel 构造即自动刷新**（`_ = RefreshAsync()`），说明其层级 DataGrid XAML 运行时加载无异常；Profile Tool / AI Chat / Markdown 主题随 Dock 构建全部加载成功。

## 剩余项（技术债）

| 项 | 说明 |
|---|------|
| Profile Tool / Orchestration 交互真机验证 | 打开 profile 展开树、右键/双击、Orchestration 移动/增删/保存需人工 UI 操作确认（代码路径已有单测覆盖） |
| 打包版 `HierarchicalNode<T>` API 差异 | 源码仓库 vs NuGet 12.0.4 有差异（typed struct 无 public Children），本项目只用 untyped `HierarchicalNode` + `SetRoots`/`Root`，无影响 |
| Markdown 主题细节 | VSCode Dark+ 是固定深色阅读面板，与浅色 Fluent 主题可能不搭；如需随系统明暗切换需再调 |
