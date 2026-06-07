# Stage 5 开发方向审视与建议

## 一、原始计划 vs 修正建议

原始 CHANGELOG 中 Stage 5 计划：
- 数据导出框架（合成表 CSV、物品百科 Markdown、战利品表 JSON）
- 自定义编辑器接口（配方可视化、地图六边形编辑、剧情树可视化）
- 批量操作增强
- Undo/Redo 命令系统

修正后的优先级排序：

| 优先级 | 功能 | 受益用户 | 状态 |
|--------|------|----------|:----:|
| **P0** | Undo/Redo 命令系统 | 所有人 | 新增 |
| **P0** | 字段帮助系统（`[Comment]` → UI Tooltip） | 小白为主 | 新增 |
| **P0** | 引用显示增强（裸ID → `名称 (id=N)`） | 所有人 | 新增 |
| **P1** | 数据验证框架 | 小白为主，老手受益 | 新增 |
| **P1** | 反向引用查询（"谁在引用我"） | 所有人 | 新增 |
| **P2** | 配方可视化编辑器 | 小白+老手 | 来自原计划 |
| **P2** | 剧情树可视化 | 老手为主 | 来自原计划 |
| **P2** | TreasureTable 树状展开 | 所有人 | 新增 |
| **P3** | 批量编辑（多行改同一字段） | 老手 | 来自原计划 |
| **P3** | 实体复制/模板 | 小白+老手 | 新增 |
| **P3** | 数据导出 | 老手+社区 | 来自原计划 |

核心思路：**先让已有功能好用，再扩展新功能**。Stage 1-4 搭好了骨架，Stage 5 应该先把肉填上。

---

## 二、用户视角审视

### 用户A：Mod 小白（刚读完模组制作指南）

**痛点1：字段名看不懂**

在 DataGrid 里看到列头 `strNotes`、`nCondID`、`vDegradeTreasureIDs` 时完全不知道含义。代码中 `[Comment]` 属性已有详细中文说明（内容来自 `游戏XML文本各项说明修正增强版.docx`），但 **UI 完全没有展示**。`GenericDataGridHelper.ConfigureColumn` 只读取 `[Display]` 给 tooltip，而 `[Display]` 只是短英文名如 "Id"、"Name"。

**关键发现**：`[Comment]` 属性被"锁"在 EF Core 数据库迁移定义里，用户永远看不到——这是**最大的信息浪费**。

**痛点2：引用字段看不懂**

看到 `nFaction = "1"` 需要手动去 Faction 表查 `id=1` 是什么。当前引用列在显示模式下只展示裸ID（去掉了 `0:` 前缀），仅在编辑模式下 ComboBox 才显示 `id: name`。

**痛点3：新增物品不知所措**

要加一个新物品，需要理解 Target Mod（Insert vs Merge）、Target XML（文件路径）、Key ID 分配、关联图片等概念。`AddRowDialog` 已简化部分流程，但向导性不足。

### 用户B：文本编辑器老手（用 Notepad++/VS Code 写了几年 XML）

**核心问题："我为什么要用这个编辑器？"**

老手已掌握所有字段名和格式，有自己成熟的搜索/替换工作流。编辑器需要提供**文本编辑器做不到的价值**：

| 编辑器优势 | 当前状态 |
|-----------|:------:|
| 合并视图 + Mod 叠加可视化 | 已实现，但缺少汇总报告 |
| 跨表引用导航 | 已实现 |
| 覆盖链可视化 | 已实现 |
| 字段级来源/冲突标记 | 已实现，但缺少汇总报告 |
| 反向引用查询 | **缺失** — 删一个 TreasureTable 不知道谁在用它 |
| 批量操作 | **缺失** — 改 50 个物品的属性要逐个点 |
| 键盘快捷键 | **缺失** — Ctrl+Z/Ctrl+D/... |
| 实体复制为模板 | **缺失** — 最常用的创建新实体方式 |
| 数据有效性自动检查 | **缺失** — 引用断裂、值域越界不报警 |
| Undo/Redo | **缺失** — 最基础的编辑保障 |

---

## 三、各功能详细设计

### P0-1: Undo/Redo 命令系统

编辑器的**根基**。没有它用户不敢大胆编辑。

利用已预留的 `Data/Command/` 目录：

```
IEditorCommand
├── Execute() / Undo() / string Description
├── EditCellCommand(entityId, colName, oldValue, newValue)
├── AddEntityCommand(entity, entityType)
├── DeleteEntityCommand(entity, entityType)
└── CommandHistory
    ├── Stack<IEditorCommand> _undoStack (max 100)
    ├── Stack<IEditorCommand> _redoStack
    ├── Execute(cmd) → 执行 + 推入 undo + 清空 redo
    ├── Undo() / Redo()
    └── CanUndo / CanRedo (绑定按钮 IsEnabled)
```

