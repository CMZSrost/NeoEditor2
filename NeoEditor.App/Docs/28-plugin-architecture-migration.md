# NeoEditor 插件化架构迁移计划

> v2.0 · 2026-07-24 · 从单体到 8 模块：Core / Infra / UI.Common / App / 3 Plugin + Integration Tests
> 根本决策: [../spec/D01](../spec/D01-core-plugin-architecture.md)
> 上承: [25-architecture-decisions.md](25-architecture-decisions.md) · [26-refactor-roadmap.md](26-refactor-roadmap.md)
> 配套 spec: [../spec/README.md](../spec/README.md)

---

## 〇、动机

### 现状问题

NeoEditor 当前是一个**单体项目**：260 个 .cs 文件、40,000 行代码挤在一个 `.csproj` 里。
核心症状：

| 症状 | 根因 |
|------|------|
| `ModGameDataTabsView` 3500 行跨 4 个 partial | 数据加载、编辑、导航、保存耦合在一个类 |
| `DocumentWorkspaceViewModel` 1216 行 | 所有 Plugin 的胶水代码集中在一处 |
| `GenericDataGridHelper` 825 行 static | DataGrid 的所有逻辑（列/排序/过滤/导航/交互）混在一起 |
| 一个 `.csproj` 引用 47 个 NuGet 包 | 没有依赖边界，任何模块可以 import 任何包 |
| 测试项目 1 个，引用整个 monolith | 测试编译要加载全部 47 个包，无法独立测试单个模块 |

### 目标架构

```
用户视角                             开发者视角

┌──────────────────┐          ┌──────────────────────────────┐
│ 基础设施           │          │ NeoEditor.Messaging           │  消息基础设施
│  消息管道         │          │ NeoEditor.Core                │  领域 + 契约
│  数据引擎         │          │ NeoEditor.Infra               │  数据 + 服务
└──────┬───────────┘          │ NeoEditor.UI.Common           │  UI 工具箱
       │                      └──────────────────────────────┘
┌──────┴───────────┐                         │
│ 基础 UI           │          ┌──────────────────────────────┐
│  Avalonia 工具箱   │          │ NeoEditor.App                │  Shell + DI
└──────┬───────────┘          └──────────────────────────────┘
       │                                     │
┌──────┴───────────┐          ┌──────────────────────────────┐
│ Plugins (功能)    │          │ NeoEditor.Plugins.XXX         │  每个独立编译
│  DataViewer      │          │   互相 0 引用                  │
│  EntityEditor    │          │   只依赖 Core + UI.Common     │
│  ImageTools      │          └──────────────────────────────┘
│  AI Generator    │
└──────────────────┘
```

**核心原则**：
- **Core 不知道 UI 的存在** — 纯数据引擎，可以在控制台/测试中独立运行
- **Plugin 互相不知道对方的存在** — 跨 Plugin 消息提升到 Core 定义
- **Plugin 测试只依赖 Core + UI.Common** — 不启动完整 App，最大限度内聚
- **App 是唯一的胶水层** — Plugin 启动时注册，版本号统一

---

## 一、模块划分方案

### 1.1 划分维度

| 维度 | 利弊 | 采用？ |
|------|------|:--:|
| 按功能领域 (DataTable / EntityEditor / ImageTools) | 每个模块独立可测，符合用户心智模型 | ✅ Plugin 维度 |
| 按依赖重量 (有无 Avalonia) | 保证 Core 可单独编译和测试 | ✅ 基础设施维度 |
| 按模块类型 (基础设施 / 基础 UI / Plugin / Shell) | 三类模块职责清晰，强制边界 | ✅ 分类维度 |

**结论**：**8 个 src 项目 + 9 个 test 项目 = 17 个项目**。分类如下：
- 基础设施（3）：Messaging、Core、Infra — 无 UI 依赖
- 基础 UI（1）：UI.Common — Avalonia 工具箱，必须随 App 发布
- Plugin（3）：DataViewer、EntityEditor、ImageTools — 功能扩展
- Shell（1）：App — 启动 + 组装

### 1.2 项目清单

