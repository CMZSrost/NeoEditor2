# M13+ Phase 9 计划：工具栏 / HostService / 图片资产 / AI-MCP UI

> **v1.5 · 2026-08-01 · Repository 契约订正为对称契约（R26 v2）**
> 议题 1-7 全部定稿 ✅（含议题 3，R26 固化）
> 上承: M13+ Phase 1-8 + A1-A4 + G1-G3 + ProDataGrid 全部完成
> 配套 spec: 新增 D02, R27, R28, R26

> **⚠️ v1.5 修订（2026-08-01）**：**Repository 契约订正**。原 §2 的 `IEntityRepository<T>`
> （PersistAsync / LoadAsync(filePath,modId) / GetXmlFileDiffAsync，DB 端抛 NotSupported）**废弃**，
> 改为**完全对称契约**：CRUD（增删查改 4 函数）+ diff（行级+字段级 2 函数）+ dirty（暴露操作，session 持有）
> + save/export（**一个函数** `SaveAsync`）+ load/import（**一个函数** `LoadAsync`），DB 与 XML 各实现一份，
> 禁止后端特判。CRUD 经 HostService command 执行（undo/标脏/hook 免费获得，R24 彻底化）。XmlRepository
> **构造绑定 modId**。配套 spec [R26 v2](../spec/R26-save-export-repository.md)。
> ✅ 议题 2（工具栏/Dock）定稿（D02 固化）；**议题 3（保存/导出工作流）已定稿（R26 固化）**；全部 7 议题讨论完成，5 子阶段就绪
> **开发进度 (2026-08-01)**：9A ✅ + **9B B1-B5 全部完成 ✅**（双 Repository + 三动作 + per-profile dirty + IncludeGame/单 Mod 去除 + ModManager 并入 + 删 Validation + View 收敛）。371/371 测试通过。**Phase 9B 收官，下一步 9C/9D/9E**。
> **开发进度 v1.5 (2026-08-01)**：**Repository 契约按对称契约重构完成 ✅（R26 v2）**——`IEntityRepository<T>` 全对称（CRUD 4 函数 + 行级/字段级 diff 2 函数 + dirty + `SaveAsync` + `LoadAsync`），DB/XML 各实现一份，无 NotSupported/空返回特判；CRUD 经 HostService command（新增 `ReplaceEntityCommand`，缓存改由 `IEditorCommand.GetCacheDelta/GetUndoCacheDelta` 通用 delta 驱动，删除 `is Add/Delete` 类型特判）；`PreExecuteHook` 修复空挂（ExecuteAsync/ExecuteBatchAsync 均触发）；XmlRepository 构造绑定 modId；`IDataRepository` 收敛只读；`RowDiff` 替代 `XmlFileDiff`。**374/374 测试通过**（+3：DbRepository CRUD 门面 2 + PreExecuteHook 1）。
> **开发进度 v1.6 (2026-08-01)**：**9C 图片资产修正全部完成 ✅**——议题1 目录结构（Browser 纯文件系统扫描，Base Game 只扫 `img/`，**不再解析 getimages.php**；Orchestration 读取 `<gameRoot>/getimages.php` 与各 mod 的 getimages.php）；议题7 拆分（`ImageAssetManagerViewModel` 收敛为 **Image Browser**，新增 **ImageOrchestrationViewModel/View**：声明顺序展示 + ✓/✗ 文件存在性校验 + R27 三路路径解析 + MoveUp/Down/Add/Delete/Save 写回 GenerateImagePhp，Base Game 只读）；议题6 自动加载（Browser + Orchestration 构造即 Refresh，订阅 `GameRootDirChangedMessage`/`LoadProfileMessage`/`RefreshModMessage`，刷新链式串行化防并发竞态）。**384/384 测试通过**（+10：Orchestration VM 7 + Browser VM 3）。**下一步 9D/9E**。
> **开发进度 v1.7 (2026-08-01)**：**9D 代码完成 ✅，MCP 运行时问题待下一轮修复 ⚠️**——
> ✅ **9D-1 AI Chat 接入 Dock**：`AiChatTool`（Id="AiChat"）+ `DocumentWorkspaceViewModel.AiChatTool` + RightToolPane 新增 `<Tool Id="AiChat">`（`aiViews:AiChatView`）。
> ✅ **9D-2 启动路径（代码 + 主机抽象完成）**：`App.CreateHost(bool mcpMode)` 抽出组合根（GUI 与 `--mcp` 共用）；`App.EnsureDatabases(IServiceProvider)` 公共化；`Program.cs` 解析 `--mcp` / `--mcp-port`，MCP 模式**不启 GUI**；MCP 模式禁用 stdout 日志（`AddSerilogLogging(logToConsole:)` + DB `.LogTo(Console.WriteLine)` 条件化，stdout 保持协议通道纯净）；`McpServerHost.RunAsync(int? port)` 支持 stdio（默认）/ TCP（`StreamServerTransport`，预留）。
> ⚠️ **已知问题（下一轮修）**：`--mcp` 运行时 `McpServerHost.BuildOptions()` 抛 **NRE**——`McpServerOptions.ToolCollection` 为 null（SDK preview.3 用 `McpServer.Create(transport,...)` 直建时未初始化 CollectionFactory，需走 `AddMcpServer()` builder 或显式初始化）。已用官方 `StdioClientTransport` 客户端 + 真机 exe 复现（`ClientTransportClosedException`）。**9D 其余部分不受影响**。
> ✅ **9D-3 AppConfig AI/MCP 字段**：`AiEndpoint`/`AiApiKey`/`AiModel`/`AiEmbeddingModel`/`ImageModel`/`McpEnabled`/`McpPort`；`ConfigService` 用 **ProtectedData（DPAPI）加密 `AiApiKey` 落盘**（`ConfigValueProtector`，兼容旧明文 key）。
> ✅ **9D-4 配置读取改 IConfigService**：AiChat `AddAiChatPlugin`（OpenAIClient/ChatClient/EmbeddingClient）+ `ImageGenerationService` 均改为 **config.json 优先 → 环境变量 fallback**。
> ✅ **9D-5 SettingsPage "AI & MCP" 分组**：Endpoint/API Key（`PasswordChar` 掩码）/3 模型/MCP 开关+端口 + 优先级提示；`SettingsPaneViewModel` 新增 `DisplayXxx` 包装属性（SaveAsync 自动持久化）；3 个 resx 新增 `Settings.*` 键。
> ✅ **388/388 测试通过**（+4：AppConfig 默认值/JSON 往返 2 + ConfigService 加密往返/旧明文兼容 2）。**下一步：修复 `--mcp` NRE → 9E**。
> **开发进度 v1.8 (2026-08-01)**：**`--mcp` 运行时 NRE 已修复 ✅**——`McpServerHost.BuildOptions()`
> 显式初始化 `options.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.OrdinalIgnoreCase)`
> （根因：SDK preview.3 直建 `new McpServerOptions{}` 时 ToolCollection 为 null，仅 DI builder `AddMcpServer()` 路径会初始化；
> 保留 stdio + TCP 双 transport，不走硬编码 transport 的 builder）。`BuildOptions()` 改 internal +
> `InternalsVisibleTo("NeoEditor.Plugins.Mcp.Tests")`；新增 `McpServerHostTests`（2 测试）。官方 `StdioClientTransport` 真机验证：
> spawn `NeoEditor.exe --mcp` → 握手成功 → `tools/list` 返回全部 **12 工具** → `tools/call GetModInfo` 返回真实数据（24 实体类型）。
> **390/390 测试通过**（+2）。
> **开发进度 v1.9 (2026-08-01)**：**AI Chat 无配置启动崩溃已修复 ✅**——未配 API Key 时
> `AddAiChatPlugin` 的 `new ApiKeyCredential("")` 抛 `ArgumentException`（`Value cannot be an empty string`），
> 沿 `OpenAIClient → ChatClient → IChatService → AiChatViewModel → DocumentWorkspaceViewModel` 冒泡导致 **GUI 启动即挂**。
> **原则（用户决策）：无配置 → 禁用 AI Chat，配置后重启应用生效**。修复：无 key 时
> `OpenAIClient`/`ChatClient`/`EmbeddingClient` 工厂返回 null（禁用态，不再构造 credential）；`IChatService/IRagService`
> 新增 `IsAvailable`；`ChatService` 未配置时返回友好提示；`RagService.BuildIndexAsync/SearchAsync` 对 null client 守卫；
> `AiChatViewModel` 新增 `IsAvailable/CanSend/CanBuildIndex`（`[NotifyPropertyChangedFor]` 联动）+ `AiChatView` ⚠️ 横幅 +
> 禁用 Send/Build Index（绑 `CanSend/CanBuildIndex`）。新增 `ChatServiceAvailabilityTests`（4 测试，含 DI 级回归：
> 空配置 IConfigService + null IHostService 解析 `AiChatViewModel` 不抛且 `IsAvailable=false`）。
> **394/394 测试通过**（+4）。**下一步：9E（工具栏/Dock 重整，D02）**。
> **开发进度 v2.0 (2026-08-01)**：**AI 配置 Provider 列表完成 ✅**——`AiEndpoint`/`AiApiKey` 扁平字段改为 `AiProviders` 列表（`AiProviderConfig`：Id/Name/Endpoint/ApiKey）+ 每模型 `AiModelProviderId`/`AiEmbeddingProviderId`/`ImageProviderId`（空 = 第一个 provider）；新增 `AiProviderResolver`（Core 纯静态，provider > env > 默认，无 key → null 禁用态）；`ConfigService` 逐 provider 加密/解密 ApiKey + legacy 扁平配置 → "Default" provider 迁移；AiChat `ChatClient`/`EmbeddingClient` 与 `ImageGenerationService` 各按模型 providerId 解析（对话/嵌入/图片可用不同供应商）；Settings UI 改 Provider 列表编辑器 + 每模型 Provider 下拉（resx 键更新）。**408/408 测试通过**（+14）。**下一步：9E（工具栏/Dock 重整，D02）**。
> **开发进度 v3.0 (2026-08-01)**：**9E 动态 Dock 构建全部完成 ✅**——D02 落地：
> - **`IToolPlugin` 增强**：新增 `CreateToolbarItems()`（默认 null）+ `ToolbarItem` record（Id/Label/IconSymbol/Command/Group/Order，D02 §四）。
> - **动态 Dock 构建**：`Documents.cs` 删除 13 个手写 Tool 子类（OverlayChain/ValueEditor/ImagePreview/ReferenceInspector/SearchResults/KeyValueEditor/PeekPanel/DataTable/ForwardIndex/ReverseIndex/ImageAssetManager/ImageOrchestration/AiChatTool）→ 新增 `PluginTool`（Id=插件类型名，Title=plugin.Title，Context=CreateToolView，CanClose=false）；`DocumentWorkspaceViewModel.BuildToolDock()` 枚举 `IEnumerable<IToolPlugin>` 按 `DefaultDock`/`Order` 分组构建 Left/Right/Bottom 三组 `ToolDock ItemsSource`（左=1/右=2/底=2），XAML 手写 `<Tool>` 元素全删，Dock 容器保留。
> - **DataViewer 拆 5 plugin**：`DataViewerPlugin`（单）→ `DataTablePlugin`(Bottom,10)/`ForwardIndexPlugin`(Bottom,11)/`ReverseIndexPlugin`(Bottom,12)/`SearchPlugin`(Bottom,13)/`PeekPlugin`(Right,10)。DataTable 初始 Context=`DataTablePlaceholder`，由 App shell 在 profile 打开时替换为共享 `ModDataToolViewModel`（保持原行为）。新增 `IIndexTableFactory` 提供 Forward/Reverse 共享 singleton。**Conflicts/Validation 因 9B 已删除，不建**。
> - **EntityEditor 拆 2 plugin**：`EntityEditorPlugin` 收敛为纯 `IDocumentPlugin`；新增 `KeyValueEditorPlugin`(Left,10) + `OverlayChainPlugin`(Left,20)。
> - **ImageTools 拆 2 plugin**：`ImageToolsPlugin`（单）→ `ImageAssetManagerPlugin`(Left,30，从 Right 移入) + `ImageOrchestrationPlugin`(Right,35)。
> - **AiChatPlugin 改构造函数注入**：`CreateToolView()` 不再依赖 `_ctx.Services`（生产从不调 `InitializeAsync`），改 DI 构造函数注入 `AiChatViewModel`。
> - **所有 Tool VM 注册 DI singleton**（D02 §五，插件视图与 App shell 共享实例）：`ModDataToolViewModel`/`SearchResultViewModel`/`PeekPanelViewModel`/`KeyValueEditorViewModel`/`OverlayChainToolContent`/`IIndexTableFactory`。
> - **Profile Tool（新，左 Dock）**：`ProfileToolPlugin`(App, Left, Order 25) + `ProfileToolViewModel` + `ProfileToolView`——Mod 管理（New/Import Mod）+ 编排入口（Edit Profile / Reload Merge View，带 profile 选择器）。
> - **工具栏 §5.0**：顶部仅剩 `💾 Save`（New/Import → Profile Tool；+Entity/Copy/-Entity → DataTable 工具栏 `[Add] [Copy] [Delete]`，DataTable 新增 Copy 按钮 `OnCopyRowButtonClick` 克隆选中行；面板切换按钮删除）。
> - **414/414 测试通过**（净 +6：DataViewer 插件拆分测试 3→7、EntityEditor 插件拆分测试 3→5、AiChat 插件测试 5→5 改构造函数注入重写；Core 架构测试 / Integration 仅同步被删插件类引用，数量不变）。真机冒烟：GUI 启动 12s 无崩溃，ProfileToolViewModel 构造即加载 profile，动态 Dock 渲染正常。**Phase 9 全部完成（9A-9E）**。
> **开发进度 v3.1 (2026-08-01)**：**三块遗留清理全部完成 ✅**——①侧边栏精简（§5.0 遗留）：删 Mods/Profiles 按钮 → 新增 Workspace 按钮（`WorkspaceHistoryViewModel`，逆序历史 profile 工作区 + dirty 状态 + 双击打开合并视图，Transient 注册每次打开刷新）；`ModDatabaseViewModel`/`ModIndexViewModel`（含导出功能）保留无入口。②`ModImagesDocument` 插件化（D02 §五注遗留）：Core 新增 `IModImagesDocumentFactory` + ImageTools `ModImagesDocumentFactory`，App shell 不再直接 new；EditProfile 属 App 内部文档保持 App shell 处理（D02 v1.2 注明）。③**NU1903 修复**：Infra.csproj 显式 `SQLitePCLRaw.bundle_e_sqlite3 3.0.5`（2.x 无修复版，EFCore 10.0.10 仍 pin 漏洞版 2.1.11）。**416/416 测试通过**（+2 工厂测试）。GUI 冒烟无崩溃。
> 7 个议题，分 5 个子阶段

