# D04 — ItemType 可视化设计（字段 / 语义 / 设计原因与目的）

> 设计文档 · 2026-08-08 · v1.0
> 上承：用户决策（可视化 = 单实体 · 只读 · 语义翻译；R40 心理模型重构）
> 下启：其余实体类型 visualizer 的设计模板（简单关联类套通用组件，复杂类型仿本文档逐类型设计）
> 依从：R04 View 只组装 · R13 VisHelper 内部组件 · N03 视图无逻辑 · 21-entity-detail-ui-design-guide.md
> 关联实现：`NeoEditor.Plugins.EntityEditor/Visualizers/ItemTypeEntityVisualizer.cs`（1484 行）
> 数据来源：`field_descriptions.json`（全字段实测值域）+ `ItemType.cs`（30 字段）

---

## 一、定位与设计原则

**可视化定位**（用户 2026-08-07 确认）：单实体 · 只读 · 语义翻译。它回答的是
「**这个物品在游戏里是什么、怎么用、能用多久、装什么、从哪来**」，而不是
「这个 XML 文件里有哪些列」。修改逻辑一律在 Value Editor / DataTable，可视化不做任何编辑。

**三条设计原则**：

1. **按用户心理模型组织，不按程序实现模型**。字段按「使用物品的认知顺序」分组：
   是什么 → 怎么用（战斗/装备/效果）→ 能撑多久（耐久）→ 装什么/从哪来（容器/来源）。
   而不是按 XML 声明顺序或数据库分组平铺。
2. **把数据翻译成问题答案**。损耗率是 `0.02/h`，用户关心的是「能用多久」→ 推演成
   `≈100h`；士气补正是 `0.25`，用户关心的是「实际伤害多少」→ 算成 `5.6 (×1.25)`。
   只展示原始数值而不回答问题是失职。
3. **只读但可交互**：跳转（Ctrl+Click / 徽章点击）、展开（攻击模式详情）、试听（音效）、
   预览（穿戴效果）都是「查看」动作，不是「修改」。

## 二、总体布局（R40 两段式信息架构）

```
┌─ ScrollViewer ─────────────────────────────────────────────────┐
│ StackPanel (Spacing=14, Margin=16)                              │
│  ├─ Raw Data Expander（全字段审计，默认折叠，兜底）              │
│  ├─ Hero Header Card（图片区 + 身份 + 关键数字行）               │
│  ├─ 情境 1 两列：⚔ 战斗           │ 🧍 装备                     │
│  ├─ 情境 2 两列：✨ 使用效果       │ ⏳ 耐久与弹药（生命周期）    │
│  ├─ 情境 3 两列：📦 容器           │ 🔗 来源与产出               │
│  └─ 被引用面板（横贯底部）                                       │
└─────────────────────────────────────────────────────────────────┘
```

- 每个情境 = `SectionHeader(图标+标题+色条)` + `Card(内容)`；某侧无内容则该区块整行合并。
- 区块有语义色：战斗红 `#C62828` / 装备蓝 `#1565C0` / 效果橙 `#E65100` / 生命周期紫 `#6A1B9A` / 容器青 `#00695C` / 关联靛 `#283593`。
- 三对情境是「并列关系」而非「层级关系」，两列布局避免无限单列堆叠（R40 用户反馈）。

## 三、字段总览（30 字段 → 8 个呈现位置）

