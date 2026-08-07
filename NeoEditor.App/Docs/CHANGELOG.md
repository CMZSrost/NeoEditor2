# NeoEditor Changelog

---

---

## R60：发布线拆分（player-v*/editor-v* 独立 workflow）+ 内测包 1.0.0 | 2026-08-08

**反馈**（用户）：① 推送脚本应该单独两条发布线——Player 发包时 release 只发播放器包，不混发；② 重新发 1.0.0 只发 player。

**改动**：
1. **发布线拆分**：删掉混发 release.yml（v* 触发编辑器+播放器）→ 两个独立 workflow——`release-player.yml`（`player-v*` tag → 只发 `NeoScavengerPlayer-{版本}-win-x64.zip`，zip 名去前缀）+ `release-editor.yml`（`editor-v*` tag → 只发编辑器）；播放器 Release body 带使用/反馈说明；CI 打包复制 `NeoEditor.Player/README.txt` 进 zip
2. **v1.0.0 内测包**：csproj 版本 0.9.0 → 1.0.0；`player-v1.0.0` tag 已推送（CI 构建中）；本地包 `NeoScavengerPlayer-1.0.0-win-x64.zip`（53MB，含 README.txt）
3. **发布隐患修复**：dist/（单文件 exe 108MB）曾误入 git → 重写历史排除 + .gitignore（`dist/`、`*.zip`）
4. **42 文档 §八**：发布方式改两条线说明 + 校验清单/版本号段更新（1.0.0）

---

## R61：SmartScreen 解除锁定指引（未签名 exe 内测提示）| 2026-08-08

**反馈**（用户）：发送的包显示作者未知、被 Windows 阻止。

**说明**：未签名 exe（无 Authenticode 证书）+ 网络下载 MOTW → SmartScreen「发布者未知/已保护你的电脑」正常提示。

**处理**：README.txt 加解除锁定指引（zip/exe 属性 → 解除锁定；或「更多信息 → 仍要运行」）；本地 zip 重打 + 重新触发 CI。代码签名（Azure Trusted Signing / 商业证书）列入后续计划——Azure Trusted Signing 免费额度 + GitHub Actions 官方 action，Publisher 显示微软签名、SmartScreen 基本放行。

---

## R59：Encounter 剧情分支重构（D06 v1.1：节点单组件 + tooltip 信息卡 + Mermaid 同源）| 2026-08-08## R59：Encounter 剧情分支重构（D06 v1.1：节点单组件 + tooltip 信息卡 + Mermaid 同源）| 2026-08-08

**反馈**（用户）：① 多个组件定位重复没有区别（反向引用三处渲染、响应数据双渲染）；② Mermaid 信息量与图形变化很大（两套独立生成）；③ 剧情分支应做成图片+title 单组件，条件/目标散开很散乱；④（追加）较复杂的信息通过 tooltip 信息卡呈现，节点关注图片（记忆）、标题（目标）、概率（可能性）。

**重构**（设计文档 `spec/D06-encounter-storybranch-design.md`，v1.1）：
1. **节点单组件**（`BuildEncounterNodeCard`）：每节点 = 52px strImg 缩略图（可点击放大，无图 BookOpen 兜底）+ 标题 + 概率胶囊，最多 ID/类型两个 9px chip——卡片高度 ≤96px
2. **tooltip 信息卡**（`BuildBranchTooltip`）：描述（截断 200 字）+ 前置条件及满足情况（✓/✗ 着色 + ¬ 样式，随过滤实时刷新）+ 物品触发 + 概率——复杂信息全部移出卡片
3. **同页去重**：左列反向引用橙卡、Tab 内「👈 Referenced By」反向链面板、根剧情标识全部移除（反向统一收到底部被引用面板）；独立 `BuildResponsesPanel` 合并进分支图（同数据双渲染消除），格式提示行移入分支图节首
4. **Mermaid 同源对齐**：`PrepareBranches` 纯函数产出 `BranchData`，节点卡渲染与 `BuildMermaidText` 共用同一数据源（结构性消除漂移）；移除反向 R 节点与 ctx 标签；分支节点 ID 改 `B{index}`（修复 >26 分支溢出）
5. **顺带修正**：类型 chip 补齐实测值域 0-3（剧情/搜刮/战斗/破解四色，现状只区分 Normal/Scavenge），Hero/分支卡/链树三处统一共享映射；概率格式统一 `0.##%`（P2 在部分文化产生 "50 %" 空格）

**测试**：EncounterVisualizerTests +15（纯函数数值断言 + Avalonia.Headless 输入模拟导航 + ToolTip.GetTip 断言 tooltip 内容）。**861/861**（13 项目全绿，EntityEditor 71→86）。

**v2 微调**（用户复测反馈）：节点卡布局改为 标题（第一行居中）→ 图片（第二行主体 168×110 ≈ 卡片 70%，点击放大）→ 第三行左&中 ID/类型 chip + 右侧概率胶囊；tooltip 物品行用 **Item.Name**（非 Description）且置于第二行（标题之后）。测试断言图片 52→168。

---

## R58：mod 图片缺失真根因（getmods2.php 换行未 Trim）+ 图片诊断进日志 | 2026-08-08

**反馈**（用户）：图片还是缺失（第三轮）。用户提供游戏目录 D:/Downloads/Neo Scavenger/。

**根因定位**（用真实目录端到端验证）：
1. 用户目录 getmods.php 是**空壳**（nRows=0），真正生效的是 **getmods2.php**（nRows=47）——R56 只读 getmods.php，解析出空列表后直接 return，Mods/ 完全没扫
2. **getmods2.php 是多行格式**（nRows=47
&strModName0=NSE&strModURL0=Mods/...
）——按 & 分割后**值末尾带 
 未 Trim**，拼出的路径含换行符 → 目录查找全部失败（原版图片正常因为主 img/ 在列表首位，mod 图片全缺——与用户现象完全吻合）

**修复**：
1. getmods.php 与 getmods2.php **都读**，任一解析出路径即用（都空才走 Mods/*/* 两层扫描兜底）
2. **ParseModUrls 值 Trim**（去 
/空白）
3. 图片缺失诊断写入日志文件（DataBrowserViewModel.LogAction → RunLogStore → player-run-*.log，含 gameRoot/getmods 内容/目录存在性）——下次再缺直接读日志定位

**验证**：临时程序对真实目录端到端验证——主图 AMode308.png ✓、mod 图 AMode12ScatterGunA/B、AMode45PistolSuppressed ✓ 全部解析成功。测试：getmods2 多行格式用例（值带 
）。Player.Core.Tests 129/129。

---
## R57：ItemType 可视化设计文档（D04）+ Creature 可视化重构（D05）+ FieldGroupMetadata 全量修正 | 2026-08-08

**目标**（用户）：① 写一份 ItemType 设计文档，说明字段、描述及字段对应的设计原因和设计目的；② 之后实现剩下的类型——简单的关联类套通用组件，复杂的用独立 agent 写设计文档 → 评估 → 再开 agent 开发。

**新增**：
1. **D04 ItemType 可视化设计文档**（`spec/D04-itemtype-visualization-design.md`）：37 列（30 模型属性）→ 8 个呈现位置逐字段设计，含游戏语义 / 设计原因 / 设计目的三栏；确立「把数据翻译成问题答案」范式（损耗→寿命、士气→有效伤害、权重→概率）。登记进 spec/README
2. **25 个 visualizer 现状审计**（Explore agent）：A 级深度设计 9 个（AttackMode/BattleMove/CampType/Condition/Encounter/Faction/HexType/Recipe/TreasureTable）、B 级表单化 8 个、C 级浅实现 6 个（纯关联类）
3. **简单补漏**（通用组件收尾）：
   - `DataFile`/`GameVar`/`Headline` 补挂反向引用面板（此前 23 个里仅这 3 个缺失）
   - `AttackMode` Sound 徽章接 `PlaySoundButton`（R42 音频播放，此前是死徽章）
   - `Encounter` 补 `fCreatureChance`（Hero 概率行）/ `aMinimapHexes`（小地图标记徽章）/ `ptEditor`（弱化显示）
   - `BattleMove` 补 `vHexTypes`（KV 行）
4. **D05 Creature 可视化设计文档 + 重构**：
   - 文档：13 字段全覆盖 + 战斗三层（Σ伤害条→Σ有效伤害→行+展开）+ 出场状态概率徽章 + 战利品双池（携带 vs 尸体）+ 遭遇链（正向事件链/反向剧情/刷新点权重归一）
   - **事实修正**：设计 agent 曾断言「nHP/nStrength 等属性游戏 XML 有、编辑器未导入」——实测全 data 目录无这些字段（creatures.xml CREATE TABLE 仅 13 列），文档改为「不预留虚构槽位」
   - 实现：`CreatureEntityVisualizer` 293→968 行全量重写（Hero/战斗/属性/战利品/遭遇五区块，两列情境布局；注册注入 IEntityLookupService）；`Vis.OnEnterConditions` 错误标签消除（vEncounterIDs 指向 Encounter）；+5 测试（CreatureVisualizerTests）
5. **FieldGroupMetadata 全量修正**：原分组与真实模型**大面积漂移**（Recipe「nHours/bConsumed」、AttackMode「fBluntDmg/fCutDmg」、Creature「nHP/nStrength/vCorpseID」等全是虚构或拼写错误列名 → 字段全部落入默认「属性」分组，KV 编辑器/Raw Data 分组失效）。按真实 `[Column]` 重写全部 24 个类型分组，脚本核对零漂移

**测试**：845/845（13 项目全绿；EntityEditor 66→71）。

---

## R56：mod 图片按 getmods.php 定位（真实 mod 路径 + 两层 Mods/*/*）| 2026-08-08

**反馈**（用户）：图片还是缺失。用户指路：**mod 图片位置要通过 getmods.php 拿到 mod 路径**，再从 mod 路径找 img/ 和 getimages.php。

**修复**（R54/R55 目录约定不对，重做）：
1. **getmods.php 驱动**：游戏自带 `getmods.php`（磁盘优先 serve）声明真实 mod 路径（`strModURL{i}`，如 `Mods/<分组>/<mod>`，可任意位置）——数据浏览器构造时**解析 gameRoot/getmods.php**，按声明路径收集图片目录（mod 根 + img/）
2. **两层兜底**：getmods.php 缺失时扫 `Mods/*/*`（两层——ModListScanner 同款；R54/R55 只扫了一层 `Mods/<mod>/`，用户 mod 在 `Mods/<分组>/<mod>/` 时扫不到，这是仍缺失的直接原因）
3. 保留主 `img/` 优先；解析容忍 URL 编码（UnescapeDataString）

**测试**：WikiDetailBuilderTests 改为两层结构用例 + 新增 getmods.php 声明路径用例（CustomMods/m9/img）。Player.Core.Tests 128/128。

---

## R55：数据浏览器五项（mod 图片根目录 · tabHeader 紧凑 · 启动日志清理 · 存档管理两行 · 闪退防御）| 2026-08-08

**反馈**（用户）：① 数据浏览器反向引用 tabHeader 太大；② 图片还是找不着；③ 去掉 v2.47 这类临时日志；④ 存档管理上边太长按钮放第二行；⑤ 浏览数据浏览器时闪退。

**修复**：
1. **mod 图片补根目录（R55）**：R54 只扫了 `Mods/<mod>/img`，但 ImageSearchService 的约定还包含 **`Mods/<mod>/` 根目录**（图片可直接放 mod 根）——补上；查找顺序 = 主 img → 各 mod 根 → 各 mod/img
2. **tabHeader 紧凑**：数据浏览器「被引用」TabControl 加 TabItem 样式（Padding 8,3 / FontSize 12），默认 header 太大
3. **启动日志清理（host.html v2.64）**：去掉 v2.47 逐条「启动展开」info/warn 日志与版本就绪行（用户反馈噪音）——只保留 error（LsoExpand 不可用/展开失败，诊断用）
4. **存档管理顶栏两行**：第一行提示文字（可换行），第二行按钮（刷新/清空全部/重启游戏），顶栏高度大减
5. **闪退防御**：日志尾部无 FATAL（托管异常处理器未触发）→ 疑似大图全尺寸解码内存爆炸（进程直接退出）。图片解码改 `Bitmap.DecodeToWidth(stream, 512)`（预览/缩略图足够，防 mod 高清图 OOM）；选中行构建整段 try/catch（异常转状态行提示不崩溃）

**测试**：ModImagesResolveFromModsDirWhenNotInGameImg 扩展 mod 根目录断言。**839/839**（13 项目全绿，Player.Core.Tests 127/127）。**待用户复测**：图片（mod 根目录）、tabHeader、存档管理顶栏、闪退是否复现（若复现请告知浏览的表/行）。

---

## R54：数据浏览器模组图片修复 + 日志窗 UI 订正 | 2026-08-07

**反馈**（用户）：① 数据浏览器里其他 mod 的图片找不着；② 订正文档。

**修复**：
1. **数据浏览器模组图片（R54）**：图片来源约定 = 主游戏 `{gameRoot}/img/` + **模组 `{gameRoot}/Mods/<mod>/img/`**（与 ProxyHttpModule.getimages.php 扫描、ImageSearchService 一致）——但 `WikiDetailBuilder.ResolveImagePath` 只查主 img/，模组图片全部显示"缺失"。修复：构造时扫描 `Mods/*/img` 目录列表缓存，解析按 主 img → 各模组 img 顺序查找（含 .png 补全）；markdown 画廊与原生走马灯（GetImageItems）共用该解析，两处同时修复
2. **文档订正**：42 计划 v2.24 图片画廊段更新模组目录约定 + 版本历史补 v2.63（R52-R54 UI 订正）

**测试**：WikiDetailBuilderTests +1（模组图片从 Mods/<mod>/img 解析、主目录缺失不误报）。**839/839**（13 项目全绿，Player.Core.Tests 127/127）。

---

## R53：日志窗去虚拟化 + 全局按钮紧凑化 | 2026-08-07

**反馈**（用户）：① 日志窗不要虚拟化——虚拟化后滚动条跳来跳去难受；② 其他工具窗口的按钮做紧凑点，别占大块地方、还要拉窗口尺寸。

**改动**：
1. **日志窗去虚拟化**：ListBox ItemsPanel 改普通 `StackPanel`（非虚拟化）——Expander 展开行高变化时滚动条不再跳动（行数受控：剪贴板截获是大块单行，渲染开销可接受）
2. **日志窗紧凑**：列表 Margin/行高/字号缩小（行 20px、字号 11-12、顶栏 Padding 10,6、ComboBox 120px）
3. **全局按钮紧凑**（App.axaml 样式）：`Window Button` → Padding 10,4、FontSize 12、MinHeight 26——存档管理/存档修改器/日志窗等所有工具窗口按钮统一变小（主窗口无 Button 不受影响）

---

## R52：日志窗跟随主题 + 顶栏两行 | 2026-08-07

**反馈**（用户）：① 日志样式和主题没对上，颜色看不清；② 顶栏按钮太多换两行。

**改动**：
1. **主题化**：移除固定深色背景（#1E1E1E/#2D2D2D）与硬编码前景色，全部换主题资源——窗口/顶栏背景 `SystemControlBackgroundChromeMediumLowBrush`、文字 `SystemControlForegroundBaseHigh/MediumBrush`、级别列 `SystemControlHighlightAccentBrush`、按钮去硬编码底色。切 系统/亮/暗 主题颜色跟随
2. **顶栏两行**：第一行标题 + 日志文件路径；第二行级别过滤 ComboBox + 全部操作按钮（打开日志目录/剪贴板/导出日志/清空/关闭）

---

## R51：日志热键 Shift+Tab → F10 | 2026-08-07

**反馈**（用户）：Shift+Tab 不要了，换成 F10。

**改动**（host.html v2.62）：日志窗口热键全链路 Shift+Tab → **F10**——host.html 页面桥（游戏聚焦时按 F10 → chrome.webview.postMessage → 切换日志窗）、日志窗口内 F10 关闭（原 Shift+Tab 关闭）、主窗口 Avalonia 焦点时 F10 切换（KeyDown 新增）。菜单文案/窗口标题/占位提示同步更新（日志 (F10) / 运行日志 — F10 关闭 / 关闭 (F10)）。

---

## R50：日志覆盖层改普通弹窗 | 2026-08-07

**反馈**（用户）：Shift+Tab 的日志覆盖层不灵活，换成弹窗。

**改动**：LogOverlayWindow 由「无边框 + 全屏 + Topmost + 透明背景」的覆盖层改为**普通弹窗**——标题栏（可拖动、X 关闭）、900×620 可调大小、固定深色背景（#1E1E1E，硬编码前景色全兼容不依赖主题）、居中于主窗口。打开方式不变（视图 → 日志 / Shift+Tab 页面桥），Shift+Tab 仍可关闭。移除 PlayerWindow 里全屏/位置跟随逻辑。

---

## R49：日志行可展开（剪贴板截获内容可见化）| 2026-08-07

**问题**（用户实机）：v2.59/2.60 日志里其实已有完整截获内容（mod 加载日志几千字符），但日志浮层每行固定 22px 高 + 省略号截断，**内容只能 hover tooltip 看到**——用户以为"日志还是空的"。

**修复**：日志浮层行改 **Expander**——平时一行摘要（时间/级别/截断消息 + tooltip 快速预览，行高仍固定防滚动跳动），**点开展开完整内容**（TextWrapping 多行显示，剪贴板截获的多行游戏日志完整可见）。v2.61 顺带移除 v2.60 的诊断行（value setter 安装 + execCommand 命中逐条 debug——使命完成）。

**剪贴板链路终态（R48-R49）**：游戏写剪贴板 = flash.desktop.Clipboard.setData → Ruffle 临时 textarea（挂在 ruffle-player 的 **shadow root**，v2.60 诊断证实 ta2=有(长度)）→ `value` setter 拦截截获内容进日志（v2.59 起，实机验证 ✓）→ execCommand 拦截阻断真实写入（系统剪贴板保持干净 ✓）。**待用户确认**：浮层点开行可见完整内容。

---

## R48：剪贴板源头截获 + 菜单重组 + 存档修改器改节点编辑器 | 2026-08-07

**反馈**（用户）：① 日志里「游戏剪贴板日志(截获): 」还是空的——要的是把内容转到日志而不是剪贴板；② 调试菜单重组：开发者工具/关于单拎出来，打开日志目录/导出日志/导出存档放文件里；③ 存档修改器去掉顶部下拉/刷新/加载；④ 存档修改器要节点编辑器不是文本编辑器。

**修复/新增**：
1. **剪贴板源头截获（host.html v2.57）**：WebView2 selection/focus 时序不稳导致 v2.56 提取仍空；且 execCommand 同步执行先于 MutationObserver 微任务会污染 lastClipboard 去重。现在 execCommand 拦截**只阻断真实写入**，内容提取交给 **MutationObserver 监听 textarea append**（Ruffle set_value 先于 append，回调直接读 value）——从源头截获，不依赖 selection/focus
2. **菜单重组**：撤销调试菜单——打开日志目录/导出日志/导出存档+日志 (zip) → **文件**；开发者工具 (F12) → **视图**（与 F11 同类）；关于 → 文件底部
3. **存档修改器精简**：去掉顶部存档下拉/刷新/加载（入口=存档管理「修改」按钮已预载），窗口标题显示当前存档 key
4. **存档修改器 → 节点编辑器**：新增 SaveNode 树模型 + SaveTree 双向转换（Build/SerializeValue）——**容器只读结构**（object names[]/values[] traits 对应不能乱，增删字段会改崩存档）、**标量内联编辑**（string/int/double 文本框、bool 复选；null/undefined/date/xml/bytes 只读 + RawJson 原样回写无损）。还原 toTree 细节：vec* values 裸数字、NaN/±Infinity 字符串标记、double "R" 精度。保存仍走 fromTree 回验；序列化失败（非法数字）状态行报字段名、不发 JS

**测试**：SaveTreeTests +7（object/编辑回写/复杂结构 DeepEquals/vec·array·dict 子节点/NaN/非法数字/bool·null）；SaveEditorViewModelTests 重写适配节点树。**838/838**（13 项目全绿，Player.Core.Tests 126/126）。**待用户复测**：剪贴板内容回日志、节点编辑器实机。

**R48 追加（v2.58）**：剪贴板截获仍无内容（v2.57 后连空行都没了）——根因：Ruffle 的临时 textarea 挂在 **ruffle-player 的 shadow root** 内部，`document.querySelector` 与观察 `documentElement` 的 MutationObserver 都看不到（且 v2.57 遗留了重复 observer 块、外层 try 未闭合）。v2.58：execCommand 拦截恢复**同步提取**（选区 → 普通 DOM textarea → **shadowRoot.querySelector** → activeElement），`__watchClipboard(root)` 支持任意根观察，player 创建后对 `player.shadowRoot` 也挂一份；清理重复块。待用户复测。

**R48 追加（v2.59）**：v2.58 仍空——selection/MutationObserver/shadowRoot 查询都是"找 textarea 再读"，受挂载位置与时序影响。**改用 `HTMLTextAreaElement.prototype.value` setter 拦截**：Ruffle 无论把临时 textarea 挂哪，`set_value` 必然经过该 setter（同步、位置无关；本页无其他 textarea）。execCommand 拦截保留阻断 + 新增「剪贴板 execCommand 拦截命中」debug 诊断行。node 沙箱功能验证通过（赋值 → /__log clipboard 行 + 去重 ✓）。待用户复测。

---

## R47：工具面板名称全本地化 + 音频资产空名/0KB 修复 | 2026-08-07

**反馈**（用户）：① 音频资产全是空名 + 0KB；② 怎么就音频资产 tool 的名字有本地化，其他 tool 名字没本地化？还是说你是硬编码的？

**修复/新增**：
1. **音频资产空名 + 0KB 修复**：`index.json` 字段是小写 `{id,name,file,bytes}`，而 C# 反序列化类用 PascalCase——System.Text.Json 默认大小写敏感导致全部字段落空。`SoundsToolViewModel` 与 `AudioPlaybackService` 统一改用 `PropertyNameCaseInsensitive = true` 的 `JsonSerializerOptions`。154 个音频现在正常显示名称/大小并可播放
2. **工具面板名称全本地化**：全部 11 个 `IToolPlugin`（ProfileTool / SoundsTool / AiChat / ImageOrchestration / Editor(KV) / ImageBrowser / WebView / ParaTranz / OverlayChain / 调试：命令日志 / 调试：会话脏状态）注入 `ILocalizationService`，`Title` 改为 `_loc["Tools.Xxx"]`；新增 `Tools.*` 资源键 11 条（en/zh/en-us 三语），删除所有硬编码 Title。插件测试断言同步改为对 key 断言（stub loc 返回 key 自身）
3. **脚本误改修复**：批处理脚本注入时误伤两处——`ParatranzPlugin` ctor 残留逗号（`viewModel), loc`）已修正；`SessionDirtyDebugTool` 的 `_loc` 字段被插进 ViewModel 类而非 Plugin 类（编译期报"不存在名称 _loc"）已移正。一次性脚本已删除

**测试**：831/831（13 项目全绿）。

---

## R46：About 消息框化 + 存档工具并入存档管理 + 剪贴板截获内容修复 | 2026-08-07

**反馈**（用户）：① About 弹窗不需要两个确认按钮，右上角能关就行（"直接拿 messagebox 做不就行了"）；② 存档修改工具应集成到存档管理里，直接加个修改按钮；③ 日志现在完全没有剪贴板里的内容了。

**修复/新增**：
1. **About → MessageBox**：Player.csproj 加 `MessageBox.Avalonia 12.0.0`（编辑器同款，命名空间 MsBox.Avalonia），`MessageBoxManager.GetMessageBoxStandard` 纯消息 + 右上角 X 关闭；PromptDialogWindow 恢复原样（此前加的"无按钮模式"撤销——不折腾共享组件）
2. **存档修改工具并入存档管理**：移除调试菜单入口；存档管理窗口每行新增「修改」按钮 → `EditSaveRequested` 事件 → 宿主打开编辑器并预载该存档（`LoadEntryAsync`）；「保存并加载」后存档管理列表自动刷新（大小变化可见）
3. **剪贴板截获内容修复（host.html v2.56）**：v2.54 后真实剪贴板干净但日志无截获——Ruffle 流程 textarea.value → focus → select() → execCommand("copy")，WebView2 里 selection 时序不稳取不到内容。提取链改为 selection → activeElement(textarea) → 兜底 `querySelector("textarea")`（execCommand 调用时 Ruffle 临时 textarea 尚未移除）——日志恢复「游戏剪贴板日志(截获)」

**测试**：831/831（13 项目全绿，本轮无新增测试）。**待用户复测**：剪贴板内容回日志。

---

## R45：存档修改工具（调试用：加载/查看结构/编辑/保存/另存/保存并加载）| 2026-08-07

**目标**（用户需求）：写个存档修改工具作为调试工具（顺带能看存档结构）——加载指定存档、修改、保存/另存、保存并加载。

**新增**：
1. **LSO ↔ JSON 双向转换（lso-expand-web.js v2.49）**：`LsoExpand.toTree(b64)` = 解析 LSO（复用展开器全链路）→ 引用内联树（`__amf` 类型标记：object/array/vecint/vecobject/dict/date/xml/bytes/int/double）；`fromTree(json)` = JSON → 全内联重编码 → **立即回验 parseLso**（改坏在写入前报错）。`sanitizeTree` 处理 JSON 不可表达的 NaN/±Infinity（存档里有 755 处未初始化 double）→ 字符串标记，`encValue` 的 setFloat64 自动还原——**round-trip 与原始树完全一致**（两个样本存档 node 验证）
2. **存档修改工具（SaveEditorWindow + SaveEditorViewModel）**：调试菜单入口（需已加载游戏）。存档下拉（key/大小）→ 加载 → JSON 树缩进文本 + 结构摘要行（LSO 名/格式 v/根条目数 + 根条目类型·className——"看结构"）；**保存**（写回原 key）/ **另存为**（弹输入框新 key，默认「原名-copy」）/ **保存并加载**（写回 + 重载页面清 Ruffle SharedObject 内存缓存后生效，关窗口）；错误路径（非 LSO/JSON 坏/编码失败）状态行提示不写坏档
3. 保存后重启游戏时启动展开器会执行 v2.48 的 m_vFactions 归一化（与游戏读取路径一致，工具不重复处理）

**测试**：SaveEditorViewModelTests +8（列表/加载+摘要/解析错误/保存脚本+状态/编码错误/另存新 key/保存并加载触发重启/未选择提示）。**831/831**（13 项目全绿，Player.Core.Tests 119/119）。

---

## R44：剪贴板真根因修复 + 版本号/About + 存档日志 zip 导出（试用发布准备）| 2026-08-07

**背景**：用户准备发布试用版，验收反馈：删档修复 ✓ / F12 ✓ / 导出日志 ✓ / 报错弹窗待确认 / **"剪贴板里依旧全都是日志"**（v2.53 未达预期）。

