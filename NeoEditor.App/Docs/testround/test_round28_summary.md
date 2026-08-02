# 架构测试第28轮 — 文档/字段订正 + 引用功能修复（Doc 37/38 落地）

> 日期：2026-08-02 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 12.1 / ProDataGrid 12.0.4)
> 上承：[test_round27_summary.md](test_round27_summary.md)（AI 图片工作台完善 + 花屏修复）
> 上下文：本轮开工前新增 [37-reference-column-semantics.md](../37-reference-column-semantics.md)（引用列值级语义）与 [38-full-field-reference.md](../38-full-field-reference.md)（全字段实测参考）两份权威文档，揭示代码三处不一致

---

## 背景

Doc 37/38 是对原版 `data/*.xml` + NSE mod 数据全量扫描的权威实测文档。二者与现有代码的差异构成本轮三块工作：

1. **文档订正**：Doc 20（581 行字段参考）有 16 处与实测冲突；R16/Doc 37 的 `0:` 前缀语义订正**已内嵌完成**
2. **字段订正**：实体模型 `[ReferenceField]` 标注存在误标/缺标（6 处）
3. **引用功能**：Doc 37「与代码现状的偏差」6 条

**用户确认范围**：图片列只标注不拾取；aTreasures 完整架构支持（多分隔符 + 三段 pattern）；Doc 20 头部汇总 + 正文 ⚠️ 标注。

目标：让实体标注、引用解析/序列化/导出/索引行为与实测语义一致，且**往返无损**。

---

## A. 引用核心修正（Phase 2，底层先行）

### A1 修 `ToString()` 损坏（偏差5）— 4 处 ⭐

- **根因**：对 `ReferenceList<T>` 调 `.ToString()` 输出 `[a, b]`（`ReferenceList.cs:129`）而非原文
- **4 个损坏点**：
  - `XmlParser.Export`（App/Helper/XmlParser.cs）→ `FormatValue` 末行 `value.ToString()` → 引用列导出为 `[16, 46]`
  - `ReferenceIndex.BuildReverse`（Infra/Services/ReferenceIndex.cs:170）→ 反向索引提取垃圾 id
  - `ReferenceIndex.AddEntity`（:393）
  - `ReferenceResolver.BuildReverseIndexAsync`（App/Helper/ReferenceResolver.cs:272）
- **修复**：公共解法 `value is ReferenceList<IReferenceEntry> rl ? rl.ToRawString(refAttr.Separator) : value?.ToString()`；XmlParser 改走 `_refSerializer.Serialize`

### A2 复合键 `86.6` 解析（偏差6）

- **根因**：`ReferenceIndexService.LookupByNsComposite` **已实现但无调用方**（死代码）；`LookupEntityId` 对 `86.6` 只走 `LookupByNs(pk="86.6")` → miss
- **内存版 `ReferenceIndex`**：新增 `_mergedCompositeIndex (Type,Gid,Sid)` + `_nsCompositeIndex (Type,Ns,Gid,Sid)`，`BuildAsync` 对含 GroupId/SubgroupId 实体（ItemType）填充；`Lookup` 在 ns/无前缀两分支后识别 `.` 拆 gid/sid
- **磁盘缓存 v7→v8**：持久化复合索引（`SaveToDisk`/`TryLoadFromDisk`/`IndexDiskData` + `NsCompositeEntry`/`MergedCompositeEntry`）
- **`LookupEntityId`**：pk 含 `.` → `LookupByNsComposite`；无前缀 + sourceNs 同样尝试
- **`LookupRef<T>` fallback**：无前缀非 int 时按 G.S + 最高 ModId 匹配

### A3 Bracket 参数保留（偏差4）

