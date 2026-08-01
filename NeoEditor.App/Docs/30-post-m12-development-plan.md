# M13+ 开发计划：领域驱动服务架构

> v1.8 · 2026-07-31 · 决策依据见 [25-architecture-decisions.md](25-architecture-decisions.md)
> 上承: M0-M12 插件化架构迁移（全部完成） · [28-plugin-architecture-migration.md](28-plugin-architecture-migration.md)
> 配套 spec: [../spec/README.md](../spec/README.md) · 新增规则 R23-R25（已落地）
> **所有 Phase 全部完成 ✅** · **像素图像生成 G1-G3 全部完成 ✅** · **ProDataGrid 迁移完成 ✅** · **ProDataGrid 内置 Filter 模板 + Column Chooser 完成 ✅** — 详见 §六.5
> 下一阶段: 暂无 · [31](31-prodatagrid-migration-plan.md) ✅ · [32](32-agent-orchestration-plan.md) ✅ · [35 P2](35-tabstrip-listbox-filter-templates-plan.md) ✅

---

## 〇、动机与目标

### 现状

M13+ Phase 7 代码 + 测试全部完成后的 NeoEditor：

| 维度 | 状态 |
|------|------|
| 项目结构 | **22 项目（11 src + 11 test）**，0 Error |
| 测试 | **296/296** 全部通过（Phase 7: +72 / 像素图像生成 G1-G3: +17 / EntityEditor 适配: +2） |
| Plugin 隔离 | R17-R22 全部落地，Plugin 间 0 csproj 引用 |
| 统一写路径 | **Phase 1 ✅** — IHostService 统一 CRUD 入口 |
| 引用类型系统 | **Phase 2 ✅** — 15 实体 ~48 引用属性 string → ReferenceList&lt;IReferenceEntry&gt; |
| KV 引用弹窗 | **Phase 3 ✅** — ReferencePickerDialog + ReferenceFieldEditor |
| DataBrowser 移除 | **Phase 4 ✅** |
| 图片资产管理 | **Phase 5 ✅** — ImageAssetManager Tool Dock |
| Plugin 分类 | **Phase 6 ✅** — PluginKind 三分类 + IServicePlugin + IExtensionPoint + 6 架构测试 |
| CLI | **Phase 7 ✅** — NeoEditor.Plugins.Cli (IServicePlugin, 8 命令, JSON/text 输出) |
| MCP | **Phase 7 ✅** — NeoEditor.Plugins.Mcp (ModelContextProtocol 官方 SDK v2, [McpServerTool] 属性) |
| AI Chat | **Phase 7 ✅** — NeoEditor.Plugins.AiChat (IToolPlugin, OpenAI 兼容 API, function calling loop) |
| Command 去 UI 化 | **Phase 8 ✅** — AddEntity/DeleteEntity 不再耦合 ObservableCollection · HostService + CommandHistory 纯 service · MCP scope undo/redo |
| 核心问题 | ✅ 全部解决 |

### 目标架构

```
                   ┌─────────────────────────────────────────────┐
                   │              App Shell                       │
                   │  (DI + MainWindow + Dock + Sidebar)          │
                   └──┬──────────────────────────────────────┬────┘
                      │          │              │             │
              ┌───────┴──┐  ┌───┴────┐  ┌──────┴────┐  ┌────┴───────┐
              │DataViewer │  │Entity  │  │ImageTools │  │  AiChat     │
              │Workbench  │  │Editor  │  │Workbench  │  │  Workbench  │
              │           │  │Workbnch│  │(含 Asset  │  │(Tool Dock)  │
              │Tool: T     │  │Tool:  │  │ Manager)  │  │            │
              │Doc:      │  │Doc: KVE│  │Tool: Asset│  │            │
              └───────────┘  └────────┘  └───────────┘  └────────────┘
                                                              │
                                              ┌───────────────┤
                                              ▼               ▼
                                   ┌──────────────┐  ┌──────────────┐
                                   │ CLI Plugin   │  │ MCP Plugin   │
                                   │ (Service)    │  │ (Service)    │
                                   └──────────────┘  └──────────────┘
                                                              │
                                              ┌───────────────┤
                                              ▼               ▼
                                   ┌──────────────┐  ┌──────────────┐
                                   │ Feature      │  │ (Future)     │
                                   │ Plugin(s)    │  │              │
                                   └──────────────┘  └──────────────┘


         ┌─────────────────────────────────────────────────────────┐
         │            IHostService (Core/Abstractions)             │
         │  (command mode + dirty tracking + diff + events)        │
         └──────────────────────┬──────────────────────────────────┘
                                │
                    ┌───────────┴────────────┐
                    ▼                        ▼
         ┌──────────────────┐   ┌─────────────────────┐
         │  Repository 层    │   │   HostService       │
         │  (统一抽象基类)    │   │   (Infra 实现)      │
         │                   │   │                     │
         │  ├─ XmlRepo       │   │  包装:              │
         │  ├─ EfRepo        │   │  ├─ CommandHistory  │
         │  └─ CompositeRepo │   │  ├─ WorkspaceSession│
         └──────────────────┘   │  ├─ DiffEngine      │
                                │  └─ EventBus        │
                                └─────────────────────┘

   ┌─────────────────────────────────────────────────────────────┐
   │         Core — 纯领域模型                                    │
   │  IEntity (无 ORM/UI 属性标记)                                │
   │  IReferenceEntry + ReferenceList<T> (引用类型系统)            │
   │  IReferenceResolver (标准引用解析接口)                         │
   │  25 Game Entity (纯 C# POCO)                                 │
   └─────────────────────────────────────────────────────────────┘
```

### 关键设计原则

| 原则 | 说明 |
|------|------|
| **统一写路径** | 所有数据修改经过 `IHostService`，UI、CLI、MCP 同入口 |
| **引用第一公民** | 引用列从 raw string 升格为 `ReferenceList<T>`，附带序列化/解析标准接口 |
| **Repository 统一抽象** | XML 和 EF Core 都是 `IDataRepository<T>` 的实现，共享 diff/value-converter 基类 |
| **Plugin 三分类** | Workbench（UI 组件）/ Service（纯后端）/ Feature（行为扩展），用元数据区分 |
| **扩展点机制** | Feature Plugin 通过 HostService 事件/管道注入行为，不修改 Workbench Plugin 代码 |