---

## 〇、议题总览

| # | 议题 | 根因 | 方案 | 状态 |
|---|------|------|------|:--:|
| 5 | 引用列放大镜一大一小、无功能 | KeyValueEditorView 与 ReferenceFieldEditor 各有一个 🔍 | 删除 View 层冗余按钮，只保留 ReferenceFieldEditor 内置 🔍 | ✅ 已完成（9A） |
| 6 | ImageAssetManager 合并视图不自动加载 | 无初始加载 / 无消息订阅 | 构造时自动 Refresh + 订阅 workspace 生命周期消息 → **并入 9C**（议题 7 重构同一文件，提前做会返工） | ✅ 已完成（9C） |
| 3 | Mod 数据 CRUD/保存/导出工作流混乱 | HostService.SaveAsync 是空壳，真正写入在 View 层 | ✅ 已定（R26 固化）：DB/XML 双 Repository + Save/Export/Publish 三动作 + partial diff 返回值 + per-profile dirty session | ✅ 已完成（9B，R26 v2） |
| 1 | Image Asset 目录结构不正确 | Base game 的 getimages.php 被忽略；顺序语义未保证 | 读取 game getimages.php；所有图片节点按编排顺序 | ✅ 已完成（9C） |
| 7 | ImageAssetManager 混合文件系统+编排 | Browser 与 Orchestration 混在一棵树 | 拆为 Image Browser（文件）+ Image Orchestration（编排） | ✅ 已完成（9C，R27） |
| 4 | AI Tool/MCP 不可见、无配置 | AiChatTool 未接入 Dock；--mcp 未实现；无设置 UI | 接入 Dock + 解析 --mcp + 新增 AI/MCP 设置页 | ✅ 已完成（9D；`--mcp` NRE + AI Chat 无配置崩溃均已修复，394/394） |
| 2 | 工具栏分散、无 Plugin 贡献模型 | IToolPlugin 元数据未被消费；Dock 手写 XAML | ✅ 全部定稿（§5.0 + §5.2.1）：顶部精简 + Profile Tool + 侧边栏精简 + IToolPlugin 动态构建（D02） | ✅ 已完成（9E，D02 落地；侧边栏工作区面板已于 2026-08-01 遗留清理完成） |

