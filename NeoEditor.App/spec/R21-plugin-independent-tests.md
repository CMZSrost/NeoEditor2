# R21 — Plugin 独立测试项目

> **规则**: 每个 Plugin 独立测试项目，只引用该 Plugin + Mock Core
> **来源**: [Docs/28-plugin-architecture-migration.md](../Docs/28-plugin-architecture-migration.md) §5
> **启用**: M9 (2026-07-28) ✅

## 测试项目清单

| 测试项目 | 引用 | 测试数 |
|----------|------|:-----:|
| DataViewer.Tests | DataViewer.csproj + Core + UI.Common | 9/9 ✅ |
| EntityEditor.Tests | EntityEditor.csproj + Core + UI.Common | 9/9 ✅ |
| ImageTools.Tests | ImageTools.csproj + Core + UI.Common | 4/4 ✅ |

Plugin 测试不引用 App 或其他 Plugin，确保真正的模块内聚。
