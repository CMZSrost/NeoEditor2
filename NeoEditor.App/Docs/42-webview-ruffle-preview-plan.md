# 42 — WebView 预览面板开发计划（WebView + Ruffle Web 预览）

> v2.44 · 2026-08-05 · **实施中（P0-P5 ✅；子应用扩展 P4/P5 ✅；数据浏览 ✅（= game.db 只读）；wiki 式详情 ✅（v2.23-2.26）；本地文件日志 ✅（v2.25）；win-only 打包 ✅（143MB，v2.27）；主题/本地化 ✅（v2.28）；日志链路修复 ✅（v2.29）；数据浏览主题化 ✅（v2.30）；发布流程 ✅（§八，v2.31）；图片走马灯 + 引用 Tab ✅（v2.32）；自动 release + 发包脚本 ✅（v2.33）；字段表 UI 化 ✅（v2.34）；内置 Carousel ✅（v2.35）；存档持久化 ✅（v2.36）；存档备份 ✅（v2.37，写入前备份 + 恢复，{gameRoot}/save_backup 最近 5 份）；存档管理解析失败修复 ✅（v2.38）；假存档修复 + 走马灯去手搓 ✅（v2.39）；存档跨启动丢失根因修复 + 单文件产物 ✅（v2.40）；手动备份/命名/改名 + 存档过滤 + 删除确认 ✅（v2.41）；菜单门控 + 备份消失追修 ✅（v2.42）；备份策略修正 + 实时刷新 ✅（v2.43）；受伤存档重启必崩修复 ✅（v2.44，接管序列化，待实机验证）；Docs/40 ruffle.exe 运行器已删除取代）**
> 目标：新增**通用 WebView 工具面板**模块（Avalonia 官方 WebView 控件），并内置 **Ruffle Web**
> （WASM Flash 模拟器）快速预览游戏 SWF；**取代** Docs/40 的 ruffle.exe 外部运行器（2026-08-05
> 已删除）。
> **P0.1 已验证（2026-08-04）**：Ruffle Web（nightly-2026-08-04）完整加载成功版数据集并进入游戏，
> 游戏内部日志全量捕获 —— 结论见 §2.5。

---

## 一、背景与目标

NeoScavenger 是 Flash/AIR 游戏，核心逻辑在 `{GameRootDir}/NEOScavenger.swf`。Docs/40 曾实现
「用 Ruffle 运行」路径（进程拉起用户自装的 ruffle.exe，P1 ✅）——**2026-08-05 已整体删除**，
由本计划的 WebView 内置预览取代（§3.5）。

**目标**：再新增一条**内置快速预览**路径 —— 编辑器内嵌一个 WebView 工具面板，用 Ruffle Web
（WASM 版）在面板内直接运行 `NEOScavenger.swf`，免去安装 ruffle.exe、无需拉起外部窗口，适合
「改完看一眼」的快速迭代场景。

**调研来源（P0.1 实测，2026-08-04）**：独立调研目录
`D:\software\Steam\steamapps\common\ruffle-0.5.0-web-selfhosted\`，含 `RESEARCH.md`（调研笔记）、
`index.html`（播放页，含 console/剪贴板捕获脚本）、`static-server.js`（稳定化静态服务器）、
`logs/success.log`（成功运行日志）。本计划的 Ruffle 部分以该目录实测结论为决策依据。

**边界**（与用户确认的决策）：

- **底层控件**：Avalonia 官方 **`Avalonia.Controls.WebView`**（Avalonia 12 重新开源版，作者
  AvaloniaUI OÜ）——不使用社区 WebView.Avalonia / CefGlue。
- **模块定位**：**通用 WebView 工具面板**（`IToolPlugin` dock 工具）。可加载本地 HTML 文件 /
  URL；Ruffle SWF 预览是内置的第一个页面。后续 Help 文档、Web 资源预览等可复用同一面板。
- **取代 ruffle.exe 运行器（2026-08-05）**：Docs/40 的外部运行器代码（`IRuffleRunner` /
  `RuffleRunnerService` / `RuffleLocator` / `RuffleOptionsBuilder.Build` / 工具栏按钮 / resx 键 /
  测试）**已全部删除**；仅保留 `RuffleOptionsBuilder.FindSwfPath`（SWF 发现）供预览复用（§3.5）。
- **实施状态（2026-08-05）**：P1（WebView 面板）/ P2（Ruffle 集成）/ P4（Player.Core 抽取）/
  P5（独立播放器）已首版完成；P0.2/P0.3 已登记结论。

---

## 二、调研结论（可行性分析）

### 2.1 Avalonia.Controls.WebView 12.0.1（官方控件，已确认存在）

| 项 | 事实 | 对本项目意义 |
|----|------|-------------|
| 包存在性 | nuget.org 可查到 `Avalonia.Controls.WebView`，最新 **12.0.1**，作者 **AvaloniaUI OÜ**（官方），verified | 与用户确认的「Avalonia 12 重新开源 WebView」一致 |
| 目标框架 | `net8.0` / `net10.0`（另有 `net10.0-android36.0` / `net10.0-browser1.0`） | **App 保持 `net10.0` 纯 TFM，无需多目标**（这是选官方包的关键收益） |
| 依赖 | 仅 `Avalonia 12.0.0`（exclude Build/Analyzers）；**不依赖** `Microsoft.Web.WebView2` NuGet 包 | 无额外原生包搬运 |
| Windows 后端 | WebView2（release notes 提及 WebView2 修复与 offscreen 渲染实验） | Win10/11 系统自带 WebView2 Runtime，用户侧零安装（个别精简系统需提示） |
| 平台覆盖 | 官方包当前侧重 Windows / Android / Browser；Linux/macOS 桌面后端未在依赖中体现 | 本功能按 **Windows 优先** 设计，非 Windows 行为在 P0.2 验证后定级 |

**P0.2 已验证（2026-08-05，随 P1 开发）**：控件为 **`Avalonia.Controls.NativeWebView`**（Avalonia 12
官方包，Windows 后端 WebView2）—— `Source`/`Navigate(Uri)`/`NavigateToString`/`InvokeScript(string)`/
`GoBack`/`GoForward`/`Refresh`/`Stop`；事件 `NavigationStarted`/`NavigationCompleted`/
`WebMessageReceived`（`Body`）/`WebResourceRequested`（仅 `Request`，**无 Response 属性 → 不能直接
自定义响应**）/`AdapterCreated`/`EnvironmentRequested`（`EnableDevTools` + `GetDeferral`）；
`TryGetWebViewPlatformHandle()` 可拿 `IWindowsWebView2PlatformHandle`（**方案 B 虚拟主机映射可行，
需 WebView2 互操作包，列为后续**；v1 采用方案 A 回环 HTTP 服务）。非 Windows 平台控件存在
（GTK/WKWebView/Android handle 类型），行为待实机定级。

### 2.2 Ruffle Web（self-hosted WASM 构建，P0.1 实测）

| 项 | 事实 |
|----|------|
| 发布形态 | self-hosted 包：`ruffle.js`（加载器 + 播放器 API）+ `core.ruffle.*.js` + `*.wasm`（本次目录为顶层布局；新版包布局以实抓为准）+ `LICENSE_MIT`/`LICENSE_APACHE` |
| 许可 | **MIT / Apache-2.0 双许可**，可自由随编辑器捆绑分发 —— 区别于桌面版「用户自装」的定位，WebView 方案可以开箱即用 |
| ⚠️ **版本选择（关键教训）** | **0.5.0（2021 年 nightly）过旧**：`flash.display.Loader.load()` 未实现等高影响 stub；**实测通过的是 nightly-2026-08-04（0.6.0-nightly.2026.8.4）**。实施时必须锁定新版（P0.3 定正式版号），旧版仅供对比 |
| 嵌入方式 | `window.RufflePlayer.config`（见 §3.3 实测配置）→ `newest().createPlayer()` → `player.ruffle().load(url)` |
| 运行时 | web 版只有 Flash Player runtime，无 AIR 模式（桌面版为 `--player-runtime air`）；**实测不影响本次目标场景** |

**API stub 清单（0.5.0 实测日志；新版已改善，实施时重测）**：
`flash.display.Loader.load`（高，动态加载）、`flash.desktop.Clipboard.setData/clear`（低，游戏频繁
调用——正是游戏内部日志通道，见 §3.4）、`flash.system.System.disposeXML`（无）、
`flash.ui.Mouse.registerCursor`（低，自定义光标失效）。

### 2.3 SWF 内容提供方式（关键设计决策）

Chromium 内核限制：`file://` 页面无法 fetch/XHR 本地文件、无法加载 WASM —— 因此**不能**直接用
`file://` 打开 SWF。三个候选方案：

| 方案 | 做法 | 评价 |
|------|------|------|
| **A. 回环 HTTP 服务（推荐）** | `HttpListener` 仅绑定 `127.0.0.1:随机端口`，把 `{gameRoot}` 挂为站点根：`http://127.0.0.1:port/NEOScavenger.swf`，SWF 内相对加载 `data/`、`img/` 语义与现有 `--base file:///{gameRoot}` 一致 | 与控件 API 无关、平台无关；**P0.1 已用同类方案（static-server.js）实测跑通**；安全可控（仅本机 + 随机端口 + 面板生命周期内运行） |
| B. WebView2 虚拟主机映射 | `SetVirtualHostNameToFolderMapping("game.local", gameRoot)`，页面从 `https://game.local/` 加载 | 零 HTTP 服务，最干净；**前提是控件暴露 CoreWebView2**（P0.2 验证）；绑定平台 |
| C. `player.load({ data })` 传字节 | C# 读 SWF 字节直接喂给播放器 | 只能解决 SWF 本体；SWF 内部对 `data/` `img/` 的相对加载仍需基准 URL → 不能独立成立，可作 A/B 的补充 |

**定案路径**：按 A 实施验证（不依赖控件内部 API）；若 P0.2 确认控件暴露 CoreWebView2，再评估 B
作为正式方案的替换/优化。

**服务器实测要求（P0.1 踩坑记录，GameContentServer 直接吸收）**：
- `npx serve` 在 ~3000 个图片请求洪峰下 `EMFILE: too many open files` 崩溃（流式读取句柄耗尽）
  → 需**整文件读入内存 + LRU 缓存（256MB 上限）+ EMFILE 重试**；
- MIME 必须正确：`.wasm → application/wasm`、`.swf → application/x-shockwave-flash`、
  `.php → text/plain`（getmods.php/getimages.php 按静态文本提供即可，见 §2.5）；
- `Cache-Control: no-cache` 防旧 404/旧数据缓存干扰（图片可 1 小时缓存）；ETag/304 支持；
- 目录索引 `index.html`。

### 2.4 可复用的现有资产

- `RuffleOptionsBuilder.FindSwfPath`（`Core/Services/RuffleOptionsBuilder.cs:66`）——SWF 发现逻辑
  （固定 `NEOScavenger.swf`，退化为根目录唯一 `*.swf`），直接复用。
- `AppConfig.GameRootDir`（`Core/Model/AppConfig.cs:12`）——游戏根目录配置。
- 插件模式：`[PluginKind(Workbench)]` + `IToolPlugin`（`DefaultDock`/`Order`/`CreateToolView`）+
  `ServiceCollectionExtensions.AddXxxPlugin()` + `App.axaml.cs CreateHost` 注册；Dock 由
  `DocumentWorkspaceViewModel.BuildToolDock()` 自动收录，**shell 零改动**。
- 本地化：中央 `Assets/Resources*.resx`（zh / en-us / 中性）三份同步加键。
- 测试：xunit 2.9.3，插件级测试项目 + `InternalsVisibleTo`（参考 `NeoEditor.Plugins.Mcp.Tests`）。

### 2.5 调研实测结论（P0.1 ✅ 已完成，2026-08-04）

**决定性结论：当前 Ruffle 可以运行 Neo Scavenger（Web 形式），日志可完整收集 —— WebView 移植
可行性成立。**

- **成功加载**：Ruffle（nightly-2026-08-04）在成功版数据集（`D:\Downloads\Neo Scavenger`，~40
  模组）**完整加载并进入游戏**：全程 5-10 分钟（慢速加载，模组逐个处理完整数据管线），TypeError 0
  次；游戏内部日志（剪贴板通道）全量捕获；`success.log` 显示「更新template-based items → 检查丢失
  的项目」等全部数据阶段完成。
- **数据加载机制（swf 字节码 + 模组作者确认）**：根目录 `neogame.xml`（404 **非致命**，自动回退）
  → `data/*.xml` ×24 分表（swf 硬编码文件名）→ `getmods.php`（解析 `strModName0`/`strModURL0`）
  → `Mods/<mod>/neogame.xml` → 模组 `getimages.php` → `img/*.png`。`neogame.xml` 与 `data/` 为
  同一 pma_xml_export 格式的两种载体，游戏按行解析合并。
- **getmods.php / getimages.php 仅需静态响应**：可内置为固定字符串或按静态文件提供，**无真服务端
  需求**。
- **游戏内部日志 = 剪贴板**：游戏把自身调试日志写入 `flash.desktop.Clipboard.setData()`（每几秒
  一次）。捕获方案（已实测）：页面轮询 `navigator.clipboard.readText()`（首次点击/按键授权后每
  0.8s 读一次，内容变化即入日志）+ `writeText`/`write`/`execCommand('copy')` 包装拦截 + 隐藏
  textarea 扫描，多通道去重。
- **播放配置（实测生效）**：`scale=showAll`（等比不裁切）+ `letterbox` + player 元素 100% 撑满
  容器（显式 display:block，避免 swf 原生 1360x768 尺寸溢出被裁切）+ **`maxExecutionDuration: 600`**
  （默认 15s 不够，数据解析需数分钟）+ `logLevel: "Debug"`（"Trace" 看最详细）。
- **日志链路（实测）**：页面 console 拦截（**必须在 ruffle.js 之前执行**）→ 批量 POST 本地日志
  端点 → 每次运行独立日志文件（run id 由页面生成）；`%c` 样式标记清洗；window.onerror 一并上报。
- ⚠️ **已知限制（影响编辑器默认预览）**：Steam 模组版（7 个 NSE 模组）卡预加载 **43%
  「更新template-based items」** —— **主要是编辑器写出的模组数据存在问题**（成功版数据集 ~40 模组
  同位置完整通过 → 带模组可正常加载，非 Ruffle/播放器能力限制），待逐模组二分定位。
  **编辑器 GameRootDir 默认指向 Steam 版 → 排查完成前快速预览可能卡 43%**。
- **存档存储机制（SharedObject，调研目录 RESEARCH.md §4 确认）**：Ruffle web 将 SharedObject
  存浏览器 **localStorage**（key 带 swf 路径前缀，如 `<路径>/<名字>`），**无 Flash Player 的 100KB
  默认 SO 限额** —— Flash 时代「存档 ~1MB 恶性 bug」源于 Flash 默认 SO 限额，**理论上 Ruffle
  不复现（待实测）**；新边界为 localStorage 整站配额（Chromium ~5MB，UTF-16 字符计，按 origin
  全站共享）：多存档槽位累积 + Ruffle LSO 序列化开销会提前占额。残余变量：游戏为 AIR 目标，若
  存档实际走 AIR `File` API（web 版无此 API，会走回退路径），存档代码路径与 Flash 时代不同，
  「是否复现」须按回退路径重新判断（P0 路径 A/B 实测定案）。
- **游戏请求面（运行日志实测，`logs/ruffle-log-1785849601679.txt` 成功轮次）**：游戏实际请求
  = 根 `neogame.xml`（404 回退）+ `data/*.xml` ×24 分表（每表请求两次）+ `Mods/<mod>/neogame.xml`；
  getmods.php / getimages.php 成功请求不记入页面日志（Debug 级只记失败 URL），由 serve 请求日志
  （RESEARCH.md §2.4）确认。**反代只需覆盖上述 26 个路径模式**。nightly stub 清单实锤：
  `Loader.load`、`Clipboard.setData/clear`、`Mouse.*`、`disposeXML` —— 反代走 URLLoader 通道，
  与 stub 无关。

---

## 三、总体设计

### 3.1 架构与分层（遵循 R07 单向分层 / R24 数据管道无关）

```
NeoEditor.Plugins.WebView/                     —— 新插件（Workbench，Windows 优先）
├── WebViewPlugin.cs                           —— IToolPlugin：Title 本地化、DefaultDock=Right、Order≈20
├── ServiceCollectionExtensions.cs             —— AddWebViewPlugin()（App.axaml.cs 调用）
├── ViewModels/
│   └── WebViewToolViewModel.cs                —— 地址栏/前进后退/刷新/打开本地文件/预览 SWF
├── Views/
│   └── WebViewToolView.axaml(.cs)             —— Avalonia.Controls.WebView + 导航条
├── Services/
│   ├── GameContentServer.cs                   —— 回环 HTTP 内容服务（见 3.2）
│   ├── ProxyHttpModule.cs                     —— 数据层反代路由（见 3.6）
│   ├── RuffleWebAssets.cs                     —— ruffle 静态资源定位 + 版本信息
│   └── SwfLogBridge.cs                        —— JS 日志 → Serilog（见 3.4）
├── Web/                                       —— Content 打包（见 3.3）
│   ├── host.html                              —— 设计输入：调研目录 index.html
│   └── ruffle/  (ruffle.js + core.ruffle.*.js + *.wasm + LICENSE*)
└── Messages/（可选）SwfPreviewRequestedMessage.cs —— 供其他模块打开面板并加载 SWF
```

职责边界：插件只负责「WebView 面板 + 内容提供」，**不接触** IHostService 数据管道；SWF 是游戏
根目录的现有文件，只读不写，不进入 game.db。

### 3.2 GameContentServer（回环 HTTP 内容服务）

- 绑定 `127.0.0.1:0`（随机可用端口），启动时打印/记录实际端口；**仅限回环**。
- 路由：
  - `/` → 插件 `Web/host.html`（Ruffle 嵌入页）
  - `/ruffle/*` → 插件 `Web/ruffle/` 静态资源（正确 MIME：`application/wasm`、`text/javascript`）
  - `/*` → `{gameRoot}` 下文件（含 `NEOScavenger.swf`、`data/`、`img/`、`Mods/`、getmods.php 等）
- 安全：路径规范化（`Path.GetFullPath` + 前缀校验），越界一律 404；服务随面板创建/销毁，面板关闭
  即 `Stop()`；不对外网卡监听；仅 GET。
- **性能（P0.1 实测教训）**：整文件读入内存 + LRU 缓存（256MB 上限）+ EMFILE 重试 + ETag/304 +
  no-cache（图片可 1h 缓存）——应对 ~3000 图片请求洪峰。
- **getmods.php / getimages.php**：按游戏目录内静态文件提供（text/plain），无真实 PHP 执行。

### 3.3 Ruffle 嵌入页（host.html，配置项均经 P0.1 实测）

```html
<script> /* console 拦截 + 剪贴板捕获脚本 —— 必须在 ruffle.js 之前执行（设计输入：调研 index.html） */ </script>
<script src="/ruffle/ruffle.js"></script>
<script>
  window.RufflePlayer = window.RufflePlayer || {};
  // DOMContentLoaded 后：
  let player = window.RufflePlayer.newest().createPlayer();
  player.config = {
    autoplay: "on", letterbox: "on", scale: "showAll",     // 等比适应，不裁切
    allowScriptAccess: false, openUrlMode: "deny",
    unmuteOverlay: "hidden", splashScreen: false, showSwfDownload: false,
    logLevel: "Debug",                                     // "Trace" 最详细
    maxExecutionDuration: 600,                             // 秒；默认 15s 不够
  };
  player.style.width = "100%"; player.style.height = "100%"; player.style.display = "block";
  container.appendChild(player);
  player.ruffle().load(swfUrl);                            // 默认 /NEOScavenger.swf；?swf= 可切换对比源
</script>
```

