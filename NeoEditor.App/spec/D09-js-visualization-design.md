# D09 — JS 可视化：center 可视化 UI 的 WebView2 + JS 渲染重构

> 设计文档 · 2026-08-08 · v1.2（实施推进：P0 → P0.8 全部落地）
> v1.1 = 用户拍板：① **Encounter 优先**（最复杂也最完善，语义契约以它为模板铺开）；
> ② P4 验收无 bug 后**下线 Avalonia visualizer**（不再长期双轨）；
> ③ 命名沿用（插件 `NeoEditor.Plugins.JsVisualization`、资源键 `Jsv.*`）。
> v1.2 = 实施记录追加（P0.5 焦点切换动画 / P0.6 局部重渲染 + XML 导入 / P0.7 崩溃修复 /
> P0.8 **单 WebView2 共享 → 修订 v2**：共享控件 reparent 实机输入失效（"像图片一样"），
> 改为**每文档 WebView2 + 环境共享**（UserDataFolder/Profile 统一 → 单一浏览器进程））+
> 订正 P0.7 性能段落中已被取代的旧架构描述。
> v1.3 = [D10](D10-js-visualization-upgrade-design.md)（AF4 审查与组件升级设计思路）发布：
> §四"原样继承"指 **D04-D08 语义规格**；组件层按 D10 统一模板与 9 项改进执行（Section 单轨/
> 导航历史/状态记忆/Raw Data 移底部/RefPanel 聚合/薄类型分级），随 P1-P4 分期吸收。
> 实施状态：插件 + 扩展点 + VizContentServer + 快照契约 + Encounter 全量语义 + JS 页面
> + 每文档 WebView2（环境共享，单浏览器进程）+ 测试 40 项，构建通过、全量 922 回归通过。
> 上承：D04/D05/D06/D07/D08 视觉规格（Avalonia visualizer 的设计语言）
>        + Docs/42 WebView 计划（NativeWebView 宿主 / 回环 HTTP / JS 桥 / 反代数据注入）
> 下启：`NeoEditor.Plugins.JsVisualization` 新插件 + `EntityEditorView` 第三 Tab「JS 可视化」
> 依从：R17/R18（插件边界，跨插件扩展走扩展点）· R24（数据只经 IHostService，只读）·
>       R12（选中由 ISelectionService 统一，以 Center 为主）· R06（四区域数据同源）·
>       R05（跨区域联动走 IMessenger）· D02（动态 Dock 布局，文档进 Center DocumentDock）
>
> 设计原则（本设计的地基）：
> **① 页面零宿主依赖** —— 可视化页面是纯静态 HTML/JS/CSS，不强制依赖 `chrome.webview`，
> 数据全部经相对路径 fetch 同源获取。同一页面在 WebView2 与独立浏览器（静态服务/file://）
> 中行为一致，因此**可视化效果可以被 AI 截图验证**（Avalonia 时代做不到）。
> **② 语义提取留 C#，布局渲染归 JS** —— D04-D08 里 Visualizer 的"语义翻译"逻辑
> （引用语法翻译、效果聚合、概率标注）留在 C# 产出结构化 JSON，JS 只负责把 JSON
> 呈现为视觉组件。语义正确性继续由 xUnit 覆盖，JS 层做到"给数据就出图"。
> **③ 渐进双轨，不一次性重写** —— 新 Tab 与现有 Avalonia「可视化」Tab 并存，逐个实体
> 类型迁移，迁移完成、验收通过后才评估下线 Avalonia visualizer。

---

## 〇、背景与目标

**现状**：center 的可视化 = `EntityEditorDocument`（Center DocumentDock 的文档 Tab）内
`EntityEditorView` 的第一个 Tab「可视化」，由 `IEntityVisualizer.BuildDetail(IEntity)` 返回
**Avalonia 控件树**（`EntityEditorView.axaml.cs:133-146`）。25 个 visualizer 全部是
代码构建的 StackPanel/Grid/Border 组合，视觉规格沉淀在 D04-D08。

**问题**（用户 2026-08-08 提出）：
1. 可视化 UI 是"写死的控件树"，改一个视觉细节 = 改 C# + 重新编译 + 重启；
2. 渲染与数据获取耦合（`IEntityLookupService`/`RefNode` 直接进 visualizer），难以独立演化；
3. **AI 无法看见效果** —— Avalonia GUI 截图模型侧不可见，视觉规格只能靠人肉验收。

**目标**：把可视化 UI 重构为 **WebView2 承载的 JS 页面**——域数据（实体快照 JSON/XML）
注入页面，JS 动态渲染；**给 center 加一个新 Tab「JS 可视化」**；并让
"传 XML → 看到渲染效果"成为可被 AI 闭环验证的能力（见 §六）。

## 一、决策摘要

