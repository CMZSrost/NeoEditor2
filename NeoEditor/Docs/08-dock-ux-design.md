# NeoEditor Dock 面板 UX 设计

> 设计日期：2026-05-30  
> 核心问题：Dock.Avalonia 是编辑器最强的基础设施，但当前只用了一个 `DocumentDock`——相当于买了全套工具箱只用了锤子

---

## 1. 现状 vs 能力

### 当前使用

```
RootDock
  └── ProportionalDock
       └── DocumentDock (唯一的文档标签页区)
```

- 只有 `DocumentDock`，没有 `ToolDock`
- 没有 `ITool` 实现
- 没有 `Factory` 自定义（直接用包里的默认 Factory）
- Layout 持久化库已引用但从未调用

### Dock.Avalonia 实际能力

| 功能 | 能力 | 当前 |
|------|------|:----:|
| **DocumentDock** | 中心文档区，标签页/拖拽排序/浮动 | ✅ 在用 |
| **ToolDock** | 侧边/底部工具面板，可 Pin/Float/AutoHide | ❌ 没用 |
| **ProportionalDock** | 分屏对比，拖拽分割 | ❌ 没用 |
| **RootDock** | 多区域嵌套，窗口级布局 | 🟡 有 Root 但扁平 |
| **Factory** | 自定义布局创建逻辑 | ❌ 默认 |
| **Layout 持久化** | 保存/恢复用户自定义布局 | ❌ 未调用 |
| **浮动窗口** | 拖出标签页变独立窗口 | 🟡 刚修好 |
| **Pin/AutoHide** | 工具面板贴边折叠 | ❌ 没用 |

---

## 2. 目标布局

```
┌──────────────────────────────────────────────────────────┐
│ 工具栏（动态上下文）                                       │
├────────┬──────────────────────────────┬──────────────────┤
│ Tool   │  DocumentDock                 │ Tool             │
│ Dock   │  ┌────────────────────────┐  │ Dock             │
│ (左)   │  │ [ItemType] [Recipe]    │  │ (右)             │
│        │  │                        │  │                  │
│ 覆盖链  │  │     DataGrid           │  │  📷 Image        │
│ 导航历史│  │                        │  │  Preview         │
│        │  │                        │  │                  │
│        │  │                        │  │  🔍 Reference    │
│        │  └────────────────────────┘  │  Inspector       │
│        │  (可拖拽标签页分屏：          │                  │
│        │   左 Recipe / 右 ItemType)   │                  │
├────────┴──────────────────────────────┴──────────────────┤
│ Tool Dock (底部)                                          │
│ ┌────────────┬──────────────┬───────────────────────────┐ │
│ │ 🔍 Search  │ ⚠ Conflicts │ ✅ Validation             │ │
│ │  Results   │   (42)       │  Report                   │ │
│ └────────────┴──────────────┴───────────────────────────┘ │
├──────────────────────────────────────────────────────────┤
│ 状态栏: Mod: MyMod · 15 entities · Saved 2m ago          │
└──────────────────────────────────────────────────────────┘
```

---

## 3. 六个工具面板

### 3.1 Reference Inspector（右侧工具面板）★ 核心差异化功能

**解决的问题**：当前 Ctrl+Click 引用会**跳转离开**当前编辑位置。对于"看一眼目标属性就回来"的场景，跳转太重了。

**新行为**：
- **Ctrl+Click** → 在 Reference Inspector 中打开目标（临时预览）
- **双击 / 右键→跳转** → 在 DocumentDock 中打开目标（完整导航）

**Inspector 显示内容**：
- 目标实体的所有属性和字段值（只读）
- 目标实体的 Subject 名称
- "Open in Document" 按钮 → 将当前预览提升为正式文档
- "Pin" 按钮 → 锁定当前 Inspector 内容，不随下一次 Ctrl+Click 改变
- 引用链：如果目标也有引用，可以继续 Ctrl+Click 深入

**实现**：
```
ReferenceInspectorTool : ITool
  ├── EntityType
  ├── CurrentEntity (IEntity?)
  ├── IsPinned (bool)
  └── OnNavigateRequest (event → 更新 CurrentEntity)
```

### 3.2 Image Preview（右侧工具面板）

