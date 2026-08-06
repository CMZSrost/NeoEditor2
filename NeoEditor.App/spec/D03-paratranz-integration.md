# D03 — ParaTranz 翻译平台集成设计（数据转换 / 工作流 / UI）

> 设计文档 · 2026-08-05 · v1.2（v1.1 = WebView 改进；v1.2 = 认证边界定案）
> 上承：用户决策（接入 paratranz.cn 项目 15258）· NeoParatranz 旧工具（github.com/CMZSrost/NeoParatranz）
> 下启：M2–M5 实现里程碑（见「七、里程碑」）
> 依从：R24 Host Service 数据通路 · R20 DI 组合根 · D01 Plugin 架构 · R28 设置页模式
> 关联：Docs/42 WebView 面板计划（`Avalonia.Controls.WebView` 12.0.1 / `NativeWebView`，已验证）

---

## 一、背景与目标

NeoEditor 需要接入 ParaTranz（https://paratranz.cn，翻译平台，已有项目 15258
「NeoScavenger」）以支撑 Mod 文本的协作翻译。旧方案是独立的 Python 脚本
NeoParatranz（XML↔CSV 互转 + 上传/下载），存在三处短板：

1. **分离式工作流**：翻译文件（CSV）在编辑器之外流转，与 game.db 数据不同步；
2. **无 UI**：token/项目/文件全靠环境变量与命令行；
3. **无撤销**：`deconvert` 直接覆写 XML 文件，无 diff、无备份。

本设计把旧能力**内建**为编辑器插件 `NeoEditor.Plugins.Paratranz`，并补齐
diff 预览、可撤销应用、设置 UI。

### 目标

- 从 game.db 提取可翻译文本 → 生成与现有项目 15258 **完全兼容**的翻译文件并上传；
- 从 ParaTranz 拉取译文 → 预览差异 → 通过 `IHostService` 命令应用（可 Undo）→ 导出 XML；
- 设置页配置 Token/项目（加密存储），Dock 面板提供文件列表与进度。

### 非目标（本期不做）

- **编辑器内自建词条级审校 UI**——由「网页工作台」（§4.1 通道 D，WebView 嵌入
  paratranz.cn 项目页）承担，不重复造轮子；
- 术语表（terms）、讨论（issues）、成员（members）API 的 UI 化；
- 多游戏多项目的完整配置管理（单一项目先跑通）。

---

## 二、已完成：API Helper 模块（M1）

`NeoEditor.Plugins.Paratranz`（本项目，已注册进 App 组合根）：

| 文件 | 内容 |
|------|------|
| `Models/ParatranzModels.cs` | Project / File / String / Artifact / Job / Stage / PagedResult / 批量操作 DTO / `ParatranzApiException` |
| `Services/IParatranzApiClient.cs` | 类型化接口：项目、文件 CRUD、文件翻译（CSV 文本）、词条 CRUD + 批量、导出触发/下载、Token 校验 |
| `Services/ParatranzApiClient.cs` | System.Text.Json（Web 命名约定）实现；Bearer 认证；429 按 Retry-After 重试（≤3 次）；错误按 `{message,code}` 解析；上传内容先缓冲再组装 multipart（重试安全） |
| `ServiceCollectionExtensions.cs` | `AddParatranzPlugin()`：单例 client（BaseAddress `https://paratranz.cn/api/`） |
| `ParatranzPlugin.cs` | `IServicePlugin`（`[PluginKind(PluginKind.Service)]`） |
| `Tests/NeoEditor.Plugins.Paratranz.Tests` | 17 个单测（FakeHttpMessageHandler），覆盖认证头、分页、429 重试、multipart 组装、错误解析等 |

> M1 期间单测抓到一个线上必现 bug：`BaseAddress` 未以 `/` 结尾时相对路径解析
> 会丢失 `api` 段（请求打到 `https://paratranz.cn/projects/...` 404）。已修复并固化断言。