---

## 一、Plugin 分类体系

### 1.1 三分类定义

| 分类 | 标签 | 说明 | 示例 |
|:----:|------|------|------|
| **Workbench** | `[PluginKind(Workbench)]` | 新增 UI 组件（Tool Dock / Document）。这是现有的 Plugin 类型。 | DataViewer, EntityEditor, ImageTools, AiChat |
| **Service** | `[PluginKind(Service)]` + `IServicePlugin` | 纯后端服务，无 UI。提供可编程入口或面向外部协议的接口。 | CLI Plugin, MCP Plugin |
| **Feature** | `[PluginKind(Feature)]` | 通过 HostService 扩展点注入行为，修改/增强已有 Workbench 的功能。不直接引用其他 Plugin。 | 数据校验、自动补全、导出格式扩展 |

### 1.2 接口设计

```csharp
// Core/Abstractions/PluginClassification.cs

public enum PluginKind { Workbench, Service, Feature }

[AttributeUsage(AttributeTargets.Class)]
public class PluginKindAttribute : Attribute
{
    public PluginKind Kind { get; }
    public PluginKindAttribute(PluginKind kind) => Kind = kind;
}

// Core/Abstractions/IServicePlugin.cs
/// <summary>
/// 纯后端 Plugin。无 UI 组件，通过 IHostService 与编辑器交互。
/// </summary>
public interface IServicePlugin : IPlugin
{
    // 无 UI 相关方法。
    // InitializeAsync(IPluginContext ctx) 中注册 MCP 服务/命令行处理器等。
}
```

### 1.3 Extension 分类（粒度比 Plugin 更细）

| Extension 类型 | 对应的 Plugin 接口 | 说明 |
|:-------------:|------------------|------|
| **Tool Extension** | `IToolPlugin.CreateToolView()` | 一个 Plugin 可以创建多个 Tool View（L/R/B Dock） |
| **Document Extension** | `IDocumentPlugin.CreateDocument()` | 一个 Plugin 可支持多种实体类型的 Document |
| **Service Extension** | `IServicePlugin` | 一个 Plugin 可暴露多个后端能力（CLI 命令、MCP tools） |

> Plugin 接口即 Extension 定义，不需要单独的显式 Extension 接口。

---

## 二、Repository 统一抽象

### 2.1 设计动机

XML 文件和 EF Core/SQLite 本质上是同一层的概念——都是数据源。两者：

| 功能 | XML | EF Core |
|------|:---:|:-------:|
| 读取实体 | ✅ | ✅ |
| 写入实体 | ✅ | ✅ |
| 生成 Diff | ✅ | ✅ |
| 反序列化 raw → 实体 | ✅ | ✅ |
| 序列化实体 → raw | ✅ | ✅ |
| ValueConverter | ✅ | ✅ |

### 2.2 接口设计

```csharp
// Core/Abstractions/IDataRepository.cs

/// <summary>
/// 统一数据仓库接口。XML / EF Core 都是其实现。
/// </summary>
public interface IDataRepository<T> where T : IEntity
{
    Task<T?> GetByIdAsync(string entityId);
    Task<IReadOnlyList<T>> GetAllAsync();
    Task SaveAsync(T entity);
    Task SaveBatchAsync(IEnumerable<T> entities);
    Task DeleteAsync(string entityId);
    
    /// <summary>获取两个版本的字段级 diff（专用于 Save Preview）</summary>
    Task<IReadOnlyList<DiffEntry>> GetDiffAsync(T before, T after);
    
    /// <summary>ValueConverter：实体 ↔ 存储格式</summary>
    IValueConverter<T, string> ValueConverter { get; }
}

// Core/Abstractions/DiffEntry.cs
public record DiffEntry(
    string PropertyName,    // 字段名
    string? OldValue,       // 旧值（序列化后）
    string? NewValue,       // 新值（序列化后）
    DiffKind Kind           // Modified / Added / Removed
);

public enum DiffKind { Modified, Added, Removed }

// Infra/Data/Repository/RepositoryBase.cs
/// <summary>Repository 基类，封装 diff、value-converter 等共享逻辑</summary>
public abstract class RepositoryBase<T> : IDataRepository<T> where T : IEntity
{
    protected abstract IValueConverter<T, string> CreateValueConverter();
    
    public virtual async Task<IReadOnlyList<DiffEntry>> GetDiffAsync(T before, T after)
    {
        // 反射遍历属性，比较 all [Column]-marked properties
    }
    
    public IValueConverter<T, string> ValueConverter { get; }
}
```

### 2.3 实现策略

- `EfRepository<T>` — 包装现有 DbContext，将现有逻辑迁移到统一接口
- `XmlRepository<T>` — 从 XML 文件直接读/写实体（用于 Mod 文件直接导出）
- `CompositeRepository<T>` — 装饰器模式，合并多个 Repository 的视图
- 导入导出工具（当前 `MergeService`, `DataExportService`）逐步改造为使用 Repository 接口

---

## 三、引用系统提升

### 3.1 引用第一公民设计

引用列从 `string` 升格为类型化的 `ReferenceList<T>`，配套标准解析/解析接口和实体化引用解析。

```
XML text ──→ IReferenceListSerializer.Deserialize() ──→ ReferenceList<T> ──→ IReferenceResolver.ResolveAsync() ──→ IEntity (目标实体)
                ↑                                                              │
          [ReferenceField] 元数据                                               │
          + 解析策略 (分隔符/pattern)                                           │
                                                                                ▼
XML text ←── IReferenceListSerializer.Serialize() ←── ReferenceList<T> ←── (编辑后)
```

### 3.2 引用类型定义

