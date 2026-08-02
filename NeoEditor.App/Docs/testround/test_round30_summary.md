# 架构测试第30轮 — 字段解释 + 可视化 + 引用解析跳转（499/499）

> 日期：2026-08-02 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 12.1 / ProDataGrid 12.0.4)
> 上承：[test_round29_summary.md](test_round29_summary.md)（Value Editor 引用解析一致性修复）
> 本轮目标：**完善所有字段的解释与可视化，以及引用字段的解析跳转功能**
> 用户确认：跳转语义与 DataTable 一致（Ctrl+Click）；可视化做 P6 徽章悬停预览 + 检查清单补齐；round29 已知限制（CSV/MCP/CLI 导出 `[a, b]`）一并修复

---

## 现状诊断（开工前）

| 维度 | 现状 |
|------|------|
| 字段解释 | DataTable 列头有 `FieldDescriptionService`（docx 提取），但提取 key 是 `attackmodes攻击模式.strname` 而查询 key 是 `attackmodes.strname` → **实际不生效**；Value Editor 完全无解释；权威数据在 Doc 38（24 表全字段含义+实测值域） |
| 可视化 | 24 个专属 visualizer 已有；Doc 21 §7 P6（徽章悬停预览）未实现 |
| 引用跳转 | DataTable Ctrl+Click/Ctrl+右键/Ctrl+Hover 已统一（round29）；**Value Editor 徽章无任何跳转**；导出路径（CSV/MCP/CLI）仍输出损坏 `[a, b]`（round29 已知限制） |

---

## A. 字段解释（所有字段）

### A1 权威数据源：Doc 38 → 内嵌资源 ⭐

- **新脚本 `artifacts/gen-field-descriptions.js`**：解析 `Docs/38-full-field-reference.md` 24 张表的 markdown 表格（`模型字段 | DB列名 | 类型 | 含义 | 示例值 | 实测值域`）→ `{table}.{property}` → 「含义 + 实测值域」文本（小写 key）
- **别名表**：`itemtypes.CondId ← ConditionId`（文档名与模型属性名不一致）
- **补充表**：Doc 38 主表漏了 3 个字段，从 Doc 20 补：`battlemoves.ChanceType`（三值格式 `0,距离档,概率系数`）/ `datafiles.Value`（可以卖的价值）/ `encounters.Image`（剧情显示的图片，默认当前六角格快照）
- **内建通配**：`*.modid` / `*.filepath` / `*.entityid`（IEntity 编排字段，任意表可用）
- 产物 `NeoEditor.Core/Model/field_descriptions.json`（266 条）作为 **EmbeddedResource** 嵌入 Core
- **新 `FieldDescriptions.cs`**（Core/Model，仿 FieldGroupMetadata 静态只读模式，符合 N01）：`GetDescription(Type, propName)` / `GetDescription(table, propName)`，大小写不敏感 + `*.` 通配回退

### A2 三条显示路径接线

| 路径 | 实现 |
|------|------|
| DataTable 列头 ToolTip | `App.axaml.cs` 的 `FieldDescriptionProvider` 改「内嵌资源优先 → docx 缓存兜底」（修复 key 不匹配根因） |
| Value Editor | `FieldRow.Description` 赋值（`BuildFieldDescription`：内嵌含义 + 引用字段追加 `🔗 引用 → 目标类型（Pattern/分隔符/目标键）`）；`KeyValueEditorView.axaml` DisplayName 挂 `ToolTip.Tip`（新 `EmptyStringToNullConverter` 防空 tooltip） |
| Visualizer RawDataTable | `BuildRawDataTable` 每行 key 挂字段解释 ToolTip；引用列值悬停显示**每段解析后的 Subject**（`Subject (rawId)` 多行） |

### A3 顺带修复：`DocxTextExtractor` key 提取正则

`第[^部分]+部分\s*(\w+)` → `([a-zA-Z_][a-zA-Z0-9_]*)`：中文后缀不再漏进 key（`attackmodes攻击模式.strname` → `attackmodes.strname`），docx 兜底路径今后可用

---

## B. 可视化

### B1 P6 徽章悬停预览 ⭐

- **`VisHelperService.BuildRefTooltip(IEntity)`**：按类型分派的小型 stat 面板（ChargeProfile/AttackMode/Condition/ItemType/Creature/TreasureTable/Encounter/Recipe，default=EntityId+ModId），白底卡片 + 底部 `Ctrl+Click → open detail` 提示
- **`RefNode`** 构造加可选第 3 参 `Func<IEntity, Control?>? tooltipBuilder`；`Badge<T>`/`BadgeWithSlot`/`BadgeForEntity` 解析成功后 `ToolTip.SetTip`（`AttachTooltip` 助手）；App DI 注册传入 `vis.BuildRefTooltip`；19 个 visualizer 的本地回退构造同步加参（脚本批量更新）
- **Value Editor 徽章**（`ReferenceFieldEditor.CreateBadge`）同样挂预览

