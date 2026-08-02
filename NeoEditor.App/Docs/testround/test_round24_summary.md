# 架构测试第24轮 — Profile Tool 崩溃修复 + Round23 八项 + 二轮修正五项

> 日期：2026-08-02 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 12.1 / ProDataGrid 12.0.4)
> 上承：[test_round23_summary.md](test_round23_summary.md)（round22 七项人工验收清单，本轮回填并订正）
> 订正对象：[test_round22_summary.md](test_round22_summary.md)（§2 / §3 / §6 描述的实现已被本替换）

---

## 本轮内容

### A. Profile Tool 展开崩溃修复（ProDataGrid 线程亲和）⭐

- **现象**：真机展开 Profile Tool 里某个 XML 节点 → 闪退：
  `HierarchicalModel.ExpandAsync → SetNodeExpandedState(node, true) → HierarchicalNode.set_IsExpanded → OnPropertyChanged → DataGridFormulaModel.Item_PropertyChanged → DataGridDataConnection.IndexOf → Dispatcher.VerifyAccess() → InvalidOperationException`。
- **根因**（源码 `ProDataGrid/src/.../HierarchicalModel.cs`）：
  - `Expand(HierarchicalNode)` 是**同步阻塞**的：`ExpandAsync(node, ct).GetAwaiter().GetResult()`；
  - `ExpandAsync` 内部全用 `ConfigureAwait(false)`，所以续体跑在**完成 await 的线程**上；
  - 上一版 `ChildrenSelectorAsync = LoadChildrenAsync` 内 `await CreateDbContextAsync().ConfigureAwait(false)`（真实异步 EF I/O）→ selector 的 Task 在线程池完成 → `ExpandAsync` 续体落线程池 → `SetNodeExpandedState`（设 `IsExpanded`）在线程池线程执行 → DataGrid formula model `VerifyAccess` 崩溃。
- **修复**（`ProfileToolViewModel`）：selector 改**完全同步** `ChildrenSelector = LoadChildren`；慢的 DB 统计
  （`ModEntityStats.LoadModEntityStats` 按 ModId 全类型扫）挪到 `RebuildTreeAsync` 的 `Task.Run` 后台**预取**
  （`PrewarmEntityStats(modIds, gameRoot)`，含 Game `ModId=-1`）；展开时只做内存字典命中 + UI 线程改树。
  `ExpandAsync` 全程无 yield → `SetNodeExpandedState` 恒在 UI 线程。
- 附：`GetAwaiter().GetResult()` 阻塞 + 同步上下文捕获时是**死锁**而非崩溃；崩溃/死锁择一取决于同步上下文是否捕获。

### B. Round23 八项（第一轮反馈）

1. **Q1（回答）**：Profile Tool 是 **ProDataGrid 层级 DataGrid**（`DataGrid` + `HierarchicalModel` + `HierarchicalRowsEnabled`），不是 Avalonia `TreeDataGrid`。
2. **Q2 叶子箭头**：`HierarchicalOptions` 加 `IsLeafSelector = item.Kind == DataType`，数据类叶子不再显示展开箭头。
3. **Q3 Orchestration 类名**：`ContentControl` + `x:DataType` 类型分派运行时匹配失败回退 `ToString()` 类名 → 两个节点类型加统一属性
   （`RowTitle/RowSubtitle/HasRowSubtitle/RowToolTip/X2Text/X2ToolTip/IsPair/NormalMissing/X2Missing`）+ 单模板（初版，见 §C#1 订正）。
4. **Q4 Enter 发送**：AI Chat 输入 TextBox 加 `<KeyBinding Gesture="Enter" Command="{Binding SendMessageCommand}"/>`。
5. **Q5 工具 Expander**：`ChatService` 在 `[tool: executing X]` 后新增 `yield "\n[tool: result X]\n{json}"`；`AiChatViewModel` 加
   `ToolResultMarkerRegex` 把结果附加到 `ChatMessageItem.Content`（新增 `ToolName`）；`AiChatView` 工具块 Border → **Expander**（header=工具名，content=结果）。
6. **Q6 Stop + 上限**：`StopCommand` → `SendMessageCommand.Cancel()`（`OperationCanceledException` 捕获为 "Stopped." 系统消息）；
   `AppConfig.MaxToolCallsPerConversation`（默认 30）+ Settings → AI&MCP「每轮最大工具调用次数」（resx `Settings.MaxToolCalls`/`MaxToolCallsNote`）+ `ChatService` 读 `IConfigService`。
7. **Q7 工具描述**：`SearchAllTypes` 标为「类型未知时首选」，`ListEntities` 改「仅已知类型时用」，`GetEntitySchema` 描述修正。
8. **Q8 放大镜（初版）**：`PeekReferenceRequestMessage` handler 加 `IsRightToolVisible=true` + `IEntityLookupService` 兜底（见 §C#2 订正——改用 `PeekEntityMessage`）。

### C. 二轮修正五项（第二轮反馈）

1. **Orchestration「同 Profile Tool 同款」**：`DataGridHierarchicalColumn` 的 CellTemplate **DataContext 就是 item 本身**
   （`BindContent` 把 `Content` 绑到 `Binding="{Binding Item}"` 解包后的实体）——所以模板**直接绑 item 属性**
   （`{Binding RowTitle}`，无 `Item.` 前缀、无 `x:DataType` 包装），与 Profile Tool 的 `{Binding Icon}` 一致。上一版
   `{Binding Item.RowTitle}` 是错的（item 没有 `Item` 属性 → 列空）。
