# 架构测试第31轮 — Ruffle 游戏运行器 P1（635/635）

> 日期：2026-08-03 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 12.1)
> 上承：[test_round30_summary.md](test_round30_summary.md)（字段解释 + 可视化 + 引用解析跳转）
> 本轮目标：**编辑器通过 Ruffle（第三方 Flash/AIR 模拟器）以进程方式运行游戏 SWF，并捕获运行日志**
> 用户确认：① 第三方扩展模式——Ruffle 用户自装，编辑器不捆绑不下载；② 检测到环境变量（`RUFFLE_PATH`）才启用该功能；③ 日志捕获是硬需求
> 计划：[Docs/40-ruffle-game-runner-plan.md](../40-ruffle-game-runner-plan.md)（P1 完成，P2/P3 待做）

---

## 背景

NeoScavenger 是 Flash/AIR 游戏：`NEOScavenger.exe` 只是 AIR 运行时壳，核心逻辑在 `{GameRootDir}/NEOScavenger.swf`。编辑器现有「保存并启动」直接拉 `NEOScavenger.exe`（`ModGameDataTabsView.Operations.cs`）。用户调研确认 Ruffle 可以模拟运行该 SWF，希望编辑器以进程方式拉起 Ruffle + SWF，并提供完整的运行日志用于排错（Ruffle 兼容性、SWF 加载错误、脚本报错）。

## A. 调研结论（Ruffle 源码事实，非猜测）

全部从 `github.com/ruffle-rs/ruffle` `desktop/src/{cli.rs, main.rs, log.rs}`（master，0.4.1）确认：

### A1 CLI（cli.rs `Opt`）

- 无子命令：`ruffle.exe [选项] <SWF路径|URL>`（`FILE` 为 clap 位置参数）
- 关键选项：`--player-runtime <flash_player|air>`（**NeoScavenger 是 AIR 目标，必须 air**）、`--base <URL>`（SWF 内相对路径解析基准，默认当前目录）、`--cache-directory`（**可重定向日志文件位置**）、`--save-directory`、`--filesystem-access-mode <ask|allow|deny>`（默认 ask）、`-P key=value` flashvars、`--fullscreen/--width/--height/--volume`

### A2 日志机制（main.rs —— 本功能核心依据）⭐

```
1. 日志路径 = {cache_directory}/log/ruffle.log（文件名模式 single_file/with_timestamp）
2. tracing_subscriber 双 writer：stdout + 日志文件（non_blocking，退出时 flush）
3. 过滤器由环境变量 RUST_LOG 控制，默认 "warn,ruffle=info,avm_trace=info"
```

→ **双通道捕获方案**：stdout 管道（实时）＋ `--cache-directory` 指向编辑器 logs 目录（`ruffle.log` 完整落盘兜底，防 non_blocking 退出丢尾部日志）。

## B. 实现

### B1 Core：检测（`Services/RuffleLocator.cs`，纯静态）

- 优先级：配置路径参数（P2 预留）→ `RUFFLE_PATH` 环境变量 → PATH 中 `ruffle`/`ruffle.exe`
- **实时解析**：`RuffleRunnerService.ExecutablePath` 每次访问重查（不缓存）——新装 Ruffle 无需重启即被点击时识别
- `RUFFLE_PATH` 为编辑器自定约定（Ruffle 官方未定义标准环境变量）

### B2 Core：参数构建（`Services/RuffleOptionsBuilder.cs`，纯静态）

- SWF 定位：`NEOScavenger.swf` 固定名优先；仅有一个 `*.swf` 时兜底；多个/无 → null
- 命令行：`--player-runtime air --base file:///游戏根目录（Uri.AbsoluteUri，自动 URL 编码含空格/中文）--cache-directory {logs}/ruffle-cache --filesystem-access-mode allow {SWF}`
- 环境：`RUST_LOG=warn,ruffle=info,avm_trace=info`（Ruffle 官方默认值）
- 输出 `RuffleLaunchOptions` 记录（ExecutablePath/SwfPath/WorkingDirectory/Arguments/Env/LogFilePath）——纯数据，可单测

### B3 Infra：进程管道（`Services/RuffleRunnerService.cs`）

