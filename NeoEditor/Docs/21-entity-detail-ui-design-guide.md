# Entity Detail UI 设计指南

> 适用：`IEntityVisualizer.BuildDetail()` 和 `BuildOverview()` 的 UI 设计参考
> 更新：2026-06-11 · 基于 25 个已实现 visualizer 的设计模式总结 + 引用解析规范

---

## 一、Detail 总体布局规范

```
┌─ ScrollViewer ────────────────────────────────────────────┐
│ StackPanel (Spacing=16, Margin=16)                        │
│                                                           │
│  ┌─ Raw Data Expander (折叠, 默认关闭) ──────────────────┐ │
│  ├─ Hero Header Card ────────────────────────────────────┤ │
│  ├─ 主要数据面板 (Combat/Stats/Modifiers...) ────────────┤ │
│  ├─ 正向引用面板 (Refs Panel) ───────────────────────────┤ │
│  ├─ 反向引用面板 (Reverse Refs Panel) ───────────────────┤ │
│  └─ 文本/描述面板 ───────────────────────────────────────┘ │
└───────────────────────────────────────────────────────────┘
```

### 布局规则

| # | 规则 | 说明 |
|---|------|------|
| 1 | **根容器** | `ScrollViewer` + `HorizontalScrollBarVisibility=Disabled` |
| 2 | **间距** | `StackPanel Spacing=16`, `Margin=16` |
| 3 | **Raw Data 必须在最顶部** | 始终折叠（`BuildExpander` + `Border IsVisible=false`），是所有 Detail 的第一个子元素 |
| 4 | **Hero Header 第二** | Raw Data 之后紧跟 Hero Header |
| 5 | **面板按信息优先级排列** | 核心属性 → 引用 → 文本/描述 |
| 6 | **空值面板不渲染** | 数据为空或默认值时，跳过整个面板（不要渲染空卡片） |
| 7 | **可变面板按条件渲染** | `if (!string.IsNullOrWhiteSpace(...)) root.Children.Add(...)` |

---

## 二、Hero Header 设计模式

### 标准布局（有图片字段的类型）

```
┌─ Card ────────────────────────────────────────────────────┐
│ Grid: [140px 图片区 | * 身份区]                            │
│                                                           │
│  ┌──────────┐  ┌─ ID: 42 ───┐ ┌─ Type Badge ─┐           │
│  │          │  │            │ │              │           │
│  │  Image   │  │  Name      │ │ Flag1 · Flag2│           │
│  │ 132×132  │  │  18pt Bold │ └──────────────┘           │
│  │          │  │            │                             │
│  │          │  │  Subtitle  │  Stat1 · Stat2 · Stat3      │
│  │          │  │  italic    │  (FontSize 11, #666)        │
│  └──────────┘  │            │                             │
│                │  Notes     │                             │
│                │  #888      │                             │
│                └────────────┘                             │
└───────────────────────────────────────────────────────────┘
```

### 无图片字段类型的 Header

```
┌─ Card ────────────────────────────────────────────────────┐
│ Grid: [* 身份区]                                          │
│                                                           │
│  ┌─ ID: 42 ───┐ ┌─ Severity ──┐ ┌─ Tag3 ──┐              │
│  Name (18pt Bold)                                         │
│  Duration · Color · Transfer · ResetTimer                 │
│  Thresholds / ChanceNext                                  │
└───────────────────────────────────────────────────────────┘
```

### Header 组件清单

| 组件 | 样式 | 何时使用 |
|------|------|---------|
| ID Badge | `CornerRadius=4, Bg=#E3F2FD, Fg=#1565C0, Bold` | 总是显示 |
| Type Badge | 彩色圆角徽章，含图标 (SymbolIcon) | 有分类/枚举字段时 |
| Flag Capsule | 灰色背景圆角标签，`·` 分隔 | bool 标记字段多个激活时 |
| StrId Badge | `Bg=#F3E5F5, Fg=#6A1B9A` | 有 `StrId` 字段时 |
| Name | `FontSize=18, FontWeight=Bold, TextWrapping=Wrap` | 总是显示 |
| Quote/Phrase | `FontSize=12, FontStyle=Italic, Fg=#666` | WieldPhrase / 引用文本 |
| Stat Row | `StackPanel Horizontal, Spacing=12, FontSize=11, Fg=#666` | 精选关键指标 3-6 个 |
| Image | `132×132, CornerRadius=10, Stretch=Uniform` | 有 `Image` / `ImageList` 字段且找到图片 |
| Image Fallback | `SymbolIcon 40px, Fg=#999` 或 `TextBlock "??"` | 无图片或图片加载失败 |