```csharp
// Core/Abstractions/IReferenceEntry.cs
public interface IReferenceEntry
{
    /// <summary>序列化回 XML 可存储的纯文本格式</summary>
    string ToRawString();
    
    /// <summary>显示文本（用于 UI 列表和预览）</summary>
    string DisplayText { get; }
    
    /// <summary>此引用命中的目标实体类型（用于 IReferenceResolver 路由）</summary>
    Type TargetEntityType { get; }
}

// Core/Model/ReferenceEntry.cs
public record EntityRef(string Namespace, string Id) : IReferenceEntry;
public record CompositeRef(int GroupId, int SubgroupId) : IReferenceEntry;
public record DecoratedRef<T>(T Inner, string Prefix, string Suffix) : IReferenceEntry
    where T : IReferenceEntry;
public record ValueAssignmentRef<T>(T Target, double Value) : IReferenceEntry
    where T : IReferenceEntry;

// Core/Model/ReferenceList.cs
public class ReferenceList<T> : ICollection<T>, IEnumerable<T>
    where T : IReferenceEntry
{
    // 内部 List<T>
    // 双向兼容现有 text 格式
}
```

### 3.3 标准引用解析接口

> 用户决策 2026-07-29：引用既是实体，解析也应做成标准 interface。

```csharp
// Core/Abstractions/IReferenceResolver.cs
/// <summary>
/// 标准引用解析接口。统一所有引用列的解析路径。
/// 注入到 HostService 和 Plugin 中，替代当前分散的 EntityLookupService + ReferenceParser 调用。
/// </summary>
public interface IReferenceResolver
{
    /// <summary>解析单条引用 → 目标实体</summary>
    Task<IEntity?> ResolveAsync(IReferenceEntry reference);
    
    /// <summary>批量解析引用列表 → 目标实体列表（含 null，不抛）</summary>
    Task<IReadOnlyList<IEntity?>> ResolveAllAsync<TRef>(ReferenceList<TRef> references)
        where TRef : IReferenceEntry;
    
    /// <summary>解析引用键 → 解析键表达式（用于复杂引用如装饰/赋值）</summary>
    Task<IReadOnlyList<IEntity?>> ResolveKeysAsync(
        string rawValue, ReferenceFieldAttribute metadata);
    
    /// <summary>序列化引用为文本（反向解析）</summary>
    string Serialize(IReferenceEntry entry);
    
    /// <summary>反序列化文本为引用对象（双向兼容旧格式）</summary>
    IReferenceEntry Deserialize(string raw, ReferenceFieldAttribute? metadata);
    
    /// <summary>获取引用字段的解析结果（含搜索建议）</summary>
    Task<ResolveResult> ResolveFieldAsync(
        string entityType, string rawValue, ReferenceFieldAttribute metadata);
}

// 解析结果的完整结构
public record ResolveResult(
    IReadOnlyList<ResolvedRefSegment> Segments,  // 分段解析
    IReadOnlyList<IEntity?> Targets,              // 实际命中的实体
    ReferenceFieldAttribute Metadata,             // 原元数据
    string RawValue                               // 原始文本
);
```

### 3.4 序列化器

```csharp
// Core/Abstractions/IReferenceListSerializer.cs
public interface IReferenceListSerializer
{
    ReferenceList<TRef> Deserialize<TRef>(string raw, ReferenceFieldAttribute? metadata)
        where TRef : IReferenceEntry;
    string Serialize<TRef>(ReferenceList<TRef> list)
        where TRef : IReferenceEntry;
}
```

### 3.5 与现有系统的关系

| 现有组件 | 关系 |
|---------|------|
| `ReferenceParser` (Infra/Helper) | 保留为 `IReferenceListSerializer` 的底层实现 |
| `ReferenceIndex` (Infra/Services) | 保留为 `IReferenceResolver` 的索引底层 |
| `ReferenceIndexService` (Infra/Services) | 保留为可选的 SQLite 后备索引 |
| `IEntityLookupService` (Infra/Services) | 功能合并到 `IReferenceResolver`，逐步弃用 |
| `[ReferenceField]` 属性 (Core/Model) | 保留为元数据标注，不改动 |
| `ParsedRef` / `ResolvedRefSegment` / `ParsedReferenceField` (Infra/Helper) | 保留作为内部中间表示 |

---

## 四、HostService

### 4.1 核心接口

```csharp
// Core/Abstractions/IHostService.cs
public interface IHostService
{
    // ── 查询 ──
    Task<IEntity?> GetEntityAsync(string entityType, string entityId);
    Task<IReadOnlyList<IEntity>> QueryAsync(EntityQuery query);
    
    // ── 写操作（Command 模式） ──
    Task<CommandResult> ExecuteAsync(IEditorCommand command);
    Task<CommandResult> ExecuteBatchAsync(IEnumerable<IEditorCommand> commands);
    Task UndoAsync();
    Task RedoAsync();
    
    // ── 脏追踪（R01 + R09） ──
    ISet<string> DirtyEntities { get; }
    bool HasUnsavedChanges { get; }
    event EventHandler? DirtyStateChanged;
    void MarkEntityDirty(string entityId);
    void MarkEntitiesDirty(IEnumerable<string> entityIds);
    void ClearDirtyEntities();
    void RemoveDirtyEntities(IEnumerable<string> entityIds);
    
    // ── 持久化 ──
    Task SaveAsync(string? entityId = null);
    Task SaveAllAsync();
    Task DiscardAsync(string? entityId = null);
    
    // ── Diff（字段级） ──
    Task<IReadOnlyList<DiffEntry>> GetDiffAsync(string? entityId = null);
    Task<IReadOnlyList<DiffEntry>> GetFullDiffAsync();
    
    // ── 事件（Feature Plugin / UI 订阅） ──
    IObservable<EntityChangedEvent> Changes { get; }
    
    // ── 引用解析（委托给 IReferenceResolver） ──
    IReferenceResolver References { get; }
    
    // ── Repository 访问（委托给 IDataRepository） ──
    IDataRepository<T> Repository<T>() where T : IEntity;
}
```

### 4.2 实现职责

| 功能 | 委托给 | 说明 |
|------|--------|------|
| Command 执行/撤销 | `CommandHistory` | 现有的 4 种 Command |
| 脏追踪 | `WorkspaceSession` | 现有的 dirty state |
| 持久化 | `DataRepository` (EF) + `CommandSerializer` | WAL + DB 双写 |
| Diff 生成 | `RepositoryBase.GetDiffAsync()` | 反射对比 [Column] 属性 |
| 事件 | `IMessenger` + `Subject<EntityChangedEvent>` | 跨 Plugin 通知 |

