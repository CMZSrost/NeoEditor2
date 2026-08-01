# D02 — 动态 Dock 布局：Tool/Document/Service Plugin 分类 + IToolPlugin 动态构建

> 项目架构决策 · 2026-08-01 · v1.1
> 效力：Dock 布局的根本决策，取代手写 XAML Tool 元素。与 D01 同级
> 上承：D01 六 § Plugin 的 UI 呈现 · 下启：Phase 9E 实现
> 关联：R17-R22 Plugin 隔离规则 · R27 图片资产双视图 · R28 AI/MCP 配置
> **v1.1 修订（2026-08-01，Phase 9E 落地）**：§五 映射按实际实现订正——DataViewer 拆 **5** 个 IToolPlugin 类
> （Conflicts/Validation 工具已于 Phase 9B 删除，不建）；补充 `ImageOrchestrationPlugin`(Right)；
> Document 侧只有 `EntityEditorPlugin` 实现 `IDocumentPlugin`，ModImages / Profile 编排 Document
> 暂由 App shell 消息处理（未插件化，见 §五注）。
> **v1.2 修订（2026-08-01，遗留清理）**：`ModImagesDocument` 改为通过
> `IModImagesDocumentFactory` / `ModImagesDocumentFactory`（ImageTools）创建（App shell 不再直接 new）；
> Profile 编排 Document（EditProfileView）属 App 内部文档，保持 App shell 消息处理（见 §五注）。

---

## 一、决策

1. **Plugin 按"是否涉及 Dock 组件"分三类**：**Tool plugin** / **Document plugin** / **Service plugin**
2. **IToolPlugin 粒度 1:1**：一个 IToolPlugin 贡献 **一个 Tool 组件**，并搭配围绕它的功能（Toolbar 按钮、关联 Document、命令）
3. **Toolbar ≠ Tool**：面板内部的按钮组（Toolbar）是固定装配，不属于独立 Tool 单元
4. **App Shell 枚举 `IEnumerable<IToolPlugin>` 动态构建 Dock 布局**，不再手写 `<Tool>` XAML 元素
5. **所有 Tool 组件都走 IToolPlugin**——包括工作台骨架工具（DataTable、Peek、KeyValueEditor、OverlayChain 等）与 Plugin 功能工具（AiChat、Profile Tool、ImageAssetManager 等）
6. **DataViewer 程序集拆分为多个 IToolPlugin 类**，每个 Tool 各对应一个（落地：底部 4 个 + Peek 右，共 5 个；Conflicts/Validation 已于 Phase 9B 删除，见 v1.1 §五）

---

## 二、为什么

### 现状问题

1. `IToolPlugin` 接口完整但**零消费**——全代码库无任何地方枚举 `IEnumerable<IToolPlugin>` 或调用 `CreateToolView()`
2. 多个 `CreateToolView()` 返回 `null!`（EntityEditor、ImageTools）——Plugin 自身的 Tool 接口形同虚设
3. Dock 布局硬编码：`Documents.cs` 里 13 个 Tool 子类，`DocumentWorkspaceView.axaml` 手写 `<Tool>` 元素
4. Plugin 元数据（`DefaultDock` / `Order`）是死配置；AiChatPlugin 实现了完整 UI 却从未出现在 Dock

### 方案 A 收益

| 维度 | 手写 XAML（现状） | 动态构建（方案 A） |
|------|:--:|:--:|
| 新增 Tool | 改 App Shell 3 个文件 | 新增一个 IToolPlugin 类 |
| Dock 位置 | 硬编码 | Plugin 自描述（DefaultDock + Order） |
| 布局持久化 | 依赖 Tool.Id | 依赖 Plugin 类型名（稳定） |
| 删除 Tool | 手动清理 App Shell | 移除 DI 注册即可 |

---

## 三、分类模型

### Dock 组件只有两种

```
Dock
  ├── Tool        → ToolDock 里的 tab 项（工具面板）
  └── Document    → DocumentDock 里的文档项
```

### Plugin 三分类（按涉及什么组件）

| 类型 | 涉及 | 例子 |
|------|------|------|
| **Tool plugin** | 提供 Tool 组件 | AiChat、Profile Tool、DataTable 等 |
| **Document plugin** | 提供 Document 组件 | EntityEditor 的实体编辑 Document、ModImagesDocument |
| **Service plugin** | 不涉及 Dock，用于其他 Page | CLI、MCP |

> 一个逻辑 Plugin（程序集）可以有多个 IToolPlugin / IDocumentPlugin 实现类，
> 但**一个 IToolPlugin 类只对应一个 Tool 组件**。

---

## 四、IToolPlugin 契约