- 错误覆盖层：加载失败 / 卡进度时显示中文/英文提示 + 回退建议（「完整游戏请用工具栏『保存并启动』」）。
- `?swf=` 参数保留：便于切换对比源（如 Steam 版 vs 其他版本数据集）调试。

### 3.4 日志链路（P0.1 实测方案 → 插件内实现）

| 通道 | 内容 | 捕获方式 | 落地 |
|------|------|----------|------|
| A | Ruffle 自身日志（INFO/DEBUG/WARN/ERROR）、stub 警告、URLLoader 错误 | 页面 console 拦截（须先于 ruffle.js） | **首选**：宿主侧 JS 桥（WebView2 `WebResourceRequested`/`WebMessageReceived`，或 `ExecuteScriptAsync` 桥）；兜底：页面内 POST 本地端点 |
| B | **游戏内部日志**（= 剪贴板，游戏每几秒写入一次） | `readText()` 轮询（首次点击授权）+ `writeText`/`write`/`execCommand` 包装 + textarea 扫描，去重 | 同上 |
| C | 页面级 JS 错误 | `window.onerror` 上报 | 同上 |

输出：Serilog + 每运行独立文件（借鉴 `logs/ruffle-log-<runid>.txt` 模式）；`%c` 样式标记清洗。
日志端点仅本机回环、仅面板存活期。

### 3.5 取代 ruffle.exe 外部运行器（Docs/40 已废弃）

**2026-08-05**：Docs/40 的 ruffle.exe 外部运行器（IRuffleRunner / RuffleRunnerService / RuffleLocator /
`RuffleOptionsBuilder.Build` / 工具栏按钮 / resx 键 / 测试）**已全部删除**——本 WebView 方案
（编辑器内置预览 + 独立 Player 播放器）完全取代它。保留 `RuffleOptionsBuilder.FindSwfPath`（SWF
发现）供预览复用；`Docs/40-ruffle-game-runner-plan.md` 标记废弃仅作历史参考。

原并存设计对比（历史记录）：外部 exe 运行器需用户自装 ruffle.exe（`--player-runtime air`、日志
落盘），本方案内置 WebView + Ruffle Web（WASM）捆绑分发、开箱即用；两者共用 `GameRootDir` 与
SWF 发现逻辑。

### 3.6 反代模块（ProxyHttpModule，数据层反代）

**定位**：GameContentServer 内的数据层反代路由 —— 游戏请求照旧走 HTTP，透明把「磁盘文件」换源为
「编辑器数据层」，**不修改任何 JS / ruffle 资源**。核心价值：**预览 = 编辑器当前状态**（未保存/
未导出的改动直接生效），mod 列表与图片列表实时反映编辑器管理结果。

**路由表**（游戏实际请求面 §2.5：仅 26 个路径模式）：

| 请求 | 反代源 | 回退 |
|------|--------|------|
| `/getmods.php` | `ModManager` 实时 mod 列表（`strModName0`/`strModURL0` 格式，RESEARCH 已取证） | 磁盘原文件 |
| `<mod>/getimages.php` | 复用 `PhpParser.GenerateImagePhp`（`App/Helper/PhpParser.cs:83`）按模组图片实时生成 | 磁盘原文件 |
| `/data/<table>.xml`（×24） | `IHostService` 实体 → `IXmlParser.Export`（pma_xml_export 按表导出，与分表一一对应） | 磁盘原文件 |
| `/neogame.xml`、`/Mods/<mod>/neogame.xml` | 同上（按 mod 过滤实体） | 磁盘原文件 |
| 其余（swf / img / ruffle 静态） | 原样磁盘 / 静态 | — |

**设计要点**：
- **只读**：不提供任何写接口；仅回环（随 GameContentServer 生命周期）。
- **一致性**：每次请求实时 Export（游戏启动仅请求一次，无需缓存）；编辑器保存后自然一致，无
  缓存失效问题。
- **兼容**：未导入编辑器的 mod / 数据回退磁盘原文件 —— 预览先保证能跑，导入过才实时化。
- **R24 合规**：实体数据一律经 `IHostService` 读取，不经 `GameDbContext`。
- **与反代无关项**：Steam 模组版 43% 卡点为编辑器写出的模组数据解析问题（R6，非 Ruffle/播放器
  限制，带模组可正常加载），反代不改变；但换源后请求时序变化，实装后顺带观察一次。

### 3.7 入口设计（语义分流：开发态 vs 交付态）

**原则**：反代预览与正式启动是**两个独立入口**，数据语义不同，不得混用。

| 入口 | 数据源 | 语义/场景 | 状态 |
|------|--------|-----------|------|
| **「内置预览（实时）」**（工具栏按钮） | WebView + ProxyHttpModule —— **编辑器当前状态**（可含未保存/未导出改动，反代实时生成） | **开发态**：数据语义 = 实时态；「改→看」，不落盘、不污染磁盘 | 新增（P2.5 按钮 + P2.6 反代） |
| 「保存并启动」（现有按钮） | 先 Save & Export（磁盘导出态）→ `NEOScavenger.exe` | **交付态**：数据语义 = 磁盘态（一致）；正式游玩 / 交付验证 | 现有，不动 |

- **按钮位置**：「内置预览（实时）」位于 ModGameDataTabsView 工具栏（原有 Ruffle 按钮已随
  Docs/40 删除，P2.5 扩展为预览入口）。
- **数据一致性语义**：实时态（所见即所编，可能半成品）≠ 磁盘态（一致性有保证）。文档注明：
  **反代预览中看到的 bug 可能是未完成编辑导致的**。
- **门控方式（已定案）**：**场景分流（按钮恒可见）**，不采用编译期 `#if DEBUG` 门控 —— 该功能
  面向用户（模组作者），Release 构建同样可用。

### 3.8 子应用扩展（NeoEditor.Player 独立播放器，2026-08-05 定案）

**背景**：WebView + Ruffle Web + 反代 + 日志链路是**平台无关的播放器核心**，编辑器只是第一个宿主。
扩展为独立子应用后，可作为**完全替代 Flash 的游戏运行工具**（含未来 APK 分发）。

**项目拆分（目标态）**：

```
NeoEditor.Player.Core（新，net10.0 类库，平台无关）
├── Services/  GameContentServer / ProxyHttpModule / SwfLogBridge / RuffleWebAssets   ← 自插件上移
├── Web/       host.html + ruffle/*（Content 打包路径不变：AppContext.BaseDirectory/Web）
├── Logging/   PlayerRunLog + RunLogStore（每运行一份日志记录，供日志查看器）
├── Data/      DataBrowserService + GameDataCatalog（data/*.xml + Mods/*/*/neogame.xml 按
│              getmods.php 顺序合并 → 24 类浏览模型；复用 Infra XmlParser 管线，P6 ✅）
├── ViewModels/（播放控制、运行日志列表/过滤 —— 平台无关）
└── 依赖：Core/Infra 抽象 + Avalonia + CommunityToolkit + Serilog；**无 Windows 专用 API**

NeoEditor.Plugins.WebView（瘦身 = 编辑器宿主）
├── 引用 Player.Core；IGameDataExportService 的 Live 实现（IHostService + IXmlParser，debug 反代）
└── 编辑器 dock 面板 UI + 入口按钮（已有，逻辑不变）

NeoEditor.Player（新 exe，Avalonia 桌面，独立运行）
├── Program.cs / App.axaml.cs（独立引导，无编辑器会话）
├── 自有 UI：PlayerWindow（NativeWebView 播放区）+ LogViewerPanel（首版）
└── 磁盘数据源（无反代）；后续：P6 数据只读 ✅ / wiki 式详情 ✅；P7 net10.0-android 变体（APK）
```

**双模式映射（用户要求）**：

| 模式 | 宿主 | UI | 数据源 | 行为 |
|------|------|----|--------|------|
| 编辑器工具 | NeoEditor.Plugins.WebView | 编辑器 dock 面板 | **Live 反代**（IHostService 实时，未保存改动可见） | **debug 加载** |
| 独立包 | NeoEditor.Player | 自有 PlayerWindow + 日志面板 | 磁盘（游戏根目录） | **独立运行**，完全替代 Flash |

**关键原则**：
- Player.Core 平台无关（无 Windows 专用 API；HttpListener 回环为纯托管）→ Android 变体可整体复用
  （Avalonia.Controls.WebView 已含 `net10.0-android36.0` 目标 + `IAndroidWebViewPlatformHandle`）。
- 播放逻辑（服务器/日志/页面）只在 Core 一份 → 双宿主不漂移。
- Web 资源路径约定不变（`AppContext.BaseDirectory/Web`），两个宿主共享同一份 ruffle 资产。
- 独立包无编辑器会话 → **反代禁用**（磁盘直供）；debug 反代是编辑器模式专属能力。

**分阶段**：
- **P4（重构，零功能变化）✅**：Player.Core 抽取（服务/Web 资源上移，插件引用 Core），现有 30 测试
  迁移兜底回归。
- **P5（独立应用首版）✅**：Player 引导 + PlayerWindow（复用 host.html/ruffle/GameContentServer 磁盘
  模式）+ 日志查看器（RunLogStore：运行列表/级别过滤/详情）。
- **P6（数据只读浏览 ✅ / wiki 式详情 ✅）**：数据只读浏览已落地为「数据浏览」工具
  （v2.12-2.20：合并 24 类语义、独立弹窗、getmods.php 加载顺序）；wiki 并入数据浏览器，
  三栏 master-detail 已实施（v2.23，§四 P6）。
- **P7（后续）**：Android 工程 + APK（架构已预留；游戏数据数百 MB → APK 只含播放器 + ruffle
  ~30MB，数据外置存储/下载）。

---

## 四、实施步骤

> 排期原则：**P0 必须先行**（高风险验证）；P1/P2 可在 P0 结论明确后并行推进；
> 本计划文档登记时点均为「实施时」。

### P0 — 验证（Spike）

| # | 内容 | 状态 |
|---|------|------|
| P0.1 | **Ruffle Web 实跑游戏 SWF**（独立调研目录，2026-08-04） | ✅ **已完成**：nightly-2026-08-04 完整加载成功版数据集并进入游戏，日志全量捕获；结论见 §2.5；0.5.0 旧版备份于调研目录 `ruffle-0.5.0-backup/` |
| P0.2 | **控件验证**：`Avalonia.Controls.WebView 12.0.1`（NativeWebView）—— 渲染、加载 `http://127.0.0.1` 页面、JS 交互、CoreWebView2 暴露与否、非 Windows 行为 | ✅ **已完成（2026-08-05）**：API 面结论见 §2.1；v1 采用方案 A（回环 HTTP）；方案 B（虚拟主机映射）列为后续 |
| P0.3 | 定案：内容服务方式（A/B/C）、**Ruffle 版本锁定（nightly-2026-08-04 即 0.6.0-nightly.2026.8.4；实施时锁定最新正式版）**、`Web/` 资源打包方式（`Content` + `CopyToOutputDirectory`）、新版 selfhosted 包布局 | ✅ **已完成**：方案 A + nightly-2026-08-04 锁定（调研目录已验证资产，28MB 随包 Content，已确认流入 App 输出） |

### P1 — 通用 WebView 工具面板

| # | 内容 | 文件 |
|---|------|------|
| P1.1 | 插件骨架：csproj（net10.0 + `Avalonia.Controls.WebView` + InternalsVisibleTo） | `NeoEditor.Plugins.WebView/NeoEditor.Plugins.WebView.csproj` |
| P1.2 | `WebViewPlugin`（`[PluginKind(Workbench)]`，Right dock，Order≈20）+ `AddWebViewPlugin()` | `WebViewPlugin.cs`、`ServiceCollectionExtensions.cs` |
| P1.3 | 面板视图：地址栏 + 前进/后退/刷新 + 打开本地 HTML / URL；内容区 WebView 控件 | `Views/WebViewToolView.axaml(.cs)`、`ViewModels/WebViewToolViewModel.cs` |
| P1.4 | DI 注册 + 本地化键（zh/en-us/中性 ×3） | `App.axaml.cs`（+1 行）、`Assets/Resources*.resx` |
| P1.5 | 测试项目骨架 + 面板 VM 单测 | `Tests/NeoEditor.Plugins.WebView.Tests/` |

验收：dock 出现「WebView」工具面板；可加载本地 HTML 文件与 URL；切语言标题刷新；布局持久化
（Dock 序列化）不破坏现有布局。

### P2 — Ruffle 集成（SWF 快速预览）

| # | 内容 | 文件 |
|---|------|------|
| P2.1 | `GameContentServer`：回环绑定、路径映射、越界 404、生命周期、LRU/ETag/EMFILE 策略（§2.3 实测要求） | `Services/GameContentServer.cs` |
| P2.2 | `host.html` + ruffle 静态资源打包（版本锁定 nightly-2026-08-04+，含 LICENSE 声明） | `Web/` |
| P2.3 | 面板内「预览游戏 SWF」入口：复用 `RuffleOptionsBuilder.FindSwfPath`；SWF 缺失/未设置游戏目录时给出指引 | `WebViewToolViewModel`、resx |
| P2.4 | 日志桥：console/剪贴板/onerror 三通道 → Serilog + 每运行独立文件（§3.4）；错误覆盖层文案（含「改用 ruffle.exe 完整运行」回退提示） | `Services/SwfLogBridge.cs`、`WebViewToolView.axaml.cs`、`Web/host.html` |
| P2.5 | **「内置预览（实时）」入口**：`SwfPreviewRequestedMessage` + 工具栏按钮（§3.7 入口矩阵）→ 打开 WebView 面板并以**反代模式**加载游戏 SWF | `Messages/`、`ModGameDataTabsView.*` |
| P2.6 | **反代模块**：`ProxyHttpModule` —— getmods.php / getimages.php / data/*.xml / neogame.xml 四类路由（§3.6），经 `IHostService` + `IXmlParser.Export` + `PhpParser.GenerateImagePhp` 实时生成，磁盘回退；只读、仅回环 | `Services/ProxyHttpModule.cs`、resx |

验收：点击预览 → 面板内运行 SWF 进入主菜单（**Steam 模组版可能卡 43% —— 编辑器写出的模组数据
问题，非播放器限制，见 R6**）；
`data/`/`img/` 正常加载；游戏内部日志（剪贴板）与 Ruffle 日志进入编辑器日志；关闭面板后回环端口
释放；游戏根目录无任何写入。
**存档实测（P0 路径 B 落地为验收项）**：预览内将存档玩至 1MB+，对比 Flash 时代恶性 bug 是否复现：
- 游戏内部日志（剪贴板通道）出现同样报错序列 → 复现（bug 在游戏自身代码）
- 仅出现 `QuotaExceededError`（console 通道）→ 是 localStorage 5MB 配额新边界，非 Flash 时代 bug
- 均无异常 → 不复现（RESEARCH.md 理论成立）；同时记录存档 key 的实际序列化膨胀比

### P3 — 文档与测试完善

- 本计划登记 `index.md`；实施完成条目记入 `Docs/CHANGELOG.md`。
- 按需新增 spec 规则（R##/D##，遵循 R27/R28 格式）：建议至少覆盖「SWF 内容仅经回环 HTTP 提供、
  仅本机、只读」「Ruffle Web 为捆绑分发、版本锁定」「Docs/40 ruffle.exe 运行器已废弃」。
- 手工验收清单：游戏主菜单可进、中英切换、面板开关与布局持久化、WebView2 Runtime 缺失提示。

### P4 — Player.Core 抽取（重构，零功能变化）✅ 已完成（2026-08-05）

| # | 内容 | 文件 | 状态 |
|---|------|------|------|
| P4.1 | 服务上移：GameContentServer / ProxyHttpModule / SwfLogBridge / RuffleWebAssets / GameTableMap + Web/（host.html + ruffle）→ Player.Core（命名空间 `NeoEditor.Player.Core`） | `NeoEditor.Player.Core/` | ✅ |
| P4.2 | `IGameDataExportService` 接口入 Player.Core；**Live 实现留守插件**（依赖编辑器会话 IHostService/IXmlParser）；新增 `GamePhpGenerator`（默认 IGamePhpGenerator 实现，独立包用）与 `ProxyHttpModule.ProxyEnabled`（独立包关闭反代） | Player.Core + 插件 | ✅ |
| P4.3 | 测试迁移：30 项迁入 `Tests/NeoEditor.Player.Core.Tests`（命名空间修正），删旧测试项目 | Tests | ✅ |
| P4.4 | 插件瘦身：WebViewPlugin / VM / View 引用 Player.Core，行为不变；Web 资源经 ProjectReference 传递（App 与 Player 输出均含 Web/） | 插件 | ✅ |

验收：全解决方案构建 0 错误 ✅；30 测试全绿 ✅；编辑器预览行为与抽取前一致（资源路径约定不变）。

### P5 — NeoEditor.Player 独立应用（播放器 + 日志查看）✅ 首版完成（2026-08-05）

| # | 内容 | 文件 | 状态 |
|---|------|------|------|
| P5.1 | 独立引导：Program / App（`WithInterFont` + Fluent）；`PlayerServices` 手动组合根（磁盘模式：`ProxyEnabled=false` + `DiskGameDataExportService`） | `NeoEditor.Player/` | ✅ |
| P5.2 | PlayerWindow：NativeWebView + host.html + ruffle + GameContentServer；「打开 SWF…」文件选择（GameRootDir = SWF 目录）；重新加载 | Player | ✅ |
| P5.3 | RunLogStore（Player.Core Logging/）+ 日志查看面板（运行逐行记录 / 级别过滤 ComboBox / 清空 / 折叠） | Player.Core + Player | ✅ |
| P5.4 | 窗口缩放 letterbox（scale=showAll 已有）、Ruffle 资源缺失提示 | Player | ✅ |

验收：独立 exe（`dotnet run --project NeoEditor.Player`）运行游戏 SWF 进主菜单（磁盘数据）；日志逐运行可查；不依赖编辑器运行。

### P6 — 数据只读浏览 ✅（= game.db 只读，v2.12-2.20 落地）+ wiki 式详情 ✅（三栏 master-detail，v2.23 落地）

**数据只读浏览已完成**（Player 侧「数据浏览」工具，非 IXmlGameDataReader 原案但语义更强）：
- `DataBrowserService.BuildCatalog()`：扫描 base `data/*.xml` + 全部 `Mods/*/*/neogame.xml`，
  **按游戏加载顺序（getmods.php `strModURL{i}` 顺序，v2.20）**，按 (表名, 主键 nID→id→首列)
  行级合并（后加载覆盖 = 胜者语义，被覆盖行不显示——与游戏实际加载一致）→ `GameDataCatalog`
  （已知实体表排序优先，24 类）。
- 解析复用编辑器管线：`XmlParser`/`ValueConverter` 下沉 Infra（v2.15/2.16），Player 与编辑器
  共享同一实现；utf8 声明容错（v2.18，`File.ReadAllText` + `XDocument.Parse`）。
- UI：独立弹窗（v2.14）左表名右行摘要；游戏退出自动重置清空（v2.19）。
- **「game.db 只读」即数据浏览本身**（只读查看游戏数据）——已由上述实现覆盖（v2.21 订正），
  不再单列 EF Core 只读查询；数据源为游戏 XML（与编辑器 game.db 是同一数据的两种载体）。

**wiki 式详情 = 数据浏览器扩充（v2.22 定案 → v2.23 已实施，与数据浏览同一功能）**：数据浏览器升级为
**master-master-detail 三栏**（窗口加宽 1000×640，左 150 / 中 230 / 右自适应滚动）：
1. **24 类数据**（表列表，现有）；
2. **行 listbox**（该表合并行摘要，现有；可加搜索过滤）；
3. **wiki 式详情页**（选中行 → 字段表 + 定制渲染模板 + 交叉引用链接）。

- **渲染引擎（第三方框架调研结论，2026-08-05）**：.NET/Avalonia 生态**无独立 wiki 框架**，
  等价物为 Markdown 渲染控件。对比：
  - **LiveMarkdown.Avalonia 2.2.2（选定）** —— 编辑器已在用（`MarkdownDocument` +
    `MarkdownRenderer` + ObservableStringBuilder，App.axaml 已注册 Defaults/Styles），
    Avalonia ≥ 12 + Markdig 1.1.2，支持表格 / 代码高亮（TextMateSharp）/ 图片 / Mermaid /
    LaTeX；详情页 = 「行数据 → Markdown 模板生成 → 渲染」，**零新增依赖**。
  - Markdown.Avalonia 11.0.3（备选）—— 12.0.0-a3 仍为预发布，依赖 AvaloniaEdit 做语法
    高亮；功能与 LiveMarkdown 重叠，未引入。
- **定制渲染模板**（参考编辑器可视化做法：`[ReferenceField]` 引用解析 / FormatSegmentDisplay
  可读化 / 字段含义 tooltip / TreasureTable 递归展开；语义文档 Docs/37、Docs/38）：
  - **recipes 配方卡**：材料徽章（strTools/strConsumed/strDestroyed 的 `{mult}x{id}` →
    Ingredient 名，复用引用解析语义）、产物、fHours 耗时、nTreasureID 掉落预览、
    vAlsoTry / nHiddenID 关联配方链接。
  - **treasuretable 掉落页**：aTreasures 概率树解析（`{id}x{prob}x{qty}`、`,`=AND、`|`=OR、
    双目标：ItemType 复合键 vs 嵌套 TreasureTable，递归深度 ≤5 + 循环检测，对齐编辑器
    TreasureTable 可视化器），bNested / bSuppress / bIdentify 标注。
  - 通用表：字段表（列=值，longtext 截断/展开）；引用列 → 链接（点击跳转目标表/行，
    复用 `MarkdownRenderer` LinkCommand 导航语义）。
- **表数量订正为 24**：`GameTableMap.KnownTableNames` 实测 24 个 `[Table]` 实体
  （`NeoEditor.Core/Model/Game/`）；代码注释与本文此前版本的 "25" 为陈旧说法
  （v2.22 已订正，UI 状态栏本就动态计数不受影响）。

**v2.23 已实施**：
- `WikiDetailBuilder`（Player.Core/Data）：行 → Markdown 详情页。通用表 = 字段表 + **引用列
  db:// 链接**（`[ReferenceField]` 反射元数据：目标表/分隔符/Pattern，`{mult}x{id}` 等
  pattern 解析支持**可选尾段**（"5x1" 缺 qty））；recipes = 配方卡（工具/消耗/破坏 ×数量、
  产物 + 掉落预览前 6、替代/隐藏配方链接、耗时与标志、其余字段表）；treasuretable = 掉落
  概率树（`{id}x{mult}x{qty}` 全条目权重归一化概率、复合键 "G.S" → itemtypes、纯数字 id →
  嵌套 TT 递归 ≤5 层 + 循环检测）。
- 链接导航：`db://table/key` → VM `LinkCommand`（MarkdownRenderer LinkCommand）→ 切表 +
  `GameDataCatalog.FindRow` 定位行（RowKey → 字段值 → 点号复合键拆分匹配）。
