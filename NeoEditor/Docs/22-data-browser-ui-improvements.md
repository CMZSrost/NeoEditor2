# 数据浏览器 UI 改进记录

> 更新：2026-06-11 · 本会话完成的13+项数据浏览器UI改进

---

## 一、组件清单

### 1. CreatureStatGrid（标准两列网格）

**用途**: 替代旧 `BuildStatCard`，用于展示非进度条类型的键值对属性。

**特征**:
- 2列Grid布局，label小字在上、大value在下
- 支持可选颜色徽章
- 适用所有 Creature 相关和非进度条属性

**影响实体**: ItemType (fDegradePerHour/fDegradePerUse), CreatureSource, BarterHex, GameVar, BattleMove (非进度条属性)

### 2. BuildReverseRefsPanel（共享被引用面板）

**用途**: 集中展示"谁引用了我"的反向引用列表。

**特征**:
- AttackMode 风格列表 + 类型徽章
- 分页（每页12条）
- Ctrl+Click 导航到引用源

**影响实体**: ItemProp, Ingredient, Encounter, Condition, TreasureTable, Recipe, Creature, ItemType (Overview内)

### 3. BuildStoryBranchDiagram（剧情分支图）

**用途**: Encounter 的剧情分支可视化。

**特征**:
- Mermaid 代码文本块（可复制）
- Avalonia 可视化树：根节点 → 箭头 → 分支节点
- 分支节点含概率徽章（如 50%）和目标遭遇战徽章
- Ctrl+Click 可导航到目标 Encounter

**影响实体**: Encounter

### 4. Recipe BuildIngredientsPanel（层次感UI）

**用途**: 展示配方材料，每个 ingredient 为垂直卡片。

**特征**:
- 总：材料名 + 数量（header 级别）
- 分1：RequiredProps（缩进，margin left 16）
- 分2：ForbidProps（缩进，margin left 16）
- 导航箭头 Ctrl+Click 跳转

**影响实体**: Recipe

---

## 二、本地化新增键（18个）

| 键名 | 英文 | 中文 |
|------|------|------|
| Vis.StoryText | Story Text | 剧情文本 |
| Vis.Option | option | 选项 |
| Vis.Options | options | 选项 |
| Vis.NoResponses | (No responses) | （无回应） |
| Vis.StoryBranch | Story Branch | 剧情分支 |
| Vis.NoBranches | (No branches) | （无分支） |
| Vis.RemoveSubmit | Remove (submit/destroy) | 移除（上交/摧毁） |
| Vis.Conditions | Conditions | 附带状态 |
| Vis.PreConditions | Pre-Conditions | 前置条件 |
| Vis.SpawnCreature | Spawn Creature | 刷新生物 |
| Vis.Accidents | Accidents | 意外事件 |
| Vis.TriggeredBy | Triggered By | 触发自 |
| Vis.Price | Price | 价格 |
| Vis.LootChance | Loot Chance | 战利品几率 |
| Vis.Accident | Accident | 意外 |
| Vis.CreatureRef | Creature | 生物 |
| Vis.Teleport | Teleport | 传送 |
| Vis.Responses | Responses | 回应选项 |

已添加至 `Resources.resx`, `Resources.zh.resx`, `Resources.en-us.resx`。

---

## 三、修改文件清单

| 文件 | 修改内容 |
|------|---------|
| `EntityVisualizers.cs` | Condition bResetTimer始终显示; ItemType degrade→CreatureStatGrid; CreatureSource/BarterHex/GameVar移除StatBar; BattleMove非进度条→CreatureStatGrid; ItemProp/Ingredient→BuildReverseRefsPanel; Recipe层次感UI; Encounter本地化+mermaid图; VisualHelper.LoadImage支持无扩展名图片 |
| `EditorHelper.cs` | Overview: Encounter+ItemType加入overlay chain; 全部主要实体类型加入反向引用; AddImagePreviews支持无扩展名图片 |
| `Documents.cs` | DmcPlace ResolveDisplayName去掉`#{dp.Id}`后缀; BrowserEntityRow显示调整（prev session） |
| `DomainBrowserView.axaml.cs` | Dock消失Bug：OnAttachedToVisualTree恢复ViewerTabs; OnDetachedFromVisualTree清空_viewerDocDock |
| `Resources.resx` | 新增18个Vis键 |
| `Resources.zh.resx` | 新增18个Vis键（中文翻译） |
| `Resources.en-us.resx` | 新增18个Vis键（英文翻译） |

---

## 四、Bug修复记录

### Dock消失Bug
- **问题**: 切换文档标签页后，DomainBrowserView内嵌套的DockControl重新初始化，丢失所有document dock，但ViewerTabs仍保留旧条目
- **根因**: `InitializeFactory="True"` 导致DockControl在detach/reattach时重建layout，旧`_viewerDocDock`引用失效
- **修复**: `OnDetachedFromVisualTree`中清空`_viewerDocDock`，`OnAttachedToVisualTree`中重新查找并调用`RestoreViewerTabs()`恢复所有标签页

### DmcPlace图片加载失败
- **问题**: DmcPlace的Image字段值如`btn_dmc_diner`不带.png扩展名，`ImageService.FindImage`使用`Directory.GetFiles`精确匹配失败
- **修复**: `VisHelper.LoadImage`和`EditorHelper.AddImagePreviews`中，对无扩展名图片名先尝试加`.png`
