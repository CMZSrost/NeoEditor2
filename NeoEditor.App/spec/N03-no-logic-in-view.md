# [N03] 禁止 View 写业务/导航逻辑

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 禁止(DON'T) |
| **创建来源** | 决策 D4/D7 / 2026-06-29 代码审查 |
| **创建时间** | 2026-06-29 |

**是什么**
> View 层（含 EntityVisualizers、code-behind）不得包含导航逻辑、引用解析逻辑、
> 数据访问逻辑。这些下沉到注入的 `INavigationRouter` / `IReferenceResolver` / Core 服务。

**为什么**
> EntityVisualizers 把 66 处 `NavigateTo` 直接写进 pointer 事件，并内联 `LookupRef` 与
> `GenericDataGridHelper.FindBestMatch`，使 View 与静态层绑死、无法测试、无法拆分。
> View 只组装控件，逻辑才能集中复用。

**决策边界**
> 适用：所有 `Views/` 下代码与 code-behind。
> 允许：纯 UI 行为（折叠、滚动、焦点、样式切换）留在 View。
> 相关：[R04](R04-view-assembles-only.md) 正面规则；[R07](R07-one-way-layering.md) 分层。
