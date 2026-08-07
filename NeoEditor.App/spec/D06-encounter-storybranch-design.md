# D06 — Encounter 剧情分支可视化重构设计（节点单组件 / 去重定位 / Mermaid 对齐）

> 设计文档 · 2026-08-08 · v1.1（v1.1 = 节点精简为「图片+标题+概率」三要素，复杂信息移入 tooltip 信息卡——用户 2026-08-08 追加反馈）
> 上承：D05 Creature 可视化设计（模板与决策体系沿用）+ 用户对「剧情分支」Tab 的反馈（2026-08-08）
> 下启：`EncounterEntityVisualizer.cs` 剧情分支区重构实现
> 依从：R04 View 只组装 · R13 VisHelper 内部组件 · N03 视图无逻辑 · D05 设计语言（区块语义色 / 徽章 / 卡片）
> 关联实现：`NeoEditor.Plugins.EntityEditor/Visualizers/EncounterEntityVisualizer.cs`（1435 行，本文档即其剧情分支区（389-1007 行）的重构目标；其余区块仅做职责边界调整）
> 数据来源：`field_descriptions.json`（`encounters.*` 22 条实测）+ `Encounters.cs`（19 列）+ `GameEnum.cs`（EncounterType 实测值域 0-3）
> 参考实现：`BuildResponsesPanel`（紧凑响应列表——本设计的卡片密度参考）、`CreatureVisualizerTests.cs`（测试手法）、D05（文档结构与区块语言）

---

## 一、定位与设计原则

**剧情分支可视化的定位**：回答一个问题——「**这个剧情选项会通向哪**」。
它是**当前剧情 → 各响应分支**的单层图，不是「谁通向这里」（反向），不是「整条剧情链」（多跳），
也不是「这个剧情有什么」（Hero / Refs）。

**四条设计原则**：

1. **一个节点 = 一个组件，只保留三个核心要素**：**图片（记忆）、标题（目标）、概率（可能性）**。
   节点卡 = 52px `strImg` 缩略图 + 标题 + 概率胶囊（最多加 ID/类型 两个 9px 小 chip）。
   其余信息——描述、条件及满足情况、物品触发——一律收进 **tooltip 信息卡**（hover 查看），
   不占卡片布局（用户 2026-08-08 追加反馈：较复杂的信息呈现通过 tooltip 信息卡做即可）。
2. **同一信息只呈现一次（同页去重）**。分支图只负责「当前→分支」；
   反向引用统一由页面底部被引用面板承担；Hero 上下文徽章（🎒🐾📋）只属于 Hero——
   分支节点只显示**分支相关**信息（物品、概率、分支可达性），两者职责分清（用户反馈 1）。
3. **图形与 Mermaid 同数据、同信息层**。Mermaid 源码与图形 Tab 由**同一份分支数据模型**
   生成，节点=名称+ID、边=物品+权重（有效概率），结构性消除两套渲染各自漂移
   （用户反馈 2）。
4. **保留已验证的交互与格式**。前置条件复选框过滤 + 概率重算（现有功能，用户未抱怨）；
   `aResponses` 的 `[itemId]x[mult]=[encounterId]x[weight]x0x0x0` 解析与权重→概率归一逻辑不变。

## 二、用户反馈与现状问题对照

| 用户反馈 | 现状表现（代码位置） | 问题根因 | 设计对策 |
|----------|----------------------|----------|----------|
| 1. 多个组件定位重复，没有做出区别 | 「谁引用我」被**三处**渲染：① 分支图左列反向引用橙卡（782-874 行）；② 剧情分支 Tab 内「👈 Referenced By」反向链面板（977-981 行调用，1136-1255 行实现）；③ 页面底部被引用面板（1433-1434 行 → VisHelper `BuildReverseRefsPanel`） | 分支图试图同时承担「正向分支」「反向引用」「响应列表」三个问题，职责边界未划分 | 分支图只回答「当前→分支」；反向信息统一收到底部被引用面板（§六） |
| 1（续） | 分支节点内的条件徽章（712-745 行）与页面 Refs 面板 PreConditions 区（1322-1341 行）、Hero/链树 📋 徽章重复；目标卡片（747-771 行）与下方 ResponsesPanel 的目标徽章（250-257 行）重复 | 同一信息在多个区块各自渲染，无「谁负责什么」的约定 | 条件/目标/物品/概率全部收进分支卡单组件（§四）；Hero 徽章只留 Hero（§六） |
| 2. 写的 mermaid 信息量变化很大 | Mermaid（516-589 行）与图形（661-774 行）是**两套独立生成的渲染**：图形有概率胶囊/条件徽章/物品徽章，Mermaid 节点只带 ctx 标签（🎒🐾📋pre:n）；且 Mermaid 含反向 R 节点，与图形左列重复 | 无共享数据模型，双份渲染必然漂移 | 共用 `BranchData` 数据源，Mermaid 与图形同一信息层（§七） |
| 3. 每个节点应做成图片+title 的单组件，信息组件内布局 | 分支节点 = 4-5 个**竖排**元素：物品徽章（685-695）→ 概率胶囊（698-709）→ 条件徽章行（712-745）→ 目标徽章（747-771），无图片、无标题层级 | 节点信息以「徽章堆叠」而非「组件内布局」呈现，视觉散乱 | 节点卡 = 52px 缩略图 + 标题 + 最多 2 行信息（Grid 双列）（§四） |