```
NeoEditor.sln
│
├── NeoEditor.Messaging/                    net10.0 only (0 外部依赖)
│   ├── MessageBase.cs                      消息基类
│   ├── IMessageHandler.cs                  处理接口
│   └── MessageBus.cs                       包装 CommunityToolkit IMessenger
│
├── NeoEditor.Core/                         net10.0 + Messaging + CommunityToolkit.Mvvm
│   ├── Model/                             Game 实体、枚举（独立于编辑器，可跨游戏复用）
│   ├── Messages/                          Core 系统消息 + 跨 Plugin 共享消息
│   ├── Abstractions/                      IWorkspaceSession, Plugin 契约 (IPlugin/IToolPlugin/IDocumentPlugin)
│   ├── Validation/                        校验规则
│   └── Extensions/                        通用扩展方法
│
├── NeoEditor.Infra/                        + EF Core, SQLite, ImageSharp, Serilog
│   ├── Data/                              DbContext, Migrations
│   ├── Services/                          ModManager, ProfileManager, MergeService
│   ├── Parsing/                           PhpParser, XmlParser
│   ├── Indexing/                          ReferenceResolver, IndexService
│   ├── Serialization/                     CommandSerializer, ExportService
│   └── Configuration/                     ConfigService
│
├── NeoEditor.UI.Common/                    + Avalonia (仅 UI 工具箱)
│   ├── Converters/                        BoolInverter, EnumToBool, ...
│   ├── Behaviors/                         FocusBehavior, DragBehavior
│   ├── AttachedProperties/                DataGrid helpers
│   ├── Controls/                          共享自定义控件
│   └── Themes/                            共享样式、模板
│
├── NeoEditor.App/                          + Avalonia, Dock, Semi, Ursa
│   ├── Hosting/                           启动引导, DI 注册, Plugin 发现
│   ├── Shell/                             MainWindow, 布局管理, 工具栏
│   │   ├── Sidebar/                       侧边栏 + 面板管理
│   │   └── StatusBar/                    状态栏
│   ├── Settings/                          AppConfig, Theme, Localization
│   └── Assets/                            资源文件
│
├── NeoEditor.Plugins.DataViewer/           + Avalonia, UI.Common, Core, Infra
│   ├── Services/                          DataTableService, NavigationService
│   ├── ViewModels/                        DataTableVm, PeekPanelVm, IndexTableVm
│   ├── Views/                             DataGrid, PeekPanel, IndexTable
│   └── Interaction/                       CellEdit, Sort, Filter, Search
│
├── NeoEditor.Plugins.EntityEditor/         + Avalonia, UI.Common, Core, Infra, AvaloniaEdit
│   ├── Services/                          EntityEditService, FieldGroupService, VisHelper
│   ├── ViewModels/                        KVEVm, EntityEditorDocument, XmlEditorVm
│   ├── Views/                             KVEView, EntityEditorView, XmlEditorView
│   ├── Visualizers/                       25 个 IEntityVisualizer 实现
│   └── Diff/                              XmlDiff, SavePreview
│
├── NeoEditor.Plugins.ImageTools/           + Avalonia, UI.Common, Core, Infra, ImageSharp
│   ├── Services/                          ImageEditService, PixelProcessing
│   ├── ViewModels/                        ImageEditorVm, CropSelection
│   └── Views/                             ImageEditor, ZoomableImage
│
└── Tests/
    ├── NeoEditor.Messaging.Tests/
    ├── NeoEditor.Core.Tests/
    ├── NeoEditor.Infra.Tests/
    ├── NeoEditor.UI.Common.Tests/
    ├── NeoEditor.App.Tests/
    ├── NeoEditor.Plugins.DataViewer.Tests/
    ├── NeoEditor.Plugins.EntityEditor.Tests/
    ├── NeoEditor.Plugins.ImageTools.Tests/
    └── NeoEditor.Integration.Tests/         ← 跨 Plugin 全链路测试
```

### 1.3 为什么是这个粒度？

| 备选方案 | 为什么否决 |
|----------|-----------|
| Core + 1 个 Plugin (回到 2 项目) | 跟现在没区别，Plugin 内部仍然会膨胀成 monolith |
| Core + 10+ 个 Plugin (每功能一个) | DataTable / Navigation / Peek / Filter / Search 共享 DataGrid 基础设施，强拆只会增加 interop 复杂度 |
| Core + 15 个 Plugin (每个 Visualizer 一个) | 25 个 visualizer × 各自 test project = 50 个 csproj，构建时间爆炸 |

**选择的 8+9 方案**：
- 基础设施（Messaging / Core / Infra）— 三层渐进的依赖。Messaging 零依赖，Core 只依赖 Messaging，Infra 加数据访问
- 基础 UI（UI.Common）— 所有 Plugin 和 App 共享的 Avalonia 工具箱。**模块而非 Plugin**，必须随 App 发布
- Plugin（3 个）— 每个对应一个用户能独立理解的功能区域
- Integration.Tests — 跨 Plugin 全链路，使用 SQLite in-memory 覆盖核心用户操作路径

### 1.4 包依赖分配

| 包 | Msg | Core | Infra | UI.Com | App | DataV. | EntEd. | ImgT. |
|----|:--:|:--:|:--:|:--:|:--:|:--:|:--:|:--:|
| CommunityToolkit.Mvvm | | ✅ | ✅ | | ✅ | ✅ | ✅ | ✅ |
| Microsoft.EntityFrameworkCore | | | ✅ | | | | | |
| EFCore.BulkExtensions | | | ✅ | | | | | |
| AutoMapper | | | ✅ | | | | | |
| Newtonsoft.Json | | | ✅ | | | | | |
| SixLabors.ImageSharp | | | ✅ | | | | | ✅ |
| Serilog | | | ✅ | | ✅ | | | |
| XMLDiffPatch | | | | | | | ✅ | |
| Avalonia 全家桶 | | | | ✅ | ✅ | ✅ | ✅ | ✅ |
| Dock.Avalonia | | | | | ✅ | | | |
| Semi.Avalonia | | | | | ✅ | | | |
| Ursa | | | | | ✅ | | | |
| AvaloniaEdit | | | | | | | ✅ | |
| MessageBox.Avalonia | | | | | ✅ | | | |
| LiveMarkdown | | | | | ✅ | | | |
| FluentIcons | | | | | ✅ | | | |

> **Msg**=Messaging, **UI.Com**=UI.Common, **DataV.**=DataViewer, **EntEd.**=EntityEditor, **ImgT.**=ImageTools
>
> Messaging 是**0 外部依赖**的纯 .NET 项目。UI.Common 只依赖 Avalonia（无业务逻辑）。Plugin 通过 Core + Infra + UI.Common 获取所有需要的能力，不依赖 App。

---

## 二、依赖方向（铁律）

```
                         ┌─────────────────────┐
                         │    NeoEditor.App      │  ← Shell: 组装一切
                         │  Hosting / Shell /    │
                         │  Settings             │
                         └──┬───┬───┬───┬────┬─┘
                            │   │   │   │    │
          ┌─────────────────┼───┼───┼───┼────┼──────────────┐
          ▼                 ▼   ▼   ▼   ▼    ▼              │
  ┌──────────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐  │
  │  UI.Common   │ │DataViewer│ │EntityEdit│ │ImageTools│  │
  │ (基础 UI)    │ └────┬─────┘ └────┬─────┘ └────┬─────┘  │
  │ 共享工具箱    │      │            │            │         │
  └──────┬───────┘      └────────────┼────────────┘         │
         │                           │                       │
         │              ┌────────────┴────────────┐          │
         │              │       NeoEditor.Infra    │          │
         │              │     (数据 + 服务)         │          │
         │              └────────────┬────────────┘          │
         │                           │                       │
         │              ┌────────────┴────────────┐          │
         │              │     NeoEditor.Core       │          │
         │              │   (领域 + 契约 + 消息)    │          │
         │              └────────────┬────────────┘          │
         │                           │                       │
         │              ┌────────────┴────────────┐          │
         │              │  NeoEditor.Messaging     │          │
         │              │  (消息基础设施, 0 依赖)   │          │
         │              └─────────────────────────┘          │
         │                                                   │
         └─── Plugin A ─── Messaging/Core ─── Plugin B ─────┘
              跨 Plugin 消息定义在 Core，双方都引用 Core
```

