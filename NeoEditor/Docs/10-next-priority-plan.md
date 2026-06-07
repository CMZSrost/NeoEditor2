# NeoEditor 下一阶段优先级规划

> 整理日期: 2026-06-07 | 当前版本: v0.18.0-dev

---

## 零、本轮已完成（Stage 16）

| 项目 | 说明 |
|------|------|
| ✨ 数据浏览器三层结构 | 侧边栏(大类→数据类) → Dock 标签页(EntityBrowserDocument+ListBox) → 实体查看(EntityViewerDocument+EntityViewerView) |
| ✨ ItemType 卡片式重设计 | Hero Header（主图+画廊）+ Stat Bars + Property Tags + Equipment/Container/Degrade/Charge Cards + Reference Bars + Reverse Refs |
| ✨ Visualizer 覆盖全 24 表 | 17 个类型专用 visualizer + Default 回退，每个 Detail + Overview 双实现 |
| 编译修复 | BottomToolsView/DataBrowserView ElementName 类型修复、WrapPanel Spacing、Math.Clamp |
| Dock 标签关闭 | 移除 DomainBrowserView 自定义关闭按钮，用主 Dock 自带关闭 |

---

## 一、当前焦点：可视化内容丰富

### 现状

`IEntityVisualizer` 接口和 `EntityVisualizerRegistry` 注册表已就绪，有 5 个类型实现（Recipe/Encounter/TreasureTable/ItemType/Default）。**但当前的 BuildDetail/BuildOverview 方法只输出纯文本字段，不是真正的"可视化"。**

### 目标

每个实体类型的 visualizer 应包含：
- 🖼️ **图片展示**：通过 `ImageService` 搜索游戏/mod 图片目录，用 `ZoomableImageView` 渲染
- 🔗 **引用树**：解析 ReferenceField，以可点击的 TreeView 展示关联实体，支持 Peek/跳转
- 📊 **关系图/流程图**：复用现有 `StoryTreeEditor`、`RecipeFlowchartEditor`、`TreasureTreePreviewEditor` 中的可视化逻辑
- 📋 **精选属性面板**：显示核心属性，非全量字段

---

## 二、24 种实体类型的可视化开发清单

### 已有自定义编辑器（可参考逻辑）

| 实体 | 数据量 | 现有编辑器 | 需要的 Detail 可视化 | 需要的 Overview 可视化 |
|------|:--:|------|------|------|
| **ItemType** | 537 | SpriteShow + WearShow | 物品图片（ImageList/SpriteList 加载 + ZoomableImageView）、装备槽位叠加预览、属性树（vProperties 解析）、引用跳转（TreasureTable/Condition/ContainerType） | 缩略图 + 核心属性(Weight/StackLimit/MonetaryValue) |
| **Recipe** | ~185 | Recipe Tree | 配方流程图（Tool→产物 树状展开）、Ingredient 解析+数量+图标、TreasureTable 战利品链接 | 工具/消耗/产物 摘要 |
| **Encounter** | 2264 | Story Graph | 剧情文本渲染、Response 关系树（LeadsFrom/To 权重用百分比条）、Trigger 触发条件列表、条件状态图标 | 名称 + 剧情摘要(150字截断) |
| **TreasureTable** | 764 | Treasure Tree | 嵌套战利品树（递归展开 aTreasures）、概率分布可视化（条形图）、OR/AND 逻辑分组 | 战利品数量 + 主要项摘要 |

### 需从零开发的 Visualizer

