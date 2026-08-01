# NeoEditor 重构路线图

> v1.1 · 2026-06-30 · 纯结构重构
> 配套: [25-architecture-decisions.md](25-architecture-decisions.md) · 规则: [../spec/README.md](../spec/README.md)

---

## 〇、总策略

**一条垂直切片先打穿，再横向铺开**——不按 UI 区域逐块改，先用一种实体（ItemType）
打通完整链路验证分层，再机械复制。

```
M0 内核   建 IWorkspaceSession + 删静态层          ← 地基，必须先做对
M1 竖切   ItemType 一条链路打通，验证 R04/R05/R06/R12/R15
M2 拆分   按已验证分层拆 EntityVisualizers.cs
M3 横铺   其余 24 实体类型套用 M1 模式
M4 全貌   正/反向索引、覆盖链、Profile 全貌
```

**M0+M1 是地基，立住后 M2/M3 是机械复制，M4 是叠加。**

---

## M0 — 内核：状态唯一所有者

> 对应 R01 / R02 / R03 / N01 / N02。本阶段结束**直接删除全量迁移**旧静态层（N01 / N02）。

| 步骤 | 内容 | 验收 |
|------|------|------|
| M0.1 | 定义 `IWorkspaceSession`（Store / ForwardIndex / ReverseIndex / DirtyEntities / OpenAsync / CloseAsync），DI scoped 注册 | 编译通过 |
| M0.2 | `IReferenceResolver` 注册进 DI，内部依赖 `IWorkspaceSession`；删除 `static Instance`（N02） | 接口被注入，0 处 `.Instance` |
| M0.3 | 迁移 141 处 `ReferenceResolver.Instance` → 注入引用（R03） | grep 确认 0 处 |
| M0.4 | 迁移 `ActiveMergeStore` / `BrowserStore` / `SetActiveStores` → `IWorkspaceSession.Store`（R01 / N01） | 0 处静态 store |
| M0.5 | `BrowserIndexService` 全静态 → 实例服务，索引并入 Session（R01 / N01） | 不在静态层 |
| M0.6 | 收敛 `App.ServiceProvider` ~90 处反向抓取 → 构造注入（N01） | 仅 composition root 使用 |
| M0.7 | 冻结消息清单：收敛 `EntitySelectedMessage` 到 `ISelectionService`；删死消息（R05 / N04 / R12） | 清单落档，0 处死消息 |

**退出条件**：全量编译通过；grep 确认无 `ReferenceResolver.Instance`、无静态
`ActiveMergeStore`、`App.ServiceProvider` 仅在 composition root；循环依赖消除。

---

## M1 — 垂直切片：ItemType 打通

> 对应 R04 / R05 / R06 / R08 / R11 / R12 / R15。用一种实体验证分层规则成立。

| 步骤 | 内容 | 规则 |
|------|------|------|
| M1.1 | `ISelectionService` 接入：Bottom 选中行高亮（不改当前实体）；双击/Ctrl+LMB 打开 Center 标签页 | R12 / R15 |
| M1.2 | Center 打开 EntityEditorDocument 双 Tab（Visual + XML），绑定同源实例 | R06 |
| M1.3 | Left KV 跟 Center 焦点切到该实例；编辑 → `INotifyPropertyChanged` → 四区域联动 | R06 / R08 |
| M1.4 | ItemType 引用（aAttacks / properties）走 `RefNode` 工厂 + 注入解析器；Ctrl+LMB Navigate / Ctrl+RMB Peek | R03 / R04 / R15 |
| M1.5 | 文档独立 Save → DB → XML 导出 → 清脏；切 Profile 有脏时弹拦截对话框 | R09 / R11 |

**退出条件**：ItemType 单类型四区域同源联动；Navigate/Peek 职责分离；Save 往返全走注入
服务；DataTable 交互符合 R15 矩阵；无静态依赖。此切片即后续所有实体的模板。

---

## M2 — 拆分 EntityVisualizers.cs

> 对应 R04 / R13 / N03。8761 行 → 模块化。

| 步骤 | 内容 | 规则 |
|------|------|------|
| M2.1 | `VisHelper`（file static，~813 行）提为独立文件，可见性改为 `internal static` | R13 |
| M2.2 | 25 个 `*EntityVisualizer` 各拆一文件（registry 显式注册无需改） | R04 |
| M2.3 | 工厂内 66 处 `NavigateTo` 收敛到注入的 `IReferenceResolver` / `INavigationRouter` | R03 / N02 / N03 |
| M2.4 | （可选）去重 `BuildHeroHeader`(24×) / `BuildReverseRefsPanel`(17×) 等重复私有方法 | — |

**退出条件**：单文件 ≤ ~1500 行；导航逻辑全在工厂；编译+运行可视化无回归。

---

## M3 — 横向铺开其余实体

> 复用 M1 模板，对应 R06。

按 M1 模式为其余 24 个实体类型接入四区域同源 + 引用工厂 + DataTable 交互矩阵（R15）。
机械复制为主，逐类型验收：选中→编辑→联动→Save。

建议顺序（按引用复杂度递增）：
简单（GameVar / Headline / ItemProp）→ 中（Recipe / Creature / Condition）→ 复杂（Encounter / AttackMode / Map）

---

## M4 — Profile 全貌

> 对应 R10（索引手动刷新）。

| 步骤 | 内容 | 规则 |
|------|------|------|
| M4.1 | Ref Index / Reverse Index 手动刷新（RefreshCommand，初始空；编辑后显示「已过期」角标） | R10 |
| M4.2 | OverlayChain 覆盖链展示（vanilla → mod → 当前） | — |
| M4.3 | Profile 全貌：全体数据 + 覆盖关系 + Mod 归属 | — |

---

## 风险与回滚

| 风险 | 缓解 | 规则 |
|------|------|------|
| M0 改动面大（141+90 处） | 按 M0.1→M0.7 顺序，每步保持编译通过，不跳步 | N01 / N02 |
| 迁移中行为漂移 | 本轮纯结构重构，不动功能；以「编译+可视化无回归」为准 | — |
| DataTable 交互回归 | M1.1 验收对照 R15 矩阵全条目 | R15 |
| 4 个功能 bug | 不在本轮范围；R01 落地后根因消失，重构稳定后单独验证 | R01 |

---

## 里程碑依赖

```
M0 ──→ M1 ──→ M3
内核   竖切   横铺
       │
       └──→ M2（M1 验证分层后可与 M3 并行）
                 │
                 └──→ M4
                      全貌
```

M0 是唯一硬前置；M2 在 M1 验证分层后可与 M3 部分并行；M4 在 M3 完成后叠加。