**强制规则**（对应 spec R07 / R14 精神）：

| # | 规则 |
|---|------|
| **R17** | Plugin A 不引用 Plugin B（csproj 级隔离） |
| **R18** | Plugin 可依赖 Core + Infra + UI.Common。不可依赖 App 或其他 Plugin |
| **R19** | 跨 Plugin 消息定义在 Core。消息方向：Plugin → Core ← Plugin（单向，不可逆） |
| **R20** | DI 注册在 App Composition Root，Plugin 不自注册 |
| **R21** | 每个模块独立测试项目。Plugin 测试只引用 Core + UI.Common + 该 Plugin |
| **R22** | Integration.Tests 独立项目，覆盖跨 Plugin 核心链路 |

### Plugin 测试原则（新）

**Plugin 测试应该能用 Core + UI.Common + Mock 跑通，不启动完整 App。**

```
NeoEditor.Plugins.DataViewer.Tests/
├── 引用: DataViewer.csproj, Core.csproj, UI.Common.csproj
├── Mock: Infra (用 Moq 模拟 IWorkspaceSession, IReferenceResolver)
├── 不引用: App.csproj, EntityEditor.csproj, ImageTools.csproj
└── 不启动: Avalonia 渲染（用 Avalonia.Headless 做 View 测试，可选）
```

这保证了每个 Plugin 的真正内聚——如果 Plugin 测试需要 mock 另一个 Plugin 的 Service，
说明边界设计有问题。

---

## 三、Plugin 契约

### 3.1 核心接口（定义在 NeoEditor.Core/Abstractions/）

```csharp
// Plugin 向 App 注册自己
public interface IPlugin
{
    string Name { get; }                    // "DataViewer", "EntityEditor"
    Version Version { get; }                // 语义版本
    Task InitializeAsync(IPluginContext ctx);
}

// App 提供给 Plugin 的上下文
public interface IPluginContext
{
    IServiceProvider Services { get; }      // DI 容器
    IMessenger Messenger { get; }           // 事件总线
    IWorkspaceSession Session { get; }      // 当前工作状态
}

// Tool 类型 Plugin（注册到 Left/Right/Bottom Dock）
public interface IToolPlugin : IPlugin
{
    string Title { get; }
    ToolDock DefaultDock { get; }           // Left | Right | Bottom
    int Order { get; }                      // 排序权重
    Control CreateToolView();
}

// Document 类型 Plugin（注册到 Center DocumentDock）
public interface IDocumentPlugin : IPlugin
{
    IReadOnlyList<string> SupportedEntityTypes { get; }
    DocumentViewBase CreateDocument(IEntity entity, IPluginContext ctx);
}
```

### 3.2 消息契约（跨 Plugin 通信）

所有跨 Plugin 消息定义在 `NeoEditor.Core/Messages/`。Plugin 只通过消息协作：

```
DataViewer 发出:
  EntitySelectedMessage(entityId)     → EntityEditor 接收 → 打开/激活文档
  EntityDoubleClickedMessage(entity)  → EntityEditor 接收 → 打开编辑
  NavigateRequestMessage(entityId)    → Navigation 接收 → 执行跳转
  PeekRequestMessage(entityId)        → Navigation 接收 → Peek 面板固定

EntityEditor 发出:
  EntityModifiedMessage(entityId)     → DataViewer 接收 → 刷新行数据
  EntitySavedMessage(entityId)        → DataViewer 接收 → 清除行高亮
  EditorFocusChangedMessage(entity)   → 其他 Tool Plugin 接收 → 焦点跟随

ImageTools 发出:
  ImageExportedMessage(path)          → EntityEditor 接收 → 刷新图片预览
```

**消息设计原则** (R05 精神):
- 消息是**通知**，不是命令。发送方不假设谁会接收，不依赖接收方的行为
- 消息携带 ID，不携带完整对象引用（避免跨 Plugin 内存共享）
- 一个消息类型最多 1-2 个接收方

---

## 四、迁移路径

### 总览

```
M7        M8                  M9            M10            M11           M12
代码卫生   Core 基础设施        DataViewer    EntityEditor   ImageTools    收尾
          (基础结构)           Plugin        Plugin [✅]    Plugin [✅]   [计划]
─────     ────────────────     ────────────  ─────────────  ────────────  ────
已完      已完成              已完成         已完成          已完成         待开始
```

**总体策略**：
1. **Core 先行** — 基础结构完整开发、单测覆盖、人工验收通过后，再开始 Plugin 工作
2. **Plugin 逐个迭代** — 每个 Plugin 独立走完完整循环，不并行、不跳跃
3. **质量门禁** — 每个阶段有明确的验收标准和文档订正要求

### 通用开发流程

每个阶段都遵循相同的循环：

```
┌─────────────────────────────────────────────────────────┐
│                                                         │
│  ① 开发 ──→ ② 单测脚本 ──→ ③ 人工验收                   │
│                              │                          │
│                         通过？                           │
│                      ╱         ╲                        │
│                    否           是                       │
│                    ↓             ↓                       │
│              ④ 反馈修正     ⑤ 订正文档 + 单测脚本         │
│                    │             │                       │
│                    └─────────────┘                       │
│                    (进入下一阶段)                          │
└─────────────────────────────────────────────────────────┘
```

