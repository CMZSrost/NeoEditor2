# 38 全字段参考手册（整合版）

> 日期：2026-08-02
> 数据源：原版 `data/*.xml`（`D:\software\Steam\steamapps\common\Neo Scavenger\data`，phpMyAdmin dump 格式）全量统计 + 抽样验证；同日与 `Mods/` 下 NSE 各 mod（NSEg/NSEb/NSEf/NSEa/NSE/NSEoverride/NSEtT 共 7 命名空间）交叉验证（见文末附录）
> 范围：24 张数据表全部字段的**含义**（非引用列完整释义 + 实测值域）
> 与 [20-data-class-field-reference.md](20-data-class-field-reference.md)（docx 初步整理）和 [37-reference-column-semantics.md](37-reference-column-semantics.md)（引用列值级语义）的关系：
> - **引用列**（指向其他表的列）语义以 37 为准，本文档只列一行摘要并给出 37 章节指引
> - **非引用列**（数值/布尔/文本/枚举列）本文档基于真实数据给出实测值域、分布与语义
> - 与 20 文档冲突时**以本文档实测为准**，修正点用 ⚠️ 标注

## 统计口径说明

- 值域 = **原版**全部行实测（min/max/常见值）；**mod 数据扩展的值域见文末附录 §B**（如 nPenetration 原版 0-3、mod 到 5）
- `空` = 该列取空字符串的行数；`全空` = 所有行为空
- 布尔列 = 仅出现 `0/1` 的列；`nX [int] 0~3` = 虽叫 bX 但实际取值超过 0/1（⚠️ 非布尔）
- 行数：encounters 2264 / conditions 872 / treasuretable 764 / itemtypes 537 / hextypes 37 等，见各表

---

## 1. AttackMode（攻击模式）— `attackmodes`（61 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 唯一标识 | 1、2、3、4 | 1-61 连续 |
| `Name` | `strName` | string | 武器/攻击名称（汉化） | 12口径 独头弹、4毫米高斯步枪 穿甲弹、.308 步枪 金属被甲弹、.308 步枪 软尖弹 | 12口径 独头弹、4毫米高斯步枪 穿甲弹、.308 步枪 金属被甲弹、.308 步枪 软尖弹、.38 左轮手枪 金属被甲弹、.38 左轮手枪 空尖弹、.45 手枪 金属被甲弹、.45 手枪 空尖弹、12口径 00号鹿弹…（共 59 种） |
| `Notes` | `strNotes` | string | 作者注解，不影响游戏 | 无人机使用的高斯步枪穿甲弹、dog bite | 无人机使用的高斯步枪穿甲弹、dog bite |
| `Range` | `nRange` | int | 攻击距离；近战=1；0=特殊近战（id16「吵死了给我闭嘴」） | 1、30、10、3 | 0、1、2、3、10、20、25、30、40、60、75、80、95、120、180 |
| `DamageCut` | `fDamageCut` | float | 切割伤害 | 0.8、0、1.5、0.4 | 0、0.1、0.2、0.4、0.6、0.8、1、1.2、1.3、1.4、1.5、1.6 |
| `DamageBlunt` | `fDamageBlunt` | float | 钝器伤害 | 0.4、0.5、0.1、0.6 | 0、0.1、0.2、0.4、0.5、0.6、0.7、0.8、1、1.1、1.2、1.3、1.4 |
| `Penetration` | `nPenetration` | int | 穿透等级（与护甲破甲判定相关） | 1、0、2、3 | 0、1、2、3 |
| `Type` | `nType` | bool | 0=近战，1=远程 | 0/1 | 0/1 |
| `Sound` | `strSnd` | string | 武器声音/动画分类 | cueBlade、cueRifle、cueClub、cueBow | cueBlade、cueRifle、cueClub、cueBow、cueThrow、cuePistol、cuePunch、cueLaser、cueBite、cueChoke、cueClaws、cueGrasp |
| `Transfer` | `bTransfer` | bool | 弹药是否留在目标/掉落（1=弓箭、投掷类可回收） | 0/1 | 0/1 |
| `Image` | `strIMG` | string (refs) | 右下角武器图标文件名 | blank.png、AModeBowAnishinabe.png、AModeBowCompound.png、AModeBowGreenwood.png | blank.png、AModeBowAnishinabe.png、AModeBowCompound.png、AModeBowGreenwood.png、AMode12GaugeShotgun.png…（共 39 种） |
| `Morale` | `fMorale` | float | 武器自带士气补正。实际伤害 = (1+士气+此值)×(1+近战/远程加成)×武器伤害（⚠️ 20 文档称默认 0.25，实测最常见 0.3） | 0.3、0.25、0.6、0.4 | 0、0.05、0.1、0.2、0.25、0.28、0.3、0.35、0.4、0.5、0.6 |
| `WieldPhrase` | `strWieldPhrase` | string | 使用该武器进入战斗时的文字描述 | "用瞄准镜里瞄准他们, 虎视眈眈地狙击。"、.308猎枪已上膛、用.308瞄准镜瞄准后射击、改变他们的.308猎枪的握法，准备攻击 | "用瞄准镜里瞄准他们, 虎视眈眈地狙击。"、.308猎枪已上膛、用.308瞄准镜瞄准后射击、改变他们的.308猎枪的握法，准备攻击、"挥舞着很危险的爪子, 准备撕裂对方"、舒展腿部，准备战斗、双拳握紧，准备战斗…（共 10 种） |
| `AttackPhrases` | `vAttackPhrases` | string | 攻击时的文字描述，逗号分隔多个 | "开了一枪,用.308 的枪开了一枪,开了一枪,开了一枪"、"开了一枪,用高斯步枪开了一枪,开了一枪,开了一枪"、把步枪的枪托向上推，把枪的枪托向上摆动，把枪的枪托向上拉。、出拳，猛击，重击，勾拳 | "开了一枪,用.308 的枪开了一枪,开了一枪,开了一枪"、"开了一枪,用高斯步枪开了一枪,开了一枪,开了一枪"、把步枪的枪托向上推，把枪的枪托向上摆动，把枪的枪托向上拉。、出拳，猛击，重击，勾拳…（共 6 种） |
| `ChargeProfiles` | `strChargeProfiles` | string (refs) | 🔗 引用列 → ChargeProfile（37 §4.1） | 10、8、9、11 | 10、8、9、11、12、17、18、4、5、"6,31"、13、14、15、16、19、20、21、6、7 |
| `AttackerConditions` | `vAttackerConditions` | string (refs) | 🔗 引用列 → Condition（37 §4.1） | 0、211x1.0、"-115x1.0,-116x1.0,-117x1.0,-118x1.0,119x1.0,115x1.0"、67x0.05 | 0、211x1.0、"-115x1.0,-116x1.0,-117x1.0,-118x1.0,119x1.0,115x1.0"、67x0.05、67x0.15、67x0.25、67x0.85 |

---

