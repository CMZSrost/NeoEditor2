# NeoEditor 引用系统重构方案

> 日期: 2026-06-07 | 版本: v0.19.0-dev | 状态: Phase 1-4 全部完成
> 前置阅读: [14-reference-resolution-system.md](14-reference-resolution-system.md) · [13-architecture-critique.md](13-architecture-critique.md)  
> 本文档描述引用解析与跳转系统的完整重构方案，后续实施计划将基于此方案制定。

---

## 一、问题根源

当前引用系统将所有职责堆在一个 1056 行的 `GenericDataGridHelper` 静态类中，导致四个互相加剧的问题：

| # | 问题 | 根因 |
|---|------|------|
| **Bug 1** | Ctrl+Click 总是跳转到 id=1 | `FindBestMatch` 中 `prop.GetValue(entity) is int val` 对 `long`/`null`/EF 代理失效，所有实体都匹配 |
| **Bug 2** | DataGrid 列索引映射偏移 | `rowPanel.Children.IndexOf(cell)` 计入了 RowHeader 等内部元素，与 `dg.Columns` 不对齐 |
| **Bug 3** | 多 DataGrid 实例竞争全局静态状态 | `_activeMergeStore` / `_activeEditStore` 全局唯一，分屏时后被 `SetActiveStores` 覆盖 |
| **Bug 4** | SubjectCache 不感知 store 切换 | 缓存跟随 `_activeMergeStore`，切换 Tab 后旧缓存仍可能被读取 |

更深层的问题是：**解析、索引、导航三个职责被耦合在一个静态类中**，没有明确的边界。详见 [13-architecture-critique.md](13-architecture-critique.md) 第 1.1 节。

---

## 二、重构目标

### 2.1 核心原则

1. **解析与导航分离** — 两个独立的子系统，各自演进，互不依赖
2. **编辑器内维护索引** — 以 `EntityId` 为统一键，将"解析→匹配"的耗时操作从点击时移到数据加载时（eager indexing）
3. **事件 / 责任链驱动的导航** — 不硬编码跳转逻辑，让能处理的目标自己响应
4. **去静态化** — 索引和导航状态绑定到具体 View 实例，消除全局状态竞争

### 2.2 目标分层架构

```
┌──────────────────────────────────────────────────────────────┐
│  UI 层                                                        │
│  ├─ SearchableDataGrid      Ctrl+Click / Ctrl+Hover / Peek   │
│  ├─ ModGameDataTabsView     INavigationTarget 实现            │
│  └─ ReferenceInspector      Peek 展示面板                     │
├──────────────────────────────────────────────────────────────┤
│  导航层  INavigationRouter (DI 单例)                           │
│  ├─ RegisterTarget / UnregisterTarget                         │
│  ├─ Navigate(entityType, entityId)    → 责任链                │
│  ├─ Peek(entityType, entityId)        → ReferenceInspector    │
│  └─ OpenInNewTab(entityType, entityId) → 兜底                 │
├──────────────────────────────────────────────────────────────┤
│  索引层  ReferenceIndex (实例对象, 放在 EntityMergeStore 中)    │
│  ├─ Build(allEntities)                → 全量构建              │
│  ├─ Update(entity, property, newVal)  → 增量更新              │
│  ├─ Lookup(type, rawId)               → EntityId?             │
│  ├─ ReverseLookup(entityId)           → List&lt;rawId&gt;     │
│  └─ GetDisplayInfo(type, rawId)       → Subject + ModName     │
├──────────────────────────────────────────────────────────────┤
│  解析层  ReferenceParser (纯函数, 无状态)                       │
│  ├─ ParseField(value, attr)           → List&lt;ResolvedRef&gt;│
│  ├─ ExtractRawId(segment, pattern)    → string                │
│  ├─ DecomposeId(rawId, targetKey)     → KeyValues             │
│  └─ FormatDisplay(segment, info)      → string                │
├──────────────────────────────────────────────────────────────┤
│  元数据层  ReferenceFieldAttribute (不变)                       │
│  └─ TargetType / Pattern / Separator / TargetKey / Secondary  │
└──────────────────────────────────────────────────────────────┘
```

数据流向：**元数据 → 解析 → 索引 → 导航 → UI**

---

## 三、解析层设计

### 3.1 设计原则

解析层是**纯函数、无状态**的工具集。输入一个字段的原始值和它的 `[ReferenceField]` attribute，输出结构化的解析结果。不持有任何字典、缓存或 store 引用。

### 3.2 核心数据类型