> **关键**：文档和单测脚本是**交付物的一部分**，不是事后补。每个阶段结束时，文档和测试脚本必须反映当前真实状态。

---

### M7: 代码卫生 [前置，1-2w]

> 与 Core/Plugin 无关的纯卫生工作。在此阶段不拆分任何项目。
> 详见 memory `short-term-plan-m7`。

| 步骤 | 内容 |
|------|------|
| M7.1 | 56 空 catch 块加日志 |
| M7.2 | 62 Warning 清零 |
| M7.3 | 核心服务单元测试 (MergeService / ReferenceResolver / CommandSerializer / PhpParser → 28+) |
| M7.4 | Serilog.RollingFile → Serilog.Sinks.File + XlsxWriter sealed |
| M7.5 | VisHelper 改为注入 Singleton |

**验收**：`dotnet build -warnaserror` 通过 · `dotnet test` 28+ 通过 · 0 空 catch 无日志 · 0 deprecated 包

**交付物**：无需订正文档（代码卫生，不改变项目结构）

---

### M8: Core 基础设施 [3-4w，含验收]

**目标**：开发完整的基础设施层。这是整个架构的地基，必须一次做对。

#### M8.1: Messaging + Core (1w)

| 步骤 | 内容 |
|------|------|
| 1 | 创建 `NeoEditor.Messaging.csproj` — net10.0, 0 外部包。`MessageBase<T>`、`IMessageHandler<T>`、`MessageBus` |
| 2 | 创建 `NeoEditor.Core.csproj` — net10.0 + Messaging + CommunityToolkit.Mvvm |
| 3 | 迁移：`Data/Model/**` (30 文件) + `Data/Messages/**` (8 文件) + `Data/Validation/**` (6 文件) + `Helper/EntityHelper.cs` + Plugin 契约接口 |

#### M8.2: Infra (1w)

| 步骤 | 内容 |
|------|------|
| 1 | 创建 `NeoEditor.Infra.csproj` — +EF Core, SQLite, ImageSharp, Serilog |
| 2 | 迁移：`Data/Command/**` + `Data/Context/**` + `Data/DTO/**` + `Data/Options/**` + `Services/**` (~25 文件) + `Helper/PhpParser.cs` + `Helper/Reference*.cs` (3 文件) |

#### M8.3: UI.Common (0.5w)

| 步骤 | 内容 |
|------|------|
| 1 | 创建 `NeoEditor.UI.Common.csproj` — +Avalonia（仅 UI 工具箱） |
| 2 | 迁移：`Helper/Converter/**` (10 文件) + `Helper/Behaviors/**` (2 文件) + `Helper/AttachedProperties/**` (2 文件) |

#### M8.4: App Shell (0.5w)

| 步骤 | 内容 |
|------|------|
| 1 | 重命名 `NeoEditor/` → `NeoEditor.App/`，csproj 重命名 |
| 2 | 创建内部三层：`Hosting/` / `Shell/`（含 Sidebar/、StatusBar/）/ `Settings/` |
| 3 | 创建 `PluginRegistry`、DI 分模块扩展方法 |
| 4 | 创建 `IPlugin` / `IToolPlugin` / `IDocumentPlugin` 接口（在 Core） |
| 5 | 删除 `App.*` V6 静态访问器 |

#### M8.5: 全量单测 + 人工验收 (1w)

| 步骤 | 内容 |
|------|------|
| 1 | 编写单测：Messaging.Tests / Core.Tests / Infra.Tests / UI.Common.Tests / App.Tests |
| 2 | `dotnet test` 全部通过（含 M7 的 28+ 测试） |
| 3 | **人工验收**：手动启动 App，验证基本功能正常（打开 Profile、浏览数据、编辑保存） |
| 4 | 订正文档：`spec/D01` / `CLAUDE.md` / `index.md` 反映新的项目结构 |
| 5 | 订正单测脚本：更新覆盖率报告，补充遗漏的边界条件 |

**验收标准**：

| 项目 | 验收条件 |
|------|---------|
| `NeoEditor.Messaging` | 0 外部依赖，`dotnet list package` 输出为空 |
| `NeoEditor.Core` | `dotnet list package --include-transitive` 不含 Avalonia |
| `NeoEditor.Infra` | `dotnet list package --include-transitive` 不含 Avalonia |
| `NeoEditor.UI.Common` | 不引用 Core / Infra / App，只能引用 Avalonia 基础包 |
| 编译 | `dotnet build NeoEditor.sln` 0 Error 0 Warning |
| 测试 | `dotnet test` 全部通过，覆盖率 ≥ 基线 |
| 人工验收 | 手动启动 App → 打开 Profile → 浏览数据 → 编辑保存 → 重启恢复 ✅ |

**不移动的内容**（留在 App，等 Plugin 阶段处理）：
- 所有 Views/UserControls（DataGrid、Editor、KV、Peek 等）
- 所有 ViewModels/MainContent（除已迁移的）
- GenericDataGridHelper、VisHelper、EntityVisualizers
- 图片编辑相关代码

---

### M9: DataViewer Plugin — ✅ 核心完成 (2026-07-28)

**目标**：DataTable + 导航 + Peek + 索引 → `NeoEditor.Plugins.DataViewer`。

#### 已完成 ✅

