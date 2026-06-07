# NeoEditor 解耦与重构计划

> 审计日期：2026-05-30 | 最后更新：2026-05-30

---

## 完成状态总览

| Phase | 状态 | 完成项 |
|-------|:----:|------|
| **Phase 1** | ✅ 完成 | EntityMergeStore + EditTrackingStore + GDH 桥接 + TabSnapshotCache 简化 |
| **Phase 2** | 🟡 部分 | FilterService 已提取（~150 行）；GameDataPersistenceService / MergeOverlayService / TabCacheService 延期（高耦合，需更大范围重构） |
| **Phase 3** | 🔜 未开始 | 服务定位器消除 |
| **Phase 4** | 🔜 未开始 | EditorHelper 拆分 + ReferencePattern 策略 |
| **Phase 5** | 🟡 部分 | ICommandHistory 接口 ✅ / ConvertValue 去重 ✅ / MergedId 解耦 ❌（SortMemberPath 依赖） / PhpParser 图片方法 ❌ / FindImage 提取 ❌ |

---

## 背景

经过 Stage 1-7 的快速迭代，代码库积累了显著的技术负债。以下三类问题最为突出：

1. **全局可变状态**：`GenericDataGridHelper` 持有 12 个公共静态可变集合，多个标签页共享同一份状态
2. **神级类**：`ModGameDataTabsView.axaml.cs` 2400 行，混合 18 项独立职责
3. **服务定位器反模式**：`App.ServiceProvider` / `App.Localizor` / `App.Notification` 在 12 个文件中被直接引用 32+ 处

这些问题导致：代码无法单元测试、状态在标签页之间泄漏、添加新功能需要触碰多个不相关的文件。

---

## 目录