- **根因**：`DeserializeBracket` 丢弃逗号后内容；`BracketFormat.ToRawString` 输出 `[id` 无参无右括号 → `[-137,0,0]` 往返变 `[-137]`
- **修复**：`BracketFormat` 加 `P1`/`P2`（原始串，可为 `0.5`）+ `RawSegment`
  - ⚠️ **踩坑**：列分隔符是 `],[`，边界括号**非对称**（首段 `[-137,0,0`、末段 `146,0,0]`），全括号条目 join `],[` 会双括号 `]],[[` → 存原始段文本 `RawSegment` 保往返，`Entity`/`P1`/`P2` 供解析/显示；picker 走字符串路径无陈旧问题
- `ReferencePattern.BracketIdPattern` 加 `FormatExtraInfo`（`,{p1},{p2}]`）

### A4 aTreasures 多分隔符 + 三段（偏差2b/3）⭐

- **根因**：`Pattern="{id}x{mult}"` 只留前两段；`Separator=","` 把 `A,B|C|D` 的 `B|C` 当一段整体 → `DeserializeIdXMult` 按**最后一个** `x` 切得稀碎（数据损坏）
- **`ReferenceFieldAttribute.OrSeparator`**（新属性，`|` 绑定比 `,` 更紧）
- **`IdXMultXQtyFormat`**：`Entity` + `string Prob` + `string? Qty`（**原始串保真**，`1.0`≠`1`、`5-9` 不漂移；qty 缺省=1）
- **`OrGroupFormat`**：`Alternatives` 列表，`ToRawString` 以 `|` join
- **serializer**：`DeserializeSegment` 加 `"{id}x{mult}x{qty}"`；`DeserializeTopLevelPart` 先按 `,` 切顶层、段含 `|` 再切包 `OrGroupFormat`
- **`ReferenceParser.Parse`/`ExtractIds`**：`SplitSegments` 按 OrSeparator 展平（反向索引逐叶）
- **`ReferenceFieldEditor.GetBaseEntityRef`**：`OrGroupFormat`/`IdXMultXQtyFormat` 解包分支（UI 不崩）

### A5 AssignFormat 字符串值（aSwitchIDs 前置）

- **根因**：`{value}={id}` 的左侧状态名是**自由文本**（`Hood Off`/`On`），`AssignFormat.Value` 是 double → 解析成 0 → 序列化 `0=8.7` 数据损坏
- **修复**：`AssignFormat.RawValue`；非数值时保留原串（数值仍走 Value 路径，如 sprite 槽位 `20=`）

## B. 字段订正（Phase 3）

### B0 `ImageAsset` 标记类型
- `NeoEditor.Core/Model/Game/ImageAsset.cs`：纯标记类（非 IEntity，TargetEntityType 只需 Type；无实体加载 → Lookup 自然 miss → **反向索引零污染**）

### B1 误标修正（2 处）
| 字段 | 原 | 改 |
|------|----|----|
| `Creature.vEncounterIDs` | `typeof(Condition)` ❌ | `typeof(Encounter)` |
| `Encounter.nItemsID` | `TargetKey="{GroupId}.{SubgroupId}"` ❌ | 默认 `{Id}` |

### B2 缺标修正（3 处）
- `ItemType.aSwitchIDs`：加 `Pattern="{value}={id}"`（依赖 A5）
- `TreasureTable.aTreasures`：`Pattern="{id}x{mult}x{qty}"` + `OrSeparator="|"`（依赖 A4）
- `Condition.aThresholds`：`string` → `ReferenceList<IReferenceEntry>` + `[ReferenceField(Condition,";","{value}={id}")]`

### B3 图片列标注（8 处，只标注不拾取）
`string` → `ReferenceList<IReferenceEntry>` + `[ReferenceField(typeof(ImageAsset), TargetKey="{FileName}")]`：
`AttackMode.Image`/`CampType.ImageList`/`Creature.Image`/`DataFile.Image`/`DmcPlace.Image`/`Encounter.Image`/`ItemType.ImageList`(+`,`) /`ItemType.SpriteList`(+`,`,`{value}={id}`)
- 特例**不改**：`ItemType.vImageUsage`（索引列非文件名）、`maps.strName`（含内部网格名）

