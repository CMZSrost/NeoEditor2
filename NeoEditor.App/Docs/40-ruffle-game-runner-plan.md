# 40 — Ruffle 游戏运行器开发计划（Ruffle Game Runner）

> 🗑 **已废弃（2026-08-05）**：本方案（外部 ruffle.exe 进程运行器）已被 **Docs/42 WebView 内置预览
> + NeoEditor.Player 独立播放器**取代，相关代码（IRuffleRunner / RuffleRunnerService / RuffleLocator /
> RuffleOptionsBuilder.Build / 工具栏按钮 / resx 键 / 测试）已全部删除。保留本文档仅作历史参考；
> SWF 发现逻辑（`RuffleOptionsBuilder.FindSwfPath`）保留并继续服务于 Docs/42 预览。
>
> v1.1 · 2026-08-03 · **P1 ✅ 完成**（P2/P3 待做）
> 目标：编辑器通过 **Ruffle**（第三方 Flash 模拟器，用户自装）以进程方式运行游戏 SWF，并捕获 Ruffle 运行日志。

---

## 一、背景与目标

NeoScavenger 是 Flash/AIR 游戏，主程序为 `NEOScavenger.exe`（AIR 运行时壳），核心逻辑在
`{GameRootDir}/NEOScavenger.swf`。当前编辑器「保存并启动」直接 `Process.Start(NEOScavenger.exe)`（见
`ModGameDataTabsView.Operations.cs:169` `OnSaveAndLaunchClickAsync`）。

**目标**：新增一条「用 Ruffle 运行」路径 —— 编辑器以进程方式拉起 Ruffle 桌面播放器加载
`NEOScavenger.swf`，并捕获其运行日志供排错（Ruffle 兼容性、SWF 加载错误、脚本报错等）。

**边界**（与用户确认的约束）：

- Ruffle 是**第三方扩展**：编辑器不捆绑、不自动下载 Ruffle，由用户自行安装。
- 编辑器**检测到 Ruffle（环境变量等）才启用**该功能；未检测到则保持现状（无新 UI 负担）。
- 日志捕获是硬需求：Ruffle 运行时的 stdout/stderr 及日志文件需落到编辑器 `logs/` 目录。

---

## 二、调研结论（Ruffle 技术事实，均从官方源码确认）

> 源码：`github.com/ruffle-rs/ruffle`，`desktop/src/{cli.rs, main.rs, log.rs}`（master，0.4.1）

### 2.1 分发与可执行文件

| 项 | 事实 |
|----|------|
| 发布包 | `ruffle-0.4.1-windows-x86_64.zip`（GitHub Releases） |
| 可执行文件 | 解压后 `ruffle.exe`（desktop player，Windows GUI 子系统） |
| 命令形式 | 无子命令：`ruffle.exe [选项] <SWF路径|URL>`（`FILE` 为 clap 位置参数，见 cli.rs `Opt`） |

### 2.2 与本功能相关的 CLI 选项（cli.rs `Opt`）

| 选项 | 含义 | 本项目用法 |
|------|------|-----------|
| `FILE` | SWF 路径或 URL | `{GameRootDir}/NEOScavenger.swf` |
| `--player-runtime <flash_player\|air>` | 模拟 Flash Player 或 Adobe AIR | **`air`**（NeoScavenger 为 AIR 目标） |
| `--base <URL>` | SWF 内相对路径解析基准（默认当前目录） | 游戏根目录（`file://` URL），SWF 内加载 `data/`、`img/` 依赖它 |
| `--save-directory <dir>` | SharedObjects（存档）位置，默认 `%LOCALAPPDATA%\ruffle\SharedObjects` | 开放问题 O2（见 §七） |
| `--cache-directory <dir>` | 缓存目录；**日志文件就写在 `{cache}/log/` 下** | 编辑器 `logs/ruffle-cache/`（实现日志落盘兜底，见 §四.3） |
| `--config <dir>` | Ruffle 配置目录（默认 `%LOCALAPPDATA%\ruffle`） | 默认即可 |
| `--filesystem-access-mode <ask\|allow\|deny>` | 非交互文件系统访问策略（默认 `ask`） | 游戏需读外部 XML → `allow`（本地单机无风险）或保留 `ask` 弹窗，见开放问题 O1 |
| `-P key=value` | flashvars，可重复 | 暂不使用，预留 |
| `--width/--height`、`--fullscreen`、`--volume` | 窗口/音量 | 预留，不默认启用 |
| `--no-gui` | 隐藏顶部菜单栏 | 预留 |
| `--dummy-external-interface` | 空 ExternalInterface | 兜底选项：游戏若依赖宿主通信可尝试，见 O1 |
| `--player-version`、`--load-behavior`、`--letterbox` | 模拟细节 | 预留 |

