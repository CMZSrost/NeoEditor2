# [R03] 引用解析只走注入的 IReferenceResolver

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 基石(DO) |
| **创建来源** | 决策 D3 / 2026-06-29 代码审查 |
| **创建时间** | 2026-06-29 |

**是什么**
> 引用解析（LookupRef / ReverseLookup 等）只通过构造注入的 `IReferenceResolver` 进行。
> `IReferenceResolver` 注册进 DI，内部依赖 `IWorkspaceSession` 拿 store。

**为什么**
> `IReferenceResolver` 接口早已存在却从未注册 DI，141 处全走静态 `ReferenceResolver.Instance`，
> 抽象做了却零解耦收益，且与 `GenericDataGridHelper` 形成循环依赖。注入化后抽象才真正生效，
> 也便于测试替换。

**决策边界**
> 适用：所有引用解析调用点（含 EntityVisualizers 的 129 处）。
> 例外：composition root 注册时可 `new`。
> 相关：[N02](N02-no-reference-resolver-instance.md) 禁止 `.Instance`；[R04](R04-view-assembles-only.md) 导航职责。
