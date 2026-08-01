# R20 — DI 注册在 App Composition Root

> **规则**: DI 注册在 App Composition Root，Plugin 不自注册
> **来源**: [Docs/28-plugin-architecture-migration.md](../Docs/28-plugin-architecture-migration.md) §3.1
> **启用**: M9 (2026-07-28) ✅

## 当前模式

每个 Plugin 提供 `ServiceCollectionExtensions.AddXxxPlugin()` 扩展方法，
在 `App.axaml.cs` 的 `ConfigureServices` 中调用。

```csharp
// App.axaml.cs
services.AddDataViewerPlugin();
services.AddEntityEditorPlugin();
services.AddImageToolsPlugin();
```

Plugin 扩展方法内部注册自己的服务，但不对外暴露 DI 容器细节。
