# R17 — Plugin 互不引用

> **规则**: Plugin A 不引用 Plugin B（.csproj 级隔离）
> **来源**: [Docs/28-plugin-architecture-migration.md](../Docs/28-plugin-architecture-migration.md) §2
> **启用**: M9 (2026-07-28) ✅

## 内容

任何 Plugin 项目的 `.csproj` 中不得包含对其他 Plugin 项目的 `ProjectReference`。
Plugin 间的协作只能通过：
- **IMessenger** 消息通信（消息定义在 Core）
- **共享接口**（定义在 Core/Abstractions）
- **IEntityLookupService** 等桥接服务（在 App Composition Root 注册，Plugin 只依赖接口）

## 当前验证

```bash
# DataViewer → EntityEditor/ImageTools：0 引用
grep -c 'ProjectReference.*Plugins\.' NeoEditor.Plugins.DataViewer/*.csproj
# → 0

# EntityEditor → DataViewer/ImageTools：0 引用
grep -c 'ProjectReference.*Plugins\.' NeoEditor.Plugins.EntityEditor/*.csproj
# → 0

# ImageTools → DataViewer/EntityEditor：0 引用
grep -c 'ProjectReference.*Plugins\.' NeoEditor.Plugins.ImageTools/*.csproj
# → 0
```
