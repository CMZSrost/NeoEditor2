# 37 引用列语义参考（三部分模型：集合 × 实体 × 装饰）

> 日期：2026-08-02
> 范围：原版 `data/*.xml`（`D:\software\Steam\steamapps\common\Neo Scavenger\data`）24 表全量扫描 + 与 [20-data-class-field-reference.md](20-data-class-field-reference.md)、游戏官方文档交叉验证
> 方法：每个引用列按「集合-实体-装饰」三部分模型分解，用真实数据全量统计验证各部分
> 用途：引用解析器（ReferenceListSerializer / ReferencePattern / ReferenceFieldAttribute）的语义依据；本文档是**值级（value syntax）**参考

## 置信度图例

| 标记 | 含义 |
|------|------|
| ✅ | 已确认（数据统计 + 交叉验证 + 文档佐证，无歧义） |
| 🟡 | 推测（有数据支撑但未完全证实 / 存在反例） |
| ❓ | 未知（有真实样本但无法推断含义） |

---

## §0 三部分模型（核心框架）

### 0.1 模型定义

每个引用列由三部分组成：

```
引用列值 = 集合(0~多个引用) + 每个引用 = 实体 + 装饰
```

| 部分 | 定义 | 规则 |
|------|------|------|
| **集合** | 引用列都是 0~多个引用的容器，用**分隔符**切分 | 空值 = 空集合；单值列 = 恰好 1 个元素的集合 |
| **实体** | 实际引用的对象，格式**固定**为 `Namespace:Id` 或 `Namespace:GroupId.SubgroupId`（itemtype 专属） | `Namespace:` 可选；缺省时 = 源实体所属 mod 的 Namespace（R16） |
| **装饰** | 实体之外的剩余部分，由等号/`x`/括号/负号等构成的**字符串模板** | 装饰可出现在实体**前**或**后**（或两侧） |

### 0.2 与代码接口的映射（现有实现即按此设计）

| 模型部分 | 代码接口 | 说明 |
|----------|---------|------|
| 集合 | `ReferenceFieldAttribute.Separator` | 分隔符；`null` = 单值集合 |
| 实体 | `ReferenceFieldAttribute.TargetEntityType` + `TargetKey` | TargetKey 声明实体键形式：`{Id}`（默认）/ `{GroupId}.{SubgroupId}`（itemtype 专属） |
| 实体（备选） | `SecondaryTargetEntityType` + `SecondaryTargetKey` | 主实体查找失败时的备用目标（aTreasures 嵌套池） |
| 装饰 | `ReferenceFieldAttribute.Pattern` | 装饰模板（`{id}`/`{id}x{mult}`/`{mult}x{id}`/`{id}={value}`/`{value}={id}`/`[{id}`） |
| 装饰实现 | `ReferencePattern` 策略类 | 6 个子类对应 6 种装饰模板，职责 = `ExtractRawId`（剥离装饰取实体）+ `FormatExtraInfo` |

### 0.3 解析顺序

```
1. 集合层：按 Separator 切分 → 段列表（0~多个）
2. 装饰层：按 Pattern 模板剥离装饰 → 得到实体原文（raw id）
3. 实体层：按 TargetKey 分解实体 →
     {Id}:    [Namespace:]Id
     {G}.{S}: [Namespace:]GroupId.SubgroupId   （仅 ItemType 目标）
4. 命名空间（⚠️ 2026-08-02 订正，见 §0.4）：无前缀 → 源实体 Namespace；"0:" → 显式 0（game base）命名空间；"NSE:" → 显式 NSE 命名空间
```

### 0.4 命名空间前缀语义（⚠️ 订正 2026-08-02，mod 数据 + 代码实证）

> R16 §1/§5 原表述「`0:` 是『同 namespace』简写」**有误**。`0:` 是**显式指向 0 命名空间（game base）**——数据实证：NSE 中 `0:5.6` 指向原版「存储」（NSE 自己的 5.6 是「水袋」）；代码 `ReferenceResolver.LookupEntityId` 对 `0:38` 直接 `LookupByNs(type, "0", pk)`。

| 写法 | 语义 | 示例 |
|------|------|------|
| 无前缀 | 同 sourceNs（源实体命名空间） | NSE 实体中 `211` → ns=NSE |
| `0:` | **显式 0 命名空间（game base）** | NSE 中 `0:5.6` → 原版 5.6 |
| `NSE:` | 显式 NSE 命名空间 | `NSE:86.6` → NSE 的 86.6 |
| `:`（空前缀） | 同 sourceNs（实际数据 0 出现，理论形式） | — |

**适用范围扩展（mod 数据发现）**：命名空间前缀同样用于
- **图片引用**（`attackmodes.strIMG`、`datafiles.strImg`、`itemtypes.vImageList`、`creatures.strImg`）：`0:AModeSpearSharp.png` / `NSE:ItmDataAddr.png` / `NSE:CreSquirrel.png`
- **aEffects 参数实体**（37 §5.3）：`SetImmunity=0:316,0:618,463`、`ChainCondition=NSE:-457`、`AddItemGround=0:17,0,0,0`、`ChangeGlobalFactionRep=NSE:1,-100,1`

---

## §1 集合层：分隔符全集

| 分隔符 | 集合语义 | 使用列 | 置信度 |
|:--:|------|----------|:--:|
| （null） | 单值集合（恰好 1 个引用） | nTreasureID/nFormatID/nCondID 等全部单值列 | ✅ |
| `,` | AND 并列（全部生效） | 绝大多数多值引用列 | ✅ |
| `\|` | OR 分支（多选一） | `treasuretable.aTreasures`（可与 `,` 混用） | ✅ |
| `+` | AND 并列 | `recipes.strTools/strConsumed/strDestroyed`、`encounters.aResponses`（左侧多物品） | ✅ |
| `;` | AND 并列（带 key 的分组） | `conditions.aThresholds`；`conditions.aEffects`（复合列，见 §5.3） | ✅ |
| `&` | AND（属性全部满足） | `ingredients.strRequiredProps/strForbidProps` | ✅ |
| `],[` | 组间分隔（组内 `,` 属装饰参数） | `battlemoves.vUsConditions` 等 6 列 | ✅ |

> **集合与装饰重叠**：`[-137,0,0],[146,0,0]` 的分隔符 `],[` 同时是装饰的一部分（`[` 括号在装饰层）。解析必须先切分再剥装饰，切分后每段为 `[-137,0,0]`，段内 `,` 属于装饰参数。

---

## §2 实体层

### 2.1 实体格式（固定两种）

```
① Id 形式：        [Namespace:]Id                    → 152、NSE:42、0:38
② 复合键形式：     [Namespace:]GroupId.SubgroupId    → 86.6、NSE:86.6（仅 ItemType 目标）
```

- `GroupId.SubgroupId` 是**游戏内物品 ID**（`nGroupID.nSubgroupID`），**不是** itemtypes 表主键 `id`（数据库自增 1-537）
- SubgroupId 可为多位数：`8.10`、`10.11`、`10.100` ✅
- 命名空间规则见 R16 与 §0.4（缺省=源实体 ns；`0:`=**显式 0 命名空间（game base）**⚠️ 2026-08-02 订正；`NSE:`=显式跨 ns）