**现状代码索引**（实现时对照）：

| 区块 | 位置 |
|------|------|
| `BuildDetail` 组装顺序 | 38-61 行 |
| `BuildHeroHeader`（Hero 图片加载参考） | 63-156 行（`_vis.LoadImage(enc.Image)` + `OpenZoomableImage`） |
| `BuildResponsesPanel`（紧凑列表，本次合并） | 173-291 行 |
| `ParseResponseEntries`（解析 + 概率归一，逻辑保留） | 293-385 行 |
| `BuildStoryBranchDiagram`（本次重构主体） | 389-1007 行 |
| `BuildEncounterChainTree`（剧情链 Tab，保留） | 1010-1133 行 |
| `BuildReverseChainPanel` / `BuildReverseChainTree`（从分支图移除） | 1136-1255 行 |
| `BuildRefsPanel`（Hero 之下各引用字段明细，保留） | 1257-1410 行 |
| `BuildReverseRefsPanel`（底部被引用面板，唯一反向入口） | 1433-1434 行 → `VisHelperService.BuildReverseRefsPanel`（724-775 行） |

## 三、总体布局（重构后的分支图）

`BuildDetail` 页面顺序调整为：Raw Data → **Hero**（含 🎒🐾📋 上下文徽章，不动）→ 剧情文本 →
**剧情分支**（含合并后的响应列表）→ Refs 面板 → 触发器 → **被引用面板**（页面底部，唯一反向入口）。
独立 `BuildResponsesPanel` 区块从页面移除（决策见 §九）。

分支图内部结构：

```
┌─ 剧情分支 ──────────────────────────────────────────────────────────────┐
│ 格式提示（9px 灰，自原 ResponsesPanel 移入）：                            │
│   [物品ID]x[数量]=[剧情ID]x[权重] · 空物品(=开头)=无需物品的选项 · 概率=权重/权重和 │
│ 前置条件过滤（仅当分支目标存在前置条件时显示，交互保留）：                    │
│   ☑ 醉酒   ☐ ¬宿醉   …                                                    │
│ ┌────────────────────────────── 横向滚动 Card ──────────────────────────┐ │
│ │  ┌──────────────────┐       ┌──────────────────┐                      │ │
│ │  │ 当前剧情（蓝卡）   │  →    │ 分支卡 1（绿边）  │                      │ │
│ │  │ ┌──┐ 便利店        │       │ ┌──┐ 逃跑         │                      │ │
│ │  │ │图│ ID 236 · 战斗 │       │ │图│ ID 238 · 剧情 │                      │ │
│ │  │ └──┘             │       │ └──┘  68%(1.4)    │                      │ │
│ │  └──────────────────┘       │ └─ hover → tooltip ┘                     │ │
│ │                                →  →  →  （每条边一个箭头，灰 16px）      │ │
│ │                               ┌──────────────────┐                     │ │
│ │                               │ 分支卡 2（半透明）│ ← 前置条件不满足时   │ │
│ │                               │ ┌──┐ 战斗         │                     │ │
│ │                               │ │图│ ID 240 · 搜刮 │                     │ │
│ │                               │ └──┘  0%(0.6)    │                     │ │
│ │                               └──────────────────┘                     │ │
│ └───────────────────────────────────────────────────────────────────────┘ │
└───────────────────────────────────────────────────────────────────────────┘
```

