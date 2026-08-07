# D05 — Creature（生物）可视化设计（字段 / 语义 / 设计原因与目的）

> 设计文档 · 2026-08-08 · v1.0
> 上承：D04 ItemType 可视化设计（本类型沿用其模板与决策体系）+ 用户决策（可视化 = 单实体 · 只读 · 语义翻译；R40 心理模型重构）
> 下启：其余实体类型 visualizer 的设计模板（简单关联类套通用组件，复杂类型仿本文档逐类型设计）
> 依从：R04 View 只组装 · R13 VisHelper 内部组件 · N03 视图无逻辑 · 21-entity-detail-ui-design-guide.md
> 关联实现：`NeoEditor.Plugins.EntityEditor/Visualizers/CreatureEntityVisualizer.cs`（293 行，本文档即为它的重构设计目标）
> 数据来源：`field_descriptions.json`（`creatures.*` 13 条实测值域，第一手来源）+ `Creature.cs`（13 列，已与 DB `creatures` 表逐列核对一致）+ Doc 38 §7（同 13 列复述）
> 参考实现：`ItemTypeEntityVisualizer.cs`（战斗区块 / 攻击模式行+展开）、`TreasureTableEntityVisualizer.cs`（战利品树 + 概率归一）、`ConditionEntityVisualizer.cs`（条件链与效果翻译）、`FactionEntityVisualizer.cs`（声望条 / 按 propName 过滤反查成员）、`CreatureSourceEntityVisualizer.cs`（刷新点权重归一）

---

## 一、定位与设计原则

**可视化定位**：单实体 · 只读 · 语义翻译。它回答的是
「**这个生物在游戏里是什么、怎么打、属性如何、掉什么、在哪遇到**」，而不是
「这个 XML 文件里有哪些列」。修改逻辑一律在 Value Editor，可视化不做任何编辑。

**三条设计原则**：

1. **按用户心理模型组织，不按程序实现模型**。字段按「面对一个生物的认知顺序」分组：
   认人（身份）→ 评估威胁（战斗/属性）→ 计算收益（战利品）→ 计划遭遇（在哪遇到）→ 评估改动影响（被引用）。
   而不是按 XML 声明顺序或引用列/非引用列平铺。
2. **把数据翻译成问题答案**。攻击模式列表是引用 ID，用户关心的是「打多少、切割还是钝击」→ 推演成
   Σ 伤害条 + 士气有效伤害；战利品池权重是 `0.25`，用户关心的是「真的掉吗」→ 算成 `11.8%`；
   基础状态 `38=1,52=0.5` 用户关心的是「它上场带着什么、多大概率」→ 状态概率徽章。
   只展示原始引用而不回答问题是失职。
3. **只读但可交互**：跳转（Ctrl+Click / 徽章点击 / 反向引用行）、展开（攻击模式详情、战利品嵌套树、
   遭遇事件行）、试听（攻击音效）、预览（地图图片放大、hover 引用 tooltip）都是「查看」动作，不是「修改」。

**与 D04 的定位差异**：ItemType 是「物品」（死物，无阵营无遭遇），Creature 是「敌人/友军」——
多出两个独有问题：「它有多强」（属性 + 出场状态）和「在哪遇到」（遭遇链），这两者是本文档的 Creature 特有设计重心。

## 二、总体布局（R40 两段式信息架构）

```
┌─ ScrollViewer ─────────────────────────────────────────────────┐
│ StackPanel (Spacing=14, Margin=16)                              │
│  ├─ Raw Data Expander（全字段审计，默认折叠，兜底）              │
│  ├─ Hero Header Card（地图图片区 + 身份 + ID/阵营/行动点行）     │
│  ├─ 情境 1 两列：⚔ 战斗           │ 🧬 属性与出场状态            │
│  ├─ 情境 2 两列：🎁 战利品         │ 📍 遭遇（事件链 + 刷新点）   │
│  └─ 被引用面板（横贯底部）                                       │
└─────────────────────────────────────────────────────────────────┘
```

