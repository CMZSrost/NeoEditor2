# R23 — Plugin 分类标记

> 生效：2026-07-29 | 来源：用户决策 M13+
> 依从：D01 Core/Plugin 架构方向 · R18 Plugin 依赖范围
> 决策：方案 C

## 规则

Plugin 需通过 `[PluginKind]` 属性标注分类。所有 Plugin 执行 `IPlugin`。Service Plugin 额外实现 `IServicePlugin`。

### 三分类

| 分类 | 标签 | 接口要求 | 说明 |
|:----:|------|----------|------|
| **Workbench** | `[PluginKind(Workbench)]` | `IPlugin` + `IToolPlugin` / `IDocumentPlugin` | 新增 UI 组件。现有 Plugin 全部属于此类。 |
| **Service** | `[PluginKind(Service)]` | `IPlugin` + `IServicePlugin` | 纯后端服务。无 UI 组件，通过 IHostService 与编辑器交互。 |
| **Feature** | `[PluginKind(Feature)]` | `IPlugin` | 通过 HostService 扩展点注入行为。不直接引用 Workbench Plugin 代码。 |

### 接口定义（Core/Abstractions/）

```csharp
public enum PluginKind { Workbench, Service, Feature }

[AttributeUsage(AttributeTargets.Class)]
public class PluginKindAttribute : Attribute
{
    public PluginKind Kind { get; }
    public PluginKindAttribute(PluginKind kind) => Kind = kind;
}

public interface IServicePlugin : IPlugin
{
    // 无 UI 相关方法。InitializeAsync 中注册后端服务。
}
```

### 理由

- Service Plugin 需要显式接口（区分 Server/Client 角色，供 DI/MCP 接入）
- Workbench 和 Feature 用元数据标识（减少 breaking change，现有 Plugin 只需加属性）
- 分类在编译期确定，运行时 Plugin 加载器据此分发

### 例外

任何 Plugin 不得同时标注多个分类。一个 Plugin 只能是一个分类。