### 2.2 例外：用「表主键 id」而非复合键的列

> 说明：`aEffects`/`aResponses` 本身是复合列（见 §5），此处仅收录其**实体格式**的证据。

| 列 | 实体类型 | 证据 | 置信度 |
|----|---------|------|:--:|
| `encounters.nItemsID` | ItemType（**主键 id**） | 249 个唯一值全部命中 itemtypes.id（111/112/125…），与 G.S 集合零交集 | ✅ |
| `conditions.aEffects` 的 `AddItemGround` 参数 | TreasureTable（**池 id**） | 573-576 条件 → 150-153，命中 treasuretable.id（河流/湿地/湖泊/森林搜刮池，语义吻合） | ✅ |
| `conditions.aEffects` 的 `AddSkill` 参数 | ItemType（**复合键**） | 91.4/91.10/91.17-91.19 全部命中（91 组=技能） | ✅ |
| `encounters.aResponses` 右侧 | Encounter（**主键 id**） | 4392/4392 命中 encounters.id | ✅ |
| `encounters.aResponses` 左侧纯数字 | Ingredient（**nID**） | 52=撬棍→49"用撬棍搜刮"、83=易思平板×3→"安装屏幕"、115=剪线钳子→"剪栅栏抄近路"，剧情语义吻合 | ✅ |

### 2.3 目标实体类型总表（58 个已标注引用列）

见 §4 各表明细。引用关系图（Doc 20 附录 B 已完整，此处不重复）。
⚠️ 2026-08-02：新增 **10 个图片/sprite 引用列**（ImageAsset 实体，见 §2.5）：attackmodes.strIMG、camptypes.vImageList、creatures.strImg、datafiles.strImg、dmcplaces.strImg、encounters.strImg、itemtypes.vImageList/vSpriteList/vImageUsage（索引列）、maps.strName。

### 2.5 图片/sprite 资源引用（ImageAsset 实体）⚠️ 2026-08-02 mod 交叉验证新增

图片列（`strIMG`/`strImg`/`vImageList`/`vSpriteList`）也是**引用列**，实体 = 图片资源（ImageAsset），TargetKey = `{FileName}`。

| 属性 | 规则 |
|------|------|
| 实体格式 | `[Namespace:]文件名`，如 `AMode308.png`、`0:AModeSpearSharp.png`、`NSE:ItmDataAddr.png` |
| 命名空间 | 与实体引用相同（R16/§0.4）：无前缀=同 ns（本 mod `img/` 或 game `img/`）、`0:`=game base `img/`、`NSE:`=NSE mod `img/` |
| 资源目录 | 单值列/`vImageList` → 该 ns 的 `img/` 目录；`vSpriteList` 同上（值取 `=` 右侧） |
| 特殊列 | `itemtypes.vImageUsage` / `vEquipSlots` 的 `=x=y` 后缀 → **不是文件名**，是指向 `vImageList` 条目的**序号索引**（0-based），见 §4.20 |

**实测证据**（原版 + NSE mod）：
- 原版图片列全部无前缀（`AMode308.png`、`CreDogman.png`、`ItmDataPDF.png`）
- mod 单值列带前缀：NSEb `attackmodes.strIMG = 0:AModeSpearSharp.png`；NSEoverride `datafiles.strImg = NSE:ItmDataAddr.png`、`creatures.strImg = NSE:CreSquirrel.png`；NSEg `encounters.strImg = 0:ItmEncGive.png`
- mod 列表列带前缀：NSEb `itemtypes.vImageList = ...,0:ItmSpearSlot100.png,...`（混排）；NSEb `vSpriteList = 20=0:CreItmSmBladeL.png`；NSEoverride `vSpriteList = 13=NSEf:CreItmBagDuffelBack.png`（**ns=0 的 mod 引用其他 mod 的图片**）

**索引列规则**（vImageUsage）：
- 6 位 0-based 索引，指向 `vImageList` 条目：`地上空,地上满,手上空,手上满,物品栏空,物品栏满`
- 原版全部索引 < vImageList 长度（0 越界）；mod 可更大（NSEb 长矛 `0,0,10,10,1,1`，vImageList 11 张图）→ 编辑器不做 6 张图上限假设

### 2.4 实体层未确认项

| 项 | 现象 | 说明 | 置信度 |
|----|------|------|:--:|
| aTreasures 未命中复合键 | 175 条（2544-2094-275）未命中原版 itemtypes 的 G.S（如 7.33/7.34/36.5/9.29），NSEoverride 数据（361 条）亦不含 | 推测：游戏版本更新中被移除的旧物品 / 指向未加载的 mod 数据；需在合并视图验证 | 🟡 |

---

## §3 装饰层：Pattern 模板全集

### 3.1 模板清单（按装饰出现位置分类）

| 模板 | 位置 | 形态 | 示例 | 使用列 | 置信度 |
|------|:--:|------|------|--------|:--:|
| `{id}` | — | 无装饰 | `152`、`NSE:42`、`-126` | 绝大多数单/多值列 | ✅ |
| `{id}x{mult}` | 后 | 实体 + 倍率 | `211x1.0`、`-115x1.0` | attackmodes.vAttackerConditions、treasuretable.aTreasures（前两段） | ✅ |
| `{mult}x{id}` | 前 | 数量 + 实体 | `1x11`、`10x12+20x13` | recipes.strTools/strConsumed/strDestroyed | ✅ |
| `{id}={value}` | 后 | 实体 + 赋值 | `38=1`、`151=1`、`0=-100` | creatures.vBaseConditions、factions.dictFactions | ✅ |
| `{value}={id}` | 前 | 值 + 实体 | `100=-414`（部位=状态）、`Off=8.0`、`20=10`（槽位=攻击模式） | itemtypes.aEquipConditions/aPossessConditions/aUseConditions/aAttackModes/aSwitchIDs | ✅ |
| `{slot}={img}` | 前 | 部位 + 图片文件名 | `20=CreItmBagPlasticL.png`、`11=0:CreItmHideLongCoat.png`（⚠️ mod 图片带前缀） | itemtypes.vSpriteList | ✅ |
| `[{id},{p1},{p2}]` | 两侧 | 括号 + 实体 + 2 参数 | `[-137,0,0]` | battlemoves vUsConditions 等 6 列 | ✅ |
| `{id}x{prob}x{qty}` | 后×2 | 实体 + 概率 + 数量 | `86.6x1.0x5-9` | treasuretable.aTreasures | ✅ |
| `{item}x{qty}={enc}x{p1}x{p2}x{p3}x{p4}` | 复合 | 左侧实体+装饰 = 右侧实体+4 参数 | `90.1x1=12x1x0x0x0`、`=1x1x0x0x0`（默认） | encounters.aResponses | ✅（p3/p4 待探索） |

### 3.2 装饰符号语义

