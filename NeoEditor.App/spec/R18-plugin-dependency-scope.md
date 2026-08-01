# R18 — Plugin 依赖范围

> **规则**: Plugin 只依赖 Core + Infra + UI.Common，不可依赖 App 或其他 Plugin
> **来源**: [Docs/28-plugin-architecture-migration.md](../Docs/28-plugin-architecture-migration.md) §2
> **启用**: M9 (2026-07-28) ✅

## 允许的依赖

| Plugin | Core | Infra | UI.Common | App | 其他 Plugin |
|--------|:----:|:-----:|:---------:|:---:|:----------:|
| DataViewer | ✅ | ✅ | ✅ | ❌ | ❌ |
| EntityEditor | ✅ | ✅ | ✅ | ❌ | ❌ |
| ImageTools | ✅ | ✅ | ✅ | ❌ | ❌ |

## 当前验证

每个 Plugin `.csproj` 中检查 `ProjectReference` 不含 `NeoEditor.App` 或 `Plugins.` 前缀的其他 Plugin。
