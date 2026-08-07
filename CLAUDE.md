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
- 测试：**xUnit**（`Tests/` 下 13 个测试项目，覆盖全部模块 + 集成测试）

## 项目结构

M8 完成，M9 DataViewer Plugin 迁移完成，**M10 EntityEditor Plugin 全部完成**，**M11 ImageTools Plugin 全部完成**，**M12 收尾完成**（详见 [Docs/28](NeoEditor.App/Docs/28-plugin-architecture-migration.md)）。
**M13+ 领域驱动服务架构**——Phase 1-8 全部完成，Agent 编排 A1-A4 全部完成，像素图像 G1-G3 全部完成，ProDataGrid 迁移完成。详见 [Docs/30-post-m12-development-plan.md](NeoEditor.App/Docs/30-post-m12-development-plan.md)。

**当前 (M13+ Phase 1-8 + A1-A4 + G1-G3 + ProDataGrid + Phase 9A-9E + 遗留清理 + Docs/39-42 + D03 ParaTranz + 播放器 v2.44 全部完成，15 src + 13 test = 28 项目，845/845 测试)**:
- ✅ **Round 57：ItemType 可视化设计文档（D04）+ Creature 可视化重构（D05）+ FieldGroupMetadata 全量修正（845/845，2026-08-08）**：① **D04 设计文档**（spec/）：37 列→8 呈现位置逐字段设计，确立「把数据翻译成问题答案」范式（损耗→寿命、士气→有效伤害、权重→概率），登记 spec/README；② **25 个 visualizer 审计**：A 级 9 个（AttackMode/BattleMove/CampType/Condition/Encounter/Faction/HexType/Recipe/TreasureTable）、B 级 8 个、C 级 6 个（纯关联类）；③ **简单补漏**：DataFile/GameVar/Headline 挂反向引用、AttackMode Sound 接播放、Encounter 补 fCreatureChance/aMinimapHexes/ptEditor、BattleMove 补 vHexTypes；④ **D05 Creature 设计文档 + 重构**：13 字段全覆盖、战斗三层、状态概率徽章、战利品双池、遭遇链双向；**设计 agent 断言「nHP 等属性游戏有、未导入」被实测证伪**（全目录无这些字段）→ 文档改「不预留虚构槽位」；`CreatureEntityVisualizer` 293→968 行重写，`OnEnterConditions` 错误标签消除，+5 测试；⑤ **FieldGroupMetadata 全量修正**：原分组与真实模型大面积漂移（虚构列名→全部落入默认分组），按真实 [Column] 重写 24 类型，脚本核对零漂移。详见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md)
- ✅ **播放器 v2.44：受伤存档重启必崩修复（接管序列化——LSO 引用全展开，2026-08-05）**：Ruffle nightly 反序列化 AMF3 引用有 bug（`Amf3ObjectReference` → Undefined，ECMAArray dense 元素丢弃）→ 存档读回崩溃。`localStorage.getItem` 拦截 + `lso-expand-web.js`（无依赖浏览器版 AMF3/LSO 解析+重编码器，与 `player-tools/lso-expand.js` node 原型逐字节一致）在 SWF 加载前把引用全部展开为内联。4312 叶子值逐叶子对比零不一致；沙箱端到端 6/6。**待用户实机验证**。详见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md)
- ✅ **Docs/42 WebView 预览 + NeoScavengerPlayer 独立播放器（P0-P5 全部完成，2026-08-05）**：`NeoEditor.Player`（独立 WinExe，程序集名 `NeoScavengerPlayer`）+ `NeoEditor.Player.Core`（`GameContentServer` 回环 HTTP + `ProxyHttpModule` 实时反代 + `SaveBackupService` 存档备份 + `SwfLogBridge` + `Web/ruffle` WASM 资源）+ `NeoEditor.Plugins.WebView`（内置 WebView 工具面板）。**取代 Docs/40 ruffle.exe 外部运行器（已整体删除，仅留 `RuffleOptionsBuilder.FindSwfPath` SWF 发现）**。工具栏「内置预览」= WebView + 实时反代（编辑器当前状态，无需导出）；独立播放器 = 交付态游玩。详见 [42](NeoEditor.App/Docs/42-webview-ruffle-preview-plan.md)
- ✅ **D03 ParaTranz 翻译平台集成（M1-M4 全部完成，2026-08-05）**：`NeoEditor.Plugins.Paratranz` — M1 API Helper（17 测试）+ M2 数据转换层（`TranslationKeyParser`/`TranslationExtractor`/`CsvTranslationSerializer`(CsvHelper)/`TranslationApplier`，31 测试）+ M3 设置页「ParaTranz」分组（Token DPAPI 加密）+ M4 Dock 面板双 Tab（同步 + NativeWebView 网页工作台 + WebView diff 预览，命令式应用）。M5（MCP 工具/词条级自动化）可选待做。详见 [D03](NeoEditor.App/spec/D03-paratranz-integration.md)
- ✅ **发布流程（2026-08-05）**：`publish.ps1` 本地交互发包脚本（单文件 ~143MB / 多文件 / 测试）+ `.github/workflows/release.yml` 自动 Release Windows（编辑器 + 播放器双产物 zip）
- ✅ **Round 47：工具面板名称全本地化 + 音频资产空名/0KB 修复（831/831，2026-08-07）**：① 音频空名/0KB 根因 = `index.json` 小写字段 vs PascalCase 类，System.Text.Json 大小写敏感——`SoundsToolViewModel`/`AudioPlaybackService` 统一 `PropertyNameCaseInsensitive = true`；② 全部 11 个 `IToolPlugin` 注入 `ILocalizationService`，`Title → _loc["Tools.Xxx"]`（新增 Tools.* 三语键），删除硬编码 Title，插件测试断言改对 key；③ 脚本误改修复：`ParatranzPlugin` ctor 残留逗号、`SessionDirtyDebugTool` 的 `_loc` 字段错位（ViewModel 类→Plugin 类）。详见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md)
- ✅ **Round 46：About 消息框化 + 存档工具并入存档管理 + 剪贴板截获内容修复（831/831，2026-08-07）**：① About 改 `MessageBox.Avalonia` 纯消息框（右上角 X 关闭，去掉双确认按钮），PromptDialogWindow 恢复原样；② 存档修改工具从调试菜单并入**存档管理窗口**（每行「修改」按钮 → 预载编辑，「保存并加载」后列表自动刷新）；③ 剪贴板截获修复（host.html v2.56）：Ruffle 用 textarea.value → focus → select() → `execCommand("copy")` 时序不稳——提取链改 selection → activeElement(textarea) → 兜底 querySelector，日志恢复「游戏剪贴板日志(截获)」。详见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md)
- ✅ **Round 45：存档修改工具（调试用，831/831，2026-08-07）**：`lso-expand-web.js` v2.49 加 **LSO ↔ JSON 双向转换**（`toTree` 引用内联 + `__amf` 类型标记；`fromTree` 重编码后**立即回验 parseLso**，改坏在写入前报错；sanitizeTree 处理 NaN/±Infinity 共 755 处）；SaveEditorWindow 调试工具：存档下拉 → 结构摘要 → 保存/另存为/保存并加载（写回 + 重载页面清 Ruffle SharedObject 缓存生效）。详见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md)
- ✅ **Round 44：剪贴板真根因修复 + 版本号 v0.9.0 + About + 存档日志 zip 导出（823/823，2026-08-07）**：源码取证 ruffle-rs `set_clipboard_content`——Ruffle 写剪贴板**不走 navigator.clipboard.writeText**（http://127.0.0.1 非安全上下文不可用），而是隐藏 textarea + `document.execCommand("copy")`，v2.44-v2.53 的 writeText 包装从未被调用 → **补拦截 execCommand**（copy/cut 捕获选区进日志，不写真实剪贴板）；版本号统一 v0.9.0（标题/About/zip 命名）；About 弹窗；「导出存档+日志 zip」`PlayerBundleExporter`（info.txt + 全量存档 + logs + 备份，Explorer 定位）。详见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md)
- ✅ **Round 43：耐久推演 + 条件效果翻译 + 游戏音频资产（821/821，2026-08-07）**：① 生命周期加**寿命推演**（`每小时 ≈100h · 每次 ≈100×`，把损耗率翻译成"能用多久"）；② hover 条件徽章翻译 `aFieldNames/aModifiers` 配对（`m_fMoveCost +0.5`）；③ **音频资产管线**：`player-tools/extract-sounds.js` 零依赖 SWF 解析（CWS zlib + DefineSound 打包字节头 + SymbolClass NULL 字符串）提取 **154 个 MP3**（含全部 cue）→ `{GameRootDir}/sounds/`；`IAudioPlaybackService`（Core 接口）+ winmm MCI 播放（App）；右 dock「音频资产」面板（搜索/播放）；可视化攻击行与 aSounds 徽章 **▶ 播放按钮**（无索引自动隐藏）。详见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md)
- ✅ **Round 40：ItemType Detail 心理模型重构（821/821，2026-08-07）**：按用户使用物品的认知顺序组织——两列情境布局（`AddRow` 战斗|装备 / 效果|生命周期 / 容器|来源产出，不再单列堆叠）+ `SectionHeader` 图标色条标题（⚔🧍✨⏳📦🔗）+ 语义聚合（条件→效果一处、耐久/弹药→生命周期）+ **声音按情境归位**（`aSounds` 拾取/放下→装备，攻击音效→攻击行）+ **Hero 错位修复**（关键数字行并入身份列，消除跨行隐式行错位）+ 来源与产出命名。详见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md)
- ✅ **Round 39：ImageAsset 引用约束修复（821/821，2026-08-07）**：实机日志 `LookupRef<ImageAsset> violates constraint` 异常刷屏——R34 统一解析入口 `MakeGenericMethod` 违反 `where T : IEntity`（ImageAsset 是纯文件名引用非实体）。修复：`ResolveRawSegment` 加 `IsAssignableFrom(IEntity)` 守卫、非实体目标渲染灰色原文徽章（不标琥珀）、未解析统计排除。详见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md)
- ✅ **Round 38：士气加成纳入伤害可视化（821/821，2026-08-07）**：R36 追修——攻击行 meta 加 `士气 +25%`；展开详情伤害区 = 基础 → 士气补正 StatBar（`25% (base)`）→ **有效伤害**（`5.6 (1.25 × 4.5)`，`(1+士气+此值)×(1+加成)×武器伤害` 公式说明）；总伤害条下方 Σ 有效伤害；`strIMG` 武器图标进展开详情；修 Morale 整数格式 bug（`+#;-#;0` → `+0%;-0%`）。详见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md)
- ✅ **Round 36：ItemType 战斗表现可视化（821/821，2026-08-07）**：战斗卡重写——总伤害构成堆叠条（`StackedDamageBar`，Cut 红/Blunt 蓝比例）+ 攻击模式明细行（点击**内联展开**：弹药消耗/攻击者条件/挥击短语/备注，Ctrl+Click 跳转）+ 条件徽章语义色（Fatal红/永久橙/堆叠绿/计时蓝 + `· FATAL/12h` 后缀）+ 耐久 StatBar；**守卫加固**：visualizer 全部 `IsNullOrWhiteSpace(it.Xxx)` → `Count > 0`、`Split` → `ToRawString().Split`（RawText 缓存依赖移除，序列化器外构造不再静默不渲染）。详见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md)
- ✅ **Round 35：切换游戏目录自动重载（821/821，2026-08-07）**：`BrowserIndexService` 监听 `GameRootDirChangedMessage`（`MarkStale` + `_forceRebuild` + `_buildGate` 串行重建，getmods.php 命名空间/索引/GlobalModNames 全量刷新；`_indexedRootDir` 防配置重载误触发，启动 Restore 秒开不受影响）+ `ModIndexViewModel.EnsureGameProfileAsync` 校验刷新 Game profile（顺带修复 LoadProfile 后重复 AddRange 的重复列表 bug）。详见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md)
- ✅ **Round 34：Raw Data 审计视图（821/821，2026-08-06）**：全字段表按 `FieldGroupMetadata` 分组 + 组头统计（`N 字段 · M 有值`）+ 类型化渲染（bool 颜色编码 / 引用列逐段徽章化：绿=已解析+P6 预览+跳转，琥珀=未解析警示）+ **引用解析统一走 `LookupRef<T>`（移除 `LookupSubject` 双路径）** + Expander 头带未解析引用计数（`BuildRawData(entity)` 一体化 API）+ 测试稳定性修复（EntityEditor.Tests 串行化，消除 `Resources["Services"]` 共享污染）。详见 [test_round34](NeoEditor.App/Docs/testround/test_round34_summary.md)
- ✅ **Round 33：追修验收 + 多 profile 隔离（653/653，2026-08-05）**：五项追修验收（dirty 计数按实体 / 字段级高亮 / 旧标记一次性升级 / .php 单行格式 / XML 差异对比磁盘基线）+ 多 profile 隔离（WAL 按 profile + `profile_edits` 覆盖层 + per-column pending_export）+ 搜索/MCP/CLI 读合并视图。详见 [test_round33](NeoEditor.App/Docs/testround/test_round33_summary.md)
- ✅ **Round 32：Docs/41 保存工作流收敛 + 新手引导（648/648，2026-08-03）**：编辑/增删自动落 DB（防抖无感）+ 黄/绿高亮 = "已缓存未导出" + 唯一显式保存 = Save & Export（Ctrl+Shift+S）+ 空状态三步卡片 + 一次性提示 + 字段 `?` 图标。详见 [test_round32](NeoEditor.App/Docs/testround/test_round32_summary.md)
- ✅ **Round 31：Ruffle 游戏运行器 P1（635/635，2026-08-03）**：`RuffleLocator`（RUFFLE_PATH/PATH 检测）+ `RuffleOptionsBuilder`（`--player-runtime air --base file:///` URL 编码）+ `RuffleRunnerService`（stdout 管道 + ruffle.log 双通道）+ 工具栏「用 Ruffle 启动」。**⚠️ 已被 Docs/42 内置预览取代，代码 2026-08-05 删除**。详见 [test_round31](NeoEditor.App/Docs/testround/test_round31_summary.md)
- ✅ **MCP 薄弱点完善 + Search 结构化搜索 (2026-08-03)**：①`GetDiffAsync` 占位 → 经 `DiffEngine` 的真实字段级 diff（顺带修 `FindEntityInDbSet` 反射——EF `FindAsync` 返回 `ValueTask<T>`、反射不补可选参数，旧代码静默失败）；②`DiffEngine` 引用字段按 `ReferenceText.GetRawString` 比较（消除 "[a, b]" 误报）；③修命令双重执行（`CommandHistory.Execute` 内部再执行一次）；④`Save` 工具回传真实 `SaveResult`；⑤新增 4 个 MCP 工具（Undo/Redo/Publish/ExportMod，12→16）+ CLI 同步 `undo/redo/publish/export-mod`；⑥`SearchEntitiesAsync` 结构化搜索（`EntitySearchRequest`：多表选择 + 类型化过滤 + 分页 offset + 排序，默认接口实现使测试桩零改动），MCP `SearchAllTypes` 新增 `entityTypesJson`/`filtersJson`/`offset`；⑦`EditorToolRegistry` 反射去重。**617/617 测试**（+36）。详见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md)。
- ✅ **CRUD 全路径收束 HostService (2026-08-03)**：审计发现并修复 4 条绕过 `IHostService` 的实体数据写路径——①`EntityEditorDocument.SaveDocument` 直接 `GameDbContext` 写库 → 改 `AddEntityToCache` + `SaveAsync`（插件/工厂不再注入 DbContextFactory）；②`ModDatabaseViewModel.ImportCsv` 直接 DbSet upsert → 改 `BatchEditCommand`/`AddEntityCommand` 经 `ExecuteBatchAsync` + `SaveAsync`；③`FindReplacePanel` 直连 `CommandHistory.Execute` → 改 `IHostService.ExecuteAsync`；④XML 导出双轨 → `IHostService` 新增 `CommitExportAsync`（唯一 mod XML 写入口），`ModGameDataTabsView` 两处 `File.WriteAllTextAsync` 改走该 API。**581/581 测试**（+4）。详见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md)。
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
- ✅ **Phase 9E 动态 Dock 构建 (2026-08-01)**：D02 全部落地，Phase 9 收官。**接口**：`IToolPlugin` 新增 `CreateToolbarItems()`（默认 null）+ `ToolbarItem` record。**动态构建**：`Documents.cs` 删 13 个手写 Tool 子类 → `PluginTool`（Id=插件类型名，Title=plugin.Title，Context=CreateToolView）；`DocumentWorkspaceViewModel.BuildToolDock()` 枚举 `IEnumerable<IToolPlugin>` 按 `DefaultDock`/`Order` 分组，XAML 三组 `ToolDock` 改 `ItemsSource` 绑定（手写 `<Tool>` 元素全删，Dock 容器保留）。**DataViewer 拆 5 plugin**：`DataTablePlugin`(Bottom,10，Content 由 App shell 在 `BuildToolDock` 绑为共享 `ModDataToolViewModel` 的直接构造 `ModGameDataTabsView`)/`ForwardIndexPlugin`(Bottom,11)/`ReverseIndexPlugin`(Bottom,12)/`SearchPlugin`(Bottom,13)/`PeekPlugin`(Right,10)，新增 `IIndexTableFactory` 提供 Forward/Reverse 共享 singleton（Conflicts/Validation 因 9B 已删除不建）。**EntityEditor 拆 2 plugin**：`EntityEditorPlugin` 收敛为纯 `IDocumentPlugin`，新增 `KeyValueEditorPlugin`(Left,10)/`OverlayChainPlugin`(Left,20)。**ImageTools 拆 2 plugin**：`ImageAssetManagerPlugin`(Left,30，从 Right 移入)/`ImageOrchestrationPlugin`(Right,35)。**AiChatPlugin 改构造函数注入**（不再依赖从不被调用的 `_ctx`）。**所有 Tool VM 注册 DI singleton**（插件视图与 App shell 共享实例）。**Profile Tool（新，左 Dock）**：`ProfileToolPlugin`(App) + `ProfileToolViewModel/View`——New/Import Mod + Edit Profile / Reload Merge View（profile 选择器）。**工具栏 §5.0**：顶部仅剩 `💾 Save`；实体操作 → DataTable 工具栏 `[Add] [Copy] [Delete]`（新增 Copy 按钮克隆选中行）；面板切换按钮删除。**414/414 测试通过**（+6）。真机冒烟：GUI 启动 12s 无崩溃，动态 Dock + Profile Tool 渲染正常。**Phase 9 全部完成（9A-9E）**。
- ✅ **遗留清理 (2026-08-01)**：三块 Phase 9 遗留完成——①**侧边栏精简**（§5.0）：删 Mods/Profiles 按钮 → 新增 Workspace 按钮（`WorkspaceHistoryViewModel`，逆序历史 profile 工作区 + dirty 状态 + 双击打开合并视图，Transient 注册每次打开刷新）；`ModDatabaseViewModel`/`ModIndexViewModel`（含 CSV/XLSX/zip 导出）保留无入口。②**ModImages 插件化**（D02 §五注）：Core 新增 `IModImagesDocumentFactory` + ImageTools `ModImagesDocumentFactory`，App shell 不再直接 new；EditProfile 属 App 内部文档保持 App 处理。③**NU1903 修复**：Infra.csproj 显式 `SQLitePCLRaw.bundle_e_sqlite3 3.0.5`（2.x 无修复版）。**416/416 测试通过**（+2 工厂测试）。
- ✅ **2026-08-02 两个运行 Bug 修复全部完成（真机验证通过）**：
  ① **ModId=0 误判「未导入」** ✅：`ModInfo.ModId` 约定 -1=Game / >=0=Mod，ModId=0 是合法 mod（NSEaid 首导拿到 0）。`ModId<=0` 判断「未导入」误伤 ModId=0 → 每次打开合并视图重导入撞 `UNIQUE constraint: mod_info.Path`。8 处改 `Id<=0`（DB PK）判断 + `ModManager.ImportModAsync` 防御（Path 已注册复用现有行）+ 回归测试。日志确认 NSEaid `LoadModAsync OK: 'NSEaid' ModId=0`。
  ② **Dock.Avalonia Tool 内容渲染** ✅ 已修复并真机验证：**根因 = `ToolDock.ItemsSource` 在 Dock.Avalonia 12.1.0 根本不把工具同步进布局 `VisibleDockables`**（运行时验证：ItemsSource 有工具 count=4/3/4，VisibleDockables 恒 0；DocumentDock.ItemsSource 正常）。9E D02 动态 Dock 依赖 ItemsSource 静默失败 → 工具从未进入布局 → 工具 Dock 空 + 合并视图（DataTable 工具）不加载。**修复三件套**：①`PluginTool` 同时设 `Content` + `Context` = 视图（Dock 渲染 Content）；②DataTable 工具 Content = **直接构造的 `ModGameDataTabsView`**（绑定共享 `ModDataToolVm`，`ProfileInfo` binding 驱动加载）——**纯 VM 作 Content 会崩**（`Tool.Build` 抛 `Unexpected content ModDataToolViewModel`，Application.DataTemplates 模板方案无效），删除 3 处运行时 swap；③工具加入布局改 `DockFactory.AddDockable` 手动注入——视图 code-behind 在 `DockControl.Loaded` 调 `DocumentWorkspaceViewModel.SyncToolDockIntoLayout`（`GetActiveRoot` 需 dock 焦点不可靠；workspace 启动时被 HomePage 隐藏、Loaded 在打开 profile 后才触发），DataTable 工具置为 Bottom 激活 tab；XAML ToolDock 移除 ItemsSource。`ModGameDataTabsView.OnPropertyChanged` 处理 ProfileInfo=null 清空 tabs。**DIAG 临时日志已移除**。真机验证链：`OnMainDockControlLoaded(layout=RootDock)` → AddDockable 11 工具进布局 → `[Attach] ENTER loadPending=true` → `[ReloadMergeTabs] completed: 24 tabs`（ItemType 26/Recipe 11/TreasureTable 13 真实数据）。**订正 9E/D02 描述**：DataTablePlugin 不再「初始 Context=DataTablePlaceholder + profile 打开时替换」，而是创建时 Content=直接构造 ModGameDataTabsView；ToolDock 不再用 ItemsSource。**417/417 测试通过**（+1 ImportModAsync 回归测试）。
