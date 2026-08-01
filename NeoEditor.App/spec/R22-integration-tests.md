# R22 — Integration.Tests 覆盖跨 Plugin 链路

> **规则**: Integration.Tests 独立项目，覆盖跨 Plugin 核心链路
> **来源**: [Docs/28-plugin-architecture-migration.md](../Docs/28-plugin-architecture-migration.md) §5
> **启用**: M12 (2026-07-29) ✅

## 项目位置

`Tests/NeoEditor.Integration.Tests/`

引用全部模块（不 Mock），使用最小 DI 容器模拟完整数据流。

## 当前覆盖场景

| 测试 | 链路 |
|------|------|
| 消息流 | EntitySelected → Active → Refresh (DataViewer → EntityEditor) |
| 保存 | SaveRequested → EntityDbSaved → SaveCompleted |
| 验证 | RequestValidation → ValidationCompleted |
| 导航 | NavigateToEntityRequested |
| Profile | Load → Edit → Save |
| DI 组合 | DataTableService 从 DI 正常解析 + 委派 Session |
| Plugin 契约 | DataViewerPlugin 实现 IToolPlugin |

当前 10/10 测试全部通过。
