# NeoEditor Changelog

---

## Stage 23 — IReferenceResolver 接口化 + 可视化本地化 + ValueEditor Peek (v0.22.0-dev) | 2026-06-11

### IReferenceResolver 接口
| 项目 | 说明 |
|------|------|
| 新增 `Helper/IReferenceResolver.cs` | 定义正规引用解析接口：`LookupRef` / `LookupSubject` / `ReverseLookup` / `NavigateTo*` |
| `ReferenceResolver` 重写 | 从 static class → `class : IReferenceResolver`，有 `static Instance`，DI 注册为 singleton |
| ~80 处调用点 | 全部改为 `ReferenceResolver.Instance.xxx` |

### 删除的过时 API
| 删除 | 替代 |
|------|------|
| `FindByKey<T>()` | `LookupRef<T>()` |
| `GetDedupedInt<T>()` | 批量: `GDH.GetEntities<T>()`；单次: `LookupRef<T>()` |
| `GetDedupedComposite<T>()` | `GDH.GetCompositeEntities<T>()` |
| `GetDedupedList<T>()` | `GDH.GetDedupedEntities<T>()` |
| `FindReverseReferences()` (全量扫描 O(n*m)) | `ResolveReverseRefs(store, entityId)` (走 Index.ReverseLookup) |
| `ResolveSubject/ResolveMultiRef/CreateNavItem/WireNavOnCtrlClick` | 删除（零调用） |

### DataGrid ConfigureColumn 统一
| 之前 | 之后 |
|------|------|
| `LookupSubjectByRawId` 自建 30 行 → Index.LookupDisplay → FindBestMatch O(n) 兜底 | 一行委托 `ReferenceResolver.Instance.LookupSubject(...)`，纯 Index |

### ReferenceIndex 磁盘持久化
| 项目 | 说明 |
|------|------|
| `ReferenceIndex.SaveToDisk(path)` | 序列化全部字典（forward/nsForward/reverse/display/merged/bizKey）到 JSON |
| `ReferenceIndex.TryLoadFromDisk(path)` | 从 JSON 恢复，跳过昂贵 BuildAsync |
| `BrowserStore` null 修复 | `TryLoadFromDiskCache` 不再绕过 BrowserStore 创建 |
| `InvalidateIndex` 修复 | 同时删除轻量 cache + Index cache 两个文件 |

### 可视化本地化
| 项目 | 说明 |
|------|------|
| `VisHelper.Loc(key)` | 可视化专用本地化快捷方式，调用 `App.Localizor[key]` |
| 新增 ~30 个 `Vis.*` 资源键 | `Vis.RawData`, `Vis.Stats`, `Vis.Cut`, `Vis.Blunt`, `Vis.Total`, `Vis.Effective`, `Vis.Ammo`, `Vis.AttackerConditions`, `Vis.AttackPhrases`, `Vis.ReferencedBy`, `Vis.CombatMelee/Ranged`, `Vis.Tiles`, `Vis.Base`, 等 |
| `Resources.zh.resx` 翻译修正 | `Morale` → 士气补正；`Vis.AttackerConditions` → 攻击带来的状态 |

### Ctrl+Click Peek 到 ValueEditor
| 项目 | 说明 |
|------|------|
| `ReferenceResolver.NavigateTo` 现在附带 Peek | 调用 `GDH.PeekEntity(type, entityId)` → 发送 `VisualEditorRequestedMessage` |
| `ValueEditorPanel` 接收 | 渲染 `visualizer.BuildOverview(entity)` 到右侧面板 |
| `Router.Navigate` "not handled" 降级 | Warning → Debug（数据浏览器无 INavigationTarget 是正常情况） |

### AttackMode Detail UI 改进
| 改进 | 说明 |
|------|------|
| fMorale 百分比显示 | 公式 `(1+士气)*(1+加成)*伤害`，`fMorale=0.25` 显示 `25% (base)` |
| Effective 伤害行 | 士气加成后有效伤害：`(Cut+Blunt) × (1+fMorale)`，格式 `5.6 (1.25 × 4.5)` |
| Sound 语义图标 | 无图片时根据 Sound 分类显示对应 FluentIcon + emoji |
| 反向引用面板 | 使用 `store.Index.ReverseLookup()` 预建 `_reverse` 字典 |
| 引用徽章 Ctrl+Click | NavigateTo + Peek 到右侧 ValueEditor 面板 |
| 全部标签本地化 | `VisHelper.Loc(key)` 替换硬编码英文字符串 |

### 关键 Bug 修复
| Bug | 修复 |
|-----|------|
| **Detail 引用全部显示 raw 文本** | `LookupRef`/`LookupSubject`/`NavigateToByKeyFor` 只查 `ActiveMergeStore` → 改为 `ActiveMergeStore ?? BrowserStore` |
| **`_indexBuilt=true` 但 BrowserStore=null** | 重构 `RebuildBrowserIndexAsync`，Store 创建后才标记 |
| **`InvalidateIndex` 后每次全量重建** | ReferenceIndex 磁盘持久化 |
| **Ctrl+Click Peek 无反应** | `PeekEntity` 从 `Router.Peek` 改为发送 `VisualEditorRequestedMessage` |

### 文档更新
| 文档 | 更新内容 |
|------|---------|
| `15-reference-system-refactoring-plan.md` | Phase 5/6/7 实施记录 + Bug 记录表 B1-B5 + 过时章节标记 |
| `09-current-status.md` | 引用系统 Phase 1-7，IReferenceResolver 路径图 |
| `14-reference-resolution-system.md` | 新增 IReferenceResolver/ReferenceResolver 文件清单，FindBestMatch 兜底标记过时 |
| `20-data-class-field-reference.md` | fMorale 说明订正 |
| `21-entity-detail-ui-design-guide.md` | 本地化模式、引用解析规范 |
| `Resources.resx` / `Resources.zh.resx` | 新增 ~30 个 `Vis.*` 显示键 |

---

## Stage 22 — ReferenceResolver 清理 + 可视化器统一 LookupRef (v0.22.0-dev) | 2026-06-11

> 此阶段内容已被 Stage 23 包含并扩展，仅保留标题作为归档。



## Stage 21 — Detail UI 设计指南文档 (v0.22.0-dev) | 2026-06-10

### 新增文档
| 项目 | 说明 |
|------|------|
| `21-entity-detail-ui-design-guide.md` | Entity Detail UI 设计参考指南 |

### 文档内容
| 章节 | 涵盖 |
|------|------|
| 布局规范 (7 条规则) | ScrollViewer → Raw Data Expander → Hero Header → 面板优先级 |
| Hero Header 模式 (2 种) | 有图 / 无图两种 Header 布局，组件清单，图片加载逻辑 |
| 数据面板类型 (7 种) | StatBar / StatCard / MiniBadge / 文本面板 / 关系横条 / 配对表 / 反向引用 |
| MiniBadge 标准配色 | 12 种引用目标类型的 bg/fg 配色表 |
| Overview 设计规范 | 260px 窄高布局，组件排版顺序 |
| 引用处理规范 | 解析优先级、默认值跳过规则 |
| VisHelper API 清单 | 11 个共享组件的签名和用途 |
| 既定改进方案 (7 项) | P1 反向引用 → P6 Tooltip 预览 → P7 动作按钮 |
| 设计反模式 (10 条) | 避免用 TreeView 罗列、空面板占位、私有组件等 |
| 类型到面板映射 | 按数据特征选择面板类型的速查表 |
| 新增 Visualizer 清单 | 11 项检查列表 |

---

## Stage 20 — 引用解析修复 + 全局索引持久化 (v0.22.0-dev) | 2026-06-10

### 引用解析修复
| 项目 | 说明 |
|------|------|
| `ReferenceResolver.FindByKey<T>(key, sourceEntity)` | 新方法：同 mod 优先，最高 ModId 兜底，不依赖 ReferenceIndex |
| `LookupRef` fallback 修复 | 从 `ReferenceField` attribute 读取 pattern 提取 ID，处理命名空间前缀 |
| 全部 visualizer 切换 | `GetDedupedInt` → `FindByKey`，`NavigateToByKeyFor` → `NavigateTo(typeof(T), entityId)` |

### 全局浏览器索引持久化
| 项目 | 说明 |
|------|------|
| `GDH.BrowserStore` | 全局 static 单例 `EntityMergeStore`，应用启动时构建 |
| `EntityBrowserDocument.GlobalBrowserCache` | `Dictionary<Type, Dictionary<int, CacheEntry>>`，序列化到 `browser_index_cache.json` |
| 磁盘缓存 | `%LocalAppData%/NeoEditor/browser_index_cache.json`，重启毫秒级加载，无需 rebuild |
| `EnsureIndexBuiltAsync()` | 去重防并发，`EntityViewerView` 渲染前等待索引就绪 |
| `InvalidateIndex()` | 删除磁盘缓存 + 清内存，Profile/Mod 变更时触发 |

### 字段标签订正
| 类型 | 修正内容 |
|------|---------|
| Encounter | `Story` → `Normal`（符合 EncounterType 枚举） |
| Condition | `Permanent` → `Instant`（瞬时的/一次性施加），`Temporary` → `Duration`，Color 加正负面标注 |
| BattleMove | 新增 `StrId` 徽章，`See Us/Them` → `Exposure`，新增 `AI Order` |
| Recipe | Hero Header 新增 `DegradeOutput: On/Off` |
| Creature | Faction 名称解析（不再仅显示 #ID） |
| Encounter | 新增 `RemoveTreasureId` 引用面板 |

### 修改文件
| 文件 | 关键改动 |
|------|---------|
| `Helper/ReferenceResolver.cs` | 新增 `FindByKey<T>()` 返回 `(Subject, EntityId)?` |
| `Helper/GenericDataGridHelper.cs` | 新增 `BrowserStore`, `ReferenceLookups` 回退链 |
| `ViewModels/MainContent/Documents.cs` | `BrowserIndexCacheEntry`, `GlobalBrowserCache`, 磁盘缓存序列化 |
| `Views/.../EntityViewerView.axaml.cs` | 异步 `BuildContentAsync` 等待索引 |
| `Views/.../Editors/EntityVisualizers.cs` | 全部 `FindByKey` 调用点更新 |
| `App.axaml.cs` | 启动时 `FireAndForget(RebuildBrowserIndexAsync)` |

---

## Stage 19 — 全类型可视化器卡式重设计 (v0.21.0-dev) | 2026-06-10

### 可视化器全面升级
所有 25 个实体类型的 `BuildDetail` 和 `BuildOverview` 均按 AttackMode 的 Card 模式重写：

| 类型 | Detail | Overview |
|------|--------|----------|
| **Recipe** | Hero Header（名称+类型标签+Hours/Reverse）+ 原料徽章面板（Tools/Consumed/Destroyed）+ 产品预览 + AlsoTry 备选配方 | 类型标签+中心名称+Stats卡(Hours/Reverse/Hidden/Tools/Consumed) |
| **TreasureTable** | Hero Header（ID+Nested/Suppress/Identify标签）+ 战利品概率面板（每项含物品名/概率徽章/数量）| 中心名称+标签+Stats卡(OR组数/物品总数) |
| **Encounter** | Hero Header（图片+ID+剧情类型标签）+ 剧情文本面板 + 回应面板 + 引用面板（战利品/状态/前置条件/生物/传送/意外） | 图片缩略图+类型标签+名称+剧情摘要+Stats卡(Price/Type/Loot/Accident/Creature) |
| **Creature** | Hero Header（图片+ID+Moves标签）+ 派系/攻击方式/基础状态/遭遇状态/战利品/尸体战利品徽章面板 + 活动描述 | 图片缩略图+名称+公开名+Stats卡(Moves/Faction/Attacks) |
| **Condition** | Hero Header（ID+致命/永久/堆叠标签+持续时间/颜色/传染范围）+ 描述 + FieldNames→Modifiers 三列配对表 + 效果文本 + 下一阶段条件链徽章 | 严重级别徽章+名称+Stats卡(Duration/Color/Transfer)+下一阶段数 |
| **BattleMove** | Hero Header（ID+行为标签标志+类型/几率/优先级/疲劳/探测/范围/视野）+ 描述/成功/失败文本面板 + 全部条件组(8组)徽章面板 | 行为类型徽章+名称+Stats卡(Type/Chance/Priority/Fatigue/Detect/Range) |
| **HexType** | Hero Header（ID+可通行标签+移动消耗/能见度/遭遇范围）+ 光线等级六列表 + 战利品/营地/进入状态引用徽章面板 | 可通行标签+名称+Stats卡(Cost/Visibility/EncRange) |
| **Faction** | Hero Header（ID）+ 外交关系横条面板（名称+彩色关系条+数值+描述）+ 成员生物徽章面板 | 名称+Stats卡(关系数/成员数) |
| **Ingredient** | Hero Header（ID）+ 必需属性/禁止属性徽章面板 + 反向引用（哪些Recipe使用） | 名称+Stats卡(Required/Forbidden属性数) |
| **ItemProp** | Hero Header（ID+属性名）+ 反向引用（被哪些实体引用）徽章面板 | 属性名+ID标签 |
| **EncounterTrigger** | Hero Header（ID+触发类型标签+几率标签）+ 区域/日期范围 + 遭遇/HexType引用徽章面板 | 触发类型徽章+名称+Stats卡(Chance/Encounter) |
| **CampType** | Hero Header（图片+ID+容量标签）+ 营地Stats卡（Capacity/Alertness/Sleep/Heal）+ 战利品引用 | 图片缩略图+名称+Stats卡(Sleep/Heal/Visibility/Alertness) |
| **ChargeProfile** | Hero Header（ID+可降级标签+物品ID）+ 消耗率Stats卡（PerUse/PerHour/PerHrEquipped/PerHex）| 名称+降级标签+速率概要+物品ID |
| **ContainerType** | Hero Header（ID+名称）+ 反向引用（哪些ItemType使用） | 名称+ID标签 |
| **CreatureSource** | Hero Header（ID+坐标/数量标签+权重）+ 生物引用徽章面板 | 名称+Stats卡(Position/Count/Weight) |
| **DmcPlace** | Hero Header（图片+ID+坐标标签）+ 遭遇引用徽章面板 | 图片缩略图+名称+Stats卡(Position/Encounter) |

### 新增可视化器（6个此前无 visualizer 的类型）
| 类型 | Detail | Overview |
|------|--------|----------|
| **BarterHex** | Hero Header（ID+Buy标签+坐标+RestockTT）| 商店类型标签+名称+Stats卡(Position/RestockTT) |
| **DataFile** | Hero Header（图片+ID+价值标签）+ 数据内容文本面板 | 图片缩略图+名称+价值标签 |
| **GameVar** | Hero Header（类型标签+名称+值）| 名称+类型标签+Value |
| **Headline** | Hero Header（ID）+ 报纸标题文本面板 | 名称+标题预览 |
| **ForbiddenHex** | Hero Header（ID+Forbidden标签+坐标）| 名称+Stats卡(Position) |
| **Map** | Hero Header（ID+数据点数）+ 地图定义文本面板 | 名称+数据点数 |

### 共享组件
| 组件 | 位置 | 说明 |
|------|------|------|
| `VisHelper.StatBar` | VisHelper | 进度条组件（从AttackMode提取） |
| `VisHelper.BuildExpander` | VisHelper | 可折叠面板组件（从AttackMode提取） |
| `VisHelper.OvSectionLabel` | VisHelper | Overview章节标签（从AttackMode提取） |
| `VisHelper.BuildStatCard` | VisHelper | 键值对Stats卡片 |
| AttackMode 清理 | 移除私有 StatBar / BuildExpander / OvSectionLabel 重复实现 |

### 注册更新
- `App.axaml.cs` 新增 6 个 visualizer 注册：BarterHex / DataFile / GameVar / Headline / ForbiddenHex / Map

### 修改文件
| 文件 | 关键改动 |
|------|---------|
| `Views/.../Editors/EntityVisualizers.cs` | 19个现有 visualizer 全部重写为Card模式 + 6个新增 visualizer + VisHelper 共享组件 |
| `App.axaml.cs` | 注册 6 个新 visualizer |

---

## Stage 17 — 引用系统重构 Phase 3+4 + 列可见性 + 行高稳定 (v0.19.0-dev) | 2026-06-10

### 引用导航系统重构 (Phase 3 — 导航层)
| 项目 | 说明 |
|------|------|
| `INavigationTarget` | 导航目标接口：`CanNavigate` / `NavigateTo` / `Priority` |
| `INavigationRouter` | DI 单例路由器：`RegisterTarget` / `UnregisterTarget` / `Navigate` / `Peek` |
| `NavigationRouter` | 责任链实现，Priority 降序，稳定排序，同 Priority 下最近附加优先 |
| `ModGameDataTabsView` 实现 `INavigationTarget` | Attach 注册 / Detach 注销，Priority=50，CanNavigate 检查 Tab 匹配 |
| `DocumentWorkspaceViewModel` | PeekHandler 从 GDH 静态委托迁移到 `INavigationRouter.PeekHandler` |

### 引用导航系统重构 (Phase 4 — GDH 清理)
| 项目 | 说明 |
|------|------|
| 移除 `_activeViews` / `RegisterNavigateTarget` | 替代为 `INavigationRouter.RegisterTarget` |
| 移除 `PeekRequested` 静态委托 | 替代为 `INavigationRouter.PeekHandler` |
| 移除 `IsPeekPinned` / `NavigateToImpl` | 不再需要 |
| `NavigateToReferenceForce` 改为委托路由器 | 解析 EntityId → Router.Navigate + Router.Peek |
| `NavigateTo` / `NavigateToByEntityId` 保留 | 改为通过路由器+索引查找，供外部调用者使用 |

### DataGrid 改进
| 项目 | 说明 |
|------|------|
| 行高虚拟化抖动修复 | `OnLoadingRow` 中每行独立计算高度（基于多值引用段数），直接设 `row.Height`，绕过列虚拟化测量 |
| 列虚拟化关闭 | `SearchableDataGrid.axaml` 加 `EnableColumnVirtualization="False"`（11.3 不支持，已移除） |
| `SwitchTabItemsSource` NRE 修复 | 大数据量切 tab 时 DataGrid 内部 `RemoveAutoGeneratedColumns` NRE — 先 `AutoGenerateColumns=false` 再设 ItemsSource，延迟恢复 |
| Mod 列 `SortMemberPath` | 补上 `SortMemberPath = "Mod"`，使列管理器能保存/恢复其可见性 |