- **两列布局**（当前 | 分支），保留现有「→」箭头连线：左列 = 当前剧情节点卡（垂直居中），
  右列 = 分支卡 StackPanel（纵向堆叠，横向居中）。左列反向引用橙卡列整体移除。
- 布局在横向 ScrollViewer 内（保留现状，`MaxHeight` 放宽至 500 以容纳卡片）。
- 分支卡宽度固定 220-260px，多分支时纵向增长，不出现水平挤压。
- 分支图内**不再出现**：反向引用橙卡、👈 Referenced By 反向链面板、根剧情标识
  （均为反向信息，收敛到页面底部被引用面板）。
- 分支图上方保留 TabControl：`剧情分支 | 剧情链 | Mermaid源码`（剧情链 Tab 保留现状，见 §六）。

## 四、节点单组件设计（核心规格，v1.1 精简版）

### 4.1 共用组件：`BuildEncounterNodeCard`

新增私有组件（建议放 visualizer 内，必要时上移 VisHelperService）：
`BuildEncounterNodeCard(Encounter e, NodeCardOptions opts)`，当前卡与分支卡共用同一视觉语言，
仅 `opts`（isCurrent / 物品+概率 / 是否可导航）不同。

**v1.1 精简规格（用户 2026-08-08）：卡片只保留三要素——图片（记忆）、标题（目标）、概率（可能性）**：

```
┌────────────────────────────────────┐  分支卡规格（折叠态）
│ ┌─────┐ 便利店抢劫                  │  ← 标题 12px SemiBold #333，可换行（与图片同行）
│ │ 52  │ ID 237 · 剧情               │  ← 行1（辅助 chip 行）：ID chip + 类型 chip
│ │ px  │                            │     （9px，无「📋×n」——条件进 tooltip）
│ │ 图  │ ─────────────────────────  │  ← 行2：概率胶囊（权重+有效概率）
│ │ 片  │      68%(1.4)              │     无物品常显（物品信息进 tooltip）
│ └─────┘                            │
│   ↑ hover → tooltip 信息卡          │
└────────────────────────────────────┘
```

| 部件 | 规格 | 说明 |
|------|------|------|
| 整体 | Border，CornerRadius 6，Padding 10，Width 220-260，MinHeight 与图片区匹配 | 当前卡：bg `#E3F2FD` / 边框 `#1565C0` 2px + 顶部 8px 标签「📍 当前剧情」；分支卡：bg `#FAFAFA` / 边框 `#E0E0E0` 1px；不满足前置条件的分支卡：`Opacity 0.5`（保留现状语义） |
| 图片区（左列 52px） | 52×52 方形，CornerRadius 6，`ClipToBounds`，bg `#0A000000`；`_vis.LoadImage(e.Image)`（与 Hero 同源加载，内部 `StripNs` 处理 `0:` 命名空间前缀），`Stretch.Uniform` | 点击 → `_vis.OpenZoomableImage(bmp, e.Subject ?? e.Name)`（与 Hero 一致）；**无图 → `SymbolIcon`（`Symbol.BookOpen`，24px，`#999`）居中兜底，不崩溃**；当前卡与分支卡一致 |
| 标题（右侧文本区） | `e.Subject ?? $"Enc #{e.Id}"`，12px SemiBold #333，TextWrapping.Wrap，与图片同行（图片行即标题行） | 分支卡整卡 `_refNode.WireNavigation`（Ctrl+Click 跳转 / Ctrl+RMB peek） |
| 行1：ID + 类型（辅助 chip） | `ID: {id}` chip（`#E3F2FD`/`#1565C0`）+ 类型 chip（映射见 4.2） | 9px 小 chip；**v1.1 起不再显示 📋×n 计数**（条件信息进 tooltip） |
| 行2：概率胶囊 | 概率胶囊（权重+有效概率），右对齐或居中（见 4.4） | 三要素之一「可能性」；无物品常显（v1.1：物品徽章移入 tooltip） |
| **tooltip 信息卡**（v1.1 核心变化） | 分支卡整卡挂 `ToolTip`，内容为分支专用信息卡：**描述**（strDesc，截断 ~200 字）+ **前置条件及满足情况**（每条：¬ 前缀 + 名称 + 当前过滤状态 ✓满足/✗不满足 着色，简单信息补充）+ **物品触发**（🛡 物品×n，若有）+ 类型/ID | hover 查看；与 `BuildRefTooltip` 机制一致但内容为分支专用；**不占用卡片布局** |