---

## 三、数据转换设计

### 3.1 翻译单元（Translation Unit）

```
TranslationUnit {
    Key       // xpath 定位串（见 3.2），文件内唯一
    Original  // 原文（提取自 game.db 实体）
    Translation // 译文（来自 ParaTranz 或本地 CSV）
    Context   // 上下文（可选，如字段名/表名/备注）
}
```

### 3.2 Key 方案：沿用 NeoParatranz 的 XPath 格式（强制兼容）

```
//table[@name="attackmodes"]/column[@name="id"][text()=1]/../column[@name="strName"]
```

**为什么必须沿用**：项目 15258 上已存在大量按此格式上传的词条（NeoParatranz
生成）。改 Key 方案会导致已有译文全部失配。NeoEditor 实体模型（`[Table]`/
`[Column]` 特性，`NeoEditor.Core/Model/Game/*.cs`）与 XML 导出格式（`XmlParser`
的 pma_xml_export 格式）与旧脚本处理的 XML 同构，Key 可以 1:1 生成。

### 3.3 可翻译字段发现

- **主规则**：实体上 string 类型的 `[Column]` 属性，且列名命中白名单：
  `strName / strNotes / strDesc / strDescAlt / strNamePublic / strWieldPhrase /
  vAttackPhrases / strSuccess / strFail / strPopUp / strHeadline /
  strPropertyName / strSecretName / strType`（即 NeoParatranz `translation_name`）。
- **特殊规则**（对齐旧脚本行为，防回归）：
  - `maps`：`strName` 为图片名跳过；**实际列 `strDef` 亦不在白名单 → maps 整体无翻译字段**
    （v1.3 实测订正，原稿误写为"保留 strDesc"）；
  - `gamevars`：无 `id` 定位列且列为变量名/数值，**整体跳过**（旧脚本对 gamevars 生成的
    key 亦为坏 key，v1.3 订正）；
  - `recipes.strType`：合成类型显示名，**需要翻译**；
  - id 字段优先 `id`，其次 `nID`（chargeprofiles / ingredients / recipes）——实现上
    直接取自实体 `UIDKeyAttribute` 的第一个非 EntityId 属性的列名，无需手写表。
- 发现逻辑收敛为一个 `ITranslationExtractor`（可单测），不依赖手写表。

### 3.4 文件组织（对齐旧项目）

Mod 目录结构 1:1 镜像：`<ModDir>/NSExtended/neogame.xml` →
ParaTranz 文件 `NSExtended/neogame.csv`（服务端 `path` 参数传目录，与
NeoParatranz 上传的 `path` 行为一致）。

### 3.5 反向转换（应用译文）

```
TranslationUnit.Key --解析--> (table, idField, id, column)
    → 查 IHostService.Repository<T>() 定位实体
    → BatchEditCommand / EditCellCommand 批量设置单元格（R24，可 Undo）
    → ExportModAsync + CommitExportAsync 写回 XML
```

Key 解析器复用旧脚本的 `parse_xpath` 正则语义（`//table[@name=...]/column[@name=...]
[text()=...]/../column[@name=...]`），写成 `ITranslationKeyParser`。

---

## 四、交互模式与工作流

### 4.1 四条通道（API 已全部就绪）

| 通道 | 接口/载体 | 特点 | 用途 |
|------|------|------|------|
| **A. 文件级 CSV** | `POST /files`（创建）、`POST /files/{id}`（更新原文）、`POST /files/{id}/translation`（更新译文，`force` 开关）、`GET /files/{id}/translation`（下载 CSV） | 批量、粗粒度；服务端按 hash 判断未变化文件 | **主通道**：与旧工作流一致，全量同步 |
| **B. 词条级** | `GET/POST/PUT /strings`、`PUT /strings` 批量 | 细粒度、增量；带 stage/context；分页（pageSize≤800） | 自动化增量修正（网页工作台不覆盖的场景） |
| **C. 导出包** | `POST /artifacts`（触发导出）、`GET /artifacts/download`（zip） | 一次性全量、含所有文件 | 「拉取最新译文」一键场景 |
| **D. 网页工作台**（v1.1 新增） | **WebView 嵌入** `https://paratranz.cn/projects/{id}`（`NativeWebView.Navigate`，Docs/42 已验证） | 平台完整 UI：翻译/审校/术语/讨论/统计；登录态靠 WebView2 cookie（spike 见 §6.3） | **翻译管理**：替代自建词条 UI；编辑器内完成「改→推→译→拉→应用」闭环 |