### 2.3 日志机制（main.rs 确认 —— 本功能核心依据）

```
main():
  1. 日志路径 = {cache_directory}/log/ruffle.log
     （文件名模式：single_file → ruffle.log；with_timestamp → ruffle_%F_%H-%M-%S.log）
  2. tracing_subscriber 双 writer：stdout + 日志文件（non_blocking，进程退出时 flush）
  3. 过滤器由环境变量 RUST_LOG 控制，默认 "warn,ruffle=info,avm_trace=info"
```

| 事实 | 意义 |
|------|------|
| 日志同时写 **stdout** 与 **`{cache}/log/ruffle.log`** | 两条捕获通道：stdout 管道重定向可实时；日志文件可作完整兜底 |
| `RUST_LOG` 环境变量控制详细度 | 编辑器启动子进程时注入，默认 `warn,ruffle=info,avm_trace=info`，可调 `debug` |
| `--cache-directory` 可重定向日志文件位置 | 传 `{editorLogs}/ruffle-cache/` 即让 ruffle.log 直接落在编辑器日志区 |
| Windows GUI 子系统（`#![windows_subsystem="windows"]`），`Console::attach()` 仅在自有控制台场景生效 | 由我们创建进程 + 管道重定向，stdout/stderr 捕获稳定，不受控制台窗口影响 |

---

## 三、总体设计

### 3.1 架构与分层（遵循 R07 单向分层 / R24 数据管道无关）

```
Core/Abstractions/IRuffleRunner.cs          —— 抽象（IsAvailable / RunAsync / Stop / 事件）
Core/Services/RuffleLocator.cs              —— 纯逻辑：按优先级定位 ruffle.exe（可单测）
Core/Services/RuffleOptionsBuilder.cs       —— 纯逻辑：由 (swf, gameRoot, logsDir) 构建参数（可单测）
Infra/Services/RuffleRunnerService.cs       —— 实现：Process 生命周期 + stdout/stderr 管道 → Serilog
App 层：
  ├─ SettingsPage（P2 可选：RufflePath 配置项，config.json）
  ├─ ModGameDataTabsView 工具栏「Ruffle 启动」按钮（P2）
  └─ resx 三语言键（P2）
```

设计原则：

- **不捆绑 Ruffle**：定位逻辑只读环境变量 / 配置 / PATH，找不到即 `IsAvailable=false`，功能整体禁用。
- **可测试性**：定位与参数构建为纯静态逻辑，放 Core（`NeoEditor.Core.Tests` 可测）；进程管理放 Infra（`NeoEditor.Infra.Tests` 可测）。App 只做按钮/通知装配（App 无测试项目，与现状一致）。
- **不新建插件项目**：本功能是 App shell 职责（与现有「保存并启动」同级），不涉及实体数据，不违反 R17/R23。

### 3.2 Ruffle 定位优先级（RuffleLocator）

```
1. AppConfig.RufflePath（设置页填写，非空且文件存在）      [P2 可选]
2. 环境变量 RUFFLE_PATH（指向 ruffle.exe 完整路径）        [P1 核心，用户明确要求]
3. PATH 中可执行名 `ruffle`（含 ruffle.exe）               [P1 便利项]
→ 均未命中：IsAvailable = false（功能隐藏/禁用）
```

- 环境变量读取时机：**实时解析**——`RuffleRunnerService.ExecutablePath` 每次访问时重查 `RUFFLE_PATH`/PATH（不缓存）；按钮显隐在视图构造与 ReadOnly（编辑 ↔ 浏览模式）切换时刷新。因此设置环境变量后**无需强制重启**：重启最稳妥，不重启则在之后切换一次编辑/浏览模式时按钮即出现。
- `RUFFLE_PATH` 命名说明：Ruffle 官方未定义标准环境变量，此为编辑器自定约定；写入设置页说明文字与帮助文档。

### 3.3 运行命令（RuffleOptionsBuilder 产出）