---

## 一、Phase 9A：Bug 修复（议题 5；议题 6 已并入 9C）

> ⚠️ **v1.4 修订**：原 9A 含议题 5、6。议题 6（ImageAssetManager 自动加载）改的
> `ImageAssetManagerViewModel` 正是 9C 议题 7 要重构拆分的文件——先做会在 9C 被覆盖返工，
> 故**议题 6 并入 9C**（见 §三 3.3）。9A 仅剩议题 5，极小、无依赖、可立即开工。

### 议题 5 — 引用列放大镜按钮修复

**变更**：
1. `KeyValueEditorView.axaml`：删除 `Grid.Column=2` 的 `<Button IsVisible="{Binding IsReference}">` 行模板按钮
2. `KeyValueEditorView.axaml.cs`：删除 `OnPeekClick` 方法
3. `KeyValueEditorViewModel.cs`：删除 `PeekFieldCommand`（若无其他引用）
4. 验证 `ReferenceFieldEditor.OnFieldEditorPeekClick` 发出的 `PeekReferenceRequestMessage` 正确到达 `DocumentWorkspaceViewModel` handler

**影响文件**：~3 个，删除代码为主。

---

## 二、Phase 9B：保存/导出工作流 — DB/XML 双 Repository + 三动作（议题 3）✅ 已定稿

> **定稿（2026-08-01，R26 固化）**：领域模型之上，DB 与 XML 都是**持久化后端（Repository）**。
> `Save/Export` 对 repository 语义是**同一个 Persist 操作**，只是目标源不同。HostService 开放
> **3 个动作**（Save / Export / Publish，默认 Publish=Save+Export 事务）。diff 是 **repository 的
> 能力**（策略模式），HostService 只开放 command。完整规则见 spec [R26](../spec/R26-save-export-repository.md)。

### 2.0 核心洞察：DB 与 XML 是同一个抽象的两个实现

| 动作 | HostService 语义 | Repository 实现 |
|------|-----------------|-----------------|
| **Save** | 内存 → DB | `DbRepository.SaveAsync`（= upsert 到 game.db） |
| **Export** | DB → XML 文件 | `XmlRepository.SaveAsync`（= 写回 mod 的 XML） |
| **Import/Load** | XML 文件 → 内存 | `XmlRepository.LoadAsync`（= 解析 mod XML） |

