# D09 — JS 可视化：center 可视化 UI 的 WebView2 + JS 渲染重构

> 设计文档 · 2026-08-08 · v1.5（实施推进：P0 → P0.14 全部落地，**P1 全部落地**）
> v1.1 = 用户拍板：① **Encounter 优先**（最复杂也最完善，语义契约以它为模板铺开）；
> ② P4 验收无 bug 后**下线 Avalonia visualizer**（不再长期双轨）；
> ③ 命名沿用（插件 `NeoEditor.Plugins.JsVisualization`、资源键 `Jsv.*`）。
> v1.2 = 实施记录追加（P0.5 焦点切换动画 / P0.6 局部重渲染 + XML 导入 / P0.7 崩溃修复 /
> P0.8 共享架构两轮修订：v2 每文档 WebView + 环境共享 → v3 共享控件（离屏））+
> 订正 P0.7 性能段落中已被取代的旧架构描述。
> v1.3 = [D10](D10-js-visualization-upgrade-design.md)（AF4 审查与组件升级设计思路）发布：
> §四"原样继承"指 **D04-D08 语义规格**；组件层按 D10 统一模板与 9 项改进执行（Section 单轨/
> 导航历史/状态记忆/Raw Data 移底部/RefPanel 聚合/薄类型分级），随 P1-P4 分期吸收。
> v1.4 = 实机验收四轮修订汇总（P0.9 滚轮修复 / P0.10 共享 v3 / P0.11 单轨道+平移动画 /
> P0.12 404 修复+XML 调试通道 / P0.13 动画重设计+通用渲染器 / P0.14 **JS 驱动动画 +
> 共享 v4（Detach 销毁）**）——动画与 WebView 生命周期两处最终定稿（见 P0.13/P0.14）。
> v1.5 = **P1 实施记录**（2026-08-09，D10 组件升级 + 类型铺开，见 §十）：
> 组件库拆 components.js（IIFE 作用域隔离——顶层解构与全局函数名冲突的坑）+ renderers.js
> 渲染器注册表；ItemType/Creature/Recipe 三个语义提取器（D04/D05 纯数据移植）+
> C 级薄类型模板（ContainerType 引用聚合/BarterHex 补货/Map 规格摘要）；战利品嵌套树
> LootTreeBuilder（Encounter 效果区一并接入）；TopBar 审计统计（N·M·K，与 Avalonia
> RawData 折叠头同口径）；RefPanel 静态版（类型分组聚合，P2 补过滤/滚动加载）；
> §3.7 [data-nav] hover + ↗ 角标。测试 77 项（新增 45）；Edge headless 截图 +
> vision 识别对照 D04/D05 全过（含 Encounter autoplay 动画回归）。
> v1.6 = **P2 实施记录**（2026-08-09，D10 §3.1/§3.2/§3.6 + D09 P2 交互桥，见 §十一）：
> 组件内导航历史（← 返回 + 快照缓存）、状态记忆（展开/滚动 sessionStorage）、RefPanel
> 过滤 + 滚动加载、postMessage 增强通道（POST 为主、桥兜底，协议唯一同一 Handler）。
> v1.7 = **P4 全类型铺开**（2026-08-09，D10 §四 24 类型全覆盖，见 §十二）：
> `TemplateSemanticsExtractor` 把剩余 17 类型全部接入——B 级 7 个（AttackMode/Condition/
> TreasureTable/HexType/Faction/BattleMove/CampType，语义迁移）与 D 级 10 个（GameVar/
> ItemProp/Headline/ForbiddenHex/ChargeProfile/Ingredient/DmcPlace/CreatureSource/
> EncounterTrigger/DataFile，反射字段表 + 特化）；StatBarDto bipolar 双向条；JS 侧
> renderTemplate 扩展（bars/mode/badgeGroups），17 类型注册到模板渲染器（零 per-type）。
> **24 类型全部可渲染**，无"未实现"兜底。
> 实施状态：插件 + 扩展点 + VizContentServer + 快照契约 + **24 类型语义全覆盖**
> （A 级 3 + B 级 7 + C 级 3 + D 级 10）+ JS 页面（组件库 + 渲染器注册表 + 通用渲染器
> NeoViz + JS 驱动动画 + P2 导航历史/状态记忆/RefPanel 交互）+ 共享 WebView2 v4
> + 测试 91 项，构建通过、全量回归通过。
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
| 10 | 生命周期 | **共享 WebView2 v4（P0.14 定稿）**：每次激活的文档 JS tab 一个 NativeWebView（懒创建），**文档失活即销毁、激活时重建**（不做控件 reparent——实测残留状态"一会行一会不行"）；所有实例共享 UserDataFolder/Profile（单一浏览器进程）+ 离屏合成（滚轮/输入走 Avalonia 转发） | 演变：初版「Tab 首次激活才建 WebView」→ 单控件共享（reparent 输入失效）→ v2 每文档+环境共享 → v3 共享控件（离屏下 reparent 恢复，但状态残留"一会行一会不行"）→ **v4：Detach 销毁 + 环境共享定稿**（重建 ~百 ms，快关快开无感） |

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
│   （切到 JS tab 才创建；文档失活即销毁、激活重建——v4，无 reparent）      │
└────────────────────────────────┬───────────────────────────────────┘
                                 │ EnvironmentRequested 事件统一配置