| 符号 | 语义 | 置信度 |
|------|------|:--:|
| `-` 前缀 | **三种语义**：① 否定（"必须不拥有"，aConditions/aPreConditions/vUsPreConditions）✅；② **抑制/移除**（aEquip/aPossess/aUseConditions 正=附加负=移除、vIDNext 下回合移除、aEffects 参数 ChainCondition/SetPlayerCondition——三处一致，2026-08-02 用户确认）✅；③ （无其他） | ✅ |
| `[` `]` | 括号条件（BattleMove），组内 `,p1,p2` 为状态参数（数据恒为 `0,0`） | ✅ |
| `x{mult}` 后缀 | 倍率/概率（vAttackerConditions 0.05~1.0；aTreasures 概率 0~1） | ✅ |
| `{mult}x` 前缀 | 数量（recipes 材料个数，如 `10x`=10 个） | ✅ |
| `={value}` 后缀 | 赋值（dictFactions 声望、vBaseConditions 概率） | ✅ |
| `{value}=` 前缀 | 前置值（aEquipConditions 槽位、aAttackModes 槽位、aSwitchIDs 状态名） | ✅ |
| `x{prob}x{qty}` | 双后缀（aTreasures：概率 + 数量范围） | ✅ |

### 3.3 装饰层未确认项

| 项 | 现象 | 说明 | 置信度 |
|----|------|------|:--:|
| `encounters.aResponses` 右侧 p1-p4 | p1=加权随机权重 ✅（用户确认）；p2=回应销毁标记 ✅（=1 销毁，=0 由目标剧情 nRemoveTreasureID 移除）；p3=成功概率（破解场景三组一致，待最终确认）；p4 恒 0（推测保留位） | 4392 段统计 | p3/p4 待探索 |

---

## §4 各表引用列三部分分解

> 每列 = 集合（分隔符）｜实体（TargetKey）｜装饰（Pattern）。标注 `[未标]` = 语义确认但代码未标 ReferenceField；`[误标]` = 代码标注与真实语义不符。

### 1. AttackMode（attackmodes）

| 列 | 集合 | 实体 | 装饰 | 真实样本 | 置信度 |
|----|:--:|------|------|---------|:--:|
| `strChargeProfiles` | `,`（多为单值） | ChargeProfile `{Id}` | `{id}` | `10`、`22` | ✅ |
| `vAttackerConditions` | `,` | Condition `{Id}` | `{id}x{mult}` | `-115x1.0,...,115x1.0` | ✅ |
| `strIMG` | null | **ImageAsset** `{FileName}` | `{id}` | `AMode308.png`、`AModePunch.png`；⚠️ mod `0:AModeSpearSharp.png` | ✅ |

### 2. BattleMove（battlemoves）

| 列 | 集合 | 实体 | 装饰 | 真实样本 | 置信度 |
|----|:--:|------|------|---------|:--:|
| `vUsConditions` 等 6 列 | `],[` | Condition `{Id}` | `[{id},{p1},{p2}]`（参数恒 0,0） | `[-137,0,0],[146,0,0]` | ✅ |
| `vUsPreConditions` / `vThemPreConditions` | `,` | Condition `{Id}` | `{id}`（`-` 否定） | `-143,-144,...,151` | ✅ |
| `strID` [未标] | null | ItemType `{G}.{S}` | `{id}` | `90.35` | ✅ |

### 3. CampType（camptypes）

| 列 | 集合 | 实体 | 装饰 | 样本 | 置信度 |
|----|:--:|------|------|------|:--:|
| `nTreasureID` | null | TreasureTable `{Id}` | `{id}` | `3` | ✅ |
| `vImageList` | null | **ImageAsset** `{FileName}` | `{id}` | `ItmScavengeGrass01.png`、`ItmScavengeApt01.png` | ✅ |

### 4. ChargeProfile（chargeprofiles）

| 列 | 集合 | 实体 | 装饰 | 样本 | 置信度 |
|----|:--:|------|------|------|:--:|
| `strItemID` [未标] | null | ItemType `{G}.{S}` | `{id}` | `10.3`、`10.10` | ✅ |

### 5. Condition（conditions）

| 列 | 集合 | 实体 | 装饰 | 样本 | 置信度 |
|----|:--:|------|------|------|:--:|
| `vIDNext` | `,` | Condition `{Id}` | `{id}`（**每回合/阶段转移列表**；**负号 = 下回合移除该状态**（若有），概率取 vChanceNext——用户确认 2026-08-02） | `116`、`135,623`、`-198,-199,...`（cond 547 咀嚼熊根：按概率移除肺炎/咳嗽/头痛/蓝腐病系列） | ✅ |
| `aThresholds` [未标] | `;` | Condition `{Id}`（**右侧**） | `{value}={id}`（左侧=等级 1~5） | `1=795;2=794;3=763` | ✅ |

### 6. ContainerType（containertypes）

无引用列。

### 7. Creature（creatures）

| 列 | 集合 | 实体 | 装饰 | 样本 | 置信度 |
|----|:--:|------|------|------|:--:|
| `vEncounterIDs` [误标] | `,` | **Encounter** `{Id}`（代码误标 Condition） | `{id}` | `1328,1362,1345` | ✅ |
| `nTreasureID` | null | TreasureTable `{Id}` | `{id}` | — | ✅ |
| `nFaction` | null | Faction `{Id}` | `{id}` | — | ✅ |
| `vAttackModes` | `,` | AttackMode `{Id}` | `{id}` | `17` | ✅ |
| `vBaseConditions` | `,` | Condition `{Id}` | `{id}={value}`（value=概率 0~1） | `151=1,210=1`、`35=0.25` | ✅ |
| `nCorpseID` | null | TreasureTable `{Id}` | `{id}` | — | ✅ |
| `strImg` | null | **ImageAsset** `{FileName}` | `{id}` | `CreDogman.png`、`CreHuman.png`；⚠️ NSE `0:CreHuman.png`、NSEoverride `NSE:CreSquirrel.png` | ✅ |

### 8. CreatureSource（creaturesources）

| 列 | 集合 | 实体 | 装饰 | 置信度 |
|----|:--:|------|------|:--:|
| `nCreatureID` | null | Creature `{Id}` | `{id}` | ✅ |

### 9. DataFile（datafiles）

| 列 | 集合 | 实体 | 装饰 | 样本 | 置信度 |
|----|:--:|------|------|------|:--:|
| `strImg` | null | **ImageAsset** `{FileName}` | `{id}` | `ItmDataPDF.png`、`ItmDataTXT.png`；⚠️ mod `NSE:ItmDataAddr.png` | ✅ |

### 10. DmcPlace（dmcplaces）

| 列 | 集合 | 实体 | 装饰 | 置信度 |
|----|:--:|------|------|:--:|
| `nEncounterID` | null | Encounter `{Id}` | `{id}` | ✅ |
| `strImg` | null | **ImageAsset** `{FileName}` | `{id}`（无扩展名按钮名：`btn_dmc_diner`） | ✅ |

### 11. Encounter（encounters）