> **认证边界（v1.1 定案）**：通道 A/B/C 全部走 API **Bearer Token**（设置页配置），
> **与网页登录态完全独立**——纯同步操作（Tab 1）即使从未在网页登录也能正常工作；
> 通道 D 网页工作台（Tab 2）才需要网页登录（cookie），登录态丢失只影响 Tab 2，不影响同步。

### 4.2 主工作流（推荐）

```
【推送】game.db ──提取──▶ TranslationUnit[] ──CSV 序列化──▶ CSV
    1) 文件不存在        → POST /files（创建）
    2) 文件已存在、原文变化 → POST /files/{id}（更新原文；服务端 hash 相同会返回 status 跳过）
    3) 本地有译文需回传   → POST /files/{id}/translation（默认不覆盖已人工编辑词条，可勾选 force）

【拉取】三种入口（面板按钮）：
    1) 单文件：GET /files/{id}/translation → CSV → 解析 TranslationUnit[]
    2) 全量：   POST /artifacts → GET /artifacts/download → zip 解压 → 逐文件解析
    3) （后续）词条级增量：GET /strings?file=&stage= → 逐条对比

【应用】译文入编辑器（全部走 R24 命令）：
    diff 预览（旧值 vs 新值）→ 确认 → IHostService.ExecuteBatchAsync(BatchEditCommand)
    → （用户主动）导出 XML / 保存数据库
    * diff 预览由 WebView 渲染（§6.2），无需 DiffPlex

【翻译管理】网页工作台（通道 D）：
    面板内 WebView 打开项目页 → 用户在网页完成翻译/审校/术语 → 回到「拉取」通道取回
```

### 4.3 冲突与安全策略

- 上传译文默认 `force=false`：ParaTranz 只覆盖未人工编辑的词条，避免破坏译者劳动；
- 应用译文前必须 diff 预览（新增/修改/跳过三态统计）；
- 应用走命令栈：可整体 Undo，不直接改 XML 文件（对比旧脚本直接覆写的风险）；
- Token 只存加密配置（DPAPI，复用 `ConfigService.ConfigValueProtector` 模式）；
- 所有 ParaTranz 调用可取消（CancellationToken），429 已自动重试。

---

## 五、开源第三方框架建议

| 框架 | 版本建议 | 用途 | 理由 |
|------|---------|------|------|
| **CsvHelper** | 33.x | CSV/SSV 读写 | 翻译文本含逗号/引号/换行/中文标点，手写解析是重灾区；CsvHelper 处理转义、`\n` 语义、UTF-8 BOM，成熟稳定（MIT） |
| **Avalonia.Controls.WebView**（v1.1） | 12.0.1（已在 Player/WebView 插件使用） | ① 嵌入 paratranz.cn 网页工作台；② diff 预览离线 HTML 渲染（`NavigateToString`） | 官方控件、零新依赖（解决方案已有）；`NavigateToString`/`InvokeScript`/`WebMessageReceived` 已在 Docs/42 P0.2 验证 |
| System.IO.Compression（内置） | — | artifact zip 解压 | 无需三方包 |
| ~~DiffPlex~~ | — | （v1.1 取消） | diff 预览改由 WebView 渲染 HTML（§6.2），不再需要独立 diff 组件；若未来需要「文本级 diff 高亮」可再引入 |
| ~~Refit / Flurl~~ | — | （不引入） | 当前手写 STJ 客户端已有 17 个单测覆盖，声明式重构收益低；若未来 SDK 化再评估 |
| ~~翻译管理框架（i18n 等）~~ | — | （不引入） | 与游戏 XML 数据模型不匹配，过度设计 |

