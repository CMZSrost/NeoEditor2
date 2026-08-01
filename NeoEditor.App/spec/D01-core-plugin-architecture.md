# D01 — Core / Plugin 架构方向

> 项目根本架构决策 · 2026-07-24 · v2.0
> 效力：高于所有其他设计文档，与 R 系规则同级
> 上承：用户工作流分析 · 下启：[Docs/28](../Docs/28-plugin-architecture-migration.md) 迁移计划

---

## 一、项目本质

NeoEditor 本质上是一个 **XML 编辑工具**，服务于 NeoScavenger 游戏的 Mod 制作。

```
用户心智模型：

  游戏 XML 文件 ──→ 编辑器 ──→ 修改后的 XML 文件
                       │
                       ├── 可视化查看（不开游戏就能理解数据）
                       ├── 字段编辑（不手写 XML）
                       └── 图片编辑（配合数据修改）
```

### 核心后台能力

| 能力 | 说明 |
|------|------|
| **导入 Mod** | 从目录/文件读取 XML → 解析 → 写入数据库 |
| **Profile 编排** | 按 `getmods.php` 规则合并多个 Mod（命名空间、加载顺序、覆盖链） |
| **数据查询** | 合并后视图、引用解析（正向/反向）、搜索、过滤 |

这些能力构成了 **Core**：一个**对数据库读写 + Mod/Profile 管理**的数据引擎。Core 不知道 UI 的存在。

### 概念层级

```
业务概念层次（由底层到上层）：

  XML 数据 ──→ 数据库（EF Core + SQLite）
      │
      ├── Mod（数据来源，对应一个目录或文件）
      ├── Profile（Mod 编排规则，对应 getmods.php）
      ├── 命名空间（Mod 间隔离机制）
      ├── 覆盖链（同命名空间内优先级）
      └── 引用（跨实体/跨 Mod 的数据关联）

  这些概念全部源于「数据管理」这一个需求，不是凭空设计的。
```

### 数据定义独立性

游戏数据模型（25 个实体类型、枚举、字段元数据）是 **纯粹的领域定义**，与编辑器 UI 无关。
将其独立为 `NeoEditor.Core` 项目后，未来如需为**其他游戏**复用编辑器框架，
只需替换 Core 中的数据定义层，Plugin 和 App 层代码不感知数据 schema 变化。

---

## 二、模块全景

### 模块分类

项目分为三类模块：**基础设施**（非 UI，必须）、**基础 UI**（UI 工具箱，必须）、**Plugin**（功能扩展）。

```
                        ┌──────────────────────┐
                        │    NeoEditor.App      │  Shell + DI + 启动
                        │  (Hosting / Shell /   │
                        │   Settings)           │
                        └──────────┬───────────┘
                                   │ 组装所有模块
            ┌──────────────────────┼──────────────────────┐
            │                      │                      │
    ┌───────┴───────┐    ┌───────┴───────┐    ┌─────────┴─────────┐
    │ 基础 UI 模块    │    │   Plugin 层    │    │    Plugin 层       │
    │ (必需, 非可选)  │    │  (功能扩展)     │    │   (功能扩展)        │
    │               │    │               │    │                   │
    │ UI.Common     │    │ DataViewer    │    │ EntityEditor      │
    │ Avalonia 工具箱 │    │ ImageTools    │    │ AI Generator(未来) │
    └───────┬───────┘    └───────┬───────┘    └─────────┬─────────┘
            │                    │                       │
            └────────────────────┼───────────────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │     NeoEditor.Infra      │  数据 + 服务 (无 UI)
                    └────────────┬────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │   NeoEditor.Core        │  领域模型 + 契约 (最底层)
                    └────────────┬────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │ NeoEditor.Messaging     │  消息基础设施 (最底层)
                    └─────────────────────────┘
```

### 8 个模块清单