- **`save 一个 XML 就是 export；export 数据到 DB 就是 save`** —— 对 repository 而言是同一个动词
  `SaveAsync`，参数是目标源。五种能力（CRUD / diff / dirty / save-export / load-import），**DB 和 XML 各实现一份**。
- **契约完全对称**（R26 v2）：`IEntityRepository<T>` 一个能力一个函数，两端全实现，**禁止后端特判**
  （NotSupported / 空返回 / 仅单端方法）。diff 分两个函数：行级 `GetDiffAsync`（DB=行，XML=文件含旧/新快照）
  + 字段级 `GetFieldDiffAsync`（DiffEngine，两端同一实现）。
- **CRUD 经 HostService command**：增/改/删不直接写后端，构造 `AddEntityCommand / ReplaceEntityCommand /
  DeleteEntityCommand` 走 `ExecuteAsync` —— undo 栈 + 标脏 + 缓存 + 事件免费获得，R24 单写路径彻底化。
- **hook 命名针对 repository**（R25 扩展）：`PreSaveHook` 挂 `DbRepository.SaveAsync` 前，
  **新增 `PreExportHook`** 挂 `XmlRepository.SaveAsync` 前。`PreExecuteHook` 在命令执行前触发（修复空挂）。

### 2.1 对称 Repository 契约 + IHostService 三动作（定稿）

```csharp
// ── 对称契约：DB 与 XML 各实现一份（spec R26 v2）──
public interface IEntityRepository<T> : IDataRepository<T> where T : IEntity
{
    Task AddAsync(T entity);                    // 增 → AddEntityCommand → ExecuteAsync
    Task UpdateAsync(T entity);                 // 改 → ReplaceEntityCommand → ExecuteAsync
    Task DeleteAsync(string entityId);          // 删 → DeleteEntityCommand → ExecuteAsync
    // 查：GetByIdAsync / GetAllAsync（IDataRepository<T>，只读）

    Task<IReadOnlyList<RowDiff>> GetDiffAsync(IReadOnlyList<T> candidates);   // 行级/文件级
    Task<IReadOnlyList<DiffEntry>> GetFieldDiffAsync(T before, T after);      // 字段级

    IReadOnlyCollection<string> DirtyIds { get; }   // 暴露，session 持有（R01）
    void MarkDirty(IEnumerable<string> ids);
    void ClearDirty(IEnumerable<string> ids);

    Task SaveAsync(IEnumerable<T> entities);        // save/export 一个函数
    Task<IReadOnlyList<T>> LoadAsync();             // load/import 一个函数
}

// ── 动作 1：Save（内存 → DB）──
Task<SaveResult> SaveAsync(string? entityId = null);   // 单个实体（EntityEditorDocument 单 tab 保存用）
Task<SaveResult> SaveAllAsync();                        // 全部 dirty，按 modId 分组 upsert

// ── 动作 2：Export（DB → XML）──
Task<IReadOnlyList<ExportResult>> ExportModAsync(int modId);      // 单 mod（profile 编排索引）
Task<IReadOnlyList<ExportResult>> ExportProfileAsync();           // 当前 profile 全部非 game mod

// ── 动作 3：Publish（默认；Save + Export，事务）──
Task<PublishResult> PublishAsync();

record SaveResult {
    IReadOnlyList<DiffEntry> PartialDiff;   // 返回 partial diff，HostService 用它驱动 dirty 清理
    IReadOnlyList<string> SavedEntityIds;   // 供 WAL snapshot marker + UI 清理
}
record ExportResult {
    int ModId;
    IReadOnlyList<ExportFile> Files;        // 每文件：路径 + OldXml + NewXml（diff 弹窗原料）
    bool UserConfirmed;                     // false = 用户在 diff 弹窗取消
}
record PublishResult {
    SaveResult Save;
    IReadOnlyList<ExportResult> Exports;
}
```

- **3 个动作**：内部 Save、Export 两动作，顶层 Publish 组合成一个动作（符合用户直觉）。
  用户**可单独触发 Save 或 Export**，但**默认是 Publish**。MCP/CLI/UI 三端共用同一接口。
- **事务语义**：diff 弹窗取消 = 整个 Publish 事务回滚（DB 也不存），不产生部分落库。
- **返回值 = partial diff**：`SaveResult.PartialDiff` 让 HostService 自行清 dirty（R01/R09），
  View 不再手工 `RemoveDirtyEntities` / `UpdateSnapshotMarker`。

### 2.2 dirty session 粒度 = profile（R01 细化）

- 一个 profile 一个 `IWorkspaceSession`（`DirtyEntities` 作用域从"全局"收窄到"当前 profile"）。
- `IWorkspaceSession` 由工厂/注册表按 `profileId` 解析，不再全局单例。
- `IHostService.DirtyEntities` 相应按 profile 作用域。

### 2.3 Profile 引入 IncludeGame（数据/配置分离）

- **mod = 数据**（管理实体），**profile = 配置**（管理"加载什么"）。
- `ProfileInfo` 新增 `bool IncludeGame`：`true` = 加载 Game（`ModId=-1`），`false` = 仅 mod。
- **单 Mod 视图 = 一个只含该 mod 且 IncludeGame=false 的 profile** → **去除单 Mod 视图概念**，
  `ModGameDataTabsView` 统一为 profile 形态（`IsMergeView`/`ModInfo` 双模式收敛）。

### 2.4 其它收口

- **ModManager 并入**：`ImportModAsync / LoadModAsync / CreateModAsync / DeleteMod / ExportZip` 迁入
  HostService（R24 彻底化），App 层不再持有 dbFactory。
- **删除 Validation/Conflicts**（议题 3 定稿结论）：`RunPreSaveValidationAsync` / ValidationService
  挂接 / `ValidationReportDialog` 从保存管线移除；DataViewer 的 Validation/Conflicts 工具暂删，
  等有更详细文档再设计。
- **Undo/Redo 脏标记**：`UndoAsync/RedoAsync` 完成后 `_session.MarkEntitiesDirty(affectedIds)`。
- **XML 直接接入另立阶段**：本阶段维持 DB 为源、XML 为导出产物；"XML-first / 让用户以为在编辑
  XML"作为更高层 UX 目标，后续单独评估（见 §五 议题 3 备注）。

### 2.5 View 层简化（目标）

