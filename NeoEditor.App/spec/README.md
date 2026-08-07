# spec — 决策规则登记表

> 一文件一规则，增量维护。规则模板见 [_template.md](_template.md)，维护方式见 [R00](R00-spec-maintenance.md)。
> 与文档冲突时**以本目录规则为准**。文档说明见根 [../index.md](../index.md)。

## 方向决策（DIRECTION）

| 规则 | 标题 | 启用 | 来源 |
|------|------|:--:|------|
| [D01](D01-core-plugin-architecture.md) | Core / Plugin 架构方向 — 项目本质、边界定义、插件化依据 | ✅ | 用户决策 2026-07-24 |
| [D02](D02-dynamic-dock-layout.md) | 动态 Dock 布局 — Tool/Document/Service Plugin 分类 + IToolPlugin 动态构建（1:1） | ✅ | 用户决策 2026-08-01 |
| [D03](D03-paratranz-integration.md) | ParaTranz 集成 — 数据转换 / 同步工作流 / UI 设计（v1.6，M1-M4 已完成，M5 可选） | ✅ | 用户决策 2026-08-05 |
| [D04](D04-itemtype-visualization-design.md) | ItemType 可视化设计 — 全字段语义 / 设计原因与目的 / 心理模型布局（其余实体类型的可视化设计模板） | ✅ | 用户决策 2026-08-08 |
| [D05](D05-creature-visualization-design.md) | Creature 可视化设计 — 13 字段全覆盖 / 战斗三层 / 出场状态概率 / 战利品双池 / 遭遇链（实现中） | ✅ | 用户决策 2026-08-08 |
| [D06](D06-encounter-storybranch-design.md) | Encounter 剧情分支重构 — 节点单组件（图片/标题/概率）+ tooltip 信息卡 + Mermaid 同源对齐（v1.1） | ✅ | 用户决策 2026-08-08 | | Creature 可视化设计 — 13 字段全覆盖 / 战斗三层 / 出场状态概率 / 战利品双池 / 遭遇链（实现中） | ✅ | 用户决策 2026-08-08 |

> **D01 是项目根本架构决策**，高于所有 R/N 规则。R17-R22 是其执行细则。
> **D02 是 Dock 布局的根本决策**，取代手写 XAML Tool 元素。Phase 9E 实现。

## 基石规则（DO）

| 规则 | 标题 | 启用 | 来源 |
|------|------|:--:|------|
| [R00](R00-spec-maintenance.md) | spec 的维护规则（元规则） | ✅ | 用户指示 |
| [R01](R01-state-single-owner.md) | 状态唯一所有者 IWorkspaceSession | ✅ | D1 |
| [R02](R02-single-active-session.md) | 单活跃 Session | ✅ | D2 |
| [R03](R03-reference-resolver-injected.md) | 引用解析只走注入的 IReferenceResolver | ✅ | D3 |
| [R04](R04-view-assembles-only.md) | View 只组装控件 | ✅ | D4 |
| [R05](R05-messages-ui-only.md) | 消息只做跨区域 UI 联动 | ✅ | D5 |
| [R06](R06-same-entity-instance.md) | 四区域数据同源 | ✅ | D6 |
| [R07](R07-one-way-layering.md) | 单向分层 | ✅ | D7 |
| [R08](R08-edit-entry-points.md) | 编辑入口仅 KV 与 XML | ✅ | D6 衍生 |
| [R09](R09-session-dirty-guard.md) | 脏数据视觉指示：Sidebar + HomePage 提示未保存编辑 | ✅ | Q1 |
| [R10](R10-index-manual-refresh.md) | 索引手动刷新，编辑后标过期不自动重建 | ✅ | Q2 |
| [R11](R11-save-granularity.md) | 文档独立保存 + 工具栏「Save Session」全局保存 | ✅ | Q3 |
| [R12](R12-selection-service.md) | 选中由 ISelectionService 统一管理，以 Center 为主 | ✅ | Q4 |
| [R13](R13-vishelper-internal.md) | VisHelper 为 internal static 单一辅助类 | ✅ | Q5 |
| [R14](R14-folder-convention-layering.md) | 分层用文件夹+命名空间约定，不拆程序集 | ✅ | Q6 |
| [R15](R15-datatable-interaction.md) | DataTable 交互矩阵（单击/双击/Ctrl 导航/Peek） | ✅ | Q4b |
| [R16](R16-reference-namespace-resolution.md) | 引用解析 namespace 完整规范（三路穷举、两种 key、merge override） | ✅ | Q11 |
| [R17](R17-plugin-no-inter-reference.md) | Plugin 互不引用（.csproj 级隔离） | ✅ | §2 |
| [R18](R18-plugin-dependency-scope.md) | Plugin 只依赖 Core + Infra + UI.Common，不依赖 App 或其他 Plugin | ✅ | §2 |
| [R19](R19-cross-plugin-messaging.md) | 跨 Plugin 通信只走 IMessenger 事件 | ✅ | §3.2 |
| [R20](R20-di-composition-root.md) | DI 注册在 App Composition Root，Plugin 不自注册 | ✅ | §3.1 |
| [R21](R21-plugin-independent-tests.md) | 每个 Plugin 独立测试项目，只引用该 Plugin + Mock Core | ✅ | §5 |
| [R22](R22-integration-tests.md) | Integration.Tests 独立项目，覆盖跨 Plugin 核心链路 | ✅ | §5 |
| [R23](R23-plugin-classification.md) | Plugin 三分类标记（Workbench/Feature/Service） | ✅ | M13+ §1 |
| [R24](R24-host-service-data-path.md) | 所有数据修改必须经过 IHostService | ✅ Phase 1 落地 | M13+ §4 |
| [R25](R25-cross-plugin-extension-points.md) | 跨 Plugin 扩展走 HostService 事件/扩展点 | ✅ | M13+ §4.3 |
| [R26](R26-save-export-repository.md) | 保存/导出工作流 — **对称 Repository 契约（CRUD/双 diff/dirty/Save/Load 全对称，v2）** + DB/XML 双 Repository + Save/Export/Publish 三动作 + per-profile dirty session | ✅ | Phase 9B |
| [R27](R27-image-asset-dual-view.md) | ImageAssetManager 拆分为 Browser + Orchestration 双视图 | ✅ | Phase 9C |
| [R28](R28-ai-mcp-configuration.md) | AI/MCP 必须有 UI 配置界面和启动路径 | ✅ | Phase 9D |

## 禁止规则（DON'T）

| 规则 | 标题 | 启用 | 来源 |
|------|------|:--:|------|
| [N01](N01-no-static-state.md) | 禁止静态可变状态 | ✅ | D1/D8 |
| [N02](N02-no-reference-resolver-instance.md) | 禁止使用 ReferenceResolver.Instance | ✅ | D3 |
| [N03](N03-no-logic-in-view.md) | 禁止 View 写业务/导航逻辑 | ✅ | D4/D7 |
| [N04](N04-no-dead-messages.md) | 禁止死消息与多接收方歧义 | ✅ | D5 |
| [N05](N05-no-bottom-editing.md) | 禁止 Bottom DataTable 原地编辑 | ✅ | D6 |
| [N06](N06-no-dirname-as-namespace.md) | 禁止用目录名作为 EntityNamespaces 值 | ✅ | Q11d |

## 待决策

| 文件 | 内容 |
|------|------|
| [open-questions.md](open-questions.md) | 暂无未决项；Q1-Q11 已全部归档为 R09-R16, N06；R01-R28 全部落地；D01-D03 全部生效（D03 M1-M4 已完成） |