快捷键：
- `Ctrl+Z` → Undo
- `Ctrl+Y` 或 `Ctrl+Shift+Z` → Redo

`ModGameDataTabsView` 持有 `CommandHistory` 实例。所有编辑操作（CellEditEnding、AddRow、DeleteRow）通过 `CommandHistory.Execute()` 执行。编辑后自动 `SetDirty(true)`。

### P0-2: 字段帮助系统

**现状**：`ConfigureColumn` 读取 `[Display]` 给列头 tooltip，内容为短英文标识符。

**改造**：优先使用 `[Comment]` 属性内容，fallback 到 `[Display]`：

```csharp
// GenericDataGridHelper.ConfigureColumn 中
var commentAttr = property.GetCustomAttribute<CommentAttribute>();
var displayAttr = property.GetCustomAttribute<DisplayAttribute>();

string tooltip;
if (commentAttr != null)
    tooltip = commentAttr.Comment;        // "攻击方式的名称，显示在游戏右下角的武器"
else if (displayAttr != null)
    tooltip = localizer(displayAttr.Name); // "Name"
else
    tooltip = property.Name;              // "strName"

ToolTip.SetTip(headerPanel, tooltip);
```

**需要补充的工作**：系统地检查所有 24 个实体的 `[Comment]` 属性，确保每个字段都有中文说明。docx 文档 `游戏XML文本各项说明修正增强版.docx` 已有完整内容，可直接参考导入。

更进一步，可在列头右键菜单添加 "这个字段是什么？" 选项，弹出更详细的说明（格式、取值范围、关联表等）。

### P0-3: 引用显示增强

**现状**：
- 单值引用显示模板：`ReferenceHelper.FormatForDisplay(raw)` → 去掉 `0:` 前缀 → 显示 `"1"`
- 多值引用显示模板：直接显示原始字符串 `"10,11,12"`

**改造**：在 CellTemplate 的显示模板中解析引用并显示目标名称。利用已有的 `ReferenceLookups` 和 `LookupSubject()` 方法：

```
现在显示: 1
改为显示: Dogman (id=1)

现在显示: 12,13,14
改为显示: Punch, Kick, Bite

现在显示: 211x1.0,NSE:42x1.0
改为显示: Bleeding x1.0, NSE:Tough x1.0
```

性能考虑：`FuncDataTemplate` 内每行都做字典查询可能慢（Encounters 有 2264 条）。可用 `Dictionary<(Type, int), string>` 做 `TargetType + Id → Subject` 的预计算缓存，在 `ReloadMergeTabsAsync` / `ReloadTabsAsync` 时一次性填充。

### P1-1: 数据验证框架

保存前自动检查以下规则：

| 类别 | 规则 | 严重度 |
|------|------|:----:|
| 引用完整性 | `[ReferenceField]` 的值指向存在的实体 | Error |
| 必填字段 | `strName` 等关键字段不为空 | Error |
| 值域 | `fChance` ∈ [0.0, 1.0]、ID 不重复 | Warning |
| ID 范围 | 业务主键不与同 Mod 内其他行冲突 | Error |

验证结果在保存前展示为报告：

```
验证报告:
✘ 3 Errors:
  - ItemType #42: nTreasureID="999" → TreasureTable #999 不存在
  - Creature #7: nFaction="15" → Faction #15 不存在
  - Recipe #95: strName 为空

⚠ 2 Warnings:
  - EncounterTrigger #3: fChance="5.0" 超出 [0.0, 1.0] 范围
  - ItemType #103: fDurability="-1" 负数
```

Error 阻止保存，Warning 可以忽略。

### P1-2: 反向引用查询

在行右键菜单添加 "查找引用者..." 选项。点击后扫描所有表的所有 `[ReferenceField]` 字段，找出引用了当前实体的全部条目：

```
▼ 以下实体引用了 TreasureTable #23 "Junk store inventory":
  ├─ [ItemType] #12 "plastic bag" → nTreasureID
  ├─ [ItemType] #15 "backpack" → nTreasureID
  ├─ [Recipe] #95 "foil scraps" → nTreasureID
  ├─ [Creature] #3 "Dogman" → nTreasureID
  └─ [HexType] #5 "forest" → nTreasureID
```

每个条目可点击跳转。需要建立反向索引 `Dictionary<(Type targetType, int targetId), List<(Type sourceType, string sourceCol, int sourceId)>>`。在 `ReloadMergeTabsAsync` 时构建。

### P2-1: 配方可视化编辑器

Recipe 数据结构天然适合可视化：