```csharp
/// <summary>单个引用段落的解析结果</summary>
public record ResolvedRefSegment
{
    /// <summary>原始文本段落，如 "NSE:211x1.0"</summary>
    public required string RawText { get; init; }

    /// <summary>提取的纯 ID（去掉倍率/赋值/方括号等格式信息），如 "NSE:211"</summary>
    public required string ExtractedId { get; init; }

    /// <summary>命名空间前缀。"NSE"、""（默认）、"0"（默认）</summary>
    public string? Namespace { get; init; }

    /// <summary>从 ExtractedId 中解析出的纯数字 ID（无命名空间前缀）</summary>
    public int NumericId { get; init; }

    /// <summary>额外信息，如倍率 1.0、赋值 0.5 等（用于显示）</summary>
    public string? ExtraInfo { get; init; }

    /// <summary>分解后的复合键值对。简单引用 → {"Id":211}，复合键 → {"GroupId":86, "SubgroupId":6}</summary>
    public Dictionary<string, int> KeyValues { get; init; } = new();
}

/// <summary>整个引用字段的解析结果</summary>
public record ParsedReferenceField
{
    /// <summary>解析后的段落列表。单值字段 = 1 个元素</summary>
    public required IReadOnlyList<ResolvedRefSegment> Segments { get; init; }

    /// <summary>回溯到原字段的 Attribute 元数据</summary>
    public required ReferenceFieldAttribute Metadata { get; init; }

    /// <summary>原始未解析的字段值</summary>
    public required string RawValue { get; init; }
}
```

### 3.3 嵌套解析流程

游戏中的引用字段存在嵌套格式：**外层分隔 → 内层模式 → 复合键分解**。

以 `vAttackerConditions = "NSE:211x1.0,42x1"` 为例（`Separator=","`, `Pattern="{id}x{mult}"`）：

```
输入: "NSE:211x1.0,42x1"

第 1 步 — 列表拆分 (按 Separator)
  → ["NSE:211x1.0", "42x1"]

第 2 步 — 逐段用 Pattern 策略提取
  段落 "NSE:211x1.0":
    ├─ ExtractRawId("NSE:211x1.0", "{id}x{mult}")  → "NSE:211"
    ├─ ParseReference("NSE:211")                     → Namespace="NSE", NumericId=211
    ├─ ExtractExtraInfo("NSE:211x1.0", "{id}x{mult}") → ExtraInfo="1.0"
    └─ KeyValues: 由下一步决定

  段落 "42x1":
    ├─ ExtractRawId("42x1", "{id}x{mult}")  → "42"
    ├─ ParseReference("42")                  → Namespace="", NumericId=42
    ├─ ExtractExtraInfo("42x1", "{id}x{mult}") → ExtraInfo="1.0"
    └─ KeyValues: 由下一步决定

第 3 步 — 结构化属性提取 (按 TargetKey 分解)
  TargetKey = "{Id}" (默认):
    KeyValues = {"Id": 211}  /  {"Id": 42}

  TargetKey = "{GroupId}.{SubgroupId}":
    输入 "86.6" → DecomposeId → {"GroupId": 86, "SubgroupId": 6}
    输入 "418"  → DecomposeId → {"Id": 418}  (fallback: 值不含分隔符)
```

### 3.4 策略模式保持

当前 `ReferencePattern` 的 5 种子类设计合理，继续保留：

```
ReferencePattern (abstract)
├── IdPattern            → "42" / "NSE:42"
├── IdXMultPattern       → "211x1.0"
├── MultXIdPattern       → "1x2"       (配方专用)
├── IdEqualsValuePattern → "38=1"
└── BracketIdPattern     → "[42,SomeData]"
```

每种子类实现三个方法：

| 方法 | 用途 | 示例 (IdXMultPattern, 输入 "NSE:211x1.0") |
|------|------|------|
| `ExtractRawId(segment)` | 提取纯 ID，用于索引键 | `"NSE:211"` |
| `FormatDisplay(segment, subject, modName)` | 格式化 DataGrid 单元格显示文本 | `"Poisonedx1.0"` |
| `ExtractExtraInfo(segment)` | 提取倍率/赋值等附加信息 | `"1.0"` |

`ReferencePattern.FromName(patternName)` 工厂方法不变。

### 3.5 ReferenceParser — 解析入口

```csharp
/// <summary>引用字段解析器。纯函数，无状态。</summary>
public static class ReferenceParser
{
    /// <summary>
    /// 解析一个引用字段的完整值，返回结构化结果列表。
    /// 单值字段返回包含一个元素的列表。
    /// </summary>
    public static ParsedReferenceField Parse(string value, ReferenceFieldAttribute attr);

    /// <summary>
    /// 只提取纯 ID 列表（用于索引构建），跳过显示格式化等开销。
    /// 返回: [("NSE:211", {"Id":211}), ("42", {"Id":42})]
    /// </summary>
    public static List<(string ExtractedId, Dictionary<string,int> KeyValues)> ExtractIds(
        string value, ReferenceFieldAttribute attr);
}
```

注意 `Parse` 和 `ExtractIds` 是两个独立入口——**索引构建时用 `ExtractIds`**（不需要显示信息），**渲染时用 `Parse`**（需要 `FormatDisplay`）。

---

## 四、索引层设计

### 4.1 设计原则