| 列 | 集合 | 实体 | 装饰 | 样本 | 置信度 |
|----|:--:|------|------|------|:--:|
| `nTreasureID` / `nRemoveTreasureID` | null | TreasureTable `{Id}` | `{id}` | — | ✅ |
| `aConditions` | `,` | Condition `{Id}` | `{id}`（`-` 否定） | `-126,-336,410` | ✅ |
| `aPreConditions` | `,` | Condition `{Id}` | `{id}`（`-` 否定） | `-114,412` | ✅ |
| `nItemsID` [误标] | null | ItemType **`{Id}`（主键）**（代码误配复合键） | `{id}` | `111`、`125` | ✅ |
| `nCreatureID` | null | Creature `{Id}` | `{id}` | — | ✅ |
| `vAccidents` | `,` | Encounter `{Id}` | `{id}` | `100,102,103,104,1724` | ✅ |
| `vLoot` | null | TreasureTable `{Id}` | `{id}` | `40` | ✅ |
| `aResponses` | `,` | 双目标+右侧实体（见 §5.1） | 复合模板 | `90.1x1=12x1x0x0x0`、`52x1=49x1x1x0x0`、`1.0x1=74x1x0x0x0` | ✅（p3/p4 待探索） |
| `strImg` | null | **ImageAsset** `{FileName}` | `{id}` | `EncBlank.png`、`EncCryoFacility.png`；⚠️ mod `0:ItmEncGive.png`、`NSE:EncDMClockers.png` | ✅ |

### 12. EncounterTrigger（encountertriggers）

| 列 | 集合 | 实体 | 装饰 | 置信度 |
|----|:--:|------|------|:--:|
| `nEncounterID` | null | Encounter `{Id}` | `{id}` | ✅ |
| `aHexTypes` | `,` | HexType `{Id}` | `{id}` | ✅ |

### 13. Faction（factions）

| 列 | 集合 | 实体 | 装饰 | 样本 | 置信度 |
|----|:--:|------|------|------|:--:|
| `dictFactions` | `,` | Faction `{Id}`（**左侧**） | `{id}={value}`（右侧=好感度/声望，**负=敌对**：`-100`=死敌、`-10`=敌对、`-5/-3`=轻微敌对、`0`=中立、`1/2/5`=友好、`10`=亲近） | `0=-100,1=-100,...,14=1` | ✅（226 键值对统计；faction 10/11 含脏数据 `9=-100=0`） |

### 14. ForbiddenHex（forbiddenhexes）

无引用列。

### 15. GameVar（gamevars）

无引用列。

### 16. Headline（headlines）

无引用列。

### 17. HexType（hextypes）

| 列 | 集合 | 实体 | 装饰 | 样本 | 置信度 |
|----|:--:|------|------|------|:--:|
| `nTreasureID` / `nScavengeInitialID` / `nScavengeItemsIDPerHour` | null | TreasureTable `{Id}` | `{id}` | — | ✅ |
| `nDefaultCampID` | null | CampType `{Id}` | `{id}` | — | ✅ |
| `vCondIDs` | `,` | Condition `{Id}` | `{id}` | `457`、`618` | ✅ |
| `nCampItems` [未标] | null | CampType `{Id}` | `{id}`（该地块默认营地类型；绝大多数=5 烧毁公寓，0=无营地（水域等 7 类）） | `0`、`2`（冷冻休眠机构）、`4`（棚户区）、`5` | ✅ |

### 18. Ingredient（ingredients）

| 列 | 集合 | 实体 | 装饰 | 样本 | 置信度 |
|----|:--:|------|------|------|:--:|
| `strRequiredProps` | `&` | ItemProp `{Id}` | `{id}` | `1&13&16&28&47` | ✅ |
| `strForbidProps` | `&` | ItemProp `{Id}` | `{id}` | `15&16` | ✅ |

### 19. ItemProp（itemprops）

无引用列（被 ItemType.vProperties / Ingredient 反向引用）。

### 20. ItemType（itemtypes）⭐ 引用最多的表

| 列 | 集合 | 实体 | 装饰 | 样本 | 置信度 |
|----|:--:|------|------|------|:--:|
| `nCondID` | null | Condition `{Id}` | `{id}` | — | ✅ |
| `vDegradeTreasureIDs` | `,` | TreasureTable `{Id}` | `{id}`（2 元素：**位置1=装备损耗产物**（对应 fEquipDegradePerHour：衣物→破烂）、**位置2=使用损耗产物**（对应 fDegradePerUse：武器→零件、瓶子→低品质版）；默认 `3,3`=无产物；消耗品无产物） | `11,3`、`758,3`（鞋子→破烂的鞋子）、`3,31`（医疗工具使用→零件）、`3,370`（瓶子使用→低品质玻璃瓶） | ✅ |
| `aEquipConditions` / `aPossessConditions` / `aUseConditions` | `,` | Condition `{Id}`（**右侧**） | `{value}={id}`（**左侧槽位三者不同**：aEquip=装备部位 2/3/4/5/6/7/8/11/17/20/21/23/100-116/207，见附录 A；aPossess=拥有槽 200=拥有/207/208=营地设施/213-232=技能/空=无条件；aUse=使用槽 211=直接使用 + 100-115=部位外用）；**正=附加状态，负=抑制/移除状态**（2026-08-02 确认） | `100=-414,101=-414`、`11=19,11=595,11=597`、`2=21,2=-210`（鞋）、`211=-40,-41,...`（医疗工具抑制全部疾病）、`214=425`（技能） | ✅（213-232 系别 🟡） |
| `vProperties` | `,` | ItemProp `{Id}` | `{id}` | `16,46,48` | ✅ |
| `aContentIDs` | `,` | ContainerType `{Id}` | `{id}` | — | ✅ |
| `nFormatID` | null | ContainerType `{Id}` | `{id}` | — | ✅ |
| `nTreasureID` / `nComponentID` | null | TreasureTable `{Id}` | `{id}` | — | ✅ |
| `strChargeProfiles` | `,`（多为单值） | ChargeProfile `{Id}` | `{id}` | `22` | ✅ |
| `aAttackModes` | `,` | AttackMode `{Id}`（**右侧**） | `{value}={id}`（左侧=槽位 20/21/17…） | `20=10,21=10`、`17=16` | ✅ |
| `aSwitchIDs` | `,` | ItemType `{G}.{S}`（**右侧**） | `{value}={id}`（左侧=状态名，⚠️ 原版 On/Off/Open/Close，mod 为自由文本如 `Hood Off`/`Shrink Back`；目标可带 `0:` 前缀如 `Hood Off=0:78.7`） | `Off=8.0`、`Close=8.2,On=8.3` | ✅ |
| `vImageList` | `,` | **ImageAsset** `{FileName}` | `{id}`（条目可带前缀） | `ItmStick.png,ItmStickHeld.png`；⚠️ mod `ItmSpearSharpStoredSling.png,...,0:ItmSpearSlot100.png,...` | ✅ |
| `vSpriteList` | `,` | **ImageAsset** `{FileName}`（**右侧**） | `{slot}={img}`（左侧=部位：2/3=脚、4=下身、11=上身、13/14=肩背、17=头部、20/21=手、22=背，见附录 A） | `20=CreItmBagPlasticL.png,21=CreItmBagPlasticR.png,22=CreItmBagPlasticBack.png`；⚠️ mod `20=0:CreItmSmBladeL.png`、NSEoverride `13=NSEf:CreItmBagDuffelBack.png` | ✅ |
| `vImageUsage` [特殊] | — | **索引列**（指向 vImageList 条目序号，非文件名） | 6 位 0-based 索引：`地上空,地上满,手上空,手上满,物品栏空,物品栏满`（⚠️ mod 索引可≥10，图列表超 6 张） | `1,0,0,0,0,0`（仅地上）、`1,1,0,0,1,1`（典型） | ✅ |

### 21. Map（maps）

