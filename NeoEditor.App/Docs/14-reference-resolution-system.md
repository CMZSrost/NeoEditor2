# NeoEditor 引用与解析系统设计

> 日期: 2026-06-08 | 更新: 2026-06-11 (v0.22.0-dev, v7 simplified index)
> 覆盖: ReferenceParser / ReferenceIndex / ReferencePattern / ReferenceFieldAttribute / IReferenceResolver / GenericDataGridHelper / SearchableDataGrid

---

## 一、系统概览

引用系统解决的核心问题：**游戏数据实体间大量使用字符串格式的跨表引用**（如 `"NSE:42"`、`"211x1.0"`、`"1x2+1x3"`），编辑器需要将这些字符串解析为可点击的链接，并支持 Ctrl+Click 跳转到目标实体。

### 1.1 涉及文件

| 文件 | 职责 |
|------|------|
| `Helper/ReferenceFieldAttribute.cs` | 元数据：标记属性为引用字段，声明目标类型/分隔符/解析模式 |
| `Helper/ReferencePattern.cs` | 策略模式：5 种引用格式的 ID 提取 + 显示格式化，支持 `-` 否定前缀 |
| `Helper/ReferenceParser.cs` | **纯函数解析层**：整合所有解析逻辑（ParsedRef / TargetKeyInfo / ResolvedRefSegment / ParsedReferenceField） |
| `Helper/ReferenceHelper.cs` | **已废弃**：所有方法委托到 ReferenceParser，标记 `[Obsolete]` |
| `Services/ReferenceIndexService.cs` | **索引层**：SQLite-backed 引用索引（`reference_index` + `reference_reverse` 表），替代旧的内存 4-dictionary ReferenceIndex；支持磁盘持久化 |
| `Helper/IReferenceResolver.cs` | **接口**：定义正规引用解析入口，强制所有调用端走 ReferenceIndex |
| `Helper/ReferenceResolver.cs` | **实现**：`class : IReferenceResolver`，统一可视化器和 DataGrid 的解析路径 |
| `Helper/GenericDataGridHelper.cs` | 编排层：列配置、导航路由，通过 index 进行引用查找；批量数据访问（`GetEntities<T>` / `GetCompositeEntities<T>` / `GetDedupedEntities<T>`） |
| `Views/UserControls/SearchableDataGrid.axaml.cs` | 事件层：Ctrl 键追踪、Tapped/Ctrl+RightClick 触发导航 |

### 1.2 架构分层（重构后）

```
┌─────────────────────────────────────────────────────────────────┐
│  用户交互层 (SearchableDataGrid)                                  │
│  ├─ KeyDown/KeyUp/LostFocus → _isCtrlHeld 状态追踪              │
│  ├─ Tapped → OnMainGridTappedNavigation (Ctrl+LeftClick)        │
│  ├─ ContextRequested → TriggerPeekForCell (Ctrl+RightClick)      │
│  └─ GDH cell template → Ctrl+Hover ToolTip / PointerPressed     │
├─────────────────────────────────────────────────────────────────┤
│  编排层 (GenericDataGridHelper)                                   │
│  ├─ ConfigureColumn() — 单元格模板生成                           │
│  ├─ NavigateToReferenceForce() — 跳转入口（传入 sourceEntityId） │
│  ├─ FindBestMatch() — 查找匹配实体（优先走 index）               │
│  └─ LookupSubjectByRawId() — Subject 查询（优先走 index）        │
├─────────────────────────────────────────────────────────────────┤
│  索引层 (ReferenceIndex, 放在 EntityMergeStore)                   │
│  ├─ Build() — 全量构建 context-aware 索引                       │
│  ├─ Lookup(sourceEid, propName, type, rawId) → EntityId         │
│  ├─ LookupDisplay() → (Subject, ModName)                        │
│  └─ ReverseLookup(entityId) → 反向引用列表                       │
├─────────────────────────────────────────────────────────────────┤
│  解析层 (ReferenceParser + ReferencePattern)                      │
│  ├─ ReferenceParser: ParseReference / DecomposeId / ExtractRawId │
│  ├─ ReferencePattern: 5 种格式策略（Id / IdXMult / MultXId /     │
│  │   IdEqualsValue / BracketId）                                  │
│  └─ 纯函数，无状态，零外部依赖                                    │
├─────────────────────────────────────────────────────────────────┤
│  元数据层 (ReferenceFieldAttribute)                               │
│  └─ TargetEntityType / TargetKey / Pattern / Separator /         │
│     SecondaryTargetEntityType                                    │
└─────────────────────────────────────────────────────────────────┘
```

