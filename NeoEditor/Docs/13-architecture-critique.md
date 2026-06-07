# NeoEditor 架构评审与改进建议

> 日期: 2026-06-06 | 基于 Stage 1-13 开发过程中暴露的问题

---

## 一、解耦设计不合理——问题反复出现的原因

### 1.1 核心病灶：`GenericDataGridHelper` 静态 God Object

```
51KB, 960行, 静态类
├── 11 个静态可变后备集合 (_fallbackEditedCells, _fallbackOverridden, ...)
├── 2 个活跃 store 引用 (_activeMergeStore?, _activeEditStore?)
├── 12 个委托属性 (EditedCells → active?.EditedCells ?? fallback)
├── 4 个静态事件 (CellEditCommitted, CloneRowRequested, ...)
├── 4 个静态 Action 回调 (OnShowAllRequest, OnCellEdited, ...)
├── 3 个静态 bool (IsPeekPinned, _ctrlWasPressed, ...)
└── 600 行 ConfigureColumn 方法
```

**为什么问题反复出现**：

- `SetActiveStores` 是多 View 竞争的唯一全局状态点。Stage 13 的行背景丢失 bug 根源就是这里——View A 设了 active，View B 在 A 未完成读之前覆盖。修了三次才找到根因。
- 后备集合 (`_fallback*`) 和活跃 store (`_active*`) 的双轨设计让数据可能在两个地方之间"漂移"。PushEditStateToGrid 先读 active，active 被覆盖后又 fallback 到空集合。
- `EditCellCommand.Execute()` 直接写 `GenericDataGridHelper.EditedCells`——命令层跨层依赖 Helper 层，undo 不清除，EditedCells 只增不减。

**建议**：拆分为有限的、有明确所有权的服务：
- `IEditSessionStore` — 每个 View 自己的编辑状态，不暴露静态访问
- `IMergeViewState` — 合并视图计算结果的不可变快照
- `IColumnConfigurationService` — 列生成（ConfigureColumn）独立为服务

### 1.2 `ModGameDataTabsView` 是 3390 行的上帝类

```
职责清单（>20 项）:
├── Tab 管理 (创建/切换/缓存/销毁)
├── 数据加载 (单 Mod + 合并视图，两种完全不同的加载路径)
├── 数据保存 (Quick Save / Full Save / Export XML / DB 持久化)
├── Undo/Redo 编排 (CommandHistory 的所有权 + 事件连线)
├── 实体导航 (Ctrl+Click + back-stack)
├── 单元格编辑管道 (事件接收 → 命令创建 → Dirty 追踪)
├── 查找替换面板 (显隐 + 剪贴板操作)
├── 复制粘贴 (内部 buffer + TSV 解析)
├── CSV 导入导出
├── 依赖分析 (触发 + 结果显示)
├── 冲突管理 (检测 + 展示)
├── Workspace 持久化 (Command 日志 + Snapshot 周期 + 崩溃恢复)
├── 自动保存定时器 (DispatcherTimer)
├── 键盘快捷键 (全局 KeyDown + 8 个快捷键)
├── 过滤 (防抖 + FilterService 委托)
├── UI 状态 (IsLoading / CanUndo / CanRedo / IsMergeView / ... / 20+ 属性)
├── 列可见性 (config.json 持久化)
├── 覆盖链展示
├── 调试状态栏
└── 嵌套子类 GameDataTypeTabItem (ViewModel-in-View)
```

**为什么问题反复出现**：

- **混合通信机制**：这个类同时用静态事件（`MergeViewDirtyChanged`）、`GenericDataGridHelper` 事件（`CellEditCommitted`）、DI 注入（`IWorkspacePersistenceService`）、`App.Notification` 静态调用、以及 `WeakReferenceMessenger`（仅 1 处）。同一个人修改代码时在不同地方用不同机制，导致修 A 时破坏 B 的通信链。
- **长方法不可测试**：`ReloadMergeTabsAsync` 370 行，包含合并算法、mod 自动加载、EntityId 计算、覆盖链构建、字段来源检测、冲突检测、排序、过滤。没有单元测试覆盖，任何修改都是盲改。
- **缓存状态泄露**：`TabSnapshotCache` 是静态 dictionary，多 View 共享。Stage 13 的 store 替换修复（命中缓存时 `EditStore = cached.EditStore` 替换字段）是个补丁——本质问题是缓存和字段所有权分离。

**建议**：拆分为：
- `MergeViewLoader` — 合并视图加载（从 ReloadMergeTabsAsync 提取）
- `SingleModLoader` — 单 Mod 加载（从 ReloadTabsAsync 提取）
- `ModDataSaveCoordinator` — 保存流程编排
- `ModDataEditSession` — 编辑会话（CommandHistory + Dirty 追踪 + CopyBuffer）
- TabViewModel — 每个 Tab 一个 ViewModel，`GameDataTypeTabItem` 从嵌套类提升为独立文件