| # | 决策 | 结论 | 理由 |
|---|------|------|------|
| 1 | 命名 | **JS 可视化**（JsVisualization） | 用户定名；插件名 `NeoEditor.Plugins.JsVisualization` |
| 2 | Tab 落点 | `EntityEditorView` TabControl **第三 Tab**（center 文档内） | center 的"可视化 UI"本来就住在实体编辑文档里；新增独立 Document 类型需改壳层打开逻辑（EntityEditorPlugin.SupportedEntityTypes → CreateDocument），侵入大且与"重构现有可视化"目标错位 |
| 3 | 插件归属 | **新建独立插件** `NeoEditor.Plugins.JsVisualization`（Feature，引用 Player.Core + Infra + UI.Common） | 与 WebView 插件（通用浏览器面板/SWF 预览）职责分离；引用 Player.Core 沿 42 §3.8 已批准的"子应用拆分"例外（见 §八·风险） |
| 4 | 跨插件接入 | UI.Common 加扩展点接口，EntityEditor 只依赖接口 | R17/R18：EntityEditor 不引用 JsVisualization；App 组合根注册实现（R20） |
| 5 | 内容承载 | **回环 HTTP 一统**（自持 `VizContentServer`，127.0.0.1 动态端口） | 复用 42 §2.3 方案 A 结论；静态页面 + JSON 数据端点 + action POST 同源，天然满足"零宿主依赖"；不采用虚拟主机映射（资源需落盘）与 NavigateToString（无资源加载） |
| 6 | 数据契约 | `EntitySnapshotDto`（C# 语义化 JSON，含 rawXml 与结构化字段） | 语义提取留 C#（xUnit 可测），JS 纯渲染；rawXml 保留供调试与未来 JS 侧自定义 |
| 7 | XML 输入 | 双通道：C# 端点接受 XML 实时出快照；页面 debug 模式 DOMParser 兜底 | 回答"传 XML 看效果"——编辑器内 XML Tab 编辑实时反映到 JS 可视化；调试/验证时直接喂 XML |
| 8 | 交互桥 | 页面 fetch POST `/viz/action`（主），`chrome.webview.postMessage`（WebView2 加速，可选） | 浏览器兼容优先；42 §3.4 已有"宿主桥优先、页面内 POST 兜底"先例，此处反用（POST 为主、桥为辅）以保零依赖 |
| 9 | JS 技术栈 | **原生 JS + CSS，无框架、无构建链** | 项目无 Node 构建链（纯 dotnet）；页面作为插件内嵌资源直接发布；组件库对应 VisHelperService 工具箱 |
| 10 | 生命周期 | **每文档一个 WebView2 + 环境共享**（P0.8 v2）：每个文档 JS tab 一个 NativeWebView（懒创建 + 关闭释放），所有实例共享同一 UserDataFolder/Profile → 单一浏览器进程 | 初版「Tab 首次激活才建 WebView」；用户实机反馈标签页快关快开、数量大 → 先试单控件共享（reparent 输入失效，废弃）→ 收敛到环境层共享（P0.8 v2） |

## 二、总体架构

```
┌─ Center DocumentDock ─────────────────────────────────────────────┐
│ ▸ Tab: EntityEditorDocument A           ▸ Tab: EntityEditorDocument B │
│   ┌─ TabControl ─────────────────┐        ┌─ TabControl ─────────────┐ │
│   │ [可视化][XML][JS 可视化]      │        │ [可视化][XML][JS 可视化]  │ │
│   │  ┌──────────────────────┐   │        │  ┌──────────────────────┐ │ │
│   │  │ JsVizView             │   │        │  │ JsVizView（懒创建）   │ │ │
│   │  │  NativeWebView #1     │   │        │  │  NativeWebView #2    │ │ │
│   │  │  同环境（共享进程）     │   │        │   │  同环境（共享进程）    │ │ │
│   │  └──────────────────────┘   │        │  └──────────────────────┘ │ │
│   └─────────────────────────────┘        └──────────────────────────┘ │
│   （切到 JS tab 才创建；关闭/Dock 回收即释放）                          │
└────────────────────────────────┬───────────────────────────────────┘
                                 │ EnvironmentRequested 事件统一配置
┌────────────────────────────────▼───────────────────────────────────┐
│ VizWebViewEnvironment（静态）—— 共享 WebView2 环境                  │
│  · UserDataFolder = %LocalAppData%/NeoEditor/WebView2Viz           │
│  · ProfileName = "neoviz" → 全部实例共用**单一浏览器进程**与缓存      │
└────────────────────────────────┬───────────────────────────────────┘
                │                                    ▲
  A. 实体变化    ▼                                    │ C. 交互（点击徽章/跳转）
┌─────────────────────────┐   B. 页面 fetch  ┌──────────────────────┐
│ NeoEditor.Plugins.       │ ───────────────► │ 页面（纯静态，零宿主依赖）│
│ JsVisualization          │   /viz/data?…    │  index.html + app.js │
│  VizContentServer(回环)   │ ◄─────────────── │  + xml-import.js     │
│  /viz/index.html 静态资源  │   EntitySnapshot │  渲染组件：Card/StatBar│
│  /viz/data      快照 JSON │   JSON           │  /NodeCard/RefBadge… │
│  /viz/assets    图片      │                  └─────────┬───────────┘
│  /viz/action    POST 桥   │ ◄──────────────────────────┘
│  VizActionHandler         │   {type, entityType, entityId, modifier}
│  ── 经 IHostService ──    │
│  · GetCachedEntity / Repository<T>（只读，R24）              │
│  · IXmlParser.Parse/Export（XML ↔ 实体）                     │
│  · INavigationRouter（跳转）/ ISelectionService（选中，R12）   │
└─────────────────────────┘
```