### B4 类型改动的涟漪
- `ItemTypeEntityVisualizer`：`it.ImageList.Split(',')`/`?? ""` → `it.ImageList.ToRawString(",")`（4 处）
- `Documents.cs:513` `dp.Image.Length` → `dp.Image.ToRawString(null)`；`ConditionEntityVisualizer`/`DmcPlaceEntityVisualizer` 插值改 `ToRawString`
- 其余 `LoadImage(x.Image)` / `IsNullOrWhiteSpace` / tuple 经 `ReferenceList` 隐式 `string?`（RawText）兼容

## C. 文档订正（Phase 1）

- **Doc 20**：头部加「⚠️ 2026-08-02 实测订正」章节（16 处差异表 + 指向 38）；正文 16 行 ⚠️ 标注（含 containertypes/factions 节级注）
- **Doc 37 §2.5**：补注 8 个图片列已标注 `[ReferenceField(typeof(ImageAsset))]`
- **Doc 38 附录 §A**：措辞「文档需订正（已列入待办）」→「已订正」（R16/37 §0.4 已内嵌订正标注）

## D. 测试（Phase 4，+28）

| 项目 | 新增 | 说明 |
|------|:--:|------|
| Core.Tests `ReferenceFieldAnnotationTests`（新文件） | +8 | 标注配置断言：vEncounterIDs→Encounter / nItemsID→{Id} / aSwitchIDs Pattern / aTreasures Pattern+OrSeparator / aThresholds / 8 图片列 TargetEntityType==ImageAsset + 属性类型 |
| Infra.Tests `ReferenceListSerializerTests` | +8 | aTreasures 三段/qty 缺省/OR 组往返；Bracket 参数往返（含 `0.5`）；aSwitchIDs 自由文本；aThresholds；Image `0:AModeSpearSharp.png`；SpriteList |
| Infra.Tests `ReferenceParserTests` | +3 | ExtractIds/Parse OR 展平；FromName 新 pattern |
| Infra.Tests `ReferenceFixRegressionTests`（新文件） | +2 | ReferenceIndex `86.6`/`0:86.6` 解析；BuildReverse 保真 |
| Integration.Tests `XmlParserExportTests`（新文件） | +1 | Export 引用列输出 `16,46` 原文（非 `[16, 46]`） |
| Core.Tests `ReferenceEntryTypesTests` | ±1 | BracketFormat `[211`→`[211]`（恒右括号，游戏格式） |

## 编译和测试

| 项目 | 结果 |
|------|:----:|
| `dotnet build NeoEditor.sln`（全量 22 项目） | **0 错误** ✅（仅 NU1701 XMLDiffPatch 预存警告） |
| 全量测试 | **471/471 ✅**（+28：Infra 150→164、Core 47→60、Integration 12→13） |

## 已知限制（本轮按计划排除）

- `battlemoves.strID` / `chargeprofiles.strItemID` / `hextypes.nCampItems` / `barterhexes.nRestockTreasureID`（int/float 型 `[未标]`，改 ReferenceList 需类型迁移，风险高）→ 文档已标语义，代码留后续
- `Condition.aEffects` / `Encounter.aResponses`（复合列，37 §5 特殊处理，非标准引用模型）
- aTreasures OR 组的**拾取器内编辑**不做（显示/往返保真已保证；picker 编辑走字符串路径天然保留 OR 组）

## 真机验证（建议）

导入真实 mod（NSEaid 等）→ 打开合并视图 → 检查：
- TreasureTable.aTreasures：OR 组（`A,B|C|D`）三段完整、导出 XML 原文一致
- BattleMove 括号列：`[-137,0,0],[146,0,0]` 参数保留
- ItemType.aSwitchIDs：`Hood Off=8.7` 徽章显示自由文本状态名
- 图片列：徽章显示 `0:AModeSpearSharp.png` 等，导出保真
- 复合键：Ctrl 导航 / Peek 到 ItemType `86.6` 可解析