**高度目标**：折叠态 = 图片 52px + 两行信息（2×~18px）+ 内边距，≤ 96px；
「卡片 = 图+标题+概率（+ID/类型 chip）」是硬约束——描述/条件/物品等复杂信息一律进 tooltip，
禁止追加常显徽章行。

### 4.2 类型 chip（补齐实测值域）

现状只区分 `Scavenge / Normal`（108-116 行），实测 `nType` 值域 0-3
（`field_descriptions.json` `encounters.type`：0=普通剧情，1=搜刮，2=战斗（仅 id236），3=破解）。
建议抽取共享映射（Hero / 分支卡 / 链树统一复用，消除现状 3 处各写各的）：

| 原始值 | 标签 | 配色 |
|--------|------|------|
| 0 | 剧情 | `#E3F2FD` / `#1565C0` |
| 1 | 搜刮 | `#FFF3E0` / `#E65100` |
| 2 | 战斗 | `#FFEBEE` / `#C62828` |
| 3 | 破解 | `#F3E5F5` / `#6A1B9A` |
| 其他 | `类型{raw}` | `#F5F5F5` / `#999`（灰色兜底，不崩溃） |

> 枚举 `EncounterType` 只有 Normal/Scavenge 两项（GameEnum.cs 62-67 行），2/3 按
> `(int)enc.Type` 原始值渲染；**不改枚举**（保持与模型一致，渲染层映射即可，与 D05 §4.5
> 「剧情/搜刮/战斗/破解」四标签口径一致）。

### 4.3 物品触发信息（v1.1：进 tooltip）

- 有物品（`resp.Item is not null`）：tooltip 内显示 `🛡 {Item.Description}{×mult}` 行，
  复用 `_refNode.BadgeForEntity` 的导航能力或纯文本——tooltip 内不放可交互导航（tooltip 是浮层），
  用纯文本描述 + 名称即可（跳转走卡片本身）。
- 物品未解析（`resp.ItemId is not null` 但查找失败）：tooltip 显示灰色 `Item #{id}` 行。
- 无物品（`=开头` 的空物品选项）：tooltip 不显示物品行（去噪）。

### 4.4 概率胶囊（权重 + 有效概率）

- 文本：`{Weight:F1}({EffectiveProb:P2})`（格式与现状一致）。
- 有效概率 = `Weight / ValidTotalWeight`（ValidTotalWeight = 满足当前过滤的 Σ 权重）；
  不满足过滤的分支 `EffectiveProb = 0` → 胶囊灰 `#999` + 卡片 Opacity 0.5（保留现状语义）。
- 胶囊底色按有效概率：≥50% 绿 `#2E7D32` / ≥10% 橙 `#E65100` / 其余灰 `#999`（白字 9px Bold，现状同款）。

### 4.5 数据模型（图形与 Mermaid 共用，结构性对齐）

从 `ParseResponseEntries` 结果推导一份纯数据，两个渲染都从它生成：

```csharp
record BranchData(
    int TargetId,
    Encounter? Target,                       // resolved 目标（null = 未解析）
    string? ItemId, double ItemMult, ItemType? Item,
    double Weight, double EffectiveProb,     // EffectiveProb=0 表示被过滤
    bool IsSatisfied,                        // 前置条件在当前过滤下是否满足
    List<(string Raw, bool IsNeg, Condition? Resolved)> PreConds);
```

- `PrepareBranches(Encounter enc, ISet<string> activePreConds) → (List<BranchData>, double ValidTotalWeight)`
  —— **纯函数**，可单测数值（过滤后概率重算的正确性在函数内验证，不依赖 UI）。
- 分支卡渲染、tooltip 信息卡内容、`BuildMermaidText` 都只消费 `List<BranchData>`，
  三者结构性不可能漂移。

## 五、交互设计