| 工作项 | 详情 |
|--------|------|
| GDH 拆解+删除 | `GenericDataGridHelper.cs` 已删除。拆为 `DataTableService` + `ColumnTemplateFactory` + `InteractionHandler` |
| 5 个 View 迁移 | SearchableDataGrid, IndexTableView, PeekPanelView, FindReplacePanel, SearchResultsView → Plugin |
| 类型提取 | `IEntityVisualizer`, `EntityVisualizerRegistry` App → Plugin |
| 服务提取 | `DataLoaderService`（6 DB 方法 + ResolveEntityKeyProperty + BuildHeader） |
| VM 增强 | `DataTableViewModel`：Tabs/MergeStore/EditStore/ModInfo/ProfileInfo 所有权 |
| Converter 改造 | 5 Converter `DataTableService.Instance` → `ConverterServiceHelper` |
| BottomTools 瘦身 | 搜索功能剥离到 `SearchResultViewModel`，仅保留 Conflicts + Validation |
| GDH 消费者全迁 | 15 文件共 ~60 处 GDH 引用全部迁至 `DataTableService.Instance?.Xxx` |

#### Plugin 最终结构

```
NeoEditor.Plugins.DataViewer/
├── IEntityVisualizer.cs
├── Converters/ (6)       含 ConverterServiceHelper
├── Services/ (11)        含 DataLoaderService, EntityVisualizerRegistry
├── ViewModels/ (6)       含 DataTableViewModel(增强), SearchResultViewModel(完善)
└── Views/ (5 .axaml)     SearchableDataGrid, IndexTable, PeekPanel, FindReplace, SearchResults
```

#### 验收结果

| 标准 | 结果 |
|------|:--:|
| Plugin 独立编译，0 对 App 的引用 | ✅ |
| Plugin 中 0 GDH / 0 ViewServices / 0 DataTableService.Instance | ✅ |
| DataViewer.Tests 10/10 通过 | ✅ |
| 人工验收：DataTable 加载、引用导航、Peek、索引、搜索、保存 | ✅ |

#### 延后项

| 项 | 说明 |
|----|------|
| ModGameDataTabsView → Plugin DataTableView | 5 partial 4153 行拆分为瘦 Plugin View。基础已就绪（DataLoaderService + VM 增强） |
| DataTableService.Instance 完全移除 | 等 App 剩余消费者（ReferenceResolver.Svc 等）改为 DI 注入 |

> 详见 [testround/test_round13_summary.md](testround/test_round13_summary.md)

---

### M10: EntityEditor Plugin [2w，含验收]

**目标**：XML 编辑器 + KV 编辑器 + 25 个 Visualizer → `NeoEditor.Plugins.EntityEditor`。
这是三个 Plugin 中最大的一个。

#### M10.1-8: 全部完成 ✅

| Phase | 工作 | 状态 | 说明 |
|:--:|------|:--:|------|
| 0 | 共享契约重定位: IEntityVisualizer + EntityVisualizerRegistry → UI.Common | ✅ | 确保 EntityEditor 不引用 DataViewer（R17） |
| 1 | Plugin 骨架: 项目创建、EntityEditorPlugin.cs、ServiceCollectionExtensions | ✅ | 12 src → 13 src 项目 |
| 2 | VisHelper → VisHelperService DI 单例 | ✅ | 864 行，5 构造参数，0 静态可变字段 |
| 3 | RefNode Plugin 副本 | ✅ | 双注册过渡策略 |
| 4 | 25 个 Visualizer → Plugin | ✅ | 全部 25 个 IEntityVisualizer |
| 5 | Editor Views/VMs 迁移 (17 文件) | ✅ | 详见 [test_round16.md](testround/test_round16_summary.md) |
| 6 | DI 简化 + App 清理 + R17 解除 | ✅ | IEntityLookupService + 删旧文件 |
| 7 | DocumentWorkspaceViewModel 解耦 | ✅ | EntityEditorDocumentFactory 替代 new |
| 8 | EntityEditor.Tests (9 tests) | ✅ | Plugin + Service + Factory 测试 |

**验收标准**：
- VisHelper 0 处 static 可变字段，仅通过 DI 获取依赖 ✅
- EntityEditor → DataViewer csproj 引用: **0** (R17 ✅)
- EntityEditor → App csproj 引用: **0** (R18 ✅)
- 旧 App VisHelper.cs / RefNode.cs: 已删除 ✅

##### 当前 Plugin 结构

```
NeoEditor.Plugins.EntityEditor/
├── EntityEditorPlugin.cs                    ← 实现 IToolPlugin + IDocumentPlugin
├── ServiceCollectionExtensions.cs           ← 含 RegisterEntityEditorVisualizers()
├── Services/
│   ├── VisHelperService.cs                 ← DI 单例
│   ├── RefNode.cs
│   └── EntityEditorDocumentFactory.cs      ← [新] 工厂模式
├── ViewModels/
│   ├── PluginDocumentBase.cs               ← DI 版基类
│   ├── EntityEditorDocument.cs
│   ├── KeyValueEditorViewModel.cs
│   └── OverlayChainToolContent.cs
├── Helper/
│   ├── HighlightBackgroundRenderHelper.cs
│   ├── XmlCompareHelper.cs
│   └── AttachedProperties/
│       └── TextEditorScrollSyncAttached.cs
├── Visualizers/ (25 个文件)
└── Views/
    ├── EntityEditorView.axaml/.cs
    ├── KeyValueEditorView.axaml/.cs
    ├── EntityViewerView.axaml/.cs
    ├── OverlayChainToolView.axaml/.cs
    ├── XmlDiffView.axaml/.cs
    ├── DiffPreviewTrack.cs
    ├── ModGameDataSavePreviewDialog.axaml/.cs
    ├── MergeXmlExportDialog.axaml/.cs
    └── ZoomableImageView.axaml/.cs
```

---

### M11: ImageTools Plugin — ✅ 全部完成 (2026-07-29)

**目标**：图片编辑功能 → `NeoEditor.Plugins.ImageTools`。

**规模**：17 个文件新建，约 3,200 行代码迁移。App 中已无 Plugin 代码残留，App = 纯 Shell。

#### 迁移结果