**修复/新增**：
1. **剪贴板真根因（host.html v2.54）**：源码取证 ruffle-rs `web/src/ui.rs` `WebUiBackend::set_clipboard_content`——Ruffle 写剪贴板**不走 `navigator.clipboard.writeText`**（注释：该 API 仅 HTTPS 安全上下文可用，本页 http://127.0.0.1 不可用），而是隐藏 textarea + select + `document.execCommand("copy")`（wasm glue 无 `__wbg_writeText`，仅 `__wbg_execCommand`）。v2.44-v2.53 的 writeText 包装从未被调用；v2.53 前日志截获行全靠 readText 轮询读回。**补拦截 `document.execCommand`**（copy/cut → 捕获选区进日志 + 返回 true 不写真实剪贴板）；Ruffle 自身 buffer 不受影响（游戏内 System.getClipboard 仍可读）。writeText/readText 包装保留防未来版本
2. **版本号 v0.9.0**：csproj `<Version>0.9.0</Version>` → 窗口标题 / 启动日志首行 / About / 导出 zip 命名同源；publish.ps1 zip 默认命名改读 csproj Version（替代当天日期）
3. **About**：调试菜单「关于」→ 版本 / Ruffle nightly-2026-08-04 / 平台 / 日志目录 / 游戏根目录 / WebView2 数据目录
4. **导出存档+日志 (zip)**：调试菜单 → `NeoScavengerPlayer-export-{版本}-{时间戳}.zip` = info.txt + saves/localstorage.json（host.html 新增 `__exportSaves()` 全量存档）+ logs/*.log + save_backup/*.json（Core 新 `PlayerBundleExporter`，System.IO.Compression 内置），完成后 Explorer 定位——试用反馈/存档迁移一键包
5. **README**：新增播放器小节（要求/使用/数据位置/已知限制/反馈方式）
6. **文档**：42 计划 v2.54 条目 + 发布前校验清单补 8-10 条（存档闭环/调试工具/报错捕捉）+ 版本号节更新 + 运行时落盘表补导出产物

**实机**（用户）：删档 ✓ / F12 ✓ / 导出日志 ✓ / 报错弹窗未确认（触发方式：DevTools Console 执行 `window.__log("error","TypeError: test")`）；剪贴板 v2.54 修复待复测。

**测试**：PlayerBundleExporterTests +2。**823/823**（13 项目全绿，Player.Core.Tests 111/111）。

---

## R43：耐久推演 + 条件效果翻译 + 游戏音频资产（提取/浏览/播放）| 2026-08-07

**定位**（用户确认）：可视化 = 单实体 · 只读 · 语义翻译。本轮补"翻译"层缺口：时间维度推演、条件真实效果、声音资产可听。

**1. 耐久寿命推演**（生命周期区块）：`寿命 每小时 ≈100h · 装备时 ≈100h · 每次使用 ≈100×`——把损耗率翻译成"能用多久"（`Vis.Lifespan`）。

**2. 条件效果翻译**（hover 徽章预览）：`BuildConditionEffectText` 解析 `aFieldNames/aModifiers` 逗号配对 → `m_fMoveCost +0.5 · m_fVisibility -0.2`——自定义条件光看名字不再看不懂（Doc 38 §5 格式）。

**3. 游戏音频资产管线** ⭐：
- **提取** `player-tools/extract-sounds.js`：零依赖 SWF 解析器——CWS zlib 解压 + 标签流起始探测 + **DefineSound**（头 = SoundId + 打包字节[Format:4|Rate:2|Size:1|Type:1] + SampleCount + [SeekSamples]）提取 MP3 + **SymbolClass**（NULL 结尾字符串，实测非长度前缀）映射 cue 名 → `{GameRootDir}/sounds/{cue}.mp3` + `index.json`。实测 **154/154 MP3 全部有效**（37MB，147 个 cue 命名，含 cueRiflePickup/cueAmmoDrops4 等）
- **播放** `AudioPlaybackService`（App，winmm MCI P/Invoke 零依赖）：`IAudioPlaybackService` 接口（Core）——cue 名精确→子串匹配（strSnd `cueRifle` → 资产 `NEOScavengerSounds_cueRiflePickup`）；游戏目录切换自动重载索引
- **音频资产 Tool Dock**（右栏「音频资产」）：搜索 cue 名 + 点击播放/停止 + 大小显示；未提取时提示运行脚本
- **可视化播放按钮**：攻击模式行声音 `▶` + 装备区块 aSounds `▶`（`VisHelper.PlaySoundButton`，无索引自动隐藏）

**测试**：EntityEditor 66/66，全量 **821/821**。⚠️ 声音为游戏 SWF 内嵌（修改需改 SWF，超出编辑器范围；此处仅资产查看/试听）。

---

## R42：Player 调试工具四件套（剪贴板接管 · F12 DevTools · 日志目录/导出 · 报错捕捉）| 2026-08-07

**目标**（用户需求）：① 游戏老往剪贴板写内部日志很烦，能否转移到日志里（且每次启动都要点「允许写入剪贴板」）；② 想要 F12 工具看 localStorage 和 network；③ 打开日志位置、导出日志；④ 游戏报错退出到播放器能被捕捉。

**修复/新增**：
1. **剪贴板完全接管（host.html v2.53）**：旧 writeText 包装器兜底 `execCommand('copy')` 仍写真实剪贴板（日志刷屏）+ 每次启动弹 WebView2「允许写入剪贴板」权限框；readText 800ms 轮询还弹读取权限框。现在 writeText 只 `captureClipboard()` 进日志（level=clipboard）返回成功、readText 返回空串、删除轮询——**启动零弹窗、真实剪贴板干净、内容完整进日志文件**（Ruffle 只调 writeText，已核实无 ClipboardItem）
2. **F12 开发者工具**：`WebView2DevTools`（ComImport 子集桥接 `ICoreWebView2.OpenDevToolsWindow`，vtable slot 48；包公共面只有原始 IntPtr）；窗口 F12 + 调试菜单触发；游戏聚焦时 WebView2 原生 F12/Ctrl+Shift+I 亦可用。DevTools 覆盖 Network / Application-localStorage / Console
3. **日志目录/导出**：LogOverlayWindow 顶栏 + 调试菜单；「打开日志目录」explorer /select 最新日志；「导出日志」= 头部信息 + localStorage 快照（`__dumpLocalStorage()`，VM 注入 ExecuteJs 桥）+ 全部 run 日志行 → `player-log-export-*.txt`（RunLogReport 纯函数构建），导出后 explorer 定位
4. **报错捕捉**：host.html 补 unhandledrejection + `load().catch` 写 /__log + `window.__log` 全局；SwfLogBridge 新增 `GameError` 事件（致命签名正则，10s 去抖）→ 播放器弹窗「检测到游戏错误」[打开日志目录/知道了]（VM 30s 去抖）+ 状态栏警示；异常退出（run 内有 error 行）状态栏「游戏异常退出」

**测试**：SwfLogBridgeTests +5 模式（cannot convert / window.onerror / unhandledrejection / stack overflow / SWF 加载失败）→ GameError，+3 良性行不误报；RunLogReportTests +2。**821/821**（13 项目全绿，Player.Core.Tests 109/109）。

---

## R41：数值条改指标值 + 颜色柔化 + 展开详情紧凑化 | 2026-08-07

**问题**（用户验收 + 截图识别）：① 数值条没有比较对象——填充比例表达的是"Cut 占比"或随意分母（有效伤害 `/8`、士气 `/1`），0.3 伤害的条看起来 60-70% 满，误导；② 高饱和纯色刺眼（Cut #C62828 / Blunt #1565C0 / 有效紫 #6A1B9A / 士气绿）；③ AttackMode 展开详情里 44px 图标 + "近战"独占一行，小图浪费行高。

**修复**：
1. **数值条 → 比例条 + 指标值**：填充条只保留有语义的比例（总伤害/攻击行的 **Cut:Blunt 构成比**）；有效伤害（`0.4 (×1.30)`）、士气补正（`+30%`）改为**纯指标值文本**（`ValueRow`/内联文本 + 语义色），删除无参照的分母
2. **颜色柔化**（Material 300-400 系）：Cut `#E57373` / Blunt `#64B5F6` / 有效 `#9575CD` / 士气 `#66BB6A`·`#E57373` / 耐久 `#66BB6A`·`#FFB74D`·`#E57373`·`#90A4AE`——条类统一低饱和，高饱和仅用于文字前景（对比度需要）
3. **展开详情紧凑化**：图标 36×36 + 类型 + 士气补正 + 有效伤害**合并为一行**（小图标不再独占行）；公式说明降为小字
4. 总伤害区有效伤害合计：StatBar → `ValueRow`

**测试**：断言适配新颜色（#E57373/#64B5F6）与新文本（`Effective 5.6 (×1.25)`）；EntityEditor 66/66。⚠️ 全量 12 项目 798/798 通过（Integration.Tests 因 NeoEditor GUI 运行锁 DLL 未构建，关闭后补齐）。

---

## R40：ItemType Detail 层级重构（两段式信息架构）| 2026-08-07

**问题**（用户批评）：Detail 层级混乱——9 个区块平铺无主次、同一语义碎片化（条件散在 3 张卡）、Card 套 StackPanel 套 WrapPanel 套 Border 嵌套 5-6 层、三种值组件（StatBar/CreatureStatGrid/StatCard）混用、标题风格不统一。

**重构**（`ItemTypeEntityVisualizer` + `VisHelperService`）：

1. **心理模型两段式**（7 区块，语义聚合）：按用户使用物品的认知顺序组织
   - 第一段「是什么/怎么用」：① Hero 身份（图/名/描述 + **关键数字行**：重量/价值/堆叠/镜像/槽深）→ ② 装备（穿装预览 + 槽位 + **交互音效 aSounds**）→ ③ 战斗（总伤害 → 攻击模式明细，攻击音效在行内）→ ④ **效果**（携带/使用/装备条件 + 必须条件 CondId + 物品属性 ItemProp——**条件从 3 处合并到 1 处**）
   - 第二段「怎么活着/和什么有关」：⑤ **生命周期**（耐久条 + 损耗 + 损坏掉落 + 弹药 ChargeProfiles）→ ⑥ **来源与产出**（开关 + 战利品表 + 组件）→ ⑦ 容器（独立区块）→ ⑧ 被引用
2. **统一视觉语言**：新组件 `SectionHeader`（**图标 + 色条** + 标题）统一所有区块头（⚔战斗/🧍装备/✨效果/⏳生命周期/📦容器/🔗来源产出）；`ValueRow`（90px 键值行）统一标量值展示；区块 = SectionHeader + Card，**嵌套 ≤ 4 层**
3. **两列情境布局**：`AddRow(left, right)`——区块按情境两两并排（战斗|装备 / 效果|生命周期 / 容器|来源产出），空区块让另一侧横贯；不再单列无限堆叠
4. **声音按心理情境归位**（用户指出"音效和关联有什么关系"）：`aSounds`（拾取/放下）→ 装备区块（"拿它干嘛"情境）；`strSnd`（攻击音效）→ 攻击模式行内（已有）
5. **Hero 错位修复**：关键数字行从跨行 Grid 放置并入身份列内部（消除隐式行导致的错位）
6. 旧函数清除：6 个卡函数 → 7 个 body 构建 + 守卫，空区块不渲染
7. 资源键：`Vis.Effects`/`Vis.Lifecycle`/`Vis.Associations`（zh **来源与产出** / en **Sources & Crafting**）

**验证**：EntityEditor 66/66，全量 **811/811**。⚠️ 待 GUI 验收整体观感。

---

## R39：ImageAsset 引用不再触发 LookupRef 约束异常（R38 追修）| 2026-08-07

**问题**（实机日志）：打开物品 Detail 时 `[RawData] LookupRef<ImageAsset> failed ... violates the constraint of type 'T'` 异常刷屏——`ItemType.ImageList/SpriteList` 与 `AttackMode.strIMG` 的 `[ReferenceField(typeof(ImageAsset), TargetKey="{FileName}")]` 指向**非 IEntity 类型**（纯文件名引用），R34 的统一解析入口 `MakeGenericMethod(targetType)` 违反 `LookupRef<T> where T : IEntity` 约束 → 每个段抛 ArgumentException（被 catch 吞掉但刷 Warning 日志），且这些段被误标为"未解析"琥珀。

**修复**（`VisHelperService.RawData.cs` 三处）：
1. `ResolveRawSegment`：入口加 `!typeof(IEntity).IsAssignableFrom(targetType)` 守卫——非实体类型直接返回 null，不再抛异常
2. `BuildRawRefCell`：非实体目标类型渲染**灰色原文徽章**（文件名引用天然无需解析），不标琥珀
3. `CountUnresolvedSegments`：非实体引用不计数"未解析"——Expander 头统计不再虚高

**测试**：+1（`BuildRawDataTable_ImageAssetRefs_RenderPlainRawText_NotAmber`：无琥珀、灰色原文徽章、`UnresolvedRefSegments == 0`）；**811/811**。

---

## R38：士气加成纳入伤害可视化（R36 追修）| 2026-08-07

**问题**（用户指出）：R36 战斗卡只显示基础 Cut/Blunt，**漏了武器士气补正（`fMorale`）**——Doc 38 定义实际伤害 = (1+士气+此值)×(1+近战/远程加成)×武器伤害（实测最常见 0.3）。且展开详情的 Morale 数值行用整数格式 `+#;-#;0`，0.25 显示成 `+0`。

**修复**（对照全字段重新审视后一并处理）：
1. **攻击模式行 meta** 加 `士气 +25%`（`+0%;-0%` 百分比格式）——武器伤害修正一眼可见
2. **展开详情伤害区重排**：基础（行内堆叠条）→ **士气补正 StatBar**（`25% (base)` 灰 / >25% 绿 / <25% 红，复用 AttackMode visualizer 成熟模式）→ **有效伤害 StatBar**（`5.6 (1.25 × 4.5)`）→ **公式说明**（`实际伤害 = (1 + 角色士气 + 武器补正) × (1 + 近战/远程加成) × 武器伤害`，新键 `Vis.DamageFormula`）
3. **总伤害条下方**：Σ 有效伤害（每模式 base×(1+morale) 求和，`6.9 (×1.25)`）
4. **`strIMG` 武器图标**：展开详情顶部显示（48×48，`Image.ToRawString`——同 R36 守卫加固思路，不依赖 RawText）
5. 删除整数格式的 Morale 数值行（bug 源）

**测试**：`ItemTypeCombatCardTests` 强化——Rifle Shot（Morale 0.25）断言 meta `士气 +25%` + 内联详情有效伤害 `5.6 (1.25 × 4.5)`；**810/810**（13 项目全绿）。

---

## R37：Player 存档修复闭环（LSO 引用展开 · 运行时生效 · 保存归一化）| 2026-08-07

**目标**（用户需求）：受伤存档重启必崩 → 读档/保存跨重启可靠（"既然 ruffle 有序列化问题，为什么我们不接管呢"）。

**根因链（三层）**：
1. **读写端引用表错位**：Ruffle 写端（对象身份去重）写出的 AMF3 引用号按写端表编号，读端表（所有复杂类型占位入表）与之错位 → 同一引用号解析到不同对象（伤口 `vCurrentStates` → 物品对象 → for-each 读 `Number.m_fDate` 崩 / `Vector.<int>` 强转崩）。方案：**接管序列化**——`lso-expand-web.js` 两遍法展开器（写端表语义精确重建 + 类型不匹配回退 + `VECINT_PROPS` 字段知识修正），SWF 加载前把 localStorage 存档引用全展开为内联。
2. **运行时展开从未执行（两层真凶）**：① 展开器导出行 `global.LsoExpand`——浏览器无 `global` → 脚本抛错，`window.LsoExpand` 从未定义，启动块与 getItem 包装全部静默跳过（node 测试有 `global` 掩盖了问题）→ 改 `globalThis`；② `GameContentServer` 路由把 `/lso-expand-web.js` 落入游戏根目录 → **404** → 脚本从未加载 → 改 **Web 目录优先**路由（+2 回归测试，13/13 绿）。
3. **保存崩溃（`cannot convert false to Vector.<int>`）**：反编译 SWF 定位 `Creature.get SaveData` 把 `m_dictFactions` 每个值 push 进 `Vector.<int>`；存档数据 100% 干净（flash-lso 0 引用 0 错误），`false` 是游戏运行期 faction 逻辑在**空字典条目**上运算产生的运行态产物（WAL 挖到崩溃前 5 秒成功保存的存档为证）。修复：**形态归一化**——玩家 `m_vFactions` 空（异常形态）补全为与生物一致的 14×-100 默认声望。

**改动**：`lso-expand-web.js`（v2.45→v2.48：两遍法展开器 + globalThis 导出 + 归一化）、`host.html`（v2.47：启动即展开落盘 + 结果 POST /__log 可验证化）、`GameContentServer.cs`（Web 目录优先路由 + .html/.js/.wasm no-store）、`GameContentServerTests`（+2 回归测试）；SWF 反编译分析（player-tools/swf-src，FFDec 取证）。

**验证**：浏览器/节点展开器双版本逐字节一致 + 幂等；flash-lso 解析展开档 0 引用 0 错误；Player.Core.Tests 13/13；**实机闭环全通过**：读档 → 保存 → 关闭重开 → 读档 → 保存。

**v2.49（存档管理"删除/覆盖无效"）**：用户实机发现同一会话内删除/覆盖存档后游戏仍加载同一存档。根因：**Ruffle SharedObject 内存缓存**（`avm2_shared_objects`）——`get_local` 缓存命中直接返回实例，运行中游戏从不重读 localStorage；`clear()` 只删 storage 不清缓存。存档管理的删除/清空/恢复只改 localStorage（备份证明真实执行），对运行中游戏无效。修复：操作后提示"需重启游戏生效"（`Storage.NeedRestart`）+ 存档管理窗口新增「重启游戏」按钮（`RestartGameCommand` → 页面刷新清缓存）；Player.Core.Tests 99/99。调试教训：用户从 Rider F5（Debug 构建）启动，存档在 Debug 的 WebView2 数据目录（与 Release 独立）——排查存档落盘先确认启动构建（leveldb LOG 的 Recovering 记录为证）。

**v2.50（旧档"复活"追修①：自动保存写回）**：删除存档 → 重启 → 读到备份前旧档。反编译取证：游戏**每回合结束自动保存**（`PlayState.update → EndDMTurn → SaveGame`，`GUIEscMenu.bAutosave`）——删除只清 localStorage，Ruffle 缓存实例仍持旧 data，玩家行动推进回合即自动保存 → flush 把缓存旧档写回。修复：删除/清空/恢复后**自动重启游戏**（~300ms 内重载页面，在游戏有机会自动保存前销毁页面）；host.html setItem 包装器加 `__blockSaves` 拦截。

**v2.51（旧档"复活"追修②：实例 Drop 全量 flush）**：游戏内删档（`SharedObject.clear`）→ 重启 → 存档复活仍可继续。源码取证（Ruffle web-lib.rs）：`RuffleInstance::drop` 调用 `flush_shared_objects()` 把**内存缓存所有 SharedObject** 写回 localStorage——`clear()` 只删存储层，缓存实例与 data 原封不动，页面卸载/实例销毁时全量回写把删除"撤销"（真实 Flash 仅显式 flush 写盘，游戏删档流程天然安全；Ruffle 的 Drop 自动 flush 是可靠性设计，删档场景成为复活源）。修复：removeItem 包装器标记 `__savesCleared`；beforeunload/pagehide 监听（先于 Ruffle 注册）在"删过档"时设 `__blockSaves` 拦截卸载 flush。

**v2.52（destroy 路径补拦截 + 拦截条件收窄 + 崩溃修复）**：① `TryDestroyPlayer`（关窗/导航/停止时先 destroy 再卸载）的 Drop flush 早于 pagehide，拦截落空——改为 destroy() 前在同一脚本里先按 `__savesCleared` 置 `__blockSaves`。② 用户质疑"删档后正常关闭是否也没法保存"——拦截条件收窄为「删过档 **且** localStorage 尚无存档」才拦截；已有新档（删除后玩新游戏已自动保存）则放行 flush，新档最后一段进度正常落盘；存档管理 VM 显式设置的 `__blockSaves` 不被覆盖。③ 崩溃修复：v2.50 自动重启与旧的手动 `AskRestartAsync` 弹框冲突（删除后窗口已关闭，再以它为 owner 弹框 → `Cannot show a window with a closed owner`）——移除三处调用。Player.Core.Tests 99/99，Debug+Release 构建 0 错误。

---

## R36：ItemType 战斗表现可视化（游戏语义视角）| 2026-08-07

**目标**（用户需求）：UI 优化 = 可视化和交互性优化——根据字段含义设计呈现与放置，让用户以最小学习/视觉成本查看物品及其**在游戏中的表现**。

**改动（聚焦 ItemType，模式确立后推广）**：

1. **战斗卡重写（BuildCombatCard）** ⭐
   - **总伤害构成堆叠条**（`VisHelperService.StackedDamageBar` 新组件，Doc 21 §7 P3 落地）：一条条内 Cut（红 #C62828）/ Blunt（蓝 #1565C0）按比例分割 + `4.5 · 切割 2.5 + 钝器 2.0`——一眼看出"切割武器还是钝器"
   - **攻击模式明细行**：每行 = 槽位+名称 | 伤害堆叠条 | 距离/穿透/音效 | 展开箭头；**点击行内联展开**完整 AttackMode 详情（数值行 + 弹药 ChargeProfile 消耗数据（每次/每小时/每装备小时/每格 + ⚠可降解）+ 攻击者条件（语义色）+ 挥击短语 + 攻击短语 + 备注 + Ctrl+Click 提示）；Ctrl+Click 仍跳转
   - 未解析的攻击模式段：灰色行保留原文（可审计），不渲染展开
2. **条件徽章语义色**：Fatal 红 #FFEBEE / 永久(Instant) 橙 #FFF3E0 / 可堆叠 绿 #E8F5E9 / 计时 Duration 蓝 #E3F2FD + 后缀（`Bleeding · FATAL` / `WellFed · 12h`）——携带/使用/装备三组 + 攻击模式内联详情统一应用
3. **耐久 StatBar**：耐久度进度条（>50% 绿 / >25% 橙 / 低红 / ∞ 灰）+ 损耗率键值行
4. **守卫加固** ⭐（真实脆弱点）：ItemType visualizer 全部 `IsNullOrWhiteSpace(it.Xxx)`（implicit→`RawText` 缓存，仅 serializer 填充）改为 `Count > 0`；`Split(',')` 改 `ToRawString(",").Split(',')`——绕过 serializer 的实体构造路径（测试/新建/部分恢复）不再静默不渲染
5. 新资源键 ×4：`Vis.TotalDamage` / `Vis.Penetration` / `Vis.Sound` / `Vis.WieldPhrase`（en/zh/en-us）

**测试**：+3（`ItemTypeCombatCardTests`：总伤害堆叠 + 行渲染（干净 id、距离/穿透 meta）/ 内联详情初始隐藏 / 条件语义色）；**810/810**。

**验收**：EntityEditor.Tests 65/65；全量 810/810（13 项目）；build 0 错误（⚠ Player 主项目 MSB3027 因 NeoScavengerPlayer 运行中锁 DLL，代码无错误）。

---

## R35：切换游戏目录后自动重载 getmods.php 与 mod 目录 | 2026-08-07

**问题**：设置页切换游戏根目录后，getmods（getmods.php 的 Game profile 与 mod 命名空间映射）和 mod 目录不自动重载——数据浏览器/引用解析继续用旧目录数据。

**根因（两处断点）**：
1. `BrowserIndexService`（getmods.php 解析 + 引用索引）**未监听 `GameRootDirChangedMessage`**——`EnsureBuiltAsync` 的 `_built` 缓存不失效，`Invalidate()` 无人调用 → 索引、`GlobalModNames`、命名空间映射永远是旧目录的
2. `ModIndexViewModel.EnsureGameProfileAsync` 是 **one-time 创建**（`FindAsync(-1)` 存在即返回）→ Game profile 的 getmods.php 内容/路径不随目录更新；且原代码 `LoadProfile`（已填 ModLoadInfos）后又 `AddRange(LoadMods(...))` 造成**重复 mod 列表**

**修复**：
- `BrowserIndexService` 注册 `GameRootDirChangedMessage` 接收：`MarkStale()`（标记强制重建 + 清内存缓存/session store，**不删 index.db**）+ 触发 `EnsureBuiltAsync` 重建（重新解析新目录 getmods.php → 命名空间映射/索引/GlobalModNames 全量更新）
- `EnsureBuiltAsync` 接入 `_forceRebuild`（目录切换后即使 index.db 存在也走全量重建）；`_indexedRootDir` 记录构建时目录——配置重载的重复消息（AppConfig setter 已跳过等值赋值）不会误触发重建，启动 Restore-from-disk 秒开不受影响
- `SemaphoreSlim _buildGate` 串行化索引构建，杜绝切换目录与进行中重建的竞态
- `ModIndexViewModel.EnsureGameProfileAsync` 改为**校验并刷新**：Game profile 的 Path/Content 与当前 getmods.php 不一致时更新并重载 `ModLoadInfos`（顺带修复重复 AddRange bug）
- 既有接收方（ResourceManager/ProfileTool/ImageTools/ModDatabase 懒加载失效）确认工作正常

**验证**：build 0 错误；全量 805/805。⚠️ 待 GUI 手动验收（切换目录 → 数据浏览器/Mod 树显示新目录数据）。

---

## R34：Raw Data 审计视图（分组 + 类型化渲染 + 统一引用解析）| 2026-08-06

**背景**：对照 center 可视化（`BuildDetail`）与全字段解析（`BuildRawDataTable`）发现三个问题——① 两条平行数据视图无连接，visualizer 条件过滤漏掉的字段**无任何提示**（静默隐藏）；② 引用解析双路径（Detail 走 `LookupRef<T>`、Raw Data 走 `LookupSubject`）可能不一致（round30 用户反馈）；③ Raw Data 不分 `FieldGroupMetadata` 组、无类型化渲染。

**方案**：Raw Data 升级为**分组审计视图**（`VisHelperService.RawData.cs` 新增 partial）：

- **按 `FieldGroupMetadata` 分组**（Core 新增 `GetSections`，与 KeyValue 编辑器同源）：组头条 = 组名 + `N 字段 · M 有值` 统计；未映射类型回退默认组
- **类型化行渲染**：bool 颜色编码（保留 `0/1` 原文）；引用列**逐段解析为可点击徽章**（绿=已解析 Subject + P6 hover 预览 + Ctrl+Click 跳转 / Ctrl+RMB Peek，琥珀=未解析警示保留原文）；文本 100 字符截断 + hover 全文
- **统一引用解析入口** ⭐：Raw Data 引用段改走 `IReferenceResolver.LookupRef<T>`（`MakeGenericMethod`，Detail 徽章同路径）——彻底移除 `LookupSubject` 依赖，两处解析不可能再不一致
- **Expander 头带审计统计**：新 API `BuildRawData(entity)` 一体化（头 + 折叠体），标签 `原始数据 (24 字段 · 12 有值 · 2 个引用未解析)`；`ComputeRawDataStats` 纯统计
- **24 个 visualizer** 调用点统一替换为 `root.Children.Add(_vis.BuildRawData(<entity>))`（96 行删除，AttackMode 的 `rawContent` 间接层消除）
- 新增 3 组资源键（`Vis.RawFields` / `Vis.RawUnresolved` / `Vis.RawOriginal`，en/zh/en-us）

**测试**：+6（`RawDataTableTests`：分组顺序 / 引用徽章走 LookupRef 干净 id / 未解析琥珀 / 统计 / 标签格式 / 一体化结构）；**805/805**。

**附：测试稳定性修复** ⭐ 全量并行偶发失败根因：`KeyValueEditorFieldExplanationTests` 写入共享 `Application.Current.Resources["Services"]`，其他 UI 测试经 `GetServices` 读取 → xUnit 类级并行互相污染。修复：EntityEditor.Tests 加 `[assembly: CollectionBehavior(DisableTestParallelization = true)]`（共享 headless Avalonia 单例下串行确定性）。基线 worktree 对照：HEAD 14 次全量 0 失败；修复后 8/8 稳定。

**验收**：build 0 错误；全量 8 次连跑 805/805 无偶发失败。详见 [test_round34_summary.md](testround/test_round34_summary.md)。

---

## 播放器 v2.44：受伤存档重启必崩修复（接管序列化——LSO 引用全展开） | 2026-08-05

**问题**：角色受伤后存档，重启必崩（`m_fDate not found on Number`，Ruffle issue #1069）。
**根因**：Ruffle（nightly 0.6.0-nightly.2026.8.4）反序列化 AMF3 引用有 bug——`deserialize_value`
把对象/向量/字典/数组的引用解析为 `Amf3ObjectReference` 后落入 `_ => Value::Undefined`；
ECMAArray 分支只遍历 associative、dense 元素被丢弃。受伤后存档大量对象被引用序列化 →
Ruffle 读回 undefined/Number → 游戏访问 `.m_fDate` → 崩溃。

**方案**：SWF 加载前**接管序列化**——`localStorage.getItem` 拦截，存档 LSO 解析 → 引用全部
展开为内联 → 重编码为无引用字节流，Ruffle 读取时无引用可崩。

- 新增 `NeoEditor.Player.Core/Web/lso-expand-web.js`：无依赖浏览器版 AMF3/LSO 解析+重编码器
  （u29/traits、ECMAArray/StrictArray、Vector*/Dictionary、字符串/对象/traits 引用、环检测），
  与 `player-tools/lso-expand.js`（node 原型）逐字节一致
- `host.html`：getItem 包装（key 含 `nsSGv1` 返回展开版本，失败回退原始值，/__log 诊断）
- 解析器自查修复：Integer 解码误用字符串奇偶规则；Dictionary 编码漏写独立 weak 字节
  （len>0 dict 被解析成引用 → 42KB 累计错位）
- 验证：崩溃档 + 3 备份档往返成功，4312 叶子值逐叶子对比零不一致；沙箱端到端测试 6/6；
  `Player.Core` 构建 0 错误。**待用户实机验证**

---

## 追修：dirty 计数 / 字段级高亮 / 旧标记升级 / .php 格式 / XML 差异对比 | 2026-08-04

✅ **五项验收缺陷修复**（构建 0 错误、645/645 测试通过）：

### 1. 外部 "N dirty" 显示 1（Home / Mod 数据库 / Profile / 工作区历史）
- **根因**：四处外部视图按"mod 布尔"计数（`HasUnsavedCommandsAsync`/`HasPendingExportsAsync` 按 mod 判一次），一个 mod 内改 N 行永远显示 "1 dirty"
- **方案**：按**实体数**计数——新增 `GetDirtyEntityIdsAsync(modId)`（pending_export ∪ WAL 窗口，ModId=-1 追加 `("game",0)` 目标）与 `CountPendingExportsAsync`（distinct EntityId）；HomePage / ModDatabase / ModIndex / WorkspaceHistory 全部改走实体计数，文案改 "N unsaved edit(s)"（"⚠ 3 dirty" = 改了 3 个实体）

### 2. 字段级 dirty 全部字段都亮（DataTable + Value Editor）
- **根因**：KV/XML 编辑路径、WAL 恢复、pending_export 恢复一律写 `(eid, "*")` 通配 → 转换器/KV `IsEditedField` 把该实体所有可改字段都标"已修改未导出"
- **方案**：
  - `IEditorCommand` 新增 `GetEditedCells()`（EditCell/BatchEdit 携带精确列名），KV/XML 消息按 `EditRecord.ColumnName`、WAL 恢复按命令逐列写入 `EditStore.EditedCells`（Add/Delete 等无列名命令保留 `"*"` 兜底）
  - `pending_export` 增加 `ColumnName` 列（唯一索引改为 ModId+EntityId+ColumnName；`RunEditorDbMigrations` 老库 ALTER + 换索引），自动保存按列持久化、重启按列恢复——字段级高亮跨重启成立
  - `SearchableDataGrid`：提交时按 `[Column]` 名入栈（原用 Header=属性名，与转换器列名不一致），并删除整行黄底（与字段级设计冲突，提交重渲染时单元格级高亮即时生效）
  - 主键锚点改为"行有任意编辑即亮"（原仅 `"*"` 通配行生效）；KV `ApplyChanges` 立即置 `IsEdited`

### 3. 旧标记一次性自动升级（无列名的历史 pending_export 行 → 字段级）
- **背景**：修复前写入的 `pending_export` 行没有 `ColumnName`（迁移补列后为 NULL），恢复时整行黄；列信息在库里已不可恢复
- **方案**：打开工作区时对旧行做一次性升级——实体未导出过，游戏 XML 仍持有原始值，用 `ImportEntities` 解析源 XML → `DiffEngine.ComputeChangedColumns(原始值, DB 当前值)` 还原精确列名，写回 per-column 标记并删除 NULL 行（自愈：下次打开无旧行）。文件缺失 / 解析失败 / 实体缺失一律回退保留 `"*"` 整行标记，绝不丢脏状态
- 真实数据验证：3 个旧实体全部匹配并还原为 `[fDamageBlunt]`（与实际修改列一致）；新增 `RemovePendingExportEntityAsync` + `DiffEngine.ComputeChangedColumns` 测试

### 4. .php 保存格式：单行、无空格、无回车
- **根因**：`GenerateModsPhp`/`GenerateImagePhp` 用 `AppendLine` 输出带 `\r\n` 的多行内容；而游戏按 URL query-string 解析 .php，`getimages.php` 旧备份里 2325 个空格曾导致整份图片清单加载失败（游戏根目录能用的 `getimages.php` = 单行、CR=0/LF=0/SP=0）
- **方案**：两个生成器改为**单行纯 `&` 连接**（`nRows=N&strModName0=X&strModURL0=path&...` / `nRows=N&nCols=2&strImageURL0=...`），mod 名/路径 trim 首尾空白；写盘（`ImageOrchestrationViewModel.SaveAsync` 的 `File.WriteAllTextAsync`）默认 UTF-8 无 BOM 保持不变
- 验证：生成内容 CR=0 LF=0 SP=0，且 App 自身 `ParseModsContent` 往返解析无损

### 5. XML 编辑"差异对比"对 dirty 项无变化
- **根因（四处）**：① diff "旧侧"取文档打开时的内存快照——实体在打开文档**之前**已被编辑（或重启恢复的 pending 标记），快照=编辑后状态 → old==new；② 追修初版把 `XmlContent`（XML 编辑内容）也初始化成磁盘原始值；③ ToggleButton 同时绑 `IsChecked`(TwoWay) 与 `Command`——Avalonia 先 Toggle 再执行 Command，`IsDiffView` 被双重翻转回 false，差异视图根本打不开；④ `ResolveOriginalXml` 的 hasEdits 门控依赖 `DirtyEntities`/`ActiveEditStore` 运行时状态（auto-save 清空 dirty 后若 EditStore 未命中 → 直接 fallback 当前值 → 无差异），且**切换实体时 diff 文档不刷新**（`IsDiffView` 残留 + `OnEntityChanged` 不重算 → 显示上一个实体的对比）
- **方案**：① diff 旧侧从磁盘游戏 XML 解析原始实体（`IXmlParser.ImportEntities` + EntityId 匹配）重建片段；② `XmlContent` 始终=当前实体值；③ ToggleButton 去掉 Command，改 `partial void OnIsDiffViewChanged` 驱动 `RefreshDiff`（单一数据源）；④ **去掉 hasEdits 门控**——无条件磁盘对比（磁盘原始==当前则短路返回当前=空差异；解析失败回退当前），并在 `OnEntityChanged`/`RefreshXml` 时重算 `_originalXml` 并同步 `RefreshDiff`
- 真实数据验证（用户 DB 4 个 pending 实体）：2 个磁盘==DB（编辑已撤销，空差异正确）、2 个 `[fDamageBlunt]` 差异可还原；`EntityEditorDocumentDiffTests` 锁定（dirty：XML 内容=当前、diff 旧侧=磁盘原始；clean：空差异；开关 hook 刷新文档）

### 6. XML 编辑滚轮导致页面放缩
- **根因（追修订正）**：**并非缩放**——AvaloniaEdit 12.0.0 源码确认无任何滚轮缩放逻辑（`Ctrl+Wheel`/捏合缩放不存在）。真实原因是 **DockPanel 布局**：XML tab 的 `DockPanel` 只有**最后一个子元素**（XmlDiffView）填满剩余空间，可见的 XmlEditor（倒数第二个）宽度取 Auto → "下半部分 width 小、看起来像放缩"
- **方案**：XML tab 改用 `Grid RowDefinitions="Auto,*"` + 内层 Grid——两个编辑器（XmlEditor / XmlDiffView）都默认填满、按 `IsDiffView` 切换可见性；保留 Tunnel 事件拦截（防御性，AvaloniaEdit 当前无缩放但未来版本可能有）

### 7. 加载时 DB vs XML 对比，校正 pending 标记（用户建议）
- **背景**：实体编辑后又撤销/改回 → pending_export 标记残留 → 重启后仍显示 dirty，但 DB 与磁盘 XML 实际相同
- **方案**：`RestorePendingExportsAsync` 恢复后新增 `ValidatePendingMarkersAsync`——对**所有** pending 实体（按文件缓存解析一次）做磁盘 XML 原始值 vs DB 当前值 diff：
  - 有差异 → 按**精确列**重建标记（同时完成 legacy NULL 行升级，替换原 `UpgradeLegacyPendingMarkersAsync`）
  - **无差异 → 清除失效标记**（内存 EditStore + DB `RemovePendingExportEntityAsync`）
  - 解析失败 / 新建实体（不在 XML）/ isNew → 保守保留
- 真实数据验证：用户 4 个 pending 实体 → 2 个 `DIFF-EMPTY` 将被清除、2 个 `[fDamageBlunt]` 保留为列级

### 8. 已知限制（上升讨论）：跨 profile 保存覆盖
- **现象**：dirty 按 profile 隔离（`_dirtyByProfile`，切换 profile 时 `ClearDirtyEntities` + WAL 兜底恢复，**不泄露**）；但 `SaveAllAsync` 保存当前 profile 的 dirty 集合到**同一个 game.db**——同一实体在两个 profile 分别编辑时，**后保存者覆盖先保存者**（"最后编辑者生效"）
- **结论**：涉及架构设计（DB 单份实体 vs 多 profile 工作区），**上升讨论**，本期不改代码；方案讨论见 Docs/41 增补 I（冲突检测 / WAL 隔离 / 编辑层三方案）

### 9. XML 编辑器 WordWrap（用户反馈）
- 三处 XML 编辑器（`EntityEditorView` 的 XmlEditor、`XmlDiffView` 的 Old/NewEditor）加 `WordWrap="True"`——长行自动换行、不再横向滚动（配合第 6 项的 Grid 布局修复，"放缩"视觉问题彻底消除）

### 10. 多 profile 隔离（B+C 实施，2026-08-04）
- **B：WAL 按 profile 隔离**——`GetPersistenceTarget` 对单 mod profile 从 `("mod", modId)` 改为 `("profile", profileId)`（两个单 mod profile 含同一 mod 时命令日志不再串扰）；`MigrateWalTargetAsync` 迁移遗留命令（未保存的移动到新 target 并重排序号，已保存的丢弃——值已在实体表）
- **C：per-profile 编辑覆盖层**——新表 `profile_edits`（ProfileId/EntityId/ColumnName/RawValue/IsNew/IsDeleted/EntityType/ModId）：
  - **保存**：`PersistEntitiesAsync` 不再写共享实体表——对每个 dirty 实体读实体表基线（`LoadBaselineAsync`）→ `DiffEngine.ComputeChangedColumns` 逐列 diff → 写覆盖层（新建→IsNew 标记+全列；删除→IsDeleted 标记）；**两个 profile 编辑同一实体互不覆盖**
  - **加载**：`ApplyProfileOverlayAsync`（ComputeMergeAsync 后）——列覆盖应用、IsNew 重建入视图（绿）、IsDeleted 移除
  - **导出**：视图 Save & Export 后 `AdvanceBaselineAsync`（实体表=导出状态 + 删除实体移除）+ `ClearProfileEditsAsync`（清本 profile 覆盖）；`ExportModAsync`（MCP/CLI）合并当前 profile 覆盖（`ApplyProfileOverlay`）
  - 实体表成为**共享基线**（导入/导出写入）；覆盖层=各 profile 的编辑
- 测试：`SaveAll_NewEntity_WritesIsNewOverlay_NotEntityTable` / `SaveAll_ExistingEntity_WritesOnlyChangedColumns` / `SaveAll_DeletedEntity_WritesIsDeletedOverlay` / `DiscardAsync_ClearsOverlay_And_Dirty`；全量 **653/653 通过**
- 已知边界：游戏基础数据（ModId=-1）编辑仍走 `("game", 0)` 共享 WAL（merge editor 聚合场景为主）；CSV 导入经 HostService 命令 → 自动进覆盖层

### 11. 关联组件修复（覆盖层语义贯通，2026-08-04）
- **读路径合并覆盖**：新增 `IHostService.MergeProfileOverlay`（列覆盖应用 + IsNew 重建 + IsDeleted 移除），接入四处直接读实体表（=基线）的路径——`SearchEntitiesAsync`（搜索/MCP SearchAllTypes）、MCP `EditorTools.GetEntityByTypeAsync/GetAllByTypeAsync`、CLI `CliCommandHandler` 同两方法、MCP `EntityResourceProvider`（entity:// 资源）——否则看不到当前 profile 的编辑
- **DiscardAsync 清覆盖层**：原只清内存 dirty → 覆盖层残留导致编辑重启复活；现同时 `ClearProfileEditsAsync`
- **ExportModAsync 统一合并**：删除专用 `ApplyProfileOverlay`/`IsOverlayRelevant`，改用通用 `MergeProfileOverlay` + mod 过滤
- `GetDiffAsync`（cache vs 基线）语义天然正确，无需改；`ModEntityStats`（HomePage 实体计数）读基线统计，IsNew 未计入（可接受）

---

## 字段级 diff / AI Chat 渲染 / MCP 评审实施 / 验收修复 | 2026-08-04

✅ **四块落地**（构建 0 错误、全量测试通过）：

### 1. DataTable 字段级 diff（含主键锚点）
- 行级黄 → **单元格级**：`CellEditedHighlightConverter` 统一包装各列 CellTemplate（`EntityId + Converter + 列名` → 查 `EditStore.EditedCells`，含 `"*"` 通配；DataGrid 重载/滚动时重算）
- **主键锚点**：`key:` 前缀参数——行有编辑 → 主键单元格同步亮黄（主键不可改、不会自己亮，作行定位锚点）
- 行级保留：覆盖灰 / 新建绿；编辑行行背景 null
- 取舍：CheckBox 列（bool）不参与单元格高亮（主键锚点仍可定位）

### 2. AI Chat
- **默认 Markdown 渲染**：assistant 气泡 → `MarkdownRenderer`（LiveMarkdown 1.9.2 新包引用；`MarkdownBuilder` 随 Content 流式同步）
- **MD 主题修复**：`MarkdownTheme.axaml` 无条件 Dark+ → **ThemeDictionaries**（Light 白底深字 / Dark 原样）——白主题黑底灰字消除
- **复制按钮**：气泡头部 📋 → `CopyCommand`

### 3. MCP 工具评审实施（AI 评审建议，16 → 19 工具）
- `BatchEditEntity`：多字段一次编辑（原子 undo、校验前置）
- `FindReferencingEntities`：反向引用（删除前查"谁引用了我"）
- `SearchAllTypes` query 改可选（空 query + filtersJson 纯过滤）
- `DiscardChanges`：清除单个实体暂存标记
- 测试：MCP +5（3 个 BatchEdit 错误路径 + DiscardChanges + 工具数断言更新）

### 4. 验收修复
- **KV 编辑后 DataTable 不刷新/无高亮根因**：`RefreshEntityEditorMessage` 接收的 `if (ReadOnly) return;` 移除（ReadOnly 只 gate CRUD 不 gate 刷新；底部 DataTable 实例 ReadOnly=true）
- **Debug 工具语义**：Command Log 空时提示"自动保存已清 WAL（正常）"；Session Dirty 新增 pending_export 摘要
- **只读值 wrap**：KV 只读 TextBlock 加 `TextWrapping="Wrap"`

---

## Docs/41 增补：pending_export 持久化 + 验收四修 | 2026-08-04

✅ **"未导出"状态持久化 + 四项验收修复**（653/653 测试通过）：

### pending_export 表（新语义闭环）
- **问题**：新语义下 dirty = "已存 DB 未导 XML"，但自动保存清 WAL 后重启无任何"未导出"指示（EditStore 会话级）
- **方案**：新表 `pending_export`（ModId/EntityId/IsNew，唯一索引）——自动/Quick 保存后 upsert（IsNew 取 EditStore），Save & Export 确认后清除，重启加载时恢复进 EditStore（黄/绿高亮 + ⚠ 徽章），老库经 `RunEditorDbMigrations` 自动建表
- ⚠ 徽章四处（ModDatabase / HomePage / ModIndex / WorkspaceHistory）= `HasUnsavedCommandsAsync || HasPendingExportsAsync`
- 恢复**不标 dirty**（已落库；dirty 仅表 WAL 窗口）
- 测试：`WorkspacePendingExportTests` +2

### 验收四修
1. **KV 只读重影**：`IsKey || IsMeta` 字段 CtrlType 强制 ReadOnly → 只渲染 TextBlock
2. **XML 精简**：隐藏 entity_id/file_path/mod_id 列与 `<?xml...?>` 声明行；主键保留显示，修改后 alert「Primary key cannot be changed」且不生效（其余字段正常应用）
3. **列头回退**：技术名 + 说明 tooltip（枚举 ≤6 时 tooltip 追加值域）
4. **Debug Dock 移位**：Command Log / Session Dirty 两工具 Left → Bottom（DataTable 旁）

---

## Docs/41 增补：字段级 diff / 只读保护 / Debug Dock / 列头说明 / Welcome 本地化 | 2026-08-04

✅ **五需求落地**（635/635 测试通过）：

### 1. Value Editor 字段级 diff（替代"未保存"alert）
- 删除 R09 黄横幅（自动保存后"unsaved changes / Press Ctrl+S"纯属误导）
- 每字段名旁新增 **黄色 ● 标记** = "本会话已修改、尚未导出"（数据源与 DataGrid 单元格高亮一致：`EditStore.EditedCells`，含 KV/XML 路径的 `"*"` 通配）；自动保存不清除，仅 Save & Export 后清除（订阅 `SaveCompletedMessage` 重算）

### 2. 只读保护（KV + XML）
- KV：编辑器元数据（EntityId/ModId/FilePath，`IsMeta`）与主键（`IsKey`）均只读显示（`IsReadOnly`）
- XML：`ApplyXmlToEntity` 跳过受保护列（`IsProtectedColumn`：IEntity 元数据 + `id`/`nID` 主键）

### 3. XML Diff 视图
- EntityEditor XML Tab 顶部新增 **Diff 切换按钮**（`EV.XmlDiffToggle`）：编辑模式 ↔ 行级 diff 视图（`XmlDiffView`，左旧右新 + DiffPreviewTrack）；旧侧 = 会话开始快照（`_originalXml`），新侧 = 当前 XML

### 4. Debug Tool Dock（仅 DEBUG 构建）
- `#if DEBUG` 注册两个 Left Dock 工具：**Debug: Command Log**（WAL `command_log` 表最近 200 条 + Refresh）与 **Debug: Session Dirty**（DirtyEntities + EditStore 实时，订阅 `DirtyStateChanged`）

### 5. DataTable 列头显示字段说明
- 列头主文本 = 字段说明（Docs/38 描述）优先，技术名兜底；`MaxWidth=180` + 省略号（完整文本在 tooltip）
- 枚举且选项 ≤6 时，tooltip 追加"可选值: A / B / C"（值域仅在少量枚举时有意义）

### 6. Welcome 页快捷键订正 + 中文本地化
- 快捷键表更新为新语义（Ctrl+Shift+S 导出 / Ctrl+S 当前 tab / Ctrl+Z·Ctrl+Shift+Z·Ctrl+Y / Ctrl+E 等）
- `Welcome.Title` / `Welcome.Loading` / `Welcome.Shortcuts` 三语言 resx；"NeoEditor Session"/"Usage"/"Loading…" 硬编码文本一并本地化

---

## 保存工作流收敛 + 非侵入式新手引导（Docs/41） | 2026-08-03

✅ **消除"保存"概念**：编辑/增删自动落 DB（无感缓存），黄/绿高亮表达"未导出"，用户唯一显式动作 = Save & Export。架构零改动（R24/R26 契约不变），全部落在 UI 层。

### 自动保存（P1，强度 0）

- 事件驱动：监听 `WorkspaceSession.DirtyStateChanged`（所有编辑入口的收敛点：KV / XML / Add / Delete / Undo / Redo / CSV 导入）→ 800ms 防抖 → `SaveAllAsync` 落库；`IsLoading` 作 WAL 恢复期抑制（防恢复命令被立即落库）
- 高亮语义改为"已缓存、未导出"：行高亮数据源 `DirtyEntities` → `EditStore` 派生（自动保存清 dirty 后高亮保留）；修改淡黄 / 新建淡绿（颜色原有，`SearchableDataGrid`）
- 高亮清除只发生在 Save & Export 确认写盘后（原 `ShowMergeSavePreviewAsync` 清理块）；`QuickSaveAsync` 与 `EntityEditorDocument.SaveDocument` 不再清高亮
- 工具栏删除 Quick Save 按钮；`AutoSaveTimer` 定时器降为兜底（`AutoSaveInterval` 默认 60s）

### 快捷键

- `Ctrl+S`：保持原语义（当前 tab 落库，R11）
- `Ctrl+Shift+S`：新增 `SaveAndExportRequestedMessage` → 完整 Save & Export 预览（`DataTableViewModel` 注册，无 dirty 守卫——自动保存清 dirty 后导出仍可达）

### 新手引导（P2/P3，强度 1+2，全部一次性/可关闭/可重置）

- 空状态横幅 → **三步卡片**（① 添加实体 ② 左侧编辑字段 ③ Save & Export）+ 自动保存/高亮图例 + `[不再显示]`（持久化 `AppConfig.EmptyModHintDismissed`）
- `IOnboardingHintService`（DI 注入，状态存 `AppConfig.DismissedHints`）：首次导出成功 toast；首次尝试编辑 Game 基础数据时引导"Copy Row 复制到你的 Mod"（挂在 cell 编辑只读拦截分支）
- Settings 新增「重置新手提示」按钮

### 字段级文档可见化（P4）

- KV 编辑器每行新增 `?` 图标（有描述时显示），ToolTip 挂图标（描述数据源为 Docs/38 生成的 `field_descriptions.json`）
- AddRowDialog XML 路径选择下新增说明行（实体按 XML 文件分组、游戏按文件名叠加）

### 测试

**648/648 通过**（无新增单测——App 层无测试项目，HintService 语义简单，由手工验证覆盖）

---

## Ruffle 游戏运行器 P1（Docs/40） | 2026-08-03

✅ **新增「用 Ruffle 启动」**：编辑器以进程方式通过 Ruffle（用户自装）运行游戏 SWF，并捕获运行日志——第三方扩展模式，未检测到 Ruffle 时不显示任何新 UI。

### 检测（Core）

