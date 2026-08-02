# [R16] 引用解析 namespace 完整规范

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 基石(DO) |
| **创建来源** | Q11a/Q11d 决策 / Bug: NSEoverride `211x1` 解析失败 |
| **创建时间** | 2026-07-05 |

**是什么**
> 引用解析的 namespace 处理、key 形式、merge override 机制的完整规范。
> 三路 namespace 解析穷举所有场景，禁止任何 fallback。两种 key 形式各走各的 SQLite 索引。
> Merge override 由 `INSERT OR REPLACE` + 构建排序自然保证，非查询时逻辑。

**为什么**
> NSEoverride mod 的 `strModName` 是 `"0"`（与 game base 同 namespace），但 `EntityNamespaces` 被错误赋值为目录名 `"NSEoverride"`，导致无前缀引用 `211x1` 查不到。
> 此前考虑过 "sourceNs 查不到就 fallback 到 game base" 的补丁方案，但这是治标不治本——namespace 本身就不该错。
> 正确 namespace 下，三路解析已覆盖所有合法场景，无需任何 fallback。

**决策边界**
> 适用：所有引用解析调用（`LookupRef` / `LookupEntityId` / `LookupSubject` / 反向索引构建）。
> 不适用：UI 层面的 namespace 显示格式化（那是 `ReferenceParser.FormatForDisplay` 的职责）。
> 相关：[N06](N06-no-dirname-as-namespace.md) 禁止目录名作 namespace；
> [R03](R03-reference-resolver-injected.md) 引用解析入口；
> [R01](R01-state-single-owner.md) 状态唯一所有者。

---

## 1. Namespace 解析三路穷举（无 fallback）

`ReferenceResolver.LookupEntityId` 中，namespace 的确定逻辑：

```
raw reference text (如 "211x1" / "NSE:86.3" / "0:38")
    │
    ▼ ExtractRawId — 去除格式后缀 (x1, =value, [bracket])
    │
    ▼ 检查是否含 ':'
    │
    ├── 有前缀 ──→ rawNs = 冒号前的部分
    │              idOnly = 冒号后的部分
    │              → LookupByNs(entityType, rawNs, idOnly)   （直接按前缀指定的 ns 查）
    │
    └── 无前缀 ──→ ns = sourceNs
                   idOnly = 整个 rawId
    │
    ▼ NormalizeNamespace: "0"/null → "", 其他保留
    │
    ▼ TryLookupNs(normalizedNs, rawNs):
      先试 normalized，若不同再试 raw（兼容索引可能存 "0" 或 "" 两种情况）
```

三条路径覆盖全部场景：

| 路径 | 原文示例 | 解析 ns | 适用 |
|------|----------|---------|------|
| 无前缀 | `211x1` | `sourceNs` | 同 namespace 内引用 |
| 显式前缀 | `NSE:5` | `"NSE"` | 跨 namespace 显式引用 |
| `0:` 前缀 | `0:38` | `"0"`（game base） | **显式指向 0（game base）命名空间**（⚠️ 2026-08-02 订正，见下） |

**⚠️ 2026-08-02 订正（mod 数据 + 代码实证）**：本 spec 原表述「`0:` 是『同 namespace』简写，不是 game base」**有误**。
- 数据：NSE 中 `Shrink Back=0:5.6` 指向原版 5.6「存储」（NSE 自己的 5.6 是「水袋」，若按 sourceNs 解析语义错误）；`ChangeGlobalFactionRep=0:2,-100,1` 指向原版 faction 2=掠夺者（NSE faction 2=清道夫）。
- 代码：`ReferenceResolver.LookupEntityId` 对含 `:` 的 rawId 直接 `LookupByNs(entityType, rawNs, pk)`——`0:38` 查的就是 0 命名空间，**从未映射到 sourceNs**。
- 正确语义：`0:` = 显式 0（game base）；无前缀 = 同 sourceNs；`NSE:` = 显式 NSE。
- 应用范围扩展：图片引用（strIMG/strImg/vImageList/creatures.strImg）与 aEffects 参数实体同样支持前缀。

**⚠️ 禁止**：在 `LookupEntityId` 中添加任何 "先查 sourceNs，失败再查 game base (`""`)" 的 fallback 逻辑。namespace 错误应从源头修正，而非在解析层打补丁。

---

## 2. 两种 Key 形式

| 形式 | 判定条件 | SQL | 示例 |
|------|----------|-----|------|
| 单 key (pk) | 默认 | `WHERE entity_type=@t AND namespace=@ns AND pk=@pk` | `211` → pk=211 |
| 复合 key (gid.sid) | `idOnly` 含 `.` 且两端为合法 int | `WHERE entity_type=@t AND namespace=@ns AND group_id=@gid AND subgroup_id=@sid` | `86.3` → gid=86, sid=3 |