### 1.3 通信机制碎片化

| 机制 | 使用位置 | 问题 |
|------|---------|------|
| 静态事件 (ModGameDataTabsView 自身) | 7 个 Action | 全局可变，无类型安全，多 View 时互相覆盖 |
| GDH 静态事件 | 4 个 event + 4 个 Action | 耦合到 God Object |
| WeakReferenceMessenger | 仅 1 处 (line 860) | 存在但不统一使用 |
| App.Notification 静态调用 | 40+ 处 | 绕过 DI，不可测试 |
| App.ServiceProvider.GetRequiredService | 多处 | Service Locator 反模式 |

**建议**：统一到 `WeakReferenceMessenger`（CommunityToolkit 已有），定义消息契约、每个消息是独立 record class。静态事件和 GDH 桥接全部移除。

### 1.4 命令序列化通过反射读取私有字段

```csharp
// CommandSerializer.cs — 反模式
var entity = (IEntity)type.GetField("_entity", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cmd)!;
```

私用字段重命名 → 运行时静默失败。`BatchEditCommand` 用 `ValueTuple` 的 `Item1`/`Item2` 字段名（编译生成的），极脆弱。

**建议**：每个命令实现 `ISerializableCommand`，暴露 `SerializedData` 属性，自行负责序列化。

---

## 二、日志体系缺陷——难以量化和定位问题

### 2.1 当前状态

- **Serilog** 已配置（Console + File，小时滚动）
- **ILogger** 注入到主 View 和少数服务
- `SearchableDataGrid` 最近才注入 `ILogger`（Stage 13 调试过程中）
- `GenericDataGridHelper` 使用 `Serilog.Log.Logger` 静态调用（无 DI）
- 多处使用 `Console.WriteLine` 而非结构化日志

### 2.2 具体缺陷

| 缺陷 | 影响 | 示例 |
|------|------|------|
| **无链路追踪 ID** | 多 View 并发时无法区分事件归属 | 同一秒内 View A 和 View B 的 PushEdit 日志混在一起，必须靠 ESHash 手动区分 |
| **噪声多** | 每次 RebuildFilter 输出 24 行（每个表一行），刷屏 | 462K 行日志中 ~400K 是重复的 filter 输出 |
| **关键事件无结构化上下文** | 出现异常时缺少上下文还原信息 | PushEdit 显示 `editedCells=0` 但不记录 WHO 调用了 SetActiveStores |
| **异常被吞** | 异步 fire-and-forget 不记录异常 | `_ = QuickSaveAsync()` 如果失败无日志 |
| **边界条件无日志** | 缓存命中/未命中、store 是否为 null 等关键决策点无记录 | 切换标签页丢失数据调试了 6 轮才找到 store 替换问题 |
| **Debug/Info 混用** | `Debug.WriteLine` 改为 `LogInformation` 后，所有调试日志都打在生产级别 | 滚动文件飙到 462K 行/小时 |

### 2.3 建议

**链路追踪**：
```csharp
// 每个 View 实例分配一个 correlationId
private readonly Guid _viewId = Guid.NewGuid();
// 所有日志带 viewId
_logger.LogInformation("[{ViewId}] PushEdit editedCells={EC}", _viewId, ...);
```

**事件级别分层**：
- `Information` — 用户操作（Save/Load/Export 成功失败）
- `Debug` — 状态转换（cache hit/miss, store replace, command persist）
- `Verbose` — 行级渲染（LoadingRow, RefreshBG）— 默认关闭，开关打开
- `Warning` — 警告但不影响功能
- `Error` — 异常

**日志契约**：每个关键操作记录 (操作, 前置状态, 后置状态)。例如 `SetActiveStores` 记录 oldStore hash、newStore hash、newStore 中已有的 editedCells 数。

**结构化而非字符串插值**：当前大量 `$"..."` 字符串插值，Serilog 的结构化模板 `{Placeholder}` 在部分地方已使用但不统一。

---

## 三、代码实现面向过程——扩展性差

### 3.1 Merge 算法嵌入 View 方法中

`ReloadMergeTabsAsync`（370 行）在 View 的 code-behind 中实现了完整的合并算法：
- Phase 1: Game 基础数据打底
- Phase 2: Merge Mod 覆盖 / Insert Mod 追加
- Phase 3: 败者检测
- Phase 4: 合并自增 ID 计算
- Phase 5: 覆盖链构建
- Phase 6: 字段来源 + 冲突检测

**没有任何抽象**。添加新的合并策略（如"冲突自动仲裁"）需要修改这个 370 行的方法。合并结果直接写入 `GenericDataGridHelper` 的静态 dictionary——没有返回值，只有副作用。