- 索引是 **每个 `EntityMergeStore` 实例持有**的对象，生命周期与合并视图 Tab 一致
- 索引的键是 `"{namespace}:{extractedId}"`，值是 `EntityId`
- **全量构建**在数据加载完成后触发（导入 Mod、切换 Profile）
- **增量更新**在单元格编辑提交后触发
- 索引同时维护正向映射（rawId → EntityId）和反向映射（EntityId → 所有引用它的 rawId 列表）

### 4.2 索引键定义

```
索引键 = "{namespace}:{extractedId}"

其中:
  namespace    = 命名空间前缀, "" 或 "0" 统一规范化为 ""
  extractedId  = ReferencePattern.ExtractRawId(segment) 去掉 namespace 前缀后的纯 ID 部分

示例:
  "NSE:211x1.0" → ExtractRawId → "NSE:211" → 键 = "NSE:211"
  "42x1"        → ExtractRawId → "42"       → 键 = ":42" → 规范化为 "42"
  "86.6"        → ExtractRawId → "86.6"     → 键 = "86.6"
  "0:152"       → ExtractRawId → "0:152"    → 键 = ":152" → 规范化为 "152"
```

**复合键的特殊处理**：复合主键的值（如 `"86.6"`）天然包含分隔符，直接作为 extractedId 使用。`ReferenceParser.ExtractIds` 会在 `KeyValues` 中同时提供分解后的键值对供匹配使用。

### 4.3 索引数据结构

```csharp
/// <summary>
/// Per-merge-view 的引用索引。绑定到 EntityMergeStore 实例。
/// 不是静态类，每个 ModGameDataTabsView 持有一个实例。
/// </summary>
public class ReferenceIndex
{
    private readonly EntityMergeStore _store;

    // ── 正向索引 ──
    // (entityType, lookupKey) → 目标 EntityId
    // lookupKey = "{namespace}:{extractedId}"，如 "NSE:211"、"86.6"、"42"
    private Dictionary<(Type EntityType, string LookupKey), string> _forwardIndex = new();

    // ── 反向索引 ──
    // 目标 EntityId → (引用者 EntityId, 引用字段属性名, rawId)
    private Dictionary<string, List<(string SourceEntityId, string PropertyName, string RawId)>> _reverseIndex = new();

    // ── 显示缓存 ──
    // (entityType, lookupKey) → (Subject, ModName)
    private Dictionary<(Type, string), (string? Subject, string? ModName)> _displayCache = new();

    /// <summary>全量构建索引。在数据加载完成后调用。</summary>
    public void Build(IReadOnlyList<IEntity> allEntities);

    /// <summary>增量更新：单个实体的指定属性变更后重索引该字段。</summary>
    public void UpdateField(IEntity entity, string propertyName, string? oldValue, string? newValue);

    /// <summary>移除一个实体的所有索引条目（删除行时调用）。</summary>
    public void RemoveEntity(IEntity entity);

    /// <summary>添加一个实体的索引条目（新增行时调用）。</summary>
    public void AddEntity(IEntity entity);

    /// <summary>正向查找：raw text → EntityId。</summary>
    public string? Lookup(Type entityType, string lookupKey);

    /// <summary>反向查找：EntityId → 所有引用它的 (来源EntityId, 属性, rawId)。</summary>
    public IReadOnlyList<(string SourceEntityId, string PropertyName, string RawId)> ReverseLookup(string entityId);

    /// <summary>清除所有索引数据。</summary>
    public void Clear();
}
```

### 4.4 全量构建流程

```
Build(allEntities):
  Clear()

  for each entity in allEntities:
    entityType = entity.GetType()

    for each property in entityType.GetProperties():
      attr = property.GetCustomAttribute<ReferenceFieldAttribute>()
      if attr is null: continue

      rawValue = property.GetValue(entity) as string
      if string.IsNullOrEmpty(rawValue): continue

      // 用解析层提取 ID 列表（不需要显示信息，用快速路径）
      ids = ReferenceParser.ExtractIds(rawValue, attr)

      for each (extractedId, keyValues) in ids:
        lookupKey = NormalizeKey(extractedId)

        // 正向索引
        _forwardIndex[(attr.TargetEntityType, lookupKey)] = entity.EntityId

        // 显示缓存
        _displayCache[(entityType, lookupKey)] = (entity.Subject, GetModName(entity))

        // 如果有 SecondaryTarget，也索引到 secondary type
        if attr.SecondaryTargetEntityType is not null:
          _forwardIndex[(attr.SecondaryTargetEntityType, lookupKey)] = entity.EntityId

        // 反向索引：需要先在 ReferenceLookups 中找到目标实体
        // 这一步在 Build 完成后统一执行 BuildReverseIndex()

    BuildReverseIndex()
```

### 4.5 增量更新流程

当用户编辑一个单元格时（假设 `vAttackerConditions` 从 `"211x1.0,42x1"` 改为 `"211x1.0,NSE:99x1"`）：

