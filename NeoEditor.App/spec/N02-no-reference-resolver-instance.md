# [N02] 禁止使用 ReferenceResolver.Instance

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 禁止(DON'T) |
| **创建来源** | 决策 D3 / 2026-06-29 代码审查 |
| **创建时间** | 2026-06-29 |

**是什么**
> 不得调用 `ReferenceResolver.Instance`。M0 删除该静态单例。所有引用解析改为注入
> `IReferenceResolver`（141 处调用点全量迁移）。

**为什么**
> 静态单例绕过 DI、无法替换、与 GenericDataGridHelper 循环依赖。保留它就等于
> [R03](R03-reference-resolver-injected.md) 形同虚设。

**决策边界**
> 适用：全部 141 处调用点（EntityVisualizers 129、StoryTreeEditor 5、RecipeFlowchartEditor 4、
> EditorHelper 2、GenericDataGridHelper 1）。
> 无例外。
> 相关：[R03](R03-reference-resolver-injected.md) 提供注入替代；[N01](N01-no-static-state.md)。