**跨插件扩展点**（决策 4 的具体形态，放 UI.Common，EntityEditor 只依赖它）：

```csharp
// NeoEditor.UI.Common/Visualizers/ — 与 EntityVisualizerRegistry 同目录
public interface IEntityJsVisualizationHost
{
    string Name { get; }                 // "JS 可视化"
    Control? BuildView();                // 返回 WebView 宿主视图（无 WebView2 环境返回 null → Tab 隐藏）
    void LoadEntity(IEntity entity);     // 实体（或文档）切换时注入：重导航/重注入
}
```

- `EntityEditorView.axaml`：TabControl 加 `<TabItem Name="JsVizTabItem">`（默认 `IsVisible=false`），
  内含空 Grid `JsVizHost`；code-behind `OnDataContextChanged`/`RebuildVisualizer` 时机
  （`EntityEditorView.axaml.cs:110-146` 旁）尝试 `GetService<IEntityJsVisualizationHost>()`
  （沿现有 `Application.Current.Resources["Services"]` 解析模式）→ 命中则挂轻量壳、
  Tab 显示、`LoadEntity(doc.Entity)`；`Entity` 变化时 `LoadEntity` 刷新。
- App 组合根（R20）注册实现；无实现时 Tab 保持隐藏——**插件可卸、壳层零改动**。

**为什么新建插件而非塞进 WebView 插件**：WebView 插件 = 通用浏览器面板 + SWF 预览
（Live 数据源/日志桥已耦合）。可视化需要的是"受控页面 + 快照数据 + action 协议"
三件套，是另一种契约；合一起会让面板 VM 背上两套导航语义。新建插件职责单一，
且 DI 注册只加一行（App.axaml.cs 组合根）。

## 三、页面与数据契约

### 3.1 EntitySnapshotDto（C# 侧序列化，`/viz/data` 端点产物）

```jsonc
{
  "type": "Creature",            // 实体类型（GameTableMap 表名）
  "id": "hunter",                // EntityId
  "modId": "",
  "displayName": "Hunter",       // 本地化名（ILocalizationService）
  "image": "/viz/assets?path=img/creature/hunter.png",  // 或 data URI；无则 null
  "rawXml": "<Creature>…</Creature>",                    // IXmlParser.Export 实时产物
  "semantics": { }               // ★ C# 语义提取结果 —— 结构由各实体类型的
                                 //   "语义契约"定义（对照 D04-D08 的卡片数据）
}
```

- `semantics` 是**每个实体类型一个的 JSON Schema**（v1 以文档形式在插件内 `semantics/*.md`
  登记，实现即"把 Visualizer 的卡片数据搬成 DTO 类"）。例（Creature，对照 D05）：
  `{ hero: {img,badges[]}, combat: {damage:[{label,value,color}], stats:[…]}, loot: {pools:[…]}, encounters: {chain:[…]}, refs: {incoming:[{type,id,label}], outgoing:[…]} }`。
- **序列化**：仿 `ParatranzApiClient.cs:26` 建共享 `JsonSerializerOptions`
  （`JsonSerializerDefaults.Web`）单例，放 JsVisualization 插件内。
- **XML ↔ 快照**：`/viz/data?type=&id=` 走实体缓存出快照；`/viz/data?xml=<text>`
  走 `IXmlParser.Parse` → 同一套语义提取 → 快照。**两条输入共用同一渲染管线**——
  这就是"传 XML 看到效果"的正式通道（编辑器内 XML Tab 编辑 → 文档脏时实时重导出 →
  JS Tab 重 fetch，所见即所得，R06 同源）。

### 3.2 页面（`/viz/index.html` + `app.js` + `components/*.js` + `viz.css`）

- **零宿主依赖**（设计原则 ①）：
  - 全部资源相对路径加载，同源 fetch；
  - 可用时检测 `window.chrome?.webview` 增强（postMessage 交互加速），**缺失时
    静默走 HTTP，绝不抛错**；
  - 支持独立打开：`index.html?sample=creature`（读 `samples/` 目录本地 JSON，
    由测试资产提供）与 `index.html?xml=<encoded>`（debug，页面内 DOMParser 兜底转快照）。
- **渲染管线**：`fetch 快照 JSON → 按 type 选渲染器 → 组件树 → 注入 DOM`。
  类型渲染器注册表：`app.js` 里 `renderers = { Creature: …(snapshot) => Card/Hero/Combat… }`。
- **组件库**（对照 VisHelperService 工具箱，§四）：纯函数组件
  `c('card', {title, children})` 返回 HTMLElement，样式全在 `viz.css`（CSS 变量主题）。