```
UpdateField(entity, "vAttackerConditions", oldValue: "211x1.0,42x1", newValue: "211x1.0,NSE:99x1"):

  // 1. 计算 old ID 集合
  oldIds = ReferenceParser.ExtractIds(oldValue, attr)  → [{"211":Id}, {"42":Id}]
  newIds = ReferenceParser.ExtractIds(newValue, attr)  → [{"211":Id}, {"NSE:99":Id}]

  // 2. 移除已删除的 ID（"42" 不再被引用）
  foreach id in oldIds - newIds:
    _forwardIndex.Remove((attr.TargetEntityType, NormalizeKey(id.ExtractedId)))
    UpdateReverseIndex(entity.EntityId, id, remove: true)

  // 3. 添加新增的 ID（"NSE:99" 是新的引用）
  foreach id in newIds - oldIds:
    _forwardIndex[(attr.TargetEntityType, NormalizeKey(id.ExtractedId))] = entity.EntityId
    UpdateReverseIndex(entity.EntityId, id, remove: false)

  // 4. "211" 在 old 和 new 中都存在，无需更新
```

### 4.6 索引存储位置

```
EntityMergeStore (已存在)
  ├── ReferenceLookups          (已有)
  ├── EntityModNames            (已有)
  ├── SubjectCache              (已有, 将被 ReferenceIndex._displayCache 替代)
  ├── NamespaceToModName        (已有)
  ├── ...其他已有字段...
  └── ReferenceIndex  _index   (★ 新增)
```

`EntityMergeStore` 已经是 per-tab 的，`ReferenceIndex` 放在里面自然继承了正确的生命周期。

---

## 五、导航层设计

### 5.1 为什么不用纯消息机制

用 `IMessenger` 发 `NavigateToEntityRequestedMessage` 看起来解耦，但有一个致命缺陷：**消息系统没有"已处理"反馈**。当多个订阅者收到同一条消息时，没人能告诉发送者"我已经处理了"，也就无法实现兜底（如果当前 Tab 都处理不了，需要打开新 Tab）。

### 5.2 改用责任链模式

```csharp
/// <summary>导航目标接口。每个能承载实体展示的组件实现此接口。</summary>
public interface INavigationTarget
{
    /// <summary>该目标是否能处理指定类型的实体导航？</summary>
    bool CanNavigate(Type entityType, string entityId);

    /// <summary>执行导航。调用前已通过 CanNavigate 确认可以处理。</summary>
    void NavigateTo(Type entityType, string entityId);

    /// <summary>导航优先级。数字越大优先级越高。当前活跃 Tab 优先级最高。</summary>
    int Priority { get; }
}

/// <summary>
/// 导航路由器。DI 注册为单例。
/// 维护所有 INavigationTarget 的注册表，按优先级依次询问。
/// </summary>
public interface INavigationRouter
{
    /// <summary>注册一个导航目标（Tab 打开/切换时调用）。</summary>
    void RegisterTarget(INavigationTarget target);

    /// <summary>注销一个导航目标（Tab 关闭/切换走时调用）。</summary>
    void UnregisterTarget(INavigationTarget target);

    /// <summary>导航到指定实体。按优先级依次询问所有已注册目标，
    /// 第一个 CanNavigate=true 的执行 NavigateTo。无人处理时打开新 Tab。</summary>
    void Navigate(Type entityType, string entityId);

    /// <summary>Peek 预览。将实体信息推送到 ReferenceInspector 面板。</summary>
    void Peek(Type entityType, string rawId, IEntity? entity);

    /// <summary>强制在指定目标中导航（用于 Ctrl+LeftClick 跳转）。</summary>
    void NavigateForce(Type entityType, string entityId);
}
```

### 5.3 导航流程

```
用户 Ctrl+LeftClick 单元格
  │
  ├─ 1. SearchableDataGrid 捕获 Tapped 事件
  │     ├─ 从 cell 获取 propertyName (通过缓存, 修复 Bug 2)
  │     ├─ 从 dataContext 获取实体
  │     └─ 从 attribute 获取元数据
  │
  ├─ 2. 解析: ReferenceParser.Parse(rawValue, attr)
  │     └─ 获得被点击段落的 ResolvedRefSegment
  │
  ├─ 3. 索引查找: index.Lookup(targetEntityType, segment.ExtractedId)
  │     └─ 获得目标 EntityId (或 null)
  │
  ├─ 4. 导航: _navigationRouter.Navigate(targetEntityType, entityId)
  │     │
  │     ├─ Target A (当前活跃 Tab 的 DataGrid):
  │     │   CanNavigate(type, entityId) → 检查自己的数据中是否包含此实体
  │     │   → YES: NavigateTo → 滚动到行 → 返回
  │     │   → NO:  跳过
  │     │
  │     ├─ Target B (同 View 的其他 Tab):
  │     │   CanNavigate(type, entityId) → 检查是否持有该类型数据
  │     │   → YES: 切换 Tab → 滚动到行 → 返回
  │     │   → NO:  跳过
  │     │
  │     ├─ Target C (DocumentWorkspace 级别的导航器):
  │     │   CanNavigate → 总是 true (兜底)
  │     │   → 打开新 ModGameDataTabsView 或新 Tab → 导航 → 返回
  │     │
  │     └─ (无 Target 能处理, Navigate 内部调用 OpenInNewTab 兜底)
  │
  └─ 5. 同时推送 Peek (Ctrl+LeftClick 始终附带 Peek)
        _navigationRouter.Peek(targetEntityType, segment.ExtractedId, targetEntity)
```

