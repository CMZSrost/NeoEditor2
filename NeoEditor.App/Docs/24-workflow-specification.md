# NeoEditor 工作流规格说明

> v2.2 · 2026-06-25
> 对应架构: [23-architecture-redesign-proposal.md](23-architecture-redesign-proposal.md)

---

## 一、页面切换规则

```
┌─────────────────┐     Session 活跃      ┌─────────────────┐
│   WelcomePage   │ ──────────────────→  │  WorkspacePage   │
│   (首页)        │ ←──────────────────  │  (工作页)         │
└────────┬────────┘    Session 清空       └────────┬────────┘
         │                                         │
         │   点击 Settings                          │ 点击 Settings / Back
         │                                         │
         ▼                                         ▼
┌─────────────────────────────────────────────────────┐
│                  SettingsPage                        │
│                  (配置页)                             │
│   Back → WorkspacePage (有 Session) 或 WelcomePage   │
└─────────────────────────────────────────────────────┘
```

| 触发条件 | 目标页面 |
|----------|----------|
| Session 创建（打开 Mod / 打开 Profile / 打开 DataBrowser） | WorkspacePage |
| Session 清空（所有文档关闭 + 无活跃 Mod/Profile） | WelcomePage |
| 侧栏 / 顶栏 Settings 按钮 | SettingsPage |
| Settings Back 按钮 | WorkspacePage（有 Session）或 Home |

**消息流**：`DocumentWorkspaceViewModel` 在 Session 创建/销毁时发送 `SessionStateChangedMessage`，`MainWindowViewModel` 据此切换 `CurrentPage`。

---

## 二、侧边栏导航

7 个按钮三组分隔，通过 `TogglePane` + SplitView 弹出面板：

```
Group 1 (导航):
  Home          → CloseAllDocuments → Session 清空 → WelcomePage

Group 2 (工作区面板):
  Data Browser  → TogglePane("DataBrowser")    领域树面板
  Mod Database  → TogglePane("ModDatabase")    Mod 导入与列表
  Profiles      → TogglePane("Profiles")       getmods.php 配置
  Explorer      → TogglePane("Explorer")       文件浏览器
  References    → ToggleRightPanel             切换 Peek 面板

Group 3 (系统):
  Settings      → NavigateToPage(Settings)
```

---

## 三、完整工作流

### 3.1 浏览游戏数据

```
HomePage "Browse Game Data"
  → OpenDataBrowserMessage
  → SessionStateChangedMessage(isActive:true)
  → 页面切换到 Workspace
  → 侧栏 Data Browser → SplitView 显示领域树
  → 点击领域展开 → 点击实体类型 → Center 创建 EntityBrowserDocument
  → 点击左侧实体列表 → EntityViewerDocument 显示 BuildDetail
```

### 3.2 新建 Mod

```
HomePage "New Mod" / 工具栏 [New Mod]
  → CreateModDialog（名称 / 路径 / 命名空间）
  → ImportMod → 创建 ModInfo
  → OpenModGameDataDocumentMessage
  → ModDataToolVm.SetMod(info) → Bottom DataTable 加载
  → Center 显示 SessionWelcomeDocument
  → 页面切换到 Workspace
```

### 3.3 导入已有 Mod

```
HomePage "Import Mod" / 工具栏 [Import Mod] / 拖放
  → FolderPicker 选择 Mod 目录
  → ModManager.ImportModAsync
  → 同上 New Mod 的打开流程
```

### 3.4 打开 Recent Mod

```
HomePage RecentMods 列表 或 双击 Profile
  → 加载 ModInfo / ProfileInfo
  → OpenModGameDataDocumentMessage / OpenMergeEditorMessage
  → Bottom DataTable 加载对应数据
  → Center 显示 SessionWelcomeDocument
  → 页面切换到 Workspace
```

### 3.5 编辑实体

```
用户在 Bottom DataTable 选中一行（不可原地编辑）
  → EntitySelectedMessage(entity, BottomDataGrid)
  → Center: EntityEditorDocument 创建/更新
      ├ Tab1 Visual: BuildDetail(entity) 渲染卡片
      └ Tab2 XML: EntityXmlHelper.GenerateXmlFragment(entity)
  → Left KV: 加载实体字段 (FieldGroupMetadata 分组)
  → Right Peek: 不受影响（除非 Pin 状态与此实体相关则更新）
  → Bottom: DataTable 高亮该行（不触发索引刷新）

用户编辑：
  方式 A: 在 Left KV 修改字段 → Enter/失焦 → 写内存 → Dirty → 四区域同步
  方式 B: 在 Center XML Tab 编辑 → ApplyXmlToEntity → 写内存 → Dirty → 四区域同步

用户保存：Ctrl+S → DB 写入 → XML 导出 → 清除 Dirty
```

---

## 四、多实体同时编辑

```
CurrentPage = Workspace
Bottom DataTable 显示 ItemType 全表

用户操作序列：
  1. 双击行 #42 → Center 创建 EntityEditorDocument "ItemType #42"
  2. 双击行 #15 → Center 创建 EntityEditorDocument "ItemType #15"
  3. 双击行 #8  → Center 创建 EntityEditorDocument "ItemType #8"

现在 Center DocumentDock 有三个标签页。

  4. 用户点击标签页 "ItemType #15"
     → Left KV 自动切换绑定到 ItemType #15
     → 用户在 KV 修改 strName
     → 只影响 #15 的实例

  5. 用户拖拽标签页 "ItemType #8" 到右侧分屏
     → 并排显示 #8 和 #15 的可视化
     → 用户点击 #8 的可视化区域
     → Left KV 切换到 ItemType #8
```