- **交互**：RefBadge 点击 → `fetch('/viz/action', POST)`（§五）；hover 详情走 CSS/局部
  tooltip（不依赖宿主）。

### 3.3 图片资产

`/viz/assets?path=<相对 gameRoot 的路径>`，参照 `ProxyHttpModule` getimages 先例：
磁盘优先、缺失 404；路径越界拒绝（42 §3.2 GameContentServer 越界 404 模式）。头像/图标
优先经快照 `image` 字段带上，减少往返。

## 四、UI 规格迁移（Avalonia 组件 → JS 组件）

D04-D08 的视觉规格**原样继承**（布局/语义色/徽章/节点卡语言不变），只换渲染载体：

| VisHelperService / Visualizer 资产 | JS 组件 | 规格来源 |
|---|---|---|
| `Card`（VisHelperService.cs:260） | `Card` | D04-D08 通用 |
| `SectionHeader`（:296）/ `ValueRow`（:331） | `Section` / `ValueRow` | D04 心理模型布局 |
| `StackedDamageBar`（:510）/ `CenteredStatBar`（:632）/ `CreatureStatGrid`（:700） | `StatBar`（stacked/centered 两种模式） | D05 战斗三层 |
| `MiniBadge`（:362） | `Badge`（语义色 class） | D05 出场状态概率 |
| `BuildEncounterNodeCard`（D06/D07/D08） | `NodeCard`（前驱/当前/后继三态 + 概率/终止胶囊 + AND/p2·p3） | D06/D07/D08 v1.3 |
| `RefNode.Badge`/`WireNavigation`（RefNode.cs:37/:120） | `RefBadge`（Ctrl+Click 跳转 / Ctrl+RMB peek） | R12/R16 |
| `BuildReverseRefsPanel`（:724）/ `BuildRefTooltip`（:384） | `RefPanel` / hover `Tooltip` | D04 反向引用 |
| RawData Expander / 折叠 | `Details` 折叠区 | 全类型兜底 |

**迁移顺序**（v1.1 用户拍板：**Encounter 优先**——最复杂也最完善，语义契约以它为模板
铺开）：Encounter（D06-D08 v1.3 全量，NodeCard 三态/流转/效果区/入口）→ Creature（D05）→
ItemType（D04）→ Recipe → 其余 21 种走通用渲染器（Card + 字段表 + rawXml 兜底，
与现有 visualizer 兜底逻辑同构）。

## 五、交互桥（JS → C#）

协议统一为 POST JSON，`/viz/action`：

```jsonc
{ "kind": "navigate" | "peek" | "select" | "openXml",
  "entityType": "Encounter", "entityId": "90", "modifier": "" | "ctrl" }
```

| kind | 含义 | 宿主动作 |
|---|---|---|
| `navigate` | 点击 RefBadge（Ctrl+LMB，沿 RefNode.WireNavigation 语义） | `INavigationRouter` 打开目标实体文档 |
| `peek` | Ctrl+RMB peek 预览（D08 v1.3：解析目标实体） | 调 peek 宿主（沿既有 peek 通道） |
| `select` | 普通点击选中（R12：以 Center 为主） | `ISelectionService` 同步选中（R05 消息联动四区域） |
| `openXml` | 页面内"在 XML Tab 打开"（调试） | 切 EntityEditorView 到 XML Tab（同文档内部导航） |

WebView2 环境：页面优先 `chrome.webview.postMessage` 同协议（42 §3.4 宿主桥语义），
宿主 `WebMessageReceived` 收到后走同一 Handler；浏览器环境（验证/独立调试）走 HTTP。
**双向可选、协议唯一**——C# 侧只写一个 `VizActionHandler`。

## 六、AI 可视化验证闭环（回答"传 XML 能看到效果吗"）

**能，这是本设计的核心收益**。Avalonia visualizer 时代 AI 看不见 GUI；JS 可视化后：

```
1. 数据：xUnit 测试资产 samples/*.json（真实实体导出 / 测试夹具，入库）
         或调试 URL：index.html?xml=<encoded>（XML 直接喂页面）
2. 渲染：浏览器自动化（browser-use）打开页面 → 截图（WebView2 = Chromium，
         浏览器渲染与编辑器内一致 —— 零宿主依赖原则的兑现）
3. 识别：deepseek-vision MCP（analyze_image）识别截图 → 对照 D04-D08 规格验收
4. 迭代：改 JS/CSS → 重截图 → 闭环
```

编辑器内的"传 XML 看效果"（用户视角）：XML Tab 编辑 → 切「JS 可视化」Tab →
`/viz/data?xml=` 端点实时出快照 → 页面重渲染（§3.1）。AI 验证与用户使用走
**同一条快照契约**，差异只在页面宿主（浏览器 vs WebView2）。

验收项示例（写进插件测试/验收清单）：Creature Hero 徽章语义色、Encounter 三行流转
布局 + 概率胶囊、RefBadge Ctrl 修饰交互在浏览器端可用 `?sample=` 复现。

