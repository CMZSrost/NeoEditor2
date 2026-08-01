# NeoEditor 架构决策与分层

> v1.1 · 2026-06-30 · 本轮纯结构重构（不修功能 bug、不写代码）
> 配套: [26-refactor-roadmap.md](26-refactor-roadmap.md) · 规则: [../spec/README.md](../spec/README.md)
> 上承: [23-architecture-redesign-proposal.md](23-architecture-redesign-proposal.md) / [24-workflow-specification.md](24-workflow-specification.md)

---

## 〇、本文档定位

这份文档是架构决策的**说明性文档**，解释每条规则的背景、取代关系与设计意图。
**硬约束以 `spec/` 目录为准**，本文是其展开说明。

代码审查发现的根本问题：一个**隐藏的静态服务定位层**（`GenericDataGridHelper`
+ `ReferenceResolver.Instance` + `BrowserIndexService`）持有应用真实状态，互相循环依赖，
并通过 `App.ServiceProvider` 反向抓取 DI 服务。`IReferenceResolver` / `INavigationRouter`
等抽象虽已存在却被 141 处 `.Instance` 调用绕过。memory 记录的 4 个未解决功能 bug
（索引为空、store 指错、Ctrl 导航失效、KV 切换延迟）本质都是**没人真正拥有当前状态**。

---

## 一、规则总表

| 规则 | 一句话 | spec 文件 |
|------|--------|-----------|
| **R01** | 状态唯一所有者 `IWorkspaceSession` | [R01](../spec/R01-state-single-owner.md) |
| **R02** | 单活跃 Session，切 Profile = 重建 | [R02](../spec/R02-single-active-session.md) |
| **R03** | 引用解析只走注入的 `IReferenceResolver` | [R03](../spec/R03-reference-resolver-injected.md) |
| **R04** | View 只组装控件 | [R04](../spec/R04-view-assembles-only.md) |
| **R05** | 消息只做跨区域 UI 联动 | [R05](../spec/R05-messages-ui-only.md) |
| **R06** | 四区域数据同源（同一 `IEntity` 实例） | [R06](../spec/R06-same-entity-instance.md) |
| **R07** | 单向分层 Domain→Core→ViewModels→Views | [R07](../spec/R07-one-way-layering.md) |
| **R08** | 编辑入口仅 KV 与 XML | [R08](../spec/R08-edit-entry-points.md) |
| **R09** | 脏数据视觉指示：Sidebar + HomePage 提示未保存编辑 | [R09](../spec/R09-session-dirty-guard.md) |
| **R10** | 索引手动刷新，编辑后标过期不自动重建 | [R10](../spec/R10-index-manual-refresh.md) |
| **R11** | 文档独立保存 + 工具栏「Save Session」全局保存 | [R11](../spec/R11-save-granularity.md) |
| **R12** | 选中由 `ISelectionService` 管理，以 Center 焦点为主 | [R12](../spec/R12-selection-service.md) |
| **R13** | `VisHelper` 为 `internal static` 单一辅助类 | [R13](../spec/R13-vishelper-internal.md) |
| **R14** | 分层用文件夹+命名空间约定，不拆程序集 | [R14](../spec/R14-folder-convention-layering.md) |
| **R15** | DataTable 交互矩阵 | [R15](../spec/R15-datatable-interaction.md) |
| **N01** | 禁止静态可变状态 | [N01](../spec/N01-no-static-state.md) |
| **N02** | 禁止 `ReferenceResolver.Instance` | [N02](../spec/N02-no-reference-resolver-instance.md) |
| **N03** | 禁止 View 写业务/导航逻辑 | [N03](../spec/N03-no-logic-in-view.md) |
| **N04** | 禁止死消息与多接收方歧义 | [N04](../spec/N04-no-dead-messages.md) |
| **N05** | 禁止 Bottom DataTable 原地编辑 | [N05](../spec/N05-no-bottom-editing.md) |

> 本轮范围：**纯结构重构**。4 个功能 bug 不在本轮修复目标内，但 R01 落地后其根因消失，
> 留待重构稳定后单独验证。

---

## 二、单向分层与模块边界（R07 / R14）