| # | 列名 | 模型属性 | 语义 | 呈现位置 |
|---|------|----------|------|----------|
| 1 | id | Id | 序列编号（⚠️ 非游戏内 ID） | Raw Data |
| 2 | nGroupID | GroupId | 物品组号 | Hero 徽章 `G.S` |
| 3 | nSubgroupID | SubgroupId | 组内序号 | Hero 徽章 `G.S` |
| 4 | strName | Name | 物品名称 | Hero 标题 |
| 5 | strDesc | Description | 游戏内描述 | Hero 副标题 |
| 6 | strDescAlt | DescriptionAlt | 辨识后真实描述 | Hero 辨识区块 |
| 7 | nCondID | CondId | 辨识所需条件 | 效果区块「辨识条件」 |
| 8 | vImageList | ImageList | 调用图片列表 | Hero 图片区（画廊） |
| 9 | vSpriteList | SpriteList | 大地图小人图片 | 装备穿戴预览（Sprite UI 页） |
| 10 | vImageUsage | ImageUsage | 6 位图片用途索引 | Raw Data（低价值） |
| 11 | fWeight | Weight | 重量 | Hero 数字行 |
| 12 | fMonetaryValue | MonetaryValue | 基础价值 | Hero 数字行 |
| 13 | fMonetaryValueAlt | MonetaryValueAlt | 辨识后价值 | Hero 数字行 |
| 14 | fDurability | Durability | 耐久比 | 生命周期 StatBar |
| 15 | fDegradePerHour | DegradePerHour | 每小时损耗 | 生命周期 + 寿命推演 |
| 16 | fEquipDegradePerHour | EquipDegradePerHour | 装备时每小时损耗 | 生命周期 + 寿命推演 |
| 17 | fDegradePerUse | DegradePerUse | 每次使用损耗 | 生命周期 + 寿命推演 |
| 18 | vDegradeTreasureIDs | DegradeTreasureIds | 破损产物池 | 生命周期「破损产物」 |
| 19 | aEquipConditions | EquipConditions | 装备时条件 | 效果区块 |
| 20 | aPossessConditions | PossessConditions | 携带时条件 | 效果区块 |
| 21 | aUseConditions | UseConditions | 使用时条件 | 效果区块 |
| 22 | aCapacities | Capacities | 容器容量 宽×高 | 容器区块 |
| 23 | vEquipSlots | EquipSlots | 装备槽位 | 装备区块 + 穿戴预览 |
| 24 | vUseSlots | UseSlots | 使用槽位 | 装备区块 |
| 25 | bSocketLocked | SocketLocked | 槽位锁定 | 装备区块警示 |
| 26 | vProperties | Properties | 物品属性 | 效果区块 |
| 27 | aContentIDs | ContentIds | 能装入的内容类别 | 容器区块 |
| 28 | nFormatID | FormatId | 自身类别 | 容器区块 |
| 29 | nTreasureID | TreasureId | 内含物池 | 来源与产出 |
| 30 | nComponentID | ComponentId | 拆解产物池 | 来源与产出 |
| 31 | bMirrored | Mirrored | 镜像 | Hero 数字行 |
| 32 | nSlotDepth | SlotDepth | 多件叠放顺序 | Hero 数字行 |
| 33 | nStackLimit | StackLimit | 最大堆叠 | Hero 数字行 |
| 34 | strChargeProfiles | ChargeProfiles | 充能档位 | 生命周期「弹药」 |
| 35 | aAttackModes | AttackModes | 攻击模式 | 战斗区块 |
| 36 | aSwitchIDs | SwitchIds | 开关状态物品 | 来源与产出 |
| 37 | aSounds | Sounds | 拾取/放下音效 | 装备区块（情境归位） |

> 37 列（含 3 组条件 ×3 + 数值对 ×2），其中 `vImageUsage`/`id` 仅出现在 Raw Data ——
> 它们要么是编辑器内部编号，要么语义已被其他字段覆盖（ImageUsage 的 6 位索引
> 对 modder 无平衡意义）。

## 四、逐字段设计（字段 / 描述 / 设计原因 / 设计目的）

### 4.1 Hero 区块（「这是什么」）

| 字段 | 游戏语义 | 设计原因 | 设计目的 |
|------|----------|----------|----------|
| `nGroupID.nSubgroupID` | **游戏内物品 ID = `G.S`**，所有引用（战利品池/配方/开关）都用它，`id` 列是数据库序列号 | 用户看到其他表里引用 `10.3` 时，必须能在本表反查；把错误的 `id` 当 ID 是新手最常见的坑 | 徽章常显 `G.S`，一眼定位；防误认 |
| `strName` | 物品名（汉化） | 身份第一要素 | 18px 加粗标题 |
| `strDesc` | 游戏内描述 | 物品"是什么"的正文 | 标题下副文本；与 Name 相同则隐藏（去噪） |
| `strDescAlt` | **辨识后**才显示的真实描述（水=有毒、.308=穿甲弹） | 游戏存在「未辨识/已辨识」两层真相；两层并置才完整 | 独立橙色区块 +「✦ 已辨识」标注，与 Description 视觉区分 |
| `fWeight` | 重量（50=不可拾取） | 携带决策的核心数字 | 数字行 chip `0.5 kg` |
| `fMonetaryValue` / `fMonetaryValueAlt` | 基础价 → 辨识后价 | 辨识会改变价值，两个价格一起显示才有意义 | `$5.00 → $25.00` 箭头形态，暗示辨识收益 |
| `nStackLimit` | 最大堆叠 | 背包管理核心 | chip `×10` |
| `bMirrored` | 镜像（鞋子类共用图） | 影响穿戴预览方向的开关 | chip「镜像」 |
| `nSlotDepth` | 多件同槽叠放顺序 | 装备叠穿顺序 | chip |
| `vImageList` | 图片列表（可多张） | 图片是最直观的"这是什么"；多张时是状态图（空/满） | 132×132 图片区：单张=点击放大（含像素尺寸角标）；多张=画廊（◀▶ + 圆点导航 + 尺寸角标） |