| 实体 | 数据量 | Detail 可视化建议 |
|------|:--:|------|
| **Creature** | 28 | 生物属性面板（攻击方式/基础状态/阵营）、战利品引用链接、TreasureTable/Corpse 关联 |
| **Condition** | 872 | FieldNames↔Modifiers 配对表、持续时间/致命/颜色可视化、vIDNext 条件链 |
| **AttackMode** | 61 | 伤害类型(Cut/Blunt/Penetration)参数面板、射程指示器 |
| **BattleMove** | ~30 | 战斗条件树（Us/Them 前置条件 + 效果条件）、效果描述、图片展示 |
| **HexType** | 37 | 地形图标、移动消耗/能见度面板、TreasureTable + CampType 关联 |
| **Faction** | 14 | 阵营图标、外交关系矩阵 |
| **Ingredient** | 128 | RequiredProps vs ForbidProps 对比面板、反向引用（哪些 Recipe 使用） |
| **ItemProp** | 108 | 属性描述、反向引用（哪些 ItemType 使用了此属性） |
| **EncounterTrigger** | ~100 | 触发条件面板（位置/日期/HexType）、关联 Encounter 跳转 |
| **CampType** | ~20 | 营地图标、TreasureTable 关联、条件要求 |
| **ChargeProfile** | ~30 | 弹药/充能参数面板、关联 ItemType |
| **ContainerType** | ~15 | 容器容量参数 |
| **CreatureSource** | ~30 | 坐标 + 关联 Creature 跳转 |
| **DmcPlace** | ~25 | 坐标 + 关联 Encounter |
| **其余** (DataFile/GameVar/Headline/Map/BarterHex/ForbiddenHex) | 少量 | 至少：图片（如有Img字段）+ 关键引用跳转 + 属性概览 |

---

## 三、可复用的可视化组件

| 组件 | 来源 | 用途 |
|------|------|------|
| `EditorHelper.BuildOverviewTab(IEntity)` | 现有 | 属性树（通用回退） |
| `EditorHelper.BuildRefChildren` | 现有 | 引用字段解析 → TreeView |
| `EditorHelper.AddImagePreviews` | 现有 | 图片缩略图 |
| `EditorUIFactory.NavOnCtrl` | 现有 | Ctrl+Click 跳转 |
| `ZoomableImageView` | 现有 | 图片缩放/平移 |
| `ImageService.FindImage()` | 现有 | 多目录图片搜索 |
| `ReferenceResolver` | 现有 | 引用解析/去重/反向引用 |
| `HexMapRenderer` | 现有 | 六边形地图渲染 |

---

## 四、实施建议

### 第一轮：完善已有 4 个 visualizer（Recipe/Encounter/TreasureTable/ItemType）
```
1. ItemType visualizer → 加入图片展示 + 装备槽预览
2. Recipe visualizer → 加入 Ingredient 解析 + 图标
3. Encounter visualizer → 加入 Response 关系树 + Trigger 列表
4. TreasureTable visualizer → 加入嵌套战利品树 + 概率条
```

### 第二轮：高价值实体（Creature/Condition/AttackMode/Faction/HexType）
```
5. Creature → 生物面板 + 战利品链接
6. Condition → 字段配对表 + 条件链
7. AttackMode → 伤害参数面板
8. Faction → 外交矩阵
9. HexType → 地形参数 + 关联跳转
```

### 第三轮：中等实体 + Overview 完善
```
10. Ingredient → 属性对比
11. BattleMove → 条件树
12. ItemProp/EncounterTrigger/CampType/ChargeProfile → 属性+引用
13. 其余 → 至少图片+引用跳转
```

### 第四轮：通用 Overview 升级
```
14. DefaultEntityVisualizer.BuildOverview → 加入图片预览 + 引用摘要
15. 合并视图可视化概览面板 → 显示实体图片缩略图
```

---

## 五、已知限制（持续跟踪）

| # | 问题 | 状态 | 备注 |
|---|------|:--:|------|
| 1 | 排序箭头不显示 | 🔴 | Avalonia 11.3 框架限制 |
| 2 | IMessenger.Send 单参数重载 | 🔴 | CommunityToolkit.Mvvm 8.4.0 疑似移除，需显式传 token |
| 3 | ModDatabase Expander 箭头遮挡 | 🟡 | 需调 Padding |
| 4 | TreasureTable aTreasures 混合引用 | ✅ | 已修复 |
| 5 | ValueEditorPanel 空白 | 🟡 | 可视化器内容不丰富，需先完善 visualizer |
| 6 | 嵌套 DockControl 空白 | ✅ | DocumentWorkspaceView 已改用 ToolDock + UserControl 内联方式（非嵌套 DockControl），四区域布局正常渲染。EntityBrowserView 仍使用 TabControl 作为查看区容器 |