| 模块 | 类型 | 职责 | 依赖 |
|------|:--:|------|------|
| `NeoEditor.Messaging` | 基础设施 | 消息基类、泛型信封、`MessageBus` 包装 | 无（仅 net10.0） |
| `NeoEditor.Core` | 基础设施 | 领域模型、Core 消息、Plugin 契约、校验 | Messaging |
| `NeoEditor.Infra` | 基础设施 | 数据访问、Mod/Profile 管理、合并、索引、导出 | Core, EF Core, SQLite |
| `NeoEditor.UI.Common` | 基础 UI | 共享 Avalonia 控件、转换器、行为、样式 | Avalonia |
| `NeoEditor.App` | Shell | 启动引导、DI、窗口布局、设置、Plugin 发现 | Core, Infra, UI.Common, Avalonia, Dock |
| `NeoEditor.Plugins.DataViewer` | Plugin | DataTable、导航、Peek、搜索、索引表 | Core, Infra, UI.Common, Avalonia |
| `NeoEditor.Plugins.EntityEditor` | Plugin | XML 编辑、KV 编辑、25 个 Visualizer | Core, Infra, UI.Common, Avalonia, AvaloniaEdit |
| `NeoEditor.Plugins.ImageTools` | Plugin | 图片查看、编辑、像素处理 | Core, Infra, UI.Common, Avalonia, ImageSharp |

> **基础 UI (`UI.Common`) 是模块，不是 Plugin。** 它是所有 Plugin 和 App 共享的 Avalonia 工具箱，
> 必须随 App 一起发布。Plugin 是可选的（理论上可以移除某个 Plugin 而不影响其他）。

### 测试项目（每个模块一个 + 集成测试）

```
Tests/
├── NeoEditor.Messaging.Tests/
├── NeoEditor.Core.Tests/
├── NeoEditor.Infra.Tests/
├── NeoEditor.UI.Common.Tests/
├── NeoEditor.App.Tests/
├── NeoEditor.Plugins.DataViewer.Tests/
├── NeoEditor.Plugins.EntityEditor.Tests/
├── NeoEditor.Plugins.ImageTools.Tests/
└── NeoEditor.Integration.Tests/          ← 跨 Plugin 全链路
```

---

## 三、消息架构

### 设计原则

1. **消息基础设施独立** — `NeoEditor.Messaging` 提供泛型基类和消息总线抽象，不包含任何业务消息
2. **Core 定义系统消息** — 与数据生命周期相关的事件（entity saved, session changed 等）
3. **Plugin 定义自身消息** — 每个 Plugin 在自己的项目中定义内部消息类型
4. **跨 Plugin 消息放在 Core** — 如果一条消息需要被其他 Plugin 接收，提升到 Core 定义
5. **Core 不依赖 Plugin** — 单向依赖，不可逆

### 消息归属规则

```
消息归属于哪里？

  ┌─────────────────────────────────────────────────┐
  │ 这条消息只有发送方自己用？                         │
  │   → 定义在发送方 Plugin 内部                      │
  │                                                 │
  │ 这条消息需要被 Core 服务处理？                      │
  │   → 定义在 Core                                   │
  │                                                 │
  │ 这条消息需要被另一个 Plugin 接收？                   │
  │   → 定义在 Core（作为跨 Plugin 契约）               │
  │                                                 │
  │ 这条消息是数据生命周期事件？                         │
  │   → 定义在 Core（EntitySaved, SessionChanged 等）  │
  └─────────────────────────────────────────────────┘
```

**关键约束**：Plugin A 不知道 Plugin B 的存在。当 Plugin A 需要 Plugin B 对某事件做出反应时，
该事件的消息类型必须定义在 Core 中，双方都引用 Core。

### 未来扩展

当 Plugin 数量超过 8 个时，评估引入弱类型消息信封（`TaggedMessage`）以进一步降低 Core 的消息定义负担。
当前阶段（3-5 个 Plugin）强类型消息完全够用。

---

## 四、Core 与 Plugin 的边界

### Core 的定义

**Core = 数据模型 + 数据访问 + 业务服务。不包含任何 UI。**

| 层 | 项目 | 职责 |
|----|------|------|
| 消息基础 | `Messaging` | 泛型基类、消息总线抽象 |
| 领域 + 契约 | `Core` | 25 个游戏实体、枚举、校验规则、Core 消息、Plugin 契约 |
| 数据 + 服务 | `Infra` | EF Core DbContext、ModManager、ProfileManager、MergeService、引用索引、XML 导入导出、WAL 持久化、ConfigService |

### Plugin 的定义

**Plugin = 面向用户的编辑/查看功能。每个 Plugin 是一个自包含的垂直切片。**