### 5.4 INavigationTarget 的实现者

| 实现者 | Priority | CanNavigate 条件 | NavigateTo 行为 |
|--------|:--------:|------------------|-----------------|
| **GameDataTypeTabItem** (DataGrid Tab) | 100 | entityType 匹配 且 数据中存在该 EntityId | 滚动到目标行并选中 |
| **ModGameDataTabsView** (Tab 容器) | 50 | 任意 Tab 的 entityType 匹配 | 切换到匹配的 Tab, 滚动到目标行 |
| **DocumentWorkspaceViewModel** (兜底) | 0 | 总是 true | 打开新 Tab 或触发全局导航请求 |

### 5.5 Peek 和 Navigate 的统一

```
┌─────────────────────────────────────────────┐
│  用户操作          Navigate    Peek         │
├─────────────────────────────────────────────┤
│  Ctrl+LeftClick    ✅ 强制      ✅ 附带     │
│  Ctrl+RightClick   ❌          ✅          │
│  Ctrl+Hover        ❌          ✅ (ToolTip)│
│  Visualizer 点击   ✅          ✅ (可选)    │
└─────────────────────────────────────────────┘
```

两种操作都先走解析 → 索引查找，区别只在于最后调用 `Navigate` 还是 `Peek`。

---

## 六、索引联动场景

索引建立后，不仅服务于导航，以下功能也可直接消费索引：

### 6.1 反向引用查询

```
用户右键实体 → "查找引用"
  → index.ReverseLookup(entity.EntityId)
  → 返回所有引用此实体的 (来源EntityId, 属性名, rawId)
  → 在 SearchResults 面板展示列表，点击可跳转
```

替代当前 `FindReferencesRequestedMessage` 需要的全表扫描。

### 6.2 悬空引用检测（验证）

```
ValidationService:
  for each 索引条目 (type, lookupKey) → targetEntityId:
    if targetEntityId 对应的实体不存在:
      → Warning: "{type.Name} 字段引用 '{lookupKey}' 指向不存在的实体"
```

### 6.3 模糊引用检测（验证）

```
  for each lookupKey 在同一个 entityType 下匹配到多个 EntityId:
    → Warning: "引用 '{lookupKey}' 在 {type.Name} 中有多个匹配: [{entityIds}]"
```

### 6.4 可视化器集成

ItemType visualizer 的引用条、Recipe 的成分列表等场景：

```
// 之前: 需要重新解析字段 + 扫描全表匹配
// 之后: 直接从索引拿
var entityId = index.Lookup(typeof(TreasureTable), "86.6");
var subject = index.GetDisplayInfo(typeof(TreasureTable), "86.6");
```

### 6.5 合并视图字段来源

```
// 引用字段的 ToolTip: "此引用指向来自 NSE Mod 的实体"
var entityId = index.Lookup(type, lookupKey);
var modName = store.EntityModNames.GetValueOrDefault(entityId);
```

---

## 七、与现有代码的衔接

### 7.1 保留的部分

| 组件 | 处理方式 |
|------|---------|
| `ReferenceFieldAttribute` | 不变，继续作为元数据标记 |
| `ReferencePattern` (5 策略) | 保留，作为解析层的策略实现 |
| `ReferenceHelper` (大部分方法) | 方法迁移到 `ReferenceParser`，旧类标记 `[Obsolete]` 后逐步删除 |
| `EntityMergeStore` | 保留，新增 `ReferenceIndex` 字段 |
| `NavigateToEntityRequestedMessage` / `PeekRequestedMessage` | 保留消息定义，发送者从 GDH 改为 `INavigationRouter` 实现 |

### 7.2 移除的部分

| 组件 | 替代方案 |
|------|---------|
| `GenericDataGridHelper.FindBestMatch` | `ReferenceIndex.Lookup` |
| `GenericDataGridHelper.LookupSubjectByRawId` | `ReferenceIndex.GetDisplayInfo` |
| `GenericDataGridHelper._activeMergeStore` (静态) | `EntityMergeStore._index` (实例) |
| `GenericDataGridHelper.SetActiveStores` | `ModGameDataTabsView` 直接持有 `ReferenceIndex` |
| `GenericDataGridHelper.PeekRequested` (静态 delegate) | `INavigationRouter.Peek` |
| `GenericDataGridHelper.NavigateToReferenceForce` | `INavigationRouter.Navigate` + `Peek` |
| `GenericDataGridHelper.RegisterNavigateTarget` | `INavigationRouter.RegisterTarget` |
| `SearchableDataGrid` 中的列索引计算 (Bug 2) | 列元数据缓存 `Dictionary<DataGridColumn, RefMeta>` |