### 列可见性全局配置
| 项目 | 说明 |
|------|------|
| `ColumnVisibilityKeys` | 统一数据源：`GetKeys(entityType)` 返回全部列 key（实体属性 + ModId/FilePath/EntityId + MergedId + Mod） |
| 侧边栏设置面板 | `Expander "Column Visibility"` + 每表 Expander + CheckBox 列表 + All/None 按钮 |
| 双向实时同步 | 两边都是增量 Add/Remove → 发送 `ColumnVisibilityChangedMessage` → DataGrid 收到即时更新 |
| 默认全可见 | 不再是 "默认隐藏 ModId/FilePath/EntityId"，全部列默认可见 |
| 移除硬编码 hiddenProps | DataGrid `OnAutoGeneratingColumn` 改用 `ColumnVisibilityKeys.IsVisible()` |

### ItemType Overview 可视化
| 项目 | 说明 |
|------|------|
| 重写 `BuildOverview` | 适配窄高面板 (~260px)：居中 88px 缩略图 + 身份 + Stats 两列网格 + Properties 标签 + Equipment / Container / Degrade / Refs / ReverseRefs 卡片 |

### 新增文件
| 文件 | 说明 |
|------|------|
| `Helper/INavigationTarget.cs` | 导航目标接口 |
| `Helper/INavigationRouter.cs` | 导航路由器接口 |
| `Services/NavigationRouter.cs` | 导航路由器实现 |
| `Helper/ColumnVisibilityKeys.cs` | 列可见性统一 key 源 |

### 修改文件
| 文件 | 关键改动 |
|------|---------|
| `Helper/GenericDataGridHelper.cs` | 移除静态导航状态，委托给 Router |
| `Views/.../SearchableDataGrid.axaml` / `.cs` | 列可见性配置恢复、行高冻结、合成列支持 |
| `Views/.../ModGameDataTabsView.axaml.cs` / `Tab.cs` | 实现 INavigationTarget、ToggleColumnVisibility 增量更新 |
| `ViewModels/.../DocumentWorkspaceViewModel.cs` | PeekHandler 迁移到 Router |
| `ViewModels/.../SettingsPaneViewModel.cs` | 列可见性配置 + TableColumnGroup/ColumnOption |
| `Views/.../Pane.axaml` | Column Visibility Expander + All/None 按钮 |
| `Views/.../Editors/EntityVisualizers.cs` | ItemType BuildOverview 重写 |
| `App.axaml.cs` | 注册 INavigationRouter DI |
| `ViewModels/.../ReferenceInspectorContent.cs` | 移除 IsPeekPinned 引用 |
| `Data/Messages/AppConfigMessages.cs` | 新增 ColumnVisibilityChangedMessage |

### 已知限制
- .NET 10.0 SDK 未安装，本次改动无法本地编译验证（用户侧 Rider 编译通过）
- `PersistColumnVisibility` 已改为增量 `ToggleColumnVisibility`，旧方法保留但不再调用

---

## Stage 16 — ItemType 卡片式可视化 + 数据浏览器三层结构 (v0.18.0-dev) | 2026-06-06

### 数据浏览器三层结构
| 项目 | 说明 |
|------|------|
| 侧边栏：大类 + 数据类 | DataBrowser 恢复为纯 Domain→EntityType 按钮，点击在 Dock 开标签页 |
| Dock 标签页：ListBox + 查看区 | `EntityBrowserDocument` 内含左 ListBox（实体列表）+ 右查看区 |
| 实体查看：独立 Dock 文档 | `EntityViewerDocument` + `EntityViewerView` 渲染，`EntityVisualizerRegistry.BuildDetail()` |

### 新增文件
| 文件 | 说明 |
|------|------|
| `EntityViewerView.axaml` + `.cs` | 实体可视化 UserControl，接收 `EntityViewerDocument`，调用 `BuildDetail()` |
| `Documents.cs` | 新增 `EntityViewerDocument : DocumentViewBase` |

### ItemType Detail 卡片式重设计
| 区域 | 内容 |
|------|------|
| Hero Header | 左：132px 主图 + 多图时可切换画廊（◀ ▶ 圆点指示器）；右：ID 徽章 + 名称 + 显示名 + 鉴定名（橙色提示框） |
| Stat Bars | 水平进度条：Weight / Stack / Durability / Value + Mirrored，Grid 星号比例列防文字裁剪 |
| Property Tags | ItemProp 引用解析 → 绿色圆角徽章，Ctrl+Click 跳转 |
| Equipment Card | EquipSlots 徽章 + 装备/使用/携带 Condition 引用解析 |
| Container Card | Capacities + FormatId + ContentIds 解析 |
| Degrade / Charge Cards | 磨损参数 + 破损掉落 TreasureTable 引用 + ChargeProfile 引用 |
| Reference Bars | 横向链接条显示 resolved Subject（TreasureTable/Condition/Component），Ctrl+Click 导航 |
| Reverse Refs | 列出引用本 ItemType 的其他实体，`[类型名] subject` 格式 |

### 图片逻辑
- 字段含逗号 → list → 始终显示画廊组件（含 ◀ ▶ 切换）
- 字段不含逗号 → 单值 → 直接 ImageView

### 编译修复
| 问题 | 修复 |
|------|------|
| `BottomToolsView` / `DataBrowserView` AXAML `ElementName` 绑定 `DataContext` 丢失类型 | 新增 typed 属性（`SearchRecentTyped` / `OpenEntityTypeTyped`），AXAML 改 `#Root.xxxTyped` |
| `EntityViewerView` 缺少 `ScrollBarVisibility` | 补充 `using Avalonia.Controls.Primitives` |
| `Documents.cs` `Id` 赋值 | 移除不存在的 `Id` 属性赋值 |
| WrapPanel `Spacing` | Avalonia 不支持，改用 `Padding` 实现间距 |
| `Math.Clamp` 实例调用 | 改为静态调用 `Math.Clamp(value, min, max)` |

### 已知限制
- **嵌套 Dock**：`DomainBrowserView` 内嵌 `DockControl`（左ListBox + 右Dock查看器）始终无法渲染。尝试方案包括 `InitializeFactory`/`InitializeLayout`/inline layout/DI Factory 注入/`ElementName` 绑定等，在 `Dock.Avalonia 11.3.11.16` 版本上均失败。当前退回 `TabControl` 方案保持功能可用。
- 拆分对比通过主 Dock 标签页拖拽实现（同一类型开两个 `EntityBrowserDocument`）

---
  
## Stage 15 — UI 重塑 + 数据浏览器 + 可视化架构 (v0.17.0-dev) | 2026-06-06

### UI 改进
| 项目 | 说明 |
|------|------|
| FontSize 全局生效 | App.axaml 添加 `AppFontSize` DynamicResource + Window Style；设置面板修改即时应用 |
| 工具栏图标统一 | 导航/操作按钮 Unicode → FluentIcons（ArrowUndo/ArrowRedo/ArrowLeft/Target/Add/Subtract） |
| 面板切换图标 | MainWindow 面板切换 Unicode(◀▶▼) → FluentIcons(PanelLeft/Right/Bottom) |
| HomePage 图标 | 表情符号(📖✨📥) → FluentIcons(BookOpen/DocumentAdd/ArrowDownload) + CardButton 样式 |
| Recent Mods | 移除硬编码 IsVisible=False，绑定 HasRecentMods |
| NumericUpDown | int/float/double 编辑用 NumericUpDown 替代 TextBox，提取 CreateEditControl |
| GridRowHeight 即时生效 | GridRowHeightChangedMessage 驱动，SearchableDataGrid 监听即时更新 |
| 合并视图空状态 | 移除 !IsMergeView 限制 |
| 侧边栏重设计 | 48px 固定宽度、Background 背景、三组分隔、FontSize=18 图标 |
| Import 简化 | 只弹 FolderPicker，取消后不再弹 FilePicker |

### 数据浏览器（新建）
| 项目 | 文件 |
|------|------|
| GameDomain — 7 领域分组 | `Helper/GameDomain.cs` |
| DataBrowserViewModel — 侧边栏领域→实体类型树 | `ViewModels/ExplorerPane/DataBrowserViewModel.cs` |
| DataBrowserView — 侧边栏面板 | `Views/UserControls/DataBrowserView.axaml` + `.cs` |
| EntityBrowserDocument — Dock 标签页文档 | `ViewModels/MainContent/Documents.cs` |
| DomainBrowserView — 标签页视图：左实体列表 + 右可视化 TabControl | `Views/UserControls/DomainBrowserView.axaml` + `.cs` |
| 侧边栏集成 + DataTemplate 注册 | `MainWindow.axaml`, `MainWindowSideBarViewModel.cs`, `DocumentWorkspaceView.axaml` |

### 可视化架构（新建）
| 项目 | 文件 |
|------|------|
| IEntityVisualizer 接口 | `Helper/IEntityVisualizer.cs` |
| EntityVisualizerRegistry | `Services/EntityVisualizerRegistry.cs` |
| 5 个 Visualizer 实现 | `Views/UserControls/Editors/EntityVisualizers.cs` |
| ValueEditorPanel 集成 | 优先用 visualizer.BuildOverview，回退 CustomEditorRegistry |

### Search Tab
| 项目 | 文件 |
|------|------|
| ISearchService 接口提取 + CancellationToken | `Services/ISearchService.cs`, `SearchService.cs` |
| BottomToolsViewModel + SearchPaneViewModel 去重 | 两个 ViewModel 统一注入 ISearchService |

### Encounter 叙事编辑器
| 项目 | 说明 |
|------|------|
| StoryTreeEditor 完全重写 | 4 标签页：Story Flow（左树+右详情编辑）、Text Editor（叙事文本编辑）、Overview、Flowchart |
| EncounterTrigger 集成 | 详情面板显示 "Triggered By" |

### 架构提升
| 项目 | 文件 |
|------|------|
| IFilterService 接口独立文件 + DI | `Services/IFilterService.cs` |
| GameDataTypeTabItem 拥有 stores | `ViewModels/MainContent/GameDataTypeTabItem.cs` |
| ActivateDocument → public | `DocumentWorkspaceViewModel.cs` |

### 本地化
| Key | 中文 | 英文 |
|-----|------|------|
| DataBrowserTitle | 数据浏览器 | Data Browser |
| DataBrowserProperties | 属性 | Properties |
| DomainCoreItems/Combat/Crafting/Loot/Story/Map/Other | 核心物品/战斗/合成/战利品/剧情/地图/其他 | ... |
| RightPanelEditor | 可视化概览 | Overview |
| ValueEditorTitle | 可视化概览 | Visual Overview |

### 新增文件（本轮 ~12 个）
`GameDomain.cs`, `DataBrowserViewModel.cs`, `DataBrowserView.axaml` + `.cs`, `DomainBrowserView.axaml` + `.cs`, `IEntityVisualizer.cs`, `EntityVisualizerRegistry.cs`, `EntityVisualizers.cs`, `ISearchService.cs`, `IFilterService.cs`

### 已知限制
- IMessenger.Send 单参数重载不可用（CommunityToolkit.Mvvm 8.4.0），EntityBrowserDocument 绕过 Messenger 直接操作 DocumentWorkspaceViewModel
- Visualizer 内容为纯文本骨架，需要注入图片/引用树/图表等真正可视化组件（详见 10-next-priority-plan.md）

---

## Stage 14 — 架构重构 (v0.16.0-dev) | 2026-06-06

> 依据 `Docs/13-architecture-critique.md` 执行

### Phase A: 止血

| 项目 | 说明 |
|------|------|
| A1 消息统一 | 14 个静态事件/Action 迁移到 `WeakReferenceMessenger`，新增 15 个消息 record。`IMessenger` 改为 Singleton |
| A2 GDH 去静态化 | `SearchableDataGrid` 持有 `MergeStore`/`EditStore`，挂载时推送。移除 11 个 `_fallback*` 后备集合。`PushEditStateToGrid` 显式调用 `SetActiveStores` |
| A3 链路追踪 | `ViewModelBase` 新增 `ViewId` Guid + `IdPrefix` |
| A4 日志分层 | `LoadingRow`/`CellEditEnd`/`PushEdit`/`ModFilter` 等 12 处降级为 `LogDebug` |
| A5 异步异常 | `AsyncHelper.FireAndForget()` 替换 17 处 `_ = AsyncMethod()` |

### Phase B: 解耦

| 项目 | 说明 |
|------|------|
| B1 MergeService | 200 行合并算法从 `ReloadMergeTabsAsync` 提取到 `Services/MergeService.cs`，返回不可变 `MergeResult` |
| B2 命令序列化 | `ISerializableCommand` 接口，4 命令类型自序列化。`BatchEditCommand` 用 `EditRecord` 替换 `ValueTuple`。`CommandSerializer` 零反射 |
| B3 服务接口 | `IXmlParser` / `IImageService` / `IFilterService` / `IMergeService` 接口 + DI 注册 |
| B4 拆分 View | `GameDataTypeTabItem` 提取到 `ViewModels/MainContent/GameDataTypeTabItem.cs` |
| B5 Console.WriteLine | 18 处替换为结构化 Serilog 日志 |

### Bug 修复

| Bug | 修复 |
|-----|------|
| 标签页切换重新加载 | `OnPropertyChanged` 中比较 `ModId`/`ProfileId` + `Tabs.Count > 0` |
| 行背景消失 | `CellEditEnding` 同步更新 `SearchableDataGrid._editedEntityIds` |
| ShowAll / 覆盖数据显示 | `RebuildFilteredItemsSources` 后更新 `SharedDataGrid.ItemsSource`；直接清除 `MergeStore`/`EditStore`；`PushEditStateToGrid` 显式调用 `SetActiveStores` |
| TabControl 内容区纯文本 | 添加 `TabControl.ContentTemplate` 含空 `Panel` |

### 移除

- `DepBtn` / `ConflictBtn` 及其所有相关代码（反复出现的空白 bug，无法根除）
- `ConflictDisplayText` / `ConflictCount` 属性
- `OnShowDependenciesClick` / `OnShowConflictsClick` 方法
- `UpdateConflictButtonStyle` 方法

### 新增文件
| 文件 | 说明 |
|------|------|
| `Data/Messages/ModGameDataMessages.cs` | 8 个 merge-view 消息 |
| `Data/Messages/GridInteractionMessages.cs` | 6 个 DataGrid 交互消息 |
| `Helper/AsyncHelper.cs` | Fire-and-forget 安全包装 |
| `Data/Command/ISerializableCommand.cs` | 命令自序列化接口 |
| `Data/Command/EditRecord.cs` | 替换 ValueTuple 的命名结构 |
| `ViewModels/MainContent/GameDataTypeTabItem.cs` | 从 View 提取的 Tab VM |
| `Services/MergeResult.cs` | 不可变合并结果 |
| `Services/MergeService.cs` | 合并算法服务 |

### 修改文件（~20 个）
主要涉及：`ModGameDataTabsView.axaml` + `.cs`、`GenericDataGridHelper.cs`、`DocumentWorkspaceViewModel.cs`、`SearchableDataGrid.axaml.cs`、`App.axaml.cs`、`CommandSerializer.cs`、4 个 Command 类、`FilterService.cs`、`ImageService.cs`、`XmlParser.cs`、`PhpParser.cs`、`ViewModelBase.cs`、`RightPanelView.axaml.cs`、`BottomToolsView.axaml.cs`、`Pane.axaml.cs`、`FindReplacePanel.axaml.cs`、`SettingsPaneViewModel.cs`、`ModIndexViewModel.cs`、`HomePageViewModel.cs`、`ConfigService.cs`、`ModManager.cs`、`ModEntryDropHandler.cs`、`Documents.cs`、`ValueEditorPanel.axaml.cs`

### Stage 14 补充修复 (2026-06-06)

| Bug | 修复 |
|-----|------|
| TreasureTable aTreasures: `582x.01x1`/`596x.04x1` 无法解析 | `ParseSingle` 中 `LastIndexOf('x')` → `IndexOf('x')`，正确提取多段 x 格式的第一个 ID |
| aTreasures Ctrl+Click/PeeK 未尝试 SecondaryTarget | 多值和单值 Ctrl+Click/Peek 查询 `SecondaryTargetEntityType`（TreasureTable）fallback；新增 `ResolveWithSecondary` 统一查找 |
| Ctrl+C/V 编辑模式无效 | 全局 KeyDown handler 检测编辑中的 TextBox → 放行原生复制/粘贴；新增 `IsEditingTextBoxFocused` 辅助方法 |
| `UpdateConflictButtonStyle` 残留调用编译错误 | Stage 14 移除 ConflictBtn 后遗留的 3 处调用已清理 |
| 文件丢失恢复 | 误用 `git checkout` 导致丢失未提交改动，已从 Rider Local History 完整恢复 |

### 文件拆分
`ModGameDataTabsView.axaml.cs` 从 3063 行拆分为 4 个 partial class 文件：
| 文件 | 行数 | 职责 |
|------|:--:|------|
| `ModGameDataTabsView.axaml.cs` | 1224 | 构造函数、属性、导航、键盘、复制粘贴、查找面板、Workspace Persistence |
| `ModGameDataTabsView.Operations.cs` | 489 | 保存管道、实体 CRUD、CSV 导入导出、FindReferences |
| `ModGameDataTabsView.Tab.cs` | 367 | Tab 管理、生命周期、列管理器、属性变更 |
| `ModGameDataTabsView.Data.cs` | 1109 | 数据加载、合并视图加载、过滤器、依赖分析、XML 工具 |

| 文档 | 更新 |
|------|------|
| 09-current-status.md | 完全重写，移除重复内容；更新版本 v0.16.0-dev；新增架构说明和文件拆分信息 |
| 10-next-priority-plan.md | 完全重写；标记已完成项（Save & Launch、XML 直接打开、DiffView、ModGameDataTabsView 拆分）；重组优先级 |

