# NeoEditor 开发状态总览

> 更新日期：2026-06-11 · 版本 v0.22.0-dev · Stage 23 (可视化本地化 + ValueEditor Peek)
> 引用系统设计文档: [14-reference-resolution-system.md](14-reference-resolution-system.md) · [15-reference-system-refactoring-plan.md](15-reference-system-refactoring-plan.md)
> UI 设计参考: [21-entity-detail-ui-design-guide.md](21-entity-detail-ui-design-guide.md)

---

## 整体进度

```
核心编辑功能     ██████████████████  95%
UI / 面板系统    ████████████████░░  92%
数据可视化       ██████████████████  95%  ← 全部 25 类型 Card 模式 + 引用解析 + 全局索引持久化完成
数据验证与诊断   ██████████░░░░░░░░  50%
架构重构         ██████████████████  95%  ← 引用系统 Phase 1-4 + 数据浏览器索引复用完成
```

---

## 一、功能清单

### 入口与导航
| 功能 | 状态 |
|------|:--:|
| HomePage 三卡片入口（Browse/New/Import）+ FluentIcons 图标 + Card 样式 | ✅ |
| Recent Mods + Profiles 快速打开 + 拖拽导入 | ✅ |
| GameRoot 未设置引导横幅 + New Mod 增强（Namespace + 自动 Profile） | ✅ |
| Save & Launch (Ctrl+Shift+S) | ✅ |
| 侧边栏 UI 重设计（FluentIcons 统一图标、48px 侧栏宽度、三组分区） | ✅ |

### 核心编辑
| 功能 | 状态 |
|------|:--:|
| DataGrid 24 表分 Tab + CRUD（CheckBox/ComboBox/NumericUpDown/TextBox 类型适配） | ✅ |
| Undo/Redo 100 步（Ctrl+Z/Y） | ✅ |
| 查找替换（正则/全词/大小写 + 字段级匹配 + 撤销支持） | ✅ |
| 单元格复制粘贴（内部 TSV buffer，编辑模式放行原生 TextBox 操作） | ✅ |
| Clone Row / AddRow / DeleteRow / 列管理器 + 列可见性持久化 | ✅ |
| Tab 脏标记 + 关闭确认 + Game 数据只读保护 | ✅ |
| 合并视图空状态引导 | ✅ |
| GridRowHeight 即时生效（设置面板修改无需重启） | ✅ |

### 引用系统（Phase 1-7 全部完成）
| 功能 | 状态 |
|------|:--:|
| 46+ ReferenceField + ReferencePattern 策略（5 实现） | ✅ |
| ReferenceParser 解析层 / ReferenceIndex 索引层 | ✅ |
| INavigationRouter 责任链导航 + INavigationTarget（Phase 3） | ✅ |
| GDH 静态状态清理 + 导航委托路由器（Phase 4） | ✅ |
| IReferenceResolver 接口 + ReferenceResolver 实例化（Phase 5-6） | ✅ |
| DataGrid ConfigureColumn 统一走 LookupSubject（Phase 6） | ✅ |
| ReferenceIndex 磁盘持久化（Phase 7） | ✅ |
| Ctrl+左键 = 跳转 + Peek，Ctrl+右键 = Peek，Ctrl+Hover | ✅ |
| 列可见性全局配置（ColumnVisibilityKeys + 侧边栏 + 双向同步） | ✅ |
| DataGrid 行高独立计算（防虚拟化抖动） | ✅ |

**正规引用解析路径**：
```
IReferenceResolver
  ├─ LookupRef<T>(source, propName, rawId)     → 可视化器
  ├─ LookupSubject(srcEid, propName, type, rawId) → DataGrid
  ├─ ReverseLookup(store, entityId)             → 反向引用
  └─ NavigateTo(type, entityId)                 → 导航
         ↓
  ReferenceIndex (内存, per-store)
    ├─ _forward / _nsForward → O(1) context-aware lookup
    ├─ _reverse → O(1) reverse lookup
    └─ _display → Subject cache
```

> 引用系统设计文档: [14-reference-resolution-system.md](14-reference-resolution-system.md)
> 重构方案: [15-reference-system-refactoring-plan.md](15-reference-system-refactoring-plan.md)