- `RuffleLocator`（纯静态）：优先级 配置路径（P2 预留）→ `RUFFLE_PATH` 环境变量 → PATH 中的 `ruffle`/`ruffle.exe`；找不到即功能禁用
- `RuffleOptionsBuilder`（纯静态）：SWF 定位（`NEOScavenger.swf` 优先，仅一个 `*.swf` 兜底）+ 命令行构建：`--player-runtime air`（AIR 模拟）、`--base file:///游戏根目录`（SWF 相对路径解析）、`--cache-directory`（重定向 Ruffle 日志文件到编辑器 logs）、`--filesystem-access-mode allow`、`RUST_LOG` 环境变量

### 运行 + 日志捕获（Infra）

- `RuffleRunnerService`：进程管道重定向 stdout/stderr → 逐行写入 `logs/ruffle-<时间戳>.log` + Serilog 主日志 + `LogLineReceived` 事件；`Exited` 事件上报退出码与日志路径；单实例锁（运行中拒绝二次启动）；`Stop()` 杀进程树
- 日志双通道：stdout 管道（实时）+ `{logs}/ruffle-cache/log/ruffle.log`（Ruffle 官方日志文件，兜底）

### UI（App）

- 工具栏「用 Ruffle 启动」按钮（PlayCircle 图标），`RuffleLaunchVisible`（`!ReadOnly && Ruffle 已检测`）控制显隐；点击 = 启动 / 再次点击 = 停止
- 退出通知「Ruffle 已退出（代码 N）。日志文件：…」（用户主动停止不弹）
- resx 三语言 5 个新键（RuffleLaunch / RuffleNotInstalled / RuffleStarted / RuffleLaunchFailed / RuffleExited）

### 测试

**635/635 通过**（+18）：`RuffleLocatorTests` 7（env 变量/优先级/PATH/空值）+ `RuffleOptionsBuilderTests` 6（SWF 定位/参数/URL 编码/无 SWF）+ `RuffleRunnerServiceTests` 5（管道捕获/退出码/单实例拒绝/Stop/无 SWF 拒启，用 cmd/powershell 桩进程驱动，无需安装 Ruffle）。

### 附带修复

- `HostServiceSearchTests.cs` 恢复被误删的 `using NeoEditor.Data.Model;`（R31 结构化搜索测试编译错误，`AttackType` 仍在该命名空间）

---

## MCP 薄弱点完善 + Search 结构化搜索（R31） | 2026-08-03

✅ **修复 3 个薄弱点并增强搜索**：MCP GetDiff 变为真实字段级 diff、Save 工具如实回传结果、修复命令双重执行 bug；新增 Undo / Redo / Publish / ExportMod 四个 MCP 工具；`SearchEntitiesAsync` 新增结构化请求（多表选择 + 类型化字段过滤 + 分页 + 排序）；CLI 同步 4 个命令；工具注册反射去重。

### 基础修复（薄弱点）

- **`HostService.GetDiffAsync` 字段级**：从占位（单条 `EntityState/Modified`）改为经 `DiffEngine.ComputeDiff(dbVersion, cachedVersion)` 的真实字段级 diff（Modified / Added / Removed 按 `[Column]` 属性逐项）。顺带修复 `FindEntityInDbSet` 反射调用——EF `FindAsync` 返回 `ValueTask<T>` 且反射不填充可选参数（CancellationToken），旧代码静默抛异常被吞，DB 版本始终查不到
- **`DiffEngine.ComputeDiff`**：引用字段按 `ReferenceText.GetRawString` 规范化比较 / 序列化，不再走损坏的 `ReferenceList.ToString()` "[a, b]" 格式——未变的引用字段不再误报
- **双重执行 bug**：`HostService.ExecuteAsync/ExecuteBatchAsync` 曾先 `command.Execute()` 再 `scope.Execute(command)`（内部又执行一次）。现在 scope 存在时只走 `scope.Execute`，集合回调只触发一次
- **MCP `Save` 工具**：回传真实 `SaveResult`（savedCount / savedEntityIds / remainingDirty / note），不再无条件假报 `{saved:true}`

### Search 结构化搜索

- Core 新增 `EntitySearchRequest` / `EntityFilter` / `FilterOperator` / `EntitySearchResult`；`IHostService.SearchEntitiesAsync(EntitySearchRequest)` 带默认接口实现（委托旧 4 参方法），6 个测试桩零改动
- `HostService` 实现：多表选择（`EntityTypes`）、类型化过滤（字符串 contains/equals/前缀/后缀、数值大小比较、布尔、枚举名或数值、引用字段 raw text，AND 语义）、列排序（含 `IEntity` 基类属性如 Subject，null 排后）、分页（offset + total + truncated）
- MCP `SearchAllTypes` 新增 `entityTypesJson` / `filtersJson` / `offset` 参数，返回 `total` / `truncated`

### 新增 MCP 工具（12 → 16）

- `Undo` / `Redo`：操作 MCP 专属 scope 撤销栈（机制早已注册，只缺协议面）
- `Publish(commit)`：SaveAll + Export 一步到位（commit=true 时直接写 XML 文件）
- `ExportMod(modId, commit)`：单 mod 导出预览 / 提交，对齐 UI"预览 → 确认 → 写文件"流程

### CLI 同步

- 新增 `undo` / `redo` / `publish [--commit]` / `export-mod <modId> [--commit]` 命令，走 `_hostService`（scope "cli"）

### 反射去重

- 新增 `EditorToolRegistry`（工具枚举 + schema 构建单一来源），`McpServerHost` 与 `McpToolExecutor` 共用，消除双份反射实现

### 测试

**617/617 通过**（+36）：`HostServiceSearchTests` +11（多表/类型化过滤/分页/排序）、`HostServiceGetDiffTests` +3（字段级 Modified/Added/Removed）、`HostServiceCommandTests` +2（双执行回归 + 无 scope 单次执行）、`DiffEngineTests` +3（引用 raw text 比较/防误报/分隔符）、MCP 测试 +8（4 新工具 + SearchAllTypes 新参数）、CLI 测试 +12（parser + handler）。新增 `GameDbReferenceSerializerCollection` 将设置静态序列化器的测试类串行化。

---

## CRUD 全路径收束到 HostService（R24 合规审计 + 修复） | 2026-08-03

✅ **审计 + 修复**：增删改查数据操作全部收束到 `IHostService` 单一管道，清除 4 条绕过 HostService 的实体数据写路径（CSV 导入、EntityEditor 文档保存、查找替换、XML 导出写入）。

### 修复的旁路

- **`EntityEditorDocument.SaveDocument`**（EntityEditor 插件）：删除直接 `GameDbContext` 写库（`db.Update/Add` + `SaveChangesAsync`），改为 `IHostService.AddEntityToCache` + `SaveAsync`——走 pre-save hooks → DbRepository upsert → 脏清理的完整管道；并校验 `SaveResult.SavedEntityIds`，未真正保存时如实报告并保留脏状态。插件 / 工厂改注入 `IHostService`（不再注入 `IDbContextFactory<GameDbContext>`）
- **`ModDatabaseViewModel.ImportCsv`**：删除直接 DbSet `Add` / `SaveChangesAsync` 批量 upsert，改为按字段 diff 构造 `BatchEditCommand` / `AddEntityCommand`，经 `_hostService.ExecuteBatchAsync(commands, "csv-import")` + 逐实体 `_hostService.SaveAsync()`（未注册 scope → 无 undo/WAL，但 hooks / 脏标记 / 缓存 / 事件全部生效）
- **`FindReplacePanel`**（DataViewer）：查找替换不再直连 `CommandHistory.Execute`，新增 `HostService`/`ScopeId` 注入点改走 `IHostService.ExecuteAsync`（无 HostService 上下文时回退原路径）；`ModGameDataTabsView.ShowFindPanel` 注入 `ViewServices.HostService` + `_scopeId`
- **XML 导出双轨**：`IHostService` 新增 `CommitExportAsync(IEnumerable<RowDiff>)` 作为**唯一 mod XML 写入口**；`ModGameDataTabsView` 两处 `File.WriteAllTextAsync`（合并保存导出 / XML 导出确认）全部改走该 API

### 测试

**581/581 通过**（+4）：`CommitExportAsync_Writes_Confirmed_Xml_Files`（Infra）+ `EntityEditorDocumentSaveTests` 3 个（走 HostService 缓存 + SaveAsync / 非脏 no-op / 脏集合缺失时如实报告跳过）。

### 有意保留（未收束，非 R24 实体数据契约范围）

- `WorkspacePersistenceService.TakeSnapshotAsync` WAL 快照直写 game.db（旧 WAL 机制固有，随旧管线退役）
- WAL 回放 `cmd.Execute()`（重启恢复机制）
- 读路径直连 `IDbContextFactory<GameDbContext>`（BrowserIndex / DataExport / Search / DataLoader 等）与 EditorDbContext 元数据（ModInfos / ProfileInfos）

---

## AI 配置 Provider 列表 + 每模型可选 Provider | 2026-08-01

✅ **AI 配置从单一扁平结构改为 Provider 列表**：Endpoint + ApiKey 按供应商分组，对话 / 嵌入 / 图片三个模型各自选择 Provider（原因：一个 API 供应商不一定能提供所有模型）。

### 数据模型（Core）

- 新增 `AiProviderConfig`（`Id` / `Name` / `Endpoint` / `ApiKey`）+ `AiProviderResolver`（纯静态解析：provider → 环境变量 → 内置默认；无任何 key → 返回 null 禁用态）
- `AppConfig`：删除 `AiEndpoint` / `AiApiKey`；新增 `List<AiProviderConfig> AiProviders` + 每模型 `AiModelProviderId` / `AiEmbeddingProviderId` / `ImageProviderId`（空 = 第一个 provider）。模型名仍全局（`AiModel` / `AiEmbeddingModel` / `ImageModel`）

### 落盘加密 + 迁移（App）

- `ConfigService`：`SaveAsync` / `LoadAsync` 遍历 `AiProviders` 逐项加密 / 解密 `ApiKey`（DPAPI `ConfigValueProtector`）
- `LoadAsync` 旧配置迁移：无 `AiProviders` 但存在旧顶层 `AiEndpoint` / `AiApiKey` 时，合成一个 `Id="default"` 的 Provider

### DI 组装

- AiChat：删除 `OpenAIClient` 单例，`ChatClient` / `EmbeddingClient` 各按 `AiModelProviderId` / `AiEmbeddingProviderId` 经 `AiProviderResolver` 建 client——**对话与嵌入可用不同供应商**
- `ImageGenerationService`：按 `ImageProviderId` 解析 endpoint / key / model
- 环境变量 `OPENAI_*` 仍是 fallback（无 provider 场景）；无任何 key 时 AI Chat 保持禁用态不崩溃（沿用 9D v1.9 修复）

### Settings UI

- Endpoint / API Key 两行 → **Provider 列表编辑器**（每行 Name / Endpoint / API Key 密码框 + 删除按钮 + 添加按钮）
- 三个模型各加 **Provider 下拉**（含「自动（第一个供应商）」选项）+ 模型名输入
- resx 三文件增删键；`Settings.AiConfigNote` 更新

### 测试

**408/408 通过**（+14）：`AiProviderResolverTests`（Core 11）+ `ChatServiceProviderTests`（AiChat DI 接线 2）+ `ConfigServiceEncryptionTests` 迁移（1）；`AppConfigTests` / 加密测试改为 provider 形态。

---

## AI Chat 无配置启动崩溃修复 | 2026-08-01

✅ **未配置 API Key 时 GUI 启动崩溃已修复**：`ApiKeyCredential("")` 抛 `ArgumentException`（`Value cannot be an empty string`）。原则：**无配置 → 禁用 AI Chat，配置后重启应用生效**。

### 根因

`AddAiChatPlugin` 配置读取改 IConfigService 后，apiKey 为空（config.json 无 + 环境变量无）时仍 `new ApiKeyCredential("")` 构造 OpenAIClient → 抛异常，沿 `OpenAIClient → ChatClient → IChatService → AiChatViewModel → DocumentWorkspaceViewModel` 链冒泡，GUI 启动即挂。

### 修复（禁用态降级，不崩溃）

- `AddAiChatPlugin`：apiKey 为空时 `OpenAIClient` 工厂返回 `null`（不再构造 credential）；`ChatClient`/`EmbeddingClient` 工厂相应返回 `null`（`GetService` + null 判断）。key 启动时读一次 → **配置后需重启生效**。
- `IChatService`/`ChatService`：新增 `IsAvailable`（`_chatClient is not null`）；未配置时 `SendMessageStreamingAsync` 返回友好提示而非抛异常。
- `IRagService`/`RagService`：新增 `IsAvailable`；`BuildIndexAsync`/`SearchAsync` 对 null embedding 客户端守卫（顺带修掉 SearchAsync 潜在 NRE）。
- `AiChatViewModel`：新增 `IsAvailable`/`CanSend`/`CanBuildIndex`（`[NotifyPropertyChangedFor]` 联动）；未配置时显示系统提示并禁用发送/建索引。
- `AiChatView.axaml`：未配置时显示 ⚠️ 提示横幅，Send 输入框/按钮绑 `CanSend`，Build Index 绑 `CanBuildIndex`。

### 测试

新增 `ChatServiceAvailabilityTests`（4 测试）：无 client 时 `IsAvailable=false`、`SendMessageAsync`/`SendMessageStreamingAsync` 返回 not-configured 提示；**DI 级回归**——`AddAiChatPlugin` + 空配置 IConfigService 解析 `AiChatViewModel` 不再抛异常且 `IsAvailable=false`。AiChat.Tests 23 → 27。**394/394 测试通过**（390 + 4）。

---

## Phase 9D `--mcp` NRE 修复 | 2026-08-01

✅ **`--mcp` 运行时 NRE 已修复**：`McpServerHost.BuildOptions()` 的 `options.ToolCollection.Add(tool)` 不再抛 NullReferenceException。9D 全部可用，下一步 9E。

### 根因（SDK preview.3）

用 `new McpServerOptions { ... }` 直建 options 时，`ToolCollection`（`McpServerPrimitiveCollection<McpServerTool>`）**为 null**——SDK 只在 DI builder 路径（`AddMcpServer()`）内初始化集合，直建 `McpServer.Create(transport, options, ...)` 不会。故首个 `options.ToolCollection.Add(tool)` 即 NRE。已用官方 `StdioClientTransport` + 真机 exe 复现。

### 修复

- `McpServerHost.BuildOptions()`：显式初始化 `options.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.OrdinalIgnoreCase)`。
- 保留 stdio + TCP 双 transport 设计（不走会硬编码 transport 的 DI builder）。
- `BuildOptions()` 改 `internal` + Mcp csproj 加 `<InternalsVisibleTo Include="NeoEditor.Plugins.Mcp.Tests" />`。
- 新增 `McpServerHostTests`（2 测试）：ToolCollection 非空 + 12 工具全注册 + 每工具名称/描述。Mcp.Tests 22 → 24。

### 真机验证

官方 `StdioClientTransport` 客户端 spawn `NeoEditor.exe --mcp`：握手成功 → `tools/list` 返回全部 **12 工具**（名称+描述） → `tools/call GetModInfo` 返回真实数据（24 实体类型 / 脏状态）。**390/390 测试通过**（388 + 2）。

---

## Phase 9D AI/MCP UI（R28 落地）| 2026-08-01

> ✅ **完成**：`--mcp` NRE 已修复（见上节），9D 全部可用。

### 9D-1 AI Chat 接入 Dock

- `Documents.cs` 新增 `AiChatTool`（Id="AiChat"，Title="AI Chat"）
- `DocumentWorkspaceViewModel` 构造解析 `AiChatViewModel` 装配 `AiChatTool`
- `DocumentWorkspaceView.axaml` RightToolPane 新增 `<Tool Id="AiChat">` + `aiViews:AiChatView`（`xmlns:aiViews`）

### 9D-2 MCP Server 启动路径

- `App.CreateHost(bool mcpMode)` 抽出组合根：GUI（`App.Initialize`）与 `--mcp` 无头模式共用同一 DI（R20）
- `App.EnsureDatabases(IServiceProvider)` 公共化（Editor+Game DB 创建 + 轻量迁移）
- `Program.cs`：解析 `--mcp`（stdio）与 `--mcp-port <port>`（TCP，预留）；MCP 模式**不启动 Avalonia GUI**；`Console.CancelKeyPress` → 优雅关闭；异常走 `Log.Fatal` 文件日志
- **MCP 模式 stdout 纯净**（协议通道关键约束）：
  - `AddSerilogLogging(logToConsole:)` 条件化——MCP 模式禁用 Serilog Console sink
  - DB `AddDbContextFactory` 的 `.LogTo(Console.WriteLine)` 条件化（`if (!mcpMode)`）
- `McpServerHost.RunAsync(int? port, CancellationToken)`：stdio（默认）/ TCP 单客户端（`StreamServerTransport` 桥接，预留）
- ⚠️ **已知 NRE**：`McpServerHost.BuildOptions()` 的 `options.ToolCollection.Add(tool)` 抛 NRE——SDK preview.3 直建 `McpServer.Create` 时 `ToolCollection` CollectionFactory 未初始化。官方 `StdioClientTransport` 真机复现 `ClientTransportClosedException`。**修复方向：改走 `AddMcpServer()` builder 或显式初始化集合。**

### 9D-3 AppConfig AI/MCP 字段 + ApiKey 加密

- `AppConfig` 新增：`AiEndpoint` / `AiApiKey` / `AiModel` / `AiEmbeddingModel` / `ImageModel` / `McpEnabled` / `McpPort`
- `ConfigService`：`AiApiKey` 落盘用 **ProtectedData（DPAPI）加密**（`ConfigValueProtector`，`System.Security.Cryptography.ProtectedData` 包）；`LoadAsync` 解密回明文；兼容旧明文 key（捕获 `CryptographicException` + `FormatException`）

### 9D-4 配置读取改为 IConfigService

- `AiChat/ServiceCollectionExtensions`：OpenAIClient/ChatClient/EmbeddingClient 改读 `IConfigService.Config`（config.json 优先 → `OPENAI_*` 环境变量 fallback → 内置默认）
- `ImageGenerationService`：注入 `IConfigService`，apiKey/endpoint/imageModel 同上优先级

### 9D-5 SettingsPage "AI & MCP" 分组

- `SettingsPageView.axaml`：Endpoint / API Key（`PasswordChar` 掩码）/ Chat / Embedding / Image 模型 / MCP 开关 + 端口 + 优先级提示
- `SettingsPaneViewModel`：新增 `DisplayAiEndpoint` / `DisplayAiApiKey` / `DisplayAiModel` / `DisplayAiEmbeddingModel` / `DisplayImageModel` / `DisplayMcpEnabled` / `DisplayMcpPort`（自动 SaveAsync）
- 3 个 resx 新增 `Settings.*` 键（AI 与 MCP / 接口地址 / API Key / 对话模型 / 嵌入模型 / 图片模型 / MCP 开关 / 端口 / 提示）

### 测试

- **388/388 通过**（+4）：`AppConfigTests`（默认值 + JSON 往返 2）+ `ConfigServiceEncryptionTests`（加密落盘往返 + 旧明文兼容 2）

---

## Phase 9C 图片资产修正（R27 Image Browser + Image Orchestration 拆分 + 议题1/6） | 2026-08-01

### 议题 1 + 7 + 6：ImageAssetManager 拆分为双视图（R27 落地）

- **Image Browser**（原 `ImageAssetManagerViewModel`/`ImageAssetManagerView` 收敛）：
  - **纯文件系统扫描**：Base Game 只扫 `<gameRoot>/img/`，mod 只扫 `<modFolder>/img/`；**删除 getimages.php 解析**（`ParseImagePairs`/`ResolveImagePath` 移出 Browser）
  - 保留 @2x 配对、搜索过滤、预览、双击打开；Tool 标题 "Image Assets" → "Image Browser"
  - **议题 6 自动加载**：构造即 `RefreshAsync()`；订阅 `GameRootDirChangedMessage` / `LoadProfileMessage` / `RefreshModMessage`
- **Image Orchestration**（新增 `ImageOrchestrationViewModel` + `ImageOrchestrationView` + `ImageOrchestrationTool`，挂 Right Dock）：
  - 读取 `<gameRoot>/getimages.php` + 各 `Mods/<mod>/getimages.php`，**严格声明顺序**展示 normal→x2 对
  - **R27 三路路径解析**（contentRoot/name → contentRoot/img/name → gameRoot/img/name）+ ✓/✗ 文件存在性校验（MissingCount/Summary 实时刷新）
  - 编辑：MoveUp/MoveDown 调整顺序、Add Pair（文件选择器导入到 mod img/）、Delete、Save（`GenerateImagePhp` 写回 + 通知）；**Base Game 只读**（不写游戏文件）
  - 无 getimages.php 的 mod 显示空状态，可 Save 创建
  - 自动加载同上（消息订阅）
- **刷新串行化**：两 VM 用 `_refreshChain` 链式队列串行化并发刷新（构造自动加载 + 消息触发 + 按钮触发互不 clobber）
- **DI**：`AddImageToolsPlugin` 注册 `ImageOrchestrationViewModel`；`Documents.cs` 新增 `ImageOrchestrationTool`
- 移除 Browser 的 `IModImageListService` 依赖（仅文件扫描）；`ImageAssetManagerViewModel` 构造签名 `(ILocalizationService, IConfigService, IMessenger)`
- **新增测试 +10**（ImageTools.Tests 16 → 26）：Orchestration（声明顺序/存在性/三路解析/Save 写回/只读禁用/重排/删除/消息自动加载/空 mod 可写）+ Browser（纯文件树忽略 getimages.php/搜索过滤/消息自动加载）
- **384/384 测试通过**（11 测试项目全绿）

---

## Phase 9 开发：9A + 9B B1-B5 (R26 双 Repository + HostService 三动作 + IncludeGame + ModManager 并入 + View 收敛) | 2026-08-01

### 9A 引用列放大镜按钮修复

`KeyValueEditorView` 行内冗余 🔍 按钮 + `OnPeekClick` + `PeekFieldCommand` 删除；保留 `ReferenceFieldEditor` 内置按钮（`PeekReferenceRequestMessage` → `DocumentWorkspaceViewModel` handler）。

### 9B B1 双 Repository 层

- `IXmlParser` 接口**上移 `NeoEditor.Core/Abstractions`**（具体类留 App，因依赖 UI.Common `Converter.ValueConverter`）；App 侧用别名 `using IXmlParser = NeoEditor.Core.Abstractions.IXmlParser;` 规避 `IWorkspaceSession` 命名空间歧义
- 新增 `Core/Abstractions/IEntityRepository.cs`：`IEntityRepository<T>`（Persist/Load/GetXmlFileDiff）+ `XmlFileDiff(FilePath, OldXml, NewXml)`
- 新增 `Infra/Data/Repository/DbRepository.cs`：EF 后端，`PersistAsync` = `DbBulkInsertOrUpdate` upsert；diff 继承 `RepositoryBase`（DiffEngine）
- 新增 `Infra/Data/Repository/XmlRepository.cs`：按 mod+FilePath 分组 → `IXmlParser.Export` → 磁盘 vs 生成 diff（仅返回有变化文件）→ 写盘；mod 目录经 `EditorDbContext.ModInfos` + `IConfigService.GameRootDir` 解析

### 9B B2 IHostService 三动作

- 新增 `Core/Abstractions/SaveResults.cs`：`SaveResult(PartialDiff, SavedEntityIds)` / `ExportResult(ModId, Files, UserConfirmed)` / `PublishResult(Save, Exports)`
- `ExtensionContexts.cs` 新增 `PreExportContext`
- `IHostService`：`SaveAsync`/`SaveAllAsync` → `Task<SaveResult>`；新增 `ExportModAsync`/`ExportProfileAsync`/`PublishAsync`/`RegisterPreExportHook`
- `HostService`：构造函数新增 `IXmlParser`/`IConfigService`/`IDbContextFactory<EditorDbContext>`；Save 走 `DbRepository`（实体须在 `_entityCache`）+ PreSaveHook 激活；Export 走 `XmlRepository` + PreExportHook；Publish = Save+Export
- 修复反射坑：`Invoke` 传 `List<IEntity>` 给 `IReadOnlyList<T>` 参数抛异常 → 构造具体类型 List

### 9B B3 per-profile dirty session

- dirty 集合按 profile 存于全局单例 `WorkspaceSession`（`ConcurrentDictionary<int, ISet<string>>`）；stores/indexes（BrowserStore/ForwardIndex）保持全局（R26 §3 实现决策）
- `IWorkspaceSession`（Infra）新增 `CurrentProfileId`/`GetDirtyEntities(profileId)`/`UnloadProfile`；`IHostService`（Core）新增 `ActiveProfileId`/`SetActiveProfile`
- Undo/Redo 补脏：`ICommandHistory.Undo()/Redo()` 返回类型 `void` → `IEditorCommand?`；`HostService.UndoAsync/RedoAsync` 对返回命令 `MarkAffectedDirty`
- App 接线：`ModIndexViewModel`/`DocumentWorkspaceViewModel` 进入 profile 时 `SetActiveProfile`

### 9B B4 ProfileInfo.IncludeGame + 单 Mod 视图去除

- `ProfileInfo` 新增 `IncludeGame`(bool, DB 列, 默认 true) + `SingleModId`(int?, DB 列, 仅单 Mod profile 非空)；`App.axaml.cs RunEditorDbMigrations` PRAGMA 检查后 `ALTER TABLE ADD COLUMN`
- **单 Mod → 持久化 profile**：`DocumentWorkspaceViewModel.EnsureSingleModProfileAsync` 查 `SingleModId` 建/复用一个仅含该 mod + IncludeGame=false 的 profile（Content 用 strModName="0" → `ModEntry.Type=Merge` → 保留业务主键）
- `ModDataToolViewModel` 删 `ModInfo/SetMod`；`ModGameDataDocument` 类 + center 模板 + 全部 `OfType<>` 死代码删除；`MainWindowViewModel` 只查 `ProfileInfo`
- `ModGameDataTabsView` 收敛：删 `ModInfoProperty`/`ReloadTabsAsync`/`ShowSavePreviewAsync`；`IsMergeView` 恒 true；`GetPersistenceTarget`：单 Mod profile → `("mod", modId)`（WAL per-mod 不回归）
- `ReloadMergeTabsAsync` 尊重 `IncludeGame`：`MergeSpaceModIds.Add(-1)`/`modMeta[-1]` 条件化 + `allModIds.Remove(-1)` → 不加载游戏数据

### 9B B5 ModManager 并入 HostService + 删 Validation/Conflicts + View 收敛

- `ModManager`+`IModManager` 迁 `Infra/Services`（namespace `NeoEditor.Services` 共享 → 零 using 变更）；**`HostService` 实现 `IModManager`**（委托内部 ModManager，构造新增第 6 参）；DI `IModManager` → HostService（R24 彻底化，App 不再持有 ModManager 实现）。`PhpParser` 依赖用 `"nRows=0&nCols=2"+Environment.NewLine` 内联（未迁 PhpParser）
- **删 Validation/Conflicts**：`RunPreSaveValidationAsync` + 保存流调用删除；`ConflictsTool`/`ValidationTool`/`ConflictsView`/`ValidationView`/`BottomToolsView`/`BottomToolsViewModel`（7 文件）+ `RequestValidationMessage`/`ValidationCompletedMessage` 记录 + DI 注册 + 集成测试删除。`Data/Validation/*` 规则文件 + `ValidationReportDialog` 保留为自包含死代码（"暂删，等更详细文档再设计"）
- **View 收敛**：`QuickSaveAsync`→`_hostService.SaveAllAsync()`（HostService 清 per-profile dirty）；`ShowMergeSavePreviewAsync`→`BuildExportPreviewAsync`（内存 diff + 弹窗，先预览后提交，取消=不落库）+ `SaveAllAsync` + 写文件；`ExportXmlAsync`→`_hostService.ExportModAsync`；**`SaveToDatabaseAsync` 删除，View 不再写 GameDbContext**（LastModified/LastImport 簿记走 EditorDbContext）

### 测试

**371/371 通过**（含 +17 B1/B2、+3 B3、+7 B4/B5：`ProfileInfoMappingTests`(3) + `HostServiceModManagerTests`(4) + `R24_DataTableView_DoesNotWrite_GameDbContext` 架构测试）。共享 `RepositoryTestHelpers`（TestDbFactory/StubConfigService/StubReferenceSerializer/StubXmlParser）+ `StubNotificationService`/`StubBrowserIndexService`。

### 影响文件

`Core/Abstractions/{IXmlParser, IEntityRepository, SaveResults, IHostService, ExtensionContexts}.cs` · `Infra/Data/Repository/{DbRepository, XmlRepository}.cs` · `Infra/Services/{HostService, ModManager, WorkspaceSession, IWorkspaceSession}.cs` · `Core/Model/ProfileInfo.cs` · `Core/Messages/ModGameDataMessages.cs` · App：`App.axaml.cs`（DI+迁移）、`ModGameDataTabsView*.cs` ×4、`DocumentWorkspaceViewModel.cs`、`DocumentWorkspaceView.axaml`、`ModDataToolViewModel.cs`、`Documents.cs`、`MainWindowViewModel.cs` · 删 7 个 Validation/Conflicts 文件 · `Tests/**` +7

---

## Bug 修复：排序闪退 + 虚拟列排序 + 游戏数据加载 (v0.34.2-dev) | 2026-07-31

### Sort NRE 回归修复（ProDataGrid）

**问题**：点击 DataGrid 列头排序 → `DataGridColumnHeader.ProcessSort` 内部 NRE → 闪退。是已知 Bug #9（CHANGELOG:1603）在 ProDataGrid 迁移（D1-D4）后回归——原来的 `DispatcherPriority.Background` 延迟替换被错误改写为同步替换。

**修复**：`SearchableDataGrid.OnSorting` 恢复延迟替换策略——`mainGrid.ItemsSource` 替换通过 `Dispatcher.UIThread.Post(…, Background)` 延迟到 ProDataGrid 内部 `ProcessSort` 完成后执行，避免列状态无效化。

### 虚拟列排序修复（→Id / Mod）

**问题**：点击 "→Id" 或 "Mod" 列头排序时数据乱序。`SortItems` 用反射 `GetProperty("MergedId")` / `GetProperty("Mod")` 查找属性——这两个是虚拟列，实体类上不存在，`propInfo` 返回 null 直接跳过排序。

**修复**：新增 `GetSortKeySelector()` 方法，虚拟列走 `DataTable.EntityMergedIds` / `DataTable.EntityModNames` 字典查值，普通列维持反射。

### 游戏数据加载修复（ModId=-1）

**问题**：启动导入游戏基础数据（`data\*.xml`）时流程错误：
1. `ImportModAsync(dataPath)` 分配自增 ModId（0 或更大）→ 实体存入 game.db 时 `mod_id` 为正数
2. `ImportGameDataOnStartupAsync` 事后把 ModInfo.ModId 改为 -1，但 **game.db 里的实体 mod_id 仍是正数**
3. 打开 Game 视图查询 `mod_id=-1` → 返回空 → 游戏数据不显示
4. 合并视图以游戏数据（ModId=-1）为覆盖链基准 → 无数据 → 覆盖链失效 → ShowAll 无效果

**修复**：
- `IModManager.ImportModAsync` 新增可选参数 `int? modId = null`，游戏数据调用时传 `modId: -1`
- `ImportGameDataOnStartupAsync`：re-import 场景（ModInfo 存在但 game.db 为空）改用 `LoadModAsync` 避免 UNIQUE 约束
- `ReloadMergeTabsAsync` 自动加载：跳过已导入的游戏数据（ModId=-1）；未导入的 namespace="0" 数据传入 `modId: -1`

### ReapplySort 优化

`ReapplySort` 改回手动排序 + null-first 同步替换（ShowAll 切换时调用，无 pending ProcessSort，安全）。

### 文档

- 新增 [34-prodatagrid-column-filter-plan.md](34-prodatagrid-column-filter-plan.md) — ProDataGrid IFilteringModel 实现计划

### 影响文件

| 文件 | 改动 |
|------|------|
| `SearchableDataGrid.axaml.cs` | OnSorting 延迟替换恢复 + GetSortKeySelector 虚拟列 + ReapplySort 优化 |
| `ModManager.cs` | ImportModAsync 可选 modId 参数 + IsBase 自动设置 |
| `App.axaml.cs` | ImportGameDataOnStartupAsync 传入 modId:-1 + re-import 用 LoadModAsync |
| `ModGameDataTabsView.Data.cs` | AutoLoad 跳过已导入游戏数据 + namespace=0 传入 modId:-1 |

---

## Bug 修复：ModInfo Schema + XmlParser + Import/Sort (v0.34.1-dev) | 2026-07-31

### ModInfo: Id vs ModId 分离

**问题**：`ModInfo.ModId` 同时充当 DB PK 和 Profile 编排业务字段，且挂了 `[DatabaseGenerated(Identity)]`。EF Core 忽略显式 `ModId=-1`，SQLite 自增生成不同 ID → `FindAsync(-1)` 找不到 Game 记录 → 重复插入 `Path="data"` → **UNIQUE constraint 崩溃**。

**修复**：
- `ModInfo` 新增 `Id` 列（DB 自增 PK，纯数据库概念，`DatabaseGeneratedOption.Identity`）
- `ModId` 降级为普通唯一列（业务字段：`-1`=Game，`>=0`=Mod）
- `EditorDbContext`：`HasKey(m => m.Id)` + `HasIndex(m => m.ModId).IsUnique()`
- `ModManager.GetNextModIdAsync()`：计算下一个可用业务 ModId
- 12 处 `FindAsync(ModId)` → `FirstOrDefaultAsync(m => m.ModId == ...)`

**影响文件**：9 个（`ModInfo.cs`, `EditorDbContext.cs`, `ModManager.cs`, `ModDatabaseViewModel.cs`, `HomePageViewModel.cs`, `App.axaml.cs`, `DataExportService.cs`, `ReferenceInspectorView.axaml.cs`, `ModGameDataTabsView.Data.cs`）

### XmlParser: ReferenceList 类型解析

**问题**：`XmlParser.ConvertValue` 不认识 `ReferenceList<T>` 类型，reference string（`NSE:86.6x1x1-2`）直接丢给 `Convert.ChangeType` → `InvalidCastException`。导致含引用属性的实体（ItemType/Creature 等）全部 import 失败，只剩无引用类型数据。

**修复**：`XmlParser` 注入 `IReferenceListSerializer`；`ConvertValue` 优先检查 `ReferenceList<T>` + `ReferenceFieldAttribute` → `_refSerializer.Deserialize()`。

### Import 去自动打开

**修复**：去掉 `ImportMod()` 里的 `Messenger.Send(OpenModGameDataDocumentMessage)`，导入只导入不跳转。

### Sort NRE（ProDataGrid）

**问题**：`SwitchTabItemsSource` 直接替换 `ItemsSource`，ProDataGrid 列状态清理不及时 → 点击列头 → `DataGridColumnHeader.ProcessSort` 内部 NRE。

**修复**：`SwitchTabItemsSource` 先设 `ItemsSource = null` 再设新值，确保旧列干净卸载。

### 文档

- `CLAUDE.md`、`CHANGELOG.md`、memory `modid-convention.md` 更新

---

## M13+ Phase 7 测试补齐 + 新功能规划 (v0.34.0-dev) | 2026-07-30

### Phase 7.1: 测试补齐

创建 Mcp / Cli / AiChat 三个 Plugin 的测试项目，**72 新测试全部通过**。总测试数: 199 → 271。

| 测试项目 | 测试数 | 内容 |
|---------|:-----:|------|
| `NeoEditor.Plugins.Mcp.Tests` | 17 | McpPlugin metadata + McpToolExecutor (GetTools/ExecuteTool 8 场景) + EntityResourceProvider |
| `NeoEditor.Plugins.Cli.Tests` | 40 | CliPlugin metadata + CliCommandParser (28: 8 命令 × 正常/别名/错误/边界) + CliOutputFormatter (8) |
| `NeoEditor.Plugins.AiChat.Tests` | 15 | AiChatPlugin metadata + ChatHistoryManager (10: Add/Clear/SetSystemPrompt/Trim) |

**配套更新：**
- `PluginArchitectureTests.cs` — PluginAssemblies 增加到 6 个（+Mcp/Cli/AiChat）
- `Core.Tests.csproj` — 新增 3 个 Plugin 项目引用
- `build.sh` — PROJECTS 22 项目（11 src + 11 test）
- `NeoEditor.sln` — 新增 3 个测试项目 + 配置平台 + 嵌套关系

### 新功能开发计划

编写了三个完整的开发计划文档：

| 文档 | 内容 | 预估 |
|------|------|:--:|
| [Doc 31](31-prodatagrid-migration-plan.md) | ProDataGrid 替换 Avalonia DataGrid：4 Phase, ~15 文件, 可删 ~300 行 | 10-15h |
| [Doc 32](32-agent-orchestration-plan.md) | Agent 编排增强：系统提示词 + Schema 注入 + RAG (VectorStore) + MCP 增强 | 6-10h |
| [Doc 33](33-image-generation-plan.md) | 像素图像生成：实体 XML → AI 文生图 → 像素化后处理 (ImageSharp) | 6-9h |

### 文档订正

- `CLAUDE.md` — 全面更新（22 项目/271 测试/Phase 7 完成/新文档地图）
- `Doc 30` — 更新现状表 + Phase 7 测试完成 + 下一阶段引用
- `memory` — 新增 `m13-phase7-complete-2026-07-30.md`

---

## M13+ Phase 5 & 6: ImageAssetManager + Plugin 分类 (v0.33.0-dev) | 2026-07-30

### Phase 5: ImageAssetManager Tool Dock

新增 "Image Assets" Tool Dock（RightToolPane 第二个 Tab），提供跨所有 Mod 的图片浏览：

| 功能 | 实现 |
|------|------|
| 树状浏览 | TreeView 按 Mod 分组，读取 `getimages.php` 声明的图片对 |
| 搜索过滤 | 实时过滤 tree 节点（mod 名 + 图片名） |
| 预览面板 | 右侧预览缩略图 + 尺寸 + Mod 来源 + x2 版本 |
| 双击打开 | → ImageDocument（center dock） |
| 刷新 | Refresh 按钮重新扫描 Mods 目录 |

**新增文件：**
- `ImageTools/ViewModels/ImageAssetManagerViewModel.cs` — ViewModel（树构建 + 搜索 + 预览 + 命令）
- `ImageTools/Views/ImageAssetManagerView.axaml` — TreeView + 预览面板布局
- `ImageTools/Views/ImageAssetManagerView.axaml.cs` — 双击 handler

**修改文件：**
- `Documents.cs` — 新增 `ImageAssetManagerTool` (Dock.Model Tool)
- `DocumentWorkspaceViewModel.cs` — 实例化 VM + Tool
- `DocumentWorkspaceView.axaml` — RightToolPane 新增 Tab
- `ServiceCollectionExtensions.cs` — 注册 Singleton

### Phase 6: Plugin 分类体系 (R23-R25)

落地 spec R23（Plugin 三分类）、R25（扩展点接口）：