---

## Stage 13 — Snapshot + Command Log 持久化 (v0.15.0-dev) | 2026-06-06

### 架构：DB 定位为透明持久化缓存

**DB 不再是用户交互面，而是辅助层：**
- game.db 负责持久化编辑中的更改、加速加载与合并计算
- editor.db 新增 `command_log` + `workspace_snapshot` 表，存储编辑历史和快照指针
- 用户不直接感知 DB，操作以 XML 为中心
- ModDatabase 面板保留但降级为辅助视图

### Snapshot + Command Log 系统
| 功能 | 说明 |
|------|------|
| Command 持久化 | 每个 EditCell/AddEntity/DeleteEntity/BatchEdit 命令执行后实时写入 `command_log` |
| Periodic Snapshot | 每 N 步（`AppConfig.SnapshotInterval`，默认 10）全量写 game.db + 更新 snapshot 指针 |
| Quick Save = Snapshot | Ctrl+S 保存到 DB 后自动更新 snapshot 指针 |
| Save & Export 清理 | 完整保存（DB + XML）后清除 snapshot + command_log（XML 成为权威数据源） |
| 崩溃恢复 | 重载时 Load game.db → 重放 snapshot 之后的 command_log → 完整恢复未保存编辑 |
| Undo/Redo 恢复 | 重放命令同时重建 undo/redo 栈 |

### 命令序列化 (`CommandSerializer`)
- `EditCellCommand` / `AddEntityCommand` / `DeleteEntityCommand` / `BatchEditCommand` ↔ JSON
- 实体属性全量序列化（反射遍历 `[Column]` 属性），类型安全反序列化（`ValueConverter`）
- 重放时通过 EntityId + 实体类型查找实体、通过 Tab 类型查找集合

### DB 迁移
- `RunEditorDbMigrations()` — 启动时 `CREATE TABLE IF NOT EXISTS`，兼容已有 `editor.db`
- `command_log`：id, target_type, target_id, sequence, command_type, serialized_data, is_unsaved, created_at
- `workspace_snapshot`：id, target_type, target_id, last_command_sequence, created_at
- 索引：`(target_type, target_id)` + unique snapshot index

### 缓存恢复架构修复（合并视图标签页切换数据保持）

**问题**：Dock.Avalonia 切换标签页时会重建 View 实例，合并视图每次做完整 DB 重载，编辑数据丢失。

| 修复 | 说明 |
|------|------|
| `TabSnapshotCache` 覆盖合并视图 | 之前只给单 Mod 用，现在两者统一用缓存做纯内存恢复，不碰 DB |
| `EntityMergeStore.MergeSpaceModIds` | 合并空间 ModId 集合从 View 私有字段移入 Store，跟缓存一起走 |
| 缓存 store 替换 | 命中缓存时 `EditStore = cached.EditStore; MergeStore = cached.MergeStore` 替换 View 字段，消除多 View 并发 store 竞争 |

**行背景色丢失修复**：
- `SearchableDataGrid` 新增自有属性 `EditedEntityIds` / `OverriddenEntityIds` / `NewEntityIds`，解耦 `GenericDataGridHelper` 全局状态
- `PushEditStateToGrid()` 在 `LoadingRow` 触发前推送到 DataGrid 本地属性
- `RefreshRowBackgrounds()` 主动遍历行重设背景
- `OnAttachedToVisualTree` 重挂载路径用 `DispatcherPriority.Loaded` 延迟刷新

### 日志增强
- Serilog rolling file 改为小时级（`RollingInterval.Hour`，保留 72 个文件）
- `GenericDataGridHelper.SetActiveStores` 加入 store hash 和内容追踪日志
- `SearchableDataGrid` 注入 `ILogger`，所有 `LoadingRow` / `CellEditEnding` / `RefreshRowBackgrounds` 追踪日志写入文件
- 调试状态栏：工具栏下方显示 `Snap:seq=N | CmdLog:N | Unsv:N | Seq:N`，点击弹出详细 command_log 列表

### 新增文件
| 文件 | 说明 |
|------|------|
| `Data/Model/CommandLog.cs` | command_log 表实体 |
| `Data/Model/WorkspaceSnapshot.cs` | workspace_snapshot 表实体 |
| `Services/CommandSerializer.cs` | 命令 ↔ JSON 序列化/反序列化（4 种命令类型 + 实体属性全量） |
| `Services/WorkspacePersistenceService.cs` | Snapshot + Command CRUD（`IWorkspacePersistenceService` 接口） |

### 修改文件
| 文件 | 关键改动 |
|------|---------|
| `Data/Context/EditorDbContext.cs` | +2 DbSet + OnModelCreating 配置 + 索引 |
| `Data/Command/ICommandHistory.cs` | + `RestoreFromLog()` |
| `Data/Command/CommandHistory.cs` | + `OnCommandPersist` 回调 + `RestoreFromLog` (跳过 Execute) + `TrimHistory` 提取 |
| `Services/EntityMergeStore.cs` | + `MergeSpaceModIds` |
| `ViewModels/AppConfig.cs` | + `SnapshotInterval` (默认 10，0=关闭) |
| `App.axaml.cs` | + `RunEditorDbMigrations` + `IWorkspacePersistenceService` DI 注册 |
| `Helper/GenericDataGridHelper.cs` | `SetActiveStores` 加追踪日志 |
| `Views/UserControls/SearchableDataGrid.axaml.cs` | + `EditedEntityIds` / `OverriddenEntityIds` / `NewEntityIds` 属性 + `RefreshRowBackgrounds` + ILogger 注入 |
| `Views/UserControls/SearchableDataGrid.axaml` | 无改动 |
| `Views/UserControls/ModGameDataTabsView.axaml` | + 调试状态栏 Border + TextBlock |
| `Views/UserControls/ModGameDataTabsView.axaml.cs` | 核心集成：Command 持久化、Snapshot 周期、缓存恢复、行背景修复、Store 替换、错误处理 |
| `Program.cs` | Rolling 改为 Hour，retained 72 |
| `Helper/Extensions/LoggingExtensions.cs` | Rolling 改为 Hour，retained 72 |

### 已知限制
| 问题 | 状态 |
|------|:--:|
| 多 View 并发挂载 store 竞争 | ✅ 已通过缓存 store 替换修复 |
| 行背景色切换标签页丢失 | ✅ 已通过解耦属性 + 主动刷新修复 |
| 排序箭头不显示 | 🔴 Avalonia 11.3 框架限制 |

---

## Stage 12 — 高性价比快速迭代 (v0.14.0-dev) | 2026-06-05~06

### P0-1: Save & Launch Game
| 功能 | 说明 |
|------|------|
| Save & Launch 按钮 | 工具栏 [▶ Launch] 按钮，先保存再启动 NEOScavenger.exe |
| Ctrl+Shift+S | 快捷键触发 Save & Launch |
| 路径推导 | 从 `AppConfig.GameRootDir` + `NEOScavenger.exe` 自动拼接 |

### P0-2: DiffView 导航增强
| 功能 | 说明 |
|------|------|
| 双编辑器跳转 | 通过 `IsFocused` 判断焦点在新/旧编辑器，用对应行号查找 diff |
| 直接输入 index | 导航栏 TextBox 输入数字 → Enter/LostFocus 跳转 |
| 展示优化 | 导航栏 `#/total` 格式 |

### P0-3: XML 直接打开
| 功能 | 说明 |
|------|------|
| Import 支持 XML 文件 | FolderPicker → 取消后自动 FilePicker(*.xml) |
| Drop 已有处理 | 拖 XML 文件取父目录为 modPath |

### 保存流程重构
| 功能 | 说明 |
|------|------|
| Quick Save (Ctrl+S) | 仅写 DB，秒级完成，不弹 diff |
| Export 按钮 | 写 DB + XML diff 预览 + 确认写盘 |
| ▶ Launch (Ctrl+Shift+S) | Export + 启动游戏 |
| 自动保存 | 改为 Quick Save（不弹 diff） |
| Export 取消不写 DB | `ExportEntitiesToXmlAsync` 从内存实体生成 diff → 用户确认后才 `SaveToDatabaseAsync` + 写 XML |

### Game 数据只读保护
| 功能 | 说明 |
|------|------|
| BeginningEdit 拦截 | `SearchableDataGrid.CanEditEntity` 钩子 → 合并视图中 Game 实体（ModId=-1）双击无反应 + 弹出引导通知 |
| 通知内容 | "游戏基础数据不能直接修改。要修改游戏数据，请在 Profile 中添加 Merge 模式 Mod（strModName=0）" |

### 合并视图数据加载
| 功能 | 说明 |
|------|------|
| Profile 打开时预加载 | `DocumentWorkspaceViewModel.Receive` 改为 `async void`，同步解析 getmods.php → 导入/加载 mod → 填充 ModLoadInfos |
| ReloadMergeTabsAsync 兜底 | 如果 modEntries 为空 → 直接从 game.db 查询所有 ModId>0 → 构建 synthetic entries |
| Merge view 不使用 TabSnapshotCache | 仅单 Mod 视图使用内存缓存；合并视图始终从 DB 重载 |

### Tab 切换数据保持
| 功能 | 说明 |
|------|------|
| 单 SharedDataGrid | 移除 `TabControl.ContentTemplate` 中的 SearchableDataGrid，改用单个 `SharedDataGrid` |
| 切换不改数据 | `OnTabChanged` 只改 `SharedDataGrid.ItemsSource`，DataGrid 不重建 |
| FilterText 提取 | TextBox 移到 TabControl 上方，单例存在，切换 Tab 不清空 |

### 工具栏 UI 重设计
- 四组布局：导航(U/G/Redo | Back/Locate) | 操作(+/-/ColumnManager) | 合并(Deps/Conflicts/Filter/ShowAll) | 保存(Quick Save/Export/Launch)
- 统一 `Padding="8,4"`，ConflictBtn 加 `MinWidth="72"`
- ConflictBtn Content 改为 code-behind 直接设置（移除 AXAML 绑定防空白）

### Bug 修复
- **MergeXmlExportDialog 按钮空白**: `DataContext = this` 缺失 → 添加
- **ConflictBtn 空白**: 移除 AXAML Content 绑定，code-behind 直接设置
- **ConflictBtn 点击区域**: `Padding="6,2"` → `"8,4"`
- **DiffView 跳转只对左边生效**: 改为焦点检测 `NewEditorControl.IsFocused`
- **保存后脏状态未清除**: 各保存路径增加 `RefreshActiveDataGrid()` 调用
- **ModLoadInfo 无 Path 属性**: 改为 `modLoad.Info.Path`

### 新增本地化键
`QuickSave`, `QuickSaveTooltip`, `SaveAndExport`, `SaveAndExportTooltip`, `Saving`, `SaveTooltip`, `SaveAndLaunchTooltip`, `Launch`, `DiffJumpToCursor`, `DiffJumpToIndex`, `GameDataReadOnly`, `GameDataReadOnlyMessage`

### 新增文件
- `Docs/10-next-priority-plan.md` — 下一阶段优先级规划 + DB 架构定位

### 修改文件
`ModGameDataTabsView.axaml` + `.cs`, `XmlDiffView.axaml` + `.cs`, `SearchableDataGrid.axaml` + `.cs`, `HomePageViewModel.cs`, `DocumentWorkspaceViewModel.cs`, `MergeXmlExportDialog.axaml.cs`, `Resources.resx` (×3)

### 已知待解决问题
| 问题 | 状态 |
|------|:--:|
| Tab 切换后数据仍被重置 | 🔴 已改为 SharedDataGrid 架构但问题依旧存在，需进一步排查 |
| 非初始 Profile 合并视图无法保存 | 🟡 已添加 AutoLoad + DB 兜底，需验证 |
| 排序箭头不显示 | 🔴 Avalonia 11.3 框架限制 |
| 像素画手绘工具 | 🔴 未实现 |
| 批量编辑 | 🔴 未实现 |
| 新实体创建向导 | 🔴 未实现 |

---

## Stage 1 — 单Mod数据编辑 (v0.2.0-dev) | 2026-05-23

### 新增功能
| 功能 | 说明 |
|------|------|
| 单元格编辑 | 双击进入编辑模式，类型适配：bool→CheckBox、Enum→ComboBox、longtext→多行TextBox、int/float/string→TextBox |
| 行增删 | 工具栏 `+`（ID 自动递增）、`-`（删除选中行） |
| 保存闭环 | 编辑 → Diff 预览（左=磁盘原始 / 右=待提交）→ 确认 → 写入 neogame.xml + 更新 game.db |
| IsDirty 追踪 | `ModGameDataDocument.IsDirty` |
| 教程导入 | Help 菜单「导入教程…」：导入 .md/.png/.jpg 到 Help 目录 |

### 修改
- `GenericDataGridHelper`：扩展 CellEditingTemplate
- `GameDataTypeTabItem.ItemsSource`：`IEnumerable` → `ObservableCollection<object>`
- `ModGameDataTabsView`：Add/Delete + 保存逻辑 + TabControl `x:Name="DataTabs"`

### Bug 修复
- **Enum 导出**：`ConditionColor.Green` → `2`（Enum 转底层 int）
- **浮点数导出**：避免科学计数法 → 十进制格式
- **新增行**：继承已有数据的 `FilePath`，避免独立成一个文件
- **ID 排序**：int 键值 `D10` 左补零 → `1,2,10` 而非 `1,10,2`
- **空字符串跳过**：导出跳过 `""` 列（游戏约定"未设置"）
- **保存顺序**：先 DB 后磁盘，DB 失败不写盘
- **Delete 按钮**：`CanDeleteRow` 默认值 → `true`
- **ClearMods**：修复 dangling else → NullReferenceException
- **DeleteMods**：IsBase 保护 + try-catch
- **ModManager**：IsBase 检查拒绝 + data/ 路径保护
- **GameDbContext**：`GetMethod("Set", Type.EmptyTypes)` 消歧 AmbiguousMatchException

### 新增本地化键
`DiffOldLabel`, `DiffNewLabel`, `AddRow`, `DeleteRow`, `ImportTutorial`

---

## Stage 2 — 引用系统 (v0.3.0-dev) | 2026-05-24

### 新增功能
| 功能 | 说明 |
|------|------|
| `[ReferenceField]` attribute | 标记引用字段 + 目标实体类型 |
| `ReferenceHelper` | `ParseReference()` / `FormatForDisplay()` 去掉 `0:` 前缀 |
| 引用列样式 | Teal 色下划线（区别于选中行浅蓝高亮） |
| 右键跳转 | 「跳转到 {目标表}」→ 自动切换 Tab + 定位匹配行 |
| ← 返回 | 导航历史栈，跳转后可返回 |
| ComboBox 编辑 | 引用列双击 → 下拉 `"id: 名称"`，选中自动提取 ID |
| ReferenceLookups | 跨表查询字典（当前 Mod 实体填充） |

### 标注字段 (16个)
| 实体 | 字段 → 目标 |
|------|-----------|
| Creature | `TreasureId`→TreasureTable, `Faction`→Faction, `CorpseId`→TreasureTable |
| Recipe | `TreasureId`→TreasureTable |
| ItemType | `nCondID`→Condition, `nTreasureID`→TreasureTable, `nFormatID`→ContainerType, `nComponentID`→ItemType |
| HexType | `nTreasureID`→TreasureTable, `nDefaultCampID`→CampType |
| EncounterTrigger | `nEncounterID`→Encounter |
| CreatureSource | `nCreatureID`→Creature |
| DmcPlace | `nEncounterID`→Encounter |
| Encounters | `nTreasureID`→TreasureTable |
| CampType | `nTreasureID`→TreasureTable |

### Bug 修复
- **Stage 1 隐藏 bug**：`OnAutoGeneratingColumn` 回退到运行时类型 → CheckBox/ComboBox/多行编辑 首次生效
- 引用列对齐：`HorizontalAlignment=Stretch` + `VerticalAlignment=Center` + `Background=Transparent`
- 跳转失败提示：表未加载 / ID 未找到 分别通知
- 自定义列对齐：统一 `VerticalAlignment=Center` + `Margin(4,0)`
- **多实例导航**：静态 `OnNavigateRequest` 改用 `RegisterNavigateTarget` + `WeakReference` 查找

### 新增文件
`Helper/ReferenceFieldAttribute.cs`, `Helper/ReferenceHelper.cs`, `Helper/OverlayChainEntry.cs`, `Helper/Converter/OverlayChainConverter.cs`

### 新增本地化键
`NavigateBack`, `GoToReference`, `NoRowSelectedMessage`, `RefTargetNotLoaded`, `RefTargetNotFound`

### 已知限制
- 多值引用字段（`aAttackModes`, `vProperties`）→ 后续处理
- `<?xml...?>` 声明 diff → 后续处理

---

## Stage 3 — 合并视图 (v0.4.0-dev) | 2026-05-24

### 入口与数据流
- Profile 列表右键 → 「打开合并视图」`OpenMergeEditorMessage` → `MergeEditorDocument` → `ModGameDataTabsView`（通过 `ProfileInfo` 属性驱动）
- 入口判断：`OnSavePreviewButtonClick` 检查 `ProfileInfo is not null` → 走合并视图 Save 流，否则走单 Mod Save 流
- 数据加载：`ReloadMergeTabsAsync` 从 game.db 加载所有 Mod + Game 实体 → 按 Phase 1/2 合并 → 构建覆盖链 → 创建 DataGrid 视图

### 合并规则引擎（`ReloadMergeTabsAsync`）
1. **Phase 1**：Game 基础数据（ModId=-1） 打底入 `mergedDict[key]`
2. **Phase 2**：按 getmods.php 加载顺序逐层处理：
   - Merge Mod（strModName=0）：同 key 覆盖 `mergedDict[key]`
   - Insert Mod（strModName≠0）：追加到 `insertedList`，不同命名空间永不覆盖
3. **败者检测**：所有不在 `mergedDict.Values ∪ insertedList` 中的实体标记为 overridden → 存入 `_overriddenEntityIds` + `GenericDataGridHelper.OverriddenEntityIds`