| 工作项 | 详情 |
|--------|------|
| Plugin 基类 | `ImageToolDocumentBase` + `ImageToolObservableObject`（DI 注入 ILocalizationService） |
| ImageEditor VM/View/Helper | `ImageEditorDocument` + `ImageCropSelection` + `ImageEditorDocumentView` + 3 Helper → Plugin |
| ModImages VM/View | `ModImagesDocument` + `ModImagesDocumentView` + `ModImagePairDropHandler` → Plugin |
| ImagePreview VM/View | `ImagePreviewContent` + `ImagePreviewView` → Plugin |
| 新服务接口 | `IModImageListService`（桥接 PhpParser + RenameImagePairDialog）、`IImageSearchService` |
| App 侧清理 | 删除 15 个旧文件；更新 DI/DataTemplates/DocumentWorkspaceViewModel/RightPanelView |
| 重复服务删除 | App/Services/ 中原 `IImageEditorProcessingService` + 实现已删除（由 Plugin 接管） |
| 测试 | `ImageTools.Tests` — 4 个测试 (Services) |

#### 验收结果

| 标准 | 结果 |
|------|:--:|
| ImageTools 项目独立编译 | ✅ |
| Plugin 0 对 App csproj 引用 (R18) | ✅ |
| ImageSharp 仅存在于 ImageTools 和 Infra | ✅ |
| `bash build.sh` 0 Error | ✅ 16/16 项目 |
| 全量测试 34/34 | ✅ 含 ImageTools.Tests 4/4 |
| App 无 Plugin 代码残留 | ✅ 纯 Shell |

#### 当前 Plugin 结构

```
NeoEditor.Plugins.ImageTools/
├── ImageToolsPlugin.cs                    ← 实现 IToolPlugin
├── ServiceCollectionExtensions.cs         ← DI 注册扩展方法
├── Services/
│   ├── IImageEditorProcessingService.cs   ← 像素画处理契约
│   ├── ImageEditorProcessingService.cs    ← ImageSharp 实现
│   ├── IImageSearchService.cs             ← 图片搜索目录接口
│   ├── ImageSearchService.cs              ← 搜索目录实现
│   └── IModImageListService.cs            ← Mod 图片列表操作接口
├── ViewModels/
│   ├── ImageToolDocumentBase.cs           ← DI 版 Document 基类
│   ├── ImageToolObservableObject.cs       ← DI 版 ObservableObject 基类
│   ├── ImageEditorDocument.cs             ← 像素画编辑器 VM
│   ├── ImageCropSelection.cs              ← 裁剪选区结构体
│   ├── ModImagesDocument.cs               ← Mod 图片列表 VM
│   └── ImagePreviewContent.cs             ← 图片预览 VM
├── Helper/
│   ├── PixelArtOutputSizeCalculator.cs    ← 像素画尺寸计算
│   ├── CropSelectionInteraction.cs        ← 裁剪交互逻辑
│   ├── ImageSelectionOverlayPresenter.cs  ← 裁剪覆盖层呈现器
│   ├── ImageSelectionViewportMapper.cs    ← 裁剪视口映射
│   └── ModImagePairDropHandler.cs         ← 图片对拖放处理
└── Views/
    ├── ImageEditorDocumentView.axaml/.cs  ← 像素画编辑器 View
    ├── ModImagesDocumentView.axaml/.cs    ← Mod 图片 View
    └── ImagePreviewView.axaml/.cs         ← 图片预览 View
```

> 详见 [testround/test_round18_summary.md](testround/test_round18_summary.md)

---

### M12: 收尾 & 清理 [1w]

**目标**：全链路集成测试 + 文档终稿。

| 步骤 | 内容 |
|------|------|
| 1 | 创建 `NeoEditor.Integration.Tests` — 引用所有模块，SQLite in-memory |
| 2 | 编写 5-10 个全链路测试：打开 Profile → 双击行 → KV 编辑 → 保存 → 重启恢复，跨 Plugin 导航等 |
| 3 | DI 注册最终形式：`services.AddPlugin<DataViewerPlugin>()` 一行注册，Dock 布局由 Plugin 遍历生成 |
| 4 | 删除 App 中所有 dead code |
| 5 | 全量 `dotnet test` + `dotnet build -warnaserror` + 性能基准 |
| 6 | 更新所有文档终稿：`CLAUDE.md` / `index.md` / `spec/README.md` / memory |

**最终验收**：

| 指标 | 目标 |
|------|:--:|
| 编译 Error | 0 |
| 编译 Warning | 0 |
| 单元测试通过 | 全部 |
| Integration.Tests 通过 | 全部 |
| Core / Infra 不引用 Avalonia | ✅ |
| Plugin 间 0 引用 | ✅ |
| 文档反映真实项目结构 | ✅ |

---

## 五、测试策略

### 5.1 分层测试

| 层级 | 测试框架 | Mock 策略 | 目标覆盖率 |
|------|---------|----------|:--:|
| Messaging | xUnit | 无依赖 | 90%+ |
| Core Models | xUnit | 无依赖，纯逻辑 | 80%+ |
| Infra Services | xUnit + SQLite in-memory | EF Core InMemory | 60%+ |
| UI.Common | xUnit + Avalonia.Headless | Mock 数据 | 50%+ |
| Plugin.Services | xUnit + Moq | Mock Core 接口 | 70%+ |
| Plugin.ViewModels | xUnit + Moq | Mock Services | 50%+ |
| Integration | xUnit + SQLite in-memory | 全部真实（不 mock） | 核心链路 90%+ |
| Plugin.Views | 手工 / Avalonia.Headless | — | 暂不强制 |

### 5.2 每个模块的测试项目结构