### 保存系统
| 功能 | 快捷键 | 说明 | 状态 |
|------|:--:|------|:--:|
| Quick Save | Ctrl+S | 仅写 DB | ✅ |
| Save & Export | — | DB + XML diff + 确认写盘 | ✅ |
| Save & Launch | Ctrl+Shift+S | Export + 启动游戏 | ✅ |
| 自动保存 + Snapshot + Command Log 持久化 | — | 崩溃恢复 | ✅ |

### 合并视图
| 功能 | 状态 |
|------|:--:|
| Merge/Insert 规则 + Show All 切换 + Mod 过滤下拉 | ✅ |
| 覆盖链面板 + 字段来源 Tooltip + 冲突高亮 | ✅ |
| 依赖分析（可导出 CSV） + Profile 预加载 + DB 兜底 | ✅ |

### 数据浏览器（三层结构）
| 功能 | 状态 |
|------|:--:|
| **第 1 层：侧边栏** — 7 领域 → 展开 → 实体类型按钮（含计数） | ✅ |
| **第 2 层：Dock 标签页** — 点击实体类型 → `EntityBrowserDocument`（左 ListBox + 右查看区） | ✅ |
| **第 3 层：实体查看** — 点击 ListBox 实体 → `EntityViewerDocument` + `EntityViewerView` 渲染 visualizer | ✅ |
| IEntityVisualizer 架构：BuildDetail / BuildOverview 接口 | ✅ |
| EntityVisualizerRegistry：按类型注册 + EF 代理类型兼容 | ✅ |
| 已实现 visualizer：25 个（全 24 表 + Default）全部采用 AttackMode 级 Card 模式可视化 | ✅ |
| 全部 visualizer Detail + Overview 均为 Card 模式（HeroHeader+Stats+Batch引用徽章+RawData可折叠面板） | ✅ |
| 新增 6 个此前无 visualizer 的类型：BarterHex / DataFile / GameVar / Headline / ForbiddenHex / Map | ✅ |
| ⚠️ 嵌套 Dock（查看区 DockControl）在 `Dock.Avalonia 11.3.11.16` 上无法渲染，已改用 TabControl | 🟡 待调查 | |

### 可视化系统
| 功能 | 状态 |
|------|:--:|
| 本地化 | ✅ `VisHelper.Loc(key)` + ~30 个 `Vis.*` 资源键（中/英） |
| Ctrl+Click Peek | ✅ `NavigateTo` 附带 `VisualEditorRequestedMessage` → ValueEditor 面板渲染 Overview |
| AttackMode Detail | ✅ HeroHeader + Combat Panel(含Effective伤害行) + Ammo/Conditions/Phrases 徽章 + 反向引用面板 + Sound语义图标 |
| AttackMode Overview | ✅ 缩略图 + Stats(含Morale百分比) + Ammo徽章 + Sound徽章 |

