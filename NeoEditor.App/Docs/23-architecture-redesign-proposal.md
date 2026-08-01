# NeoEditor 架构设计

> v0.23.0-dev · 2026-06-25
> 前置阅读: [24-workflow-specification.md](24-workflow-specification.md) · [14-reference-resolution-system.md](14-reference-resolution-system.md)

---

## 一、页面生命周期

编辑器划分为四个页面：

```
NeoEditor
├── WelcomePage    — 引导、快捷导航、恢复工作历史
├── WorkspacePage  — Mod 数据查看与编辑（核心页面）
├── SettingsPage   — 全局配置展示与修改
└── OtherPage      — 扩展预留
```

**WorkspacePage 的核心目标**：

| 优先级 | 目标 | 说明 |
|--------|------|------|
| 1 | **方便查看数据** | 不开游戏就能知道数据长什么样、有什么用 |
| 2 | **方便修改数据 + 查看变更** | 两个编辑入口（KV / XML），变更即时可见可追踪 |
| 3 | **Profile 全貌** | 全体数据、覆盖关系、Mod 归属、正向索引、反向索引 |

---

## 二、CRUD 模型

| 操作 | 入口 | 说明 |
|------|------|------|
| **Create** | 工具栏 | Workspace 级元操作 |
| **Copy** | 工具栏 | 复制当前选中实体 |
| **Delete** | 工具栏 | 删除当前选中实体 |
| **Read** | 全部四个区域 | Center 可视化、Bottom DataTable、Left KV、Right Peek |
| **Update** | Left KV + Center XML | 两种编辑模式，共享同一实体实例 |

---

## 三、工作区四区域布局

```
┌──────────────────────────────────────────────────────────────────┐
│ 工具栏: [New] [Copy] [Delete] | [Save] | [面板切换 Left/Right/Bottom] │
├──────┬──────────────────────────────┬───────────────────────────┤
│ 侧栏 │ Left ToolDock               │ Right ToolDock            │
│      │ ┌────────────────────────┐  │ ┌───────────────────────┐ │
│      │ │ KeyValue Editor        │  │ │ Peek Panel            │ │
│      │ │ 绑定 = 最后焦点的实体    │  │ │ Ctrl+RMB 固定预览      │ │
│      │ │ Update 入口之一         │  │ │ 面包屑（低调样式）      │ │
│      │ ├────────────────────────┤  │ │ 只读, 不夺焦, 不跳转    │ │
│      │ │ OverlayChain           │  │ └───────────────────────┘ │
│      │ │ 覆盖链                  │  │                           │
│      │ └────────────────────────┘  │                           │
│      │ Center DocumentDock          │                           │
│      │ ┌────────────────────────┐  │                           │
│      │ │ EntityEditorDocument   │  │                           │
│      │ │ Tab1: 可视化 (Read)     │  │                           │
│      │ │ Tab2: XML 编辑 (Update) │  │                           │
│      │ │ Document 模式, 可多开   │  │                           │
│      │ └────────────────────────┘  │                           │
│      │                              │                           │
│      │ Bottom ToolDock (Scope 唯一)  │                           │
│      │ ┌────────┬────────┬────────┐ │                           │
│      │ │Data    │Ref     │Reverse │ │                           │
│      │ │Table   │Index   │Index   │ │                           │
│      │ │只读     │只读     │只读     │ │                           │
│      │ │+跳转   │手动刷新  │手动刷新  │ │                           │
│      │ └────────┴────────┴────────┘ │                           │
├──────┴──────────────────────────────┴───────────────────────────┤
│ 状态栏: 当前 Mod/Profile · 选中实体 · Dirty 状态 · 保存时间       │
└──────────────────────────────────────────────────────────────────┘
```

### 3.1 Center — 可视化 + XML（最大面积）