┌────────────────────────────────▼───────────────────────────────────┐
│ VizWebViewEnvironment（静态）—— 共享 WebView2 环境（v4 共享层）       │
│  · UserDataFolder = %LocalAppData%/NeoEditor/WebView2Viz           │
│  · ProfileName = "neoviz" → 全部实例共用**单一浏览器进程**与缓存      │
│  · ExperimentalOffscreen = true（离屏合成：滚轮/输入走 Avalonia 转发）│
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
  **⚠️ 该版已被 P0.14 v4 取代**：reparent 实机出现"一会行一会不行"（状态残留）→
  最终改为 Detach 销毁 + Attach 重建（见 P0.14 ②）。
- **P0.11 XML 输入改拖拽 + 流转平移动画**（用户三项反馈）：
  ① **去掉「加载 XML」按钮**——XML 输入 = **拖拽**：把 .xml 文件拖进页面即渲染
  （window dragover/drop + file.text() → openXml 全链路，零 C# 改动）；
  **⚠️ 拖拽入口后续两次修订**：P0.12 按用户澄清移除（XML 输入定位为开发调试通道，
  非应用功能）→ P0.13 以**通用渲染器**形式恢复（`NeoViz.render` API + 拖拽作为
  输入通道之一，页面能力而非应用 UI）。
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
（时序设计即 P0.14 定稿版；实现于 P0.14 起改为 **JS 驱动插值**——CSS transition 在
离屏 WebView2 合成器不播放，见 P0.14 ①）
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

**P0.14 动画与 WebView 生命周期最终定稿（2026-08-08，实机"浏览器正常、编辑器没有"）**：
- **① 动画改 JS 驱动插值**（根因：**离屏模式 WebView2 合成器不播放 CSS transition**——
  浏览器正常（抓到中间帧 opacity=0.415），编辑器瞬间跳变）。方案：
  `animateJs(duration, onFrame)`——`setTimeout` 16ms 步进 + 内联 style 插值
  （不依赖合成器，任何环境强制播放）：淡化（点击瞬间直接压到 0.12，fetch 并行）→
  平移（transform 插值 550ms，easeInOut）→ 重建 → 新卡依次淡入
  （当前 0ms / 前驱 +200ms / 后继 +350ms，每卡 +120ms）。CSS transition 全部移除
  （仅保留脉冲 keyframes——animation 在离屏下正常）。防重入锁（连点时忽略并发
  load）+ fetch 失败恢复透明度。