**解决的问题**：ItemType 编辑器的 SpriteShow/WearShow 是 Tab 内的控件，查看图片需要切换 Tab。图片应该是一个始终可见的独立面板。

**行为**：
- 选中 DataGrid 行时，自动检测 `strImg` / `vSpriteList` / `vImageList` 字段
- 有图片引用 → 加载显示缩略图 / 全身叠加预览
- 无图片引用 → 显示 "No image" 占位
- 支持缩放/平移（复用 `ZoomableImageView`）
- 与 Reference Inspector 共享右侧 ToolDock（Tab 切换）

### 3.3 Overlay Chain（左侧工具面板）

**解决的问题**：当前覆盖链是 `RowDetails`（选中行下方展开），每次展开/折叠都需要鼠标操作，且滚动会丢失位置。

**新行为**：
- 合并视图打开时，左侧自动出现 Overlay Chain 面板
- 选中 DataGrid 行 → 显示该实体的覆盖链：各 Mod 中的版本、来源、胜者/败者
- 链节点可点击 → 跳转到对应 Mod 的该实体（在 DocumentDock 中打开）
- 单 Mod 视图时自动隐藏（无覆盖链可显示）

**实现**：
```
OverlayChainTool : ITool
  ├── CurrentEntityId (string)
  ├── ChainEntries (ObservableCollection<OverlayChainEntry>)
  └── 选中行联动 (通过 DataGrid SelectionChanged)
```

### 3.4 Search Results（底部工具面板）

**解决的问题**：当前搜索结果在侧栏中显示，每次要看结果必须切换侧栏面板，挡住其他侧栏内容。

**新行为**：
- 全局搜索结果在底部面板中显示（与侧栏 Search 面板互补：侧栏输入查询条件，底部显示结果）
- 结果按实体类型分组
- 双击结果 → 在主 DataGrid 中定位到对应行
- 结果可筛选（按实体类型过滤）
- "Clear Results" 按钮

### 3.5 Conflicts（底部工具面板）

**解决的问题**：冲突指示器只能显示总数，要看具体内容需要点按钮弹 MessageBox——一次性操作，无法持续对比。

**新行为**：
- 底部面板的一个 Tab，实时列出所有冲突
- 三列：Entity | Field | Conflicting Mods
- 双击冲突行 → 跳转到对应实体
- 右键冲突行 → "Adopt Mod A value" / "Adopt Mod B value"
- 冲突数实时更新在 Tab 头上：`⚠ Conflicts (42)`

### 3.6 Validation Report（底部工具面板）

**解决的问题**：数据验证框架代码已存在但未接入。保存后如果有警告，需要持续可见的列表。

**新行为**：
- 保存后自动运行验证（如有错误/警告）
- 底部面板 Tab 显示：`✅ Validation (3 warnings)`
- 三列：Severity | Entity | Field | Message
- 双击跳转到问题字段
- Warning 不阻止保存，Error 阻止

---

## 4. 分屏对比（DocumentDock 原生能力）

Dock.Avalonia 的 `DocumentDock` 原生支持拖拽分屏：将一个 Tab 拖到另一个 Tab 的边缘 → 自动创建 `ProportionalDock` 分屏。

**场景 1：编辑 Recipe 时查看 Ingredient**
- 主 Tab：Recipe DataGrid
- 分屏 Tab：Ingredient DataGrid
- 两边独立滚动、独立搜索

**场景 2：对比两个 Mod 的同一张表**
- 左：Mod A 的 ItemType
- 右：Mod B 的 ItemType
- 并排对比差异

**场景 3：文本编辑 + 数据预览**
- 左：XML 文本编辑器（`AvaloniaEdit`）
- 右：解析后的 DataGrid（实时预览）

当前 `DockControl` 已支持此功能（`EnableWindowDrag="False"` 只禁用了窗口拖拽，标签页内部拖拽仍然可用），但因为没有第二个 DocumentDock 区域，分屏效果不明显。通过添加 `ProportionalDock` 嵌套，用户可以将文档区域分割为上下/左右两个子区域。

---

## 5. 交互重设计

### 5.1 引用导航改为 "Peek + Navigate" 双层