### 7.3 改造顺序

```
Phase 1 — 解析层独立 (不破坏现有功能)
  1. 创建 ReferenceParser 静态类
  2. 将 ReferenceHelper 方法迁移进去
  3. 添加 ResolvedRefSegment / ParsedReferenceField 类型
  4. 现有 GDH 内部调用切换到 ReferenceParser
  5. 添加单元测试 (解析层是纯函数, 最容易测试)

Phase 2 — 索引层实现
  1. 创建 ReferenceIndex 类
  2. 在 EntityMergeStore 中添加 _index 字段
  3. 在 ModGameDataTabsView 数据加载完成后调用 Build()
  4. Hook 单元格编辑事件 → UpdateField()
  5. 将 GDH.LookupSubjectByRawId 替换为 index.Lookup
  6. Bug 1 在此阶段自然修复 (索引构建时做类型安全比较)

Phase 3 — 导航层重构
  1. 创建 INavigationRouter 接口和实现
  2. ModGameDataTabsView / GameDataTypeTabItem 实现 INavigationTarget
  3. 替换 GDH.NavigateToReferenceForce / NavigateToImpl
  4. 修复 Bug 2 (列元数据缓存)
  5. Bug 3 在此阶段自然修复 (不再依赖全局 _activeMergeStore)

Phase 4 — GDH 清理
  1. 移除 GDH 中已迁移的方法
  2. 移除 GDH 中的静态状态 (active stores / delegates)
  3. ConfigureColumn 保持在 GDH 或独立为 ColumnConfigurator
```

---

## 八、风险与边界条件

### 8.1 复合键 fallback 的歧义

`DecomposeId("418", targetKey="{GroupId}.{SubgroupId}")` 在值不含 `.` 时会 fallback 到 `{"Id": 418}`。这意味着一个按复合主键查找的引用，可能错误匹配到 `Id=418` 而非 `GroupId=418` 的实体。

**缓解**：在索引构建时，当 `DecomposeId` 触发 fallback 路径时记录 Debug 级别日志。在验证系统中增加一条规则检测这种歧义。

### 8.2 SecondaryTarget 的索引策略

混合引用字段（如 `vAttackerConditions` 同时引用 Condition 和 AttackMode）需要特殊处理：

```
// 对每个 extractedId，同时索引到 primary 和 secondary type
_forwardIndex[(primaryType, lookupKey)]   = entityId;
_forwardIndex[(secondaryType, lookupKey)] = entityId;
```

运行时查找：先查 primary type → 未找到再查 secondary type。这与当前 `ResolveWithSecondary` 的行为一致。

### 8.3 全量构建的性能

最坏情况：合并视图加载 24 个表、合计 ~5000 个实体、每个实体约 3-5 个引用字段。全量构建需要遍历约 5000 × 4 = 20000 个字段调用。每个字段的解析是纯字符串操作（无 IO、无网络），预计耗时 < 500ms。如果实测超过 1 秒，可以将 `Build` 放到 `Task.Run` 中后台执行，构建期间 UI 显示"索引中..."。

### 8.4 索引内存占用

正向索引的每个条目约 100-120 字节（两个 string key + 一个 string value + Dictionary overhead）。20000 个条目约 2-2.5 MB。反向索引类似。总体内存增加约 5-6 MB，对桌面应用完全可接受。

---

## 九、实施状态

| Phase | 内容 | 状态 | 日期 |
|-------|------|:--:|------|
| Phase 1 | 解析层独立（ReferenceParser） | ✅ | 2026-06-08 |
| Phase 2 | 索引层实现（ReferenceIndex） | ✅ | 2026-06-08 |
| Phase 3 | 导航层重构（INavigationRouter） | ✅ | 2026-06-09 |
| Phase 4 | GDH 清理（去静态化） | ✅ | 2026-06-09 |
| Phase 5 | ReferenceResolver API 清理 | ✅ | 2026-06-11 |
| Phase 6 | IReferenceResolver 接口 + DataGrid 统一 | ✅ | 2026-06-11 |
| Phase 7 | ReferenceIndex 磁盘持久化 + BrowserStore 修复 | ✅ | 2026-06-11 |

### 已过时的章节
> 以下章节描述的实现已于 Phase 5-6 被替换，仅保留作为历史参考：
- **六、6.1 反向引用查询** — `FindReverseReferences` 全量扫描已删除，改用 `store.Index.ReverseLookup()` → `ResolveReverseRefs()`
- **七、7.2 移除的部分** 中 `FindBestMatch` / `LookupSubjectByRawId` — Phase 6 已改为委托 `IReferenceResolver.LookupSubject`
- **六、6.4 可视化器集成** 中的示例 `index.Lookup(typeof(TreasureTable), "86.6")` — 正确，但实际使用应通过 `IReferenceResolver.LookupRef/LookupSubject`