- **② 共享 WebView 定稿 v4（Detach 销毁）**：v3（唯一控件 + reparent 移入/移出
  停靠容器）实机出现"**一会行一会不行**"——reparent 残留输入/渲染状态。最终：
  **文档失活即销毁 NativeWebView，激活时重建**（不做任何控件移动）；共享收敛在
  **环境层**：UserDataFolder/ProfileName 统一（单一浏览器进程 + 缓存）+
  ExperimentalOffscreen（离屏：滚轮/输入走 Avalonia 转发 + 无 airspace）。
  重建成本 ~百 ms（环境已就绪），快关快开场景不受影响。
- **③ 页面版本标记**：右下角小字 `v20260808-2120`（`VIZ_VERSION` 常量，点击显示
  完整版本）——编辑器加载页面版本可一眼确认（排查"是不是旧构建"）。
- **④ 构建产物排查**：`bin/Release` 曾缺 `Web/viz/`（旧构建输出）——Debug/Release
  均已重新构建同步；`artifacts/` 下历史验证 exe 均无 viz 资源（非日常运行入口）。
  用户实机确认全 Debug 运行 → 排除构建覆盖，问题锁定运行时行为。

## 七、实现分期（v1.1：Encounter 优先）

| 阶段 | 内容 | 验收 |
|---|---|---|
| **P0 骨架 + Encounter**（本会话） | 插件 + `IEntityJsVisualizationHost`（UI.Common）+ EntityEditorView 第三 Tab（无实现隐藏）+ `VizContentServer`（静态页/`/viz/data` 快照/`/viz/assets`/`/viz/action`/`/viz/xmlfile`）+ `EntitySnapshotDto` + **Encounter 全量语义提取**（Hero/流转三行/NodeCard/终止胶囊/效果区/入口区）+ JS 页面（组件库 + Encounter 渲染器 + **通用渲染器 NeoViz** + 焦点切换平移动画 + 回到当前/前置过滤/导航桥）+ samples + **共享 WebView2 v4**（环境共享 + Detach 销毁，P0.14 定稿） | 构建通过；xUnit 41 项（纯函数/合并归一/终止语义/前驱反查/效果/入口/action 桥/快照契约/sample 生成/EntityId 导航）；浏览器截图 + IAB DOM 对照 D08 布局 |
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

## 十、P1 实施记录（2026-08-09，D09 P1 + D10 组件升级吸收）

**C# 语义层**（`Services/`，全部纯数据移植自 Avalonia visualizer，语义以快照 DTO 为准）：
- `SemanticsShared`：条件语义色（Fatal/Instant/Stackable/时长 + 后缀）、攻击模式行/展开详情
  （D04 BuildAttackModeRow/Expanded 纯数据版）、`Raw` 引用列读取（解析条目优先、RawText-only
  桩回退——`ToRawString` 只走解析条目的坑）、反向引用聚合摘要（静态版）、TopBar 审计统计
  （与 Avalonia RawData 折叠头同口径：N 字段 · M 有值 · K 未解析）；
- `LootTreeBuilder`：战利品嵌套树（`物品x权重x数量` → 表内权重 Σ 归一，嵌套 TT 递归
  depth ≤ 3，嵌套概率独立归一，未解析灰色兜底）——Encounter 效果区战利品行一并接入
  （`EffectRowDto.Trees`）；
- `ItemTypeSemanticsExtractor`（D04 全量：Hero G.S/辨识/关键数字/画廊、⚔ 战斗三层、
  🧍 装备、✨ 效果三组条件、⏳ 生命周期耐久/寿命推演/破损产物、📦 容器、🔗 来源产出）、
  `CreatureSemanticsExtractor`（D05 全量：Hero 徽章、⚔ 三层+阵营关系/空手去噪、🧬 属性/
  出场状态概率/Activities≤30、🎁 双池、📍 遭遇三侧+刷新点权重归一）、
  `RecipeSemanticsExtractor`（原料三组 + Required/Forbidden、产物树、Temp/AlsoTry/Hidden）、
  `ThinSemanticsExtractor`（C 级 3 个：ContainerType 按属性分组引用聚合、BarterHex 买卖/
  补货、Map N cells/定义截断）；