**P0 验证实录（2026-08-08）**：
- `samples/encounter90.json`（构造真实感数据：物品触发/消耗/成功率/终止自指/效果全字段/
  触发器）由**测试生成器**经与 `/viz/data` 相同的提取管线产出并入库（随 Content 发布）；
- IAB 浏览器打开 `index.html?sample=encounter90`：**domSnapshot 逐区块验证通过**——
  Hero（ID/类型 chip/RemoveCreatures/四概率行）→ 内容与效果（描述 + 7 类效果行全出）→
  场景流转（前驱「🛡 撬棍 ×1」/当前 📍/后继 Enc#12 40% + Enc#9 40% + ⏹ 停留 20%）
  → 如何进入（饥饿 Fatal 红 / NOT -3 / 触发器 📍📅♻）→ Raw XML 折叠；
- **已知事项（P1）**：① 地图标注 `aMinimapHexes` 按逗号分割会拆开 "5,5" 坐标
  （`📍(5)`、`📍(7)` 碎片）——与 Avalonia 版同源行为，P1 改为分号/智能分割；
  ② 未解析前置条件显示 `NOT -3`（原始 id，可接受）；③ 本环境 IAB 截图管道不可用
  （screenshot capture failed），视觉布局验收需在编辑器内或 headless CDP 下人工确认。

**P0.5 交互演示（2026-08-08，用户指定「加个小改动」）**：场景流转**焦点切换动画**——
左键点击前驱/后继卡（及「回到当前」/前置过滤重算）时：旧流转区 `.flow-leave`
淡出 220ms → 新内容 `.flow-enter` 淡入 350ms + 当前场景卡 `.card-pulse` 高亮脉冲
（`box-shadow` 扩散一圈）。纯 CSS keyframes + JS 类切换，**零 C# 改动**（Avalonia 版
需重编译）——即 D09 论证的演化成本差异。配套：① `?autoplay=N` 调试参数（自动模拟
点击第 N 张可导航卡，headless 截图/自动化无法真实点击时的验收通道，触发同一动画
路径）；② sample 模式焦点切换走本地 `samples/<type><id>.json`（静态服务器无
/viz/data 端点时的客户端数据源，正式环境不受影响）；③ `body[data-flow-animated]`
动画执行探针。验证：Edge headless 截图确认切换后当前卡 = 前驱「翻找垃圾桶」
（前后文重算正确：无前驱 → ⛳ 入口、后继 Enc#90 100% 绿胶囊）。

**P0.6 交互修正（2026-08-08，用户验收反馈）**：
- **① 焦点切换改为流转区局部重渲染**——初版整页 replaceChildren 偏离 D08 v1.3
  组件内导航语义（Avalonia 版 RebuildFlow 只重建流转区）。现 `buildFlowSection` /
  `renderFlowInto` 只替换 `.flow-section`：**Hero/内容与效果/入口区保持当前场景不动**，
  仅流转区淡出→淡入→脉冲。验证（IAB DOM）：autoplay 切换后 `.hero-title` 仍为
  「加油站便利店」、`.node-card.current` 变为「翻找垃圾桶」。
- **② 加载 XML 文件入口**（工具栏「📂 加载 XML」，file picker）——用户要"直接加载
  XML 看效果"。双通道：正式环境优先 POST 语义端点 `/viz/data?type=&xml=`（C# 全量）；
  静态/浏览器独立环境走**页面内提取器** `xml-import.js`（DOMParser + 与 C# 快照同构的
  精简语义：Hero/分支解析（D07 语法）/效果区/入口区；单文件模式无全表 → 前驱恒显示
  ⛳ 入口 + formatHint 明示「前驱/触发器不可用」+ 未解析引用显示灰色 #id）。
  另加 `?xmlurl=` 调试参数（自动加载 XML 文件走 openXml 全链路，headless 验收通道）。
  验证：Edge headless 截图 `?xmlurl=samples/encounter90.xml`——工具栏按钮、Hero、
  分支卡 40%/40%/停留20%、效果区、单文件提示全部正确，无报错。
- **③ 切换失败保留视图**——404/网络错误时不再整页替换为错误，页面主体保留 +
  顶部红色横幅（8s 自动消失，`showErrorBanner`）。

**P0.7 崩溃修复 + 资源生命周期（2026-08-08，用户实机验收反馈）**：
- **闪退根因**：`JsVisualizationHost.BuildView()` 缓存单例视图（`_view ??=`）+ Dock 布局
  重建 `EntityEditorView` 实例（DeferredContentControl）→ 同一 `JsVizView` 被二次 Add
  进新宿主 Grid → `already has a visual parent` 崩溃。修复：**BuildView 每次返回新视图**，
  `LoadEntity` 广播到全部存活视图；`EntityEditorView.EnsureJsVizHost` 加幂等防护
  （`Children.Count > 0` 早退）。
- **资源生命周期**：`JsVizView` 卸载（文档关闭/Dock 回收）即 `ReleaseWebView`
  （释放 NativeWebView 引用 + 清空子元素 + 复位状态，重挂载时重建）；
  `JsVisualizationHost` 在 `LoadEntity` 时清理已分离视图（防列表增长）。