### Phase 3 实施记录
- 新增：`INavigationTarget.cs`, `INavigationRouter.cs`, `NavigationRouter.cs`
- `ModGameDataTabsView` 实现 `INavigationTarget`（Priority=50），Attach/Detach 时注册/注销
- `DocumentWorkspaceViewModel` 将 PeekHandler 设在 Router 上
- `GenericDataGridHelper` 移除 `_activeViews`, `PeekRequested`, `RegisterNavigateTarget`, `NavigateToImpl` 等静态状态
- 导航路径：`SearchableDataGrid` → `GDH.NavigateToReferenceForce` → `Router.Navigate`(责任链) + `Router.Peek`
- Bug 3（多实例静态竞争）自然修复

### Phase 4 实施记录
- 移除 GDH 中的 `_activeViews`, `PeekRequested`, `IsPeekPinned`, `NavigateToImpl`
- `NavigateTo` / `NavigateToByEntityId` 保留为薄包装，委托给 Router + Index
- ~~`FindBestMatch` / `LookupSubjectByRawId` 保留为 ConfigureColumn 内联处理器使用~~ → Phase 6 后已改为委托 `ReferenceResolver.Instance.LookupSubject`
- `SetActiveStores` / 静态属性委托保留（Converter 兼容性）

### Phase 5 — ReferenceResolver API 清理 (v0.22.0-dev) | 2026-06-11

**删除的 API**：

| 方法 | 原因 | 替代 |
|------|------|------|
| `ReferenceResolver.FindByKey<T>()` | 绕过 ReferenceIndex，自建遍历逻辑 | `ReferenceResolver.LookupRef<T>()` |
| `ReferenceResolver.GetDedupedInt<T>()` | 全局最高 ModId 优先，无视来源实体上下文 | 批量: `GDH.GetEntities<T>()`；单次: `LookupRef<T>()` |
| `ReferenceResolver.GetDedupedComposite<T>()` | 同上 | `GDH.GetCompositeEntities<T>()` |
| `ReferenceResolver.GetDedupedList<T>()` | 同上 | `GDH.GetDedupedEntities<T>()` |
| `ReferenceResolver.FindReverseReferences()` | 全量 O(n*m) 扫描，不查 ReferenceIndex | 批量: `ReferenceResolver.ResolveReverseRefs(store, entityId)` |
| `ReferenceResolver.ResolveSubject()` | 零外部调用 | 删除 |
| `ReferenceResolver.ResolveMultiRef()` | 零外部调用 | 删除 |
| `ReferenceResolver.CreateNavItem()` | UI 辅助，VisHelper 已有 NavLeaf | 删除 |
| `ReferenceResolver.WireNavOnCtrlClick()` | 零外部调用 | 删除 |

**迁移统计**：

| 模式 | 迁移前 | 迁移后 |
|------|--------|--------|
| `FindByKey` 调用点 | 10 个 | 全部改为 `LookupRef<T>(sourceEntity, propName, seg)` |
| `GetDedupedInt` 调用点 | ~25 个 | visualizers → `LookupRef`，editors → `GDH.GetEntities<T>()` |
| `GetDedupedComposite/List` | 4 个 | → `GDH.GetCompositeEntities/GetDedupedEntities<T>()` |
| `FindReverseReferences` | 7 个 | → `ReferenceResolver.ResolveReverseRefs(store, entityId)` |

### Phase 6 — IReferenceResolver 接口 + DataGrid 统一 (v0.22.0-dev) | 2026-06-11

**问题**：Phase 5 后，DataGrid 的 `ConfigureColumn` → `LookupSubjectByRawId` 仍然有自己的一套 FindBestMatch O(n) 兜底逻辑，与可视化器的 `LookupRef` 走的不同路径，重复造轮子。

**方案**：
1. 定义 `IReferenceResolver` 接口，声明所有正规解析入口
2. `ReferenceResolver` 从 static class 改为 instance class，实现 `IReferenceResolver`
3. `GDH.LookupSubjectByRawId` 改为一行委托给 `ReferenceResolver.Instance.LookupSubject`
4. 砍掉 `FindBestMatch` + `SubjectCache` 的 O(n) 兜底

**新增文件**：
| 文件 | 说明 |
|------|------|
| `Helper/IReferenceResolver.cs` | 引用解析接口，定义 `LookupRef/LookupSubject/ReverseLookup/NavigateTo/NavigateToByKey/NavigateToByKeyFor` |

**接口定义**：
```csharp
public interface IReferenceResolver
{
    T? LookupRef<T>(IEntity, string propertyName, string rawId);
    string? LookupSubject(string srcEid, string propName, Type, string rawId, Type? secondary);
    IReadOnlyList<...> ReverseLookup(EntityMergeStore store, string entityId);
    void NavigateTo(Type, string entityId);
    void NavigateToByKey<T>(int key);
    void NavigateToByKeyFor<T>(int key, IEntity sourceEntity);
}
```