**新增 Core/Abstractions 类型：**
- `PluginKind.cs` — 枚举 (Workbench / Service / Feature)
- `PluginKindAttribute.cs` — 单次使用、不可继承
- `IServicePlugin.cs` — 后端插件标记接口（空，继承 IPlugin）
- `IExtensionPoint.cs` — 泛型扩展点契约 `IExtensionPoint<TContext>`
- `ExtensionContexts.cs` — PreSaveContext / PostLoadContext / PreExecuteContext

**修改：**
- `IHostService.cs` — 新增 RegisterPreSaveHook / RegisterPostLoadHook / RegisterPreExecuteHook
- `HostService.cs` — 实现 3 个方法（存储到 List，调用延后到 Phase 7）
- 3 个 Plugin 类 + `[PluginKind(Workbench)]`
- Core.Tests 新增 6 个架构测试（199/199 ✅）

---

## M13+ Phase 2: 引用类型系统 (v0.32.0-dev) | 2026-07-30

### Phase 2: 引用第一公民 — 从 raw string 到 ReferenceList&lt;IReferenceEntry&gt;

将全部引用列从 plain `string` 提升为类型化的 `ReferenceList<IReferenceEntry>`。每个 `[ReferenceField]` pattern 对应一个自包含 Format 类，直接持有类型化参数。XML/DB 格式保持 100% 双向兼容。

**核心抽象 (Core/Abstractions)**:

| 新建文件 | 内容 |
|---------|------|
| `IReferenceEntry.cs` | 引用条目基接口（`ToRawString()` + `DisplayText`） |
| `IReferenceFormat.cs` | Format 基接口（继承 IReferenceEntry，增加 `FormatTemplate`） |
| `IReferenceListSerializer.cs` | 序列化接口（`Deserialize`/`Serialize`） |

**领域模型 (Core/Model)** — 三层：

| 新建文件 | 内容 |
|---------|------|
| `ReferenceEntryTypes.cs` | `EntityRef` — 纯实体引用（NS + Id + GroupId/SubgroupId），只有定位信息 |
| `ReferenceFormats.cs` | 7 个 Format 类 — 每个自包含 template + 类型化参数 |
| `ReferenceList.cs` | 泛型引用集合，含隐式转换 string + `.Split()` 代理 |

Format 类：`PureRefFormat`, `NegatedRefFormat`（`Inner: IReferenceEntry` 支持嵌套）, `IdXMultFormat`, `MultXIdFormat`, `AssignFormat`, `BracketFormat`, `MultiIngredientRecipeFormat`（复合嵌套子 Format）

**基础设施 (Infra)**:

| 新建文件 | 内容 |
|---------|------|
| `Helper/ReferenceListSerializer.cs` | pattern → Format 类直接映射，零手工拆解 prefix/suffix |
| `Data/Converters/ReferenceListStringConverter.cs` | EF Core `ValueConverter` |

**修改**: `GameDbContext.cs` OnModelCreating 自动发现 · `App.axaml.cs` DI 注册 · 15 实体 ~48 属性迁移

**测试**: 176/176 全过（83 Parser 特征化 + 21 Serializer roundtrip + 22 Entry/Format + 其余）

**设计决策**:
- **Format 类代替 DecoratedRef**：整个 segment 由一个 Format 类承载，UI 可按类型分发（如 `IdXMultFormat.Multiplier` → NumberBox）
- `NegatedRefFormat.Inner: IReferenceEntry` 允许嵌套包装（如 `-211x1.5` → NegatedRefFormat { Inner = IdXMultFormat }）
- `ReferenceList<T>` 含向后兼容桥接（隐式转换 string + `.Split()` 代理）
- EF Core `OnModelCreating` 自动发现，0 逐实体配置
- 现有 `IReferenceResolver`（Infra）保留不变，Phase 2 聚焦数据层类型化

---

## M13+ Phase 1: HostService (v0.31.0-dev) | 2026-07-29

### Phase 1: 统一 CRUD 路径 — IHostService

创建 `IHostService` 作为所有数据修改的唯一入口，将所有分散的 CRUD 路径收敛到单一接口。

**接口层 (Core/Abstractions)**:

| 新建文件 | 内容 |
|---------|------|
| `IEditorCommand.cs` | 从 Infra 提升到 Core，namespace `NeoEditor.Core.Abstractions` |
| `ICommandHistory.cs` | 从 Infra 提升到 Core（HostService scope 管理依赖） |
| `IHostService.cs` | 统一写路径接口（Execute/Undo/Redo/Save/Dirty/Events/Scope） |
| `CommandResult.cs` | 执行结果 record |
| `DiffEntry.cs` + `DiffKind` | 字段级 diff 模型 |
| `EntityChangedEvent.cs` + `ChangeType` | 可观察事件 payload |
| `IDataRepository.cs` | 通用仓储接口（Phase 1 只读） |

| 删除文件 | 原因 |
|---------|------|
| `Infra/Data/Command/IEditorCommand.cs` | 已移到 Core |
| `Infra/Data/Command/ICommandHistory.cs` | 已移到 Core |

**实现层 (Infra)**:

| 新建文件 | 内容 |
|---------|------|
| `HostService.cs` | 核心实现：Scope-based CommandHistory 管理 + 脏追踪 + 事件分发 |
| `RepositoryBase.cs` | 仓库基类（委托 DiffEngine 生成字段级 diff） |
| `DiffEngine.cs` | 静态反射 diff 引擎（比较 `[Column]` 属性） |
| `PassThroughRepository<T>` | 简单 GameDbContext 只读实现 |

**迁移的写路径 (App)**:

| 路径 | 改动 |
|------|------|
| `OnCellEditCommitted` | `_commandHistory.Execute(cmd)` → `_hostService.ExecuteAsync(cmd, scopeId)` |
| `OnEntityFieldEditsFromXml` | `_commandHistory.Execute(cmd)` + 手动 `MarkEntityDirty` → `_hostService.ExecuteAsync` |
| `AddOrCloneEntityAsync` | `_commandHistory.Execute(addCmd)` → `_hostService.ExecuteAsync(addCmd, scopeId)` |
| `OnDeleteRowButtonClick` | `_commandHistory.Execute(delCmd)` → `_hostService.ExecuteAsync(delCmd, scopeId)` |
| `PasteCells` | `_commandHistory.Execute(...)` → `_hostService.ExecuteAsync(...)` |
| Ctrl+Z / Ctrl+Y | `_commandHistory.Undo()` → `_hostService.UndoAsync(scopeId)` |
| `OnUndoClick` / `OnRedoClick` | 同上 |
| `ViewServices` | 新增 `HostService` 访问器 |
| `App.axaml.cs` | 新增 `services.AddSingleton<IHostService, HostService>()` |

**Scope 隔离**: 每个 `ModGameDataTabsView` 实例在构造函数注册 `RegisterCommandScope(scopeId, commandHistory)`，undo/redo 按 tab 隔离。

**测试**:

| 测试文件 | 数量 |
|---------|:----:|
| `Infra.Tests/Services/HostServiceTests.cs` | 7 个（Execute/Dirty/Event/Scope/Undo/Discard） |
| `Core.Tests/Spec/R24HostServiceRuleTests.cs` | 3 个架构测试（EF 隔离、接口完整性） |

**架构指标**:

- 51/51 测试全部通过 ✅（+10：HostService 7 + 架构测试 3）
- 编译 0 Error ✅
- R24 规则已落地（所有数据经过 IHostService）
- `IEditorCommand` / `ICommandHistory` 提升到 Core 层
- Scope 机制保留 per-tab undo/redo 隔离

---

## M13+ Phase 4 + 依赖包精简 (v0.30.0-dev) | 2026-07-29

### Phase 4: 删除 DataBrowser

DataBrowser（侧边栏领域→实体类型树面板）已被 DataViewer Plugin 完全取代，已无用户入口。

| 删除的文件 | 作用 |
|-----------|------|
| `DataBrowserViewModel.cs` | 侧边栏面板 ViewModel |
| `DataBrowserView.axaml` + `.cs` | 面板 UI + Code-behind |

| 修改的文件 | 改动 |
|-----------|------|
| `App.axaml.cs` | 移除 DI 注册 |
| `MainWindow.axaml` | 移除侧边栏按钮 |
| `MainWindowSideBarViewModel.cs` | 移除 `CreatePaneContent` case |
| `Pane.axaml` | 移除 DataTemplate |
| `HomePage.axaml` + `.cs` + `.axaml.cs` | 移除 Browse 卡片和事件处理 |
| `HomePageViewModel.cs` | 移除 `BrowseGameData()` 命令 |
| `DocumentWorkspaceViewModel.cs` | 移除 `OpenDataBrowserDocument()`, `CloseCenterDocuments()`, `LoadGameDataIntoBottomTable()` |
| `GameDomain.cs (Infra)` | 移除 `DomainGroup` record（仅 DataBrowser 用） |
| `NeoEditor.App.csproj` | 移除 DataBrowserView Compile Update |

### 全量依赖包精简

审计并清理了所有 8 个 src 项目的 `PackageReference`，**合计移除 25 个死包引用**：

| 项目 | 精简前 → 后 | 移除内容 |
|------|:----------:|---------|
| `NeoEditor.App` | 51 → **40** | `CompareNETObjects`, `DiffPlex`, `XMLDiffPatch`, `SixLabors.ImageSharp`, `AvaloniaEdit.TextMate`, `Irihi.Ursa`, `LiveMarkdown.Avalonia.Math`, `LiveMarkdown.Avalonia.Svg`, `Tmds.DBus.Protocol`, `TreeDataGrid.Avalonia`, `Dock.Avalonia.Diagnostics` |
| `NeoEditor.Infra` | 16 → **6** | `AutoMapper`, `Microsoft.Extensions.Hosting`, `Serilog.Extensions.Hosting`, `Serilog.Extensions.Logging.File`, `Serilog.Settings.Configuration`, `Serilog.Sinks.Console`, `Serilog.Sinks.File`, `SixLabors.ImageSharp`, `System.Configuration.ConfigurationManager` |
| `NeoEditor.UI.Common` | 3 → **2** | `Avalonia.Controls.DataGrid` |
| `NeoEditor.Plugins.EntityEditor` | 8 → **7** | `Avalonia.Controls.DataGrid`（AvaloniaEdit.TextMate → base `Avalonia.AvaloniaEdit`） |
| `NeoEditor.Plugins.ImageTools` | 7 → **5** | `Avalonia.Controls.DataGrid`, `Xaml.Behaviors.Avalonia` |
| `NeoEditor.Messaging` | 0 → 0 | — |
| `NeoEditor.Core` | 1 → 1 | — |
| `NeoEditor.Plugins.DataViewer` | 5 → 5 | —（全部使用中，保留） |

**移除原则**：App 不声明 Plugin/Infra 已覆盖的包（transitive 可用）；死代码包直接删除（全代码库零引用）。

### 架构指标

- 41/41 测试全部通过 ✅
- 编译 0 Error ✅
- Phase 4 (DataBrowser 删除) 已完成 ✅
- Sidebar 从 6 按钮变为 5 按钮，HomePage 从 3 卡片变为 2 卡片

---

## M9 DataViewer Plugin 核心迁移完成 (v0.26.0-dev) | 2026-07-28

### 插件化迁移

M9 将 DataViewer 功能从单体 App 拆分为独立 Plugin 项目，实现 0 静态依赖。

| 阶段 | 内容 |
|------|------|
| 前置清理 | V6 static 删除、App 重复副本删除、DI 集中注册 |
| Views 迁移第1轮 | SearchableDataGrid 解耦+迁移，GDH 轻量化(839→167)，CreateToolView |
| Views 迁移第2轮 | IndexTableView / PeekPanelView / FindReplacePanel / SearchResultsView → Plugin |
| 核心服务提取 | DataLoaderService（6 DB 方法）、EntityVisualizerRegistry、IEntityVisualizer |
| VM 增强 | DataTableViewModel：Tabs/MergeStore/EditStore/ModInfo/ProfileInfo 所有权 |
| GDH 清零+删除 | 全部消费者迁移完毕，`GenericDataGridHelper.cs` 已删除 |
| Converter 改造 | 5 Converter 从 `DataTableService.Instance` → `ConverterServiceHelper` |

### Plugin 最终结构

```
NeoEditor.Plugins.DataViewer/
├── Converters/ (6)   含 ConverterServiceHelper
├── Services/ (11)    含 DataLoaderService, EntityVisualizerRegistry
├── ViewModels/ (6)   含 DataTableViewModel(增强), SearchResultViewModel(完善)
└── Views/ (5)        SearchableDataGrid, IndexTable, PeekPanel, FindReplace, SearchResults
```

### 架构指标

- Plugin: 0 GDH, 0 ViewServices, 0 DataTableService.Instance, 0 App 命名空间引用
- GDH.cs 已删除
- 10 项目 0 Error | 12/12 测试通过

### 测试轮

- 第11轮: 前置清理
- 第12轮: Views 迁移第1轮
- 第13轮: Views 迁移第2轮 + GDH 删除 ([test_round13_summary.md](testround/test_round13_summary.md))

---

## M6 完成 + 用户验证 (v0.24.0-dev) | 2026-07-24

### M6 收尾
M6 四阶段全部完成，Spec 22/22 (100%) 落地，0 架构债，0 Error，8/8 测试通过。

| 子阶段 | 内容 |
|--------|------|
| M6.1 | GDH ConfigureColumn 模板化 — `IDataGridCellInteractionService` + 注入实现，GDH -160 行 |
| M6.2 | `ModGameDataTabsViewModel` — CommandHistory / WAL / Dirty 从 View 提取至 ViewModel |
| M6.3 | `App.ServiceProvider` 归零 — 剩余 2 处标注框架豁免 |
| M6.4 | 功能回归测试 — 编译 0 Error / 8/8 测试通过 |

### 用户验收

| 验证项 | 结果 |
|--------|:--:|
| P1: DataTable 引用解析 / Ctrl+Click 导航 / Ctrl+Hover Peek | ✅ 通过 |
| P2: Bug 2 XML 编辑同步 (R06 四区域同源) | ✅ 通过 |
| P2: Bug 3 XML 编辑持久化 (WAL snapshot + command_log 恢复) | ✅ 通过 |

三个历史 bug 全部关闭。

### 新增文件

| 文件 | 行数 | 用途 |
|------|:--:|------|
| `Services/IDataGridCellInteractionService.cs` | 49 | 单元格交互接口 |
| `Services/DataGridCellInteractionService.cs` | 239 | 注入式交互处理器 |
| `ViewModels/MainContent/ModGameDataTabsViewModel.cs` | 286 | CommandHistory / WAL / Dirty 所有者 |

### 代码瘦身

| 文件 | 改前 | 改后 | 变化 |
|------|:--:|:--:|:--:|
| `Helper/GenericDataGridHelper.cs` | 979 | 819 | **-160** |
| `ModGameDataTabsView.axaml.cs` | 1574 | 1511 | **-63** |

---

## T7 — GDH 静态可变状态提取 (v0.23.0-dev) | 2026-07-18 (续)

### 背景
`GenericDataGridHelper` 作为 `static class` 持有 4 个静态可变字段（N01 违规）：
`ColumnMetaCache`、`_ctrlWasPressed`、`NavigationHandled`、`SuppressNextSelectionChanged`。

### 方案
创建 `DataGridInteractionState` 注入单例服务，将上述字段提取为实例属性，
通过 `App.DataGridState` 暴露。GDH 删除静态可变字段，改为属性委托。

### 新建文件

| 文件 | 说明 |
|------|------|
| `Services/DataGridInteractionState.cs` | 注入单例，收容 `CtrlWasPressed`、`SuppressNextSelectionChanged`、`ColumnMetaCache` |

### GDH 变更

| 原静态字段 | 变更 |
|-----------|------|
| `ColumnMetaCache` | `static Dictionary` → `App.DataGridState.ColumnMetaCache` 委托属性 |
| `_ctrlWasPressed` / `CtrlWasPressed` | `private static bool` + 手动属性 → `App.DataGridState.CtrlWasPressed` 委托属性（含 4 处内部引用替换） |
| `NavigationHandled` | **删除** — 死代码（声明后从未读/写） |
| `SuppressNextSelectionChanged` | `static auto-property` → `App.DataGridState.SuppressNextSelectionChanged` 委托属性 |

### 外部消费者同步

| 文件 | 替换数 | 说明 |
|------|:--:|------|
| `SearchableDataGrid.axaml.cs` | 8 | `GenericDataGridHelper.SuppressNextSelectionChanged`/`ColumnMetaCache` → `App.DataGridState.*` |
| `ModGameDataTabsView.Tab.cs` | 2 | `GenericDataGridHelper.SuppressNextSelectionChanged` → `App.DataGridState.*` |

### 影响文件

| 文件 | 关键改动 |
|------|---------|
| `Services/DataGridInteractionState.cs` | **新建** — 注入单例，3 个 DataGrid UI 交互 flag |
| `App.axaml.cs` | 新增 `DataGridState` V6 访问器 + DI 注册 + 初始化 |
| `Helper/GenericDataGridHelper.cs` | 删除 4 个静态可变字段/属性；新增委托属性 |
| `Views/UserControls/SearchableDataGrid.axaml.cs` | 8 处 `GenericDataGridHelper.*` → `App.DataGridState.*` |
| `Views/UserControls/ModGameDataTabsView.Tab.cs` | 2 处 `GenericDataGridHelper.*` → `App.DataGridState.*` |
| `spec/audit-2026-07-05.md` | T7 → ✅已完成；N01 残余表重写；架构债 #1 降级 |

### 结果
GDH **0 处静态可变字段**，N01 完全合规 ✅。建议执行顺序 7 项全部完成，审计报告无待办。

---

## T8 — _copyBuffer 去静态化 (v0.23.0-dev) | 2026-07-18 (续)

### 背景
T7 消除了 `GenericDataGridHelper` 中的静态可变字段，但 `ModGameDataTabsView.axaml.cs`
code-behind 中仍有一个 `private static string? _copyBuffer` 字段未被覆盖。
该字段用作 DataGrid 单元格复制/粘贴的内部缓冲区（避免系统剪贴板问题），
是 View code-behind 中最后一个静态可变字段。

### 修复
`private static string? _copyBuffer;` → `private string? _copyBuffer;`

### 影响方法

| 方法 | 行号 | 说明 |
|------|:--:|------|
| `CopySelectedCells()` | L867-897 | 写 `_copyBuffer` |
| `PasteCells()` | L900-958 | 读 `_copyBuffer` |

两处均为实例方法，改为实例字段后语义不变。

### 影响文件

| 文件 | 关键改动 |
|------|---------|
| `Views/UserControls/ModGameDataTabsView.axaml.cs` | `_copyBuffer` 去掉 `static` 修饰符 |

### 结果
View code-behind 中 **0 处静态可变字段**，N01 全面合规 ✅。

---

## T8 — _copyBuffer 去静态化 (v0.23.0-dev) | 2026-07-18 (续)

### 背景
T7 消除了 `GenericDataGridHelper` 中的静态可变字段，但 `ModGameDataTabsView.axaml.cs`
code-behind 中仍有一个 `private static string? _copyBuffer` 字段未被覆盖。
该字段用作 DataGrid 单元格复制/粘贴的内部缓冲区（避免系统剪贴板问题），
是 View code-behind 中最后一个静态可变字段。

### 修复
`private static string? _copyBuffer;` → `private string? _copyBuffer;`

### 影响方法

| 方法 | 行号 | 说明 |
|------|:--:|------|
| `CopySelectedCells()` | L867-897 | 写 `_copyBuffer` |
| `PasteCells()` | L900-958 | 读 `_copyBuffer` |

两处均为实例方法，改为实例字段后语义不变。

### 影响文件

| 文件 | 关键改动 |
|------|---------|
| `Views/UserControls/ModGameDataTabsView.axaml.cs` | `_copyBuffer` 去掉 `static` 修饰符 |

### 结果
View code-behind 中 **0 处静态可变字段**，N01 全面合规 ✅。

---


## T8 鈥?_copyBuffer 鍘婚潤鎬佸寲 (v0.23.0-dev) | 2026-07-18 (缁?

### 鑳屾櫙
T7 娑堥櫎浜?GenericDataGridHelper 涓殑闈欐€佸彲鍙樺瓧娈碉紝浣?ModGameDataTabsView.axaml.cs
code-behind 涓粛鏈変竴涓?private static string? _copyBuffer 瀛楁鏈瑕嗙洊銆?璇ュ瓧娈电敤浣?DataGrid 鍗曞厓鏍煎鍒?绮樿创鐨勫唴閮ㄧ紦鍐插尯锛堥伩鍏嶇郴缁熷壀璐存澘闂锛夛紝
鏄?View code-behind 涓渶鍚庝竴涓潤鎬佸彲鍙樺瓧娈点€?
### 淇
private static string? _copyBuffer; 鈫?private string? _copyBuffer;

### 褰卞搷鏂规硶

| 鏂规硶 | 琛屽彿 | 璇存槑 |
|------|:--:|------|
| CopySelectedCells() | L867-897 | 鍐?_copyBuffer |
| PasteCells() | L900-958 | 璇?_copyBuffer |

涓ゅ鍧囦负瀹炰緥鏂规硶锛屾敼涓哄疄渚嬪瓧娈靛悗璇箟涓嶅彉銆?
### 褰卞搷鏂囦欢

| 鏂囦欢 | 鍏抽敭鏀瑰姩 |
|------|---------|
| Views/UserControls/ModGameDataTabsView.axaml.cs | _copyBuffer 鍘绘帀 static 淇グ绗?|

### 缁撴灉
View code-behind 涓?**0 澶勯潤鎬佸彲鍙樺瓧娈?*锛孨01 鍏ㄩ潰鍚堣 鉁呫€?
---
## 审计报告订正 — T4/T5 验证 + T6 App.ServiceProvider 消除 (v0.23.0-dev) | 2026-07-18 (续)

### T4/T5 — 第二轮验证发现已完成

| 条目 | 证据 |
|------|------|
| T4 GDH App.ServiceProvider | `GenericDataGridHelper.cs` 0 处 `App.ServiceProvider`；已改用 `App.NavigationRouter`/`App.WorkspaceSession`/`App.ReferenceResolver` |
| T5 DirtyEntities 封装 | 全量改用 `IWorkspaceSession.MarkEntityDirty`/`MarkEntitiesDirty`/`ClearDirtyEntities`/`RemoveDirtyEntities`；`DirtyEntities.Add`/`Clear` 在 View 层 0 处匹配 |

### T6 — App.ServiceProvider 大幅消除

**目标**：消除 View 层 code-behind 中除 Avalonia 框架限制外的所有 `App.ServiceProvider` 直访。

**方案**：在 `App` 添加 V6 静态访问器属性，View code-behind 通过 `App.*` 替代
`App.ServiceProvider.GetRequiredService<T>()`。

**新增 App.* 访问器**（7 个）：

| 属性 | 类型 | 替换场景 |
|------|------|---------|
| `App.GameDbFactory` | `IDbContextFactory<GameDbContext>` | ModGameDataTabsView, etc. |
| `App.EditorDbFactory` | `IDbContextFactory<EditorDbContext>` | ModGameDataTabsView, ReferenceInspectorView |
| `App.LoggerFactory` | `ILoggerFactory` | SearchableDataGrid, XmlDiffView, ModGameDataTabsView |
| `App.XmlParser` | `IXmlParser` | ModGameDataTabsView |
| `App.WorkspacePersistence` | `IWorkspacePersistenceService` | ModGameDataTabsView |
| `App.ProfileManager` | `IProfileManager` | ModGameDataTabsView |
| `App.MergeService` | `IMergeService` | ModGameDataTabsView |

**替换统计**：

| 文件 | 替换数 | 改前 | 改后 |
|------|:--:|------|------|
| `ModGameDataTabsView.axaml.cs` | 12 | `App.ServiceProvider.GetRequiredService<T>()` ×12 | `App.GameDbFactory`/`App.EditorDbFactory`/`App.LoggerFactory`/`App.XmlParser`/`App.WorkspacePersistence`/`App.NavigationRouter`/`App.ProfileManager`/`App.MergeService`/`App.WorkspaceSession`/`App.ReferenceResolver` |
| `Pane.axaml.cs` | 2 | `App.ServiceProvider...GetRequiredService<INavigationRouter>()` ×2 | `App.NavigationRouter` |
| `SearchResultsView.axaml.cs` | 1 | `App.ServiceProvider...GetRequiredService<INavigationRouter>()` | `App.NavigationRouter` |
| `ReferenceInspectorView.axaml.cs` | 1 | `App.ServiceProvider...GetRequiredService<IDbContextFactory<EditorDbContext>>()` | `App.EditorDbFactory` |
| `SearchableDataGrid.axaml.cs` | 1 | `App.ServiceProvider.GetRequiredService<ILogger<SearchableDataGrid>>()` | `App.LoggerFactory.CreateLogger<SearchableDataGrid>()` |
| `XmlDiffView.axaml.cs` | 1 | `App.ServiceProvider.GetRequiredService<ILogger<XmlDiffView>>()` | `App.LoggerFactory.CreateLogger<XmlDiffView>()` |

**结果**：View 层 `App.ServiceProvider.GetRequiredService<T>()` 从 **~22 处降至 3 处**
（2 弹窗参数化构造器 + 1 HomePage scoped VM），均为 Avalonia 框架限制的正当例外。

### 影响文件

| 文件 | 关键改动 |
|------|---------|
| `App.axaml.cs` | 新增 7 个 V6 静态访问器属性 + `OnFrameworkInitializationCompleted` 初始化 |
| `ModGameDataTabsView.axaml.cs` | 构造器 12 处服务获取替换为 `App.*` 访问器 |
| `Pane.axaml.cs` | 2 处 `INavigationRouter` 替换 |
| `SearchResultsView.axaml.cs` | 1 处 `INavigationRouter` 替换 |
| `ReferenceInspectorView.axaml.cs` | 1 处 `EditorDbFactory` 替换 |
| `SearchableDataGrid.axaml.cs` | 1 处 `ILogger<>` 替换为 `LoggerFactory.CreateLogger<>()` |
| `XmlDiffView.axaml.cs` | 1 处 `ILogger<>` 替换为 `LoggerFactory.CreateLogger<>()` |
| `spec/audit-2026-07-05.md` | 建议执行顺序 T4-T6 → ✅已完成；架构债 #2 重写；N03/N01 证据更新 |

---

## 审计报告订正 — 第一梯队清零 + R03 去静态化验证 (v0.23.0-dev) | 2026-07-18

### 背景
`spec/audit-2026-07-05.md` 审计报告标记了 ❌ 未完成项（N04 死消息、Q10 死消息策略）和
⚠️ 部分落地项（Q7 AddRowDialog 工厂、R03 ReferenceResolver 静态化）。重新 grep 全代码库
逐条验证后发现，这些条目在之前的迭代中已悄然完成，审计报告处于过时状态。

### 已完成条目（先前标记为 ❌/⚠️）

| 条目 | 原状态 | 现状态 | 证据 |
|------|:--:|:--:|------|
| N04 禁止死消息 | ❌ | ✅ | `GridRowHeightChangedMessage` / `FontSizeChangedMessage` 全代码库 0 处匹配 |
| Q10 死消息处理策略（方案A） | ❌ | ✅ | 3 条死消息（含 `SwitchToSettingsMessage`）全部删除 |
| Q7 AddRowDialog 工厂方法 | ⚠️ | ✅ | `Create(IConfigService, ...)` 已实现；`ShowAsync`/`ShowSimpleAsync` 均接收注入的 `IConfigService` |
| R03 引用解析走注入接口 | ⚠️ | ✅ | `ReferenceResolver` 0 处 `static` 修饰符；`_lookupCache`/`_cachedIndexService` 均为实例字段；构造支持 DI |
| R05 消息规范 | ⚠️ | ✅ | 死消息已删，残余问题自动消失 |

### 审计报告修正

| 章节 | 变更 |
|------|------|
| 总览统计 | ✅ 13→18 (69%) / ⚠️ 7→4 (15%) / ❌ 2→0 (0%) |
| ✅ 完全落地 | 新增 R03、N04、Q7、Q10 四项 |
| ❌ 未完成 | 整章删除（N04、Q10 均已完成） |
| 架构债 | 移除 ReferenceResolver 静态化条目；新增 ModGameDataTabsView N03 条目；更新 App.ServiceProvider 引用清单 |
| 补记 | 追加 2026-07-18 补记，记录 5 项条目状态迁移及证据 |

### 建议执行顺序更新

第一梯队 (T1/T2) 和第二梯队 T3 (ReferenceResolver 去静态化) 标记为已完成 ✅；
当前待办聚焦于第二梯队 T4-T5（GDH 清理 + DirtyEntities 下沉）和第三梯队 T6-T7。

### 影响文件

| 文件 | 关键改动 |
|------|---------|
| `spec/audit-2026-07-05.md` | 总览统计、✅/⚠️/❌ 重新分配、架构债表更新、补记追加 |

---

## WAL 恢复后脏状态视觉提示持久化 (v0.23.0-dev) | 2026-07-05

### 问题
WAL (Write-Ahead Log) 持久化已生效，编辑器重启后编辑数据能正确恢复，但三个界面
的**未保存状态视觉提示**全部丢失：
- Data Table：编辑过的行不再高亮为黄色
- 可视化界面 (EntityEditorDocument)：标题不显示 `*` 标记
- Value Editor：不显示任何未保存警告

### 根因分析（4 项）
| # | 问题 | 根因 |
|---|------|------|
| 1 | DataGrid 行不高亮 | `BatchEditCommand.Execute()` 未调用 `GenericDataGridHelper.EditedCells.Add()`，WAL 重放后 `EditedEntityIds` 为空 |
| 2 | Tab 标题无 `*` | WAL 恢复后只调 `SetDirty(true)`，但 `_dirtyTabs` 和 `IWorkspaceSession.DirtyEntities` 均未填充 |
| 3 | EntityEditor 标题无 `*` | 构造函数未检查 `DirtyEntities`，无法在打开文档时立即显示脏标记 |
| 4 | Value Editor 无警告 | `KeyValueEditorViewModel.LoadEntity` 的 `FieldRow.IsDirty` 逻辑依赖 `OriginalValue`，而 WAL 恢复后 `OriginalValue` 已被修改为当前值 |

### 修复

| 项目 | 文件 | 说明 |
|------|------|------|
| `Execute()` 追踪编辑 | `Data/Command/BatchEditCommand.cs` | 循环中调用 `GenericDataGridHelper.EditedCells.Add((entityId, columnName))`，确保 DataGrid 行高亮 |
| WAL 恢复后脏标记 | `Views/UserControls/ModGameDataTabsView.axaml.cs` | 新增 `MarkTabsDirtyFromEditedCells()` 方法：遍历 `EditedCells` → 匹配 tab → `MarkDirty()` + 填充 `_dirtyTabs` + 填充 `WorkspaceSession.DirtyEntities`；在 `RestoreCommandsFromLogAsync` 和 `RestoreMergeCommandsFromLogAsync` 两处调用 |
| 保存后清除 | `Views/UserControls/ModGameDataTabsView.Data.cs` | `ShowSavePreviewAsync` 和 `ShowMergeSavePreviewAsync` 结尾加 `WorkspaceSession.DirtyEntities.Clear()` |
| 构造时检查脏状态 | `ViewModels/MainContent/EntityEditorDocument.cs` | 构造函数读 `_session.DirtyEntities.Contains(entity.EntityId)` → 设 `IsDirty=true` + 标题 `"* {subject}"` |
| Value Editor 脏提示 | `ViewModels/MainContent/KeyValueEditorViewModel.cs` | 新增 `IsCurrentEntityDirty` 可观察属性；`LoadEntity` 中检查 `IWorkspaceSession.DirtyEntities` |
| 黄色警告横幅 | `Views/UserControls/KeyValueEditorView.axaml` | 新增顶部 Border：黄色背景 `#FFF3CD` + 文字 "⚠ This entity has unsaved changes. Press Ctrl+S to save."，`IsVisible` 绑定 `IsCurrentEntityDirty` |

### 三端提示对照
| 界面 | 提示方式 | 实现 |
|------|----------|------|
| **Data Table** | 编辑过的行黄色背景高亮 | `EditedCells` → `EditedEntityIds` → `OnLoadingRow` 设 `row.Background` |
| **可视化界面** | 标题前 `*` 标记 | `EntityEditorDocument.IsDirty` → `SetStaticTitle("* {subject}")` |
| **Value Editor** | 顶部黄色横幅 + Ctrl+S 提示 | `IsCurrentEntityDirty` 绑定 `IsVisible` |

### 修改文件
| 文件 | 关键改动 |
|------|---------|
| `Data/Command/BatchEditCommand.cs` | 添加 `using NeoEditor.Helper`；`Execute()` 循环中调 `GenericDataGridHelper.EditedCells.Add()` |
| `Views/UserControls/ModGameDataTabsView.axaml.cs` | 新增 `_workspaceSession` 惰性字段、`WorkspaceSession` 属性、`MarkTabsDirtyFromEditedCells()` 方法 |
| `Views/UserControls/ModGameDataTabsView.Data.cs` | `ShowSavePreviewAsync` / `ShowMergeSavePreviewAsync` 加 `WorkspaceSession.DirtyEntities.Clear()` |
| `ViewModels/MainContent/EntityEditorDocument.cs` | 构造函数检查 `DirtyEntities.Contains(entity.EntityId)` → 设 `IsDirty=true` |
| `ViewModels/MainContent/KeyValueEditorViewModel.cs` | 新增 `IsCurrentEntityDirty` 属性；`LoadEntity` 检查 `session.DirtyEntities` |
| `Views/UserControls/KeyValueEditorView.axaml` | 新增黄色警告横幅 Border + `IsVisible="{Binding IsCurrentEntityDirty}"` |

---

## 脏提示精准化 + Overview 移除 + 引用列交互优先级修复 (v0.23.0-dev) | 2026-07-05

### 问题
三个遗留交互问题：
1. KV Editor "this entity has unsaved changes" 黄色警告在未修改实体上仍出现
2. ProfileOverview 组件已无实际用途，占用 Left 面板空间
3. DataTable 引用列 Ctrl+LMB/Ctrl+RMB 优先级低于数据行点击，导致无论点哪里都是行级导航/Peek

### 脏提示根因
`IsCurrentEntityDirty` 在 `ApplyChanges()` 执行后未清除，用户提交 KV 编辑后切换实体时，
残留标志导致下一实体打开即显示警告。

### 引用列优先级根因
DataGrid 内部 `OnPointerPressed` 在 **Tunnel 阶段**触发，**早于** GDH 引用单元格上的
Tunnel handler，导致 `SelectionChanged` 先发 `EntitySelectedMessage`，
`SuppressNextSelectionChanged` 标志来不及设置。

### 修复

| 项目 | 文件 | 说明 |
|------|------|------|
| KV 脏提示清除 | `ViewModels/MainContent/KeyValueEditorViewModel.cs` | `ApplyChanges()` 末尾设 `IsCurrentEntityDirty = false` |
| 删除 Overview | `ProfileOverviewView.*` / `ProfileOverviewViewModel.cs` | 删除 3 个文件 |
| 删除 Overview 引用 | `DocumentWorkspaceViewModel.cs` / `Documents.cs` / `DocumentWorkspaceView.axaml` | 移除属性声明、Tool 类、XAML Tool 块 |
| 提前设抑制标志 | `Views/UserControls/SearchableDataGrid.axaml.cs` | 构造器中新增 UserControl 级 Tunnel handler，在 DataGrid 内部处理前设 `SuppressNextSelectionChanged=true` |
| 标志残留修复 | `Views/UserControls/SearchableDataGrid.axaml.cs` | Tunnel handler 每次点击先重置 `SuppressNextSelectionChanged=false`，防止上次引用列点击的残留标志阻塞后续数据行 Ctrl+RMB Peek |
| 重复 break 修复 | `Views/UserControls/ModGameDataTabsView.axaml.cs` | 删除 `case Key.Y` 后重复的 `break;` |

### 事件路由时序
```
每次点击的 Tunnel → Bubble 路由：
  ① SearchableDataGrid Tunnel: 重置标志 → Ctrl+LMB 时设=true
  ② DataGrid.OnPointerPressed (内部): 更新选中行
  ③ OnDataGridSelectionChanged: 检查标志 → 抑制 EntitySelectedMessage
  ④ GDH ConfigureColumn Tunnel (引用单元格): 设标志=true → Navigate/Peek
  ⑤ SearchableDataGrid Bubble: 标志未设时 → 行级 Navigate/Peek
```

> **关键**：标志必须在每次新点击周期开始时重置（①），否则上次 Ctrl+LMB 引用列残留的
> `true` 会阻塞下次 Ctrl+RMB 数据行的 Bubble handler（⑤ 直接 return）。

---

## 架构重设计 (v0.23.0-dev) | 2026-06-22

详见 [23-architecture-redesign-proposal.md](23-architecture-redesign-proposal.md) 和 [24-workflow-specification.md](24-workflow-specification.md)

### Phase 1 — 页面体系与侧边栏重构
| 项目 | 说明 |
|------|------|
| 三页面架构 | WelcomePage / WorkspacePage / SettingsPage，通过 `CurrentPage` + `IsVisible` 切换 |
| `NavigateToPageMessage` | 页面导航消息，`PageType.Home/Workspace/Settings` |
| 侧边栏重排 | 7 按钮重排: Home / Data Browser / Mod Database / Profiles / Explorer / References / Settings (三组分隔) |
| 页面自动切换 | `SessionStateChangedMessage` 驱动；Session 活跃 → WorkspacePage；无 Session → WelcomePage |
| `SessionStateChangedMessage` | 新增消息，`DocumentWorkspaceViewModel` 在打开/关闭 Session 时发送 |
| 工具栏 | 顶栏: New Mod + Import Mod + 面板切换按钮 (Left/Right/Bottom) |
| `SettingsPageViewModel` | 独立设置页 VM，含 GoBack 命令 |
| `SettingsPageView.axaml` | 设置页视图：GameRootDir / Language / Theme / FontSize / AutoSave / ExportFormat / GridRowHeight / Column Visibility |

### Phase 2 — 底部三表联动
| 项目 | 说明 |
|------|------|
| `EntitySelectedMessage` | 实体选中消息，驱动全区域联动 |
| 底部 6 Tab | SearchResults / Conflicts / Validation / **DataTable** / **Ref Index** / **Reverse Index** |
| `BottomDataTableViewModel` | 选中实体时加载同类型全部实体到 DataGrid |
| `IndexTableViewModel` | 正向/反向引用索引表，支持 `LoadForwardFromService` + `LoadReverse` |
| `DataTableView.axaml` | DataTable 的 UserControl，编译时类型安全绑定 |
| `IndexTableView.axaml` | 索引 Table 的 UserControl，复用为正向+反向两个实例 |

### Phase 3 — 主视区可视化编辑器
| 项目 | 说明 |
|------|------|
| `EntityEditorDocument` | 双 Tab 文档：Visual (只读可视化) + XML Edit |
| `EntityEditorView.axaml` | 双 Tab 视图，Tab 切换，Refresh 按钮 |
| `EntityXmlHelper` | 反射生成实体 XML 片段 |
| 实体变更自动重建 | View 监听 `PropertyChanged`，Entity 变更时自动 `RebuildVisualizer` |

