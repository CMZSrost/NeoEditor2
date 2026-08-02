# 架构测试第30轮 — 字段解释 + 可视化 + 引用解析跳转（530/530）

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
| 全量测试 | **517/517 ✅**（482→517，+35） |

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

## 追修 2（用户报告）：AttackMode Conditions 解析失败 + 「三者引用不一致」根治 ⭐

> 用户：「attackMode 的 Conditions 这块的解析好多没解析成功」「经常看见 Data Table 和 Value Editor 和可视化这三者的引用不一致，这是为什么？明明应该同一走 hostService 拿的」

### 为什么三者会不一致（根因）

**解析后端已统一**（全部走注入的 `IReferenceResolver` → 内存 `ReferenceIndex`），真正的差异在**调用方喂给索引的数据形状不同**：

| 入口 | 传给 `index.Lookup` 的 rawId | 结果 |
|------|------------------------------|------|
| DataTable 显示/导航 | 先 `ReferenceParser.ExtractRawId`（`67x0.05`→`67`） | ✅ 命中 |
| Value Editor 徽章 | 先 `ExtractRawId`（`baseRef.ToRawString()` 保留 ns/复合键） | ✅ 命中 |
| Visualizer 徽章（`LookupRef`） | **完整段 `67x0.05` 直接传** | ❌ 主路径 miss，靠 fallback 兜底 |

根因：`ReferenceIndex.Lookup` **不做 Pattern 提取**，要求调用方自己先提取；`LookupRef` 没提取（fallback 才提取），且 fallback 与主路径语义分叉。**修 10 个调用方不如修 1 个后端**。

### 根治：段归一化下沉到索引内部（统一语义）

`ReferenceIndex.Lookup` 现在**内部**按源字段的 `[ReferenceField].Pattern` 提取 id 后再查：

- 新增 `_entityIdToType`（Build 时填充：EntityId → 源实体类型）+ `_patternCache`（(sourceEntityId, propertyName) → Pattern）
- `Lookup` 开头：`ResolvePattern(sourceEntityId, propertyName)` → `ReferenceParser.ExtractRawId(rawId, pattern)` → 后续逻辑不变
- **幂等**：已提取的 id（`67`）再提取不变 → DataTable/Value Editor 路径零行为变化；完整段（`67x0.05`/`-115x1.0`/`[155,0,0]`/`Hood Off=8.7`/`1x2`/`NSE:67x1.0`）现在直接命中
- `Clear()` 同步清缓存
- **顺带修 `BracketIdPattern.ExtractRawId` 负号 bug**：`[-137,0,0]` 原提取 `-137`（非 int → miss，DataTable 也受影响）；现与 IdPattern 一致去负号 → `137`（negated 语义）

效果：**所有正向解析入口（DataTable / Value Editor / Visualizer / Picker）传什么都能解析，同一后端同一语义**——即用户要的「统一」。

**新增测试 `ReferenceSegmentNormalizationTests`（+9，Infra.Tests）**：完整段 `67x0.05`（AttackMode 场景）/ 负值段 `-115x1.0` / 幂等 `67` / bracket `[155,0,0]`+`[-137,0,0]` / `Hood Off=8.7` 复合键 / `11=211` / `1x2`（Recipe Tools→Ingredient）/ `NSE:67x1.0` / 全局查找。**全量 512/512 通过**（503→512，构建 0 错误；Infra 166→175）。

## 追修 3：全链路引用解析审计（3 个 Explore agent + 汇总修复，517/517）⭐

> 用户：「检查下全部的引用解析问题吧，看看还有啥没整」——3 个并行 agent 审计 DataViewer / EntityEditor / 解析核心，发现 ~30 项，本轮修复 21 项：

