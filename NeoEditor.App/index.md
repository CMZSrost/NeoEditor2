# NeoEditor 文档索引

> 项目文档总入口。`spec/` 放**决策（规则）**，`Docs/` 放**文档（说明/设计/历史）**。
> 项目背景与开发约定见根 [CLAUDE.md](../CLAUDE.md)（工作区指令见 [AGENTS.md](../AGENTS.md)）。

---

## spec/ — 决策（最小规则形式，是开发的硬约束）

一文件一规则，增量维护（只增不改语义）。登记表见 [spec/README.md](spec/README.md)。

| 文件 | 含义 | 状态 |
|------|------|------|
| [spec/README.md](spec/README.md) | 规则登记表（方向 D / 基石 R / 禁止 N 全列表） | ✅ |
| [spec/_template.md](spec/_template.md) | 新增规则模板（六项字段） | — |
| [spec/D01](spec/D01-core-plugin-architecture.md) | **根本架构方向** — Core/Plugin 边界、插件化依据 | ✅ |
| [spec/D02](spec/D02-dynamic-dock-layout.md) | **动态 Dock 布局** — Tool/Document/Service 分类 + IToolPlugin 动态构建 | ✅ |
| [spec/D03](spec/D03-paratranz-integration.md) | **ParaTranz 翻译平台集成** — 数据转换 / 同步工作流 / UI 设计 | ⏳ M1-M4 已完成（M5 可选） |
| [spec/R00](spec/R00-spec-maintenance.md) | spec 维护元规则 | ✅ |
| **R01-R16** | 基石规则（DO）：状态所有者/单 Session/注入/分层/同源/保存/选中/交互等 | ✅ |
| **R17-R22** | Plugin 规则：互不引用/依赖范围/跨 Plugin 通信/DI/独立测试/集成测试 | ✅ |
| **R23-R25** | Plugin 分类标记/统一写路径/跨 Plugin 扩展点 | ✅ |
| **R26-R28** | 保存导出双 Repository（v2）/ 图片双视图 / AI-MCP 配置 | ✅ |
| **N01-N06** | 禁止规则（DON'T）：禁静态状态/禁 .Instance/禁 View 逻辑等 | ✅ |
| [spec/open-questions.md](spec/open-questions.md) | 暂无未决项 | ✅ |

> 规则与文档冲突时，**以 spec/ 规则为准**。方向决策 (D) 高于基石规则 (R) 和禁止规则 (N)。

---

## Docs/ — 文档（说明、设计、历史）

### 当前计划（进行中）

| 文件 | 含义 | 状态 |
|------|------|------|
| [Docs/42-webview-ruffle-preview-plan.md](Docs/42-webview-ruffle-preview-plan.md) | **WebView + Ruffle Web 内置预览 + NeoScavengerPlayer 独立播放器**（取代 Docs/40 ruffle.exe 运行器） | ✅ P0-P5 全部完成（v2.44） |
| [Docs/40-ruffle-game-runner-plan.md](Docs/40-ruffle-game-runner-plan.md) | Ruffle 游戏运行器（外部 ruffle.exe 进程） | 🗑 已废弃（2026-08-05，被 Docs/42 取代删除） |

### 已完成里程碑

| 文件 | 含义 | 状态 |
|------|------|------|
| [Docs/41-save-workflow-onboarding-plan.md](Docs/41-save-workflow-onboarding-plan.md) | **保存工作流收敛 + 非侵入式新手引导**（自动保存事件驱动化/黄绿高亮=未导出/单保存按钮/Ctrl+Shift+S/三步空状态卡片/一次性提示/字段 ? 图标） | ✅ 2026-08-03 |
| [Docs/39-image-editor-workstation-refactor-plan.md](Docs/39-image-editor-workstation-refactor-plan.md) | **图像编辑工作站重构**（创建/编辑双 Document，瘦身解耦 · 统一管线 · 提升可测试性） | ✅ 2026-08-02 |
| [Docs/38-full-field-reference.md](Docs/38-full-field-reference.md) | **全字段参考手册（整合版）**——24 表全部字段含义（非引用列实测值域+语义，引用列汇总 37） | ✅ 2026-08-02 |
| [Docs/35-tabstrip-listbox-filter-templates-plan.md](Docs/35-tabstrip-listbox-filter-templates-plan.md) | TabStrip → ListBox + ProDataGrid 内置 Filter 模板集成 | ✅ 完成 2026-08-01 |
| [Docs/30-post-m12-development-plan.md](Docs/30-post-m12-development-plan.md) | **M13+ 领域驱动服务架构开发计划**（Phase 1-8 + Agent A1-A4 + 像素 G1-G3） | ✅ 全部完成 |
| [Docs/31-prodatagrid-migration-plan.md](Docs/31-prodatagrid-migration-plan.md) | ProDataGrid 迁移计划（Avalonia DataGrid → ProDataGrid 12.0.4） | ✅ 完成 |
| [Docs/32-agent-orchestration-plan.md](Docs/32-agent-orchestration-plan.md) | Agent 编排增强计划（系统提示词 + RAG + MCP + Streaming） | ✅ A1-A4 完成 |
| [Docs/33-image-generation-plan.md](Docs/33-image-generation-plan.md) | 像素风格图像生成计划（XML → 像素图） | ✅ G1-G3 完成 |
| [Docs/34-prodatagrid-column-filter-plan.md](Docs/34-prodatagrid-column-filter-plan.md) | ProDataGrid 列过滤器实现计划（F1-F4） | ✅ 完成 |
| [Docs/28-plugin-architecture-migration.md](Docs/28-plugin-architecture-migration.md) | **插件化架构迁移计划**（M0-M12，模块划分/依赖/迁移步骤） | ✅ 完成 |