### 4.2 战斗区块（⚔「怎么打」）

| 字段 | 游戏语义 | 设计原因 | 设计目的 |
|------|----------|----------|----------|
| `aAttackModes` | 攻击模式列表（`槽位=攻击`，如 `20=11,21=11` 双手持枪） | 攻击是物品最复杂的行为数据，但用户第一眼只想知道「砍还是砸、打多少」 | 见下分层设计 |

**分层呈现**（把 6 个攻击字段拆成 3 层，由浅入深）：

1. **Σ 总伤害条**（`StackedDamageBar`）：所有攻击模式 DamageCut+DamageBlunt 合计，
   Cut 红 `#E57373` / Blunt 蓝 `#64B5F6` 比例条。回答「这武器是切割型还是钝击型」——
   不用展开任何一行。
2. **Σ 有效伤害**（`ValueRow`，无条）：`Σ (cut+blunt)×(1+morale)`，`5.6 (×1.25)`。
   R41 决策：不用比例条——**没有比较对象的填充条无意义**，指标值即可。
3. **逐攻击模式行**（`BuildAttackModeRow`）：`槽位名: 名称 | 单条伤害比例条 | 射程/穿透/士气/音效 | ▶展开`。
   - 名称带导航 + tooltip（`_refNode.WireNavigation` + `BuildRefTooltip`），Ctrl+Click 跳转 AttackMode。
   - 士气显示在行 meta：`士气 +25%`（伤害公式 `(1+角色士气+武器士气)×(1+加成)×武器伤害`，Doc 38）。
   - **▶ 音效按钮**（R42）：攻击音效 cue 直接从行内试听。
   - 未解析引用渲染灰色原始段（不崩溃、不静默丢失）。
4. **展开详情**（`BuildAttackModeExpanded`，点击行头内联展开，R41 紧凑化）：
   - 顶行：36px 武器图标 + 近战/远程 + 士气 %（`25% (base)` 标注）+ 有效伤害——**一行搞定，图标不独占一行**（R41 用户反馈）。
   - 公式注（9px 灰字，R37 伤害公式说明）。
   - 数值格：射程 / 穿透 / 弹药转移。
   - 弹药：`ChargeProfile` 徽章带消耗率（`每次 1.00 · 每小时 2.00`），`degrade` 加 ⚠。
   - 攻击者条件：语义色徽章（Fatal 红/Instant 橙/Stackable 绿/时长蓝）。
   - 挥击短语（斜体引用）→ 攻击短语（蓝徽章）→ 注解。
   - 底部提示「Ctrl+Click 跳转」。

### 4.3 装备区块（🧍「怎么穿」）

| 字段 | 游戏语义 | 设计原因 | 设计目的 |
|------|----------|----------|----------|
| `vEquipSlots` | 槽位（`20,21`=双手；`11=0=0`=上身+图索引；`-1`=不可装备） | 槽位决定"穿在哪"，直接决定穿戴预览 | 槽位徽章（`Torso`/`L-Hand`/`R-Hand` 可读名，100-115=伤口部位名映射） |
| `vUseSlots` | 使用槽（`211`=直接使用；`100-115`=对部位使用） | 「用在哪」和「穿在哪」是不同问题 | 使用槽徽章（`211`→`Self`） |
| `bSocketLocked` | 物品无法从槽位移除 | 玩家不可卸下的特殊物品 | 红色警示徽章 |
| `aSounds` | 拾取/放下音效 cue | **R40 情境归位**：声音属于"与物品交互"的上下文（拿起来/放下），不属于"关联" | 装备区块「音效」行：cue 徽章 + ▶ 试听（R42）；默认值 `cuePickup,cuePutdown` 隐藏（无信息量） |
| `vSpriteList` | 小人穿戴图 `槽位=图名` | 大地图小人视觉 | 穿戴预览 Sprite UI 页 |
| `bMirrored` | 镜像 | 预览时右持图要翻转 | 预览仅 Image UI 页翻转（sprite 不翻） |