- ✅ **2026-08-02 UI 六项改造全部完成（419/419 测试）**：①**DataTable 工具栏**：删「返回」按钮 + 清理导航死代码（`_navHistory`/`_isNavigatingBack`/`CanNavigateBack`/nav push），工具栏 Border 加作用域内统一样式（`Button`/`ToggleButton`/`TextBox`/`ComboBox` MinHeight=26、`Button` Padding=8,3/FontSize=11），文本按钮去掉内联 Padding，✕ 改 `FilterDismiss` 图标。②**Ref/Reverse Index**：`IndexTableView` Forward `entity_id`/Reverse `raw_id` 列 `Width="*"` 吃满宽度。③**ProDataGrid 确认**：Ref/Reverse 本就是 ProDataGrid（同名程序集 hard-fork），无改动。④**ImageTools 按 profile 找 img**：新增 `ProfileModSourceProvider`（ImageTools/Services，`IProfileModSourceProvider`+`ModContentRoot`），从最近 `LoadProfileMessage`/`OpenMergeEditorMessage` 的 `ModLoadInfos` 解析 mod contentRoot（`gameRoot+ModInfo.Path`，兼容绝对/相对），替代硬编码 `gameRoot/Mods/*`，无 profile 回退 `Mods/` 扫描；`ImageOrchestrationViewModel`/`ImageAssetManagerViewModel` 构造注入 + BuildSources/BuildTree 改 roots 驱动 + 补订 `OpenMergeEditorMessage`；DI 注册 + 新增 2 测试（profile 指定任意目录能找到）。⑤**Profile Tool 重构**：删 profile 下拉/两个标题（一个页面=一个 profile），4 按钮（New/Import/Edit/Reload）全图标+ToolTip 一行工具栏，Edit/Reload 作用于活跃 profile（`HasActiveProfile`）；新增 `ProfileToolTreeNodes`（Mod/Xml/DataTypeNode）+ `ModEntityStats`（25 类反射按 ModId+FilePath 分组），三层树 Mod→XML→非空数据类（XML eager 扫描、数据类懒加载按 mod 缓存 + basename 兜底）。⑥**AI Chat 布局修复**：根因 = `DockPanel.LastChildFill` 让输入区撑满、历史被挤压 → 改 Grid 行布局（历史 `*`）；历史 `ScrollViewer+ItemsControl` → 单列 **ProDataGrid ChatMimic** 气泡（新增 `Converters/BubbleConverters.cs`）+ 滚底；AiChat 插件补引 ProDataGrid 12.0.4。全量构建 0 problems，**419/419 测试通过**（+2）。详见 [test_round21](NeoEditor.App/Docs/testround/test_round21_summary.md)。
- ✅ **2026-08-02 七项改造全部完成（430/430 测试）**，详见 [test_round22](NeoEditor.App/Docs/testround/test_round22_summary.md)：①**AI Chat 可视化**：`ChatMessageItem` 加 `ChatMessageKind` 枚举（User/Assistant/Tool/System）+ `Kind`；流式循环把 `[tool: executing X]` 标记（原 `StartsWith("[tool:")` 因前导 `\n` 从未命中）解析为**独立 Tool 块**（`[GeneratedRegex]`），收尾 assistant 气泡 → 新增 Tool 消息 → 后续正文进新气泡；`AiChatView` 弃用 **DataGrid ChatMimic** → `ScrollViewer+ItemsControl` 每 Kind 独立模板（User 右蓝气泡 / Assistant 左气泡加可见边框 / Tool VSCode 深色条 / System 居中灰条），滚底改 `ScrollToEnd`；删 `BubbleConverters.cs` + AiChat 移除 ProDataGrid。②**HostService search**：`IHostService.SearchEntitiesAsync(query, limit)` 新方法（遍历 `Constants.GameTypes` 反射 `Repository<T>().GetAllAsync()` + `$"{Subject} {EntityId}"` 子串过滤），MCP `SearchAllTypes` 重构走它（工具数 12 不变），3 处 stub 补方法。③**Profile Tool 树形网格**：三节点 → 单一 `ProfileTreeItem`（Kind/Name/Path/ModId/Count/IsGame）；**游戏本体**无条件首个 Game 节点扫 `gameRoot/data/*.xml`；`TreeView+EventTriggerBehavior` → **ProDataGrid 层级网格**（`HierarchicalModel<T>` + `ChildrenSelectorAsync` 懒加载叶子，替代 XAML Interaction.Behaviors）；**叶子展开根因**：`ModEntityStats.LoadModEntityStats(db, modId, gameRoot)` 非 rooted FilePath 先 Combine gameRoot 再 Normalize（游戏数据 `data/*.xml` 相对路径不匹配）+ basename 兜底 + Serilog 日志；双击 XML 发 `OpenXmlDocumentMessage` 打开只读页；右键 ContextMenu（Open in Explorer `explorer.exe /select,` / Open file 默认程序）；`SelectedRow`(object) 解包 `HierarchicalNode.Item`。④**Image Browser** 预览改上下分栏（工具栏/树/splitter/预览/页脚，预览 `MinHeight=140` 内部横向）。⑤**MD VSCode 主题**：新增 `Assets/MarkdownTheme.axaml` 覆盖 LiveMarkdown 资源 token（VSCode Dark+ 配色），App.axaml 在 Defaults 后 include（**注意 App 程序集名是 `NeoEditor`**，非 NeoEditor.App），DocumentWorkspaceView MarkdownRenderer 加 `CodeBlockColorTheme="DarkPlus"`。⑥**Image Orchestration 树形网格**：双 ListBox → **单一 ProDataGrid 层级 DataGrid**（`HierarchicalModel<object>` source→pairs，Name/x2/Status 三列，行模板 ContentControl 按类型选 DataTemplate）；选中 `SelectedRow` 同步 `SelectedSource/SelectedPair`（选 source 自动选首对，与旧行为一致）；命令语义不变；ImageTools 补引 ProDataGrid 12.0.4。⚠️ 打包版 `HierarchicalNode<T>` 无 public `Children`（与源码仓库差异），只用 untyped `HierarchicalNode` + `SetRoots/Root`。全量构建 0 错误，**430/430 测试通过**（+11）。
- ✅ **2026-08-02 Profile Tool 崩溃修复 + Round23 八项 + 二轮修正（432/432 测试）**，详见 [test_round24](NeoEditor.App/Docs/testround/test_round24_summary.md)：**①Profile Tool 展开崩溃修复（ProDataGrid 线程亲和）**：`Expand()` 同步阻塞（`GetAwaiter().GetResult()`）+ 全 `ConfigureAwait(false)` → `ChildrenSelectorAsync` 内真实异步 DB I/O 让 `SetNodeExpandedState`（设 `IsExpanded`）落线程池 → `DataGridFormulaModel`/`DataGridDataConnection` `VerifyAccess` 崩溃。修复 = **同步 `ChildrenSelector = LoadChildren`** + `RebuildTreeAsync` 后台 `Task.Run` **预取 stats**（`PrewarmEntityStats` 含 Game `ModId=-1`）；`IsLeafSelector` 隐藏数据类叶子箭头。②**订正 round22 实现**：Orchestration 单元格模板**直接绑 item 属性**（`DataGridHierarchicalColumn` 的 CellTemplate DataContext = **item 本身**，`ContentControl` 类型分派运行时失效回退类名）→ 统一属性 + 单模板；`SearchEntitiesAsync` 加 `entityType`/`modId` 过滤 + 搜索**所有 string 属性**（`SearchAllTypes` limit 默认 100、结果含 modId）。③**AI Chat**：工具块改 **Expander**（`[tool: result]` marker + `ChatMessageItem.ToolName`）、Send/Stop **toggle**（`SendOrStopCommand`/`SendOrStopLabel`）、工具上限按**调用次数**计 + `[system:]` 提示、Enter 发送、`MaxToolCallsPerConversation` 可配（Settings → AI&MCP，默认 30）。④**放大镜**：`ReferenceFieldEditor.OnPeekClick` 用徽章同款解析（`Deserialize`→`GetBaseEntityRef`→`FindBestMatch`）+ 发 **`PeekEntityMessage`**（删死消息 `PeekReferenceRequestMessage`）。全量构建 0 错误，**432/432 测试通过**（+2 Infra 搜索测试）。
- ✅ **2026-08-02 图像工作台重设计 + Image Browser 右键 + 崩溃修复（437/437 测试）**，详见 [test_round25](NeoEditor.App/Docs/testround/test_round25_summary.md)：**①Image Orchestration 修复**：列头可拉伸（`CanUserResizeColumns=True`）；**普通列 DataContext 陷阱**（层级模式普通 `DataGridTemplateColumn` 单元格 DataContext = `HierarchicalNode`，只有 `DataGridHierarchicalColumn` 经列上 `Binding="{Binding Item}"` 解包成 item → x2/Status 列绑定全失败、`IsVisible` 回退默认 `true` 导致 Status 列每行「✓✗✓✗」4 符号；修复 = 普通列绑定加 `Item.` 前缀）。②**合并视图崩溃修复**：`ModGameDataTabsView.Tab.cs` 缓存命中路径 `foreach (var tab in cached.Tabs) Tabs.Add(tab)` 自我修改集合（`TabSnapshotCache` 存的是活引用 `_vm.Tabs`）→ `Collection was modified`；删冗余循环。③**Image Browser 右键**：「新增图片」（选图拷贝进 mod `img/` + 刷新树 + 打开编辑页；Base Game 只读，`ModImageTreeNode.IsGame`）+「AI 生成图片」（发 `OpenAiImageWorkbenchMessage` 打开工作台；`GenerateImageCommand.CanExecute=IsAvailable` 未配置禁用）。④**图像编辑工作台 4 槽模型**：`SelectedImage`/`ProcessedImage`/`AiImage`(新)/`AiProcessedImage`(新) **2×2 网格**；**每图独立保存按钮**（`SaveBitmapPairAsync` 写 PNG + 2× `x2_`）；`LoadGeneratedImage` **只设 AiImage** 不再污染原图/处理图；`PixelateAiImage` 命令（`PixelArtConversionService` 直处理内存位图，`ToImageSharp`/`ToAvaloniaBitmap` 转换）；AI 生成面板（prompt→生成，`GenerateAsync` 共享 `GenerateCoreAsync` 管线）；尺寸调整宽高**上下堆叠**加宽（130px）。⑤**移除实体右键 Generate Image**：删 `EntityImageGenActionProvider` + `IEntityContextActionProvider` 扩展点（EntityEditorDocument/Fatory/Plugin/View 接线、DI、测试）+ 死消息 `ImageGeneratedMessage`。⑥测试：ImageTools 测试项目引 **Avalonia.Headless+Skia**（`TestApp.EnsureAvaloniaInitialized` 手动初始化，避开 Headless.XUnit 拉 xunit v3 与项目 xunit 2.9 冲突）+ `ImageEditorDocumentTests` 3 测试 + `ImageAssetManagerViewModelTests` 2 测试。全量构建 0 错误，**437/437 测试通过**（+3）。
- ✅ **2026-08-02 PixelateAiImage 技术债清除（439/439 测试）**，详见 [test_round26](NeoEditor.App/Docs/testround/test_round26_summary.md)：round25 遗留技术债「PixelateAiImage 无自动单测（headless 下 Avalonia Bitmap 编码不可靠，留真机）」根治。**根因探测（5 探针）**：headless 下 Avalonia PNG **编码**产垃圾字节（ImageSharp `Image.Load` 抛 `UnknownImageFormatException`）、PNG **解码**返 1×1 占位（不抛异常）、`WriteableBitmap.Lock()` framebuffer **内存也是假的**（写 0/1/2/3 读回 48/'Ava' 字符串残留）→ round25 现有测试只验状态流不断言像素故未暴露。**修复**：`ImageEditorDocument` 新增 `_aiSourceBytes`（`LoadGeneratedImage` 保存 PNG 源字节，`ClearAiImage` 清空）；`PixelateAiImage` 改**直接从源字节 `Image.Load<Rgba32>` 走 ImageSharp 管线**（纯托管、headless 可靠），不再经 `Bitmap.Save` 往返（删 `ToImageSharp`，`SavePng` 保留供磁盘保存用）——生产更高效（省一次 Bitmap→PNG→解码往返）。**测试**：新增 `PixelateAiImage_ProducesAiProcessedSlot`（真实管线：字节流→像素化→落位 AiProcessedImage，不污染原图/处理图槽）+ `PixelateAiImage_WithoutAiImage_IsNoOp`（空槽 no-op），ImageTools 38→40，437→**439/439 全过**（构建 0 错误）。
- ✅ **2026-08-02 AI 图片工作台完善 + 花屏修复（442/442 测试）**，详见 [test_round27](NeoEditor.App/Docs/testround/test_round27_summary.md)：**①尺寸自由设置**：固定下拉 512/1024 → `AiWidth`/`AiHeight` 宽高 NumericUpDown（`AiSizeMin/Max/Step`=512/2880/16，贴合智谱约束）；**踩坑**：NumericUpDown Min/Max/Step 绑属性（`decimal?`）会显示空白不可输，须用字面量。**②生成 loading**：`IsGeneratingAi` + AI 槽 `ProgressBar IsIndeterminate` 覆盖。**③槽位标题标注尺寸**：`OriginalTitle`/`ProcessedTitle`/`AiTitle`/`AiProcessedTitle`（有图=名+尺寸，无图=纯名）。**④生成错误提示**：原空 catch 静默吞异常 → `AiGenerationError` 红色提示。**⑤花屏根因（JPEG/PNG）**：智谱返回 **JPEG** 但按 `.png` 临时文件解码 → 花屏；`GenerateCoreAsync` 统一 `Image.Load`→`SaveAsPng` 归一化 + `LoadGeneratedImage`/`ToAvaloniaBitmap` 改临时文件解码（`Bitmap(Stream)` 持源流引用，dispose 后 Skia 渲染花屏）。**⑥智谱 url 兼容**：CogView **忽略 `response_format` 返回 `data[0].url`**，原 `GetProperty("b64_json")` 抛 KeyNotFound → 双格式兼容（b64_json 优先，否则下载 url）。**⑦`ApplyPixelArt` 开关**：工作台 AI 生成传 false 显示原始真实图（不被像素化后处理毁掉）；MCP/实体保持默认 true。**⑧Settings「测试图片模型连接」按钮**：真实调 `/images/generations` 显示 HTTP 状态+错误详情。**模型质量对照**：`cogview-3-flash` 免费模型输出暖色模糊无轮廓（ASCII 分析确认内容正常但质量差）；换 `glm-image` 后显示正常——**花屏在换模型后消失，确认主因是模型质量，代码归一化是必要健壮性修复**。**运行约束**：GUI 锁 DLL 致 build/test MSB3027（验证完须关 GUI）；配置单例构造快照须重启生效。ImageTools 40→44，439→**442/442**（+3：错误提示/AI 尺寸/槽位标题，+1 上轮遗留）。

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
│   ├── Services/ (11)                IImageEditorProcessingService + ImageEditorProcessingService + IImageSearchService + IModImageListService + PixelArtConversionService(颜色量化/边缘增强/抖动) + ImageGenerationService(OpenAI Images API, prompt/实体双管线 + 自动像素后处理, OPENAI_IMAGE_MODEL 可配置) + EntityToPromptConverter + ProfileModSourceProvider(profile mod 路径) + ModImagesDocumentFactory
│   ├── ViewModels/ (6)               ImageEditorDocument + ImageCropSelection + ModImagesDocument + ImagePreviewContent + ImageAssetManagerViewModel + ImageOrchestrationViewModel
│   ├── Helper/ (4)                   PixelArtOutputSizeCalculator + CropSelectionInteraction + ImageSelectionOverlayPresenter + ModImagePairDropHandler
│   └── Views/ (5 .axaml)             ImageEditorDocumentView + ModImagesDocumentView + ImagePreviewView + ImageAssetManagerView + ImageOrchestrationView
├── NeoEditor.Plugins.Mcp/               MCP Plugin ⭐ (M13+ Phase 7)
│   ├── Server/                        McpServerHost (官方 ModelContextProtocol SDK v2, stdio + TCP transport)
│   ├── Tools/                         EditorTools ([McpServerTool] 属性, 19 工具) + McpToolExecutor (IMcpToolProvider)
│   └── Resources/                     EntityResourceProvider (entity://{type}/{id})
├── NeoEditor.Plugins.Cli/               CLI Plugin ⭐ (M13+ Phase 7)
│   └── Cli/                           CliCommandParser + CliCommandHandler (13 命令) + CliOutputFormatter (JSON/text)
├── NeoEditor.Plugins.AiChat/            AI Chat Plugin ⭐ (M13+ Phase 7 + Agent 增强 A1-A4)
│   ├── Services/                      ChatService (function-calling loop + RAG 上下文注入 + Streaming typewriter) + ChatHistoryManager + SystemPromptBuilder (实体 Schema 自动生成) + EntitySummaryBuilder + RagService ✅
│   ├── ViewModels/                    AiChatViewModel (系统提示词面板 + BuildIndex + streaming 增量更新 + IsThinking 指示器)
│   └── Views/                         AiChatView (聊天面板 + System Prompt 可折叠面板 + RAG 索引按钮 + 思考指示器, Right Tool Dock, Order=40)
├── NeoEditor.Plugins.Paratranz/         ParaTranz 翻译平台 Plugin ⭐ (D03, M1-M4 完成)
│   ├── Services/                      ParatranzApiClient (API Helper, M1) + ParatranzSyncService (同步编排, M4)
│   ├── Conversion/                    TranslationKeyParser + TranslationExtractor + CsvTranslationSerializer (CsvHelper) + TranslationApplier (M2)
│   ├── ViewModels/                    ParatranzPanelViewModel (同步 Tab + 工作台 Tab)
│   └── Views/                         ParatranzPanelView (NativeWebView 网页工作台 + WebView diff 预览弹窗)
├── NeoEditor.Plugins.WebView/           WebView 工具面板 Plugin ⭐ (Docs/42)
│   ├── Services/                      LiveGameDataExportService (实时反代)
│   └── ViewModels/Views/              WebViewToolViewModel + WebViewToolView (地址栏/前进后退/刷新/打开本地文件/预览 SWF)
├── NeoEditor.Player.Core/               独立播放器核心库 ⭐ (Docs/42, 0 UI)
│   ├── Services/                      GameContentServer (回环 HTTP) + ProxyHttpModule (实时反代) + GamePhpGenerator + SaveBackupService + SwfLogBridge + RuffleWebAssets
│   ├── Data/                          DataBrowserService + ReferenceAnalyzer + WikiDetailBuilder (数据浏览/wiki 式详情)
│   └── Web/                           host.html + lso-expand-web.js (AMF3/LSO 引用展开) + ruffle/ (WASM 资源)
├── NeoEditor.Player/                    独立播放器 GUI (Docs/42, WinExe `NeoScavengerPlayer`, 内置 WebView 游玩)
└── Tests/
    ├── NeoEditor.Messaging.Tests/        3/3 ✅
    ├── NeoEditor.Core.Tests/            74/74 ✅ (+ Plugin 架构测试, 含 Mcp/Cli/AiChat assembly)
    ├── NeoEditor.Infra.Tests/          212/212 ✅ (Repository/HostService/diff/WAL 覆盖层)
    ├── NeoEditor.UI.Common.Tests/        1/1 ✅
    ├── NeoEditor.Plugins.DataViewer.Tests/  67/67 ✅
    ├── NeoEditor.Plugins.EntityEditor.Tests/ 56/56 ✅
    ├── NeoEditor.Plugins.ImageTools.Tests/  91/91 ✅
    ├── NeoEditor.Plugins.Mcp.Tests/      37/37 ✅
    ├── NeoEditor.Plugins.Cli.Tests/      53/53 ✅
    ├── NeoEditor.Plugins.AiChat.Tests/   32/32 ✅
    ├── NeoEditor.Plugins.Paratranz.Tests/ 63/63 ✅ (D03: API Client + 转换层 + SyncService + Diff 渲染)
    ├── NeoEditor.Player.Core.Tests/      97/97 ✅ (GameContentServer/Proxy/SaveBackup/SwfLogBridge/DataBrowser/Wiki)
    └── NeoEditor.Integration.Tests/         13/13 ✅
```

## 构建与测试

在仓库根 `D:\RiderProjects\NeoEditor` 执行：

```bash
dotnet build NeoEditor.sln                     # 编译全部 28 个项目
dotnet watch run --project NeoEditor.App       # 开发模式：监测文件变更 → 自动重编 → 重启
dotnet test NeoEditor.sln                      # 全量测试（13 个测试项目，当前 821/821）
dotnet test Tests/NeoEditor.Messaging.Tests    # 运行指定测试项目
./publish.ps1                                  # 本地发包（单文件 ~143MB / 多文件 / 测试，交互菜单）
```

> **重要**：运行中的 NeoEditor 进程会锁 DLL，导致 `dotnet build` 重试（MSB3027）。改完代码先关 GUI 再编译，
> 或先用 `publish.ps1` 的「运行测试」入口（会自动跳过被锁项目）。
> **打包/发布**：`publish.ps1`（交互式）+ `.github/workflows/release.yml`（自动 Release Windows，编辑器 + 播放器双产物）。

**当前已知警告/错误**（非阻塞）：
- ✅ ~~`NU1903`~~ 已修复（2026-08-01）：SQLitePCLRaw.lib.e_sqlite3 高危漏洞（CVE-2025-6965 / GHSA-2m69-gcr7-jv3q，2.x 无修复版）→ Infra.csproj 显式引用 `SQLitePCLRaw.bundle_e_sqlite3 3.0.5`（EFCore 10.0.10 仍 pin 漏洞版 2.1.11，故显式覆盖）
- ⚠️ 构建期有少量非阻塞警告：`TextBox.Watermark` 过时（→ `PlaceholderText`，AVLN5001）、CA2017 日志模板参数、CA1416 Windows-only ProtectedData 等。不阻塞构建/测试。

## 架构规则（开发前必读，硬约束）

项目已完成 M0-M12 重构和插件化迁移，**M13+ Phase 1-8 + A1-A4 + G1-G3 + ProDataGrid + Phase 9A-9E 全部完成**。**Docs/39-42 + D03 全部落地**。所有代码改动必须遵守 `NeoEditor.App/spec/` 下的规则
（方向 D01-D03 / 基石 R00-R28 全部落地 / 禁止 N01-N06）。冲突时**以 spec 为准**。

**当前状态**：M0-M12 全部完成 → M13+ Phase 1-8（HostService/引用类型系统/插件化）✅ → Agent 编排 A1-A4 ✅ → 像素图像 G1-G3 ✅ → ProDataGrid 迁移 + 列过滤器 ✅ → Phase 9A-9E 全部完成 ✅（双 Repository + Save/Export/Publish + Image Browser/Orchestration + AI/MCP UI + 动态 Dock）→ 遗留清理 ✅ → Docs/39 图像工作站重构 ✅ → Docs/40 Ruffle 外部运行器（已废弃，2026-08-05 删除）→ **Docs/41 保存工作流收敛 + 新手引导（v2.5，2026-08-04）✅** → **Docs/42 WebView 内置预览 + NeoScavengerPlayer 独立播放器（v2.44，P0-P5 全部完成）✅** → **D03 ParaTranz 翻译平台集成（M1-M4 全部完成）✅**。**15 src + 13 test = 28 项目，821/821 测试通过（2026-08-06 实测）**。0 架构债。R00-R28 全部达成。D01-D03 生效。各轮次细节与测试计数见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md) 与 [testround/](NeoEditor.App/Docs/testround/)（test_round01~34，最新 821/821）。

核心几条：

- **R01 / N01 / R24** 状态唯一所有者 `IWorkspaceSession` + 统一写路径 `IHostService`；禁止新增静态可变状态
- **R03 / N02** 引用解析只走注入的 `IReferenceResolver`；禁止 `ReferenceResolver.Instance`
- **R04 / N03** View 只组装控件；禁止在 View 写业务/导航逻辑
- **R05 / N04** 消息只做跨区域 UI 联动，单一接收方；禁止死消息
- **R07 / R14** 单向分层 `Domain → Core → ViewModels → Views`（文件夹+命名空间约定）
- **D02** Dock 布局由 IToolPlugin 动态构建（Tool/Document/Service 分类，Tool↔Plugin 1:1），不再手写 XAML Tool 元素

完整规则见 [NeoEditor.App/spec/README.md](NeoEditor.App/spec/README.md)。

**当前阶段**：M13+ Phase 1-8 全部完成 ✅，Agent 编排 A1-A4 全部完成 ✅，像素图像 G1-G3 全部完成 ✅，ProDataGrid 迁移 + 列过滤器 F1-F4 全部完成 ✅，TabStrip → ListBox 替换 ✅，Avalonia 12 升级 + Semi/Ursa 移除 ✅，**Phase 9 全部完成（9A-9E，Doc 36 v3.0）** ✅，遗留清理 ✅，**Docs/39 图像工作站重构 ✅**，**Docs/40 Ruffle 外部运行器 🗑 已废弃（被 Docs/42 取代）**，**Docs/41 保存工作流收敛 + 新手引导 ✅（v2.5）**，**Docs/42 WebView 预览 + NeoScavengerPlayer 播放器 ✅（v2.44，P0-P5）**，**D03 ParaTranz 集成 ✅（M1-M4）**。**15 src + 13 test = 28 项目，821/821 测试通过（2026-08-06 实测）**。spec：D01-D03 / R00-R28 / N01-N06 全部生效。遗留清理 2026-08-01 ✅（侧边栏工作区面板/ModImages 工厂化/NU1903 修复）。各轮次细节见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md) 与 [testround/](NeoEditor.App/Docs/testround/)（01~34）。

## 文档地图

- [NeoEditor.App/spec/D01-core-plugin-architecture.md](NeoEditor.App/spec/D01-core-plugin-architecture.md) — **根本架构方向** ← 必读
- [NeoEditor.App/spec/D02-dynamic-dock-layout.md](NeoEditor.App/spec/D02-dynamic-dock-layout.md) — **动态 Dock 布局方向** ← 必读（Tool/Document/Service 分类，IToolPlugin 动态构建）
- [NeoEditor.App/spec/D03-paratranz-integration.md](NeoEditor.App/spec/D03-paratranz-integration.md) — **ParaTranz 翻译平台集成设计**（数据转换 / 同步工作流 / UI；M1-M4 完成，M5 可选）⭐ 2026-08-05
- [NeoEditor.App/Docs/30-post-m12-development-plan.md](NeoEditor.App/Docs/30-post-m12-development-plan.md) — M13+ 领域驱动服务架构开发计划 ⭐
- [NeoEditor.App/Docs/42-webview-ruffle-preview-plan.md](NeoEditor.App/Docs/42-webview-ruffle-preview-plan.md) — **WebView 预览 + NeoScavengerPlayer 独立播放器**（v2.44，P0-P5 全部完成）⭐ 当前
- [NeoEditor.App/Docs/41-save-workflow-onboarding-plan.md](NeoEditor.App/Docs/41-save-workflow-onboarding-plan.md) — **保存工作流收敛 + 新手引导**（v2.5，自动落库 + 高亮语义 + Save & Export）✅ 2026-08-04
- [NeoEditor.App/Docs/40-ruffle-game-runner-plan.md](NeoEditor.App/Docs/40-ruffle-game-runner-plan.md) — Ruffle 外部运行器 🗑 **已废弃（2026-08-05，被 Docs/42 取代）**
- [NeoEditor.App/Docs/39-image-editor-workstation-refactor-plan.md](NeoEditor.App/Docs/39-image-editor-workstation-refactor-plan.md) — 图像编辑工作站重构（创建/编辑双 Document）✅ 2026-08-02
- [NeoEditor.App/Docs/37-reference-column-semantics.md](NeoEditor.App/Docs/37-reference-column-semantics.md) — **引用列字段语义参考**（原版 data/*.xml 全量统计：格式模板/分隔符/每部分含义/置信度标注）⭐ 2026-08-02
- [NeoEditor.App/Docs/38-full-field-reference.md](NeoEditor.App/Docs/38-full-field-reference.md) — **全字段参考手册（整合版）**（24 表全部字段含义，实测值域）⭐ 2026-08-02
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
- [NeoEditor.App/Docs/testround/test_round21_summary.md](NeoEditor.App/Docs/testround/test_round21_summary.md) — 2026-08-02 UI 六项改造全部完成 ✅（419/419）
- [NeoEditor.App/Docs/testround/test_round22_summary.md](NeoEditor.App/Docs/testround/test_round22_summary.md) — 2026-08-02 七项改造全部完成 ✅（430/430，⚠️ 部分实现已被 round24 订正）
- [NeoEditor.App/Docs/testround/test_round23_summary.md](NeoEditor.App/Docs/testround/test_round23_summary.md) — 2026-08-02 Round22 七项改造人工验收清单（已由 round24 订正）
- [NeoEditor.App/Docs/testround/test_round24_summary.md](NeoEditor.App/Docs/testround/test_round24_summary.md) — 2026-08-02 Profile Tool 崩溃修复 + Round23 八项 + 二轮修正 ✅（432/432）⭐
- [NeoEditor.App/Docs/testround/test_round25_summary.md](NeoEditor.App/Docs/testround/test_round25_summary.md) — 2026-08-02 图像工作台重设计 + Image Browser 右键 + 崩溃修复 ✅（437/437）⭐
- [NeoEditor.App/Docs/testround/test_round26_summary.md](NeoEditor.App/Docs/testround/test_round26_summary.md) — 2026-08-02 PixelateAiImage 技术债清除 ✅（439/439）⭐
- [NeoEditor.App/Docs/testround/test_round27_summary.md](NeoEditor.App/Docs/testround/test_round27_summary.md) — 2026-08-02 AI 图片工作台完善 + 花屏修复 ✅（442/442）⭐
- [NeoEditor.App/Docs/testround/test_round28_summary.md](NeoEditor.App/Docs/testround/test_round28_summary.md) — 2026-08-02 文档/字段订正 + 引用功能修复 ✅（471/471）⭐
- [NeoEditor.App/Docs/testround/test_round29_summary.md](NeoEditor.App/Docs/testround/test_round29_summary.md) — 2026-08-02 Value Editor 引用解析一致性修复 ✅（482/482）⭐
- [NeoEditor.App/Docs/testround/test_round30_summary.md](NeoEditor.App/Docs/testround/test_round30_summary.md) — 2026-08-02 字段解释 + 可视化 + 引用解析跳转 ✅（530/530）⭐
- [NeoEditor.App/Docs/testround/test_round31_summary.md](NeoEditor.App/Docs/testround/test_round31_summary.md) — 2026-08-03 Ruffle 游戏运行器 P1 ✅（635/635，⚠️ 已被 Docs/42 取代删除）
- [NeoEditor.App/Docs/testround/test_round32_summary.md](NeoEditor.App/Docs/testround/test_round32_summary.md) — 2026-08-03 Docs/41 保存工作流收敛 + 新手引导 ✅（648/648）
- [NeoEditor.App/Docs/testround/test_round33_summary.md](NeoEditor.App/Docs/testround/test_round33_summary.md) — 2026-08-05 追修验收 + 多 profile 隔离 ✅（653/653，现 821/821）⭐
- [NeoEditor.App/Docs/testround/test_round34_summary.md](NeoEditor.App/Docs/testround/test_round34_summary.md) — 2026-08-06 Raw Data 审计视图（分组 + 类型化渲染 + 统一引用解析）✅（821/821）⭐

## 工作约定

- 文件操作用完整绝对路径（Windows 盘符 + 反斜杠）。
- 当前阶段：**M0-M12 + M13+ Phase 1-8 + A1-A4 + G1-G3 + ProDataGrid + Phase 9A-9E + 遗留清理 + Docs/39-42 + D03 全部完成**。**15 src + 13 test = 28 项目，821/821 测试通过（2026-08-06 实测）**。spec：D01-D03 / R00-R28 / N01-N06 全部生效。核心：R24 = 所有数据修改经 `IHostService`；R26 = DB/XML 双 Repository + Save/Export/Publish 三动作（写盘唯一入口 `CommitExportAsync`）；D02 = 动态 Dock 布局（IToolPlugin 1:1 消费）；Docs/42 = WebView 内置预览 + 独立播放器取代外部 ruffle.exe；D03 = ParaTranz 集成（M1-M4）。各轮次细节见 [CHANGELOG](NeoEditor.App/Docs/CHANGELOG.md) 与 [testround/](NeoEditor.App/Docs/testround/)（01~34）。

- **编译优先用 `dotnet build NeoEditor.sln`**（先关运行中的 GUI 避免 DLL 锁 MSB3027）。`dotnet test NeoEditor.sln` 全量 821/821。打包用 `./publish.ps1`（交互菜单）。
- **代码搜索优先用 `search_symbol` / `search_text`（Rider MCP）**，比 `grep` 更精确、无权限弹窗。
- **重构优先用 `rename_refactoring` / `extract_method` 等 Rider MCP**，一次改所有引用，比手动安全。
- **改完代码用 `reformat_file` + `get_file_problems`（Rider MCP）** 确保格式和零 warning。
- 改动后确认编译通过；涉及核心服务/数据的改动补充测试。
- XAML 引用跨程序集类型时必须加 `;assembly=` 前缀（如 `clr-namespace:NeoEditor.Data.Model;assembly=NeoEditor.Core`）。
- `ConfigService.SaveAsync()` 已有 `SemaphoreSlim` 写锁保护，并发调用安全；设置页绑定请用 ViewModel 层的 `DisplayXxx` 包装属性（而非直接绑 `Config.Xxx`），确保 `OnPropertyChanged` + `SaveAsync` 正常触发。
- **用户贴图识别（ZCode deepseek-vision MCP，2026-08-05 接入）**：当前会话模型不支持多模态，聊天中的图片会被过滤（模型看不到内容），但已可通过 ZCode 图像识别 MCP（MiMo v2.5）识别：
  1. **定位附件**：`C:\Users\Cromzst\.zcode\cli\artifacts\<会话ID>\prompt-attachment-upload-*.txt`（内容为 base64 data URI，用**修改时间**确认是刚发的那张）
  2. **解码**：去掉 `data:image/xxx;base64,` 前缀，解码保存为 `.png` 到工作目录
  3. **识别**：`node C:\Users\Cromzst\.zcode\workspace\default\deepseek-vision\analyze.js <图片路径> [识别提示词]`（默认提示词=详细描述，含文字/数据/表格/图表；识别结果整理成中文分区块回答用户）
  4. **凭据**：`DEEPVISION_API_KEY` / `DEEPVISION_BASE_URL` / `DEEPVISION_MODEL` 从 `C:\Users\Cromzst\.zcode\cli\config.json` 的 `mcp.servers["deepseek-vision"].env` 读取，**不硬编码密钥**
  5. 大图报错先缩小再发；解码出的图片保留在工作目录便于追问；备选：会话内 `analyze_image` MCP 工具（剪贴板/路径/URL 三模式，`analyze.js` 更可靠）
  - 完整流程见全局 `C:\Users\Cromzst\.zcode\AGENTS.md`（用户级指令，随会话加载）。