### Phase 4 — 左侧 Key-Value 编辑器
| 项目 | 说明 |
|------|------|
| `KeyValueEditorViewModel` | 字段分组 (Section) + 字段行 (FieldRow) |
| `FieldSection` / `FieldRow` | 可折叠分组，控件类型适配 (TextBox/Numeric/Toggle/ComboBox/RefPicker/ReadOnly) |
| `FieldGroupMetadata` | 实体类型 → 字段分组元数据 |
| 引用 Peek | `PeekReferenceRequestMessage` → 解析目标实体 → PeekPanel 预览 |
| Apply / Revert | 提交或撤销全部 pending 编辑 |

### Phase 5 — Peek 面板增强
| 项目 | 说明 |
|------|------|
| `PeekPanelViewModel` | 面包屑导航 (最多10), Pin 锁定, Open Full, Split |
| `PeekPanelView.axaml` | 面包屑列表 + 操作按钮 + 内容区 (BuildOverview) |
| 内容自动刷新 | View 监听 VM 的 `PropertyChanged`，`CurrentEntity` 变更时重建 Overview |

### 工作流修正
| 问题 | 修复 |
|------|------|
| HomePage "Browse Game Data" 不切换页面 | → 发送 `OpenDataBrowserMessage` + `SessionStateChangedMessage` → 页面切换到 Workspace |
| 侧边栏 Data Browser 按钮 | → 仍通过 `TogglePane("DataBrowser")` 打开 SplitView 弹出面板（未改为 Dock Document） |
| 侧边栏 Mod Manager 按钮 | → 仍通过 `TogglePane("ModDatabase")` 打开 SplitView 弹出面板（未改为 Dock Document） |

### 新增文件 (~16 个)
| 文件 | 说明 |
|------|------|
| `Data/Messages/PageNavigationMessage.cs` | 页面导航消息 |
| `Data/Messages/WorkspaceMessages.cs` | 工作区消息 (EntitySelected / PeekEntity / FieldEdited / PeekReferenceRequest 等) |
| `Data/Model/FieldGroupMetadata.cs` | 字段分组元数据 |
| `ViewModels/SettingsPageViewModel.cs` | 设置页 VM |
| `ViewModels/MainContent/EntityEditorDocument.cs` | 实体编辑器文档 + EntityXmlHelper |
| `ViewModels/MainContent/KeyValueEditorViewModel.cs` | Key-Value 编辑器 VM + FieldSection/FieldRow |
| `ViewModels/MainContent/PeekPanelViewModel.cs` | Peek 面板 VM + PeekBreadcrumb |
| `ViewModels/MainContent/IndexTableViewModel.cs` | 索引表 VM (正向/反向) |
| `ViewModels/MainContent/ModDataToolViewModel.cs` | 底部 DataTable 的 Context VM |
| `Services/BrowserIndexService.cs` | 浏览器索引服务（从 EntityBrowserDocument 提取） |
| `Services/ReferenceIndexService.cs` | 引用索引服务 |
| `Views/UserControls/SettingsPageView.axaml` | 设置页视图 |
| `Views/UserControls/EntityEditorView.axaml/.cs` | 实体编辑器视图 |
| `Views/UserControls/KeyValueEditorView.axaml/.cs` | Key-Value 编辑器视图 |
| `Views/UserControls/PeekPanelView.axaml/.cs` | Peek 面板视图 |
| `Views/UserControls/IndexTableView.axaml/.cs` | 索引表视图 |
| `Views/UserControls/DataTableView.axaml/.cs` | 数据表视图 |
| `Docs/24-workflow-specification.md` | 工作流规格说明 |

### 修改文件 (~12 个)
| 文件 | 关键改动 |
|------|---------|
| `MainWindow.axaml` | 三页面 visibility 绑定 + 侧边栏 7 按钮三组分隔 + 顶部静态工具栏 |
| `MainWindowViewModel.cs` | `CurrentPage` + `NavigateToPageMessage` + `SessionStateChangedMessage` 接收 |
| `MainWindowSideBarViewModel.cs` | TogglePane 命令 + 6 种 Pane 内容工厂 (Explorer/Search/Settings/ModDatabase/Profiles/DataBrowser) |
| `DocumentWorkspaceViewModel.cs` | 新增 KeyValueEditorVm / PeekPanel / ForwardIndex / ReverseIndex / ModDataToolVm；INavigationRouter.PeekHandler；面板可见性 Toggle 命令；SessionStateChangedMessage 发送 |
| `DocumentWorkspaceView.axaml` | 四区域 ProportionalDock：Left(KeyValueEditor+OverlayChain) + Center(DocumentDock) + Right(Peek/ValueEditorPanel) + Bottom(6工具) |
| `Documents.cs` | 新增 EntityEditorDocument + EntityXmlHelper + 12 个 Tool 类（含 KeyValueEditorTool/PeekPanelTool/DataTableTool/ForwardIndexTool/ReverseIndexTool） |
| `ModGameDataTabsView.Tab.cs` | OnDataGridSelectionChanged → 发送 EntitySelectedMessage |
| `ModGameDataTabsView.Data.cs` | WireActiveGridSelection → 发送 EntitySelectedMessage |
| `ModGameDataMessages.cs` | 新增 OpenDataBrowserMessage / OpenModManagerMessage / VisualEditorRequestedMessage / NavigateToEntityRequestedMessage / SaveRequestedMessage |
| `HomePageViewModel.cs` | BrowseGameData → 发送 OpenDataBrowserMessage |
| `App.axaml.cs` | 注册新 View/ViewModel + DI 服务 |

---

## Stage 23 — IReferenceResolver 接口化 + 可视化本地化 + ValueEditor Peek (v0.22.0-dev) | 2026-06-11

### IReferenceResolver 接口
| 项目 | 说明 |
|------|------|
| 新增 `Helper/IReferenceResolver.cs` | 定义正规引用解析接口：`LookupRef` / `LookupSubject` / `ReverseLookup` / `NavigateTo*` |
| `ReferenceResolver` 重写 | 从 static class → `class : IReferenceResolver`，有 `static Instance`，DI 注册为 singleton |
| ~80 处调用点 | 全部改为 `ReferenceResolver.Instance.xxx` |

### 删除的过时 API
| 删除 | 替代 |
|------|------|
| `FindByKey<T>()` | `LookupRef<T>()` |
| `GetDedupedInt<T>()` | 批量: `GDH.GetEntities<T>()`；单次: `LookupRef<T>()` |
| `GetDedupedComposite<T>()` | `GDH.GetCompositeEntities<T>()` |
| `GetDedupedList<T>()` | `GDH.GetDedupedEntities<T>()` |
| `FindReverseReferences()` (全量扫描 O(n*m)) | `ResolveReverseRefs(store, entityId)` (走 Index.ReverseLookup) |
| `ResolveSubject/ResolveMultiRef/CreateNavItem/WireNavOnCtrlClick` | 删除（零调用） |

### DataGrid ConfigureColumn 统一
| 之前 | 之后 |
|------|------|
| `LookupSubjectByRawId` 自建 30 行 → Index.LookupDisplay → FindBestMatch O(n) 兜底 | 一行委托 `ReferenceResolver.Instance.LookupSubject(...)`，纯 Index |

### ReferenceIndex 磁盘持久化
| 项目 | 说明 |
|------|------|
| `ReferenceIndex.SaveToDisk(path)` | 序列化全部字典（forward/nsForward/reverse/display/merged/bizKey）到 JSON |
| `ReferenceIndex.TryLoadFromDisk(path)` | 从 JSON 恢复，跳过昂贵 BuildAsync |
| `BrowserStore` null 修复 | `TryLoadFromDiskCache` 不再绕过 BrowserStore 创建 |
| `InvalidateIndex` 修复 | 同时删除轻量 cache + Index cache 两个文件 |

### 可视化本地化
| 项目 | 说明 |
|------|------|
| `VisHelper.Loc(key)` | 可视化专用本地化快捷方式，调用 `App.Localizor[key]` |
| 新增 ~30 个 `Vis.*` 资源键 | `Vis.RawData`, `Vis.Stats`, `Vis.Cut`, `Vis.Blunt`, `Vis.Total`, `Vis.Effective`, `Vis.Ammo`, `Vis.AttackerConditions`, `Vis.AttackPhrases`, `Vis.ReferencedBy`, `Vis.CombatMelee/Ranged`, `Vis.Tiles`, `Vis.Base`, 等 |
| `Resources.zh.resx` 翻译修正 | `Morale` → 士气补正；`Vis.AttackerConditions` → 攻击带来的状态 |

### Ctrl+Click Peek 到 ValueEditor
| 项目 | 说明 |
|------|------|
| `ReferenceResolver.NavigateTo` 现在附带 Peek | 调用 `GDH.PeekEntity(type, entityId)` → 发送 `VisualEditorRequestedMessage` |
| `ValueEditorPanel` 接收 | 渲染 `visualizer.BuildOverview(entity)` 到右侧面板 |
| `Router.Navigate` "not handled" 降级 | Warning → Debug（数据浏览器无 INavigationTarget 是正常情况） |

### AttackMode Detail UI 改进
| 改进 | 说明 |
|------|------|
| fMorale 百分比显示 | 公式 `(1+士气)*(1+加成)*伤害`，`fMorale=0.25` 显示 `25% (base)` |
| Effective 伤害行 | 士气加成后有效伤害：`(Cut+Blunt) × (1+fMorale)`，格式 `5.6 (1.25 × 4.5)` |
| Sound 语义图标 | 无图片时根据 Sound 分类显示对应 FluentIcon + emoji |
| 反向引用面板 | 使用 `store.Index.ReverseLookup()` 预建 `_reverse` 字典 |
| 引用徽章 Ctrl+Click | NavigateTo + Peek 到右侧 ValueEditor 面板 |
| 全部标签本地化 | `VisHelper.Loc(key)` 替换硬编码英文字符串 |

### 关键 Bug 修复
| Bug | 修复 |
|-----|------|
| **Detail 引用全部显示 raw 文本** | `LookupRef`/`LookupSubject`/`NavigateToByKeyFor` 只查 `ActiveMergeStore` → 改为 `ActiveMergeStore ?? BrowserStore` |
| **`_indexBuilt=true` 但 BrowserStore=null** | 重构 `RebuildBrowserIndexAsync`，Store 创建后才标记 |
| **`InvalidateIndex` 后每次全量重建** | ReferenceIndex 磁盘持久化 |
| **Ctrl+Click Peek 无反应** | `PeekEntity` 从 `Router.Peek` 改为发送 `VisualEditorRequestedMessage` |

### 文档更新
| 文档 | 更新内容 |
|------|---------|
| `15-reference-system-refactoring-plan.md` | Phase 5/6/7 实施记录 + Bug 记录表 B1-B5 + 过时章节标记 |
| `09-current-status.md` | 引用系统 Phase 1-7，IReferenceResolver 路径图 |
| `14-reference-resolution-system.md` | 新增 IReferenceResolver/ReferenceResolver 文件清单，FindBestMatch 兜底标记过时 |
| `20-data-class-field-reference.md` | fMorale 说明订正 |
| `21-entity-detail-ui-design-guide.md` | 本地化模式、引用解析规范 |
| `Resources.resx` / `Resources.zh.resx` | 新增 ~30 个 `Vis.*` 显示键 |

---

## Stage 22 — ReferenceResolver 清理 + 可视化器统一 LookupRef (v0.22.0-dev) | 2026-06-11

> 此阶段内容已被 Stage 23 包含并扩展，仅保留标题作为归档。



## Stage 21 — Detail UI 设计指南文档 (v0.22.0-dev) | 2026-06-10

### 新增文档
| 项目 | 说明 |
|------|------|
| `21-entity-detail-ui-design-guide.md` | Entity Detail UI 设计参考指南 |

### 文档内容
| 章节 | 涵盖 |
|------|------|
| 布局规范 (7 条规则) | ScrollViewer → Raw Data Expander → Hero Header → 面板优先级 |
| Hero Header 模式 (2 种) | 有图 / 无图两种 Header 布局，组件清单，图片加载逻辑 |
| 数据面板类型 (7 种) | StatBar / StatCard / MiniBadge / 文本面板 / 关系横条 / 配对表 / 反向引用 |
| MiniBadge 标准配色 | 12 种引用目标类型的 bg/fg 配色表 |
| Overview 设计规范 | 260px 窄高布局，组件排版顺序 |
| 引用处理规范 | 解析优先级、默认值跳过规则 |
| VisHelper API 清单 | 11 个共享组件的签名和用途 |
| 既定改进方案 (7 项) | P1 反向引用 → P6 Tooltip 预览 → P7 动作按钮 |
| 设计反模式 (10 条) | 避免用 TreeView 罗列、空面板占位、私有组件等 |
| 类型到面板映射 | 按数据特征选择面板类型的速查表 |
| 新增 Visualizer 清单 | 11 项检查列表 |

---

## Stage 20 — 引用解析修复 + 全局索引持久化 (v0.22.0-dev) | 2026-06-10

### 引用解析修复
| 项目 | 说明 |
|------|------|
| `ReferenceResolver.FindByKey<T>(key, sourceEntity)` | 新方法：同 mod 优先，最高 ModId 兜底，不依赖 ReferenceIndex |
| `LookupRef` fallback 修复 | 从 `ReferenceField` attribute 读取 pattern 提取 ID，处理命名空间前缀 |
| 全部 visualizer 切换 | `GetDedupedInt` → `FindByKey`，`NavigateToByKeyFor` → `NavigateTo(typeof(T), entityId)` |

### 全局浏览器索引持久化
| 项目 | 说明 |
|------|------|
| `GDH.BrowserStore` | 全局 static 单例 `EntityMergeStore`，应用启动时构建 |
| `EntityBrowserDocument.GlobalBrowserCache` | `Dictionary<Type, Dictionary<int, CacheEntry>>`，序列化到 `browser_index_cache.json` |
| 磁盘缓存 | `%LocalAppData%/NeoEditor/browser_index_cache.json`，重启毫秒级加载，无需 rebuild |
| `EnsureIndexBuiltAsync()` | 去重防并发，`EntityViewerView` 渲染前等待索引就绪 |
| `InvalidateIndex()` | 删除磁盘缓存 + 清内存，Profile/Mod 变更时触发 |

### 字段标签订正
| 类型 | 修正内容 |
|------|---------|
| Encounter | `Story` → `Normal`（符合 EncounterType 枚举） |
| Condition | `Permanent` → `Instant`（瞬时的/一次性施加），`Temporary` → `Duration`，Color 加正负面标注 |
| BattleMove | 新增 `StrId` 徽章，`See Us/Them` → `Exposure`，新增 `AI Order` |
| Recipe | Hero Header 新增 `DegradeOutput: On/Off` |
| Creature | Faction 名称解析（不再仅显示 #ID） |
| Encounter | 新增 `RemoveTreasureId` 引用面板 |

### 修改文件
| 文件 | 关键改动 |
|------|---------|
| `Helper/ReferenceResolver.cs` | 新增 `FindByKey<T>()` 返回 `(Subject, EntityId)?` |
| `Helper/GenericDataGridHelper.cs` | 新增 `BrowserStore`, `ReferenceLookups` 回退链 |
| `ViewModels/MainContent/Documents.cs` | `BrowserIndexCacheEntry`, `GlobalBrowserCache`, 磁盘缓存序列化 |
| `Views/.../EntityViewerView.axaml.cs` | 异步 `BuildContentAsync` 等待索引 |
| `Views/.../Editors/EntityVisualizers.cs` | 全部 `FindByKey` 调用点更新 |
| `App.axaml.cs` | 启动时 `FireAndForget(RebuildBrowserIndexAsync)` |

---

## Stage 19 — 全类型可视化器卡式重设计 (v0.21.0-dev) | 2026-06-10

### 可视化器全面升级
所有 25 个实体类型的 `BuildDetail` 和 `BuildOverview` 均按 AttackMode 的 Card 模式重写：

| 类型 | Detail | Overview |
|------|--------|----------|
| **Recipe** | Hero Header（名称+类型标签+Hours/Reverse）+ 原料徽章面板（Tools/Consumed/Destroyed）+ 产品预览 + AlsoTry 备选配方 | 类型标签+中心名称+Stats卡(Hours/Reverse/Hidden/Tools/Consumed) |
| **TreasureTable** | Hero Header（ID+Nested/Suppress/Identify标签）+ 战利品概率面板（每项含物品名/概率徽章/数量）| 中心名称+标签+Stats卡(OR组数/物品总数) |
| **Encounter** | Hero Header（图片+ID+剧情类型标签）+ 剧情文本面板 + 回应面板 + 引用面板（战利品/状态/前置条件/生物/传送/意外） | 图片缩略图+类型标签+名称+剧情摘要+Stats卡(Price/Type/Loot/Accident/Creature) |
| **Creature** | Hero Header（图片+ID+Moves标签）+ 派系/攻击方式/基础状态/遭遇状态/战利品/尸体战利品徽章面板 + 活动描述 | 图片缩略图+名称+公开名+Stats卡(Moves/Faction/Attacks) |
| **Condition** | Hero Header（ID+致命/永久/堆叠标签+持续时间/颜色/传染范围）+ 描述 + FieldNames→Modifiers 三列配对表 + 效果文本 + 下一阶段条件链徽章 | 严重级别徽章+名称+Stats卡(Duration/Color/Transfer)+下一阶段数 |
| **BattleMove** | Hero Header（ID+行为标签标志+类型/几率/优先级/疲劳/探测/范围/视野）+ 描述/成功/失败文本面板 + 全部条件组(8组)徽章面板 | 行为类型徽章+名称+Stats卡(Type/Chance/Priority/Fatigue/Detect/Range) |
| **HexType** | Hero Header（ID+可通行标签+移动消耗/能见度/遭遇范围）+ 光线等级六列表 + 战利品/营地/进入状态引用徽章面板 | 可通行标签+名称+Stats卡(Cost/Visibility/EncRange) |
| **Faction** | Hero Header（ID）+ 外交关系横条面板（名称+彩色关系条+数值+描述）+ 成员生物徽章面板 | 名称+Stats卡(关系数/成员数) |
| **Ingredient** | Hero Header（ID）+ 必需属性/禁止属性徽章面板 + 反向引用（哪些Recipe使用） | 名称+Stats卡(Required/Forbidden属性数) |
| **ItemProp** | Hero Header（ID+属性名）+ 反向引用（被哪些实体引用）徽章面板 | 属性名+ID标签 |
| **EncounterTrigger** | Hero Header（ID+触发类型标签+几率标签）+ 区域/日期范围 + 遭遇/HexType引用徽章面板 | 触发类型徽章+名称+Stats卡(Chance/Encounter) |
| **CampType** | Hero Header（图片+ID+容量标签）+ 营地Stats卡（Capacity/Alertness/Sleep/Heal）+ 战利品引用 | 图片缩略图+名称+Stats卡(Sleep/Heal/Visibility/Alertness) |
| **ChargeProfile** | Hero Header（ID+可降级标签+物品ID）+ 消耗率Stats卡（PerUse/PerHour/PerHrEquipped/PerHex）| 名称+降级标签+速率概要+物品ID |
| **ContainerType** | Hero Header（ID+名称）+ 反向引用（哪些ItemType使用） | 名称+ID标签 |
| **CreatureSource** | Hero Header（ID+坐标/数量标签+权重）+ 生物引用徽章面板 | 名称+Stats卡(Position/Count/Weight) |
| **DmcPlace** | Hero Header（图片+ID+坐标标签）+ 遭遇引用徽章面板 | 图片缩略图+名称+Stats卡(Position/Encounter) |

### 新增可视化器（6个此前无 visualizer 的类型）
| 类型 | Detail | Overview |
|------|--------|----------|
| **BarterHex** | Hero Header（ID+Buy标签+坐标+RestockTT）| 商店类型标签+名称+Stats卡(Position/RestockTT) |
| **DataFile** | Hero Header（图片+ID+价值标签）+ 数据内容文本面板 | 图片缩略图+名称+价值标签 |
| **GameVar** | Hero Header（类型标签+名称+值）| 名称+类型标签+Value |
| **Headline** | Hero Header（ID）+ 报纸标题文本面板 | 名称+标题预览 |
| **ForbiddenHex** | Hero Header（ID+Forbidden标签+坐标）| 名称+Stats卡(Position) |
| **Map** | Hero Header（ID+数据点数）+ 地图定义文本面板 | 名称+数据点数 |

### 共享组件
| 组件 | 位置 | 说明 |
|------|------|------|
| `VisHelper.StatBar` | VisHelper | 进度条组件（从AttackMode提取） |
| `VisHelper.BuildExpander` | VisHelper | 可折叠面板组件（从AttackMode提取） |
| `VisHelper.OvSectionLabel` | VisHelper | Overview章节标签（从AttackMode提取） |
| `VisHelper.BuildStatCard` | VisHelper | 键值对Stats卡片 |
| AttackMode 清理 | 移除私有 StatBar / BuildExpander / OvSectionLabel 重复实现 |

### 注册更新
- `App.axaml.cs` 新增 6 个 visualizer 注册：BarterHex / DataFile / GameVar / Headline / ForbiddenHex / Map

### 修改文件
| 文件 | 关键改动 |
|------|---------|
| `Views/.../Editors/EntityVisualizers.cs` | 19个现有 visualizer 全部重写为Card模式 + 6个新增 visualizer + VisHelper 共享组件 |
| `App.axaml.cs` | 注册 6 个新 visualizer |

---

## Stage 17 — 引用系统重构 Phase 3+4 + 列可见性 + 行高稳定 (v0.19.0-dev) | 2026-06-10

### 引用导航系统重构 (Phase 3 — 导航层)
| 项目 | 说明 |
|------|------|
| `INavigationTarget` | 导航目标接口：`CanNavigate` / `NavigateTo` / `Priority` |
| `INavigationRouter` | DI 单例路由器：`RegisterTarget` / `UnregisterTarget` / `Navigate` / `Peek` |
| `NavigationRouter` | 责任链实现，Priority 降序，稳定排序，同 Priority 下最近附加优先 |
| `ModGameDataTabsView` 实现 `INavigationTarget` | Attach 注册 / Detach 注销，Priority=50，CanNavigate 检查 Tab 匹配 |
| `DocumentWorkspaceViewModel` | PeekHandler 从 GDH 静态委托迁移到 `INavigationRouter.PeekHandler` |

### 引用导航系统重构 (Phase 4 — GDH 清理)
| 项目 | 说明 |
|------|------|
| 移除 `_activeViews` / `RegisterNavigateTarget` | 替代为 `INavigationRouter.RegisterTarget` |
| 移除 `PeekRequested` 静态委托 | 替代为 `INavigationRouter.PeekHandler` |
| 移除 `IsPeekPinned` / `NavigateToImpl` | 不再需要 |
| `NavigateToReferenceForce` 改为委托路由器 | 解析 EntityId → Router.Navigate + Router.Peek |
| `NavigateTo` / `NavigateToByEntityId` 保留 | 改为通过路由器+索引查找，供外部调用者使用 |

### DataGrid 改进
| 项目 | 说明 |
|------|------|
| 行高虚拟化抖动修复 | `OnLoadingRow` 中每行独立计算高度（基于多值引用段数），直接设 `row.Height`，绕过列虚拟化测量 |
| 列虚拟化关闭 | `SearchableDataGrid.axaml` 加 `EnableColumnVirtualization="False"`（11.3 不支持，已移除） |
| `SwitchTabItemsSource` NRE 修复 | 大数据量切 tab 时 DataGrid 内部 `RemoveAutoGeneratedColumns` NRE — 先 `AutoGenerateColumns=false` 再设 ItemsSource，延迟恢复 |
| Mod 列 `SortMemberPath` | 补上 `SortMemberPath = "Mod"`，使列管理器能保存/恢复其可见性 |

### 列可见性全局配置
| 项目 | 说明 |
|------|------|
| `ColumnVisibilityKeys` | 统一数据源：`GetKeys(entityType)` 返回全部列 key（实体属性 + ModId/FilePath/EntityId + MergedId + Mod） |
| 侧边栏设置面板 | `Expander "Column Visibility"` + 每表 Expander + CheckBox 列表 + All/None 按钮 |
| 双向实时同步 | 两边都是增量 Add/Remove → 发送 `ColumnVisibilityChangedMessage` → DataGrid 收到即时更新 |
| 默认全可见 | 不再是 "默认隐藏 ModId/FilePath/EntityId"，全部列默认可见 |
| 移除硬编码 hiddenProps | DataGrid `OnAutoGeneratingColumn` 改用 `ColumnVisibilityKeys.IsVisible()` |

### ItemType Overview 可视化
| 项目 | 说明 |
|------|------|
| 重写 `BuildOverview` | 适配窄高面板 (~260px)：居中 88px 缩略图 + 身份 + Stats 两列网格 + Properties 标签 + Equipment / Container / Degrade / Refs / ReverseRefs 卡片 |

### 新增文件
| 文件 | 说明 |
|------|------|
| `Helper/INavigationTarget.cs` | 导航目标接口 |
| `Helper/INavigationRouter.cs` | 导航路由器接口 |
| `Services/NavigationRouter.cs` | 导航路由器实现 |
| `Helper/ColumnVisibilityKeys.cs` | 列可见性统一 key 源 |

### 修改文件
| 文件 | 关键改动 |
|------|---------|
| `Helper/GenericDataGridHelper.cs` | 移除静态导航状态，委托给 Router |
| `Views/.../SearchableDataGrid.axaml` / `.cs` | 列可见性配置恢复、行高冻结、合成列支持 |
| `Views/.../ModGameDataTabsView.axaml.cs` / `Tab.cs` | 实现 INavigationTarget、ToggleColumnVisibility 增量更新 |
| `ViewModels/.../DocumentWorkspaceViewModel.cs` | PeekHandler 迁移到 Router |
| `ViewModels/.../SettingsPaneViewModel.cs` | 列可见性配置 + TableColumnGroup/ColumnOption |
| `Views/.../Pane.axaml` | Column Visibility Expander + All/None 按钮 |
| `Views/.../Editors/EntityVisualizers.cs` | ItemType BuildOverview 重写 |
| `App.axaml.cs` | 注册 INavigationRouter DI |
| `ViewModels/.../ReferenceInspectorContent.cs` | 移除 IsPeekPinned 引用 |
| `Data/Messages/AppConfigMessages.cs` | 新增 ColumnVisibilityChangedMessage |

### 已知限制
- .NET 10.0 SDK 未安装，本次改动无法本地编译验证（用户侧 Rider 编译通过）
- `PersistColumnVisibility` 已改为增量 `ToggleColumnVisibility`，旧方法保留但不再调用

---

## Stage 16 — ItemType 卡片式可视化 + 数据浏览器三层结构 (v0.18.0-dev) | 2026-06-06

### 数据浏览器三层结构
| 项目 | 说明 |
|------|------|
| 侧边栏：大类 + 数据类 | DataBrowser 恢复为纯 Domain→EntityType 按钮，点击在 Dock 开标签页 |
| Dock 标签页：ListBox + 查看区 | `EntityBrowserDocument` 内含左 ListBox（实体列表）+ 右查看区 |
| 实体查看：独立 Dock 文档 | `EntityViewerDocument` + `EntityViewerView` 渲染，`EntityVisualizerRegistry.BuildDetail()` |

### 新增文件
| 文件 | 说明 |
|------|------|
| `EntityViewerView.axaml` + `.cs` | 实体可视化 UserControl，接收 `EntityViewerDocument`，调用 `BuildDetail()` |
| `Documents.cs` | 新增 `EntityViewerDocument : DocumentViewBase` |

### ItemType Detail 卡片式重设计
| 区域 | 内容 |
|------|------|
| Hero Header | 左：132px 主图 + 多图时可切换画廊（◀ ▶ 圆点指示器）；右：ID 徽章 + 名称 + 显示名 + 鉴定名（橙色提示框） |
| Stat Bars | 水平进度条：Weight / Stack / Durability / Value + Mirrored，Grid 星号比例列防文字裁剪 |
| Property Tags | ItemProp 引用解析 → 绿色圆角徽章，Ctrl+Click 跳转 |
| Equipment Card | EquipSlots 徽章 + 装备/使用/携带 Condition 引用解析 |
| Container Card | Capacities + FormatId + ContentIds 解析 |
| Degrade / Charge Cards | 磨损参数 + 破损掉落 TreasureTable 引用 + ChargeProfile 引用 |
| Reference Bars | 横向链接条显示 resolved Subject（TreasureTable/Condition/Component），Ctrl+Click 导航 |
| Reverse Refs | 列出引用本 ItemType 的其他实体，`[类型名] subject` 格式 |

### 图片逻辑
- 字段含逗号 → list → 始终显示画廊组件（含 ◀ ▶ 切换）
- 字段不含逗号 → 单值 → 直接 ImageView

### 编译修复
| 问题 | 修复 |
|------|------|
| `BottomToolsView` / `DataBrowserView` AXAML `ElementName` 绑定 `DataContext` 丢失类型 | 新增 typed 属性（`SearchRecentTyped` / `OpenEntityTypeTyped`），AXAML 改 `#Root.xxxTyped` |
| `EntityViewerView` 缺少 `ScrollBarVisibility` | 补充 `using Avalonia.Controls.Primitives` |
| `Documents.cs` `Id` 赋值 | 移除不存在的 `Id` 属性赋值 |
| WrapPanel `Spacing` | Avalonia 不支持，改用 `Padding` 实现间距 |
| `Math.Clamp` 实例调用 | 改为静态调用 `Math.Clamp(value, min, max)` |

### 已知限制
- **嵌套 Dock**：`DomainBrowserView` 内嵌 `DockControl`（左ListBox + 右Dock查看器）始终无法渲染。尝试方案包括 `InitializeFactory`/`InitializeLayout`/inline layout/DI Factory 注入/`ElementName` 绑定等，在 `Dock.Avalonia 11.3.11.16` 版本上均失败。当前退回 `TabControl` 方案保持功能可用。
- 拆分对比通过主 Dock 标签页拖拽实现（同一类型开两个 `EntityBrowserDocument`）

---
  
## Stage 15 — UI 重塑 + 数据浏览器 + 可视化架构 (v0.17.0-dev) | 2026-06-06

### UI 改进
| 项目 | 说明 |
|------|------|
| FontSize 全局生效 | App.axaml 添加 `AppFontSize` DynamicResource + Window Style；设置面板修改即时应用 |
| 工具栏图标统一 | 导航/操作按钮 Unicode → FluentIcons（ArrowUndo/ArrowRedo/ArrowLeft/Target/Add/Subtract） |
| 面板切换图标 | MainWindow 面板切换 Unicode(◀▶▼) → FluentIcons(PanelLeft/Right/Bottom) |
| HomePage 图标 | 表情符号(📖✨📥) → FluentIcons(BookOpen/DocumentAdd/ArrowDownload) + CardButton 样式 |
| Recent Mods | 移除硬编码 IsVisible=False，绑定 HasRecentMods |
| NumericUpDown | int/float/double 编辑用 NumericUpDown 替代 TextBox，提取 CreateEditControl |
| GridRowHeight 即时生效 | GridRowHeightChangedMessage 驱动，SearchableDataGrid 监听即时更新 |
| 合并视图空状态 | 移除 !IsMergeView 限制 |
| 侧边栏重设计 | 48px 固定宽度、Background 背景、三组分隔、FontSize=18 图标 |
| Import 简化 | 只弹 FolderPicker，取消后不再弹 FilePicker |

### 数据浏览器（新建）
| 项目 | 文件 |
|------|------|
| GameDomain — 7 领域分组 | `Helper/GameDomain.cs` |
| DataBrowserViewModel — 侧边栏领域→实体类型树 | `ViewModels/ExplorerPane/DataBrowserViewModel.cs` |
| DataBrowserView — 侧边栏面板 | `Views/UserControls/DataBrowserView.axaml` + `.cs` |
| EntityBrowserDocument — Dock 标签页文档 | `ViewModels/MainContent/Documents.cs` |
| DomainBrowserView — 标签页视图：左实体列表 + 右可视化 TabControl | `Views/UserControls/DomainBrowserView.axaml` + `.cs` |
| 侧边栏集成 + DataTemplate 注册 | `MainWindow.axaml`, `MainWindowSideBarViewModel.cs`, `DocumentWorkspaceView.axaml` |

### 可视化架构（新建）
| 项目 | 文件 |
|------|------|
| IEntityVisualizer 接口 | `Helper/IEntityVisualizer.cs` |
| EntityVisualizerRegistry | `Services/EntityVisualizerRegistry.cs` |
| 5 个 Visualizer 实现 | `Views/UserControls/Editors/EntityVisualizers.cs` |
| ValueEditorPanel 集成 | 优先用 visualizer.BuildOverview，回退 CustomEditorRegistry |

### Search Tab
| 项目 | 文件 |
|------|------|
| ISearchService 接口提取 + CancellationToken | `Services/ISearchService.cs`, `SearchService.cs` |
| BottomToolsViewModel + SearchPaneViewModel 去重 | 两个 ViewModel 统一注入 ISearchService |

### Encounter 叙事编辑器
| 项目 | 说明 |
|------|------|
| StoryTreeEditor 完全重写 | 4 标签页：Story Flow（左树+右详情编辑）、Text Editor（叙事文本编辑）、Overview、Flowchart |
| EncounterTrigger 集成 | 详情面板显示 "Triggered By" |

### 架构提升
| 项目 | 文件 |
|------|------|
| IFilterService 接口独立文件 + DI | `Services/IFilterService.cs` |
| GameDataTypeTabItem 拥有 stores | `ViewModels/MainContent/GameDataTypeTabItem.cs` |
| ActivateDocument → public | `DocumentWorkspaceViewModel.cs` |

### 本地化
| Key | 中文 | 英文 |
|-----|------|------|
| DataBrowserTitle | 数据浏览器 | Data Browser |
| DataBrowserProperties | 属性 | Properties |
| DomainCoreItems/Combat/Crafting/Loot/Story/Map/Other | 核心物品/战斗/合成/战利品/剧情/地图/其他 | ... |
| RightPanelEditor | 可视化概览 | Overview |
| ValueEditorTitle | 可视化概览 | Visual Overview |

### 新增文件（本轮 ~12 个）
`GameDomain.cs`, `DataBrowserViewModel.cs`, `DataBrowserView.axaml` + `.cs`, `DomainBrowserView.axaml` + `.cs`, `IEntityVisualizer.cs`, `EntityVisualizerRegistry.cs`, `EntityVisualizers.cs`, `ISearchService.cs`, `IFilterService.cs`

### 已知限制
- IMessenger.Send 单参数重载不可用（CommunityToolkit.Mvvm 8.4.0），EntityBrowserDocument 绕过 Messenger 直接操作 DocumentWorkspaceViewModel
- Visualizer 内容为纯文本骨架，需要注入图片/引用树/图表等真正可视化组件（详见 10-next-priority-plan.md）

---

## Stage 14 — 架构重构 (v0.16.0-dev) | 2026-06-06

> 依据 `Docs/13-architecture-critique.md` 执行

### Phase A: 止血

| 项目 | 说明 |
|------|------|
| A1 消息统一 | 14 个静态事件/Action 迁移到 `WeakReferenceMessenger`，新增 15 个消息 record。`IMessenger` 改为 Singleton |
| A2 GDH 去静态化 | `SearchableDataGrid` 持有 `MergeStore`/`EditStore`，挂载时推送。移除 11 个 `_fallback*` 后备集合。`PushEditStateToGrid` 显式调用 `SetActiveStores` |
| A3 链路追踪 | `ViewModelBase` 新增 `ViewId` Guid + `IdPrefix` |
| A4 日志分层 | `LoadingRow`/`CellEditEnd`/`PushEdit`/`ModFilter` 等 12 处降级为 `LogDebug` |
| A5 异步异常 | `AsyncHelper.FireAndForget()` 替换 17 处 `_ = AsyncMethod()` |

### Phase B: 解耦

| 项目 | 说明 |
|------|------|
| B1 MergeService | 200 行合并算法从 `ReloadMergeTabsAsync` 提取到 `Services/MergeService.cs`，返回不可变 `MergeResult` |
| B2 命令序列化 | `ISerializableCommand` 接口，4 命令类型自序列化。`BatchEditCommand` 用 `EditRecord` 替换 `ValueTuple`。`CommandSerializer` 零反射 |
| B3 服务接口 | `IXmlParser` / `IImageService` / `IFilterService` / `IMergeService` 接口 + DI 注册 |
| B4 拆分 View | `GameDataTypeTabItem` 提取到 `ViewModels/MainContent/GameDataTypeTabItem.cs` |
| B5 Console.WriteLine | 18 处替换为结构化 Serilog 日志 |

### Bug 修复

| Bug | 修复 |
|-----|------|
| 标签页切换重新加载 | `OnPropertyChanged` 中比较 `ModId`/`ProfileId` + `Tabs.Count > 0` |
| 行背景消失 | `CellEditEnding` 同步更新 `SearchableDataGrid._editedEntityIds` |
| ShowAll / 覆盖数据显示 | `RebuildFilteredItemsSources` 后更新 `SharedDataGrid.ItemsSource`；直接清除 `MergeStore`/`EditStore`；`PushEditStateToGrid` 显式调用 `SetActiveStores` |
| TabControl 内容区纯文本 | 添加 `TabControl.ContentTemplate` 含空 `Panel` |

### 移除

- `DepBtn` / `ConflictBtn` 及其所有相关代码（反复出现的空白 bug，无法根除）
- `ConflictDisplayText` / `ConflictCount` 属性
- `OnShowDependenciesClick` / `OnShowConflictsClick` 方法
- `UpdateConflictButtonStyle` 方法

### 新增文件
| 文件 | 说明 |
|------|------|
| `Data/Messages/ModGameDataMessages.cs` | 8 个 merge-view 消息 |
| `Data/Messages/GridInteractionMessages.cs` | 6 个 DataGrid 交互消息 |
| `Helper/AsyncHelper.cs` | Fire-and-forget 安全包装 |
| `Data/Command/ISerializableCommand.cs` | 命令自序列化接口 |
| `Data/Command/EditRecord.cs` | 替换 ValueTuple 的命名结构 |
| `ViewModels/MainContent/GameDataTypeTabItem.cs` | 从 View 提取的 Tab VM |
| `Services/MergeResult.cs` | 不可变合并结果 |
| `Services/MergeService.cs` | 合并算法服务 |

### 修改文件（~20 个）
主要涉及：`ModGameDataTabsView.axaml` + `.cs`、`GenericDataGridHelper.cs`、`DocumentWorkspaceViewModel.cs`、`SearchableDataGrid.axaml.cs`、`App.axaml.cs`、`CommandSerializer.cs`、4 个 Command 类、`FilterService.cs`、`ImageService.cs`、`XmlParser.cs`、`PhpParser.cs`、`ViewModelBase.cs`、`RightPanelView.axaml.cs`、`BottomToolsView.axaml.cs`、`Pane.axaml.cs`、`FindReplacePanel.axaml.cs`、`SettingsPaneViewModel.cs`、`ModIndexViewModel.cs`、`HomePageViewModel.cs`、`ConfigService.cs`、`ModManager.cs`、`ModEntryDropHandler.cs`、`Documents.cs`、`ValueEditorPanel.axaml.cs`

