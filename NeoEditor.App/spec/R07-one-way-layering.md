# [R07] 单向分层

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 基石(DO) |
| **创建来源** | 决策 D7 / 2026-06-29 |
| **创建时间** | 2026-06-29 |

**是什么**
> 代码分四层，依赖只能向下：`Domain → Core/Services → ViewModels → Views`。
> Domain 无依赖；Core 依赖 Domain；ViewModels 依赖 Core；Views 只依赖 ViewModels。

**为什么**
> 旧代码静态 helper 层（GenericDataGridHelper 等）位置含糊，既被 View 调又反向抓 DI 服务，
> 依赖成环。明确单向分层后，模块边界清晰、改动影响可预测、可逐层替换。

**决策边界**
> 适用：所有新增/迁移代码的命名空间与依赖方向。
> 铁律：Views 不得触碰 Core 静态成员；Core 不得引用 Avalonia 控件。
> 落地方式（文件夹约定 vs 独立程序集）见 open-questions Q6。
> 相关：[N01](N01-no-static-state.md) / [N03](N03-no-logic-in-view.md)。