---

## 二、ReferenceFieldAttribute — 元数据声明

每个引用字段通过 `[ReferenceField]` Attribute 声明其引用目标：

```csharp
// 简单引用: 属性值 "42" 引用 TreasureTable.id=42
[ReferenceField(typeof(TreasureTable))]
[Column("nTreasureID")]
public string? NTreasureID { get; set; }

// 多值引用: 属性值 "211x1.0,NSE:42x1" 逗号分隔，每段 pattern="{id}x{mult}"
[ReferenceField(typeof(Condition), Separator = ",", Pattern = "{id}x{mult}",
    SecondaryTargetEntityType = typeof(AttackMode), SecondaryTargetKey = "{Id}")]
[Column("vAttackerConditions")]
public string? VAttackerConditions { get; set; }

// 复合键: 属性值 "86.6" 需要用 GroupId+SubgroupId 组合查找目标
[ReferenceField(typeof(TreasureTable), TargetKey = "{GroupId}.{SubgroupId}")]
[Column("treasureGroup")]
public string? TreasureGroup { get; set; }
```

---

## 三、ReferencePattern — 格式策略

游戏中引用字符串有多种格式。`ReferencePattern` 使用策略模式将格式差异封装为 5 个子类：

```
ReferencePattern (abstract)
├── IdPattern          → "42" / "-115" / "NSE:42"
├── IdXMultPattern     → "211x1.0" / "-211x1.0" (ID x 倍率，支持否定)
├── MultXIdPattern     → "1x2" (数量 x 成分ID, 配方专用)
├── IdEqualsValuePattern → "38=1" (ID=值, 属性赋值)
└── BracketIdPattern   → "[42,SomeData]" (方括号包裹)
```

三个核心方法：

| 方法 | 用途 | IdPattern("-115") | IdXMultPattern("-211x1.0") |
|------|------|:--:|:--:|
| `ExtractRawId(segment)` | 提取纯 ID（剥离 `-`/`x`/`=` 修饰符） | `"115"` | `"211"` |
| `FormatDisplay(segment, subject, modName)` | 格式化显示文本 | `"~Dogman (115)"` | `"~Dogmanx1.0"` |
| `FormatExtraInfo(segment)` | 提取额外信息 | `"-"` | `"-x1.0"` |

**`-` 前缀处理**：`-` 是否定修饰符（表示排除/取反），不是 ID 的一部分。`ExtractRawId` 自动剥离，`FormatDisplay` 显示为 `~` 前缀。

---

## 四、ReferenceParser — 纯函数解析层

`ReferenceParser` 整合了原来 `ReferenceHelper` 的所有解析逻辑，零外部依赖。

### 4.1 数据类型

```csharp
ParsedRef(modName, id, multiplier)         // 单条解析后的引用
TargetKeyInfo(keyNames[], keySeparator)    // 描述如何分解 rawId 为查找键
ResolvedRefSegment {                       // 单段解析结果
    RawText, ExtractedId, Namespace, NumericId, ExtraInfo, KeyValues
}
ParsedReferenceField { Segments[], Metadata, RawValue }  // 整字段解析结果
```

### 4.2 核心方法

```
ExtractRawId("-115", "{id}")     → "115"       // 委托给 ReferencePattern
ParseReference("NSE:42")         → ("NSE", 42)
ParseTargetKey("{GroupId}.{SubgroupId}") → TargetKeyInfo(["GroupId","SubgroupId"], ".")
DecomposeId("86.6", keyInfo)    → {"GroupId":86, "SubgroupId":6}
BuildLookupKey("0:42")           → "42"        // 剥离默认命名空间
BuildLookupKey("NSE:42")         → "NSE:42"    // 保留非默认命名空间
Parse(value, attr)               → ParsedReferenceField  // 完整解析入口
ExtractIds(value, attr)          → [(extractedId, keyValues), ...]  // 快速提取
```

