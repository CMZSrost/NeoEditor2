# NeoEditor 开发状态总览

> 更新日期：2026-06-07 · 版本 v0.18.0-dev · 基于 Stage 16

---

## 整体进度

```
核心编辑功能     ██████████████████  95%
UI / 面板系统    ████████████████░░  90%
数据可视化       ████████░░░░░░░░░░  40%  ← ItemType 完成，其余待跟进
数据验证与诊断   ██████████░░░░░░░░  50%
架构重构         ██████████████░░░░  80%
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

### 引用系统
| 功能 | 状态 |
|------|:--:|
| 46+ ReferenceField（多值/复合键/SecondaryTarget） + ReferencePattern 策略（5 实现） | ✅ |
| Ctrl+左键 = 跳转（含 SecondaryTarget fallback），Ctrl+右键 = Peek | ✅ |
| Ctrl+Hover 显示 Subject + 反向引用查询 | ✅ |
| TreasureTable `aTreasures` 多段 x 格式解析已修复 | ✅ |

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
| 已实现 visualizer：17 个（ItemType / Recipe / TreasureTable / Encounter / Creature / Condition / AttackMode / BattleMove / HexType / Faction / Ingredient / ItemProp / EncounterTrigger / CampType / ChargeProfile / ContainerType / CreatureSource / DmcPlace + Default） | ✅ |
| ItemType visualizer 卡片式重设计（主图+画廊+stat bar+属性标签+引用条） | ✅ |
| ⚠️ 其余 visualizer 内容为纯文本 TreeView，缺乏图片/关系图等真正可视化 | 🔴 待开发 |
| ⚠️ 嵌套 Dock（查看区 DockControl）在 `Dock.Avalonia 11.3.11.16` 上无法渲染，已改用 TabControl | 🟡 待调查 | |

### 可视化系统
| 实体 | 旧编辑器(ICustomTableEditor) | 新 Detail | 新 Overview | 状态 |
|------|-----------|-----------|-------------|:--:|
| Recipe | Recipe Tree | 纯文本 | 纯文本 | ⚠️ 骨架就绪，需丰富 |
| Encounter | Story Graph | 纯文本 | 纯文本 | ⚠️ 骨架就绪，需丰富 |
| TreasureTable | Treasure Tree | 纯文本 | 纯文本 | ⚠️ 骨架就绪，需丰富 |
| ItemType | SpriteShow+WearShow | 纯文本 | 纯文本 | ⚠️ 骨架就绪，需丰富 |
| 其余 20 类型 | EntityOverviewEditor | 属性树 | 字段列表 | ⚠️ 骨架就绪，需丰富 |

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
| ModDatabase Expander 箭头遮挡文字 | 需调整 Padding |
| IMessenger.Send 单参数重载不可用 | CommunityToolkit.Mvvm 8.4.0 疑似只有双参数 Send<T,TToken> |
| 侧边栏 Import 按钮弹两次对话框 | 已移除 FilePicker 回退，待验证 |

### P0 — 当前焦点：可视化内容丰富

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