### 图片加载逻辑

```csharp
// 1. 尝试加载图片
var bmp = VisHelper.LoadImage(entity.Image);
// 2. 有图片 → Image 控件；无图片 → 按实体类型选合适的 SymbolIcon
// 3. 始终包裹在 Border(Width=132,Height=132,CornerRadius=10,ClipToBounds=true,Bg=#0A000000) 内
```

---

## 三、数据面板设计模式

### A. StatBar 进度条面板（AttackMode Combat 等）

```
┌─ Card ────────────────────────────────────────────────────┐
│  🎯 Melee Combat  (Header: SymbolIcon + label)            │
│                                                           │
│  Range     ████████████████░░░░  25 tiles                 │
│  Cut       ██████████████░░░░░░  3.5                      │
│  Blunt     ██████████░░░░░░░░░░  1.2                      │
│  Dmg Bonus ██████████░░░░░░░░░░  +0.25 (base)              │
│                                                           │
│  Penetration  ●●●○○  Lv.3                                 │
│  Sound        ▶ Rifle                                     │
│  Transfer     Arrows stay on target                       │
└───────────────────────────────────────────────────────────┘
```

**StatBar 实现**: `VisHelper.StatBar(label, valueText, fillRatio, colorHex)`
- `fillRatio` 自动 clamp 到 `[0.05, 1.0]`
- 内部用 Grid 三列（80px label | fill* | empty*）实现
- 进度条文字白色显示在填充区域内

### B. StatCard 键值对面板（Overview 主力）

```
┌─ Card ──────────────────────────┐
│  Range          80 tiles        │
│  Cut Dmg        2.5             │
│  Blunt Dmg      1.2             │
│  Total          3.7             │
│  Dmg Bonus      +0.25 (base)    │
└─────────────────────────────────┘
```

**实现**: `VisHelper.BuildStatCard(rows)` — 接受 `List<(string label, string value, string? color)>`

### C. MiniBadge 引用徽章面板

```
┌─ Card ────────────────────────────────────────────────────┐
│  Ammo (3 types)              ← SectionLabel                │
│  ┌──────────┐ ┌────────┐ ┌──────────┐                     │
│  │ .308 Win │ │ 12 Gauge│ │ 5.56 NATO│    ← MiniBadge     │
│  └──────────┘ └────────┘ └──────────┘                     │
│                                                           │
│  Ctrl+Click 跳转                                           │
└───────────────────────────────────────────────────────────┘
```

**MiniBadge 签名**: `VisHelper.MiniBadge(text, bg, fg, onClick?)`
- 有 onClick 时：`Cursor=Hand`，Ctrl+Click 触发跳转
- 无 onClick 时：纯展示徽章

**标准配色**:

| 引用目标 | bg | fg |
|---------|-----|-----|
| ChargeProfile | `#E0F7FA` | `#006064` |
| Condition (正面/必需) | `#E8F5E9` | `#2E7D32` |
| Condition (负面/伤害) | `#FCE4EC` | `#C62828` |
| Condition (中性) | `#E8EAF6` | `#283593` |
| AttackMode | `#FFEBEE` | `#C62828` |
| Creature | `#E8EAF6` | `#283593` |
| Faction | `#FFF3E0` | `#E65100` |
| ItemType | `#E3F2FD` | `#1565C0` |
| TreasureTable (正) | `#E8F5E9` | `#2E7D32` |
| TreasureTable (负) | `#FFEBEE` | `#C62828` |
| Recipe | `#F3E5F5` | `#6A1B9A` |
| Encounter | `#FFF3E0` | `#E65100` |
| ItemProp | `#E8F5E9` | `#2E7D32` |
| 解析失败/未知 | `#F5F5F5` | `#999` |