`ModGameDataTabsView` 的 `QuickSaveAsync / ShowSavePreviewAsync / ShowMergeSavePreviewAsync /
ExportEntitiesToXmlAsync / ExportXmlAsync / SaveToDatabaseAsync`（约 500 行）全部收敛为：
- `_hostService.SaveAllAsync()` / `_hostService.ExportProfileAsync()` / `_hostService.PublishAsync()`
- diff 预览数据从 `SaveResult.PartialDiff` / `ExportResult.Files[].OldXml+NewXml` 获取
- 不再持有 `IDbContextFactory<GameDbContext>` 直接写库

### 2.6 开发顺序（5 子步）

> **v1.4 新增**：9B 是最大阶段，拆 5 子步，每步独立可验证、不破坏现有 344 测试。

| 子步 | 内容 | 验证 |
|------|------|------|
| **B1** | 双 Repository 抽象（**对称契约 R26 v2**）：`IEntityRepository<T>`（CRUD 4 函数 + diff 行级/字段级 2 函数 + dirty + `SaveAsync` + `LoadAsync`）+ `DbRepository`（EF 读/行级 diff/bulk upsert+delete）+ `XmlRepository`（**构造绑定 modId**，解析本 mod/文件级 diff/写文件+删节点）。废弃原 `PersistAsync`/`LoadAsync(filePath,modId)`/`GetXmlFileDiffAsync` 特判形态 | ✅ 契约定稿（v1.5 修订），实现见本轮重构 |
| **B2** | `IHostService` 三动作：`SaveAsync/SaveAllAsync` + `ExportModAsync/ExportProfileAsync` + `PublishAsync`（事务）；返回值 `SaveResult/ExportResult/PublishResult`；激活 `PreSaveHook` + 新增 `PreExportHook` | ✅ 已完成（三动作 + 两 hook + 返回值；严格"取消回滚"随 B5 弹窗流落位；+8 测试） |
| **B3** | per-profile dirty session：dirty 集合按 profile 存于 `WorkspaceSession`（stores/indexes 保持全局单例）；`IHostService.DirtyEntities` 作用域 = 当前 profile（`SetActiveProfile`，R26 §3）；Undo/Redo 补脏标记（`ICommandHistory.Undo/Redo` 返回命令） | ✅ 已完成（per-profile session + undo/redo 补脏，+3 测试） |
| **B4** | `ProfileInfo.IncludeGame` + 单 Mod 视图去除（`ModGameDataTabsView` 双模式收敛为 profile 形态） | ✅ 已完成（`IncludeGame`/`SingleModId` DB 列 + 迁移；单 Mod 打开 = 持久化单 Mod profile；`ModGameDataTabsView` profile 化；`ReloadMergeTabsAsync` 尊重 IncludeGame；+3 测试） |
| **B5** | `ModManager` 并入 HostService + 删 Validation/Conflicts + View 收敛（约 500 行 → HostService 调用） | ✅ 已完成（ModManager 迁 Infra + HostService 实现 IModManager；删 Validation/Conflicts 7 文件 + 2 消息；`QuickSaveAsync`→`SaveAllAsync`、`ShowMergeSavePreviewAsync`→预览后提交、`ExportXmlAsync`→`ExportModAsync`；`SaveToDatabaseAsync` 删，View 不再写 GameDbContext；+4 测试 + 1 架构测试） |

**顺序理由**：B1→B2 是自底向上（repository 先有，HostService 才有得委托）；B3（per-profile session）是横切，先做让 B4/B5 的 View 改动直接落在新 session 语义上；B4（单 Mod 视图去除）先于 B5（View 收敛），避免在旧双模式上做两遍 View 简化。B5 最后（收口，牵动最多 View 代码）。

### 影响范围

| 层 | 文件 | 改动 |
|----|------|------|
| Core | `IHostService.cs` | 新增 Export/GetDiff 补齐 + `SaveResult/ExportResult/PublishResult` + `PreExportHook` 注册 |
| Core | `IWorkspaceSession` | 按 profile 作用域（工厂/注册表） |
| Core | `ProfileInfo.cs` | 新增 `IncludeGame` |
| Core | `IEntityRepository<T>` | **对称契约**：CRUD + 行级/字段级 diff + dirty + `SaveAsync` + `LoadAsync`（R26 v2） |
| Core | `IDataRepository<T>` | 收敛为只读（`GetByIdAsync`/`GetAllAsync`） |
| Core | 新增 `RowDiff` | 行级/文件级 diff 记录（`TargetId` + `DiffKind` + `Old/NewContent`） |
| Infra | `RepositoryBase<T>` | 补 `UpsertAsync`（DbRepository.Persist）；保留 `GetDiffAsync`（字段级，DiffEngine） |
| Infra | 新增 `XmlRepository<T>` | `Persist`（实体→XML 写盘）+ `GetDiff`（XML 级）+ `Load`（XML→实体，复用 XmlParser） |
| Infra | `HostService.cs` | 实现三动作 + 委托 repository + 激活 hook + 事务 |
| Infra | `ModManager.cs` | 迁入 HostService（或 Infra） |
| App | `ModGameDataTabsView*.cs` | 收敛为 HostService 调用，删除直接 DB 写入 |
| App | `ProfileManager.cs` | `IncludeGame` 支持 |

---

## 三、Phase 9C：图片资产修正（议题 1, 7）✅ 已完成

> **完成（2026-08-01）**：议题 1 + 议题 7 + 议题 6（并入）全部落地，spec R27 落地。
> - **Image Browser**（原 ImageAssetManager 收敛）：纯文件系统扫描（Base Game `img/` + 各 mod `img/`），不再解析 getimages.php；@2x 配对、搜索、预览、双击打开保留；构造即自动加载 + 订阅 workspace 生命周期消息。
> - **Image Orchestration**（新增）：读取 `<gameRoot>/getimages.php` + 各 mod 的 getimages.php，声明顺序展示 normal→x2 对；R27 三路路径解析（contentRoot/name → contentRoot/img/name → gameRoot/img/name）做 ✓/✗ 文件存在性校验；MoveUp/Down 调整顺序、Add Pair（导入文件到 img/）、Delete、Save（GenerateImagePhp 写回）；**Base Game 只读**。
> - **刷新串行化**：两 VM 均用链式队列（`_refreshChain`）串行化并发刷新，避免构造自动加载与消息触发互相 clobber。
> - 新增测试 +10：Orchestration VM 7 + Browser VM 3（声明顺序/存在性/三路解析/Save 写回/只读/重排/删除/消息自动加载）。**384/384 测试通过**。