### 4.3 扩展点（Feature Plugin 基础）

```csharp
// Core/Abstractions/IExtensionPoint.cs
public interface IExtensionPoint<TContext>
{
    string Name { get; }
    int Order { get; }
    Task ExecuteAsync(TContext context);
}

// HostService 上的扩展点注册
public interface IHostService
{
    // ... 前述方法 ...
    
    void RegisterPreSaveHook(IExtensionPoint<PreSaveContext> hook);
    void RegisterPostLoadHook(IExtensionPoint<PostLoadContext> hook);
    void RegisterPreExecuteHook(IExtensionPoint<PreExecuteContext> hook);
}
```

Phase 6（Plugin 分类改造）**只设计扩展点接口**，Hook 系统的完整实现留给 Phase 7 之后评估。

---

## 五、Image Asset Manager Tool Dock

### 5.1 界面示意

```
┌─────────────────────────────────────────────┐
│ 📁 Image Assets (Title Bar)        [🔍] [↻] │
├──────────────────────┬──────────────────────┤
│ 📁 MyMod              │  Preview Pane        │
│  ├── cuePickup.png   │  ┌──────────────┐    │
│  ├── cuePutdown.png  │  │  thumbnail   │    │
│  └── sprite.png      │  │  (fit)       │    │
│ 📁 NSEb               │  └──────────────┘    │
│  ├── item_bottle.png  │  Name: cuePickup.png │
│  ├── icon_water.png   │  Size: 32x32         │
│  │  └── icon_2x.png   │  x2: cuePickup@2x    │
│  └──                  │  Mod: MyMod          │
│ [Floating footer]     │                     │
│ 拖入图片到 Mod 节点    │  [Open] [Edit] [🗑] │
└──────────────────────┴──────────────────────┘
```

### 5.2 功能清单

| 功能 | 实现方式 |
|------|---------|
| 树状浏览 | 按 Mod 分组，展开时读取 `getimages.php` 中的声明 |
| 搜索/过滤 | ViewModel 维护 `FilteredImages` 集合，绑定到 ListBox |
| 预览 | 右侧 InfoPanel，复用现有 `ImagePreviewContent` 的渲染逻辑 |
| 打开图片 | 双击 → `DocumentWorkspaceViewModel.OpenDocument` → `ImageEditorDocument` |
| 外部拖入 | 拖入 Mod 节点 → `ModImagePairDropHandler` 确认弹窗 → 导入 + 更新 `getimages.php` |
| 刷新 | Mod 重载或图片变更时触发 |
| 图片引用 | 右键菜单 "复制引用路径" 供其他编辑器粘贴 |

### 5.3 文件清单（在 ImageTools Plugin 内新增）

| 文件 | 类型 |
|------|------|
| `ViewModels/ImageAssetManagerViewModel.cs` | ViewModel |
| `Views/ImageAssetManagerView.axaml` | View |
| `Views/ImageAssetManagerView.axaml.cs` | Code-behind |
| `Services/ImageAssetManagerService.cs` | 可选：复杂逻辑抽离 |

---

## 六、CLI + MCP + AI Chat

### 6.1 项目结构

```
NeoEditor.Plugins.Cli/                       ← IServicePlugin
├── CliPlugin.cs
├── CliCommandParser.cs                       ← 解析命令行参数
├── Commands/                                  ← 每个子命令一个文件
│   ├── GetEntityCommand.cs
│   ├── EditEntityCommand.cs
│   ├── SaveCommand.cs
│   └── QueryCommand.cs
└── ServiceCollectionExtensions.cs

NeoEditor.Plugins.Mcp/                       ← IServicePlugin (MCP Server)
├── McpPlugin.cs
├── McpServer/                                 ← MCP 协议实现
│   ├── McpSessionManager.cs
│   ├── Tools/                                 ← MCP Tools
│   │   ├── GetEntityTool.cs
│   │   ├── EditEntityTool.cs
│   │   ├── QueryReferencesTool.cs
│   │   └── SaveTool.cs
│   └── Resources/                             ← MCP Resources
│       └── EntityResourceProvider.cs
└── ServiceCollectionExtensions.cs

NeoEditor.Plugins.AiChat/                    ← IToolPlugin (Tool Dock)
├── AiChatPlugin.cs
├── ViewModels/
│   └── AiChatViewModel.cs
├── Views/
│   ├── AiChatView.axaml
│   └── AiChatView.axaml.cs
├── Services/
│   └── ChatService.cs                         ← LLM API 调用 + MCP Client
└── ServiceCollectionExtensions.cs
```

### 6.2 工作原理

```
用户: "创建一把新剑，攻击15，耐久80"
  → AiChat Tool → LLM → 结构化 JSON
      → AI Chat 调用 HostService.ExecuteAsync(AddEntityCommand + EditCellCommands)
          → CommandHistory 持久化到 WAL
          → EntityChangedEvent → UI 刷新
```

### 6.3 通信方式

| 通道 | 传输 | 适用场景 |
|------|:----:|---------|
| AI Chat ↔ MCP Server | 进程内 stdio 模式 | 同一进程，零配置 |
| MCP Server ↔ 外部工具 | stdio / SSE | 未来扩展 |
| CLI ↔ HostService | 直接方法调用 | 命令行模式，直接在 App 进程内调用 |

---

### 六.5 Phase 8: Command 去 UI 化 + 实体中心注册表 ✅

> 完成于 2026-07-30 · 296/296 测试通过 · 0 Error

#### 问题诊断

`AddEntityCommand` 和 `DeleteEntityCommand` 的构造耦合了 UI 层概念：

```csharp
// 旧签名：需要 ObservableCollection + Action onChanged —— UI Tab 才有的东西
new AddEntityCommand(collection, entity, onChanged)
new DeleteEntityCommand(collection, entity, onChanged)
```

对比 `EditCellCommand` / `BatchEditCommand` 没有这个问题——它们只操作 `IEntity` 对象本身的属性。