- 每个情境 = `SectionHeader(图标+标题+色条)` + `Card(内容)`；某侧无内容则该区块整行合并。
- 区块语义色：战斗红 `#C62828` / 属性紫 `#6A1B9A` / 战利品绿 `#2E7D32` / 遭遇青 `#00695C` / 被引用靛 `#283593`。
- 两对情境是「并列关系」而非「层级关系」，两列布局避免无限单列堆叠（R40 用户反馈，与 D04 同一决策）。
- 布局顺序即认知顺序：先看是谁（Hero）→ 怎么打多强（战斗|属性）→ 掉什么（战利品）→ 在哪遇（遭遇）→ 改动影响（被引用）。

## 三、字段总览（13 字段 → 5 个呈现位置）

| # | 列名 | 模型属性 | 语义 | 呈现位置 |
|---|------|----------|------|----------|
| 1 | id | Id | 序列编号（1-28 连续）**⚠️ 本类型无 G.S 复合键，游戏内引用（刷新点/剧情）直接用此编号** | Hero 徽章 + Raw Data |
| 2 | strName | Name | 生物名称（汉化）：狗人/掠夺者/圣王伊莱亚斯等 | Hero 标题 |
| 3 | strNamePublic | NamePublic | 未接触前显示的名称（`陌生人`…；⚠️ 1 行 `陌生人r` 疑似笔误） | Hero 副文本 |
| 4 | strNotes | Notes | 注解（剧情身份）：`JD`、`Recruiter`、`玩家的基础统计` 等 | Hero 副文本 |
| 5 | strImg | Image | 地图上显示的图片文件名（CreHuman.png…共 15 种） | Hero 图片区（点击放大） |
| 6 | vEncounterIDs | EncounterIds | 遇到该生物触发的事件（→ **Encounter**，⚠️ 20 文档误标 Condition） | 遭遇区块「出场事件链」 |
| 7 | nMovesPerTurn | MovesPerTurn | 每回合行动点数（3、4、5、8） | Hero 数字行 + 属性格 |
| 8 | nTreasureID | TreasureId | 初始携带物池（→ TreasureTable；3=空） | 战利品区块「随身携带」 |
| 9 | nFaction | Faction | 所属阵营（→ Faction；**0=玩家/中立**，1-14 实测） | Hero 徽章 + 战斗区块 |
| 10 | vAttackModes | AttackModes | 攻击模式（→ AttackMode；**1=拳头**；实测 1/7/17/50/59/61） | 战斗区块（行+展开） |
| 11 | vBaseConditions | BaseConditions | 基础状态，格式 **`状态=概率`**（→ Condition） | 属性区块「出场状态」 |
| 12 | nCorpseID | CorpseId | 尸体掉落池（→ TreasureTable；实测 396/402/409/424/490/666/757） | 战利品区块「尸体掉落」 |
| 13 | vActivities | Activities | 待机活动描述（逗号分隔，**仅注释用途**） | 属性区块「日常行为」 |

> 13 列全部进入呈现位置，**无「仅 Raw Data」字段**——这是与 D04 的有意差异：
> ItemType 有 `id`（序列号无引用意义）和 `vImageUsage`（语义被覆盖）两个低价值列可省略；
> Creature 的 `id` 是刷新点/剧情的引用键（必须常显防误认），其余各列均携带独立游戏语义。
> 相对最低价值的 `vActivities` 是纯注释列（21 种实测值，仅 flavor 文本），采用最小呈现（折叠在属性区块底部，
> 不做主视觉），属于「有意的低价值弱化」而非省略——它回答「它平时在干嘛」，成本只有一行徽章。

## 四、逐字段设计（字段 / 游戏语义 / 设计原因 / 设计目的）

### 4.1 身份区块（Hero，「这是什么」）