### Stage 14 补充修复 (2026-06-06)

| Bug | 修复 |
|-----|------|
| TreasureTable aTreasures: `582x.01x1`/`596x.04x1` 无法解析 | `ParseSingle` 中 `LastIndexOf('x')` → `IndexOf('x')`，正确提取多段 x 格式的第一个 ID |
| aTreasures Ctrl+Click/PeeK 未尝试 SecondaryTarget | 多值和单值 Ctrl+Click/Peek 查询 `SecondaryTargetEntityType`（TreasureTable）fallback；新增 `ResolveWithSecondary` 统一查找 |
| Ctrl+C/V 编辑模式无效 | 全局 KeyDown handler 检测编辑中的 TextBox → 放行原生复制/粘贴；新增 `IsEditingTextBoxFocused` 辅助方法 |
| `UpdateConflictButtonStyle` 残留调用编译错误 | Stage 14 移除 ConflictBtn 后遗留的 3 处调用已清理 |
| 文件丢失恢复 | 误用 `git checkout` 导致丢失未提交改动，已从 Rider Local History 完整恢复 |

### 文件拆分
`ModGameDataTabsView.axaml.cs` 从 3063 行拆分为 4 个 partial class 文件：
| 文件 | 行数 | 职责 |
|------|:--:|------|
| `ModGameDataTabsView.axaml.cs` | 1224 | 构造函数、属性、导航、键盘、复制粘贴、查找面板、Workspace Persistence |
| `ModGameDataTabsView.Operations.cs` | 489 | 保存管道、实体 CRUD、CSV 导入导出、FindReferences |
| `ModGameDataTabsView.Tab.cs` | 367 | Tab 管理、生命周期、列管理器、属性变更 |
| `ModGameDataTabsView.Data.cs` | 1109 | 数据加载、合并视图加载、过滤器、依赖分析、XML 工具 |

| 文档 | 更新 |
|------|------|
| 09-current-status.md | 完全重写，移除重复内容；更新版本 v0.16.0-dev；新增架构说明和文件拆分信息 |
| 10-next-priority-plan.md | 完全重写；标记已完成项（Save & Launch、XML 直接打开、DiffView、ModGameDataTabsView 拆分）；重组优先级 |

---

## Stage 13 — Snapshot + Command Log 持久化 (v0.15.0-dev) | 2026-06-06

### 架构：DB 定位为透明持久化缓存

**DB 不再是用户交互面，而是辅助层：**
- game.db 负责持久化编辑中的更改、加速加载与合并计算
- editor.db 新增 `command_log` + `workspace_snapshot` 表，存储编辑历史和快照指针
- 用户不直接感知 DB，操作以 XML 为中心
- ModDatabase 面板保留但降级为辅助视图

### Snapshot + Command Log 系统
| 功能 | 说明 |
|------|------|
| Command 持久化 | 每个 EditCell/AddEntity/DeleteEntity/BatchEdit 命令执行后实时写入 `command_log` |
| Periodic Snapshot | 每 N 步（`AppConfig.SnapshotInterval`，默认 10）全量写 game.db + 更新 snapshot 指针 |
| Quick Save = Snapshot | Ctrl+S 保存到 DB 后自动更新 snapshot 指针 |
| Save & Export 清理 | 完整保存（DB + XML）后清除 snapshot + command_log（XML 成为权威数据源） |
| 崩溃恢复 | 重载时 Load game.db → 重放 snapshot 之后的 command_log → 完整恢复未保存编辑 |
| Undo/Redo 恢复 | 重放命令同时重建 undo/redo 栈 |

### 命令序列化 (`CommandSerializer`)
- `EditCellCommand` / `AddEntityCommand` / `DeleteEntityCommand` / `BatchEditCommand` ↔ JSON
- 实体属性全量序列化（反射遍历 `[Column]` 属性），类型安全反序列化（`ValueConverter`）
- 重放时通过 EntityId + 实体类型查找实体、通过 Tab 类型查找集合

### DB 迁移
- `RunEditorDbMigrations()` — 启动时 `CREATE TABLE IF NOT EXISTS`，兼容已有 `editor.db`
- `command_log`：id, target_type, target_id, sequence, command_type, serialized_data, is_unsaved, created_at
- `workspace_snapshot`：id, target_type, target_id, last_command_sequence, created_at
- 索引：`(target_type, target_id)` + unique snapshot index

### 缓存恢复架构修复（合并视图标签页切换数据保持）

**问题**：Dock.Avalonia 切换标签页时会重建 View 实例，合并视图每次做完整 DB 重载，编辑数据丢失。

| 修复 | 说明 |
|------|------|
| `TabSnapshotCache` 覆盖合并视图 | 之前只给单 Mod 用，现在两者统一用缓存做纯内存恢复，不碰 DB |
| `EntityMergeStore.MergeSpaceModIds` | 合并空间 ModId 集合从 View 私有字段移入 Store，跟缓存一起走 |
| 缓存 store 替换 | 命中缓存时 `EditStore = cached.EditStore; MergeStore = cached.MergeStore` 替换 View 字段，消除多 View 并发 store 竞争 |

**行背景色丢失修复**：
- `SearchableDataGrid` 新增自有属性 `EditedEntityIds` / `OverriddenEntityIds` / `NewEntityIds`，解耦 `GenericDataGridHelper` 全局状态
- `PushEditStateToGrid()` 在 `LoadingRow` 触发前推送到 DataGrid 本地属性
- `RefreshRowBackgrounds()` 主动遍历行重设背景
- `OnAttachedToVisualTree` 重挂载路径用 `DispatcherPriority.Loaded` 延迟刷新

### 日志增强
- Serilog rolling file 改为小时级（`RollingInterval.Hour`，保留 72 个文件）
- `GenericDataGridHelper.SetActiveStores` 加入 store hash 和内容追踪日志
- `SearchableDataGrid` 注入 `ILogger`，所有 `LoadingRow` / `CellEditEnding` / `RefreshRowBackgrounds` 追踪日志写入文件
- 调试状态栏：工具栏下方显示 `Snap:seq=N | CmdLog:N | Unsv:N | Seq:N`，点击弹出详细 command_log 列表

### 新增文件
| 文件 | 说明 |
|------|------|
| `Data/Model/CommandLog.cs` | command_log 表实体 |
| `Data/Model/WorkspaceSnapshot.cs` | workspace_snapshot 表实体 |
| `Services/CommandSerializer.cs` | 命令 ↔ JSON 序列化/反序列化（4 种命令类型 + 实体属性全量） |
| `Services/WorkspacePersistenceService.cs` | Snapshot + Command CRUD（`IWorkspacePersistenceService` 接口） |

### 修改文件
| 文件 | 关键改动 |
|------|---------|
| `Data/Context/EditorDbContext.cs` | +2 DbSet + OnModelCreating 配置 + 索引 |
| `Data/Command/ICommandHistory.cs` | + `RestoreFromLog()` |
| `Data/Command/CommandHistory.cs` | + `OnCommandPersist` 回调 + `RestoreFromLog` (跳过 Execute) + `TrimHistory` 提取 |
| `Services/EntityMergeStore.cs` | + `MergeSpaceModIds` |
| `ViewModels/AppConfig.cs` | + `SnapshotInterval` (默认 10，0=关闭) |
| `App.axaml.cs` | + `RunEditorDbMigrations` + `IWorkspacePersistenceService` DI 注册 |
| `Helper/GenericDataGridHelper.cs` | `SetActiveStores` 加追踪日志 |
| `Views/UserControls/SearchableDataGrid.axaml.cs` | + `EditedEntityIds` / `OverriddenEntityIds` / `NewEntityIds` 属性 + `RefreshRowBackgrounds` + ILogger 注入 |
| `Views/UserControls/SearchableDataGrid.axaml` | 无改动 |
| `Views/UserControls/ModGameDataTabsView.axaml` | + 调试状态栏 Border + TextBlock |
| `Views/UserControls/ModGameDataTabsView.axaml.cs` | 核心集成：Command 持久化、Snapshot 周期、缓存恢复、行背景修复、Store 替换、错误处理 |
| `Program.cs` | Rolling 改为 Hour，retained 72 |
| `Helper/Extensions/LoggingExtensions.cs` | Rolling 改为 Hour，retained 72 |

### 已知限制
| 问题 | 状态 |
|------|:--:|
| 多 View 并发挂载 store 竞争 | ✅ 已通过缓存 store 替换修复 |
| 行背景色切换标签页丢失 | ✅ 已通过解耦属性 + 主动刷新修复 |
| 排序箭头不显示 | 🔴 Avalonia 11.3 框架限制 |

---

## Stage 12 — 高性价比快速迭代 (v0.14.0-dev) | 2026-06-05~06

### P0-1: Save & Launch Game
| 功能 | 说明 |
|------|------|
| Save & Launch 按钮 | 工具栏 [▶ Launch] 按钮，先保存再启动 NEOScavenger.exe |
| Ctrl+Shift+S | 快捷键触发 Save & Launch |
| 路径推导 | 从 `AppConfig.GameRootDir` + `NEOScavenger.exe` 自动拼接 |

### P0-2: DiffView 导航增强
| 功能 | 说明 |
|------|------|
| 双编辑器跳转 | 通过 `IsFocused` 判断焦点在新/旧编辑器，用对应行号查找 diff |
| 直接输入 index | 导航栏 TextBox 输入数字 → Enter/LostFocus 跳转 |
| 展示优化 | 导航栏 `#/total` 格式 |

### P0-3: XML 直接打开
| 功能 | 说明 |
|------|------|
| Import 支持 XML 文件 | FolderPicker → 取消后自动 FilePicker(*.xml) |
| Drop 已有处理 | 拖 XML 文件取父目录为 modPath |

### 保存流程重构
| 功能 | 说明 |
|------|------|
| Quick Save (Ctrl+S) | 仅写 DB，秒级完成，不弹 diff |
| Export 按钮 | 写 DB + XML diff 预览 + 确认写盘 |
| ▶ Launch (Ctrl+Shift+S) | Export + 启动游戏 |
| 自动保存 | 改为 Quick Save（不弹 diff） |
| Export 取消不写 DB | `ExportEntitiesToXmlAsync` 从内存实体生成 diff → 用户确认后才 `SaveToDatabaseAsync` + 写 XML |

### Game 数据只读保护
| 功能 | 说明 |
|------|------|
| BeginningEdit 拦截 | `SearchableDataGrid.CanEditEntity` 钩子 → 合并视图中 Game 实体（ModId=-1）双击无反应 + 弹出引导通知 |
| 通知内容 | "游戏基础数据不能直接修改。要修改游戏数据，请在 Profile 中添加 Merge 模式 Mod（strModName=0）" |

### 合并视图数据加载
| 功能 | 说明 |
|------|------|
| Profile 打开时预加载 | `DocumentWorkspaceViewModel.Receive` 改为 `async void`，同步解析 getmods.php → 导入/加载 mod → 填充 ModLoadInfos |
| ReloadMergeTabsAsync 兜底 | 如果 modEntries 为空 → 直接从 game.db 查询所有 ModId>0 → 构建 synthetic entries |
| Merge view 不使用 TabSnapshotCache | 仅单 Mod 视图使用内存缓存；合并视图始终从 DB 重载 |

### Tab 切换数据保持
| 功能 | 说明 |
|------|------|
| 单 SharedDataGrid | 移除 `TabControl.ContentTemplate` 中的 SearchableDataGrid，改用单个 `SharedDataGrid` |
| 切换不改数据 | `OnTabChanged` 只改 `SharedDataGrid.ItemsSource`，DataGrid 不重建 |
| FilterText 提取 | TextBox 移到 TabControl 上方，单例存在，切换 Tab 不清空 |

### 工具栏 UI 重设计
- 四组布局：导航(U/G/Redo | Back/Locate) | 操作(+/-/ColumnManager) | 合并(Deps/Conflicts/Filter/ShowAll) | 保存(Quick Save/Export/Launch)
- 统一 `Padding="8,4"`，ConflictBtn 加 `MinWidth="72"`
- ConflictBtn Content 改为 code-behind 直接设置（移除 AXAML 绑定防空白）

### Bug 修复
- **MergeXmlExportDialog 按钮空白**: `DataContext = this` 缺失 → 添加
- **ConflictBtn 空白**: 移除 AXAML Content 绑定，code-behind 直接设置
- **ConflictBtn 点击区域**: `Padding="6,2"` → `"8,4"`
- **DiffView 跳转只对左边生效**: 改为焦点检测 `NewEditorControl.IsFocused`
- **保存后脏状态未清除**: 各保存路径增加 `RefreshActiveDataGrid()` 调用
- **ModLoadInfo 无 Path 属性**: 改为 `modLoad.Info.Path`

### 新增本地化键
`QuickSave`, `QuickSaveTooltip`, `SaveAndExport`, `SaveAndExportTooltip`, `Saving`, `SaveTooltip`, `SaveAndLaunchTooltip`, `Launch`, `DiffJumpToCursor`, `DiffJumpToIndex`, `GameDataReadOnly`, `GameDataReadOnlyMessage`

### 新增文件
- `Docs/10-next-priority-plan.md` — 下一阶段优先级规划 + DB 架构定位

### 修改文件
`ModGameDataTabsView.axaml` + `.cs`, `XmlDiffView.axaml` + `.cs`, `SearchableDataGrid.axaml` + `.cs`, `HomePageViewModel.cs`, `DocumentWorkspaceViewModel.cs`, `MergeXmlExportDialog.axaml.cs`, `Resources.resx` (×3)

### 已知待解决问题
| 问题 | 状态 |
|------|:--:|
| Tab 切换后数据仍被重置 | 🔴 已改为 SharedDataGrid 架构但问题依旧存在，需进一步排查 |
| 非初始 Profile 合并视图无法保存 | 🟡 已添加 AutoLoad + DB 兜底，需验证 |
| 排序箭头不显示 | 🔴 Avalonia 11.3 框架限制 |
| 像素画手绘工具 | 🔴 未实现 |
| 批量编辑 | 🔴 未实现 |
| 新实体创建向导 | 🔴 未实现 |

---

## Stage 1 — 单Mod数据编辑 (v0.2.0-dev) | 2026-05-23

### 新增功能
| 功能 | 说明 |
|------|------|
| 单元格编辑 | 双击进入编辑模式，类型适配：bool→CheckBox、Enum→ComboBox、longtext→多行TextBox、int/float/string→TextBox |
| 行增删 | 工具栏 `+`（ID 自动递增）、`-`（删除选中行） |
| 保存闭环 | 编辑 → Diff 预览（左=磁盘原始 / 右=待提交）→ 确认 → 写入 neogame.xml + 更新 game.db |
| IsDirty 追踪 | `ModGameDataDocument.IsDirty` |
| 教程导入 | Help 菜单「导入教程…」：导入 .md/.png/.jpg 到 Help 目录 |

### 修改
- `GenericDataGridHelper`：扩展 CellEditingTemplate
- `GameDataTypeTabItem.ItemsSource`：`IEnumerable` → `ObservableCollection<object>`
- `ModGameDataTabsView`：Add/Delete + 保存逻辑 + TabControl `x:Name="DataTabs"`

### Bug 修复
- **Enum 导出**：`ConditionColor.Green` → `2`（Enum 转底层 int）
- **浮点数导出**：避免科学计数法 → 十进制格式
- **新增行**：继承已有数据的 `FilePath`，避免独立成一个文件
- **ID 排序**：int 键值 `D10` 左补零 → `1,2,10` 而非 `1,10,2`
- **空字符串跳过**：导出跳过 `""` 列（游戏约定"未设置"）
- **保存顺序**：先 DB 后磁盘，DB 失败不写盘
- **Delete 按钮**：`CanDeleteRow` 默认值 → `true`
- **ClearMods**：修复 dangling else → NullReferenceException
- **DeleteMods**：IsBase 保护 + try-catch
- **ModManager**：IsBase 检查拒绝 + data/ 路径保护
- **GameDbContext**：`GetMethod("Set", Type.EmptyTypes)` 消歧 AmbiguousMatchException

### 新增本地化键
`DiffOldLabel`, `DiffNewLabel`, `AddRow`, `DeleteRow`, `ImportTutorial`

---

## Stage 2 — 引用系统 (v0.3.0-dev) | 2026-05-24

### 新增功能
| 功能 | 说明 |
|------|------|
| `[ReferenceField]` attribute | 标记引用字段 + 目标实体类型 |
| `ReferenceHelper` | `ParseReference()` / `FormatForDisplay()` 去掉 `0:` 前缀 |
| 引用列样式 | Teal 色下划线（区别于选中行浅蓝高亮） |
| 右键跳转 | 「跳转到 {目标表}」→ 自动切换 Tab + 定位匹配行 |
| ← 返回 | 导航历史栈，跳转后可返回 |
| ComboBox 编辑 | 引用列双击 → 下拉 `"id: 名称"`，选中自动提取 ID |
| ReferenceLookups | 跨表查询字典（当前 Mod 实体填充） |

### 标注字段 (16个)
| 实体 | 字段 → 目标 |
|------|-----------|
| Creature | `TreasureId`→TreasureTable, `Faction`→Faction, `CorpseId`→TreasureTable |
| Recipe | `TreasureId`→TreasureTable |
| ItemType | `nCondID`→Condition, `nTreasureID`→TreasureTable, `nFormatID`→ContainerType, `nComponentID`→ItemType |
| HexType | `nTreasureID`→TreasureTable, `nDefaultCampID`→CampType |
| EncounterTrigger | `nEncounterID`→Encounter |
| CreatureSource | `nCreatureID`→Creature |
| DmcPlace | `nEncounterID`→Encounter |
| Encounters | `nTreasureID`→TreasureTable |
| CampType | `nTreasureID`→TreasureTable |

### Bug 修复
- **Stage 1 隐藏 bug**：`OnAutoGeneratingColumn` 回退到运行时类型 → CheckBox/ComboBox/多行编辑 首次生效
- 引用列对齐：`HorizontalAlignment=Stretch` + `VerticalAlignment=Center` + `Background=Transparent`
- 跳转失败提示：表未加载 / ID 未找到 分别通知
- 自定义列对齐：统一 `VerticalAlignment=Center` + `Margin(4,0)`
- **多实例导航**：静态 `OnNavigateRequest` 改用 `RegisterNavigateTarget` + `WeakReference` 查找

### 新增文件
`Helper/ReferenceFieldAttribute.cs`, `Helper/ReferenceHelper.cs`, `Helper/OverlayChainEntry.cs`, `Helper/Converter/OverlayChainConverter.cs`

### 新增本地化键
`NavigateBack`, `GoToReference`, `NoRowSelectedMessage`, `RefTargetNotLoaded`, `RefTargetNotFound`

### 已知限制
- 多值引用字段（`aAttackModes`, `vProperties`）→ 后续处理
- `<?xml...?>` 声明 diff → 后续处理

---

## Stage 3 — 合并视图 (v0.4.0-dev) | 2026-05-24

### 入口与数据流
- Profile 列表右键 → 「打开合并视图」`OpenMergeEditorMessage` → `MergeEditorDocument` → `ModGameDataTabsView`（通过 `ProfileInfo` 属性驱动）
- 入口判断：`OnSavePreviewButtonClick` 检查 `ProfileInfo is not null` → 走合并视图 Save 流，否则走单 Mod Save 流
- 数据加载：`ReloadMergeTabsAsync` 从 game.db 加载所有 Mod + Game 实体 → 按 Phase 1/2 合并 → 构建覆盖链 → 创建 DataGrid 视图

### 合并规则引擎（`ReloadMergeTabsAsync`）
1. **Phase 1**：Game 基础数据（ModId=-1） 打底入 `mergedDict[key]`
2. **Phase 2**：按 getmods.php 加载顺序逐层处理：
   - Merge Mod（strModName=0）：同 key 覆盖 `mergedDict[key]`
   - Insert Mod（strModName≠0）：追加到 `insertedList`，不同命名空间永不覆盖
3. **败者检测**：所有不在 `mergedDict.Values ∪ insertedList` 中的实体标记为 overridden → 存入 `_overriddenEntityIds` + `GenericDataGridHelper.OverriddenEntityIds`

### 合并自增 ID（→Id 列）
- **算法**：`mergeSpaceIds` = Game + Merge mod 实体的 EntityId 集合
  - 整数 key 类型：merge 空间实体 `mergedId = 自身 key`；insert 空间实体 `mergedId = max(mergeKeys) + 1` 起顺序自增
  - 非整数 key（如 GameVar）：全部顺序自增 1,2,3...
- **存储**：`GenericDataGridHelper.EntityMergedIds` — `Dictionary<EntityId, int>`
- **获取**：`GetEntityMergedId(entity)` — 返回 int，没找到返回 0
- **DataGrid 列**：`→Id` 列动态插入，仅在 `EntityMergedIds.Count > 0`（合并视图）时可见
- **默认排序**：合并视图按 mergedId 升序排列

### 双模式切换
- `Show All` ToggleButton（仅合并视图可见，通过 `IsMergeView` 绑定 `IsVisible`）
- **Mode 1（默认）**：仅胜者，CV Filter 排除 `_overriddenEntityIds`
- **Mode 2（Show All）**：全部数据，败者浅灰底 `rgb(200,200,200)`
- 切换通过 `RebuildFilteredItemsSources` → `DataGridCollectionView.Filter` 赋值 + `Refresh()` 实现

### 架构决策（重要）

#### DataGridCollectionView 的使用
- 合并视图：`ItemsSource = new DataGridCollectionView(visibleItems)` — CV 包裹预过滤集合，CV 提供排序能力
- 单 Mod 视图：`ItemsSource = items`（plain `ObservableCollection`）— DataGrid 自己创建内部 CV
- **为什么不用 CV.Filter**：CV 的 Filter 属性在多轮调试中表现不可靠，改用预过滤集合方案（先过滤再包裹）
- **排序原理**：`Sorting` 事件手动提取/排序/替换 ItemsSource（见下方"排序机制"）