| 交互 | 行为 | 现状 → 重构 |
|------|------|-------------|
| 跳转 | 分支卡（resolved）Ctrl+Click → 目标 Encounter，Ctrl+RMB → peek | 保留（WireNavigation 从目标徽章移挂到整卡） |
| **tooltip 信息卡**（v1.1 核心） | 分支卡整卡 hover → 信息卡：**描述**（strDesc 截断 ~200 字，9-10px）+ **前置条件及满足情况**（每条 `¬` 前缀 + 名称 + 当前过滤状态 ✓满足/✗不满足 着色；简单信息补充）+ **物品触发**（🛡 物品×n，若有）+ 类型/ID 行 | **新增**（v1.1：复杂信息从卡片移入 tooltip；条件满足情况随过滤实时刷新——勾选复选框后 tooltip 同步更新） |
| 图片放大 | 缩略图点击 → `OpenZoomableImage`（标题 = 节点名） | 保留（Hero 同款，79-83 行参考） |
| 前置条件过滤 | 复选框区（分支目标前置条件的并集，☑/¬ 样式不变）：勾选 → 重算 ValidTotalWeight → 不满足分支卡 Opacity 0.5 + 概率 0%，其余分支概率按新总和重算；**同步刷新卡片、tooltip 与 Mermaid** | 保留（662-674 行逻辑原样平移进纯函数） |
| 当前卡 | 显示「📍 当前剧情」标识；**不接导航**（本身就是当前实体） | 保留现状语义 |
| Mermaid | 只读源码文本（可复制），随过滤实时刷新 | 保留（§七） |

## 六、与周边组件职责边界（什么留在分支图、什么移走）

| 组件/面板 | 回答的问题 | 重构后位置 | 处理 |
|-----------|-----------|-----------|------|
| **分支图** | 这个剧情选项会通向哪（当前→分支，单层） | 剧情分支 Tab | 重构主体：两列 + 节点卡 |
| **BuildResponsesPanel** | 响应选项列表（物品/目标/概率） | ~~分支图下方独立节~~ | **合并进分支图并移除独立区块**：分支卡是它的严格超集（+图片+标题+前置条件）；同数据同页双渲染正是反馈 1 的「重复定位」。其格式提示行移入分支图节首 |
| 左列反向引用橙卡 | 谁通向这里 | ~~分支图内~~ | 移除（反向信息统一到底部面板） |
| 👈 Referenced By 反向链面板 | 谁引用我（Encounter 递归） | ~~剧情分支 Tab 内~~ | 移除（底部面板已覆盖同层引用，全类型+分页；递归视图无独立增量） |
| 页面底部被引用面板（`BuildReverseRefsPanel`） | 谁引用我（全类型） | 页面底部 | **唯一反向入口，保留**；「根剧情」（无反向引用）由该面板为空隐含，不再在分支图内显示 |
| Hero 上下文徽章（🎒战利品 / 🐾生物 / ⚡ / 📋） | 这个剧情有什么 | Hero | 保留不动；**分支卡不渲染目标 Encounter 的 🎒🐾⚡**（跨实体总览价值在 Hero/剧情链，分支卡只放分支相关信息） |
| Refs 面板（PreConditions / Conditions / Loot / Treasure…） | 各引用字段明细 | 页面中部 | 保留不动（当前剧情的自有前置条件明细在此；分支卡的 📋×n 是「分支目标的可达条件」——**不同实体的不同字段，不构成同页重复**） |
| 剧情链 Tab（`BuildEncounterChainTree`） | 整条剧情链（多跳总览） | Tab 内 | 保留现状：跨实体总览中 🎒🐾⚡📋 徽章是正当比较信息，不属本次去重范围；节点样式后续可换用 `BuildEncounterNodeCard`（可选增强） |

**一句话边界**：分支图 = 正向一层（图）；Hero/Refs = 实体自身的「有什么」（卡+明细）；
被引用面板 = 反向（列表）。三者互不重叠。

## 七、Mermaid 决策

**决策：保留 Mermaid Tab，但改为与图形 Tab 同数据源、同信息层（结构性对齐）。**

- **同一数据源**：`BuildMermaidText(List<BranchData>, BranchData current)` 为纯函数，
  只消费 §4.5 的 `BranchData`——与分支卡渲染同源，两套输出不可能再各自漂移（反馈 2 根因消除）。
- **同一信息层**（与图形一致，只含当前→分支）：
  - 节点：`A["📍 {当前名称} (#{id})"]`、`B{i}["{分支名称} (#{id})"]`——**名称+ID**；
  - 边：`A -->|"{物品名 ×n | 权重(有效概率)[📋×n][⚠m/t]}"| B{i}`——**物品+权重+有效概率**，
    物品/📋/⚠ 均为条件附加（无物品不写物品段、无前置条件不写 📋、未过滤不写 ⚠）；
  - **移除反向 R 节点**（与图形移除左列一致）与节点 ctx 标签（🎒🐾📋pre:n，与图形移除 Hero 徽章一致）。