**调用端变更（~80 处）**：
| 之前 | 之后 |
|------|------|
| `ReferenceResolver.LookupRef<T>(...)` | `ReferenceResolver.Instance.LookupRef<T>(...)` |
| `ReferenceResolver.NavigateTo(t, id)` | `ReferenceResolver.Instance.NavigateTo(t, id)` |

**DI 注册**：
```csharp
services.AddSingleton<IReferenceResolver>(ReferenceResolver.Instance);
```

**DataGrid ConfigureColumn 变更**：
```csharp
// 之前: 自建 30 行 LookupSubjectByRawId → index.LookupDisplay → FindBestMatch O(n) 兜底
// 之后: 一行委托，纯索引
private static string? LookupSubjectByRawId(...)
    => ReferenceResolver.Instance.LookupSubject(sourceEntityId, propertyName, entityType, rawId, secondaryEntityType);
```

### Phase 7 — ReferenceIndex 磁盘持久化 + BrowserStore 修复 (v0.22.0-dev) | 2026-06-11

**Bug**: `TryLoadFromDiskCache` 只恢复了 `GlobalBrowserCache`，但没有创建 `BrowserStore` 和 `ReferenceIndex`。`_indexBuilt = true` 欺骗调用方"已完成"，实际 `GDH.BrowserStore` 为 null，所有引用解析返回 raw 文本。

**另一个 Bug**: `LookupRef`/`LookupSubject`/`NavigateToByKeyFor` 只查 `ActiveMergeStore`，数据浏览器只用 `BrowserStore`，Index 路径全跳过。

**修复**：
| 修复 | 文件 | 改动 |
|------|------|------|
| store 回退 | `ReferenceResolver.cs` | 三处 `ActiveMergeStore` → `ActiveMergeStore ?? BrowserStore` |
| Index 持久化 | `ReferenceIndex.cs` | 新增 `SaveToDisk(path)` / `TryLoadFromDisk(path)`，序列化全部字典 |
| 缓存恢复 | `Documents.cs` | `RebuildBrowserIndexAsync` 始终加载实体，优先从磁盘恢复 ReferenceIndex |
| 失效清理 | `Documents.cs` | `InvalidateIndex` 同时删除 `IndexCachePath` |
| **MergedId 一致性** | `Documents.cs` | 浏览器 MergedId 计算改为与 `MergeService.ComputeTypeMerge` 一致：ns="0"→pk，ns≠"0"→自增 |

**磁盘缓存文件**：
- `browser_index_cache.json` — GlobalBrowserCache（轻量 lookup）
- `browser_reference_index.json` — ReferenceIndex 全量（forward/reverse/display/merged/bizKey）

**启动流程**：
```
RebuildBrowserIndexAsync:
  1. 始终从 DB 加载实体 → ReferenceLookups（有索引，快）
  2. store.Index.TryLoadFromDisk(indexCache)  
     ├─ 命中 → 跳过 BuildAsync（省 O(n*m) 遍历）
     └─ 未命中 → BuildAsync → SaveToDisk(indexCache)
  3. GDH.BrowserStore = store  ← 始终设置
  4. _indexBuilt = true        ← Store 就绪后才标记
```

### Bug 记录 — 引用解析

| # | Bug | 根因 | 修复 | 日期 |
|---|-----|------|------|------|
| B1 | `.308子弹变成AA电池` | `FindByKey` 和 `GetDedupedInt` fallback 到最高 ModId，选了错误 mod 的实体 | 删除两方法，统一 `LookupRef`（同 mod 优先） | 2026-06-11 |
| B2 | Detail 引用全部显示 raw 文本 | `ReferenceResolver` 只查 `ActiveMergeStore`，BrowserStore 为 null 时 Index 路径跳过，fallback 也失败 | `ActiveMergeStore ?? BrowserStore` | 2026-06-11 |
| B3 | DataGrid 引用列解析走自建 FindBestMatch O(n) | `ConfigureColumn` → `LookupSubjectByRawId` 有 Index + FindBestMatch 双路径，Index 未命中时 O(n) 扫描 | 改为委托 `ReferenceResolver.Instance.LookupSubject`，纯 Index | 2026-06-11 |
| B4 | `_indexBuilt=true` 但 BrowserStore 为 null | `TryLoadFromDiskCache` 只恢复 GlobalBrowserCache，不创建 BrowserStore | 重构 `RebuildBrowserIndexAsync`，始终创建 Store；ReferenceIndex 磁盘持久化 | 2026-06-11 |
| B5 | `InvalidateIndex` 后 rebuild 每次从头构建 | ReferenceIndex 未持久化到磁盘 | ReferenceIndex.SaveToDisk + TryLoadFromDisk | 2026-06-11 |
| B6 | 浏览器视图 MergedId 与合并视图不一致 | 浏览器曾将所有实体 MergedId=主键，合并视图对 insert-space 实体用自增 MergedId。同一实体在两个视图中 mid 不同（如 pk=3→mid=3 vs mid=1204），无前缀引用查到不同实体 | Documents.RebuildBrowserIndexAsync 改为与 MergeService.ComputeTypeMerge 一致 | 2026-06-11 |