- `VizSnapshotService` 类型分发 switch + 快照级 `Image`（Encounter/Creature 图列、ItemType
  首图、Map 名即图）+ `Audit`。

**JS 页面**（`Web/viz/`，无构建链多 script）：
- `components.js` 组件库（D10 §二统一模板）：`Section`（图标+色条+标题+计数+右侧操作区，
  §3.4 单轨）、`Hero`（图 132px | ID/类型/旗标行 | 名称 | 副文本/数字行）、`ValueGrid`、
  `StatBar`（stacked/centered）、`Badge` 增强（`[data-nav]` hover 描边+抬升 + `↗` 角标，
  §3.7）、`LootTree`、`TopBar`（类型名 + 审计统计，← 返回 P2）、`RefPanel`（聚合摘要 +
  类型分组 + 前 N 徽章 + more）、`Details`（Raw XML 底部，§3.3）；
- `renderers.js` 渲染器注册表：ItemType（D04 三对两列）/ Creature（D05 两对两列）/
  Recipe / 薄类型模板（§3.8 零 per-type 渲染器，C 级直接组合）；
- `app.js`：主渲染分发（TopBar → renderer → 底部），Encounter 渲染器迁移统一组件
  （流转区局部重渲染锚点保留，P0.6 语义不变），NeoViz API / 动画 / 拖拽原样保留；
- **实施坑记录**：① 组件函数全局声明与渲染器 `const { el }` 顶层解构冲突
  （`Identifier 'el' has already been declared`）→ components.js/renderers.js/app.js
  全部 IIFE 作用域隔离，交互桥 postAction 经 `window.VizActions` 路由；
  ② `ConditionChipDto.label` vs `BadgeDto.text` 字段不一致 → `badge()` 兼容回退；
  ③ `ReferenceList.ToRawString` 只走解析条目，RawText-only 构造返回空 → `SemanticsShared.Raw`
  统一回退。

**验收**（§六 AI 闭环，Edge headless + deepseek-vision）：
- samples 扩至 9 个：encounter90/41 + itemtype52（完整语义）+ creature101 + recipe1 +
  containertype3 + barterhex1 + map1（全部走与 /viz/data 同一提取管线，真实键值本地化）；
- DOM 验证：各类型区块/徽章/树/审计/引用面板全出；Encounter autoplay 动画回归
  （`data-flow-animated=1`、当前卡切至前驱、Hero 不动、无错误横幅）；
- 截图逐区块对照 D04/D05：ItemType 六区块 + 条件语义色 + 耐久条 + 破损产物树、
  Creature 战斗三层 + 刷新点权重归一 + 双池树、Recipe 原料卡 + 必需徽章、Encounter
  效果区战利品树（概率 33.3%/66.7%）——无 undefined/错位/溢出；
- 测试 77 项（新增 45：提取器 5 类 + LootTreeBuilder + SemanticsShared + 分发 + 样本生成）。

**遗留（P2 吸收项）**：§3.1 组件内导航历史（← 返回）、§3.2 状态记忆（sessionStorage）、
§3.6 RefPanel 过滤框 + 滚动加载；postMessage 增强通道；选中同步（R12）联调。

## 十一、P2 实施记录（2026-08-09，D09 P2 + D10 §3.1/§3.2/§3.6 吸收）

**导航历史（D10 §3.1 组件内）**：
- `state.navStack`（来源 id 栈，连续相同 id 去重）+ `state.snapshotCache`（id → 快照，
  返回**不重新 fetch**，缓存优先——`fetchSnapshot` 改造）；组件内焦点切换与「回到当前」
  均入栈；TopBar「← 返回」逐级回退（无动画直接重建流转区）；
