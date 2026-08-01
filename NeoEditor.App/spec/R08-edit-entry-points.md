# [R08] 编辑入口仅 KV 与 XML

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 基石(DO) |
| **创建来源** | 决策 D6 衍生 / 文档 23 |
| **创建时间** | 2026-06-29 |

**是什么**
> 实体只有两个编辑入口：Left KV 编辑器、Center XML Tab。
> Bottom DataTable 只读 + 跳转（选中行打开 Center 文档），不可原地编辑单元格。

**为什么**
> 多个编辑源会破坏 [R06](R06-same-entity-instance.md) 的同源约束，并增加脏状态追踪难度。
> 收敛到两个入口，编辑路径清晰、变更可追踪。

**决策边界**
> 适用：所有实体字段编辑。
> 不适用：CRUD 元操作（New/Copy/Delete）走工具栏，不算字段编辑入口。
> 相关：[R06](R06-same-entity-instance.md) 同源实例；[N05](N05-no-bottom-editing.md) 禁止 Bottom 编辑。