### 架构设计

| 文件 | 含义 |
|------|------|
| [Docs/25-architecture-decisions.md](Docs/25-architecture-decisions.md) | 架构决策详解 R01-R16/N01-N06 + 单向分层 + UI 原型（spec 规则的展开说明） |
| [Docs/26-refactor-roadmap.md](Docs/26-refactor-roadmap.md) | 重构路线图 M0-M4，竖切先打穿再横铺 |
| [Docs/23-architecture-redesign-proposal.md](Docs/23-architecture-redesign-proposal.md) | 四区域工作区架构提案（页面/CRUD/Document·Tool 体系） |
| [Docs/24-workflow-specification.md](Docs/24-workflow-specification.md) | 用户工作流规格（页面切换/Peek/索引/CRUD 流程） |

### 测试与合规

| 文件 | 含义 |
|------|------|
| [Docs/27-compliance-test-checklist.md](Docs/27-compliance-test-checklist.md) | 合规性测试检查清单 |
| [Docs/testround/](testround/) | 测试轮次记录（test_round01~33） |

### 参考资料

| 文件 | 含义 |
|------|------|
| [Docs/37-reference-column-semantics.md](Docs/37-reference-column-semantics.md) | **引用列字段语义参考**（值级：集合/实体/装饰三部分模型） |
| [Docs/38-full-field-reference.md](Docs/38-full-field-reference.md) | **全字段参考手册（整合版）**（24 表全部字段含义，实测值域） |
| [Docs/20-data-class-field-reference.md](Docs/20-data-class-field-reference.md) | 游戏数据类字段参考（25 个 Entity 类型） |
| [Docs/14-reference-resolution-system.md](Docs/14-reference-resolution-system.md) | 引用解析系统说明（IReferenceResolver / ReferenceList / 7 Format 类） |
| [Docs/21-entity-detail-ui-design-guide.md](Docs/21-entity-detail-ui-design-guide.md) | 实体详情 UI 设计指南 |
| [Docs/22-data-browser-ui-improvements.md](Docs/22-data-browser-ui-improvements.md) | 数据浏览器 UI 改进 |
| [Docs/11-nested-dock-control-fix.md](Docs/11-nested-dock-control-fix.md) | 嵌套 Dock 控件修复记录 |

### 外部参考

| 路径 | 含义 |
|------|------|
| [Docs/third-party/prodatagrid/](Docs/third-party/prodatagrid/) | **ProDataGrid 外部文档镜像**（API / articles / filtering-model-end-to-end / styling-themes 等） |
| `C:\Users\Cromzst\RiderProjects\ProDataGrid` | **ProDataGrid 源码仓库** — 主题模板：`src/Avalonia.Controls.DataGrid/Themes/Generic.xaml`；Sample：`src/DataGridSample/`；测试：`src/Avalonia.Controls.DataGrid.UnitTests/` |

### 历史

| 文件 | 含义 |
|------|------|
| [Docs/CHANGELOG.md](Docs/CHANGELOG.md) | 变更历史 |

---

## 阅读顺序建议

1. **先读** [spec/README.md](spec/README.md) — 知道硬约束（R/N 规则全表，R00-R28 + D01-D03 + N01-N06 全部落地）
2. **再读** [Docs/25](Docs/25-architecture-decisions.md) + [Docs/26](Docs/26-refactor-roadmap.md) — 理解架构决策与重构路线
3. **当前** [Docs/42](Docs/42-webview-ruffle-preview-plan.md) — WebView 内置预览 + NeoScavengerPlayer 播放器（v2.44，P0-P5 完成）⭐
4. **进行中** [spec/D03](spec/D03-paratranz-integration.md)（M1-M4 已完成，M5 可选）+ [Docs/42](Docs/42-webview-ruffle-preview-plan.md)（播放器后续打磨）— 下一个任务
5. **按需深入**：
   - ProDataGrid → [Docs/31](Docs/31-prodatagrid-migration-plan.md) + [Docs/34](Docs/34-prodatagrid-column-filter-plan.md) + [外部文档镜像](Docs/third-party/prodatagrid/)
   - Agent/AI → [Docs/32](Docs/32-agent-orchestration-plan.md)
   - 像素图像 → [Docs/33](Docs/33-image-generation-plan.md) + [Docs/39](Docs/39-image-editor-workstation-refactor-plan.md)
   - 插件拆分 → [Docs/28](Docs/28-plugin-architecture-migration.md)
   - 工作流 → [Docs/23](Docs/23-architecture-redesign-proposal.md) + [Docs/24](Docs/24-workflow-specification.md) + [Docs/41](Docs/41-save-workflow-onboarding-plan.md)