- `RedirectStandardOutput/Error` + `BeginOutputReadLine` 异步读 → 每行三写：`logs/ruffle-<yyyyMMdd-HHmmss>.log`（AppendAllText）+ Serilog 主日志（`[Ruffle]` 前缀）+ `LogLineReceived` 事件（供 P3 日志面板）
- `Exited` 事件上报 `RuffleExitInfo(ExitCode, LogFile)`；`Stop()` `Kill(entireProcessTree: true)`
- 单实例锁：`_process is { HasExited: false }` 时拒绝二次启动
- 测试钩子：`Launch(RuffleLaunchOptions)` 公开重载——单测用 cmd/powershell 桩进程驱动完整管道，无需真实 Ruffle

### B4 App：UI（`ModGameDataTabsView`）

- 工具栏「用 Ruffle 启动」按钮（PlayCircle 图标，位于「启动」旁）
- 显隐 `RuffleLaunchVisible`（DirectProperty）= `!ReadOnly && ExecutablePath != null`；在 ctor 与 **ReadOnly 切换**（Tab.cs 已有 `OnPropertyChanged`）时刷新 → 设置环境变量后切换一次编辑/浏览模式按钮即出现，无需强制重启
- 点击 = 启动 / 再点 = 停止（`_ruffleStopRequested` 抑制停止时的退出 toast）；退出提示「Ruffle 已退出（代码 N）。日志文件：…」
- DI：`App.axaml.cs` 注册单例；`ViewServices.RuffleRunner` 访问器
- 本地化：resx ×3 新增 5 键（RuffleLaunch / RuffleNotInstalled / RuffleStarted / RuffleLaunchFailed / RuffleExited）
- 教程：`Help/zh/Ruffle运行游戏.md`（安装 → 环境变量 → 使用 → 日志 → FAQ）

## C. 测试（+18，均真实进程管道验证）

| 测试 | 覆盖 |
|------|------|
| `RuffleLocatorTests` ×7（Core.Tests） | env 命中/缺失回退/优先级/PATH exe 与无扩展名/空值 |
| `RuffleOptionsBuilderTests` ×6（Core.Tests） | SWF 定位 4 种、参数全量断言（`--base` URL 编码含空格目录）、无 SWF 返回 null |
| `RuffleRunnerServiceTests` ×5（Infra.Tests） | 桩进程（cmd/powershell）管道捕获 + 日志落盘、退出码、**单实例拒绝**、Stop 杀进程树、空闲 Stop no-op、无 SWF 拒启 |

## D. 踩坑

1. **快速退出进程丢管道输出（.NET 已知竞态）**：`cmd /c echo` 在 `BeginOutputReadLine` 附上前就退出 → 输出全丢、测试超时。测试改用「先 `Start-Sleep 800ms` 再 `Write-Output`」的 powershell 慢写桩规避。生产影响小（Ruffle 启动 >1s），完整性由通道 B（`ruffle.log` 文件）兜底。
2. **Exited 晚订阅竞态**：`WaitForExitAsync` 在 `Stop()` 后才订阅 `Exited`，进程恰在断言期间自然退出 → 事件错过、超时。改为**启动前订阅** + 睡 10 秒保证存活（测试自身问题，非服务 bug）。
3. **`OnPropertyChanged` 重复定义**：`ModGameDataTabsView` 的 ReadOnly 联动钩子已存在于 `Tab.cs` 分部类——新代码必须并入既有重写，不能另写（CS0111）。
4. **他人进行中功能编译错误**：`HostServiceSearchTests.cs` 被 R31 搜索功能误删 `using NeoEditor.Data.Model;`（`AttackType` 仍在该命名空间）导致全量构建失败——恢复一行 using（已告知用户，CHANGELOG 标注）。

## 结果

**全量 635/635 通过**（构建 0 错误；617→635，+18）。真机验收路径：`RUFFLE_PATH` 指向 ruffle.exe → 重启编辑器（或切换编辑/浏览模式）→ 工具栏出现「用 Ruffle 启动」→ 点击拉起 Ruffle 运行 `NEOScavenger.swf` → `logs/ruffle-*.log` 有完整日志 → 关闭游戏提示退出码与日志路径。

## 后续（Docs/40 P2/P3）

- P2：设置页 `RufflePath` 配置项（Locator 的 `configuredPath` 参数已就绪，仅需 UI 接入）、运行中「停止」按钮状态、未安装指引
- P3：「查看 Ruffle 日志」入口、`RUST_LOG` 可配置、`--version` 校验
- 开放问题：O1 AIR 兼容度（真机验证）、O2 存档目录开关（`--save-directory`）、O5 stdout 尾部日志（通道 B 已兜底）