### 4.3 命名空间规则

```
"" 或 "0"  → 游戏基础命名空间 (data/ 目录)
"NSE", "FoD" → AddOn Mod 的内部名称 (strModName)
```

`IsDefaultNamespace(modName)` → `modName is "" or "0" or null`

---

## 五、ReferenceIndex — 索引层（★ 核心）

### 5.1 设计动机

> **[v0.22.0-dev 更新]** `FindBestMatch` 和 `LookupSubjectByRawId` 的 O(n) 扫描兜底已于 Phase 6 移除。DataGrid 引用列现在委托 `IReferenceResolver.LookupSubject`，纯 Index 查询。以下章节描述的 FindBestMatch 回退路径已过时，仅供历史参考。

原来 `FindBestMatch` 是 O(n) 扫描全表，且渲染和导航走不同的解析路径导致结果不一致。索引层解决两个问题：
- **性能**：O(1) 字典查找替代 O(n) 反射遍历
- **一致性**：渲染和导航走同一个 Lookup 方法，保证解析结果相同

### 5.2 索引结构（v7 simplified model）

当前采用简化的双索引模型，不再区分 context-aware 和 global fallback：

```csharp
// Namespace lookup — 有命名空间前缀的引用（如 "NSE:3"）
_nsIndex[(EntityType, Namespace, PrimaryKey)] → EntityId

// MergedId lookup — 无命名空间前缀的引用（如 "3"）
_mergedIdIndex[(EntityType, MergedId)] → EntityId
```

**关键设计决策**：`_mergedIdIndex` 使用 MergedId 作为键，而非实体的主键 Id。MergedId 由 MergeService 或 BrowserIndex 统一计算：
- ns="0"（Game）→ MergedId = 主键
- ns≠"0"（mod）→ MergedId = 从 maxMergeKey+1 开始自增

这确保了同一实体的 MergedId 在合并视图和浏览器视图中保持一致，引用解析不会因视图切换而找到不同实体。

**旧版（已废弃）**上下文感知索引（context-aware）已移除。原来的 `_forward[(sourceEid, propName, rawId)]` 和 `_mergedFallback[(type, mergedId)]` 已被简化为上述双索引模型，其中 nsIndex 已隐含了"同 mod 优先"语义（通过 MergeStore 中最高 ModId 覆盖实现）。

### 5.3 构建流程

```
BuildAsync()
  ├── 遍历 ReferenceLookups 中所有实体
  │     ├── ComputeEntityKey(entity) — 获取主键（Id 或 nID）
  │     ├── Index by MergedId:
  │     │    _mergedIdIndex[(entityType, mergedId)] = EntityId
  │     │    （同 type+mid 的后写入者覆盖前写入者）
  │     └── Index by Namespace:
  │          _nsIndex[(entityType, namespace, primaryKey)] = EntityId
  │          （同 type+ns+pk 的后写入者覆盖前写入者）
  └── BuildReverse() — 反向索引（遍历 ReferenceLookups 的引用字段）
```

**覆盖语义**：nsIndex 和 mergedIdIndex 都采用"后写入覆盖"策略。合并视图中实体按 mod 加载顺序排列，高优先级 mod 的实体后写入，因此 `_nsIndex[(Recipe, "0", "3")]` 总是指向合并链中最顶层的 id=3 的 Recipe。浏览器视图中同理，后遍历的 mod 覆盖先遍历的。

### 5.4 查找流程

```csharp
Lookup(sourceEntityId, propertyName, targetType, rawId)
  │
  ├── 1. 解析 rawId: ReferencePattern 提取 ExtractedId + Namespace
  │
  ├── 2. 命名空间前缀（如 "NSE:3"）:
  │     ParseReference → (namespace, numericId)
  │     → _nsIndex[(targetType, namespace, numericId.ToString())]
  │
  └── 3. 无命名空间前缀（如 "3"）:
        int.TryParse → mergedId
        → _mergedIdIndex[(targetType, mergedId)]
```