### 3.1 议题 1 — 目录结构修正

**变更**：

1. **Base Game getimages.php**：`BuildTree()` 中 base game 节点改为先读 `<gameRoot>/getimages.php`（如存在），否则回退到扫描 `img/` 目录
2. **路径解析**：`ResolveImagePath` 对 mod 图片依次尝试 `modFolder/<name>`、`modFolder/img/<name>`、`<gameRoot>/img/<name>`（mod 可引用 game 图片）
3. **排序**：所有节点的排序顺序 = `getimages.php` 中 `strImageURL` 的声明顺序。不额外按文件名排序

### 3.2 议题 7 — 拆分为 Browser + Orchestration

| 面板 | 功能 | 数据源 |
|------|------|--------|
| **Image Browser** | 按 mod 分组浏览实际图片文件、预览、双击打开编辑 | 文件系统扫描（`img/` 目录） |
| **Image Orchestration** | 展示/编辑 getimages.php 编排（每对 normal→x2、声明顺序、文件存在性校验） | `getimages.php` 解析 |

**Image Browser**：
- 放在现有 `ImageAssetManager` 的 Tool Dock 位置
- 树节点：Mod → 图片对（normal + x2），显示缩略图预览
- 支持搜索、双击打开编辑

**Image Orchestration**：
- 放在新的 Tool Dock（与 Browser 并列或替换）
- 列表展示每对图片的声明顺序（可拖拽排序）、文件是否实际存在
- 编辑后写回 `getimages.php`（通过 `PhpParser.GenerateImagePhp`）
- 与 Browser 联动：Browser 中的图片可拖入编排

**影响范围**：

| 文件 | 改动 |
|------|------|
| `ImageAssetManagerViewModel.cs` | 重构为 Browser 模式，只扫描文件系统 |
| `ImageAssetManagerView.axaml` | 调整 UI |
| 新增 `ImageOrchestrationViewModel.cs` | getimages.php 编排视图 |
| 新增 `ImageOrchestrationView.axaml` | 编排视图 |
| `PhpParser.cs` | 可能增强（验证文件存在性） |
| `DocumentWorkspaceView.axaml` | 新增 Orchestration Tool Dock |
| `Documents.cs` | 新增 `ImageOrchestrationTool` |

### 3.3 议题 6（并入）— Browser 自动加载

> **v1.4 修订**：议题 6 原属 9A。因议题 7 重构 `ImageAssetManagerViewModel`，自动加载逻辑
> 直接在拆分后的 **Image Browser** ViewModel 上实现，避免 9A 先改、9C 覆盖的返工。

**变更**（作用于拆分后的 `ImageBrowserViewModel`）：
1. 构造函数内 `FireAndForget(RefreshAsync())`
2. 注入 `IMessenger`，订阅：
   - `GameFolderChangedMessage` → Refresh
   - `ProfileLoadedMessage` → Refresh
   - 新增/删除 mod 的消息 → Refresh

---

## 四、Phase 9D：AI/MCP UI 接入（议题 4）

### 4.1 AI Chat 接入 Dock

**变更**：
1. `Documents.cs`：新增 `AiChatTool : Tool { Id="AiChat", Title="AI Chat" }`
2. `DocumentWorkspaceView.axaml`：在 RightToolPane 新增 `<Tool Id="AiChat">` 并实例化 `AiChatView`
3. `DocumentWorkspaceViewModel`：构造时 `AiChatTool.Context = provider.GetRequiredService<AiChatViewModel>()`

> 这是临时接入方式。Phase 9E（Plan A）完成后，改由 `IToolPlugin` 动态提供。

### 4.2 MCP Server 启动

**变更**：
1. `Program.cs`：解析 `args`，若含 `--mcp`：
   - 初始化 DI（`IHostService` + `McpServerHost`）
   - `await mcpServerHost.RunAsync()`（stdio transport）
   - 不启动 Avalonia GUI
2. `McpServerHost`：确保 `RunAsync` 正确处理启动和 graceful shutdown
3. 预留 TCP transport 支持（`--mcp --mcp-port 5000`），供 GUI 内启动

### 4.3 AI/MCP 配置界面

**变更**：
1. `AppConfig` 新增字段（**实现按 Provider 列表落地，见顶部 v2.0 进度**）：
   ```csharp
   List<AiProviderConfig> AiProviders { get; set; }  // Id / Name / Endpoint / ApiKey
   string AiModelProviderId { get; set; }           // 对话模型 Provider（空 = 第一个）
   string AiEmbeddingProviderId { get; set; }       // RAG 嵌入 Provider（空 = 第一个）
   string ImageProviderId { get; set; }             // 图片生成 Provider（空 = 第一个）
   string AiModel { get; set; }                     // 默认 gpt-4o
   string AiEmbeddingModel { get; set; }            // 默认 text-embedding-3-small
   string ImageModel { get; set; }                  // 默认 dall-e-3
   bool McpEnabled { get; set; }                    // 默认 false
   int McpPort { get; set; }                        // 默认 0（stdio）
   ```
2. `ServiceCollectionExtensions`：改为从 `IConfigService` 读取配置（优先 config，fallback 环境变量）
3. `SettingsPage`：新增 "AI & MCP" 分组，含：
   - Endpoint URL 文本框
   - API Key 密码框
   - Model 选择
   - MCP 开关 + 端口
4. ApiKey 写入 `config.json` 时做简单加密（`ProtectedData.Protect`）

### 影响范围

| 文件 | 改动 |
|------|------|
| `Documents.cs` | 新增 `AiChatTool` |
| `DocumentWorkspaceView.axaml` | 新增 AiChat Tool |
| `DocumentWorkspaceViewModel.cs` | 注入 AiChatViewModel |
| `Program.cs` | 解析 `--mcp` |
| `AppConfig.cs` | 新增 AI/MCP 字段 |
| `ServiceCollectionExtensions.cs`（AiChat + ImageTools） | 从 ConfigService 读配置 |
| `SettingsPageView.axaml` | 新增 AI/MCP 分组 |
| `SettingsPageViewModel.cs` | 新增 AI/MCP 属性 |

---

## 五、Phase 9E：工具栏 / Dock 重整 — 方案 A（议题 2）✅ 已定稿 + 已完成

