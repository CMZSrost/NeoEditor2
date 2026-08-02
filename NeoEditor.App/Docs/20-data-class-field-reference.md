# Neo Scavenger 数据类字段参考手册

> 来源：`游戏XML文本各项说明修正增强版.docx` + NeoEditor Data/Model/Game/*.cs
> 用途：订正可视化器的字段含义、引用目标、显示优先级

---

## ⚠️ 2026-08-02 实测订正

> 本节为对原版 `data/*.xml`（phpMyAdmin dump）+ NSE mod 数据全量扫描后的**实测更正**。
> **权威基准：[38-full-field-reference.md](38-full-field-reference.md)**（24 表实测值域）。
> 引用列语义以 **[37-reference-column-semantics.md](37-reference-column-semantics.md)** 为准。
> 与本文档正文冲突时，以 38/37 为准；正文对应行已用 ⚠️ 标注。

| # | 表.字段 | 本文档原说法 | 实测结论（详见 38） |
|---|--------|-------------|---------------------|
| 1 | `conditions.bPermanent` | "长期影响" | ⚠️ **1=瞬时效果**（吃/喝/一次性状态，dur=0） |
| 2 | `conditions.aFieldNames` | 74 种 | ⚠️ **78 种**（新增 WoundCut/WoundBruise/fFatigueModifier/m_fMoveCost） |
| 3 | `containertypes.strName` | 仅 6 种 | ⚠️ **39 种**（弹药/电池/滤芯等类别名） |
| 4 | `chargeprofiles.fPerUse/fPerHour` | 无负值说明 | ⚠️ **负值=充电/补充** |
| 5 | `battlemoves.bInAttackRange` | bool | ⚠️ 实测 **0-3 三值**（3=窃眼特例） |
| 6 | `battlemoves.vChanceType` | 未知 | ⚠️ 三值格式 `0,距离档,概率系数` |
| 7 | `encountertriggers.bUnique` | bool | ⚠️ 实测 **0-2 三值**（2=剔骸之谷特例） |
| 8 | `encounters.nType` | 0/1 | ⚠️ 实测 **4 值**：0=普通、1=搜刮、2=战斗、3=破解 |
| 9 | `encounters.aMinimapHexes` | 坐标+标签 | ⚠️ 格式 `x,y=标签[=flag]`，flag 语义待探索 |
| 10 | `factions.strName` | 3=食人族等 | ⚠️ 实测 14 阵营全确认：3=摇滚帮、5=魔迦怨灵… |
| 11 | `itemtypes.nCondID` | 辨识状态ID | ⚠️ **1=空状态（无条件）**占 468/537；mod 用 0 同样表示无条件 |
| 12 | `itemtypes.fDurability` | 耐久性 | ⚠️ 实测**恒 1**，原版未用可变值 |
| 13 | `hextypes.nTerrainCost` | 行动力 | ⚠️ **11=不可通行标记**（海洋/海滨/山地） |
| 14 | `recipes.bScrap` | 是否可分解 | ⚠️ 实测恒 1 |
| 15 | `itemtypes.Weight` | 重量 | ⚠️ **50×130 为不可拾取系统物品标记** |
| 16 | `battlemoves.strID` | 物品编号 | ⚠️ 实测 float **90.1-90.95**（90 组=战斗 UI 类物品） |

---

## 1. AttackMode（攻击模式）— `attackmodes`

## 1. AttackMode（攻击模式）— `attackmodes`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 唯一标识 | — |
| 2 | `Name` | `strName` | string | 武器名称，显示在游戏右下角 | — |
| 3 | `Notes` | `strNotes` | string | 作者注解，不影响游戏 | — |
| 4 | `Range` | `nRange` | int | 攻击距离，近战为1 | — |
| 5 | `DamageCut` | `fDamageCut` | float | 切割伤害，造成割伤/流血 | — |
| 6 | `DamageBlunt` | `fDamageBlunt` | float | 钝器伤害，造成挫伤/骨折 | — |
| 7 | `ChargeProfiles` | `strChargeProfiles` | string | 充能/弹药类型ID，逗号分隔 | → ChargeProfile |
| 8 | `Penetration` | `nPenetration` | int | 穿透等级 | — |
| 9 | `Type` | `nType` | enum | 0=近战(Melee), 1=远程(Ranged) | — |
| 10 | `Sound` | `strSnd` | string | 武器声音分类：Punch/Claws/Club/Blade/Rifle/Pistol/Laser/Bow/Throw/Choke/Grasp/Bite | — |
| 11 | `Transfer` | `bTransfer` | bool | 转移性：弹药是否留在目标/掉落地面（弓箭可回收） | — |
| 12 | `AttackerConditions` | `vAttackerConditions` | string | 攻击时的状态，格式 `{condId}x{mult}` | → Condition |
| 13 | `Image` | `strIMG` | string | 右下角武器图标图片文件名 | — |
| 14 | `Morale` | `fMorale` | float | **伤害加成系数（非百分比）**。实际伤害=(1+fMorale)×(1+近战/远程士气加成)×武器伤害。默认 0.25 | — |
| 15 | `WieldPhrase` | `strWieldPhrase` | string | 使用武器进入战斗时的文字描述 | — |
| 16 | `AttackPhrases` | `vAttackPhrases` | string | 攻击敌人时的文字描述，逗号分隔 | — |

**可视化重点**：Type(近战/远程图标) → 伤害条(Cut/Blunt) → Range → 弹药徽章 → 条件徽章 → 武器图片

---

## 2. BattleMove（战斗行动）— `battlemoves`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 序列编号 | — |
| 2 | `StrId` | `strID` | string | ⚠️ 物品编号（实测 float 90.1-90.95，90 组=战斗 UI 类物品，见 [38 §2](38-full-field-reference.md)） | → ItemType |
| 3 | `Name` | `strName` | string | 动作名称，不显示在游戏内 | — |
| 4 | `Notes` | `strNotes` | string | 注解 | — |
| 5 | `Success` | `strSuccess` | string | 行动成功时游戏内显示的文本，`<us>`=玩家，`<them>`=目标 | — |
| 6 | `Fail` | `strFail` | string | 行动失败时显示的文本 | — |
| 7 | `PopUp` | `strPopUp` | string | 游戏内战斗行动的说明 | — |
| 8 | `ChanceType` | `vChanceType` | string | ⚠️ 三值格式 `0,距离档,概率系数`（`0,7,0`=潜行类），见 [38 §2](38-full-field-reference.md) | — |
| 9 | `UsConditions` | `vUsConditions` | string | 需要我方处于的状态，格式 `[condId,param1,param2]` | → Condition |
| 10 | `ThemConditions` | `vThemConditions` | string | 需要对方处于的状态，格式同上 | → Condition |
| 11 | `PairConditions` | `vPairConditions` | string | 需要同时满足的状态 | → Condition |
| 12 | `UsFailConditions` | `vUsFailConditions` | string | 行动失败会导致我方陷入的状态 | → Condition |
| 13 | `ThemFailConditions` | `vThemFailConditions` | string | 行动失败会给对方带来的状态 | → Condition |
| 14 | `PairFailConditions` | `vPairFailConditions` | string | 行动失败会带来的影响 | → Condition |
| 15 | `UsPreConditions` | `vUsPreConditions` | string | 我方前一轮状态，负数=不可拥有该状态 | → Condition |
| 16 | `ThemPreConditions` | `vThemPreConditions` | string | 敌方前一轮状态 | → Condition |
| 17 | `SeeThem` | `nSeeThem` | int | 需要看见对方的暴露等级 | — |
| 18 | `SeeUs` | `nSeeUs` | int | 对方需要看见我方的暴露等级 | — |
| 19 | `AllOutOfRange` | `bAllOutOfRange` | bool | 是否需要在所有敌方攻击范围外 | — |
| 20 | `InAttackRange` | `bInAttackRange` | bool | ⚠️ 实测 0-3 三值（3=窃眼特例），非纯 bool，见 [38 §2](38-full-field-reference.md) | — |
| 21 | `MinCharges` | `nMinCharges` | int | 攻击次数（存疑） | — |
| 22 | `MinRange` | `nMinRange` | int | 最小使用距离，-1=全场覆盖 | — |
| 23 | `MaxRange` | `nMaxRange` | int | 最大使用距离，-1=全场覆盖 | — |
| 24 | `AttackModeType` | `nAttackModeType` | enum | -1=非攻击, 0=近战, 1=远程 | — |
| 25 | `HexTypes` | `vHexTypes` | string | 所在地图格子类型（官方全留空） | — |
| 26 | `Chance` | `fChance` | float | 可用的几率（百分比），如致命陷阱=0.05 | — |
| 27 | `Priority` | `fPriority` | float | AI优先级：同一回合中哪边的行动先触发 | — |
| 28 | `Detect` | `fDetect` | float | 被发现几率，0=不会发现 | — |
| 29 | `Order` | `fOrder` | float | AI使用行动的优先级 | — |
| 30 | `Fatigue` | `fFatigue` | float | 疲劳值消耗 | — |
| 31 | `Approach` | `bApproach` | bool | 是否接近对方 | — |
| 32 | `Offense` | `bOffense` | bool | 是否是攻击性动作 | — |
| 33 | `FallBack` | `bFallBack` | bool | 后退距离 | — |
| 34 | `Retreat` | `bRetreat` | bool | 撤退距离 | — |
| 35 | `Position` | `bPosition` | bool | 是否为姿势动作 | — |
| 36 | `Passive` | `bPassive` | bool | 是否被动 | — |

**可视化重点**：行为标签(Offensive/Retreat/Passive等) → 条件树(UsPre/ThemPre/UsEffect/ThemEffect/Pair等8组) → Success/Fail文本 → PopUp说明

---

## 3. CampType（营地类型）— `camptypes`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 序列编号 | — |
| 2 | `Description` | `strDesc` | string | 营地描述（游戏内显示） | — |
| 3 | `ImageList` | `vImageList` | string | 调用的图片文件名（默认 `ItmScavengeGrass01.png`） | — |
| 4 | `Capacities` | `aCapacities` | string | 营地容量（如 `34x26`：34行×26列） | — |
| 5 | `TreasureId` | `nTreasureID` | string | 营地战利品池ID | → TreasureTable |
| 6 | `Alertness` | `m_fAlertness` | float | 警戒值（百分比） | — |
| 7 | `Visibility` | `m_fVisibility` | float | 可见值（百分比），如-0.05=-5% | — |
| 8 | `WetTempAdjustMod` | `WetTempAdjustMod` | float | 温度调节修正值 | — |
| 9 | `HealPerHourMod` | `m_fHealPerHourMod` | float | 每小时回复修正值（百分比） | — |
| 10 | `SleepQuality` | `fSleepQuality` | float | 睡眠质量（百分比），负值=差 | — |

**可视化重点**：营地图片 → 容量 → Sleep/Heal/Visibility/Alertness 进度条 → TreasureTable引用

---

## 4. ChargeProfile（充能/弹药类型）— `chargeprofiles`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `nID` | int | 序列编号 | — |
| 2 | `Name` | `strName` | string | 名称（不显示在游戏中），如 `nanomedkit electricity` | — |
| 3 | `ItemId` | `strItemID` | string | 物品编码（如 `10.3`，10为组号，3为次级组号） | — |
| 4 | `PerUse` | `fPerUse` | float | ⚠️ 每次使用消耗；**负=充电/补充**（id30 每次+40格电量），见 [38 §4](38-full-field-reference.md) | — |
| 5 | `PerHour` | `fPerHour` | float | ⚠️ 每小时消耗；**负=每小时补充**（id28 每小时+10格电量），见 [38 §4](38-full-field-reference.md) | — |
| 6 | `PerHourEquipped` | `fPerHourEquipped` | float | 装备时每小时消耗（仅用于XM54过滤芯片） | — |
| 7 | `PerHex` | `fPerHex` | float | 每走一格消耗的数量 | — |
| 8 | `Degrade` | `bDegrade` | bool | 是否降解（如防毒面具碳芯就不降解） | — |

**可视化重点**：消耗率条(PerUse/PerHour/PerHex) → Degrade标签 → ItemId引用

---

## 5. Condition（状态）— `conditions`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 序列编号（其他地方用此编号而非名称引用） | — |
| 2 | `Name` | `strName` | string | 状态名称（游戏内显示），如 `Starving` | — |
| 3 | `Description` | `strDesc` | string | 获得该状态的描述文本，`<us>`=玩家 | — |
| 4 | `FieldNames` | `aFieldNames` | string | ⚠️ 效果字段列表，实测 **78 种**（含 WoundCut/WoundBruise/fFatigueModifier/m_fMoveCost），见 [38 §5 附录A](38-full-field-reference.md) | — |
| 5 | `Modifiers` | `aModifiers` | string | 与 FieldNames 一一对应的属性变化值 | — |
| 6 | `Effects` | `aEffects` | string | 特殊影响：SetImmunity(免疫力)、ArmorWound(护甲) | — |
| 7 | `Fatal` | `bFatal` | bool | 是否致命（得到该状态即死） | — |
| 8 | `IdNext` | `vIDNext` | string | 此状态结束后触发的下一状态ID | → Condition |
| 9 | `Duration` | `fDuration` | float | 持续时间（小时） | — |
| 10 | `Permanent` | `bPermanent` | bool | ⚠️ **1=瞬时效果**（吃/喝/一次性消费状态，dur=0），非"永久"，见 [38 §5](38-full-field-reference.md) | — |
| 11 | `ChanceNext` | `vChanceNext` | string | 触发下一状态的几率，1=100% | — |
| 12 | `Stackable` | `bStackable` | bool | 是否可堆叠（不可叠加则在持续时间内再获得不刷新） | — |
| 13 | `Display` | `bDisplay` | bool | 该状态是否可见（如蓝腐1不显示） | — |
| 14 | `DisplayOther` | `bDisplayOther` | bool | 是否对其他人可见（战斗中可见敌方strong/tough状态） | — |
| 15 | `DisplayGameOver` | `bDisplayGameOver` | bool | 是否显示在游戏通关/死亡列表中 | — |
| 16 | `Color` | `nColor` | enum | 状态颜色：0=白, 1=红(负面), 2=绿(正面), 3=黄 | — |
| 17 | `ResetTimer` | `bResetTimer` | bool | 刷新时间（每小时） | — |
| 18 | `RemoveAll` | `bRemoveAll` | bool | 移除所有（ZomZom食堂/投降） | — |
| 19 | `RemovePostCombat` | `bRemovePostCombat` | bool | 移除物品所在位置 | — |
| 20 | `TransferRange` | `nTransferRange` | int | 传染距离，-1=不传播 | — |
| 21 | `Thresholds` | `aThresholds` | string | 阈值（目前用于传奇技能触发） | — |

**可视化重点**：致命/永久/可堆叠标签 → FieldNames↔Modifiers配对表 → 条件链(IdNext) → 颜色+持续时间 → Effects描述

---

## 6. ContainerType（容器类型）— `containertypes`

> ⚠️ **实测订正**：`strName`（容器/内容类别名）实测 **39 种**（弹药/电池/滤芯/粗/地形/电/防火/防水…），非 6 种，见 [38 §6](38-full-field-reference.md)。

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 序列编号 | — |
| 2 | `Name` | `strName` | string | 属性名称：1=防水, 2=精, 3=粗, 4=事件(encounter), 5=技能(skill), 6=营地(camps) | — |

**可视化重点**：名称 → 反向引用（哪些ItemType使用）

---

## 7. Creature（生物）— `creatures`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 序列编号 | — |
| 2 | `Name` | `strName` | string | 生物名称，如 `Dogman` | — |
| 3 | `NamePublic` | `strNamePublic` | string | 未接触时显示的名称（如DMC guard在接近前显示为"stranger"） | — |
| 4 | `Notes` | `strNotes` | string | 注解 | — |
| 5 | `Image` | `strImg` | string | 地图上显示的图片文件名 | — |
| 6 | `EncounterIds` | `vEncounterIDs` | string | 遇到该生物会触发的事件ID，逗号分隔 | → Condition |
| 7 | `MovesPerTurn` | `nMovesPerTurn` | int | 每回合移动点数 | — |
| 8 | `TreasureId` | `nTreasureID` | string | 初始带有的物品 | → TreasureTable |
| 9 | `Faction` | `nFaction` | string | 所属阵营/派别ID（同阵营不互攻） | → Faction |
| 10 | `AttackModes` | `vAttackModes` | string | 攻击方式ID，逗号分隔 | → AttackMode |
| 11 | `BaseConditions` | `vBaseConditions` | string | 基础状态，格式 `condId=probability` | → Condition |
| 12 | `CorpseId` | `nCorpseID` | string | 尸体编号（战利品池编号） | → TreasureTable |
| 13 | `Activities` | `vActivities` | string | 生物的活动描述（仅注释用途） | — |

**可视化重点**：生物图片 → 派系徽章 → 攻击方式徽章 → 基础状态(condId=概率) → 战利品/尸体TreasureTable → 活动描述

---

## 8. CreatureSource（生物刷新点）— `creaturesources`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 序列编号 | — |
| 2 | `Name` | `strName` | string | 刷新生物的名称 | — |
| 3 | `X` | `nX` | int | X轴坐标，-1=玩家当前坐标 | — |
| 4 | `Y` | `nY` | int | Y轴坐标 | — |
| 5 | `CreatureId` | `nCreatureID` | string | 刷新的生物编号 | → Creature |
| 6 | `Min` | `nMin` | int | 最小刷新数量 | — |
| 7 | `Max` | `nMax` | int | 最大刷新数量 | — |
| 8 | `Weight` | `fWeight` | float | 权重 | — |

**可视化重点**：坐标(X,Y) → 刷新数量(Min-Max) → Creature引用 → Weight

---

## 9. DataFile（电子产品数据）— `datafiles`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 序列编号 | — |
| 2 | `Name` | `strName` | string | 数据类型：Database/txt/img/df/vid等 | — |
| 3 | `Description` | `strDesc` | string | 游戏内对数据的描述（汉化主要内容） | — |
| 4 | `Value` | `fValue` | float | 可以卖的价值 | — |
| 5 | `Image` | `strImg` | string | 调用的图片文件名 | — |

**可视化重点**：图片 → 数据内容(Description) → 价值(Value)

---

## 10. DmcPlace（底特律城区建筑）— `dmcplaces`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 序列编号 | — |
| 2 | `Image` | `strImg` | string | 建筑图标名称，如 `btn_dmc_diner` | — |
| 3 | `EncounterId` | `nEncounterID` | int | 调用的剧情代码ID | → Encounter |
| 4 | `X` | `nX` | int | X轴坐标 | — |
| 5 | `Y` | `nY` | int | Y轴坐标 | — |

**可视化重点**：建筑图标 → 坐标(X,Y) → Encounter引用

---

## 11. Encounter（剧情/遭遇）— `encounters`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 唯一编号（新增时在最高数字上加1） | — |
| 2 | `Name` | `strName` | string | 剧情名称（黄色文本，仅输入信息选项时可见） | — |
| 3 | `Description` | `strDesc` | string | 剧情主体文本（最长约130-150词，更长需拆分） | — |
| 4 | `Image` | `strImg` | string | 剧情显示的图片，默认使用当前六角格快照 | — |
| 5 | `TreasureId` | `nTreasureID` | string | 发生剧情获得物品 | → TreasureTable |
| 6 | `RemoveTreasureId` | `nRemoveTreasureID` | string | 移除物品编号（提交任务物品/失去/摧毁） | → TreasureTable |
| 7 | `Conditions` | `aConditions` | string | 触发后玩家状态改变 | → Condition |
| 8 | `PreConditions` | `aPreConditions` | string | 发生剧情前提状态，负数=必须不拥有 | → Condition |
| 9 | `Price` | `fPrice` | float | 给/扣的钱，玩家钱不够则节点不显示 | — |
| 10 | `Responses` | `aResponses` | string | 玩家可选回应：`物品IDx数量=接下来剧情IDx参数...` | — |
| 11 | `MinimapHexes` | `aMinimapHexes` | string | ⚠️ 格式 `x,y=标签[=flag]`（flag 语义待探索），见 [38 §11](38-full-field-reference.md) | — |
| 12 | `RemoveCreatures` | `bRemoveCreatures` | bool | 是否移除当前格生物 | — |
| 13 | `RemoveUsed` | `bRemoveUsed` | bool | 是否移除用来抵达此节点的物品（如光源） | — |
| 14 | `ItemsId` | `nItemsID` | string | 特殊遭遇可获得的物品（如破碎窗户/控制面板） | → ItemType |
| 15 | `CreatureId` | `nCreatureID` | string | 产生的生物ID | → Creature |
| 16 | `CreatureHex` | `ptCreatureHex` | string | 生物出现坐标，如 `40,0`（半径,方向） | — |
| 17 | `Teleport` | `ptTeleport` | string | 玩家传送目标坐标，仅x=随机传送到x半径环 | — |
| 18 | `Editor` | `ptEditor` | string | 编辑器坐标（游戏忽略） | — |
| 19 | `Type` | `nType` | enum | ⚠️ 实测 4 值：0=普通、1=搜刮、2=战斗（仅 id236）、3=破解，见 [38 §11](38-full-field-reference.md) | — |
| 20 | `LootChance` | `fLootChance` | float | 搜刮成功几率 | — |
| 21 | `AccidentChance` | `fAccidentChance` | float | 事故发生几率 | — |
| 22 | `CreatureChance` | `fCreatureChance` | float | 生物出现几率 | — |
| 23 | `Accidents` | `vAccidents` | string | 事故发生时的表格ID | → Encounter |
| 24 | `Loot` | `vLoot` | string | 搜刮额外战利品 | → TreasureTable |

**可视化重点**：剧情图片 → 类型标签(Normal/Scavenge) → 剧情文本(Description) → Responses解析 → 前/后置条件徽章 → TreasureTable/Creature引用 → 各种几率

---

## 12. EncounterTrigger（事件触发器）— `encountertriggers`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 序列编号 | — |
| 2 | `Name` | `strName` | string | 触发器名称 | — |
| 3 | `EncounterId` | `nEncounterID` | int | 触发的事件编号 | → Encounter |
| 4 | `Chance` | `fChance` | float | 触发几率 | — |
| 5 | `LocBased` | `bLocBased` | bool | 基于坐标触发 → 对应 aArea | — |
| 6 | `DateBased` | `bDateBased` | bool | 基于时间触发 → 对应 dateMin/dateMax | — |
| 7 | `HexBased` | `bHexBased` | bool | 基于格点触发 → 对应 aHexTypes | → HexType |
| 8 | `Unique` | `bUnique` | bool | ⚠️ 实测 0-2 三值（2=剔骸之谷特例），非纯 bool，见 [38 §12](38-full-field-reference.md) | — |
| 9 | `AIPassable` | `bAIPassable` | bool | 是否可被AI触发 | — |
| 10 | `Area` | `aArea` | string | 触发坐标，格式 `x,y,距离` | — |
| 11 | `DateMin` | `dateMin` | string | 最小触发时间，格式 `年-月-日-小时`，游戏开始=`1000-0-1-6` | — |
| 12 | `DateMax` | `dateMax` | string | 最大触发时间 | — |
| 13 | `HexTypes` | `aHexTypes` | string | 可触发的地图格点ID列表 | → HexType |

**可视化重点**：触发类型标签(Loc/Date/Hex) → 几率 → 区域/日期范围 → Encounter引用 → HexType引用

---

## 13. Faction（阵营/派系）— `factions`

> ⚠️ **实测订正**：14 阵营名称全部确认——3=摇滚帮、5=魔迦怨灵、8=鹿、9=夜辛卡…（非旧表 3=食人族），见 [38 §13](38-full-field-reference.md)。

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 派别编号（1=狗人, 2=掠夺者, 3=食人族, 4=蓝蛙教...） | — |
| 2 | `Name` | `strName` | string | 阵营名称 | — |
| 3 | `DictFactions` | `dictFactions` | string | 与其他派系声望关系，格式 `factionId=value` | → Faction |

**已识别阵营**：
| ID | 名称 | 说明 |
|----|------|------|
| 0 | — | 玩家/中立(?) |
| 1 | Dogman | 狗人 |
| 2 | Looter | 掠夺者 |
| 3 | Bad Mutha | 食人族 |
| 4 | Blue Frog | 蓝蛙教 |
| 5-14 | — | 其他阵营 |

**可视化重点**：阵营名 → 外交关系条（彩色横条：Allied/Friendly/Neutral/Hostile/Enemy）→ 成员生物列表

---

## 14. ForbiddenHex（保护区/禁用格点）— `forbiddenhexes`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 序列编号 | — |
| 2 | `X` | `nX` | int | X轴坐标 | — |
| 3 | `Y` | `nY` | int | Y轴坐标 | — |
| 4 | `Name` | `strName` | string | 保护区所属阵营或名称（如ATN, DMC） | — |

**可视化重点**：名称 → 坐标(X,Y) → Forbidden标签

---

## 15. GameVar（游戏变量）— `gamevars`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Name` | `strName` | string | 变量名（如 `nSkillPoints`） | — |
| 2 | `Type` | `strType` | string | 数值类型（int/Number等） | — |
| 3 | `Value` | `strValue` | string | 具体数值 | — |

**可视化重点**：变量名 → 类型标签 → 值

---

## 16. Headline（新闻头条）— `headlines`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 序列编号 | — |
| 2 | `HeadlineText` | `strHeadline` | string | 报纸具体文本内容（汉化内容） | — |

**可视化重点**：标题文本展示

---

## 17. HexType（地块类型）— `hextypes`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 序列编号（地图strDef中用此编号） | — |
| 2 | `Name` | `strName` | string | 地块名称，如 `ocean` | — |
| 3 | `Description` | `strDesc` | string | 游戏内显示的名称，如 `deep water` | — |
| 4 | `TerrainCost` | `nTerrainCost` | int | ⚠️ 移动消耗；**11=不可通行标记**（海洋/海滨/山地），见 [38 §17](38-full-field-reference.md) | — |
| 5 | `VizLimiter` | `nVizLimiter` | int | 视野减少值 | — |
| 6 | `VizIncrease` | `nVizIncrease` | int | 视野增加值 | — |
| 7 | `TreasureId` | `nTreasureID` | string | 地形上的战利品池ID（默认3=空） | → TreasureTable |
| 8 | `Passable` | `bPassable` | enum | 能否通行：0=不可，1=可 | — |
| 9 | `ScavengeInitialId` | `nScavengeInitialID` | string | 初次搜刮战利品池ID | → TreasureTable |
| 10 | `ScavengeItemsIdPerHour` | `nScavengeItemsIDPerHour` | string | 每小时搜刮战利品ID（默认25） | → TreasureTable |
| 11 | `CampItems` | `nCampItems` | int | 营地类型（默认5） | — |
| 12 | `LightLevels` | `vLightLevels` | string | 24小时亮度：黎明/上午/正午/下午/黄昏/午夜 | — |
| 13 | `DefaultCampId` | `nDefaultCampID` | int | 默认营地ID（默认517） | → CampType |
| 14 | `MinRange` | `nMinRange` | int | 遭遇生物的最小距离 | — |
| 15 | `MaxRange` | `nMaxRange` | int | 遭遇生物的最大距离 | — |
| 16 | `ConditionIds` | `vCondIDs` | string | 进入该地块会获得的状态ID | → Condition |

**可视化重点**：可通行标签 → Cost/Visibility → 光线等级表（6列） → TreasureTable引用(Scavenge/Initial/Hourly) → Camp引用 → On-Enter Condition

---

## 18. Ingredient（合成材料）— `ingredients`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `nID` | int | 材料编号（itemtype引用此编号） | — |
| 2 | `Name` | `strName` | string | 材料名称，如 `flame source` | — |
| 3 | `RequiredProps` | `strRequiredProps` | string | 材料必须带有的属性ID，`&`表示"与" | → ItemProp |
| 4 | `ForbidProps` | `strForbidProps` | string | 材料不可带有的属性ID | → ItemProp |

**重要机制**：配方不以具体物品为准，而是以物品的**属性**为准。如：打火机+纸=小火，实际是"热源(Igniter属性3)"+"易燃物(EasilyIgnitable属性1)"=小火。

**可视化重点**：必需属性(RequiredProps, 绿色徽章) vs 禁止属性(ForbidProps, 红色徽章) → 反向引用（哪些Recipe使用）

---

## 19. ItemProp（物品属性）— `itemprops`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `nID` | int | 属性编号（配方中引用） | — |
| 2 | `PropertyName` | `strPropertyName` | string | 属性名称：1=易燃, 2=光学放大, 3=可燃物/热源, 4=丹尼酸源... | — |

**已知属性**：
| ID | 名称 | 含义 |
|----|------|------|
| 1 | easily ignitable | 易燃 |
| 2 | optical zoom | 光学放大 |
| 3 | igniter | 可燃物/热源 |
| 4 | tannin source | 丹尼酸源 |

**可视化重点**：属性名 → 反向引用（哪些ItemType和Ingredient使用了此属性）

---

## 20. ItemType（物品类型）— `itemtypes`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 序列编号（**注意**：物品用GroupID.SubgroupID表示，非此Id） | — |
| 2 | `GroupId` | `nGroupID` | int | 分组编号 | — |
| 3 | `SubgroupId` | `nSubgroupID` | int | 次级分组编号（如5.3=塑料购物袋） | — |
| 4 | `Name` | `strName` | string | 名称 | — |
| 5 | `Description` | `strDesc` | string | 游戏内描述（汉化内容） | — |
| 6 | `DescriptionAlt` | `strDescAlt` | string | 真实描述（需要技能才能看到，如阿莫西林没技能=白色药丸，有技能=阿莫西林） | — |
| 7 | `ConditionId` | `nCondID` | int | ⚠️ 辨识需要的状态ID；**1=空状态（无条件）**占 468/537，mod 用 0 同样表示无条件（0/1 等价），见 [38 §20](38-full-field-reference.md) | → Condition |
| 8 | `ImageList` | `vImageList` | string | 调用图片（多张逗号分隔） | — |
| 9 | `SpriteList` | `vSpriteList` | string | 大地图小人显示图片：`部位=图片名`（20=左手,21=右手,22=背部,11=上身...） | — |
| 10 | `ImageUsage` | `vImageUsage` | string | ImageList图片使用位置：0=地上空,1=地上满,2=手上空,3=手上满,4=物品栏空,5=物品栏满 | — |
| 11 | `Weight` | `fWeight` | float | ⚠️ 重量；**50×130 为不可拾取系统物品标记**，见 [38 §20](38-full-field-reference.md) | — |
| 12 | `MonetaryValue` | `fMonetaryValue` | float | 价值 | — |
| 13 | `MonetaryValueAlt` | `fMonetaryValueAlt` | float | 鉴定后价值 | — |
| 14 | `Durability` | `fDurability` | float | ⚠️ 实测**恒 1**，原版未用可变值，见 [38 §20](38-full-field-reference.md) | — |
| 15 | `DegradePerHour` | `fDegradePerHour` | float | 每小时耐久损耗 | — |
| 16 | `EquipDegradePerHour` | `fEquipDegradePerHour` | float | 装备时每小时损耗 | — |
| 17 | `DegradePerUse` | `fDegradePerUse` | float | 每次使用消耗耐久 | — |
| 18 | `DegradeTreasureIds` | `vDegradeTreasureIDs` | string | 自然损耗完毕/使用损耗完获得的物品 | → TreasureTable |
| 19 | `EquipConditions` | `aEquipConditions` | string | 装备上的状态 | → Condition |
| 20 | `PossessConditions` | `aPossessConditions` | string | 拥有的状态（不装备也生效，技能原理） | → Condition |
| 21 | `UseConditions` | `aUseConditions` | string | 使用后带来的状态 | → Condition |
| 22 | `Capacities` | `aCapacities` | string | 容器容积（如 `4x6`） | — |
| 23 | `EquipSlots` | `vEquipSlots` | string | 装备插槽：`部位=参数1=参数2` | — |
| 24 | `UseSlots` | `vUseSlots` | string | 使用槽（代码211=右键有使用选项） | — |
| 25 | `SocketLocked` | `bSocketLocked` | bool | 锁定属性（如残废图片、对话选项栏都是锁定的物品） | — |
| 26 | `Properties` | `vProperties` | string | 物品属性ID列表 | → ItemProp |
| 27 | `ContentIDs` | `aContentIDs` | string | 能装入的nFormatID属性物品 | — |
| 28 | `FormatId` | `nFormatID` | int | 物品本身属性（与ContentIDs配合） | — |
| 29 | `TreasureId` | `nTreasureID` | string | 物品内装了什么 | → TreasureTable |
| 30 | `ComponentId` | `nComponentID` | string | 可逆向合成时拆解所得物品 | → TreasureTable |
| 31 | `Mirrored` | `bMirrored` | bool | 镜像（鞋子的属性） | — |
| 32 | `SlotDepth` | `nSlotDepth` | int | 决定多件衣服哪件在上面 | — |
| 33 | `ChargeProfiles` | `strChargeProfiles` | string | 充能ID（破解软件/电子产品） | → ChargeProfile |
| 34 | `AttackModes` | `aAttackModes` | string | 攻击方式ID | → AttackMode |
| 35 | `StackLimit` | `nStackLimit` | int | 最大堆叠数 | — |
| 36 | `SwitchIds` | `aSwitchIDs` | string | 转变ID（电子产品开关机实际是物品替换） | → ItemType |
| 37 | `Sounds` | `aSounds` | string | 拿起放下物品的声音文件 | — |

**已知装备槽位**：
| 代码 | 部位 |
|------|------|
| 2 | 左脚 |
| 3 | 右脚 |
| 4 | 下身(Legs) |
| 11 | 上身(Torso) |
| 13 | 左肩/背部 |
| 14 | 右肩 |
| 17 | 头部/面部 |
| 20 | 左手 |
| 21 | 右手 |
| 22 | 背部 |

**可视化重点**：图片画廊(ImageList) → 物品身份(ID+Name+Description) → Stat条(Weight/StackLimit/Durability/Value) → 装备槽位 → Properties标签 → 引用条(AttackMode/ChargeProfile/Condition/TreasureTable)

---

## 21. Map（地图）— `maps`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 序列编号 | — |
| 2 | `Name` | `strName` | string | 调用的图片文件名（如 `MapMiniMichigan.png`） | — |
| 3 | `Definition` | `strDef` | string | 地图定义数据（逗号分隔的数字=HexType ID） | → HexType |

**说明**：地图由数字构成，0=深海/浅水随机，3=树林/平原/沙地随机，其他编号与HexType一致。

**可视化重点**：地图图片名 → 数据点数

---

## 22. Recipe（配方/合成表）— `recipes`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `nID` | int | 序列编号 | — |
| 2 | `Name` | `strName` | string | 配方名称 | — |
| 3 | `SecretName` | `strSecretName` | string | 隐秘名称（如水表面上名称一样，实际有毒水/污染水/无菌水不同） | — |
| 4 | `Tools` | `strTools` | string | 合成需要工具/技能，格式 `quantity x ingredientId` | → Ingredient |
| 5 | `Consumed` | `strConsumed` | string | 消耗的材料，格式同上 | → Ingredient |
| 6 | `Destroyed` | `strDestroyed` | string | 摧毁的物品（仅发现火把类物品有，且值为2） | — |
| 7 | `TreasureId` | `nTreasureID` | string | 合成获得的物品战利品池ID | → TreasureTable |
| 8 | `Hours` | `fHours` | float | 合成消耗的行动值 | — |
| 9 | `Reverse` | `nReverse` | int | 是否可逆（如衣服拆了不能合成，长短线可互拆） | — |
| 10 | `HiddenId` | `nHiddenID` | string | 隐藏配方（在配方列表找不到） | → Recipe |
| 11 | `Identify` | `bIdentify` | bool | 是否需鉴别（如水需水分析器） | — |
| 12 | `TransferComponents` | `bTransferComponents` | bool | 未知 | — |
| 13 | `AlsoTry` | `vAlsoTry` | string | 相同成品的其他配方ID | → Recipe |
| 14 | `TempTreasureId` | `nTempTreasureID` | string | 合成时虚影显示的合成结果（默认3=空） | → TreasureTable |
| 15 | `DegradeOutput` | `bDegradeOutput` | bool | true=成品耐久100%，false=成品耐久=材料中最低耐久 | — |
| 16 | `Type` | `strType` | string | 配方类型：工具/食物/医务/武器/载具/杂项(misc) | — |
| 17 | `Scrap` | `bScrap` | bool | ⚠️ 实测恒 1（原版无 0 值，语义待探索），见 [38 §22](38-full-field-reference.md) | — |

**核心机制**：配方以Ingredient的**属性**匹配物品，非具体物品。

**可视化重点**：配方类型标签 → Tools/Consumed/Destroyed Ingredient徽章 → 产品(TreasureTable→ItemType) → AlsoTry备选配方 → Hours/Reverse/DegradeOutput

---

## 23. TreasureTable（战利品池）— `treasuretable`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 战利品池编号 | — |
| 2 | `Name` | `strName` | string | 战利品名称 | — |
| 3 | `Treasures` | `aTreasures` | string | 战利品内容：`物品IDx几率x数量`，`\|`=或，`,`=和 | → ItemType / TreasureTable |
| 4 | `Nested` | `bNested` | bool | 生成物品是否装在同时生成的容器里 | — |
| 5 | `Suppress` | `bSuppress` | bool | 抑制内容物生成（1时水瓶里没水，枪里没子弹） | — |
| 6 | `Identify` | `bIdentify` | bool | 生成的物品是否已辨识 | — |

**格式解析**：
- `411x1x3-5`：411号战利品池，100%概率，随机3-5个
- `32.1x0.0625x1-1|101.1x0.0625x1-1`：竖线分隔=OR关系，16种随机1种，各0.0625概率
- `物品IDx概率x数量`：数量可用`min-max`格式
- 物品ID格式 `GroupId.SubgroupId` 指向ItemType，纯数字指向TreasureTable(嵌套)

**可视化重点**：战利品树(OR组+AND项) → 概率徽章(颜色深浅) → 数量范围 → 嵌套TreasureTable递归 → Nested/Suppress/Identify标签

---

## 24. BarterHex（交易商店）— `barterhexes`

| # | 模型字段 | DB列名 | 类型 | 说明 | 引用 |
|---|---------|--------|------|------|------|
| 1 | `Id` | `id` | int | 序列编号 | — |
| 2 | `X` | `nX` | int | X轴坐标 | — |
| 3 | `Y` | `nY` | int | Y轴坐标 | — |
| 4 | `Buys` | `bBuys` | bool | 是否购买玩家物品（0=不买，1=买） | — |
| 5 | `RestockTreasureId` | `nRestockTreasureID` | int | 引用的战利品数据（底特律C商店例外，使用3号池） | (→ TreasureTable) |

---

## 附录A：常用数据格式速查

### 引用模式
| 模式 | 格式 | 示例 |
|------|------|------|
| 逗号分隔ID | `id,id,id` | `7,12,15` |
| ID=值 | `id=value` | `38=1,50=0.5` |
| 数量xID | `qty x id` | `1x2+1x3` |
| 物品ID | `GroupId.SubgroupId` | `90.29` |
| 战利品项 | `IDx概率x数量` | `411x1x3-5` |
| OR组 | `\|` | `item1|item2|item3` |
| Condition方括号组 | `[id,param1,param2]` | `[98,0,0],[339,0,0]` |
| Encounter Response | `itemId x qty = nextEncId x p1 x p2 x p3 x p4` | `90.1x1=12x1x0x0x0` |
| 坐标 | `x,y` | `20,164` 或 `40,0`（半径,方向） |
| 时间 | `年-月-日-小时` | `1000-0-1-22` |

### Condition Color 枚举
| 值 | 颜色 | 含义 |
|----|------|------|
| 0 | 白色 | — |
| 1 | 红色 | 负面状态 |
| 2 | 绿色 | 正面状态 |
| 3 | 黄色 | — |

---

## 附录B：类型间引用关系图

```
AttackMode → ChargeProfile, Condition
BattleMove → Condition (8组不同条件)
CampType → TreasureTable
Condition → Condition (IdNext链)
ContainerType ← ItemType (反向引用)
Creature → Faction, AttackMode, Condition, TreasureTable (×2: Loot+Corpse)
CreatureSource → Creature
DataFile → (无引用)
DmcPlace → Encounter
Encounter → TreasureTable (×2), Condition (×2), Creature, Encounter (Accidents)
EncounterTrigger → Encounter, HexType
Faction → Faction (DictFactions), ← Creature (反向)
ForbiddenHex → (无引用)
GameVar → (无引用)
Headline → (无引用)
HexType → TreasureTable (×3: Scavenge/Initial/Hourly), CampType, Condition
Ingredient → ItemProp, ← Recipe (反向)
ItemProp ← ItemType, Ingredient (反向)
ItemType → Condition, TreasureTable (×3), ChargeProfile, AttackMode, ItemProp
Map → HexType (strDef中的数字)
Recipe → Ingredient (×2), TreasureTable (×2), Recipe (×2: HiddenId+AlsoTry)
TreasureTable → ItemType, TreasureTable (嵌套)
BarterHex → (无代码引用字段，RestockTreasureId为int)
```
