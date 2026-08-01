# [N06] 禁止用目录名作为 EntityNamespaces 值

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 禁止(DON'T) |
| **创建来源** | Q11d 决策 / Bug: NSEoverride namespace 错误 |
| **创建时间** | 2026-07-05 |

**是什么**
> `EntityMergeStore.EntityNamespaces` 的值必须是 `strModName`（来自 `ModLoadInfo.Namespace` 或 `getmods.php` 的 `strModNameN`）。
> **禁止**使用 `ModInfo.Name`（目录名）、`modInfo.Name`、或任何等同于 mod 文件夹名的值作为 namespace。

**为什么**
> `ModInfo.Name` = 目录名（如 `"NSEoverride"`），`ModLoadInfo.Namespace` = `strModName`（如 `"0"`）。
> 两者在多数 mod 中恰好相同，但 NSEoverride 是典型案例：`strModName="0"`，目录名`="NSEoverride"`。
> 混淆会导致 `EntityNamespaces` 存错值 → 引用解析在错误 namespace 中查找 → 找不到 → 灰色显示。

**决策边界**
> 适用：所有对 `EntityNamespaces` 字典的写入操作。
> 例外：`BrowserStore.EntityNamespaces`（由 `BrowserIndexService` 正确填充）可作为只读数据源。
> 相关：[R16](R16-reference-namespace-resolution.md) 完整 namespace 规范；[R01](R01-state-single-owner.md)。

---

## 错误模式（曾出现）

```csharp
// ❌ 错误 — 目录名 "NSEoverride" 被当成了 namespace
MergeStore.EntityNamespaces[entity.EntityId] = modInfo.Name;

// ❌ 同样错误 — fallback 到目录名
: entityModNames.GetValueOrDefault(entity.EntityId, "");
```

## 正确写法

```csharp
// ✅ 从 BrowserStore（已由 BrowserIndexService 正确填充）查找
var browserStore = WorkspaceSession.BrowserStore;
browserStore?.EntityNamespaces.TryGetValue(firstEntity.EntityId, out var ns);
MergeStore.EntityNamespaces[entity.EntityId] = ns ?? modInfo.Name; // fallback 仅兜底

// ✅ 从 ModLoadInfo.Namespace（profile 中解析的 strModName）传入
modIdToNs[entry.Info.ModId] = entry.Namespace; // ModLoadInfo.Namespace
// 传入 MergeService.ComputeMergeAsync 的 modIdToNamespace 参数
```
