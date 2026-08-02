# 架构测试第21轮 — UI 六项改造（工具栏 / Index 列宽 / 图片源 / Profile 树 / AI Chat）

> 日期：2026-08-02 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 12.1 / ProDataGrid 12.0.4)
> 上承：[test_round20_summary.md](test_round20_summary.md) (M13+ Phase 5 & 6)
> 下接：Phase 9 收官后的 UI 收尾轮

## 本轮目标

完成用户提出的 **6 项 UI 改造**，覆盖 DataViewer / DataViewer 共享视图 / ImageTools / App shell（Profile Tool）/ AiChat 插件：

1. DataTable 工具栏：去掉「返回」按钮 + 统一按钮尺寸（消除一大一小参差不齐）
2. Ref Index / Reverse Index 网格列宽自适应（占满宽度、列头不被遮挡）
3. 确认 Ref/Reverse 已用 ProDataGrid（**确认项，无代码改动**）
4. Image Orchestration / Image Browser 改为**按 profile 里指定的 mod 目录**找 `img/`（不再硬编码 `gameRoot/Mods/*`）
5. Profile Tool：全按钮换图标 + 一行工具栏 + 去掉下拉与标题 + Mod→XML→非空数据类树（跟随活跃 profile）
6. AI Chat：修复输入框撑满布局根因 + 对话历史改 **DataGrid ChatMimic** 气泡

> 用户拍板：任务 5 删除 profile 下拉（**一个页面 = 一个 profile**）；任务 6 历史用 DataGrid ChatMimic（ProDataGrid 无独立「对话式控件」，只有官方 Sample 里用单列 DataGrid 模拟聊天的示例）。

---

## 完成的工作

### 1. DataTable 工具栏（任务 1）
- **文件**：`NeoEditor.App/Views/UserControls/ModGameDataTabsView.axaml` + `.axaml.cs`
- 删除左上角「返回」按钮（`OnBackNavigationClick`）并清理全部导航死代码：`_navHistory`、`_isNavigatingBack`、`CanNavigateBack`（StyledProperty + 属性）、`NavigateToEntity` 内的 nav push。
- 工具栏外层 `<Border>` 新增**作用域内统一控件样式**：`Button`/`ToggleButton`/`TextBox`/`ComboBox` 统一 `MinHeight=26`，`Button` 统一 `Padding=8,3` + `FontSize=11`；移除文本按钮内联 `Padding/FontSize`（原来是内联值盖过默认，造成图标按钮用大 Padding、文本按钮偏矮）。
- 「清除列过滤 ✕」文本按钮 → `SymbolIcon FilterDismiss`；SaveAndLaunch 图标 `FontSize` 12→14 对齐。

### 2. Ref/Reverse Index 列宽自适应（任务 2）
- **文件**：`NeoEditor.Plugins.DataViewer/Views/IndexTableView.axaml`
- Forward 主列 `entity_id`、Reverse 主列 `raw_id` 由固定像素 → `Width="*"`，网格吃满剩余空间，列头不再被固定窄列挤压（窗格过窄时 DataGrid 水平滚动兜底）。

### 3. ProDataGrid 确认（任务 3）
- **无代码改动**。ProDataGrid 12.0.4 产出**同名程序集** `Avalonia.Controls.DataGrid.dll`（hard fork），全项目 `<DataGrid>` 元素（含 Ref/Reverse 共用的 `IndexTableView`）**本来就是 ProDataGrid** 实例。

### 4. Image Tools 按 profile 找 img 目录（任务 4）
- **新文件**：`NeoEditor.Plugins.ImageTools/Services/ProfileModSourceProvider.cs` — `IProfileModSourceProvider` + `ModContentRoot` record。从最近 `LoadProfileMessage`/`OpenMergeEditorMessage` 的 `ProfileInfo.ModLoadInfos` 解析每个 mod 的 contentRoot（`gameRoot + ModInfo.Path`，兼容绝对/相对路径），Base Game 恒为 `gameRoot`，`IsGameMod` 去重；**无 profile 时回退到原 `Mods/` 扫描**。消息 handler 只存 ProfileInfo 引用（ModLoadInfos 由其它接收方在 dispatch 时同步填充，handler 内读会竞态）；`GetContentRoots()` 在 VM 的 `Task.Run` 之前于 UI 线程取快照。
- **改动**：`ImageOrchestrationViewModel` / `ImageAssetManagerViewModel` 构造注入 provider，`BuildSources()`/`BuildTree()` 改由 `IReadOnlyList<ModContentRoot>` 驱动，并**补订 `OpenMergeEditorMessage`**（否则从 Profile Tool 触发加载对 ImageTools 不可见）。
- **DI**：`ServiceCollectionExtensions.cs` 加 `AddSingleton<IProfileModSourceProvider, ProfileModSourceProvider>()`。
- **测试**：两个 VM 测试改用真实 provider（无 profile 走 Mods/ 兜底，现有断言不变）+ **新增 2 例**：`LoadProfileMessage` 携带 ModLoadInfos → 源来自 `Path` 指定的任意目录（非 `Mods/`）。