- **初始化反馈**：WebView2 首次初始化较慢（环境启动 ~1s），`EnsureWebView` 先挂
  「正在启动 JS 可视化…」占位再创建，消解"卡住无反馈"。
- **性能结论（用户问「每开一个标签页都部署一个 WebView2 吗」）**：
  `VizContentServer` 是**全局单例**（所有 tab 共享一个回环 HTTP 服务，不重复部署）。
  NativeWebView 部分：P0.7 时为「每实体文档 tab 一个（懒创建 + 关闭即释放）」的
  中间态；**该结论已被 P0.8 取代**——现为**单 WebView2 全局共享**（任意数量文档
  只有一个实例，快关快开零重建，见下）。

**P0.8 单 WebView2 共享（2026-08-08，用户实机反馈「标签页快关快开、打开数量很大」）**：
上文 P2 的「单 WebView2 多文档复用」提前实施为 `SharedJsVizWebView`（浏览器 tab 模型）：
- **架构**：`SharedJsVizWebView`（插件级单例）持有唯一 NativeWebView + 停靠容器
  `_parkingLot`；每个文档的 `JsVizView` 变**轻量壳**（无 native 资源），
  `Loaded`/`Unloaded` = 文档 tab 激活/失活信号（Avalonia TabControl 非选中 tab 不
  attach）→ 共享 WebView **移入**激活壳宿主 / **移回**停靠容器。
- **收益**：① 任意数量文档同时只存在 **1 个 WebView2**（内存恒定 ~几十 MB）；
  ② 快关快开**零重建**——关闭文档只是把 WebView 移回停车场，重开直接复用；
  ③ WebView2 环境初始化（~1s 卡顿）整个应用只发生一次。
- **时序**：实体注入走 `LoadEntity` → `SharedJsVizWebView.LoadEntity`（记录当前实体，
  就绪即 Navigate）；tab 激活 Attach 时若已有实体自动导航；XML 编辑 → Entity 变化 →
  同一通道重导航（R06 同源）。
- 崩溃修复（P0.7）语义保留：BuildView 每次返回新**壳**（无 native，Dock 重建安全）。

**P0.8 修订 v2（2026-08-08，实机验收：共享控件 reparent 输入失效）**：
用户实测「JS 可视化像图片一样无法滚动/交互」——**WebView2 控件在文档间移动
（reparent）后输入通道（鼠标 hit-test/滚轮）不重建**，即使首次创建已在 attach 宿主
也无法修复。结论：**共享必须收敛到环境层而非控件层**。最终架构：
- **每文档一个 NativeWebView**（懒创建：切到「JS 可视化」tab 才建；关闭/Dock 回收
  `ReleaseWebView` 即释放）——WebView 从出生就在 attach 宿主，交互正常
  （与 WebView 插件面板/播放器同款模式）；
- **共享 WebView2 环境**：`VizWebViewEnvironment.Attach` 经
  `NativeWebView.EnvironmentRequested` 事件（Windows 子类
  `WindowsWebView2EnvironmentRequestedEventArgs`，反射设置，非 Windows 静默回退）
  统一 `UserDataFolder=%LocalAppData%/NeoEditor/WebView2Viz` + `ProfileName=neoviz`
  → **所有实例共用单一浏览器进程与缓存**，环境初始化全局仅一次，后续实例创建
  ~百 ms 级；
- 内存峰值 = 同时停留在 JS tab 的文档数 × 单实例（用户场景快关快开、同刻激活
  文档有限，可接受）；进程数恒为 1。
- API 依据：Avalonia.Controls.WebView 12.0.1 `NativeWebView.EnvironmentRequested`
  （42 文档 §2.1 已登记；反射确认事件参数基类含 `EnableDevTools`/`GetDeferral`，
  Windows 子类含 `UserDataFolder`/`ProfileName`/`ExplicitEnvironment`）。
- 遗留：`SharedJsVizWebView.cs` 已删除；若未来需要控件级收敛，可评估
  `ExperimentalOffscreen`（ICoreWebView2CompositionController 离屏合成，避免 airspace
  与 reparent 问题），列为 P2 可选项。

**P0.9-P0.11（2026-08-08，实机验收三轮）**：
- **P0.9 滚轮修复（根因：Avalonia.WebView 普通模式不转发滚轮）**：反编译源码确认
  `NativeWebView.OnPointerWheelChanged` **仅对离屏适配器（IWebViewAdapterWithOffscreenInput）
  调用 PointerWheelInput**（`SendMouseInput(WHEEL)` 完整实现），普通 WebView2 模式直接
  丢弃滚轮（Win32 WM_MOUSEWHEEL 发焦点窗口 → Avalonia 路由 → 空实现吞掉）。先试
  InvokeScript scrollBy 转发未生效（事件路由链问题）→ **启用 ExperimentalOffscreen**
  （EnvironmentRequested 事件设置）→ 滚轮经 CompositionController 注入，实机可用 ✓。
  离屏合成顺带消除 airspace（native HWND 叠加）问题。
