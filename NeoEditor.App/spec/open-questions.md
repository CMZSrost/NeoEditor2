# 待决策点（Open Questions）

> 需要你拍板后才能转为正式规则文件（`R0X`/`N0X`）。每条给出推荐项。
> 已生效规则见 [README.md](README.md)。维护方式见 [R00](R00-spec-maintenance.md)。

---

## ✅ 已决（Q1-Q11）

Q1-Q6 (2026-06-29) + Q7-Q10 (2026-07-01) + Q11 (2026-07-05) 已拍板，原题保留见文末「已归档」。

---

## 待决策

_（暂无未决项）_

---

## 已归档

### Q11 — 引用解析与 namespace 治理规范（2026-07-05）

| 子项 | 题目 | 选定 | 规则 |
|------|------|------|------|
| Q11a | 引用解析是否允许 cross-ns fallback | **A** 不允许 | [R16](R16-reference-namespace-resolution.md) |
| Q11d | modStrName vs 目录名的 namespace 语义 | **A** 以 ModLoadInfo.Namespace 为准 | [R16](R16-reference-namespace-resolution.md) + [N06](N06-no-dirname-as-namespace.md) |
| Q11b/Q11c | key 形式 / merge override | 已确认，无需决策 | [R16](R16-reference-namespace-resolution.md) |

<details>
<summary>展开：原始决策过程</summary>

**背景**：NSEoverride mod 的 `strModName` 是 `"0"`（与 game base 相同 namespace），但 `EntityNamespaces` 被错误地赋值为 mod 目录名 `"NSEoverride"`，导致无前缀引用 `211x1` 解析失败。

**待决策子项**：

#### Q11a — 引用解析是否允许 cross-namespace fallback？

| 选项 | 描述 |
|------|------|
| **A** | ❌ 不允许 fallback。无前缀引用严格使用 sourceNs；显式前缀使用指定 ns；`0:`/`:`简写映射为 sourceNs。三个路径已穷举所有场景，无需"先查 sourceNs 再查 game base"的 fallback |
| B | 允许 fallback。当 sourceNs 查不到时，fallback 到 `""` (game base) 再查一次 |

**推荐 A**。当前 `LookupEntityId` 已在以下三路全覆盖：
- 无前缀 `211x1` → `rawNs = sourceNs`（使用源实体 namespace）
- 显式前缀 `NSE:5` → `rawNs = "NSE"`（使用指定 namespace）
- 简写前缀 `0:38` / `:38` → `rawNs = sourceNs`（映射为同 namespace，非字面 `"0"`）

---

#### Q11b — 引用 key 的两种形式

| 形式 | 适用类型 | SQL 查询 | 示例 |
|------|----------|----------|------|
| 单 key (pk) | 所有类型（Condition/Recipe/ItemType/...） | `WHERE entity_type=@t AND namespace=@ns AND pk=@pk` | `211` → pk=211 |
| 复合 key (gid.sid) | 仅 ItemType | `WHERE entity_type=@t AND namespace=@ns AND group_id=@gid AND subgroup_id=@sid` | `86.3` → gid=86,sid=3 |

判定逻辑：`idOnly` 包含 `.` 且两部分均为合法 int → 走复合 key；否则走单 key。
`ReferenceIndexService` 已有两个独立索引 `idx_reference_index_lookup` 和 `idx_reference_index_composite`。

**无需决策** — 已实现且验证通过。

---

#### Q11c — Merge override 的索引实现

**规则**：索引构建时 Game base (ns="0") 先 INSERT，Mod 后 INSERT OR REPLACE。同 `(entity_type, namespace, pk)` 组合，后来的覆盖前面的。查询到的自然就是最高优先级 mod 的实体。

```
entries.Sort: game base ns="0" first → mods by load order → INSERT OR REPLACE
→ PRIMARY KEY (entity_type, namespace, pk) 确保每个 key 只有一行，最后插入的胜出
→ 查询时无需 GROUP BY / MAX / 应用层 override
```

**无需决策** — 已实现且验证通过。

---

#### Q11d — modStrName 与目录名的 namespace 语义区分

这是本系统**最容易混淆的概念**，已导致实际 bug。

