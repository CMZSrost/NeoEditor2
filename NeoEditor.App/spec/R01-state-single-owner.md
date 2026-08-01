# [R01] 状态唯一所有者 IWorkspaceSession

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 基石(DO) |
| **创建来源** | 决策 D1 / 2026-06-29 代码审查 |
| **创建时间** | 2026-06-29 |

**是什么**
> 当前 store、正向索引、反向索引、脏状态，只由 `IWorkspaceSession`（DI scoped）一个对象持有。
> 所有区域通过注入 `IWorkspaceSession` 访问这些状态。

**为什么**
> 旧代码用 `GenericDataGridHelper.ActiveMergeStore`、`BrowserStore`、`ReferenceResolver.Instance`
> 三个静态各自持有「当前数据+索引」，互相循环依赖，没人真正拥有状态。memory 记录的 4 个
> 未解决 bug（索引为空、store 指错、Ctrl 导航失效、KV 切换延迟）根因都是此。单一所有者
> 消除歧义与循环依赖。

**决策边界**
> 适用：一切对「当前数据/索引/脏状态」的读写。
> 不适用：与具体 Profile 无关的全局配置（走 `IConfigService`）。
> 相关：[R02](R02-single-active-session.md) 定义 Session 边界；[N01](N01-no-static-state.md) 禁止静态状态。