> v1.1 后唯一新增必需依赖仍是 **CsvHelper**（M2 引入）；WebView 相关能力全部复用现有
> `Avalonia.Controls.WebView` 包与 Docs/42 已验证的 API 面。

---

## 六、UI 设计（M3–M4）

### 6.1 设置页新增「ParaTranz」分组（仿 R28 AI & MCP）

`SettingsPaneViewModel` 新增 Display* 包装 + `SettingsPageView.axaml` 新增分组 +
三个语言资源文件补 `Settings.Paratranz*` 键：

| 控件 | 绑定 | 说明 |
|------|------|------|
| API Token 密码框 | `DisplayParatranzToken` | 保存时 DPAPI 加密（复用 ConfigService 加密路径，参照 `AiProviders[].ApiKey`） |
| 项目下拉 | `ParatranzProjectChoices` / `DisplayParatranzProjectId` | `GET /projects` 填充（Token 有效时） |
| 测试连接按钮 | `TestParatranzConnectionCommand` | `ValidateTokenAsync` + `GetProjectAsync`，仿 `TestImageConnection` |

### 6.2 Dock 工具面板「ParaTranz」（IToolPlugin，ToolDock.Right）——v1.1 双 Tab

`ParatranzPaneViewModel` + `ParatranzPaneView`（引用 `Avalonia.Controls.WebView` 12.0.1，
`NativeWebView` 用法照抄 `NeoEditor.Plugins.WebView/Views/WebViewToolView.axaml.cs` 的
懒创建/异常兜底模式）：

**Tab 1「同步」**（自建轻量 UI，编辑器数据通道）：
1. **项目概览卡片**：源/目标语言、词条总数/已翻译/已检查/已审核进度（`GET /files` 汇总或 `GET /strings`）；
2. **文件列表**（镜像 Mod 目录结构）：每行 = 文件名 + 翻译进度条（translated/total）+ 状态徽标（未翻译/已翻译/有疑问）+ 操作：
   - 上传原文（创建/更新，展示 hash 未变化跳过提示）
   - 上传译文（force 复选项）
   - 下载译文（单文件 CSV 或全量 zip）
3. **进度与日志**：同步任务进度条、最近操作日志（错误 → 通知横幅，仿现有 `NotificationService` 模式）。

**Tab 2「翻译工作台」**（v1.1 新增，WebView 嵌入网页）：
- `NativeWebView` 加载 `https://paratranz.cn/projects/{项目ID}`（设置页配置的项目）；地址锁死
  本域（防导航到外部），提供 刷新 / 在系统浏览器打开 两个按钮；
- 用户在网页内完成翻译/审校/术语管理（平台完整功能），登录态依赖 WebView2 cookie（spike §6.3）；
- 登录提示：加载页面后检测（导航到登录页/无用户态）时显示「请在网页中登录 ParaTranz 账号」
  引导条（v1.2，登录仅影响本 Tab；Tab 1 同步不受影响）；
- 面板「拉取译文」等同步操作对网页侧实时可见（服务端数据，天然一致）。

**diff 预览（v1.1 改为 WebView 渲染）**：
- 应用译文前弹窗内嵌 `NativeWebView`，`NavigateToString` 渲染**离线 HTML 模板**（内嵌 CSS，
  无外部依赖）：行级双语对照（原文 | 译文），按 新增/修改/跳过 三色高亮 + 顶部统计条
  （新增 N / 修改 M / 跳过 K）→ 确认应用 / 取消；