- **P0.10 共享 WebView 重做（v3）**：离屏模式下**所有输入（点击/滚轮/键盘）走 Avalonia
  转发**（不依赖 native HWND hit-test）→ 控件 reparent 不再破坏输入通道 →
  `SharedJsVizWebView`（唯一 NativeWebView + 停靠容器）恢复，文档 JS tab 为轻量壳，
  激活移入/失活移回；与离屏 + 环境共享（UserDataFolder/Profile）三合一。快关快开零重建。
- **P0.11 XML 输入改拖拽 + 流转平移动画**（用户三项反馈）：
  ① **去掉「加载 XML」按钮**——XML 输入 = **拖拽**：把 .xml 文件拖进页面即渲染
  （window dragover/drop + file.text() → openXml 全链路，零 C# 改动）；
  ② **流转布局改单轨道**：修正为 D08 R64 结构——**一个横向滚动容器**包三行
  （`.flow-track` 垂直三行，整体横向滚动），不再是三个独立横向滚动；
  ③ **视角平移动画**（用户指定效果）：点击前驱/后继卡 →
  同级场景（当前卡+其他卡）**淡化**（opacity 0.18）→ 轨道 transform 平移让目标卡
  移到当前卡位置（视角跟随）→ 重建轨道（目标快照三行）+ 复位 + 平滑滚动居中 →
  **新当前卡先出现、目标的前驱/后继依次淡入**（错开 delay，从目标向外扩散）+ 脉冲。
  验证（IAB DOM）：flow-scroll=1/track=1/rows=3、切换后当前卡正确、动画标记=1、
  ghost/dim 残留=0（动画干净收尾）。

**P0.12 实机验收修订（2026-08-08）**：
- **修复「切换失败: /viz/data → 404」**：页面点分支卡跳转时，快照里只带数字 targetId，
  而缓存/查找按 **EntityId**（可能带 mod 前缀）→ 必然 miss → 404。修复双管齐下：
  ① `BranchDto.EntityId`（解析成功时携带，页面导航键优先用 EntityId，ID chip 仍显示
  数字 id）；② `VizSnapshotService.FindInLookups` 增强：EntityId 匹配失败后按数字主键
  （Id/nID 反射）兜底。**澄清**：应用内页面 fetch 的 `/viz/data` 就是应用自己起的
  回环服务（VizContentServer，127.0.0.1），不是外网。
- **XML 输入定位修正（用户澄清）**：XML 输入是**开发/AI 调试通道**（直接从游戏目录
  加载真实 XML 看渲染效果），**不是应用功能**。① 移除页面「加载 XML」按钮与拖拽
  （xml-import.js 删除、页面零 XML 入口）；② 新增 `/viz/xmlfile?path=<gameRoot 相对>`
  调试端点（越界 404，v1 仅 encounters 表）→ `BuildFromXml` C# 全量语义 →
  页面 `?file=` 参数直接渲染。调试用法：`viz/index.html?file=data/encounters.xml`。

**P0.13 动画优化 + 通用渲染器（2026-08-08，用户两项反馈）**：
- **① 场景流转动画重设计（原"没啥动画/太快"）**：根因 = fetch 间隙（点击后先等网络
  无反馈）+ 各阶段偏短（总 ~700ms）。新版时序：
  `点击瞬间立即淡化（fetch 并行，消除间隙）→ 淡化 250ms（opacity 0.12）
  → 轨道平移 550ms（目标卡→当前卡位置）→ 重建三行 + 平滑滚动居中
  → 新当前卡 0ms 出现 → 前驱 +200ms → 后继 +350ms（每卡 +120ms 错开，向外扩散）
  + 脉冲 0.9s → 总 ~1.4s`。前置过滤 checkbox 重算不再触发平移动画（无导航语义，
  直接重建）；「回到当前」目标不在视图 → 全部淡化 + 重建淡入。
- **② 页面改通用渲染器**（用户："做成将 xml 或 json 作为传入，然后返回页面的形式"）：
  页面核心 = **`NeoViz.render(xml|json)`**（`window.NeoViz = { render, renderJson,
  renderXml, applySnapshot }`）——输入 XML/JSON 文本，输出渲染页面（纯函数形态）：
  - `renderJson`：完整快照 JSON（与 /viz/data 产出同构）直接渲染；
  - `renderXml`：页面内 DOMParser + xml-import.js 提取语义渲染（单文件模式）；
  - 传入通道：全局 API（宿主 InvokeScript / console / 自动化）、**拖拽 XML/JSON 文件**、
    URL 参数 `?json=` / `?xml=` / `?file=` / `?sample=`；
  - 应用内主通道不变（C# /viz/data 全量语义含全表反查）。
  验证：`?json=` 迷你快照渲染全对（Hero/流转/效果/描述）；autoplay 动画后
  dim/ghost 残留=0、无错误横幅、切换成功。
- **回归修复**：main() 重构时 sample 分支误走 `applySnapshot`（把 `state.sample=null`
  清掉 → 切换 fetch /viz/data → 404）——sample 模式改为独立渲染保留 state.sample。