2. **放大镜改用 `PeekEntityMessage`**：`DocumentWorkspaceViewModel` 已有能工作的 peek 路径
   （`PeekEntityMessage` handler = `IsRightToolVisible=true` + `PeekPanel.Peek`）。`ReferenceFieldEditor.OnPeekClick`
   不再依赖脆弱的 `_currentEntity`（visual-tree walk 找 KV VM，可能为 null 导致直接 return），改用徽章同款解析：
   `IReferenceListSerializer.Deserialize` → `GetBaseEntityRef` → `IEntityLookupService.FindBestMatch`，命中后发
   `PeekEntityMessage(_refAttr.TargetEntityType, target.EntityId, target)`。删除 `_currentEntity`/`FindCurrentEntity`
   与死消息 `PeekReferenceRequestMessage`（record + handler 全删，N04）。
3. **SearchAllTypes 增强**：`IHostService.SearchEntitiesAsync` 签名加 `entityType`/`modId` 过滤 + **搜索所有 string 属性**
   （原来只搜 Subject+EntityId，搜「NeoScavExtended」必 0）；`SearchAllTypes` 加 `entityType`/`modId` 参数、limit 默认 100、
   结果项含 `modId`、描述更新。接口 / 实现 / 3 个测试桩全同步。+2 Infra 测试。
4. **Send/Stop toggle**：输入框按钮 `Content="{Binding SendOrStopLabel}"`、`Command="{Binding SendOrStopCommand}"`
   （`IsBusy ? StopCommand : SendMessageCommand`）、`IsEnabled="{Binding IsAvailable}"`；header 里的独立 Stop 按钮删除。
5. **工具上限按「调用次数」计**：原按迭代次数（一次迭代可含多个工具调用，故「看起来没触发」）；改 `executedToolCalls` 计数，
   达到上限 yield `[system: Tool-call limit reached (N)...]`；`AiChatViewModel` 加 `SystemMarkerRegex` 渲染为 System 消息。

---

## 订正说明（对 round22 文档）

| round22 章节 | 原描述 | 订正为 |
|------|--------|--------|
| §2 HostService search | `SearchEntitiesAsync(string query, int limit = 50)` 只搜 `$"{Subject} {EntityId}"` | 加 `entityType`/`modId` 过滤 + 搜所有 string 属性（§C#3） |
| §3 Profile Tool 懒加载 | `ChildrenSelectorAsync = LoadChildrenAsync`（首展开 DB 查询） | 同步 `ChildrenSelector = LoadChildren` + `RebuildTreeAsync` 后台预取 stats（§A）——原实现引发展开崩溃 |
| §6 Orchestration 行模板 | `ContentControl` 按 Source/Pair 类型选 DataTemplate | 统一属性 + 单模板，**直接绑 item 属性**（§C#1）——CellTemplate DataContext = item，`ContentControl` 类型分派运行时失效回退类名 |

---

## 编译和测试

| 项目 | 结果 |
|------|:----:|
| `dotnet build NeoEditor.sln`（全量 22 项目） | **0 错误** ✅ |
| Messaging.Tests | 3/3 ✅ |
| Core.Tests | 47/47 ✅ |
| UI.Common.Tests | 1/1 ✅ |
| Infra.Tests | **150/150 ✅（+2：entityType/modId 过滤）** |
| DataViewer.Tests | 61/61 ✅ |
| EntityEditor.Tests | 28/28 ✅ |
| Mcp.Tests | 25/25 ✅ |
| Cli.Tests | 40/40 ✅ |
| AiChat.Tests | **32/32 ✅**（工具块断言改为 `ToolName` + result marker） |
| ImageTools.Tests | 33/33 ✅ |
| Integration.Tests | 12/12 ✅ |
| **总计** | **432/432 ✅** |

## 真机验证（待人工回填）

- [ ] **Profile Tool**：展开 XML 节点数据类叶子正常懒加载、**不再闪退**（§A 修复）
- [ ] **Profile Tool**：数据类叶子不显示展开箭头（§B#2）
- [ ] **Image Orchestration**：Name 列显示 source 名 / 图片文件名（§C#1，不再空/类名）
- [ ] **KV 编辑器放大镜**：点 🔍 → 右侧 Peek 面板弹出并显示目标实体（§C#2）
- [ ] **AI Chat**：Enter 发送；工具块 Expander 点开看结果；发送时按钮原地变 Stop 可中断（§B#4/5 + §C#4）
- [ ] **Settings → AI&MCP**：MaxToolCalls 设 10 → 触发后出现「Tool-call limit reached (10)」提示条（§C#5）
- [ ] **MCP**：`--mcp` 启动 `tools/call SearchAllTypes {"query":"...","entityType":"AttackMode"}` 返回过滤结果（§C#3）

## 剩余项（技术债）

| 项 | 说明 |
|---|------|
| SearchAllTypes 按 mod **名**（strModName）过滤 | 目前只有 `modId`（数字 namespace）；按名需 profile→mod 映射，待模型场景验证后按需加 |
| 真机验收 | 上表 7 项需人工逐项确认 |