### 合并自增 ID（→Id 列）
- **算法**：`mergeSpaceIds` = Game + Merge mod 实体的 EntityId 集合
  - 整数 key 类型：merge 空间实体 `mergedId = 自身 key`；insert 空间实体 `mergedId = max(mergeKeys) + 1` 起顺序自增
  - 非整数 key（如 GameVar）：全部顺序自增 1,2,3...
- **存储**：`GenericDataGridHelper.EntityMergedIds` — `Dictionary<EntityId, int>`
- **获取**：`GetEntityMergedId(entity)` — 返回 int，没找到返回 0
- **DataGrid 列**：`→Id` 列动态插入，仅在 `EntityMergedIds.Count > 0`（合并视图）时可见
- **默认排序**：合并视图按 mergedId 升序排列

### 双模式切换
- `Show All` ToggleButton（仅合并视图可见，通过 `IsMergeView` 绑定 `IsVisible`）
- **Mode 1（默认）**：仅胜者，CV Filter 排除 `_overriddenEntityIds`
- **Mode 2（Show All）**：全部数据，败者浅灰底 `rgb(200,200,200)`
- 切换通过 `RebuildFilteredItemsSources` → `DataGridCollectionView.Filter` 赋值 + `Refresh()` 实现

### 架构决策（重要）

#### DataGridCollectionView 的使用
- 合并视图：`ItemsSource = new DataGridCollectionView(visibleItems)` — CV 包裹预过滤集合，CV 提供排序能力
- 单 Mod 视图：`ItemsSource = items`（plain `ObservableCollection`）— DataGrid 自己创建内部 CV
- **为什么不用 CV.Filter**：CV 的 Filter 属性在多轮调试中表现不可靠，改用预过滤集合方案（先过滤再包裹）
- **排序原理**：`Sorting` 事件手动提取/排序/替换 ItemsSource（见下方"排序机制"）