| 实体 | 旧编辑器(ICustomTableEditor) | 新 Detail | 新 Overview | 状态 |
|------|-----------|-----------|-------------|:--:|
| Recipe | Recipe Tree | 纯文本 | 纯文本 | ⚠️ 骨架就绪，需丰富 |
| Encounter | Story Graph | 纯文本 | 纯文本 | ⚠️ 骨架就绪，需丰富 |
| TreasureTable | Treasure Tree | 纯文本 | 纯文本 | ⚠️ 骨架就绪，需丰富 |
| ItemType | SpriteShow+WearShow | 卡片式（图片/画廊/Stat Bars/属性标签） | 窄高面板（图片/身份/Stats/属性/装备/引用卡片） | ✅ 完成 |
| AttackMode | EntityOverviewEditor | 卡片式（Hero Header + Combat Fieldset 进度条 + 弹药/条件/短语徽章） | 窄高面板（图片/身份/Stats/弹药/音效） | ✅ 完成 |
| Recipe | EntityOverviewEditor | 卡片式（Hero Header + 原料徽章面板 + 产品预览 + AlsoTry） | 类型标签+Stats卡(Hours/Reverse/Tools/Consumed) | ✅ 完成 |
| TreasureTable | EntityOverviewEditor | 卡片式（Hero Header + 战利品概率面板含物品名/概率徽章/数量） | 名称+Stats卡(OR组数/物品总数) | ✅ 完成 |
| Encounter | EntityOverviewEditor | 卡片式（图片Hero+剧情文本+回应+引用全徽章面板） | 图片缩略图+类型标签+Stats卡 | ✅ 完成 |
| Creature | EntityOverviewEditor | 卡片式（图片Hero+派系/攻击/状态/战利品全徽章面板） | 图片缩略图+Stats卡 | ✅ 完成 |
| Condition | EntityOverviewEditor | 卡片式（Hero+FieldNames→Modifiers三列表+效果+条件链徽章） | 严重级别徽章+Stats卡 | ✅ 完成 |
| BattleMove | EntityOverviewEditor | 卡片式（Hero+行为标签+文本面板+8组条件徽章） | 行为类型徽章+Stats卡 | ✅ 完成 |
| HexType | EntityOverviewEditor | 卡片式（Hero+光线等级六列表+战利品/营地/状态引用徽章） | 可通行标签+Stats卡 | ✅ 完成 |
| Faction | EntityOverviewEditor | 卡片式（Hero+外交关系横条面板+成员生物徽章） | 名称+Stats卡 | ✅ 完成 |
| Ingredient | EntityOverviewEditor | 卡片式（Hero+必需/禁止属性徽章+Recipe反向引用） | 名称+Stats卡 | ✅ 完成 |
| ItemProp | EntityOverviewEditor | 卡片式（Hero+反向引用徽章面板） | 属性名+ID标签 | ✅ 完成 |
| EncounterTrigger | EntityOverviewEditor | 卡片式（Hero+触发类型标签+日期/区域+遭遇/Hex引用徽章） | 触发类型徽章+Stats卡 | ✅ 完成 |
| CampType | EntityOverviewEditor | 卡片式（图片Hero+Stats卡+战利品引用） | 图片缩略图+Stats卡 | ✅ 完成 |
| ChargeProfile | EntityOverviewEditor | 卡片式（Hero+消耗率Stats卡） | 名称+降级标签+速率概要 | ✅ 完成 |
| ContainerType | EntityOverviewEditor | 卡片式（Hero+ItemType反向引用徽章） | 名称+ID标签 | ✅ 完成 |
| CreatureSource | EntityOverviewEditor | 卡片式（Hero+坐标/数量+生物引用徽章） | 名称+Stats卡 | ✅ 完成 |
| DmcPlace | EntityOverviewEditor | 卡片式（图片Hero+遭遇引用徽章） | 图片缩略图+Stats卡 | ✅ 完成 |
| BarterHex | — (新增) | 卡片式（Hero+Buy标签+坐标） | 商店类型标签+Stats卡 | ✅ 完成 |
| DataFile | — (新增) | 卡片式（图片Hero+数据内容文本面板） | 图片缩略图+价值标签 | ✅ 完成 |
| GameVar | — (新增) | 卡片式（Hero+类型/名称/值） | 名称+类型标签+Value | ✅ 完成 |
| Headline | — (新增) | 卡片式（Hero+报纸标题文本面板） | 名称+标题预览 | ✅ 完成 |
| ForbiddenHex | — (新增) | 卡片式（Hero+Forbidden标签+坐标） | 名称+Stats卡 | ✅ 完成 |
| Map | — (新增) | 卡片式（Hero+地图定义文本面板） | 名称+数据点数 | ✅ 完成 |

### 数据浏览器引用索引
| 功能 | 状态 |
|------|:--:|
| `EntityMergeStore` → `ReferenceIndex` 管道（与合并视图复用，`Index.BuildAsync()` 构建） | ✅ |
| `RebuildBrowserIndexAsync()` — 24 类型 + `ReferenceLookups` + `EntityModNames` + `Index.BuildAsync` | ✅ |
| 侧边栏 `Rebuild Index` 按钮（ArrowSync 图标） | ✅ |
| Mod/Profile 变更消息自动索引失效 | ✅ |
| `ReferenceResolver.LookupRef<T>()` — 走 `ReferenceIndex.Lookup` 统一解析（与 DataGrid 同源） | ✅ |
| ListBox 搜索过滤（`DomainBrowserView` TextBox） | ✅ |

### 导入导出
| 功能 | 状态 |
|------|:--:|
| Import Mod（文件夹/ZIP/拖拽）→ 自动打开 | ✅ |
| CSV/XLSX/MD/JSON 全导出 + DiffView 导航增强 | ✅ |

### 面板系统
| 面板 | 状态 |
|------|:--:|
| 左侧覆盖链（ToolDock，CanClose=False） | ✅ |
| 右侧 3 工具标签页（ValueEditor/ImagePreview/RefInspector） | ✅ |
| 底部 3 工具标签页（SearchResults/Conflicts/Validation） | ✅ |
| ToolDock 布局（嵌套 ProportionalDock，比例左1:中4:右2，底1） | ✅ |
| Search Tab 完善（ISearchService 接口 + 去重 + CancellationToken） | ✅ |
| 面板显隐 + 列宽布局持久化 | ✅ |