### 显示/解析（用户可感知）
| # | 问题 | 修复 |
|---|------|------|
| P1 | 多字符 Separator（`"],["`）用 `separator[0]` 拆 → BattleMove bracket 条件被切碎成 `"[155"、"0"、"0"` | `ColumnTemplateFactory` 多值分支按整串 Separator 拆顶层段 + bracket 段保持完整 + OR 组用 `OrSeparator` |
| P3/E1 | 显示/徽章/Peek 不查 SecondaryTargetEntityType → aTreasures 嵌套 TT 显示原文 | `FormatSegmentDisplay`/单值分支/`CreateBadge`/`OnPeekClick` 主类型 miss 后查 secondary |
| C1/C2 | AttackMode 徽章 `xx1.0`、Creature 徽章 `== 100` 双拼接 | `$"{Subject} {extra}"`（extra 已含 x/= 前缀） |
| P15/L1/L2 | bracket 显示丢 P1/P2 参数；`ParseWithPattern` 无 `"[{id}"` 分支；`ParseMultiplierReversed` 丢乘数；`NSE:[-137,0,0]` 解析错乱 | `BracketIdPattern.FormatDisplay`（`Subject (155, 0, 0)`）+ ns 前缀剥离；`ParseWithPattern` 加 bracket 分支；`ParseMultiplierReversed` 返回乘数；`MultXIdPattern.FormatDisplay` 显示乘数 |
| H4/P11 | `NavigateTo` 用 `ns=""` 查 SQLite（行存 `"0"`）→ 兜底永远 miss | 改查 `"0"`（保留 `""` 兜底） |
| P9/H3 | MCP `ResolveReferences` 工具 + `ReferenceIntegrityRule` 校验仍吃 `[a, b]` 损坏格式 | 改 `ReferenceText.GetRawString` + 整串 Separator 拆分 |
| P7 | 「Find References」右键手写扫描（损坏格式 + `separator[0]`） | 改走内存 `Index.ReverseLookup`（无索引时正确回退扫描） |

### 索引/后端
| # | 问题 | 修复 |
|---|------|------|
| H1 | SQLite 反向索引在内存索引**之前**构建 → 首次加载 `reference_reverse` 全空 | `MergeStore.Index.BuildAsync()` 提前；`LookupRefByRawId` fallback 改按 MergedId/pk 匹配（原比较哈希 EntityId 恒 false） |
| H2/P4 | Browser 模式：`SetBrowserStore` 在反向构建之后（用错 store）+ 内存索引从不构建 | `BrowserIndexService` 先建内存索引 + 先发布 store；`LookupRefByRawId` 加 `storeOverride` 显式 store |
| M3 | `LookupRef` fallback ns 分支缺复合键（`NSE:86.6`）与 `NamespaceToModName` 映射 | 补 `SameNsMapped` + ns 复合键扫描 |
| M5 | 复合索引填充所有 ItemType（含无效 `(0,0)`）→ 裸 `0.0` 解析任意实体 | 填充/查找跳过 `gid==0 && sid==0` |
| M2 | 序列化往返尾零丢失（`211x1.0`→`211x1`）→ 导出 diff 噪音 | `IdXMultFormat`/`MultXIdFormat` 加 `RawMult` 原文保留（同 `AssignFormat.RawValue` 模式） |

### 编辑/写回（静默丢数据）
| # | 问题 | 修复 |
|---|------|------|
| A1 | XML 标签页对引用列 `ValueConverter.Convert` 抛异常被吞 → 引用编辑静默丢弃 | `ApplyXmlToEntity` 对 ReferenceList 字段走 `IReferenceListSerializer.Deserialize`（构造注入 serializer） |
| A2 | WAL（`BatchEditCommand`/`EditCellCommand`/`AddEntityCommand`）把 ReferenceList 序列化成条目数组，重放无法还原 → 引用编辑重启后回滚 | 序列化统一存 raw string（`ReferenceText.GetRawString`）；`DeserializeValue` 带 property 上下文经 serializer 还原 |
| P2 | DataGrid 引用列编辑控件直接绑定 ReferenceList → 编辑无效 | 新 `ReferenceListConverter`（读 raw text / 写回 serializer）+ 编辑模板接线 |

### 清理
| # | 问题 | 修复 |
|---|------|------|
| M1 | `InvalidateRawText` 是 no-op → Add 后 RawText 静默旧值（30 处 visualizer 隐患源头） | 置空（serializer 的 Deserialize/Serialize 末尾恢复 RawText） |
| C3 | `VisHelperService.RefNode` 死代码条件写反（`\|\|` → `&&`） | 修正 |
| L3 | `_display` 名称缓存永不失效 → 编辑 Subject 后单元格显示旧名 | `ClearDisplayCache()` + `ClearLookupCache` 接线 |

**新增测试（+5）**：`ReferenceListConverterTests`（DataViewer，读原文/写回/空输入）+ `CommandSerializerReferenceTests`（Infra，BatchEdit/EditCell 引用列 WAL 往返）。