### D. 文本面板

```
┌─ Card ────────────────────────────────────────────────────┐
│  "You ready the rifle, feeling its weight in your         │
│   hands. The cold steel barrel gleams in the dim          │
│   light of the wasteland..."                              │
│                                                           │
│  FontSize=11, Fg=#555, TextWrapping=Wrap                  │
│  超过 400/800 字符时截断                                    │
└───────────────────────────────────────────────────────────┘
```

### E. 外交关系横条面板（Faction 专用）

```
┌─ Card ────────────────────────────────────────────────────┐
│  Bad Muthas       ████████████████░░░░  66 (Friendly)     │
│  Merga Cult       ██████░░░░░░░░░░░░░░  25 (Neutral)      │
│  DMC Guards       ████████████████████  -80 (Enemy)       │
└───────────────────────────────────────────────────────────┘
```

### F. 配对表面板（Condition Modifiers）

```
┌─ Card ────────────────────────────────────────────────────┐
│  Field Name        →  Modifier                            │
│  ─────────────────     ─────────────────────────────────  │
│  m_fMoveCost       →  +0.5                                │
│  m_fVisibility     →  -0.2                                │
│  ...                                                      │
└───────────────────────────────────────────────────────────┘
```

### G. 反向引用面板

```
┌─ Card ────────────────────────────────────────────────────┐
│  Referenced by (12)                                       │
│  ┌─ Creature ───┐  Dogman                                 │
│  ┌─ ItemType ───┘  Remington 700                          │
│  ┌─ ItemType ───┘  Hunting Knife                          │
│  ... + 8 more                                             │
└───────────────────────────────────────────────────────────┘
```

**实现细节**:
- 每个条目是一个可点击的 Row（`Cursor=Hand`, Ctrl+Click 跳转）
- Row 由类型标签 + 实体名称组成
- 超过 8-20 条时截断，显示 `+ N more`
- 使用 `ReferenceResolver.FindReverseReferences(entityType, entityId)`

---

## 四、Overview 设计模式

### 徽章条（所有 Overview 统一强制）

每个实体的 Overview 顶部都有 **ValueEditorPanel 自动包装的徽章条**，显示四类信息：

```
┌─ 徽章条 (bg=#0D000000, pad=8/4) ───────────────────────────┐
│  Entity Name  [5:MyMod]  [mid=1204]  [pk=3]  [a1b2c3d4e5] │
│  (14pt Bold)  (蓝/绿)    (橙色)      (紫色)  (深灰,10chars)│
└─────────────────────────────────────────────────────────────┘
```

| 徽章 | 颜色 | 格式 | 含义 |
|------|------|------|------|
| ModId:ModName | 蓝 `#1565C0` (Game/modId<10000) / 绿 `#1B5E20` (modId≥10000) | `5:MyMod` | 来源 mod |
| MergedId | 橙 `#E65100` | `mid=1204` | 合并后 ID，引用匹配键 |
| 主键 Id | 紫 `#6A1B9A` | `pk=3` | 数据库主键 (Id 或 nID) |
| EntityId | 深灰 `#37474F` | `a1b2c3d4e5` | 全局唯一实体 ID 前10位 |

**实现位置**：`ValueEditorPanel.Show()` 方法统一包装，不依赖各类型 `BuildOverview` 自行实现。因此所有实体类型（包括自定义可视化器的 ItemType、Recipe 等）自动获得一致的徽章显示。EditorTitle 也同步显示格式：`[mod=5:MyMod mid=1204 pk=3 eid=a1b2c3d4]`。

**用途**：对比浏览器视图和合并视图中同一实体的 mid 和 pk 是否一致，快速诊断引用解析 Bug。

### 布局规范