> **重要**：MergedId 查找使用的是 EntityMergedIds 中存储的值，不是实体的数据库主键。浏览器视图和合并视图必须对同一实体分配相同的 MergedId。

### 5.5 渲染 vs 导航的一致性

```
渲染路径:
  FormatSegmentDisplay → LookupSubjectByRawId(type, rawId, sourceEid, propName, ...)
    → index.LookupDisplay(sourceEid, propName, type, rawId)
      → Lookup(sourceEid, propName, type, rawId)  ← context-aware

导航路径:
  Ctrl+Click → NavigateToReferenceForce(type, rawId, ..., sourceEid, propName)
    → FindBestMatch(type, rawId, key, sourceEid, propName)
      → index.Lookup(sourceEid, propName, type, rawId)  ← 同一个入口
```

渲染和导航走同一个 `Lookup(sourceEid, propName, type, rawId)`，保证解析结果完全一致。

---

## 六、GenericDataGridHelper — 编排层

### 6.1 实体查找

```csharp
// Context-aware（渲染和导航使用）
FindBestMatch(type, rawId, targetKey, sourceEntityId, propertyName)
  → index.Lookup(sourceEntityId, propertyName, type, rawId)
  → 命中: 从 ReferenceLookups 获取 IEntity
  → 未命中: O(n) fallback 扫描

// 旧签名（无 source context，向后兼容）
FindBestMatch(type, rawId, targetKey)
  → FindBestMatch(type, rawId, targetKey, "", "")
```

### 6.2 导航路由

```
NavigateToReferenceForce(type, rawId, targetKey, secondary, sourceEid, propName)
  ├── ResolveWithSecondary() → FindBestMatch → index.Lookup (context-aware)
  ├── PeekRequested?.Invoke() → ReferenceInspector
  └── DoNavigateToReference()
        ├── ResolveEntityIdByTargetKey() → index.Lookup → EntityId
        └── NavigateToByEntityId() → ModGameDataTabsView.NavigateToEntityImpl()
              └── SharedDataGrid → 搜索匹配 EntityId → 滚动
```

### 6.3 列配置

列模板使用 `LookupSubjectByRawId`（走 index）获取 Subject 显示名。

多值单元格的 TextBlock 设置 `Tag = rawSegment`（原始引用文本），避免显示文本被误解析为引用 ID。

---

## 七、已修复的 Bug

### Bug 1: FindBestMatch 类型比较缺陷 ✅

**根因**：`prop.GetValue(entity) is int val` 对 long/null/EF 代理类型返回 false。
**修复**：`Convert.ToInt64(propValue)` + `Equals` 类型安全比较。
**文件**：`Helper/GenericDataGridHelper.cs`

### Bug 2: DataGrid 列索引映射偏移 ✅

**根因**：`rowPanel.Children.IndexOf(cell)` 包含 RowHeader 等内部元素。
**修复**：只计数 DataGridCell 子元素计算列索引；同时在 `OnAutoGeneratingColumn` 中填充 `ColumnMetaCache`。
**文件**：`Views/UserControls/SearchableDataGrid.axaml.cs`

### Bug 3: 渲染与跳转解析不一致 ✅

**根因**：渲染和导航走不同的索引查询路径（导航用全局 MergedId 优先，渲染用 context-aware 优先），导致同一 rawId 解析到不同实体。
**修复**：统一渲染和导航都走 `ReferenceIndex.Lookup(sourceEid, propName, type, rawId)`。
**文件**：`Services/ReferenceIndexService.cs`, `Helper/GenericDataGridHelper.cs`

### Bug 4: 多值单元格用显示文本当 rawId ✅

**根因**：Tapped handler 读 `sourceTb.Text`（显示文本如 `"AA Battery (4)"`）当 raw text 给 ExtraRawId，解析出垃圾。
**修复**：多值 TextBlock 设置 `Tag = rawSegment`，优先读 Tag。
**文件**：`Helper/GenericDataGridHelper.cs`, `Views/UserControls/SearchableDataGrid.axaml.cs`

### Bug 5: `-` 前缀被当 ID 的一部分 ✅

**根因**：`IdPattern.ExtractRawId("-115")` 返回 `"-115"`，但 `-` 是否定修饰符。
**修复**：`IdPattern` 和 `IdXMultPattern` 剥离 `-`，`FormatExtraInfo` 报告 `"-"`。
**文件**：`Helper/ReferencePattern.cs`

