# R24 — 统一数据修改路径

> 生效：2026-07-29 | 来源：用户决策 M13+
> 依从：R01 状态唯一所有者 · N01 禁止静态可变状态

## 规则

所有数据写操作必须经过 `IHostService` 接口。

### 禁止

```csharp
// ❌ 禁止：ViewModel 直接操作 EF Core DbContext
dbContext.Set<ItemType>().Add(newItem);

// ❌ 禁止：ViewModel 直接操作 EntityMergeStore
entityMergeStore.SomeData = newData;

// ❌ 禁止：绕过 HostService 直接调用 CommandHistory
commandHistory.Execute(cmd);
```

### 强制

```csharp
// ✅ 必须：通过 IHostService
await hostService.ExecuteAsync(new AddEntityCommand(newItem));

// ✅ 必须：批量操作
await hostService.ExecuteBatchAsync(commands);

// ✅ 必须：撤销/重做
await hostService.UndoAsync();
```

### 例外

- **读操作**不在此规则限制内（ViewModel 可以直接读 Store/EF Core 做查询）
- **HostService 实现内部**可以操作 DbContext 和 CommandHistory

### 理由

1. 统一写入口后，Command 模式覆盖所有修改（撤销/重做/WAL 持久化自洽）
2. CLI/MCP/AI Chat 可以走同一路径，不绕路
3. Feature Plugin 的扩展点可以在 HostService 层面拦截

### 验收

- 架构测试扫描所有引用 `DbContext` / `EntityMergeStore` / `CommandHistory` 的 ViewModel 文件
- 每个写操作调用都能追溯到 `IHostService` 方法