### 5. Profile Tool：图标工具栏 + Mod→XML→数据类树（任务 5）
- **新文件**：
  - `NeoEditor.App/ViewModels/MainContent/ProfileToolTreeNodes.cs` — `ProfileModNode`（Name/ModId/Path/ContentRoot/IsGame + XmlNodes）、`ProfileXmlNode`（Name/AbsolutePath + TypeNodes + TypesLoaded）、`ProfileDataTypeNode`（TypeName/Count + DisplayName）。
  - `NeoEditor.App/Helper/ModEntityStats.cs` — `LoadModEntityStats(db, modId)` 遍历 `Constants.GameTypes`（25 类反射）+ `db.GetDbSet(type)`，按 `IEntity.FilePath` 分组计数，路径归一化 `GetFullPath` + 正斜杠 + OrdinalIgnoreCase。
- **改动**：`ProfileToolViewModel.cs` 删除 `ProfileOption`/`Profiles`/`SelectedProfile`/`LoadProfilesAsync`/`_editorDbFactory`；新增 `_currentProfile` + `ModNodes` 树 + `HasActiveProfile`；实现 `LoadProfileMessage`/`OpenMergeEditorMessage`/`EditProfileMessage`/`RefreshModMessage`/`GameRootDirChangedMessage` 接收（活跃 profile 跟随，空 ModLoadInfos 用 `IProfileManager.LoadMods` 幂等补填）；`EditProfile`/`ReloadMergeView` 作用于活跃 profile（null 时禁用）；`LoadXmlTypesCommand` 懒加载 XML 节点数据类（按 mod 一次 DB 查询缓存）。
- **改动**：`ProfileToolView.axaml` 重写为 `DockPanel`（一行图标工具栏 `AddCircle`/`ArrowCircleDownRight`/`Edit`/`ArrowClockwise` + ToolTip，**无下拉/无标题**）+ 三层 `TreeView`（`TreeView.DataTemplates` 按节点类型匹配；XML 层 eager 扫描，数据类层经 `TreeViewItem.Expanded` → `EventTriggerBehavior` → `LoadXmlTypesCommand` 懒加载；XML 节点与 DB `FilePath` 归一化路径不匹配时 basename 兜底）。

### 6. AI Chat：DataGrid ChatMimic + 布局修复（任务 6）
- **根因**：`AiChatView.axaml` 用 `DockPanel`，输入区 `Border` 是**最后一个子元素** → `LastChildFill` 让它填充剩余空间，历史 ScrollViewer（默认 Dock=Left）被挤压 →「输入框太大、没有历史显示」。
- **改动**：`AiChatView.axaml` 改 **Grid 行布局**（`Auto,Auto,Auto,*,Auto`：header / 未配置横幅 / System Prompt 面板 / 历史 / 输入）；历史 `ScrollViewer+ItemsControl` → **单列 ProDataGrid**（`HeadersVisibility=None`、`DataGridTemplateColumn Width="*"`，气泡模板 `HorizontalAlignment` 按 `IsUser` 右/左 + 半透明蓝气泡，保留 Role 标签 + IsThinking 指示器）。
- **新文件**：`NeoEditor.Plugins.AiChat/Converters/BubbleConverters.cs` — `BoolToHorizontalAlignmentConverter` + `BoolToBubbleBrushConverter`。
- **改动**：`AiChatView.axaml.cs` 订阅 `Messages.CollectionChanged` → `ScrollIntoView` 最后一条。
- **csproj**：AiChat 插件补引 `ProDataGrid 12.0.4`（此前无 DataGrid 包，`<DataGrid>` 无法解析）。

---

## 编译和测试

| 项目 | 结果 |
|------|:----:|
| Rider `build_solution_start` + `build_solution_state`（全量 22 项目） | **buildIsSuccess: true，0 problems** ✅ |
| Messaging.Tests | 3/3 ✅ |
| UI.Common.Tests | 1/1 ✅ |
| Cli.Tests | 40/40 ✅ |
| Mcp.Tests | 24/24 ✅ |
| Infra.Tests | 144/144 ✅ |
| DataViewer.Tests | 61/61 ✅ |
| EntityEditor.Tests | 28/28 ✅ |
| Core.Tests | 47/47 ✅ |
| AiChat.Tests | 29/29 ✅ |
| ImageTools.Tests | **30/30 ✅（+2 新增）** |
| Integration.Tests | 12/12 ✅ |
| **总计** | **419/419 ✅（+2）** |

> 修过的编译问题：`ImageAssetManagerViewModel` 缺 `using NeoEditor.Plugins.ImageTools.Services`；Avalonia `HorizontalAlignment` 在 `Avalonia.Layout`（非 Controls）；`DataGrid` 无 `Items` 属性（改用 VM 的 `Messages` 滚底）；`TreeDataTemplate` 无 `ItemTemplate`（改用 `TreeView.DataTemplates` 按类型匹配）。

---

## 真机冒烟

- 从输出目录（`bin/Debug/net10.0`）直接启动 `NeoEditor.exe`（须 CWD=输出目录，否则找不到 `appsettings.json`）。
- 进程 15s+ 稳定存活（~358MB），**无崩溃**；日志无 Error/FTL（`PhpParser parsed 2326 images`、Help 加载 9 项）。
- 两个重写的 View（`ProfileToolView` / `AiChatView`）在 D02 动态 Dock 构建时实例化，**XAML 运行时加载无异常**。

## 剩余项（技术债）

| 项 | 说明 |
|---|------|
| AI Chat streaming 滚动跟随 | 仅在新增消息时滚底；逐 token 更新期间不跟随（增强项，未做） |
| 工具栏观感 | 统一 MinHeight=26，但具体间距/图标大小需肉眼确认微调 |
| 树数据类懒加载 | 首次展开某 mod 的 XML 时做一次 25 类 DB 查询（SQLite 本地，可接受）；超大 profile 首展开略慢 |
