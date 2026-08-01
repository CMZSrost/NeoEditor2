# [N05] 禁止 Bottom DataTable 原地编辑

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 禁止(DON'T) |
| **创建来源** | 决策 D6 / 文档 23 技术债 #1 |
| **创建时间** | 2026-06-29 |

**是什么**
> Bottom DataTable 不得原地编辑单元格。它只读 + 跳转（选中行打开 Center 文档）。
> 不得在 Bottom 引入第三个编辑源。

**为什么**
> 多编辑源破坏 [R06](R06-same-entity-instance.md) 同源约束，让脏状态来源不可控。
> 编辑统一收敛到 KV 与 XML 才能保证一致。

**决策边界**
> 适用：`ModGameDataTabsView` 及任何 Bottom 区域 DataGrid。
> 保留：行选中、跳转、搜索、列显隐等只读交互。
> 相关：[R08](R08-edit-entry-points.md) 编辑入口；[R06](R06-same-entity-instance.md) 同源。