| 列 | 集合 | 实体 | 装饰 | 样本 | 置信度 |
|----|:--:|------|------|------|:--:|
| `strDef` | `,` | HexType `{Id}`（数字序列） | `{id}` | `5,3,3,4,5,...` | ✅ |
| `strName` [特殊] | null | **ImageAsset** `{FileName}`（或内部网格名） | `{id}` | `MapMiniMichigan.png`（小地图图片）、`Excel50x100`（内部网格地图标识，非图片） | ✅ |

### 22. Recipe（recipes）

| 列 | 集合 | 实体 | 装饰 | 样本 | 置信度 |
|----|:--:|------|------|------|:--:|
| `strTools` / `strConsumed` / `strDestroyed` | `+` | Ingredient `{Id}`（**右侧**） | `{mult}x{id}`（左侧=数量） | `1x11+1x14+1x22`、`10x12+20x13` | ✅ |
| `nTreasureID` / `nTempTreasureID` | null | TreasureTable `{Id}` | `{id}` | — | ✅ |
| `nHiddenID` | null | Recipe `{Id}` | `{id}` | — | ✅ |
| `vAlsoTry` | `,` | Recipe `{Id}` | `{id}` | `79,80,81` | ✅ |

### 23. TreasureTable（treasuretable）⭐ 最复杂

| 列 | 集合 | 实体 | 装饰 | 样本 | 置信度 |
|----|:--:|------|------|------|:--:|
| `aTreasures` | `,`(AND) + `\|`(OR) | **双目标**：`G.S`→ItemType、纯数字→TreasureTable（嵌套，SecondaryTarget） | `{id}x{prob}x{qty}`：② 概率 0~1（1.0×920、1×724、0.0141…）；③ 数量整数或 min-max（1-1×1368、3-5、80-80），**省略=数量 1**（45 条，全为 36 组数据文件，概率恒 1/59=均匀分布——用户确认 2026-08-02） | `86.6x1.0x5-9`、`35.1x0.1x1-1\|35.2x0.1x1-1\|...`、`36.6x0.01694915254` | ✅ |

> **OR 组语义**：`A,B\|C\|D` = A（必出）+ {B 或 C 或 D} 三选一（各 0.0625 概率）；`|` 组内各段共享概率分配。

### 24. BarterHex（barterhexes）

| 列 | 集合 | 实体 | 装饰 | 置信度 |
|----|:--:|------|------|:--:|
| `nRestockTreasureID` [未标] | null | TreasureTable `{Id}` | `{id}` | ✅ |

### 25. 无引用列表

ForbiddenHex / GameVar / Headline / ContainerType / ItemProp（Map 的 strDef 是 HexType 引用列，见 §21）。⚠️ 2026-08-02 更新：DataFile 的 `strImg` 为 ImageAsset 引用（见 §9），不再在无引用列表。

---

## §5 超出标准模型的复合列

标准模型假设「一列 = 同一种实体 + 统一装饰」。以下列不满足，需特殊处理：

### 5.1 `encounters.aResponses` — 每段含 **2~3 个实体**

```
{itemRef}x{qty} = {encId}x{p1}x{p2}x{p3}x{p4}
  ├─ 左侧实体（双目标）：
  │     G.S 复合键 → ItemType（90.28 前进按钮、91.8 技能、11.3 瓶子、19.1 探测仪…）
  │     纯数字     → Ingredient（nID：47=透镜、52=撬棍、49=手持光源、78=夜视仪、
  │                   83=易思平板、115=剪线钳子 — 经剧情语义交叉验证 ✅）
  │     「=」前省略 → 默认回应（无条件选项）
  ├─ 装饰① x{qty}：数量（多数 x1；特殊 `83x3`=3 个平板、`86.0x5`=5 个）
  ├─ "=" 分隔左右
  └─ 右侧实体：Encounter（主键 id，4392/4392 命中 ✅）
       装饰② x{p1}x{p2}x{p3}x{p4}：
         p1 = 加权随机权重（见下）✅
         p2 = 回应销毁标记 ✅（=1 销毁；=0 由目标剧情 nRemoveTreasureID 移除）
         p3 = 成功概率（破解场景证据，待探索）
         p4 = 恒 0（推测保留位，待探索）
```

- 集合分隔符 `,`；左侧可用 `+` 连接多物品 = **AND（同时需要全部）**（用户确认；`91.8x1+91.3x1=22` 需同时拥有两个技能）
- **左侧实体双目标**（ItemType G.S / Ingredient nID）与 aTreasures 类似，解析器需支持

**p1 = 加权随机权重**（用户确认 ✅）：同一条件的多个段概率和 ≈ 1：
`=1926x0.2,=1927x0.8`（enc#1914 抄近路随机进薄弱/强壮点）、`=211x0.5,=205x0.5`（enc#201）、`=212x0.75,=213x0.25`（enc#207）、`0.5+0.25+0.25`（enc#210）、`0.7+0.3`（enc#1445）、`0.2+0.8`（enc#1563）；enc#774 拿武器 8+ 段（0.2/0.3/0.072×n）。p1=1 的段=独立必显选项；p1<1 的段=随机组权重

**p2 = 是否由回应本身销毁左侧物品**（用户思路验证 ✅ 2026-08-02）：
- **=1 销毁**：目标剧情**从不**用 nRemoveTreasureID 移除（32/32 段目标移除池=3）——物品由回应直接销毁；部分物品通过自身 vDegradeTreasureIDs 返回残留（94.0 医疗工具 → 池31 小型零件，实现"销毁但不白消耗"）
- **=0 不销毁**：物品保留；若剧情需要消耗，由**目标 encounter 的 nRemoveTreasureID** 精确移除——54/4360 段有此需求，如 `1.0x1=74`（监控录像→enc#74 移除池68 安保录像）、`89.0x1=154`（手环→移除池123）、`88.0x1=229`（护身符换步枪→移除池133 项链）、`86.0x5+46.0x1=1037`（移除池362=86.0×5，**只移除 5 个物件、刀具保留**）

**Q7 数量语义**（用户确认 ✅）：左侧 `x{qty}` **既是拥有要求（须有 N 个），也是消耗数量**——p2=1 时销毁 N 个；p2=0 时由目标剧情 nRemoveTreasureID 移除 N 个（池 362"5个小型物件"=86.0×5 精确对应段内数量）

**p3 成功概率证据**（破解场景三组一致模式）：
- `8.7x1=1088x1x0x0.5x0`（平板运行 iSl-AK，50%）+ `8.7x1=1082x1x0x0x0`（→1082"电量不足的破解目标"=失败兜底）
- `8.15x1=1091x1x0x1x0`（手机运行 Br1nG，必成功）+ `8.15x1=1082x1x0x0x0`
- `35.1x1=1085x1x0x0.5x0`（平板软件 50%）+ `35.1x1=1095x1x0x0x0`
- `49x1=930x1x1x1x0`（手持光源，p2=1 且 p3=1）→ 🟡（p3=0 段=失败兜底；p3=1=必成功）

### 5.2 `treasuretable.aTreasures` — 实体**双目标** + 双后缀装饰