**穿戴预览**（`BuildEquipSlotOverlay`，R40）：
- 双 Tab：**Image UI**（物品栏图标，`btn_inv_body.png` 底图）/ **Sprite UI**（小人穿戴，`CreHuman.png` 底图）。
- 裸手槽循环选择可用图（CheckBox 每次点击轮换下一个空闲图/精灵）。
- 滚轮缩放 + 拖拽平移；初始缩放自适应。
- 目的：让 modder **不用导出游戏就能看到装备效果**——这是"物品在游戏中的表现"问题的直接答案。

### 4.4 效果区块（✨「有什么效果」）

| 字段 | 游戏语义 | 设计原因 | 设计目的 |
|------|----------|----------|----------|
| `aPossessConditions` | 携带时条件（`槽位=状态`，负号=否定） | 三组条件语义完全不同（带着就有/用了触发/穿上触发），必须分开展示 | 「携带时」区 |
| `aUseConditions` | 使用时条件 | 同上 | 「使用时」区 |
| `aEquipConditions` | 装备时条件 | 同上 | 「装备时」区 |
| `nCondID` | **辨识所需条件**（87=擅长远程射击 → 枪械可辨识） | 辨识是 ItemType 特有机制，与行为条件并列但不同 | 「辨识条件」单独行 |
| `vProperties` | 物品属性（ItemProp：技能/工具/类别/标记） | 属性决定"这物品能干哪些活"（制作/修理门槛） | 绿色徽章（`#E8F5E9`/`#2E7D32`） |

**条件徽章语义色**（R36，`ConditionBg/Fg`）：Fatal 红 / 瞬时橙 / 可堆叠绿 / 时长蓝，
后缀标注 `· FATAL` / `· Instant` / `· Stackable` / `· 12h`——**不开条件详情就能判断严重性**。
条件 label 解析 `槽位=状态` 前缀 + 负号（`~`），tooltip 悬停可看效果翻译（R43 条件效果文本）。

### 4.5 生命周期区块（⏳「能用多久」）

| 字段 | 游戏语义 | 设计原因 | 设计目的 |
|------|----------|----------|----------|
| `fDurability` | 耐久比（0-1，实测全为 1） | 物品损耗的"总量" | StatBar `100%`（R41 柔色：>50% 绿 / >25% 橙 / 其余红；999=∞ 灰） |
| `fDegradePerHour` | 每小时损耗 | 裸损耗率对 modder 无直觉 | ValueRow + **寿命推演** |
| `fEquipDegradePerHour` | 装备时每小时损耗 | 同上 | ValueRow + **寿命推演** |
| `fDegradePerUse` | 每次使用损耗 | 同上 | ValueRow + **寿命推演** |
| `vDegradeTreasureIDs` | 破损产物池 | 「坏了掉什么」是损耗的另一半 | 「破损产物」区：TreasureTable 名（可跳转）+ 内联战利品树 |
| `strChargeProfiles` | 充能档位 | 弹药/电量是"能用多久"的子问题 | 「弹药」区：ChargeProfile 徽章（青 `#E0F7FA`/`#006064`） |

**寿命推演**（R42，用户确认"耐久推演可以搞"）：
`寿命 每小时 ≈100h · 装备时 ≈100h · 每次使用 ≈100×` —— 把损耗率翻译成**能用多久**，
这是 modder 调平衡时真正问的问题。仅当 `Durability < 999`（无限耐久不推演）。

### 4.6 容器区块（📦「装什么」）

| 字段 | 游戏语义 | 设计原因 | 设计目的 |
|------|----------|----------|----------|
| `aCapacities` | 容量 `宽x高` | 「能装多大」 | `2x3` 值行 |
| `aContentIDs` | 能装入的内容类别（ContainerType：`.308`/`电池`/`软件`…） | 「能装什么」——物品分类语义 | 靛蓝徽章（`#E8EAF6`/`#283593`），可跳转 |
| `nFormatID` | 自身类别 | 与 ContentIds 配对：A 的 Content 含 B 的 Format，A 就能装 B | 「类别」值行（容器匹配逻辑的一端） |

设计原因：容器三字段（容量/内容/自身类别）是**同一次交互**（放东西进去）的三要素，
拆到不同区块会强迫用户脑内拼装——R40 用户反馈「组件之间没有关联」。