```
┌─────────────────────────────────────────┐
│  配方 #95: foil scraps                  │
│  类型: misc    耗时: 0.01h              │
│                                         │
│  ┌──────────┐                           │
│  │ 工具      │  不需要工具               │
│  └──────────┘                           │
│       ↓                                 │
│  ┌──────────┐                           │
│  │ 消耗      │  1x aluminium (ingredient)│
│  └──────────┘                           │
│       ↓                                 │
│  ┌──────────┐                           │
│  │ 产物      │  TreasureTable #682      │
│  │          │  → "aluminium foil"       │
│  └──────────┘                           │
└─────────────────────────────────────────┘
```

实现方式：实现 `ICustomTableEditor` 接口（架构文档已设计），为 Recipe 注册专用编辑器。在 Tab 中点击 Recipe 表时自动切换到可视化面板。

### P2-2: 剧情树可视化

Encounter 的 `aResponses` 字段定义了选项→下一个 Encounter 的跳转关系，这形成**有向图**。

可用节点图展示：
- 每个节点 = 一个 Encounter（显示 strName / id）
- 边 = Response 选项（显示选项标签/物品名）
- 可拖拽节点、点击编辑、新建连接

这是编辑器**比文本编辑器强得多的杀手级功能**。2264 条 Encounters 的剧情网络在文本编辑器里几乎无法理解，在可视化图中则一目了然。

### P2-3: TreasureTable 树状展开

TreasureTable 的 `aTreasures` 格式 `411x1x3-5|412x1x1-2,...` 是嵌套结构。在行详情面板中递归展开：

```
▼ TreasureTable #23 "Junk store inventory"
  ├─ TreasureTable #411 x1 (100%) x3~5  "Random food"
  │   ├─ ItemType 32.1 x1 (6.25%) x1~1  → 罐头
  │   ├─ ItemType 101.1 x1 (6.25%) x1~1 → 水瓶
  │   └─ ... (16 items)
  ├─ TreasureTable #412 x1 (100%) x1~2  "Random weapon"
  │   └─ ... (8 items)
  └─ ItemType 394 x1 (100%) x1~3 → 破布
```

`|` 分隔 = 互斥（OR），`,` 分隔 = 共存（AND）。展开时需要递归解析（TreasureTable 可以引用 TreasureTable）。

### P3-1: 批量编辑

选中多行 → 右键 → "编辑选中行的..." → 弹出字段选择器 → 输入新值 → 确认后批量应用。

每次修改生成一个 `EditCellCommand`，全部纳入 CommandHistory 的同一次 Undo 分组。

### P3-2: 实体复制

右键行 → "复制为新实体" → 弹出简化版 AddRowDialog（ID 自动设为 max+1，其余字段从源复制）。

高频场景：创建类似物品（如 "大背包" 复制自 "背包"，只改容量和名称）。

### P3-3: 数据导出

- **合成表 CSV**：Recipe + Ingredient + TreasureTable → 完整合成路径 → CSV
- **物品百科 Markdown**：ItemType → 名称/重量/属性/描述 → 表格
- **TreasureTable JSON**：递归展开嵌套 → 结构化 JSON
- **剧情文本 Markdown**：Encounters → 对话文本 → 便于校对和翻译

---

## 四、方向修正总览

| 原计划 | 修正后 | 调整原因 |
|--------|--------|----------|
| 数据导出框架 | 降为 P3 | 非核心编辑痛点，导出是锦上添花 |
| 自定义编辑器接口 | 保留 P2，先做配方和剧情 | 需先做接口抽象，选两个最高价值的实现 |
| 批量操作增强 | 降为 P3 | 需先有 Undo 保护，且优先级低于字段帮助 |
| Undo/Redo | 升为 P0 | 编辑器的根基，没有它用户不敢编辑 |
| — **新增** 字段帮助系统 | P0 | `[Comment]` 数据已存在，UI 未展示——最大浪费 |
| — **新增** 引用显示增强 | P0 | 引用列显示裸ID，可读性差，小白无法理解 |
| — **新增** 数据验证 | P1 | 防止游戏崩溃，降低试错成本 |
| — **新增** 反向引用查询 | P1 | 删除前评估影响，老手刚需 |
| — **新增** TreasureTable 树 | P2 | 核心数据结构的可视化，影响面广 |
| — **新增** 实体复制模板 | P3 | 高频操作，实现代价低 |

---

## 五、实现顺序建议

```
Week 1-2:  P0-1 Undo/Redo (基础设施)
           P0-2 字段帮助系统 (Comment → Tooltip)

Week 3:    P0-3 引用显示增强 (裸ID → 名称)

Week 4:    P1-1 数据验证框架
           P1-2 反向引用查询

Week 5-6:  P2-1 配方可视化编辑器
           P2-2 剧情树可视化 (调研+原型)

Week 7:    P2-3 TreasureTable 树状展开
           P3-2 实体复制模板

Week 8+:   P3-1 批量编辑
           P3-3 数据导出
```