| 属性 | 说明 |
|------|------|
| **定位** | 主要数据展示区，不开游戏就能理解数据 |
| **构成** | `EntityEditorDocument` 双 Tab：Visual(只读) + XML Edit |
| **更新入口** | XML Tab 是 Update 入口之一 |
| **显示形式** | **Document 模式**，可多开拖拽对比不同实体 |
| **绑定** | 与 Left KV、Bottom DataTable 共享同一 `IEntity` 实例 |

**Visual Tab**：通过 `IEntityVisualizer.BuildDetail(entity)` 渲染卡片式可视化。

**XML Tab**：AvaloniaEdit 显示 `EntityXmlHelper.GenerateXmlFragment(entity)` 生成的 XML。`ApplyXmlToEntity` 将编辑写回实体对象。

### 3.2 Left — KeyValue 编辑器 + 覆盖链

| 属性 | 说明 |
|------|------|
| **绑定对象** | **最后一次获得焦点的实体**（非当前打开文档） |
| **定位** | 为不想编辑 XML 的用户提供直接字段修改入口 |
| **分组** | `FieldGroupMetadata` 按语义分组，可折叠 Section |
| **控件适配** | TextBox(string) / NumericUpDown(int/float) / ToggleSwitch(bool) / ComboBox(Enum) / ReadOnly(主键) |

**焦点跟随逻辑**：

```
多开场景：
  [Doc A: ItemType #42] [Doc B: Recipe #15] [Doc C: AttackMode #8]
                                ↑ 用户刚点击
  Left KV Editor → 编辑 Recipe #15 的字段

触发来源：
  Center EntityEditorDocument 获得焦点  → KV 切换到该实体
  Bottom DataTable 行选中              → KV 切换到该实体
```

### 3.3 Right — Peek 面板

| 属性 | 说明 |
|------|------|
| **旧名** | ValueEditor（不合适——只读面板）→ 改为 **Peek** |
| **触发方式** | **Ctrl+右键** 点击引用 → 固定预览目标 Overview |
| **设计原则** | 不夺焦，不跳转，用户注意力保持在主动作数据上 |
| **面包屑** | 支持多重引用链回溯，低调样式 |
| **按钮** | Pin 锁定 / Open Full 跳转 / Close |

**交互模型**：

```
Ctrl+RMB 点击引用徽章/单元格
  → Peek Panel 固定显示目标实体 BuildOverview
  → 用户松手，Peek 内容保持
  → 面包屑追加一条，点击回溯
  → 再次 Ctrl+RMB 另一个引用 → 刷新为新目标
  → 手动关闭或 Clear → 面板清空
```

### 3.4 Bottom — DataTable + 索引（Scope 唯一）

三个 Tab 共享当前 Profile / Mod 的单一 Scope。作为 **Tool 而非 Document**，只能有一个实例，避免多 Profile 导致索引和覆盖混乱。

| Tab | 内容 | 读写 | 刷新策略 |
|------|------|:--:|------|
| **DataTable** | 全体数据（ModGameDataTabsView） | 只读 + 跳转 | 随数据加载自动 |
| **Ref Index** | 正向引用索引 | 只读 | **手动刷新** |
| **Reverse Index** | 反向引用索引 | 只读 | **手动刷新** |

> 索引构建开销大，不跟随实体选中自动重建。用户需要时点击刷新按钮，避免卡顿。

**DataTable 只读说明**：不可原地编辑单元格，但保留跳转功能（选中行 → Center 打开对应 EntityEditorDocument）。编辑必须走 Left KV 或 Center XML。

---

## 四、快捷键与交互

| 操作 | 触发 | 行为 |
|------|------|------|
| **Navigate** | Ctrl+左键 | 打开/跳转 EntityEditorDocument，改变主视区焦点 |
| **Peek** | Ctrl+右键 | 锁定目标 Overview 到 Right Peek Panel，不跳转 |
| **KV 焦点跟随** | Center 文档被点击 / Bottom 行被选中 | Left KV 自动切换到该实体 |

> **设计意图**：Navigate 改变主视区焦点，Peek 是临时查阅不夺焦。两者职责分离。

---

## 五、四区域同步机制