- 保留理由（决策记录）：Mermaid 是图形 Tab 不具备的**可移植文本形态**（mermaid.live 渲染、
  设计文档/PR 引用），移除属删功能且无必要；对齐成本 = 共用数据模型，维护成本趋近 0。
- 顺带修正：分支节点 ID 用 `B{index}` 而非现状 `(char)('A'+i)`（超过 26 个分支会溢出）。

## 八、通用组件复用（VisHelperService / RefNode）

| 组件 | 用途 |
|------|------|
| `SectionHeader(title, icon, accent)` / `SectionLabel` | 分支图区块标题 |
| `Card(content)` | 分支图两列图形容器 |
| `MiniBadge(text, bg, fg)` | 概率胶囊 / 类型 chip / 📋×n chip |
| `LoadImage(string?)` | 缩略图加载（`StripNs` 处理 `0:` 前缀，`.png` 兜底候选）——Hero 同款 |
| `OpenZoomableImage(bitmap, title)` | 缩略图点击放大 |
| `RefNode.BadgeForEntity / WireNavigation` | 物品徽章导航+tooltip、分支卡跳转+peek |
| `BuildRefTooltip(entity)` | 分支卡 hover 预览（Encounter 行：Type/Price/LootChance） |
| `BuildReverseRefsPanel(entityId)` | 页面底部唯一反向入口（不动） |
| `ParseResponseEntries`（保留） | `[itemId]x[mult]=[encounterId]x[weight]x0x0x0` 解析 + 权重归一（293-385 行原样保留，输出供 `PrepareBranches` 消费） |

**实现建议**（本文档不改代码，仅记录）：
- 新增 `PrepareBranches` / `BuildMermaidText` 两个纯函数（§4.5 / §七），便于直接单测；
- 类型 chip 映射（§4.2）抽成共享 helper，Hero / 分支卡 / 链树三处统一，消除现状 3 份重复判断；
- `BuildEncounterNodeCard` 若后续剧情链 Tab 也要用，再上移 VisHelperService（本次放 visualizer 内即可）。

## 九、设计决策记录

| 决策 | 依据（用户反馈 / 现状 / 来源） |
|------|--------------------------|
| 分支图 = 当前→响应分支单层；移除左列反向引用与 Tab 内反向链面板 | 用户反馈 1：「多个组件定位重复」——反向信息三处渲染；用户心理模型是「这个剧情选项会通向哪」 |
| 反向信息统一收到底部 `BuildReverseRefsPanel` | 底部面板已存在且全类型+分页，是「谁引用我」的既有权威入口；分支图不再承担反向职责 |
| 节点 = 单组件卡（strImg 52px + 标题 + 概率 + ID/类型 chip），**描述/条件/物品进 tooltip 信息卡** | 用户反馈 3「每个节点应该做成单组件，图片+title…组件内布局紧凑展示」+ 2026-08-08 追加「复杂信息通过 tooltip 信息卡做；节点关注图片（记忆）、标题（目标）、概率（可能性）」 |
| 分支卡只显示分支相关信息（概率/可达性），不渲染目标 🎒🐾⚡ | 用户反馈 1：Hero 上下文徽章是「这个剧情有什么」；职责分清后同页不再重复 |
| 前置条件从「常显徽章行」改为「tooltip 信息卡内呈现（含满足情况）」 | 用户反馈 3「条件使用目标等散开来…很散乱」+ 2026-08-08「tooltip 放描述、条件&条件情况（简单信息补充）」；过滤交互本身保留（用户未抱怨） |
| Mermaid 保留并对齐（同数据源纯函数生成） | 用户反馈 2：「信息量变化很大」根因是双套渲染；对齐后结构性消除漂移；Mermaid 有可移植文本价值（mermaid.live/文档/PR），移除属删功能且无必要 |
| ResponsesPanel 合并进分支图（移除独立区块） | 用户反馈 1：同一响应数据在页面上下两处渲染；分支卡是面板的严格超集；格式提示行保留移入分支图节首 |
| 类型 chip 补齐 0-3（剧情/搜刮/战斗/破解） | `field_descriptions.json` `encounters.type` 实测值域 0/1/2/3；现状只区分 Normal/Scavenge 漏掉 2/3；与 D05 §4.5 四标签口径一致 |
| 概率胶囊格式 `{w}({p:P2})` 与过滤重算保留 | 现有功能，用户未抱怨；只改呈现载体（胶囊位置），不改计算 |
| 当前节点卡不接导航、无 ctx 徽章 | 当前实体即本页，导航无意义；Hero/Refs 已覆盖「这个剧情有什么」 |
| 剧情链 Tab 保留现状 | 跨实体多跳总览是另一问题，其徽章是总览比较信息，不属同页重复；节点卡化列为可选增强 |