**已知限制（审计后未做）**：D 类 visualizer RawText 隐式转换隐患 ~30 处（M1 已把危害从「静默旧值」降为「空」，正常加载/编辑流程不受影响）；P5/P6 搜索/过滤搜不到引用列（功能缺口非损坏）；ReferencePicker 装饰 UI（A3/A4）为死 UI 未接线；P10 增量索引 API 无调用点（编辑后反向面板过期，下次 Reload 恢复）；P12 IndexTableViewModel SelectRow 死代码；M8 ns 大小写敏感（当前数据无感）；M4 SQLite 无 MergedId 列（Browser 模式裸 id=基础 ns 语义）。

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

---

## 追修 4：打开实体必脏（dirty-on-open 回归修复）⭐

> 用户：「出问题了，每次打开一个实体，都会被标记dirty」——合并视图（ProfileId=-1）下**每次打开任何实体都显示已修改**。

### 根因链（全链路确认）

1. 合并视图的 WAL 命令（`BatchEditCommand`/`EditCellCommand`/`AddEntityCommand`）通过 `OnCommandPersistAsync` 持久化到 **`("mod", modId)` / `("game", 0)`**（按 mod 分桶）
2. `MergeSave` 保存后调用 `ClearWorkspaceAsync()`，但旧实现取 `GetPersistenceTarget()` = **`("", -1)`**，且 `if (targetId < 0) return;` **直接返回 → WAL 从未清理**
3. 每次打开 profile → `RestoreMergeCommandsFromLogAsync` 把 `("mod", X)` 所有历史命令**全部重放** → 填充 `EditStore.EditedCells` → `PushEditStateToGrid`/`MarkTabsDirtyFromEditedCells` → 相关实体打开即 dirty
4. 追修 3 的 A2（引用编辑 WAL 重放从「失败跳过」变为「成功重放」）把这条路径从「静默丢数据」变成「可见的必脏」，暴露了此 bug
5. 佐证：普通 profile（非合并视图）`GetPersistenceTarget()` 有效 → 保存后正常清理 → 无此问题；KV 实体加载路径本身干净（探测测试 1/2 通过）

### 修复

`ModGameDataTabsView.ClearWorkspaceAsync()`：目标从单一 `("", -1)` 改为**按 profile 枚举全部实际持久化目标**——

- 普通 profile：保留原 `GetPersistenceTarget()` 语义
- 合并视图（ProfileId=-1）：额外清理 `("game", 0)` + 每个已加载 mod 的 `("mod", ModId)`（去重、逐目标 try/catch，单个失败不影响其余）
- 清理后重置 `_persistSequence` / `_commandsSinceSnapshot` / `UpdatePersistenceDebugInfo()`

**新增回归测试 `OpenEntityDirtyProbeTests`（+3，EntityEditor.Tests）**：
1. 打开实体不标记当前文档 dirty（KV 路径）
2. 打开实体不产生任何新 WAL 命令（`persist_sequence` 不变）
3. `LoadEntity` 不改动实体引用值（加载前后 `EncounterIds.ToRawString(",")` 一致）

### 结果

**全量 519/520 通过**（构建 0 错误）。唯一失败为 `ImageTools.RefreshModMessage_TriggersAutoRefresh` 的**已知 flaky**：测试轮询 `ObservableCollection<Sources>` 时 VM 在异步 refresh 续体里 `Clear/Add`（`Collection was modified` 竞态）——与本轮改动无关（ImageTools 目录零改动，单测隔离 3/3 通过），留后续加锁/快照轮询。

> 注：若用户环境存在追修前残留的旧 WAL（保存后未被清理），首次打开 profile 仍会重放一次旧命令；**重新保存一次即可清空**，之后恢复正常。

---

## 追修 5：dirty-on-open 真根因（QuickSave 从不清理 WAL）⭐

> 用户复测：「不行啊，现在双击打开实体，依旧会被标记dirty，保存和重启好几次了」——追修 4 只修了 `MergeSaveAsync`（全量保存+XML 导出）路径的 `ClearWorkspaceAsync`，但**主保存路径（💾 Save 按钮 + 30s 自动保存）走的是 `QuickSaveAsync`，它从不清理 WAL**。

### 真根因（全链路确认）

1. 合并视图（ProfileId=-1）下，游戏本体（ModId=-1）实体可通过 KV/XML 编辑器编辑（`OnEntityFieldEditsFromXml` 显式允许）→ `BatchEditCommand(SourceModId=-1)` → WAL 持久化到 **`("game", 0)`**
2. 💾 Save → `SaveRequestedMessage` → `QuickSaveAsync`：
   - `SaveAllAsync` 把**整个 per-profile dirty 集合**（含游戏实体）写入 game.db ✓
   - 但只对 `ModId > 0` 的 mod 更新 snapshot marker（`savedModIds` 过滤 `id is > 0`）——**`("game", 0)` 的 marker 从不推进**
   - **WAL 行从不删除**