四个区域绑定**同一个 `IEntity` 对象实例**，通过 `INotifyPropertyChanged` 自动联动。

```
KeyValue 编辑器 (Enter / 失焦) 或 XML Tab (ApplyXmlToEntity)
  → 写内存对象属性
  → 标记 Dirty
  → PropertyChanged 广播：

同一个实例
  ├→ Center 可视化自动刷新 (BuildDetail)
  ├→ Center XML 自动刷新 (若 !IsXmlFocused)
  ├→ Bottom DataTable 对应单元格更新
  ├→ Left KV 其他反射字段刷新
  └→ 状态栏 Dirty 标记

Ctrl+S / Save
  → 提交 DB → 导出 XML → 清除 Dirty
```

---

## 六、Document 与 Tool 体系

### Document 类型

```
IDocumentBase
  ├── DocumentBase (ObservableObject)
  │     ├── XmlDocument, XmlDiffDocument
  │     ├── ModGameDataDocument    (单 Mod 视图, Center)
  │     ├── MergeEditorDocument    (合并视图, Center)
  │     ├── PlainTextDocument, ImageDocument
  │     └── EntityEditorDocument ★ (可视化 + XML 双 Tab, Center)
  └── DocumentViewBase (ViewModelBase)
        ├── MarkdownDocument, ModImagesDocument
        ├── ImageEditorDocument, EditProfileViewModel
        ├── EntityBrowserDocument ★ (数据浏览器)
        └── EntityViewerDocument ★ (单实体查看)
```

### Tool 类型（ToolDock 面板）

| Tool | ID | 区域 | Context |
|------|-----|------|---------|
| `KeyValueEditorTool` | KeyValueEditor | Left | `KeyValueEditorViewModel` |
| `OverlayChainTool` | OverlayChain | Left | `OverlayChainToolContent` |
| `PeekTool` | Peek | Right | `PeekPanelViewModel` |
| `DataTableTool` | DataTable | Bottom | `ModDataToolViewModel` |
| `ForwardIndexTool` | ForwardIndex | Bottom | `IndexTableViewModel` |
| `ReverseIndexTool` | ReverseIndex | Bottom | `IndexTableViewModel` |
| `SearchResultsTool` | SearchResults | Bottom | `BottomToolsViewModel` |
| `ConflictsTool` | Conflicts | Bottom | `BottomToolsViewModel` |
| `ValidationTool` | Validation | Bottom | `BottomToolsViewModel` |

---

## 七、消息系统

### 实体选择与导航

| 消息 | 发送方 | 接收方 | 用途 |
|------|--------|--------|------|
| `EntitySelectedMessage` | Bottom DataGrid | DocWorkspaceVM | 全区域联动（Center 开文档 + KV 切换 + 覆盖链刷新） |
| `NavigateToEntityRequestedMessage` | Peek"Open Full"/Ctrl+LMB | DocWorkspaceVM | 打开/跳转 EntityEditorDocument |
| `ActiveEntityChangedMessage` | EntityEditorDocument | Left KV | 焦点实体变更 → KV 重新绑定 |

### Peek 引用

| 消息 | 用途 |
|------|------|
| `PeekEntityMessage` | Ctrl+RMB → PeekPanel 固定目标 Overview |
| `PeekReferenceRequestMessage` | KV 引用字段 Peek 按钮 → 解析引用 → Peek |
| `PeekContentChangedMessage` | Peek 面板内容更新通知 |

### 页面与 Session

| 消息 | 用途 |
|------|------|
| `NavigateToPageMessage(PageType)` | 页面切换 |
| `SessionStateChangedMessage(bool)` | Session 活跃/清空 → 自动切换 Workspace/Home |

### 编辑与保存

| 消息 | 用途 |
|------|------|
| `FieldEditedMessage` | KV 字段修改标记 |
| `EntityChangesAppliedMessage` | 变更已提交 |
| `EntityChangesRevertedMessage` | 变更已撤销 |
| `SaveRequestedMessage` | 保存请求 → DB + XML 导出 |