**建议**：
```csharp
interface IMergeStrategy
{
    MergeResult Merge(IReadOnlyList<ModEntities> modsInLoadOrder);
}

class MergeResult
{
    ImmutableDictionary<string, IEntity> Winners { get; }
    ImmutableHashSet<string> Overridden { get; }
    ImmutableDictionary<string, List<OverlayChainEntry>> OverlayChains { get; }
    ImmutableDictionary<(string, string), string> FieldSources { get; }
    ImmutableHashSet<(string, string)> FieldConflicts { get; }
}
```

### 3.2 实体发现通过全局扫描

```csharp
// Constants.cs — 静态构造时扫描整个 Assembly
GameTypes = typeof(IEntity).Assembly.GetTypes()
    .Where(type => type.IsClass && !type.IsAbstract && type != typeof(IEntity) ...)
    .ToDictionary(type => type.Name, type => type);
```

添加新实体类型自动生效——这是优点也是缺点。没有编译期验证所有实体类型是否被正确处理，没有显式注册。如果新实体的主键不是 `id` 或 `nID`，会在运行时崩溃。

### 3.3 数据流全程基于 `object` 类型

`ObservableCollection<object>` 是核心数据结构。所有 Table 的实体放在同一个 `object` 集合中。类型安全全靠 `OfType<T>()` 运行时过滤。DataGrid 绑定到 `IEnumerable`（不是 `IEnumerable<T>`）。反射用于属性读写 (`PropertyInfo.SetValue/GetValue`)。

**后果**：
- 编译期无法检测类型错误
- 重构实体属性时没有 IDE 支持
- 装箱/拆箱开销

### 3.4 保存流程硬编码

三种保存路径（QuickSave / Save & Export / Launch）都在 View 中以 async void 方法实现，切换保存策略需要改代码。没有 `ISaveStrategy` 抽象。

### 3.5 无接口的"服务"类

18 个服务类中 **11 个没有接口**：`LocalizationService`, `PhpParser`, `XmlParser`, `CsvImportExportService`, `DataExportService`, `CustomEditorRegistry`, `ImageService`, `SearchService`, `FieldDescriptionService`, `FilterService`, `DependencyAnalysisService`。消费者直接依赖具体类，无法 mock 或替换。

### 3.6 命令层无序列化契约

`CommandSerializer` 用反射挖私有字段，命令类没有暴露序列化接口。添加新命令类型需要在 `CommandSerializer` 的 switch 中加 case + 添加新的反射访问代码。

---

## 四、改进路线图

### Phase A：止血（低风险，高收益）

| # | 项目 | 说明 |
|---|------|------|
| 1 | **统一消息机制** | 所有静态事件 (`MergeViewDirtyChanged`, `SaveRequested`, ...) 迁移到 `WeakReferenceMessenger`，每个消息定义为 record |
| 2 | **GDH 去静态化** | `SetActiveStores` 移除，改为每个 View 持有自己的 `EditSessionStore`，通过消息或 DI scope 传递给子控件 |
| 3 | **链路追踪 ID** | 每个 View/Service 构造时生成 `Guid`，所有日志带 ID |
| 4 | **日志级别分层** | RebuildFilter 等高频日志降级到 `Debug`，行渲染降级到 `Verbose`，关键状态转换保持 `Information` |
| 5 | **异步异常处理** | 所有 `_ = AsyncMethod()` 替换为带 try-catch + 日志的 `FireAndForget(AsyncMethod, "description")` 工具方法 |

### Phase B：解耦（中等重构）

| # | 项目 | 说明 |
|---|------|------|
| 6 | **Merge 算法提取** | `ReloadMergeTabsAsync` 的合并逻辑提取到 `MergeService`，返回不可变的 `MergeResult` |
| 7 | **ModGameDataTabsView 拆分** | 拆出 `MergeViewLoader`, `SingleModLoader`, `ModDataSaveCoordinator` |
| 8 | **命令序列化契约** | 每个命令实现 `ISerializableCommand` 接口，移除反射读取私有字段 |
| 9 | **服务接口补齐** | 给 `XmlParser`, `CsvImportExportService`, `DataExportService` 等加接口，注册到 DI |

### Phase C：架构提升（长期）

| # | 项目 | 说明 |
|---|------|------|
| 10 | **编辑会话 ViewModel** | `GameDataTypeTabItem` 提升为真正的 ViewModel，每个 Tab 有自己的 VM，View 只做绑定 |
| 11 | **EntityStore 替代 ObservableCollection<object>** | 类型安全的数据访问层，编译期验证 |
| 12 | **Merge 策略可插拔** | `IMergeStrategy` 接口，支持不同的合并行为 |
| 13 | **保存策略可插拔** | `ISaveStrategy` 接口 |
| 14 | **单元测试覆盖** | 合并算法、命令序列化、保存流程——当前零覆盖 |