| 字段 | 游戏语义 | 设计原因 | 设计目的 |
|------|----------|----------|----------|
| `id` | **游戏内生物 ID = 序列编号（1-28）**，`creaturesources.nCreatureID` / `encounters.nCreatureID` 都直接引用它，不是「数据库自增」这么简单 | 用户在其他表看到 `nCreatureID: 17` 时必须能反查；与 ItemType 不同，这里没有 `G.S` 复合键可借，id 就是唯一键，**不能只丢 Raw Data** | Hero 蓝色 `ID: n` 徽章（tooltip 注明「被刷新点/剧情引用」），一眼定位 |
| `strName` | 生物名称（汉化），18 种实测值 | 身份第一要素 | 18px 加粗标题 |
| `strNamePublic` | 未辨识（未接触）前显示的名称 | 游戏有「陌生 / 相识」两层认知：野狗先显示 `陌生人`，接触后才知真名 | 标题下斜体副文本；与 Name 相同则隐藏（去噪）；保留 `陌生人r` 这类原版笔误不动（审计职责在 Raw Data） |
| `strNotes` | 注解，多为剧情身份（`JD`、`Recruiter`、`玩家的基础统计`） | 作者留的剧情线索是「这个生物是谁」的一部分 | 小字灰副文本；为空隐藏 |
| `strImg` | 地图上显示的图片文件（15 种实测值） | 图片是最直观的「这是什么」——玩家一眼认出狗人 | 132×132 图片区：点击放大（`OpenZoomableImage`）；无图时 `Person` 图标兜底（不崩溃） |
| `nMovesPerTurn` | 每回合行动点数（实测 3/4/5/8） | 行动节奏的核心数字，也是唯一已建模的数值属性 | Hero 橙色 chip `4 moves/turn`（与属性格重复出现是「关键数字行」惯例，D04 同款：重量/价值也进 Hero） |
| `nFaction` | 阵营引用：**0=玩家/中立**，1-14 为 `factions` 表 id | 阵营决定「见面打不打」；名字在另一张表，必须跨表解析 | Hero 橙 chip 显示**解析后的阵营名**（`LookupRef<Faction>`）；`0` 不显示（中立无信息量）；chip 可 Ctrl+Click 跳转 Faction |

设计原因：Hero 回答「是什么、属于谁、多能跑」三个问题，全部字段一屏读完，不需要滚动。

### 4.2 战斗区块（⚔「怎么打」）

| 字段 | 游戏语义 | 设计原因 | 设计目的 |
|------|----------|----------|----------|
| `vAttackModes` | 攻击模式引用列表（逗号分隔裸 ID，**无槽位前缀**——生物不像物品分左右手；`1`=拳头，实测 1/7/17/50/59/61） | 生物的战斗 = 它的攻击模式集合；用户第一眼想知道「它打多狠、切割还是钝击、近战远程」 | 分层呈现（见下） |

**分层呈现**（与 D04 §4.2 同一设计语言，差异仅两处：无槽位名、空手默认值说明）：

1. **Σ 总伤害条**（`StackedDamageBar`）：所有攻击模式 DamageCut+DamageBlunt 合计，
   Cut 红 `#E57373` / Blunt 蓝 `#64B5F6` 比例条。回答「这生物是切割型还是钝击型」——不用展开任何一行。
2. **Σ 有效伤害**（`ValueRow`，无条）：`Σ (cut+blunt)×(1+morale)`，如 `5.6 (×1.25)`。
   R41 决策沿用：**没有比较对象的填充条无意义**，指标值即可。士气是武器自带补正（`attackmodes.morale`，
   实测 0.05-0.6），伤害公式 `(1+角色士气+武器士气)×(1+加成)×武器伤害`（Doc 38）。
3. **逐攻击模式行**（复用 `BuildAttackModeRow` 模式，建议从 ItemType 抽取为 VisHelper 组件）：
   `名称 | 单条伤害比例条 | 射程/穿透/士气/音效 | ▶展开`。
   - 名称带导航 + tooltip（`_refNode.WireNavigation` + `BuildRefTooltip`），Ctrl+Click 跳转 AttackMode。
   - 士气显示在行 meta：`士气 +30%`。
   - **▶ 音效按钮**（R42）：攻击音效 cue 直接从行内试听（`PlaySoundButton`）。
   - 未解析引用渲染灰色原始段（不崩溃、不静默丢失）。
   - 全部攻击模式都是 `1`（拳头）时：显示一行灰色注释 `仅有空手攻击`（默认值去噪）。