**KV 焦点跟随规则**：
- EntityEditorDocument 获得焦点 → KV 切换
- Bottom DataTable 选中行 → KV 切换
- KV 绑定 = 最后一次获得焦点的 IEntity

---

## 五、Peek 引用工作流

### 5.1 Ctrl+RMB Peek（固定预览）

```
用户在 Center 可视化 / Bottom DataTable / Left KV 中
  Ctrl+右键 点击引用徽章或单元格
    → ReferenceResolver 解析引用目标
    → PeekPanel.Peek(targetEntity)
    → Right Peek 面板固定显示 BuildOverview(targetEntity)
    → 面包屑: [源实体类型] → [目标实体类型]

用户松手后 Peek 内容保持不动。
用户可以继续在主视区操作（滚动、切换标签页）。
Peek 面板不跟随主视区焦点变化。

再次 Ctrl+RMB 另一个引用
  → 面包屑追加: [前目标] → [新目标]
  → 显示新目标的 Overview

点击面包屑中间节点
  → 回溯到该节点实体的 Overview
  → 删除该节点之后的面包屑项

点击 [Open Full]
  → NavigateToEntityRequestedMessage
  → Center 打开/跳转 EntityEditorDocument

点击 [Close] 或按 Esc
  → Peek 内容清空
```

### 5.2 Ctrl+LMB Navigate（跳转）

```
用户在任意区域 Ctrl+左键 点击引用
  → ReferenceResolver 解析引用目标
  → NavigateToEntityRequestedMessage
  → Center: 打开/跳转 EntityEditorDocument（目标实体）
  → Left KV: 跟随切换到目标实体
  → 不触发 Peek 面板

如果目标实体已有一个标签页
  → 激活该标签页（不重复创建）
```

### 5.3 快捷键对照

| 操作 | 快捷键 | 行为 | Peek 面板 |
|------|--------|------|:--:|
| Navigate | Ctrl+左键 | 跳转标签页 | 不影响 |
| Peek | Ctrl+右键 | 固定预览 | 刷新 |
| Pin 锁定 | Peek 面板 Pin 按钮 | 锁定当前 Peek，新 Peek 请求被忽略 | 锁定 |

---

## 六、索引刷新工作流

```
用户打开 Profile，Bottom 加载 DataTable。
Ref Index 和 Reverse Index 初始为空（或显示提示"Click Refresh to load"）。

用户选中 DataTable 中的实体
  → Center 打开 EntityEditorDocument
  → Left KV 切换
  → 索引 Tab 不变（避免卡顿）

用户需要查看引用索引时
  → 切换到 Ref Index Tab
  → 点击 [Refresh] 按钮
  → IndexTableViewModel.LoadForwardFromService(store)
  → 显示当前选中实体的正向引用索引

用户切换到 Reverse Index Tab
  → 点击 [Refresh] 按钮
  → 显示反向引用索引
```

---

## 七、CRUD 工具栏流程

```
工具栏始终显示四个按钮:
  [New Entity]   [Copy Entity]   [Delete Entity]   [Save]

New Entity:
  → 弹出类型选择对话框（24 个实体类型）
  → 在当前 Mod 中创建新实体
  → Bottom DataTable 新增一行
  → Center 自动打开 EntityEditorDocument

Copy Entity:
  → 复制当前选中实体（Bottom 选中行或 Center 当前实体）
  → 新 ID，复制全部字段
  → 同上打开

Delete Entity:
  → 确认对话框
  → 从 Mod 中移除
  → 关闭对应 EntityEditorDocument（如有）
  → Bottom DataTable 移除该行

Save (Ctrl+S):
  → 提交所有 Dirty 实体 → DB → 导出 XML
  → 清除 Dirty 标记
  → 状态栏更新 "Saved at HH:mm"
```

---

## 八、当前技术债务

| # | 问题 | 严重度 | 说明 |
|---|------|--------|------|
| 1 | Bottom DataTable 仍可编辑 | 🟡 | `ModGameDataTabsView` 需改为只读模式 + 保留跳转 |
| 2 | Ctrl+LMB 仍触发 Peek | 🟡 | 需分离为 Navigate（`GenericDataGridHelper` + `SearchableDataGrid`） |
| 3 | Right Panel 仍用 ValueEditorPanel | 🟡 | `PeekPanelView` 已创建，需接入 ToolDock |
| 4 | KV 绑定走消息非焦点 | 🟡 | 需监听焦点事件而非仅 `EntitySelectedMessage` |
| 5 | Create/Copy/Delete 在 DataGrid 右键 | 🟢 | 需提取到工具栏 |
| 6 | 索引自动跟随选中刷新 | 🟡 | 需改为手动刷新（`IndexTableViewModel` 加 RefreshCommand） |
| 7 | 四区域不同实例 | 🟡 | 需确保 Center/Lef/Bottom 绑定同一 `IEntity` 引用 |
| 8 | DataBrowser/ModManager 以 SplitView 打开 | 🟢 | 低优先级，当前功能可用 |