### B2 检查清单补齐（Doc 21 §10 审计，Explore agent 全量审计 24 个）

**引用类型不一致 bug（2 处，解析会 miss）**：
| 位置 | 原渲染 | 模型/文档标注 | 修复 |
|------|--------|--------------|------|
| `CreatureEntityVisualizer` :190 | `Badge<Condition>` | `EncounterIds → Encounter`（round28 已改模型） | `Badge<Encounter>` |
| `EncounterEntityVisualizer` :1032/:1318 | `CreatureSource` | `nCreatureID → Creature`（Doc 37 §4.11） | `LookupRef<Creature>` / `Badge<Creature>` |

**缺失的引用徽章面板（3 处，此前只出现在 RawData）**：
- `Encounter.ItemsId`（→ItemType，`{Id}` 主键）→ 新 `Vis.GiveItem` resx 键（en/zh）+ Badge 面板
- `Encounter.Loot`（→TreasureTable）→ Badge 面板（`Vis.Loot` 已有键）
- `Recipe.HiddenId`（→Recipe）→ 新 `BuildHiddenPanel`（`Vis.Hidden` 已有键）

**Hero 图点击放大（5 处）**：DataFile / DmcPlace / CampType / Map / Encounter 的 Hero 图片加 `PointerPressed → OpenZoomableImage`（与 AttackMode 一致）

**确认无需补**：CreatureSource/DataFile/GameVar/Headline 无任何实体引用（模型 [ReferenceField] 图核查），不补反向引用属正确行为

---

## C. 引用字段解析跳转

### C1 Value Editor 徽章跳转（与 DataTable 一致）⭐

`ReferenceFieldEditor.CreateBadge`：解析成功 → `Cursor=Hand` + `PointerPressed` 接线 ——
- **Ctrl+Click** → `INavigationRouter.NavigateToEntity`（打开目标实体详情）
- **Ctrl+右键** → `RequestPeek`（Peek 面板）
- 普通点击不动作（与 DataGrid 完全一致）
- 决策逻辑抽成 `internal static ResolveClickAction(modifiers, isRightButton)` 供纯逻辑测试

### C2 导出路径清扫（round29 已知限制）⭐

4 条导出路径的 `prop.GetValue(entity)?.ToString()` → `ReferenceText.GetRawString(value, refAttr)`（`[16, 46]` → `16,46`）：
- `CsvImportExportService`（:82 导出 / :142/:143 比较 / :163 新增 diff）
- `Mcp EntityResourceProvider`（:66 JSON 序列化）
- `CliCommandHandler`（:204 `refs` 命令 rawValue / :286 `show` 序列化）

---

## D. 测试（+17）

| 项目 | 新增 | 文件 / 说明 |
|------|:--:|------|
| Core.Tests | +5 | `FieldDescriptionsTests`：**全 263 个 [Column] 属性都有解释的覆盖断言**（缺即失败 → 驱动别名表补齐）+ 非平凡长度 + 通配（ModId/FilePath/EntityId）+ 大小写不敏感 + 语义抽查 |
| Infra.Tests | +2 | `CsvImportExportServiceTests`：引用列导出原文 `3,14`（非 `[3, 14]`）+ 单值引用 |
| Mcp.Tests | +1 | `ReadResourceAsync_ReferenceColumns_SerializeRawText_NotBrokenBrackets`（Stub IHostService + StubRepository 全套） |
| Cli.Tests | +2 | `CliCommandHandlerReferenceTests`：`show` 序列化原文 + `refs` rawValue 原文（新 Stub 全套） |
| EntityEditor.Tests | +7 | `KeyValueEditorFieldExplanationTests`：全行 Description 非空 / 引用行含目标类型+格式 / 普通行 Doc38 含义 / `ResolveClickAction` 三态 / 解析成功徽章有 Hand 光标+ToolTip 预览 / 未解析徽章无导航接线 |

**踩坑记录**：
- headless `window.MouseDown` 对迷你徽章命中不可靠（hit-test 坐标偏移）→ 改测 `ResolveClickAction` 决策逻辑 + 徽章接线存在性（Cursor/ToolTip）
- `Dispatcher.UIThread.RunJobs()` 在全量并行测试里跨线程抛 `VerifyAccess` → 移除（徽章同步创建，无需布局）
- `Constants.GameTypes` 以**类名**为键（"Creature"）非表名（"creatures"）
- `Pointer`/`PointerPressedEventArgs` 构造签名在 Avalonia 12 已变（`Pointer(int, PointerType, bool)`）——规避输入模拟后无需

## 编译和测试

| 项目 | 结果 |
|------|:----:|
| `dotnet build NeoEditor.sln`（全量 22 项目） | **0 错误** ✅ |
| 全量测试 | **503/503 ✅**（482→503，+21：Core 65→70、Infra 164→166、Mcp 25→26、Cli 40→42、EntityEditor 31→42） |

