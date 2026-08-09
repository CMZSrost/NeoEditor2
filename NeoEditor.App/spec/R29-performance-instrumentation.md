# R29 — 性能埋点与两阶段加载

> 生效：2026-08-08 | 来源：用户决策（R65 性能优化）
> 依从：R01 状态唯一所有者 · R03 引用解析只走注入的 IReferenceResolver

## 规则

### 1. 性能排查统一走 PerfTracer

新增/修改耗时环节时，一律用 `NeoEditor.Infra/Diagnostics/PerfTracer.cs` 埋点，**不要自造计时**：

```csharp
PerfTracer.Start("flow-name");                 // 开启/重置一条流程的累计计时
PerfTracer.Checkpoint("flow-name", "Stage");   // 输出 ms=累计 (+本段增量)
PerfTracer.End("flow-name");                   // 输出总耗时并清除
using (PerfTracer.Scope("flow-name", "Detail")) { ... }  // 独立计时段（不依赖 Start）
```

- 输出带 `[Perf]` 前缀，写入 Serilog，`grep "\[Perf\]" logs/modeditor-*.log` 即可看全流程
- 常用 flow：`app-startup`（启动）、`profile-open`（打开 profile 到 UI 就绪）、`LoadModAsync`（冷导入明细）
- 循环内的每个实体**不要**逐条 Scope（Stopwatch 开销），按类型/阶段粒度即可

### 2. Debug 日志禁止 O(N) 参数求值

`Serilog.Log.Logger.Debug(...)` 即使不输出，**参数表达式仍会求值**。热路径（如 `ReferenceIndex.Lookup`，每个引用段调用一次）里禁止在参数中做 `Count(kv => ...)` 全表扫描或反射取值——必须用 `IsEnabled(LogEventLevel.Debug)` 守卫包裹。教训：`RefIndex.Build.Reverse` 4.1s 的元凶正是 Debug 参数里的 `_nsIndex.Count(...)`（每 miss 一次 O(1.6 万) 扫描）。

### 3. 引用索引两阶段加载（数据先出，索引后台）

- `ReloadMergeTabsAsync` 拆两段：阶段 1 数据就绪即 `IsLoading=false` 显示网格（引用列 rawtext）；阶段 2 后台建内存 `ReferenceIndex` → SQLite `reference_index`/`reference_reverse`（**必须保序**：SQLite 反向解析依赖内存索引）→ 完成后 UI 线程重绑 ItemsSource 刷新引用显示
- 引用列文本是**渲染时物化进 TextBlock 的（无 Binding）**——索引就绪后必须主动 rebind（`RebuildFilteredItemsSources`，自带排序保存/恢复），不会自动更新
- **`TabSnapshotCache` 保存必须在索引构建完成后**——缓存保存 store 实例含已构建索引，缓存命中路径不重建
- 未就绪时的降级行为（依赖既有 null 守卫，勿破坏）：跳转静默 no-op；视觉器/Referenced By 面板空；`IsLoading` 只控制遮罩层

### 4. SQLite 缓存表批量写入

`reference_index`/`reference_reverse` 是可重建的缓存表，批量重建时：
- **字面量 SQL**（`BuildIndexLiteralSql`/`BuildReverseLiteralSql`）：值均为应用内部字符串（sha256 id、C# 属性名、解析段），只转义 `'`；不要用 `AddWithValue`（37.8 万次绑定 ≈ 2s）
- 重建期间临时 `PRAGMA synchronous=OFF`，完成恢复（崩溃最坏丢缓存，下次打开重建）

## 为什么

性能问题靠猜不可靠（"从数据库加载"实测 EF 只占 ~550ms，慢的是索引构建）。埋点 + 数据驱动的优化把 profile 打开从 20.6s 压到 3.9s（网格 2.3s 可见）。上述约定保证：任何环节变慢，`grep [Perf]` 一次定位；新增写入路径不重蹈参数绑定/fsync 覆辙。

## 决策边界

- 不引入 OTel/Jaeger：桌面 App 单机排查，Serilog 日志埋点足够
- 两阶段只拆「数据 / 索引」两段，不再细分（后续如需再拆，须重验缓存时序与 rebind 时机）
- 同步 `BuildAsync`（`InsertBatch` 同步版）保留字面量 SQL 但未拆两阶段——仅供既有同步路径使用