| 操作 | 当前行为 | 新行为 |
|------|---------|--------|
| Ctrl+Click 引用 | 跳转到目标（替换当前 Tab） | **Peek**：在 Reference Inspector 中预览目标 |
| 双击引用 | （无） | **Navigate**：在 DocumentDock 中打开目标 Tab |
| 右键 → "Go to Reference" | 跳转到目标 | **Navigate**：在 DocumentDock 中打开目标 Tab |
| Ctrl+Hover | 显示 Tooltip | 不变：显示 `EntityType: Subject (id=N)` |

**优势**：查看引用不中断当前编辑流。需要深入编辑时才做完整导航。

### 5.2 图片预览自动联动

| 触发条件 | 行为 |
|---------|------|
| DataGrid 行选中 | 自动加载该实体的图片到 Image Preview 面板 |
| 切换到不同实体类型 Tab | 如有图片字段，自动显示；无则显示占位 |
| 双击图片 | 在 DocumentDock 中打开全尺寸 ImageDocument |

### 5.3 工具面板生命周期

| 面板 | 何时显示 | 何时隐藏 |
|------|---------|---------|
| Reference Inspector | 始终可见（右 ToolDock），无内容时显示 "Ctrl+Click a reference to inspect" | — |
| Image Preview | 始终可见（右 ToolDock），无图片时显示占位 | — |
| Overlay Chain | 合并视图打开时自动出现 | 合并视图关闭时自动折叠 |
| Search Results | 首次搜索时自动出现 | 可手动关闭 |
| Conflicts | 合并视图 + 冲突 > 0 时自动出现 | 冲突解决完毕可关闭 |
| Validation | 保存后自动出现（如有 Warning/Error） | 可手动关闭 |

---

## 6. 布局持久化

当前 Dock.Serializer 已注册但从未调用。

**实现**：
- **保存**：应用关闭时 `DockSerializer.Serialize()` → 写入 `dock_layout.json`
- **恢复**：应用启动时从 `dock_layout.json` 反序列化 → 恢复上次布局
- **重置**：Settings 面板提供 "Reset Layout" 按钮

这样用户自定义的布局（面板位置、分屏比例、哪些面板打开/关闭）在重启后保持不变。

---

## 7. 实施路线

### Phase A：基础设施（Dock 重构）

| # | 项目 | 说明 |
|---|------|------|
| A1 | **引入 ITool 基类** | 新建 `ToolBase : ITool`，提供 Id/Title/Context 等基础属性 |
| A2 | **重写 Dock 布局** | AXAML 改为 RootDock → ProportionalDock → [ToolDock Left, Split(ProportionalDock → DocumentDock), ToolDock Right, ToolDock Bottom] |
| A3 | **自定义 Factory** | `NeoEditorFactory : Factory`，重写 `CreateLayout()` 创建带 ToolDock 的初始布局 |
| A4 | **布局持久化** | 启动时 LoadLayout / 关闭时 SaveLayout |

### Phase B：工具面板实现

| # | 项目 | 说明 |
|---|------|------|
| B1 | **Overlay Chain Tool** | 从 RowDetails 迁移到左侧 ToolDock |
| B2 | **Image Preview Tool** | 新建右侧工具面板，选中行联动加载图片 |
| B3 | **Reference Inspector Tool** | 新建右侧工具面板，Ctrl+Click 预览目标 |
| B4 | **Search Results Tool** | 从侧栏迁移到底部面板 |
| B5 | **Conflicts Tool** | 从 MessageBox 迁移到底部面板，支持列表交互 |
| B6 | **Validation Tool** | 新建底部面板 Tab，保存后报告验证结果 |

### Phase C：交互优化

| # | 项目 | 说明 |
|---|------|------|
| C1 | **引用 Peek vs Navigate** | 改造 Ctrl+Click → Peek，双击/右键 → Navigate |
| C2 | **DataGrid 选中行 → 工具面板联动** | 统一 SelectionChanged → 更新 Image Preview / Overlay Chain / Reference Inspector |
| C3 | **DocumentDock 分屏引导** | 首次使用时提示 "拖拽标签页到边缘可分屏对比" |

---

## 8. 关键设计决策

### 8.1 为什么不用弹出窗口（Popup/Flyout）

Dock 面板优于弹出窗口：
- **持久可见**：不遮挡 DataGrid，不随焦点丢失关闭
- **可调整大小**：用户可拖拽分割条调整面板比例
- **可 Pin/Float/AutoHide**：用户自行决定面板的驻留方式
- **布局可保存**：用户自定义的排列在重启后保留

