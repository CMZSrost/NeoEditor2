# [R12] 选中由 ISelectionService 统一管理，以 Center 焦点为主

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 基石(DO) |
| **创建来源** | open-questions Q4 / 2026-06-29 用户确认 |
| **创建时间** | 2026-06-29 |

**是什么**
> 引入注入的 `ISelectionService` 作为「当前焦点实体」的统一来源。各处（Bottom 选中行、
> Center 文档获焦）调它，由它对外发 `EntitySelectedMessage`。当前实体**以 Center 聚焦的
> 对象为主**：用户焦点在 Center，Center 当前文档的实体即全局当前实体。

**为什么**
> 旧代码 `EntitySelectedMessage` 有 4 个发送方、所有权扩散。统一到一个服务后，发送入口唯一、
> 可审计（[R05](R05-messages-ui-only.md)）。以 Center 为主符合用户注意力所在。

**决策边界**
> 适用：跨区域「当前实体」的确定与广播。
> 已定（Q4b）：当前实体 = 最后获焦的 Center 文档实体（GotFocus 时间戳最新者）；
> Center 无文档时当前实体为空（Left KV / Peek 显示空态）。DataTable 单击选中**不**改当前实体，
> 仅打开标签页/Ctrl 导航才改——交互矩阵见 [R15](R15-datatable-interaction.md)。
> 相关：[R05](R05-messages-ui-only.md) 消息准则；[R06](R06-same-entity-instance.md) 同源实例。
