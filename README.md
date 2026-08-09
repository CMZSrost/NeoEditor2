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
> 本地发包：`./publish.ps1`（单文件 / 多文件 / 测试）

## 独立播放器 NeoScavenger Player（试用版 v0.9.0）

内置 Ruffle 的 WebView 播放器，直接运行游戏 SWF，无需安装 Flash。开发运行：
`dotnet run --project NeoEditor.Player`；发布：`./publish.ps1`（或打 `v*` tag 走
GitHub Actions 自动出 zip，见 [Docs/42 §八](NeoEditor.App/Docs/42-webview-ruffle-preview-plan.md)）。

### 要求

- Windows 10/11 x64（暂只支持 win10 实测）；系统需自带 **WebView2 Runtime**
- **自备游戏文件**：`NEOScavenger.swf` + 游戏 `data/` 目录放在**同一文件夹**（游戏根目录 = SWF 所在目录），版权归游戏厂商

### 使用

1. 启动后把 `NEOScavenger.swf` 拖进窗口，或 `文件 → 打开 SWF`（Ctrl+O）
2. `文件 → 重新加载`（F5）重开游戏；`视图 → 全屏`（F11）；`视图 → 日志`（Shift+Tab）查看运行日志
3. `存档管理`：查看/删除 localStorage 存档（游戏内"继续游戏"读取的就是它），删除后自动重启生效
4. `调试` 菜单：F12 开发者工具（Network / localStorage / Console）、打开日志目录、导出日志、**导出存档+日志 (zip)**、关于

### 数据位置

| 内容 | 位置 |
|------|------|
| 游戏存档 | 页面 localStorage（固定回环端口 → 重开播放器保留）；另自动备份到 `{游戏根目录}/save_backup/`（最近 5 份） |
| 运行日志 | exe 旁 `logs/player-run-*.log`（每 run 一个，保留最新 2 份）；不可写时落到 `%LocalAppData%/NeoScavengerPlayer/logs` |
| 启动日志 | exe 旁 `logs/player-boot-*.log`（每次启动一个，保留最新 5 份——启动里程碑 + 崩溃原因，启动即闪退时凭它定位）；不可写时同上回退 |
| 设置 | `%LocalAppData%/NeoScavengerPlayer/settings.json` |
| WebView2 缓存 | `%LocalAppData%/NeoScavengerPlayer/WebView2` |

### 已知限制

- **杀软误报**：未签名 self-contained 单文件 exe 可能被 Defender 误报——加白名单，或改用多文件发布（`./publish.ps1` 选项 2）

### 反馈 bug

请提供：① 窗口标题栏的**版本号**（或 `调试 → 关于`）；② `调试 → 导出存档+日志 (zip)` 生成的 zip 文件（含 localStorage 存档、日志、info.txt）。