> **全部完成（2026-08-01，v3.0）**：顶部工具栏精简 + Profile Tool（左 Dock）+ **IToolPlugin 动态构建机制**。
> 落地情况见顶部「开发进度 v3.0 + v3.1」。侧边栏精简（删 Mods/Profiles → Workspace 工作区历史按钮）✅ 已于 2026-08-01 遗留清理完成。
> 决策固化：spec [D02](../spec/D02-dynamic-dock-layout.md)（v1.1 已按实现订正映射）。

### 5.0 已定稿：工具栏布局设计（2026-08-01）

**顶部工具栏** → 仅剩 `💾 Save`：

| 原按钮 | 处置 |
|--------|------|
| ToggleLeft/Right/Bottom 面板切换 ×3 | ❌ 删除（无效果） |
| +Entity / Copy / -Entity | ➜ 移入 DataTable 工具栏，合并为 `[Add] [Copy] [Delete]` |
| New Mod / Import Mod | ➜ 移入 Profile Tool 的 Mod 管理区 |
| 💾 Save | ✅ 保留 |

**实体操作**（DataTable 工具栏）：
- `[Add] [Copy] [Delete]` —— Add/Copy 新增一行（Copy 为克隆），字段细节编辑在打开的 **Document**（XML 编辑器 / Value Editor）内完成

**左侧 Dock**（4 个 Tool）：
```
KeyValueEditor │ OverlayChain │ Profile Tool ★ │ ImageAssetManager(从右侧移入)
```

**★ Profile Tool（新，左 Dock）**：

| 区 | 内容 |
|----|------|
| Mod 管理 | New Mod / Import Mod + mod 列表 + XML 文件树 + zip 导入导出 + 删除 |
| Mod 编排 | getmods.php 结构查看（加载顺序 + 命名空间）→ **查看器 + 入口**：<br>· 「Edit Profile」→ 打开 EditProfileView **Document**（编排主体，留中心）<br>· 「Reload Merge View」→ 重载合并数据视图（重操作，编排变更后手动触发） |

**中心 Document**：EditProfileView / MergeEditorDocument 保留（不迁移）。

**侧边栏** → 移除 Mods / Profiles 按钮，改为 **Workspace** 按钮（工作区历史：逆序 profile + dirty，点击打开合并视图）；侧边栏 = **Home / Explorer / Workspace / Settings**（2026-08-01 遗留清理落地）。

### 目标

激活 `IToolPlugin` 机制，让 Plugin 自描述 UI 位置，App Shell 动态构建 Dock 布局 + 工具栏。

### 5.1 IToolPlugin 契约（见 D02 四 §）

**粒度 1:1**：一个 IToolPlugin = 一个 Tool 组件 + 围绕它的功能（Toolbar 按钮、关联 Document、命令）。
接口 `CreateToolView()` 已存在，仅新增 `CreateToolbarItems()`：

```csharp
public interface IToolPlugin : IPlugin
{
    string Title { get; }
    ToolDock DefaultDock { get; }    // Left | Right | Bottom
    int Order { get; }
    object CreateToolView();         // Tool 面板本体（object 避免 Core 依赖 Avalonia）

    // 新增：面板内 Toolbar 按钮贡献（可选，返回 null 不贡献）
    IReadOnlyList<ToolbarItem>? CreateToolbarItems() => null;
}

public record ToolbarItem
{
    public string Id { get; init; }
    public string Label { get; init; }
    public string? IconSymbol { get; init; }
    public IRelayCommand Command { get; init; }
    public string? Group { get; init; }     // 分组（Navigation / Edit / View / Persistence）
    public int Order { get; init; }
}
```

### 5.2 动态 Dock 构建

**现有**（手写 XAML）：
```xml
<ToolDock Id="RightToolPane">
    <Tool Id="Peek">...</Tool>
    <Tool Id="ImageAssetManager">...</Tool>
</ToolDock>
```

**改为**（`DocumentWorkspaceViewModel` 中动态构建）：
```csharp
// 枚举所有 IToolPlugin
var plugins = _serviceProvider.GetRequiredService<IEnumerable<IToolPlugin>>();
foreach (var plugin in plugins.OrderBy(p => p.Order))
{
    var tool = new PluginTool(plugin);  // Tool 子类，包装 IToolPlugin
    tool.Context = plugin.CreateToolView();
    switch (plugin.DefaultDock)
    {
        case ToolDock.Left: leftTools.Add(tool); break;
        case ToolDock.Right: rightTools.Add(tool); break;
        case ToolDock.Bottom: bottomTools.Add(tool); break;
    }
}
```

`PluginTool` 是一个新的 `Tool` 子类（在 `Documents.cs`），`Id` = `plugin.GetType().Name`，`Title` = `plugin.Title`。

**Dock 布局序列化**：Dock.Avalonia 通过 Tool.Id 做布局持久化。Plugin Id 以类型名生成（`"DataTablePlugin"`, `"AiChatPlugin"` 等），确保重启后布局恢复。

### 5.2.1 Tool → Plugin 拆分（核心工作）

**DataViewer 程序集拆分为 7 个 IToolPlugin 类（计划；落地 5 个，见下注）**：

| Tool | IToolPlugin 类 | Dock |
|------|---------------|------|
| DataTable | `DataTablePlugin` | Bottom |
| Ref Index | `ForwardIndexPlugin` | Bottom |
| Reverse Index | `ReverseIndexPlugin` | Bottom |
| Search | `SearchPlugin` | Bottom |
| Conflicts | `ConflictsPlugin` | Bottom |
| Validation | `ValidationPlugin` | Bottom |
| Peek | `PeekPlugin` | Right |

**其他程序集**：

| Tool | IToolPlugin 类 | Dock | 归属 |
|------|---------------|------|------|
| KeyValueEditor | `KeyValueEditorPlugin` | Left | EntityEditor |
| OverlayChain | `OverlayChainPlugin` | Left | EntityEditor |
| ImageAssetManager | `ImageAssetManagerPlugin` | Left（从 Right 移入） | ImageTools |
| Profile Tool | `ProfileToolPlugin` | Left | App（新） |
| AI Chat | `AiChatPlugin` | Right | AiChat（已有） |

**Document Plugin**：`EntityEditorDocumentPlugin`（实体编辑）、`ModImagesDocumentPlugin`、`ProfileDocumentPlugin`（EditProfileView）。