4. **展开详情**（`BuildAttackModeExpanded` 模式）：顶行 36px 武器图标 + 近战/远程 + 士气 %
   （`25% (base)` 标注）+ 有效伤害；公式注（9px 灰字）；数值格（射程/穿透/弹药转移）；
   弹药 ChargeProfile 徽章（带消耗率，`degrade` 加 ⚠）；攻击者条件语义色徽章
   （Fatal 红/Instant 橙/Stackable 绿/时长蓝）；挥击短语 → 攻击短语；底部提示「Ctrl+Click 跳转」。

**阵营关系行（战斗区块的上下文）**：解析 `nFaction` 对应的 `Faction.dictFactions`（格式 `0=-100,1=-100,…`）
中「对玩家（0）」的声望值，用 `CenteredStatBar` 显示一行 `对玩家 −100（敌对）`——回答「**见面打不打**」。
声望 ≥50 绿（友好）/ 0-50 灰（中立）/ <0 红（敌对），复用 Faction 视觉器的语义色。
此为增强项：仅当 dictFactions 含 `0=` 条目时显示，解析失败则静默隐藏。

设计原因：生物没有装备、没有弹药槽、没有耐久——战斗区块是它最大的信息块，直接复用 D04 验证过的
「Σ 比例条 → Σ 有效伤害 → 逐行展开」三层，避免为 Creature 重造一套（同构语义，同构呈现）。

### 4.3 属性区块（🧬「属性如何」）

**属性格（`CreatureStatGrid`，Creature 特有）**：

当前模型已建模的数值只有 `nMovesPerTurn`，属性格第一行呈现它。其余 8 个生物属性
（`nHP` 生命 / `nMoveCost` 移动消耗 / `nVisibility` 可见度 / `nStrength` 力量 /
`nToughness` 韧性 / `nAgility` 敏捷 / `nPerception` 感知 / `nMorale` 士气）**在游戏中不存在**：
- `field_descriptions.json`、DB `creatures` 表、游戏 XML `creatures.xml`（CREATE TABLE 13 列）
  均无这些字段——已逐列核对，且全 data 目录无任何 XML 含 `nHP`/`nStrength` 等字样；
- `FieldGroupMetadata.cs` 的 Creature「属性」分组是**早期虚构/漂移的元数据**（还含不存在的
  `strId/nBodyType/nSpecies`、拼写漂移的 `vCorpseID`），不是"编辑器尚未导入"。

设计口径（不臆造）：
- 属性格只呈现**真实存在的数字**（`nMovesPerTurn` 行动点、攻击模式数等派生计数），
  **不预留虚构槽位**——留空槽位会让用户以为数据缺失；
- 实现阶段**同步修正 `FieldGroupMetadata`**：删除虚构字段，按真实 13 列重写 Creature 分组
  （同时核查全部实体类型——该元数据与模型大面积漂移，不止 Creature）；
- 生物强度由「攻击模式（打多狠）+ 出场状态（带什么病）+ 战利品（值多少）」共同表达，
  这些都在各自区块覆盖，缺失数值属性不产生信息空洞。

**出场状态（`vBaseConditions` → Condition，`状态=概率` 格式，Creature 特有设计）**：

| 字段 | 游戏语义 | 设计原因 | 设计目的 |
|------|----------|----------|----------|
| `vBaseConditions` | 生物出生/出场即带的状态，**每个状态带一个概率**（实测 `38=1,52=0.5,71=0.75,57=1,…`；20 种取值组合） | 概率是 modder 调强度时的关键数字——`52=0.5` 意味着「半个狗人带肠胃炎」；ItemType 的条件列没有概率，这是 Creature 独有的语义维度 | 「出场状态」区：**状态概率徽章**（见下） |