## 七、实现分期（v1.1：Encounter 优先）

| 阶段 | 内容 | 验收 |
|---|---|---|
| **P0 骨架 + Encounter**（本会话） | 插件 + `IEntityJsVisualizationHost`（UI.Common）+ EntityEditorView 第三 Tab（无实现隐藏）+ `VizContentServer`（静态页/`/viz/data` 快照/`/viz/assets`/`/viz/action`）+ `EntitySnapshotDto` + **Encounter 全量语义提取**（Hero/流转三行/NodeCard/终止胶囊/效果区/入口区）+ JS 页面（组件库 + Encounter 渲染器 + 焦点切换/回到当前/前置过滤/导航桥）+ samples + **每文档 WebView2 + 环境共享**（P0.8 v2） | 构建通过；xUnit 40 项（纯函数/合并归一/终止语义/前驱反查/效果/入口/action 桥/快照契约/sample 生成）；浏览器截图对照 D08 布局 |
| **P1 组件库与其余类型迁移** | 战利品嵌套树 + Creature/ItemType/Recipe 渲染器 + 图片资产完善 + hover tooltip 全量 | 四类实体 samples 截图逐项对照 D04-D08 |
| **P2 交互桥完善** | postMessage 增强通道 + 反向引用面板 + peek 细节 + 选中同步（R12）联调 | 浏览器与 WebView2 行为一致 |
| **P3 AI 验证工具链** | `samples/` 测试资产 + 验收脚本（静态服务/截图清单）+ XML debug 入口打磨 | 「喂 XML → 截图 → 识别」全链路 |
| **P4 收尾** | 剩余 21 类型通用渲染器 + 本地化（en/zh，`Jsv.*` 键）+ 性能 + **下线 Avalonia visualizer**（v1.1 决策：验收无 bug 即下线，不留双轨） | 全量 24 类型可渲染；`IEntityVisualizer` 回归路径移除 |

## 八、风险与边界

1. **R17/R18 边界**：JsVisualization 引用 Player.Core 属 42 §3.8"子应用拆分"批准的例外
   （WebView 插件同款）；不引用任何其他 Plugin；EntityEditor 只依赖 UI.Common 新接口。
   若审查要求收紧，替代方案为把 `VizContentServer` 下沉 Player.Core/Infra（P4 前不阻塞）。
2. **WebView2 不可用**：`BuildView()` 返回 null → Tab 隐藏，现有 Avalonia 可视化不受影响
   （双轨的价值）。非 Windows 平台整体降级与 WebView 插件一致（错误 TextBlock）。
3. **JSON 传输坑**：页面侧一律 fetch JSON（不走 InvokeScript 传大 JSON，避开 42
   §v2.36-2.44 记录的双重编码陷阱）；快照含 rawXml 时体积大（Encounter 可达数十 KB），
   端点支持 `?fields=min`（语义字段优先、rawXml 可选）做懒加载。
4. **语义双源**：迁移期同一实体有两份语义逻辑（C# visualizer 内 + 快照 DTO），
   以**快照 DTO 为准**（xUnit 覆盖），Avalonia visualizer 冻结不新增语义 —— 双轨
   过渡的标准写进 P4 评估。
5. **性能**：单页单实体渲染（v1 不做多实体图谱）；NodeCard 大量引用（Encounter
   2264 节点）时懒渲染 + IntersectionObserver，默认只渲染前后一层（D08 v1.3 语义）。
6. **安全**：回环端口只绑定 127.0.0.1；`/viz/assets` 路径越界 404（沿 GameContentServer
   模式）；action 协议白名单（kind 枚举），宿主侧校验 entityType/entityId。

## 九、测试与验收

- **xUnit（R21，`Tests/NeoEditor.Plugins.JsVisualization.Tests`）**：
  `EntitySnapshotDto` 序列化往返（含 rawXml、中文、特殊字符）、`/viz/data` 两种输入
  （id / xml）产出一致快照、`/viz/action` Handler 四类 kind 路由与参数校验、
  `VizContentServer` 路由与越界 404。
- **浏览器验收（§六闭环）**：samples 截图清单逐项对照 D04-D08；XML debug 入口冒烟。
- **回归**：现有 861 测试不动；EntityEditor 侧只加"无 IEntityJsVisualizationHost 时
  Tab 隐藏"一条测试。

---

> **v1.1 定稿**（2026-08-08 用户拍板）：① 语义契约先做 **Encounter**（最复杂也最完善）
> 作为模板再铺开；② P4 验收后 **Avalonia visualizer 下线**（不留双轨）；
> ③ 命名/资源键沿用：插件 `NeoEditor.Plugins.JsVisualization` + `Jsv.*`。
> 本文档 v1.1 起进入实施；**v1.2**（2026-08-08）追加 P0.5-P0.8 实施记录
> （动画/局部重渲染/XML 导入/崩溃修复/单 WebView2 共享），并订正 P0.7 性能段落
> 中已被 P0.8 取代的「每文档一 WebView2」旧架构描述。