3. 重启 → `RestoreMergeCommandsFromLogAsync` 读 `("game", 0)`（snapshot=-1 → 全部行）→ 重放 → `EditStore.EditedCells` 填充 → `MarkTabsDirtyFromEditedCells` → `WorkspaceSession.MarkEntitiesDirty` → 打开相关实体即 dirty
4. **无论保存多少次都一样**——QuickSave 既不删行也不推进 game marker → 每次重启必重放必脏 ✓ 与用户复测完全吻合
5. 追修 4 的 MergeSave 清理只在「全量保存+导出」路径生效，用户用的是 💾 Save（QuickSave）→ 修复从未执行

**附带同类洞**：ModId=0（NSEaid 合法 mod）——WAL 持久化到 `("mod", 0)`，但 QuickSave marker 过滤 `id > 0` 跳过 0、restore 过滤 `id > 0` 也跳过 0 → NSEaid 的 WAL 永不恢复也永不清理（泄漏）；`EntityDbSavedMessage` 处理器 `if (m.ModId < 0) return;` → 实体文档单独保存游戏实体后，game marker 也不推进 → 同样必脏。

### 修复（4 处）

| # | 位置 | 修复 |
|---|------|------|
| 1 | `QuickSaveAsync`（Operations.cs） | 保存成功后调 `ClearWorkspaceAsync()`（与 MergeSave 一致）——**删除全部持久化目标的 WAL 行 + snapshot**。安全性：`HostService.ExecuteAsync` 对每个命令的受影响实体调用 `_session.MarkEntitiesDirty`，而 `SaveAllAsync` 保存整个 dirty 集合 → 每条 WAL 行的实体必在本次保存内 → WAL 已冗余，删除无损。`savedModIds` 过滤改 `>= 0`（含 NSEaid，用于 `UpdateLastModifiedAsync`） |
| 2 | `ClearWorkspaceAsync`（axaml.cs） | mod 过滤 `id > 0` → `id >= 0`（含 ModId=0） |
| 3 | `RestoreMergeCommandsFromLogAsync` / `RestoreMergeUndoStackFromLogAsync`（axaml.cs） | 同上 `id >= 0`——与 `EntityDbSavedMessage` 注释（"ModId=0 是合法 mod，跳过会导致 WAL snapshot 过期、重启重放重脏"）对齐 |
| 4 | `EntityDbSavedMessage` 处理器（axaml.cs） | `ModId < 0` → 更新 `("game", 0)` marker（镜像 mod 路径），不再直接 return |

**新增测试 `WorkspacePersistenceClearTests`（+3，Infra.Tests）**：
1. `PersistedCommands_ReplayAfterRestart_WhenNeverCleared`——基线证明失败模式（("game",0) 无 marker 时重启必重放）
2. `ClearWorkspace_RemovesCommandsAndSnapshot_ForTarget`——清理后目标 `LoadCommands` 为空 + snapshot=-1，**其他目标不受影响**
3. `ClearWorkspace_ModZeroTarget_IsCleared`——ModId=0 目标可清理

### 结果

**全量 523/523 通过**（构建 0 错误；519→523，+3；此前 ImageTools flaky 本轮也通过）。真机预期：更新后首次打开 profile 会把**残留旧 WAL 重放一次**（值已入库，幂等），之后 💾 保存一次即彻底清空——从此打开实体不再被标脏。

> **追修 4 订正**：追修 4 的根因链（MergeSave 不清 WAL）方向正确但**不完整**——主保存路径 QuickSave 才是用户实际触发路径，且其 marker 机制漏了 `("game", 0)` 与 ModId=0 目标。追修 4 的 `ClearWorkspaceAsync` 多目标改造仍保留（MergeSave 路径 + 本追修复用）。

---

## 追修 6：保存本身就是空操作（实体从不进 HostService 缓存）⭐

> 用户复测（附完整日志）：「还是不行 我是直接ctrl+s来做的」——日志实锤：**每次 Ctrl+S 都弹 `<Quick Save> No mod entities to save`**，一个实体都没存进去。

### 日志铁证