```
┌─────────────────────────────────────────────┐
│ Views (Avalonia axaml + code-behind)         │  只依赖 ViewModels
│   纯控件组装；EntityVisualizers 产控件+声明引用 │  无业务逻辑、无静态全局
├─────────────────────────────────────────────┤
│ ViewModels / App                             │  依赖 Core
│   通过 DI 注入服务 + 消息做跨区域联动           │  不再 new、不读 App.ServiceProvider
├─────────────────────────────────────────────┤
│ Core / Services                              │  依赖 Domain
│   IWorkspaceSession · IReferenceResolver      │  状态唯一所有者层
│   索引 · 合并 · 导出 · ModManager             │
├─────────────────────────────────────────────┤
│ Domain (实体模型 / 枚举)                       │  无依赖
└─────────────────────────────────────────────┘
```

落地方式：文件夹+命名空间约定（R14），配合审查把关依赖方向；不拆多程序集。

**铁律（R07/N01/N03）**：依赖只能向下；Views 不得触 Core 静态；Core 不得引用 Avalonia 控件。

---

## 三、状态唯一所有者：IWorkspaceSession（R01 / R02 / R03）

### 3.1 取代关系

| 删除（旧静态） | 取代为（注入） | 规则 |
|---------------|---------------|------|
| `GenericDataGridHelper.ActiveMergeStore` / `BrowserStore` / `SetActiveStores()` | `IWorkspaceSession.Store` | N01 |
| `ReferenceResolver.Instance` | DI 注入的 `IReferenceResolver` | N02 / R03 |
| `BrowserIndexService`（全静态） | `IWorkspaceSession.ForwardIndex` / `ReverseIndex` | N01 |
| `App.ServiceProvider` 反向抓取（~90 处） | 构造函数注入 | N01 |

### 3.2 职责（概念契约，非最终签名）

```
IWorkspaceSession   (DI scoped，单活跃实例)
  ├─ Store              当前 Profile/Mod 的 EntityMergeStore
  ├─ ForwardIndex       正向引用索引（手动刷新，R10）
  ├─ ReverseIndex       反向引用索引（手动刷新，R10）
  ├─ DirtyEntities      脏实体集合（R09 / R11）
  ├─ OpenAsync(profile) 重建会话；旧 store 释放（R02）
  └─ CloseAsync()       清空 → 触发 SessionStateChanged
```

切 Profile/Mod 时 WAL 按 Mod 隔离持久化，无需拦截弹窗；脏数据通过 Sidebar/HomePage
视觉指示器告知用户（R09）。

### 3.3 循环依赖消除

旧：`GenericDataGridHelper ↔ ReferenceResolver.Instance`（互相读对方静态）。
新：两者都注入 `IWorkspaceSession`，彼此不再相互引用，依赖图变为单向树。

---

## 四、引用与导航职责分离（R04 / N03）

`EntityVisualizers.cs`（8761 行）混了三层：建 Avalonia 控件 + 66 处导航逻辑 + 引用查询。
重构后职责切分：

| 职责 | 归属 | 规则 |
|------|------|------|
| 产出控件 | Visualizer（View 层）仅 `BuildDetail` / `BuildOverview` | R04 |
| 声明「这里有个指向 X 的引用」 | `RefNode`/`NavLeaf` 工厂，不直接调静态 | R04 |
| 解析引用目标 | 注入的 `IReferenceResolver`（`LookupRef` / `ReverseLookup`） | R03 |
| 点击后导航/Peek | 注入的 `INavigationRouter`（Ctrl+LMB→Navigate；Ctrl+RMB→Peek） | R04 |

拆分路径（路线图 M2）：`VisHelper` 提为独立文件，`internal static`（R13）→ 每实体类型一文件
→ 66 处 `NavigateTo` 收敛到工厂内注入解析器。Registry 显式注册无需改动。

---

## 五、消息系统准则（R05 / N04）

**准则**：
- 跨区域 UI 联动（开标签页、切 KV 焦点、刷新覆盖链）→ **消息**
- 读数据 / 读状态 → **注入服务直调**，不发消息
- 每条消息单一接收方；多发送方允许，多接收方需明确理由（N04）

**M0 产出：消息清单冻结**
- `EntitySelectedMessage`（现 4 发送方）→ 收敛到 `ISelectionService`（R12）统一发送
- 删除无接收方死消息：`FontSizeChangedMessage` / `ColumnVisibilityChangedMessage` /
  `GridRowHeightChangedMessage` → 改为注入 `IConfigService` 直调
- `DocumentWorkspaceViewModel` 15 种消息注册 → 明确每条所有权

---

## 六、四区域数据同源与选中机制（R06 / R08 / R12 / R15）

### 6.1 同源实例