```csharp
public interface IToolPlugin : IPlugin
{
    string Title { get; }
    ToolDock DefaultDock { get; }     // Left | Right | Bottom
    int Order { get; }

    object CreateToolView();          // Tool 面板本体

    // 围绕 Tool 的功能（可选）——Toolbar 按钮，固定装配在 Tool 面板内
    IReadOnlyList<ToolbarItem>? CreateToolbarItems() => null;
}

public record ToolbarItem
{
    public string Id { get; init; }
    public string Label { get; init; }
    public string? IconSymbol { get; init; }
    public IRelayCommand Command { get; init; }
    public string? Group { get; init; }   // 分组（Navigation / Edit / View / Persistence）
    public int Order { get; init; }
}
```

**配套功能**（与 Tool 绑定在同一 plugin）：
- `CreateToolbarItems()` —— 面板内工具栏按钮
- 关联 Document —— 通过 `IDocumentPlugin` 提供（如 KV Tool ↔ 实体编辑 Document）
- 相关命令 / 服务

---

## 五、Tool → Plugin 映射

### DataViewer 程序集（拆分为 5 个 IToolPlugin 类）

> 原 7 个含 Conflicts/Validation，两者工具已于 Phase 9B 删除（9B 删 Validation/Conflicts），故不建。

| Tool | IToolPlugin 类 | Dock |
|------|---------------|------|
| DataTable | `DataTablePlugin` | Bottom |
| Ref Index | `ForwardIndexPlugin` | Bottom |
| Reverse Index | `ReverseIndexPlugin` | Bottom |
| Search | `SearchPlugin` | Bottom |
| Peek | `PeekPlugin` | Right |

### 其他程序集

| Tool | IToolPlugin 类 | Dock | 归属 |
|------|---------------|------|------|
| KeyValueEditor | `KeyValueEditorPlugin` | Left | EntityEditor |
| OverlayChain | `OverlayChainPlugin` | Left | EntityEditor |
| ImageAssetManager | `ImageAssetManagerPlugin` | Left（从 Right 移入） | ImageTools |
| ImageOrchestration | `ImageOrchestrationPlugin` | Right | ImageTools |
| Profile Tool | `ProfileToolPlugin` | Left | App（新） |
| AI Chat | `AiChatPlugin` | Right | AiChat |

### Document Plugin

> 落地现状：只有实体编辑 Document 走 `IDocumentPlugin`（类名沿用 `EntityEditorPlugin`）。
> `ModImagesDocument` 走 `IModImagesDocumentFactory` / `ModImagesDocumentFactory`（ImageTools），
> App shell 通过 DI 获取工厂创建（v1.2，不再直接 new 文档类型）。
> Profile 编排 Document（EditProfileView）属 App 内部文档，保持 App shell 消息处理
> （`EditProfileMessage` → `DocumentWorkspaceViewModel`），无需跨程序集解耦。

| Document | 提供机制 | 归属 |
|----------|----------|------|
| 实体编辑 Document | `EntityEditorPlugin`（IDocumentPlugin） | EntityEditor |
| ModImagesDocument | `ModImagesDocumentFactory`（IModImagesDocumentFactory） | ImageTools |
| Profile 编排 Document（EditProfileView） | App shell 消息处理（App 内部文档） | App |

---

## 六、动态构建

App Shell 构造时：

```csharp
// 1. 枚举所有 IToolPlugin
var plugins = _serviceProvider.GetRequiredService<IEnumerable<IToolPlugin>>();

// 2. 按 DefaultDock 分组，组内按 Order 排序
foreach (var plugin in plugins.OrderBy(p => p.Order))
{
    var tool = new PluginTool(plugin);          // Tool 子类：Id = plugin 类型名，Title = plugin.Title
    tool.Context = plugin.CreateToolView();
    tool.ToolbarItems = plugin.CreateToolbarItems();
    switch (plugin.DefaultDock)
    {
        case ToolDock.Left:   leftTools.Add(tool);   break;
        case ToolDock.Right:  rightTools.Add(tool);  break;
        case ToolDock.Bottom: bottomTools.Add(tool); break;
    }
}
```

- `PluginTool` 的 `Id` = `plugin.GetType().Name`（稳定，供 Dock.Avalonia 布局持久化）
- **App Shell 只保留 Dock 容器结构**（ToolDock / DocumentDock / ProportionalDock / Splitter），Tool 组件全部由 Plugin 贡献

---

## 七、不做什么

- **不做**：Plugin 热拔插（D01 已决策）
- **不做**：Toolbar 动态排序 UI（用户不可拖拽重排）；`CreateToolbarItems()` 只提供静态贡献
- **不做**：改 Dock.Avalonia 布局序列化格式（复用内置，按 Plugin 类型名做 Id）

---

## 八、与现有规则关系

| 规则 | 关系 |
|------|------|
| D01 六 § Plugin 的 UI 呈现 | 本决策是其实现：`IToolPlugin` 从"定义但未用"变为"定义且消费" |
| R17 Plugin 互不引用 | Plugin 不感知彼此；分组排序由 App Shell 统一处理 |
| R20 DI 注册在 App | 动态构建在 App 内完成，Plugin 只提供契约实现 |
| R27 / R28 | ImageAssetManager（左 Dock）与 AiChat 均通过本机制接入 |