- 渲染：LiveMarkdown.Avalonia 2.2.2 引入 Player（同编辑器包），App.axaml 注册
  Defaults/Styles + 新 `Assets/MarkdownTheme.axaml`（Light/Dark 双字典，同编辑器 token）。
- 数据层配套：`GameDataRow.RowKey` 属性化（原 DataBrowserService 私有方法上移）、
  `GameTableMap.FindTableName`（Type → 表名反查）。
- 测试：新增 `WikiDetailBuilderTests` 6 项（通用表/引用列/配方/概率/嵌套循环/空表），
  Player.Core.Tests 50/50 全绿，全解决方案 0 错误。

**v2.24 已实施（引用分析 + 图片画廊）**：
- **引用分析（入站）**：`ReferenceAnalyzer`（Player.Core/Data）—— 详情页新增「被引用」区块
  （谁引用了当前行，按来源表分组、带跳转链接与来源列）。实现：懒构建 (表|RowKey)/(表|字段值)
  查找索引一次扫描全 catalog；双目标（aTreasures 纯数字 id → 嵌套 TT）与复合键
  （"G.S" → itemtypes，线性兜底）均解析；同源同列去重；**表名 + RowKey 双校验**防跨表同 key
  误判；图片列（ImageAsset 无真实目标表）自动排除。
- **图片画廊**：`[ReferenceField(typeof(ImageAsset))]` 列（strImg/strIMG/vImageList/vSpriteList，
  含 ItemType/Creature/AttackMode/CampType/DmcPlace/DataFile/Encounters）→ 解析文件名列
  （vSpriteList 的 `{value}={id}` 取 id；防御性剥离 "ns:" 前缀）→ 存在性检查 →
  markdown 图片 **3 列网格**（表格内图片，ImageBasePath = gameRoot，
  `Uri.EscapeDataString` 处理中文/空格文件名）；缺失文件回退为 `文件名（缺失）` 文本；
  图片列从字段表排除。**R54：图片来源 = 主游戏 `img/` + 各模组 `Mods/<mod>/img/`**
  （与 ProxyHttpModule / ImageSearchService 约定一致），构造时扫描缓存目录列表，
  按 主 img → 模组 img 顺序查找——模组图片不再显示"缺失"。
- 重构：`ReferenceMetadata`（Player.Core/Data，internal）提取 [ReferenceField] 元数据 +
  段解析（RefColumn 增加 IsImage 标志），WikiDetailBuilder 与 ReferenceAnalyzer 共用。
- 测试：新增 ReferenceAnalyzerTests 6 项（跨表/跨表同 key 不误判/双目标/复合键/去重/图片列
  排除）+ WikiDetailBuilderTests 6 项（画廊网格/缺失回退/vSpriteList/ns: 剥离/被引用区块/
  无引用无区块），Player.Core.Tests 62/62 全绿，全解决方案 0 错误。

**v2.25 已实施（本地文件日志）**：
- **痛点**：日志此前只在内存（RunLogStore）+ 控制台，闪退/崩溃即丢失。
- `FileRunLogWriter`（Player.Core/Logging）：订阅 RunLogStore 新增的强类型 `LineAppended`
  事件（含 runId + 行）→ **每 run 一个文件** `player-run-{yyyyMMdd-HHmmss}-{runId}.log`
  （runId 非法文件名字符清洗）；**逐行 Flush**（闪退最多丢半行）；run 轮换与启动时清理，
  **只保留最新 2 个文件**（文件名时间戳前缀排序）；写失败静默（日志 sink 永不拖垮播放器）；
  `WriteCrash` 供异常处理器追加 `[FATAL]` 行。
- 目录策略：`{BaseDirectory}/logs`（便携、用户易找），不可写回退
  `%LocalAppData%/NeoScavengerPlayer/logs`；`LogDirectory` 暴露给 UI。
- 接线：`PlayerServices.FileLog`；`AppDomain.UnhandledException` +
  `TaskScheduler.UnobservedTaskException` → `WriteCrash`（托管崩溃保底；非托管 WebView2
  崩溃无法捕获，但逐行 flush 已保证崩溃前内容落盘）；日志覆盖层顶部显示
  `日志文件：{路径}`（VM.FileLogDirectory）。
- 测试：新增 `FileRunLogWriterTests` 6 项（写入内容/每 run 一文件/保留最新 2 个/清理预置
  旧文件/runId 清洗/crash 行），Player.Core.Tests 68/68 全绿，全解决方案 0 错误。

**v2.26 已实施（detail 两栏 + 图片画廊修复）**：
- **detail 拆两栏**（用户反馈）：主体（字段表/配方卡/掉落树）左栏，**图片画廊 + 被引用
  右栏侧边**（宽 280）。`WikiDetailBuilder` 拆分 `BuildDetail` / `BuildReferences` /
  `GetImageItems`（`Build` 保留完整输出兼容测试）；VM 增加 `SideMarkdown`（引用）、
  `Images`（`WikiImage` 集合）、`HasImages`/`HasReferences`。
- **图片缺失修复（根因）**：画廊此前 = markdown 表格单元格内图片 + `ImageBasePath`
  相对路径解析 —— ①`ImageBasePath` 是 get-only 属性（无通知，窗口复用场景绑定值
  固定）；②markdown 表格内图片在 LiveMarkdown 渲染不可靠（编辑器自身也从不用
  ImageBasePath，而是把图片重写为绝对 file URI）。修复：画廊改 **原生 `Image` 控件**
  直接解码 `FullPath` 绝对路径（确定性加载），WrapPanel 缩略图 72×72 + 文件名标注；
  缺失文件显示 `文件名（缺失）` 文本；引用侧栏仍走 MarkdownRenderer（保留 db:// 链接
  跳转）。移除 `ImageBasePath` 绑定。
- 测试：新增 3 项（GetImageItems 存在/缺失、BuildDetail 不含画廊与引用、
  BuildReferences 只含引用），Player.Core.Tests 71/71 全绿，全解决方案 0 错误。

**v2.27 已实施（win-only 打包裁剪，release 1G → 129MB）**：
- **问题**：win-x64 publish 255MB（整个 bin 目录 979MB）——SkiaSharp PDB 符号 ~330MB、
  Skia/HarfBuzz 全平台 native（linux-musl-x64/arm64/arm、osx、win-x86/arm64）~200MB、
  WebView2 运行缓存写入输出目录、EF Core/SQLite 随 Infra 传递进包。
- **依赖裁剪**：Player.Core **移除 NeoEditor.Infra 引用**（EF Core 栈整体退出 Player
  链路）——①`GameTableMap` 自反射实体模型（`[Table]` 扫描，替代 Infra.Constants）；
  ②`IConfigService` 抽象接口**移到 Core/Abstractions**（App/插件/测试逐文件补 using 或
  别名，EntityEditorDocument 保持别名风格）；③删除 Player.Core 死代码
  ServiceCollectionExtensions 后因 WebView 插件实际调用而恢复，改引
  `Microsoft.Extensions.DependencyInjection.Abstractions`（仅抽象，无 EF Core）。
- **win-only**：`Avalonia.Desktop` → **`Avalonia.Win32`**（不打包 X11/macOS 后端）、
  `UsePlatformDetect` → **`UseWin32`**、固定 `RuntimeIdentifier=win-x64` +
  `SelfContained`（只复制 win-x64 native）；**其他平台通过新增编译目标
  （RuntimeIdentifier linux-x64 / TFM net10.0-android36.0）支持，绝不混入本目标**
  （csproj 注释写明策略）。**Avalonia 12 分包注意**：渲染与文本整形是独立包——
  Win32 后端需补 **`Avalonia.Skia` + `UseSkia()`** 与 **`Avalonia.HarfBuzz` +
  `UseHarfBuzz()`**（实机报 `No rendering system configured` / `No text shaping
  system configured` 后补上；Avalonia.Desktop 元包此前隐式携带）。
- **发布配置**：Release 构建 `DebugType=None`（PDB 排除）+ post-publish target 删除
  NuGet 原生包自带的 PDB（libSkiaSharp.pdb ~81MB，DebugType 管不到）；
  `WEBVIEW2_USER_DATA_FOLDER` 环境变量把 WebView2 缓存移到
  `%LocalAppData%/NeoScavengerPlayer/WebView2`（输出目录不再被写缓存）。
- **体积实测**：win-x64 self-contained publish **255MB → 143MB**（.NET 运行时 ~70MB +
  ruffle wasm 28MB + Avalonia/Skia/HarfBuzz win-x64 单平台 + TextMateSharp 6.5MB；
  EF Core/SQLite 0、PDB 0、多平台 native 0；启动冒烟通过）。推荐发布命令：
  `dotnet publish NeoEditor.Player -c Release -o dist -p:PublishSingleFile=true -p:DebugType=None`
  （单文件可再省文件数；PublishTrimmed 需 `TrimmerRootAssembly` 保留 Core/Player.Core
  反射程序集，未启用——可选后续优化）。
- **事故记录**：裁剪过程中误用 `sed 0,/re/d` 破坏
  `EntityEditorDocument.cs` 的 using 区并 `git checkout` 还原了该文件**未提交的 R24/
  XML-diff 修改**；已通过反编译最后一次成功构建的 DLL（ilspycmd）逐方法移植恢复
  （SaveDocument 走 IHostService 管道、IsDiffView/DiffOld/DiffNew、ResolveOriginalXml/
  FindOriginalEntity/RefreshDiff/IsPrimaryKeyColumn、ApplyXmlToEntity 主键保护、
  EntityXmlHelper 过滤 IEntity 声明属性），测试全绿确认语义一致。

### P7 — Android / APK（后续，架构已预留）

- `NeoEditor.Player.Android`（net10.0-android）：复用 Player.Core（平台无关已验证原则）；
  Avalonia.Controls.WebView 的 `net10.0-android36.0` 目标 + `IAndroidWebViewPlatformHandle`；
  回环 HTTP 在 Android 的可用性需实机验证（O7）。
- 游戏数据外置存储/下载（SWF+data+img 数百 MB），APK 只含播放器 + ruffle（~30MB）。

---

## 五、涉及文件清单（预估）

**新插件**：`NeoEditor.Plugins.WebView/`（csproj、WebViewPlugin.cs、ServiceCollectionExtensions.cs、
ViewModels/WebViewToolViewModel.cs、Views/WebViewToolView.axaml(.cs)、Services/GameContentServer.cs、
Services/ProxyHttpModule.cs、Services/RuffleWebAssets.cs、Services/SwfLogBridge.cs、Web/host.html +
ruffle/ 静态资源 + LICENSE*）

**新项目（§3.8 子应用扩展）**：`NeoEditor.Player.Core/`（服务/Web/Logging/Data/ViewModels 上移）、
`NeoEditor.Player/`（独立应用：Program、PlayerWindow、LogViewerPanel）、（未来
`NeoEditor.Player.Android/`）

**Tests**：`Tests/NeoEditor.Plugins.WebView.Tests/`（现有 30 项）、`Tests/NeoEditor.Player.Core.Tests/`
（P4 迁移）、（未来 `Tests/NeoEditor.Player.Tests/`）

**App**：`App.axaml.cs`（+AddWebViewPlugin 注册）、`Assets/Resources*.resx`（+键，zh/en-us/中性）、
`Docs/index.md`（登记）、`Docs/CHANGELOG.md`（实施后）

**可选**：`NeoEditor.App/Views/UserControls/ModGameDataTabsView.*`（P2.5 预览按钮，与现有 Ruffle
按钮并列）

**Tests**：`Tests/NeoEditor.Plugins.WebView.Tests/`（新）

**不涉及**：`NeoEditor.Core` / `NeoEditor.Infra` 现有代码（SWF 发现复用 Core 已有纯函数，不新增
依赖）；`IRuffleRunner` 体系零改动。

**外部参考（只读，不复制进仓库）**：调研目录
`D:\software\Steam\steamapps\common\ruffle-0.5.0-web-selfhosted\` —— `index.html`（§3.3/§3.4
设计输入）、`static-server.js`（§3.2 设计输入）、`RESEARCH.md`（结论）、`logs/success.log`（证据）。

---

## 六、测试计划

| 测试 | 覆盖 | 位置 |
|------|------|------|
| `GameContentServerTests` | 回环绑定、随机端口、路径映射、越界 404、MIME（wasm/swf/php→text/plain）、ETag/304、缓存策略、Stop 释放端口 | WebView.Tests |
| `GameContentServerStressTests` | 图片请求洪峰（并发 ~3000）下无 EMFILE/句柄泄漏 | WebView.Tests |
| `WebViewToolViewModelTests` | 地址解析、打开本地文件、SWF 入口（FindSwfPath 复用：存在/缺失/多 swf） | WebView.Tests |
| `ProxyHttpModuleTests` | 四类路由命中/回退：getmods.php 格式（strModName0/strModURL0）、getimages.php 复用 PhpParser、data/*.xml 按表 Export 内容与磁盘等价、未导入 mod 回退、只读（无写接口）、越界 404 | WebView.Tests |
| `RuffleWebAssetsTests` | 静态资源存在性、版本号、LICENSE 声明 | WebView.Tests |
| `host.html` 结构测试 | console 拦截先于 ruffle.js、剪贴板三通道存在、`?swf=` 参数 | WebView.Tests（文本断言） |
| `RunLogStoreTests`（P5） | 运行日志记录/级别过滤/持久化路径 | Player.Core.Tests |
| 手工验收 | 真实游戏 SWF 面板内运行（§四 P2 验收）、中英切换、布局持久化、WebView2 Runtime 缺失提示、Steam 模组版 43% 卡点的表现与提示 | — |

---

## 七、风险与开放问题

| # | 问题 | 现状/建议 |
|---|------|----------|
| R1（已降级） | 游戏 SWF 为 AIR 目标，web 版 Ruffle 无 AIR runtime | **P0.1 已实测缓解**：nightly-2026-08-04 完整加载成功版数据集并进入游戏（TypeError 0）；残余差异见 R6/O1；本功能定位快速预览，完整游戏仍走 ruffle.exe |
| R2（中） | `Avalonia.Controls.WebView 12.0.1` 为较新官方控件：API 面、稳定性、非 Windows 行为未验证 | P0.2 最小工程验证；Windows 优先，非 Windows 平台行为定级后决定隐藏或降级 |
| R3（已关闭） | WebView2 Runtime 缺失（个别精简版 Win10） | **v2.68 已实施**：启动注册表检测 + 缺失弹窗（提示 + 官方安装链接，「打开安装页面」直达下载页）；控件创建异常兜底提示保留 |
| R4（低） | 回环 HTTP 服务安全 | 仅 127.0.0.1 + 随机端口 + 面板生命周期内运行 + 路径越界 404；不做任何写接口 |
| R5（低） | ruffle 版本漂移（0.5.0 过旧缺 Loader 等） | **版本锁定 nightly-2026-08-04（0.6.0-nightly.2026.8.4）起，实施时锁定最新正式版** + `RuffleWebAssets` 记录版本；升级走独立变更 |
| **R6（高）** | **Steam 模组版（编辑器默认 GameRootDir）卡预加载 43%「更新template-based items」** —— **主要是编辑器写出的模组数据存在问题**（成功版数据集 ~40 模组同位置完整通过 → 带模组可正常加载，非 Ruffle/播放器限制） | 快速预览在排查完成前可能卡 43%：P2 验收需「卡进度提示 + 回退 ruffle.exe」兜底；根因排查（逐模组二分）可选跟进，成功版数据集可作对比基线 |
| R7（中） | 加载时长 5-10 分钟（模组多时数据解析慢） | `maxExecutionDuration: 600` + 加载进度提示；预览定位「慢但可用」，日志可观察进度 |
| R8（低） | 图片请求洪峰（~3000 请求）压垮服务器 | P0.1 已实测解法：整读入内存 + LRU 256MB + EMFILE 重试 + ETag/304（static-server.js） |
| O1 | `flash.display.Loader.load()` stub 是否影响后续运行 | 0.5.0 实测为 stub；成功版数据集完整通过（图片经 URLLoader 并行加载成功），**最新 nightly 是否已实现待重测**；运行时持续观察 |
| O2 | 预览态存档（SharedObject → localStorage）：Flash 时代 1MB 存档恶性 bug 源于 Flash 默认 SO 限额（100KB），**理论上 Ruffle 不复现（待实测）**；新边界 = localStorage 整站 ~5MB（UTF-16 字符计，按 origin 共享），多存档累积 + LSO 序列化开销会提前占额；存档若走 AIR File API 则 web 版为回退路径 | 预览默认不承诺持久化（快速预览定位）；实测方案已入 P2 验收（存档 1MB+ 三通道观察）；完整存档走 ruffle.exe；面板提供「清空预览存档」入口（`localStorage` 按前缀清理） |
| O3 | 剪贴板读取权限需用户首次点击/按键授权 | WebView2 内用户操作面板即可触发；授权失败仅丢游戏内部日志通道，Ruffle 日志不受影响 |
| R9（中） | P4 Core 抽取回归（行为变化/资源路径漂移） | 30 测试兜底 + 双宿主手工验收；Web 资源路径约定不变（`AppContext.BaseDirectory/Web`） |
| R10（低） | 双宿主功能漂移（播放器逻辑两处实现） | 播放逻辑（服务器/日志/页面）只在 Player.Core 一份，插件与 Player 均引用 |
| R11（中） | Android：WebView 控件 Android 后端 / HttpListener 回环在 Android 的行为未验证 | P7 单独验证（实机）；本期仅架构预留 |
| R12（中） | 游戏数据体积（SWF+data+img 数百 MB） | APK 只含播放器 + ruffle（~30MB），游戏数据外置存储/下载 |
| O6 | 独立包数据源：无编辑器会话 → 反代禁用（磁盘直供）；原「挂载编辑器 game.db 只读」设想 | P5 起按磁盘模式；「game.db 只读」= 数据浏览（P6 ✅，Player 数据浏览工具：游戏 XML 只读呈现，非 EF Core 查询），挂载编辑器数据库不作单列项 |
| O4 | 反代格式兼容：getmods.php（strModName0/strModURL0）与 getimages.php 的输出格式须被游戏原样解析；data/*.xml 反代内容须与磁盘导出等价（游戏按行解析、不可按表名去重） | 格式已由 RESEARCH.md §2.2/§2.3 取证；P2.6 实现时以「反代输出 vs 磁盘文件」字节级对比测试兜底（ProxyHttpModuleTests） |

---

## 八、Player 发布流程（win-only，v2.27 起）

> 裁剪后的发布**仅面向 Windows x64 单平台**（Avalonia.Win32 + Skia/HarfBuzz + 固定 RID）。
> 其他平台 = **新增编译目标**（RuntimeIdentifier linux-x64 / TFM net10.0-android36.0），
> 不混入本包（csproj 策略注释）。

### 前置条件

- .NET SDK 10（global.json 锁定）；首次构建需联网恢复 NuGet。
- 目标机器：Windows 10/11 x64 + **WebView2 Runtime**（系统自带；精简版系统需安装）。
- ruffle 资产锁定（`Web/ruffle`，nightly-2026-08-04，随 Player.Core Content 打包，勿手动改）。

### 发布方式（v2.33 起：GitHub Actions 自动 release + 本地脚本）

**方式 A — GitHub Actions 自动 release（推荐，v2.33；R59 起两条发布线分开）**：
播放器与编辑器**独立 workflow、独立 tag 前缀**——发播放器只发播放器包，不混发：
- 播放器：`.github/workflows/release-player.yml`，打 `player-vX.Y.Z` tag → 只发 `NeoScavengerPlayer-{X.Y.Z}-win-x64.zip`
- 编辑器：`.github/workflows/release-editor.yml`，打 `editor-vX.Y.Z` tag → 只发 `NeoEditor-{X.Y.Z}-win-x64.zip`
```bash
git tag player-v1.0.1 && git push origin player-v1.0.1   # 只发播放器
git tag editor-v1.0.0 && git push origin editor-v1.0.0   # 只发编辑器
```

**方式 B — 本地交互脚本 `publish.ps1`（仓库根，v2.33）**：
```powershell
.\publish.ps1          # 交互菜单
.\publish.ps1 -Single  # 跳过菜单：测试 → 单文件发布 → 打包 zip
```
菜单选项：发布并打包（单文件/多文件）、仅发布、运行测试、打开输出目录、退出；
取消 = 菜单输入 0/q 或随时 Ctrl+C。zip 命名 `NeoScavengerPlayer-{版本}-win-x64.zip`
（版本默认当天日期，可输入）。

### 发布命令（脚本/CI 内部等价）

```bash
# 推荐（self-contained + 单文件，产物 ~143MB）
dotnet publish NeoEditor.Player -c Release -o dist -p:PublishSingleFile=true -p:DebugType=None