```
Tests/
├── NeoEditor.Messaging.Tests/          MessageBus 往返, 序列化
├── NeoEditor.Core.Tests/               模型校验, 契约接口
├── NeoEditor.Infra.Tests/              MergeService, ReferenceResolver, PhpParser, ExportService
├── NeoEditor.UI.Common.Tests/          转换器, 行为, 自定义控件
├── NeoEditor.App.Tests/                PluginRegistry, DI 注册验证
├── NeoEditor.Plugins.DataViewer.Tests/
│   ├── Services/                       DataTableService, NavigationRouter
│   ├── ViewModels/                     DataTableVm, PeekPanelVm
│   └── TestData/                       SampleEntities
├── NeoEditor.Plugins.EntityEditor.Tests/
│   ├── Services/                       VisHelper, FieldGroupService
│   ├── ViewModels/                     KVEVm, EntityEditorDocument
│   └── Visualizers/                    ItemType / Encounter / ... 单元测试
├── NeoEditor.Plugins.ImageTools.Tests/
└── NeoEditor.Integration.Tests/         ← 跨 Plugin 全链路
    ├── CoreWorkflowTests.cs            打开 Profile → 编辑 → 保存 → 恢复
    ├── CrossPluginNavigationTests.cs   DataViewer → EntityEditor → KV 联动
    └── TestFixtures/                   SQLite in-memory 启动配置
```

**Integration.Tests 设计原则**：
- 引用所有模块（不 mock），使用 SQLite in-memory 模拟完整数据流
- 测试场景 = 用户真实操作路径（端到端）
- 5-10 个核心场景覆盖最关键的跨 Plugin 交互链路

---

## 六、风险 & 缓解

| 风险 | 影响 | 缓解 |
|------|------|------|
| 文件移动导致大量 import 断裂 | 编译失败 | 每一步只移动一个模块，`dotnet build` 验证后再继续 |
| Plugin 间隐式依赖被发现 | 需要重新设计接口 | M8c-M8e 每步先画依赖图，发现环依赖立即停下来抽接口到 Core |
| 构建时间增加 | 8 个 csproj vs 1 个 | 用 `dotnet build --no-restore` 增量编译；基础设施层极少变动 |
| 现有功能回归 | 用户可见 bug | M7 先铺测试安全网；M9 增加 Integration.Tests 覆盖核心链路 |
| Plugin 粒度过粗/过细 | 后期再调整 | 先用 8 模块方案跑通，根据实际情况决定是否再拆/合 |

---

## 七、目标解决方案结构