```
Center 文档 / Left KV / Bottom DataTable 高亮行
  → 三者绑定同一个 IEntity 实例（R06）
  → 一处修改 → INotifyPropertyChanged → 各区域自动刷新
```

编辑入口仅两个：Left KV、Center XML Tab（R08 / N05）。

### 6.2 当前实体判定（R12）

- **当前实体 = 最后获焦的 Center 文档实体**（GotFocus 时间戳最新者）
- Center 无文档时当前实体为空；Left KV / Peek 显示空态
- 由注入的 `ISelectionService` 统一持有并发 `EntitySelectedMessage`

### 6.3 DataTable 控件层级说明

Bottom DataTable 区域实际渲染链路：

```
DataTableTool (Dock Tool)
  └─ Context = ModDataToolViewModel
        └─ DataTemplate → ModGameDataTabsView  (Views/UserControls/ModGameDataTabsView.axaml)
              └─ SearchableDataGrid            (Views/UserControls/SearchableDataGrid.axaml)
                    └─ DataGrid  ← 实际渲染的行列表，GDH 在此挂 Tunnel handler
```

- **`SearchableDataGrid`**：通用可重用的带搜索框 DataGrid 控件，用于 Bottom DataTable、合并视图等多处。`SelectionMode="Single"`（只读浏览场景），GDH 在列配置时挂 Tunnel handler 处理引用跳转。
- **`DataTableView`**：历史遗留孤立控件，绑定 `SessionDataGridViewModel`，目前**未被任何地方引用**（dead code）。不参与实际运行链路，应在后续清理中删除。
- **`ModGameDataTabsView`**：实际把 Mod/Profile 数据组织成多 Tab 的容器，Tab 之间共享一个 `SearchableDataGrid` 实例（`SharedDataGrid`），切 Tab 时只换 `ItemsSource`，控件不重建。

### 6.4 DataTable 交互矩阵（R15）

DataTable 以浏览为主，单击**不**改当前实体、不开标签页。
`SearchableDataGrid` 使用 `SelectionMode="Single"` 以避免 Ctrl 多选与自定义导航冲突：

| 操作 | 点在数据项行 | 点在引用单元格 |
|------|-------------|--------------|
| 单击 | 行高亮（仅浏览，不改当前实体） | 行高亮 |
| 双击 | Center 打开/跳转该实体标签页 | — |
| Ctrl+LMB | Center 打开该实体标签页（Navigate） | 跳转引用目标 |
| Ctrl+RMB | Peek 该数据项 → Right 面板 | Peek 引用目标 |
| Shift | 无特殊行为（留白） | 无特殊行为 |
| 右键 | 无上下文菜单（已屏蔽） | 无上下文菜单 |

> Ctrl/Shift 在 `Single` 模式下无 DataGrid 内置语义，可完全自由分配给导航/Peek。
> 若未来需要批量操作需切 `Extended` 模式，届时需将 Navigate/Peek 改为其他修饰键（如 Alt）。

### 6.5 引用列优先级实现

DataTable 中引用单元格的 Ctrl+LMB/Ctrl+RMB 通过 Avalonia **路由事件阶段**保证优先级：

```
每次点击事件路由（Tunnel → Bubble）：
  ① SearchableDataGrid.UserControl Tunnel  handler  ← 最早，重置+提前设抑制标志
  ② DataGrid.OnPointerPressed  (内部Tunnel)          ← 更新选中行
  ③ OnDataGridSelectionChanged                        ← 检查标志，抑制 EntitySelectedMessage
  ④ GDH ConfigureColumn Tunnel  (引用单元格)           ← 设标志 + Navigate/Peek 引用目标
  ⑤ SearchableDataGrid Bubble  handler                ← 标志未设时，行级 Navigate/Peek
```

**为何需要 UserControl 级 Tunnel handler（①）**：
- DataGrid 内部 `OnPointerPressed`（②）与 GDH ConfigureColumn handler（④）同为 Tunnel 阶段
- Avalonia 路由按**视觉树深度**决定同阶段执行顺序：父控件先于子控件
- 若不设①，②（DataGrid内部）先于④（单元格）执行 → `SelectionChanged` 先发
  `EntitySelectedMessage` → ④来不及设抑制标志
- 在 UserControl（DataGrid 的父级）上注册 Tunnel handler，确保在②之前设
  `SuppressNextSelectionChanged=true`

**标志生命周期（防残留）**：
- ① 每次点击**必须先重置** `SuppressNextSelectionChanged=false`
- 若用户先 Ctrl+LMB 引用列（④设标志=true，未被重置），再 Ctrl+RMB 数据行，
  ⑤ Bubble handler 看到残留的 `true` 直接 return，行级 Peek 失效