```
┌─ 260px 宽 ──────────────────────┐
│ StackPanel (Spacing=10, Margin=8)│
│                                 │
│  ┌─ Image (72×72, 居中) ──────┐ │
│  ├─ Type Badge (居中) ────────┤ │
│  ├─ Name (14pt Bold, 居中) ───┤ │
│  ├─ Subtitle / Quote ─────────┤ │
│  ├─ Separator ────────────────┤ │
│  ├─ OvSectionLabel("Stats") ──┤ │
│  ├─ BuildStatCard(...) ───────┤ │
│  ├─ Separator ────────────────┤ │
│  ├─ OvSectionLabel("Ammo") ───┤ │
│  └─ Card(WrapPanel Badges) ───┘ │
└─────────────────────────────────┘
```

**Overview 原则**:
- 窄高布局，适应侧边面板（~260px）
- 缩略图 72×72（Detail 是 132×132）
- 所有元素居中对齐
- 图片 + 类型标签 + 名称 必须在最顶部
- 用 `Separator()` + `OvSectionLabel()` 分区
- 优先使用 `BuildStatCard` 而非 `StatBar`

---

## 五、引用处理规范

### 引用解析优先级

```
1. LookupRef<T>(entity, propName, rawValue)  — 走 ReferenceField attribute，通过 ReferenceIndex 解析
2. ReferenceResolver.Instance.LookupSubject(...) — DataGrid 列渲染用，纯索引
3. 解析失败 → 显示原始文本的灰色 MiniBadge
```

### 引用面板何时展示

| 条件 | 处理 |
|------|------|
| 字段为 null/空 | 不展示面板 |
| 字段为默认值 (如 "0", "1", "3", "3,3") | 不展示（游戏约定：0=无, 1=默认, 3=无） |
| 引用的实体未找到 | 展示灰色 MiniBadge，文本为原始值 |
| 引用的实体已找到 | 展示彩色 MiniBadge + onClick 跳转 |

### 引用导航

```csharp
// 统一跳转入口（通过 IReferenceResolver）
ReferenceResolver.Instance.NavigateTo(typeof(TargetType), entityId);
ReferenceResolver.Instance.NavigateToByKey<TargetType>(keyValue, sourceEntity);
```

---

## 六、VisHelper 共享组件 API

| 组件 | 签名 | 用途 |
|------|------|------|
| `Card(content)` | `Control → Border` | 卡片容器：`CornerRadius=8, Bg=#08000000, Border=#18000000, Padding=14` |
| `SectionLabel(text)` | `string → TextBlock` | 区块标题：`FontSize=11, SemiBold, Fg=#888` |
| `OvSectionLabel(text)` | `string → TextBlock` | Overview 标题：`FontSize=10, SemiBold, Fg=#888` |
| `Separator()` | `→ Border` | 分隔线：`Height=1, Bg=#18000000` |
| `MiniBadge(text, bg, fg, onClick?)` | `→ Border` | 引用徽章：`CornerRadius=9, FontSize=10` |
| `StatBar(label, value, ratio, color)` | `→ Grid` | 进度条 |
| `BuildStatCard(rows)` | `List<(string,string,string?)> → Control` | 键值对卡片 |
| `BuildExpander(label, body)` | `(string, Border) → Border` | 折叠面板 |
| `BuildRawDataTable(entity)` | `IEntity → Control` | 全字段原始数据表 |
| `LoadImage(name?)` | `string? → Bitmap?` | 图片加载（自动处理命名空间前缀） |
| `StripNs(name)` | `string → string` | 去除 `NS:filename` 前缀 |

---

## 七、既定改进方案（2026-06-10）

以下为对现有 Detail UI 的改进设计，已记录供后续实现参考。

### P1 — 反向引用面板（部分类型缺失）

**现状**: ItemType、Ingredient、ItemProp 已有反向引用面板；AttackMode、Creature、Encounter、TreasureTable 等缺失。

**目标**: 所有有被引用关系的实体类型都应展示 "Referenced by" 面板。

**AttackMode 反向引用示例**:
```
Referenced by (5 items, 3 creatures)
┌─ ItemType ───┐ Remington 700
┌─ ItemType ───┘ Hunting Knife
┌─ Creature ───┐ Dogman
┌─ Creature ───┘ Merga Raider
+ 4 more...
```