- 收益：大文本滚动性能好、双语排版清晰、零新依赖（替代原 DiffPlex 方案）。

插件类型从 `IServicePlugin` 升级为 `IToolPlugin`（或两者都实现），在
`AddParatranzPlugin()` 中补注册，App 无需其他改动（动态 Dock 自动拾取）。

### 6.3 WebView spike（P0-PT，M4 前置验证）

| # | 验证项 | 结论（2026-08-05） | 回退/状态 |
|---|--------|----------|------|
| PT1 | **WebView2 cookie 持久化** | **代码侧 ✅ 通过（源码级确认）**：`WindowsWebView2EnvironmentRequestedEventArgs.UserDataFolder` 为公开属性，直接流入原生 `CreateCoreWebView2EnvironmentWithOptions`（AvaloniaUI/Avalonia.Controls.WebView 仓库 `CoreWebView2Environment.CreateAsync`）；`NativeWebView.EnvironmentRequested` 在环境创建前同步触发，处理器内设置属性即可，**纯托管、无需 WebView2 互操作包**。已实现 `ParatranzWebViewSession`（`%LOCALAPPDATA%/NeoEditor/paratranz-webview` + 界面语言 zh-CN）。**实机验证待 GUI**（登录→重启→登录态保持） | 回退方案（每次登录）不再需要；实机验证随 M4 面板验收 |
| PT2 | **paratranz.cn 实机加载** | **待实机验证**（spike 宿主已就绪，见下） | 若不可用 → 自建词条表格原案（+2~3 天） |
| PT3 | **`NavigateToString` 渲染 diff HTML** | **✅ 通过**：`DiffHtmlRenderer`（离线 HTML，内嵌 CSS、`white-space:pre-wrap` 中文换行、HTML 转义）万行基准测试 **< 2s**（实测约百毫秒级），4 项测试全绿 | 无需降级 |

**Spike 宿主**（`artifacts/paratranz-webview-spike/`，git 忽略目录，不污染仓库）：
最小 Avalonia 窗口 + `NativeWebView`，挂 `ParatranzWebViewSession.ApplyPersistentSession`，
导航 paratranz.cn；运行 `dotnet run --project artifacts/paratranz-webview-spike`。
实机验证步骤：① 登录 paratranz.cn 一次 → ② 关闭窗口 → ③ 重新运行 → ④ 若登录态保持则 PT1 实机通过；
同时观察登录流程（人机验证）、项目页交互与中文渲染（PT2）。

### 6.4 可选扩展（M5+，不在本期）

- MCP 工具暴露「查询词条 / 提交译文」（复用 `IMcpToolProvider` 模式），让外部 AI 客户端可读翻译进度；
- 词条级 API 自动化（通道 B）：如「拉取 stage=2 有疑问词条」批量修正入口（网页工作台不满足的自动化场景）。

---

## 七、里程碑