### 4.7 来源与产出区块（🔗「从哪来、变什么」）

| 字段 | 游戏语义 | 设计原因 | 设计目的 |
|------|----------|----------|----------|
| `nTreasureID` | 内含物池（出生自带/搜刮产出） | 「这物品出现时肚子里有什么」 | TreasureTable 名（可跳转）+ 内联战利品树 |
| `nComponentID` | 拆解产物池 | 「这物品能拆成什么」 | 同上，与 TreasureId 并置便于对比 |
| `aSwitchIDs` | 开关状态物品（`Off=8.8`：点击切换到另一物品） | 「点击它会变成什么」——可切换物品的闭环 | 紫徽章（`#F3E5F5`/`#6A1B9A`）：`8.8 手电筒(关)` 名称+短描述 |

**内联战利品树**（`BuildTreasureLootTree`）：解析 `物品x概率x数量` 段，逐项显示
物品名 + **真实概率**（`权重/Σ权重`）+ 数量区间；嵌套 TreasureTable 可展开。概率是
modder 调掉落时唯一关心的数字。

### 4.8 被引用面板（反向关联）

`BuildReverseRefsPanel(it.EntityId)`：谁引用了我（配方用到我/战利品池包含我/其他物品
开关到我）。回答「改了它会影响什么」——**只读编辑器的风险提示**。

## 五、通用组件复用（VisHelperService）

| 组件 | 用途 |
|------|------|
| `SectionHeader(title, icon, accent)` | 区块标题（图标+色条） |
| `Card(content)` | 区块卡片容器 |
| `ValueRow(label, value, color)` | 指标值行（90px label） |
| `StatBar(label, valueText, fillRatio, color)` | 带填充的比例条（耐久） |
| `StackedDamageBar(label, cut, blunt, rightText)` | Cut/Blunt 双色堆叠比例条 |
| `CreatureStatGrid(cells)` | 2 列数值格（射程/穿透） |
| `MiniBadge(text, bg, fg, onClick)` | 小徽章 |
| `PlaySoundButton(cueName)` | ▶ 音效试听（无索引自动隐藏） |
| `BuildReverseRefsPanel(entityId)` | 反向引用 |
| `BuildRawData(entity)` | 全字段审计兜底 |
| `LoadImage / OpenZoomableImage` | 图片加载/放大 |
| `RefNode.WireNavigation / Badge / BadgeForEntity` | 引用解析+跳转+tooltip |

## 六、设计决策记录（为什么是现在这样）

| 决策 | 依据（用户反馈 / 轮次） |
|------|--------------------------|
| 两列情境布局替代单列堆叠 | R40「布局呆板，只有一列并且一直堆叠」 |
| 数值条只保留比例语义（伤害构成/耐久），其余改指标值 | R41「数值条没用，因为没有比较对象」 |
| 展开详情紧凑顶行（图标不独占一行） | R41「图片比较小的情况下，没必要独占一行」 |
| 声音按情境归位（装备区）+ 行内 ▶ 试听 | R40「音效是怎么和关联扯上关系的」+ R42 |
| 条件徽章语义色 + 效果翻译 | R36 + R43「hover 条件徽章翻译」 |
| 寿命推演（损耗率 → 能用多久） | R42 用户确认「耐久推演可以搞」 |
| 可视化零修改逻辑 | 「可视化不应该有修改逻辑，必须是只读的」 |
| 值域信息不进可视化（由 DataTable 承担） | 「值域信息没用，不同 mod 跨度大得很」 |
| 攻击链路不做深度图（深度 1 即可） | 「攻击链路图没啥必要」 |
| 克隆入口放 Value Editor（可视化不做） | 「克隆入口放 value editor 更合适」 |
| 图片修改后可视化重渲染可见（不单独做锚定） | 「图像修改后可视化重新渲染不就看得到」 |

## 七、验收要点

1. 全部 37 列语义覆盖：30 列进入 8 个呈现位置，`id`/`vImageUsage` 仅 Raw Data（有意的低价值省略）。
2. 每个数字类字段回答一个问题（不是复述列名）：损耗→寿命、士气→有效伤害、权重→概率。
3. 引用全部可跳转、可 hover 预览；未解析引用灰色兜底不崩溃。
4. 只读：无任何编辑控件；修改路径全部在 Value Editor。
5. 无数据区块整体隐藏（`null` body 跳过），不给用户看空壳。
