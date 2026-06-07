# NeoEditor 工作流重设计

> 审计日期：2026-05-30  
> 核心原则：**让 UI 告诉用户下一步该做什么，而不是让用户自己摸索**

---

## 目录

- [1. 当前问题诊断](#1-当前问题诊断)
- [2. 五条工作流](#2-五条工作流)
- [3. UI 改造方案](#3-ui-改造方案)
- [4. 实施路线](#4-实施路线)

---

## 1. 当前问题诊断

### 1.1 UI 结构现状

```
┌──────────────────────────────────────────────────┐
│ MainMenuBar (File/Edit/View/Help)                 │
├──────┬───────────────────────────────────────────┤
│ 📁   │  [AddImage] [Tab▼]                        │ ← 工具栏和主内容脱节
│ 🗄️   │                                           │
│ 📋   │  DocumentWorkspace (空 Dock)               │ ← 首次打开一片空白
│ 🔍   │                                           │
│ ⚙️   │                                           │
├──────┴───────────────────────────────────────────┤
│ MainStatusBar                                     │ ← 没有上下文信息
└──────────────────────────────────────────────────┘
```

### 1.2 核心问题

| # | 问题 | 影响 |
|---|------|------|
| 1 | **首次打开一片空白** | 用户不知道能做什么。Dock 区域是空的，没有引导 |
| 2 | **5 个侧栏按钮权重相等** | Explorer / ModDatabase / Profiles / Search / Settings 地位相同，没有主次 |
| 3 | **工具栏和内容脱节** | AddImage 按钮和 Tab 下拉与其他功能没有关联 |
| 4 | **ModDatabase 面板功能堆砌** | Import / Create / Load / Clear / Delete / CSV / ZIP 全部挤在一起 |
| 5 | **单 Mod 视图和合并视图入口分散** | 合并视图只能从 Profiles 右键打开，新用户找不到 |
| 6 | **没有 "下一步" 指引** | 创建 Mod 后不知道怎么加数据；导入后不知道从哪里开始编辑 |
| 7 | **状态栏无上下文** | 看不出当前是什么模式、打开了哪个 Mod、有多少实体 |

---

## 2. 五条工作流

为三种用户画像设计五条独立的工作流。每条工作流的每一步都有明确的 UI 对应。

### 工作流 A：浏览游戏数据（新人了解游戏）

```
用户心理: "这个游戏有哪些物品？怪物属性是什么样的？"
```

| 步骤 | 操作 | UI 位置 | 反馈 |
|:----:|------|---------|------|
| 1 | 启动编辑器 | — | 显示**欢迎页**，三个入口卡片 |
| 2 | 点击 "Browse Game Data" | 欢迎页主卡片 | 加载游戏基础数据（只读） |
| 3 | DataGrid 打开，所有 24 种实体 Tab | 主 Dock 区 | 每列有字段 Tooltip；引用列显示名称+可跳转 |
| 4 | 悬停列头看字段说明 | DataGrid 列头 | 中文说明 Tooltip |
| 5 | Ctrl+Click 引用值跳转 | DataGrid 单元格 | 跳转到目标表+定位行 |
| 6 | 看到感兴趣的，点击 "Create Mod to Edit" | 工具栏 CTA 按钮 | 进入工作流 B |

**关键 UI 元素**：
- 欢迎页 "Browse Game Data" 卡片（大图标 + 描述文字）
- 浏览模式下工具栏显示 "🔒 Read-only · [Create Mod to Edit]"

---

### 工作流 B：创建新 Mod（新人 / 作者从零开始）

```
用户心理: "我想加一把新武器 / 改一个配方"
```

| 步骤 | 操作 | UI 位置 | 反馈 |
|:----:|------|---------|------|
| 1 | 点击 "+ New Mod" | 欢迎页 或 工具栏 | 弹出创建对话框 |
| 2 | 填写 Mod 名称、选择类型（Insert/Merge） | 对话框 | Insert = 追加数据 / Merge = 覆盖数据（有说明） |
| 3 | 确认 → Mod 创建完成 | — | DataGrid 自动打开，所有 24 种实体 Tab 就位 |
| 4 | 状态栏显示 `Mod: MyMod (Insert) · 0 entities` | 底部状态栏 | 知道当前上下文 |
| 5 | 点击工具栏 `[+]` | 工具栏 | AddRowDialog 弹出 |
| 6 | 在对话框中选实体类型（ItemType / Recipe / Creature...）、数量 | 对话框 | 自动分配 ID、创建空白行 |
| 7 | 编辑单元格 | DataGrid | 类型适配编辑器；引用列有下拉选择 |
| 8 | `Ctrl+S` 保存 | — | DB + XML 一次性写入；通知 "Saved 3 entities to MyMod/neogame.xml" |
| 9 | 去游戏测试 | — | 状态栏更新 `Last saved: just now` |

**关键 UI 元素**：
- 创建对话框简洁：名称 + Insert/Merge 单选（带说明文字）
- 工具栏 CTA 变化：
  - Mod 为空时：`[+ Add Entity] [Import XML...]` 
  - Mod 有数据时：`[+] [-] [←导航] [🔍过滤] [💾 Save]`
- 状态栏：`Mod: MyMod · 15 entities · Last saved 2m ago · ⚠ 3 unsaved`

---

### 工作流 C：导入已有 Mod（作者迁移）

```
用户心理: "我有个写了三年的 mod，想用这个编辑器继续维护"
```

| 步骤 | 操作 | UI 位置 | 反馈 |
|:----:|------|---------|------|
| 1 | 点击 "Import Mod" | 欢迎页 / 工具栏 / 拖拽文件夹到窗口 | 文件选择器 |
| 2 | 选择 Mod 文件夹 → 确认 | 文件选择器 | 自动解析 XML → DB |
| 3 | 解析完成 → DataGrid 自动打开 | 主 Dock | 提示 "Imported MyMod: 42 entities from 3 XML files" |
| 4 | 如解析有警告（类型错误等） | 通知栏 | "⚠ 2 warnings: Creature id=7 nFaction='abc' 不是整数，已跳过" |
| 5 | 编辑 → `Ctrl+S` | DataGrid | 写回源 XML 文件 |
| 6 | 状态栏更新 | 状态栏 | `Mod: MyMod · 42 entities · Saved` |

**关键 UI 元素**：
- 支持**拖拽文件夹/XML 文件到 Dock 区域**直接导入
- 导入后立即显示**解析报告**（成功 N 条 / 警告 M 条）
- 工具栏显示 Mod 名称 + 实体数量

---

### 工作流 D：合并 Mod（整合包作者）

```
用户心理: "我有 10 个 Mod，哪个覆盖了哪个？有没有冲突？"
```

| 步骤 | 操作 | UI 位置 | 反馈 |
|:----:|------|---------|------|
| 1 | 导入所有 Mod（逐个或批量） | 工作流 C 重复 / 侧栏 Batch Import | 每个 Mod 显示在 ModDatabase |
| 2 | 打开 Profiles 侧栏 | 侧栏按钮 | 已有 getmods.php 自动导入为 Profile |
| 3 | 双击 Profile → 合并视图打开 | Profile 列表 | 所有 Mod 数据合并展示 |
| 4 | 工具栏显示冲突数 | 工具栏 `⚠ 42 conflicts` 按钮 | 红色数字醒目 |
| 5 | 点击冲突按钮 → 冲突详情弹窗 | 弹窗 | 按表分组：`[ItemType] NSE/MyMod: nTreasureID 冲突` |
| 6 | 选中行 → 下方覆盖链面板 | 覆盖链面板 | 看到数据在各 Mod 中的版本链 |
| 7 | 通过 Mod 过滤下拉框只看某个 Mod | 工具栏 ComboBox | DataGrid 筛选 |
| 8 | Show All 切换查看被覆盖的实体 | 工具栏 Toggle | 被覆盖行灰色背景 |
| 9 | 编辑数据（自动回落源 Mod） | DataGrid | 点击保存写回对应 Mod XML |
| 10 | Export Pack → .zip | Profile 右键 / 工具栏 | 打包所有 Mod |

**关键 UI 元素**：
- 冲突按钮**始终可见**，数字实时更新
- 合并视图打开时，侧栏自动切换到**合并工具面板**（覆盖链 / 冲突列表 / Mod 贡献统计）
- 状态栏：`Merge View: 5 mods · 3,241 entities · ⚠ 42 conflicts`

---

### 工作流 E：全局搜索（所有用户）

```
用户心理: "我记得有个叫 'Water Bottle' 的物品，在哪？"
```

| 步骤 | 操作 | UI 位置 | 反馈 |
|:----:|------|---------|------|
| 1 | 点击侧栏 Search | 侧栏按钮 | 搜索面板打开 |
| 2 | 输入 "Water" → 防抖 200ms → 结果 | 搜索面板 | 按实体类型分组显示匹配项 |
| 3 | 双击结果行 | 搜索结果 | 跳转到对应 DataGrid Tab + 定位行 |
| 4 | 搜索历史可复用 | 搜索面板 | 最近 20 条搜索 |

---

## 3. UI 改造方案

### 3.1 欢迎页（HomePage）

编辑器首次打开 / 所有文档关闭时显示，替代当前的空 Dock。

```
┌──────────────────────────────────────────────────┐
│                                                  │
│              🛠️ NeoEditor                        │
│        Neo Scavenger Mod Editor                  │
│                                                  │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────┐ │
│  │  📖 Browse    │ │  ✨ New Mod   │ │ 📥 Import │ │
│  │  Game Data   │ │  Start fresh │ │  Existing │ │
│  │              │ │              │ │  Mod      │ │
│  │  Explore all │ │  Create your │ │  Open an  │ │
│  │  game items, │ │  own mod from│ │  existing │ │
│  │  recipes,    │ │  scratch     │ │  mod folder│ │
│  │  creatures   │ │              │ │           │ │
│  └──────────────┘ └──────────────┘ └──────────┘ │
│                                                  │
│  ──────────────────────────────────────────────  │
│  Recent Mods:                                     │
│  ┌──────────────────────────────────────────────┐│
│  │ 📄 MyMod         12 entities · 2h ago        ││
│  │ 📄 NSE Patch      3 entities · yesterday      ││
│  │ 📄 Game Data     (read-only)                 ││
│  └──────────────────────────────────────────────┘│
│                                                  │
│  Or drop a mod folder / XML file here            │
└──────────────────────────────────────────────────┘
```

**实现方式**：
- 新建 `Views/UserControls/HomePage.axaml`，作为 Dock 的默认内容
- `DocumentWorkspaceViewModel.Documents` 为空时显示 HomePage
- 三个卡片点击 → 触发对应 Command
- Recent Mods 从 `EditorDbContext.ModInfos` 加载（按 `LastModified` 排序）
- 拖放区域接受文件夹 / XML 文件

### 3.2 侧栏重设计

**当前**：5 个按钮垂直排列，每个打开独立面板  
**改为**：2 组 + Quick Action 区

```
┌──────┐
│ 🏠   │  ← Home (回到欢迎页)
│ ──── │
│ 📥   │  ← Import Mod (直达操作，不是面板)
│ 📋   │  ← Profiles (合并视图入口)
│ ──── │
│ 📁   │  ← Explorer (文件浏览)
│ 🔍   │  ← Search (全局搜索)
│ ⚙️   │  ← Settings
└──────┘
```

**变更**：
- 新增 **Home** 按钮 —— 返回欢迎页 / 关闭所有文档
- **Import** 按钮提升为顶级 —— 直接打开文件选择器，不再藏在 ModDatabase 面板里
- **Profiles** 按钮保留 —— 整合包作者的核心入口
- **ModDatabase** 面板降格 —— 内容合并到 Home 页的 Recent Mods 列表 + ContentArea 的工具栏 Mod 选择器
- 底下三个（Explorer / Search / Settings）保持不变

### 3.3 工具栏上下文切换

工具栏根据当前视图显示不同按钮。**不再使用静态布局**，而是根据 `ActiveDocument` 类型动态切换。

**无文档打开时（HomePage）**：
```
[✨ New Mod] [📥 Import Mod]
```

**单 Mod 编辑时**：
```
[← Back] [+] [-]  |  [🔍 filter...]  |  [💾 Save (Ctrl+S)]
状态栏: Mod: MyMod · 15 entities · ⚠ unsaved
```

**合并视图时**：
```
[← Back] [+] [-]  |  [⚠ 42 conflicts] [All Mods ▼] [Show All ☐]  |  [💾 Save (Ctrl+S)]
状态栏: Merge: 5 mods · 3,241 entities · ⚠ 42 conflicts
```

**浏览模式时（只读 Game Data）**：
```
[🔒 Read-only]  |  [🔍 filter...]  |  [Create Mod to Edit →]
状态栏: Game Data (read-only) · 3,241 entities in 24 tables
```

### 3.4 状态栏升级

当前状态栏基本没用。改为三区布局：

```
┌──────────────────────────────────────────────────────────────┐
│ Mod: MyMod (Insert) │ 15 entities │ 💾 Saved 2m ago │ ⚠ 3 conflicts │
└──────────────────────────────────────────────────────────────┘
```

信息从左到右：
- **上下文**（Mod 名 + 模式 / Merge View + Profile 名）
- **数据量**（实体数 / Mod 数）
- **保存状态**（Saved just now / ⚠ Unsaved changes / 上次保存时间）
- **警告**（冲突数 / 验证问题数 / 解析警告数）

### 3.5 ModDatabase 面板替代方案

当前 `ModDatabase` 面板功能繁多（Import / Create / Load / Clear / Delete / CSV / ZIP / ShowData / ShowImage）。重构为：

- **Import** → 提升为侧栏顶级按钮 + 欢迎页卡片
- **Create** → 欢迎页卡片 + 工具栏按钮
- **CSV / 数据导出** → 移到 Profiles 面板（与导出关联）
- **ZIP 导入导出** → 保留在 Mod 右键菜单
- **Mod 列表** → 移到 Home 页 "Recent Mods" + 工具栏 Mod 选择器（下拉）
- **ShowData / ShowImage** → 双击 Recent Mods 列表项打开

**工具栏 Mod 选择器**（替换当前 ComboBox）：
```
[MyMod ▼]  ← 下拉显示所有已导入 Mod，选中后打开 DataGrid
```

---

## 4. 实施路线

### Phase 1：入口重设计（先让用户知道做什么）

| # | 项目 | 说明 |
|---|------|------|
| 1 | **Welcome Page** | `HomePage.axaml`：三个入口卡片 + Recent Mods 列表 + 拖放区域 |
| 2 | **侧栏按钮调整** | 新增 Home / Import 顶级按钮；ModDatabase 面板内容精简 |
| 3 | **工具栏动态切换** | 根据 `ActiveDocument` 类型显示不同工具栏按钮组 |
| 4 | **Import 直达** | 侧栏 Import 按钮 → 选文件夹 → 自动解析 → 打开 DataGrid |

### Phase 2：上下文感知（让用户知道在哪）

| # | 项目 | 说明 |
|---|------|------|
| 5 | **状态栏升级** | 三区信息：上下文 / 数据量 / 保存状态 / 警告 |
| 6 | **工具栏 Mod 选择器** | 下拉选择已导入 Mod → 打开 DataGrid |
| 7 | **Notification 优化** | 解析警告/保存结果/冲突警告分组显示，不刷屏 |

### Phase 3：引导链（让用户知道下一步）

| # | 项目 | 说明 |
|---|------|------|
| 8 | **浏览模式 CTA** | 只读 Game Data 时工具栏显示 "Create Mod to Edit" |
| 9 | **空 Mod 引导** | 新建 Mod 后 DataGrid 为空 → 工具栏突出显示 `[+ Add Entity]` |
| 10 | **合并视图冲突引导** | 冲突 > 0 → 冲突按钮脉动 / 红色，引导点击 |

---

## 5. 关键设计决策

### 5.1 为什么把 Import 提升为顶级按钮

当前 Import 藏在 ModDatabase 面板里，需要 2 步才能触发（打开面板 → 点 Import）。Import 是三个用户群都会用到的**最高频操作之一**，应该一步直达。侧栏只放 6 个按钮，Import 值得占一个。

### 5.2 为什么保留 Explorer

Explorer（文件浏览器）对老手和整合包作者有价值——他们需要看 Mod 目录结构、图片文件、XML 文件。新人不常用，但放在底层不碍事。

### 5.3 为什么用 Welcome Page 而不是空 Dock

空 Dock 是最差的初始状态——它告诉用户"这里什么都没有"。Welcome Page 提供三条路径，用户一眼就知道能做什么。Recent Mods 列表让回头客快速恢复工作。

### 5.4 为什么工具栏要动态切换

单 Mod 编辑和合并视图有**截然不同的操作需求**。静态工具栏要么塞太多按钮（臃肿），要么丢功能（缺失）。动态切换让每个场景的工具栏精准匹配需求。

---

## 实施状态 (2026-05-30)

| Phase | 完成项 | 状态 |
|:------|------|:--:|
| 1 | WelcomePage, 侧栏重组, RecentMods, MergeView 入口, 回主页刷新, Import 直达 | ✅ |
| 2 | 工具栏 CTA, 状态栏, Deps/Conflicts 按钮 | ✅ |
| 3 | 空 Mod 引导，冲突脉动 | ✅ |

**完成度: 90%** — 所有 Phase 完成。工具栏 CTA、面板系统通过 Grid 分栏实现（非 ToolDock）。
> 更新 2026-05-31: HomePage 三卡片；侧栏 7 按钮三组；RecentMods(实体数+时间)；Profiles 入口；回主页刷新；Import 直达；工具栏 CTA+面板切换按钮；状态栏；空 Mod 引导；冲突脉动。全部完成。