状态概率徽章设计：
- 解析 `{id}={value}` 段（`ReferencePattern.FromName("{id}={value}")`），解析成功 → 徽章显示
  `状态名 + 概率后缀`：概率 `1` 全量携带 → 无后缀（去噪）；`<1` → `· 50%` 后缀。
- 徽章复用 D04 条件语义色（Fatal 红 / Instant 橙 / Stackable 绿 / 时长蓝，`ConditionBg/Fg`），
  hover tooltip 显示条件效果翻译（`BuildRefTooltip` 已支持 Condition 的 `aFieldNames/aModifiers` 翻译，R42）。
- 未解析段渲染灰色原始文本（不崩溃）。

**日常行为（`vActivities`）**：待机活动描述（仅注释用途）→ 「日常行为」轻量徽章行
（WrapPanel，最多 30 个 + `+N more`，沿用现有实现的截断策略）。设计原因：它是纯 flavor 文本，
但「licking itself, pacing, digging」这类描述是 modder 写剧本时的灵感来源——低价值弱化而非省略。

### 4.4 战利品区块（🎁「掉什么」）

| 字段 | 游戏语义 | 设计原因 | 设计目的 |
|------|----------|----------|----------|
| `nTreasureID` | 初始携带物池（→ TreasureTable；实测 3=空、9/70/71/352/473/545/559/597/612/642/678/708） | 「搜它身体有什么」问题的第一半；3=空池无信息量 | 「随身携带」区：池名徽章（可跳转）+ 内联战利品树 |
| `nCorpseID` | 尸体掉落池（→ TreasureTable；实测 396/402/409/424/490/666/757） | 「宰了它掉什么」是收益问题的另一半；与携带池是**两种不同收益**（活着搜 vs 杀掉摸尸） | 「尸体掉落」区：同上，与携带池**并置对比** |

**内联战利品树**（直接复用 `TreasureTableEntityVisualizer.BuildItemRow` / `BuildNestedItems`
——两者已是 `internal static`，零改造可用）：解析 `物品x权重x数量` 段，逐项显示
物品名 + **真实概率**（`权重/Σ权重`，红→绿渐变）+ 数量区间；嵌套 TreasureTable 递归展开（深度 ≤3，可折叠）。
概率归一是 modder 调掉落时唯一关心的数字——`0.25` 权重没有意义，`11.8%` 才有。

设计原因：把「战利品 = 两个引用 ID」翻译成「带什么/掉什么」两棵可对比的树；
两池并置让 modder 一眼看出「这生物活着值钱还是死了值钱」（例：掠夺者携带池肥、尸体池薄）。

### 4.5 遭遇区块（📍「在哪遇到」——Creature 特有）

| 字段/数据源 | 游戏语义 | 设计原因 | 设计目的 |
|------|----------|----------|----------|
| `vEncounterIDs`（正向） | 遇到该生物触发的事件 ID 列表（→ **Encounter**；实测 `"1728,1729,1905"`、1189 等） | 「见到它会触发什么」——遭遇是生物-剧情的桥；现有实现把它标成 `OnEnterConditions` 是 R30 前的错误标签遗留 | 「出场事件链」：Encounter 徽章行（事件名 + 类型标签 剧情/搜刮/战斗/破解 + Ctrl+Click 跳转 + hover 预览 Type/Price/LootChance） |
| `Encounter.CreatureId`（反向） | 哪些剧情会刷出本生物（实测 0/2/3/17/23/24/25/26/29；`0`=无） | 「哪些剧情里我会出现」是遭遇问题的另一侧 | 「会出现在哪些剧情」：Encounter 徽章 + `creatureHex`（`半径,方向`，如 `40,0`=半径 40 任意方向） |
| `CreatureSource.CreatureId`（反向） | 哪些刷新点刷本生物：坐标（`-1`=跟随玩家）、数量 Min–Max、权重（0.2-1，同点竞争概率） | 「在哪刷、刷几只、多大概率」——遭遇问题的第三侧，现有实现只靠通用反向引用面板平铺，不回答问题 | 「刷新点」：每行 `点名 (x,y) · 2–4 只 · 权重 0.5（占同点 45%）`，权重按同 (x,y) 的 Σ 归一（复用 `CreatureSourceEntityVisualizer.GetWeightInfo` 逻辑），可跳转 CreatureSource |