| 里程碑 | 内容 | 依赖 |
|--------|------|------|
| **M1 ✅ 已完成** | API Helper 模块 + 17 单测 + DI 注册 | — |
| **M2 ✅ 已完成** | 数据转换层：`TranslationKeyParser`（xpath 兼容）/ `TranslationExtractor`（白名单+特殊规则）/ `CsvTranslationSerializer`（CsvHelper，2/3 列兼容+宽松容错）/ `TranslationApplier`（纯命令构建 → BatchEditCommand）；31 单测（Parser 9 / Extractor 6 / CSV 9 / Applier 7） | 引入 CsvHelper 33.1.0 |
| **M3 ✅ 已完成** | 设置页「ParaTranz」分组：`AppConfig.ParatranzToken`（ConfigService DPAPI 加密落盘）+ `ParatranzProjectId`；`SettingsPaneViewModel` 的 Token 密码框 / 项目下拉（测试连接后填充）/ 测试连接（GET /projects）；构造时 Token 同步到 `IParatranzApiClient` 单例（M4 面板复用） | M1 |
| **M4 ✅ 已完成** | Dock 面板「ParaTranz」（IToolPlugin，Right dock Order=60）：Tab 1 同步（Mod 下拉 + 项目摘要 + 文件列表/进度 + 上传原文/应用译文）+ Tab 2 翻译工作台（NativeWebView + `ParatranzWebViewSession` 持久会话 + 地址锁域/刷新/浏览器打开）；diff 预览弹窗（NativeWebView + `DiffHtmlRenderer`，确认后 R24 命令应用）；`ParatranzSyncService` 编排（提取→按 FilePath 镜像上传创建/更新、下载→构建→执行）；11 新增单测（SyncService 7 + 路径 5 参数化） | M2 + M3 + PT1/PT3 |
| **M4** | Dock 面板（双 Tab）+ 同步工作流：文件列表/进度、上传原文/译文、下载（单文件+全量 zip）、**WebView 网页工作台（通道 D）**、**WebView 渲染 diff 预览**、命令式应用 | M2 + M3 + P0-PT spike |
| **M5（可选）** | MCP 工具（查询词条/提交译文）、词条级自动化（通道 B，如 stage=2 批量修正） | M4 |

> v1.1 变更：原 M5「自建词条级管理表格」**取消**（由通道 D 网页工作台替代）；
> 原 M4「DiffPlex diff 预览」改为 WebView 渲染（§6.2）；新增 P0-PT spike（§6.3，M4 前置）。

---

## 八、决策边界

### 适用

- NeoScavenger Mod 文本的 ParaTranz 双向同步（推送原文 / 拉取译文）；
- 编辑器内可撤销的译文应用（R24 命令通路）。

### 不适用

- **代替 ParaTranz 网页的翻译/审校工作台**——由通道 D（WebView 嵌入网页）承担；
- 非本游戏格式（xpath key 方案与 pma_xml_export 绑定）；
- 多项目并行管理。

---

## 九、验收标准（M2–M4 完成后）

1. 设置页可配置 Token 与项目，测试连接给出明确成败提示；Token 在 config.json 中非明文；
2. 面板列出项目 15258 的文件与翻译进度，与网页端一致；
3. 一键上传原文：新文件创建、原文变化更新、未变化跳过（展示 status 提示）；
4. 拉取译文（单文件与全量 zip）→ WebView diff 预览 → 应用 → 可在编辑器 Undo → 导出 XML 后游戏内生效；
5. 全程无 `GameDbContext` 直写（R24）、无 XML 文件直接覆写；
6. 旧 NeoParatranz 上传过的文件（现有 xpath Key）拉取/应用后译文不丢失；
7. （v1.1）面板 Tab 2 网页工作台：编辑器内可直接打开项目页并完成一次翻译操作（PT2 通过）；
   diff 预览为离线 HTML 渲染，无网络依赖（PT3 通过）。

---

## 十、评审记录（已定案 / 按推荐实施）

1. **同步主通道**：文件级 CSV（通道 A）为主——**按推荐实施**；词条级（通道 B）留 M5 自动化场景；
2. **面板入口**：Dock 右侧面板 + 设置页配置——**按推荐实施**；
3. **CsvHelper 引入**：同意——**按推荐实施**（M2 引入，唯一新增必需依赖）；
4. ~~DiffPlex~~（v1.1 已取消）：diff 预览改 WebView 渲染；
5. **网页工作台**——**已定案（v1.2）**：双 Tab 面板结构；PT1 回退（每次网页登录一次）已接受
   （用户 2026-08-05：纯 API 操作配 Token 无需登录，网页登录仅 Tab 2 工作台使用）。

---

## 版本历史

- **v1.0（2026-08-05）**：初稿——数据转换（xpath Key 兼容、字段白名单、特殊规则）、三同步通道
  （A 文件级 / B 词条级 / C 导出包）、主工作流、第三方框架（CsvHelper + DiffPlex）、UI（设置页 +
  Dock 面板）、M2–M5 里程碑、验收标准。
