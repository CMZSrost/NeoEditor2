# AGENT.md — NeoEditor 项目说明

> 项目初始化入口。新会话先读本文件，再按需深入 `spec/` 与 `NeoEditor.App/Docs/`。

## 这是什么

NeoEditor 是给独立游戏 **NeoScavenger** 做的 **Mod 编辑器**。NeoScavenger 是 Flash/ActionScript
游戏，全部游戏数据以 **XML 文件**保存，运行时由 ActionScript 加载。本编辑器让 modder 不必手写
XML，就能查看、编辑、校验并导出游戏数据与图片资源。

## 游戏数据模型（编辑器要理解的领域）

游戏根目录（开发机示例）：`D:\software\Steam\steamapps\common\Neo Scavenger`

| 路径 | 含义 |
|------|------|
| `data\*.xml` | 游戏主数据，每个 XML 文件对应一个数据类（ItemType / Recipe / Creature 等） |
| `Mods\` | Mod 目录。一个 Mod 要么用单个 `neogame.xml` 放全部数据，要么每个数据类一个 XML 文件 |
| `getmods.php` | Mod 加载配置：记录加载**顺序**、相对**路径**、**命名空间**（`strModName`）。一个路径下所有 XML 加载到该命名空间 |
| `img\` + `getimages.php` | 图片资源与加载配置 |

**命名空间（关键概念）**：
- 默认命名空间是 `0`，游戏本身数据就在 `0`。
- Mod 把数据加载进自己的命名空间；若把 `strModName` 设为 `0`，相当于**覆盖游戏原始数据**。
- 加载顺序决定覆盖优先级——这是编辑器「覆盖链 / OverlayChain」「合并视图」的来源。

**图片加载规则**：`getimages.php` 顺序固定——先加载正常 png，再加载 x2 版本，交替进行。
Mod 内也有 `getimages.php`，路径相对 Mod 目录。

## 参考资料（游戏侧，只读）

游戏根目录下，编辑器开发/校验数值逻辑时参考：

| 文件 | 内容 |
|------|------|
| `游戏XML文本各项说明修正增强版.docx` | 字段含义说明（已整理进 [Docs/20-data-class-field-reference.md](NeoEditor.App/Docs/20-data-class-field-reference.md)） |
| `NeoScavenger 模组制作指南中文翻译精修1.2（新）.docx` | Mod 制作指南 |
| `NEO伤害以及破甲公式.txt` | 伤害 / 破甲计算公式 |
| `NEO命中率计算公式.txt` | 命中率计算公式 |
| `NEO全代码.注释与基础修改思路.xml` | 全代码注释 + 基础修改思路（~52KB，最全的逻辑参考） |

> 涉及战斗数值可视化（AttackMode / BattleMove / Condition 等 visualizer）或公式校验时，
> 查上面三个公式 / 代码文件。

## 技术栈

- **.NET 10** / C# (`LangVersion=preview`, `Nullable=enable`)，SDK 见 `global.json`
- **Avalonia 12.1** 桌面 UI（`WinExe`），编译绑定默认开启
- **Dock.Avalonia 12.1** 停靠布局；**Fluent** 主题
- **CommunityToolkit.Mvvm** MVVM + `IMessenger` 消息
- **EF Core 10 + SQLite** 数据持久化（实体存 DB，导出为 XML）
- **AvaloniaEdit** XML 编辑器；**DiffPlex / XMLDiffPatch** 差异比较
- **SixLabors.ImageSharp** 图片处理；**Serilog** 日志
- **ModelContextProtocol** MCP 官方 C# SDK v2.0；**Microsoft.Extensions.AI** + **OpenAI** AI 抽象与模型接入
- **ProDataGrid 12.0.4**（wieslawsoltes 高性能 fork，替换 `Avalonia.Controls.DataGrid`）— Fluent 主题
- **LiveMarkdown.Avalonia 2.2.2** + **FluentIcons.Avalonia 2.1.333** + **Avalonia.AvaloniaEdit 12.0.0** + **Xaml.Behaviors 12.0.5**
- **Anthropic** → 已移除，改用 OpenAI 兼容 API（Provider 列表配置于 Settings，环境变量 `OPENAI_API_KEY` / `OPENAI_ENDPOINT` / `OPENAI_MODEL` 作 fallback）
- 测试：**xUnit**（`Tests/` 下 11 个测试项目，覆盖全部模块 + 集成测试）

## 项目结构

M8 完成，M9 DataViewer Plugin 迁移完成，**M10 EntityEditor Plugin 全部完成**，**M11 ImageTools Plugin 全部完成**，**M12 收尾完成**（详见 [Docs/28](NeoEditor.App/Docs/28-plugin-architecture-migration.md)）。
**M13+ 领域驱动服务架构**——Phase 1-8 全部完成，Agent 编排 A1-A4 全部完成，像素图像 G1-G3 全部完成，ProDataGrid 迁移完成。详见 [Docs/30-post-m12-development-plan.md](NeoEditor.App/Docs/30-post-m12-development-plan.md)。

**当前 (M13+ Phase 1-8 + A1-A4 + G1-G3 + ProDataGrid + Phase 9A-9E + 遗留清理全部完成，11 src + 11 test = 22 项目)**:
- ✅ **Phase 1 (HostService) 完成**：IHostService 统一写路径，IEditorCommand 提升到 Core，Scope 隔离保留 per-tab undo
- ✅ **Phase 2 (引用类型系统) 完成**：IReferenceEntry + IReferenceFormat + EntityRef + 7 Format 类（PureRef/NegatedRef/IdXMult/MultXId/Assign/Bracket/MultiIngredientRecipe）+ ReferenceList<T> + IReferenceListSerializer + EF Core ValueConverter，15 实体 ~48 引用属性从 string → ReferenceList<IReferenceEntry>
- ✅ **Phase 3 (KV 引用弹窗) 完成**：ReferencePickerViewModel + ReferencePickerDialog（搜索/单选多选/装饰编辑）+ ReferenceFieldEditor（内联徽章控件）替代纯 TextBox；ControlTypeVisibilityConverter 拆分 refpicker；17 个新测试
- ✅ **Phase 4 (删 DataBrowser) 完成**：移除 DataBrowser ViewModel/View/Sidebar/DI 注册 + 废弃 DomainGroup record
- ✅ **Phase 5 (ImageAssetManager) 完成**：新增 Tool Dock "Image Assets"（TreeView 按 Mod 分组 + 搜索过滤 + 预览面板 + 双击打开编辑 + Refresh），ViewModel + View 在 ImageTools Plugin 内
- ✅ **Phase 6 (Plugin 分类) 完成**：PluginKind 枚举 + [PluginKind] Attribute + IServicePlugin + IExtensionPoint<TContext> + 3 Context record（PreSave/PostLoad/PreExecute）+ IHostService 扩展点注册方法 + HostService hook 存储 + 6 架构测试，3 个现有 Plugin 加 [PluginKind(Workbench)]
- ✅ **Phase 7 (CLI + MCP + AI Chat) 代码 + 测试完成**：新增 3 个 Plugin 项目（Mcp / Cli / AiChat）+ 3 Core 接口（IMcpToolProvider / IMcpResourceProvider / McpToolInfo）+ 3 个测试项目（Mcp 21 / Cli 40 / AiChat 23 = 84 测试）。MCP 用 ModelContextProtocol 官方 C# SDK v2.0；CLI 手写轻量命令行；AI Chat 用 Microsoft.Extensions.AI + OpenAI 兼容 API
- ✅ **Agent 编排增强 Phase A1-A4 全部完成**：A1 系统提示词（SystemPromptBuilder — 25 实体 Schema 自动注入 + UI 可编辑面板）；A2 RAG 检索增强（RagService — OpenAI 兼容 Embedding + EntitySummaryBuilder + 自动上下文注入）；A3 MCP 工具增强（3 新工具 GetEntitySchema / SearchAllTypes / GetModInfo + 描述优化 + 结果截断，共 12 工具）；A4 Streaming 响应（CompleteChatStreamingAsync + 逐 token typewriter 效果 + 工具调用状态指示 + IsThinking 指示器）。详见 [32](NeoEditor.App/Docs/32-agent-orchestration-plan.md)
- ✅ **Command 去 UI 化 Phase 8 完成**：AddEntity/DeleteEntity 不再耦合 ObservableCollection；IHostService 新增实体缓存 + 集合注册；MCP "mcp" scope 支持 undo/redo；CommandSerializer 签名简化。详见 [30 §六.5](NeoEditor.App/Docs/30-post-m12-development-plan.md)
- ✅ **ProDataGrid 迁移 D1-D4 完成**：`Avalonia.Controls.DataGrid 11.3.12` → `ProDataGrid 11.3.11` + `Semi.Avalonia.ProDataGrid 11.3.9-beta.1`；SwitchTabItemsSource 简化（-55行 AutoGenerateColumns hack）；OnSorting 简化（-12行 Dispatcher hack）；296/296 测试全过。详见 [31](NeoEditor.App/Docs/31-prodatagrid-migration-plan.md)
- ✅ **ModInfo Schema 修复 (2026-07-31)**：分离 `Id`（DB 自增 PK）和 `ModId`（Profile 编排业务字段，`-1`=Game / `>=0`=Mod）。修复 `[DatabaseGenerated(Identity)]` 与 `ModId=-1` 约定冲突导致的 UNIQUE constraint 崩溃。XmlParser 注入 `IReferenceListSerializer` 修复 ReferenceList 类型 XML 解析失败。Import 不再自动打开 ModGameData。SwitchTabItemsSource 先置 null 再设 ItemsSource 避免 ProDataGrid Sort NRE。
- ✅ **排序闪退 + 虚拟列排序 + 游戏数据加载修复 (2026-07-31)**：OnSorting 恢复延迟替换（DispatcherPriority.Background）避免 ProcessSort NRE 回归；GetSortKeySelector 支持 →Id/Mod 虚拟列字典排序；ImportModAsync 新增 modId 参数，游戏数据正确导入为 ModId=-1，修复覆盖链失效。新增 [34](NeoEditor.App/Docs/34-prodatagrid-column-filter-plan.md) 列过滤器计划（✅ 已完成 2026-07-31）。
- ✅ **Avalonia 12 升级 + Semi/Ursa 移除 (2026-07-31)**：Avalonia 11.3.12 → 12.1.1，ProDataGrid 11.3.11 → 12.0.4，Dock.Avalonia 11.3.11.16 → 12.1.0，移除 Semi.Avalonia（5 包）+ Irihi.Ursa（2 包）+ Avalonia.Diagnostics，Ursa ImageViewer → Avalonia Image（Uniform Stretch），修复 Avalonia 12 breaking changes（DragEventArgs.Data → DataTransfer、OpenFileDialog → IStorageProvider、Clipboard API）。314/314 测试通过。ProDataGrid #820 过滤按钮 bug 随 Semi 移除自动修复。
- ✅ **ProDataGrid 列过滤器 F4 (2026-08-01)**：FilterContexts.cs（4 个 IFilter*Context 实现 + IEnumOption）+ FilterFlyoutFactory.cs（TypeFilterFlyout 自建 UI：文本操作符下拉+输入、数值 Min-Max、枚举多选 CheckBox 列表）。SearchableDataGrid.OnAutoGeneratingColumn 改用工厂。TabStrip WrapPanel → StackPanel + ScrollViewer 垂直滚动（+ TargetType）。344/344 测试通过（+30 新测试）。
- ✅ **Doc 35 P2 内置 Filter 模板 + Column Chooser (2026-08-01)**：FilterFlyoutFactory 删除自建 TypeFilterFlyout（~310行），改用 ProDataGrid 内置 DataGridFilter{Text|Number|Enum}EditorTemplate + DataGridFilterFlyoutPresenterTheme；FilterContexts 接线至生产代码。手写列管理器 ContextMenu → 内置 DataGridColumnChooser（DropDownButton + CheckBox 列表），ColumnHeaderTextConverter 解决 visual parenting 冲突。MergedId 列 CanUserHide=false。Settings ColumnOption 双向同步 ColumnVisibilityChangedMessage。344/344 测试通过。详见 [35 P2](NeoEditor.App/Docs/35-tabstrip-listbox-filter-templates-plan.md)。
- ✅ **Phase 9 计划全部定稿 (2026-08-01)**：7 议题全部定稿。详见 [36](NeoEditor.App/Docs/36-phase9-plan.md)。9A Bug 修复（放大镜/Loading）→ 9B HostService Save/Export（✅ 已定稿：DB/XML 双 Repository + Save/Export/Publish 三动作，见 R26）→ 9C Image Assets 修正 → 9D AI/MCP UI → 9E 工具栏/Dock 重整（顶部栏仅剩 Save、实体操作并入 DataTable Add/Copy/Delete、Profile Tool 左 Dock、侧边栏页面化、IToolPlugin 动态构建 + DataViewer 拆 7 plugin，见 D02）。新增 spec D02, R26, R27-R28 全部固化。
- ✅ **Phase 9 开发进度 (2026-08-01)**：9A Bug 修复（放大镜）✅ + **9B B1 双 Repository ✅** + **9B B2 HostService 三动作 ✅** + **9B B3 per-profile dirty session ✅** + **9B B4 IncludeGame/单 Mod 去除 ✅** + **9B B5 ModManager 并入/删 Validation/View 收敛 ✅**。`IXmlParser` 接口上移 Core/Abstractions（具体类留 App，别名 using 避免 `IWorkspaceSession` 命名空间歧义）；新增 `IEntityRepository<T>` + `XmlFileDiff` + `DbRepository` + `XmlRepository`（Infra/Data/Repository）；`IHostService` 新增 `SaveResult/ExportResult/PublishResult` + `ExportModAsync/ExportProfileAsync/PublishAsync` + `PreExportHook`（PreSaveHook 激活）。B3：dirty 集合按 profile 存于 `WorkspaceSession`（stores/indexes 保持全局），`DirtyEntities` 作用域 = 当前 profile（`SetActiveProfile`），Undo/Redo 补脏标记。**B4**：`ProfileInfo.IncludeGame`(DB 列) + `SingleModId`；单 Mod 打开 = 持久化单 Mod profile（`DocumentWorkspaceViewModel.EnsureSingleModProfileAsync`，strModName="0" 保留业务主键）；`ModGameDataTabsView` 收敛为 profile 形态（`ModInfoProperty`/`ReloadTabsAsync`/`ShowSavePreviewAsync` 删，`IsMergeView` 恒 true）；`ReloadMergeTabsAsync` 尊重 `IncludeGame`。**B5**：`ModManager`+`IModManager` 迁 Infra，`HostService` 实现 `IModManager`（`IModManager`→HostService DI）；删 `RunPreSaveValidationAsync` + Validation/Conflicts 工具（7 文件）+ 2 消息；View 收敛：`QuickSaveAsync`→`SaveAllAsync`、`ShowMergeSavePreviewAsync`→`BuildExportPreviewAsync`+`SaveAllAsync`、`ExportXmlAsync`→`ExportModAsync`，`SaveToDatabaseAsync` 删（View 不再写 GameDbContext）。374/374 测试。**R26 v2 对称 Repository 契约重构 ✅（2026-08-01）**：`IEntityRepository<T>` 全对称（CRUD 4 函数 + 行级/字段级 diff 2 函数 + dirty + `SaveAsync` + `LoadAsync`），DB/XML 各实现一份、无 NotSupported/空返回特判；CRUD 经 HostService command（新增 `ReplaceEntityCommand`，缓存改由 `IEditorCommand.GetCacheDelta/GetUndoCacheDelta` 通用 delta 驱动，删除 `is Add/Delete` 类型特判）；`PreExecuteHook` 修复空挂（Execute/ExecuteBatch 均触发）；XmlRepository 构造绑定 modId；`RowDiff` 替代 `XmlFileDiff`；`IDataRepository` 收敛只读。**Phase 9B 收官，下一步 9C/9D/9E**。
- ✅ **Phase 9C 图片资产修正 (2026-08-01)**：议题1+6+7 全部完成，spec R27 落地。**Image Browser**（原 ImageAssetManager 收敛）：纯文件系统扫描（Base Game `img/` + 各 mod `img/`），不再解析 getimages.php，@2x 配对/搜索/预览/双击保留，Tool 标题 "Image Browser"。**Image Orchestration**（新增 `ImageOrchestrationViewModel/View` + `ImageOrchestrationTool`，Right Dock）：读取 gameRoot + 各 mod 的 getimages.php，声明顺序展示 normal→x2 对 + R27 三路路径解析（contentRoot/name → contentRoot/img/name → gameRoot/img/name）✓/✗ 存在性校验 + MoveUp/Down/Add/Delete/Save（GenerateImagePhp 写回），**Base Game 只读**。议题6 自动加载：两 VM 构造即 Refresh + 订阅 `GameRootDirChangedMessage`/`LoadProfileMessage`/`RefreshModMessage`；刷新用 `_refreshChain` 链式串行化防并发 clobber。384/384 测试（+10：Orchestration 7 + Browser 3）。**下一步 9D/9E**。
- ✅ **Phase 9D AI/MCP UI (2026-08-01)**：全部完成，含 **`--mcp` NRE 修复**。**9D-1** `AiChatTool` + DocumentWorkspaceVM + RightToolPane `<Tool Id="AiChat">` 接入 Dock ✅。**9D-2** `App.CreateHost(bool mcpMode)` 抽组合根（GUI 与 --mcp 共用）+ `App.EnsureDatabases()` 公共化 + `Program.cs` 解析 `--mcp`/`--mcp-port`（不启 GUI）；MCP 模式禁用 stdout 日志（`AddSerilogLogging(logToConsole:)` + DB `.LogTo(Console.WriteLine)` 条件化，协议通道纯净）；`McpServerHost.RunAsync(int? port)` stdio + TCP（StreamServerTransport 预留）✅。**NRE 修复（v1.8）**：`McpServerHost.BuildOptions()` 显式初始化 `options.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.OrdinalIgnoreCase)`（SDK preview.3 直建 `McpServerOptions{}` 时 ToolCollection 为 null，仅 DI builder 路径会初始化；保留 stdio+TCP 双 transport）；`BuildOptions()` 改 internal + `InternalsVisibleTo`；新增 `McpServerHostTests`（2 测试）。真机验证：官方 StdioClientTransport spawn `NeoEditor.exe --mcp` → `tools/list` 12 工具 + `tools/call GetModInfo` 返回真实数据。**9D-3** AppConfig 7 字段 + `ConfigService` ProtectedData 加密 `AiApiKey` 落盘（兼容旧明文）✅。**9D-4** AiChat + ImageGenerationService 配置改 **IConfigService 优先 → 环境变量 fallback** ✅。**9D-5** SettingsPage "AI & MCP" 分组（Endpoint/API Key 掩码/3 模型/MCP 开关+端口）+ `Settings.*` resx 键 ✅。**AI Chat 无配置崩溃修复**：未配 API Key 时 `new ApiKeyCredential("")` 抛 ArgumentException 导致 GUI 启动挂 → 改为**禁用态降级**（无 key 注册 null 客户端 + `IChatService/IRagService.IsAvailable` + AiChatViewModel 禁用面板提示，配置后重启生效）。**394/394 测试**（+2：McpServerHost 2；+4：AiChat 无配置降级 4）。**下一步：9E（工具栏/Dock 重整，D02）**。
- ✅ **AI 配置 Provider 列表 (2026-08-01)**：`AiEndpoint`/`AiApiKey` 扁平字段 → `AiProviders` 列表（`AiProviderConfig`：Id/Name/Endpoint/ApiKey）+ 每模型 `AiModelProviderId`/`AiEmbeddingProviderId`/`ImageProviderId`（空 = 第一个 provider）；新增 `AiProviderResolver`（Core 纯静态，provider > env > 默认，无 key → 禁用态）；`ConfigService` 逐 provider 加密 ApiKey + legacy `AiEndpoint/AiApiKey` → "Default" provider 迁移；AiChat `ChatClient`/`EmbeddingClient` 与 `ImageGenerationService` 各按自身模型 providerId 解析（对话/嵌入/图片可用不同供应商）；Settings UI 改 Provider 列表编辑器 + 每模型 Provider 下拉（resx 键更新）。**408/408 测试**（+14）。
- ✅ **Phase 9E 动态 Dock 构建 (2026-08-01)**：D02 全部落地，Phase 9 收官。**接口**：`IToolPlugin` 新增 `CreateToolbarItems()`（默认 null）+ `ToolbarItem` record。**动态构建**：`Documents.cs` 删 13 个手写 Tool 子类 → `PluginTool`（Id=插件类型名，Title=plugin.Title，Context=CreateToolView）；`DocumentWorkspaceViewModel.BuildToolDock()` 枚举 `IEnumerable<IToolPlugin>` 按 `DefaultDock`/`Order` 分组，XAML 三组 `ToolDock` 改 `ItemsSource` 绑定（手写 `<Tool>` 元素全删，Dock 容器保留）。**DataViewer 拆 5 plugin**：`DataTablePlugin`(Bottom,10，初始 Context=`DataTablePlaceholder`，App shell 在 profile 打开时替换为共享 `ModDataToolViewModel`)/`ForwardIndexPlugin`(Bottom,11)/`ReverseIndexPlugin`(Bottom,12)/`SearchPlugin`(Bottom,13)/`PeekPlugin`(Right,10)，新增 `IIndexTableFactory` 提供 Forward/Reverse 共享 singleton（Conflicts/Validation 因 9B 已删除不建）。**EntityEditor 拆 2 plugin**：`EntityEditorPlugin` 收敛为纯 `IDocumentPlugin`，新增 `KeyValueEditorPlugin`(Left,10)/`OverlayChainPlugin`(Left,20)。**ImageTools 拆 2 plugin**：`ImageAssetManagerPlugin`(Left,30，从 Right 移入)/`ImageOrchestrationPlugin`(Right,35)。**AiChatPlugin 改构造函数注入**（不再依赖从不被调用的 `_ctx`）。**所有 Tool VM 注册 DI singleton**（插件视图与 App shell 共享实例）。**Profile Tool（新，左 Dock）**：`ProfileToolPlugin`(App) + `ProfileToolViewModel/View`——New/Import Mod + Edit Profile / Reload Merge View（profile 选择器）。**工具栏 §5.0**：顶部仅剩 `💾 Save`；实体操作 → DataTable 工具栏 `[Add] [Copy] [Delete]`（新增 Copy 按钮克隆选中行）；面板切换按钮删除。**414/414 测试通过**（+6）。真机冒烟：GUI 启动 12s 无崩溃，动态 Dock + Profile Tool 渲染正常。**Phase 9 全部完成（9A-9E）**。
- ✅ **遗留清理 (2026-08-01)**：三块 Phase 9 遗留完成——①**侧边栏精简**（§5.0）：删 Mods/Profiles 按钮 → 新增 Workspace 按钮（`WorkspaceHistoryViewModel`，逆序历史 profile 工作区 + dirty 状态 + 双击打开合并视图，Transient 注册每次打开刷新）；`ModDatabaseViewModel`/`ModIndexViewModel`（含 CSV/XLSX/zip 导出）保留无入口。②**ModImages 插件化**（D02 §五注）：Core 新增 `IModImagesDocumentFactory` + ImageTools `ModImagesDocumentFactory`，App shell 不再直接 new；EditProfile 属 App 内部文档保持 App 处理。③**NU1903 修复**：Infra.csproj 显式 `SQLitePCLRaw.bundle_e_sqlite3 3.0.5`（2.x 无修复版）。**416/416 测试通过**（+2 工厂测试）。

```
NeoEditor.sln
├── NeoEditor.Messaging/           消息基础设施 (net10.0, 0 外部依赖)
├── NeoEditor.Core/                领域模型 + 契约 + 插件分类 (0 Avalonia)
│   ├── Abstractions/              IHostService, IReferenceEntry, IReferenceListSerializer, PluginKind, IExtensionPoint 等
│   ├── Messages/                  跨 Plugin 消息（OpenImageDocumentMessage 等）
│   └── Model/                     IEntity, ReferenceList<T>, EntityRef, ReferenceFormats (7 Format 类) 等 25 实体 + 引用类型
├── NeoEditor.Infra/               数据访问 + 业务服务 (EF Core + SQLite, 0 Avalonia)
│   ├── Data/Context/              GameDbContext (含 OnModelCreating ValueConverter 自动发现)
│   ├── Data/Converters/           ReferenceListStringConverter
│   ├── Helper/                    ReferenceParser, ReferencePattern, ReferenceListSerializer
│   └── Services/                  ReferenceIndex, EntityMergeStore
├── NeoEditor.UI.Common/           共享 Avalonia 控件/转换器/行为 + HexMapRenderer
├── NeoEditor.App/                  Shell + DI + 启动 + Settings
│   ├── Helper/                 解析/引用/工具
│   ├── ViewModels/             MVVM 视图模型
│   ├── Views/                  Avalonia axaml + code-behind
│   │   └── UserControls/
│   │       └── ModGameDataTabsView (5 partial)
│   ├── spec/                   架构决策规则（硬约束）
│   └── Docs/                   设计/参考/历史文档 + testround/
├── NeoEditor.Plugins.DataViewer/   DataViewer Plugin ⭐
│   ├── Converters/ (5 + 1 helper)
│   ├── Services/ (11)
│   ├── ViewModels/ (6)
│   └── Views/ (5 .axaml)
├── NeoEditor.Plugins.EntityEditor/ EntityEditor Plugin ⭐
│   ├── Services/                    VisHelperService + RefNode
│   ├── Visualizers/ (25)            全部 25 个 IEntityVisualizer
│   ├── ViewModels/                  EntityEditorDocument + KeyValueEditorViewModel + OverlayChainToolContent + ReferencePickerViewModel
│   ├── Helper/                      HighlightBackgroundRenderHelper + XmlCompareHelper + TextEditorScrollSyncAttached
│   └── Views/                       EntityEditorView + KeyValueEditorView + XmlDiffView + ReferencePickerDialog + ReferenceFieldEditor + Dialogs + ZoomableImageView
├── NeoEditor.Plugins.ImageTools/        ImageTools Plugin ⭐ (含像素图像生成 G1-G3 + 集成收尾)
│   ├── Services/ (8)                 IImageEditorProcessingService + ImageEditorProcessingService + IImageSearchService + IModImageListService + PixelArtConversionService(颜色量化/边缘增强/抖动) + ImageGenerationService(OpenAI Images API + 自动像素后处理, OPENAI_IMAGE_MODEL 可配置) + EntityToPromptConverter + EntityImageGenActionProvider(右键生成)
│   ├── ViewModels/ (6)               ImageEditorDocument + ImageCropSelection + ModImagesDocument + ImagePreviewContent + ImageAssetManagerViewModel + ImageOrchestrationViewModel
│   ├── Helper/ (4)                   PixelArtOutputSizeCalculator + CropSelectionInteraction + ImageSelectionOverlayPresenter + ModImagePairDropHandler
│   └── Views/ (5 .axaml)             ImageEditorDocumentView + ModImagesDocumentView + ImagePreviewView + ImageAssetManagerView + ImageOrchestrationView
├── NeoEditor.Plugins.Mcp/               MCP Plugin ⭐ (M13+ Phase 7)
│   ├── Server/                        McpServerHost (官方 ModelContextProtocol SDK v2, stdio transport)
│   ├── Tools/                         EditorTools ([McpServerTool] 属性, 12 工具) + McpToolExecutor (IMcpToolProvider)
│   └── Resources/                     EntityResourceProvider (entity://{type}/{id})
├── NeoEditor.Plugins.Cli/               CLI Plugin ⭐ (M13+ Phase 7)
│   └── Cli/                           CliCommandParser + CliCommandHandler (8 命令) + CliOutputFormatter (JSON/text)
├── NeoEditor.Plugins.AiChat/            AI Chat Plugin ⭐ (M13+ Phase 7 + Agent 增强 A1-A4)
│   ├── Services/                      ChatService (function-calling loop + RAG 上下文注入 + Streaming typewriter) + ChatHistoryManager + SystemPromptBuilder (实体 Schema 自动生成) + EntitySummaryBuilder + RagService ✅
│   ├── ViewModels/                    AiChatViewModel (系统提示词面板 + BuildIndex + streaming 增量更新 + IsThinking 指示器)
│   └── Views/                         AiChatView (聊天面板 + System Prompt 可折叠面板 + RAG 索引按钮 + 思考指示器, Right Tool Dock, Order=40)
└── Tests/
    ├── NeoEditor.Messaging.Tests/      3/3 ✅
    ├── NeoEditor.Core.Tests/          33/33 ✅ (+6 Plugin 架构测试, 含 Mcp/Cli/AiChat assembly)
    ├── NeoEditor.Infra.Tests/        143/143 ✅ (+17 Repository/HostService 三动作测试 +3 R26 v2 对称契约)
    ├── NeoEditor.UI.Common.Tests/      1/1 ✅
    ├── NeoEditor.Plugins.DataViewer.Tests/  57/57 ✅
    ├── NeoEditor.Plugins.EntityEditor.Tests/ 26/26 ✅ (+17 ReferencePicker VM 测试)
    ├── NeoEditor.Plugins.ImageTools.Tests/  16/16 ✅ (像素转换 7 + prompt 构建 5 + 现有 4)
    ├── NeoEditor.Plugins.Mcp.Tests/      22/22 ✅ (MCP Plugin: Metadata + ToolExecutor + 新工具 GetEntitySchema/SearchAllTypes/GetModInfo/GenerateImage)
    ├── NeoEditor.Plugins.Cli.Tests/      40/40 ✅ (CLI: Parser 28 + Formatter 8 + Plugin 4)
    ├── NeoEditor.Plugins.AiChat.Tests/   23/23 ✅ (AiChat: ChatHistoryManager + SystemPromptBuilder + Plugin Metadata)
    └── NeoEditor.Integration.Tests/         10/10 ✅
```

## 构建与测试

在仓库根 `D:\RiderProjects\NeoEditor` 执行：

```bash
bash build.sh                                 # 编译（先杀旧进程再编 22 个项目）
dotnet watch run --project NeoEditor.App      # 开发模式：监测文件变更 → 自动重编 → 重启
dotnet test Tests/NeoEditor.Messaging.Tests   # 运行指定测试项目
```

> **重要**：改代码后运行 `bash build.sh` 而非 `dotnet build NeoEditor.sln`。后者会因运行的 NeoEditor 进程锁 DLL 而卡住重试。
> **Claude Code 会话内优先用 Rider MCP `build_solution_start` + `build_solution_state`**——效果等同于 `bash build.sh`（IDE 编译前自动杀进程），且能直接拿到 problems 列表。

**当前已知警告/错误**（非阻塞）：
- ✅ ~~`NU1903`~~ 已修复（2026-08-01）：SQLitePCLRaw.lib.e_sqlite3 高危漏洞（CVE-2025-6965 / GHSA-2m69-gcr7-jv3q，2.x 无修复版）→ Infra.csproj 显式引用 `SQLitePCLRaw.bundle_e_sqlite3 3.0.5`（EFCore 10.0.10 仍 pin 漏洞版 2.1.11，故显式覆盖）

## 架构规则（开发前必读，硬约束）

项目已完成 M0-M12 重构和插件化迁移，**M13+ Phase 1-8 + A1-A4 + G1-G3 + ProDataGrid 全部完成**。**Phase 9 计划已全部定稿（7/7 议题，D02/R26/R27/R28 固化）**。所有代码改动必须遵守 `NeoEditor.App/spec/` 下的规则
（方向 D01-D02 / 基石 R01-R28 全部落地 / 禁止 N01-N06）。冲突时**以 spec 为准**。

**当前状态**：M0-M12 全部完成，M13+ Phase 1-8 全部完成，Agent 编排增强 A1-A4 全部完成 ✅，像素图像生成 G1-G3 全部完成 + 集成收尾 ✅，ProDataGrid 迁移完成 ✅，ProDataGrid 列过滤器 F1-F4 全部完成 ✅（自建 TypeFilterFlyout + FilterContexts），TabStrip → ListBox 替换 ✅ (Doc 35 P1)，tab 头部本地化修复 ✅（类名不走 Loc[]），ModInfo Schema 修复 + XmlParser ReferenceList 修复 + Import/Sort bug 修复 ✅，排序闪退 + 虚拟列排序 + 游戏数据加载修复 ✅。**Phase 9 全部完成（9A-9E）**。414/414 测试通过。0 架构债，28/28 spec 落地（R26 v2 + R27 + R28 固化）。R17-R28 全部达成。D01-D02 生效。**Phase 9 收官（[36](NeoEditor.App/Docs/36-phase9-plan.md) v3.0）**：9A Bug 修复 ✅ + **9B B1-B5 全部完成 ✅**（双 Repository + 三动作 + per-profile dirty + IncludeGame/单 Mod 去除 + ModManager 并入 + 删 Validation + View 收敛）+ **R26 v2 对称 Repository 契约重构 ✅**（CRUD/双 diff/dirty/Save/Load 全对称 + command 门面 + PreExecuteHook 修复）+ **9C 图片资产修正 ✅**（R27 Image Browser + Image Orchestration 拆分 + 议题1 目录结构 + 议题6 自动加载）+ **9D AI/MCP UI 全部完成 ✅**（AiChat 入 Dock + --mcp 启动路径 + AppConfig Provider 列表（AiProviders + 每模型 ProviderId）+ 逐 Provider 加密 ApiKey + IConfigService 配置 + Settings "AI & MCP" 分组；**`--mcp` NRE 已修复 v1.8**：ToolCollection 显式初始化 + McpServerHostTests 2 测试 + 真机 StdioClientTransport 验证 12 工具；**AI Chat 无配置崩溃已修复 v1.9**：无 key 禁用态降级 + IsAvailable，配置后重启生效）+ **9E 动态 Dock 构建全部完成 ✅**（D02 落地：IToolPlugin 增强 CreateToolbarItems + PluginTool 动态构建 + DataViewer 拆 5 plugin + EntityEditor 拆 2 plugin + ImageTools 拆 2 plugin + Profile Tool 新增 + 顶部工具栏仅剩 Save + DataTable 工具栏 [Add][Copy][Delete]）。核心：9B = DB/XML 双 Repository + Save/Export/Publish 三动作（R26 v2）；9E = D02 动态 Dock 布局（IToolPlugin 1:1 消费）。**Phase 9 全部完成**。遗留清理 2026-08-01 ✅（侧边栏工作区面板/ModImages 工厂化/NU1903 修复），416/416 测试。

核心几条：

- **R01 / N01 / R24** 状态唯一所有者 `IWorkspaceSession` + 统一写路径 `IHostService`；禁止新增静态可变状态
- **R03 / N02** 引用解析只走注入的 `IReferenceResolver`；禁止 `ReferenceResolver.Instance`
- **R04 / N03** View 只组装控件；禁止在 View 写业务/导航逻辑
- **R05 / N04** 消息只做跨区域 UI 联动，单一接收方；禁止死消息
- **R07 / R14** 单向分层 `Domain → Core → ViewModels → Views`（文件夹+命名空间约定）
- **D02** Dock 布局由 IToolPlugin 动态构建（Tool/Document/Service 分类，Tool↔Plugin 1:1），不再手写 XAML Tool 元素

完整规则见 [NeoEditor.App/spec/README.md](NeoEditor.App/spec/README.md)。

**当前阶段**：M13+ Phase 1-8 全部完成 ✅，Agent 编排 A1-A4 全部完成 ✅，像素图像生成 G1-G3 全部完成 ✅，ProDataGrid 迁移完成 ✅，ProDataGrid 列过滤器 F1-F4 全部完成 ✅（内置模板 + Column Chooser），TabStrip → ListBox 替换 ✅ (Doc 35 P1)，Bug 修复完成 ✅，Avalonia 12 升级 + Semi/Ursa 移除完成 ✅。**Phase 9 全部完成**（Doc 36 v3.0）——9A ✅ + **9B B1-B5 全部完成 ✅** + **R26 v2 对称 Repository 契约重构 ✅** + **9C 图片资产修正 ✅** + **9D AI/MCP UI 全部完成 ✅**（`--mcp` NRE + AI Chat 无配置崩溃均已修复）+ **AI 配置 Provider 列表** + **9E 动态 Dock 构建全部完成 ✅**（D02 落地：IToolPlugin 消费 + PluginTool 动态构建 + DataViewer/EntityEditor/ImageTools 插件拆分 + Profile Tool + 工具栏重整）。414/414 测试通过。11 src + 11 test = 22 项目。Doc 35 P2 全部完成 ✅。新增 spec D02, R26(v2), R27-R28 全部固化。D01-D02 全部生效。遗留清理 2026-08-01 ✅（侧边栏工作区面板/ModImages 工厂化/NU1903 修复），416/416 测试。

## 文档地图

- [NeoEditor.App/spec/D01-core-plugin-architecture.md](NeoEditor.App/spec/D01-core-plugin-architecture.md) — **根本架构方向** ← 必读
- [NeoEditor.App/spec/D02-dynamic-dock-layout.md](NeoEditor.App/spec/D02-dynamic-dock-layout.md) — **动态 Dock 布局方向** ← 必读（Tool/Document/Service 分类，IToolPlugin 动态构建）
- [NeoEditor.App/Docs/30-post-m12-development-plan.md](NeoEditor.App/Docs/30-post-m12-development-plan.md) — M13+ 领域驱动服务架构开发计划 ⭐
- [NeoEditor.App/Docs/36-phase9-plan.md](NeoEditor.App/Docs/36-phase9-plan.md) — Phase 9 开发计划（7/7 议题全部定稿：工具栏 D02 + 保存导出 R26 + 图片 + AI UI）⭐ 当前
- [NeoEditor.App/Docs/31-prodatagrid-migration-plan.md](NeoEditor.App/Docs/31-prodatagrid-migration-plan.md) — ProDataGrid 迁移计划 ✅ 完成（2026-07-31）
- [NeoEditor.App/Docs/32-agent-orchestration-plan.md](NeoEditor.App/Docs/32-agent-orchestration-plan.md) — Agent 编排增强计划（系统提示词 + RAG + MCP + Streaming） ✅ A1-A4 完成
- [NeoEditor.App/Docs/33-image-generation-plan.md](NeoEditor.App/Docs/33-image-generation-plan.md) — 像素风格图像生成计划（XML → 像素图） ✅ G1-G3 完成
- [NeoEditor.App/Docs/34-prodatagrid-column-filter-plan.md](NeoEditor.App/Docs/34-prodatagrid-column-filter-plan.md) — ProDataGrid 列过滤器实现计划（F1-F4 ✅）
- [NeoEditor.App/Docs/35-tabstrip-listbox-filter-templates-plan.md](NeoEditor.App/Docs/35-tabstrip-listbox-filter-templates-plan.md) — TabStrip → ListBox + ProDataGrid 内置 Filter 模板 + Column Chooser ✅（全部完成）
- [NeoEditor.App/Docs/third-party/prodatagrid/](NeoEditor.App/Docs/third-party/prodatagrid/) — ProDataGrid 外部文档镜像（API / articles / filtering-model-end-to-end 等）
- ProDataGrid 源码仓库：`C:\Users\Cromzst\RiderProjects\ProDataGrid`（主题模板 → `src/Avalonia.Controls.DataGrid/Themes/Generic.xaml`，Sample → `src/DataGridSample/`）
- [NeoEditor.App/spec/README.md](NeoEditor.App/spec/README.md) — 决策规则登记表（D01-D02 / R01-R28 / N01-N06 全表，含待决策项）
- [NeoEditor.App/Docs/28-plugin-architecture-migration.md](NeoEditor.App/Docs/28-plugin-architecture-migration.md) — 插件化迁移计划（M0-M12 已完成 ✅）
- [NeoEditor.App/Docs/25-architecture-decisions.md](NeoEditor.App/Docs/25-architecture-decisions.md) — 架构决策详解 + UI 原型
- [NeoEditor.App/Docs/26-refactor-roadmap.md](NeoEditor.App/Docs/26-refactor-roadmap.md) — 重构路线图 M0-M4
- [NeoEditor.App/Docs/23](NeoEditor.App/Docs/23-architecture-redesign-proposal.md) / [24](NeoEditor.App/Docs/24-workflow-specification.md) — 工作区架构与用户工作流
- [NeoEditor.App/Docs/20-data-class-field-reference.md](NeoEditor.App/Docs/20-data-class-field-reference.md) — 游戏数据类字段参考
- [NeoEditor.App/Docs/CHANGELOG.md](NeoEditor.App/Docs/CHANGELOG.md) — 变更历史
- [NeoEditor.App/Docs/testround/test_round13_summary.md](NeoEditor.App/Docs/testround/test_round13_summary.md) — M9 DataViewer Plugin ✅
- [NeoEditor.App/Docs/testround/test_round16_summary.md](NeoEditor.App/Docs/testround/test_round16_summary.md) — M10 Phase 5: Editor Views/VMs 迁移
- [NeoEditor.App/Docs/testround/test_round17_summary.md](NeoEditor.App/Docs/testround/test_round17_summary.md) — M10 Phase 6-8: DI 简化 + R17 解除 ✅
- [NeoEditor.App/Docs/testround/test_round18_summary.md](NeoEditor.App/Docs/testround/test_round18_summary.md) — M11 ImageTools Plugin ✅
- [NeoEditor.App/Docs/testround/test_round19_summary.md](NeoEditor.App/Docs/testround/test_round19_summary.md) — M12 收尾全部完成 ✅

## 工作约定

- 文件操作用完整绝对路径（Windows 盘符 + 反斜杠）。
- 当前阶段：**Phase 9 全部完成（[36](NeoEditor.App/Docs/36-phase9-plan.md) v3.0）**——9A ✅ + **9B B1-B5 全部完成 ✅** + **R26 v2 对称 Repository 契约重构 ✅** + **9C 图片资产修正 ✅（R27 Browser/Orchestration 拆分）** + **9D AI/MCP UI 全部完成 ✅**（AiChat 入 Dock + `--mcp` 启动 + AppConfig Provider 列表/逐 Provider 加密/IConfigService/Settings 分组；**`--mcp` NRE 已修复**：ToolCollection 显式初始化 + 2 回归测试 + 真机验证 12 工具；**AI Chat 无配置崩溃已修复**：无 key 禁用态 + 配置后重启生效）+ **AI 配置 Provider 列表** + **9E 动态 Dock 构建全部完成 ✅**（D02：IToolPlugin 消费 + PluginTool + DataViewer 拆 5 / EntityEditor 拆 2 / ImageTools 拆 2 plugin + Profile Tool 新增 + 顶部工具栏仅剩 Save + DataTable 工具栏 [Add][Copy][Delete]）。414/414 测试通过。11 src + 11 test = 22 项目。28/28 spec 落地（含 R26 v2 + R27 + R28 固化）。D01-D02 生效。核心：9B = DB/XML 双 Repository + Save/Export/Publish 三动作；9E = D02 动态 Dock 布局。**Phase 9 收官**。遗留清理 2026-08-01 ✅（侧边栏工作区面板/ModImages 工厂化/NU1903 修复），416/416 测试。

- **编译优先用 Rider MCP `build_solution_start`**（自动杀旧进程，无 DLL 锁，返回 problems）。`bash build.sh` 作为备选。`dotnet build NeoEditor.sln` 不要直接用。
- **代码搜索优先用 `search_symbol` / `search_text`（Rider MCP）**，比 `grep` 更精确、无权限弹窗。
- **重构优先用 `rename_refactoring` / `extract_method` 等 Rider MCP**，一次改所有引用，比手动安全。
- **改完代码用 `reformat_file` + `get_file_problems`（Rider MCP）** 确保格式和零 warning。
- 改动后确认编译通过；涉及核心服务/数据的改动补充测试。
- XAML 引用跨程序集类型时必须加 `;assembly=` 前缀（如 `clr-namespace:NeoEditor.Data.Model;assembly=NeoEditor.Core`）。
- `ConfigService.SaveAsync()` 已有 `SemaphoreSlim` 写锁保护，并发调用安全；设置页绑定请用 ViewModel 层的 `DisplayXxx` 包装属性（而非直接绑 `Config.Xxx`），确保 `OnPropertyChanged` + `SaveAsync` 正常触发。