```
{itemId}x{prob}x{qty}
  ├─ 实体：G.S → ItemType（2094/2544 命中）；纯数字 → TreasureTable 嵌套池（275/2544 命中）
  ├─ 装饰① x{prob}：概率 0~1
  └─ 装饰② x{qty}：数量（整数或 min-max；45 条省略=1）
```

- 集合：`,`（AND）+ `|`（OR）混合
- 175 条 G.S 未命中原版 itemtypes（7.33/36.5 等，见 §2.4）

### 5.3 `conditions.aEffects` — 效果名 + 参数列表（非标准引用列）

```
{effectName}={p1},{p2},{p3};   （集合分隔符 `;`）
  ├─ 效果名：15 种固定字符串（附录 B），非实体
  └─ 参数：按效果名解释（全量样本）——
       ArmorWound={部位},{p2},{p3}              e.g. ArmorWound=100,0.02,0.01
       SetImmunity={condId},...                 免疫列表    e.g. SetImmunity=40,48,198,303
       ChainCondition={±condId},...             链式（-620=移除620+添加731）✅
       RemoveTrait={itemtype G.S}               移除特质    e.g. RemoveTrait=96.0
       AddSkill / RemoveSkill={itemtype G.S}    技能        e.g. AddSkill=91.10
       ChangeFactionRep={±数值}                 对当前派系  e.g. ChangeFactionRep=-5
       ChangeGlobalFactionRep={factionId},{±数值},{flag}  e.g. 1,200,1
       AddItemGround={treasuretable id},{qty},{x},{y}     e.g. 150,0,0,0（已验证=池 id）
       SetWaypoint={x},{y},{encounterId},{1}    e.g. 43,118,1730,1
       SetPlayerCondition={±condId}             （-633=移除633）✅
       Confiscate={group},{0/1},{0/1}           没收物品组  e.g. 98,0,1
       Despawn={0/1} / PassTime={小时},{0} / EndGame={encounterId}
```

> 该列整体不是标准引用列（效果名非实体），但其参数子集含实体引用（SetImmunity/ChainCondition/SetPlayerCondition→Condition、RemoveTrait/AddSkill/RemoveSkill→ItemType G.S、AddItemGround→TreasureTable、ChangeGlobalFactionRep→Faction、SetWaypoint/EndGame→Encounter）。**参数中的负号同样是"移除"语义**（ChainCondition=-620、SetPlayerCondition=-633）。

---

## 附录 A：装备槽位/部位编码表（aEquipConditions/aAttackModes 装饰左侧的取值字典）

| 编码 | 部位 | 佐证 |
|------|------|------|
| 2 | 左脚 | vSpriteList `2=CreItmWorkBootL.png` ✅ |
| 3 | 右脚 | vSpriteList `3=CreItmWorkBootR.png` ✅ |
| 4 | 下身(Legs) | vSpriteList `4=CreItmBluejeans.png` ✅ |
| 5, 6 | 左/右腕（手链） | 手链物品 `5=0=0,6=0=0,20,21` ✅ |
| 7, 8 | 未知 | aEquipConditions 出现 2-3 次 🟡 |
| 11 | 上身(Torso) | vSpriteList `11=CreItmTShirtBrown.png` ✅ |
| 13 | 左肩/背部 | vSpriteList `13=CreItmBagDuffelBack.png` ✅ |
| 14 | 右肩 | Doc 20 ✅ |
| 17 | 头部/面部 | Doc 20 ✅ |
| 20 | 左手 | vSpriteList `20=CreItmBagPlasticL.png` ✅ |
| 21 | 右手 | 同上 ✅ |
| 22 | 背部 | vSpriteList `22=...Back.png` ✅ |
| 23 | 颈部（项链） | 项链 `23=0=0,21,20` ✅ |
| 100-116 | 身体细分部位（护甲/外用） | aEquipConditions 护甲条目 `100=-414,101=-414,...` ✅ |
| 202 | 界面容量槽 | 93.0 界面容量 ✅ |
| 200 | 拥有槽（aPossessConditions） | 人肉 `200=458` 触发贩卖人肉事件 ✅ |
| 208 | 营地设施槽（aPossessConditions） | 营火 `208=70`、睡袋 `208=107`、庇护棚 `208=108` ✅ |
| 211 | 使用槽（aUseConditions） | 医疗工具 `211=-40,...`（使用治疗）、人肉 `211=48,...` ✅ |
| 213-232 | 特质组槽（aPossessConditions） | 特质物品（96.x）把 **213-232 全部 20 个槽位重复列出**同一状态（96.0 近视：`213=24,...,232=24`）；96.8 例外只列 213 ✅ |
| 207 | **大型/特殊物品类别槽**：aEquip 载具 3.0-3.4/86.3 → 74 推车、477 无法奔跑、488 可以弃车；aPossess 疫苗容器（药水瓶/桶）→ 693（与 20/21/200/208 并列，**非 207 专属**——见 aPossessConditions 槽位语义） | ✅ |
| 214 | 能力/技能槽 | 91.x 能力系列 ✅ |
| -1 | 不可装备（对话/系统/战斗按钮/特质） | 90.x/96.x/93.0 ✅ |

### vEquipSlots 的 `=x=y` 后缀（图片索引，2026-08-02 确认）

- 格式：`{槽位}[={x}={y}]`，逗号分隔多槽位；**带 `=x=y` = 该槽可装备并指定显示图索引**；不带 =（如 `21,20`）= 可装备用默认图
- **x = vImageList（人像图）索引，0-based**，95/95 物品全部有效且图名与槽位/状态精确吻合：
  - `100=1=1` → vImageList[1]=`ItmDirtyRagsSlot100.png`（部位 100 包扎图）
  - `110=2=2` → vImageList[2]=`ItmSplintSlot110.png`；`100=2=2` → `ItmSpearSlot100.png`
  - `21=1=1` → vImageList[1]=`ItmStickHeld.png`（手持图）；`14=0=0` → [0]=背负图、`21=2=2` → [2]=手持图（3 张：Worn/Stored/Held）
- **x 与 y 恒相等**（95/95），无法从数据区分二者；y **不是** vSpriteList 段索引（反例：鞋子 `3=0=0` 右脚槽 y=0 指向 vSpriteList 段 0=左脚图）；vSpriteList 为 `槽位=图名` 格式，大地图小人按槽位直接匹配、不走索引
- **bMirrored（镜像）**：6 个 bMirrored=1 的物品**全部为鞋子**（50.0/50.2/50.4/50.6/50.7/50.9），且每款鞋都有成对的 bMirrored=0 变体（50.0↔50.1、50.2↔50.3…）——镜像版与普通版 sprite 完全相同（`2=L,3=R`）；推测 bMirrored=1 时右脚槽**镜像左脚图**显示，`3=0=0` 的 y=0 "反例"由镜像机制消化（用户判断 🟡）
- 特殊槽位 `-1`/`214`/`202` 均无 `=x=y` 后缀

### aPossessConditions 槽位 = 物品"拥有类别"（2026-08-02 确认）

`{槽位}=状态` 列表标识物品属于哪些拥有类别；拥有该物品时**每个列出的槽位对应状态都生效**（同一状态可多槽位重复）。组合模式全量统计（8 种）：

