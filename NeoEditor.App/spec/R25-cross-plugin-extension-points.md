# R25 — 跨 Plugin 扩展点

> 生效：2026-07-29 | 来源：用户决策 M13+
> 依从：R17 Plugin 互不引用 · R19 跨 Plugin 通信走 IMessenger

## 规则

跨 Plugin 的功能扩展通过 HostService 事件/扩展点实现，禁止直接引用其他 Plugin 的 Service 类型。

### 禁止

```csharp
// ❌ 禁止：Feature Plugin 直接引用 Workbench Plugin 的 Service
using NeoEditor.Plugins.DataViewer.Services;
var svc = new DataTableService();

// ❌ 禁止：通过 DI 获取其他 Plugin 的 Service 类型
var svc = serviceProvider.GetRequiredService<DataTableService>();

// ❌ 禁止：修改其他 Plugin 的 ViewModel 属性
dataTableVm.SomeCollection.Add(x);
```

### 强制

```csharp
// ✅ 允许：通过 HostService 事件订阅
hostService.Changes.Subscribe(e => {
    if (e.Kind == ChangeKind.EntitySaved)
        RefreshMyFeature();
});

// ✅ 允许：通过 HostService 扩展点注册
hostService.RegisterPreSaveHook(new MyValidationHook());

// ✅ 允许：通过 IMessenger 跨 Plugin 通信（R19）
WeakReferenceMessenger.Default.Send(new EntityModifiedMessage(entityId));
```

### 扩展点接口

```csharp
public interface IExtensionPoint<TContext>
{
    string Name { get; }
    int Order { get; }
    Task ExecuteAsync(TContext context);
}

// 已定义的扩展点插槽
public interface IHostService
{
    void RegisterPreSaveHook(IExtensionPoint<PreSaveContext> hook);
    void RegisterPostLoadHook(IExtensionPoint<PostLoadContext> hook);
    void RegisterPreExecuteHook(IExtensionPoint<PreExecuteContext> hook);
}
```

### 理由

1. 保持 Plugin 间 0 引用（R17）
2. 扩展点集中在 HostService，可观测、可测试
3. Feature Plugin 不依赖具体 Workbench Plugin 的存在，组合更灵活

### 验收

- 架构测试验证 `NeoEditor.Plugins.*` 间无类型引用
- 每个跨 Plugin 交互可追踪到 HostService 或 IMessenger