## 追修（用户报告）：visualizer 引用解析与 DataTable 不一致 ⭐

> 用户：「eventtrigger 的可视化里的引用解析怎么和 datatable 也不一致？datatable 能解析出来，但是可视化里没法解析出来。这块都是要统一到 host service 的」

**根因**：round29 修了 2 条显示路径（KeyValueEditorViewModel / ColumnTemplateFactory + DataGridCellInteractionService），**漏了 visualizer**。`EncounterTriggerEntityVisualizer` 用 `et.EncounterId.ToString()` 把 `ReferenceList` 渲染成损坏格式 `[123]`（`ReferenceList.ToString()` = `[{DisplayText}]`）→ `Badge<Encounter>` 收到 `[123]` → 内存 `ReferenceIndex.Lookup` miss（索引里是 `123`）→ 灰徽章。DataTable 走 `ReferenceText.GetRawString` → `123` → 解析成功。**解析后端本身已统一**（round29 都走内存 ReferenceIndex + IReferenceResolver），差异纯在调用方传入了损坏 rawId。

**修复（全仓审计所有 ReferenceList → `.ToString()` 显示路径，统一到 `ReferenceText.GetRawString` / `ToRawString`）**：

| 文件 | 原 | 改 |
|------|----|----|
| `EncounterTriggerEntityVisualizer` :123 ⭐ | `et.EncounterId.ToString()` → `[123]` | `et.EncounterId.ToRawString(null)` → `123` |
| `EncounterTriggerEntityVisualizer` :131 | `et.HexTypes.Split(',')`（依赖 RawText） | `et.HexTypes.ToRawString(",").Split(',')`（条目派生，不依赖外部状态） |
| `DmcPlaceEntityVisualizer` :140 | `dp.EncounterId.ToString()` | `ToRawString(null)` |
| `HexTypeEntityVisualizer` :242 | `ht.DefaultCampId.ToString()` | `ToRawString(null)` |
| `DefaultEntityVisualizer` :50（兜底 TreeView） | `val?.ToString()` | `ReferenceText.GetRawString(val, refAttr)` |
| `VisHelperService.BuildRawDataTable` :413（所有 visualizer 的 RawData 面板） | `val?.ToString()` | 同上 |
| `EntityEditorDocument` :312（XML 编辑页片段） | `value?.ToString()` | 同上 |
| `ReferenceInspectorContent` :77（Peek 面板属性） | `prop.GetValue(entity)?.ToString()` | 同上 |
| `DataExportService` :239-249（CSV/XLSX 导出） | `val is string refStr`（**过时类型判断**，ReferenceList 时代前写的，恒 false）+ `val?.ToString()` | `ReferenceText.GetRawString` + 修复分支 |
| `ModGameDataTabsView` :958（复制行）/:1253（CSV 导出到桌面） | `prop.GetValue(entity)?.ToString()` | 同上 |
| `EncounterEntityVisualizer` :1403（FindTriggers） | `t.EncounterId.RawText == ...`（RawText 在 Add 后可过期） | `t.EncounterId.ToRawString(null) == ...` |

**新增回归测试 `VisualizerReferenceConsistencyTests`（+4）**：RecordingReferenceResolver 记录 `LookupRef<T>` 收到的 rawId，断言 EncounterTrigger（EncounterId + HexTypes 段）/ DmcPlace / HexType 的徽章收到**干净 id**（`123`）且不含 `[123]`。

**测试**：499→**503/503**（EntityEditor 38→42）。

## 已知限制（本轮未做）

- Doc 21 §7 P2（徽章内联展开）/ P3（伤害堆叠条）/ P5（数值上下文）/ P7（动作按钮栏）——用户未选，留后续
- Value Editor 徽章的「悬停预览」在 DataGrid 单元格上不生效（DataGrid 用自己简化的 Ctrl-Hover tooltip；跨插件共享 BuildRefTooltip 需提 UI.Common，留后续）
- `field_descriptions.json` 是 Doc 38 的快照；Doc 38 更新后需重跑 `artifacts/gen-field-descriptions.js` 再提交

## 真机验证（建议）

- 打开合并视图 → 任意数据表列头悬停：显示字段含义 + 实测值域（不再只显示属性名）
- 打开实体详情 → Value Editor：每行字段名悬停有解释；引用徽章 Ctrl+Click 跳到目标实体、Ctrl+右键 Peek、悬停显示目标摘要面板
- Visualizer：Encounter 的 Give Item / Loot 徽章、Recipe 的 Hidden 徽章；Creature 的 EncounterIds 徽章解析到 Encounter
- Hero 图片（DataFile/DmcPlace/CampType/Map/Encounter）点击放大
- `show` CLI 命令 / MCP `entity://` / CSV 导出：引用列输出 `16,46` 原文