| Plugin | 基于 Core 的什么能力 | 提供的用户功能 |
|--------|-------------------|---------------|
| **DataViewer** | 数据查询（Store / MergeView）、引用索引 | 只读 DataTable、排序过滤、Ctrl+Click 跳转、Ctrl+RMB Peek、正向/反向索引 |
| **EntityEditor** | 实体 CRUD（IWorkspaceSession） | XML 编辑、KV 字段编辑、25 个卡片式 Visualizer、覆盖链展示 |
| **ImageTools** | 图片文件 I/O | 图片查看、编辑、像素算法处理 |
| **AI Generator**（未来） | 数据模型 Schema | LLM 生成符合规范的 XML 游戏数据 |

### 边界铁律

```
┌──────────────────────────────────────────────────┐
│                  Plugin Layer                      │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐          │
│  │DataViewer│ │EntityEdit│ │ImageTools│          │  ← 互相 0 引用
│  └────┬─────┘ └────┬─────┘ └────┬─────┘          │
│       │             │            │                 │
│       │     ┌───────┴───────┐    │                 │
│       │     │  UI.Common    │    │                 │  ← 共享 UI 工具箱
│       │     └───────┬───────┘    │                 │
│       └─────────────┼────────────┘                 │
├─────────────────────┼──────────────────────────────┤
│                     │                               │
│              ┌──────┴──────┐                        │
│              │    Infra    │  数据 + 服务              │  ← 0 UI 依赖
│              └──────┬──────┘                        │
│                     │                               │
│              ┌──────┴──────┐                        │
│              │    Core     │  领域 + 契约              │
│              └──────┬──────┘                        │
│                     │                               │
│              ┌──────┴──────┐                        │
│              │  Messaging  │  消息基础设施             │
│              └─────────────┘                        │
└─────────────────────────────────────────────────────┘
```

**Plugin 测试原则**：每个 Plugin 的测试项目只引用 Core + UI.Common + 该 Plugin 本身。
使用 Mock 替代 Infra 和 App，**不启动完整的 Avalonia 应用**。

---

## 五、Plugin 加载方式

### 决策：启动时加载（非热拔插）

| 方面 | 决策 |
|------|------|
| **加载时机** | 应用启动时，App 扫描已注册的 Plugin 程序集，调用 `InitializeAsync` |
| **生命周期** | Plugin 随应用启动而初始化，随应用退出而释放。运行期不卸载 |
| **版本管理** | 所有模块共享同一版本号（Solution 级别统一），不做独立版本 |
| **分发方式** | Plugin 随 App 一起编译和发布。不做 NuGet 包、不做独立 dll 分发 |

### 为什么不做热拔插

- 桌面应用重启用时几秒，热拔插带来的复杂度（AssemblyLoadContext、依赖隔离、状态迁移）不值得
- 所有 Plugin 共享同一数据引擎实例（`IWorkspaceSession`），运行时卸载 Plugin 需要处理脏状态、未保存编辑等复杂问题
- 当前阶段 Plugin 数量少（3-5 个），不需要动态加载能力
- 未来如果有**外部贡献者**编写第三方 Plugin，再评估动态加载方案

### 当前注册方式

```csharp
// App 启动时显式注册（编译期已知所有 Plugin）
services.AddPlugin<DataViewerPlugin>();
services.AddPlugin<EntityEditorPlugin>();
services.AddPlugin<ImageToolsPlugin>();
```

---

## 六、Plugin 的 UI 呈现

### 注册模型

Plugin 不自己决定 UI 位置。App Shell 通过契约询问 Plugin，然后统一布局：

```csharp
// Core 定义的契约
public interface IToolPlugin : IPlugin
{
    string Title { get; }
    ToolDock DefaultDock { get; }    // Left | Right | Bottom
    int Order { get; }               // 在同侧 Dock 中的排序
    Control CreateToolView();
}

public interface IDocumentPlugin : IPlugin
{
    IReadOnlyList<string> SupportedEntityTypes { get; }
    DocumentViewBase CreateDocument(IEntity entity, IPluginContext ctx);
}
```

### App Shell 内部结构

```
NeoEditor.App/
├── Hosting/          启动引导、DI 注册、Plugin 发现、生命周期管理
├── Shell/            窗口管理、Dock 布局、页面路由、工具栏
│   ├── Sidebar/      侧边栏 + 面板管理
│   └── StatusBar/    状态栏
├── Settings/         全局配置、Plugin 配置注册、设置页面
└── Infrastructure/   主题、本地化 UI 资源、字体
```

---

## 七、设置系统的定位

设置不是独立 Plugin，是 **App Shell 的一部分**。它管理：

