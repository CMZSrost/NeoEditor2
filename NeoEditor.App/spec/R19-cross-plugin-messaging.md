# R19 — 跨 Plugin 消息通信

> **规则**: 跨 Plugin 通信只走 IMessenger 事件
> **来源**: [Docs/28-plugin-architecture-migration.md](../Docs/28-plugin-architecture-migration.md) §3.2
> **启用**: M9 (2026-07-28) ✅

## 消息设计原则

- 消息是**通知**，不是命令。发送方不假设谁会接收，不依赖接收方的行为
- 消息携带 ID，不携带完整对象引用（避免跨 Plugin 内存共享）
- 一个消息类型最多 1-2 个接收方

## 当前消息流

```
DataViewer 发出:
  EntitySelectedMessage(entityId)     → EntityEditor 接收 → 打开/激活文档
  NavigateToEntityRequestedMessage    → Navigation 接收 → 执行跳转

EntityEditor 发出:
  EntityDbSavedMessage(modId)         → DataViewer 接收 → 刷新行数据

ImageTools 发出:
  (via shared Core messages, no direct plugin-to-plugin)
```