- 局部重渲染与 TopBar 解耦：流转切换只重建流转区（P0.6 语义），返回按钮由
  `updateTopBar()` 同步（渲染层不感知导航栈）；
- 调试通道：`?autoback=N` 自动点击返回 N 次（headless 验收，同 autoplay 模式）。

**状态记忆（D10 §3.2）**：
- `sessionStorage['jsv:ui:{type}:{rootId}']` = `{scrollY, expanded[]}`——键锚定**文档实体**
  （rootId），流转焦点切换属文档内导航不换键；scroll debounce 500ms 存储；
- 统一展开协议：可展开元素带 `data-expand-key`，`.open` 类控制显示（CSS 侧），
  `bindExpand`/`restoreExpands` 读写状态——攻击模式行、战利品树 TT 行、Raw XML details
  全部接入；`<details>` 的原生开关在 summary 上（合成点击需点 summary）；
- 调试通道：`?autotoggle=key1,key2` 自动点击展开元素（headless 验收）。

**RefPanel（D10 §3.6）**：
- 过滤框（名称/id 前缀即时过滤，组计数联动）+ IntersectionObserver 滚动加载
  （首批 20 + 哨兵补批，rootMargin 200px）；C# `BuildRefSummary` cap 8 → 100
  （过滤/懒渲染的数据基础，快照体积可控）。

**postMessage 增强通道（D09 §五/P2）**：
- C# `SharedJsVizWebView` 挂 `WebMessageReceived` → `VizActionHandler`（与 /viz/action
  POST **同一协议、同一 Handler**——"双向可选、协议唯一"）；
- 页面 `postAction`：POST 为主（决策 8 零宿主依赖），fetch 失败回退
  `chrome.webview.postMessage`；浏览器环境无桥自然回退 HTTP。

**验收**（Edge headless + dump-dom/截图 + vision）：
- 导航历史：autoplay 切至前驱 → TopBar 返回按钮出现；autoback=1 回退至根场景 90、
  按钮消失、无错误横幅；快照缓存命中（无重复 fetch）；
- 状态记忆：隔离页验证保存（点击 → sessionStorage 写入 `{expanded:["am:X"]}`）与恢复
  （restoreExpands → `.open`）闭环（sessionStorage 会话内持久化，跨进程不持久——
  符合 D10 设计意图）；真实页面 `autotoggle` 展开 `am:R-Hand: 劈砍` 成功；
- RefPanel：45 条合成数据首批 20 + 哨兵存在；过滤 "猎刀3" → 11 条即时生效、计数联动；
  真实页面过滤框渲染正常；
- 回归：Encounter autoplay 动画、P1 六类型页面截图 vision 复核无 undefined/错位；
  全量测试通过（77 JsVisualization + 全解决方案）。

**遗留（P4 吸收项）**：§3.1 文档级 back（宿主导航历史，可选）、§3.9 sticky 段头、
§四 D 级模板（剩余 21 类型）、§3.4 全量核查、下线 Avalonia visualizer。

## 十二、P4 全类型铺开实施记录（2026-08-09，D10 §四 24 类型全覆盖）

**背景**：用户反馈"24 个类的 JS 可视化看不全，AttackMode 显示未实现"——P1 只铺了 7 类，
剩余 17 类走"渲染器未实现"兜底。本轮把 **D10 §四 B 级 7 + D 级 10 全部接入**，24 类型
全覆盖，无兜底路径。