- **v1.1（2026-08-05）**：**WebView 改进**（用户提议，项目已有 Docs/42 WebView 能力）——
  ① 新增通道 D「网页工作台」：面板 Tab 2 内嵌 `NativeWebView` 加载 paratranz.cn 项目页，替代
  原 M5 自建词条级管理表格（省 2~3 天工作量，网页功能更完整）；② diff 预览改为
  `NavigateToString` 离线 HTML 渲染（取消 DiffPlex 依赖，零新包）；③ 新增 P0-PT spike
  （cookie 持久化 / 实机加载 / diff 渲染性能）与回退方案；④ §4/§5/§6/§7/§8/§10 同步修订。
- **v1.2（2026-08-05）**：**认证边界定案**（用户决策）——通道 A/B/C 纯 API 操作配 Token 即用、
  与网页登录态完全独立；通道 D 网页工作台登录态仅影响 Tab 2，PT1 回退方案接受
  （每次使用登录一次）；§4.1 增「认证边界」说明，§6.3/§10 定案标注。
- **v1.3（2026-08-05）**：**M2 数据转换层完成**——实现差异订正：①maps 实际列 `strDef`
  不在白名单 → maps 整体无翻译字段（原稿误写"保留 strDesc"）；②gamevars 整体跳过
  （旧脚本生成坏 key）；③CSV 解析用 CsvParser（CsvReader 会对异列数行抛 BadDataException）
  + `BadDataFound=null`（宽松格式宽容解析）；④id 字段直接取自 UIDKeyAttribute 列名。
  测试 48/48 全绿（M1 17 + M2 31）。
- **v1.4（2026-08-05）**：**M3 设置页完成**——AppConfig 新增 `ParatranzToken`/`ParatranzProjectId`
  （Token 经 ConfigService 的 ConfigValueProtector DPAPI 加密落盘，与 AiProviders 同机制）；
  SettingsPaneViewModel 新增 ParaTranz 区块（Token 密码框 / 项目下拉 / 测试连接按钮，
  测试连接 = GET /projects 填充下拉）；Token 在 VM 构造时同步到 IParatranzApiClient 单例
  （M4 面板复用同一 token）；resx ×3 新增 11 键；构建 0 错误、48/48 全绿。
- **v1.5（2026-08-05）**：**P0-PT spike 完成**——PT1 代码侧定案（源码级确认
  `WindowsWebView2EnvironmentRequestedEventArgs.UserDataFolder` 公开属性 → 原生 env 创建，
  纯托管实现 `ParatranzWebViewSession`，实机验证并入 M4 验收）；PT3 通过
  （`DiffHtmlRenderer` 离线 HTML，万行基准 <2s，4 测试）；PT2 待实机（spike 宿主
  `artifacts/paratranz-webview-spike/` 就绪）；插件新增 Avalonia.Controls.WebView 12.0.1
  引用（M4 面板用，与 Player 同包）。测试 52/52 全绿。
- **v1.6（2026-08-05）**：**M4 面板完成**——插件升级为 `IToolPlugin`（Workbench，Right dock）；
  双 Tab 面板（同步 + WebView 工作台，地址锁死 paratranz.cn 项目页）；diff 预览弹窗
  （`NavigateToString` 离线 HTML + 确认/取消，确认后 `IHostService.ExecuteBatchAsync` 可 Undo）；
  `ParatranzSyncService`（上传按实体 FilePath 镜像旧工具文件结构、下载→构建→执行）；
  实现差异：插件不依赖 App 的 ViewModelBase（R18）→ 面板 VM 直接用 `ObservableObject`
  + 注入 `ILocalizationService`/`INotificationService`；SyncService 的 IHostService 调用点
  为可覆写虚方法（测试替身）。测试 63/63 + 全量 793/793 全绿。PT1/PT2 实机验证待 GUI。
