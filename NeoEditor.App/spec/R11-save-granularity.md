# [R11] 文档独立保存，工具栏「Save Session」全局保存

| 字段 | 内容 |
|------|------|
| **是否启用** | ✅ 生效 |
| **类型** | 基石(DO) |
| **创建来源** | open-questions Q3 / 2026-06-29 用户确认 |
| **最后修订** | 2026-07-17 — 移除切 Profile 全局保存（不再拦截） |
| **创建时间** | 2026-06-29 |

**是什么**
> Save 默认粒度为**单文档**：每个 EntityEditorDocument 的 Save 只提交该文档实体。
> **Ctrl+S** 快捷键同样遵循单文档粒度：保存当前激活的 DataGrid 标签页或 EntityEditorDocument。
> 工具栏「Save Session」按钮执行全局保存。

**为什么**
> 文档独立保存符合用户「编辑哪个存哪个」的直觉，避免误提交其他文档的半成品。
> ~~切 Profile 是会话边界，此处全局保存可防止跨 Profile 丢失编辑。~~
>
> **2026-07-17 修订**：R09 拦截已移除（WAL 按 Mod 隔离），切 Profile/Mod 不再触发
> 全局保存。用户通过 Sidebar/HomePage 的脏数据视觉指示器了解未保存状态，主动保存。

**决策边界**
> 适用：Center 文档 Save、Ctrl+S、DataGrid 工具栏 Save 按钮、工具栏「Save Session」
> 按钮（全局保存）。
> 不适用：（已废弃）切 Profile 拦截保存。
> 实现：[SaveScope](file:///D:/RiderProjects/NeoEditor/NeoEditor/Data/Messages/ModGameDataMessages.cs) 枚举控制保存粒度，QuickSaveAsync 按 scope 筛选实体。
> 相关：[R09](R09-session-dirty-guard.md) 脏数据视觉指示；[R08](R08-edit-entry-points.md) 编辑入口。