> **落地修订（v3.0，2026-08-01）**：实际拆分见 spec [D02 v1.1](../spec/D02-dynamic-dock-layout.md)——DataViewer 实为 **5** 个 IToolPlugin 类（Conflicts/Validation 已于 9B 删除）；ImageTools 另增 `ImageOrchestrationPlugin`(Right)；Document 侧仅实体编辑走 `IDocumentPlugin`（类名沿用 `EntityEditorPlugin`），ModImages / Profile 编排 Document 仍由 App shell 消息处理。

### 5.3 工具栏统一

**现状三套工具栏** → **顶部命令栏精简 + DataTable 工具栏分组重构**：

| 工具栏 | 位置 | 内容来源 |
|--------|------|---------|
| **顶部命令栏** | MainWindow 顶部 | 仅剩 `💾 Save`（New/Import Mod 移入 Profile Tool，实体操作移入 DataTable，面板切换删除）|
| **DataTable 工具栏** | ModGameDataTabsView 内部 | 导航组 + 实体操作组 + 视图组 + 持久化组 |

**DataTable 工具栏分组重构**：

| 分组 | 按钮 | 来源 |
|------|------|------|
| 导航 | Undo, Redo, Back, Locate | DataViewer Plugin |
| 实体操作 | **Add / Copy / Delete**（Add/Copy 新增行，编辑在 Document；合并原顶部栏 +Entity/Copy/-Entity 与行操作） | EntityEditor Plugin |
| 视图 | ColumnChooser, ModFilter, ShowAll, Filter | DataViewer Plugin |
| 持久化 | Quick Save, Save & Export, Save & Launch | App Shell（通过 HostService） |

分组之间用 `Separator` 分隔，视觉上清晰。

### 5.4 向后兼容

- 现有 4 个 `IToolPlugin`（DataViewer/EntityEditor/ImageTools/AiChat）中，**AiChatPlugin 已真实可用**；其余按映射表拆分/补全，修掉 `CreateToolView()` 返回 null 的 stub
- `ToolbarItem` 可选（返回 null 不贡献）
- 过渡期：先实现动态 Dock 构建（App 枚举 + 分组），工具栏贡献后续迭代

### 影响范围

| 文件 | 改动 |
|------|------|
| `Core/Abstractions/IToolPlugin.cs` | 新增 `CreateToolbarItems()` |
| `Core/Abstractions/ToolbarItem.cs` | 新增 record |
| `Documents.cs` | 新增 `PluginTool`、删除手写 Tool 类（KeyValueEditorTool、DataTableTool 等 11 个） |
| `DocumentWorkspaceView.axaml` | 删除手写 `<Tool>` 元素，保留 Dock 容器结构 |
| `DocumentWorkspaceViewModel.cs` | 枚举 IToolPlugin，动态构建 Dock |
| `NeoEditor.Plugins.DataViewer` | 拆分为 IToolPlugin 类（计划 7；落地 5，Conflicts/Validation 已于 9B 删除）+ Peek/DataTable 视图补全 |
| `NeoEditor.Plugins.EntityEditor` | `KeyValueEditorPlugin` + `OverlayChainPlugin` + Document Plugin |
| `NeoEditor.Plugins.ImageTools` | `ImageAssetManagerPlugin` 移左 Dock + Document Plugin |
| `NeoEditor.App` | 新增 `ProfileToolPlugin` + `ProfileDocumentPlugin` |
| `ModGameDataTabsView.axaml` | 工具栏分组重构 |

---

## 六、测试策略

| 阶段 | 新增测试 | 现有测试 |
|------|---------|---------|
| 9A | 无（删除代码为主） | 全量回归 |
| 9B | HostService Save/Export/Publish 单元测试（Infra.Tests ~15 新增）+ XmlRepository 测试（~8）+ per-profile dirty session 测试 | 现有 344 回归 |
| 9C | ImageOrchestration VM 测试（ImageTools.Tests ~8 新增）+ Browser 自动加载/消息订阅测试（~3，来自议题 6） | 现有 344 回归 |
| 9D | --mcp 启动集成测试、ConfigService AI 字段测试 | 现有 344 回归 |
| 9E | PluginTool 构建测试、ToolbarItem 序列化测试（Core.Tests ~10 新增） | 现有 344 回归 |

**目标**：完成后 ~393 测试全过。

---

## 七、阶段顺序与依赖

> **v1.4 修订**：9A 议题 6 并入 9C（同一文件 `ImageAssetManagerViewModel` 会被 9C 重构覆盖）。
> 9B 与 9C 可并行（图片不依赖保存/导出）。

```
Phase 9A (Bug Fix: 仅议题5 放大镜) ── 独立，极小，可立即开工 ──┐
    ↓                                                         │
Phase 9B (HostService/R26, 基石) ──并行── Phase 9C (Image Assets + 议题6) ──┐
    ↓                                     ↓                                  │
Phase 9D (AI/MCP UI, 依赖 9B) ──────────┘                                  │
    ↓                                                                        │
Phase 9E (Toolbar Plan A) ←─────────────────────────────────────────────────┘
    (9E 依赖 9B 的 HostService 保存路径 / PublishAsync，依赖 9C 的 ImageOrchestration Tool，
     依赖 9D 的 AiChatTool)
```

- **9A**（仅议题 5）独立、无依赖，可立即开工。
- **9B / 9C 可并行**；**9B 是 9D / 9E 的基石**（9D 的 MCP 工具走 HostService，9E 的 Save 走 `PublishAsync`）。
- **9E 最后做**（等 9B HostService 三动作 + 9C ImageOrchestration Tool + 9D AiChatTool 全部就绪）。
- 9C 内部先做议题 1（目录结构）再做议题 7+6（拆分 Browser + 自动加载），议题 6 随 Browser 一并实现。

---

## 八、决策记录

已固化为 spec 的决策：

| 决策文件 | 内容 |
|----------|------|
| [D02](../spec/D02-dynamic-dock-layout.md) | 动态 Dock 布局 — Tool/Document/Service Plugin 分类 + IToolPlugin 动态构建（1:1），DataViewer 拆 5 plugin（v1.1 订正） |
| [R26](../spec/R26-save-export-repository.md) | 保存/导出工作流 — DB/XML 双 Repository + Save/Export/Publish 三动作 + partial diff 返回值 + per-profile dirty session |
| [R27](../spec/R27-image-asset-dual-view.md) | ImageAssetManager 拆分为 Browser + Orchestration |
| [R28](../spec/R28-ai-mcp-configuration.md) | AI/MCP 必须有 UI 配置界面和启动路径 |

**全部 7 议题已定稿**，无待讨论项。议题 3（保存/导出）定稿为 [R26](../spec/R26-save-export-repository.md)。