```
[23:23:57] [Restore] mod:4 replaying 9 commands → [MarkTabsDirtyFromEditedCells] 4 edited entities
[23:23:59] <Quick Save> No mod entities to save. Ensure mods are loaded in the profile.   ← Ctrl+S
[23:24:00] [XmlEdit→WAL] executing BatchEditCommand … [Persist] mod:4 seq=10               ← 用户编辑
[23:24:06] <Quick Save> No mod entities to save.                                           ← Ctrl+S 仍空保存
[23:24:08~11] [Persist] mod:4 seq=11~14                                                    ← WAL 持续增长
[23:24:21] <Quick Save> No mod entities to save.
```

追修 5 的 `ClearWorkspaceAsync()` 在 `savedEntityIds.Count == 0` 的 early-return **之后**，保存为空 → 从不执行 → WAL 永不清理 → 重启必重放必脏。**为什么保存为空？这才是真根因：**

### 根因链（R26 v2 重构遗留缺口）

1. R26 v2 把缓存驱动改为 `IEditorCommand.GetCacheDelta()` 通用 delta（默认空实现），但**只给 Add/Delete/Replace 实现了，`BatchEditCommand`/`EditCellCommand` 没覆写** → 编辑命令的 delta 恒空 → `ApplyCacheDelta` 空转 → **编辑的实体从不进入 `_entityCache`**
2. `SaveAllAsync` → `PersistEntitiesAsync(dirty)`：`_entityCache.TryGetValue` **miss** → `entities.Count == 0` → `_session.RemoveDirtyEntities(entityIds)`（**静默丢弃 dirty**）+ 返回空结果
3. `QuickSaveAsync`：`savedEntityIds.Count == 0` → 提示「No mod entities to save」+ early return → WAL 不清理
4. 合并视图加载（ReloadMergeTabsAsync）也**从不把实体注册进缓存**（`AddEntityToCache` 存在但无人调用）→ 恢复重放的实体、未编辑过的实体同样 miss
5. 于是：**合并视图里所有编辑（单元格/XML/KV）从未真正落库**，只存在于内存 + WAL；重启 → 重放 → 标脏 → 打开即 dirty。与保存次数无关 ✓

### 修复（3 处 + 测试）

| # | 位置 | 修复 |
|---|------|------|
| 1 | `BatchEditCommand` / `EditCellCommand` | 实现 `GetCacheDelta()`/`GetUndoCacheDelta()`——按 EntityId 去重 upsert 受影响实体（原地变更，缓存引用天然最新）。**编辑即入缓存**，任何编辑路径（含 MCP/CLI）都能保存 |
| 2 | `ReloadMergeTabsAsync`（Data.cs） | 加载完成后遍历所有 tab 的 `SourceCollection` 调 `_hostService.AddEntityToCache(e)`——**合并视图所有实体（含 WAL 重放目标）进缓存**，杜绝 miss |
| 3 | `PersistEntitiesAsync`（HostService.cs） | 缓存 miss 丢弃时输出 **`Log.Warning`**（不再静默）——同类回归可立刻从日志发现 |

**新增测试（+3，Infra.Tests）**：
1. `Execute_BatchEditCommand_UpsertsEntityIntoCache_And_SaveAll_Persists`——执行 BatchEdit 后 `GetCachedEntity` 命中 + `SaveAllAsync` 真正保存 + dirty 清空
2. `Execute_EditCellCommand_UpsertsEntityIntoCache_And_SaveAll_Persists`——同上 EditCell
3. `SaveAll_Drops_DirtyEntity_Missing_From_Cache_Without_Saving`——护栏行为固化（缺缓存丢弃不崩）

### 结果

**全量 526/526 通过**（构建 0 错误；523→526，+3）。真机预期：更新后**首次 Ctrl+S 会真正保存**（不再弹「No mod entities to save」），同时清掉 WAL；此后重启打开实体不再被标脏。残留旧 WAL（mod:4 的 9+5 条）会在首次保存时一并清除。

---

## 追修 7：双击打开实体本身就在写 WAL（XML 自动 Apply 假 diff）⭐

> 用户复测（第二份日志）：「还是不行，我什么修改都没做，只是双击了下就被标记dirty了」——追修 5+6 已生效（日志实锤：`[QuickSave] cleared WAL, saved 5 entities to DB`，WAL 真的清了），但**保存后 1 秒、双击实体时**：

```
[23:35:05] [DockFocus] tab activated: AttackMode 893b…   ← 双击打开实体
[23:35:05] [XML-Apply] Phase2 done: 3 diffs for entity 893b…   ← 打开即自动 Apply！
[23:35:05] [XmlEdit→WAL] executing BatchEditCommand (3 edits) → [Persist] mod:4 seq=1
```