| 槽位组合 | 物品类别 | 代表物品 | 状态 |
|---------|---------|---------|------|
| `214` | 能力 | 91.x 能力系列（x20） | 425 精通黑客、53 精通医学… |
| （空） | 无条件拥有 | 手链/灰烬/V-MADS/笔记本（x17） | 438 携带追踪设备… |
| `200` | 普通拥有（背包） | 人肉 101.x（x10） | 458 触发贩卖人肉事件 |
| `200+208` | 营地设施（拥有+放置双通道） | 营火/睡袋/陷阱（x7） | 25 营火取暖、107 使用睡袋 |
| `213~232` 全部 | 特质组（20 槽重复同一状态） | 96.x 特质（x6） | 24 近视、58 新陈代谢快速… |
| `208` | 营地/放置 | 庇护棚/营地设施 26.x（x5） | 108 使用油布帐篷 |
| `20+21+200+207+208` | 疫苗容器（5 槽全列） | 药水瓶/桶/空白（x4） | 693 携带着蓝腐疫苗或样本 |
| `213` | 特质（单槽例外） | 96.8 夜视增强 | 360 视觉增益: 夜视 |

- **693 不是 207 专属**：疫苗容器在 20/21/200/207/208 全部触发 693；用户补充的"NPC 拖车携带疫苗容器"是 693 的剧情应用场景（一次性特殊情节），数据侧 693 是容器常规状态
- **207 = 大型/特殊物品类别**：载具（aEquip 推车/无法奔跑/可以弃车）+ 大容器（aPossess）
- **213-232 = 特质组**：特质物品重复列出全部槽位（96.8 只列 213 的例外原因待探索）

## 附录 B：aEffects 效果名全集（15 种，conditions.xml 全量统计）

| 效果名 | 次数 | 参数格式 | 参数实体 | 含义 |
|--------|:--:|---------|---------|------|
| `ArmorWound` | 98 | `{部位},{p2},{p3}` | 部位=槽位码 | 护甲伤口（p2/p3 为 0.02/-1 级数值）🟡 |
| `SetImmunity` | 18 | `{condId}` | Condition 🟡 | 获得免疫 |
| `ChangeFactionRep` | 10 | `{factionId},{val}` | Faction 🟡 | 改变阵营声望 |
| `AddSkill` | 6 | `{itemId}` | ItemType 复合键（5/5 ✅） | 获得技能（91 组） |
| `ChainCondition` | 5 | — | — | 连锁状态 |
| `RemoveTrait` | 5 | `{traitId}` | 🟡 | 移除特质 |
| `RemoveSkill` | 4 | — | — | 移除技能 |
| `AddItemGround` | 4 | `{poolId},{qty},{x},{y}` | TreasureTable（4/4 ✅） | 地面生成搜刮池 |
| `ChangeGlobalFactionRep` | 4 | — | — | 全局阵营声望 |
| `SetWaypoint` | 4 | — | — | 设置路标 |
| `Confiscate` | 4 | — | — | 没收物品 |
| `SetPlayerCondition` | 3 | — | — | 设置玩家状态 |
| `Despawn` | 2 | — | — | 消失 |
| `PassTime` | 1 | — | — | 时间流逝 |
| `EndGame` | 1 | — | — | 游戏结束 |

## 附录 C：aSwitchIDs 状态名（原版 4 种，⚠️ mod 为自由文本）

原版 `On` / `Off` / `Open` / `Close`（4 种全量）；**mod 扩展为自由文本**（NSE：`Hood Off`/`Hood On`/`Shrink Back` 等，见 38 附录 §B）。解析器不应把左侧限定为固定枚举。

## 附录 D：未确认项汇总（含示例数据，供逐项判断）

| 层 | # | 字段 | 未确认点 | 置信度 |
|----|---|------|---------|:--:|
| 实体 | 1 | aTreasures 未命中复合键 | 107 个唯一 G.S 值不在原版+NSE itemtypes，集中在 7.x/9.29-9.30/12.x/36.x 四组 | 🟡 |
| 实体 | 2 | ~~槽位 207（及 213-232）~~ | ✅ 已解决：207=大型/特殊物品类别槽（载具装备 + 大容器）；213-232=特质组（特质物品重复列全部槽位）；693=疫苗容器常规状态（5 槽全挂，非 207 专属；"NPC 拖车"是剧情应用） | ✅ |
| 实体 | 3 | ~~vIDNext 负值~~ | ✅ 已解决：负=下回合移除该状态（概率 vChanceNext）；vChanceNext 与 vIDNext 逐位对应（16 条等长样本全部合理：`417,416`+`1,0.05`、`40,48`+`0.05,0.2`），非等长时单值=整体触发概率（`41,65,66`+`1`=恶化+腹泻+呕吐同时） | ✅ |
| 装饰 | 4 | aResponses 右侧 p3/p4（待探索） | p3=成功概率（破解场景 3 组一致证据：`8.7→1088x0.5/1082x0`，证据强但未最终确认）；p4 恒 0（4392/4392，推测保留位） | 🟡 |
| 装饰 | 5 | aEffects 个别参数（待探索） | SetWaypoint=43,118,1730,**1** / Confiscate=98,**0**,**1** / PassTime=24,**0** / ChangeGlobalFactionRep=1,200,**1** 的尾部参数语义 | 🟡 |
| 集合 | 6 | ~~aTreasures 省略第三段~~ | ✅ 已解决：省略=数量默认 1（45 条全为 36 组数据文件，概率恒 1/59） | ✅ |
| 其他 | 7 | ~~recipes.nReverse 三值~~ | ✅ 已解决：0=不可拆、1=可拆回材料（简单组装物）、2=可拆回组件（复合装备） | ✅ |

---

### 项1：aTreasures 未命中复合键（107 个唯一值 → 集中在 4 组）

| 未命中 G.S | 所在池（id + 名称） | 条目原文 | 推断 |
|------|------|------|------|
| `7.33`-`7.36` | 124（Stoat absent treasure） | `7.33x1x1-1,7.34x1x1-1,...` | 池名含 "absent"（缺席）→ 疑似已移除物品 |
| `9.29` / `9.30` | 160（空调制法）/ 162（低温照明制法） | `9.29x1x1-1` | 9 组=纸条；疑似制作配方纸条，原版 itemtypes 仅 9.0 |
| `12.2`-`12.14` | 518/520/522/524/526/736-739/755（营地类池） | `12.3x0.25x1-1,...` | 12 组=营地设施，原版仅 12.0/12.1 |
| `36.1`-`36.21` | 447/528/529（数据文件类池） | `36.6x0.0169...` | 36 组=电子数据文件，原版无 |

> 对照：itemtypes 实际只有 7.0（旧报纸）/9.0（纸条）/12.0-12.1（露营地等）/36 组缺失——**4 组物品在原版 XML 中缺失**。
> ✅ **2026-08-02 mod 交叉验证升级**：这 107 个唯一 G.S 在 NSE 全部 8 组 mod 数据（NSEg/NSEb/NSEf/NSEa/NSE/NSEoverride/NSEtT/用户 mod，合计 947 个 G.S 键）中**也全部不存在** → 基本证实是**游戏版本移除的旧物品**，而非指向 mod 数据（详见 38 附录 §C）。

### 项2：~~槽位 207（及 213-232）~~ 已解决（2026-08-02）