## 2. BattleMove（战斗行动）— `battlemoves`（63 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 序列编号 | 1、2、3、4 | 1-63 连续 |
| `StrId` | `strID` | float (refs) | 物品编号（90 组 = 战斗 UI 选项/对话），🔗 引用列 → ItemType `{G}.{S}`（37 §4.2） | 90.10、90.102、90.103、90.104 | 90.10~90.95 |
| `Name` | `strName` | string | 动作名称（游戏内不直接显示） | 呼叫底特律无人机、前进、向目标投降、寻找 | 呼叫底特律无人机、前进、向目标投降、寻找、撤离、冲锋！、等待、躲闪、翻滚闪避、疯狂的近距离攻击、格挡、后退、唤来瓜头人、践踏、接受目标的投降、接受停战、近距离攻击、离开战斗、连续开火、落荒而逃、盲目攻击、盲目射击…（共 59 种） |
| `Notes` | `strNotes` | string | 注解：`(blind)`、`AI version`、`Ignore ceasefire/talk version`、`drone`、`non-drone` | (blind)、AI version、drone、Ignore ceasefire/talk version | (blind)、AI version、drone、Ignore ceasefire/talk version、non-drone |
| `Success` | `strSuccess` | string | 行动成功时游戏内文本，`<us>`=自己、`<them>`=目标 | &lt;us&gt;靠近&lt;them&gt;。、&lt;us&gt;请求向&lt;them&gt;投降！、&lt;us&gt;寻找&lt;them&gt;。、&lt;us&gt; 开始哀嚎求助! | &lt;us&gt;靠近&lt;them&gt;。、&lt;us&gt;请求向&lt;them&gt;投降！、&lt;us&gt;寻找&lt;them&gt;。、&lt;us&gt; 开始哀嚎求助!…（共 60 种） |
| `Fail` | `strFail` | string | 行动失败时文本 | &lt;us&gt; 尝试攻击 &lt;them&gt;...但没有击中!、&lt;us&gt; tries to lure &lt;them&gt; into a trap...but fail… | &lt;us&gt; 尝试攻击 &lt;them&gt;...但没有击中!、&lt;us&gt; tries to lure &lt;them&gt; into a trap...but fails!、&lt;us&g……（共 19 种） |
| `PopUp` | `strPopUp` | string | 战斗动作说明（含前提/效果描述） | 前进 向目标移动一格。 必须看见目标。 暴露自己。 有几率被地形绊倒 如果处于隐藏状态，有几率被发现。、寻找 浏览区域寻找隐藏的目标。 必须尚未看见目标。、撤回掩体 远离目标一格，躲入掩体。 必须… | 前进 向目标移动一格。 必须看见目标。 暴露自己。 有几率被地形绊倒 如果处于隐藏状态，有几率被发现。、寻找 浏览区域寻找隐藏的目标。 必须尚未看见目标。、撤回掩体 远离目标一格，躲入掩体。 必须看见目标。 低几率被地……（共 61 种） |
| `UsConditions` | `vUsConditions` | string (refs) | 🔗 引用列 → Condition（37 §4.2） | "[155,0,0],[737,0,0]"、"[-137,0,0],[147,0,0],[155,0,0],[339,0,0],[737,0,0]"、"[-137,0,0],[147,0,0],[… | "[155,0,0],[737,0,0]"、"[-137,0,0],[147,0,0],[155,0,0],[339,0,0],[737,0,0]"、"[-137,0,0],[147,0,0],[155,0,0],[7……（共 40 种） |
| `ThemConditions` | `vThemConditions` | string (refs) | 🔗 引用列 → Condition（37 §4.2） | "[152,0,0],[144,0,0]"、"[-137,0,0],[152,6,0.75],[144,6,0.75],[67,6,0.5],[68,6,0.5],[69,6,0.5],[75,6… | "[152,0,0],[144,0,0]"、"[-137,0,0],[152,6,0.75],[144,6,0.75],[67,6,0.5],[68,6,0.5],[69,6,0.5],[75,6,0.5]"、"[14……（共 19 种） |
| `PairConditions` | `vPairConditions` | string (refs) | 🔗 引用列 → Condition（37 §4.2） | "[496,0,0]"、"[491,0,0]"、"[140,0,0]"、"[140,0,0],[491,0,0],[491,0,0]" | "[496,0,0]"、"[491,0,0]"、"[140,0,0]"、"[140,0,0],[491,0,0],[491,0,0]"、"[209,0,0],[159,4,0.5]"、"[209,7,0]"…（共 27 种） |
| `UsFailConditions` | `vUsFailConditions` | string (refs) | 🔗 引用列 → Condition（37 §4.2） | "[155,0,0],[737,0,0]"、"[-137,0,0],[147,0,0],[155,0,0],[737,0,0]"、"[737,0,0]"、"[-137,0,0],[155,0,0]" | "[155,0,0],[737,0,0]"、"[-137,0,0],[147,0,0],[155,0,0],[737,0,0]"、"[737,0,0]"、"[-137,0,0],[155,0,0]"…（共 13 种） |
| `ThemFailConditions` | `vThemFailConditions` | string (refs) | 🔗 引用列 → Condition（37 §4.2，官方全留空） | — | — |
| `PairFailConditions` | `vPairFailConditions` | string (refs) | 🔗 引用列 → Condition（37 §4.2） | "[491,0,0],[491,0,0],[491,0,0]"、"[491,0,0]"、"[138,0,0],[138,0,0],[491,0,0]"、"[138,0,0],[491,0,0]" | "[491,0,0],[491,0,0],[491,0,0]"、"[491,0,0]"、"[138,0,0],[138,0,0],[491,0,0]"、"[138,0,0],[491,0,0]"、"[139,7,0]"…（共 15 种） |
| `UsPreConditions` | `vUsPreConditions` | string (refs) | 🔗 引用列 → Condition（37 §4.2，负号=不可拥有） | "-144,-145,-146,151,-727"、"-56,-143,-144,-148,-145,-146,-192,-390"、"-143,-144,-148,-145,-146,-190,… | "-144,-145,-146,151,-727"、"-56,-143,-144,-148,-145,-146,-192,-390"、"-143,-144,-148,-145,-146,-190,-191,-367,-……（共 51 种） |
| `ThemPreConditions` | `vThemPreConditions` | string (refs) | 🔗 引用列 → Condition（37 §4.2） | "-143,-367"、-18、"-18,-727"、"-18,151,-727" | "-143,-367"、-18、"-18,-727"、"-18,151,-727"、-367、"143,-367"、"-143,-144"、"-143,-144,-367"、"-143,-367,-725,-858"…（共 22 种） |
| `SeeThem` | `nSeeThem` | int | 需要看到对方的**暴露等级**（2=完全暴露才可） | 1、2、0 | 0、1、2 |
| `SeeUs` | `nSeeUs` | int | 对方需要看到我方的暴露等级 | 2、1、0 | 0、1、2 |
| `AllOutOfRange` | `bAllOutOfRange` | bool | 1=需在所有敌方攻击范围外（撤离类） | 0/1 | 0/1 |
| `InAttackRange` | `bInAttackRange` | int | ⚠️ 非布尔（0-3）：1=需在攻击范围内；3=特例（id44「窃眼」，需近距离） | 0、1、3 | 0、1、3 |
| `MinCharges` | `nMinCharges` | int | 最低弹药/充能数（0=不限） | 0、1、2、3 | 0、1、2、3 |
| `MinRange` | `nMinRange` | int | 最小使用距离，-1=全场覆盖 | -1、1、2、3 | -1、1、2、3 |
| `MaxRange` | `nMaxRange` | int | 最大使用距离，-1=全场覆盖 | -1、1、4、0 | -1、0、1、3、4、5 |
| `AttackModeType` | `nAttackModeType` | int | -1=非攻击，0=近战，1=远程 | -1、0、1 | -1、0、1 |
| `HexTypes` | `vHexTypes` | string | 所需地图格类型（官方全留空） | — | — |
| `Chance` | `fChance` | float | 可使用该动作的概率（0.05-1） | 1、0.1、0.5、0.75 | 0.05、0.1、0.15、0.5、0.65、0.75、1 |
| `Priority` | `fPriority` | float | AI 同回合行动优先级（0-1，仅 BOT 生效） | 0.9、0.6、0.2、0.4 | 0、0.05、0.1、0.2、0.3、0.4、0.5、0.6、0.8、0.9、1 |
| `Detect` | `fDetect` | float | 执行后被发现几率（0=永不发现） | 2、1、0.25、0.5 | 0、0.25、0.5、1、2 |
| `Order` | `fOrder` | float | AI 使用该动作的排序权重 | 0.5、0.75、0.8、0.15 | 0、0.15、0.25、0.5、0.55、0.75、0.8、0.85、0.9 |
| `Fatigue` | `fFatigue` | float | 疲劳值消耗（实际为整数 0-10） | 3、1、0、2 | 0、1、2、3、6、10 |
| `Approach` | `bApproach` | bool | 1=接近对方的动作 | 0/1 | 0/1 |
| `Offense` | `bOffense` | bool | 1=攻击性动作 | 0/1 | 0/1 |
| `FallBack` | `bFallBack` | bool | 1=后退动作 | 0/1 | 0/1 |
| `Retreat` | `bRetreat` | bool | 1=撤退动作 | 0/1 | 0/1 |
| `Position` | `bPosition` | bool | 1=姿势动作（蹲伏/趴下等） | 0/1 | 0/1 |
| `Passive` | `bPassive` | bool | 1=被动动作（等待/潜行等） | 0/1 | 0/1 |

---

## 3. CampType（营地类型）— `camptypes`（14 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 序列编号 | 1、2、3、4 | 1、2、3、4、5、6、7、8、9、10、11、12、13、14 |
| `Description` | `strDesc` | string | 营地描述（游戏内显示） | 17号实验室、惊骸之谷地下室、露天的公共垃圾焚烧桶、某家公司的5x10储存仓库 | 17号实验室、惊骸之谷地下室、露天的公共垃圾焚烧桶、某家公司的5x10储存仓库、区域的暗处、树林、一栋烧毁的建筑残骸、一间废弃的小屋、一间废弃的IT工作室、一间烧毁的公寓、一辆皮卡车、一辆全尺寸厢车、一辆掀背汽车…（共 14 种） |
| `ImageList` | `vImageList` | string (refs) | 营地图片文件名（`ItmScavenge*.png`） | ItmScavengeForest01.png、ItmScavengeApt01.png、ItmScavengeCar01.png、ItmScavengeCar02.png | ItmScavengeForest01.png、ItmScavengeApt01.png、ItmScavengeCar01.png、ItmScavengeCar02.png、ItmScavengeCar03.png…（共 13 种） |
| `Capacities` | `aCapacities` | string | ⚠️ 营地搜索界面容量 `宽x高`：`12x16/15x20/17x26/20x25/22x26/34x26` | 15x20、17x26、20x25、12x16 | 15x20、17x26、20x25、12x16、22x26、34x26 |
| `TreasureId` | `nTreasureID` | int (refs) | 🔗 引用列 → TreasureTable（37 §4.3） | 3、735、163、615 | 3、163、615、734、735、754 |
| `Alertness` | `m_fAlertness` | float | 营地默认警戒值（0-0.4） | 0.2、0.4、0.3、0 | 0、0.2、0.3、0.4 |
| `Visibility` | `m_fVisibility` | float | 可见度修正（负=降低可见，藏身效果） | -0.8、-0.5、-0.4、-0.05 | -0.8、-0.5、-0.4、-0.05 |
| `WetTempAdjustMod` | `WetTempAdjustMod` | float | 营地温度调节（避风/遮蔽效果），实测整数 | -15、-5、0、-13 | -15、-13、-12、-5、-3、0 |
| `HealPerHourMod` | `m_fHealPerHourMod` | float | 每小时回复修正（0-0.05） | 0.04、0、0.03、0.005 | 0、0.005、0.01、0.02、0.03、0.04、0.05 |
| `SleepQuality` | `fSleepQuality` | float | 睡眠质量修正（-0.36~0.18，负=差） | -0.18、-0.14、-0.36、-0.26 | -0.36、-0.26、-0.23、-0.18、-0.17、-0.14、-0.12、0、0.18 |

---

## 4. ChargeProfile（充能/弹药类型）— `chargeprofiles`（32 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `nID` | int | 序列编号 | 1、2、3、4 | 1-32 连续 |
| `Name` | `strName` | string | 充能描述（游戏内不显示） | .308步枪金属被甲弹、.308步枪软尖弹、.38手枪金属被甲弹、.38手枪空尖弹 | .308步枪金属被甲弹、.308步枪软尖弹、.38手枪金属被甲弹、.38手枪空尖弹、.45手枪金属被甲弹、.45手枪空尖弹、12口径00号鹿弹、12口径独头弹、4毫米高斯步枪穿甲弹、笔记本电量、粗制贯穿箭、粗制宽矢箭…（共 32 种） |
| `ItemId` | `strItemID` | float (refs) | 🔗 引用列 → ItemType `{G}.{S}`（37 §4.4） | 10.3、10.0、10.1、10.10 | 10.0、10.1、10.10、10.11、10.12、10.13、10.3、10.4、10.5、10.6、10.7、10.8、10.9、31.0、31.1、31.2、40.0、86.8、86.9、94.1 |
| `PerUse` | `fPerUse` | float | 每次使用消耗数量；**负=补充/充电**（id30 `每次增加40格电量` perUse=-40） | 1、0、-40、4 | -40、0、1、4、10、12 |
| `PerHour` | `fPerHour` | float | 每小时消耗；**负=每小时补充**（id28 `每小时增加10格电量`） | 0、1、-10、2 | -10、0、1、2、5、10、60 |
| `PerHourEquipped` | `fPerHourEquipped` | float | 装备时每小时消耗（仅 XM54 过滤芯片用） | 0、0.08 | 0、0.08 |
| `PerHex` | `fPerHex` | bool | 每移动一格消耗（原版全 0，未使用） | 0/1 | 0/1 |
| `Degrade` | `bDegrade` | bool | 1=耗尽的物品会降级（防毒面具滤芯不降解） | 0/1 | 0/1 |

⚠️ **负数语义（2026-08-02 实测确认）**：`fPerUse`/`fPerHour` 为负 = 充电/补充（如太阳能充电器、手摇发电机），正 = 消耗。编辑器应支持负值展示。

---

## 5. Condition（状态）— `conditions`（872 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 唯一标识（其他表用此编号引用） | 1、2、3、4 | 1-872 连续 |
| `Name` | `strName` | string | 状态名称（汉化） | 饱腹、肠胃炎、接受眼部手术、内出血 | 饱腹、肠胃炎、接受眼部手术、内出血、脱水、虚弱、鹰眼、-0.05 免疫力、-0.05 免疫力每小时、0.05小时内强制消失、阿勒根焚烧大厅、阿勒根瓜头发出警报、拔掉伤口上的尖刺、拔掉伤口上的尖锐物、把监控录像带交给哈特…（共 864 种） |
| `Description` | `strDesc` | string | 获得状态时的描述文本，`<us>`/`<them>` 占位符 | &lt;us&gt; 已经有了这种遭遇。、&lt;us&gt; 得到了基本配方。、&lt;us&gt; 做了件好事。、&lt;us&gt; 做了件坏事。 | &lt;us&gt; 已经有了这种遭遇。、&lt;us&gt; 得到了基本配方。、&lt;us&gt; 做了件好事。、&lt;us&gt; 做了件坏事。、"&lt;us&gt; 被纳因罗格诅咒,导致了痛苦和不幸。"…（共 795 种） |
| `FieldNames` | `aFieldNames` | string | 效果字段列表（逗号分隔）。⚠️ **实测 78 种**（含 20/Help 文档未列的 `WoundCut/WoundBruise/fFatigueModifier/m_fMoveCost` 等），全集见附录 A | TriggerEncounter、Money、"fFoodDebt,fWaterDebt"、m_fMorale | TriggerEncounter、Money、"fFoodDebt,fWaterDebt"、m_fMorale、"MinSafeTemp,MaxSafeTemp,BodyInsulation"、m_fPainLeft…（共 151 种） |
| `Modifiers` | `aModifiers` | string | 与 `aFieldNames` 一一对应的数值 | 0、1、-1、-0.3 | 0、1、-1、-0.3、0.1、2、-2、0.3、-0.5、"-1,0,-0.05"、0.5、-0.1、0.05、3、"-0.01,-0.05,2.5"、"-0.01,-0.15,5"、-0.05、-0.35…（共 250 种） |
| `Effects` | `aEffects` | string (refs) | 🔗 复合列（效果名+参数，含实体引用）→ 37 §5.3 | "ArmorWound=100,0.3,0.1;ArmorWound=111,0.3,0.1;ArmorWound=102,0.3,0.1;ArmorWound=112,0.3,0.1;Armor… | "ArmorWound=100,0.3,0.1;ArmorWound=111,0.3,0.1;ArmorWound=102,0.3,0.1;ArmorWound=112,0.3,0.1;ArmorWound=103,0……（共 72 种） |
| `Fatal` | `bFatal` | bool | 1=致命（死亡原因） | 0/1 | 0/1 |
| `IdNext` | `vIDNext` | string (refs) | 🔗 引用列 → Condition（37 §4.5，负号=移除） | 0、818、306、"530,531" | 0、818、306、"530,531"、609、866、"-198,-199,-200,-202,-203,-294,-295,-296,-297"、-827、116、117、118、133、134、"135,623"…（共 74 种） |
| `Duration` | `fDuration` | float | 持续时间（**小时**），0=瞬时/永久 | 0、24、1、0.011 | 0~17520 |
| `Permanent` | `bPermanent` | bool | ⚠️ **1=瞬时效果**（吃/喝/伤等一次性消费状态，dur=0），非"永久"！ | 0/1 | 0/1 |
| `ChanceNext` | `vChanceNext` | string | 触发 `vIDNext` 的概率（1=100%，与 vIDNext 逐位对应） | 0、1、0.25、0.7 | 0、1、0.25、0.7、"0.5,1"、0.8、0.9、"1,0.5"、0.1、"0.25,1"、0.3、0.5、0.95、0.01、"0.05,0.2"、0.4…（共 25 种） |
| `Stackable` | `bStackable` | bool | 1=可堆叠（重复获得会叠加） | 0/1 | 0/1 |
| `Display` | `bDisplay` | bool | 状态是否显示在状态栏 | 0/1 | 0/1 |
| `DisplayOther` | `bDisplayOther` | bool | 是否对他人可见（战斗中） | 0/1 | 0/1 |
| `DisplayGameOver` | `bDisplayGameOver` | bool | 是否显示在死亡/结局总结列表 | 0/1 | 0/1 |
| `Color` | `nColor` | int | 状态颜色：0=白、1=红(负面)、2=绿(正面)、3=黄 | 0、2、1、3 | 0、1、2、3 |
| `ResetTimer` | `bResetTimer` | bool | 1=每小时刷新剩余时间 | 0/1 | 0/1 |
| `RemoveAll` | `bRemoveAll` | bool | 1=移除所有同组状态（投降/食堂等） | 0/1 | 0/1 |
| `RemovePostCombat` | `bRemovePostCombat` | bool | 1=战斗结束后移除 | 0/1 | 0/1 |
| `TransferRange` | `nTransferRange` | int | 传染距离（格），-1=不传播 | -1、0、1、2 | -1、0、1、2、3、4 |
| `Thresholds` | `aThresholds` | string (refs) | 🔗 引用列 → Condition（37 §4.5，传奇技能等级阈值 `等级=状态`） | 1=795;2=794;3=763;4=764;5=761、3=672、3=750;4=751;5=744、4=783 | 1=795;2=794;3=763;4=764;5=761、3=672、3=750;4=751;5=744、4=783、4=784 |

### 附录 A：aFieldNames 实测全集（78 种，按出现次数降序）

```
TriggerEncounter(49) m_fImmuneRestoreRate(41) m_fDefense(37) m_fMorale(36) MinSafeTemp(33)
MaxSafeTemp(23) m_fPainLeft(24) m_fVisibility(24) BaseDetectionLevel(21) fFoodDebt(21)
m_fBloodRestoreRate(20) fSleepQuality(19) m_fMoraleHidden(19) m_fEncumberanceLimit(17)
m_fScent(17) m_fHealPerHourMod(27) m_fImmuneLeft(27) fWaterConsumptionRate(25)
fMovesPerTurnModifier(25) fWaterDebt(18) BodyInsulation(18) Money(18) fFoodConsumptionRate(13)
m_fFatigueModifier(13) m_fSleepAwareness(9) fPassiveRewarmPerHour(7) VisionRange(7)
LoseAllItems(7) m_fTrackingThreshold(6) m_nMorality(6) LoseRandomItem(5) Asleep(5)
LightLevel(5) AttDmgMult(5) m_fMoveReserve(9) fCoreTemp(4) m_fPainLeftBase(4)
m_fImmuneLeftBase(4) m_fBloodLeft(4) fSleepDebt(4) KnockDown(4) Crippled(4) DropAllItems(4)
m_fMoveReserveRemaining(3) m_fMoveCost(3) m_fPain(3) ApplyCutDamage(3) m_fMovesLeft(1)
WoundCut(2) WoundBruise(2) DefDmgMult(2) ChangeRange(2) ExitBattle(2) Infected(2)
Disinfected(2) ResetTemp(2) SpawnNewCreature(2) Threat(2) BattleRange(2) GetDiagnostic(1)
WetTempAdjustMod(1) Attack(1) JustMoved(1) Discharge(1) Trip(1) Bandaged(1)
fFatigueModifier(1) Splinted(1) ChangeRangeAll(1) LootTarget(1) ScatterMissile(1) UseGPS(1)
ResetUsSpotted(1) EmptyGroundSlot(1) CleanAndDress(1) AddRecipe(75) (空名×1)
```

> ⚠️ 与 20 文档差异：20 文档列 74 种，实测 78 种（新增 `WoundCut/WoundBruise/fFatigueModifier/m_fMoveCost` 及 1 个空名）；`m_fMoveCost`（20 文档写作 `MoveCost`）。

---

## 6. ContainerType（容器类型）— `containertypes`（39 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 序列编号 | 1、2、3、4 | 1-39 连续 |
| `Name` | `strName` | string | ⚠️ **容器/内容类别名**（20 文档称仅 1-6 种，实测 39 种）：即"什么物品能装进什么容器"（弹匣类别、电池类别等） | .308、.38、.45、12口径 | .308、.38、.45、12口径、4mm、笔记本电池、粗、地形、电、防火、防水、非嵌套容器、购物车和手推车、光盘、技能、箭（箭术）、胶状物、精、军用纽扣电池、纳米机器人、软件: 平板电脑、软件: 数据、软件：个人电脑…（共 39 种） |

> 语义：`itemtypes.aContentIDs`（能装入的内容类别）↔ `nFormatID`（自身类别）配合使用，值域一致（1-39）。🔗 反向引用见 37 §4.6。

---

## 7. Creature（生物）— `creatures`（28 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 序列编号 | 1、2、3、4 | 1-28 连续 |
| `Name` | `strName` | string | 生物名称（汉化）：狗人/掠夺者/圣王伊莱亚斯等 | 掠夺者、圣王伊莱亚斯、野狗、部落战士 | 掠夺者、圣王伊莱亚斯、野狗、部落战士、玛莎的军队、底特律守卫、底特律无人机、恩菲尔德恐魔、狗人、瓜头人、蓝蛙传教士、蓝蛙教徒、鹿、魔迦怨灵、玩家、王后莉莎、摇滚帮、夜辛卡 |
| `NamePublic` | `strNamePublic` | string | 未接触前显示的名称，1 行 `陌生人r` ⚠️ 疑似笔误 | 陌生人、圣王伊莱亚斯、野狗、恩菲尔德恐魔 | 陌生人、圣王伊莱亚斯、野狗、恩菲尔德恐魔、狗人、瓜头人、鹿、魔迦怨灵、陌生人r、你、王后莉莎、无人机、夜辛卡 |
| `Notes` | `strNotes` | string | 注解（剧情身份）：`JD`、`Recruiter`、`玩家的基础统计` 等 | 毕蒂（喋喋不休的个性）、伯恩山犬、部落男战士、部落女战士 | 毕蒂（喋喋不休的个性）、伯恩山犬、部落男战士、部落女战士、底特律无人机、底特律延伸区的守卫、黑毛/纽芬兰犬、金毛猎犬、金姆（独裁专政的个性）、丽塔（老实憨厚的个性）、圣王圣驾于惊骸谷后前往底特律…（共 19 种） |
| `Image` | `strImg` | string (refs) | 地图上显示的图片文件名 | CreHuman.png、CreATN.png、CreDeerDoe.png、CreDMCDrone.png | CreHuman.png、CreATN.png、CreDeerDoe.png、CreDMCDrone.png、CreDogBM.png、CreDogGR.png、CreDogman.png、CreDogNF.png…（共 15 种） |
| `EncounterIds` | `vEncounterIDs` | string (refs) | 🔗 引用列 → **Encounter**（37 §4.7，⚠️ 20 文档误标 Condition） | "1728,1729,1905"、"1397,1416"、1189、"1328,1362,1345,1335,1399,2241" | "1728,1729,1905"、"1397,1416"、1189、"1328,1362,1345,1335,1399,2241"、"1334,1384,1394,1400,2238"…（共 15 种） |
| `MovesPerTurn` | `nMovesPerTurn` | int | 每回合行动点数 | 4、5、3、8 | 3、4、5、8 |
| `TreasureId` | `nTreasureID` | int (refs) | 🔗 引用列 → TreasureTable（37 §4.7，初始携带物） | 3、70、612、559 | 3、9、70、71、352、473、545、559、597、612、642、678、708 |
| `Faction` | `nFaction` | int (refs) | 🔗 引用列 → Faction（37 §4.7；0=玩家/中立） | 2、4、12、14 | 0、1、2、3、4、5、6、7、8、9、10、11、12、13、14 |
| `AttackModes` | `vAttackModes` | int (refs) | 🔗 引用列 → AttackMode（37 §4.7；1=拳头） | 1、61、7、17 | 1、7、17、50、59、61 |
| `BaseConditions` | `vBaseConditions` | string (refs) | 🔗 引用列 → Condition（37 §4.7，`状态=概率`） | "38=1,52=0.5,71=0.75,57=1,124=0.75,149=0.4,473=1,577=1,858=1"、"51=0.5,52=0.5,61=0.5,71=0.5,81=0.5,… | "38=1,52=0.5,71=0.75,57=1,124=0.75,149=0.4,473=1,577=1,858=1"、"51=0.5,52=0.5,61=0.5,71=0.5,81=0.5,150=0.5,151……（共 20 种） |
| `CorpseId` | `nCorpseID` | int (refs) | 🔗 引用列 → TreasureTable（37 §4.7，尸体掉落池） | 402、757、396、409 | 396、402、409、424、490、666、757 |
| `Activities` | `vActivities` | string | 生物待机活动描述（逗号分隔，仅注释用途） | "licking itself,sniffing the air,pacing,digging"、"reciting something silently,picking at a scab,st… | "licking itself,sniffing the air,pacing,digging"、"reciting something silently,picking at a scab,staring solem……（共 21 种） |

---

## 8. CreatureSource（生物刷新点）— `creaturesources`（32 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 序列编号 | 1、2、3、4 | 1-32 连续 |
| `Name` | `strName` | string | 刷新点描述（汉化）：`东部鹿群`、`冷冻休眠机构的狗人` | 来自东南方的掠夺者、阿勒根瓜头人、北部独行的狗人、北部群居的狗人 | 来自东南方的掠夺者、阿勒根瓜头人、北部独行的狗人、北部群居的狗人、部落女战士、部落战士、底特律守卫、东部鹿群、皇后莉莎（到剔骸谷，接着是萨吉诺）、僵僵食堂的摇滚帮、蓝蛙传教士、蓝蛙教徒、冷冻休眠机构的狗人、玛莎军 士兵1…（共 27 种） |
| `X` | `nX` | int | X 坐标；**-1=跟随玩家当前坐标** | -1、48、20、26 | -1、1、17、18、20、26、28、32、40、42、48、57 |
| `Y` | `nY` | int | Y 坐标；-1=跟随玩家 | -1、164、158、30 | -1、2、30、69、78、100、104、108、124、148、158、164、192 |
| `CreatureId` | `nCreatureID` | int (refs) | 🔗 引用列 → Creature（37 §4.8） | 1、8、24、0 | 0~28 |
| `Min` | `nMin` | int | 最小刷新数量 | 1、2、0、3 | 0、1、2、3、4 |
| `Max` | `nMax` | int | 最大刷新数量 | 1、4、2、3 | 0、1、2、3、4、6 |
| `Weight` | `fWeight` | float | 刷新权重（0.2-1，多刷点竞争时概率） | 1、0.4、0.33、0.5 | 0.2、0.33、0.4、0.5、0.6、0.8、1 |

---

## 9. DataFile（电子产品数据）— `datafiles`（88 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 序列编号 | 1、2、3、4 | 1-88 连续 |
| `Name` | `strName` | string | 数据类型（⚠️ mod 新增 `地址簿/邮件文件/PDF文档`，见附录 §B） | 文本文件、视频文件、图像文件、数据库 | 文本文件、视频文件、图像文件、数据库、PDF文件 |
| `Description` | `strDesc` | string | 数据内容描述（汉化，电子设备内可读取的文本/图片内容） | 底特律储蓄银行某活动账户的预授权借记申请程序、底特律储蓄银行某账户登入详情、"美国顶级流浪摔跤手" S8E21、"内衣不干净的第一季!" 真人秀S2E1 | 底特律储蓄银行某活动账户的预授权借记申请程序、底特律储蓄银行某账户登入详情、"美国顶级流浪摔跤手" S8E21、"内衣不干净的第一季!" 真人秀S2E1…（共 83 种） |
| `Image` | `strImg` | string (refs) | 图标：`ItmDataPDF/IMG/DB/TXT/VID.png`（⚠️ mod 可引用其他命名空间图片：`NSE:ItmDataAddr.png`，见附录 §A） | ItmDataTXT.png、ItmDataVID.png、ItmDataIMG.png、ItmDataDB.png | ItmDataTXT.png、ItmDataVID.png、ItmDataIMG.png、ItmDataDB.png、ItmDataPDF.png |

---

## 10. DmcPlace（底特律城区建筑）— `dmcplaces`（7 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 序列编号 | 1、2、3、4 | 1、2、3、4、5、6、7 |
| `Image` | `strImg` | string (refs) | 建筑按钮图标：`btn_dmc_apts/bank/diner/gate/health` | btn_dmc_bank、btn_dmc_apts、btn_dmc_diner、btn_dmc_gate | btn_dmc_bank、btn_dmc_apts、btn_dmc_diner、btn_dmc_gate、btn_dmc_health |
| `EncounterId` | `nEncounterID` | int (refs) | 🔗 引用列 → Encounter（37 §4.10） | 814、842、1123、1124 | 814、842、1123、1124、1172、1174、1223 |
| `X` | `nX` | int | X 坐标（底特律城区内） | 162、86、100、189 | 86、100、162、189、289 |
| `Y` | `nY` | int | Y 坐标 | 267、158、233、282 | 158、233、267、282、447 |

---

## 11. Encounter（剧情/遭遇）— `encounters`（2264 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 唯一编号 | 1、2、3、4 | 1-2264 连续 |
| `Name` | `strName` | string | 剧情名称（黄色文本，输入信息选项时可见） | 对话、离开。、在道路上、打电话给卡尔的银行账户 续 | 对话、离开。、在道路上、打电话给卡尔的银行账户 续、离开房子。、翻找你的东西。、被底特律流放、退出、往西走去大门。、哎，你可真是个好孩子。好吧，看看你都能为爸爸做些什么。、不，谢了。、不要紧。、底特律储蓄银行…（共 1870 种） |
| `Description` | `strDesc` | string | 剧情主体文本（对话/描述） | 你翻遍了你的物品来寻找有用的东西。、无法判断，但看起来他爸吸毒过量了，你帮不了什么忙。 这孩子看起来也是，你很确定他也染了毒瘾。他抓着你不放，看来你已经被他选为下一任监护人了。 你低下身子，拍拍他… | 你翻遍了你的物品来寻找有用的东西。、无法判断，但看起来他爸吸毒过量了，你帮不了什么忙。 这孩子看起来也是，你很确定他也染了毒瘾。他抓着你不放，看来你已经被他选为下一任监护人了。 你低下身子，拍拍他的头，说了些安抚的话。……（共 2084 种） |
| `TreasureId` | `nTreasureID` | int (refs) | 🔗 引用列 → TreasureTable（37 §4.11） | 3、456、590、591 | 3~738 |
| `RemoveTreasureId` | `nRemoveTreasureID` | int (refs) | 🔗 引用列 → TreasureTable（37 §4.11） | 3、654、658、590 | 3~739 |
| `Conditions` | `aConditions` | string (refs) | 🔗 引用列 → Condition（37 §4.11，负号=否定） | 1、0、229、228 | 1、0、229、228、226、227、-506、-499、671、"-499,-506"、762、112、"743,743"、743、68、744、230、683、"226,309"、"67,226"…（共 509 种） |
| `PreConditions` | `aPreConditions` | string (refs) | 🔗 引用列 → Condition（37 §4.11） | 222、224、223、72 | 222、224、223、72、-72、-761、"376,-499,506"、425、-122、412、714、-136、-565、-651、26、"376,-499,506,782"、419、565、-16、-23…（共 452 种） |
| `Price` | `fPrice` | float | 给/扣的钱；玩家钱不够则选项不显示（负值语义待验证） | 0、5、20、4 | 0~5600 |
| `Responses` | `aResponses` | string (refs) | 🔗 复合列 → 37 §5.1（双实体 + 4 参数） | =1x1x0x0x0、=2007x1x0x0x0、=16x1x0x0x0、=2041x1x0x0x0 | =1x1x0x0x0、=2007x1x0x0x0、=16x1x0x0x0、=2041x1x0x0x0、=1500x1x0x0x0、=860x1x0x0x0、=1080x1x0x0x0、=181x1x0x0x0…（共 1034 种） |
| `MinimapHexes` | `aMinimapHexes` | string | ⚠️ 小地图标记：`x,y=标签[=flag]`（flag 语义待探索） | 25x36=格雷林营地、40x108=萨吉诺精神病院、26x104=ATN 营地、"57x192=底特律大门,57x191=" | 25x36=格雷林营地、40x108=萨吉诺精神病院、26x104=ATN 营地、"57x192=底特律大门,57x191="、17x124=阿勒根游乐场、43x118=剔骨地、14x163=怪异林…（共 17 种） |
| `RemoveCreatures` | `bRemoveCreatures` | bool | 是否移除当前格生物（原版未使用） | 0/1 | 0/1 |
| `RemoveUsed` | `bRemoveUsed` | bool | 1=移除用来触发此剧情的物品 | 0/1 | 0/1 |
| `ItemsId` | `nItemsID` | int (refs) | 🔗 引用列 → ItemType **主键 id**（37 §4.11，⚠️ 20 文档误标复合键） | 3、92、540、79 | 2~764 |
| `CreatureId` | `nCreatureID` | int (refs) | 🔗 引用列 → Creature（37 §4.11；0=无） | 0、2、3、23 | 0、2、3、17、23、24、25、26、29 |
| `CreatureHex` | `ptCreatureHex` | string | 生物出现位置：`半径,方向`（`40,0`=半径 40 任意方向；`0,0`=本格） | "0,0"、"1,2"、"1,4"、"1,5" | "0,0"、"1,2"、"1,4"、"1,5"、"28,56"、"40,0" |
| `Teleport` | `ptTeleport` | string | 玩家传送坐标 `x,y`；`0,0`=不传送 | "0,0"、"26,110"、"56,193"、"26,104" | "0,0"、"26,110"、"56,193"、"26,104"、"57,191"、"58,198"、"20,163"、"25,37"、"26,102"、"56,191"、"58,191"、"58,192" |
| `Editor` | `ptEditor` | string | 编辑器坐标（游戏忽略） | "-10028,-1953"、"-10144,-13700"、"-10167,-7051"、"-10178,-7771" | "-10028,-1953"、"-10144,-13700"、"-10167,-7051"、"-10178,-7771"、"-10276,-15471"、"-10279,-9553"、"-10544,-11815"…（共 2264 种） |
| `Type` | `nType` | int | ⚠️ 0=普通剧情，1=搜刮(Scavenge)，2=战斗（仅 id236），3=破解（黑客/软件运行） | 0、1、3、2 | 0、1、2、3 |
| `LootChance` | `fLootChance` | float | 搜刮成功几率 | 0、0.2、0.1、0.75 | -0.2、-0.1、0、0.1、0.15、0.2、0.25、0.35、0.4、0.5、0.75、0.85、1 |
| `AccidentChance` | `fAccidentChance` | float | 搜刮事故几率 | 0、0.07、0.1、0.05 | -0.5、-0.3、-0.2、0、0.05、0.07、0.1、0.15、0.17、0.25 |
| `CreatureChance` | `fCreatureChance` | float | 生物袭击几率 | 0、0.25、0.1、0.5 | -0.5、-0.1、0、0.1、0.2、0.25、0.35、0.5 |
| `Accidents` | `vAccidents` | string (refs) | 🔗 引用列 → Encounter（37 §4.11；`0`=无事故、`1`=事故占位） | 1、0、"100,102,103,104,1724"、"100,102,103,104" | 1、0、"100,102,103,104,1724"、"100,102,103,104"、"39,1724"、44、"100,102,103,104,39"、"100,102,103,104,39,1724"…（共 10 种） |
| `Loot` | `vLoot` | int (refs) | 🔗 引用列 → TreasureTable（37 §4.11；0=无） | 0、3、50、606 | 0、3、5、40、41、42、43、44、45、46、48、50、51、52、67、73、93、574、598、606 |

---

## 12. EncounterTrigger（事件触发器）— `encountertriggers`（133 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 序列编号 | 1、2、3、4 | 1-133 连续 |
| `Name` | `strName` | string | 触发器名称（`me_E1.1` 等内部编号或中文描述名） | blank、到达底特律大门、到达底特律大门 (隔离14天)、到达底特律大门 (隔离24小时) | blank、到达底特律大门、到达底特律大门 (隔离14天)、到达底特律大门 (隔离24小时)、到达剔骸之谷、第一次抵达废墟，搜索教程、第一次造访延伸区、第一次造访沼泽、发现阿勒根游乐场、发现底特律大都市、发现格雷林营地…（共 130 种） |
| `EncounterId` | `nEncounterID` | int (refs) | 🔗 引用列 → Encounter（37 §4.12） | 1、840、1553、2 | 1~2251 |
| `Chance` | `fChance` | float | 触发几率（0-1） | 1、0.35、0、0.15 | 0、0.15、0.35、1 |
| `LocBased` | `bLocBased` | bool | 1=坐标触发（对应 `aArea`） | 0/1 | 0/1 |
| `DateBased` | `bDateBased` | bool | 1=时间触发（对应 `dateMin/dateMax`） | 0/1 | 0/1 |
| `HexBased` | `bHexBased` | bool | 1=格类型触发（对应 `aHexTypes`） | 0/1 | 0/1 |
| `Unique` | `bUnique` | int | ⚠️ 非布尔：1=唯一（只触发一次）、0=可重复、**2=特例**（id6「到达剔骸之谷」，语义待探索） | 1、0、2 | 0、1、2 |
| `AIPassable` | `bAIPassable` | bool | 1=AI 也可触发 | 0/1 | 0/1 |
| `Area` | `aArea` | string | 触发坐标：`x,y,距离`；`0,0,1000`=全场任意位置（1000 表示无限） | "0,0,0"、"0,0,1000"、"26,104,2"、"40,108,0" | "0,0,0"、"0,0,1000"、"26,104,2"、"40,108,0"、"57,192,0"、"20,164,0"、"58,197,0"、"20,148,2"、"34,126,0"、"43,118,0"…（共 14 种） |
| `DateMin` | `dateMin` | string | 最早触发时间 `年-月-日-小时`；游戏开始 `1000-0-1-6`，其他为 21/22/0 等时段 | 1000-0-1-0、1000-0-1-21、1000-0-1-22、1000-0-1-6 | 1000-0-1-0、1000-0-1-21、1000-0-1-22、1000-0-1-6、1000-0-1-7 |
| `DateMax` | `dateMax` | string | 最晚触发时间；`9999-11-31-20/21/23/6`（9999=不限；⚠️ mod 新增 `9999-11-31-7/4`，见附录 §B） | 9999-11-31-23、9999-11-31-6、9999-11-31-20、9999-11-31-21 | 9999-11-31-23、9999-11-31-6、9999-11-31-20、9999-11-31-21 |
| `HexTypes` | `aHexTypes` | string (refs) | 🔗 引用列 → HexType（37 §4.12） | "10,12,16"、"1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27"、5、32 | "10,12,16"、"1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27"、5、32…（共 18 种） |

---

## 13. Faction（阵营/派系）— `factions`（14 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 派别编号 | 1、2、3、4 | 1、2、3、4、5、6、7、8、9、10、11、12、13、14 |
| `Name` | `strName` | string | 阵营名称（完整列表见示例列） | 部落战士、底特律守卫、恩菲尔德恐魔、狗人 | 部落战士、底特律守卫、恩菲尔德恐魔、狗人、瓜头人、蓝蛙教、鹿、掠夺者、玛莎的军队、魔迦怨灵、圣王伊莱亚斯、摇滚帮、野狗、夜辛卡 |
| `DictFactions` | `dictFactions` | string (refs) | 🔗 引用列 → Faction（37 §4.13，声望值，负=敌对） | "0=-100,1=-100,2=-100,3=-100,4=-100,5=-100,6=-100,7=-100,8=-100,9=-100,10=-100,11=-100,12=-100,13=… | "0=-100,1=-100,2=-100,3=-100,4=-100,5=-100,6=-100,7=-100,8=-100,9=-100,10=-100,11=-100,12=-100,13=-100,14=1"、……（共 14 种） |

> ⚠️ 与 20 文档差异：20 文档阵营名与实测不完全一致（20 称 3=食人族/Bad Mutha，实测 3=摇滚帮；5-14 实测已全部有名称）。

---

## 14. ForbiddenHex（保护区/禁用格点）— `forbiddenhexes`（16 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 序列编号 | 1、2、3、4 | 1、2、3、4、5、6、7、8、9、10、11、12、13、14、15、16 |
| `X` | `nX` | int | X 坐标 | 58、26、25、57 | 25、26、57、58 |
| `Y` | `nY` | int | Y 坐标 | 103、105、189、191 | 102、103、104、105、106、187、188、189、190、191、192、193 |
| `Name` | `strName` | string | 所属区域（区域外敌人不可进入） | 底特律、阿尼什纳比部族 | 底特律、阿尼什纳比部族 |

---

## 15. GameVar（游戏变量）— `gamevars`（19 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Name` | `strName` | string | 变量名（见下表） | fCloudChanceJan、fCloudChanceJuly、fCloudChanceVar、fPrecipChanceJan | fCloudChanceJan、fCloudChanceJuly、fCloudChanceVar、fPrecipChanceJan、fPrecipChanceJuly、fPrecipChanceVar…（共 19 种） |
| `Type` | `strType` | string | 数值类型：int / Number（小数变量） | int、Number | int、Number |
| `Value` | `strValue` | string | 变量值（整数，实测 6-2064） | 20、25、30、80 | 6、8、9、14、15、20、25、30、35、42、50、58、80、164、2064 |

### 实测变量全集

| 变量 | 类型 | 值 | 含义 |
|------|------|-----|------|
| `fCloudChanceJan/July/Var` | Number | 80/20/30 | 1 月/7 月云量基准+浮动 |
| `fPrecipChanceJan/July/Var` | Number | 50/30/35 | 1 月/7 月降水概率+浮动 |
| `nSkillPoints` | int | 15 | 开局技能点数 |
| `nStartDateDay/Hour/Month/Year` | int | 14/6/9/2064 | 开局日期 2064-9-14 6 时 |
| `nStartHexX/Y` | int | 20/164 | 开局坐标 |
| `nTempJanHigh/Low/Var` | int | 25/8/42 | 1 月气温高/低/浮动 |
| `nTempJulyHigh/Low/Var` | int | 80/58/25 | 7 月气温高/低/浮动 |

---

## 16. Headline（新闻头条）— `headlines`（48 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 序列编号 | 1、2、3、4 | 1-48 连续 |
| `HeadlineText` | `strHeadline` | string | 报纸头条文本（汉化，含多行正文） | “韩先开的枪！” 在代号“不朽捍卫者”的超级士兵项目揭晓的前夕，接连而起的暴动已然动摇了工业巨商们的根基。尽管中国的官方新闻系统仍未公开的任何相关新闻，但市民们在接二连三发起的小型暴动中奋勇的表现… | “韩先开的枪！” 在代号“不朽捍卫者”的超级士兵项目揭晓的前夕，接连而起的暴动已然动摇了工业巨商们的根基。尽管中国的官方新闻系统仍未公开的任何相关新闻，但市民们在接二连三发起的小型暴动中奋勇的表现似乎已经表明，一场受人……（共 48 种） |

---

## 17. HexType（地块类型）— `hextypes`（37 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 序列编号（地图 strDef 用此编号） | 1、2、3、4 | 1-37 连续 |
| `Name` | `strName` | string | 地块名称（内部名） | 检验平原、平原、阿勒根游乐场、北城墙 | 检验平原、平原、阿勒根游乐场、北城墙、城郊废墟、城市大门、城市废墟、城市废墟 (摩天大厦)、大都市市中心、东北城墙、东北方偏北城墙、东南城墙、格雷林营地、诡异森林、海滨、海洋、僵僵食堂、巨型黑沼泽、冷冻休眠机构、林中小屋…（共 34 种） |
| `Description` | `strDesc` | string | 游戏内显示名 | 测坦草地、平坦草地、山麓丘陵、阿勒根游乐场 | 测坦草地、平坦草地、山麓丘陵、阿勒根游乐场、北城墙、城市大门、大都市市中心、东北城墙、东北方偏北城墙、东南城墙、废弃大厦、废弃的房屋和拖车、废弃的建筑物、高山、格雷林营地、诡异森林、海滩、化为瓦砾的城市废墟、僵僵食堂…（共 33 种） |
| `TerrainCost` | `nTerrainCost` | int | 移动消耗；11=不可通行（海洋/海滨/山地） | 1、2、11 | 1、2、11 |
| `VizLimiter` | `nVizLimiter` | int | 视野减少值（树林/城市=2 遮挡） | 2、0、1 | 0、1、2 |
| `VizIncrease` | `nVizIncrease` | bool | 视野增加值（1=山丘类，可眺望） | 0/1 | 0/1 |
| `TreasureId` | `nTreasureID` | int (refs) | 🔗 引用列 → TreasureTable（37 §4.17） | 3、22、6、7 | 1、3、6、7、22、49、149、164、309、310、311、474、500、614 |
| `Passable` | `bPassable` | bool | 0=不可通行（海洋/海滨/城墙 ×6） | 0/1 | 0/1 |
| `ScavengeInitialId` | `nScavengeInitialID` | int (refs) | 🔗 引用列 → TreasureTable（37 §4.17，首次搜刮池，爆率高） | 3、579、727、575 | 3、575、578、579、580、581、725、726、727 |
| `ScavengeItemsIdPerHour` | `nScavengeItemsIDPerHour` | int (refs) | 🔗 引用列 → TreasureTable（37 §4.17，后续每小时搜刮池） | 3、29、25、27 | 3、25、27、28、29、30、475 |
| `CampItems` | `nCampItems` | int (refs) | 🔗 引用列 → CampType（37 §4.17，⚠️ 20 文档称默认 5，实测 5×27 为主；0=不可扎营） | 5、0、2、3 | 0、2、3、4、5 |
| `LightLevels` | `vLightLevels` | string | 6 时段亮度（0.15-1.0；室内/城墙恒 1.0） | "0.57,1.0,1.0,1.0,0.57,0.15"、"1.0,1.0,1.0,1.0,1.0,1.0"、"0.8,1.0,1.0,1.0,0.8,0.57"、"0.8,1.0,1.0,1.0… | "0.57,1.0,1.0,1.0,0.57,0.15"、"1.0,1.0,1.0,1.0,1.0,1.0"、"0.8,1.0,1.0,1.0,0.8,0.57"、"0.8,1.0,1.0,1.0,0.8,0.8"…（共 6 种） |
| `DefaultCampId` | `nDefaultCampID` | int (refs) | 🔗 引用列 → CampType（37 §4.17；517=默认） | 517、519、521、525 | 517、519、521、523、525、527、755 |
| `MinRange` | `nMinRange` | int | 遭遇生物最小距离 | 3、20、4、15 | 3、4、15、20 |
| `MaxRange` | `nMaxRange` | int | 遭遇生物最大距离 | 6、10、15、5 | 5、6、8、10、15、30、40 |
| `ConditionIds` | `vCondIDs` | string (refs) | 🔗 引用列 → Condition（37 §4.17，进入地块获得的状态） | 457、618 | 457、618 |

---

## 18. Ingredient（合成材料）— `ingredients`（128 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `nID` | int | 材料编号（recipe 用 `数量x编号` 引用） | 1、2、3、4 | 1-128 连续 |
| `Name` | `strName` | string | 材料描述（汉化） | 中型带毛皮的尸体、".308 步枪,无准镜无枪带"、12号口径猎枪 无背带、"4毫米高斯步枪,无瞄准镜, 无背带" | 中型带毛皮的尸体、".308 步枪,无准镜无枪带"、12号口径猎枪 无背带、"4毫米高斯步枪,无瞄准镜, 无背带"、4毫米高斯步枪有瞄准镜，无背带、4毫米高斯枪机匣、4毫米高斯枪枪管、笔记本电池、捕猎技能、超高能电容器…（共 127 种） |
| `RequiredProps` | `strRequiredProps` | string (refs) | 🔗 引用列 → ItemProp（37 §4.18，`&`=全部满足） | 27&amp;29&amp;56、17&amp;9&amp;36、3&amp;11&amp;16、48 | 27&amp;29&amp;56、17&amp;9&amp;36、3&amp;11&amp;16、48、56、8&amp;9&amp;10、1、1&amp;13&amp;16&amp;28…（共 120 种） |
| `ForbidProps` | `strForbidProps` | string (refs) | 🔗 引用列 → ItemProp（37 §4.18） | 28、39、68、17&amp;65 | 28、39、68、17&amp;65、3、2、2&amp;68、26&amp;67、26&amp;67&amp;70、56、85&amp;86、1、15、15&amp;16、17、17&amp;28&amp;65…（共 27 种） |

---

## 19. ItemProp（物品属性）— `itemprops`（108 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `nID` | int | 属性编号（配方/物品引用） | 1、2、3、4 | 1-108 连续 |
| `PropertyName` | `strPropertyName` | string | 属性名（英文原文），含：技能类（skill: trapping/lockpicking/mechanic/electrician/ranged/botany）、工具类（tool: Philips-head screwdriver）、武器类别（.308 rifle/12-gauge shotgun/4mm Gauss Rifle）、标记类（AI never loots/ignore in crafting screen）等。全集见 20 文档 §19 已知属性 + 37 §4.19 | .308 rifle、12-gauge shotgun、4mm Gauss gun barrel、4mm Gauss gun receiver | .308 rifle、12-gauge shotgun、4mm Gauss gun barrel、4mm Gauss gun receiver、4mm Gauss Rifle、absorbent…（共 108 种） |

---

## 20. ItemType（物品类型）— `itemtypes`（537 行）⭐

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 序列编号（**注意**：游戏内物品 ID = `GroupId.SubgroupId`，非此 Id） | 1、2、3、4 | 1-537 连续 |
| `GroupId` | `nGroupID` | int | 物品组号（90=对话/UI 类×131、101=食物×28、8=？×20） | 90、101、8、26 | 0~103 |
| `SubgroupId` | `nSubgroupID` | int | 组内序号 | 0、1、2、3 | 0-130 连续 |
| `Name` | `strName` | string | 物品名称（汉化）：`iSlab平板电脑`、`95GHz探测仪` 等 | 食物、服饰(衬衫)、瓶子、搜索点 | 食物、服饰(衬衫)、瓶子、搜索点、弹药、步枪、剧情点、服饰(鞋子)、界面容量、软件、液体、尸体、中型物件、电池、药丸、存储、头饰、营地设施、区域资源、服饰(裤子)、光盘、文件、小型物件、载具、弓、光盘盒、火把、手链…（共 249 种） |
| `Description` | `strDesc` | string | 游戏内描述（汉化） | 一些子弹、电池、水、阿塔斯"大宇"智能手机（关闭） | 一些子弹、电池、水、阿塔斯"大宇"智能手机（关闭）、橙色药片、底特律大都市追踪手环、底特律无线电塔、点燃的营火、电气面板、福珺 "铜书"笔记本(关闭)、福珺 "铜书"笔记本(合拢)、可背带的猎枪、空调通风口、蘑菇…（共 506 种） |
| `DescriptionAlt` | `strDescAlt` | string | 真实描述（需技能辨识后显示） | 水(有毒、煮沸)、.308 金属被甲步枪弹、.308 软尖步枪弹、.38 金属被甲手枪弹 | 水(有毒、煮沸)、.308 金属被甲步枪弹、.308 软尖步枪弹、.38 金属被甲手枪弹、.38 空尖手枪弹、.45 金属被甲手枪弹、.45 中空手枪弹、12口径 00号鹿弹、12口径 独头弹、4毫米高斯步枪 穿甲弹…（共 74 种） |
| `ConditionId` | `nCondID` | int (refs) | 🔗 引用列 → Condition（37 §4.20）。⚠️ **1=空状态（无条件辨识）**（mod 用 **0** 同样表示无条件，见附录 §B）；实测映射：87=擅长远程射击（枪械/弹药）、53=精通医学（药丸/瓶子）、216=熟练电工（V-MADS/探测仪）、393=植物学家（食物）、425=精通黑客（破解软件）、28=戴上底特律通行证（手链） | 1、87、53、216 | 1、28、53、87、216、393、425 |
| `ImageList` | `vImageList` | string (refs) | 调用图片（逗号分隔多张） | blank.png、ItmUVD.png、ItmWater.png、ItmEncDoorPanel.png | blank.png、ItmUVD.png、ItmWater.png、ItmEncDoorPanel.png、ItmEncVent.png、ItmMemStick.png…（共 444 种） |
| `SpriteList` | `vSpriteList` | string (refs) | 大地图小人图片：`部位=图片名`（20=左手、21=右手、22=背部、11=上身） | "14=CreItmHuntingRifleShoulderR.png,20=CreItmHuntingRifleHeldL.png,21=CreItmHuntingRifleHeldR.png"… | "14=CreItmHuntingRifleShoulderR.png,20=CreItmHuntingRifleHeldL.png,21=CreItmHuntingRifleHeldR.png"、"20=CreItm……（共 63 种） |
| `ImageUsage` | `vImageUsage` | string (refs) | 6 位索引（0-based，指向 ImageList）：`地上空,地上满,手上空,手上满,物品栏空,物品栏满`。⚠️ 实测 9 种组合：`0,0,0,0,0,0`（无图）、`1,1,0,0,1,1`（典型物品）等；⚠️ mod 索引可更大（长矛 `0,0,10,10,1,1`，图列表超 6 张，见附录 §B） | "0,0,0,0,0,0"、"1,1,0,0,1,1"、"0,0,0,0,1,1"、"1,0,1,0,1,0" | "0,0,0,0,0,0"、"1,1,0,0,1,1"、"0,0,0,0,1,1"、"1,0,1,0,1,0"、"1,1,0,0,2,2"、"0,1,0,1,0,1"、"1,0,0,0,0,0"…（共 9 种） |
| `Weight` | `fWeight` | float | 重量（**50=不可拾取**的系统物品） | 50、0、0.05、0.25 | 0~1000 |
| `MonetaryValue` | `fMonetaryValue` | float | 基础价值（未辨识时价格） | 0、1、5、0.25 | 0~2690 |
| `MonetaryValueAlt` | `fMonetaryValueAlt` | float | 真实价值（辨识后价格） | 0、50、1、60 | 0~3000 |
| `Durability` | `fDurability` | float | ⚠️ 实测全部=1（0-1 耐久比，原版未用可变值） | 0/1 | 0/1 |
| `DegradePerHour` | `fDegradePerHour` | float | 每小时耐久损耗（0=不损耗） | 0、0.085、0.006、0.0125 | 0、0.0001、0.0002、0.005、0.006、0.008、0.0125、0.02、0.025、0.05、0.085、0.25、0.4、0.5、12、3600 |
| `EquipDegradePerHour` | `fEquipDegradePerHour` | float | 装备时每小时损耗 | 0、0.0001、0.0007、0.1 | 0~2000 |
| `DegradePerUse` | `fDegradePerUse` | float | 每次使用损耗（1=用一次报废） | 0、1、0.01、0.001 | 0、0.00001、0.0001、0.001、0.005、0.01、0.025、0.05、0.09、0.1、0.19、0.2、0.25、0.35、1 |
| `DegradeTreasureIds` | `vDegradeTreasureIDs` | string (refs) | 🔗 引用列 → TreasureTable（37 §4.20，损耗产物） | "3,3"、"31,31"、"11,3"、3 | "3,3"、"31,31"、"11,3"、3、"758,3"、"600,600"、"397,3"、"54,3"、"761,3"、"468,3"、"138,3"、"467,3"、"469,3"、"556,556"…（共 33 种） |
| `EquipConditions` | `aEquipConditions` | string (refs) | 🔗 引用列 → Condition（37 §4.20，`槽位=状态`） | 11=19、"20=34,21=34"、"2=21,2=-210,2=715,3=22,3=-210"、"2=21,2=-210,3=22,3=-210,3=715" | 11=19、"20=34,21=34"、"2=21,2=-210,2=715,3=22,3=-210"、"2=21,2=-210,3=22,3=-210,3=715"、"20=411,21=411"…（共 50 种） |
| `PossessConditions` | `aPossessConditions` | string (refs) | 🔗 引用列 → Condition（37 §4.20） | "=438,=572"、200=458、"20=693,21=693,200=693,207=693,208=693"、=438 | "=438,=572"、200=458、"20=693,21=693,200=693,207=693,208=693"、=438、"200=107,208=107"、"200=25,208=25"、208=214…（共 47 种） |
| `UseConditions` | `aUseConditions` | string (refs) | 🔗 引用列 → Condition（37 §4.20） | "211=-295,211=-296,211=-297,211=-620,211=630,211=631"、"211=36,211=467,100=468,101=468,102=468,104=… | "211=-295,211=-296,211=-297,211=-620,211=630,211=631"、"211=36,211=467,100=468,101=468,102=468,104=468,105=468……（共 68 种） |
| `Capacities` | `aCapacities` | string | 容器容量 `宽x高` | 1x1、3x4、1x2、2x2 | 1x1、3x4、1x2、2x2、3x5、2x1、2x3、3x3、10x1、24x12、4x1、7x1、16x16、16x12、20x29、22x12、33x11、10x10、10x6、16x10、22x29、2x4…（共 33 种） |
| `EquipSlots` | `vEquipSlots` | string | 装备槽位：`槽位[=x=y]`（`20,21`=双手；`11=0=0`=上身+图索引）。⚠️ 37 附录 A 完整解码：x/y=ImageList 索引（恒相等）、vSpriteList 槽位=图名直接匹配 | -1、"21,20"、"20,21"、214 | -1、"21,20"、"20,21"、214、"11=0=0,21,20"、"21=1=1,20=1=1"、"14=0=0,21=2=2,20=2=2"、"2=0=0,3=0=0,20,21"…（共 37 种） |
| `UseSlots` | `vUseSlots` | string | 使用槽：`211`=直接使用；`100-115`=对部位使用（外用/包扎） | 211、"211,100,101,102,104,105,106,107,108,109,111,112,114,115"、"100,101,102,104,105,106,107,108,109… | 211、"211,100,101,102,104,105,106,107,108,109,111,112,114,115"、"100,101,102,104,105,106,107,108,109,111,112,11……（共 3 种） |
| `SocketLocked` | `bSocketLocked` | bool | 1=物品无法从槽位移除（残废图标/对话选项栏） | 0/1 | 0/1 |
| `Properties` | `vProperties` | string (refs) | 🔗 引用列 → ItemProp（37 §4.20） | 86、"1,16,36,37,48,50"、"77,86"、76 | 86、"1,16,36,37,48,50"、"77,86"、76、"1,9,15,28,48,50,75,87"、"8,9,15,28,48,50,87"、"1,16,36,37,48,50,60,88"…（共 224 种） |
| `ContentIDs` | `aContentIDs` | string (refs) | 🔗 引用列 → ContainerType（37 §4.20，能装入的内容类别） | 10、"2,3,8,9,14,19,20,21,22,23,25,29,30,31,33,34,36,37,38"、"2,13,22,23,30,31,33"、"25,26,28" | 10、"2,3,8,9,14,19,20,21,22,23,25,29,30,31,33,34,36,37,38"、"2,13,22,23,30,31,33"、"25,26,28"…（共 44 种） |
| `FormatId` | `nFormatID` | int (refs) | 🔗 引用列 → ContainerType（37 §4.20；自身类别） | 3、4、2、5 | 1~39 |
| `TreasureId` | `nTreasureID` | int (refs) | 🔗 引用列 → TreasureTable（37 §4.20；0=无内含物） | 0、3、433、436 | 0~752 |
| `ComponentId` | `nComponentID` | int (refs) | 🔗 引用列 → TreasureTable（37 §4.20；拆解产物池） | 3、77、374、387 | 3~759 |
| `Mirrored` | `bMirrored` | bool | 镜像（6 个全为鞋子，左右脚共用图） | 0/1 | 0/1 |
| `SlotDepth` | `nSlotDepth` | int | 多件装备叠放顺序 | 0、2、1、3 | 0、1、2、3 |
| `StackLimit` | `nStackLimit` | int | 最大堆叠数（1=不可堆叠） | 1、5、3、2 | 1、2、3、4、5、6、7、10、12、20、30、100 |
| `ChargeProfiles` | `strChargeProfiles` | int (refs) | 🔗 引用列 → ChargeProfile（37 §4.20） | 25、23、24、27 | 2、3、22、23、24、25、26、27、28、29、30、32 |
| `AttackModes` | `aAttackModes` | string (refs) | 🔗 引用列 → AttackMode（37 §4.20，`槽位=攻击`） | "20=11,21=11"、"20=2,20=3,20=6,21=2,21=3,21=6"、"20=20,21=20"、"20=26,20=27,20=28,21=26,21=27,21=28" | "20=11,21=11"、"20=2,20=3,20=6,21=2,21=3,21=6"、"20=20,21=20"、"20=26,20=27,20=28,21=26,21=27,21=28"…（共 36 种） |
| `SwitchIds` | `aSwitchIDs` | string (refs) | 🔗 引用列 → ItemType（37 §4.20，`状态名=物品`；⚠️ 状态名原版 4 种 On/Off/Open/Close，mod 为自由文本，见附录 §B） | Off=8.12、Off=8.8、"Close=8.2,On=8.3"、"Close=8.5,On=8.6" | Off=8.12、Off=8.8、"Close=8.2,On=8.3"、"Close=8.5,On=8.6"、Off=102.0、Off=34.0、Off=8.0、Off=8.1、Off=8.10、Off=8.14…（共 22 种） |
| `Sounds` | `aSounds` | string | 拾取/放下音效 cue（逗号分隔） | "cuePickup,cuePutdown"、"cueCloth,cueClothesEnd"、"cueSmallPlasticItemPickUp,cuePlasticPutdown7"、"cu… | "cuePickup,cuePutdown"、"cueCloth,cueClothesEnd"、"cueSmallPlasticItemPickUp,cuePlasticPutdown7"、"cueMeatPickup……（共 75 种） |

---

## 21. Map（地图）— `maps`（2 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 序列编号 | 1、2 | 1、2 |
| `Name` | `strName` | string (refs) | 地图标识：`Excel50x100`（内部网格地图）、`MapMiniMichigan.png`（小地图图片） | Excel50x100、MapMiniMichigan.png | Excel50x100、MapMiniMichigan.png |
| `Definition` | `strDef` | string (refs) | 🔗 引用列 → HexType（37 §4.21；逗号分隔的网格定义，2 行分别 5901/12509 格 ≈ 50×100+ 和 100×125+） | "0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0… | "0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,……（共 2 种） |

---

## 22. Recipe（配方/合成表）— `recipes`（105 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `nID` | int | 序列编号 | 1、2、3、4 | 1-105 连续 |
| `Name` | `strName` | string | 配方名称（汉化）：`一串烤肉`、`中型篝火(点燃)` | 分析过的水、带瞄准镜的.308步枪、可背带的配有瞄准镜的4毫米高斯步枪、通过营火熏制的肉块 | 分析过的水、带瞄准镜的.308步枪、可背带的配有瞄准镜的4毫米高斯步枪、通过营火熏制的肉块、一串烤肉、一串烤肉（中等）、中型带毛皮尸体的毛皮和肉、4毫米高斯步枪、拆解底特律无人机残骸、从营火熏制过的肉条、粗制贯穿箭…（共 96 种） |
| `SecretName` | `strSecretName` | string | 隐藏名称（区分人肉/动物肉、分析过的水） | 中小型带毛皮尸体的毛皮和肉 (动物)、中型带毛皮尸体的毛皮和肉(动物)、从营火熏制过的肉条(动物)、大型带毛皮尸体的肉(动物) | 中小型带毛皮尸体的毛皮和肉 (动物)、中型带毛皮尸体的毛皮和肉(动物)、从营火熏制过的肉条(动物)、大型带毛皮尸体的肉(动物)、大型尸体上的肉(动物)、断裂的树枝(仅反向)、分析过的水(生物污染)…（共 29 种） |
| `Tools` | `strTools` | string (refs) | 🔗 引用列 → Ingredient（37 §4.22，`数量x材料`） | 1x22、1x14、1x70+1x7、1x1 | 1x22、1x14、1x70+1x7、1x1、1x14+1x16、1x16+1x7+1x17、1x7+1x17、1x87、1x11、1x14+1x51、1x6+1x7、1x40+1x22+1x35、1x121+1x122…（共 28 种） |
| `Consumed` | `strConsumed` | string (refs) | 🔗 引用列 → Ingredient（37 §4.22） | 1x2、1x127、1x65、1x75 | 1x2、1x127、1x65、1x75、1x86、10x12+20x13、1x10+1x27、1x10+1x27+2x48、1x10+2x42、1x100+1x18+2x21、1x100+1x20+1x18+4x21…（共 97 种） |
| `Destroyed` | `strDestroyed` | int (refs) | 🔗 引用列 → Ingredient（37 §4.22）。⚠️ 实测仅 3 条（粗制/精致火把、桃木棒点燃时），值恒 2 = 摧毁自身 | 2 | 2 |
| `TreasureId` | `nTreasureID` | int (refs) | 🔗 引用列 → TreasureTable（37 §4.22，合成产物池） | 20、10、74、481 | 8~763 |
| `Hours` | `fHours` | float | 合成消耗行动值（0-1） | 0.1、1、0.01、0.25 | 0、0.01、0.05、0.1、0.15、0.2、0.25、0.3、0.4、0.5、0.6、1 |
| `Reverse` | `nReverse` | int | ⚠️ 0=不可拆解、1=可拆回材料（简单组装）、2=可拆回组件（复合装备） | 0、1、2 | 0、1、2 |
| `HiddenId` | `nHiddenID` | int (refs) | 🔗 引用列 → Recipe（37 §4.22；0=非隐藏） | 0、82、3、4 | 0、3、4、6、16、56、57、63、64、82、100 |
| `Identify` | `bIdentify` | bool | 1=需辨识材料（水需水分析器等） | 0/1 | 0/1 |
| `TransferComponents` | `bTransferComponents` | bool | 1=材料属性转移给产物（语义待探索） | 0/1 | 0/1 |
| `AlsoTry` | `vAlsoTry` | string (refs) | 🔗 引用列 → Recipe（37 §4.22） | 101、71、72、73 | 101、71、72、73、74、75、76、77、78、"79,80,81" |
| `TempTreasureId` | `nTempTreasureID` | int (refs) | 🔗 引用列 → TreasureTable（37 §4.22；合成虚影预览池） | 3、482、676 | 3、482、676 |
| `DegradeOutput` | `bDegradeOutput` | bool | 1=产物耐久与材料关联，0=固定耐久 | 0/1 | 0/1 |
| `Type` | `strType` | string | 配方分类（⚠️ **自由分类文本**，非固定枚举）：原版 `食物/医务/工具/武器/载具/杂项`；mod 用英文 `weapon/food/tool/misc/gear/aid/Zcheat`（见附录 §B） | 食物、工具、武器、杂项 | 食物、工具、武器、杂项、医务、载具 |
| `Scrap` | `bScrap` | bool | ⚠️ 实测全部=1（20 文档称"是否可分解"，原版恒真，语义待探索） | 0/1 | 0/1 |

---

## 23. TreasureTable（战利品池）— `treasuretable`（764 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 战利品池编号 | 1、2、3、4 | 1-764 连续 |
| `Name` | `strName` | string | 池描述（汉化） | 一个粗糙的火炬 (点燃)、一个精致的火炬 (燃烧着)、.308步枪内含物、.38 手枪部件 | 一个粗糙的火炬 (点燃)、一个精致的火炬 (燃烧着)、.308步枪内含物、.38 手枪部件、.45 手枪部件、“新世界陶片”手稿、1-2 脏布和 1-2 干净的布、10块脏布和4只鞋子和裤子和纯净水和小吃…（共 762 种） |
| `Treasures` | `aTreasures` | string (refs) | 🔗 复合列 → 37 §5.2（`物品x概率x数量`，`\|`=OR、`,`=AND，双目标） | 10.3x0.25x1-20、101.24x1x2-2、"11.4x1x1-1,98.1x1x2-2"、13.1x1x1-1 | 10.3x0.25x1-20、101.24x1x2-2、"11.4x1x1-1,98.1x1x2-2"、13.1x1x1-1、18.2x1x1-1、90.23x1x1-1…（共 756 种） |
| `Nested` | `bNested` | bool | 1=生成物品装进同时生成的容器 | 0/1 | 0/1 |
| `Suppress` | `bSuppress` | bool | 1=抑制内容物生成（枪无子弹、瓶无水） | 0/1 | 0/1 |
| `Identify` | `bIdentify` | bool | 1=生成的物品已辨识 | 0/1 | 0/1 |

---

## 24. BarterHex（交易商店）— `barterhexes`（3 行）

| 模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域 |
| --------- | -------- | ------ | ------ | ------ | --------- |
| `Id` | `id` | int | 序列编号 | 1、2、3 | 1、2、3 |
| `X` | `nX` | int | X 坐标 | 26、57、58 | 26、57、58 |
| `Y` | `nY` | int | Y 坐标 | 102、192、194 | 102、192、194 |
| `Buys` | `bBuys` | bool | 1=收购玩家物品 | 0/1 | 0/1 |
| `RestockTreasureId` | `nRestockTreasureID` | int (refs) | 🔗 引用列 → TreasureTable（37 §4.24，⚠️ 20 文档未标引用） | 3、23、558 | 3、23、558 |

---

## 附：与 20 文档/Help 文档的主要差异汇总（2026-08-02 实测）

| # | 表 | 字段 | 20/Help 文档说法 | 实测结论 |
|---|----|------|----------------|---------|
| 1 | conditions | `bPermanent` | "是否长期影响（不会自动消失）" | ⚠️ **1=瞬时效果**（吃/喝/一次性状态，dur=0） |
| 2 | conditions | `aFieldNames` | 74 种 | ⚠️ 实测 **78 种**（新增 WoundCut/WoundBruise/fFatigueModifier/m_fMoveCost） |
| 3 | containertypes | `strName` | 仅 6 种（防水/精/粗/事件/技能/营地） | ⚠️ 实测 **39 种**（弹药/电池/滤芯等类别名） |
| 4 | chargeprofiles | `fPerUse/fPerHour` | 无负值说明 | ⚠️ **负值=充电/补充**（id28/30 实测） |
| 5 | battlemoves | `bInAttackRange` | bool | ⚠️ 实测 0-3 三值（3=窃眼特例） |
| 6 | battlemoves | `vChanceType` | "未知" | ⚠️ 三值格式 `0,距离档,概率系数`；`0,7,0`=潜行类 |
| 7 | encountertriggers | `bUnique` | bool | ⚠️ 实测 0-2 三值（2=剔骸之谷特例） |
| 8 | encounters | `nType` | 0=普通、1=搜刮 | ⚠️ 实测 4 值：0=普通、1=搜刮、2=战斗（1 条）、3=破解（6 条） |
| 9 | encounters | `aMinimapHexes` | "小地图显示点" | ⚠️ 格式 `x,y=标签[=flag]`，flag 0/1 语义待探索 |
| 10 | factions | `strName` | 3=食人族等 | ⚠️ 实测：3=摇滚帮、5=魔迦怨灵、8=鹿、9=夜辛卡…全部 14 阵营确认 |
| 11 | itemtypes | `nCondID` | "辨识需要的状态ID" | ⚠️ **1=空状态（无条件）**占 468/537；映射确认：87=远程/53=医学/216=电工/393=植物/425=黑客/28=通行证 |
| 12 | itemtypes | `fDurability` | "耐久性（1=100%）" | ⚠️ 实测**恒 1**，原版未用可变值 |
| 13 | hextypes | `nTerrainCost` | 行动力 | ⚠️ 11=不可通行标记（海洋/海滨/山地） |
| 14 | recipes | `bScrap` | "是否可分解" | ⚠️ 实测恒 1 |
| 15 | itemtypes | `Weight` | 重量 | ⚠️ 50×130 为不可拾取系统物品标记 |
| 16 | battlemoves | `strID` | "物品编号" | ⚠️ 实测 float 90.1-90.95（90 组=战斗 UI 类物品） |

## 附：各表行数与字段数速查

| 表 | 行数 | 字段数 | 表 | 行数 | 字段数 |
|----|-----:|-----:|----|-----:|-----:|
| attackmodes | 61 | 16 | factions | 14 | 3 |
| barterhexes | 3 | 5 | forbiddenhexes | 16 | 4 |
| battlemoves | 63 | 34 | gamevars | 19 | 3 |
| camptypes | 14 | 10 | headlines | 48 | 2 |
| chargeprofiles | 32 | 8 | hextypes | 37 | 16 |
| conditions | 872 | 21 | ingredients | 128 | 4 |
| containertypes | 39 | 2 | itemprops | 108 | 2 |
| creatures | 28 | 13 | itemtypes | 537 | 37 |
| creaturesources | 32 | 8 | maps | 2 | 3 |
| datafiles | 88 | 5 | recipes | 105 | 17 |
| dmcplaces | 7 | 5 | treasuretable | 764 | 6 |
| encounters | 2264 | 24 | encountertriggers | 133 | 13 |

---

## 附录：mod 数据交叉验证（2026-08-02）

> 数据源：`Mods/NeoScavExtended/` 下 7 个命名空间（NSEg/NSEb/NSEf/NSEa/NSE/NSEoverride/NSEtT）+ `Mods/Cromzst/测试`，共 8 组 mod 数据（NSEoverride 为分段式 `data/*.xml`，其余为 `neogame.xml` 合并式）。
> 方法：全表解析 + 与主数据逐字段值域对比 + **引用列交叉命中验证**。

### §A. 命名空间前缀语义订正（⚠️ 重要）

**结论（用户指正 + 数据 + 代码三方证实）**：

| 写法 | 语义 | 示例 |
|------|------|------|
| `无前缀` | **同 sourceNs**（源实体所属命名空间） | NSE 实体中 `211` → ns=NSE |
| `0:` | **显式指向 0 命名空间（game base）**，⚠️ 不是"同 ns 简写" | NSE 中 `0:5.6` → 原版 5.6「存储」（NSE 自己的 5.6 是「水袋」，不同物品） |
| `NSE:` | 显式指向 NSE 命名空间 | `NSE:1` → NSE 的 faction 1（=DMC 警卫，原版 faction 1=狗人） |
| `:`（空前缀） | 同 sourceNs（实际数据 0 出现，仅理论形式） | — |

**实证**：
- NSE `itemtypes 5.1 包`：`Shrink Back=0:5.6` → 原版 5.6「存储」✅（NSE 5.6=「水袋」，若按"同 ns"解析语义错误）
- NSE `46.1 工具`：`Open=0:46.0` → 原版 46.0「刀具」（NSE 无 46.0）✅
- NSEf `ChangeGlobalFactionRep=0:2,-100,1` → 原版 faction 2=掠夺者（NSE faction 2=清道夫）✅
- 代码 `ReferenceResolver.LookupEntityId`：`0:38` → `LookupByNs(type, "0", pk)` 直接查 0 命名空间 ✅

**⚠️ 与既有文档冲突**：R16 §1/§5 与 37 §0.3 写「`0:` 是『同 namespace』简写，不是 game base」——**表述错误**，实际 `0:` 就是显式 0（game base）命名空间。代码行为正确，文档需订正（已列入待办）。

**图片引用同样支持命名空间前缀**（37 未覆盖，原版无此用法，mod 使用）：
- `attackmodes.strIMG`：NSEb `0:AModeSpearSharp.png`（引用原版图片）
- `datafiles.strImg`：NSEoverride `NSE:ItmDataAddr.png`（引用 NSE 图片）
- `itemtypes.vImageList`：NSEb `0:ItmSpearSlot100.png` 混排在无前缀图片中
- `creatures.strImg`：NSE `0:CreHuman.png`、NSEoverride `NSE:CreSquirrel.png`
- `itemtypes.vEquipSlots` 的 `=x=y` 图索引指向的 vImageList 条目同样可带前缀

**aEffects 参数实体同样带命名空间前缀**（37 §5.3 需补充）：
- `SetImmunity=0:316,0:618,463`（NSE cond 2：免疫列表混用 0: 前缀与无前缀）
- `ChainCondition=332,0:736`、`ChainCondition=NSE:-457`（NSEoverride cond 3 引用 NSE 状态）
- `AddItemGround=0:17,0,0,0`（NSEa cond 42：池 id 带前缀）
- `ChangeGlobalFactionRep=NSE:1,-100,1`（faction 参数带前缀；且 NSEf cond 8 一条 `ChangeGlobalFactionRep` 含 **7 组** `ns:派系,值,flag` 用 `;` 并列）

### §B. mod 数据扩展的值域（原版值域 → mod 扩展）

| 表.字段 | 原版 | mod 扩展 | 说明 |
|--------|------|---------|------|
| attackmodes.nPenetration | 0-3 | **0-5** | 穿透等级开放取值 |
| battlemoves.nSeeThem/nSeeUs | 0-2 | **-1**（威胁类动作：NSEb「威胁」「用信号弹威胁」「用桃木棒威胁」） | -1=无需看见对方 |
| battlemoves.vChanceType | 档位 1/2/7 | **档位 6**（NSE「擒拿」「扼杀」） | 6=擒拿/扼杀类 |
| conditions.nTransferRange | -1~4 | **-1~30** | 传染距离可更大 |
| creatures.nMovesPerTurn | 3-8 | **1-8** | 行动点数更低 |
| encounters.fPrice | 0-5600 | **0-7600** | 价格开放取值 |
| itemtypes.nCondID | 1/28/53/87/216/393/425 | **0**（NSEa 食物/瓶子、NSEoverride 液体） | ⚠️ **0 与 1 都表示"无条件辨识"**（1=condition id=1 空状态；0=直接写 0，mod 两种都用） |
| recipes.fHours | 0-1 | **0.01-12** | 合成耗时可超过 1 |
| recipes.strType | 中文 6 类（食物/医务/工具/武器/载具/杂项） | **英文**：weapon/food/tool/misc/gear/aid/Zcheat（NSEtT） | ⚠️ **strType 是自由分类文本**，非固定枚举（中文/英文混用，NSEb 恒 weapon、NSEf 恒 gear） |
| camptypes.m_fAlertness | 0-0.4 | **0-1**（0.52/1） | 警戒值开放取值 |
| camptypes.fSleepQuality | -0.36~0.18 | **-3~0.18** | 睡眠质量负值更大 |
| encountertriggers.dateMax | 9999-11-31-20/21/23/6 | **9999-11-31-7/4** | 时间值更多样 |
| datafiles.strName | PDF/图像/数据库/文本/视频 | **地址簿/邮件文件/PDF文档** | 类型自由文本 |
| containertypes.strName | 39 种 | **+33 种**（鸟蛋/香烟/瓶子/枪套/弹匣/绳索等） | 类别自由文本 |
| itemtypes.vImageUsage | 索引 0-3 | **索引 10**（NSEb 长矛 `0,0,10,10,1,1`） | ⚠️ 索引指向 vImageList 条目，可>图片使用位置数（图列表可超 6 张，如长矛 11 张图） |
| itemtypes.aSwitchIDs | 状态名 On/Off/Open/Close | **Hood Off/Hood On/Shrink Back** 等 | ⚠️ **状态名是自由文本**（37 附录 C 全集 4 种仅原版） |
| battlemoves.strNotes | (blind)/AI version 等英文 | **中文说明**（隐蔽/瞄准射击/俯卧/火把） | 注释自由文本 |

### §C. 引用列交叉命中验证（37 项1 的补充证据）

**37 §2.4 项1「aTreasures 未命中复合键」（107 个唯一 G.S）**：在主数据 + NSEoverride + NSExtended + NSEaid 等全部 mod 的 itemtypes（合计 947 个 G.S 键）中**全部未命中**（7.33-7.36 / 9.29-9.30 / 12.2-12.14 / 36.1-36.21 在 mod 中也不存在）。
→ 结论升级：这些 G.S 是**游戏版本移除的旧物品**（37 推测 🟡 → ✅ 基本证实），不是指向 mod 数据。

**NSEoverride（ns=0）覆盖机制实证**：NSEoverride 的 361 条 itemtypes **全部**是覆盖原版（361/361 与原版 id 交集，0 条新增），且覆盖项直接改 `strName`（汉化）——与 R16 §3 `INSERT OR REPLACE` 机制一致；加载顺序靠 getmods.php 顺序保证。

### §D. 各 mod 数据规模速查

| 命名空间 | 目录 | 表数 | 行数合计 | 备注 |
|---------|------|-----:|--------:|------|
| NSEg | NSEgame | 4 | 188 | conditions/encounters/itemtypes/treasuretable |
| NSEb | NSEbattle | 9 | 562 | attackmodes/battlemoves/recipes |
| NSEf | NSEfashion | 6 | 662 | itemtypes/recipes/conditions |
| NSEa | NSEaid | 7 | 976 | itemtypes/conditions/recipes |
| NSE | NSExtended | 20 | 2871 | 核心 mod，全表 |
| 0 | NSEoverride | 17 | 205 | 覆盖原版（ns=0），含 datafiles 引用 NSE 图片 |
| NSEtT | NSEtestTools | 6 | 71 | 测试工具（strType=Zcheat） |
| （用户） | Cromzst/测试 | 1 | — | 用户自建 mod |