### 根因链（日志实锤）

1. **打开实体 = 自动触发 XML Apply**：`EntityEditorView.XmlEditor.TextChanged` 150ms 防抖 → `ApplyXmlToEntityCommand`——文档打开时程序化设置 `XmlContent.Text`（`OnEntityChanged`/构造函数）也会触发 `TextChanged` → 防抖自动 Apply。`LostFocus → FlushXmlChanges` 同样无条件 Apply
2. **`ApplyXmlToEntity` 的 diff 比较 `Equals(oldValue, newValue)` 对 `ReferenceList` 恒 false**（无值相等性）——AttackMode 恰好 3 个引用列（AttackerConditions/ChargeProfiles/Image）→ **每次 Apply 必出 3 个假 diff** → `EntityFieldEditsMessage` → `BatchEditCommand` → WAL + `EditStore` + dirty
3. **字符串列 null ↔ "" 往返不对称**：DB NULL 经 XML 片段渲染为 `""`，解析回 `""` ≠ null → 假 diff
4. 于是：**只要打开过实体就写 WAL**——追修 5/6 修好了保存和清理，但每开一个实体又产生新 WAL → 重启又重放又脏

### 修复（2 处）

| # | 位置 | 修复 |
|---|------|------|
| 1 | `EntityEditorView.axaml.cs` `TextChanged` | **仅 `IsXmlFocused`（用户在 XML 编辑器内打字）才防抖自动 Apply**——程序化设文本（初始加载/RefreshXml）不再触发 |
| 2 | `EntityEditorDocument.ApplyXmlToEntity` | 引用字段按**原文比较**（`ReferenceText.GetRawString`，同 KV 编辑器语义）；字符串 null ↔ "" 归一化相等；**只对 changed 属性 SetValue**（不再无条件替换实例） |

**新增测试 `EntityEditorDocumentXmlApplyTests`（+4，EntityEditor.Tests）**：
1. `ApplyXml_WithUnchangedGeneratedFragment_ProducesNoEdits`——打开流（生成片段→回灌）零 diff，`doc.IsDirty=false`，引用值原样
2. `ApplyXml_WithRealReferenceChange_ProducesEditAndMarksDirty`——真改引用列 → 1 个 edit + dirty + 实体更新
3. `ApplyXml_NonReferenceField_Unchanged_NoEdits`——重复 Apply 幂等
4. `ApplyXml_RealStringChange_ProducesEdit`——真改字符串列正常生效

### 结果

**全量 530/530 通过**（构建 0 错误；526→530，+4）。真机预期：双击打开实体**不再产生任何 WAL 命令**（日志不再出现 `[XML-Apply] Phase2 done: N diffs`），打开即干净；真正在 XML 里改动才写 WAL。

### ✅ 真机验证通过（用户确认 2026-08-02）

**「每次打开实体都被标记 dirty」已彻底解决**。dirty-on-open 全链路四连根因（追修 4→7）全部关闭：

| 追修 | 根因 | 修复 |
|------|------|------|
| 4 | MergeSave 的 WAL 清理 target 不匹配（`("",-1)` vs `("mod",X)`/`("game",0)`） | `ClearWorkspaceAsync` 枚举全部实际持久化目标 |
| 5 | 主保存路径 `QuickSaveAsync` 从不清理 WAL，`("game",0)`/ModId=0 marker 永不推进 | QuickSave 保存后清 WAL；restore/clear 过滤 `id>0`→`id>=0`；`EntityDbSavedMessage` 补 game 分支 |
| 6 | R26 v2 缓存 delta 缺口：`BatchEditCommand`/`EditCellCommand` 不实现 `GetCacheDelta` → 编辑实体不进缓存 → 保存空操作 → WAL 永不清理 | 编辑命令实现缓存 delta + 合并加载 `AddEntityToCache` 全量 seed + 缓存 miss 告警日志 |
| 7 | 打开实体自动触发 XML Apply，`ReferenceList` 的 `Equals` 恒 false + null↔`""` → 假 diff 写 WAL | `TextChanged` 仅用户编辑触发；`ApplyXmlToEntity` 引用字段按原文比较 + null 归一化 + 只 SetValue changed |

经验教训：**WAL 设计上用「marker 推进」替代「保存即清空」反复出问题**（追修 5 的 game:0/mod:0 漏网），最终统一为「保存 = WAL 清空」语义；「假 diff」类问题（无值相等性类型、null↔"" 往返）需要按原文/归一化比较，不能依赖 `Equals`。
