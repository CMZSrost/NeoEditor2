# 架构测试第34轮 — Raw Data 审计视图（分组 + 类型化渲染 + 统一引用解析）（805/805）

> 日期：2026-08-06 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 12.1)
> 上承：[test_round30_summary.md](test_round30_summary.md)（字段解释 + 可视化 + 引用解析跳转）
> 本轮目标：**把「全字段解析」（Raw Data）升级为分组审计视图，并与 Detail 语义视图统一引用解析路径**
> 用户输入：阅读项目 → 对照 center 可视化（BuildDetail）与全字段解析（BuildRawDataTable）→ 设计更优 UI → 开工

---

## 现状诊断（开工前）

| 维度 | 现状 |
|------|------|
| Center 可视化 | `EntityEditorDocument`（Center DocumentDock）双 Tab：可视化 = `IEntityVisualizer.BuildDetail()`（25 个 visualizer）；XML 编辑 |
| 全字段解析 | `VisHelperService.BuildRawDataTable`：反射全部 `[Column]` 属性 → 130px\|* 两列 Grid；bool→`0/1`、引用列→原文 + `LookupSubject` hover；100 字符截断；字段解释 ToolTip（Doc 38） |
| 核心问题 1 | **两条平行数据视图无连接**：Detail 精选+语义，Raw Data 全量+原始；visualizer 条件过滤漏掉的字段**无任何提示**（静默隐藏） |
| 核心问题 2 | **引用解析双路径**：Detail 走 `LookupRef<T>`（上下文感知），Raw Data 走 `LookupSubject`（display 索引）→ 两处解析结果可能不一致（round30 用户反馈） |
| 核心问题 3 | Raw Data 形态单一：不分 `FieldGroupMetadata` 组、无类型化渲染，与 KeyValue 编辑器分组不一致 |

---

## A. Raw Data 升级为「分组审计视图」（VisHelperService.RawData.cs，新增 partial 文件）

### A1 按 FieldGroupMetadata 分组

- `FieldGroupMetadata.GetSections(Type)`（Core 新增，与 KeyValue 编辑器同源分组）→ 未映射类型回退默认组「属性」
- 每组：**组头条**（组名 + `N 字段 · M 有值` 统计徽章，Tag=组名便于测试/定位）+ 两列 Grid（130px 列名 | 值）
- 组顺序 = metadata 作者顺序，与 KeyValue 编辑器完全一致

### A2 类型化行渲染

| 字段类型 | 渲染 | 说明 |
|---------|------|------|
| bool | `0`/`1` 原样 + 颜色编码（1=绿 #2E7D32 / 0=灰 #999） | 保留原始值保真（审计），颜色辅助理解 |
| 引用列 | **逐段解析为可点击徽章**：解析成功 → 绿徽章（Subject + Pattern 附加信息如 `(x1.0)`）+ P6 hover 预览 + Ctrl+Click 跳转 / Ctrl+RMB Peek + `原始值: xxx` 提示；解析失败 → **琥珀警示徽章**（#FFF8E1/#B45309）保留原文 | 与 Detail 徽章视觉/交互一致 |
| 普通文本 | 100 字符截断 + hover 全文 ToolTip | 截断时挂全文 |
| 空值 | `(empty)` 灰色 | 不变 |

### A3 统一引用解析入口 ⭐

- 新增 `ResolveRawSegment`：通过 `MakeGenericMethod` 调用 **`IReferenceResolver.LookupRef<T>`**（Detail 徽章同路径，含 ReferenceIndex 上下文感知 + namespace/MergedId fallback + 次目标类型回退）
- **彻底移除 Raw Data 路径对 `LookupSubject` 的依赖** —— 两处解析不可能再不一致
- 引用列 ToolTip 预览复用 `BuildRefTooltip`（Doc 21 §7 P6）

### A4 Expander 头部审计统计

- 新 API `BuildRawData(entity)`：一体化的「Expander 头（带统计）+ 折叠体」，替代原来两个子元素（BuildExpander + Border）的手工拼接
- 头部标签：`原始数据  (24 字段 · 12 有值 · 2 个引用未解析)` —— 未解析引用数直接暴露在标题上
- `ComputeRawDataStats(entity)`：纯统计（测试/其他调用方用）；`BuildRawDataTable(entity)` 签名保持兼容