# 标准（多文件，便于排查）
dotnet publish NeoEditor.Player -c Release -o dist
```

csproj 已内置（v2.27）：`RuntimeIdentifier=win-x64` + `SelfContained`；Release 配置
`DebugType=None`；`StripPublishedSymbols` target（删除 NuGet 原生包自带 PDB，
libSkiaSharp.pdb ~81MB）；Avalonia.Win32/Skia/HarfBuzz（无 X11/macOS 后端、无 EF Core）。

### 产物结构与体积（~143MB）

```
dist/
├── NeoScavengerPlayer.exe        # self-contained；单文件模式含 .NET 运行时
├── *.dll                         # Avalonia / Skia / WebView / LiveMarkdown / Serilog…
└── Web/ruffle/                   # ruffle.js + core.*.js + *.wasm（~28MB，单文件模式外置）
# 无 PDB、无 EF Core/SQLite、无 runtimes/ 多平台 native、无 WebView2 缓存
```

### 运行时落盘（不随包分发）

| 路径 | 内容 |
|------|------|
| `%LocalAppData%/NeoScavengerPlayer/settings.json` | 主题 / 语言持久化 |
| exe 旁 `logs/`（不可写时回退 `%LocalAppData%/NeoScavengerPlayer/logs/`） | `player-run-*.log`，每 run 一个，保留最新 2 个；导出产物（`player-log-export-*.txt`、`NeoScavengerPlayer-export-*.zip`）也落这里 |
| 同上 `logs/` | `player-boot-*.log`，每启动一个，保留最新 5 个——**启动里程碑 + 崩溃原因**（v2.69；启动即闪退时凭它定位，游戏/运行日志另在 `player-run-*.log`）；反馈 zip 一并包含 |
| `%LocalAppData%/NeoScavengerPlayer/WebView2/` | WebView2 缓存（EBWebView） |

### 发布前校验清单

1. `dotnet build NeoEditor.sln` → 0 错误；`dotnet test NeoEditor.sln` → 13 项目全绿。
2. publish 产物 < 200MB、**无 `*.pdb`**、无 `runtimes/` 多平台目录、Web/ruffle 存在。
3. 冒烟：运行 exe 数秒无异常；拖入 SWF → 游戏进主菜单（Steam 模组版卡 43% 为编辑器写出的
   模组数据问题，非播放器限制，见 R6）。
4. 数据浏览：合并 24 类、详情两栏、图片画廊、被引用链接跳转。
5. 主题（跟随系统/亮/暗）与语言（中/英）切换即时生效，重启后保持。
6. 日志：覆盖层分类过滤（console/clipboard/warn/error/debug）；游戏退出后 `logs/`
   有文件且目录只剩最新 2 个。
7. 游戏内退出 → 回到「把 SWF 文件拖到这里」占位态，无残留音频/进程。
8. **存档闭环（R37 起）**：读档/保存跨重启可靠；存档管理删档 → 重启不复活；
   删档后玩新档 → 正常关闭不丢进度；`save_backup/` 有自动备份。
9. **调试工具（R42/R43）**：启动**无剪贴板权限弹窗**且系统剪贴板不被游戏日志污染
   （剪贴板内容只出现在日志 level=clipboard）；F12/调试菜单开 DevTools；导出日志 /
   导出存档+日志 zip 生成文件并在 Explorer 定位；「关于」显示 v1.0.0。
10. 报错捕捉（R42）：DevTools Console 执行 `window.__log("error","TypeError: test")`
    → 状态栏警示 + 弹窗「检测到游戏错误」。

### 版本号

- 分发名固定 `NeoScavengerPlayer`（AssemblyName）；版本号 = csproj `<Version>`（R43 起，
  当前 `1.0.1` 内测版，2026-08-09 已发 player-v1.0.1），窗口标题/About/启动日志/导出 zip 命名同源；publish.ps1 的 zip
  默认命名读取该值，git tag `vX.Y.Z` 走 GitHub Actions 自动 release。
- ruffle 版本锁定 nightly-2026-08-04；升级走独立变更（替换 `Web/ruffle` + 更新本文档）。

### 常见问题

- **杀毒/Defender 误报** self-contained 单文件：加白名单或改多文件发布。
- **SmartScreen「发布者未知/已保护你的电脑」**：未签名 exe 从网络下载（带 MOTW）的正常提示——右键 zip/exe → 属性 → 解除锁定，或「更多信息 → 仍要运行」；代码签名（Azure Trusted Signing / 商业证书）列入后续计划（R61）。
- **WebView2 Runtime 缺失**：启动时检测（v2.68）——缺失即弹窗（提示 + 官方安装链接，「打开安装页面」直达下载页）；控件创建异常兜底提示仍在。
- **端口占用**：GameContentServer 随机回环端口，冲突自动重试。
- **日志文件打不开**：日志 sink 以 FileShare.ReadWrite 打开，可并发读；保留策略自动清理。

---

## 九、文档登记

- 本计划：`Docs/42-webview-ruffle-preview-plan.md`（编号 42 顺延 —— 41 已被
  `41-save-workflow-onboarding-plan.md` 占用）。登记到 `index.md`「当前计划」待实施时执行。
- 与 Docs/40 的关系：**已取代**（§3.5）—— Docs/40 外部 ruffle.exe 运行器 2026-08-05 整体删除，
  文档标记废弃仅作历史参考；`RuffleOptionsBuilder.FindSwfPath` 保留复用。
- spec 规则：见 P3，按需新增（本功能不改变 R07/R24 分层与数据管道）。
- 版本历史：
  - v1.0（2026-08-04）：初稿（调研：官方控件确认 + Ruffle Web 事实 + 方案对比）。
  - v1.1（2026-08-04）：**P0.1 完成** —— 并入实测结论（§2.2/§2.3/§2.5/§3.2-3.4）、版本要求
    （0.5.0 过旧 → nightly-2026-08-04）、R1 降级 + 新增 R6（模组版 43% 卡点）。
  - v1.2（2026-08-04）：**存档机制结论同步**（调研目录 RESEARCH.md §4）—— SharedObject →
    localStorage（无 Flash 100KB SO 限额，Flash 时代 1MB 存档 bug 理论上不复现，待实测）；
    更新 O2、§2.5 补充、P2 验收新增「存档 1MB+ 三通道实测」项。
  - v1.3（2026-08-04）：**反代模块定案**（调研目录运行日志实测）—— §2.5 补充游戏实际请求面
    （26 路径模式）、新增 §3.6 ProxyHttpModule（getmods.php / getimages.php / data XML / neogame.xml
    四类路由，经 IHostService + IXmlParser.Export + PhpParser 实时生成，磁盘回退）、P2.6 实施项、
    ProxyHttpModuleTests、O4 格式兼容风险。
  - v1.4（2026-08-04）：**入口分流定案** —— 新增 §3.7 入口矩阵（「内置预览（实时）」= debug 态
    反代入口；「保存并启动」/「用 Ruffle 启动」= release 态磁盘入口）、P2.5 改为预览入口按钮、
    O5 门控方式开放问题。
  - v1.5（2026-08-04）：**门控方式定案** —— O5 关闭：场景分流、按钮恒可见，不采用编译期
    `#if DEBUG` 门控（功能面向用户，Release 构建同样可用）。
  - v1.6（2026-08-04）：**入口语义澄清** —— 「保存并启动」与「用 Ruffle 启动」确认同一语义
    （正式启动 · 磁盘态，播放器为正交维度）；§3.7 矩阵收敛为两语义（实时态 vs 磁盘态）；
    记录现有「用 Ruffle 启动不先保存」的细节差异（不改动，可选演进另立变更）。
  - v1.7（2026-08-05）：**P1/P2 首版开发完成** —— 新插件 `NeoEditor.Plugins.WebView`（P1.1-P1.5）、
    GameContentServer（P2.1，方案 A + MIME/越界/__log）、host.html + ruffle nightly-2026-08-04 打包
    （P2.2）、日志三通道（P2.4）、反代 ProxyHttpModule（P2.6：data/*.xml 实时 Export、getmods.php/
    getimages.php 磁盘优先缺失生成、根 neogame.xml 故意 404）、入口按钮 + 面板激活（P2.5）；
    P0.2/P0.3 完成（§2.1）；测试 30/30 通过；全解决方案构建 0 错误。
    实现差异：getmods.php/getimages.php 采用「磁盘优先，缺失时从 Mods//img 扫描生成」（§3.6
    原为直接生成）；`IGamePhpGenerator` 抽象新增于 Core（App PhpParser 实现）以满足插件分层。
  - v1.8（2026-08-05）：**子应用扩展定案（设计先行）** —— 新增 §3.8 NeoEditor.Player 独立播放器
    架构（Player.Core 共享核心 / 插件瘦身 = 编辑器宿主 / Player 独立应用；双模式映射：编辑器
    debug 反代加载 vs 独立包自有 UI 磁盘运行；APK 架构预留）；实施分阶段 P4（Core 抽取，零功能
    变化）/ P5（独立应用首版：播放器+日志）/ P6（数据只读+wiki，后续）/ P7（Android，后续）；
    新增 R9-R12、O6；文件清单/测试计划同步。本期仅改文档，代码重构另行排期。
  - v1.9（2026-08-05）：**P4/P5 完成** —— Player.Core 抽取（服务/Web/GameTableMap 上移命名空间
    `NeoEditor.Player.Core`，新增 RunLogStore/GamePhpGenerator/ProxyEnabled 磁盘模式开关，测试
    30 项迁入 Player.Core.Tests）；NeoEditor.Player 独立应用首版（手动组合根、PlayerWindow、
    日志查看面板，磁盘模式无反代）；全解决方案构建 0 错误、30 测试全绿、Web 资源经
    ProjectReference 传递到 App 与 Player 两个输出。实现差异：PlayerViewModel 暂留 Player 应用
    （待编辑器面板复用同一 VM 时再上移 Player.Core）；P5.4 的 WebView2 Runtime 缺失提示由控件
    加载异常兜底（视图层 try/catch 显示错误文本）。
  - v2.0（2026-08-05）：**生命周期语义定案 + 实机修复** —— 游戏运行在 WebView 页面内（无独立
    进程），停止 = 页面卸载/导航：关闭窗口（进程退出 + 服务器 Dispose）/ 打开别的 SWF（不同 URL
    导航，旧页面连同 wasm/音频/定时器销毁）/ 重新加载（Refresh）均正确停止旧游戏；**修复同 URL
    导航可能被 WebView2 忽略的缺陷**：两个宿主 VM 统一 `RequestNavigate`（目标 == 当前 URL →
    Refresh，否则 Navigate），Player 新增「停止」按钮（导航 about:blank）。实机修复：Player 缺
    app.manifest（NativeControlHost 子窗口创建失败）、StorageProvider `Path.AbsolutePath` 在
    Windows 为 `/D:/` 风格（改用 `Path.LocalPath`）、`__log` 的 run 字段为数字（SwfLogBridge
    类型容错，新增 NumericRunId 测试，31/31 绿）。
  - v2.1（2026-08-05）：**Docs/40 ruffle.exe 外部运行器删除** —— `IRuffleRunner` /
    `RuffleRunnerService` / `RuffleLocator` / `RuffleOptionsBuilder.Build` / 工具栏按钮 /
    resx 键（RuffleLaunch 等 5 键 ×3 语言）/ 相关测试全部删除；`RuffleOptionsBuilder` 精简为
    SWF 发现工具（`FindSwfPath` 保留，供预览复用）；ModGameDataTabsView 清理 Ruffle 字段/事件/
    按钮；Docs/40 标记废弃（§3.5 改写为"取代"）；§3.7 入口矩阵收敛为两入口（内置预览 +
    保存并启动）；构建 0 错误、Core.Tests 74/74 + Player.Core.Tests 31/31 全绿。
  - v2.2（2026-08-05）：**自适应 + 全屏** —— host.html 增加 `forceScale: true`（忽略 SWF 自身
    scaleMode/noScale，窗口 resize 时画面等比自适应；若个别 SWF 出现布局异常可移除）；Player
    新增「全屏」按钮 + F11 切换 `WindowState.FullScreen`（ESC 退出；WebView2 子窗口持焦时键盘
    可能被吞，按钮为主入口）；编辑器面板全屏受 dock 限制不做（自适应天然具备）。
  - v2.3（2026-08-05）：**无边框全屏 + Steam 式覆盖层** —— 游戏内全屏路径取证：ESC →
    `GUIEscMenu` → 全屏按钮/分辨率按钮（800/1024/1360/1600/2400/AUTO）→ `Stage.displayState`
    （Ruffle 已实现：wasm `set_display_state` + JS `requestFullscreen`，游戏内全屏直接可用）；
    Player 窗口全屏即无边框（Avalonia FullScreen 行为）。**日志覆盖层**：WebView2 是原生子窗口
    （Avalonia 控件无法覆盖），覆盖层实现为独立无边框 Topmost 全屏窗口（`LogOverlayWindow`，
    半透明 + 日志列表/级别过滤/清空）；触发 = 「日志」按钮 或 页面内 **Shift+Tab**（host.html
    capture 阶段监听 → `chrome.webview.postMessage` → `NativeWebView.WebMessageReceived` →
    ToggleLogOverlay，Steam shift+tab 同款交互）；底部日志面板移除（统一覆盖层）。
  - v2.4（2026-08-05）：**覆盖层修复** —— ①Shift+Tab 被 Tab 焦点导航捕捉：覆盖层打开时焦点在
    Avalonia 窗口（页面桥收不到键），改为覆盖层窗口自身**隧道阶段**（`AddHandler` +
    `RoutingStrategies.Tunnel`，先于焦点导航）捕获 Shift+Tab → 关闭；关闭后 `Activate()` 主窗口
    归还焦点给 WebView，页面桥继续负责"再按打开"。②覆盖层按钮黑块不可见：窗口强制
    `RequestedThemeVariant="Dark"` + 按钮显式 `Background="#E8E8E8"`/`Foreground=Black`。
  - v2.5（2026-08-05）：**游戏 Flash 事件捕捉** —— 取证：Ruffle 实现了 `System.exit`
    （wasm `System.exit`/`system::exit`）但 **fscommand 命令集无 quit**（会被静默忽略）；游戏基于
    Flixel（`org.flixel:FlxSave`），ESC 菜单 `m_btnQuit`/`m_btnExit` 为退出入口；`navigateToURL`
    在 `openUrlMode=deny` 下拦截并打日志。实现：`SwfLogBridge` 增加**游戏事件识别层**（模式表：
    GameExit / NavigationBlocked / ApiStub + 10 秒去抖）→ `GameEventDetected` 事件；Player 响应
    （GameExit → 状态提示 + 停止，不自动关窗防误报）；编辑器面板响应（toast）。**模式表为 v1
    候选，待实证校准（O8）**：进游戏点 ESC→退出，看日志覆盖层实际输出后收紧/放宽正则。
  - v2.6（2026-08-05）：**O8 校准完成** —— 用户实测确认游戏退出路径 = `fscommand("quit")`，
    Ruffle 日志 `unknown FSCommand:quit`；GameExit 模式校准为 `FSCommand:\s*quit`（主信号，
    中文「退出游戏」保留为剪贴板日志辅助信号）；新增 `SwfLogBridgeTests` 4 项（FSCommand 检测 /
    中文剪贴板检测 / 无关日志不误报 / 10 秒去抖），35/35 全绿。
  - v2.7（2026-08-05）：**失焦暂停补齐 + 停止按钮移除** —— 取证：游戏用 `Event.DEACTIVATE` +
    `onFocusLost`（Flixel）实现失焦暂停；Ruffle 只有 `document.visibilitychange`（
    `backgroundExecutionMode=None` 默认）→ **仅最小化/切 tab 暂停，窗口失焦不触发**。补齐：
    host.html 监听 `window blur/focus`（宿主窗口失焦必然触发）→ 调用 RufflePlayer 公开
    `pause()`/`play()`（`window.__player` 全局暴露），覆盖层打开时同样暂停（Steam 行为一致）。
    「停止」按钮移除（GameExit 响应的 StopCommand 保留）。
  - v2.8（2026-08-05）：**加载期不暂停 + 后台运行选项 + 退出释放 + 拖拽启动** —— ①失焦暂停
    仅在 Ruffle `loaded` 事件后生效（加载中不暂停）；「后台运行」ToggleButton（
    `window.__backgroundMode`，开启后失焦不暂停、声音继续，切换瞬间按焦点状态立即生效）。
    ②游戏退出/关窗时先 `InvokeScript(player.destroy())` 停音频/AVM 再导航/释放（修复退出后
    音效残留）。③拖拽启动：SWF 拖到 exe（命令行参数 → `App.StartupSwfPath` → 窗口 Loaded 后
    自动加载）+ 运行中拖入窗口（`DragDrop` + `DataFormat.File` → `IStorageItem`，Avalonia 12
    API：`DragEventArgs.DataTransfer`/`DataFormat` 替代旧 `Data`/`DataFormats`）。
    实现差异：ToggleButton 的 Checked/Unchecked 在 Avalonia 12 已移除（改用 Click 读 IsChecked）；
    NativeWebView 无 Dispose（依赖 destroy() 停音频 + 进程退出清理）。
  - v2.9（2026-08-05）：**退出真正生效 + 后台运行改 Switch + 拖放占位区** —— ①**根因修复**：
    GameEvent 从日志服务器线程触发，此前直接在非 UI 线程调 Avalonia 控件 → 停止/导航被吞；
    现在 `HandleGameEvent` 全部 marshal 到 `Dispatcher.UIThread.Post`，且 **GameExit 升级为关闭
    播放器窗口**（`ExitRequested` 事件 → `Close()`，真正的"退出"）。②「后台运行」改 Fluent
    **ToggleSwitch**（绑定 VM.BackgroundMode → `BackgroundModeChanged` 事件 → 页面
    `window.__backgroundMode`）。③**初始拖放占位区**：WebView 改为**懒创建**（首次导航时才
    创建，原生子窗口无法被 Avalonia 控件覆盖），未加载时中间显示「把 SWF 文件拖到这里 / 或点击
    浏览」占位控件（可拖可点）；host.html 阻止 Chromium 默认 dragover/drop（播放区拖文件不再
    变下载）。编辑器面板 GameEvent 响应同样 marshal UI 线程。
  - v2.10（2026-08-05）：**退出彻底关闭 + 宿主失焦暂停 + 覆盖层跟随窗口** —— ①退出后进程残留/
    音频继续的根因：Avalonia `ShutdownMode` 默认 `OnLastWindowClose`，日志覆盖层还开着时主窗口
    关闭不退出进程；显式 `OnMainWindowClose` + 退出序列（destroy → 导航 about:blank → 250ms
    → 关覆盖层 → 关主窗口）。②失焦暂停改用**宿主 `Deactivated`/`Activated`**（WebView2 不向页面
    投递 window blur，页面方案失效；宿主窗口事件可靠），`_backgroundMode` 由 switch 同步。③覆盖
    层尺寸跟随主窗口：全屏 → 覆盖层全屏；非全屏 → Position/Width/Height 对齐主窗口。
  - v2.11（2026-08-05）：**游戏退出回退待加载态** —— GameExit 不再关闭播放器（关应用用窗口 X），
    改为回退到「把 SWF 文件拖到这里」初始状态：VM 重置（状态/当前 SWF/URI）→ `ResetRequested`
    → 视图 `destroy()` + **从可视树移除 WebView 控件**（原生子窗口销毁 → WebView2 音频彻底停止，
    比导航 about:blank 更干净）→ placeholder 重新可见。App 的 `OnMainWindowClose` ShutdownMode
    保留（窗口 X 关闭仍正常退出进程）。
  - v2.12（2026-08-05）：**菜单栏 + 数据浏览侧边工具** —— 顶栏按钮组改为紧凑菜单栏（文件：
    打开 SWF Ctrl+O / 重新加载 F5 / 退出；视图：全屏 F11 / 日志 Shift+Tab / **数据浏览** / 后台
    运行勾选），状态文本右对齐菜单栏右侧；欢迎页（placeholder）加菜单功能介绍。
    **数据浏览**：`DataBrowserService`（Player.Core/Data，只读解析 data/*.xml + Mods/*/*/neogame.xml
    的 pma_xml_export → 行/字段，路径越界防护，纯读不写）+ `DataBrowserViewModel` + 侧边覆盖窗口
    `DataBrowserWindow`（无边框独立 HWND——WebView 原生子窗口无法被窗口内控件覆盖，同日志覆盖层
    机制；**覆盖而非挤压**，右侧对齐主窗口，宽 400，DPI 经 RenderScaling 换算）；呈现 = 文件列表
    + 行摘要（前 4 个非空字段，无动态列）；数据源复用 GameContentServer 同源的 gameRoot 磁盘读取。
    测试新增 DataBrowserServiceTests 5 项，40/40 全绿。
  - v2.13（2026-08-05）：**后台运行默认开启 + 数据浏览提为顶级菜单** —— 「后台运行」默认
    true（VM 初始值 `= true`，WebView 创建时同步页面 `window.__backgroundMode`，视图 Attach 时
    同步本地开关状态）；「数据浏览」从视图子菜单提为**顶级菜单项**（文件 / 视图 / 数据浏览）。
  - v2.14（2026-08-05）：**数据浏览改为独立弹窗** —— 由无边框右侧覆盖窗口改为普通带边框弹窗
    （宽 420 × 高 540、`CenterOwner`、可移动缩放、ShowInTaskbar=false 工具窗口），方便与播放器
    并排对比查看；保留菜单顶级入口与 Refresh 行为。
  - v2.15/2.16（2026-08-05）：**数据浏览复用编辑器管线（合并 25 类）** —— ①**XmlParser 下沉
    Infra**（`App/Helper/XmlParser.cs` → `Infra/Services/XmlParser.cs`，只依赖 Core 抽象 +
    ILogger；`ValueConverter` 同步下沉 `Infra/Helper`；App 仅改注册一行，其余全走 IXmlParser
    接口零改动）——Player 与编辑器共享同一解析/序列化实现，消除 Player 侧自写解析。②数据浏览
    从"按文件浏览"改为**按实体类浏览（25 类）**：`DataBrowserService.BuildCatalog()` 扫描
    base `data/*.xml` + 全部 `Mods/*/*/neogame.xml`（游戏加载顺序）→ 按 (表名, 主键 nID→id→
    首列) 行级合并（后加载覆盖）→ `GameDataCatalog`（已知实体表排序优先）；VM/UI 左侧=表名
    列表，右侧=合并行摘要；状态栏显示"25 类数据，共 N 行（已合并模组覆盖）"。测试重写 5 项
    （base+mod 覆盖/追加、空根、坏文件跳过、已知表排序、无 nID 回退），40/40 全绿。
  - v2.17（2026-08-05）：**游戏退出生命周期补漏** —— 退出回待加载态时：①音频残留根因：
    **仅从可视树移除 WebView 控件不会立即销毁 WebView2 浏览器进程**（音频继续）——先
    `destroy()` + 导航 about:blank 卸载页面（音频随页面卸载停止）→ 300ms → 再移除控件；
    ②游戏退出时一并关闭**数据浏览弹窗与日志覆盖层**（此前残留显示旧数据），placeholder
    显示前回到完全干净的待加载态。
  - v2.18（2026-08-05）：**数据浏览大面积 0 行修复（根因）** —— 游戏 XML 声明为
    `encoding='utf8'`（非标准拼写），`XDocument.Load` 抛 `System does not support 'utf8'
    encoding` → 整个文件数据全丢（ingredients.xml 与模组 neogame.xml 均失败，仅标准 utf-8
    的 gamevars 成功）。修复：`ParseFile` 改用 `File.ReadAllText` + `XDocument.Parse`
    （解析已解码字符串，忽略声明）。新增 utf8 声明回归测试（含中文值），41/41 全绿。
  - v2.19（2026-08-05）：**数据浏览器退出重置** —— 游戏退出时除隐藏窗口外，新增
    `DataBrowserViewModel.Reset()`（清空 catalog/表/行/状态，显示"游戏已退出，数据已重置"），
    旧游戏数据不再残留；换 SWF 后打开数据浏览由 `Refresh()` 按新 gameRoot 重建。
  - v2.20（2026-08-05）：**模组覆盖顺序对齐 getmods.php** —— 数据浏览的模组枚举顺序由目录
    字母序改为解析游戏根目录 `getmods.php` 的 `strModURL{i}` 序列（按 index 排序、URL 解码、
    去重，缺失时回退字母序），未列入 php 的模组目录（如编辑器新建）附后加载；模组间覆盖与
    游戏实际加载顺序完全一致（后加载胜出）。新增 2 项测试（php 顺序覆盖 / 未列目录附加），
    43/43 全绿。
  - v2.21（2026-08-05）：**文档订正 —— game.db 只读 = 数据浏览** —— 原 P6 待做项「game.db 只读
    查询（指定路径时 EF Core 只读）」与已落地的数据浏览实为同一事项：数据浏览即「只读查看游戏
    数据」的实现（数据源 = 游戏 XML，与编辑器 game.db 同源），故删除该待做项、O6 移除「EF Core
    只读」表述，§四 P6 标题同步标注（= game.db 只读）。纯文档修正，代码无改动。
  - v2.22（2026-08-05）：**wiki = 数据浏览器扩充 + 三栏 master-detail 定案 + 表数订正 24** ——
    ①「wiki」并入数据浏览器（同一功能）：升级为 **master-master-detail 三栏**（表列表 → 行
    listbox → wiki 式详情页）；详情页 = 行数据 → Markdown 模板 → **LiveMarkdown.Avalonia
    2.2.2** 渲染（第三方框架调研结论：编辑器已在用、Avalonia 12 + Markdig、零新增依赖；备选
    Markdown.Avalonia 11.0.3 未引入）；定制渲染模板以 recipes 配方卡 / treasuretable 掉落
    概率树为首发（复用 `[ReferenceField]`/FormatSegmentDisplay 语义与编辑器 TreasureTable
    递归展开深度 5 + 循环检测做法），通用表引用列转链接跳转目标表/行。②**表数量订正 24**：
    `GameTableMap.KnownTableNames` 实测 24 个 `[Table]` 实体，数据浏览相关代码注释 "25" →
    24（UI 状态栏本就动态计数不受影响）；本文 "25 类" 表述同步订正（v2.15/2.16 历史条目保留
    原样）。纯文档 + 注释修正，功能代码无改动。
  - v2.23（2026-08-05）：**wiki 式详情实施完成（三栏 master-detail）** —— ①`WikiDetailBuilder`
    （Player.Core/Data）：通用表字段表 + 引用列 db:// 链接（`[ReferenceField]` 反射元数据，
    pattern 解析支持可选尾段）、recipes 配方卡（材料 ×数量/产物 + 掉落预览/替代/隐藏配方）、
    treasuretable 掉落概率树（权重归一化概率、复合键 → itemtypes、嵌套 TT 递归 ≤5 + 循环
    检测）。②数据层配套：`GameDataRow.RowKey` 属性化、`GameDataCatalog.FindRow`（RowKey →
    字段值 → 点号复合键）、`GameTableMap.FindTableName`。③UI：`DataBrowserViewModel` 三栏
    （SelectedRow → `ObservableStringBuilder` 详情 + `LinkCommand` db:// 导航），
    `DataBrowserWindow` 三栏 Grid（150/230/*，1000×640，中栏改显 RowKey），LiveMarkdown.Avalonia
    2.2.2 + `Assets/MarkdownTheme.axaml` 集成。④测试新增 6 项（WikiDetailBuilderTests：
    通用表/引用列/配方/概率/嵌套循环/空表）；修复 pattern 可选尾段正则构建 bug 与
    InvariantCulture P1 百分比空格；Player.Core.Tests 50/50 全绿、全解决方案 0 错误。
  - v2.24（2026-08-05）：**引用分析 + 图片画廊** —— ①`ReferenceAnalyzer`（入站引用：
    懒构建索引全 catalog 扫描、双目标/复合键解析、同源去重、表名+RowKey 双校验防跨表
    误判、图片列排除），详情页新增「被引用」区块（按来源表分组 + 链接 + 来源列）。
    ②图片画廊：ImageAsset 引用列 → img/*.png 存在性检查 → markdown 3 列网格
    （ImageBasePath=gameRoot，EscapeDataString 处理中文/空格名，缺失回退文本，图片列
    从字段表排除；vSpriteList `{value}={id}`、ns: 前缀剥离）。③重构 `ReferenceMetadata`
    共用（RefColumn 加 IsImage）。④VM/XAML：ImageBasePath 绑定 + builder 注入 imageRoot。
    ⑤测试新增 12 项（ReferenceAnalyzerTests 6 + WikiDetailBuilderTests 画廊/被引用 6），
    Player.Core.Tests 62/62 全绿、全解决方案 0 错误。
  - v2.25（2026-08-05）：**本地文件日志（闪退不丢日志）** —— ①`RunLogStore` 新增强类型
    `LineAppended` 事件（含 runId + 行）。②`FileRunLogWriter`：每 run 一个
    `player-run-{时间戳}-{runId}.log`（runId 文件名清洗）、**逐行 Flush**（崩溃最多丢半行）、
    **只保留最新 2 个文件**（启动与轮换时按文件名时间序清理）、`WriteCrash` 追加 `[FATAL]`；
    目录 `{BaseDirectory}/logs`（不可写回退 LocalAppData）；`FileShare.ReadWrite`（日志查看
    器可并发读热文件）。③App 接线：`PlayerServices.FileLog` +
    `AppDomain.UnhandledException`/`TaskScheduler.UnobservedTaskException` → WriteCrash；
    日志覆盖层顶部显示日志文件路径。④测试新增 6 项（FileRunLogWriterTests），
    Player.Core.Tests 68/68 全绿、全解决方案 0 错误。
  - v2.26（2026-08-05）：**detail 两栏 + 图片画廊修复** —— ①detail 拆两栏：主体
    （字段/配方/掉落）左栏，图片画廊 + 被引用右栏侧边（280px）；`WikiDetailBuilder`
    拆 `BuildDetail`/`BuildReferences`/`GetImageItems`（`Build` 保留兼容），VM 增
    `SideMarkdown`/`Images`（`WikiImage`）/`HasImages`/`HasReferences`。②图片缺失根因：
    markdown 表格内图片 + `ImageBasePath` 相对解析不可靠（且 ImageBasePath 为 get-only
    无通知）→ 画廊改**原生 Image 控件**直接解码绝对路径（WrapPanel 72×72 缩略图 + 文件名，
    缺失显示 `文件名（缺失）`），移除 ImageBasePath 绑定；引用侧栏保留 MarkdownRenderer
    （db:// 链接跳转）。③测试新增 3 项，Player.Core.Tests 71/71 全绿、全解决方案
    0 错误。
  - v2.27（2026-08-05）：**win-only 打包裁剪（1G → 129MB）** —— ①Player.Core 去 Infra
    依赖（GameTableMap 自反射、IConfigService 移 Core/Abstractions、DI Abstractions 包），
    EF Core/SQLite 整体退出 Player 链路。②win-only：Avalonia.Win32 + UseWin32 +
    RuntimeIdentifier win-x64（其他平台新增编译目标，不混入）。③Release 无 PDB、
    WebView2 缓存外移 LocalAppData。④publish 255MB → 129MB；事故记录：EntityEditorDocument
    未提交修改被误还原，经反编译 DLL 逐方法恢复（R24 管道 + XML diff），全量测试全绿。
  - v2.28（2026-08-05）：**实机反馈修复 + 主题/本地化** —— ①发布物实机可运行（订正）。
    ②图片画廊：Image.Source 绑 string 不渲染 → VM 解码 `Bitmap`（ImageItem）。③detail
    两栏修正：280px 侧条 → detail 内部均分两栏。④主题跟系统：数据浏览/日志覆盖层移除
    强制 Dark；新增 视图→主题（跟随系统/亮/暗），AppConfig.Theme 持久化到
    %LocalAppData% settings.json（PlayerConfigService 首次持久化）。⑤本地化：resx
    zh/en + LocalizationManager（索引器绑定 + 全量刷新）+ 视图→语言（中文/English）；
    覆盖菜单/欢迎页/覆盖层/状态文本，wiki markdown 保持中文。⑥全解决方案 0 错误、
    13 测试项目全绿、publish 143MB 冒烟通过。
  - v2.29（2026-08-05）：**真实运行日志驱动修复** —— ①host.html flush 按 level 分组
    （原全部标 "console" → 分类/过滤失效根因）；%c 与样式参数清洗；Ruffle logLevel
    Debug→Warn（噪音削减）。②滚动跳动：VM 增量追加（LineAppended）+ 覆盖层行固定高度。
    ③启动 FATAL：页面加载前 InvokeScript 移除，改 NavigationCompleted 后同步
    backgroundMode（消除控件内部 UnobservedTaskException）。④澄清 neogame.xml 404/
    Error #2032 为设计行为（游戏自动回退 data/*.xml）。⑤全解决方案 0 错误、13 测试
    项目全绿。

**v2.28 已实施（实机反馈修复：图片画廊 / detail 两栏 / 主题与本地化）**：
- **订正**：v2.27 发布物经用户实机验证**可正常运行**（补 Avalonia.Skia/HarfBuzz 后）。
- **图片画廊修复**：Image.Source 绑定 string 路径在编译绑定下不渲染（只有文件名
  文本可见）→ 改为 **VM 在 UI 线程直接解码 `Bitmap`**（`ImageItem(Bitmap?, FileName)`，
  Image.Source 绑定 Bitmap 对象；解码失败回退文件名文本）。
- **detail 两栏修正**：右栏固定 280px 侧条（视觉上"放外边"）→ 改为 **detail 内部两栏
  均分**（主体 markdown | 图片+被引用），缩略图加大到 92px。
- **主题跟系统 + 切换**：数据浏览窗口与日志覆盖层**移除强制 Dark**（此前不跟系统）；
  新增菜单 **视图 → 主题 → 跟随系统 / 亮色 / 暗色**（RadioMenuItem）——
  `PlayerViewModel.Theme`（AppConfig.Theme）→ 应用级 `RequestedThemeVariant`；
  **持久化**：`PlayerConfigService` 读写
  `%LocalAppData%/NeoScavengerPlayer/settings.json`（Theme + Language，原无持久化）。
- **本地化切换**：新增 `Localization/Resources.resx`（zh 中性）+ `Resources.en.resx` +
  `LocalizationManager`（索引器绑定 `{Binding [key], Source={x:Static ...}}` +
  INotifyPropertyChanged 全量刷新）；菜单 **视图 → 语言 → 中文 / English**；
  覆盖范围：菜单栏/欢迎页/日志覆盖层/窗口标题/Player 状态文本/数据浏览状态文本；
  **wiki 详情 markdown 保持中文**（游戏数据语义，后续可选）。
- 验证：全解决方案 0 错误、13 测试项目全绿、publish 143MB 冒烟通过（干净启动：
  无 SWF 时 server/WebView/日志文件均不创建）。

**v2.29 已实施（真实运行日志驱动修复）**：
- **日志分类修复（根因）**：host.html 的 `flush()` 把**所有批次统一标记为
  `level: "console"`**（clipboard/debug 只以 `[clipboard]` 前缀内嵌在消息里）→ UI
  全部显示 console、过滤失效。修复：**按 level 分组发送**（console/clipboard/debug 各自
  独立 POST），`LevelFilter` 真正生效。
- **日志噪音削减**：①console 捕获**清洗 `%c` 样式标记与纯样式参数**
  （"color: lawngreen; ..." 不再拼进消息）；②Ruffle `logLevel` **"Debug" → "Warn"**
  （register_export / symphonia 音频 / Audio underrun 等 DEBUG 刷屏消失；stub 警告与
  游戏日志不受影响）。
- **滚动条跳动修复**：PlayerViewModel 由 `LogAdded` 每批**全量重建 VisibleLines**
  改为 `LineAppended` **增量追加**（集合只增长）；日志覆盖层行模板**固定行高**
  （NoWrap + CharacterEllipsis + ToolTip 全文，原 Wrap 使行高不定、虚拟化滚动跳动）。
- **启动 FATAL 修复**：`GetOrCreateWebView` 在**页面加载前**调用
  `InvokeScript(window.__backgroundMode)` —— 控件内部异步泄漏
  `UnobservedTaskException`（crash 日志出现
  "Unable to invoke script before any page was loaded"）。修复：移除页面加载前的
  InvokeScript，改在 **`NavigationCompleted` 后**同步 backgroundMode。
- **非问题澄清（用户实机日志）**：`neogame.xml` 404 + 游戏日志 `Error #2032:
  Stream Error / 找不到文件。` 是**设计行为**——根 neogame.xml 故意 404（§3.6），游戏
  自动回退加载 `data/*.xml`（同一份日志可见 404 后全部 24 个 data 表加载成功并完成
  "更新 template-based items" 进入游戏）。
- **v2.29 追修**：level 分组后 `console.log` 暴露为 "log"（下拉框无此项，无法过滤）——
  console.log/info/trace **归一为 "console"**（warn/error/debug/clipboard 保持独立）。
- 验证：全解决方案 0 错误、13 测试项目全绿。

**v2.30 已实施（数据浏览器主题化）**：v2.26 时代的硬编码浅色
（中栏行 RowKey #7FC8FF / Summary #EEEEEE、状态栏 #CCCCCC、侧栏背景 #22101010 等）
在 v2.28 移除强制 Dark 后于 Light 主题下不可读（白底浅字）。全部改为主题资源
（`SystemControlForegroundBaseHigh/Medium`、`SystemControlHighlightAccentBrush`（主键
强调）、`SystemControlBackgroundChromeMediumLow/BaseLow`）；图片缩略图底衬换
BaseLow 与侧栏区分；Markdown 区域走 MarkdownTheme.axaml 双字典（v2.28）。日志覆盖层
保持固定深色半透明浮层设计（深底浅字不依赖主题，两种主题下均可读）。
  - v2.30（2026-08-05）：**数据浏览器主题化** —— v2.26 硬编码浅色（中栏行 #7FC8FF/
    #EEEEEE、状态栏、侧栏背景等）在移除强制 Dark 后于 Light 主题不可读；全部改主题
    资源（ForegroundBaseHigh/Medium、HighlightAccentBrush 主键强调、
    ChromeMediumLow/BaseLow 背景），图片缩略图底衬换 BaseLow 区分；日志覆盖层保持
    固定深色浮层设计（两种主题均可读）。
  - v2.31（2026-08-05）：**发布流程文档化** —— 新增 §八「Player 发布流程（win-only）」：
    前置条件、发布命令（单文件/多文件）、csproj 内置裁剪配置、产物结构（143MB）、
    运行时落盘路径（settings/logs/WebView2）、发布前校验清单、版本号管理、常见问题；
    原「八、文档登记」顺延为九。纯文档变更。
  - v2.32（2026-08-05）：**图片走马灯 + 引用 Tab 分类** —— ①图片画廊改**走马灯**：
    大图预览（180px 高、Stretch=Uniform）+ ◀▶ 循环切换 + 文件名/计数（i/n）+ 缩略图条
    （40px，点击跳转，code-behind）；VM 增 CurrentImage/CurrentImageIndex/ImageCounter +
    PrevImage/NextImage 命令。②被引用改 **TabControl 按来源表分类**：每表一个 tab
    （表名 + 该表 markdown 行列表，保留 db:// 链接跳转）；WikiDetailBuilder 增
    `BuildReferenceGroups`（ReferenceGroup(TableName, Markdown, Count)），
    `BuildReferences` 重构复用（输出不变，兼容测试）；VM 用 ReferenceTab
    （TableName + ObservableStringBuilder + LinkCommand）替换 SideMarkdown。
    ③测试新增 BuildReferenceGroups 分组断言，Player.Core.Tests 72/72 全绿、
    全解决方案 0 错误、13 测试项目全绿。
  - v2.33（2026-08-05）：**自动 release + 本地发包脚本** —— ①`.github/workflows/release.yml`
    扩展：`v*` tag 触发（windows-latest），同一 job 串行发布编辑器 + 播放器两个 zip
    （NeoEditor-{tag} / NeoScavengerPlayer-{tag}），一次 Release 双产物上传，body 说明
    两产物与运行前提。②仓库根新增 `publish.ps1`（交互式 CLI，UTF-8 BOM 兼容
    PowerShell 5.1）：菜单 = 发布并打包（单文件推荐/多文件）、仅发布、运行测试、打开
    dist、退出；取消 = 0/q 或随时 Ctrl+C；支持 `-Single/-Multi/-SkipTests` 参数模式；
    zip 命名带版本（默认当天日期）。③§八 发布流程补充两种方式（Actions/脚本）。
  - v2.34（2026-08-05）：**字段表 UI 化（多行值修复）** —— 字段值含换行会撑破 markdown
    表格 → 字段表改 **UI 网格**（原生 ItemsControl 两列：列名 | 值，值 TextWrapping 多行
    完整显示）；`WikiDetailBuilder.GetFields`（FieldItem/FieldLink：原始多行值 +
    引用列解析为可点击链接，未解析 id 保持纯文本）+ `AppendFieldTable`（Build 完整输出
    兼容：值清洗换行、链接渲染）；BuildGeneric/BuildRecipe 不再输出 markdown 字段表；
    VM Fields/HasFields；左栏 = 主体 markdown + 字段网格；resx Field.Title。
  - v2.35（2026-08-05）：**内置 Carousel 替换手写走马灯** —— 图片预览改用 Avalonia
    内置 `Carousel`（零依赖）+ `PageSlide` 切换动画，SelectedIndex 双向绑定
    CurrentImageIndex（◀▶/缩略图直接驱动）；无需引入 AtomUI（第三方 Ant Design 风格
    库，引入过重）。测试 77/77 全绿、全解决方案 0 错误、13 测试项目全绿。
  - v2.36（2026-08-05）：**存档持久化（根因修复）+ 存档管理** —— ①根因：GameContentServer
    每次随机回环端口 → 页面 origin 每次变化 → WebView2 按 origin 隔离 localStorage →
    Ruffle SharedObject 存档（key 带 swf 路径前缀）在重开 Player 后"消失"。修复：
    `AppConfig.ServerPort` **持久化端口**（首次启动随机生成写入 settings.json，之后固定；
    占用时 +1 重试最多 20 次并把胜者回写持久化）→ origin 稳定 → 存档跨启动保留。
    ②存档管理：菜单「存档管理」（顶级）→ `StorageManagerWindow` 列出 localStorage 存档
    （key + 大小），支持刷新 / 单个删除 / 清空全部；数据经 WebView `InvokeScript` 读取
    （JS 桥），`StorageManagerViewModel`（Player.Core，注入 executeJs/localize 委托，
    可单测）。③InvokeScript 为异步 API（`Task<string?>`）——executeJs 委托异步化，
    顺带消除启动期"未观察任务异常"的另一潜在来源。④测试：StorageManagerViewModelTests
    5 项（解析/空/读取失败/删除/清空，fake JS）+ PortPersistenceTests 3 项（指定端口/
    占用 bump 回写/随机持久化），Player.Core.Tests 85/85 全绿、全解决方案 0 错误。
  - v2.37（2026-08-05）：**存档备份（写入前备份 + 恢复）** —— 游戏死亡会删除存档 →
    host.html 包装 `localStorage.setItem`/`removeItem`：**覆盖/删除前把旧值 POST
    `/__backup`**；宿主 `SaveBackupService` 落盘到 **`{gameRoot}/save_backup`**（用户
    指定目录；动态跟随 GameRootDir，LocalAppData 兜底）——备份不进 localStorage（与
    存档共享 ~5MB 配额会爆）。文件名 `backup-{时间戳}-{safeKey}.json`（含 key/时间/
    旧值），**保留最近 5 份**（按时间清理）。存档管理窗口改**双 Tab**：存档（原）
    + **备份**（时间/key/大小 + **恢复**（写回 localStorage）/删除）；VM 增
    RefreshBackups/Restore/DeleteBackup（注入 SaveBackupService）。测试：新增
    SaveBackupServiceTests 5 项（目录/5 份保留/读值/删除/页面载荷解析），
    Player.Core.Tests 90/90 全绿、全解决方案 0 错误。
  - v2.38（2026-08-05）：**存档管理"解析失败"追修** —— 根因：WebView2
    `ExecuteScriptAsync` 的返回值本身是**表达式结果的 JSON 编码**（数组/对象直接序列化，
    字符串则带引号转义）；页面里 `JSON.stringify(...)` 再包一层 → 宿主拿到
    `"\"[{...}]\""` 双重转义字符串 → `Deserialize<SaveItem[]>` 抛 JsonException →
    "显示存档数据解析失败"。修复：①列表脚本去掉 `JSON.stringify`，直接返回数组
    表达式（WebView2 自动序列化）；②宿主 `DeserializeSaveItems` 防御性解包——先按数组
    解析，失败则按字符串解一层再解析（兼容两种形态）。新增
    `RefreshToleratesDoubleEncodedJsonString` 测试锁定，StorageManagerViewModelTests
    6/6 全绿。
  - v2.39（2026-08-05）：**假存档追修 + 走马灯去手搓** —— ①存档管理出现两个 0.0 KB
    假存档（key 为 `setItem`/`removeItem`）：根因是 v2.37 备份包装直接
    `localStorage.setItem = fn` 赋值 → 在 Storage 实例上创建 **own enumerable 属性**
    → `Object.keys(localStorage)` 把方法名列出来，`(localStorage[k]||'').length` 取到
    函数的 `.length`=0 → 显示为 0 字节"存档"。修复：包装改用
    `Object.defineProperty(..., enumerable:false)`（与原型方法语义一致，不再被枚举）；
    列表脚本加 `typeof localStorage[k] === 'string'` 过滤兜底。②数据浏览器图片画廊
    "依旧是手搓组件"：大图轮播确为内置 Carousel（v2.35），但 ◀▶ 按钮和缩略图条
    （ItemsControl+Border+PointerPressed code-behind）是手搓的。修复：缩略图条换内置
    **ListBox**（SelectedIndex 双向绑定 CurrentImageIndex，自带选中高亮 + 方向键导航），
    删除 ◀▶ 按钮与 VM 的 PrevImage/NextImage/StepImage——画廊 = Carousel + ListBox，
    零手写轮播逻辑。Player.Core.Tests 91/91 全绿、构建 0 错误。
  - v2.40（2026-08-05）：**存档跨启动丢失根因修复 + 单文件产物** —— ①存档丢失根因：
    v2.36 的"端口持久化"**从未生效**——`PlayerConfigService.PlayerSettings` 只有
    Theme/Language，`LoadAsync`/`SaveAsync` 都不碰 ServerPort → 每次启动 ServerPort=0
    → 重新随机端口 → origin 每次不同 → WebView2 按 origin 隔离 localStorage → 存档
    "消失"（GameContentServer 的 bump 回写也只是改内存）。修复：PlayerSettings 增
    ServerPort 字段，Load 读（>0 且 <65536 才应用）、Save 写 → 首次随机端口落盘后
    固定，origin 稳定，存档跨启动保留。②发布产物 dll 太多：单文件发布已捆绑 managed
    dll，但 native dll（libSkiaSharp/av_libglesv2/libHarfBuzzSharp/libonigwrap ~19MB）
    外置（release.yml 的 player 步骤和 publish.ps1 都没传
    IncludeNativeLibrariesForSelfExtract）。修复：两处发布命令补
    `-p:IncludeNativeLibrariesForSelfExtract=true` → 产物 = **单个
    NeoScavengerPlayer.exe（113MB）+ Web/**，零 dll；已本地发布验证 + 冒烟启动
    （native 自解压正常，进程存活）。Player.Core.Tests 91/91 全绿。
  - v2.41（2026-08-05）：**存档管理增强（手动备份 + 过滤 + 二次确认）** —— ①备份为空的
    根因说明：写入前备份只备份**旧值**（`old !== null` 才 POST /__backup），而 v2.40
    之前每次启动 origin 都变 → localStorage 恒空 → 旧值恒 null → 从未产生备份；v2.40
    修复后第二次保存起自动备份生效。②存档列表过滤：JS 只显示**当前 swf 路径前缀 +
    值非空 + 名称不含 test** 的条目（nsTest 等开发噪音隐藏，setItem/removeItem 属性
    已由 typeof 过滤）→ 列表只剩正式存档（nsSGv1）。③**手动备份**：存档行新增「备份」
    按钮 → 命名弹窗（PromptDialogWindow，输入模式，默认 SharedObject 名）→
    `SaveBackupService.SaveManual` 写 `manual-{时间戳}-{名称}.json`（payload 带
    Manual/Name 标记）；**手动备份不参与自动 5 份清理**（Trim 只扫 backup-*），永不被
    自动备份覆盖。④备份行新增「改名」：`Rename` 更新 payload.Name 并重命名文件
    （时间戳保留）；自动备份拒绝改名。备份行显示 DisplayName（手动=名称，自动=key）。
    ⑤删除/删备份/清空全部均弹**模态确认框**（PromptDialog 消息模式，OK/Cancel）。
    测试：SaveBackupServiceTests +5（手动备份/trim 保留 manual/改名/自动备份拒改）、
    StorageManagerViewModelTests +2（手动备份落盘/改名），Player.Core.Tests 97/97
    全绿、构建 0 错误。
  - v2.42（2026-08-05）：**菜单门控 + 备份"消失"追修** —— ①未运行 SWF 时「数据浏览」
    和「存档管理」菜单项禁用：`PlayerViewModel` 新增 `IsSwfLoaded`（`StartAsync` 成功后
    置 true，游戏退出 `OnGameReset` 复位 false），两个菜单项 `IsEnabled` 绑定。
    ②备份"被删"实为**未加载**：存档管理窗口每次打开都是新建 VM（`Opened` 只执行
    `RefreshCommand` 刷存档列表）→ 备份 tab 的 `Backups` 集合为空 → 重开窗口后备份
    列表空白，看起来像"备份被删了"（磁盘文件其实完好）。修复：`Opened` 时同时执行
    `RefreshBackupsCommand`，两个 tab 每次打开都加载。Player.Core.Tests 97/97 全绿、
    构建 0 错误。
  - v2.43（2026-08-05）：**备份"不持续"追修（策略修正 + 实时刷新）** —— 用户实机
    验证：备份目录只有 1 个自动备份 + 1 个手动备份（真实文件排查，非重名——文件名
    带毫秒时间戳唯一）。两个根因：①**策略漏洞**：v2.37 的 setItem 包装备份的是
    **覆盖前的旧值**——首次写入或先删后写（死亡删档后新开档）时 `getItem` 为 null →
    不备份 → 之后每次保存都无备份。修复：**setItem 改为备份写入的新值**（每次保存都
    落盘），removeItem 保持备份旧值（死亡删档保护），JS 统一为 `backupValue(key,
    value)`。②**UI 不实时**：窗口开着时磁盘新增备份，`Backups` 集合不刷新。修复：
    窗口 `Activated`（从游戏切回）与备份 Tab 切换（`SelectionChanged`）时执行
    `RefreshBackupsCommand`。Player.Core.Tests 97/97 全绿、构建 0 错误。
    **追修（同日）**：`OnTabChanged` 在 XAML 初始化期间即被触发——TabControl 创建选择
    模型时 raise SelectionChanged，此时 `BackupTab` 字段尚未赋值（null）→ 打开存档
    管理窗口 NRE 崩溃（用户日志 `StorageManagerWindow.OnTabChanged`
    NullReferenceException，退出码 -532462766）。修复：判空守卫
    `BackupTab is { IsSelected: true }`。
  - v2.44（2026-08-05）：**受伤存档重启必崩修复（接管序列化：LSO 引用全展开）** ——
    用户实机：角色受伤后存档，重启必崩（`m_fDate not found on Number`，Ruffle
    issue #1069）。**根因链**：Ruffle（nightly 0.6.0-nightly.2026.8.4）反序列化 AMF3
    引用有 bug——`core/src/avm1/amf.rs` 的 `deserialize_value` 对
    `Amf3ObjectReference`（VectorObject/Dictionary/ECMAArray/Object 的引用）落入
    `_ => Value::Undefined`（第 341 行）；`AmfValue::Reference` 分支也只查缓存、
    引用目标再次解析失败；ECMAArray 分支（239-270 行）只遍历 associative、dense
    元素被丢弃。存档内对象引用（游戏反复序列化同一批组件对象产生大量引用）被
    解析成 undefined/Number → 游戏访问 `.m_fDate` → 崩溃。**方案（"既然 ruffle 有
    序列化问题，为什么我们不接管呢"）**：SWF 加载前接管序列化——`getItem` 拦截
    localStorage，把存档 LSO 解析 → **引用全部展开为内联** → 重新编码，Ruffle 读取
    时无引用可崩。实现：
    ① `Web/lso-expand-web.js`（新增）：无依赖浏览器版 AMF3/LSO 解析+重编码器，
      与 `player-tools/lso-expand.js`（node 原型）逻辑一一对应、输出逐字节一致；
      u29/traits 编码、ECMAArray/StrictArray、Vector*/Dictionary、字符串/对象/
      traits 引用、环检测（环 → undefined 保守处理）全部对齐 flash-lso 源码
      （write.rs/read.rs）。
    ② `host.html`：`localStorage.getItem` 包装——key 含 `nsSGv1` 时返回展开版本
      （引用全内联），解析失败回退原始值绝不阻塞游戏，成功/失败均有 /__log 诊断。
    ③ 修复过程中挖出的两个解析器自身 bug（对照 flash-lso 源码）：
      Integer 解码误用字符串奇偶规则（-1 → 268435455）；Dictionary 编码把 weak
      标志塞进 u29 bit0 且漏写独立 weak 字节（len>0 的 dict 被解析成引用、跳过
      pairs 导致 42KB 累计错位）。**验证**：崩溃档 + 3 个备份档全部往返成功
      （23732/28792/42668 字节 → 展开后 79600/97356/114560），值树逐叶子对比
      （4312 叶子值）**零不一致**；host.html 拦截脚本沙箱端到端测试 6/6 通过；
      `Player.Core` 构建 0 错误。**待用户实机验证**：受伤存档重启不再崩。
  - v2.45（2026-08-06）：**读写端引用表错位根因确认 + 两遍法重写** —— 实机验证暴露
    新崩溃（`fDurability not found on Number`、`cannot convert 0 to Vector.<int>`）。
    对照 flash-lso write.rs/read.rs 源码确认**根因链**：写端（Ruffle
    `get_or_create_value`，对象身份去重）写出的引用号按**写端表**（值相等去重、
    VectorInt 等 to_length 类型不入表）编号；读端表（所有复杂类型占位式入表）与
    写端表错位 → 同一引用号解析到不同对象（如伤口 `vCurrentStates` 引用指向物品
    对象 → 游戏 for-each 读 `Number.m_fDate` 崩）。重写为**两遍法**（P1 解析建
    占位树 + 写端表语义重建 → 引用全展开内联）：表按写端语义（仅
    Object/VectorObject/Dictionary/ECMAArray 入表、值相等去重）重建 + 类型不匹配
    回退（0x10/0x11/0x0a 引用标记 vs 表项类型不符 → 回退到包含该表项的容器）；
    `VECINT_PROPS` 字段知识修正（`m_vWaypoints` 等 Vector.<int> 属性被去重成空
    vecobject → 还原空 vecint，修复保存时 `cannot convert 0 to Vector.<int>`）。
    node 版验证 17/17 伤口正确；浏览器版同步 + 输出逐字节一致 + 幂等。
  - v2.46（2026-08-06）：**启动即修复（自动修复循环）+ 缓存追修** —— 展开从
    getItem 读取时扩展到**启动时**：页面加载即扫描 localStorage 展开并 setItem
    落盘（游戏保存写回原始 LSO → 下次启动自动再展开 = 用户要的自动修复循环）；
    GameContentServer 对 .html/.js/.wasm 改 `no-store`（WebView2 曾缓存旧版
    host.html/展开器导致展开脚本缺失）；版本标记入日志（`NE v2.46 展开器就绪`）
    便于实机确认加载的是新 host.html。**待实机验证**。
  - v2.47（2026-08-07）：**两层真根因修复 —— 运行时展开从未执行** —— 实机日志
    显示启动展开块报 `LsoExpand 不可用`。排查发现两层根因：① `lso-expand-web.js`
    导出行 `global.LsoExpand = ...` —— 浏览器无 `global`（仅 window/globalThis），
    脚本抛 ReferenceError，`window.LsoExpand` 从未定义 → 启动块与 getItem 包装
    全部静默跳过（node 测试能过是因为 node VM 有 `global`，验证方法缺陷掩盖了
    问题）；修复为 `globalThis.LsoExpand`。② 更早被忽略的真凶：**GameContentServer
    路由**把 `/lso-expand-web.js` 落入游戏根目录回退 → **404**（特殊路由只有
    `/`、`/ruffle/*`、代理路径）→ 脚本从未加载；修复为 **Web 目录优先**（Web 根
    存在文件先于游戏根，放代理路由前不冲突）+ 2 个回归测试
    （ServesWebRootScriptsLikeLsoExpander / WebRootDoesNotShadowGameRootFiles，
    13/13 绿）。启动展开块直接 POST /__log（早于 console 拦截，原 console.log
    不可见）可验证化。**实机：读档成功**（`v2.47 启动展开: nsSGv1 29676->82844`）。
  - v2.48（2026-08-07）：**保存崩溃修复（cannot convert false to Vector.<int>）** ——
    读档成功后可玩但保存必崩（`Creature/get SaveData → AICreature/get SaveData →
    DataHandler/SaveGame`）。反编译 SWF（FFDec + 便携 JRE，源码存
    player-tools/swf-src）定位：`Creature.get SaveData` 把 `m_dictFactions` 每个值
    push 进 `Vector.<int> m_vFactions`，活体字典混入 `false` 即崩。排查结论：
    存档数据**100% 干净**（flash-lso 0 引用 0 错误、全部值数字）；WAL 挖到崩溃前
    5 秒（00:41:08）游戏**成功保存**的存档为证——`false` 是游戏运行期 faction
    逻辑（`m_dictFactions[0] += delta` 在空字典条目上运算）在加载后数秒内产生的
    运行态产物（游戏自身缺陷，无法改 SWF）。修复：**形态归一化**——玩家存档
    `m_vFactions` 为空是异常形态（生物均为 14×-100 默认声望），展开时补全为
    14×-100，使运行期 faction 运算全部落在已存在的数字条目上；浏览器版同步
    （双版本逐字节一致 + 幂等验证）。**实机闭环验证通过**：读档 → 保存 → 关闭
    重开 → 读档 → 保存全部成功。旧备份（save_backup 原始字节快照）需经同样
    展开处理方可加载，游戏 SharedObject 单槽、新档覆盖槽位。
  - v2.49（2026-08-07）：**存档管理"删除/覆盖无效"根因修复（Ruffle SharedObject
    内存缓存）** —— 用户实机：同一会话内「每次加载同一个存档 / 覆盖后仍同一个 /
    删除后仍可加载」。源码取证（Ruffle avm2-shared-object.rs）：`get_local` 按
    full_name 缓存实例（`avm2_shared_objects`），**缓存命中即返回，运行中游戏
    从不重读 localStorage**；`clear()` 只删 storage 不清缓存与 data。因此存档
    管理的删除/清空/恢复只改 localStorage（备份证明 setItem/removeItem 真实
    执行），对运行中的游戏无效；唯一生效路径 = 重载页面（缓存随文档销毁）。
    修复：StorageManagerViewModel 删除/清空/恢复后提示「需重启游戏生效」
    （Storage.NeedRestart），存档管理窗口新增「重启游戏」按钮
    （RestartGameCommand → PlayerViewModel.RestartGame → 页面 Refresh）；
    新增 resx 键 ×2（zh/en）。**调试教训**：用户从 Rider F5（Debug 构建）启动，
    存档在 `bin/Debug/.../NeoScavengerPlayer.exe.WebView2` 数据目录，与 Release
    目录独立——排查存档落盘先确认启动的构建（leveldb LOG 的 Recovering 记录
    是判定数据目录活跃度的直接证据）。Player.Core.Tests 99/99 绿。
  - v2.50（2026-08-07）：**删除/恢复后旧档"复活"追修（自动保存写回）** ——
    用户实机：删除存档 → 手动重启游戏 → 读到**备份前的旧档**。反编译取证：
    游戏**每回合结束自动保存**（PlayState.update → EndDMTurn → SaveGame，
    `GUIEscMenu.bAutosave`）——删除只清 localStorage，运行中游戏的 Ruffle
    缓存实例仍持有旧 data，玩家任意行动推进回合即触发自动保存 → flush 把
    缓存旧数据**写回 localStorage**，重启后自然读到旧档。修复：删除/清空/
    恢复成功后**自动重启游戏**（`RestartGameNow`：操作完成后 ~300ms 重载页面，
    在游戏有机会自动保存前销毁页面），提示文案同步更新。手动「重启游戏」
    按钮保留（不触发变更时也可用）。
  - v2.51（2026-08-07）：**游戏内删档重启复活根因（Ruffle 实例 Drop 全量 flush）** ——
    用户实机：游戏内删档（`SharedObject.clear`）→ 重启 → 存档复活仍可继续。
    源码取证（web-lib.rs）：`RuffleInstance::drop` 调用 `flush_shared_objects()`
    把**内存缓存的所有 SharedObject** 写回 localStorage——`clear()` 只删存储层
    （`storage.remove_key`），缓存实例与 data 原封不动；页面卸载/实例销毁时
    全量回写把删除"撤销"。真实 Flash 只有显式 `flush()` 才写盘（游戏删档流程
    天然安全）；Ruffle 的 Drop 自动 flush 是可靠性设计，在删档场景成为复活源。
    修复：host.html 的 removeItem 包装器标记 `window.__savesCleared`；
    beforeunload/pagehide 监听（先于 Ruffle 注册）在"删过档"时设
    `window.__blockSaves` 拦截卸载 flush（setItem 包装器拒绝写入）。
  - v2.52（2026-08-07）：**destroy 路径补拦截 + 拦截条件收窄 + 存档管理崩溃修复** ——
    ① 用户实机仍复活：`TryDestroyPlayer`（关窗/导航/停止时先 destroy 再卸载）的
    Drop flush 早于 pagehide，拦截落空——改为 destroy() 前在**同一脚本**里先按
    `__savesCleared` 置 `__blockSaves`。② 用户质疑"删档后正常关闭是否也没法保存"
    ——拦截条件收窄为「本会话删过档 **且** localStorage 尚无存档」（= 删除后没
    保存过新档）才拦截；已有新档（删除后玩新游戏已自动保存）则放行 flush，
    新档最后一段进度正常落盘；存档管理 VM 显式设置的 `__blockSaves` 不被覆盖
    （恢复场景安全）。③ 崩溃修复：v2.50 自动重启与旧的手动 `AskRestartAsync`
    弹框冲突（删除后窗口已关闭，再以它为 owner 弹框 →
    `Cannot show a window with a closed owner`）——移除三处 AskRestartAsync
    调用。Player.Core.Tests 99/99 绿，Debug+Release 构建 0 错误。
  - v2.53（2026-08-07）：**调试工具四件套（R42）——剪贴板完全接管 · F12 DevTools ·
    日志目录/导出 · 报错捕捉** ——
    ① **剪贴板零弹窗**：旧 writeText 包装器用隐藏 textarea + `execCommand('copy')`
    兜底，仍写真实剪贴板（游戏内部日志刷屏用户剪贴板）且每次启动触发 WebView2
    「允许写入剪贴板」弹窗；readText 800ms 轮询还会弹读取权限框。v2.53 起
    `writeText` 只 `captureClipboard()` 进日志并返回成功、`readText` 返回空串、
    删除轮询——**真实剪贴板不再被写、启动零弹窗、隐私不再偷读**（Ruffle 只调
    writeText，已核实 ruffle.js/core 无 ClipboardItem）。内容完整保留在
    /__log（level=clipboard）与日志文件。
    ② **F12 开发者工具**：Chromium DevTools（Network / Application-localStorage /
    Console）。Avalonia.Controls.WebView 12.0.1 公共面只暴露原始 COM 指针
    （`IWindowsWebView2PlatformHandle.CoreWebView2` 是 IntPtr，托管 ICoreWebView2
    包装内部且无工厂）——用最小 `[ComImport]` 子集接口（GUID
    76eceacb-0462-4d94-ac83-423a6793775e，vtable slot 48 = OpenDevToolsWindow，
    0..47 声明但不调用）桥接调用；包从未禁用 DevTools（默认开启），游戏聚焦时
    F12/Ctrl+Shift+I 原生可用，本桥覆盖菜单项/窗口聚焦场景。失败降级状态栏提示。
    ③ **日志目录/导出**：LogOverlayWindow 顶栏 + 调试菜单 →「打开日志目录」
    （explorer /select 最新 player-run-*.log）、「导出日志」（头部信息 +
    localStorage 快照 + 全部 run 日志行 → `player-log-export-*.txt`，导出后
    explorer 定位；localStorage 快照走 host.html 新增 `window.__dumpLocalStorage()`，
    key/长度/前 200 字符预览，VM 注入 ExecuteJs 桥）。
    ④ **报错捕捉**：本版 Ruffle 不派发 error 事件（仅 loadedmetadata/loadeddata），
    运行时 AVM 错误走 console.error 通道。补全 unhandledrejection 监听 +
    `load().catch` 写 /__log + `window.__log` 全局暴露；SwfLogBridge 新增
    GameError 事件（致命签名：window.onerror / unhandledrejection / cannot
    convert / TypeError·ReferenceError·RangeError·SyntaxError / stack overflow /
    Maximum call stack / SWF 加载失败）→ 播放器弹窗「检测到游戏错误」+
    状态栏警示（30s 去抖）；异常退出（run 内有 error 行）状态栏「游戏异常退出」。
    测试：Player.Core.Tests 109/109（+10：GameError 模式 5 + 良性行 3 +
    RunLogReport 2）；全量 821/821 绿。
  - v2.54（2026-08-07）：**剪贴板真根因修复（execCommand）+ 版本号/About + 存档日志 zip 导出（R43）** ——
    ① **剪贴板仍被写**（用户实机：v2.53 后"剪贴板里依旧全都是日志"）。源码取证
    （ruffle-rs master web/src/ui.rs `WebUiBackend::set_clipboard_content`）：
    Ruffle 的剪贴板写入**不走 navigator.clipboard.writeText**（注释：该 API 仅 HTTPS
    安全上下文可用，本页 http://127.0.0.1 不可用）——而是隐藏 textarea + select() +
    `document.execCommand("copy")`（已核实本版 wasm glue 无 `__wbg_writeText`，
    仅 `__wbg_execCommand` + `__wbg_clipboard_ed`[navigator.clipboard getter]）。
    因此 v2.44-v2.53 的 writeText 包装从未被 Ruffle 调用，v2.53 前日志里的截获行
    全靠 readText 800ms 轮询读回（当初加轮询的原因），v2.53 删轮询后 execCommand
    仍写真实剪贴板。修复：**拦截 document.execCommand**（copy/cut → 捕获选区文本
    进日志 + 返回 true，不写真实剪贴板）；writeText/readText 包装保留（防未来版本/
    HTTPS 部署 + 读权限弹窗）。Ruffle 自身 buffer（clipboard_content）不受影响，
    游戏内 System.getClipboard 仍可读到它写的内容。
    ② **版本号**：csproj `<Version>0.9.0</Version>`（试用版）——窗口标题、启动日志
    首行、About 同源；publish.ps1 的 zip 命名默认取 csproj Version（替代当天日期）。
    ③ **About**：调试菜单 → 版本 / Ruffle nightly-2026-08-04 / 平台 / 日志目录 /
    游戏根目录 / WebView2 数据目录。
    ④ **导出存档+日志 zip**：调试菜单 → `NeoScavengerPlayer-export-{版本}-{时间戳}.zip`
    = info.txt + saves/localstorage.json（新增 `window.__exportSaves()` 全量存档）+
    logs/*.log + save_backup/*.json（PlayerBundleExporter，System.IO.Compression 内置），
    完成后 explorer 定位——试用反馈/存档迁移一键包。
    实机结果（用户）：删档复活修复 ✓ 确认；F12 ✓；导出日志 ✓；报错弹窗**未确认**
    （触发方式：DevTools Console 执行 `window.__log("error","TypeError: test")`）。
    测试：Player.Core.Tests 111/111（+2 PlayerBundleExporter）；全量 823/823 绿。
  - v2.55（2026-08-07）：**存档修改工具（R45，调试用）** —— 调试菜单「存档修改工具」：
    加载指定存档（localStorage 列表）→ LSO 反序列化为 JSON 树（`LsoExpand.toTree`，
    __amf 类型标记保留：object/array/vecint/vecobject/dict/date/xml/bytes/int/double）
    → 文本编辑 → **保存**（写回原 key）/ **另存为**（新 key）/ **保存并加载**（写回 +
    重载页面清 Ruffle SharedObject 内存缓存后生效）。保存走 `LsoExpand.fromTree`：
    JSON → 全内联重编码 → 立即回验 parseLso，改坏在写入前报错；NaN/±Infinity
    （存档里 755 处未初始化 double）经 sanitizeTree 转字符串标记保语义，round-trip
    与原始树完全一致（_current-wal-save / _precrash-save 两样本 node 验证）。
    结构查看 = 摘要行（LSO 名/格式版本/根条目数 + 根条目类型与 className）+
    JSON 树本身。注意：工具不执行 v2.48 的 m_vFactions 归一化——保存后重启时
    启动展开器会补全（与游戏读取路径一致）。
    测试：SaveEditorViewModelTests +8（加载/错误/保存/另存/保存并加载/去抖）；
    Player.Core.Tests 119/119；全量 831/831 绿。
  - v2.56（2026-08-07）：**About 消息框化 + 存档工具并入存档管理 + 剪贴板截获内容修复（R46）** ——
    ① **About**：用户反馈提示弹窗不该带两个确认按钮——改用编辑器同款
    `MessageBox.Avalonia`（MsBox，包 12.0.0 加入 Player.csproj），纯消息 +
    右上角关闭；PromptDialogWindow 保持原样（不再加"无按钮模式"）。
    ② **存档修改工具入口迁移**：用户反馈工具应集成进存档管理——移除调试菜单
    入口，存档管理窗口每行加「修改」按钮（EditSaveRequested 事件 → 宿主打开
    编辑器并预载该存档 LoadEntryAsync）；「保存并加载」后存档管理列表自动刷新。
    ③ **剪贴板截获内容修复**：用户实机 v2.54 后真实剪贴板干净了，但**日志里
    完全没有截获内容**——Ruffle 流程是 textarea.value → focus → select() →
    execCommand("copy")，WebView2 里 focus/selection 时序不稳，getSelection()
    为空导致提取失败。v2.56 提取链：selection → activeElement(textarea) →
    兜底 querySelector("textarea")（execCommand 时 Ruffle 的临时 textarea 尚未
    移除）——日志恢复显示「游戏剪贴板日志(截获)」。待用户复测。
    测试：全量 831/831 绿（本轮无新增测试）。
  - v2.57（2026-08-07）：**剪贴板源头截获 + 菜单重组 + 存档修改器改节点编辑器（R47）** ——
    ① **剪贴板再修复**：用户实机 v2.56 日志里「游戏剪贴板日志(截获): 」仍为空——
    WebView2 selection/focus 时序不稳，execCommand 同步执行先于 MutationObserver
    微任务还会污染 lastClipboard 去重。v2.57：execCommand 拦截只负责**阻断真实
    写入**（return true），内容提取改由 **MutationObserver 监听 textarea append**
    （Ruffle set_value 先于 append，回调时直接读 value；节点被 remove 不影响已捕获
    引用）——从源头截获，不依赖 selection/focus。待用户复测。
    ② **菜单重组**：调试菜单撤销——「打开日志目录/导出日志/导出存档+日志」移入
    **文件**菜单；「开发者工具 (F12)」移入**视图**（与 F11 同类）；「关于」移入
    文件底部。
    ③ **存档修改器 → 节点编辑器**（用户需求"不要文本编辑器"）：去掉顶部存档
    下拉/刷新/加载（入口已在存档管理「修改」按钮，窗口标题显示当前 key）；
    新增 SaveNode 树模型（SaveObjectNode/SaveListNode/SavePairNode/SaveScalarNode）
    + SaveTree 双向转换（Build/SerializeValue）：**容器只读结构**（保持 object
    names[]/values[] traits 对应，增删字段会把存档改崩——不提供），**标量内联
    编辑**（string/int/double TextBox、bool CheckBox；null/undefined/date/xml/
    bytes 只读显示 + RawJson 原样回写无损）。数值标记还原：vec* 的 values 是
    裸数字（非 {"__n"}）、NaN/±Infinity 保持字符串标记、double 用 "R" 保留精度。
    保存仍走 fromTree（编码回验），序列化失败（如「m_fHealth」不是有效数字）
    状态行报字段名、不发 JS、不写坏档。
    测试：SaveTreeTests +7（object 构建/编辑回写/复杂结构等价/vec·array·dict
    子节点/NaN 标记/非法数字报字段名/bool·null）；SaveEditorViewModelTests 重写
    适配节点树。Player.Core.Tests 126/126；全量 838/838 绿。
  - v2.58（2026-08-07）：**剪贴板截获再修复（shadow root）** —— 用户实机 v2.57 后
    「游戏剪贴板日志(截获)」**完全没有**（连空行都没了）。根因：Ruffle 的临时
    textarea 可能 append 到 ruffle-player 元素的 **shadow root**（attachShadow open）
    内部——`document.querySelector` 和观察 `documentElement` 的 MutationObserver
    都看不到 shadow 内部，v2.57 的 observer 从未触发。v2.58：execCommand 拦截恢复
    **同步提取**（调用时 set_value 已执行、textarea 尚未移除，读取最可靠），提取链
    选区 → 普通 DOM textarea → **shadowRoot.querySelector("textarea")** → activeElement；
    同时 `window.__watchClipboard(root)` 可在任意根挂 observer，block 3 在 player
    创建后对 `player.shadowRoot` 也挂一份。顺带清理 v2.57 遗留的重复 observer 块
    （外层 try 未闭合的残留）。待用户复测。
  - v2.59（2026-08-07）：**剪贴板截获改走 value setter（确定性方案）** —— 用户实机
    v2.58 仍空。v2.56-58 的 selection / MutationObserver / shadowRoot 查询都是
    "找 textarea 再读"，受挂载位置与时序影响不可靠。v2.59 改为
    **`HTMLTextAreaElement.prototype.value` setter 拦截**：Ruffle 无论把临时
    textarea 挂在哪（普通 DOM / shadow root），`set_value` 必然经过该 setter——
    同步、位置无关、时序无关（本页无其他 textarea 不会误伤）。execCommand 拦截
    保留阻断真实写入，并新增 `send("debug", "剪贴板 execCommand 拦截命中")`
    诊断行（若日志出现该行说明 Ruffle 确实走 execCommand；若始终没有则需重新
    审视调用链）。node 沙箱功能验证：textarea.value 赋值 → POST /__log
    level=clipboard「游戏剪贴板日志(截获): …」+ 去重 ✓。待用户复测。
  - v2.60/v2.61（2026-08-07）：**实机确认 + 诊断收尾 + 日志行可展开（R49）** ——
    用户实机日志（v2.60 诊断）证实：`剪贴板 value setter 拦截已安装` ✓ +
    `剪贴板拦截: cmd=copy sel=56 ta1=无 ta2=有(56)`（**textarea 挂在 ruffle-player
    的 shadow root**，value 长度 = 选区长度）+ **截获内容完整进日志**（游戏 mod
    加载日志，几千字符）。用户"日志还是空的"实为**浮层行高固定截断**（省略号 +
    tooltip），非截获失败。v2.61：移除 v2.60 诊断行（完成使命）；日志浮层行改
    **Expander**——摘要行固定高度 + 点开展开完整内容（多行剪贴板日志可见）。
    剪贴板链路终态：游戏 setData → Ruffle 临时 textarea（shadow root）→ value
    setter 截获进日志（v2.59 起 ✓）→ execCommand 阻断真实写入（系统剪贴板干净 ✓）。
  - v2.62（2026-08-07）：**日志热键 Shift+Tab → F10（R51）** —— 用户要求换键。
    host.html 页面桥改 F10（游戏聚焦时转发 toggle-overlay）；日志窗内 F10 关闭；
    主窗口 Avalonia 焦点时 F10 切换（三处同步，见 R51）。
  - v2.64（2026-08-08）：**临时日志清理（R55）** —— 去掉 v2.47 逐条「启动展开」info/warn
    日志与版本就绪行（用户反馈噪音）——只保留 error（LsoExpand 不可用/展开失败，诊断用）。
  - v2.66（2026-08-08）：**发布线拆分 + 1.0.0 内测包 + SmartScreen 指引（R60/R61，host.html 未变）** —— ① 两条独立发布线：release-player.yml（player-v* tag，只发播放器 zip）/ release-editor.yml（editor-v* tag，只发编辑器），删混发 release.yml；② csproj 版本 1.0.0，player-v1.0.0 已推送（内测包，含 README.txt）；dist/ 与 zip 不入库（.gitignore + 历史重写，单文件 exe 超 GitHub 100MB）；③ SmartScreen「发布者未知」= 未签名 + MOTW 正常提示，README 加解除锁定指引，代码签名（Azure Trusted Signing）列入计划。全量 861/861 绿。
  - v2.65（2026-08-08）：**mod 图片缺失真根因修复（R56-R58）+ 版本 1.0.0 内测包** ——
    用户目录实查（D:/Downloads/Neo Scavenger/）：① getmods.php 是空壳（nRows=0），
    真正生效的是 getmods2.php（nRows=47）——图片目录收集改为**两个文件都读**；
    ② getmods2.php 多行格式值末尾带 
 未 Trim，拼出的路径含换行符导致 mod 目录
    查找全失败（原版图片正常、mod 图片全缺——与用户现象吻合）——ParseModUrls 值
    Trim 修复；③ 图片缺失诊断写入日志文件（LogAction → RunLogStore）。真实目录
    端到端验证：主图 + mod 图（NSExtended/img）全部解析成功。版本号 0.9.0 → 1.0.0
    （首个内测包，publish.ps1 自动读取）。
  - v2.63（2026-08-07）：**UI 订正批次（R52-R54，host.html 未变）** —— ① 日志窗
    跟随主题（去掉固定深色，全量 DynamicResource）+ 顶栏两行（标题/路径 + 过滤/按钮）；
    ② 日志列表去虚拟化（StackPanel ItemsPanel，Expander 展开时滚动条不再跳动）+ 紧凑
    （行 20px、字号 11-12）；③ 全局工具窗口按钮紧凑（App.axaml `Window Button` 样式，
    存档管理/存档修改器/日志窗统一缩小）；④ **数据浏览器模组图片修复**：图片来源 =
    主 `img/` + `Mods/<mod>/img/`（与 ProxyHttpModule/ImageSearchService 约定一致），
    WikiDetailBuilder 构造时扫描缓存、按序查找——mod 图片不再"缺失"（R54）。
  - v2.67（2026-08-08）：**43% 卡点根因订正（非播放器限制）** —— Steam 模组版卡 43%
    「更新template-based items」**主要是编辑器写出的模组数据存在问题**（带模组实际可正常加载：
    成功版数据集 ~40 模组完整加载进入游戏），非 Ruffle/播放器兼容性限制。订正：README「已知限制」
    只保留杀软误报一条（删除 43% 条目）、Help/zh/Ruffle运行游戏.md FAQ 归因、本文 §2.5 / §3.6 /
    P2 验收 / R6 / §八 冒烟表述。纯文档订正，代码无改动。
  - v2.68（2026-08-08）：**WebView2 Runtime 启动检测 + 缺失弹窗** —— 播放器唯一渲染路径是
    WebView2 承载的 Ruffle 预览，运行时缺失不能再等到拖入 SWF 才在面板里露出裸错误文本。
    `WebView2RuntimeCheck`（Services/，注册表检测 EdgeUpdate Clients 键的 pv 值，四视图
    HKLM/HKCU × 64/32，与 WebView2 SDK 内部逻辑一致，零新依赖——Avalonia.Controls.WebView
    自带 interop 未引 SDK 包）：PlayerWindow OnLoaded 时检测，缺失 → 弹窗（提示 + 官方安装
    链接 https://go.microsoft.com/fwlink/?linkid=2124701，resx zh/en 三键），「打开安装页面」
    按钮默认浏览器直达；非缺失类异常不回退弹窗（懒创建 WebView 的兜底错误文本仍负责）。
  - v2.69（2026-08-08）：**启动日志（player-boot-*.log）** —— 实机反馈：部分机器启动即闪退
    （鼠标转圈后无反应、无任何输出），且此前启动期崩溃发生在 Serilog/FileRunLogWriter/UI 就绪
    之前，原因不可见。新增 `BootstrapLog`（Player/Services/）：Program.Main 第一行即建
    `logs/player-boot-*.log`（沿用 FileRunLogWriter.ResolveDirectory 的 exe 旁/回退规则，每启动
    一个、保留最新 5 个、逐行落盘），记录启动里程碑（Main 进入 + OS/CLR/args/WebView2 检测 →
    AppBuilder 启动 → XAML 加载 → PlayerServices.Create → 主窗口创建/显示 → 退出），并挂
    AppDomain.UnhandledException / TaskScheduler.UnobservedTaskException 早绑处理器 +
    Avalonia 启动 try/catch，崩溃原因必有落盘；游戏/运行日志保持独立 player-run-*.log 不动，
    反馈 zip 同时包含 boot 日志。纯日志基础设施，UI 无变化。
  - v2.70（2026-08-09）：**v1.0.1 内测包** —— 版本号 1.0.0 → 1.0.1，git tag
    `player-v1.0.1`（release-player.yml 自动构建 + 发 Release）；v2.68（WebView2 启动检测
    弹窗）与 v2.69（player-boot-*.log 启动日志）随 0809.01 入库后首个内测包。
  - v2.71（2026-08-09）：**闪退实测定案（无日志型）** —— 用户反馈机器（Win10 22H2 教育版）
    启动即闪退且**连 player-boot-*.log 都没有**：BootstrapLog 是托管 Main 第一行，无日志 =
    进程死在**托管代码之前**（CLR host 原生加载阶段）。实测定案：系统未升级/缺组件（装 .NET
    运行库 + 升级系统后可启动）——self-contained 虽自带运行时，host 仍依赖系统 UCRT/API
    组件，老 build Win10 缺件时静默失败（无提示、无日志、不产生 WER 条目属正常）。FAQ 补充
    「无 boot 日志 → 先升级系统/查杀软」。纯文档订正。
  - v2.72（2026-08-09）：**数据浏览器全量本地化 + 存档操作免重启（墓碑/保护）** ——
    ① **数据浏览器本地化**：编辑器数据表用 `[Display(Name=…)]` 属性 → resx `Xxx`（本地化
    字段名）/`XxxDesc`（本地化字段描述）——Player 侧复用同一套元数据：新增
    `artifacts/gen-player-field-resx.js`（从实体模型 + editor resx 提取 173 个字段键 × zh/en，
    写入 Player resx `FieldName.*`/`FieldDesc.*`；itemtypes 核心字段无 [Display]，手工补齐
    13 键含 SpriteList）；`GameTableMap.GetFieldDisplayKey`（列 → Display 键，无 Display 回退
    属性名，public）；`WikiDetailBuilder` 注入文本委托（表名/字段名/全部标签走 resx，无委托
    时保持内置中文默认 → 既有测试不变）；字段网格 = 本地化名 + 描述 ToolTip（原始列名保留
    在 Column 供对照）；表列表/引用 Tab = 本地化表名（`Table.*` 24 键，原始键 ToolTip）；
    行摘要列名前缀本地化（`GameDataRow.ColumnLabel`，取值时解析 → 语言切换即时生效）；
    语言切换就地重渲染（`Relocalize`：表名/行摘要/详情 markdown/字段/引用全部跟随，窗口
    可开着切语言）。② **存档管理免重启（用户反馈的坑：Ruffle 内存副本覆盖操作结果）**——
    根因：Ruffle 把运行中游戏的 SharedObject 缓存于 AVM 内存（avm2_shared_objects），从不
    重读 localStorage；自动保存/卸载 flush 会把内存副本写回，删除/恢复都被覆盖。host.html
    新增 `__deletedKeys`（墓碑）/`__protectedKeys`（恢复保护）两张拦截表：删除/清空 =
    墓碑化该 key → 一切写回被拦截（删除立即永久生效、不会复活；游戏可继续玩但该档保存
    挂起，直到重启游戏）；恢复 = 写入 + 保护 → 内存旧档无法覆盖恢复的存档；**游戏内
    clear/新开档自动解除墓碑**（新档保存放行）；存档管理操作**不再自动重启游戏**
    （移除 v2.50 的 RestartGameNow 自动重载，手动「重启游戏」按钮保留）；存档修改器保存前
    解除墓碑（明确意图）。③ 日志浮层级别过滤「全部」本地化（`Log.LevelAll`，`LevelFilterOptions`
    随语言重建）。测试：新增 LocalizationTests 9 项 + StorageManagerViewModelTests +4（墓碑/
    保护/免重启），Player.Core.Tests 126 → 143 全绿。
  - v2.73（2026-08-09）：**v2.72 追修（实机反馈）** —— ① **恢复必须自动重启**：删除→恢复后
    游戏**仍持空内存档**（Ruffle SharedObject 缓存），不重启则读档界面「显示没有存档」——
    恢复改回自动重启（300ms 重载页面加载恢复存档），删除/清空保持免重启；保护标志保证重启
    瞬间内存旧档（含卸载 flush）无法覆盖恢复值（沙箱验证：`player-tools/saves-no-restart-flow.js`
    删除→恢复→重启全流程通过）。② **字段词典审查订正**：编辑器 resx 的字段「名称」多为完整
    描述句，不适合做列头——全部 172 键订正为短名（zh ≤6 字/en 1-3 词，含义以
    `Core/Model/field_descriptions.json`（Docs/38）为准）；描述订正 ~16 处（共享键过于具体/
    错误，如 Description/Image/Price/Weight/Order/Editor/RemoveAll/TransferComponents 等）；
    修复生成器 XML 实体二次转义（`&lt;us&gt;` → `&amp;lt;us&amp;gt;`，实体先解码再重转义）。
  - v2.74（2026-08-09）：**存档操作免重启最大化（__saveTouched 检测）** —— 用户反馈重启
    3-8 分钟太慢。反编译取证（`player-tools/swf-src/scripts/DataHandler.as`）：**主菜单启动
    不读存档**——`FlxSave` 静态实例在首次 `LoadGame/SaveGame/DeleteSave` 才
    `SharedObject.getLocal("nsSGv1")` 创建并被 Ruffle 缓存于 AVM。host.html 包装器在
    get/set/remove 上标记 `window.__saveTouched`（启动展开块在包装器安装前执行不误标）：
    - **未触碰**（主菜单/新开档早期，游戏尚无内存副本）→ 删除/清空直接生效、
      **恢复直接写回立即生效，全部免重启**（游戏首次读档才创建实例，读到的就是最新值）；
    - **已触碰**（载入过/保存过/死亡删档后）→ 删除/清空仍免重启（墓碑）；**恢复必须重启**
      ——Ruffle 内存缓存是硬限制（读档界面读的是 AVM 内存副本，非 localStorage）。
    存档管理三操作状态文案分流（`Storage.*Instant` 三键）。测试 +3（未触碰删除/清空/恢复
    即时生效无墓碑无重启），Player.Core.Tests 146/146 全绿；沙箱
    `player-tools/saves-no-restart-flow.js` 增补未触碰流程验证。
  - v2.75（2026-08-09）：**数据浏览器本地化修复（用户反馈）** —— ① **表名列表退回英文的
    根因**：v2.72/2.73 生成器 `--apply` 按「注释 → `</root>`」替换整段，误删了插入在字段块
    之后的 `Table.*`(24)/`Wiki.*`(29)/`Log.LevelAll`/`Storage.DeleteDone` 等键——`--apply`
    改为只替换显式 BEGIN/END 标记之间的内容，误删键全部恢复（resx 全文件按 key 去重 +
    修复 en 文件既有的 `Log.ExportBundle` 嵌套坏行）。② **itemtypes 翻译不全根因**：生成器
    属性名提取正则只匹配属性声明前的文本，`[Column("fDurability")] public double Durability`
    紧凑单行写法提取失败 → 无 `[Display]` 的字段全部跳过——正则在 200 字符内跨到
    `public` 并从声明本身提取属性名，itemtypes 37 列翻译全覆盖（校验脚本 0 缺失）。③
    **chargeprofiles 语义订正**：attackmodes/itemtypes 的 strChargeProfiles 实际是**弹药类型**
    （用户确认）——字段名「耗电配置」→「弹药配置」，描述同步（chargeprofiles 表名也改为
    弹药配置）。④ **存档管理提示白话化**（用户反馈「不要复杂术语」）：窗口顶部
    `Storage.Hint` 只说明什么情况需要重启——备份/删除永不需要；游戏刚打开还没玩过时恢复
    也立即生效；玩过或保存过之后恢复才需要重启。删除/清空/恢复的状态文案同步简化。
  - v2.76（2026-08-09）：**英文字段名保持原样（用户要求）** —— 英文模式下字段名不再做
    可读化改写：`FieldName.*` 直接用 editor en-us 的原值（技术名/属性名，如
    PerUse/DamageCut/ChargeProfiles），编辑器缺失的键回退属性名本身（如 itemtypes 的
    Durability/MonetaryValue）；中文短名与中英文描述不受影响。生成器已同步（英文名永不
    走 NAMES 改写）。
  - v2.77（2026-08-09）：**存档管理引导式重启（用户要求）** —— 不再「点一下就重启」：
    - 不需要重启的操作（备份/删除/清空、未触碰时的恢复）：**静默直接生效**，无提示、无
      拦截、无重启标志；
    - 需要重启的操作（已触碰后的恢复）：写入 + 保护照常（内存旧档无法覆盖恢复值），但
      **不打断流程、不立即重启**——`NeedsRestart` 置位 → 窗口标题追加「退出时将重启游戏
      生效」（`Storage.RestartPendingTitle`，标题绑定 VM.WindowTitle），**窗口关闭时统一
      触发一次重启**（Closed 处理器；手动「重启游戏」按钮保留，RestartGameCommand 先清
      标志避免重复触发）；玩家可连续做多次操作后一次性重启。
    测试：恢复用例改为断言「不立即重启 + 待重启标志 + 标题提示 + 手动触发后重启一次」，
    Player.Core.Tests 146/146 全绿。
  - v2.79（2026-08-09）：**玩家向体验（不懂代码的玩家视角：首次引导/死亡恢复/更新/FAQ）** ——
    ① **死亡删档自动提示恢复**（玩家向核心）：游戏内删除「真实存在的存档」（死亡删档/
    重新开始）→ host.html removeItem 包装器（非存档管理操作 + 有旧值）postMessage
    `save-deleted` → 宿主弹窗「检测到存档被删除——要从最近备份继续吗？」。玩家不知道
    save_backup 的存在，这是进度救回的唯一入口；「继续」复用存档管理恢复逻辑（保护 +
    立即重启——死亡场景点了继续就是要玩）。② **记住游戏目录 + 启动自动加载**：
    PlayerSettings 持久化 GameRootDir；启动链 = 命令行 SWF → 上次游戏目录 → 自动定位。
    ③ **游戏定位（首次引导）**：`GameLocator`——Steam 注册表 SteamPath →
    steamapps/common/NeoScavenger（含别名）+ 下载/桌面/文档浅层扫描 NEOScavenger.swf
    （固定名优先，缺时唯一 *.swf）；「选择游戏文件夹…」入口（File 菜单 + 占位页按钮——
    文件夹选择器比选 SWF 直观），文件夹内无 SWF 给图文提示。④ **加载等待提示**：状态栏
    「正在加载游戏数据…（首次/模组多时约需 5-10 分钟，勿关闭窗口）」，Ruffle `loaded`
    事件 → `game-loaded` 消息 → 状态栏「游戏已启动 ✓」（玩家不再以为卡死）。⑤ **一键
    反馈包**：报错弹窗主按钮改为「生成反馈包」（导出存档+日志 zip 并 Explorer 定位）。
    ⑥ **检查更新**：`UpdateCheckService`（GitHub Releases API 对比 player-v 标签版本），
    启动静默 + 帮助菜单手动，有新版本弹窗直达下载页。⑦ **帮助菜单 + 界面内 FAQ**：
    FaqWindow 常见问题 7 条（存档在哪/死亡恢复/加载久/闪退/SmartScreen/mod 安装/快捷键），
    resx 双语随语言切换。沙箱增补死亡删档通知流程；Player.Core.Tests 146/146 全绿。
  - v2.80（2026-08-09）：**检查更新修复 + FAQ 精简 + 导出本地化修复** —— ① **检查更新
    误报「网络不可用」根因**：`CheckLatestAsync` 用 null 表示「没有新版本」，而手动检查
    处理器把 null 一律当作失败——刚发完 v1.0.2，latest == 当前版本 → 每次都报网络错误。
    重构为三态结果 `UpdateCheckResult(Ok, Info)`：网络/解析失败 = Ok=false 提示网络问题；
    成功但已是最新 = Ok=true + Info=null 明确告知「已是最新版本（vX）」；有新版本才弹
    下载页。**双通道兜底**：api.github.com（国内网络常被墙/限流）失败后走
    github.com/releases/latest 的 302 Location 头（不跟随重定向，从 URL 解析 tag）。②
    **FAQ 精简**（用户要求）：去掉「游戏闪退怎么办」「发布者未知/已保护你的电脑」「怎么
    安装 mod/汉化」三项，保留存档/死亡恢复/加载/快捷键 4 条。③ **导出本地化修复**：
    去重脚本把 en 修复正则误用到 zh，`Log.ExportBundle` 中文值被覆盖成英文——恢复
    「导出存档+日志 (zip)」。
    （注：NeoEditor.Plugins.JsVisualization 有未提交 WIP 编译错误
    IReferenceResolver/ReferenceList<>，非本版本引入，全解决方案构建待 WIP 完成后验证）
  - v2.78（2026-08-09）：**v1.0.2 内测包** —— 版本号 1.0.1 → 1.0.2，git tag
    `player-v1.0.2`（release-player.yml 自动构建 + 发 Release）；Release body 改为
    `body_path` 直接附 `NeoEditor.Player/CHANGELOG.md`（用户向更新内容，替代内联 body，
    CHANGELOG 同时作为资产上传），generate_release_notes 关闭；v2.72-v2.77（数据浏览器
    全量本地化、存档免重启/引导式重启、字段词典订正）随 1.0.2 入库。
    （注：NeoEditor.Plugins.JsVisualization 有未提交 WIP 编译错误
    IReferenceResolver/ReferenceList<>，非本版本引入，全解决方案构建待 WIP 完成后验证）