| 字段 | 来源 | 示例(NSEoverride) | 用途 |
|------|------|--------------------|------|
| **目录名** (`ModInfo.Name`) | `mod_info` 表 `Name` 列 | `"NSEoverride"` | UI 显示、文件路径 |
| **strModName** (`ModLoadInfo.Namespace`) | `getmods.php` 的 `strModNameN` | `"0"` | **引用解析的 namespace** |

```php
// getmods.php 示例
$strModName0 = "NSEb";      // namespace = "NSEb"
$strModURL0  = "NSEb";      // 目录名 = "NSEb" (两者恰好相同)
$strModName1 = "0";         // namespace = "0" ← 这是 namespace！
$strModURL1  = "NSEoverride"; // 目录名 = "NSEoverride" ← 这不是 namespace！
```

**`EntityNamespaces` 的正确语义**：EntityId → strModName（namespace），不是目录名。

| 选项 | 描述 |
|------|------|
| **A** | 确认上述语义。代码中所有 `EntityNamespaces` 赋值点必须以 `ModLoadInfo.Namespace` 而非 `ModInfo.Name` 为数据源。已修复以下位置：`ReloadTabsAsync`(line 86)、`MergeService.ComputeTypeMerge`(line 248-250)、`ReloadMergeTabsAsync` → `modIdToNamespace` 传入 |
| B | 其他方案 |

**推荐 A**。已生效修复记录：
- `ModGameDataTabsView.Data.cs:86` — 单 mod 视图改为从 `BrowserStore.EntityNamespaces` 查找正确 namespace
- `MergeService.cs` — 接口增加 `modIdToNamespace` 参数传递 `ModLoadInfo.Namespace`，`ComputeTypeMerge` 不再回落至目录名
- `ModGameDataTabsView.Data.cs:715` — 合并视图构建 `modIdToNamespace` 映射传入 `ComputeMergeAsync`

</details>

---

## 已归档（原始 Q1-Q6 题目与选择）

| 编号 | 题目 | 选定 | 规则 |
|------|------|------|------|
| Q1 | 切 Profile 时未保存编辑处置 | A 提示保存/丢弃/取消 | [R09](R09-session-dirty-guard.md) |
| Q2 | 编辑后索引过期处理 | A 标过期角标，手动刷新 | [R10](R10-index-manual-refresh.md) |
| Q3 | 多开文档 Save 粒度 | 文档独立 Save + 切 Profile 全局保存 | [R11](R11-save-granularity.md) |
| Q4 | EntitySelectedMessage 统一入口 | B ISelectionService，以 Center 为主 | [R12](R12-selection-service.md) |
| Q4b | Center 焦点判定与冲突优先级 | 1=A 时间戳最新；2=B Center 为主，DataTable 浏览选中；3=A 空态 | [R12](R12-selection-service.md) + [R15](R15-datatable-interaction.md) |
| Q5 | VisHelper 可见性 | A internal static | [R13](R13-vishelper-internal.md) |
| Q6 | 分层落地方式 | A 文件夹+命名空间约定 | [R14](R14-folder-convention-layering.md) |

### Q7-Q10（2026-07-01，审计清单扫描发现）

| 编号 | 题目 | 选定 | 说明 |
|------|------|------|------|
| Q7 | Dialog code-behind 的服务获取方式 | **C** 静态工厂方法 + IServiceProvider | ✅ 已落地 (2026-07-04)：`CreateModDialog`/`RenameImagePairDialog`/`AddRowDialog` 添加 `Create(IServiceProvider)` 工厂方法，调用点全部更新 |
| Q8 | `Documents.cs` GlobalBrowserCache/GlobalModNames 迁移目标 | **B** 迁移到 BrowserIndexService 实例属性 | 保持 Service 层职责聚焦，不污染 IWorkspaceSession 接口 |
| Q9 | `ImagePreviewContent._cachedImgDirs` 静态缓存 | **A** 迁移到 IImageService 内部 | IImageService 已是 Singleton，路径缓存归入合理，消除静态状态 |
| Q10 | 死消息处理策略 | **A** 全部删除，按需重建 | 强约束：18 条死消息一律删除；如需恢复功能再按 R05 规范新增消息 |