**结论：207 = 大型/特殊物品类别槽；213-232 = 特质组（重复列槽）；693 = 疫苗容器常规状态**

| 物品 | 字段 | 段 | 状态身份 |
|------|------|------|---------|
| 3.0-3.4 载具、86.3 大型物件 | aEquipConditions | `207=74,207=477,207=488` | 74=推车、477=无法奔跑、488=可以弃车（装备载具/大件） |
| 44.0/44.1 药水瓶、45.0 桶、0.0 空白 | aPossessConditions | `20=693,21=693,200=693,207=693,208=693` | 693=携带着蓝腐疫苗或样本——**同时挂在 5 个槽位**，207 非专属；NPC 拖车携带疫苗容器是 693 的剧情应用场景（一次性特殊情节，用户补充） |
| 96.x 特质 | aPossessConditions | `213=24,214=24,...,232=24` | 特质物品把 213-232 **全部 20 槽重复列出**同一状态（96.8 例外只列 213，原因待探索） |

### 项3：~~vIDNext 负值~~ 已解决（2026-08-02 用户确认）

**结论：vIDNext = 每回合/阶段转移列表；负号 = 下回合移除该状态（若有），概率由 vChanceNext 决定**

| cond | vIDNext | vChanceNext | 解释 |
|------|---------|-------------|------|
| 547 咀嚼熊根（药草） | `-198,-199,-200,-202,-203,-294,-295,-296,-297` | `0.5,.025,.0125,1,0.5,0.4,0.25,0.0125,0.007` | **逐位**：按概率移除疾病——咳嗽 100%、肺炎1 50%、头痛 40%、蓝腐病1 25%…（治愈药草）✅ |
| 818 圣詹姆斯停车场过期 | `-827` | `1` | 移除"在圣詹姆斯停车场租车"状态 ✅ |
| 40 霍乱阶段1 | `41,65,66` | `1` | 非等长：单值=整体触发——恶化霍乱2+腹泻+呕吐同时（100%） |
| 198 肺炎1 | `199,202` | `0.25` | 25% 触发恶化肺炎2+咳嗽 |
| 417 伤口插着尖锐物 | `417,416` | `1,0.05` | **逐位**：保持 417（100%）+ 敲入 416（5%） |
| 429 喝了脏水 | `40,48` | `0.05,0.2` | **逐位**：霍乱1（5%）+ 肠胃炎（20%）独立判定 |

> 等长样本（16 条）逐位概率全部语义合理；非等长（26 条）单值=整体触发概率（列表内状态同时发生）。

### 项4：aResponses 右侧 p1-p4（详见 §5.1）

| 参数 | 分布 | 示例 | 结论 |
|------|------|------|------|
| p1 | 1×4132、0.5、0.072、0.25、0.75、0.2 | `=1926x0.2,=1927x0.8`（和=1） | **加权随机权重** ✅（用户确认）；p1=1=独立必显 |
| p2 | 0×4353、1×39 | `52x1=49x1x1x0x0`（撬棍）、`94.0x1=222x1x1x0x0`（医疗工具） | **=1 回应销毁物品** ✅；=0 不销毁（目标剧情 nRemoveTreasureID 移除：`1.0x1=74`→移除池68、`89.0x1=154`→移除池123；32/32 的 p2=1 段目标无移除池） |
| p3 | 0×4351、1×32、0.5、0.1 | `8.7x1=1088x1x0x0.5x0`（破解 50%）+ `8.7x1=1082x1x0x0x0`（失败兜底） | 成功概率（破解场景 3 组一致）待探索 |
| p4 | 0×4392 | 全部 0 | 保留位（推测）待探索 |

### 项5：aEffects 个别参数（待探索，效果名全集见附录 B）

| 效果名 | 参数样本 | 已确认 | 待探索 |
|--------|---------|--------|--------|
| ArmorWound | `100,0.02,0.01`（部位,0.02,0.01） | 部位=槽位码 | p2/p3 数值语义 |
| SetImmunity | `40,48,198,303`（免疫霍乱1/肠胃炎/肺炎1/肝炎1） | 参数=Condition 列表 ✅ | — |
| ChainCondition | `-620,731`（移除620+添加731） | 负号=移除 ✅ | — |
| ChangeFactionRep | `-5` / `1` | 参数=数值（对当前派系） | 无 faction id？ |
| ChangeGlobalFactionRep | `1,200,1`（犬人,+200,1） | 参数=faction,数值,flag | 第三参数语义 |
| AddItemGround | `150,0,0,0` | 参数=池 id,qty,x,y ✅ | — |
| SetWaypoint | `43,118,1730,1`（坐标,剧情,1） | 参数=坐标+Encounter ✅ | 第四参数=1？ |
| Confiscate | `98,0,1`（没收物品组） | — | 三参数语义 |
| PassTime | `24,0` | 参数=小时 | 第二参数 |
| EndGame | `2198` | 参数=Encounter ✅ | — |
| SetPlayerCondition | `-633`（移除633） | 负号=移除 ✅ | — |

### 项6：~~aTreasures 省略第三段~~ 已解决（用户确认 2026-08-02）

**结论：省略第三段 = 数量默认 1**。45 条全部为 36 组（数据文件）条目、概率恒 `0.01694915254`=1/59（59 项均匀分布），例：`36.6x0.01694915254` 等价于 `36.6x0.01694915254x1`（池 447/528/529：随机手机数据/无用数据/支付数据）。

### 项7：~~recipes.nReverse 三值~~ 已解决（用户确认 2026-08-02）

**结论**：

| nReverse | 含义 | 条数 | 代表配方 |
|:--:|------|:--:|------|
| 0 | **不可拆解**（消耗/制作物） | 60 | 篝火、消毒水、烤肉、尸体处理 |
| 1 | **可拆回材料**（简单组装物） | 25 | 松鼠陷阱、开锁工具、自制警报器、油布帐篷、粗制/精致火把 |
| 2 | **可拆回组件**（复合装备） | 20 | 自制毛皮大衣/手套、带瞄准镜+背带的.308步枪、空调、照明设备、双筒望远镜 |

---

## 附：与代码现状的偏差（按模型表述，供后续修复参考）

1. **实体层误标**：`creatures.vEncounterIDs` 实体应为 Encounter（现标 Condition）；`encounters.nItemsID` 实体键应为 `{Id}`（现配 `{G}.{S}`）
2. **装饰层缺失**：`itemtypes.aSwitchIDs` 缺 `Pattern="{value}={id}"`（`Off=8.0` 现解析为 `0.0`）；`treasuretable.aTreasures` 缺 `x{qty}` 段与 `|` 集合分隔
3. **集合层缺失**：aTreasures 的 `|` OR 分隔符不支持（`Separator` 仅单分隔符）
4. **装饰实现不完整**：`[{id},{p1},{p2}]` 的括号参数被丢弃（往返 `[-137,0,0]` → `[-137]`）
5. **导出/反向索引用 ToString()**：ReferenceList 输出 `[a, b]` 显示格式而非 RawText，导致 XmlParser.Export 与 BuildReverse 数据损坏
6. **复合键实体无索引**：R16 §2 规定的 `group_id/subgroup_id` 索引随 SQLite 版移除，内存版 ReferenceIndex 对 `86.6` 无法解析