### A5 24 个 visualizer 调用点统一替换

- 所有 `var rawBody = new Border {...}; root.Children.Add(BuildExpander(...)); root.Children.Add(rawBody);` → `root.Children.Add(_vis.BuildRawData(<entity>));`（24 文件，96 行删除）
- AttackMode 的 `rawContent` 间接层一并消除
- Default visualizer 不受影响（不走 RawData 面板）

---

## B. 新增资源键（3 组 × 3 资源文件）

| key | en | zh |
|-----|----|----|
| `Vis.RawFields` | `{0} fields · {1} set` | `{0} 字段 · {1} 有值` |
| `Vis.RawUnresolved` | ` · {0} unresolved refs` | ` · {0} 个引用未解析` |
| `Vis.RawOriginal` | `Raw: {0}` | `原始值: {0}` |

---

## C. 测试（+6，805/805）

`RawDataTableTests`（EntityEditor.Tests）：
1. `BuildRawDataTable_ItemType_GroupsByFieldGroupMetadata` — 组头顺序 = FieldGroupMetadata 作者顺序（Tag 收集断言）
2. `BuildRawDataTable_RefColumn_ResolvesViaLookupRef` — 引用段走 `LookupRef<T>`（干净 id `"7"` 非 `"[7]"`），徽章显示 Subject
3. `BuildRawDataTable_UnresolvedRefSegment_AmberBadge` — 未解析段琥珀色 + 原文保留
4. `ComputeRawDataStats_CountsFields` — 总数/有值/未解析计数
5. `BuildRawDataLabel_FormatsStats` — 标签格式（含/不含未解析段）
6. `BuildRawData_ReturnsExpanderWithStatsLabel` — 一体化结构

⚠️ 踩坑记录：
- **Avalonia 12 `Brush.Parse` 返回 `ImmutableSolidColorBrush`**（非 `SolidColorBrush`）→ 断言须用 `ISolidColorBrush`
- **override 泛型方法不能重写约束** → stub 用 `where T : class` + `(T)(object)e`

---

## D. 测试稳定性修复 ⭐（既有 flaky 根因）

- **现象**：全量并行跑偶发失败（每次不同测试：`EncounterTrigger_HexTypesBadges`、`Badge_ResolvedRef_HasNavigationCursorAndHoverPreview`、ImageTools `Refresh_FindsImg...`）；EntityEditor 单项目 12/12 稳定；基线（HEAD，worktree 对照）14 次全量 0 失败 → 我的 6 个新 UI 测试放大了并行窗口
- **根因**：`KeyValueEditorFieldExplanationTests` 写入**共享状态** `Application.Current.Resources["Services"]`（:97/:133），其他 UI 测试经 `GetServices` 解析 → xUnit 类级并行时互相污染
- **修复**：`AssemblyInfo.cs` 新增 `[assembly: CollectionBehavior(DisableTestParallelization = true)]`（一行，附注释说明）——共享 headless Avalonia 单例下串行确定性；62 测试 392ms 可忽略
- **验证**：修复后全量 8/8 稳定通过

---

## E. 验收

- `dotnet build NeoEditor.sln` 0 错误
- `dotnet test NeoEditor.sln` **805/805**（全量 8 次连跑无偶发失败）
- 新增文件：`VisHelperService.RawData.cs`（partial）、`RawDataTableTests.cs`、`AssemblyInfo.cs`
- 修改：`FieldGroupMetadata.cs`（GetSections）、`VisHelperService.cs`（partial + Loc 带参）、24 个 visualizer、3 个 resx

---

## 后续（未做，留待 P4 声明式引擎）

- Detail 语义行携带源列名 → 与 Raw Data 双向锚定（点击高亮/滚动定位）——依赖 visualizer 传列名，改动面大
- 「已展示/未展示」覆盖率判定 —— 需要 visualizer 声明字段清单（声明式渲染引擎天然具备）