| 设置分类 | 内容 | 影响范围 |
|----------|------|---------|
| 环境配置 | GameRootDir、语言、主题、字体大小 | App Shell 全局 |
| 编辑行为 | 自动保存间隔、快照频率、导出格式 | Core 服务 |
| Plugin 配置 | 列可见性、默认排序、面板可见性 | 各 Plugin 读取 |
| 保存策略 | Quick Save vs Export、XML 格式化选项 | Core 导出服务 |

Plugin 通过 `IConfigService`（Core 接口）读写自身配置，不直接依赖 Settings UI。

---

## 八、开发策略

### 当前阶段

```
Phase 1 (M0-M6): 架构修复      ✅ 完成   Spec 22/22, 0 架构债
Phase 2 (M7):    代码卫生      🔵 当前   质量基础
Phase 3 (M8-M9): 插件化拆分    ⬜ 计划   结构基础
Phase 4 (将来):   功能扩展     ⬜ 待定   新 Plugin 开发
```

### Plugin 开发判断标准

是否值得新开一个 Plugin？
- ✅ 有自己的数据模型或外部 API 依赖
- ✅ 有自己的 UI 区域（新的 Tool 或 Document）
- ✅ 可以独立描述为一个用户故事
- ❌ 只是对现有 Plugin 的增强 → 在现有 Plugin 内迭代

### 候选 Plugin（按需启动）

| Plugin | 触发条件 | 工作量 |
|--------|---------|:--:|
| **AI Generator** | LLM API 可用 + 实际需求 | 1-2w |
| **Validation Dashboard** | 用户反馈校验需求 | 1w |
| **Batch Operations** | 用户反馈批量编辑需求 | 1w |

---

## 九、反模式 & 暂不做的

### 什么不是 Plugin

| 反模式 | 为什么 | 正确做法 |
|--------|--------|---------|
| 每个 Visualizer 一个 Plugin | 25 个项目，构建爆炸。共享 VisHelper、EntityVisualizerRegistry | 放在 EntityEditor Plugin 内 |
| Navigation 独立 Plugin | 与 DataTable 共享 DataGrid 基础设施 | 作为 DataViewer 内子模块 |
| Settings 独立 Plugin | 管理的是 App 配置，是胶水层 | 放在 App 内 |
| Localization 独立 Plugin | 所有层都需要，横切关注点 | 放在 Core（资源字符串）+ App（UI 资源） |

### 暂不做的（留到未来评估）

| 议题 | 为什么现在不做 | 触发条件 |
|------|---------------|---------|
| Plugin 热加载 | 桌面应用重启足够快，热加载收益 < 复杂度 | 有外部贡献者写第三方 Plugin |
| 多窗口支持 | 单窗口 + Dock 覆盖当前需求 | 用户明确需要时 |
| 跨进程 Plugin 隔离 | .NET 桌面应用进程隔离太重 | 某 Plugin 频繁崩溃时 |
| 游戏数据版本兼容 | NeoScavenger 已停止更新 | 实际发生时处理 |

---

## 十、与现有规则的关系

### 本决策是以下规则的上位依据

| 规则 | 如何推导 |
|------|---------|
| R07 (单向分层) | Messaging → Core → Infra → UI.Common / Plugin → App |
| R14 (文件夹约定) | 扩展为 csproj 级别隔离 |
| R17-R21 (插件规则) | 本决策定义了 Core/Plugin 边界后的执行细则 |

### spec 新增计划

| 规则 | 内容 | 写入时机 |
|------|------|:--:|
| **R17** | Plugin 互不引用 | M8b |
| **R18** | Plugin 只依赖 Core + Infra + UI.Common | M8b |
| **R19** | 跨 Plugin 消息放 Core，单向依赖 | M8b |
| **R20** | DI 注册在 App Composition Root | M8b |
| **R21** | 每个模块独立测试项目 | M8a |
| **R22** | 集成测试独立项目，覆盖跨 Plugin 核心链路 | M9 |

---

## 十一、总结

```
一句话方向：

  Messaging 做消息管道，Core 做领域模型，Infra 做数据引擎，
  UI.Common 做共享工具箱，Plugin 做功能，App 做组装。
  
  Plugin 启动时加载，互相零引用，通过 Core 消息通信。
  新功能 = 新 Plugin = 新 csproj，不改现有代码。
  
  数据定义独立于编辑器，为跨游戏复用预留空间。
```
