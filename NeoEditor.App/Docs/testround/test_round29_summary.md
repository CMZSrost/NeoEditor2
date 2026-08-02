# 架构测试第29轮 — Value Editor 引用解析一致性修复（482/482）

> 日期：2026-08-02 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 12.1 / ProDataGrid 12.0.4)
> 上承：[test_round28_summary.md](test_round28_summary.md)（文档/字段订正 + 引用功能修复）
> 背景：用户报告 **Value Editor 徽章引用解析与 DataTable 不一致**（"data table 的引用是正确的，但 data table 似乎存在没能解析的引用列"）

---

## 根因（两个叠加问题）

### 根因一：`ReferenceList.ToString()` 损坏格式漏网 2 条显示路径

`ReferenceList<T>.ToString()` 返回 `[a, b]`（`Core/Model/ReferenceList.cs:129`）。round28 修了 4 处 `.ToString()` 损坏点，但漏了两条**显示路径**：

| 路径 | 位置 | 表现 |
|------|------|------|
| Value Editor | `KeyValueEditorViewModel` `val?.ToString()` → `FieldRow.CurrentValue` | 徽章在解析前数据就已带括号（`[2]` / `[NSE:42]`） |
| DataTable | `ColumnTemplateFactory` `GetValue(item)?.ToString()` | 引用单元格显示 `[2]` → 解析 miss → **「没能解析的引用列」** |
| DataTable Ctrl-Hover | `DataGridCellInteractionService`（2 处） | 同上损坏 |

### 根因二：双解析后端语义分叉

即使原文正确，两条路径用不同后端 + 不同语义：

| | DataTable | Value Editor 徽章 |
|---|---|---|
| 入口 | `LookupSubject` → 内存 `ReferenceIndex` | `FindBestMatch` → SQLite `ReferenceIndexService.LookupByNs` |
| 无前缀引用 | **按 MergedId**（R16 规范） | 一律 `(ns="0", pk)`，**SQLite 索引无 MergedId 列** |
| 命名空间 | rawId 保留 `NSE:` 前缀 → ns 索引 | `lookupKey = baseRef.Id` **丢弃 `EntityRef.Namespace`** → `NSE:42` 当 ns0 查 |

### 附带 bug：KV 引用编辑写不进去

`ApplyChanges` 用 `ValueConverter.ChangeType` 把 string 转 `ReferenceList<IReferenceEntry>` → 抛异常被 catch 吞掉 → ReferencePicker 改的引用**从未写进实体**。修完显示后编辑必然失效，故同轮修复。

---

## A. 共享助手：`ReferenceText.GetRawString`（根治损坏类）

新增 `NeoEditor.Core/Model/ReferenceText.cs`：

```csharp
public static class ReferenceText
{
    public static string GetRawString(object? value, ReferenceFieldAttribute? attr)
        => value is ReferenceList<IReferenceEntry> rl
            ? rl.ToRawString(attr?.Separator)
            : value?.ToString() ?? "";
}
```

round28 已用此内联模式 4 次，抽成助手统一（所有项目引用 Core，随处可用）。

## B. 显示路径改用助手（消除 `[a, b]`）

- `KeyValueEditorViewModel`（读取 2 处）→ `ReferenceText.GetRawString(val, prop.GetCustomAttribute<ReferenceFieldAttribute>())`
- `ColumnTemplateFactory` 引用单元格 raw → 同助手（`refAttr` 在作用域内）
- `DataGridCellInteractionService` Ctrl-Hover（2 处）→ 同助手

## C. `FindBestMatch` 统一到内存 `ReferenceIndex`

`DataGridNavigationService.FindBestMatch`：
- **主路径**：`(ActiveMergeStore ?? BrowserStore)?.Index?.Lookup(sourceEntityId, propertyName, entityType, rawId)` → 命中则从 `ReferenceLookups` 返回实体（R16 语义）。
- **回退**：仅当 store 内存索引不可用时（Browser 模式未建内存索引）走原有 SQLite `_resolver.LookupEntityId`。

全部 6 个 `FindBestMatch` 调用方（Value Editor 徽章 / DataGrid Ctrl+click peek / ReferencePicker / Visualizer）与 DataTable 显示**共用同一套语义**。

## D. 徽章保留命名空间

`ReferenceFieldEditor.CreateBadge` / `OnPeekClick`：`lookupKey = baseRef.Id` → **`baseRef.ToRawString()`**（`EntityRef.ToRawString()` 保留 `NSE:` 前缀与 `86.6` 复合键）。配合 C 后 `NSE:42` 走 ns 路由、`42` 走 MergedId 路由，与 DataTable 一致。

## E. KV 引用编辑写回

- `KeyValueEditorViewModel` 构造注入 `IReferenceListSerializer`（DI 已注册 singleton）。
- `ApplyChanges` 对 `field.IsReference && PropertyType == typeof(ReferenceList<IReferenceEntry>)` 字段：`changed = OriginalValue != CurrentValue`（ReferenceList 无值相等），`oldValue/newValue = _serializer.Deserialize(...)` 替代 `ValueConverter.Convert`。
- 命名空间别名：`using IReferenceEntry = NeoEditor.Core.Abstractions.IReferenceEntry;` + `using IReferenceListSerializer = ...;`（避免与 `NeoEditor.Services.IWorkspaceSession` 歧义，同 ReferenceResolver 模式）。

## F. 测试基建

EntityEditor.Tests 新增 **Avalonia.Headless + Avalonia.Skia**（`TestApp.EnsureAvaloniaInitialized` 手动初始化，复刻 ImageTools.Tests，绕开 Headless.XUnit 拉 xunit v3 冲突）。

## 编译和测试

| 项目 | 结果 |
|------|:----:|
| `build_solution_start`（全量 22 项目） | **0 错误** ✅ |
| 全量测试 | **482/482 ✅**（471→482，+11） |

新增 11 测试：

| 项目 | 新增 | 文件 |
|------|:--:|------|
| Core.Tests | +5 | `ReferenceTextTests`（GetRawString 对 ReferenceList 返原文非 `[a, b]` / 保留命名空间 / 复合键 / string 原样 / null→""） |
| EntityEditor.Tests | +3 | `KeyValueEditorReferenceTests`（LoadEntity CurrentValue==原文非 `[a,b]` / 保留 `NSE:` 前缀 / ApplyChanges 写回 round-trip） |
| DataViewer.Tests | +3 | `ReferenceResolutionConsistencyTests`（FindBestMatch 无前缀走 MergedId / ns 前缀不落 merged / 与 LookupDisplay 一致） |

## 已知限制（本轮未做，用户未选）

- CSV/MCP/CLI 导出路径的同一 `.ToString()` 清扫（`CsvImportExportService` / `Mcp EntityResourceProvider` / `CliCommandHandler` 会输出 `[a, b]`）
- 搜索/过滤类（`FilterService` / `SearchService` / `FindReplacePanel`）把 `[a, b]` 当搜索目标，危害低

## 真机验证（建议）

导入真实 mod（NSEaid 等）→ 打开合并视图 → 对比同一行引用列：
- DataTable 单元格显示 `Subject (rawId)`，无 `[2]` / `[NSE:42]` 括号残留
- Value Editor 徽章显示**同一 Subject**，`NSE:` 前缀引用解析到 mod 实体而非游戏实体
- ReferencePicker 改引用 → Apply → 保存导出，确认新值已写入 XML