---

## 八、文件清单

### 新增文件

| 文件 | 说明 |
|------|------|
| `PageNavigationMessage.cs` | 页面导航消息 + `PageType` 枚举 |
| `WorkspaceMessages.cs` | 工作区消息 |
| `FieldGroupMetadata.cs` | 字段分组元数据 |
| `SettingsPageViewModel.cs` | 设置页 VM |
| `EntityEditorDocument.cs` | 实体编辑器文档 + `EntityXmlHelper` |
| `KeyValueEditorViewModel.cs` | KV 编辑器 VM + `FieldSection`/`FieldRow` |
| `PeekPanelViewModel.cs` | Peek 面板 VM + `PeekBreadcrumb` |
| `IndexTableViewModel.cs` | 索引表 VM |
| `ModDataToolViewModel.cs` | 底部 DataTable 的 Context |
| `EntityEditorView.axaml/.cs` | 实体编辑器视图 |
| `KeyValueEditorView.axaml/.cs` | KV 编辑器视图 |
| `PeekPanelView.axaml/.cs` | Peek 面板视图 |
| `IndexTableView.axaml/.cs` | 索引表视图 |
| `SettingsPageView.axaml/.cs` | 设置页视图 |
| `BrowserIndexService.cs` | 浏览器索引服务 |
| `ReferenceIndexService.cs` | SQLite 引用索引服务 |

### 关键修改文件

| 文件 | 改动 |
|------|------|
| `MainWindow.axaml` | 三页面 + 侧栏 + 工具栏 |
| `MainWindowViewModel.cs` | `CurrentPage` + 消息接收 |
| `DocumentWorkspaceViewModel.cs` | 四区域 VM + 消息协调 |
| `DocumentWorkspaceView.axaml` | ProportionalDock 四区域 |
| `Documents.cs` | EntityEditorDocument + Tool 类 |
| `ModGameDataTabsView.*` | EntitySelectedMessage 发送 |

---

## 九、与当前代码的差距

| # | 项目 | 当前代码 | 目标 |
|---|------|---------|------|
| 1 | Bottom DataTable | 可编辑（ModGameDataTabsView 读写） | **只读 + 保留跳转** |
| 2 | Create/Copy/Delete | DataGrid 右键菜单 | **工具栏元操作** |
| 3 | Ctrl+LMB | 触发 Peek | **改为 Navigate**（跳转标签页） |
| 4 | Ctrl+RMB | 同 LMB | 保持 Peek（**与 LMB 职责分离**） |
| 5 | Right Panel View | ValueEditorPanel | **PeekPanelView** + 低调面包屑 |
| 6 | KV 绑定逻辑 | 跟消息触发 | 跟**最后焦点实体** |
| 7 | 四区域同步 | 部分走消息 | 绑定**同一 IEntity 实例** |
| 8 | Scope 唯一性 | 单实例无强约束 | 强制 **Tool 语义**，阻止多开 |
| 9 | 索引刷新 | 跟随选中自动 | **手动刷新**（减少卡顿） |

---

## 十、关键设计原则

1. **可视化优先** — Center 可视化 + XML 占据最大面积，DataTable 沉底辅助。
2. **Scope 唯一性** — Bottom Tool 是 Workspace 级单例，同一时刻只有一个 Profile 的数据+索引。
3. **只读数据全貌** — Bottom 只读不引入额外编辑源，修改必须走 Left KV 或 Center XML。
4. **Peek 不夺焦** — Ctrl+RMB 固定预览，用户注意力保持在主数据。Navigate 走 Ctrl+LMB，职责分离。
5. **KV 跟焦点不跟文档** — 多开 EntityEditorDocument 时，KV 绑定最后交互的实体。
6. **同源同实例** — 四区域共享同一 `IEntity` 对象，一处改处处更新，消除数据不一致。
7. **索引手动刷新** — 避免跟随选中自动重建导致的卡顿。
