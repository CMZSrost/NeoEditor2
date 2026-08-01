# [N01] 禁止静态可变状态

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 禁止(DON'T) |
| **创建来源** | 决策 D1/D8 / 2026-06-29 代码审查 |
| **创建时间** | 2026-06-29 |

**是什么**
> 不得新增任何静态可变状态来持有应用数据/会话状态。M0 后删除
> `ActiveMergeStore`、`BrowserStore`、`SetActiveStores()`、`BrowserIndexService` 静态成员、
> `ReferenceResolver.Instance`。业务代码不得读 `App.ServiceProvider`（仅 composition root 可用）。

**为什么**
> 静态可变状态是本项目耦合与 4 个 bug 的总根源：没人拥有、无法注入、循环依赖、难测试。
> 直接禁止才能防止重构后又退回老路。

**决策边界**
> 适用：Services / Helper / ViewModels / Views 全部业务代码。
> 例外：不可变常量（`const`/`static readonly` 纯值）、纯函数静态工具方法允许。
> 例外：composition root（App 启动注册）可短暂用 `App.ServiceProvider`。
> 相关：[R01](R01-state-single-owner.md) 提供替代；[R03](R03-reference-resolver-injected.md)。