反向数据获取方式（实现注）：`IndexService.ReverseLookup(entityId)` 返回 `(srcEid, propName, …)` 元组，
按 `propName == nameof(Encounter.CreatureId)` / `nameof(CreatureSource.CreatureId)` 过滤
（Faction 视觉器 `BuildMembersPanel` 同款手法），再经 `ReferenceLookups` 解析实体。

设计原因：「在哪遇到」是生物特有的问题（ItemType 没有对应概念），现有实现完全没回答它——
`vEncounterIDs` 只渲染成徽章列表、反向引用面板只是通用平铺。遭遇区块把三个数据源
（正向事件链 + 反向剧情刷出 + 刷新点）合成一个「遭遇全景」，这才是 modder 平衡生物强度时的完整上下文。

### 4.6 被引用面板（反向关联）

`BuildReverseRefsPanel(entityId)`：谁引用了我（刷新点/剧情/其他）。回答「改了它会影响什么」——
**只读编辑器的风险提示**。与遭遇区块的关系：被引用面板是全量审计（含非 Creature 来源），
遭遇区块是语义翻译后的定向呈现，两者并存不冲突。

## 五、通用组件复用（VisHelperService / RefNode）

| 组件 | 用途 |
|------|------|
| `SectionHeader(title, icon, accent)` | 区块标题（图标+色条） |
| `Card(content)` | 区块卡片容器 |
| `ValueRow(label, value, color)` | 指标值行（90px label）——Σ 有效伤害 |
| `StatBar(label, valueText, fillRatio, color)` | 带填充的比例条 |
| `CenteredStatBar(label, valueText, value, maxAbs)` | 双向条——对玩家声望（敌对/友好） |
| `StackedDamageBar(label, cut, blunt, rightText)` | Cut/Blunt 双色堆叠比例条——Σ 总伤害、单攻击模式 |
| `CreatureStatGrid(cells)` | 2 列数值格——属性格（攻击详情数值格复用） |
| `MiniBadge(text, bg, fg, onClick)` | 小徽章——状态概率/活动/事件标签 |
| `PlaySoundButton(cueName)` | ▶ 音效试听（无索引自动隐藏）——攻击行内 |
| `BuildReverseRefsPanel(entityId)` | 反向引用 |
| `BuildRawData(entity)` | 全字段审计兜底 |
| `LoadImage / OpenZoomableImage` | 地图图片加载/放大 |
| `RefNode.WireNavigation / Badge / BadgeForEntity` | 引用解析+跳转+tooltip（Faction/AttackMode/Condition/Encounter/TreasureTable/CreatureSource 徽章全部走它） |
| `TreasureTableEntityVisualizer.BuildItemRow / BuildNestedItems`（internal static） | 战利品树——零改造直接复用 |
| `CreatureSourceEntityVisualizer.GetWeightInfo` 逻辑 | 刷新点同点权重归一 |

**实现建议**（本文档不改代码，仅记录）：ItemType 的 `BuildAttackModeRow` / `BuildAttackModeExpanded`
目前是私有方法，Creature 需要同一套行+展开——应抽取为 VisHelperService 组件（与 `StackedDamageBar`
同层），两个 visualizer 共用，避免复制粘贴漂移。

## 六、设计决策记录（为什么是现在这样）