### 8.2 为什么 Reference Inspector 是 "Peek" 而非弹窗

当前 Ctrl+Click 直接跳转 = 离开编辑上下文。Inspector 中 Peek 让用户"看一眼就回来"，不打断工作流。需要深入编辑时才做完整导航。

### 8.3 为什么图片放在右侧而非编辑器内 Tab

ItemTypeEditor 里的 SpriteShow/WearShow Tab 是编辑器 Tab 内的子标签页——查看图片需要先切换到 ItemType Tab，再切换到 SpriteShow 子标签页。独立 Image Preview 面板始终可见，选中任何实体都能立即看到图片，不需要切换标签页。

---

## 实施状态 (2026-06-07)

**架构变更**: 已完成 Dock.Avalonia ToolDock 集成。DocumentWorkspaceView 使用嵌套 ProportionalDock + ToolDock 实现四区域布局，替代早期的 Grid + Border + GridSplitter 方案。

### 当前布局结构

```
RootDock
  └── ProportionalDock (Vertical)
       ├── ProportionalDock (Horizontal)  Proportion=3
       │    ├── ToolDock (左, Id=LeftToolPane)   Proportion=1
       │    │    └── Tool: OverlayChain (CanClose=False)
       │    ├── ProportionalDockSplitter
       │    ├── DocumentDock (中)                 Proportion=4
       │    ├── ProportionalDockSplitter
       │    └── ToolDock (右, Id=RightToolPane)  Proportion=2
       │         ├── Tool: ValueEditor    (CanClose=False)
       │         ├── Tool: ImagePreview   (CanClose=False)
       │         └── Tool: RefInspector   (CanClose=False)
       ├── ProportionalDockSplitter
       └── ToolDock (底, Id=BottomToolPane)  Proportion=1
            ├── Tool: SearchResults  (CanClose=False)
            ├── Tool: Conflicts      (CanClose=False)
            └── Tool: Validation     (CanClose=False)
```

### 比例配置

| 区域 | Proportion | 占比 | 说明 |
|------|:----------:|:----:|------|
| 左侧 | 1 | ~14% | OverlayChain 覆盖链 |
| 中间 | 4 | ~57% | DocumentDock 文档标签页 |
| 右侧 | 2 | ~29% | 3 个工具标签页 |
| 底部 | 1 | 25% 高度 | 3 个工具标签页，全宽 |

### Tool 子类定义 (Documents.cs)

| Tool 子类 | Id | 位置 | 内容控件 |
|----------|-----|------|----------|
| OverlayChainTool | OverlayChain | 左 | OverlayChainToolView |
| ValueEditorTool | ValueEditor | 右 | ValueEditorPanel |
| ImagePreviewTool | ImagePreview | 右 | ImagePreviewView |
| ReferenceInspectorTool | RefInspector | 右 | ReferenceInspectorView |
| SearchResultsTool | SearchResults | 底 | SearchResultsView |
| ConflictsTool | Conflicts | 底 | ConflictsView |
| ValidationTool | Validation | 底 | ValidationView |

### 面板实现状态

| 面板 | 位置 | 状态 |
|------|------|:--:|
| 覆盖链 | 左 ToolDock | ✅ OverlayChainToolView |
| 可视化编辑器 | 右 ToolDock (Tab 1) | ✅ ValueEditorPanel + VisualEditorRequestedMessage 自注册 |
| 图片预览 | 右 ToolDock (Tab 2) | ✅ ImagePreviewView |
| 引用检查 | 右 ToolDock (Tab 3) | ✅ ReferenceInspectorView |
| Search | 底 ToolDock (Tab 1) | ✅ SearchResultsView (独立拆分) |
| Conflicts | 底 ToolDock (Tab 2) | ✅ ConflictsView (独立拆分) |
| Validation | 底 ToolDock (Tab 3) | ✅ ValidationView (独立拆分) |

| Phase | 状态 |
|:------|:--:|
| A (基础设施) | ✅ ToolDock 方案 |
| B (6 工具面板) | ✅ 7/7 完成（全部 CanClose=False） |
| C (Peek + Navigate) | 🔜 未开始 |

**完成度: 50%**