**C#（`TemplateSemanticsExtractor.cs`，全部输出 TemplateSemantics，JS 零 per-type）**：
- **B 级 7 个**（语义原样迁移，区块 Section 化）：
  - `ExtractAttackMode`：复用 `SemanticsShared.BuildAttackMode`（AttackModeDto 全套）——
    战斗区块 `Mode` 字段渲染单模式行+展开详情；近战/远程类型徽章、WieldPhrase 副文本、
    弹药/攻击者条件/攻击短语区块；
  - `ExtractCondition`：严重度徽章（FATAL/Instant/时长）、属性键值表、**效果区块**
    （`ConditionFieldTranslations` 中文字典 + 带符号值 + **bipolar 双向条**）、Effects 原文、
    状态链（IdNext 徽章 + ChanceNext）；
  - `ExtractTreasureTable`：Nested/Suppress/Identify 旗标 + 战利品树（LootTreeBuilder）；
  - `ExtractHexType`：Passable/Blocked 徽章、地形移动表（移动消耗/净能见度/营地物资映射）、
    6 时段光照热力、引用分组（搜刮战利品/条件/默认营地，哨兵 3/25 跳过）；
  - `ExtractFaction`：**外交关系 bipolar 条**（DictFactions → 名称 + 声望分级 同盟/友好/
    中立/敌对/仇敌，按值升序）、成员（ReverseLookup Creature.Faction）；
  - `ExtractBattleMove`：类型徽章（攻击类型·大类 + flags）、决策属性（Chance/Detect/
    Fatigue/Order bipolar + 射程/暴露/MinCharges）、PopUp/Success/Fail 文本、
    **8 组条件**（Pre 橙/双方粉蓝/Fail 灰 + NOT 否定红）；
  - `ExtractCampType`：Capacities 徽章、营地属性 5 条 bipolar、营地物资树（"3" 哨兵跳过）；
- **D 级 10 个**（模板组合保持薄）：通用反射字段表（FieldDescriptions 中文列名 + 原始值，
  空值不渲染）+ 类型特化——GameVar（Type 蓝 + Value 绿大字）、Headline（N chars + 正文）、
  ForbiddenHex（Forbidden 红 + 坐标）、ChargeProfile（Degrade ⚠ + 消耗率表 + ItemId 徽章）、
  Ingredient（Required/Forbid 属性分组）、DmcPlace（坐标 + 剧情徽章）、CreatureSource
  （坐标·数量 + 同点权重占比）、EncounterTrigger（类型徽章 + Chance + Area/DateRange +
  剧情/格类型）、DataFile（$Value + 内容）；ItemProp 纯通用。

**DTO/JS**：
- `StatBarDto` 加 `NegativeColor` + `Mode="bipolar"`（零中心双向条：正右负左，负色
  #C62828 兜底）；`TemplateBlockDto` 加 `Bars` / `Mode`（AttackModeDto）/ `BadgeGroups`；
- `statBar()` 实现 bipolar 渲染（|v|/max × 50%，负值 marginLeft:auto）；`renderTemplate`
  扩展渲染 Bars/Mode/BadgeGroups；渲染器注册表补 17 个类型 → renderTemplate。

**验收**：
- xUnit 91 项（新增 14：TemplateSemanticsExtractorTests 11 + 分发 2 + 样本 1）；
  样本扩至 18 个（+9 B/D 代表类型）；
- Edge headless dump-dom：9 个 B/D 样本全部渲染（hero 标题正确、无 not-implemented/
  加载失败）；condition5 内容抽查（FATAL/中文翻译/双向条）、battlemove1（决策属性 +
  NOT 饱食 否定组）；
- vision 截图验收：condition5（FATAL 红徽章/血液总量 (m_fBloodLeft) 翻译/双向条
  绿减红增）、faction2（中立 -30 敌对 / 玩家 +100 同盟 双向条）；
- 全量测试通过（App 构建受运行中编辑器实例 DLL 锁影响，重启后生效）。

**遗留**：§3.9 sticky 段头（>3 区块页面）、§3.1 文档级 back（可选）、§3.4 全量核查
（Avalonia 侧双轨语义冻结）、下线 Avalonia visualizer（用户验收无 bug 后）。

## 十三、图片加载修复记录（2026-08-09，用户反馈"JS 可视化经常找不到图片"）