## 十、验收要点（可测试断言）

测试落点：`Tests/NeoEditor.Plugins.EntityEditor.Tests/EncounterVisualizerTests.cs`（新建），
沿用 `CreatureVisualizerTests` 手法（`BuildDetail` + `FindAll<T>` 树遍历 + Stub resolver/router/loc）；
`PrepareBranches` / `BuildMermaidText` 为纯函数，直接断言数值与文本。

1. **分支图单层**：渲染分支图，图形区不存在反向引用橙卡（背景 `#FFF3E0` 且边框 `#E65100` 的
   Border 数量 = 0）；剧情分支 Tab 内容中不存在「👈 Referenced By」文本。
2. **节点单组件**：每条响应对应一张分支卡——卡内含 1 个 `Image`（或兜底 `SymbolIcon`）、
   标题 TextBlock（`Subject`）、`ID: {id}` 文本、类型标签、概率胶囊；
   **卡片内不存在描述长文本、条件徽章、物品徽章等常显元素**（v1.1：三要素 + ID/类型 chip 之外无内容）。
3. **图片规格**：`enc.Image` 有值时分支卡出现 48-56px 方形 `Image`（`Stretch.Uniform`）；
   无值时出现 `SymbolIcon`（`Symbol.BookOpen`）且不抛异常；点击缩略图触发
   `OpenZoomableImage`（以 stub/可观测弹窗断言）。
4. **反向不在分支图**：Mermaid 文本不含反向节点（无 `R{idx}["←` 片段）；页面底部仍渲染
   被引用面板（`FindAll` 含「Referenced By」节）。
5. **Hero 徽章不重复**：分支卡内不含 🎒 / 🐾 / ⚡ 文本（目标 Encounter 的 TreasureId/CreatureId
   不渲染进分支卡）；Hero 区块徽章保留。
6. **Mermaid 对齐**：`BuildMermaidText` 输出含当前与分支节点名称与 `(#id)`；每条边标签含
   权重与有效概率（`{w:F1}({p:P2})`）；有物品的边含物品名与 `×n`；不出现反向 R 节点与 ctx 标签。
7. **过滤交互**：勾选某前置条件后，不满足的分支 `EffectiveProb == 0`、卡片 `Opacity == 0.5`、
   概率胶囊文本含 `0%`；满足分支的概率 = `Weight / 新有效总和`（纯函数断言具体数值）；
   Mermaid 文本同步更新（边标签出现 `⚠` 或 `0%`）。
8. **tooltip 信息卡（v1.1）**：分支卡挂有 `ToolTip`；其内容含描述文本（strDesc 截断）、
   前置条件条目（含名称与满足/不满足状态标记）、有物品时含物品名与 `×n`；
   过滤变化后 tooltip 内容同步更新（`ToolTip.GetTip` 断言）。
9. **ResponsesPanel 合并**：`BuildDetail` 顶层不再出现独立「Vis.Responses」区块
   （`FindAll` 中节标题数量 = 0）；分支图节内存在格式提示行文本
   （`[物品ID]x[数量]=[剧情ID]x[权重]`）。
10. **类型映射**：`nType` 0/1/2/3 分别渲染为 剧情/搜刮/战斗/破解 四色 chip；未知值灰色兜底不崩溃。
11. **导航**：Ctrl+Click 分支卡 → `StubNavigationRouter` 记录跳转到目标 Encounter；
    Ctrl+RMB → peek。
12. **当前卡**：含「📍 当前剧情」标识；当前卡不触发任何导航记录。
13. **回归**：TabControl 仍含 3 个 Tab（剧情分支 / 剧情链 / Mermaid源码）；`Responses` 为空时
    显示「无分支」占位；`PrepareBranches` 对 `=1x1x0x0x0`、`90.3x2=16x2x0x0x0,=16x1x0x0x0`
    等实测格式解析出正确的物品/权重/概率（对照 `field_descriptions.json` `encounters.responses`
    实测值域）。
