# 架构测试第23轮 — Round22 七项改造·人工验收清单

> 日期：2026-08-02 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 12.1 / ProDataGrid 12.0.4)
> 被测内容：test_round22 的七项改造（AI Chat 可视化 / HostService 搜索 / Profile Tool 树形网格 / Image Browser 布局 / MD 主题 / Orchestration 树形网格）
> 上承：[test_round22_summary.md](test_round22_summary.md)（开发完成，430/430 单测通过）
> 本清单由**人工逐项验收**，请在「结果」列标记 ✅ / ❌ / ⚠️，并记录异常日志。

> ⚠️ **清单已由 [round24](test_round24_summary.md) 订正**（2026-08-02）：
> 验收过程中暴露的问题已修复——**Profile Tool 展开崩溃**（§3.3，同步 selector + 后台预取）、**Orchestration Name 列显示类名/空**（§6，
> CellTemplate DataContext=item → 统一属性+单模板）、**AI Chat 工具块改 Expander + Send/Stop toggle**（§1）。相关验收预期以下表为准。

---

## 0. 准备

- [ ] 从仓库根执行 `bash build.sh`（或 Rider build）确认全量构建 0 错误。
- [ ] 确认单测：`dotnet test NeoEditor.sln` → 预期 **430/430**。
- [ ] 从输出目录启动：`cd NeoEditor.App/bin/Debug/net10.0 && ./NeoEditor.exe`（须 CWD=输出目录，否则找不到 appsettings.json）。
- [ ] 打开一个 profile（Workspace 按钮 → 双击历史 profile，或 Profile Tool 里 New/Import 后 Edit Profile），让 Profile Tool / 合并视图 / Image 工具拿到活跃 profile。

> 提示：窗口里日志在控制台/stdout 可见；涉及 Serilog 的报错会打 `[ERR]` / `[FTL]`。

---

## 1. AI Chat — 气泡 + 工具调用块（`AiChatView`）

**前置**：Settings → AI & MCP 已配置 API Key（否则面板显示禁用提示，见 §1.4）。

- [ ] **1.1 普通对话**：发送一条消息 → 左侧出现「AI」标签的 **assistant 气泡**（有可见边框、半透明白底、可选中文本），右侧出现你的 **user 蓝色气泡**。两边对齐一左一右。
- [ ] **1.2 流式打字**：回复逐字出现（typewriter），无整块卡顿；未完成时气泡右上角有「…」。
- [ ] **1.3 工具调用区分**：发送一条会触发 MCP 工具的提问（如「搜索名字含 stone 的实体」「看看当前工作区状态」）→ 期待：
  - 工具执行期间出现**独立深色块**（⚙ + 工具名，如 `SearchAllTypes` / `GetModInfo`），**不再混进正文**；
  - 工具块之后继续输出正文，正文里**不含** `[tool:` 标记；
  - 若模型只调工具没正文，不出现空白的 assistant 气泡。
- [ ] **1.4 未配置降级**：清空 API Key 重启 → 面板顶部橙色提示「not configured」，Send/Build Index 禁用，应用不崩溃。

---

## 2. HostService 搜索 — MCP `SearchAllTypes` 走新方法

**前置**：可二选一验证。

- [ ] **2.1（GUI 侧，推荐）**：无 API key 也行——另开终端，启动 `./NeoEditor.exe --mcp`（stdio 协议），用任意 MCP 客户端（或官方 `StdioClientTransport` 样例）：
  1. `tools/list` → 应仍返回 **12** 个工具，含 `SearchAllTypes`；
  2. `tools/call SearchAllTypes` `{"query":"独头弹"}` → 返回 `totalMatches>0`、`items[].entityType/entityId/subject`，且不再直接全表内存扫（日志无变化不影响功能）。
- [ ] **2.2（AI Chat 侧）**：§1.3 中工具块名称出现 `SearchAllTypes` 即证明链路通。
- [ ] **2.3 回归**：`GetEntity` / `ListEntities` / `GetEntitySchema` 仍正常（未受影响）。

---

## 3. Profile Tool — 树形网格（`ProfileToolView`）

**前置**：已打开 profile；游戏根目录存在 `data/*.xml`。

- [ ] **3.1 游戏本体节点**：树最上方应出现 **🌍 Game** 节点（不再缺），展开后列出 `data/*.xml`（itemtypes.xml / recipes.xml 等）。
- [ ] **3.2 Mod 节点**：Game 之下按 profile 列出各 mod（📦），展开显示其 XML 文件。
- [ ] **3.3 XML 展开数据类**：双击或点击某个 XML 节点的展开箭头 → 首次展开会**懒加载**出非空数据类叶子（如 `ItemType (26)`、`Recipe (11)`）；无数据的 XML 展开后为空。
- [ ] **3.4 双击 XML → 只读页**：双击任意 XML 节点 → 主工作区打开该 XML 的**只读**编辑器（AvaloniaEdit，XML 高亮，禁用编辑）。
- [ ] **3.5 右键菜单**：在 Mod 行右键 → 「Open in Explorer」打开其目录；在 XML 行右键 → 「Open in Explorer」用资源管理器选中该文件、「Open file」用默认程序打开。
- [ ] **3.6 工具栏**：New / Import / Edit / Reload 四个图标按钮，Edit/Reload 仅在活跃 profile 存在时可用。