**根因**：JS 侧 `SemanticsShared.ImageUrl` 直接 `_findImage(raw)`，而
`ImageService.FindImage` 用 `Directory.GetFiles(dir, name, AllDirectories)` **精确全名
匹配**——Avalonia `LoadImage` 的三个兜底在 JS 侧缺失：
1. **NSE: 前缀**未去除（`StripNs`）；
2. **子目录引用**（`img/scenario/x.png`）——GetFiles 的 searchPattern 含路径分隔符在
   Windows 上必然匹配不到，需退化为纯文件名搜索；
3. **无扩展名引用**（游戏数据常见，如 `img/creature/dog`）不补 `.png` 必 miss。

**修复**：`SemanticsShared.ImageUrl` 静态化并实现 Avalonia LoadImage 同款候选链
（StripNs → 子目录退纯文件名 → 无扩展补 .png）；`EncounterSemanticsExtractor.ImageUrl`
统一走同一逻辑；`VizSnapshotService.FindImage` 补 CampType/DmcPlace/DataFile 快照根图
（P4 铺开时漏接）。测试 +4（NSE/子目录/补 png/未找到），95 项全绿。

## 十四、真实数据验证 + 收尾修复 + 页签默认调整（2026-08-11）

### 14.1 图片链路：用真实 game.db 逐一裁决（结论：解析层无差异）

用户反馈"依旧 Avalonia 有图、JS 没图"并强调游戏机制（getmods.php → mod 路径、
getimages.php → 图片字典、图片在 mod 路径 + img 下）。编写验证程序直连真实
`game.db`（8.5MB，7 张带图表，8451 行 / 9560 个图片引用）对照两套逻辑：

| 验证项 | 方法 | 结果 |
|---|---|---|
| JS `ImageUrl` vs Avalonia `LoadImage` 候选集 | 对每个引用跑两边解析 | 各命中 9559/9560，**0 差异**（唯一 miss 为磁盘真缺 `ItmEncBlock.png`，两侧一致） |
| 快照 `ToRawString` 往返 | 真实 `ReferenceListSerializer.Deserialize` → `ToRawString` vs 库中原始值 | 8451 行 **0 失配**，0 行解析为空 |
| `/viz/assets` 端点 | 绝对路径 + `File.Exists` + no-store 头 | 正确（此链路上轮已真实出图验证） |

**结论**：JS 与 Avalonia 走同一 `IImageService.FindImage` 委托 + 同款候选链，解析层
不可能出现"一边有图一边没图"。真实数据证明 99.99% 引用可解析；剩余个例（如
`ItmEncBlock.png`）是磁盘缺文件，两边都无图（属预期）。

### 14.2 本轮修复的两个真实 bug

1. **`profile_edits` 表缺列**（用户日志 `SQLite Error 1: 'no such column: p.EntityType'`）：
   建表 DDL 与 `ProfileEdit` 模型不同步——模型新增 `EntityType`/`ModId`（Docs/41 追修
   的 IsNew 重建需要）但 CREATE TABLE 未含两列，`CREATE TABLE IF NOT EXISTS` 不改旧表
   → 打开 profile 时 EF 查询必炸，profile 编辑覆盖层加载失败。修复：DDL 补两列 +
   `AddColumnIfMissing` 迁移（与 pending_export/profile_info 同模式），在真实 editor.db
   副本上验证列已加、EF 形状查询通过。
2. **AttackMode 快照 hero 图缺失**：`VizSnapshotService.FindImage` switch 漏接
   AttackMode（`strIMG`，attackmodes 表 300+ 行可解析）——Avalonia 可视化器显示 132px
   hero 图、JS 模板页只有模式行小图标。修复：switch 补 AttackMode，快照级统一改用
   `SemanticsShared.Raw` 兜底（RawText-only 列表不再丢值）；新增回归测试
   `BuildById_AttackMode_ImageField_IsVizAssetUrl`（97/97 绿）。