### 配置与基础设施
| 功能 | 状态 |
|------|:--:|
| FontSize 全局生效（Style DynamicResource，设置面板即时应用） | ✅ |
| GridRowHeight 即时生效（消息驱动） | ✅ |
| GameRootDir / Language / Theme / AutoSaveInterval 等 | ✅ |
| 三语言（en/zh/en-us）+ 字段说明 .docx 集成 | ✅ |
| Serilog 结构化日志 + ViewId 链路追踪 + 级别分层 | ✅ |
| 消息机制统一到 WeakReferenceMessenger | ✅ |
| GDH 去静态化 + MergeService + 命令序列化 + 服务接口补齐 | ✅ |
| IFilterService 接口提取 + DI 注册 | ✅ |
| GameDataTypeTabItem 拥有 EditTrackingStore / EntityMergeStore | ✅ |

---

## 二、新增架构（本次对话）

### IEntityVisualizer — 可视化接口

```
Helper/IEntityVisualizer.cs
  ├── BuildDetail(IEntity) → Control    // 数据浏览器详情视图
  └── BuildOverview(IEntity) → Control  // 合并视图简略概览

Services/EntityVisualizerRegistry.cs     // 注册表 + EF 代理类型兼容

Views/UserControls/Editors/EntityVisualizers.cs
  ├── DefaultEntityVisualizer      // 回退：属性树 / 紧凑字段列表
  ├── RecipeEntityVisualizer       // 配方详情
  ├── EncounterEntityVisualizer    // 剧情详情
  ├── TreasureTableEntityVisualizer // 战利品详情
  └── ItemTypeEntityVisualizer     // 物品详情
```

### DataBrowser — 数据浏览器文档体系

```
ViewModels/MainContent/Documents.cs
  └── EntityBrowserDocument : DocumentViewBase
      ├── ObservableCollection<BrowserEntityRow> Entities  // 实体列表
      └── ObservableCollection<EntityViewerTab> ViewerTabs // 查看标签页

Views/UserControls/DomainBrowserView.axaml + .cs
  ├── 左侧 280px: 实体 ListBox (DisplayName + EntityId)
  ├── GridSplitter
  └── 右侧: TabControl (可关闭 + 中键关闭)

DocumentWorkspaceView.axaml
  └── DataTemplate: EntityBrowserDocument → DomainBrowserView
```

### 接入点

| 展示位置 | 使用接口 | 方法 |
|---------|---------|------|
| 数据浏览器标签页 | IEntityVisualizer | BuildDetail(entity) |
| 合并视图可视化概览面板 | IEntityVisualizer | BuildOverview(entity) |

---

## 三、已知待解决问题

### 🚨 热修复需求
| 问题 | 说明 |
|------|------|
| ~~可视化器引用解析显示为纯文本~~ | ✅ Stage 20 修复：`FindByKey` 同 mod 优先 + 全局磁盘缓存索引 |
| ~~Ctrl+Click 导航总是跳转到 id=1~~ | ✅ 已修复 |
| ~~DataGrid 列索引映射偏移~~ | ✅ 已修复 |
| ~~多 DataGrid 实例竞争全局静态状态~~ | ✅ Phase 3 修复：INavigationRouter 替代 static _activeViews |
| ~~GDH 静态导航状态~~ | ✅ Phase 4 清理 |
| ModDatabase Expander 箭头遮挡文字 | 需调整 Padding |
| IMessenger.Send 单参数重载不可用 | CommunityToolkit.Mvvm 8.4.0 |
| 侧边栏 Import 按钮弹两次对话框 | 已移除 FilePicker 回退，待验证 |

### P0 — 当前焦点：可视化内容丰富（ItemType 详情+概览完成）

**当前 visualizer 是纯文本骨架，需要注入真正的可视化组件：**