**实现要点**:
- 利用 `ReferenceResolver.FindReverseReferences(typeof(AttackMode), entityId)`
- 按引用类型分组显示数量摘要
- 每行可 Ctrl+Click 跳转到引用源

### P2 — 引用徽章内联展开

**目标**: 引用徽章点击后可在原地展开显示被引用实体的关键属性，无需跳转即可了解详情。

**交互设计**:
```
点击前:  ┌─ .308 Round ─┐  ┌─ Bleeding x1.0 ─┐

点击后:  ┌─ .308 Round ──────────────────────┐
         │  Per Use: 1.0   Per Hour: 0.0      │  ← 展开的详情卡片
         │  Degrade: Yes   Per Hex: 0.0       │
         │  [Ctrl+Click to open full detail]  │
         └────────────────────────────────────┘
```

**实现思路**:
- MiniBadge 点击切换展开/折叠
- 展开内容是一个嵌套的小 Card，通过 `IsVisible` 控制
- 需要为每种引用目标类型准备一个 `BuildInlinePreview<T>(T entity)` 方法
- 展开卡片中包含 `Ctrl+Click to open` 提示

### P3 — 伤害堆叠条可视化

**现状**: AttackMode 的 Cut/Blunt 是独立进度条，无法看伤害构成比例。

**目标**: 改为堆叠条（stacked bar）的一体化展示。

```
┌─ Card ──────────────────────────────────────────────────────┐
│  Total Damage: 4.5                                         │
│  ████████████▓▓▓▓▓▓▓▓▓▓░░░░░░░░░░░░░░░░░░░░  (avg: 3.2)    │
│   ← Cut 2.5 →← Blunt 2.0 →                                 │
│                                                             │
│  Dmg Bonus +0.25 (base)                                     │
│  ████████████████████████████████░░░░░░░░░░                  │
│                                                             │
│  Penetration  ●●●○○  Lv.3  (ignores 3 armor layers)         │
└─────────────────────────────────────────────────────────────┘
```

**实现要点**:
- 用 Grid 多列 + 不同颜色的 Border 实现堆叠
- 总长度代表 max(所有伤害之和, 某参照值)
- 第二行展示士气加成后的有效伤害
- 穿透等级用圆点可视化，附带护甲穿透说明

### P4 — 武器图片增强

**现状**: 仅显示单张 132×132 图片。

**改进项**:
- 点击图片 → 弹出 ZoomableImageView 放大查看
- 多张图片时显示缩略图切换（类似 ItemType 的画廊圆点）
- 无图片时根据 Sound 分类显示语义化图标：

| Sound 分类 | 占位图标 |
|-----------|---------|
| Punch/Claws/Grasp | `Symbol.Hand` |
| Club/Blunt | `Symbol.Flash` |
| Blade | `Symbol.Cut` |
| Rifle/Pistol | `Symbol.Target` |
| Bow/Throw | `Symbol.Arrow` |
| Choke | `Symbol.Dismiss` |
| Bite | `Symbol.Warning` |

### P5 — 数值上下文

**目标**: 为数值提供游戏内排名/比较参考。

```
Damage: 4.5  ──  Top 20% of 61 attack modes
Range:  80   ──  Top 5% (max: 80 tiles)
```

**实现**:
- 在 StatBar 或 StatCard 的值旁边显示排名百分位
- 需要 VisHelper 提供 `PercentileRank<T>(value, selector)` 工具方法

### P6 — 引用徽章悬停 Tooltip 预览

**目标**: 鼠标悬停在 MiniBadge 上时，显示被引用实体的核心属性摘要。

```
┌──────────────────────────────────┐
│ .308 Round (ChargeProfile #6)    │
│ PerUse: 1.0    PerHour: 0.0      │
│ PerHrEquip: 0  PerHex: 0.0       │
│ Degradeable: Yes (per use)       │
│                                  │
│ Ctrl+Click → open full detail    │
└──────────────────────────────────┘
```

**实现**:
```csharp
// VisHelper 新增方法
public static Control BuildRefTooltip<T>(T entity) where T : IEntity
{
    // 根据类型返回不同的 mini stat 面板
    return entity switch
    {
        ChargeProfile cp => BuildChargeProfilePreview(cp),
        Condition c => BuildConditionPreview(c),
        AttackMode am => BuildAttackModePreview(am),
        // ...
    };
}
```