```
工作目录：{GameRootDir}
参数示例：
  ruffle.exe
    --player-runtime air
    --base file:///{GameRootDir}          （URL 编码；SWF 相对路径解析基准）
    --cache-directory {editorLogs}/ruffle-cache
    --filesystem-access-mode allow        （O1 待定）
    {GameRootDir}/NEOScavenger.swf
环境：RUST_LOG=warn,ruffle=info,avm_trace=info   （P3 可调 debug）
```

SWF 路径解析顺序：`{GameRootDir}/NEOScavenger.swf` 存在则用之；否则扫描 `*.swf`（P1 只认固定名，找不到报错提示，与现有 exe 缺失提示同风格）。

### 3.4 日志捕获（双通道，P1 起生效）

```
通道 A（实时）：RedirectStandardOutput/RedirectStandardError → 逐行 → Serilog
              写 logs/ruffle-<yyyyMMdd-HHmmss>.log + Log.Information 进主日志
通道 B（兜底）：--cache-directory 指到 logs/ruffle-cache/ → ruffle.log 完整落盘，
              进程退出后若通道 A 有丢失（non_blocking flush 竞态）可从 B 补全
```

- 行解析：按 `\n` 拆流（`Process` 管道需自行缓冲半行），统一加 `[Ruffle]` 前缀进 Serilog。
- 进程退出：`Exited` 事件 → 通知用户（退出码、日志文件路径）、触发 UI 状态复位（P2）。
- 单实例：同一时刻只允许一个 Ruffle 进程（防存档冲突），运行中禁用启动按钮。

---

## 四、实施步骤

### P1 — 检测 + 运行 + 日志落盘（核心闭环，可独立验收）✅ 已完成 (2026-08-03)

| # | 内容 | 文件 | 状态 |
|---|------|------|------|
| P1.1 | `IRuffleRunner` 抽象 + `RuffleLocator`（优先级 2/3：环境变量/PATH） | `Core/Abstractions/IRuffleRunner.cs`、`Core/Services/RuffleLocator.cs` | ✅ |
| P1.2 | `RuffleOptionsBuilder`：SWF 定位 + 参数构建 + URL 编码 + RUST_LOG | `Core/Services/RuffleOptionsBuilder.cs` | ✅ |
| P1.3 | `RuffleRunnerService`：进程启动/管道读取/Exited/单实例锁 | `Infra/Services/RuffleRunnerService.cs` | ✅ |
| P1.4 | DI 注册 + 工具栏「用 Ruffle 启动」按钮（`RuffleLaunchVisible`） | `App.axaml.cs`、`Helper/ViewServices.cs`、`ModGameDataTabsView.axaml(.cs/Tab.cs/Operations.cs)` | ✅ |
| P1.5 | 本地化键 ×3 resx（RuffleLaunch / RuffleNotInstalled / RuffleStarted / RuffleLaunchFailed / RuffleExited） | `Assets/Resources*.resx` | ✅ |
| P1.6 | 单测：Locator 7 + Builder 6 + Runner 5 | `Tests/NeoEditor.Core.Tests/Services/`、`Tests/NeoEditor.Infra.Tests/Services/` | ✅ |

**实施细节（与计划差异）**：

- `Launch` 同步返回 bool；日志行事件 `LogLineReceived` 已实现（供 P3 日志面板）。
- 测试钩子：`Launch(RuffleLaunchOptions)` 公开重载，单测用 cmd/powershell 桩进程驱动完整管道（真实 Ruffle 无需安装）。
- `--filesystem-access-mode allow` 采纳（开放问题 O3 结论：本地单机）。
- stdout 管道存在「快速退出进程丢输出」的 .NET 竞态（`BeginOutputReadLine` 晚于进程退出）——生产影响小（Ruffle 启动 >1s），完整性由通道 B（ruffle.log 落盘）兜底；单测用慢写桩规避。

验收：`RUFFLE_PATH` 指向 ruffle.exe → 编辑器工具栏出现「用 Ruffle 启动」→ 点击拉起 Ruffle 运行 SWF → `logs/ruffle-*.log` 有完整日志 → 退出后提示「Ruffle 已退出（代码 N）。日志文件：…」。

### P2 — 设置页配置项 + 状态反馈（体验完善）