**后果**：
- MCP/CLI 的 `AddEntity` / `DeleteEntity` 创建临时 `new ObservableCollection()` —— 删的是临时集合，DataGrid 里数据不动
- MCP 未注册 scope（不像 CLI 有 `CliSession`），Undo/Redo 不可用

**根因**：命令把"改什么数据"和"UI 怎么响应"混在一起了。`ObservableCollection` 和 `onChanged` 是 UI Tab 的实现细节，不应该出现在命令接口里。

#### 架构设计：纯 Service 层

`CommandHistory` 和 `HostService` 是 **纯 service**，零 UI 依赖：

```
          CommandHistory              HostService
          ─────────────               ───────────
依赖:       System.*                   System.*
            Core.Abstractions          Core.Abstractions
            (无 Avalonia)              EF Core (无 Avalonia)

         ┌──────────────────────────────────────────────┐
         │              IHostService                     │
         │  ExecuteAsync(cmd, scopeId)   ← 唯一写入口    │
         │  UndoAsync / RedoAsync                        │
         │  _entityCache (中心实体缓存)                   │
         │  _scopes (undo 栈隔离)                         │
         └────┬──────────┬──────────┬───────────────────┘
              │          │          │
        ┌─────┴──┐  ┌───┴───┐  ┌──┴──────────────┐
        │  MCP   │  │  CLI  │  │  UI Tab           │
        │ scope: │  │ scope:│  │ scope: "tab_xxx"  │
        │ "mcp"  │  │ "cli" │  │                    │
        │        │  │       │  │ callback 含:       │
        │ callback│  │callback│ │ cache + collection │
        │ 仅缓存  │  │ 仅缓存 │  │                    │
        └────────┘  └───────┘  └───────────────────┘
        无 UI 集合   无 UI 集合    有 ObservableCollection
```

**关键设计点**：
- `CommandHistory` 和 `HostService` 都在 `NeoEditor.Infra`，只依赖 `System.*` + `Core.Abstractions` + EF Core，**零 Avalonia 引用**
- 命令通过可选 `Action<IEntity>` callback 执行具体操作：MCP/CLI 传 `null` → 只操作缓存；UI Tab 传 `collection.Add/Remove` → 同时操作 UI 集合
- `IHostService.RegisterEntityCollection` 接受的是 `System.Collections.IList`（基接口），不绑定任何 UI 框架
- MCP、CLI、UI 三条路径完全平等，差异只在 scope 隔离和 callbacks 有无

#### 实际实现

| 步骤 | 内容 | 状态 |
|------|------|:--:|
| 8.1 | **IHostService 新增实体缓存 + 集合注册** | ✅ |
| | `ConcurrentDictionary<string, IEntity> _entityCache` — 中心实体注册表 | |
| | `RegisterEntityCollection(scopeId, entityType, IList)` — 接受基接口，不耦合 ObservableCollection | |
| | `GetCachedEntity / GetCachedEntitiesByType / AddEntityToCache / RemoveEntityFromCache` | |
| 8.2 | **重构 AddEntityCommand** | ✅ |
| | 新签名：`AddEntityCommand(entityType, entity, addAction?, removeAction?)` | |
| | Execute/Undo 通过可选 callback 实现，无 callback 时纯数据描述 | |
| 8.3 | **重构 DeleteEntityCommand** | ✅ |
| | 新签名：`DeleteEntityCommand(entityType, entity, removeAction?, addAction?)` | |
| 8.4 | **CommandSerializer 简化** | ✅ |
| | `Deserialize` 去掉 `collectionResolver` 参数 | |
| | `IWorkspacePersistenceService.LoadCommandsAsync` 同样去掉 | |
| 8.5 | **MCP 注册 scope** | ✅ |
| | `McpPlugin.InitializeAsync` → `RegisterCommandScope("mcp", new CommandHistory())` | |
| | MCP 操作现在可 undo/redo，与 CLI 一致 | |
| 8.6 | **所有调用点更新** | ✅ |
| | MCP `EditorTools` — 去掉临时 `ObservableCollection`，传 cache callback | |
| | CLI `CliCommandHandler` — 同上 | |
| | UI `ModGameDataTabsView.Operations` — 传 cache + collection callback | |
| | WAL 重放 (`ModGameDataTabsView.axaml.cs`) — 去掉 `ResolveCollectionForReplay` | |

#### 影响范围（实际）

| 文件 | 改动 |
|------|------|
| `Core/Abstractions/IHostService.cs` | +6 实体缓存/集合注册方法 |
| `Infra/Services/HostService.cs` | + 实体缓存 + ExecuteAsync/ExecuteBatchAsync 统一调度 |
| `Infra/Data/Command/AddEntityCommand.cs` | 去掉 ObservableCollection，改用可选 callback |
| `Infra/Data/Command/DeleteEntityCommand.cs` | 同上 |
| `Infra/Services/CommandSerializer.cs` | `Deserialize` 签名简化 |
| `Infra/Services/WorkspacePersistenceService.cs` | `LoadCommandsAsync` 签名简化 |
| `Plugins/Mcp/McpPlugin.cs` | + scope 注册 |
| `Plugins/Mcp/Tools/EditorTools.cs` | 去掉临时 ObservableCollection |
| `Plugins/Cli/Cli/CliCommandHandler.cs` | 去掉临时 ObservableCollection |
| `App/Views/UserControls/ModGameDataTabsView.Operations.cs` | 新命令构造 + cache callback |
| `App/Views/UserControls/ModGameDataTabsView.axaml.cs` | 5 处 WAL 重放调用去参 |
| `Tests/…/McpToolExecutorTests.cs` | StubHostService 补齐新接口方法 |

---

## 七、Phase 依赖与时间线

### 7.1 Phase 依赖图