#### GameDataTypeTabItem 设计
- 继承 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`（确保 Avalonia 编译绑定正确识别 `PropertyChanged`）
- `SourceCollection`（`ObservableCollection<object>`）：完整未过滤数据，增删操作修改此集合
- `ItemsSource`（`IEnumerable`）：绑到 DataGrid，setter 触发 `SetProperty` → `PropertyChanged` → DataGrid 重绑
- `IsMergeView`（DirectProperty）：控制 ShowAll 按钮、覆盖链面板的可见性

#### ModInfo 时间戳
- `LastImport`：`LoadModAsync` 调用时更新（XML → DB 同步时间）
- `LastModified`：`SaveToDatabaseAsync` 调用时更新（编辑器 → DB 保存时间）
- `IsDirty`（计算属性）：`LastModified > LastImport` → DB 有未导出的改动
- `DatabaseGeneratedOption.Computed` 已移除，改为手动设置

### 覆盖链 (Overlay Chain)
- **链节点**：`OverlayChainEntry(ModName, Id, EntityType, EntityId)` — EntityId 支持精确导航
- **构建规则**（Phase 2 中）：
  - 仅 Merge mod（strModName=0）加入 `keyOverlayHistory`
  - Insert mod 实体不参与覆盖链（不同命名空间），始终显示为独立链
  - Game 条目仅在 base 实体存在时添加（不再创建虚假 `[?]` 条目）
- **过时实体覆盖链**：在败者检测后补充填充 `EntityModNames` + `OverlayChainDisplay`
- **导航**：链节点点击 → `NavigateToByEntityId(type, entityId)` 精确跳转

### 导航系统
- **EntityId 导航**（合并视图）：`NavigateToByEntityId` → 按 EntityId 在 DataGrid 中匹配
- **业务 key 导航**（单 Mod 视图 / 引用列）：`NavigateTo` → 按 key 属性匹配
- **返回栈**：`_navHistory` 栈追踪跳转路径，`←` 按钮可用时返回上一位置
- **跳转到过时实体**（非 ShowAll）：阻止跳转 + 通知 "Enable Show All to navigate to it"
- **跳转到自身**：通知 "Already at this entity"
- **ShowAll 自动打开**：`GenericDataGridHelper.OnShowAllRequest` 静态回调，当前未被覆盖链触发（仅声明，覆盖链导航改为阻止 + 通知）

### 排序机制
- **问题**：Avalonia DataGrid 替换自定义列后不设置 `SortMemberPath`，且 CV 的 `SortDescriptions` 机制不稳定
- **解决**：
  - `ConfigureColumn` 中所有自定义列显式设置 `SortMemberPath = e.PropertyName`（含 `??=` 回退）
  - `OnSorting` 事件中手动排序：提取所有 item → `List.Sort` 按反射读取属性值排序 → `DispatcherPriority.Background` 延迟替换 `MainGrid.ItemsSource`
  - 延迟替换的原因：DataGrid 内部 `ProcessSort` 在 `Sorting` 事件后异步执行，直接替换 ItemsSource 会导致 `NullReferenceException`
- **局限性**：排序箭头（列头 ↑↓）不显示——因为绕过了 DataGrid 内部 CV 的 `SortDescriptions` 机制
- **方向切换**：同列点击翻转升序/降序，换列默认升序

### 保存机制
- **合并视图 Save**（`ShowMergeSavePreviewAsync`）：
  - 收集所有 `ModId > 0` 的实体（不过滤败者——败者也回存到源 Mod）
  - 按 entity type 分组 bulk upsert 到 game.db
  - 更新受影响的 `ModInfo.LastModified`
  - **不写 XML**——XML 导出是独立步骤（后续实现）
- **单 Mod Save**：保持原有 Diff + 写 XML 流程

### 工具栏布局
```
[←] [+] [-]              [status]                    [Show All] [Save]
```
- `IsMergeView` 绑定 `Show All` 按钮可见性
- `ShowRowDetails`（覆盖链面板）仅在合并视图时 `VisibleWhenSelected`，单 Mod 为 `Collapsed`

### XML 编码兼容
- 游戏 XML 文件使用 `encoding="utf8"`（缺少连字符），.NET 不识别
- `ModManager.LoadXmlFile()` 和 `ModGameDataTabsView.LoadXmlSafe()` 在 `XDocument.Parse` 前替换 `"utf8"` → `"utf-8"`

### Bug 修复清单（本阶段）
| # | 问题 | 根因 | 修复 |
|---|------|------|------|
| 1 | 败者一直可见 | `GameDataTypeTabItem` 无 `PropertyChanged`，ItemsSource 替换静默丢失 | 继承 `ObservableObject`，`SetProperty` 触发通知 |
| 2 | 覆盖链跳转到错误实体 | 按业务 key 导航，多 Mod 共享同 key | 改为按 EntityId 精确匹配 |
| 3 | 败者漏判 | `HashSet<IEntity>` 引用相等 | 改用 `HashSet<string>` + EntityId |
| 4 | 覆盖链包含 Insert mod | Insert mod 实体不应参与覆盖链 | Phase 2 仅 Merge mod 加入 `keyOverlayHistory` |
| 5 | 覆盖链过时实体无数据 | 仅 winners 填充 `EntityModNames` | 败者检测后补充填充 |
| 6 | 虚假 `[?]` Game 条目 | base 不存在时仍创建空 EntityId 的 Game 条目 | 仅在 base 存在时添加 |
| 7 | ViewLocator 崩溃 | `GameDataTypeTabItem` 被 `INotifyPropertyChanged` 宽泛匹配 | Match 缩窄为仅 `ViewModelBase` + `IDockable` |
| 8 | 排序不工作 | `SortMemberPath` 未被 DataGrid 设置（事件后置）+ CV SortDescriptions 不稳定 | 手动替换列时设 `SortMemberPath`，`Sorting` 事件手动排序 + 延迟替换 |
| 9 | 排序崩溃 | 直接替换 ItemsSource 导致 `ProcessSort` NRE | 延迟到 `DispatcherPriority.Background` 替换 |
| 10 | 导航到过时实体无提示 | 检查在 `targetItem` 之后，CV Filter 已隐藏 | 在搜索前通过 EntityId 检查 `OverriddenEntityIds` |

### 新增文件
- `Helper/Converter/EntityMergedIdConverter.cs` — EntityId → 合并 ID 值转换器
- `Helper/Converter/OverlayChainConverter.cs`（Stage 2 创建，Stage 3 修改导航逻辑）

### 修改文件摘要
| 文件 | 关键改动 |
|------|---------|
| `ModGameDataTabsView.axaml.cs` | 合并加载、过滤、CV 管理、排序、导航、保存 |
| `ModGameDataTabsView.axaml` | 工具栏三列 Grid 布局、`IsMergeView` 绑定 |
| `SearchableDataGrid.axaml.cs` | `→Id` 列管理、排序逻辑、`ShowRowDetails` 属性 |
| `SearchableDataGrid.axaml` | `Sorting` 事件 |
| `GameDataTypeTabItem`（同文件内类） | `ObservableObject` 基类、`IEnumerable ItemsSource`、`SourceCollection` |
| `GenericDataGridHelper.cs` | `EntityMergedIds`、`GetEntityMergedId`、`NavigateToByEntityId`、`SortMemberPath` |
| `OverlayChainEntry.cs` | `EntityId` 属性 |
| `OverlayChainConverter.cs` | EntityId 优先导航 |
| `ModInfo.cs` | `LastModified`/`LastImport` 取消 Computed、`IsDirty` |
| `ModManager.cs` | `LoadXmlFile` 编码修复、`LastImport` 更新 |
| `ViewLocator.cs` | Match 缩窄 |
| `App.axaml.cs` | 移除 `BindingPlugins`（Avalonia 11.3 API 变更） |
| `Resources.resx` | 新本地化键 |

### 新增本地化键
`OpenMergeEditor`, `MergeEditorTitleFormat`, `NavigateSameEntity`, `NavigateToOverriddenRequiresShowAll`

### 新增字典（GenericDataGridHelper 静态属性）
- `EntityMergedIds` — `Dictionary<string, int>`，EntityId → 合并自增 ID
- `EntityModNames` — `Dictionary<string, string>`，EntityId → 来源 Mod 名称
- `OverriddenEntityIds` — `HashSet<string>`，败者 EntityId 集合
- `OverlayChainDisplay` — `Dictionary<string, List<OverlayChainEntry>>`，EntityId → 覆盖链

### 当前已知限制
1. **排序箭头不显示**：Avalonia 11.3 `DataGridSortDescription` 抽象类
2. **列头排序只支持简单属性**：不支持嵌套路径
3. **Avalonia 版本锁定 11.3.x**

---

## Stage 4 — 保存/导出重构 + 体验增强 (v0.5.0-dev) | 2026-05-24~27

### 保存流程重构
- **Save 按钮统一只写 DB**：单 Mod / 合并视图一致，不再自动 Diff + 写 XML
- **Export XML 独立按钮**：从 DB 加载 → 对比磁盘 → `MergeXmlExportDialog` → 确认写回
- **通用导出方法** `ExportXmlAsync(List<ModInfo>)`

### `+` 新增行弹窗（`AddRowDialog`）
- Target Mod（仅 Insert mod）、Target XML（按 Mod 过滤+绝对路径）、Copy From（Subject 显示）
- 新增行浅绿背景、ID=目标 Mod 内最大+1、自动重算 mergeId+排序+跳转

### 编辑体验
| 功能 | 说明 |
|------|------|
| Loading 遮罩 | `OnAttachedToVisualTree` + `IsLoading` → 半透明遮罩 |
| 脏关闭拦截 | `SetDirty` → `MergeViewDirtyChanged` → 单 Mod + 合并视图均有确认弹窗 |
| Tab 脏标记 | `Header` 加 `*` 后缀 |
| Cell 编辑高亮 | `CellEditEnding` → 浅黄背景 `rgb(255,255,220)`，即时生效 |
| 列宽固定 | int=80, float=90, bool=70, enum=120, string=160, longtext=280, ref=160 |
| ◎ 定位按钮 | `ScrollIntoView` 聚焦选中行 |
| 标签页切换保护 | 合并视图 Tab 切换缓存恢复；单 Mod 有未保存修改时缓存恢复，无修改重新加载 |
| ☰ 列管理器 | 工具栏按钮弹出当前 Tab 列清单，勾选/取消即时显隐 |

### Subject 属性
- `IEntity.Subject`：反射查找 `strName`/`Name` 等；覆盖链、Copy From、引用 ToolTip 均使用

### MergeId 计算
- Merge 空间（Game + `strModName=0`）= business key；Insert 空间 = `max(mergeKeys)+1` 顺延
- `IEntity.MergedId` + `SortMemberPath` → →Id 列可排序

### 搜索与过滤
| 功能 | 说明 |
|------|------|
| 搜索框 `col:value` 语法 | `strName:Water` 按列过滤；`Water Bottle` 全文字段搜索；双引号分组 |
| 列名辅助输入 `?` | 按钮弹出当前 Tab 可用列名列表，点击自动插入 `col:` |
| Mod 过滤 ComboBox | 合并视图工具栏：All Mods / Game / 各 Mod 名称，按 ModId 筛选行 |
| 防抖 | 200ms debounce，加载中不触发 |

### 字段级来源标记 + 冲突检测
| 功能 | 说明 |
|------|------|
| `FieldSources` 字典 | `(EntityId, ColName) → ModName`，合并加载时逐字段比较记录来源 |
| `FieldConflicts` | 两个不同 Merge Mod 修改同一字段时标记 |
| Cell ToolTip | 悬停单元格 → `Source: [ModName]` 或 `⚠ CONFLICT` |
| 冲突高亮 | 冲突字段浅红背景 `rgb(255,220,220)` |

### 引用导航重构
| 功能 | 说明 |
|------|------|
| `ReferenceFieldAttribute` 扩展 | `IsMultiValue` + `MultiValueFormat`（CommaList / IdMultiplier / IdAssignment） |
| 多值引用渲染 | 按分隔符拆分为独立元素，各自 Ctrl+Hover/Ctrl+Click |
| Ctrl+Hover | 悬停引用值 → ToolTip 显示 `EntityType: Subject (id=X)` |
| Ctrl+Click | 按住 Ctrl 点击单值或多值引用 → 直接跳转到目标实体 |
| 右键菜单保留 | 单值引用直接跳转；多值列出全部解析引用（>25 折叠），每项显示 Subject |
| 复杂字段标注 | 新增 20+ 字段标注（Encounters/Creature/HexType/Recipe/ItemType/AttackMode/Faction/Ingredient/TreasureTable） |

### 合并视图行为
| 规则 | 实现 |
|------|------|
| 合并视图打开 → 所有单 Mod 视图只读 | `DocumentWorkspaceViewModel` 自动设置 |
| 关闭合并视图 → 恢复可编辑 | 检测合并视图关闭后恢复 |
| 只能打开一个合并视图 | 打开新合并视图自动关闭旧的 |

### Profile 拖拽排序
- `EditProfileView` DataGrid 已集成 `ContextDragBehavior` + `ContextDropBehavior` + `ModEntryDropHandler`
- 补充缺失的 `xmlns:dd` 声明，拖拽排序现已可用

### Bug 修复
- 新增行覆盖链 `?` → 正确显示 Mod 名称
- Tab 切换后空白 → `DebounceFilter` 加载中不触发 + 移除手动 `DataGridCollectionView` 包装 + `OnAttachedToVisualTree` 强制刷新
- 单 Mod 修改在 Dock 切换后丢失 → 有未保存修改时存缓存、恢复时从缓存加载
- `EditProfileView` 拖拽行为命名空间缺失 → 补充 `using:` 声明
- 引用 ToolTip 去掉 `Ctrl+Click → ` 前缀

### 新增文件
`Views/Dialog/AddRowDialog.axaml` + `.cs`、`Views/Dialog/MergeXmlExportDialog.axaml` + `.cs`、`Helper/Converter/FieldSourceConverter.cs`、`Helper/Converter/FieldConflictBackgroundConverter.cs`

### 修改文件
`IEntity.cs`、`OverlayChainEntry.cs`、`ReferenceFieldAttribute.cs`、`ReferenceHelper.cs`、`XmlParser.cs`、`OverlayChainConverter.cs`、`GenericDataGridHelper.cs`、`ModGameDataTabsView.axaml` + `.cs`、`SearchableDataGrid.axaml` + `.cs`、`DocumentWorkspaceViewModel.cs`、`EditProfileView.axaml` + `.cs`、9 个 Model 文件（参考字段标注）、3 个 Resources.resx

### 新增本地化键
`ShowAll`, `ExportXml`, `ExportXmlTooltip`, `Loading`, `LocateRowTooltip`, `SearchHelpTooltip`, `SearchHelpButtonTooltip`, `SearchHelpAvailableColumns`, `ModFilterAll`, `ColumnManagerTooltip`, `ColumnManagerHeader`

### 当前已知限制
1. **排序箭头不显示**：Avalonia 11.3 `DataGridSortDescription` 抽象类
2. **Avalonia 版本锁定 11.3.x**
3. **TreasureTable `aTreasures`**：`|` OR 分隔符未兼容（comma 分隔的 AND 部分正常工作）
4. **Recipe `strTools`/`strConsumed`**：`1x2+1x3` 格式（`+` 分隔符）未标注

---

## Stage 5 — 体验夯实 + 引用系统重构 (v0.6.0-dev) | 2026-05-27~28

> 方向修正依据：[04-stage5-analysis.md](04-stage5-analysis.md)
> 核心思路：先让已有功能好用，再扩展新功能。

---

### 已完成功能

#### Undo/Redo 命令系统
- `Data/Command/IEditorCommand` + `EditCellCommand` / `AddEntityCommand` / `DeleteEntityCommand` + `CommandHistory`（上限 100 步）
- 工具栏 `↩` `↪` 按钮；`Ctrl+Z` / `Ctrl+Y` 全局快捷键
- AddRow / DeleteRow / 单元格编辑均纳入 Undo 栈
- 重载数据时自动清空历史

#### 字段帮助系统
- 列头 Tooltip 优先查 `*Desc` 本地化键（`RangeDesc` → "攻击距离，1 for melee..."），fallback 到 `[Display]` 短名
- 修改 `GenericDataGridHelper.ConfigureColumn` 的 tooltip 查找逻辑

#### 引用系统重构
- **`ReferenceFieldAttribute` 重新设计**：新增 `Separator`（null=单值, `,`/`&`/`|`=多值）、`Pattern`（`{id}`/`{id}x{mult}`/`{id}={value}`）、`TargetKey`（`{Id}` 默认 / `{GroupId}.{SubgroupId}` ItemType 复合键）
- 移除 `MultiValueFormat` 枚举；`IsMultiValue` 改为 `Separator is not null` 计算属性
- 45 处 `[ReferenceField]` 标注批量更新，4 处 ItemType 引用加 `TargetKey = "{GroupId}.{SubgroupId}"`
- Ingredient `RequiredProps`/`ForbidProps` 分隔符从 `,` 改为 `&`（游戏数据 `16&amp;46` → `16&46`）

#### 引用显示增强
- 单值 `{Subject} (id=N)` 替代裸ID；负数 `~Subject`（条件取反，如 Condition 取反）；`LookupSubjectByRawId` 使用 TargetKey 复合键匹配
- 多值每段解析 Subject；`FormatSegmentDisplay` 处理 `{id}`/`{id}x{mult}`/`{id}={value}` 三种 pattern
- 两级分隔符：CellTemplate 检测 `|`/`,` + 显示 `or`/`+` 连接；右键菜单展开所有子项
- `ExtractRawId` 按 Pattern 提取 ID：`{id}={value}` → `=` 前、`{id}x{mult}` → 第一个 `x` 前（非最后一个，TreasureTable 多段 x 格式）
- `DecomposeId` 先剥离命名空间前缀 `NSE:`，复合键无分隔符 fallback 用 `"Id"` 键
- `FindBestMatch()` 选 ModId 最大（覆盖链胜者），防止引用指向被覆盖实体；Subject 搜索增加 `PropertyName`/`strPropertyName`

#### ModName 列 & 元数据列
- 多 Mod 视图新增只读 `Mod` 列（`ModNameColumnConverter` 从 `EntityModNames` 查）；单 Mod 自动隐藏
- `ModId` / `FilePath` / `EntityId` 默认 `IsVisible=false`（列管理器可恢复）

#### AddRow 拆分
- `+` 按钮 → `AddRowDialog.ShowSimpleAsync` 仅选 Mod+XML（无 Copy From）
- 右键行 → "Clone Row" → 直接拷贝全字段+ID自增（`skipDialog=true`）

#### 列可见性持久化
- `AppConfig.ColumnVisibility`：`Dictionary<string, HashSet<string>>`（表名 → 可见列集合）
- 列生成时 `ApplyColumnVisibilityConfig()` 按表名匹配；列管理器勾选即时写入 `config.json`

#### 反向引用查询
- 右键行 → "Find references to this..." → 扫描所有加载 Tab 的 `[ReferenceField]` → 弹窗显示引用者列表（含表名、字段名）

#### FindReplace 悬浮面板
- `Ctrl+F` 打开搜索 / `Ctrl+H` 打开替换；同模式再次按关闭
- 右上角悬浮面板：搜索框、匹配计数、`^` `v` 导航、`Aa`（大小写）/`ab`（全词）/`.*`（正则）Toggle
- Enter 跳下一个匹配、Escape 关闭（全局按键，不依赖焦点）；`ScrollIntoView` + 选中行
- Replace 替换当前 / Replace All 全部替换（反射写入实体属性）
- 左侧 4px 拖拽柄调整面板宽度（最小 200px）
- 按钮使用 FluentIcons `SymbolIcon`（`ArrowUp`/`ArrowDown`/`Dismiss`）
- 面板默认关闭（`IsVisible="False"`），高度紧凑

#### CSV 导出（方法保留，按钮已移除）
- `OnExportCsvClick` / `OnImportCsvClick` 方法实现完整（CSV 解析含引号转义、列名匹配 `[Column]` 属性、`ConvertValue` 类型转换、ID 自增）
- 工具栏按钮已移除 — 后续迁移到 ModDatabase 面板（Stage 6）

#### 历史搜索栏移除
- 原搜索 TextBox + `?` 列名助手 + `col:value` 语法废弃
- `FilterText` / `DebounceFilter` / `OnSearchHelpClick` 代码保留但 UI 移除

---

### Bug 修复清单

| 问题 | 根因 | 修复 |
|------|------|------|
| Ctrl+Z/Y UI 不刷新 | IEntity 未实现 INotifyPropertyChanged，反射设值后 DataGrid 不感知 | undo/redo 后 `RefreshActiveDataGrid()` ItemsSource 重绑 |
| 引用负数 ID 显示 `[class]` | `LookupSubject(int)` 负数查 `_subjectCache` 永远 miss，fallback 到 `$"[{TypeName}] {keyVal}"` | `LookupSubject` 用 `Math.Abs(id)`；`~Subject` 前缀 |
| ShowAll 关不掉 | `Click` handler 读到 ToggleButton 旧 `IsChecked` 值，绑定与 handler 冲突 | 移除 `IsChecked` 绑定；Click handler `ShowAllEntities = !ShowAllEntities` + 手动 `ShowAllToggle.IsChecked = ShowAllEntities` |
| 跳转到被覆盖实体 | `LookupSubject` 返回第一个匹配，可能是败者 | `FindBestMatch` 选最高 ModId；导航加 `ScrollIntoView` + `Background` 延迟居中 |
| ExtractRawId 多段 `x` 截错 | `LastIndexOf('x')` 对 `55.4x0.75x1` 返回 `55.4x0.75` | `IndexOf('x')` 取第一个 `x` → `55.4` |
| DecomposeId 不处理命名空间前缀 | `"NSE:86.6"` → `int.TryParse("NSE:86")` 失败 → GroupId=0 | 先剥离 `NSE:` 前缀再解析 → `86.6` |
| 无 `.` 的复合键 ID 找不到 | `"418"` fallback 用 GroupId=418，但 ItemType 主键是 `id` 字段 | fallback 改用 `"Id"` 键 → `Id=418` |
| FindPanel 按钮空白 | 固定 `Width="24" Height="24"` + Avalonia 默认 Padding(12,4) → 内容空间为 0 | 去除宽高，用 `Padding="2,0"` + FontSize 11 |
| FindPanel 关不掉 | 自定义 `new IsVisibleProperty` 覆盖 `Control.IsVisible` → `IsVisible=false` 不影响视觉 | 移除自定义属性，直接用 `base.IsVisible` |
| FindPanel 拖拽比例不对 | `GetPosition(this)` 返回 UserControl 相对坐标，面板宽度变化时坐标系偏移 | `GetPosition(null)` 屏幕绝对坐标 |
| 右键菜单文本为空 | ContextMenu 是 Popup，`ElementName=Root` 绑定在弹出层中解析失败 | constructor 中 `CloneMenuItem.Header = Loc["CloneRow"]` 直接赋值 |
| Faction Tab 头中文不一致 | `[Display(Name="Faction")]` 与实体类型名 `Faction` 共用资源键 | Display 改为 `"FactionId"`；实体类型无中文 |
| Ingredient `&` 分隔符 | XML `16&amp;46` → 实体值 `16&46`，`Separator=","` 无法分割 | `Separator=","` → `Separator="&"` |
| FindPanel 焦点丢失后 Ctrl+F 无效 | `KeyDown` 只在 UserControl 有焦点时触发 | `TopLevel.AddHandler(KeyDownEvent, OnGlobalKeyDown, handledEventsToo: true)` 全局注册 |

---

### 验证框架（已废弃）
- 创建 `Data/Validation/ReferenceIntegrityRule` / `RequiredFieldRule` / `ValueRangeRule` + `ValidationReportDialog`
- 接入保存流程后单 Mod 5k+、合并视图 30k+ Warning（跨 Mod/Game 基础数据引用无法可靠验证，游戏数据不在编辑器上下文）
- 已从保存流程移除，文件保留

---

### 新增文件（13个）
`Data/Command/IEditorCommand.cs`, `CommandHistory.cs`, `EditCellCommand.cs`, `AddEntityCommand.cs`, `DeleteEntityCommand.cs`
`Data/Validation/IValidationRule.cs`, `ValidationResult.cs`, `ReferenceIntegrityRule.cs`, `RequiredFieldRule.cs`, `ValueRangeRule.cs`, `ValidationService.cs`
`Helper/Converter/ModNameColumnConverter.cs`
`Views/UserControls/FindReplacePanel.axaml` + `.cs`
`Views/Dialog/ValidationReportDialog.axaml` + `.cs`
`Views/Dialog/BatchEditDialog.axaml` + `.cs`（已删除，批量编辑废弃）
`Docs/04-stage5-analysis.md`

### 修改文件（21个）
`SearchableDataGrid.axaml` + `.cs`, `ModGameDataTabsView.axaml` + `.cs`, `GenericDataGridHelper.cs`, `ReferenceFieldAttribute.cs`, `ReferenceHelper.cs`, `AddRowDialog.axaml` + `.cs`, `AppConfig.cs`, `IEntity.cs`, `Creature.cs`, `Ingredient.cs`, `TreasureTable.cs`, `Encounters.cs`, `ItemType.cs`, 其余 7 个 Game Model 文件, `Resources.resx` / `.zh.resx` / `.en-us.resx`

### 新增/变更本地化键
`UndoTooltip`, `RedoTooltip`, `FactionId`, `FactionIdDesc`, `CloneRow`, `FindReferences`, `ExportCsv`, `ImportCsv`, `ExportCsvTooltip`, `ImportCsvTooltip`

---

### 当前已知限制
1. **排序箭头不显示**：Avalonia 11.3 `DataGridSortDescription` 抽象类
2. **Avalonia 版本锁定 11.3.x**
3. **Recipe `strTools`/`strConsumed`**：`1x2+1x3` 格式（`+` 分隔）未标注 `[ReferenceField]`
4. **TreasureTable `aTreasures`**：同字段混用 ItemType（GroupId.SubgroupId）和简单 id（TreasureTable）引用，单一 `[ReferenceField]` 无法覆盖两种目标类型
5. **FindPanel 不跟随 Semi.Avalonia 主题深色/浅色切换**

---

## Stage 6 — 可视化编辑器 + 数据导出 + 体验增强 (v0.7.0-dev) | 2026-05-29~30

### CSV/XML 导入导出迁移
| 功能 | 说明 |
|------|------|
| CSV 导出 | ModDatabase 右键菜单 → 导出当前 Mod 数据为 CSV |
| CSV 导入 | 文件选择 → 实体类型匹配 → CsvDiffDialog 预览变更 → 确认导入 |
| 旧代码清理 | `ModGameDataTabsView.axaml.cs` 中 CSV 方法移除，逻辑提取到 `CsvImportExportService` |

### 数据导出（Profile 面板）
| 导出 | 格式 | 说明 |
|------|------|------|
| 合成表 | CSV/XLSX | Recipe→Ingredient→TreasureTable，引用列解析为 Subject |
| 物品百科 | Markdown | ItemType 全字段，Condition 引用解析 `{id}x{mult}` 格式 |
| 战利品表 | JSON | TreasureTable 递归展开（最大深度5层，循环检测） |
| 全部导出 | XLSX | 24 种实体按类型分 Sheet，含 `→Id` 合并列，引用解析为 Subject，支持 Unicode 转义 |

### 数据导出增强
- 自动默认文件名（`crafting_table_20260530.csv` 等）
- `EnsureGameDataLoadedAsync` 导出前自动确保 Game 数据已加载
- `ToDedupedDict` 辅助方法处理多 Mod 重复 ID
- `XlsxWriter`：纯 C# 无外部依赖的 .xlsx 生成器（ZIP+XML）

### 可视化编辑器架构

#### ICustomTableEditor + CustomEditorRegistry
- 接口：`EntityType` / `EditorName` / `CreateEditor()` / `UpdateEntity(IEntity?)`
- 注册表：`CustomEditorRegistry` 按实体类型注册编辑器
- 面板：`ValueEditorPanel` 右侧分割面板，GridSplitter 可拖拽调整宽度，Star 弹性伸缩

#### EditorHelper 统一工具
- `BuildOverviewTab(IEntity)` 通用概览标签页（所有属性 + 引用 + 图片 + 反向引用）
- `BuildRefChildren` 引用列解析（去重赛选胜者→复合键匹配→Subject 显示→Ctrl+Click 跳转）
- 支持嵌套 `|` OR 分隔符（TreasureTable aTreasures）
- `FormatExtraInfo` 额外信息格式化（0~1 浮点自动显示为百分比）
- `FmtPct` 百分比格式化、`StripNs` 命名空间前缀剥离、`AddImagePreviews` 图片缩略图

#### ReferenceResolver 引用解析器
- `GetDedupedInt<T>` / `GetDedupedComposite<T>` 去重查找（最高 ModId）
- `ResolveSubject` / `ResolveMultiRef` 引用解析
- `NavigateTo` / `NavigateToByKey` 统一导航
- `FindReverseReferences` 反向引用索引

### 可视化编辑器实现清单

| 实体类型 | 标签页 | 功能 |
|---------|--------|------|
| **Recipe** | Overview, Recipe Tree | 工具/消耗/产物树状展开，Ingredient→ItemProp 属性展开，Ctrl+Click 跳转 |
| **Encounter** | Overview, Story Graph | 流程图(Canvas)+关系树(TreeView)，LeadsFrom/Self/LeadsTo，响应权重百分比 |
| **TreasureTable** | Overview, Treasure Tree | 嵌套战利品递归树，OR Group/AND Item，概率/数量范围，循环检测 |
| **ItemType** | Overview, Sprite Show, Wear Show | 属性概览+引用+图片；SpriteShow 多选下拉叠加 CreHuman.png；WearShow 多选下拉叠加 btn_inv_body.png |
| **Ingredient** | Overview | 必需/禁止属性展开 + 反向引用 |
| **ItemProp** | Overview | 属性信息 + 反向引用 |
| **AttackMode 等其余** | Overview | EntityOverviewEditor 通用概览 |

### 引用系统增强
- `ReferenceField` Pattern 新增 `{mult}x{id}` 支持（Recipe strTools/Consumed/Destroyed）
- Recipe 的 Tools/Consumed/Destroyed 标注 `[ReferenceField(typeof(Ingredient), Separator="+", Pattern="{mult}x{id}")]`
- 引用列语义化显示：`ReferenceHelper.ExtractRawId` + `ParseMultiplierReversed`
- 可视化编辑器所有引用统一 Ctrl+Click 跳转

### 图片系统
- `ImageViewerWindow` 浮动窗口（ScaleTransform+TranslateTransform 缩放/平移）
- 搜索路径：GameRoot/img + Mods/*/img/ 子目录
- `vSpriteList` 解析（`slot=imagePath` 格式）→ 身体部位映射
- `vImageList` + `vSpriteList` + `strImg` 等字段图片自动缩略图预览
- 图片命名空间前缀剥离（`NSE:img.png` → `img.png`）

### 身体部位槽位映射

| 槽位 | 部位 | 槽位 | 部位 |
|------|------|------|------|
| 20 | L-Hand | 14 | R-Shoulder |
| 21 | R-Hand | 17 | Face |
| 22 | Back | 13 | L-Back |
| 23 | Head | 4 | Legs |
| 11 | Torso | 2 | L-Foot |
| — | — | 3 | R-Foot |

### 容器与布局
- 可视化编辑器面板从悬浮窗 → 右侧分割面板（GridSplitter + Star 弹性列宽）
- 默认打开，无自定义编辑器时显示占位提示
- 统一标签页布局：Tab 1 = Overview，Tab 2+ = 特性化视图
- 文档打开时自动折叠左侧边栏

### Bug 修复 & 优化
- **重复 key 崩溃**：DataExportService + 可视化编辑器所有 ToDictionary → GroupBy 去重
- **RowDetail 不展开**：SearchableDataGrid 构造时显式设置初始 RowDetailsVisibilityMode
- **首次点击空白**：OnTabChanged 使用 Dispatcher.UIThread.Post 延迟更新
- **面板宽度固定**：移除 Width=320 硬编码 + 内部拖拽柄冲突
- **文本换行**：所有 TextBlock 添加 TextWrapping=Wrap
- **百分比显示**：0~1 浮点值自动格式化为百分比

### 新增文件（~20 个）
| 文件 | 说明 |
|------|------|
| `Services/CsvImportExportService.cs` | CSV 解析/对比/转换 |
| `Services/DataExportService.cs` | 合成表/百科/战利品/XLSX 全导出 |
| `Services/CustomEditorRegistry.cs` | 编辑器注册表 |
| `Helper/ICustomTableEditor.cs` | 编辑器接口 |
| `Helper/ReferenceResolver.cs` | 统一引用解析器 |
| `Helper/HexMapRenderer.cs` | 地图六边形网格 Bitmap 渲染 |
| `Views/UserControls/ValueEditorPanel.axaml` + `.cs` | 右侧分割面板 |
| `Views/UserControls/ZoomableImageView.axaml` + `.cs` | 可缩放拖动图片查看器 |
| `Views/UserControls/Editors/EditorHelper.cs` | 编辑器公共工具（概览/引用/图片） |
| `Views/UserControls/Editors/EntityOverviewEditor.cs` | 通用属性概览 |
| `Views/UserControls/Editors/RecipeFlowchartEditor.cs` | 配方树 |
| `Views/UserControls/Editors/StoryTreeEditor.cs` | 剧情编辑器 |
| `Views/UserControls/Editors/TreasureTreePreviewEditor.cs` | 战利品树 |
| `Views/UserControls/Editors/ItemTypeEditor.cs` | 物品类型编辑器 |
| `Views/UserControls/Editors/IngredientEditor.cs` | 合成项编辑器 |
| `Views/UserControls/Editors/ItemPropEditor.cs` | 属性编辑器 |
| `Views/Dialog/CsvImportDiffDialog.axaml` + `.cs` | CSV 导入对比 |
| `Views/Dialog/ImageViewerWindow.axaml` + `.cs` | 图片浮动弹窗 |
| `Data/Messages/ModMessages.cs` | 新增 OpenImageDocumentMessage |

### 修改文件（~10 个）
| 文件 | 改动 |
|------|------|
| `Pane.axaml` | ModDatabase 右键菜单（CSV导入导出移除ImportCsv）；Profile 工具栏（导出 DropDown + XLSX）；合并视图恢复 |
| `ModDatabaseViewModel.cs` | CSV/XLSX 导出命令，ImportXml 移除 |
| `ModIndexViewModel.cs` | ExportWithDialog 重构 + 默认文件名 |
| `ModGameDataTabsView.axaml` + `.cs` | GridSplitter 分割布局，IsValueEditorVisible 属性，标签页/选中行联动 |
| `DocumentWorkspaceView.axaml` | ImageDocument DataTemplate |
| `DocumentWorkspaceViewModel.cs` | OpenImageDocumentMessage 接收 + 侧边栏折叠 |
| `Documents.cs` | ImageDocument 新增 ImageSource 属性 |
| `SearchableDataGrid.axaml.cs` | RowDetailsVisibilityMode 初始化修复 |
| `App.axaml.cs` | 编辑器 DI 注册 + EntityOverviewEditor 自动注册 |
| `Recipe.cs` | 新增 3 个 [ReferenceField] |
| `ReferenceHelper.cs` | 新增 {mult}x{id} 模式 + ParseMultiplierReversed |
| `Resources.resx` (×3) | 新增 ~15 个本地化键 |

### 当前已知限制
1. **ImageDocument 浮动显示**：Dock.Avalonia 浮动窗口缺少 DataTemplate（停靠正常）
2. **排序箭头不显示**：Avalonia 11.3 `DataGridSortDescription` 抽象类
3. **Avalonia 版本锁定 11.3.x**
4. **TreasureTable `aTreasures`**：同字段混用 ItemType（GroupId.SubgroupId）和简单 id（TreasureTable）引用，单一 `[ReferenceField]` 无法覆盖两种目标类型


---

## Stage 7 — 查找替换完善 + 可视化编辑器夯实 + 体验增强 (v0.8.0-dev) | 2026-05-30

### 查找替换系统重构

#### 撤销支持
- 新建 `Data/Command/BatchEditCommand.cs` — 批量编辑命令，N 次替换作为单次原子撤销
- `FindReplacePanel` 集成 `CommandHistory` + `OnDirtyChanged` 回调
- `ReplaceOne` 创建 `EditCellCommand` 并通过 CommandHistory 执行（可撤销）
- `ReplaceAll` 创建 `BatchEditCommand` 一次性执行（一次撤销还原全部）

#### 字段级匹配
- `PerformSearch` 不再每行 `break` 第一个匹配列 → 逐列匹配，每行可有多个 `MatchInfo`
- `MatchInfo` 记录新增 `ColumnName`（C# 属性名）用于 SortMemberPath 列定位
- `NavigateTo` 精确滚动到匹配列（`ScrollIntoView(entity, col)`）
- 匹配 cell 边框高亮（OrangeRed 2px），通过索引定位正确列

#### 替换后刷新
- `RefreshGrid`：优先 `DataGridCollectionView.Refresh()`（合并视图），回退安全 ItemsSource 交换
- 替换后网格立即可见更新

#### 本地化
- FindReplacePanel 新增 `Loc` 属性，所有 AXAML/CS 硬编码字符串替换为本地化键
- 新增 14 个本地化键（`FindPrevious`、`FindNext`、`FindMatchCase`、`FindWholeWord`、`FindRegex`、`FindClose`、`FindWatermark`、`ReplaceWatermark`、`ReplaceButton`、`ReplaceAllButton`、`FindInvalid`、`FindNoMatches`、`FindMatchCount`、`FindReplaceSuccess`、`FindReplaceTitle`）

### 搜索模块
- `SearchPaneViewModel` 实现全局搜索：遍历全部 24 种实体类型，匹配所有 string 属性
- 支持 `col:value` 列筛选语法
- 搜索结果按实体类型分组，双击跳转到目标实体
- 最近搜索历史（最多 20 条）
- Pane.axaml 搜索面板：ScrollViewer + ItemsControl 扁平布局，禁用虚拟化避免滚动跳动

### 可视化编辑器增强

#### 编辑器面板架构
- `ValueEditorPanel` 改为**每子标签页独立实例**，自装配：`OnAttachedToVisualTree` → 查找兄弟 DataGrid → 绑定 SelectionChanged
- TabControl.ContentTemplate 内部采用三列 Grid（`*,Auto,Auto`）布局：DataGrid | GridSplitter | ValueEditorPanel
- 编辑器面板可见性通过 `GameDataTypeTabItem.IsEditorVisible` 控制，Toggle 按钮统一切换全部标签页

#### ItemType 编辑器
- **缩放/平移**：SpriteShow + WearShow 叠加视图支持鼠标滚轮缩放（0.1x–20x，光标位置为中心）+ 左键/中键平移
- **重置按钮**（`⟲`）：与下拉按钮水平并排，一键恢复默认缩放/平移
- **Flyout 滚动崩溃修复**：ListBox 虚拟化回收导致 NRE，`FuncDataTemplate` 添加 null 守卫 + 显式捕获闭包变量

#### Condition 编辑器
- 概览标签页新增 `FieldNames → Modifiers` 配对展示（两个逗号分隔列表按索引 1:1 配对）

### 引用系统增强

#### 命名空间感知匹配
- `FindBestMatch` 优先匹配命名空间前缀对应的实体：
  - 提取 `rawId` 中 `:` 前的命名空间前缀
  - 通过 `EntityModNames`（直接目录名匹配）或 `NamespaceToModName`（strModName → 目录名映射）查找
  - 命名空间匹配优先于最高 ModId 匹配
- `NavigToReference` 回退解析：命名空间 ID 在 int 解析前剥离前缀
- `ModLoadInfo` 新增 `Namespace` 属性，`ProfileManager.LoadMods` 填充 strModName
- `ReloadMergeTabsAsync` 构建 `NamespaceToModName` 字典

#### Overview 标签页引用解析
- `BuildRefChildren` + `ResolveSingleRefItem` 重构：直接调用 `FindBestMatch` 而非本地去重字典
- 与 DataGrid 单元格渲染行为完全一致（命名空间匹配 + 复合键 + ModId 优先级）
- `FindBestMatch` 改为 `internal` 可见性

#### BattleMove 条件引用
- `vUsPreConditions` / `vThemPreConditions`（简单逗号格式 `137,151,-143`）：`[ReferenceField(typeof(Condition), Separator = ",")]`
- 6 个括号三元组字段（`vUsConditions` 等，格式 `[98,0,0],[339,0,0]`）：`[ReferenceField(typeof(Condition), Separator = "],[", Pattern = "[{id}")]`
- `ExtractRawId` 新增 `"[{id}"` 模式：从括号包裹的三元组中提取第一个数字作为 condition ID

### 剧情编辑器
- **Story Graph 流程图连线**：`LayoutFlowNode` 递归布局后在父子节点间绘制 `Line` 连线

### 合并视图
- **Save 写 XML**：`ShowMergeSavePreviewAsync` DB 保存后自动调用 `ExportXmlAsync` 写回源 Mod XML 文件
- **标签页空白修复**：缓存恢复后调用 `RebuildFilteredItemsSources()` + 确保默认标签页选中
- **ShowAll 切换卡死修复**：缓存恢复后 `_overriddenEntityIds` 实例字段从静态 `OverriddenEntityIds` 恢复

### 体验增强

#### DataGrid 筛选栏
- 工具栏恢复 `FilterText` TextBox（`col:value` 语法），位于操作按钮右侧
- 后移至每个子标签页内部，DataGrid 上方独占一行，宽度填满

#### 编辑器设置面板
- `AppConfig` 新增 `Language`、`Theme`、`FontSize` 配置属性
- `SettingsPaneViewModel` 新增 BrowseGameDir、SetLanguage、SetTheme 命令
- Settings 面板 DataTemplate 重新设计：GameRootDir 可编辑 + 浏览按钮、Language ComboBox、Theme Toggle
- 配置持久化到 `config.json`

#### Profile 差异对比
- 新建 `Views/Dialog/ProfileDiffDialog` — 双栏 DataGrid 对比两个 Profile 的 Mod 加载列表
- ModIndexViewModel 新增 `CompareProfilesCommand`
- Profile 右键菜单新增 "Compare" 选项

#### Mod 打包 (.zip)
- `ModManager.ExportModToZipAsync` / `ImportModFromZipAsync`
- ModDatabase 右键菜单新增 Export Zip / Import Zip

### 新增文件（~8 个）
| 文件 | 说明 |
|------|------|
| `Data/Command/BatchEditCommand.cs` | 批量编辑命令（ReplaceAll 原子撤销） |
| `Views/Dialog/ProfileDiffDialog.axaml` + `.cs` | Profile 差异对比对话框 |
| `Views/UserControls/Editors/` 目录 | 已在 Stage 6 创建 |

### 修改文件（~15 个）
| 文件 | 关键改动 |
|------|---------|
| `FindReplacePanel.axaml` + `.cs` | 撤销支持、字段级匹配、cell 高亮、本地化 |
| `ModGameDataTabsView.axaml` + `.cs` | 每标签页编辑器、筛选栏、合并视图修复、缓存恢复修复 |
| `ValueEditorPanel.axaml.cs` | 自装配 DataGrid 绑定 |
| `GenericDataGridHelper.cs` | `FindBestMatch` 命名空间匹配、internal 可见 |
| `ReferenceHelper.cs` | `ExtractRawId` 新增 `[{id}` bracket 模式 |
| `StoryTreeEditor.cs` | 流程图连线 |
| `ItemTypeEditor.cs` | 缩放/平移 + 重置按钮 + Flyout NRE 修复 |
| `EditorHelper.cs` | Condition 配对字段、引用解析改用 FindBestMatch |
| `BattleMove.cs` | 8 个条件字段标注 ReferenceField（两种格式） |
| `Condition.cs` | 已在 Stage 6 存在 |
| `SearchPaneViewModel.cs` | 全局搜索实现 |
| `SettingsPaneViewModel.cs` + `AppConfig.cs` | 编辑器设置 |
| `ModIndexViewModel.cs` | Profile 对比命令 |
| `ModManager.cs` | ZIP 导入导出 |
| `ModInfo.cs` | ModLoadInfo.Namespace 属性 |
| `ProfileManager.cs` | 填充 ModLoadInfo.Namespace |
| `Pane.axaml` | 搜索面板重设计、设置面板重设计、ZIP/对比菜单项 |
| `Resources.resx` (×3) | 新增 ~25 个本地化键 |

### 新增/变更本地化键
`FindPrevious`, `FindNext`, `FindMatchCase`, `FindWholeWord`, `FindRegex`, `FindClose`, `FindWatermark`, `ReplaceWatermark`, `ReplaceButton`, `ReplaceAllButton`, `FindInvalid`, `FindNoMatches`, `FindMatchCount`, `FindReplaceSuccess`, `FindReplaceTitle`, `FieldNamesModifiers`, `Language`, `Theme`, `FontSize`, `BrowseGameRoot`, `ExportModZip`, `ImportModZip`, `CompareProfiles`, `ResetZoom`

### 当前已知限制
1. **ImageDocument 浮动显示**：Dock.Avalonia 浮动窗口缺少 DataTemplate（停靠正常）
2. **排序箭头不显示**：Avalonia 11.3 `DataGridSortDescription` 抽象类
3. **Avalonia 版本锁定 11.3.x**
4. **TreasureTable `aTreasures`**：同字段混用 ItemType（GroupId.SubgroupId）和简单 id（TreasureTable）引用，单一 `[ReferenceField]` 无法覆盖两种目标类型
5. **数据验证**：`Data/Validation/` 代码存在但未接入保存流程 — 应作为提示（Warning）而非阻止（Error），需缩小验证范围到当前 Mod 数据以避免跨 Mod 误报
6. **像素画编辑器**：缺少逐像素手绘工具（画笔/橡皮擦/填充/取色）、背景透明化处理、调色板编辑
7. **像素编辑器 ↔ ModImages 联动**：无法从 ModImages 图片列表双击直接打开像素编辑器，也无法从像素编辑器自动添加到图片列表
8. **资源浏览器**：缺少右键菜单（删除/重命名/复制路径）、文件图标、文件类型过滤
9. **Mod 依赖检查**：跨 Mod 命名空间依赖分析 — 未实现
10. **查找面板**：不跟随 Semi.Avalonia 深色/浅色主题切换


---

## Stage 8 — 解耦与重构 (v0.9.0-dev) | 2026-05-30

> 详细设计见 `Docs/05-refactoring-plan.md`

### Phase 1：全局可变状态解耦

#### 问题
`GenericDataGridHelper` 持有 12 个公共静态可变字典/HashSet，所有 `ModGameDataTabsView` 实例共享同一份状态，标签页切换时通过 `TakeSnapshot`/`RestoreSnapshot` 手动复制 9 个集合来模拟隔离。

#### 方案
创建每个标签页独立的实例 store，通过 GDH 桥接属性委托访问。

#### 新建文件
| 文件 | 说明 |
|------|------|
| `Services/EntityMergeStore.cs` | 合并状态容器：`ReferenceLookups`、`EntityModNames`、`EntityMergedIds`、`OverriddenEntityIds`、`OverlayChainDisplay`、`FieldSources`、`FieldConflicts`、`NamespaceToModName`、`SubjectCache` |
| `Services/EditTrackingStore.cs` | 编辑追踪容器：`EditedCells`、`NewEntityIds` |

#### 修改
- `GenericDataGridHelper.cs`：所有公共静态属性（`ReferenceLookups`、`EntityModNames` 等 12 个）改为委托给 `SetActiveStores()` 设置的活跃实例 store；无活跃 store 时回退到私有静态集合（向后兼容）
- `TakeSnapshot` / `RestoreSnapshot` 大幅简化：直接缓存 `(EntityMergeStore, EditTrackingStore)` 实例，不再逐字段复制 9 个集合
- `ModGameDataTabsView.axaml.cs`：`OnAttachedToVisualTree` 设置活跃 store；`TabSnapshotCache` 值类型更新
- 所有现有消费者（`ReferenceResolver`、`EditorHelper`、`DataExportService`、8 个 Converter）无需改动——通过桥接属性透明访问

### Phase 2：巨型类拆分

#### FilterService 提取
- 新建 `Services/FilterService.cs`
- 从 `ModGameDataTabsView` 提取：`ApplyFilters`、`ParseFilterTokens`、`SplitFilterText`、`MatchesAllTokens`、`FindColumnProperty`、`GetStringProperties`（~150 行移除）
- `RebuildFilteredItemsSources` 保留在视图中（与 `Tabs`、`DataTabs` UI 元素耦合），委托给 `_filterService.ApplyFilters()`

### Phase 4：引用 Pattern 策略 + EditorUIFactory

#### ReferencePattern 策略
- 新建 `Helper/ReferencePattern.cs` — 抽象基类 + 5 个私有嵌套实现
  - `IdPattern`、`IdXMultPattern`、`MultXIdPattern`、`IdEqualsValuePattern`、`BracketIdPattern`
- 每个子类封装：`ExtractRawId`（ID 提取）、`FormatDisplay`（DataGrid 显示）、`FormatExtraInfo`（Overview 额外信息）
- 调用方迁移：
  - `ReferenceHelper.ExtractRawId`：40 行 switch/if → 1 行 `ReferencePattern.FromName(pattern).ExtractRawId(segment)`
  - `GenericDataGridHelper.FormatSegmentDisplay`：30 行 → 5 行委托
  - `EditorHelper.FormatExtraInfo`：12 行 → 1 行委托；移除未使用的 `FmtPct`

#### EditorUIFactory 提取
- 新建 `Helper/EditorUIFactory.cs` — 纯 UI 工厂：`NewNode`、`NavOnCtrl`、`MakeTab`、`CreateEditorTabs`
- `EditorHelper` 中的 4 个方法改为委托给 `EditorUIFactory`（向后兼容）

### Phase 5：去重与接口化

#### ImageService 统一图片逻辑
- 新建 `Services/ImageService.cs`（DI 注册为 Singleton）
- 整合源：
  - `PhpParser`：`PairImages`、`LooksLikeSplitHalfPairs`、`IsX2Variant`、`IsX2Image` → 委托给 `ImageService`
  - `EditorHelper`：`GetImageSearchDirs` → 移除，使用 `ImageService.GetImageSearchDirs()`
  - `ItemTypeEditor`：`FindImage` + `root` 参数 → 移除，使用 `_imageService.FindImage()`

#### ConvertValue 去重
- `XmlParser.ConvertValue` 内部实现 → 委托给 `ValueConverter.Convert`

#### ICommandHistory 接口
- 新建 `Data/Command/ICommandHistory.cs`
- `CommandHistory` 实现接口，支持 DI 注入和 mock

#### ViewModelBase 注入
- `Loc` 和 `Notification` 属性改为可注入（优先使用注入实例，回退 `App.Localizor` / `App.Notification`）
- 新增带参数构造函数，无参构造函数保留（Avalonia 框架兼容）

#### CommandHistory 剪枝优化
- `Execute` 方法满容量时避免 `ToArray()` 分配，改用临时栈翻转

### 修改文件清单（本阶段）
| 文件 | 关键改动 |
|------|---------|
| `GenericDataGridHelper.cs` | 静态属性 → 实例 store 委托桥接 |
| `ModGameDataTabsView.axaml.cs` | 实例 store 绑定/缓存；FilterService 集成 |
| `ViewModelBase.cs` | Loc + Notification 可注入 |
| `ReferenceHelper.cs` | ExtractRawId → ReferencePattern 策略 |
| `EditorHelper.cs` | 委托给 ReferencePattern / EditorUIFactory / ImageService |
| `PhpParser.cs` | 图片方法 → ImageService |
| `ItemTypeEditor.cs` | FindImage + root → ImageService；DI 字段 |
| `XmlParser.cs` | ConvertValue → ValueConverter |
| `CommandHistory.cs` | 实现 ICommandHistory；O(1) 分配剪枝 |
| `ModInfo.cs` | ModLoadInfo.Namespace 属性 |
| `ProfileManager.cs` | 填充 Namespace |
| `App.axaml.cs` | ImageService DI 注册 |
| `Resources.resx` (×3) | 新增 ~25 个本地化键（Stage 7 查找替换相关） |

### 重构效果

| 指标 | 改善前 | 改善后 |
|------|--------|--------|
| GDH 公共静态可变集合 | 12 个 | 0（全部委托给实例 store） |
| 添加引用 Pattern 需修改文件 | 3-4 个 | 1 个 |
| `ModGameDataTabsView` | ~2400 行 | ~2250 行 |
| `EditorHelper` | ~375 行 | ~290 行 |
| `PhpParser` | ~164 行 | ~124 行 |
| `ItemTypeEditor` | ~248 行 | ~228 行 |
| CommandHistory 满容量分配 | 1 数组 + N push | 1 临时栈 + N push/pop |
| ViewModelBase 可测试性 | 硬编码静态属性 | 可注入 mock |

---

## Stage 9 — UI 重构与面板系统 (v0.11.0-dev) | 2026-05-30 ~ 2026-05-31

### 新增功能

**HomePage 欢迎页**
| 功能 | 说明 |
|------|------|
| 三卡片入口 | Browse Game Data / New Mod / Import Mod |
| Recent Mods 列表 | 显示实体数 + 时间（跨 13 张核心表计数） |
| Profiles 入口 | 主页直接列出 Profile → 双击打开合并视图 |
| 拖拽导入 | 拖文件夹/XML 到窗口 → 自动导入 + 打开 |
| 自动刷新 | 关闭所有文档回主页时自动刷新列表 |

**工具面板（Grid 分栏）**
| 面板 | 位置 | 内容 |
|------|------|------|
| 覆盖链 | 左 220px | Winner/Loser 分区 + 字段贡献展开 |
| 可视化编辑器 | 右 280px | Recipe/Story/Treasure/ItemType 编辑器 |
| 图片预览 | 右 | 扫描实体所有含 "Img" 字段 → mod img 目录 + 游戏 img 目录 |
| 引用预览 | 右 | Ctrl+Click 引用 → Peek（预览目标属性，不跳转）；Pin 锁定 |
| 搜索/冲突/验证 | 底部 150px | 三个 Tab，冲突实时刷新 |

**引用系统增强**
| 功能 | 说明 |
|------|------|
| Peek（预览） | `GenericDataGridHelper.PeekRequested` — Ctrl+Click 在右侧面板预览，只有 "Open Full" 才跳转 |
| SecondaryTarget | `ReferenceFieldAttribute` 支持 `SecondaryTargetEntityType` + `SecondaryTargetKey`（TreasureTable 混合引用） |

**数据验证**
| 功能 | 说明 |
|------|------|
| 保存时验证 | 只验证改动过的实体（EditedCells + NewEntityIds），不弹窗 |
| 底部面板 | 验证结果写入底部 Validation Tab |

**依赖分析**
| 功能 | 说明 |
|------|------|
| 扫描 | 合并视图工具栏 "Deps" 按钮 → 5 列 DataGrid（Source/Mod/Field/Target/Issue） |
| 导出 | CSV 导出 + 列宽可拖拽 + Ctrl+C 复制 |

**日志系统**
| 功能 | 说明 |
|------|------|
| 早期启动 | `Program.cs` try/finally 包裹 → 崩溃也能记录 |
| 配置读取 | `appsettings.json` → `Logging:LogLevel` 覆盖 |
| 过滤 | `Microsoft.Extensions.Localization` 调为 Warning |
| 结构化 | 修复 6 处插值 `$"..."` → 结构化 `{Placeholder}` |

**Profile 多环境**
| 功能 | 说明 |
|------|------|
| 选中即加载 | `OnSelectedProfileChanged` → 自动 LoadMods |
| 设为活跃 | 右键 "Set as Active" → `ActiveProfileId` 持久化 |
| 双击打开 | 双击 Profile 列表项 → 打开合并视图 |
| 添加 Mod | 右键 "Add Mod..." → EditProfileView |

**编辑体验**
| 功能 | 说明 |
|------|------|
| Save 统一 | 单 Mod Save = DB + 写 XML，去掉独立 Export XML 按钮 |
| Ctrl+S | 键盘快捷键保存 |
| 自动保存 | `AppConfig.AutoSaveInterval` → DispatcherTimer 定时保存 |
| 空 Mod 引导 | 0 实体时显示公告栏 "Add Your First Entity" |
| 冲突脉动 | 冲突 > 0 时按钮红底白字 (#DC3C28) |
| 首个非空 Tab | 打开视图自动跳转到有数据的第一个 Tab |
| 脏关闭确认 | 关闭 dirty Tab 时弹出保存/不保存/取消对话框 |
| Tab 表头 | IsDirty 属性 "● " + IsDirty 属性 |
| FindReplace 脏标记 | Ctrl+H 替换 → `EditCellCommand.Execute` 调用 `EditedCells.Add` |
| FindReplace 主题跟随 | `Brushes.OrangeRed` → `SystemControlHighlightAccentBrush` |

**配置与偏好**
| 功能 | 说明 |
|------|------|
| AutoSaveInterval | 秒，0=关闭 |
| DefaultExportFormat | SaveFileDialog 默认扩展名（csv/xlsx/md/json） |
| GridRowHeight | SearchableDataGrid 行高 |
| ActiveProfileId | 当前活跃 Profile（主页/侧栏高亮） |

**侧栏重组**
| 按钮 | 说明 |
|------|------|
| 🏠 Home | 关闭所有文档回主页 |
| 📥 Import | 直接导入（不开面板） |
| 🗄️ Mod Database | 查看已导入 Mod 列表 |
| 👥 Profiles | 合并视图入口 |
| 📁 Explorer | 文件浏览器 |
| 🔍 Search | 全局搜索 |
| ⚙️ Settings | 编辑器偏好 |

**底部面板**
| Tab | 内容 |
|------|------|
| Search | 搜索结果列表（框架就位） |
| Conflicts | FieldConflicts 实时列表 + 刷新按钮 |
| Validation | 保存后验证警告计数 |

### 新建文件（本轮 ~20 个）

| 文件 | 用途 |
|------|------|
| `ViewModels/MainContent/HomePageViewModel.cs` | 主页逻辑（Browse/New/Import/Recent/Profiles） |
| `Views/UserControls/HomePage.axaml/.cs` | 三卡片入口页 |
| `ViewModels/MainContent/OverlayChainToolContent.cs` | 覆盖链数据（Winner/Loser + 字段贡献） |
| `Views/UserControls/OverlayChainToolView.axaml/.cs` | 覆盖链面板 UI |
| `ViewModels/MainContent/ReferenceInspectorContent.cs` | 引用预览数据 |
| `Views/UserControls/ReferenceInspectorView.axaml/.cs` | 引用预览面板 UI |
| `ViewModels/MainContent/ImagePreviewContent.cs` | 图片预览数据 |
| `Views/UserControls/ImagePreviewView.axaml/.cs` | 图片预览面板 UI |
| `ViewModels/MainContent/BottomToolsViewModel.cs` | 底部面板数据（Search/Conflicts/Validation） |
| `Views/UserControls/BottomToolsView.axaml/.cs` | 底部面板 UI |
| `Views/UserControls/RightPanelView.axaml/.cs` | 右侧面板包装（Editor/Images/Ref Inspect Tab） |
| `Views/Dialog/ConflictListDialog.axaml/.cs` | 冲突详情弹窗（可调列宽/复制/CSV 导出） |
| `Views/Dialog/DependencyListDialog.axaml/.cs` | 依赖分析弹窗（同上） |
| `Services/DependencyAnalysisService.cs` | 跨 Mod 引用完整性扫描 |
| `Docs/09-current-status.md` | 总进度报告 |

### 修改文件（本轮 ~15 个）

| 文件 | 重要变更 |
|------|---------|
| `MainWindow.axaml` | 侧栏重排（7 按钮三组）+ 工具栏 New/Import + 面板切换 ◀ ▶ ▼ + HomePage 层 |
| `DocumentWorkspaceView.axaml` | Grid 三区分栏（左/中/右/底）+ 拆分器 |
| `DocumentWorkspaceView.axaml.cs` | 拖拽导入 + VisualEditorRequested 桥接 |
| `DocumentWorkspaceViewModel.cs` | IsHomePageVisible + ActiveDocumentTitle + 三面板内容持有 |
| `ModGameDataTabsView.axaml` | 去掉 inline ValueEditorPanel + GridSplitter + PanelRight 按钮 |
| `ModGameDataTabsView.axaml.cs` | 静态事件总线 + 空 Mod 引导 + Ctrl+S + 自动保存 + 首个非空 Tab + 缓存恢复修正 + 关闭确认 |
| `SearchableDataGrid.axaml` | 去掉 RowDetails 覆盖链展开面板 |
| `ModMessages.cs` | `OpenModGameDataDocumentMessage` 加 `ReadOnly` 参数 |
| `EditCellCommand.cs` | `Execute()` 调用 `GenericDataGridHelper.EditedCells.Add` |
| `FindReplacePanel.axaml.cs` | 主题跟随 `SystemControlHighlightAccentBrush` |
| `ReferenceFieldAttribute.cs` | `SecondaryTargetEntityType` + `SecondaryTargetKey` |
| `GenericDataGridHelper.cs` | `PeekRequested` + `LookupSubjectByRawId` 二级 fallback |
| `TreasureTable.cs` | 二级 ReferenceField 标注 |
| `ViewModelBase.cs` | `ILogger Logger` 属性 |
| `Program.cs` / `LoggingExtensions.cs` | Serilog 早期启动 + appsettings 读取 |
| `AppConfig.cs` | AutoSaveInterval / DefaultExportFormat / GridRowHeight / ActiveProfileId |
| `SettingsPaneViewModel.cs` | 4 个新设置项显示绑定 |
| `Pane.axaml` | 4 个新设置 UI 行 + 双 Profile 菜单项 |
| `MainStatusBar.axaml` | 文档数 + 活跃标题 |
| `3 个 resx 文件` | 10+ 新本地化 Key |
| `ModManager.cs` | `ImportModAsync` 返回 `ModInfo?` + `LoadModAsync` 快速跳过 |

### 架构决策

| 决策 | 原因 |
|------|------|
| Grid 面板 > Dock ToolDock | Dock.Avalonia 11.3.11 的 ToolDock ItemsSource MVVM 绑定不成熟——`CreateLayout()` 非 virtual、`IRootDock` 命名空间未暴露、`InitLayout` 介入时机不明确 |
| 静态事件总线 | `ModGameDataTabsView` 的 5 个 `static Action` 连接 DataGrid 选区到各面板，简单直接 |
| `DocumentWorkspaceViewModel.Instance` | scoped VM 需静态访问点供 DataTemplate 创建的控件获取面板数据 |
| Save = DB + XML 统一 | 消除单 Mod / 合并视图行为差异 |

### 已知问题

| 问题 | 状态 |
|------|:--:|
| ToolDock 集成 | 🔜 需等 Dock.Avalonia 版本更新或更完整的文档 |
| 底部 Search Tab 空 | 🔜 需接入 SearchPaneViewModel |
| Validation Tab 报告简略 | 🔜 只显示计数，未显示详情 |
| 列复制粘贴 | 🔜 未实现 |
| 批量编辑 | 🔜 未实现 |
| Dock 面板布局持久化 | 🔜 未实现 |
| 图片搜索逻辑重复 | 3 处（EditorHelper ×2 + ItemTypeEditor） | 1 处（ImageService） |

---

## Stage 10 — 引用检查器 + 资源管理器 + 搜索增强 + 面板打磨 (v0.12.0-dev) | 2026-05-31

### 底部搜索 Tab 接入
| 功能 | 说明 |
|------|------|
| SearchService | 抽取共享搜索引擎（24 实体类型全文搜索 + `col:value` 语法） |
| SearchResults 共享模型 | `SearchResultGroup` / `SearchResultItem` 提取到 `Helper/SearchResults.cs` |
| 底部搜索 UI | TextBox + Go 按钮 + 进度条 + 分组结果列表 |
| 搜索栏 Ctrl+Click | Ctrl+左键=跳转，Ctrl+右键=peek |

### 资源管理器右键菜单 + F2 重命名
| 功能 | 说明 |
|------|------|
| 右键菜单 | Open / Open in Explorer / Copy Full Path / Rename / Delete |
| F2 快捷键 | TreeView 中选中项按 F2 → `RenameDialog` 弹窗输入新名称 |
| DeleteItem | 确认弹窗后删除文件/文件夹 |
| 文件类型图标 | `FileTypeIconConverter`：按扩展名显示不同 Symbol 图标（图片/XML/JSON 等） |

### Reference Inspector 全面重做
| 功能 | 说明 |
|------|------|
| Ctrl+LeftClick = 跳转 | 导航到目标实体（同时 peek 入栈） |
| Ctrl+RightClick = Peek | 推送 history 栈 + 预览（Pin 时只推不入栈） |
| Peek 历史栈 | 后退/前进双栈，**智能去重**（`TryPopFromHistory` 搜索全部历史） |
| Pin 逻辑 | **仅冻结自动显示**（新 peek 不覆盖概览），历史导航 ◀▶ 始终可用 |
| Unpin | 即时同步概览到当前栈顶 |
| Open Full | 修复为 `NavigateToByEntityId` + 打开源 Mod 文档（双重保障） |
| 快照存储属性 | `PeekSnapshot.SavedProperties` 保存完整属性列表，退/进时完整恢复 |
| 视觉反馈 | Pin 时 DarkOrange 边框 + "🔒 PINNED" 标签 + 按钮 "Pin"/"Unpin" 切换 |
| 引导文字 | 增强空状态 + 页脚说明 |
| 右键菜单移除 | 引用列右键菜单与 Ctrl+右键冲突，已移除 → 统一用 Ctrl 操作 |
| modSource tooltip | 默认单元格 mod 来源提示栏移除 |

### 覆盖链面板精简
| 功能 | 说明 |
|------|------|
| 移除字段贡献 | 删去 `ContributedFields` / `HasFields` / `IsExpanded` / 展开按钮（游戏引擎按整实例覆盖，非逐字段） |
| 简化为 Winner/Loser 列表 | 只显示 Mod 名 + Subject + ID |

### Dock 面板布局持久化
- `AppConfig` 新增 `LeftPanelWidth` / `RightPanelWidth` / `BottomPanelHeight` + 面板可见性配置项
- `DocumentWorkspaceViewModel` 构造时恢复可见性，Toggle 面板时保存

### Images 面板修复与增强
| 功能 | 说明 |
|------|------|
| 多目录搜索 | 扫 `{gameRoot}/img/` + `Mods/*/img/` + `Mods/*/SubMod/img/`（两级深度） |
| Entity FilePath 推导 | 使用 `entity.FilePath` 推导实体所属 Mod 的 img 目录 |
| 显示文件名 | 仅显示原始文件名 + 字段来源，不显示全路径 |
| 状态指示 | ✓ 绿色 = 文件存在，✗ 红色 = 缺失 |
| 双击打开 | 双点 ✓ 项 → 系统默认程序打开图片 |
| 诊断信息 | 没找到时列出搜索目录和实体 FilePath 便于排查 |

### 帮助文档内嵌
- 5 篇中文 + 2 篇英文 .md 帮助文档加入 `Help/` 目录
- `.csproj` 添加 `AvaloniaResource` include（编译进 DLL）
- 清除测试文件 `Help/en/aa.md`

### Bug 修复清单
| 问题 | 修复 |
|------|------|
| Ctrl 时右键菜单弹出 | `_ctrlWasPressed` 静态标志 + `ContextRequested` 事件拦截 |
| SearchResultItem EntityId 引用 | `item.EntityId` → `item.Entity.EntityId` |
| FileTypeIcon 编译错误 | `WindowConsoleApp` → `AppGeneric`（FluentIcons 无此符号） |
| ResourceManager Clipboard API | `Application.Current.Clipboard` → `TopLevel.Clipboard`（Avalonia 11） |
| BottomToolsViewModel GetRequiredService | 补充 `using Microsoft.Extensions.DependencyInjection` |
| DocumentWorkspaceViewModel 启动 NRE | Config 未加载时添加 null guard |
| OverlayChainToolView OnToggleFieldsClick | 随字段贡献移除一并清理 |

### 新增文件（12 个）
| 文件 | 说明 |
|------|------|
| `Helper/SearchResults.cs` | 共享搜索模型 |
| `Services/SearchService.cs` | 共享搜索引擎 |
| `Helper/Converter/FileTypeIconConverter.cs` | 文件类型图标转换器 |
| `Views/Dialog/RenameDialog.axaml` + `.cs` | 重命名弹窗 |
| `Help/zh/GettingStarted.md` | 中文入门指南 |
| `Help/zh/ReferenceSystem.md` | 中文引用系统指南 |
| `Help/zh/MergeView.md` | 中文合并视图指南 |
| `Help/en/Welcome.md` | 英文欢迎页 |
| `Help/en/GettingStarted.md` | 英文入门指南 |

### 修改文件（~20 个）
| 文件 | 关键改动 |
|------|---------|
| `ReferenceInspectorContent.cs` | 完整重写：Pin/Unpin/history 双栈/智能去重/快照属性存储 |
| `ReferenceInspectorView.axaml` + `.cs` | Pin/Unpin 按钮 + ◀▶ 导航 + 增强引导 + Open Full 修复 |
| `GenericDataGridHelper.cs` | Ctrl+LeftClick=跳转, Ctrl+RightClick=Peek; `IsPeekPinned`; `_ctrlWasPressed`; 移除引用列右键菜单 + modSource tooltip |
| `BottomToolsViewModel.cs` + `BottomToolsView.axaml` + `.cs` | 搜索 UI + SearchService 集成 + Ctrl+Click 支持 |
| `SearchPaneViewModel.cs` | 委托给 `SearchService` |
| `ResourceManagerViewModel.cs` | Delete/Rename/CopyPath/OpenInExplorer 命令 + `RenameDialogRequested` |
| `Pane.axaml` + `.cs` | TreeView 右键菜单 + F2 重命名 + 搜索 Ctrl+Click |
| `DocumentWorkspaceViewModel.cs` | Pin/Peek 逻辑 + 面板布局恢复 |
| `DocumentWorkspaceView.axaml` + `.cs` | 布局持久化 |
| `ImagePreviewContent.cs` + `ImagePreviewView.axaml` + `.cs` | 多目录搜索 + 文件名显示 + 双击打开 |
| `OverlayChainToolContent.cs` + `OverlayChainToolView.axaml` + `.cs` | 移除字段贡献 |
| `AppConfig.cs` | 面板布局属性 |
| `App.axaml` + `.cs` | FileTypeIconConverter + SearchService DI |
| `NeoEditor.csproj` | Help 文件 AvaloniaResource include |

### 当前已知限制
1. **排序箭头不显示**：Avalonia 11.3 `DataGridSortDescription` 抽象类
2. **Avalonia 版本锁定 11.3.x**
3. **TreasureTable `aTreasures`**：同字段混用复合键引用，单一 `[ReferenceField]` 无法覆盖两种目标类型
4. **ImageDocument 浮动显示**：Dock.Avalonia 浮动窗口缺少 DataTemplate
5. **像素画编辑器**：缺少逐像素手绘工具
6. **列复制粘贴**：Ctrl+C/V 单元格未实现
7. **批量编辑**：多行选中批量改同字段未实现

---

### Stage 10 补充 (2026-05-31)

#### Help 菜单修复
| 问题 | 修复 |
|------|------|
| Help 菜单不显示文档 | `MainMenuBar` 改为代码动态构建 MenuItem（`ItemsSource`+DataTemplate 在 Avalonia 11.3 不可靠） |
| Help 文件找不到 | `.csproj` 移除 `LinkBase` + 遍历上层目录查找 `Help/` |
| Markdown 渲染空白 | 升级 Markdown.Avalonia 11.0.2→11.0.3 解决 |

#### 首次启动引导
- HomePage 新增 GameRoot 设置提示横幅（未设置时显示，定时检测，配置完成后自动隐藏）
- `NavigateToSettings()` 直接切换侧栏到 Settings 面板
- `MainWindowSideBarViewModel` 接收 `SwitchToSettingsMessage`

#### New Mod 创建流程
- `CreateModDialog` 新增 Namespace 输入 + 自动创建 Profile 复选框
- 创建后自动生成 getmods.php 内容并打开合并视图

#### 本地化补齐
- 新增 4 个缺失 Key（`GameRootDir`, `Help`, `ProfileDescription`, `Content`）
- 补全 20+ 实体类型名（Tab 表头显示中文）
- `Faction`/`FindInvalid`/`NavigateSameEntity` 三文件同步

---

## Stage 11 — 编辑体验夯实 + Markdown 升级 (v0.13.0-dev) | 2026-06-01

### 单元格复制/粘贴 (Ctrl+C/V)
- **内部 buffer 方案**：弃用系统剪贴板，改用静态 `_copyBuffer` 变量
- `Ctrl+C` → 提取选中行可见列原始属性值 → TSV 格式存入 buffer
- `Ctrl+V` → 取 buffer 首行 → 逐列写入选中行第一个实体
- 支持撤销：单格 → `EditCellCommand`，多格 → `BatchEditCommand`
- 类型安全：`ValueConverter.Convert` 包裹 try-catch，转换失败自动跳过

### 底部 Search Tab 完善
| 功能 | 说明 |
|------|------|
| Enter 键搜索 | TextBox `<KeyBinding Gesture="Enter">` 直接触发 |
| 清除按钮 | ✕ 按钮 → `ClearSearchCommand` |
| Recent Searches | 最多 15 条历史，标签式展示，点击直接搜索 |
| 双击导航 | `DoubleTapped` → 直接跳转到目标实体 |
| 样式改进 | 结果项 `Foreground="Teal"` + `TextWrapping="Wrap"` |
| 摘要增强 | 显示匹配总数 `"{statusText} (N result(s))"` |

### Dock 面板列宽持久化
- `DocumentWorkspaceView` 监听 VM `PropertyChanged`，切换面板显隐时自动调整 Grid Column/Row 宽度
- 隐藏面板 → 宽度设为 0（空间完全回收），显示 → 恢复 config 中保存的宽度
- `OnSplitterDragCompleted` → 实时保存当前列宽到 `AppConfig`
- `OnAttachedToVisualTree` → 从 config 恢复初始列宽

### Markdown → LiveMarkdown.Avalonia 迁移
- 替换 `Markdown.Avalonia 11.0.3` → `LiveMarkdown.Avalonia 1.9.2` + Math/Svg 扩展
- `App.axaml` 注册 `Styles.axaml` + `Defaults.axaml`（之前缺失导致无格式化）
- `MarkdownDocument` 新增 `MarkdownBuilder` 属性（`ObservableStringBuilder`）
- `LinkCommand` → `RelayCommand<LinkClickedEventArgs>` 拦截 `.md` 链接
- `.md` 相对链接 → 通过 `OpenHelpDocumentMessage` 在编辑器内打开标签页
- 外部链接 → `Process.Start` 系统浏览器打开

### Mod 制作指南内嵌
- 从 `NeoScavenger 模组制作指南中文翻译精修1.2（新）.docx` 提取纯文本
- 保存为 `Help/zh/ModGuide.md`（~41KB，257 行）
- 段落间添加空行确保 markdown 正确渲染
- 关键术语添加反引号强调（`VanillaOverride`、`AddOn`、`neogame.xml` 等）
- getmods.php 代码示例用 ` ``` ` 代码块包裹

### XML 字段说明集成
- 新建 `DocxTextExtractor` — .docx 文本提取 + 字段描述解析
- 新建 `FieldDescriptionService` — 加载/缓存/查询字段描述
- 启动时自动从 `游戏XML文本各项说明修正增强版.docx` 提取 → 缓存 `field_descriptions.json`
- `GenericDataGridHelper.ConfigureColumn` 优先显示 .docx 字段说明作为列头 Tooltip
- 优先级：.docx 描述 > `[Display]` 本地化资源 > `[Comment]` 属性

### Ref Inspect UI 改进
- `?` 图标移到 "Ref Inspect" 标题旁，Tooltip 清晰区分 Ctrl+Left/Right/Double-click
- 按钮操作说明全部收入 Tooltip（◀ ▶ Pin Open Full）
- 空状态仅保留一行简洁提示
- 移除底部永久占用的说明文字条

### 新增文件
| 文件 | 说明 |
|------|------|
| `Helper/DocxTextExtractor.cs` | .docx 文本提取与字段描述解析 |
| `Services/FieldDescriptionService.cs` | 字段描述加载/缓存/查询 |
| `Help/zh/ModGuide.md` | 模组制作指南（从 .docx 提取） |

### 修改文件
| 文件 | 关键改动 |
|------|---------|
| `ModGameDataTabsView.axaml.cs` | 内部 buffer 复制粘贴 + `HasFlag` 修复 |
| `BottomToolsViewModel.cs` | Recent Searches + ClearSearch + NavigateToResult |
| `BottomToolsView.axaml` + `.cs` | Enter 键绑定 + Clear 按钮 + 双击 + 样式 |
| `DocumentWorkspaceView.axaml` + `.cs` | 面板列宽持久化 + 显隐空间回收 |
| `App.axaml` | LiveMarkdown.Avalonia 样式注册 |
| `App.axaml.cs` | FieldDescription 初始化 + ModGuide 提取 |
| `Documents.cs` | MarkdownBuilder + LinkCommand + 图片路径预处理 |
| `ReferenceInspectorView.axaml` | ? 图标 + Tooltip 重构 |
| `GenericDataGridHelper.cs` | FieldDescriptions 静态桥接 + Tooltip 增强 |
| `NeoEditor.csproj` | 替换 Markdown.Avalonia → LiveMarkdown.Avalonia |
| `Resources.resx` (×3) | 新增本地化键 |

### 新增本地化键
`ModGuide`, `RefInspect`, `RefInspectPinned`, `RefInspectHelp`, `RefInspectBack`, `RefInspectForward`, `RefInspectPin`, `RefInspectUnpin`, `RefInspectPinHelp`, `RefInspectOpenFull`, `RefInspectOpenFullHelp`, `RefInspectEmptyHint`, `BottomSearchWatermark`, `BottomSearchClear`, `RunValidation`, `ConflictsTab`, `ValidationTab`, `SearchTab`

### 当前已知限制
1. **排序箭头不显示**：Avalonia 11.3 `DataGridSortDescription` 抽象类
2. **Avalonia 版本锁定 11.3.x**
3. **像素画编辑器**：缺少逐像素手绘工具
4. **批量编辑**：多行选中批量改同字段未实现
5. **Markdown 链接内部打开**：LiveMarkdown.Avalonia 1.9.2 `LinkCommand` 绑定需验证运行时行为

---

## Stage 17 — 引用系统重构 (v0.18.0-dev Phase 2) | 2026-06-08

### 新增文件
| 文件 | 说明 |
|------|------|
| `Helper/ReferenceParser.cs` | 纯函数解析层：`ParsedRef` / `TargetKeyInfo` / `ResolvedRefSegment` / `ParsedReferenceField` 类型 + 所有解析方法 |
| `Helper/ReferenceIndex.cs` | Context-aware 引用索引：`(sourceEntityId, propertyName, rawId) → targetEntityId`，O(1) 查找 |

### 重构文件
| 文件 | 变更 |
|------|------|
| `Helper/ReferenceHelper.cs` | 所有方法标记 `[Obsolete]`，委托到 `ReferenceParser` |
| `Helper/ReferenceFieldAttribute.cs` | 不变 |
| `Helper/ReferencePattern.cs` | `IdPattern` / `IdXMultPattern` 新增 `-` 否定前缀剥离（`ExtractRawId("-115")` → `"115"`），`FormatExtraInfo` 报告 `"-"` |
| `Helper/GenericDataGridHelper.cs` | Bug 1 修复（`Convert.ToInt64` 类型安全比较）；新增 `FindBestMatch(sourceEid, propName)` 重载；`LookupSubjectByRawId` 接受 source context；导航路径传递 sourceEid |
| `Views/UserControls/SearchableDataGrid.axaml.cs` | Bug 2 修复（Cell 计数替代 `IndexOf`）；`ColumnMetaCache` 缓存；排序路径安全模式；多值单元格 `Tag=rawText` |
| `Views/UserControls/ModGameDataTabsView.axaml.cs` | NavigateToEntityImpl 改用 `SharedDataGrid` + `DoScrollToEntity` 重试机制 |
| `Views/UserControls/ModGameDataTabsView.Tab.cs` | `SwitchTabItemsSource`（try-catch + 安全重置） |
| `Views/UserControls/ModGameDataTabsView.Data.cs` | `await Index.BuildAsync()` 异步索引构建 |
| `Services/EntityMergeStore.cs` | 新增 `Index` 属性（lazy init `ReferenceIndex`） |

### 迁移文件（ReferenceHelper → ReferenceParser）
`ReferenceResolver.cs` / `DataExportService.cs` / `ReferenceIntegrityRule.cs` / `EditorHelper.cs` / `EntityVisualizers.cs` / `ModGameDataTabsView.Operations.cs`

### 已修复 Bug (7 个)
| # | 问题 | 修复 |
|---|------|------|
| 1 | FindBestMatch `is int val` 对 long/null/EF 代理失效 → 总是返回 id=1 | `Convert.ToInt64` 类型安全比较 |
| 2 | DataGrid 列索引 `Children.IndexOf(cell)` 含 RowHeader 偏移 | Cell 计数 + `ColumnMetaCache` |
| 3 | 渲染与跳转解析不一致（不同路径查不同 key） | 统一走 `index.Lookup(sourceEid, propName, type, rawId)` |
| 4 | 多值单元格用显示文本当 rawId → 解析出垃圾 | `TextBlock.Tag = rawText` |
| 5 | `-` 前缀被当 ID 一部分 → 索引查负数 | `ReferencePattern` 剥离 `-`，`FormatExtraInfo` 报告 |
| 6 | 显示缓存 key 冲突（MergedId vs businessKey） | 缓存 key 改为 EntityId（全局唯一） |
| 7 | DataGrid `RemoveAutoGeneratedColumns` NRE 崩溃 | `SwitchTabItemsSource` try-catch + 安全重置 |

### 架构
- 四层结构：交互层 → 编排层(GDH) → 索引层(ReferenceIndex) → 解析层(ReferenceParser+Pattern)
- Index Build 异步：`Task.Run` 后台线程，不阻塞 UI
- 索引键：`(sourceEntityId, propertyName, rawId)` context-aware
- 查找优先级：context-aware → 同模组主键 → MergedId → 全局主键

---

## Stage 18 — AttackMode 可视化深化 + 数据浏览器引用索引 (v0.20.0-dev) | 2026-06-10

### AttackMode Detail 卡片式重设计

Hero Header（Image + Badge 行 + Name + WieldPhrase 引文 + Notes）：

| 元素 | 设计 |
|------|------|
| 图片区 | 128x128 圆角缩略图，无图片时用 `SymbolIcon`（`Flash` 近战 / `Target` 远程）|
| ID 徽章 | 蓝底白字 `ID: N` |
| 类型徽章 | 绿色近战 / 红色远程，带射程：`Melee (1 tile)` / `Ranged (80 tiles)` |
| 名称 | 18px Bold，自动换行 |
| WieldPhrase | 斜体引文格式，120 字符截断，灰色 `#666` |
| Notes | 12px，灰色 |

Combat Fieldset — 基于 nType 的图标标题（`SymbolIcon` + "Melee Combat" / "Ranged Combat"）+ 进度条：

| 属性 | 条形颜色 | 缩放 |
|------|---------|------|
| Range | `#607D8B` 灰蓝 | max(Range, 10) |
| Cut | `#E53935` 红 | max(Cut, Blunt, 2.0) |
| Blunt | `#FB8C00` 橙 | max(Cut, Blunt, 2.0) |
| Morale | 绿（>25%）/ 红（<25%）/ 灰（=25% 基础值） | Morale 值直接映射 |

穿透：●○ 圆点 + 等级（仅 >0 时显示）
音效：紫色可点击徽章 `▶ cueName`，ToolTip 说明 "embedded in game SWF"
Transfer 标记：绿色文字行

**Attacker Conditions** — 解析 `{id}x{mult}` 模式引用，同 mod 实体优先

**Attack Phrases** — 按半角/全角逗号切分，蓝色 WrapPanel 徽章，显示计数

**Ammo（Charge Profiles）** — 解析 ChargeProfile 引用，显示计数标题 + 可点击徽章（Ctrl+Click 导航）

### 统一引用解析 — `LookupRef`

**问题**：可视化器用 `GetDedupedInt<T>()` 自己建字典 → 按 `ModId` 去重，DataGrid 用 `ReferenceIndex.Lookup()` 上下文感知解析。两套路径不一致，引用解析频繁出错。

**根本解决方案**：
- `ReferenceResolver.LookupRef<T>(sourceEntity, propertyName, rawId)` — 唯一入口
  - 优先：`ReferenceIndex.Lookup(sourceEid, propName, targetType, rawId)` — 与 DataGrid 完全相同
  - 回退：`EntityModNames` 同 mod 优先 — 与 `ReferenceIndex.ResolveTargetEntityId` 同逻辑
  - 回退：最高 `ModId`
- `NavigateToByKeyFor<T>(key, sourceEntity)` — 导航入口，改用 `Index.LookupGlobal`
- 可视化器不再建字典，每 ID 逐走 `LookupRef`
- `GenericDataGridHelper.ActiveMergeStore` — 新增公开属性供 `LookupRef` 访问索引

### 数据浏览器引用索引

**架构**：复用与合并视图相同的 `EntityMergeStore` → `ReferenceIndex` 管道。

| 组件 | 职责 |
|------|------|
| `EntityBrowserDocument.RebuildBrowserIndexAsync()` | 为全部 24 类型创建 `EntityMergeStore`，填充 `ReferenceLookups` + `EntityModNames`，`Index.BuildAsync()` 构建索引，调用 `SetActiveStores` |
| `EntityBrowserDocument.InvalidateIndex()` | 在 mod/profile 变更后设置 `_indexBuilt = false` |
| `DataBrowserViewModel` | 监听 `SaveProfileMessage` / `RefreshModMessage` / `InitModMessage` / `CellEditedMessage` 自动失效 |
| 侧边栏 "Rebuild Index" 按钮 | `Symbol.ArrowSync` 图标，绑定到 `RebuildIndexCommand` |

**调用时机**：
- 首次 `EntityBrowserDocument` 打开时惰性构建
- 侧边栏按钮手动触发重建 → 全量重建 Store + Index
- Mod/Profile 变更 → 自动失效 → 下次浏览器打开时重建

### AttackPhrases 分隔符

从仅支持半角逗号 `Split(',')` 改为 `Split(',', '，')`，正确处理中文标点。

### nType 图标：文本 → FluentIcons

所有 nType 图标（detail hero 占位图、combat 标题）从文字字符替换为 `SymbolIcon`：
- 近战 `Symbol.Flash`
- 远程 `Symbol.Target`

### 数据浏览器 ListBox 搜索过滤

`DomainBrowserView.axaml` — 列表上方新增 `Watermark="Filter..."` 的 TextBox，按 `DisplayName` 和 `EntityId` 大小写不敏感匹配。`_allEntities` 保留完整后备列表，`ApplyFilter()` 重建 `Entities`。

### 设计原则

- **可视化器不直接访问数据库**：通过 `ReferenceResolver.LookupRef<T>()` 解析，优先走 `ReferenceIndex.Lookup`（与 DataGrid 同源），回退 `EntityModNames`
- **单一路径引用解析**：`ReferenceIndex` 是唯一真实数据源。可视化器和 DataGrid 走同一套解析逻辑，杜绝双路径不一致
- **上下文感知解析**：无命名空间前缀的引用优先在同一 mod 内解析；带命名空间前缀的引用在指定命名空间内查找
- **索引已缓存**：活跃 merge store 成为所有引用的真实数据源；无数据重复

---

## M6 — 架构债清零 (v0.24.0-dev) | 2026-07-24

### 背景
M0-M5 完成后，22 条 spec 规则中 20 条完全落地，剩余 2 条架构债：
- N03: `ModGameDataTabsView` 持有 CommandHistory/WAL/脏状态等业务逻辑
- GDH `ConfigureColumn` 中的 Ctrl+Click/RMB inline handler 代码（~160 行）

### M6.1: GDH ConfigureColumn 事件处理器模板化

**新建文件**：

| 文件 | 行数 | 说明 |
|------|:--:|------|
| `Services/IDataGridCellInteractionService.cs` | 49 | 单元格交互接口 |
| `Services/DataGridCellInteractionService.cs` | 239 | 注入单例，构造注入 `IDataGridNavigationService` + `INavigationRouter` + `IReferenceResolver` + `DataGridInteractionState` |

**方法**：
- `AttachSingleRefHandlers` — 单值引用列 Ctrl+Hover/Ctrl+Click/RMB/ContextMenu 抑制
- `AttachMultiRefSegmentHandlers` — 多值引用段 Ctrl+Hover/ContextMenu 抑制
- `AttachMultiRefCellHandler` — 多值引用列 cell-wide Ctrl+Click/RMB
- `FormatSegmentDisplay` — 多值段 Subject 名称解析格式化

**GDH 变更**：`GenericDataGridHelper.cs` 979 → 819 行 (-160 行)。
`FormatSegmentDisplay` 保留内联实现（避免跨服务间接调用影响展示性能）。

**DI 注册**：
```csharp
services.AddSingleton<Services.IDataGridCellInteractionService, Services.DataGridCellInteractionService>();
```
V6 访问器：`App.DataGridCellInteraction`

### M6.2: ModGameDataTabsView ViewModel

**新建文件**：

| 文件 | 行数 | 说明 |
|------|:--:|------|
| `ViewModels/MainContent/ModGameDataTabsViewModel.cs` | 286 | CommandHistory/WAL/Dirty state 所有者 |

**迁移内容**：
- `CommandHistory` 实例所有权 → ViewModel
- WAL `OnCommandPersist` 回调 → ViewModel（持久化 persist + 周期性快照）
- `_isDirty` / `SetDirty()` 脏状态 → ViewModel
- Auto-save timer + `SaveRequestedMessage` handler → ViewModel
- `_persistSequence` / `_commandsSinceSnapshot` → ViewModel 属性

**View 变更**：`ModGameDataTabsView.axaml.cs` 1574 → 1511 行 (-63 行)。
View 保留 `_dirtyTabs` UI 列表管理和 `TabSnapshotCache` 逻辑。
View 通过属性委托桥接 `_commandHistory` / `_isDirty` / `_persistSequence` → ViewModel。

**N03 消除** ✅：CommandHistory/WAL 不再由 View 直接持有。

### M6.3: App.ServiceProvider 归零

剩余 2 处直访 (`CreateModDialog.axaml.cs:15` / `RenameImagePairDialog.axaml.cs:15`) 为 AXAML Designer 无参构造兜底，标注 **框架豁免**。
所有运行时调用路径使用 `Create(IServiceProvider)` 工厂方法。

### M6.4: 结果

| 指标 | M6 前 | M6 后 |
|------|:--:|:--:|
| 编译 Error | 0 | **0** |
| 编译 Warning | 58 | 62 |
| xUnit 测试 | 8/8 | **8/8** |
| Spec 完全落地 | 20/22 (77%) | **22/22 (100%)** |
| App.ServiceProvider | 2 | 2 (框架豁免) |
| 架构债 | 2 | **0** |

### 影响文件

| 文件 | 关键改动 |
|------|---------|
| `Services/IDataGridCellInteractionService.cs` | **新建** — 单元格交互接口 |
| `Services/DataGridCellInteractionService.cs` | **新建** — 注入式交互处理器 |
| `ViewModels/MainContent/ModGameDataTabsViewModel.cs` | **新建** — CommandHistory/WAL/Dirty 所有者 |
| `App.axaml.cs` | 新增 `DataGridCellInteraction` V6 访问器 + DI 注册 + 初始化 |
| `Helper/GenericDataGridHelper.cs` | 979→819 行；`FormatSegmentDisplay` 保留内联；Ctrl+Click/Ctrl+Hover/RMB handler → service 委托 |
| `Views/UserControls/ModGameDataTabsView.axaml.cs` | 1574→1511 行；`_commandHistory`/`_isDirty`/`_persistSequence` → ViewModel 属性委托；`OnCommandPersist`/`CheckPeriodicSnapshot`/`ExtractModIdFromCommand` → ViewModel；auto-save timer → ViewModel |
| `Views/Dialog/CreateModDialog.axaml.cs` | 无参构造添加框架豁免注释 |
| `Views/Dialog/RenameImagePairDialog.axaml.cs` | 无参构造添加框架豁免注释 |

---

## M11 ImageTools Plugin 迁移完成 (v0.28.0-dev) | 2026-07-29

### 插件化迁移

M11 将 ImageTools 功能（ImageEditor + ModImages + ImagePreview）从单体 App 拆分为独立 Plugin 项目。至此 **3 个 Plugin 全部迁移完成**，App = 纯 Shell。

| 阶段 | 内容 |
|------|------|
| Phase 1 | Plugin 基类 ImageToolDocumentBase + ImageToolObservableObject |
| Phase 2 | ImageEditor VM/Views/Helpers → Plugin (5 文件) |
| Phase 3 | ModImagesDocument + DropHandler → Plugin + IModImageListService 桥接 |
| Phase 4 | ImagePreview VM/Views → Plugin + IImageSearchService |
| Phase 5 | App 侧清理：删除 15 旧文件、更新 DI/DataTemplates/DWVM/RightPanelView |
| Phase 6 | ImageTools.Tests 4/4 |

### 架构指标

- App 0 Plugin 代码残留（纯 Shell ✅）
- Plugin 0 对 App csproj 引用 (R18 ✅)
- 14 src + 8 test = 22 项目, 0 Error, 34/34 测试全部通过

### 测试轮

- 第18轮: M11 ImageTools Plugin 完整迁移 (test_round18_summary.md)

---

## M10 EntityEditor Plugin 迁移完成 (v0.27.0-dev) | 2026-07-29

### 插件化迁移

M10 将 EntityEditor 功能（25 Visualizer + Editor Views/VMs + VisHelperService）从单体 App 拆分为独立 Plugin 项目。

| Phase | 内容 |
|:-----:|------|
| 0 | 共享契约 IEntityVisualizer → UI.Common |
| 1 | Plugin 骨架 |
| 2 | VisHelper → VisHelperService DI 单例 |
| 3 | RefNode Plugin 副本 |
| 4 | 25 Visualizer → Plugin |
| 5 | Editor Views/VMs 迁移 (17 文件) |
| 6 | DI 简化 + App 清理 + R17 解除 |
| 7 | DocumentWorkspaceViewModel 解耦 (Factory 模式) |
| 8 | EntityEditor.Tests 9/9 |

### 架构指标

- R17 违规彻底解除: EntityEditor → DataViewer csproj 引用 = 0
- 旧 App VisHelper.cs (864行) + RefNode.cs (158行) 已删除
- 13 src + 7 test = 20 项目

### 测试轮

- 第16轮: Editor Views/VMs 迁移 (test_round16_summary.md)
- 第17轮: DI 简化 + R17 解除 + Tests (test_round17_summary.md)