| # | 内容 | 文件 |
|---|------|------|
| P2.1 | `AppConfig.RufflePath` + 设置页文本行（Game Root 区域下）+ 说明文字 | `Core/Model/AppConfig.cs`、`SettingsPageView.axaml`、`SettingsPaneViewModel.cs` |
| P2.2 | Locator 增加优先级 1（配置项）；路径失效时回退环境变量 | `RuffleLocator` |
| P2.3 | 运行中状态：按钮变「停止」（Kill）+ 状态栏/通知显示退出码与日志路径 | `ModGameDataTabsView`、`MainStatusBar`（视现状取舍） |
| P2.4 | 未检测到 Ruffle 时：按钮隐藏；设置页显示安装指引（下载 zip 解压 + 设置环境变量） | resx + 帮助文档 |

### P3 — 日志增强（可选，按需）

- 「查看 Ruffle 日志」入口（打开 `logs/ruffle-cache/log/` 目录）。
- RUST_LOG 可配置（默认 info；出问题时切 debug 重跑）。
- 捕获 Ruffle 的 `--version` 输出用于诊断（启动时校验可执行性）。

---

## 五、涉及文件清单（预估）

**Core**：`Abstractions/IRuffleRunner.cs`（新）、`Services/RuffleLocator.cs`（新）、`Services/RuffleOptionsBuilder.cs`（新）、`Model/AppConfig.cs`（+RufflePath，P2）

**Infra**：`Services/RuffleRunnerService.cs`（新）

**App**：`App.axaml.cs`（+DI）、`Helper/ViewServices.cs`（+访问器）、`Views/UserControls/ModGameDataTabsView.axaml(.cs/Tab.cs/Operations.cs)`（+按钮）、`Views/UserControls/SettingsPageView.axaml`（P2）、`ViewModels/ExplorerPane/SettingsPaneViewModel.cs`（P2）、`Assets/Resources*.resx`（+键）、`Help/zh/模组制作指南.md`（安装指引）

**Tests**：`NeoEditor.Core.Tests`（Locator/Builder）、`NeoEditor.Infra.Tests`（RunnerService 用 stub 模拟进程）

**文档**：`Docs/CHANGELOG.md`（实施后）、`index.md`（本计划登记，见 §八）

---

## 六、测试计划

| 测试 | 覆盖 | 位置 |
|------|------|------|
| `RuffleLocatorTests` | 环境变量命中/缺失、PATH 命中、优先级（配置 > 环境变量 > PATH）、空串/空白串 | Core.Tests |
| `RuffleOptionsBuilderTests` | SWF 路径解析（存在/缺失/多 swf）、`--player-runtime air`、`--base` URL 编码（含空格/中文路径）、`--cache-directory`、RUST_LOG 值 | Core.Tests |
| `RuffleRunnerServiceTests` | 用假进程（如 `cmd /c echo`）验证管道读取、Exited 事件、单实例拒绝 | Infra.Tests |
| 手工验收 | 真实 Ruffle + 游戏 SWF 运行、日志完整性（对照 ruffle.log 与 ruffle-*.log） | — |

---

## 七、风险与开放问题

| # | 问题 | 现状/建议 |
|---|------|----------|
| O1 | NeoScavenger SWF 为 AIR 目标，且可能依赖宿主交互（ExternalInterface/FSCommand）；`--player-runtime air` 的兼容度 | 用户已调研「Ruffle 可运行该游戏」；`--dummy-external-interface` 作兜底；P1 手工验收确认 |
| O2 | 存档位置：Ruffle 默认 SharedObjects 在 `%LOCALAPPDATA%\ruffle\SharedObjects`，与原版存档目录不同 → 玩家存档隔离 | P1 用默认位置（不污染原存档，安全）；后续可加「`--save-directory` 指向游戏目录」开关 |
| O3 | 文件系统访问：游戏运行时需读 `data/` 等外部 XML，Ruffle 默认 `ask` 会弹权限窗 | 建议 P1 直接 `allow`（本地单机）；若 SWF 有联网/写盘行为再收紧 |
| O4 | Ruffle 对 SWF 的版本/DRM 兼容 | 属 Ruffle 侧能力，编辑器只负责进程与日志；报错通过日志暴露 |
| O5 | non_blocking stdout 退出时可能丢尾部日志 | 通道 B（ruffle.log 文件）兜底，验收时对比 |

---

## 八、文档登记

- 本计划：`Docs/40-ruffle-game-runner-plan.md`（已登记到 `index.md`「当前计划（进行中）」）。
- CHANGELOG：P1 完成条目已记录（2026-08-03）。
- 是否需要 spec 规则：本功能不新增架构约束（沿用 R07/R24 分层与数据管道），暂不新增 R/N；若后续「运行器」扩展成通用能力（多模拟器）再考虑 D03 方向决策。
