# NeoEditor

NeoScavenger 游戏的 Mod 编辑器（.NET 10 / Avalonia）。

- 项目说明与开发约定：[CLAUDE.md](CLAUDE.md)
- 工作区指令（Agent 初始化）：[AGENTS.md](AGENTS.md)
- 文档总索引：[NeoEditor.App/index.md](NeoEditor.App/index.md)
- 架构决策规则（spec）：[NeoEditor.App/spec/](NeoEditor.App/spec/)

## 快速开始

```bash
dotnet build NeoEditor.sln
dotnet run --project NeoEditor.App
```

> 无 GUI 的 MCP 服务模式：`dotnet run --project NeoEditor.App -- --mcp`