```
NeoEditor.sln
│
├── Solution Items/
│   ├── CLAUDE.md
│   ├── compose.yaml
│   └── global.json
│
├── NeoEditor.Core/
│   ├── NeoEditor.Core.csproj
│   ├── Abstractions/
│   │   ├── IPlugin.cs
│   │   ├── IPluginContext.cs
│   │   ├── IToolPlugin.cs
│   │   ├── IDocumentPlugin.cs
│   │   └── IWorkspaceSession.cs
│   ├── Messages/
│   │   ├── AppConfigMessages.cs
│   │   ├── GridInteractionMessages.cs
│   │   ├── ModGameDataMessages.cs
│   │   ├── ModMessages.cs
│   │   ├── PageNavigationMessage.cs
│   │   ├── ProfileMessage.cs
│   │   └── WorkspaceMessages.cs
│   ├── Model/
│   │   ├── Game/ (ItemType, Recipe, Creature, ... 25 entities)
│   │   ├── ModInfo.cs
│   │   ├── ProfileInfo.cs
│   │   └── FieldGroupMetadata.cs
│   ├── Validation/
│   └── Extensions/
│
├── NeoEditor.Core.Infrastructure/
│   ├── NeoEditor.Core.Infrastructure.csproj
│   ├── Data/
│   │   ├── Context/ (GameDbContext, EditorDbContext)
│   │   ├── Command/ (CommandHistory, Command types)
│   │   └── DTO/
│   ├── Services/
│   │   ├── ModManager.cs
│   │   ├── ProfileManager.cs
│   │   ├── MergeService.cs
│   │   ├── DataExportService.cs
│   │   ├── WorkspacePersistenceService.cs
│   │   ├── ConfigService.cs
│   │   ├── FilterService.cs
│   │   ├── SearchService.cs
│   │   ├── ImageService.cs
│   │   └── FieldDescriptionService.cs
│   ├── Parsing/
│   │   ├── PhpParser.cs
│   │   └── XmlParser.cs
│   ├── Indexing/
│   │   ├── ReferenceResolver.cs
│   │   ├── ReferenceIndexService.cs
│   │   └── BrowserIndexService.cs
│   ├── Serialization/
│   │   └── CommandSerializer.cs
│   └── Configuration/
│
├── NeoEditor.App/
│   ├── NeoEditor.App.csproj
│   ├── App.axaml / App.axaml.cs
│   ├── Composition/
│   │   ├── ServiceCollectionExtensions.cs   (分模块 DI 注册)
│   │   └── PluginRegistry.cs
│   ├── Shell/
│   │   ├── MainWindow.axaml/.cs
│   │   ├── MainWindowViewModel.cs
│   │   └── MainWindowSideBarViewModel.cs
│   ├── Settings/
│   │   ├── AppConfig.cs
│   │   ├── SettingsPageViewModel.cs
│   │   └── SettingsPageView.axaml/.cs
│   ├── Pages/
│   │   ├── WelcomePage (HomePageViewModel.cs + View)
│   │   └── WorkspacePage (DocumentWorkspaceView)
│   ├── Explorer/
│   │   ├── DataBrowser (ViewModel + View)
│   │   ├── ModDatabase (ViewModel + View)
│   │   ├── SettingsPane (ViewModel + View)
│   │   └── ModIndex (ViewModel + View)
│   └── Assets/ (Resources.resx, Icons, Fonts)
│
├── NeoEditor.Plugins.DataViewer/
│   ├── NeoEditor.Plugins.DataViewer.csproj
│   ├── DataViewerPlugin.cs
│   ├── Services/
│   │   ├── DataTableService.cs
│   │   ├── ColumnTemplateFactory.cs
│   │   ├── InteractionHandler.cs
│   │   ├── NavigationRouter.cs
│   │   └── ColumnVisibilityKeys.cs
│   ├── ViewModels/
│   │   ├── DataTableViewModel.cs
│   │   ├── PeekPanelViewModel.cs
│   │   ├── IndexTableViewModel.cs
│   │   └── SearchResultViewModel.cs
│   └── Views/
│       ├── DataTableView.axaml/.cs
│       ├── SearchableDataGrid.axaml/.cs
│       ├── PeekPanelView.axaml/.cs
│       ├── IndexTableView.axaml/.cs
│       ├── FindReplacePanel.axaml/.cs
│       └── SearchResultsView.axaml/.cs
│
├── NeoEditor.Plugins.EntityEditor/
│   ├── NeoEditor.Plugins.EntityEditor.csproj
│   ├── EntityEditorPlugin.cs
│   ├── Services/
│   │   ├── EntityEditService.cs
│   │   ├── FieldGroupService.cs
│   │   ├── VisHelper.cs
│   │   ├── EntityVisualizerRegistry.cs
│   │   └── RefNode.cs
│   ├── ViewModels/
│   │   ├── EntityEditorDocument.cs
│   │   ├── KeyValueEditorViewModel.cs
│   │   ├── XmlEditorViewModel.cs
│   │   └── OverlayChainViewModel.cs
│   ├── Views/
│   │   ├── EntityEditorView.axaml/.cs
│   │   ├── EntityViewerView.axaml/.cs
│   │   ├── KeyValueEditorView.axaml/.cs
│   │   ├── XmlDiffView.axaml/.cs
│   │   └── SavePreviewDialog.axaml/.cs
│   └── Visualizers/
│       ├── IEntityVisualizer.cs
│       ├── ItemTypeEntityVisualizer.cs
│       ├── EncounterEntityVisualizer.cs
│       ├── RecipeEntityVisualizer.cs
│       └── ... (25 total)
│
├── NeoEditor.Plugins.ImageTools/
│   ├── NeoEditor.Plugins.ImageTools.csproj
│   ├── ImageToolsPlugin.cs
│   ├── ServiceCollectionExtensions.cs
│   ├── Services/
│   │   ├── IImageEditorProcessingService.cs
│   │   ├── ImageEditorProcessingService.cs
│   │   ├── IImageSearchService.cs             ← [M11 新增]
│   │   ├── ImageSearchService.cs              ← [M11 新增]
│   │   └── IModImageListService.cs            ← [M11 新增]
│   ├── ViewModels/
│   │   ├── ImageToolDocumentBase.cs           ← [M11 新增]
│   │   ├── ImageToolObservableObject.cs       ← [M11 新增]
│   │   ├── ImageEditorDocument.cs
│   │   ├── ImageCropSelection.cs
│   │   ├── ModImagesDocument.cs
│   │   └── ImagePreviewContent.cs
│   ├── Helper/
│   │   ├── PixelArtOutputSizeCalculator.cs
│   │   ├── CropSelectionInteraction.cs        ← [M11 迁移]
│   │   ├── ImageSelectionOverlayPresenter.cs  ← [M11 迁移]
│   │   ├── ImageSelectionViewportMapper.cs    ← [M11 迁移]
│   │   └── ModImagePairDropHandler.cs         ← [M11 迁移]
│   └── Views/
│       ├── ImageEditorDocumentView.axaml/.cs
│       ├── ModImagesDocumentView.axaml/.cs
│       └── ImagePreviewView.axaml/.cs
│
└── Tests/
    ├── NeoEditor.Messaging.Tests/
    ├── NeoEditor.Core.Tests/
    ├── NeoEditor.Infra.Tests/
    ├── NeoEditor.UI.Common.Tests/
    ├── NeoEditor.App.Tests/
    ├── NeoEditor.Plugins.DataViewer.Tests/
    ├── NeoEditor.Plugins.EntityEditor.Tests/
    ├── NeoEditor.Plugins.ImageTools.Tests/    ← [M11 新增]
    └── NeoEditor.Integration.Tests/            ← [M12 计划]
```

---

## 八、与 spec 规则的关系

本迁移**不改变**任何现有 spec 规则。新增规则：

| 规则 | 内容 | 对应章节 |
|------|------|---------|
| **R17** | Plugin 互不引用（.csproj 不含其他 Plugin 的 ProjectReference） | §2 |
| **R18** | Plugin 只依赖 Core + Infrastructure（禁止依赖 App 或其他 Plugin） | §2 |
| **R19** | 跨 Plugin 通信只走 IMessenger 事件 | §3.2 |
| **R20** | DI 注册在 App 的 Composition Root，Plugin 不自注册 | §3.1 |
| **R21** | 每个 Plugin 独立测试项目，只引用该 Plugin + Mock Core | §5 |

> 完整规则列表见 [spec/README.md](../spec/README.md)。迁移完成后将 R17-R21 加入正式规则表。

---

## 九、未来扩展

架构设计为以下扩展预留了插槽：

| 扩展 | 接入方式 |
|------|---------|
| **AI Generator** | 新 `NeoEditor.Plugins.AiGenerator` 项目，实现 `IToolPlugin`，调用 LLM API 生成 XML |
| **Validation Dashboard** | 新 `NeoEditor.Plugins.Validation`，订阅 `EntityModifiedMessage`，实时校验 |
| **Plugin Marketplace** | 扫描 `Plugins/` 目录加载外部 `.dll`，实现 `IPlugin` 即可 |
| **Headless CLI** | 新 `NeoEditor.Cli` 项目，只引用 Core + Infrastructure，不加载 Avalonia，做批量导入/导出 |

每个新功能 = 一个新 csproj + 一个 `IPlugin` 实现 + 在 App DI 注册一行。**不改任何现有代码。**