#### GameDataTypeTabItem 设计
- 继承 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`（确保 Avalonia 编译绑定正确识别 `PropertyChanged`）
- `SourceCollection`（`ObservableCollection<object>`）：完整未过滤数据，增删操作修改此集合
- `ItemsSource`（`IEnumerable`）：绑到 DataGrid，setter 触发 `SetProperty` → `PropertyChanged` → DataGrid 重绑
- `IsMergeView`（DirectProperty）：控制 ShowAll 按钮、覆盖链面板的可见性

#### ModInfo 时间戳
- `LastImport`：`LoadModAsync` 调用时更新（XML → DB 同步时间）
- `LastModified`：`SaveToDatabaseAsync` 调用时更新（编辑器 → DB 保存时间）
- `IsDirty`（计算属性）：`LastModified > LastImport` → DB 有未导出的改动
- `DatabaseGeneratedOption.Computed` 已移除，改为手动设置

### 覆盖链 (Overlay Chain)
- **链节点**：`OverlayChainEntry(ModName, Id, EntityType, EntityId)` — EntityId 支持精确导航
- **构建规则**（Phase 2 中）：
  - 仅 Merge mod（strModName=0）加入 `keyOverlayHistory`
  - Insert mod 实体不参与覆盖链（不同命名空间），始终显示为独立链
  - Game 条目仅在 base 实体存在时添加（不再创建虚假 `[?]` 条目）
- **过时实体覆盖链**：在败者检测后补充填充 `EntityModNames` + `OverlayChainDisplay`
- **导航**：链节点点击 → `NavigateToByEntityId(type, entityId)` 精确跳转

### 导航系统
- **EntityId 导航**（合并视图）：`NavigateToByEntityId` → 按 EntityId 在 DataGrid 中匹配
- **业务 key 导航**（单 Mod 视图 / 引用列）：`NavigateTo` → 按 key 属性匹配
- **返回栈**：`_navHistory` 栈追踪跳转路径，`←` 按钮可用时返回上一位置
- **跳转到过时实体**（非 ShowAll）：阻止跳转 + 通知 "Enable Show All to navigate to it"
- **跳转到自身**：通知 "Already at this entity"
- **ShowAll 自动打开**：`GenericDataGridHelper.OnShowAllRequest` 静态回调，当前未被覆盖链触发（仅声明，覆盖链导航改为阻止 + 通知）

### 排序机制
- **问题**：Avalonia DataGrid 替换自定义列后不设置 `SortMemberPath`，且 CV 的 `SortDescriptions` 机制不稳定
- **解决**：
  - `ConfigureColumn` 中所有自定义列显式设置 `SortMemberPath = e.PropertyName`（含 `??=` 回退）
  - `OnSorting` 事件中手动排序：提取所有 item → `List.Sort` 按反射读取属性值排序 → `DispatcherPriority.Background` 延迟替换 `MainGrid.ItemsSource`
  - 延迟替换的原因：DataGrid 内部 `ProcessSort` 在 `Sorting` 事件后异步执行，直接替换 ItemsSource 会导致 `NullReferenceException`
- **局限性**：排序箭头（列头 ↑↓）不显示——因为绕过了 DataGrid 内部 CV 的 `SortDescriptions` 机制
- **方向切换**：同列点击翻转升序/降序，换列默认升序

### 保存机制
- **合并视图 Save**（`ShowMergeSavePreviewAsync`）：
  - 收集所有 `ModId > 0` 的实体（不过滤败者——败者也回存到源 Mod）
  - 按 entity type 分组 bulk upsert 到 game.db
  - 更新受影响的 `ModInfo.LastModified`
  - **不写 XML**——XML 导出是独立步骤（后续实现）
- **单 Mod Save**：保持原有 Diff + 写 XML 流程

### 工具栏布局
```
[←] [+] [-]              [status]                    [Show All] [Save]
```
- `IsMergeView` 绑定 `Show All` 按钮可见性
- `ShowRowDetails`（覆盖链面板）仅在合并视图时 `VisibleWhenSelected`，单 Mod 为 `Collapsed`

### XML 编码兼容
- 游戏 XML 文件使用 `encoding="utf8"`（缺少连字符），.NET 不识别
- `ModManager.LoadXmlFile()` 和 `ModGameDataTabsView.LoadXmlSafe()` 在 `XDocument.Parse` 前替换 `"utf8"` → `"utf-8"`

### Bug 修复清单（本阶段）
| # | 问题 | 根因 | 修复 |
|---|------|------|------|
| 1 | 败者一直可见 | `GameDataTypeTabItem` 无 `PropertyChanged`，ItemsSource 替换静默丢失 | 继承 `ObservableObject`，`SetProperty` 触发通知 |
| 2 | 覆盖链跳转到错误实体 | 按业务 key 导航，多 Mod 共享同 key | 改为按 EntityId 精确匹配 |
| 3 | 败者漏判 | `HashSet<IEntity>` 引用相等 | 改用 `HashSet<string>` + EntityId |
| 4 | 覆盖链包含 Insert mod | Insert mod 实体不应参与覆盖链 | Phase 2 仅 Merge mod 加入 `keyOverlayHistory` |
| 5 | 覆盖链过时实体无数据 | 仅 winners 填充 `EntityModNames` | 败者检测后补充填充 |
| 6 | 虚假 `[?]` Game 条目 | base 不存在时仍创建空 EntityId 的 Game 条目 | 仅在 base 存在时添加 |
| 7 | ViewLocator 崩溃 | `GameDataTypeTabItem` 被 `INotifyPropertyChanged` 宽泛匹配 | Match 缩窄为仅 `ViewModelBase` + `IDockable` |
| 8 | 排序不工作 | `SortMemberPath` 未被 DataGrid 设置（事件后置）+ CV SortDescriptions 不稳定 | 手动替换列时设 `SortMemberPath`，`Sorting` 事件手动排序 + 延迟替换 |
| 9 | 排序崩溃 | 直接替换 ItemsSource 导致 `ProcessSort` NRE | 延迟到 `DispatcherPriority.Background` 替换 |
| 10 | 导航到过时实体无提示 | 检查在 `targetItem` 之后，CV Filter 已隐藏 | 在搜索前通过 EntityId 检查 `OverriddenEntityIds` |

### 新增文件
- `Helper/Converter/EntityMergedIdConverter.cs` — EntityId → 合并 ID 值转换器
- `Helper/Converter/OverlayChainConverter.cs`（Stage 2 创建，Stage 3 修改导航逻辑）

### 修改文件摘要
| 文件 | 关键改动 |
|------|---------|
| `ModGameDataTabsView.axaml.cs` | 合并加载、过滤、CV 管理、排序、导航、保存 |
| `ModGameDataTabsView.axaml` | 工具栏三列 Grid 布局、`IsMergeView` 绑定 |
| `SearchableDataGrid.axaml.cs` | `→Id` 列管理、排序逻辑、`ShowRowDetails` 属性 |
| `SearchableDataGrid.axaml` | `Sorting` 事件 |
| `GameDataTypeTabItem`（同文件内类） | `ObservableObject` 基类、`IEnumerable ItemsSource`、`SourceCollection` |
| `GenericDataGridHelper.cs` | `EntityMergedIds`、`GetEntityMergedId`、`NavigateToByEntityId`、`SortMemberPath` |
| `OverlayChainEntry.cs` | `EntityId` 属性 |
| `OverlayChainConverter.cs` | EntityId 优先导航 |
| `ModInfo.cs` | `LastModified`/`LastImport` 取消 Computed、`IsDirty` |
| `ModManager.cs` | `LoadXmlFile` 编码修复、`LastImport` 更新 |
| `ViewLocator.cs` | Match 缩窄 |
| `App.axaml.cs` | 移除 `BindingPlugins`（Avalonia 11.3 API 变更） |
| `Resources.resx` | 新本地化键 |

### 新增本地化键
`OpenMergeEditor`, `MergeEditorTitleFormat`, `NavigateSameEntity`, `NavigateToOverriddenRequiresShowAll`

### 新增字典（GenericDataGridHelper 静态属性）
- `EntityMergedIds` — `Dictionary<string, int>`，EntityId → 合并自增 ID
- `EntityModNames` — `Dictionary<string, string>`，EntityId → 来源 Mod 名称
- `OverriddenEntityIds` — `HashSet<string>`，败者 EntityId 集合
- `OverlayChainDisplay` — `Dictionary<string, List<OverlayChainEntry>>`，EntityId → 覆盖链

### 当前已知限制
1. **排序箭头不显示**：Avalonia 11.3 `DataGridSortDescription` 抽象类
2. **列头排序只支持简单属性**：不支持嵌套路径
3. **Avalonia 版本锁定 11.3.x**

---

## Stage 4 — 保存/导出重构 + 体验增强 (v0.5.0-dev) | 2026-05-24~27

### 保存流程重构
- **Save 按钮统一只写 DB**：单 Mod / 合并视图一致，不再自动 Diff + 写 XML
- **Export XML 独立按钮**：从 DB 加载 → 对比磁盘 → `MergeXmlExportDialog` → 确认写回
- **通用导出方法** `ExportXmlAsync(List<ModInfo>)`

### `+` 新增行弹窗（`AddRowDialog`）
- Target Mod（仅 Insert mod）、Target XML（按 Mod 过滤+绝对路径）、Copy From（Subject 显示）
- 新增行浅绿背景、ID=目标 Mod 内最大+1、自动重算 mergeId+排序+跳转

### 编辑体验
| 功能 | 说明 |
|------|------|
| Loading 遮罩 | `OnAttachedToVisualTree` + `IsLoading` → 半透明遮罩 |
| 脏关闭拦截 | `SetDirty` → `MergeViewDirtyChanged` → 单 Mod + 合并视图均有确认弹窗 |
| Tab 脏标记 | `Header` 加 `*` 后缀 |
| Cell 编辑高亮 | `CellEditEnding` → 浅黄背景 `rgb(255,255,220)`，即时生效 |
| 列宽固定 | int=80, float=90, bool=70, enum=120, string=160, longtext=280, ref=160 |
| ◎ 定位按钮 | `ScrollIntoView` 聚焦选中行 |
| 标签页切换保护 | 合并视图 Tab 切换缓存恢复；单 Mod 有未保存修改时缓存恢复，无修改重新加载 |
| ☰ 列管理器 | 工具栏按钮弹出当前 Tab 列清单，勾选/取消即时显隐 |

### Subject 属性
- `IEntity.Subject`：反射查找 `strName`/`Name` 等；覆盖链、Copy From、引用 ToolTip 均使用

### MergeId 计算
- Merge 空间（Game + `strModName=0`）= business key；Insert 空间 = `max(mergeKeys)+1` 顺延
- `IEntity.MergedId` + `SortMemberPath` → →Id 列可排序

### 搜索与过滤
| 功能 | 说明 |
|------|------|
| 搜索框 `col:value` 语法 | `strName:Water` 按列过滤；`Water Bottle` 全文字段搜索；双引号分组 |
| 列名辅助输入 `?` | 按钮弹出当前 Tab 可用列名列表，点击自动插入 `col:` |
| Mod 过滤 ComboBox | 合并视图工具栏：All Mods / Game / 各 Mod 名称，按 ModId 筛选行 |
| 防抖 | 200ms debounce，加载中不触发 |

### 字段级来源标记 + 冲突检测
| 功能 | 说明 |
|------|------|
| `FieldSources` 字典 | `(EntityId, ColName) → ModName`，合并加载时逐字段比较记录来源 |
| `FieldConflicts` | 两个不同 Merge Mod 修改同一字段时标记 |
| Cell ToolTip | 悬停单元格 → `Source: [ModName]` 或 `⚠ CONFLICT` |
| 冲突高亮 | 冲突字段浅红背景 `rgb(255,220,220)` |

### 引用导航重构
| 功能 | 说明 |
|------|------|
| `ReferenceFieldAttribute` 扩展 | `IsMultiValue` + `MultiValueFormat`（CommaList / IdMultiplier / IdAssignment） |
| 多值引用渲染 | 按分隔符拆分为独立元素，各自 Ctrl+Hover/Ctrl+Click |
| Ctrl+Hover | 悬停引用值 → ToolTip 显示 `EntityType: Subject (id=X)` |
| Ctrl+Click | 按住 Ctrl 点击单值或多值引用 → 直接跳转到目标实体 |
| 右键菜单保留 | 单值引用直接跳转；多值列出全部解析引用（>25 折叠），每项显示 Subject |
| 复杂字段标注 | 新增 20+ 字段标注（Encounters/Creature/HexType/Recipe/ItemType/AttackMode/Faction/Ingredient/TreasureTable） |

### 合并视图行为
| 规则 | 实现 |
|------|------|
| 合并视图打开 → 所有单 Mod 视图只读 | `DocumentWorkspaceViewModel` 自动设置 |
| 关闭合并视图 → 恢复可编辑 | 检测合并视图关闭后恢复 |
| 只能打开一个合并视图 | 打开新合并视图自动关闭旧的 |

### Profile 拖拽排序
- `EditProfileView` DataGrid 已集成 `ContextDragBehavior` + `ContextDropBehavior` + `ModEntryDropHandler`
- 补充缺失的 `xmlns:dd` 声明，拖拽排序现已可用

### Bug 修复
- 新增行覆盖链 `?` → 正确显示 Mod 名称
- Tab 切换后空白 → `DebounceFilter` 加载中不触发 + 移除手动 `DataGridCollectionView` 包装 + `OnAttachedToVisualTree` 强制刷新
- 单 Mod 修改在 Dock 切换后丢失 → 有未保存修改时存缓存、恢复时从缓存加载
- `EditProfileView` 拖拽行为命名空间缺失 → 补充 `using:` 声明
- 引用 ToolTip 去掉 `Ctrl+Click → ` 前缀

### 新增文件
`Views/Dialog/AddRowDialog.axaml` + `.cs`、`Views/Dialog/MergeXmlExportDialog.axaml` + `.cs`、`Helper/Converter/FieldSourceConverter.cs`、`Helper/Converter/FieldConflictBackgroundConverter.cs`

### 修改文件
`IEntity.cs`、`OverlayChainEntry.cs`、`ReferenceFieldAttribute.cs`、`ReferenceHelper.cs`、`XmlParser.cs`、`OverlayChainConverter.cs`、`GenericDataGridHelper.cs`、`ModGameDataTabsView.axaml` + `.cs`、`SearchableDataGrid.axaml` + `.cs`、`DocumentWorkspaceViewModel.cs`、`EditProfileView.axaml` + `.cs`、9 个 Model 文件（参考字段标注）、3 个 Resources.resx

### 新增本地化键
`ShowAll`, `ExportXml`, `ExportXmlTooltip`, `Loading`, `LocateRowTooltip`, `SearchHelpTooltip`, `SearchHelpButtonTooltip`, `SearchHelpAvailableColumns`, `ModFilterAll`, `ColumnManagerTooltip`, `ColumnManagerHeader`

### 当前已知限制
1. **排序箭头不显示**：Avalonia 11.3 `DataGridSortDescription` 抽象类
2. **Avalonia 版本锁定 11.3.x**
3. **TreasureTable `aTreasures`**：`|` OR 分隔符未兼容（comma 分隔的 AND 部分正常工作）
4. **Recipe `strTools`/`strConsumed`**：`1x2+1x3` 格式（`+` 分隔符）未标注

---

## Stage 5 — 体验夯实 + 引用系统重构 (v0.6.0-dev) | 2026-05-27~28

> 方向修正依据：[04-stage5-analysis.md](04-stage5-analysis.md)
> 核心思路：先让已有功能好用，再扩展新功能。

---

### 已完成功能

#### Undo/Redo 命令系统
- `Data/Command/IEditorCommand` + `EditCellCommand` / `AddEntityCommand` / `DeleteEntityCommand` + `CommandHistory`（上限 100 步）
- 工具栏 `↩` `↪` 按钮；`Ctrl+Z` / `Ctrl+Y` 全局快捷键
- AddRow / DeleteRow / 单元格编辑均纳入 Undo 栈
- 重载数据时自动清空历史

#### 字段帮助系统
- 列头 Tooltip 优先查 `*Desc` 本地化键（`RangeDesc` → "攻击距离，1 for melee..."），fallback 到 `[Display]` 短名
- 修改 `GenericDataGridHelper.ConfigureColumn` 的 tooltip 查找逻辑

#### 引用系统重构
- **`ReferenceFieldAttribute` 重新设计**：新增 `Separator`（null=单值, `,`/`&`/`|`=多值）、`Pattern`（`{id}`/`{id}x{mult}`/`{id}={value}`）、`TargetKey`（`{Id}` 默认 / `{GroupId}.{SubgroupId}` ItemType 复合键）
- 移除 `MultiValueFormat` 枚举；`IsMultiValue` 改为 `Separator is not null` 计算属性
- 45 处 `[ReferenceField]` 标注批量更新，4 处 ItemType 引用加 `TargetKey = "{GroupId}.{SubgroupId}"`
- Ingredient `RequiredProps`/`ForbidProps` 分隔符从 `,` 改为 `&`（游戏数据 `16&amp;46` → `16&46`）

#### 引用显示增强
- 单值 `{Subject} (id=N)` 替代裸ID；负数 `~Subject`（条件取反，如 Condition 取反）；`LookupSubjectByRawId` 使用 TargetKey 复合键匹配
- 多值每段解析 Subject；`FormatSegmentDisplay` 处理 `{id}`/`{id}x{mult}`/`{id}={value}` 三种 pattern
- 两级分隔符：CellTemplate 检测 `|`/`,` + 显示 `or`/`+` 连接；右键菜单展开所有子项
- `ExtractRawId` 按 Pattern 提取 ID：`{id}={value}` → `=` 前、`{id}x{mult}` → 第一个 `x` 前（非最后一个，TreasureTable 多段 x 格式）
- `DecomposeId` 先剥离命名空间前缀 `NSE:`，复合键无分隔符 fallback 用 `"Id"` 键
- `FindBestMatch()` 选 ModId 最大（覆盖链胜者），防止引用指向被覆盖实体；Subject 搜索增加 `PropertyName`/`strPropertyName`

#### ModName 列 & 元数据列
- 多 Mod 视图新增只读 `Mod` 列（`ModNameColumnConverter` 从 `EntityModNames` 查）；单 Mod 自动隐藏
- `ModId` / `FilePath` / `EntityId` 默认 `IsVisible=false`（列管理器可恢复）

#### AddRow 拆分
- `+` 按钮 → `AddRowDialog.ShowSimpleAsync` 仅选 Mod+XML（无 Copy From）
- 右键行 → "Clone Row" → 直接拷贝全字段+ID自增（`skipDialog=true`）

#### 列可见性持久化
- `AppConfig.ColumnVisibility`：`Dictionary<string, HashSet<string>>`（表名 → 可见列集合）
- 列生成时 `ApplyColumnVisibilityConfig()` 按表名匹配；列管理器勾选即时写入 `config.json`

#### 反向引用查询
- 右键行 → "Find references to this..." → 扫描所有加载 Tab 的 `[ReferenceField]` → 弹窗显示引用者列表（含表名、字段名）

#### FindReplace 悬浮面板
- `Ctrl+F` 打开搜索 / `Ctrl+H` 打开替换；同模式再次按关闭
- 右上角悬浮面板：搜索框、匹配计数、`^` `v` 导航、`Aa`（大小写）/`ab`（全词）/`.*`（正则）Toggle
- Enter 跳下一个匹配、Escape 关闭（全局按键，不依赖焦点）；`ScrollIntoView` + 选中行
- Replace 替换当前 / Replace All 全部替换（反射写入实体属性）
- 左侧 4px 拖拽柄调整面板宽度（最小 200px）
- 按钮使用 FluentIcons `SymbolIcon`（`ArrowUp`/`ArrowDown`/`Dismiss`）
- 面板默认关闭（`IsVisible="False"`），高度紧凑

#### CSV 导出（方法保留，按钮已移除）
- `OnExportCsvClick` / `OnImportCsvClick` 方法实现完整（CSV 解析含引号转义、列名匹配 `[Column]` 属性、`ConvertValue` 类型转换、ID 自增）
- 工具栏按钮已移除 — 后续迁移到 ModDatabase 面板（Stage 6）

#### 历史搜索栏移除
- 原搜索 TextBox + `?` 列名助手 + `col:value` 语法废弃
- `FilterText` / `DebounceFilter` / `OnSearchHelpClick` 代码保留但 UI 移除

---

### Bug 修复清单

| 问题 | 根因 | 修复 |
|------|------|------|
| Ctrl+Z/Y UI 不刷新 | IEntity 未实现 INotifyPropertyChanged，反射设值后 DataGrid 不感知 | undo/redo 后 `RefreshActiveDataGrid()` ItemsSource 重绑 |
| 引用负数 ID 显示 `[class]` | `LookupSubject(int)` 负数查 `_subjectCache` 永远 miss，fallback 到 `$"[{TypeName}] {keyVal}"` | `LookupSubject` 用 `Math.Abs(id)`；`~Subject` 前缀 |
| ShowAll 关不掉 | `Click` handler 读到 ToggleButton 旧 `IsChecked` 值，绑定与 handler 冲突 | 移除 `IsChecked` 绑定；Click handler `ShowAllEntities = !ShowAllEntities` + 手动 `ShowAllToggle.IsChecked = ShowAllEntities` |
| 跳转到被覆盖实体 | `LookupSubject` 返回第一个匹配，可能是败者 | `FindBestMatch` 选最高 ModId；导航加 `ScrollIntoView` + `Background` 延迟居中 |
| ExtractRawId 多段 `x` 截错 | `LastIndexOf('x')` 对 `55.4x0.75x1` 返回 `55.4x0.75` | `IndexOf('x')` 取第一个 `x` → `55.4` |
| DecomposeId 不处理命名空间前缀 | `"NSE:86.6"` → `int.TryParse("NSE:86")` 失败 → GroupId=0 | 先剥离 `NSE:` 前缀再解析 → `86.6` |
| 无 `.` 的复合键 ID 找不到 | `"418"` fallback 用 GroupId=418，但 ItemType 主键是 `id` 字段 | fallback 改用 `"Id"` 键 → `Id=418` |
| FindPanel 按钮空白 | 固定 `Width="24" Height="24"` + Avalonia 默认 Padding(12,4) → 内容空间为 0 | 去除宽高，用 `Padding="2,0"` + FontSize 11 |
| FindPanel 关不掉 | 自定义 `new IsVisibleProperty` 覆盖 `Control.IsVisible` → `IsVisible=false` 不影响视觉 | 移除自定义属性，直接用 `base.IsVisible` |
| FindPanel 拖拽比例不对 | `GetPosition(this)` 返回 UserControl 相对坐标，面板宽度变化时坐标系偏移 | `GetPosition(null)` 屏幕绝对坐标 |
| 右键菜单文本为空 | ContextMenu 是 Popup，`ElementName=Root` 绑定在弹出层中解析失败 | constructor 中 `CloneMenuItem.Header = Loc["CloneRow"]` 直接赋值 |
| Faction Tab 头中文不一致 | `[Display(Name="Faction")]` 与实体类型名 `Faction` 共用资源键 | Display 改为 `"FactionId"`；实体类型无中文 |
| Ingredient `&` 分隔符 | XML `16&amp;46` → 实体值 `16&46`，`Separator=","` 无法分割 | `Separator=","` → `Separator="&"` |
| FindPanel 焦点丢失后 Ctrl+F 无效 | `KeyDown` 只在 UserControl 有焦点时触发 | `TopLevel.AddHandler(KeyDownEvent, OnGlobalKeyDown, handledEventsToo: true)` 全局注册 |

---

### 验证框架（已废弃）
- 创建 `Data/Validation/ReferenceIntegrityRule` / `RequiredFieldRule` / `ValueRangeRule` + `ValidationReportDialog`
- 接入保存流程后单 Mod 5k+、合并视图 30k+ Warning（跨 Mod/Game 基础数据引用无法可靠验证，游戏数据不在编辑器上下文）
- 已从保存流程移除，文件保留

---

### 新增文件（13个）
`Data/Command/IEditorCommand.cs`, `CommandHistory.cs`, `EditCellCommand.cs`, `AddEntityCommand.cs`, `DeleteEntityCommand.cs`
`Data/Validation/IValidationRule.cs`, `ValidationResult.cs`, `ReferenceIntegrityRule.cs`, `RequiredFieldRule.cs`, `ValueRangeRule.cs`, `ValidationService.cs`
`Helper/Converter/ModNameColumnConverter.cs`
`Views/UserControls/FindReplacePanel.axaml` + `.cs`
`Views/Dialog/ValidationReportDialog.axaml` + `.cs`
`Views/Dialog/BatchEditDialog.axaml` + `.cs`（已删除，批量编辑废弃）
`Docs/04-stage5-analysis.md`

### 修改文件（21个）
`SearchableDataGrid.axaml` + `.cs`, `ModGameDataTabsView.axaml` + `.cs`, `GenericDataGridHelper.cs`, `ReferenceFieldAttribute.cs`, `ReferenceHelper.cs`, `AddRowDialog.axaml` + `.cs`, `AppConfig.cs`, `IEntity.cs`, `Creature.cs`, `Ingredient.cs`, `TreasureTable.cs`, `Encounters.cs`, `ItemType.cs`, 其余 7 个 Game Model 文件, `Resources.resx` / `.zh.resx` / `.en-us.resx`

### 新增/变更本地化键
`UndoTooltip`, `RedoTooltip`, `FactionId`, `FactionIdDesc`, `CloneRow`, `FindReferences`, `ExportCsv`, `ImportCsv`, `ExportCsvTooltip`, `ImportCsvTooltip`

---

### 当前已知限制
1. **排序箭头不显示**：Avalonia 11.3 `DataGridSortDescription` 抽象类
2. **Avalonia 版本锁定 11.3.x**
3. **Recipe `strTools`/`strConsumed`**：`1x2+1x3` 格式（`+` 分隔）未标注 `[ReferenceField]`
4. **TreasureTable `aTreasures`**：同字段混用 ItemType（GroupId.SubgroupId）和简单 id（TreasureTable）引用，单一 `[ReferenceField]` 无法覆盖两种目标类型
5. **FindPanel 不跟随 Semi.Avalonia 主题深色/浅色切换**

---

## Stage 6 — 可视化编辑器 + 数据导出 + 体验增强 (v0.7.0-dev) | 2026-05-29~30

### CSV/XML 导入导出迁移
| 功能 | 说明 |
|------|------|
| CSV 导出 | ModDatabase 右键菜单 → 导出当前 Mod 数据为 CSV |
| CSV 导入 | 文件选择 → 实体类型匹配 → CsvDiffDialog 预览变更 → 确认导入 |
| 旧代码清理 | `ModGameDataTabsView.axaml.cs` 中 CSV 方法移除，逻辑提取到 `CsvImportExportService` |

### 数据导出（Profile 面板）
| 导出 | 格式 | 说明 |
|------|------|------|
| 合成表 | CSV/XLSX | Recipe→Ingredient→TreasureTable，引用列解析为 Subject |
| 物品百科 | Markdown | ItemType 全字段，Condition 引用解析 `{id}x{mult}` 格式 |
| 战利品表 | JSON | TreasureTable 递归展开（最大深度5层，循环检测） |
| 全部导出 | XLSX | 24 种实体按类型分 Sheet，含 `→Id` 合并列，引用解析为 Subject，支持 Unicode 转义 |

### 数据导出增强
- 自动默认文件名（`crafting_table_20260530.csv` 等）
- `EnsureGameDataLoadedAsync` 导出前自动确保 Game 数据已加载
- `ToDedupedDict` 辅助方法处理多 Mod 重复 ID
- `XlsxWriter`：纯 C# 无外部依赖的 .xlsx 生成器（ZIP+XML）

### 可视化编辑器架构

#### ICustomTableEditor + CustomEditorRegistry
- 接口：`EntityType` / `EditorName` / `CreateEditor()` / `UpdateEntity(IEntity?)`
- 注册表：`CustomEditorRegistry` 按实体类型注册编辑器
- 面板：`ValueEditorPanel` 右侧分割面板，GridSplitter 可拖拽调整宽度，Star 弹性伸缩

#### EditorHelper 统一工具
- `BuildOverviewTab(IEntity)` 通用概览标签页（所有属性 + 引用 + 图片 + 反向引用）
- `BuildRefChildren` 引用列解析（去重赛选胜者→复合键匹配→Subject 显示→Ctrl+Click 跳转）
- 支持嵌套 `|` OR 分隔符（TreasureTable aTreasures）
- `FormatExtraInfo` 额外信息格式化（0~1 浮点自动显示为百分比）
- `FmtPct` 百分比格式化、`StripNs` 命名空间前缀剥离、`AddImagePreviews` 图片缩略图

#### ReferenceResolver 引用解析器
- `GetDedupedInt<T>` / `GetDedupedComposite<T>` 去重查找（最高 ModId）
- `ResolveSubject` / `ResolveMultiRef` 引用解析
- `NavigateTo` / `NavigateToByKey` 统一导航
- `FindReverseReferences` 反向引用索引

### 可视化编辑器实现清单

| 实体类型 | 标签页 | 功能 |
|---------|--------|------|
| **Recipe** | Overview, Recipe Tree | 工具/消耗/产物树状展开，Ingredient→ItemProp 属性展开，Ctrl+Click 跳转 |
| **Encounter** | Overview, Story Graph | 流程图(Canvas)+关系树(TreeView)，LeadsFrom/Self/LeadsTo，响应权重百分比 |
| **TreasureTable** | Overview, Treasure Tree | 嵌套战利品递归树，OR Group/AND Item，概率/数量范围，循环检测 |
| **ItemType** | Overview, Sprite Show, Wear Show | 属性概览+引用+图片；SpriteShow 多选下拉叠加 CreHuman.png；WearShow 多选下拉叠加 btn_inv_body.png |
| **Ingredient** | Overview | 必需/禁止属性展开 + 反向引用 |
| **ItemProp** | Overview | 属性信息 + 反向引用 |
| **AttackMode 等其余** | Overview | EntityOverviewEditor 通用概览 |

### 引用系统增强
- `ReferenceField` Pattern 新增 `{mult}x{id}` 支持（Recipe strTools/Consumed/Destroyed）
- Recipe 的 Tools/Consumed/Destroyed 标注 `[ReferenceField(typeof(Ingredient), Separator="+", Pattern="{mult}x{id}")]`
- 引用列语义化显示：`ReferenceHelper.ExtractRawId` + `ParseMultiplierReversed`
- 可视化编辑器所有引用统一 Ctrl+Click 跳转

### 图片系统
- `ImageViewerWindow` 浮动窗口（ScaleTransform+TranslateTransform 缩放/平移）
- 搜索路径：GameRoot/img + Mods/*/img/ 子目录
- `vSpriteList` 解析（`slot=imagePath` 格式）→ 身体部位映射
- `vImageList` + `vSpriteList` + `strImg` 等字段图片自动缩略图预览
- 图片命名空间前缀剥离（`NSE:img.png` → `img.png`）

### 身体部位槽位映射

| 槽位 | 部位 | 槽位 | 部位 |
|------|------|------|------|
| 20 | L-Hand | 14 | R-Shoulder |
| 21 | R-Hand | 17 | Face |
| 22 | Back | 13 | L-Back |
| 23 | Head | 4 | Legs |
| 11 | Torso | 2 | L-Foot |
| — | — | 3 | R-Foot |

### 容器与布局
- 可视化编辑器面板从悬浮窗 → 右侧分割面板（GridSplitter + Star 弹性列宽）
- 默认打开，无自定义编辑器时显示占位提示
- 统一标签页布局：Tab 1 = Overview，Tab 2+ = 特性化视图
- 文档打开时自动折叠左侧边栏

### Bug 修复 & 优化
- **重复 key 崩溃**：DataExportService + 可视化编辑器所有 ToDictionary → GroupBy 去重
- **RowDetail 不展开**：SearchableDataGrid 构造时显式设置初始 RowDetailsVisibilityMode
- **首次点击空白**：OnTabChanged 使用 Dispatcher.UIThread.Post 延迟更新
- **面板宽度固定**：移除 Width=320 硬编码 + 内部拖拽柄冲突
- **文本换行**：所有 TextBlock 添加 TextWrapping=Wrap
- **百分比显示**：0~1 浮点值自动格式化为百分比

### 新增文件（~20 个）
| 文件 | 说明 |
|------|------|
| `Services/CsvImportExportService.cs` | CSV 解析/对比/转换 |
| `Services/DataExportService.cs` | 合成表/百科/战利品/XLSX 全导出 |
| `Services/CustomEditorRegistry.cs` | 编辑器注册表 |
| `Helper/ICustomTableEditor.cs` | 编辑器接口 |
| `Helper/ReferenceResolver.cs` | 统一引用解析器 |
| `Helper/HexMapRenderer.cs` | 地图六边形网格 Bitmap 渲染 |
| `Views/UserControls/ValueEditorPanel.axaml` + `.cs` | 右侧分割面板 |
| `Views/UserControls/ZoomableImageView.axaml` + `.cs` | 可缩放拖动图片查看器 |
| `Views/UserControls/Editors/EditorHelper.cs` | 编辑器公共工具（概览/引用/图片） |
| `Views/UserControls/Editors/EntityOverviewEditor.cs` | 通用属性概览 |
| `Views/UserControls/Editors/RecipeFlowchartEditor.cs` | 配方树 |
| `Views/UserControls/Editors/StoryTreeEditor.cs` | 剧情编辑器 |
| `Views/UserControls/Editors/TreasureTreePreviewEditor.cs` | 战利品树 |
| `Views/UserControls/Editors/ItemTypeEditor.cs` | 物品类型编辑器 |
| `Views/UserControls/Editors/IngredientEditor.cs` | 合成项编辑器 |
| `Views/UserControls/Editors/ItemPropEditor.cs` | 属性编辑器 |
| `Views/Dialog/CsvImportDiffDialog.axaml` + `.cs` | CSV 导入对比 |
| `Views/Dialog/ImageViewerWindow.axaml` + `.cs` | 图片浮动弹窗 |
| `Data/Messages/ModMessages.cs` | 新增 OpenImageDocumentMessage |

### 修改文件（~10 个）
| 文件 | 改动 |
|------|------|
| `Pane.axaml` | ModDatabase 右键菜单（CSV导入导出移除ImportCsv）；Profile 工具栏（导出 DropDown + XLSX）；合并视图恢复 |
| `ModDatabaseViewModel.cs` | CSV/XLSX 导出命令，ImportXml 移除 |
| `ModIndexViewModel.cs` | ExportWithDialog 重构 + 默认文件名 |
| `ModGameDataTabsView.axaml` + `.cs` | GridSplitter 分割布局，IsValueEditorVisible 属性，标签页/选中行联动 |
| `DocumentWorkspaceView.axaml` | ImageDocument DataTemplate |
| `DocumentWorkspaceViewModel.cs` | OpenImageDocumentMessage 接收 + 侧边栏折叠 |
| `Documents.cs` | ImageDocument 新增 ImageSource 属性 |
| `SearchableDataGrid.axaml.cs` | RowDetailsVisibilityMode 初始化修复 |
| `App.axaml.cs` | 编辑器 DI 注册 + EntityOverviewEditor 自动注册 |
| `Recipe.cs` | 新增 3 个 [ReferenceField] |
| `ReferenceHelper.cs` | 新增 {mult}x{id} 模式 + ParseMultiplierReversed |
| `Resources.resx` (×3) | 新增 ~15 个本地化键 |

### 当前已知限制
1. **ImageDocument 浮动显示**：Dock.Avalonia 浮动窗口缺少 DataTemplate（停靠正常）
2. **排序箭头不显示**：Avalonia 11.3 `DataGridSortDescription` 抽象类
3. **Avalonia 版本锁定 11.3.x**
4. **TreasureTable `aTreasures`**：同字段混用 ItemType（GroupId.SubgroupId）和简单 id（TreasureTable）引用，单一 `[ReferenceField]` 无法覆盖两种目标类型


---

## Stage 7 — 查找替换完善 + 可视化编辑器夯实 + 体验增强 (v0.8.0-dev) | 2026-05-30

### 查找替换系统重构

#### 撤销支持
- 新建 `Data/Command/BatchEditCommand.cs` — 批量编辑命令，N 次替换作为单次原子撤销
- `FindReplacePanel` 集成 `CommandHistory` + `OnDirtyChanged` 回调
- `ReplaceOne` 创建 `EditCellCommand` 并通过 CommandHistory 执行（可撤销）
- `ReplaceAll` 创建 `BatchEditCommand` 一次性执行（一次撤销还原全部）

#### 字段级匹配
- `PerformSearch` 不再每行 `break` 第一个匹配列 → 逐列匹配，每行可有多个 `MatchInfo`
- `MatchInfo` 记录新增 `ColumnName`（C# 属性名）用于 SortMemberPath 列定位
- `NavigateTo` 精确滚动到匹配列（`ScrollIntoView(entity, col)`）
- 匹配 cell 边框高亮（OrangeRed 2px），通过索引定位正确列

#### 替换后刷新
- `RefreshGrid`：优先 `DataGridCollectionView.Refresh()`（合并视图），回退安全 ItemsSource 交换
- 替换后网格立即可见更新

#### 本地化
- FindReplacePanel 新增 `Loc` 属性，所有 AXAML/CS 硬编码字符串替换为本地化键
- 新增 14 个本地化键（`FindPrevious`、`FindNext`、`FindMatchCase`、`FindWholeWord`、`FindRegex`、`FindClose`、`FindWatermark`、`ReplaceWatermark`、`ReplaceButton`、`ReplaceAllButton`、`FindInvalid`、`FindNoMatches`、`FindMatchCount`、`FindReplaceSuccess`、`FindReplaceTitle`）

### 搜索模块
- `SearchPaneViewModel` 实现全局搜索：遍历全部 24 种实体类型，匹配所有 string 属性
- 支持 `col:value` 列筛选语法
- 搜索结果按实体类型分组，双击跳转到目标实体
- 最近搜索历史（最多 20 条）
- Pane.axaml 搜索面板：ScrollViewer + ItemsControl 扁平布局，禁用虚拟化避免滚动跳动

### 可视化编辑器增强

#### 编辑器面板架构
- `ValueEditorPanel` 改为**每子标签页独立实例**，自装配：`OnAttachedToVisualTree` → 查找兄弟 DataGrid → 绑定 SelectionChanged
- TabControl.ContentTemplate 内部采用三列 Grid（`*,Auto,Auto`）布局：DataGrid | GridSplitter | ValueEditorPanel
- 编辑器面板可见性通过 `GameDataTypeTabItem.IsEditorVisible` 控制，Toggle 按钮统一切换全部标签页

#### ItemType 编辑器
- **缩放/平移**：SpriteShow + WearShow 叠加视图支持鼠标滚轮缩放（0.1x–20x，光标位置为中心）+ 左键/中键平移
- **重置按钮**（`⟲`）：与下拉按钮水平并排，一键恢复默认缩放/平移
- **Flyout 滚动崩溃修复**：ListBox 虚拟化回收导致 NRE，`FuncDataTemplate` 添加 null 守卫 + 显式捕获闭包变量

#### Condition 编辑器
- 概览标签页新增 `FieldNames → Modifiers` 配对展示（两个逗号分隔列表按索引 1:1 配对）

### 引用系统增强

#### 命名空间感知匹配
- `FindBestMatch` 优先匹配命名空间前缀对应的实体：
  - 提取 `rawId` 中 `:` 前的命名空间前缀
  - 通过 `EntityModNames`（直接目录名匹配）或 `NamespaceToModName`（strModName → 目录名映射）查找
  - 命名空间匹配优先于最高 ModId 匹配
- `NavigToReference` 回退解析：命名空间 ID 在 int 解析前剥离前缀
- `ModLoadInfo` 新增 `Namespace` 属性，`ProfileManager.LoadMods` 填充 strModName
- `ReloadMergeTabsAsync` 构建 `NamespaceToModName` 字典

#### Overview 标签页引用解析
- `BuildRefChildren` + `ResolveSingleRefItem` 重构：直接调用 `FindBestMatch` 而非本地去重字典
- 与 DataGrid 单元格渲染行为完全一致（命名空间匹配 + 复合键 + ModId 优先级）
- `FindBestMatch` 改为 `internal` 可见性

#### BattleMove 条件引用
- `vUsPreConditions` / `vThemPreConditions`（简单逗号格式 `137,151,-143`）：`[ReferenceField(typeof(Condition), Separator = ",")]`
- 6 个括号三元组字段（`vUsConditions` 等，格式 `[98,0,0],[339,0,0]`）：`[ReferenceField(typeof(Condition), Separator = "],[", Pattern = "[{id}")]`
- `ExtractRawId` 新增 `"[{id}"` 模式：从括号包裹的三元组中提取第一个数字作为 condition ID

### 剧情编辑器
- **Story Graph 流程图连线**：`LayoutFlowNode` 递归布局后在父子节点间绘制 `Line` 连线

### 合并视图
- **Save 写 XML**：`ShowMergeSavePreviewAsync` DB 保存后自动调用 `ExportXmlAsync` 写回源 Mod XML 文件
- **标签页空白修复**：缓存恢复后调用 `RebuildFilteredItemsSources()` + 确保默认标签页选中
- **ShowAll 切换卡死修复**：缓存恢复后 `_overriddenEntityIds` 实例字段从静态 `OverriddenEntityIds` 恢复

### 体验增强

#### DataGrid 筛选栏
- 工具栏恢复 `FilterText` TextBox（`col:value` 语法），位于操作按钮右侧
- 后移至每个子标签页内部，DataGrid 上方独占一行，宽度填满

#### 编辑器设置面板
- `AppConfig` 新增 `Language`、`Theme`、`FontSize` 配置属性
- `SettingsPaneViewModel` 新增 BrowseGameDir、SetLanguage、SetTheme 命令
- Settings 面板 DataTemplate 重新设计：GameRootDir 可编辑 + 浏览按钮、Language ComboBox、Theme Toggle
- 配置持久化到 `config.json`

#### Profile 差异对比
- 新建 `Views/Dialog/ProfileDiffDialog` — 双栏 DataGrid 对比两个 Profile 的 Mod 加载列表
- ModIndexViewModel 新增 `CompareProfilesCommand`
- Profile 右键菜单新增 "Compare" 选项

#### Mod 打包 (.zip)
- `ModManager.ExportModToZipAsync` / `ImportModFromZipAsync`
- ModDatabase 右键菜单新增 Export Zip / Import Zip

### 新增文件（~8 个）
| 文件 | 说明 |
|------|------|
| `Data/Command/BatchEditCommand.cs` | 批量编辑命令（ReplaceAll 原子撤销） |
| `Views/Dialog/ProfileDiffDialog.axaml` + `.cs` | Profile 差异对比对话框 |
| `Views/UserControls/Editors/` 目录 | 已在 Stage 6 创建 |

### 修改文件（~15 个）
| 文件 | 关键改动 |
|------|---------|
| `FindReplacePanel.axaml` + `.cs` | 撤销支持、字段级匹配、cell 高亮、本地化 |
| `ModGameDataTabsView.axaml` + `.cs` | 每标签页编辑器、筛选栏、合并视图修复、缓存恢复修复 |
| `ValueEditorPanel.axaml.cs` | 自装配 DataGrid 绑定 |
| `GenericDataGridHelper.cs` | `FindBestMatch` 命名空间匹配、internal 可见 |
| `ReferenceHelper.cs` | `ExtractRawId` 新增 `[{id}` bracket 模式 |
| `StoryTreeEditor.cs` | 流程图连线 |
| `ItemTypeEditor.cs` | 缩放/平移 + 重置按钮 + Flyout NRE 修复 |
| `EditorHelper.cs` | Condition 配对字段、引用解析改用 FindBestMatch |
| `BattleMove.cs` | 8 个条件字段标注 ReferenceField（两种格式） |
| `Condition.cs` | 已在 Stage 6 存在 |
| `SearchPaneViewModel.cs` | 全局搜索实现 |
| `SettingsPaneViewModel.cs` + `AppConfig.cs` | 编辑器设置 |
| `ModIndexViewModel.cs` | Profile 对比命令 |
| `ModManager.cs` | ZIP 导入导出 |
| `ModInfo.cs` | ModLoadInfo.Namespace 属性 |
| `ProfileManager.cs` | 填充 ModLoadInfo.Namespace |
| `Pane.axaml` | 搜索面板重设计、设置面板重设计、ZIP/对比菜单项 |
| `Resources.resx` (×3) | 新增 ~25 个本地化键 |

### 新增/变更本地化键
`FindPrevious`, `FindNext`, `FindMatchCase`, `FindWholeWord`, `FindRegex`, `FindClose`, `FindWatermark`, `ReplaceWatermark`, `ReplaceButton`, `ReplaceAllButton`, `FindInvalid`, `FindNoMatches`, `FindMatchCount`, `FindReplaceSuccess`, `FindReplaceTitle`, `FieldNamesModifiers`, `Language`, `Theme`, `FontSize`, `BrowseGameRoot`, `ExportModZip`, `ImportModZip`, `CompareProfiles`, `ResetZoom`

### 当前已知限制
1. **ImageDocument 浮动显示**：Dock.Avalonia 浮动窗口缺少 DataTemplate（停靠正常）
2. **排序箭头不显示**：Avalonia 11.3 `DataGridSortDescription` 抽象类
3. **Avalonia 版本锁定 11.3.x**
4. **TreasureTable `aTreasures`**：同字段混用 ItemType（GroupId.SubgroupId）和简单 id（TreasureTable）引用，单一 `[ReferenceField]` 无法覆盖两种目标类型
5. **数据验证**：`Data/Validation/` 代码存在但未接入保存流程 — 应作为提示（Warning）而非阻止（Error），需缩小验证范围到当前 Mod 数据以避免跨 Mod 误报
6. **像素画编辑器**：缺少逐像素手绘工具（画笔/橡皮擦/填充/取色）、背景透明化处理、调色板编辑
7. **像素编辑器 ↔ ModImages 联动**：无法从 ModImages 图片列表双击直接打开像素编辑器，也无法从像素编辑器自动添加到图片列表
8. **资源浏览器**：缺少右键菜单（删除/重命名/复制路径）、文件图标、文件类型过滤
9. **Mod 依赖检查**：跨 Mod 命名空间依赖分析 — 未实现
10. **查找面板**：不跟随 Semi.Avalonia 深色/浅色主题切换


---

## Stage 8 — 解耦与重构 (v0.9.0-dev) | 2026-05-30

> 详细设计见 `Docs/05-refactoring-plan.md`

### Phase 1：全局可变状态解耦

#### 问题
`GenericDataGridHelper` 持有 12 个公共静态可变字典/HashSet，所有 `ModGameDataTabsView` 实例共享同一份状态，标签页切换时通过 `TakeSnapshot`/`RestoreSnapshot` 手动复制 9 个集合来模拟隔离。

#### 方案
创建每个标签页独立的实例 store，通过 GDH 桥接属性委托访问。

#### 新建文件
| 文件 | 说明 |
|------|------|
| `Services/EntityMergeStore.cs` | 合并状态容器：`ReferenceLookups`、`EntityModNames`、`EntityMergedIds`、`OverriddenEntityIds`、`OverlayChainDisplay`、`FieldSources`、`FieldConflicts`、`NamespaceToModName`、`SubjectCache` |
| `Services/EditTrackingStore.cs` | 编辑追踪容器：`EditedCells`、`NewEntityIds` |

#### 修改
- `GenericDataGridHelper.cs`：所有公共静态属性（`ReferenceLookups`、`EntityModNames` 等 12 个）改为委托给 `SetActiveStores()` 设置的活跃实例 store；无活跃 store 时回退到私有静态集合（向后兼容）
- `TakeSnapshot` / `RestoreSnapshot` 大幅简化：直接缓存 `(EntityMergeStore, EditTrackingStore)` 实例，不再逐字段复制 9 个集合
- `ModGameDataTabsView.axaml.cs`：`OnAttachedToVisualTree` 设置活跃 store；`TabSnapshotCache` 值类型更新
- 所有现有消费者（`ReferenceResolver`、`EditorHelper`、`DataExportService`、8 个 Converter）无需改动——通过桥接属性透明访问

### Phase 2：巨型类拆分

#### FilterService 提取
- 新建 `Services/FilterService.cs`
- 从 `ModGameDataTabsView` 提取：`ApplyFilters`、`ParseFilterTokens`、`SplitFilterText`、`MatchesAllTokens`、`FindColumnProperty`、`GetStringProperties`（~150 行移除）
- `RebuildFilteredItemsSources` 保留在视图中（与 `Tabs`、`DataTabs` UI 元素耦合），委托给 `_filterService.ApplyFilters()`

### Phase 4：引用 Pattern 策略 + EditorUIFactory

#### ReferencePattern 策略
- 新建 `Helper/ReferencePattern.cs` — 抽象基类 + 5 个私有嵌套实现
  - `IdPattern`、`IdXMultPattern`、`MultXIdPattern`、`IdEqualsValuePattern`、`BracketIdPattern`
- 每个子类封装：`ExtractRawId`（ID 提取）、`FormatDisplay`（DataGrid 显示）、`FormatExtraInfo`（Overview 额外信息）
- 调用方迁移：
  - `ReferenceHelper.ExtractRawId`：40 行 switch/if → 1 行 `ReferencePattern.FromName(pattern).ExtractRawId(segment)`
  - `GenericDataGridHelper.FormatSegmentDisplay`：30 行 → 5 行委托
  - `EditorHelper.FormatExtraInfo`：12 行 → 1 行委托；移除未使用的 `FmtPct`

#### EditorUIFactory 提取
- 新建 `Helper/EditorUIFactory.cs` — 纯 UI 工厂：`NewNode`、`NavOnCtrl`、`MakeTab`、`CreateEditorTabs`
- `EditorHelper` 中的 4 个方法改为委托给 `EditorUIFactory`（向后兼容）

### Phase 5：去重与接口化

#### ImageService 统一图片逻辑
- 新建 `Services/ImageService.cs`（DI 注册为 Singleton）
- 整合源：
  - `PhpParser`：`PairImages`、`LooksLikeSplitHalfPairs`、`IsX2Variant`、`IsX2Image` → 委托给 `ImageService`
  - `EditorHelper`：`GetImageSearchDirs` → 移除，使用 `ImageService.GetImageSearchDirs()`
  - `ItemTypeEditor`：`FindImage` + `root` 参数 → 移除，使用 `_imageService.FindImage()`

#### ConvertValue 去重
- `XmlParser.ConvertValue` 内部实现 → 委托给 `ValueConverter.Convert`

#### ICommandHistory 接口
- 新建 `Data/Command/ICommandHistory.cs`
- `CommandHistory` 实现接口，支持 DI 注入和 mock

#### ViewModelBase 注入
- `Loc` 和 `Notification` 属性改为可注入（优先使用注入实例，回退 `App.Localizor` / `App.Notification`）
- 新增带参数构造函数，无参构造函数保留（Avalonia 框架兼容）

#### CommandHistory 剪枝优化
- `Execute` 方法满容量时避免 `ToArray()` 分配，改用临时栈翻转

### 修改文件清单（本阶段）
| 文件 | 关键改动 |
|------|---------|
| `GenericDataGridHelper.cs` | 静态属性 → 实例 store 委托桥接 |
| `ModGameDataTabsView.axaml.cs` | 实例 store 绑定/缓存；FilterService 集成 |
| `ViewModelBase.cs` | Loc + Notification 可注入 |
| `ReferenceHelper.cs` | ExtractRawId → ReferencePattern 策略 |
| `EditorHelper.cs` | 委托给 ReferencePattern / EditorUIFactory / ImageService |
| `PhpParser.cs` | 图片方法 → ImageService |
| `ItemTypeEditor.cs` | FindImage + root → ImageService；DI 字段 |
| `XmlParser.cs` | ConvertValue → ValueConverter |
| `CommandHistory.cs` | 实现 ICommandHistory；O(1) 分配剪枝 |
| `ModInfo.cs` | ModLoadInfo.Namespace 属性 |
| `ProfileManager.cs` | 填充 Namespace |
| `App.axaml.cs` | ImageService DI 注册 |
| `Resources.resx` (×3) | 新增 ~25 个本地化键（Stage 7 查找替换相关） |

### 重构效果

| 指标 | 改善前 | 改善后 |
|------|--------|--------|
| GDH 公共静态可变集合 | 12 个 | 0（全部委托给实例 store） |
| 添加引用 Pattern 需修改文件 | 3-4 个 | 1 个 |
| `ModGameDataTabsView` | ~2400 行 | ~2250 行 |
| `EditorHelper` | ~375 行 | ~290 行 |
| `PhpParser` | ~164 行 | ~124 行 |
| `ItemTypeEditor` | ~248 行 | ~228 行 |
| CommandHistory 满容量分配 | 1 数组 + N push | 1 临时栈 + N push/pop |
| ViewModelBase 可测试性 | 硬编码静态属性 | 可注入 mock |

---

## Stage 9 — UI 重构与面板系统 (v0.11.0-dev) | 2026-05-30 ~ 2026-05-31

### 新增功能

**HomePage 欢迎页**
| 功能 | 说明 |
|------|------|
| 三卡片入口 | Browse Game Data / New Mod / Import Mod |
| Recent Mods 列表 | 显示实体数 + 时间（跨 13 张核心表计数） |
| Profiles 入口 | 主页直接列出 Profile → 双击打开合并视图 |
| 拖拽导入 | 拖文件夹/XML 到窗口 → 自动导入 + 打开 |
| 自动刷新 | 关闭所有文档回主页时自动刷新列表 |

**工具面板（Grid 分栏）**
| 面板 | 位置 | 内容 |
|------|------|------|
| 覆盖链 | 左 220px | Winner/Loser 分区 + 字段贡献展开 |
| 可视化编辑器 | 右 280px | Recipe/Story/Treasure/ItemType 编辑器 |
| 图片预览 | 右 | 扫描实体所有含 "Img" 字段 → mod img 目录 + 游戏 img 目录 |
| 引用预览 | 右 | Ctrl+Click 引用 → Peek（预览目标属性，不跳转）；Pin 锁定 |
| 搜索/冲突/验证 | 底部 150px | 三个 Tab，冲突实时刷新 |

**引用系统增强**
| 功能 | 说明 |
|------|------|
| Peek（预览） | `GenericDataGridHelper.PeekRequested` — Ctrl+Click 在右侧面板预览，只有 "Open Full" 才跳转 |
| SecondaryTarget | `ReferenceFieldAttribute` 支持 `SecondaryTargetEntityType` + `SecondaryTargetKey`（TreasureTable 混合引用） |

**数据验证**
| 功能 | 说明 |
|------|------|
| 保存时验证 | 只验证改动过的实体（EditedCells + NewEntityIds），不弹窗 |
| 底部面板 | 验证结果写入底部 Validation Tab |

**依赖分析**
| 功能 | 说明 |
|------|------|
| 扫描 | 合并视图工具栏 "Deps" 按钮 → 5 列 DataGrid（Source/Mod/Field/Target/Issue） |
| 导出 | CSV 导出 + 列宽可拖拽 + Ctrl+C 复制 |

**日志系统**
| 功能 | 说明 |
|------|------|
| 早期启动 | `Program.cs` try/finally 包裹 → 崩溃也能记录 |
| 配置读取 | `appsettings.json` → `Logging:LogLevel` 覆盖 |
| 过滤 | `Microsoft.Extensions.Localization` 调为 Warning |
| 结构化 | 修复 6 处插值 `$"..."` → 结构化 `{Placeholder}` |

**Profile 多环境**
| 功能 | 说明 |
|------|------|
| 选中即加载 | `OnSelectedProfileChanged` → 自动 LoadMods |
| 设为活跃 | 右键 "Set as Active" → `ActiveProfileId` 持久化 |
| 双击打开 | 双击 Profile 列表项 → 打开合并视图 |
| 添加 Mod | 右键 "Add Mod..." → EditProfileView |

**编辑体验**
| 功能 | 说明 |
|------|------|
| Save 统一 | 单 Mod Save = DB + 写 XML，去掉独立 Export XML 按钮 |
| Ctrl+S | 键盘快捷键保存 |
| 自动保存 | `AppConfig.AutoSaveInterval` → DispatcherTimer 定时保存 |
| 空 Mod 引导 | 0 实体时显示公告栏 "Add Your First Entity" |
| 冲突脉动 | 冲突 > 0 时按钮红底白字 (#DC3C28) |
| 首个非空 Tab | 打开视图自动跳转到有数据的第一个 Tab |
| 脏关闭确认 | 关闭 dirty Tab 时弹出保存/不保存/取消对话框 |
| Tab 表头 | IsDirty 属性 "● " + IsDirty 属性 |
| FindReplace 脏标记 | Ctrl+H 替换 → `EditCellCommand.Execute` 调用 `EditedCells.Add` |
| FindReplace 主题跟随 | `Brushes.OrangeRed` → `SystemControlHighlightAccentBrush` |

**配置与偏好**
| 功能 | 说明 |
|------|------|
| AutoSaveInterval | 秒，0=关闭 |
| DefaultExportFormat | SaveFileDialog 默认扩展名（csv/xlsx/md/json） |
| GridRowHeight | SearchableDataGrid 行高 |
| ActiveProfileId | 当前活跃 Profile（主页/侧栏高亮） |

**侧栏重组**
| 按钮 | 说明 |
|------|------|
| 🏠 Home | 关闭所有文档回主页 |
| 📥 Import | 直接导入（不开面板） |
| 🗄️ Mod Database | 查看已导入 Mod 列表 |
| 👥 Profiles | 合并视图入口 |
| 📁 Explorer | 文件浏览器 |
| 🔍 Search | 全局搜索 |
| ⚙️ Settings | 编辑器偏好 |

**底部面板**
| Tab | 内容 |
|------|------|
| Search | 搜索结果列表（框架就位） |
| Conflicts | FieldConflicts 实时列表 + 刷新按钮 |
| Validation | 保存后验证警告计数 |

### 新建文件（本轮 ~20 个）

| 文件 | 用途 |
|------|------|
| `ViewModels/MainContent/HomePageViewModel.cs` | 主页逻辑（Browse/New/Import/Recent/Profiles） |
| `Views/UserControls/HomePage.axaml/.cs` | 三卡片入口页 |
| `ViewModels/MainContent/OverlayChainToolContent.cs` | 覆盖链数据（Winner/Loser + 字段贡献） |
| `Views/UserControls/OverlayChainToolView.axaml/.cs` | 覆盖链面板 UI |
| `ViewModels/MainContent/ReferenceInspectorContent.cs` | 引用预览数据 |
| `Views/UserControls/ReferenceInspectorView.axaml/.cs` | 引用预览面板 UI |
| `ViewModels/MainContent/ImagePreviewContent.cs` | 图片预览数据 |
| `Views/UserControls/ImagePreviewView.axaml/.cs` | 图片预览面板 UI |
| `ViewModels/MainContent/BottomToolsViewModel.cs` | 底部面板数据（Search/Conflicts/Validation） |
| `Views/UserControls/BottomToolsView.axaml/.cs` | 底部面板 UI |
| `Views/UserControls/RightPanelView.axaml/.cs` | 右侧面板包装（Editor/Images/Ref Inspect Tab） |
| `Views/Dialog/ConflictListDialog.axaml/.cs` | 冲突详情弹窗（可调列宽/复制/CSV 导出） |
| `Views/Dialog/DependencyListDialog.axaml/.cs` | 依赖分析弹窗（同上） |
| `Services/DependencyAnalysisService.cs` | 跨 Mod 引用完整性扫描 |
| `Docs/09-current-status.md` | 总进度报告 |

### 修改文件（本轮 ~15 个）

| 文件 | 重要变更 |
|------|---------|
| `MainWindow.axaml` | 侧栏重排（7 按钮三组）+ 工具栏 New/Import + 面板切换 ◀ ▶ ▼ + HomePage 层 |
| `DocumentWorkspaceView.axaml` | Grid 三区分栏（左/中/右/底）+ 拆分器 |
| `DocumentWorkspaceView.axaml.cs` | 拖拽导入 + VisualEditorRequested 桥接 |
| `DocumentWorkspaceViewModel.cs` | IsHomePageVisible + ActiveDocumentTitle + 三面板内容持有 |
| `ModGameDataTabsView.axaml` | 去掉 inline ValueEditorPanel + GridSplitter + PanelRight 按钮 |
| `ModGameDataTabsView.axaml.cs` | 静态事件总线 + 空 Mod 引导 + Ctrl+S + 自动保存 + 首个非空 Tab + 缓存恢复修正 + 关闭确认 |
| `SearchableDataGrid.axaml` | 去掉 RowDetails 覆盖链展开面板 |
| `ModMessages.cs` | `OpenModGameDataDocumentMessage` 加 `ReadOnly` 参数 |
| `EditCellCommand.cs` | `Execute()` 调用 `GenericDataGridHelper.EditedCells.Add` |
| `FindReplacePanel.axaml.cs` | 主题跟随 `SystemControlHighlightAccentBrush` |
| `ReferenceFieldAttribute.cs` | `SecondaryTargetEntityType` + `SecondaryTargetKey` |
| `GenericDataGridHelper.cs` | `PeekRequested` + `LookupSubjectByRawId` 二级 fallback |
| `TreasureTable.cs` | 二级 ReferenceField 标注 |
| `ViewModelBase.cs` | `ILogger Logger` 属性 |
| `Program.cs` / `LoggingExtensions.cs` | Serilog 早期启动 + appsettings 读取 |
| `AppConfig.cs` | AutoSaveInterval / DefaultExportFormat / GridRowHeight / ActiveProfileId |
| `SettingsPaneViewModel.cs` | 4 个新设置项显示绑定 |
| `Pane.axaml` | 4 个新设置 UI 行 + 双 Profile 菜单项 |
| `MainStatusBar.axaml` | 文档数 + 活跃标题 |
| `3 个 resx 文件` | 10+ 新本地化 Key |
| `ModManager.cs` | `ImportModAsync` 返回 `ModInfo?` + `LoadModAsync` 快速跳过 |

### 架构决策

| 决策 | 原因 |
|------|------|
| Grid 面板 > Dock ToolDock | Dock.Avalonia 11.3.11 的 ToolDock ItemsSource MVVM 绑定不成熟——`CreateLayout()` 非 virtual、`IRootDock` 命名空间未暴露、`InitLayout` 介入时机不明确 |
| 静态事件总线 | `ModGameDataTabsView` 的 5 个 `static Action` 连接 DataGrid 选区到各面板，简单直接 |
| `DocumentWorkspaceViewModel.Instance` | scoped VM 需静态访问点供 DataTemplate 创建的控件获取面板数据 |
| Save = DB + XML 统一 | 消除单 Mod / 合并视图行为差异 |

### 已知问题

| 问题 | 状态 |
|------|:--:|
| ToolDock 集成 | 🔜 需等 Dock.Avalonia 版本更新或更完整的文档 |
| 底部 Search Tab 空 | 🔜 需接入 SearchPaneViewModel |
| Validation Tab 报告简略 | 🔜 只显示计数，未显示详情 |
| 列复制粘贴 | 🔜 未实现 |
| 批量编辑 | 🔜 未实现 |
| Dock 面板布局持久化 | 🔜 未实现 |
| 图片搜索逻辑重复 | 3 处（EditorHelper ×2 + ItemTypeEditor） | 1 处（ImageService） |

---

## Stage 10 — 引用检查器 + 资源管理器 + 搜索增强 + 面板打磨 (v0.12.0-dev) | 2026-05-31

### 底部搜索 Tab 接入
| 功能 | 说明 |
|------|------|
| SearchService | 抽取共享搜索引擎（24 实体类型全文搜索 + `col:value` 语法） |
| SearchResults 共享模型 | `SearchResultGroup` / `SearchResultItem` 提取到 `Helper/SearchResults.cs` |
| 底部搜索 UI | TextBox + Go 按钮 + 进度条 + 分组结果列表 |
| 搜索栏 Ctrl+Click | Ctrl+左键=跳转，Ctrl+右键=peek |

### 资源管理器右键菜单 + F2 重命名
| 功能 | 说明 |
|------|------|
| 右键菜单 | Open / Open in Explorer / Copy Full Path / Rename / Delete |
| F2 快捷键 | TreeView 中选中项按 F2 → `RenameDialog` 弹窗输入新名称 |
| DeleteItem | 确认弹窗后删除文件/文件夹 |
| 文件类型图标 | `FileTypeIconConverter`：按扩展名显示不同 Symbol 图标（图片/XML/JSON 等） |

### Reference Inspector 全面重做
| 功能 | 说明 |
|------|------|
| Ctrl+LeftClick = 跳转 | 导航到目标实体（同时 peek 入栈） |
| Ctrl+RightClick = Peek | 推送 history 栈 + 预览（Pin 时只推不入栈） |
| Peek 历史栈 | 后退/前进双栈，**智能去重**（`TryPopFromHistory` 搜索全部历史） |
| Pin 逻辑 | **仅冻结自动显示**（新 peek 不覆盖概览），历史导航 ◀▶ 始终可用 |
| Unpin | 即时同步概览到当前栈顶 |
| Open Full | 修复为 `NavigateToByEntityId` + 打开源 Mod 文档（双重保障） |
| 快照存储属性 | `PeekSnapshot.SavedProperties` 保存完整属性列表，退/进时完整恢复 |
| 视觉反馈 | Pin 时 DarkOrange 边框 + "🔒 PINNED" 标签 + 按钮 "Pin"/"Unpin" 切换 |
| 引导文字 | 增强空状态 + 页脚说明 |
| 右键菜单移除 | 引用列右键菜单与 Ctrl+右键冲突，已移除 → 统一用 Ctrl 操作 |
| modSource tooltip | 默认单元格 mod 来源提示栏移除 |

### 覆盖链面板精简
| 功能 | 说明 |
|------|------|
| 移除字段贡献 | 删去 `ContributedFields` / `HasFields` / `IsExpanded` / 展开按钮（游戏引擎按整实例覆盖，非逐字段） |
| 简化为 Winner/Loser 列表 | 只显示 Mod 名 + Subject + ID |

### Dock 面板布局持久化
- `AppConfig` 新增 `LeftPanelWidth` / `RightPanelWidth` / `BottomPanelHeight` + 面板可见性配置项
- `DocumentWorkspaceViewModel` 构造时恢复可见性，Toggle 面板时保存

### Images 面板修复与增强
| 功能 | 说明 |
|------|------|
| 多目录搜索 | 扫 `{gameRoot}/img/` + `Mods/*/img/` + `Mods/*/SubMod/img/`（两级深度） |
| Entity FilePath 推导 | 使用 `entity.FilePath` 推导实体所属 Mod 的 img 目录 |
| 显示文件名 | 仅显示原始文件名 + 字段来源，不显示全路径 |
| 状态指示 | ✓ 绿色 = 文件存在，✗ 红色 = 缺失 |
| 双击打开 | 双点 ✓ 项 → 系统默认程序打开图片 |
| 诊断信息 | 没找到时列出搜索目录和实体 FilePath 便于排查 |

### 帮助文档内嵌
- 5 篇中文 + 2 篇英文 .md 帮助文档加入 `Help/` 目录
- `.csproj` 添加 `AvaloniaResource` include（编译进 DLL）
- 清除测试文件 `Help/en/aa.md`

### Bug 修复清单
| 问题 | 修复 |
|------|------|
| Ctrl 时右键菜单弹出 | `_ctrlWasPressed` 静态标志 + `ContextRequested` 事件拦截 |
| SearchResultItem EntityId 引用 | `item.EntityId` → `item.Entity.EntityId` |
| FileTypeIcon 编译错误 | `WindowConsoleApp` → `AppGeneric`（FluentIcons 无此符号） |
| ResourceManager Clipboard API | `Application.Current.Clipboard` → `TopLevel.Clipboard`（Avalonia 11） |
| BottomToolsViewModel GetRequiredService | 补充 `using Microsoft.Extensions.DependencyInjection` |
| DocumentWorkspaceViewModel 启动 NRE | Config 未加载时添加 null guard |
| OverlayChainToolView OnToggleFieldsClick | 随字段贡献移除一并清理 |

### 新增文件（12 个）
| 文件 | 说明 |
|------|------|
| `Helper/SearchResults.cs` | 共享搜索模型 |
| `Services/SearchService.cs` | 共享搜索引擎 |
| `Helper/Converter/FileTypeIconConverter.cs` | 文件类型图标转换器 |
| `Views/Dialog/RenameDialog.axaml` + `.cs` | 重命名弹窗 |
| `Help/zh/GettingStarted.md` | 中文入门指南 |
| `Help/zh/ReferenceSystem.md` | 中文引用系统指南 |
| `Help/zh/MergeView.md` | 中文合并视图指南 |
| `Help/en/Welcome.md` | 英文欢迎页 |
| `Help/en/GettingStarted.md` | 英文入门指南 |

### 修改文件（~20 个）
| 文件 | 关键改动 |
|------|---------|
| `ReferenceInspectorContent.cs` | 完整重写：Pin/Unpin/history 双栈/智能去重/快照属性存储 |
| `ReferenceInspectorView.axaml` + `.cs` | Pin/Unpin 按钮 + ◀▶ 导航 + 增强引导 + Open Full 修复 |
| `GenericDataGridHelper.cs` | Ctrl+LeftClick=跳转, Ctrl+RightClick=Peek; `IsPeekPinned`; `_ctrlWasPressed`; 移除引用列右键菜单 + modSource tooltip |
| `BottomToolsViewModel.cs` + `BottomToolsView.axaml` + `.cs` | 搜索 UI + SearchService 集成 + Ctrl+Click 支持 |
| `SearchPaneViewModel.cs` | 委托给 `SearchService` |
| `ResourceManagerViewModel.cs` | Delete/Rename/CopyPath/OpenInExplorer 命令 + `RenameDialogRequested` |
| `Pane.axaml` + `.cs` | TreeView 右键菜单 + F2 重命名 + 搜索 Ctrl+Click |
| `DocumentWorkspaceViewModel.cs` | Pin/Peek 逻辑 + 面板布局恢复 |
| `DocumentWorkspaceView.axaml` + `.cs` | 布局持久化 |
| `ImagePreviewContent.cs` + `ImagePreviewView.axaml` + `.cs` | 多目录搜索 + 文件名显示 + 双击打开 |
| `OverlayChainToolContent.cs` + `OverlayChainToolView.axaml` + `.cs` | 移除字段贡献 |
| `AppConfig.cs` | 面板布局属性 |
| `App.axaml` + `.cs` | FileTypeIconConverter + SearchService DI |
| `NeoEditor.csproj` | Help 文件 AvaloniaResource include |

### 当前已知限制
1. **排序箭头不显示**：Avalonia 11.3 `DataGridSortDescription` 抽象类
2. **Avalonia 版本锁定 11.3.x**
3. **TreasureTable `aTreasures`**：同字段混用复合键引用，单一 `[ReferenceField]` 无法覆盖两种目标类型
4. **ImageDocument 浮动显示**：Dock.Avalonia 浮动窗口缺少 DataTemplate
5. **像素画编辑器**：缺少逐像素手绘工具
6. **列复制粘贴**：Ctrl+C/V 单元格未实现
7. **批量编辑**：多行选中批量改同字段未实现

---

### Stage 10 补充 (2026-05-31)

#### Help 菜单修复
| 问题 | 修复 |
|------|------|
| Help 菜单不显示文档 | `MainMenuBar` 改为代码动态构建 MenuItem（`ItemsSource`+DataTemplate 在 Avalonia 11.3 不可靠） |
| Help 文件找不到 | `.csproj` 移除 `LinkBase` + 遍历上层目录查找 `Help/` |
| Markdown 渲染空白 | 升级 Markdown.Avalonia 11.0.2→11.0.3 解决 |

#### 首次启动引导
- HomePage 新增 GameRoot 设置提示横幅（未设置时显示，定时检测，配置完成后自动隐藏）
- `NavigateToSettings()` 直接切换侧栏到 Settings 面板
- `MainWindowSideBarViewModel` 接收 `SwitchToSettingsMessage`

#### New Mod 创建流程
- `CreateModDialog` 新增 Namespace 输入 + 自动创建 Profile 复选框
- 创建后自动生成 getmods.php 内容并打开合并视图

#### 本地化补齐
- 新增 4 个缺失 Key（`GameRootDir`, `Help`, `ProfileDescription`, `Content`）
- 补全 20+ 实体类型名（Tab 表头显示中文）
- `Faction`/`FindInvalid`/`NavigateSameEntity` 三文件同步

---

## Stage 11 — 编辑体验夯实 + Markdown 升级 (v0.13.0-dev) | 2026-06-01

### 单元格复制/粘贴 (Ctrl+C/V)
- **内部 buffer 方案**：弃用系统剪贴板，改用静态 `_copyBuffer` 变量
- `Ctrl+C` → 提取选中行可见列原始属性值 → TSV 格式存入 buffer
- `Ctrl+V` → 取 buffer 首行 → 逐列写入选中行第一个实体
- 支持撤销：单格 → `EditCellCommand`，多格 → `BatchEditCommand`
- 类型安全：`ValueConverter.Convert` 包裹 try-catch，转换失败自动跳过

### 底部 Search Tab 完善
| 功能 | 说明 |
|------|------|
| Enter 键搜索 | TextBox `<KeyBinding Gesture="Enter">` 直接触发 |
| 清除按钮 | ✕ 按钮 → `ClearSearchCommand` |
| Recent Searches | 最多 15 条历史，标签式展示，点击直接搜索 |
| 双击导航 | `DoubleTapped` → 直接跳转到目标实体 |
| 样式改进 | 结果项 `Foreground="Teal"` + `TextWrapping="Wrap"` |
| 摘要增强 | 显示匹配总数 `"{statusText} (N result(s))"` |

### Dock 面板列宽持久化
- `DocumentWorkspaceView` 监听 VM `PropertyChanged`，切换面板显隐时自动调整 Grid Column/Row 宽度
- 隐藏面板 → 宽度设为 0（空间完全回收），显示 → 恢复 config 中保存的宽度
- `OnSplitterDragCompleted` → 实时保存当前列宽到 `AppConfig`
- `OnAttachedToVisualTree` → 从 config 恢复初始列宽

### Markdown → LiveMarkdown.Avalonia 迁移
- 替换 `Markdown.Avalonia 11.0.3` → `LiveMarkdown.Avalonia 1.9.2` + Math/Svg 扩展
- `App.axaml` 注册 `Styles.axaml` + `Defaults.axaml`（之前缺失导致无格式化）
- `MarkdownDocument` 新增 `MarkdownBuilder` 属性（`ObservableStringBuilder`）
- `LinkCommand` → `RelayCommand<LinkClickedEventArgs>` 拦截 `.md` 链接
- `.md` 相对链接 → 通过 `OpenHelpDocumentMessage` 在编辑器内打开标签页
- 外部链接 → `Process.Start` 系统浏览器打开

### Mod 制作指南内嵌
- 从 `NeoScavenger 模组制作指南中文翻译精修1.2（新）.docx` 提取纯文本
- 保存为 `Help/zh/ModGuide.md`（~41KB，257 行）
- 段落间添加空行确保 markdown 正确渲染
- 关键术语添加反引号强调（`VanillaOverride`、`AddOn`、`neogame.xml` 等）
- getmods.php 代码示例用 ` ``` ` 代码块包裹

### XML 字段说明集成
- 新建 `DocxTextExtractor` — .docx 文本提取 + 字段描述解析
- 新建 `FieldDescriptionService` — 加载/缓存/查询字段描述
- 启动时自动从 `游戏XML文本各项说明修正增强版.docx` 提取 → 缓存 `field_descriptions.json`
- `GenericDataGridHelper.ConfigureColumn` 优先显示 .docx 字段说明作为列头 Tooltip
- 优先级：.docx 描述 > `[Display]` 本地化资源 > `[Comment]` 属性

### Ref Inspect UI 改进
- `?` 图标移到 "Ref Inspect" 标题旁，Tooltip 清晰区分 Ctrl+Left/Right/Double-click
- 按钮操作说明全部收入 Tooltip（◀ ▶ Pin Open Full）
- 空状态仅保留一行简洁提示
- 移除底部永久占用的说明文字条

### 新增文件
| 文件 | 说明 |
|------|------|
| `Helper/DocxTextExtractor.cs` | .docx 文本提取与字段描述解析 |
| `Services/FieldDescriptionService.cs` | 字段描述加载/缓存/查询 |
| `Help/zh/ModGuide.md` | 模组制作指南（从 .docx 提取） |

### 修改文件
| 文件 | 关键改动 |
|------|---------|
| `ModGameDataTabsView.axaml.cs` | 内部 buffer 复制粘贴 + `HasFlag` 修复 |
| `BottomToolsViewModel.cs` | Recent Searches + ClearSearch + NavigateToResult |
| `BottomToolsView.axaml` + `.cs` | Enter 键绑定 + Clear 按钮 + 双击 + 样式 |
| `DocumentWorkspaceView.axaml` + `.cs` | 面板列宽持久化 + 显隐空间回收 |
| `App.axaml` | LiveMarkdown.Avalonia 样式注册 |
| `App.axaml.cs` | FieldDescription 初始化 + ModGuide 提取 |
| `Documents.cs` | MarkdownBuilder + LinkCommand + 图片路径预处理 |
| `ReferenceInspectorView.axaml` | ? 图标 + Tooltip 重构 |
| `GenericDataGridHelper.cs` | FieldDescriptions 静态桥接 + Tooltip 增强 |
| `NeoEditor.csproj` | 替换 Markdown.Avalonia → LiveMarkdown.Avalonia |
| `Resources.resx` (×3) | 新增本地化键 |

### 新增本地化键
`ModGuide`, `RefInspect`, `RefInspectPinned`, `RefInspectHelp`, `RefInspectBack`, `RefInspectForward`, `RefInspectPin`, `RefInspectUnpin`, `RefInspectPinHelp`, `RefInspectOpenFull`, `RefInspectOpenFullHelp`, `RefInspectEmptyHint`, `BottomSearchWatermark`, `BottomSearchClear`, `RunValidation`, `ConflictsTab`, `ValidationTab`, `SearchTab`

### 当前已知限制
1. **排序箭头不显示**：Avalonia 11.3 `DataGridSortDescription` 抽象类
2. **Avalonia 版本锁定 11.3.x**
3. **像素画编辑器**：缺少逐像素手绘工具
4. **批量编辑**：多行选中批量改同字段未实现
5. **Markdown 链接内部打开**：LiveMarkdown.Avalonia 1.9.2 `LinkCommand` 绑定需验证运行时行为

---

## Stage 17 — 引用系统重构 (v0.18.0-dev Phase 2) | 2026-06-08

### 新增文件
| 文件 | 说明 |
|------|------|
| `Helper/ReferenceParser.cs` | 纯函数解析层：`ParsedRef` / `TargetKeyInfo` / `ResolvedRefSegment` / `ParsedReferenceField` 类型 + 所有解析方法 |
| `Helper/ReferenceIndex.cs` | Context-aware 引用索引：`(sourceEntityId, propertyName, rawId) → targetEntityId`，O(1) 查找 |

### 重构文件
| 文件 | 变更 |
|------|------|
| `Helper/ReferenceHelper.cs` | 所有方法标记 `[Obsolete]`，委托到 `ReferenceParser` |
| `Helper/ReferenceFieldAttribute.cs` | 不变 |
| `Helper/ReferencePattern.cs` | `IdPattern` / `IdXMultPattern` 新增 `-` 否定前缀剥离（`ExtractRawId("-115")` → `"115"`），`FormatExtraInfo` 报告 `"-"` |
| `Helper/GenericDataGridHelper.cs` | Bug 1 修复（`Convert.ToInt64` 类型安全比较）；新增 `FindBestMatch(sourceEid, propName)` 重载；`LookupSubjectByRawId` 接受 source context；导航路径传递 sourceEid |
| `Views/UserControls/SearchableDataGrid.axaml.cs` | Bug 2 修复（Cell 计数替代 `IndexOf`）；`ColumnMetaCache` 缓存；排序路径安全模式；多值单元格 `Tag=rawText` |
| `Views/UserControls/ModGameDataTabsView.axaml.cs` | NavigateToEntityImpl 改用 `SharedDataGrid` + `DoScrollToEntity` 重试机制 |
| `Views/UserControls/ModGameDataTabsView.Tab.cs` | `SwitchTabItemsSource`（try-catch + 安全重置） |
| `Views/UserControls/ModGameDataTabsView.Data.cs` | `await Index.BuildAsync()` 异步索引构建 |
| `Services/EntityMergeStore.cs` | 新增 `Index` 属性（lazy init `ReferenceIndex`） |

### 迁移文件（ReferenceHelper → ReferenceParser）
`ReferenceResolver.cs` / `DataExportService.cs` / `ReferenceIntegrityRule.cs` / `EditorHelper.cs` / `EntityVisualizers.cs` / `ModGameDataTabsView.Operations.cs`

### 已修复 Bug (7 个)
| # | 问题 | 修复 |
|---|------|------|
| 1 | FindBestMatch `is int val` 对 long/null/EF 代理失效 → 总是返回 id=1 | `Convert.ToInt64` 类型安全比较 |
| 2 | DataGrid 列索引 `Children.IndexOf(cell)` 含 RowHeader 偏移 | Cell 计数 + `ColumnMetaCache` |
| 3 | 渲染与跳转解析不一致（不同路径查不同 key） | 统一走 `index.Lookup(sourceEid, propName, type, rawId)` |
| 4 | 多值单元格用显示文本当 rawId → 解析出垃圾 | `TextBlock.Tag = rawText` |
| 5 | `-` 前缀被当 ID 一部分 → 索引查负数 | `ReferencePattern` 剥离 `-`，`FormatExtraInfo` 报告 |
| 6 | 显示缓存 key 冲突（MergedId vs businessKey） | 缓存 key 改为 EntityId（全局唯一） |
| 7 | DataGrid `RemoveAutoGeneratedColumns` NRE 崩溃 | `SwitchTabItemsSource` try-catch + 安全重置 |

### 架构
- 四层结构：交互层 → 编排层(GDH) → 索引层(ReferenceIndex) → 解析层(ReferenceParser+Pattern)
- Index Build 异步：`Task.Run` 后台线程，不阻塞 UI
- 索引键：`(sourceEntityId, propertyName, rawId)` context-aware
- 查找优先级：context-aware → 同模组主键 → MergedId → 全局主键

---

## Stage 18 — AttackMode 可视化深化 + 数据浏览器引用索引 (v0.20.0-dev) | 2026-06-10

### AttackMode Detail 卡片式重设计

Hero Header（Image + Badge 行 + Name + WieldPhrase 引文 + Notes）：

| 元素 | 设计 |
|------|------|
| 图片区 | 128x128 圆角缩略图，无图片时用 `SymbolIcon`（`Flash` 近战 / `Target` 远程）|
| ID 徽章 | 蓝底白字 `ID: N` |
| 类型徽章 | 绿色近战 / 红色远程，带射程：`Melee (1 tile)` / `Ranged (80 tiles)` |
| 名称 | 18px Bold，自动换行 |
| WieldPhrase | 斜体引文格式，120 字符截断，灰色 `#666` |
| Notes | 12px，灰色 |

Combat Fieldset — 基于 nType 的图标标题（`SymbolIcon` + "Melee Combat" / "Ranged Combat"）+ 进度条：

| 属性 | 条形颜色 | 缩放 |
|------|---------|------|
| Range | `#607D8B` 灰蓝 | max(Range, 10) |
| Cut | `#E53935` 红 | max(Cut, Blunt, 2.0) |
| Blunt | `#FB8C00` 橙 | max(Cut, Blunt, 2.0) |
| Morale | 绿（>25%）/ 红（<25%）/ 灰（=25% 基础值） | Morale 值直接映射 |

穿透：●○ 圆点 + 等级（仅 >0 时显示）
音效：紫色可点击徽章 `▶ cueName`，ToolTip 说明 "embedded in game SWF"
Transfer 标记：绿色文字行

**Attacker Conditions** — 解析 `{id}x{mult}` 模式引用，同 mod 实体优先

**Attack Phrases** — 按半角/全角逗号切分，蓝色 WrapPanel 徽章，显示计数

**Ammo（Charge Profiles）** — 解析 ChargeProfile 引用，显示计数标题 + 可点击徽章（Ctrl+Click 导航）

### 统一引用解析 — `LookupRef`

**问题**：可视化器用 `GetDedupedInt<T>()` 自己建字典 → 按 `ModId` 去重，DataGrid 用 `ReferenceIndex.Lookup()` 上下文感知解析。两套路径不一致，引用解析频繁出错。

**根本解决方案**：
- `ReferenceResolver.LookupRef<T>(sourceEntity, propertyName, rawId)` — 唯一入口
  - 优先：`ReferenceIndex.Lookup(sourceEid, propName, targetType, rawId)` — 与 DataGrid 完全相同
  - 回退：`EntityModNames` 同 mod 优先 — 与 `ReferenceIndex.ResolveTargetEntityId` 同逻辑
  - 回退：最高 `ModId`
- `NavigateToByKeyFor<T>(key, sourceEntity)` — 导航入口，改用 `Index.LookupGlobal`
- 可视化器不再建字典，每 ID 逐走 `LookupRef`
- `GenericDataGridHelper.ActiveMergeStore` — 新增公开属性供 `LookupRef` 访问索引

### 数据浏览器引用索引

**架构**：复用与合并视图相同的 `EntityMergeStore` → `ReferenceIndex` 管道。

| 组件 | 职责 |
|------|------|
| `EntityBrowserDocument.RebuildBrowserIndexAsync()` | 为全部 24 类型创建 `EntityMergeStore`，填充 `ReferenceLookups` + `EntityModNames`，`Index.BuildAsync()` 构建索引，调用 `SetActiveStores` |
| `EntityBrowserDocument.InvalidateIndex()` | 在 mod/profile 变更后设置 `_indexBuilt = false` |
| `DataBrowserViewModel` | 监听 `SaveProfileMessage` / `RefreshModMessage` / `InitModMessage` / `CellEditedMessage` 自动失效 |
| 侧边栏 "Rebuild Index" 按钮 | `Symbol.ArrowSync` 图标，绑定到 `RebuildIndexCommand` |

**调用时机**：
- 首次 `EntityBrowserDocument` 打开时惰性构建
- 侧边栏按钮手动触发重建 → 全量重建 Store + Index
- Mod/Profile 变更 → 自动失效 → 下次浏览器打开时重建

### AttackPhrases 分隔符

从仅支持半角逗号 `Split(',')` 改为 `Split(',', '，')`，正确处理中文标点。

### nType 图标：文本 → FluentIcons

所有 nType 图标（detail hero 占位图、combat 标题）从文字字符替换为 `SymbolIcon`：
- 近战 `Symbol.Flash`
- 远程 `Symbol.Target`

### 数据浏览器 ListBox 搜索过滤

`DomainBrowserView.axaml` — 列表上方新增 `Watermark="Filter..."` 的 TextBox，按 `DisplayName` 和 `EntityId` 大小写不敏感匹配。`_allEntities` 保留完整后备列表，`ApplyFilter()` 重建 `Entities`。

### 设计原则

- **可视化器不直接访问数据库**：通过 `ReferenceResolver.LookupRef<T>()` 解析，优先走 `ReferenceIndex.Lookup`（与 DataGrid 同源），回退 `EntityModNames`
- **单一路径引用解析**：`ReferenceIndex` 是唯一真实数据源。可视化器和 DataGrid 走同一套解析逻辑，杜绝双路径不一致
- **上下文感知解析**：无命名空间前缀的引用优先在同一 mod 内解析；带命名空间前缀的引用在指定命名空间内查找
- **索引已缓存**：活跃 merge store 成为所有引用的真实数据源；无数据重复