| 实体类型 | 需要的可视化组件 |
|---------|----------------|
| **ItemType** | 物品图片展示（ImageList/SpriteList 加载 + ZoomableImageView），装备槽位预览（CreHuman + btn_inv_body 叠加），属性树（vProperties 解析） |
| **Recipe** | 配方流程图（工具→产物树状展开），成分图标+数量，战利品引用链接 |
| **Encounter** | 剧情文本（strDesc 富文本渲染），Response 树（LeadsFrom/To 权重可视化），Trigger 列表，条件/状态图标 |
| **TreasureTable** | 嵌套战利品树（递归展开 aTreasures），概率分布条，OR/AND 逻辑图 |
| **Creature** | 生物属性面板（攻击方式/状态/阵营），战利品引用，身体部位示意图 |
| **Condition** | 字段修饰配对表（aFieldNames ↔ aModifiers），持续时间/致命标记可视化 |
| **AttackMode** | 伤害类型饼图、射程指示器、穿透/暴击参数面板 |
| **BattleMove** | 战斗动作图标 + 条件条件树（UsPre/ThemPre 条件解析），效果描述 |
| **HexType** | 地形图标 + 移动消耗/能见度参数 + 战利品引用 + 营地关联 |
| **Faction** | 阵营图标 + 外交关系矩阵可视化 |
| **Ingredient** | 必需/禁止属性对比（RequiredProps vs ForbidProps） |
| **ItemProp** | 属性描述 + 反向引用（哪些物品使用了此属性） |
| **其余类型** | 至少展示图片（如有 Img字段）、关键引用跳转、属性概览 |

**每个 visualizer 应包含的元素：**
- 🖼️ 图片（通过 ImageService 搜索 + ZoomableImageView 展示）
- 🔗 引用字段（解析为可点击的链接，支持 Peek/跳转）
- 📊 关系图（树状/流程图/矩阵，参考现有 StoryTreeEditor/RecipeFlowchartEditor）
- 📋 关键属性面板（非全部字段，精选核心属性）

### P1 — 高优先级
| 问题 | 说明 |
|------|------|
| 非初始 Profile 合并视图保存 | 需验证 |
| 导出 diff 无差异时不提示 | 缺反馈 |
| 工具栏按钮 Padding 不一致 | Save 系列 vs 导航系列 |

### P2 — 中短期
| 问题 | 说明 |
|------|------|
| 图片处理管线化 | 拖入 → 像素化 → x2 → 注册 |
| 像素画手绘工具 | 画笔/橡皮擦/填充/取色/透明化 |
| 像素编辑器 ↔ ModImages 联动 | 双击打开 / 保存后注册 |
| Validation 详情展示 | 底部面板完整条目 |

### 本次对话已完成（Stage 17）
| 项目 | 说明 |
|------|------|
| ✅ 引用导航重构 Phase 3+4 | INavigationRouter 责任链 + GDH 清理 |
| ✅ DataGrid 行高稳定 | 每行独立计算高度，冻结后不再受虚拟化影响 |
| ✅ Apple tab 切换 NRE | SwitchTabItemsSource 先 AutoGenerateColumns=false |
| ✅ 列可见性全局配置 | ColumnVisibilityKeys + 侧边栏面板 + 双向同步 |
| ✅ ItemType Overview | 窄高面板完整可视化 |

### 本次对话已完成（Stage 20）
| 项目 | 说明 |
|------|------|
| ✅ 全 19 个现有 visualizer 重写 | Recipe ~ DmcPlace 全部从 TreeView 升级为 AttackMode 级 Card 模式 |
| ✅ 新增 6 个 visualizer | BarterHex / DataFile / GameVar / Headline / ForbiddenHex / Map |
| ✅ VisHelper 共享组件 | StatBar / BuildExpander / OvSectionLabel / BuildStatCard 提取到 VisHelper |
| ✅ AttackMode 清理 | 移除私有 StatBar / BuildExpander / OvSectionLabel 重复实现 |
| ✅ 引用解析修复 | `FindByKey<T>(key, sourceEntity)` — 同 mod 优先，不依赖 ReferenceIndex 就绪 |
| ✅ LookupRef fallback 修复 | 从 ReferenceField attribute 读取 pattern 提取 ID + 命名空间前缀处理 |
| ✅ 全局索引持久化 | `GDH.BrowserStore` + `GlobalBrowserCache` 磁盘缓存（`browser_index_cache.json`） |
| ✅ 索引构建时机 | 应用启动时 FireAndForget 构建，Profile/Mod 变更时 `InvalidateIndex` 清缓存 |
| ✅ EntityViewerView 异步等待 | `BuildContentAsync` await `EnsureIndexBuiltAsync()` 再渲染 visualizer |
| ✅ 字段标签订正 | Encounter Type (Normal/Scavenge), Condition (Instant/Duration, Color含义), BattleMove (StrId/Exposure) |
| ✅ 文档更新 | 20-data-class-field-reference.md, CHANGELOG, 09-current-status |