用 `ToolTip.SetTip(badge, previewPanel)` 挂载。

### P7 — 动作按钮栏

**目标**: 在 Hero Header 下方提供常用操作的快捷按钮。

```
┌─ Card ──────────────────────────────────────┐
│  [📋 Clone]  [🗑 Delete]  [🔗 Copy ID]      │
│  [📊 Compare]  [📝 Edit Raw XML]            │
└─────────────────────────────────────────────┘
```

**状态**: 待讨论是否需要，涉及后端操作集成。

---

## 八、设计反模式（避免）

| # | 反模式 | 正确做法 |
|---|--------|---------|
| 1 | TreeView 风格的纯字段名值罗列 | 用 Card + 语义化面板 + MiniBadge |
| 2 | 所有字段无差别展示 | 选精选核心字段，Raw Data 面板供应急使用 |
| 3 | 引用字段显示为纯文本 "id=5" | 解析为可点击 MiniBadge，显示目标实体名称 |
| 4 | 空面板占位 Card | 条件渲染，无数据不渲染 |
| 5 | 在 visualizer 内定义私有 StatBar/Expander | 使用 VisHelper 共享实现 |
| 6 | 硬编码颜色字符串分散在各处 | 优先使用参考标准配色表（第三节 C），新增颜色需有语义依据 |
| 7 | 面板之间无限留白 | 统一用 `Spacing=16`（根 StackPanel）+ 面板内部 `Spacing=4~8` |
| 8 | Overview 使用进度条 | Overview 用 StatCard 键值对，Detail 才用 StatBar |
| 9 | 图片直接放 StackPanel | 必须包裹 `Border(CornerRadius, ClipToBounds, Bg=#0A000000)` |
| 10 | BuildDetail 不用 ScrollViewer | 内容可能超出可视区，必须包裹 |

---

## 九、类型到面板的映射参考

| 如果实体有 | 使用面板类型 | 参考 visualizer |
|-----------|------------|----------------|
| 图片字段 | Hero Header (带图) | AttackMode, ItemType, Creature |
| 枚举/类型字段 | Type Badge + Flag Capsule | AttackMode, BattleMove, Condition |
| 进度条型数值 | StatBar 面板 | AttackMode Combat |
| 键值对属性 | BuildStatCard | 所有 Overview |
| 引用字段(逗号分隔) | MiniBadge WrapPanel + SectionLabel | ChargePanel, ConditionsPanel |
| 引用字段(条件格式 `{id}x{mult}`) | ReferencePattern 解析 + MiniBadge | AttackerConditions |
| 引用字段(键值对 `{id}={value}`) | ReferencePattern 解析 + MiniBadge | Creature BaseConditions |
| 长文本 (WieldPhrase, Desc) | 文本面板 (引用样式或普通卡片) | Encounter, AttackMode |
| 反向引用 | Reverse Refs Panel | ItemType, Ingredient, ItemProp |
| 成对列表 (FieldName↔Modifier) | 配对表面板 | Condition Modifiers |
| 外交关系 | 关系横条面板 | Faction |
| bool 标记 (多个) | Flag Capsule 行 | BattleMove, AttackMode |

---

## 十、新增 Visualizer 清单

当为新数据类型实现 visualizer 时，依次检查：

- [ ] 有图片字段 → 实现 Hero Header (带图)
- [ ] 有枚举/分类 → 添加 Type Badge（含语义图标 + 颜色）
- [ ] 有 bool 标记 → 添加 Flag Capsule
- [ ] 有数值属性 → 精选 4-8 个核心值，Detail 用 StatBar，Overview 用 BuildStatCard
- [ ] 有引用字段 → 解析为 MiniBadge + 可点击跳转
- [ ] 有被引用关系 → 添加反向引用面板
- [ ] 有长文本 → 添加文本面板（截断处理）
- [ ] Raw Data → 始终添加，折叠在最顶部
- [ ] Overview → 缩略图(72×72) + 居中 Name + StatCard