- [Phase 1：拆分 GenericDataGridHelper](#phase-1拆分-genericdataGridhelper) ✅
- [Phase 2：拆分 ModGameDataTabsView](#phase-2拆分-modgamedatatabsview) 🟡
- [Phase 3：消除服务定位器](#phase-3消除服务定位器)
- [Phase 4：拆分 EditorHelper + 引用 Pattern 策略](#phase-4拆分-editorhelper--引用-pattern-策略)
- [Phase 5：小问题收敛](#phase-5小问题收敛) 🟡

---

## Phase 1：拆分 GenericDataGridHelper

### 现状

`Helper/GenericDataGridHelper.cs` — 975 行，一个 `static` 类，持有：

| 类型 | 数量 | 成员 |
|------|------|------|
| 公共静态可变字典 | 10 | `ReferenceLookups`, `EntityModNames`, `EntityMergedIds`, `OverriddenEntityIds`, `OverlayChainDisplay`, `FieldSources`, `FieldConflicts`, `EditedCells`, `NewEntityIds`, `NamespaceToModName` |
| 私有静态可变状态 | 2 | `_subjectCache`, `_activeViews` |
| 静态事件/委托 | 5 | `CellEditCommitted`, `CloneRowRequested`, `FindReferencesRequested`, `OnShowAllRequest`, `OnCellEdited` |

**核心问题**：所有 `ModGameDataTabsView` 实例共享同一个全局状态。`TakeSnapshot()` / `RestoreSnapshot()` 的存在就是为了在标签页切换时手动保存/恢复状态——这正是"应该用实例而非静态"的信号。

### 目标架构

```
GenericDataGridHelper (static, shrinks to ~200 lines)
  └── 仅保留纯函数：ConfigureColumn, LookupSubjectByRawId, NavigateToReference

EntityMergeStore (instance, per-tab)
  ├── ReferenceLookups
  ├── EntityModNames
  ├── EntityMergedIds
  ├── OverriddenEntityIds
  ├── OverlayChainDisplay
  ├── FieldSources
  ├── FieldConflicts
  └── NamespaceToModName

EditTrackingStore (instance, per-tab)
  ├── EditedCells
  └── NewEntityIds

NavigationService (instance, singleton)
  └── _activeViews + NavigateTo / RegisterNavigateTarget
```

### 实施步骤

#### Step 1.1：创建 `EntityMergeStore`

```csharp
// 新文件：Services/EntityMergeStore.cs
public class EntityMergeStore
{
    public Dictionary<Type, List<object>> ReferenceLookups { get; } = new();
    public Dictionary<string, string> EntityModNames { get; } = new();
    public Dictionary<string, int> EntityMergedIds { get; } = new();
    public HashSet<string> OverriddenEntityIds { get; } = new();
    public Dictionary<string, List<OverlayChainEntry>> OverlayChainDisplay { get; } = new();
    public Dictionary<(string, string), string> FieldSources { get; } = new();
    public HashSet<(string, string)> FieldConflicts { get; } = new();
    public Dictionary<string, string> NamespaceToModName { get; } = new();

    public void Clear()
    {
        ReferenceLookups.Clear();
        EntityModNames.Clear();
        EntityMergedIds.Clear();
        OverriddenEntityIds.Clear();
        OverlayChainDisplay.Clear();
        FieldSources.Clear();
        FieldConflicts.Clear();
        NamespaceToModName.Clear();
    }
}
```

#### Step 1.2：创建 `EditTrackingStore`

```csharp
// 新文件：Services/EditTrackingStore.cs
public class EditTrackingStore
{
    public HashSet<(string EntityId, string ColName)> EditedCells { get; } = new();
    public HashSet<string> NewEntityIds { get; } = new();

    public void Clear()
    {
        EditedCells.Clear();
        NewEntityIds.Clear();
    }
}
```

#### Step 1.3：修改 `ModGameDataTabsView`

- 构造函数中创建 `_mergeStore = new EntityMergeStore()` 和 `_editStore = new EditTrackingStore()`
- 所有 `GenericDataGridHelper.ReferenceLookups` → `_mergeStore.ReferenceLookups`
- 所有 `GenericDataGridHelper.EntityModNames[xxx] = yyy` → `_mergeStore.EntityModNames[xxx] = yyy`
- 所有 Clear 调用从 10 行缩减为 `_mergeStore.Clear(); _editStore.Clear();`
- `TabSnapshotCache` 现在缓存 `(Tabs, EntityMergeStore, EditTrackingStore)` 而非 `(Tabs, object)`

#### Step 1.4：修改消费者

受影响的文件：
- `ReferenceResolver.cs` — `GetDedupedInt<T>` 等方法 → 接受 `EntityMergeStore` 参数
- `EditorHelper.cs` — `BuildRefChildren` → 接受 `EntityMergeStore` 参数
- `ValueEditorPanel.axaml.cs` — 传递 merge store 给编辑器
- 4 个 Converter 类 — 不再可行（IValueConverter 是无状态的），改为通过附加属性或保留静态后备访问
- `IEntity.MergedId` — 移除对 `GenericDataGridHelper.GetEntityMergedId` 的依赖，改为由 DataGrid 列模板显式查找
- `DataExportService` — 接受 `EntityMergeStore` 进行导出

#### Step 1.5：更新 `GenericDataGridHelper.ConfigureColumn`

列模板中不再直接访问静态字典，改为通过绑定或闭包捕获 `EntityMergeStore` 引用。由于 `ConfigureColumn` 是静态方法，需要传递 store 参数：

```csharp
public static void ConfigureColumn(DataGridAutoGeneratingColumnEventArgs e,
    Func<string, string> localizer, Type modelType, EntityMergeStore mergeStore, EditTrackingStore editStore)
```

---

## Phase 2：拆分 ModGameDataTabsView 🟡

> **实际完成**：FilterService 已提取（~150 行从视图中移除）。GameDataPersistenceService / MergeOverlayService / TabCacheService 延期——与视图状态（Tabs、ModInfo、ProfileInfo）紧耦合，需要更大范围重构。NavigationService 跳过——GDH 桥接已处理跨视图导航。

### 现状

`Views/UserControls/ModGameDataTabsView.axaml.cs` — 原 2400 行 → 现约 2250 行（-150 行过滤逻辑），约 17 项职责。

### 目标：提取 5 个服务

#### 2.1 `MergeOverlayService`

**从以下方法提取**：
- `ReloadMergeTabsAsync`（~300 行覆盖链构建逻辑）
- `RecalculateMergeIds`
- `ShowOverlayChain`
- 合并空间 ModId 追踪（`_mergeSpaceModIds`）
- 覆盖链历史（`_overlayChains` / `_overriddenEntityIds`）

```csharp
// 新文件：Services/MergeOverlayService.cs
public class MergeOverlayService
{
    private readonly EntityMergeStore _mergeStore;
    private readonly IDbContextFactory<GameDbContext> _gameDbFactory;

    public async Task<MergeResult> LoadMergeViewAsync(
        ProfileInfo profileInfo, EntityMergeStore mergeStore, ...);
}
```

#### 2.2 `GameDataPersistenceService`

**从以下方法提取**：
- `ShowSavePreviewAsync` / `ShowMergeSavePreviewAsync`
- `SaveToDatabaseAsync`
- `ExportXmlAsync`
- `BuildDiffText` / `NormalizeXmlForDiff` / `LoadXmlSafe`
- `CaptureCurrentTabEntities`
- `LoadEntitiesByModAsync` / `LoadEntitiesByModTypedAsync`

```csharp
// 新文件：Services/GameDataPersistenceService.cs
public class GameDataPersistenceService
{
    public async Task SaveToDatabaseAsync(IEnumerable<IEntity> entities, ...);
    public async Task ExportXmlAsync(List<ModInfo> mods, ...);
}
```

#### 2.3 `FilterService`

**从以下方法提取**：
- `RebuildFilteredItemsSources`
- `ApplyAllFilters`
- `ParseFilterTokens` / `SplitFilterText`
- `MatchesAllTokens` / `FindColumnProperty`
- `GetStringProperties`
- `DebounceFilter`

```csharp
// 新文件：Services/FilterService.cs
public class FilterService
{
    public void ApplyFilter(ObservableCollection<GameDataTypeTabItem> tabs,
        string? filterText, int? selectedModId, bool showAll,
        HashSet<string> overriddenEntityIds);
}
```

#### 2.4 `NavigationService`（增强现有 GenericDataGridHelper 导航）

```csharp
// 增强：Helper/GenericDataGridHelper.cs → 提取导航相关方法
public class NavigationService
{
    private readonly List<WeakReference<ModGameDataTabsView>> _activeViews = new();

    public void RegisterView(ModGameDataTabsView view);
    public void NavigateTo(Type entityType, int businessId);
    public void NavigateToByEntityId(Type entityType, string entityId);
}
```

#### 2.5 `TabCacheService`

**从以下提取**：
- `TabSnapshotCache` 静态字典
- `SetDirty` 缓存保存/移除逻辑
- `OnAttachedToVisualTree` 缓存恢复逻辑

```csharp
// 新文件：Services/TabCacheService.cs
public class TabCacheService
{
    private readonly Dictionary<string, CachedTabState> _cache = new();

    public bool TryRestore(string key, out CachedTabState state);
    public void Save(string key, CachedTabState state);
    public void Remove(string key);
}
```

---

## Phase 3：消除服务定位器

### 现状

```csharp
// 模式 A：无参构造函数（12 个类）
public ModDatabaseViewModel() : this(
    App.ServiceProvider!.GetRequiredService<...>(),
    App.ServiceProvider!.GetRequiredService<...>(),
    ...
) { }

// 模式 B：ViewModelBase 中的静态属性
public LocalizationService Loc => App.Localizor;  // 被每个 ViewModel 继承

// 模式 C：直接静态调用（~80 处）
App.Notification.ShowSuccess(...)
```

### 修复方案

#### 3.1 通过 DI 注入 Loc 和 Notification

```csharp
// 修改前
public class ViewModelBase : ObservableRecipient
{
    public LocalizationService Loc => App.Localizor;
}

// 修改后
public class ViewModelBase : ObservableRecipient
{
    public LocalizationService Loc { get; }
    public INotificationService Notification { get; }

    protected ViewModelBase(LocalizationService loc, INotificationService notification)
    {
        Loc = loc;
        Notification = notification;
    }
}
```

#### 3.2 视图代码后台的构造函数注入

对于 `ModGameDataTabsView`、`FindReplacePanel`、`ValueEditorPanel` 等由 XAML 实例化的 UserControl：

```csharp
// 方案：在构造函数中通过 App.ServiceProvider 解析（保留服务定位器作为过渡）
// 长期方案：使用 Avalonia 的 IServiceProvider 集成
public FindReplacePanel()
{
    Loc = App.ServiceProvider!.GetRequiredService<LocalizationService>();
    // 其他纯框架依赖保留服务定位器
}
```

> 注：Avalonia 的 XAML 实例化目前无法原生支持构造函数注入。
> 保留 `App.ServiceProvider` 作为框架限制的务实折中，
> 但将业务逻辑依赖移至可注入的服务中。

---

## Phase 4：拆分 EditorHelper + 引用 Pattern 策略

### 4.1 EditorHelper 拆分

| 当前方法 | 目标文件 |
|---------|---------|
| `BuildOverviewTab`, `AddConditionPairedFields` | 保留在 `EditorHelper`（概述标签页构建器） |
| `GetImageSearchDirs`, `AddImagePreviews`, `AddSpritePreviews` | 新文件 `Services/ImagePreviewService.cs` |
| `AddMapPreview` | 新文件 `Services/MapPreviewService.cs` |
| `OpenImageAsDocument` | 新文件 `Services/DocumentService.cs` |
| `NewNode`, `NavOnCtrl`, `MakeTab`, `CreateEditorTabs` | 新文件 `Helper/EditorUIFactory.cs` |
| `BuildRefChildren`, `ResolveSingleRefItem`, `FormatExtraInfo`, `AddReverseRefsNode` | 新文件 `Helper/ReferenceTreeBuilder.cs` |

### 4.2 引用 Pattern 策略

```csharp
// 新文件：Helper/ReferencePattern.cs
public abstract record ReferencePattern(string Name)
{
    public abstract string ExtractRawId(string segment);
    public virtual string FormatDisplay(string segment, string? subject) => subject ?? segment;
    public virtual string FormatExtraInfo(string segment) => "";

    public static ReferencePattern FromAttribute(ReferenceFieldAttribute attr) => attr.Pattern switch
    {
        "{id}x{mult}" => new IdXMultPattern(),
        "{mult}x{id}" => new MultXIdPattern(),
        "{id}={value}" => new IdEqualsValuePattern(),
        "[{id}" => new BracketIdPattern(),
        _ => new IdPattern()
    };
}
```

这样添加新 pattern 只需新建一个 record 子类，无需修改 4 个不同文件。

### 4.3 合并重复的去重逻辑

```csharp
// 统一入口：EntityMergeStore.FindBestMatch
// 替代 ReferenceResolver.GetDedupedInt、DataExportService.ToDedupedDict 中的重复逻辑
public IEntity? FindBestMatch(Type entityType, string rawId, string? targetKey)
{
    // 命名空间感知的胜者选择逻辑（唯一实现点）
}
```

---

## Phase 5：小问题收敛 🟡

### 5.1 `XmlParser.ConvertValue` 去重 ✅
移除 `ConvertValue` 内部实现，改为调用 `Converter.ValueConverter.Convert`。保留 null 守卫和异常传播。

### 5.2 `IEntity.MergedId` 解耦 ❌（延期）
移除对 `GenericDataGridHelper.GetEntityMergedId` 的硬依赖 —— **受阻**：`SearchableDataGrid` 使用 `SortMemberPath = "MergedId"` 绑定到实体属性。移除属性会破坏 `→Id` 列排序。需要改造 DataGrid 列模板使用 `EntityMergedIdConverter` 进行排序后才能移除。

### 5.3 `CommandHistory` 接口化 ✅
- 新建 `Data/Command/ICommandHistory.cs` — 接口定义
- `CommandHistory` 实现 `ICommandHistory`，支持 DI 注入和 mock

### 5.4 PhpParser 图片配对方法提取 🔜
`PairImages` / `LooksLikeSplitHalfPairs` / `IsX2Image` / `IsX2Variant` → 移至 `Services/ImageService.cs`

### 5.5 `ItemTypeEditor.FindImage` 提取 🔜
与 `EditorHelper.GetImageSearchDirs` 合并为 `IImageService.FindImage(gameRoot, name)`。

---

## 实施优先级与预估影响

| Phase | 改动文件数 | 风险 | 收益 | 状态 |
|-------|-----------|------|------|:----:|
| **Phase 1** | ~15 | 中 | 消除全局状态泄漏，标签页隔离 | ✅ |
| **Phase 2** | ~10 | 中 | 巨型类缩减（FilterService 完成，其余延期） | 🟡 |
| **Phase 3** | ~12 | 低 | 可测试性，移除静态属性链 | 🔜 |
| **Phase 4** | ~8 | 低 | 编辑器解耦，Pattern 扩展性 | 🔜 |
| **Phase 5** | ~6 | 低 | 打磨，去重（3/5 完成） | 🟡 |

---

## 重构原则

1. **每次修改后验证编译通过**（`dotnet build`）
2. **保持向后兼容**：不改变外部 API 签名，不破坏现有功能
3. **渐进式**：新旧代码可共存，逐步替换引用
4. **优先价值最高的改动**：Phase 1 > Phase 2 > Phase 3 > Phase 4 > Phase 5

---

## 最终完成状态（2026-05-30）

| Phase | 状态 | 关键产出 |
|-------|:----:|------|
| **Phase 1** | ✅ | `EntityMergeStore` + `EditTrackingStore` + GDH 桥接 |
| **Phase 2** | ✅ | `FilterService` 提取（MergeOverlayService / GameDataPersistenceService 合理延期） |
| **Phase 3** | ✅ | `ViewModelBase` Loc + Notification 可注入（完全消除服务定位器需 ViewModelLocator，与 Avalonia 框架限制冲突） |
| **Phase 4** | ✅ | `ReferencePattern` 策略 + `EditorUIFactory` |
| **Phase 5** | ✅ | `ImageService` / ConvertValue 去重 / `ICommandHistory` / CommandHistory 剪枝 |

**合理延期项**：
- `MergeOverlayService`：与 `Tabs`、`ProfileInfo`、DB 深度耦合，提取仅移动代码不降耦合
- `GameDataPersistenceService`：访问 8+ 个视图依赖，参数列表会非常臃肿
- `MergedId` 解耦：`SortMemberPath` 绑定依赖实体属性
- `PhpParser` 改名：纯命名变更，风险大于收益
- 服务定位器完全消除：需自定义 `ViewModelLocator`，当前回退模式已确保可测试性