- 单 key 适用于所有实体类型（Condition、Recipe、ItemType…）
- 复合 key 仅 ItemType 同时拥有 `group_id`/`subgroup_id` 列
- 两个索引独立：`idx_reference_index_lookup` 和 `idx_reference_index_composite`（后者是 partial index，仅覆盖 `group_id IS NOT NULL` 的行）
- 判定逻辑在 `LookupEntityId`（[ReferenceResolver.cs](file:///D:/RiderProjects/NeoEditor/NeoEditor/Helper/ReferenceResolver.cs#L209-L227)）

---

## 3. Merge Override 由索引构建自然实现

合并（mod 覆盖 game base / 高优先级 mod 覆盖低优先级）在**索引构建时**就已确定，查询时无需额外处理。

### 构建机制

```
1. entries.Sort:
   - 先按 entity_type 分组
   - 同 type 内，namespace="0" 排前 (game base)，非 "0" 排后
   - 同 namespace 内按添加顺序（load order）

2. INSERT OR REPLACE INTO reference_index (...)
   - PRIMARY KEY (entity_type, namespace, pk)
   - 同一个 (type, ns, pk) 组合，后来的覆盖前面的

3. 查询 SELECT ... LIMIT 1
   - 返回唯一一行，即最后 INSERT 的 → 最高优先级的 mod
```

### 关键约束

- **排序必须** game base (ns="0") 先于 mod
- **必须用** `INSERT OR REPLACE`（不是 `INSERT OR IGNORE`）
- **查询端不写** GROUP BY / ORDER BY / MAX / 应用层筛选
- 参考代码：`BrowserIndexService` [line 248-254](file:///D:/RiderProjects/NeoEditor/NeoEditor/Services/BrowserIndexService.cs#L248-L254)；
  `BuildMergeViewIndexAsync` [line 915-920](file:///D:/RiderProjects/NeoEditor/NeoEditor/Views/UserControls/ModGameDataTabsView.Data.cs#L915-L920)

---

## 4. modStrName vs 目录名

### 文件 `getmods.php` 定义

```php
$strModNameN   ← namespace，用于引用隔离（如 "0", "NSEb"）
$strModURLN    ← 目录名，即 mod 文件夹名（如 "NSEoverride"）
```

两者**不是同一个东西**。大多数 mod 两者恰好相同，但 NSEoverride 是典型案例：`strModName="0"`，`strModURL="NSEoverride"`。

### 数据模型中对应

| 概念 | C# 字段 | 来源 | 示例 |
|------|---------|------|------|
| namespace | `ModLoadInfo.Namespace` | getmods.php `strModNameN` | `"0"` |
| 目录名 | `ModInfo.Name` | 数据库 `mod_info.Name` | `"NSEoverride"` |

```csharp
// ModInfo — 每个 mod 一条，存 DB
public class ModInfo {
    public string Name { get; set; }  // ← 目录名，不是 namespace！
}

// ModLoadInfo — 每个 mod 在 profile 中的加载条目
public class ModLoadInfo {
    public ModInfo Info { get; set; }
    public string? Namespace { get; set; }  // ← 这才是 namespace（strModName）
}
```

### `EntityNamespaces` 的正确语义

```csharp
/// EntityId → strModName (namespace)
/// Base game = "0", mods = their strModName.
/// ⚠️ 这是 strModName，绝不能用 ModInfo.Name（目录名）！
public Dictionary<string, string> EntityNamespaces { get; }
```

### 数据来源规范

| 写入位置 | 正确数据源 | 错误数据源（已修复） |
|----------|-----------|---------------------|
| `BrowserIndexService:198` | `modNsNames`（来自 getmods.php 的 strModName） | — 一直正确 |
| `ReloadTabsAsync:98` | `BrowserStore.EntityNamespaces` 查找 | `modInfo.Name` ❌ |
| `MergeService.ComputeTypeMerge:248-250` | `modIdToNamespace`（来自 `ModLoadInfo.Namespace`） | 目录名 fallback ❌ |
| `ReloadMergeTabsAsync` → `modIdToNamespace` | `entry.Namespace`（来自 `ModLoadInfo`） | 未传入 ❌ |

### 已修复的 bug

NSEoverride 实体在单 mod 视图和合并视图中，`EntityNamespaces` 被存为 `"NSEoverride"` 而非 `"0"`。
导致：源实体 namespace=`"NSEoverride"`，无前缀引用 `211x1` 尝试在 `"NSEoverride"` namespace 查找目标实体（其 namespace 正确为 `"0"`），查不到 → 灰色未解析。

修复：namespace 统一为 `"0"` → 同 namespace 内查找成功。

---

## 5. 命名空间语法汇总

| 语法 | 含义 | 示例 |
|------|------|------|
| `211` | 无前缀 → 使用源实体 namespace | NSEoverride 实体中 → ns="0" |
| `NSE:5` | 显式指定 namespace `"NSE"` | 跨 namespace 引用 |
| `0:38` | **显式指定 0（game base）命名空间**（⚠️ 2026-08-02 订正：非"同 ns 简写"） | 在 NSE 实体中 → ns="0"（原版 38） |
| `:38` | 空前缀 → 同 sourceNs（理论形式，真实数据 0 出现） | 同上 |