- 因此①对**所有点击**（含非 Ctrl）都执行重置，确保残留状态不跨点击周期传播

---

## 七、保存与索引（R09 / R10 / R11）

**保存粒度（R11）**：默认单文档保存（每个 EntityEditorDocument 只提交自身实体）。
WAL 按 Mod 隔离持久化后，切 Profile 不再拦截保存；用户通过 Sidebar/HomePage 脏数据
视觉指示器了解未保存状态，主动保存（R09 修订）。

**索引刷新（R10）**：Ref Index / Reverse Index 初始为空，仅手动刷新构建。
编辑实体后标「已过期」角标提示，但不自动重建（避免卡顿）。

---

## 八、UI 原型（控件级）

### 8.1 顶部工具栏（Workspace 级，常驻）
```
┌─────────────────────────────────────────────────────────────┐
│ [＋New] [⧉Copy] [🗑Delete] │ [💾Save●] │ Profile: ▾vanilla+mod │
│                            │          │ [Left][Right][Bottom] │
└─────────────────────────────────────────────────────────────┘
  Save● = 当前文档有脏标记时显示（R11）
```

### 8.2 Left — KV 编辑器（跟最后焦点实体，R12）
```
┌── ItemType #42  "Knife" ──────────────┐
│ ▼ 基础           (Section 可折叠)       │
│   strName     [ Knife            ]     │  TextBox
│   nNameID     [ 1024        ] ▲▼       │  Numeric
│ ▼ 战斗                                  │
│   fAttack     [ 3.5         ] ▲▼       │
│   aAttacks    [Slash] [Stab] ⟲引用     │  引用徽章 Ctrl+LMB跳/Ctrl+RMB Peek
│ ▼ 覆盖链 OverlayChain                    │
│   vanilla → mod_a(改 fAttack) → 当前     │
└────────────────────────────────────────┘
```

### 8.3 Center — 双 Tab Document（可多开/分屏，最大面积）
```
┌[Visual] [XML]──────────────── ItemType #42 ─┐
│ ╔═ Knife ════════════════[img][img×2]═══╗   │
│ ║ 攻击 3.5  防御 1.0  重量 0.4kg         ║   │
│ ║ properties: ⟦Sharp⟧ ⟦Metal⟧  ←可点击  ║   │
│ ╚════════════════════════════════════════╝   │
│  (XML Tab: AvaloniaEdit 显示/编辑 XML 片段)   │
└───────────────────────────────────────────────┘
```

### 8.4 Right — Peek 面板（Ctrl+RMB 固定预览，不夺焦）
```
┌── Peek ─────────────────[📌Pin][↗Open][✕]─┐
│ 面包屑: ItemType#42 › ItemProp "Sharp"     │  低调样式
│ ┌────────────────────────────────────────┐ │
│ │ ItemProp "Sharp"  (BuildOverview 只读)   │ │
│ └────────────────────────────────────────┘ │
└──────────────────────────────────────────────┘
```

### 8.5 Bottom — 三表（Scope 唯一，R15）
```
┌[DataTable][Ref Index ⟲][Reverse Index ⟲]──────────┐
│ id  │ strName │ fAttack │ ...     [🔍搜索______]    │
│ 42  │ Knife   │ 3.5     │  ← 单击高亮(浏览)
│ 43  │ Axe     │ 5.0     │  ← 双击/Ctrl+LMB 开 Center 标签页
│ (Ref/Reverse 初始空，⟲ 手动刷新，编辑后显示「已过期」R10)  │
└──────────────────────────────────────────────────────┘
```

---

## 九、决策一致性自检

| 风险 | 兜底规则 |
|------|---------|
| 后续又写静态全局 | R01 唯一所有者 + R07 单向分层 + N01 禁止 |
| View 里再混业务逻辑 | R04 + N03 |
| 消息再次扩散失控 | R05 + N04 |
| 数据不一致 | R06 同源实例 |
| 多 Profile 索引混乱 | R02 单活跃 Session |
| 意外触发 Bottom 编辑 | R08 + N05 |
| DataTable 交互行为分歧 | R15 交互矩阵 |
| 切 Profile 丢失编辑 | R09 Session 脏状态拦截 |
| 索引卡顿 | R10 手动刷新 |
| 误存其他文档编辑 | R11 文档独立保存 |