| 决策 | 依据（用户反馈 / 轮次 / 来源） |
|------|--------------------------|
| 两列情境布局（战斗\|属性 / 战利品\|遭遇） | R40「布局呆板，只有一列并且一直堆叠」（D04 同款决策） |
| 攻击模式「Σ 比例条 → Σ 有效伤害 → 逐行展开」三层，直接沿用 D04 战斗区块 | 生物战斗与物品战斗语义同构（都是 AttackMode 集合）；士气→有效伤害推演（D04 R41）；「没有比较对象的填充条无意义」 |
| 属性格（`CreatureStatGrid`）替代计数网格 | 现有实现缺陷：`Attacks: 3` / `Loot: Yes` / `Status: 8` 是计数不是语义，「3 个攻击」不回答「打多少」 |
| 状态概率徽章（`状态=概率` 概率显性化） | `vBaseConditions` 是 Creature 独有的概率语义（ItemType 条件列无概率）；概率是调强度的关键数字 |
| 战利品双池并置（携带 vs 尸体）+ 内联树概率归一 | 活着搜 vs 杀掉摸尸是两种收益；`权重/Σ权重` 才是 modder 关心的数字（TreasureTable 视觉器既有决策） |
| 遭遇链双向呈现（正向事件链 + 反向剧情刷出 + 刷新点） | 「在哪遇到」是 Creature 独有问题；现有实现只平铺徽章，且 `OnEnterConditions` 标签是 R30 前 Condition 误解的遗留错误 |
| 阵营跨表解析进 Hero + 战斗区「对玩家声望」 | `nFaction` 是引用列，名在 Faction 表；「见面打不打」由 Faction.dictFactions 的 `0=` 条目决定 |
| `id` 进 Hero 常显（区别于 D04 只进 Raw Data） | Creature 无 `G.S` 复合键，id 就是游戏内引用键（刷新点/剧情直接引用 0-28），隐藏会让反查失效 |
| 属性格只呈现真实数字，**不预留虚构槽位** | 8 个属性（nHP 等）经核实**在游戏数据中不存在**（XML/DB/JSON 均无，全目录核对）；`FieldGroupMetadata` 的「属性」分组是虚构/漂移元数据——依「以实测值为准」原则，不臆造、不占位 |
| `vActivities` 低价值弱化而非省略 | 纯注释列但回答「它平时在干嘛」（flavor 价值）；13 列全部有语义，本类型无 Raw Data 专属字段（与 D04 的有意差异） |
| 可视化零修改逻辑 | 「可视化不应该有修改逻辑，必须是只读的」 |
| 值域信息不进可视化 | 「值域信息没用，不同 mod 跨度大得很」（D04 同款决策；由 DataTable 承担） |

## 七、验收要点

1. **13 列全覆盖**：`id` / `strName` / `strNamePublic` / `strNotes` / `strImg` / `vEncounterIDs` /
   `nMovesPerTurn` / `nTreasureID` / `nFaction` / `vAttackModes` / `vBaseConditions` / `nCorpseID` /
   `vActivities` 全部进入呈现位置（对照 `Creature.cs` 的 `[Column]` 逐条核对，一个不漏）；
   本类型无「仅 Raw Data」字段，`vActivities` 是有意的低价值弱化（理由见 §三）。
2. 每个数字类字段回答一个问题（不是复述列名）：行动点→行动节奏、士气→有效伤害、战利品权重→真实概率、
   状态概率→出场携带概率、刷新权重→同点竞争概率。
3. 引用全部可跳转、可 hover 预览；未解析引用灰色兜底不崩溃（Faction/AttackMode/Condition/Encounter/
   TreasureTable/CreatureSource 六个引用维度全覆盖）。
4. 只读：无任何编辑控件；修改路径全部在 Value Editor。
5. 无数据区块整体隐藏（`null` body 跳过），不给用户看空壳；`nTreasureID`=3（空池）隐藏随身携带区。
6. 遭遇链双向可验证：点开任一 Encounter 反查该生物，刷新点行与 `CreatureSource` 页一致（权重归一逻辑同源）。
7. 现有实现的遗留错误已消除：`Vis.OnEnterConditions` 标签改名（vEncounterIDs 指向 Encounter）、
   计数网格不再出现、`FieldGroupMetadata` 虚构/漂移元数据在实现阶段同步修正（全类型核查）。