### Bug 6: 显示缓存 key 冲突（MergedId vs businessKey） ✅

**根因**：`_display` 缓存 key 用非唯一 rawId，MergedId=4 和 Id=4 的缓存互相污染。
**修复**：缓存 key 改为目标 EntityId（全局唯一）。
**文件**：`Helper/ReferenceIndex.cs`

### Bug 7: 排序导致 DataGrid NRE 崩溃 ✅

**根因**：`ItemsSource = new ObservableCollection` 触发 Avalonia DataGrid 的 `RemoveAutoGeneratedColumns` 内部 NRE。
**修复**：排序前置 `AutoGenerateColumns = false`，排序后恢复，并加 try-catch。
**文件**：`Views/UserControls/SearchableDataGrid.axaml.cs`

### Bug 8: 浏览器视图与合并视图 MergedId 不一致 ✅

**根因**：浏览器视图曾将所有实体的 MergedId 设为数据库主键（`store.EntityMergedIds[entityId] = k`），合并视图则对 ns≠"0" 的实体使用自增 MergedId。导致同一 insert-space 实体的 MergedId 在两个视图中不同（如 pk=3→浏览器 mid=3，合并视图 mid=1204），无前缀引用解析时 `_mergedIdIndex[(type, mergedId)]` 查到不同实体。
**修复**：`Documents.RebuildBrowserIndexAsync` 中 MergedId 计算改为与 `MergeService.ComputeTypeMerge` 一致：ns="0"→mid=pk，ns≠"0"→mid=自增。同时删除旧磁盘缓存强制全量重建。
**文件**：`ViewModels/MainContent/Documents.cs`, `Services/MergeService.cs`
**诊断**：ValueEditor 徽章条显示 mod:name / mid / pk / entityId 四字段，可在两个视图中对比同一实体的 mid 值。

---

## 八、已知限制

| # | 问题 | 状态 | 备注 |
|---|------|:--:|------|
| 1 | GDH 仍为 static class，多 DataGrid 共享全局状态 | 🟡 | ReferenceIndex 已去静态化（per-store 实例），但 GDH 编排层未动 |
| 2 | 反向索引增量更新未实现 | 🟡 | `UpdateField` 只更新正向索引，反向索引需全量重建 |
| 3 | 排序箭头不显示 | 🔴 | Avalonia 11.3 框架限制 |
| 4 | 单元测试零覆盖 | 🔴 | |

---

## 九、完整导航链路（调试用）

```
用户 Ctrl+LeftClick 单元格
  → [SearchableDataGrid] KeyDown → _isCtrlHeld = true
  → [SearchableDataGrid] Tapped → OnMainGridTappedNavigation
    → NavigationHandled 检查（防止双重触发）
    → 列索引 (DataGridCell 计数，跳过 RowHeader)
    → column.SortMemberPath → propName
    → dataItem → sourceEid = (row.DataContext as IEntity).EntityId
    → refAttr → ExtractRawId(rawValue, pattern) → rawId
    → [GDH] NavigateToReferenceForce(type, rawId, key, secondary, sourceEid, propName)
      → [GDH] ResolveWithSecondary(type, rawId, key, ..., sourceEid, propName)
        → FindBestMatch(type, rawId, key, sourceEid, propName)
          → [RefIndex] Lookup(sourceEid, propName, type, rawId)
            ├─ 有命名空间前缀: _nsIndex[(type, ns, primaryKey)] → EntityId
            └─ 无命名空间前缀: _mergedIdIndex[(type, mergedId)] → EntityId
      → PeekRequested?.Invoke() → ReferenceInspector
      → DoNavigateToReference()
        → ResolveEntityIdByTargetKey() → index.Lookup → EntityId
        → NavigateToByEntityId(type, entityId)
          → [ModGameDataTabsView] NavigateToEntityImpl
            → DataTabs.SelectedItem = targetTab → OnTabChanged
            → SharedDataGrid.ItemsSource 替换
            → DoScrollToEntity → 搜索 EntityId → 选中+滚动
```