### 14.3 页签默认调整（用户拍板：JS 可视化默认，原可视化放最后）

`EntityEditorView` 页签序由「可视化(原) → XML → JS 可视化」调整为
**「JS 可视化(默认) → XML → 可视化(原,最后)」**：

- XAML：`JsVizTabItem` 移至 `TabControl.Items` 首位，`VisualTabItem` 移至末位；
- `SelectDefaultTab()`：JS 页签可见（插件可用）时默认选中，否则回退原可视化——
  替换原 `EditorTabs.SelectedIndex = 0`（对隐藏首项置 0 会显示空白内容）；
- `EnsureJsVizHost()` 挂载成功后显式 `SelectedItem = JsVizTabItem`，首次打开实体即
  落在 JS 可视化；`OnDataContextChanged` 先挂载再选默认页签，避免首帧空内容。

---

## 十五、页面内容订正（2026-08-11，用户反馈 5 项）

1. **HexType 光照等级与 Avalonia 不匹配**：原实现是"数值行 + statBar"，用户指出设计应为
   **从早到晚 6 时段横排、数值 + 色块**。改为 `LightCellDto` 热力格（`TemplateBlockDto.LightCells`）：
   与 Avalonia `BuildLightPanel` 完全同款——Dawn/Morning/Noon/Afternoon/Dusk/Midnight 六列并排，
   时段名在上、热力色块内数值，红(0)→黄(0.5)→绿(1.0+) 同公式插值（r=198→46、g=0→125→0、
   b=40→0），ratio>0.5 白字。JS 新增 `lightGrid` 组件 + `.light-grid` CSS。测试断言改为
   逐格校验热力色（`#A73218`/`#2E7D00` 白字/`#7AFA00`）。
2. **CreatureSource/EncounterTrigger 的"值域"**：根因是字段表标签直接用了
   `field_descriptions.json` 的长描述——描述自带 `实测值域：…（共 N 种）` 多行文本，
   又长又随数据漂移。**全局移除**：`BuildFieldTable` 标签改为模型 `[Display(Name)]`
   短字段名（与合并视图列名一致，如 `Name`/`Chance`/`LocBased`/`Min`/`Max`/`Weight`），
   不再引用 FieldDescriptions。测试断言：标签无"值域"/换行。
3. 同 2（值域全局不写）。
4. **字段名用字段描述太长**：同 2 根因，一并修复（短字段名）。
5. **ItemType 加载失败 `Cannot read properties of null (reading 'totalBar')`**：无攻击模式的
   物品 `combat` 为 null，`combatSection(sem.combat)` 内 `combat.totalBar` 崩溃 → 整页错误横幅。
   修复：`combatSection` 开头 `if (!combat) return null`。用构造的 `combat:null` 样本 headless
   验证不再报错、页面正常渲染。

**验收**：Edge headless + vision 三页确认——hextype1 六色块热力格（Dawn 深红 0.2 / Morning
深绿 1.0 白字 / Noon 黄 0.5 / Afternoon 棕 0.3 / Dusk·Midnight 灰 ?）、encountertrigger1 字段表
全短名无"值域"、itemtype52 正常渲染。测试 97/97（+光照格断言重写、+短字段名断言）；全量
14 项目全绿。

---

> **v1.1 定稿**（2026-08-08 用户拍板）：① 语义契约先做 **Encounter**（最复杂也最完善）
> 作为模板再铺开；② P4 验收后 **Avalonia visualizer 下线**（不留双轨）；
> ③ 命名/资源键沿用：插件 `NeoEditor.Plugins.JsVisualization` + `Jsv.*`。
> 本文档 v1.1 起进入实施；**v1.2**（2026-08-08）追加 P0.5-P0.8 实施记录
> （动画/局部重渲染/XML 导入/崩溃修复/单 WebView2 共享），并订正 P0.7 性能段落
> 中已被 P0.8 取代的「每文档一 WebView2」旧架构描述。