> ⚠️ 若 3.3 无叶子：看日志 `[ERR] Failed to load data-class stats for ...`（路径匹配失败会有该日志，说明 DB FilePath 与磁盘路径仍不匹配）。

---

## 4. Image Browser — 预览上下分栏（`ImageAssetManagerView`）

- [ ] 打开 Image Browser 工具：左侧**树**在上、**预览区在下**（不再是左右分栏）；拖动中间横向 splitter 可调预览高度。
- [ ] 选中一张图 → 下方预览显示缩略图（左侧）+ 文件名 / 尺寸 / Mod / x2 路径（右侧）+ Open 按钮。
- [ ] 搜索框 / 刷新按钮仍可用。

---

## 5. 帮助文档 MD — VSCode Dark+ 主题

- [ ] 菜单 Help → 任选一篇帮助文档（`Help/zh/*.md`）→ 渲染为**深色阅读面板**（背景 `#1E1E1E`、正文浅色），标题层级分明。
- [ ] 代码块为 **Dark+ 语法高亮**（深色块）；行内代码橙色；引用块有绿色左边框。
- [ ] 表格有清晰边框；图片能正常加载（相对路径）。
- [ ] 其它页面（非 md 文档）不受影响。

> ⚠️ 若仍是旧观感：确认 `App.axaml` 里 `MarkdownTheme.axaml` 在 `Defaults.axaml` **之后** include，且 URI 是 `avares://NeoEditor/Assets/MarkdownTheme.axaml`（程序集名是 `NeoEditor`）。

---

## 6. Image Orchestration — 树形网格（`ImageOrchestrationView`）

- [ ] 打开 Image Orchestration 工具：**单一层级表格**（不再是左右两个 ListBox）。表头：Name / x2 / Status。
- [ ] 根行 = 各 source（Base Game 只读 + 各 mod），**source 可展开** → 其下挂它的 normal→x2 图片对。
- [ ] 每对显示：normal 名（Name 列）、x2 名（x2 列）、两个 ✓/✗（Status 列：绿=文件存在，红=缺失；x2 无文件时为 ✗）。
- [ ] source 行在 x2 列显示「N missing」摘要（有缺失时）。
- [ ] 选中一个 mod 的对（或 source）→ 顶部 `↑` / `↓` / `+` / `-` / `💾` 生效：
  - `↑`/`↓` 调整该 source 内对的顺序；
  - `+` 选图导入（拷入 `img/`）；
  - `-` 删除该对；
  - `💾` 写回 `getimages.php`（Base Game 选中时 Save/Add 禁用）。
- [ ] 刷新按钮 `↻` 重新加载；Base Game 行只读（选中后 Save 灰）。

---

## 7. 回归

- [ ] 启动 12s+ 无崩溃，无 `[ERR]/[FTL]`。
- [ ] Profile Tool 展开 Mod/XML/数据类时**不闪退**（round21 的 `RelayCommand<ProfileXmlNode>` 类型崩溃已消除）。
- [ ] 合并视图（DataTable 工具）正常加载 24 tabs（ItemType / Recipe / TreasureTable）。
- [ ] AI Chat 无配置时不崩（§1.4）；`--mcp` 启动路径正常（§2.1）。

---

## 验收结果汇总

> 注意：§1 / §2 / §3 / §6 的实现已在 round24 订正，请按下方「备注」的 round24 版本验收。

| 区块 | 结论 | 备注 |
|------|:----:|------|
| 1 AI Chat 气泡/工具块 | ☐ | 工具块已改 **Expander**（header=工具名，展开看结果）；Send 原地变 Stop 可中断（round24 §C#4/§C#5） |
| 2 HostService 搜索 / MCP | ☐ | `SearchAllTypes` 加 `entityType`/`modId` 过滤 + 全字段搜索 + limit 100（round24 §C#3） |
| 3 Profile Tool 树形网格 | ☐ | **展开崩溃已修复**：同步 selector + 后台预取，3.3 懒加载叶子可用（round24 §A）；叶子箭头隐藏（round24 §B#2） |
| 4 Image Browser 上下分栏 | ☐ | 无改动 |
| 5 MD VSCode 主题 | ☐ | 无改动 |
| 6 Orchestration 树形网格 | ☐ | Name 列显示 source 名/图片文件名（round24 §C#1） |
| 7 回归 | ☐ | |

## 问题记录

| # | 区块 | 现象 | 日志/截图 | 严重度 |
|---|------|------|-----------|:----:|
| 1 | | | | |