### P3 — 长期
| 问题 | 说明 |
|------|------|
| 冲突批量仲裁 | "全部采用 Mod A 的值" |
| 直接编辑 Raw XML + DataGrid 双面板同步 | |
| 单元测试覆盖 | 零覆盖 |
| ToolDock 集成 | ✅ 已完成（DocumentWorkspaceView 使用嵌套 ProportionalDock + ToolDock） |

---

## 四、架构说明（关键给下一轮对话）

### 事件总线
- 所有消息：`Data/Messages/` 目录下的 record/class
- 注册/发送通过 `ViewModelBase.Messenger`（`ObservableRecipient` 属性）
- ⚠️ IMessenger.Send 单参数重载可能不可用（CommunityToolkit.Mvvm 8.4.0），需用 `Send<TMessage, string>(msg, default!)` 或直接操作 DocumentWorkspaceViewModel

### 文档类型体系
```
IDocumentBase
  ├── DocumentBase (ObservableObject)
  │   ├── XmlDocument, XmlDiffDocument, ModGameDataDocument
  │   ├── MergeEditorDocument, PlainTextDocument
  │   └── ImageDocument
  └── DocumentViewBase (ViewModelBase)
      ├── MarkdownDocument, ModImagesDocument
      ├── ImageEditorDocument
      └── EntityBrowserDocument  ← 新增
```

### Visualizer 注册流程
```csharp
// App.axaml.cs OnFrameworkInitializationCompleted:
var vr = _host.Services.GetRequiredService<EntityVisualizerRegistry>();
vr.SetDefault(new DefaultEntityVisualizer(typeof(IEntity)));
vr.Register(new RecipeEntityVisualizer());
vr.Register(new EncounterEntityVisualizer());
// ... 需要为每种实体类型注册

// 使用时：
var vis = visualizerRegistry.Get(entityType);
var control = vis?.BuildDetail(entity);  // 或 BuildOverview(entity)
```

### 注意事项
1. EF Core 返回实体可能是代理类型，`EntityVisualizerRegistry.Get()` 已处理（向上遍历 BaseType）
2. `DocumentWorkspaceViewModel.ActivateDocument()` 已改为 public
3. 数据浏览器 CountEntitiesFast 使用同步 DbContext 枚举（同线程安全）
4. Sidebar Import 按钮：FolderPicker 取消后不再弹出 FilePicker（已移除回退逻辑）
5. **导航系统**：Ctrl+Click 走 `INavigationRouter.Navigate` → 责任链遍历已注册 `INavigationTarget` → `ModGameDataTabsView` 的 Priority=50。Peek 走 `INavigationRouter.PeekHandler`
6. **列可见性**：`ColumnVisibilityKeys` 是唯一 key 源，两边（设置面板 + DataGrid 列管理器）都通过它读写 `Config.ColumnVisibility`，增量 Add/Remove + `ColumnVisibilityChangedMessage` 实时同步
7. **数据浏览器引用索引**：`EntityBrowserDocument.RebuildBrowserIndexAsync()` 为全部 24 个类型创建 `EntityMergeStore`，填充 `ReferenceLookups` + `EntityModNames`，调用 `GDH.SetActiveStores(store, null)`。可视化器通过 `ReferenceResolver.GetDedupedInt<T>()` 读取，后者委托到活跃 store。在 `DataBrowserViewModel` 注册消息监听以在 mod/profile 变更时自动失效。
8. **统一引用解析**：`ReferenceResolver.FindByKey<T>(key, sourceEntity)` — 同 mod 优先 → 最高 ModId 兜底。查找链：`ReferenceLookups`（活跃 merge store）→ `GlobalBrowserCache`（磁盘持久化缓存）。终结了可视化器自己建字典去重的双路径问题。
9. **全局浏览器索引**：`GDH.BrowserStore` + `EntityBrowserDocument.GlobalBrowserCache` — 应用启动时从 DB 构建一次，序列化到 `%LocalAppData%/NeoEditor/browser_index_cache.json`。重启时从磁盘加载，毫秒级。仅 Profile/Mod 变更时重建。Merge 视图用自己的 `_activeMergeStore`，互不影响。
10. **索引构建等待**：`EntityViewerView` 在渲染 visualizer 前 `await EnsureIndexBuiltAsync()`，避免索引未就绪导致的引用解析失败。