```
Phase 1: HostService ◄──── 所有后续的基石 ✅ 已完成
    │
    ├── Phase 2: 引用类型系统 ◄── 依赖 Phase 1 ✅ 已完成
    │       │
    │       └── Phase 3: KV 引用弹窗 ◄── 依赖 Phase 2 ✅ 已完成
    │
    ├── Phase 4: 删 DataBrowser  ◄── 独立，零依赖 ✅ 已完成
    │
    ├── Phase 5: ImageAssetManager ◄── 半独立（IHostService 已到位） ✅ 已完成
    │
    └── Phase 6: Plugin 分类 ◄── 依赖 Phase 1（HostService 事件管道） ✅ 已完成
            │
            └── Phase 7: CLI + MCP + AI Chat ◄── 依赖 Phase 1 + Phase 6

### 7.2 Phase 详情

#### Phase 1: HostService ✅ 已完成 (2026-07-29)

| 步骤 | 内容 | 涉及文件 |
|------|------|---------|
| 1.1 | `IEditorCommand` 提升到 `Core/Abstractions` | ✅ `Infra/Data/Command/IEditorCommand.cs` → `Core/Abstractions/` |
| 1.2 | 创建 `IHostService` 接口（Core） | ✅ 新文件 |
| 1.3 | 创建 `RepositoryBase<T>` 抽象基类 + diff 引擎（Infra） | ✅ 新文件 |
| 1.4 | 实现 `HostService`（包装 CommandHistory + WorkspaceSession + Repository） | ✅ 新文件 |
| 1.5 | 迁移 DataTableService 中的 CRUD 调用 | ✅ ModGameDataTabsView 写路径 |
| 1.6 | 迁移 KVEditor.ApplyChanges 中的写路径 | ✅ 基础设施到位 |
| 1.7 | 迁移 DocumentWorkspaceViewModel 中的保存/撤销 | ✅ Ctrl+Z/Y/Undo/Redo 走 HostService |
| 1.8 | 编写 HostService 单元测试 | ✅ 7 个测试 |
| 1.9 | 编写架构测试确保 R24 合规 | ✅ 3 个架构测试 |

> **交付物**：`dotnet build` 0 Error · 51/51 测试通过 · 所有 CRUD 经 HostService

> **架构指标**：IEditorCommand/ICommandHistory 提升到 Core；HostService 管理 scope 级 undo/redo；R24 规则已落地；+10 测试（HostService 7 + 架构 3）

#### Phase 2: 引用类型系统 ✅ 已完成 (2026-07-30)

| 步骤 | 内容 | 涉及文件 | 状态 |
|------|------|---------|:--:|
| 2.1 | 创建 `Core/Abstractions/IReferenceEntry.cs` | 新文件 | ✅ |
| 2.2 | 创建 `EntityRef`, `CompositeRef`, `DecoratedRef`, `ValueAssignmentRef` | `Core/Model/ReferenceEntryTypes.cs` | ✅ |
| 2.3 | 创建 `ReferenceList<T>` 集合类型 | `Core/Model/ReferenceList.cs` | ✅ |
| 2.4 | 创建 `IReferenceListSerializer` 接口 + Infra 实现（双向兼容旧格式） | `Core/Abstractions/` + `Infra/Helper/ReferenceListSerializer.cs` | ✅ |
| 2.5 | 保留现有 `IReferenceResolver`（Infra），Phase 2 聚焦引用**数据**类型化 | 不变 | ✅ |
| 2.6 | 批量修改 15 个实体的引用属性：`string` → `ReferenceList<IReferenceEntry>` | 15 实体文件 | ✅ |
| 2.7 | 配置 EF Core `ReferenceListStringConverter` + `GameDbContext.OnModelCreating` 自动发现 | Infra/Data/Converters/ + GameDbContext | ✅ |
| 2.8 | `ReferenceParser` + `ReferencePattern` 保留为 `ReferenceListSerializer` 的内部实现 | 无改 | ✅ |
| 2.9 | `EntityMergeStore` / `ReferenceIndex` 反射逻辑兼容新类型（隐式转换保证向后兼容） | 无需大改 | ✅ |

> **交付物**：`dotnet build` 0 Error · 168/168 测试通过 · 15 实体 ~48 引用属性改造完成 · XML/DB 格式不变

> **架构指标**：`ReferenceList<IReferenceEntry>` 通过隐式转换 + `.Split()` 代理向后兼容所有现有 string 操作；EF Core ValueConverter 自动发现 0 逐实体配置；`ReferenceParser` 特征化测试 83 个安全网；新增测试 117 个（83 Parser + 21 Serializer + 13 EntryTypes）
>
> **设计要点**：`EntityRef` 扁平吸收常见修饰符（Negated/Multiplier/MultiplierFirst/Bracketed），避免 `DecoratedRef<T>` 深层嵌套；`ReferenceList<T>.RawText` 保存原始序列化文本，所有修改型操作后由 Serializer 更新

#### Phase 3: KV 引用弹窗 ✅ 已完成 (2026-07-30)

| 步骤 | 内容 | 涉及文件 | 状态 |
|------|------|---------|:--:|
| 3.1 | 创建 `ReferencePickerViewModel` — 搜索/浏览/多选/装饰编辑 | `EntityEditor/ViewModels/ReferencePickerViewModel.cs` | ✅ |
| 3.2 | 创建 `ReferencePickerDialog.axaml/.cs` — 弹窗 UI (Pattern A) | `EntityEditor/Views/ReferencePickerDialog.axaml` + `.cs` | ✅ |
| 3.3 | 创建 `ReferenceFieldEditor.axaml/.cs` — 引用列内联控件（徽章 + 按钮） | `EntityEditor/Views/ReferenceFieldEditor.axaml` + `.cs` | ✅ |
| 3.4 | 更新 `KeyValueEditorView.axaml` — `EditControlType.ReferencePicker` 渲染新控件 | 现有文件修改 | ✅ |
| 3.5 | 更新 `ControlTypeVisibilityConverter` — 引用列不再 fallback 到 TextBox | 现有文件修改 | ✅ |
| 3.6 | 支持多值引用列的增删改（添加/删除单个引用元素） | ReferencePickerViewModel + Dialog | ✅ |
| 3.7 | 支持装饰属性编辑（Multiplier, Negation, Assignment） | ReferencePickerViewModel + Dialog UI | ✅ |
| 3.8 | 预览模式显示引用目标的 display name | ReferenceFieldEditor 内联徽章 + Dialog 预览 | ✅ |

> **交付物已达成**：KV 编辑器中引用列显示 ✏️ 按钮 + 已解析实体徽章 · 点击打开 ReferencePickerDialog 弹窗 · 选择后自动更新字段 · 17 个新单元测试

#### Phase 4: 删 DataBrowser（已 ✅ 完成 2026-07-29）

| 步骤 | 内容 | 状态 |
|------|------|:----:|
| 4.1 | 删除 `DataBrowserViewModel.cs` | ✅ |
| 4.2 | 删除 `DataBrowserView.axaml` + `.cs` | ✅ |
| 4.3 | 删除 Sidebar 注册和 DataTemplate | ✅ |
| 4.4 | 删除 DI 注册 | ✅ |
| 4.5 | 评估 `GameDomain.cs` 中的 `DomainGroup` record（仅有 EntityTypeGroup 被 EntityBrowserDocument 使用，已删除 DomainGroup） | ✅ |

> **交付物已达成**：`dotnet build` 0 Error · App 启动正常 · Sidebar 无 DataBrowser 入口

#### Phase 5: ImageAssetManager ✅ 已完成 (2026-07-30)

| 步骤 | 内容 | 涉及文件 | 状态 |
|------|------|---------|:--:|
| 5.1 | 创建 `ImageAssetManagerViewModel` — 树状浏览 + 搜索过滤 + 预览逻辑 | `ImageTools/ViewModels/ImageAssetManagerViewModel.cs` | ✅ |
| 5.2 | 创建 `ImageAssetManagerView.axaml/.cs` — Tool Dock UI | `ImageTools/Views/ImageAssetManagerView.axaml` + `.cs` | ✅ |
| 5.3 | 创建 `ImageAssetManagerTool` Tool wrapper | `App/ViewModels/MainContent/Documents.cs` | ✅ |
| 5.4 | 实现树构建（Mods 目录扫描 + getimages.php 解析） | ViewModel | ✅ |
| 5.5 | 实现双击 → 打开 ImageDocument | 消息/WeakReferenceMessenger | ✅ |
| 5.6 | 实现刷新和搜索过滤 | ViewModel | ✅ |
| 5.7 | 注册到 DI + 添加到 RightToolPane | `ServiceCollectionExtensions.cs` + `DocumentWorkspaceView.axaml` | ✅ |

> **交付物**：新增 "Image Assets" Tab 在 RightToolPane · TreeView 按 Mod 分组 + 预览面板 + 双击打开 · 搜索过滤 · Refresh 按钮

#### Phase 6: Plugin 分类改造 ✅ 已完成 (2026-07-30)

| 步骤 | 内容 | 涉及文件 | 状态 |
|------|------|---------|:--:|
| 6.1 | 创建 `PluginKind` 枚举 + `PluginKindAttribute` | `Core/Abstractions/PluginKind.cs` + `PluginKindAttribute.cs` | ✅ |
| 6.2 | 创建 `IServicePlugin` 接口 | `Core/Abstractions/IServicePlugin.cs` | ✅ |
| 6.3 | 创建 `IExtensionPoint<TContext>` + 3 Context record | `Core/Abstractions/IExtensionPoint.cs` + `ExtensionContexts.cs` | ✅ |
| 6.4 | IHostService 新增 3 个扩展点注册方法 | `IHostService.cs` | ✅ |
| 6.5 | HostService 实现 hook 存储（List<T>） | `HostService.cs` | ✅ |
| 6.6 | 现有 3 个 Plugin 加 `[PluginKind(Workbench)]` | DataViewer / EntityEditor / ImageTools Plugin | ✅ |
| 6.7 | 编写架构测试（6 个） | `Tests/Core.Tests/Spec/PluginArchitectureTests.cs` | ✅ |

> **交付物**：三分类标识体系生效 · IServicePlugin + IExtensionPoint 接口定义 · 0 破坏性改动 · 6 个架构测试通过 · 新增 Core.Tests → 3 个 Plugin 的程序集引用

#### Phase 7: CLI + MCP + AI Chat ✅ 全部完成 (2026-07-30)

**实际交付物**（与计划有调整，测试已于 2026-07-30 补齐）：

| 步骤 | 内容 | 涉及文件 | 状态 |
|------|------|---------|:--:|
| 7.0 | Core 新增 `IMcpToolProvider` + `IMcpResourceProvider` + `McpToolInfo`（跨 Plugin 桥接，R17 合规） | `Core/Abstractions/` 3 文件 | ✅ |
| 7.1 | 创建 `NeoEditor.Plugins.Cli` 项目 + `CliPlugin` (IServicePlugin) | 新项目 10 文件 | ✅ |
| 7.2 | 实现 CLI：`CliCommandParser` + `CliCommandHandler` (8 命令) + `CliOutputFormatter` (JSON/text) + `CliSession` | `Cli/` 5 文件 | ✅ |
| 7.3 | 创建 `NeoEditor.Plugins.Mcp` 项目 + `McpPlugin` (IServicePlugin) | 新项目 8 文件 | ✅ |
| 7.4 | MCP Server：**改用 `ModelContextProtocol` 官方 C# SDK v2.0**（非手写），`[McpServerTool]` 属性定义 8 工具，`StdioServerTransport` | `Server/` + `Tools/` + `Resources/` | ✅ |
| 7.5 | MCP 工具实现：`EditorTools` 通过 `IHostService` 操作数据，`McpToolExecutor` 实现 `IMcpToolProvider` 供 AI Chat 进程内调用 | `Tools/EditorTools.cs` + `McpToolExecutor.cs` | ✅ |
| 7.6 | 创建 `NeoEditor.Plugins.AiChat` 项目 + `AiChatPlugin` (IToolPlugin, Right Dock, Order=40) | 新项目 8 文件 | ✅ |
| 7.7 | AI Chat：**改用 `Microsoft.Extensions.AI` + `OpenAI` SDK**（模型无关），手动 function-calling 循环，通过 `IMcpToolProvider` 集成 MCP 工具 | `Services/ChatService.cs` + `ViewModels/` + `Views/` | ✅ |
| 7.8 | 构建集成：`App.axaml.cs` + `App.csproj` + `build.sh` + `.sln` 更新，**19 项目全部 0 Error** | 4 文件修改 | ✅ |

**架构决策记录**：
- **MCP**: 选用 `ModelContextProtocol` 2.0.0-preview.3 官方 C# SDK（v2.0 stateless），弃用手写 JSON-RPC
- **AI Chat**: 选用 OpenAI 兼容 API 接入（`OPENAI_API_KEY` / `OPENAI_ENDPOINT` / `OPENAI_MODEL` 环境变量），支持 Ollama / LM Studio / OpenAI 等任意 OpenAI 格式 API。不绑定特定 AI 公司
- **NuGet 新增**: `ModelContextProtocol`, `Microsoft.Extensions.AI`, `Microsoft.Extensions.AI.OpenAI`, `CommunityToolkit.VectorData.InMemory`
- **Agent 编排**: 暂用手动 while 循环实现 function calling，未引入 `Microsoft.Agents.AI`（待 RAG/多 Agent 需求时升级 → [Doc 32](32-agent-orchestration-plan.md)）
- **测试**: Phase 7.1 测试补齐已完成 ✅ — Mcp 17/17 · Cli 40/40 · AiChat 15/15 = 72 新测试 · 架构测试含 6 个 Plugin assembly

| 7.9 | Phase 7.1 测试补齐：创建 Mcp/Cli/AiChat 三测试项目 + 更新 build.sh/.sln/架构测试 | 12 文件新增/修改 | ✅ 2026-07-30 |

> **交付物**：CLI 可操作编辑器 · MCP 协议（官方 SDK）可供外部工具调用 · AI Chat 面板支持 function calling + MCP tool 集成 · 模型无关（OpenAI 兼容 API） · 72 新测试全过

### 7.3 时间线

```
Week   1  2  3  4  5  6  7  8  9  10 11 12 13 14 15 16 17 18 19
Phase 1 [████████████████] ──── Host Service ──── 架构基石 ✅
Phase 2 [        ████████████████] ── 引用类型系统 ✅ (2026-07-30)
Phase 3                     [████] ── KV 引用弹窗 ✅ (2026-07-30)
Phase 4 [██] ── 删 DataBrowser ✅ (2026-07-29)
Phase 5       [████████████] ── ImageAssetManager ✅ (2026-07-30)
Phase 6                   [████] ── Plugin 分类 ✅ (2026-07-30)
Phase 7                        [██████] ── CLI/MCP/AI Chat ✅ (2026-07-30)
Phase 8                               [████] ── Command 去 UI 化 ✅ (2026-07-30)
```

**并行策略**：
- Phase 1-8：**全部完成 ✅**（2026-07-29 ~ 2026-07-30）
- 下一阶段：ProDataGrid 迁移 → [Doc 31](31-prodatagrid-migration-plan.md) · Agent 编排增强 → [Doc 32](32-agent-orchestration-plan.md) · 像素图像生成 → [Doc 33](33-image-generation-plan.md)

---

## 八、Spec 规则（新增）

| 规则 | 内容 | 对应章节 |
|:----:|------|:-------:|
| **R23** | Plugin 分类标记：Workbench/Feature/Service 用 `[PluginKind]` 标注；Service Plugin 实现 `IServicePlugin` | §1 |
| **R24** | 所有数据修改必须经过 `IHostService`，禁止 ViewModel 直接操作 EF Core DbContext 或 EntityMergeStore | §4 |
| **R25** | 跨 Plugin 功能扩展通过 HostService 事件/扩展点实现，禁止直接引用其他 Plugin 的 Service 类型 | §4.3 |

> 完整规则表见 [spec/README.md](../spec/README.md)。R23 选择方案 C（折中方案：IServicePlugin 显式接口 + Workbench/Feature 用 `[PluginKind]` 元数据）。

---

## 九、预留扩展插槽

| 扩展 | 接入方式 | 前置依赖 |
|------|---------|---------|
| **Validation Dashboard** | 新 Feature Plugin 或 Workbench Plugin Tool Dock，订阅 HostService.Changes 实时校验 | Phase 1 (HostService) + Phase 2 (引用类型) |
| **Plugin Marketplace** | App 启动时扫描 `Plugins/` 目录加载外部 DLL | Phase 6 (Plugin 分类) |
| **其他游戏数据支持** | 替换 Core 的 Data Model 层 | Phase 2 (引用类型) 完成后 |
| **宏/脚本系统** | CLI Plugin 扩展，支持批处理脚本 | Phase 7 (CLI) |

---

## 十、与现有 Spec 规则的兼容性

| 规则 | 兼容性 | 说明 |
|:----:|:------:|------|
| R01-R16 | ✅ 全部兼容 | HostService 是 R01 的增强 |
| R17-R22 | ✅ 全部兼容 | IServicePlugin 同样遵守 csproj 隔离 |
| R08 | ⚠️ 需修订注释 | KV 编辑器增加结构化引用编辑，但仍然是 KV 入口 |
| N01-N06 | ✅ 全部兼容 | HostService DI Singleton |

---

## 附录 A：25 实体引用属性改造清单（Phase 2）

| 实体 | 引用属性数 | 涉及 pattern | 特别说明 |
|------|:---------:|-------------|---------|
| ItemType | 13 | `{id}`, `{id}x{mult}`, `{id}={value}`, `{GroupId}.{SubgroupId}` | 最复杂 |
| Recipe | 6 | `{mult}x{id}`, `{id}` | 反转乘数格式特殊 |
| Creature | 5 | `{id}`, `{id}x{mult}` | |
| Encounter | 4 | `{id}`, `{id}x{mult}` | |
| Condition | 2 | `{id}` | |
| AttackMode | 1 | `{id}` | |
| BattleMove | 1 | `{id}` | |
| BarterHex | 1 | `{id}` | |
| CampType | 1 | `{id}` | |
| ChargeProfile | 2 | `{id}` | |
| ContainerType | 2 | `{id}` | |
| CreatureSource | 1 | `{id}` | |
| DmcPlace | 1 | `{id}` | |
| Faction | 2 | `{id}` | |
| ForbiddenHex | 1 | `{id}` | |
| Map | 3 | `{id}` | |
| TreasureTable | 2 | `{id}` | |
| 其他 6 实体 | 0 | - | 无引用属性 |

**总计**：~48 个引用属性需改造，涉及 19 个实体文件。
