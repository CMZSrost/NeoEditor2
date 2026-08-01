# 架构测试第8轮 — M9 接口提取 + ViewModel 迁移验收

> 日期：2026-07-26 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round7_summary.md](test_round7_summary.md) (M9 计划——本轮执行子集)
> 后续：[test_round9_summary.md](test_round9_summary.md) (Dirty State 统一 + DataViewer 功能验收)

## 本轮目标

M9 收束轮（test_round7）因 Views 迁移需要协调 AXAML 绑定重构，拆分为两轮执行。本轮是第一轮：**接口提取 + ViewModel 迁移 + App 集成**。

核心完成：

1. **服务接口提取到 Infra** — `INotificationService`、`ILocalizationService`、`IConfigService`、`IBrowserIndexService` 从 App 移至 Infra，解除 DataViewer → App 循环依赖
2. **AppConfig 迁移到 Core** — 基类从 `ViewModelBase` 改为 `ObservableRecipient`
3. **ViewModelBase / ViewServices 更新** — 全部改用 Infra 接口
4. **6 个 ViewModel 迁移到 DataViewer** — 含重命名、基类替换、构造注入
5. **App 集成 DataViewer Plugin** — ProjectReference、Global Usings、DI 注册更新
6. **清理与废弃标记** — `GenericDataGridHelper` [Obsolete]、`ViewServices` DataViewer 属性 [Obsolete]、`ColumnVisibilityKeys` 去重删除

> **本轮完成后**：DataViewer 作为独立 Plugin 运行，App 通过 Infra 接口引用服务。Views 仍在 App 中（UI 功能不受影响），待下一轮迁移。

---

## 前置条件

- [x] `bash build.sh` 编译通过（12 项目，0 Error）
- [x] `dotnet test` 21/21 通过（6 个测试项目）
- [x] DataViewer.Tests 10/10 通过
- [x] `NeoEditor.Plugins.DataViewer` 独立编译（0 引用 App）
- [x] M9 第一阶段（Service + Converter 迁移）已在 test_round6 完成

---

## 实际完成 vs 原始计划

### 对照表

| # | test_round7 计划 | 本轮实际 | 说明 |
|---|-----------------|---------|------|
| 1 | App 引用 DataViewer | ✅ | ProjectReference + Global Usings |
| 2 | 删除 11 个已迁移文件 | ✅ | 6 ViewModel + 1 Infra 去重（Service/Converter 已在 M9P1 删除） |
| 3 | ~50 处 using 更新 | ✅ | 3 个 Global Usings 覆盖全 App |
| 4 | 10 个 View 迁移 | ⏭️ | 下一轮 — 需协调 AXAML 绑定 |
| 5 | 5 个 ViewModel 迁移 | ✅ | 实际迁移 6 个 |
| 6 | ViewServices 残余清理 | ✅ | 3 个 DataViewer 属性标 [Obsolete] |
| 7 | GenericDataGridHelper | ✅ | 类级 [Obsolete] |
| 8 | 接口提取 (额外) | ✅ | 4 个接口 → Infra，AppConfig → Core |

### 本轮改动清单

#### 新建文件（Infra — 服务接口）

| 文件 | 说明 |
|------|------|
| `NeoEditor.Infra/Services/INotificationService.cs` | 通知服务接口（4 方法：ShowSuccess/Error/Info/Warning） |
| `NeoEditor.Infra/Services/ILocalizationService.cs` | 本地化接口（indexer + SetCulture + INotifyPropertyChanged） |
| `NeoEditor.Infra/Services/IConfigService.cs` | 配置接口（Config / LoadAsync / SaveAsync） |
| `NeoEditor.Infra/Services/IBrowserIndexService.cs` | 浏览器索引接口（Index / IsBuilding / Invalidate / EnsureBuiltAsync / GlobalModNames） |

#### 新建文件（Core — 领域模型）

| 文件 | 说明 |
|------|------|
| `NeoEditor.Core/Model/AppConfig.cs` | 从 `App.ViewModels` 迁移，基类改为 `ObservableRecipient` |

#### 新建文件（DataViewer — ViewModels）

| 文件 | 来源 | 改动 |
|------|------|------|
| `ViewModels/ModDataToolViewModel.cs` | App | 基类 ViewModelBase → ObservableObject |
| `ViewModels/PeekPanelViewModel.cs` | App | 基类 → ObservableRecipient，Loc 改构造注入 ILocalizationService |
| `ViewModels/IndexTableViewModel.cs` | App | 基类 → ObservableRecipient，BrowserIndexService → IBrowserIndexService 注入 |
| `ViewModels/GameDataTypeTabItem.cs` | App | 仅命名空间更新 |
| `ViewModels/DataTableViewModel.cs` | App（改名） | ModGameDataTabsViewModel → DataTableViewModel，IConfigService 改注入 Infra 版 |
| `ViewModels/SearchResultViewModel.cs` | 新建（拆分） | 从 BottomToolsViewModel 提取搜索逻辑，独立 ViewModel |

#### 已删除文件（App）

| 文件 | 原因 |
|------|------|
| `ViewModels/AppConfig.cs` | 迁移到 Core，旧文件改为空转发类后删除 |
| `ViewModels/MainContent/ModDataToolViewModel.cs` | 迁移到 DataViewer |
| `ViewModels/MainContent/PeekPanelViewModel.cs` | 迁移到 DataViewer |
| `ViewModels/MainContent/IndexTableViewModel.cs` | 迁移到 DataViewer |
| `ViewModels/MainContent/GameDataTypeTabItem.cs` | 迁移到 DataViewer |
| `ViewModels/MainContent/ModGameDataTabsViewModel.cs` | 重命名为 DataTableViewModel，迁移到 DataViewer |
| `Infra/Helper/ColumnVisibilityKeys.cs` | 已迁移到 DataViewer.Services，删除 Infra 重复副本 |

#### 已修改文件（App）

| 文件 | 改动 |
|------|------|
| `NeoEditor.App.csproj` | 加 ProjectReference、3 个 Global Usings |
| `App.axaml.cs` | DI 注册改用 Infra 接口、字段类型更新 |
| `Helper/ViewServices.cs` | Loc → ILocalizationService；DataGridState/DataGridNavigationService/DataGridCellInteraction 标 [Obsolete] |
| `Helper/GenericDataGridHelper.cs` | 类级 [Obsolete] |
| `ViewModels/ViewModelBase.cs` | Loc/Notification 改用 Infra 接口 |
| `Services/ConfigService.cs` | 移除 IConfigService 接口定义，implement Infra 版 |
| `Services/NotificationService.cs` | 移除 INotificationService 接口定义，implement Infra 版 |
| `Services/LocalizationService.cs` | 加 `: ILocalizationService` |
| `Services/BrowserIndexService.cs` | 加 `: IBrowserIndexService` |
| `ViewModels/MainContent/DocumentWorkspaceViewModel.cs` | 构造调用改用 Infra 接口 |
| `ViewModels/MainContent/Documents.cs` | 属性类型修正 |
| `ViewModels/ExplorerPane/SettingsPaneViewModel.cs` | ColumnVisibilityKeys 去前缀 |
| `Views/UserControls/DocumentWorkspaceView.axaml` | 加 `dv:` xmlns，ModDataToolViewModel 引用更新 |
| `Views/UserControls/IndexTableView.axaml` | 加 `dv:` xmlns，IndexTableViewModel 引用更新 |
| `Views/UserControls/ModGameDataTabsView.axaml` | 加 `dv:` xmlns，GameDataTypeTabItem 引用更新 |
| `Views/UserControls/ModGameDataTabsView.axaml.cs` | ModGameDataTabsViewModel → DataTableViewModel (4 处) |
| `Views/UserControls/SearchableDataGrid.axaml.cs` | ColumnVisibilityKeys 去前缀 |
| `Views/UserControls/ModGameDataTabsView.Tab.cs` | ColumnVisibilityKeys 去前缀 |
| `Views/Dialog/*.axaml.cs` (12 文件) | LocalizationService → ILocalizationService |

---

## 验收清单

### 验收 A：编译与测试（自动化，3 项）

| 步骤 | 操作 | 预期 | 结果 |
|:--:|------|------|:--:|
| 1 | `bash build.sh` | 12 项目全部编译通过，0 Error | ⬜ |
| 2 | `dotnet test`（全部测试项目） | 21+ 测试通过，0 Failure | ⬜ |
| 3 | `dotnet build NeoEditor.Plugins.DataViewer` 单独编译 | 0 Error，确认 0 引用 App 程序集 | ⬜ |

### 验收 B：DataViewer 功能回归（14 项）

> 说明：Views 仍在 App 中，DataViewer 功能通过 App 中的旧 View 代码访问。本轮验证接口提取和 ViewModel 迁移未破坏现有功能。

| 步骤 | 操作 | 检查点 | 结果 |
|:--:|------|--------|:--:|
| 1 | 启动编辑器 | 无启动异常 / crash / DI 解析失败 | ⬜ |
| 2 | 打开 Profile → Browse Game Data | DataTable 正常打开，列头显示正确 | ⬜ |
| 3 | 表切换 | 切换 ItemType / Recipe / Creature 等，表格内容正确刷新 | ⬜ |
| 4 | 排序 | 点击列头排序，升序/降序切换正常 | ⬜ |
| 5 | 过滤 | Filter 输入框输入关键字，行过滤正常 | ⬜ |
| 6 | 搜索（Ctrl+F） | FindReplace 面板打开，搜索关键词，结果高亮 | ⬜ |
| 7 | 单击行 | 行高亮，Bottom 面板显示详情 | ⬜ |
| 8 | Ctrl+Click 引用字段 | DataTable 跳转到引用实体行 | ⬜ |
| 9 | Ctrl+RMB 引用字段 | Peek 面板弹出，显示目标实体信息 | ⬜ |
| 10 | 修改字段 → Ctrl+S | 保存成功，脏标记清除 | ⬜ |
| 11 | KV 编辑 | Bottom KeyValueEditor 字段编辑正常 | ⬜ |
| 12 | 合并视图 | Merge View 下拉切换，覆盖链 / tooltip 正确 | ⬜ |
| 13 | 列可见性 | Settings → Column Visibility 勾选 → DataGrid 列显隐生效 | ⬜ |
| 14 | Index 表 | 侧边栏 Index Tab 切换，正向/反向索引数据正确 | ⬜ |

### 验收 C：新增接口专项（4 项）

> 本轮新增了 ILocalizationService / INotificationService / IConfigService / IBrowserIndexService 接口，需要验证 DI 解析正确。

| 步骤 | 操作 | 检查点 | 结果 |
|:--:|------|--------|:--:|
| 1 | 切换语言 | 菜单 Language → English / 中文，UI 文本即时切换 | ⬜ |
| 2 | 触发通知 | 执行保存 / 导出 / 报错操作，Toast 通知正常弹出 | ⬜ |
| 3 | 修改 Settings | 修改 AutoSaveInterval / 主题 → 重启，配置保留 | ⬜ |
| 4 | 浏览器索引 | 打开 Profile 后 Index 表自动加载，Refresh 正常重建 | ⬜ |

### 验收 D：回归检查（4 项）

| 步骤 | 操作 | 检查点 | 结果 |
|:--:|------|--------|:--:|
| 1 | Settings 持久化 | 修改 GameRootDir / 语言 / 主题 → 重启编辑器 → 配置保留 | ⬜ |
| 2 | 脏数据一致性 | DataGrid 黄色高亮 ↔ Value Editor Alert ↔ Ctrl+S 状态三者一致 | ⬜ |
| 3 | `dotnet test` 全部 | 所有测试项目通过 | ⬜ |
| 4 | 启动无 Obsolete 异常 | 日志中无因 [Obsolete] 标记导致的运行时异常 | ⬜ |

---

## 结果汇总

| # | 验收项 | 结果 |
|---|--------|:--:|
| A1 | `bash build.sh` — 0 Error | ⬜ |
| A2 | `dotnet test` — 全部通过 | ⬜ |
| A3 | DataViewer 独立编译 — 0 引用 App | ⬜ |
| B1-B14 | DataViewer 功能回归（14 项） | ⬜ |
| C1-C4 | 新增接口专项（4 项） | ⬜ |
| D1-D4 | 回归检查（4 项） | ⬜ |

**自动化通过率**：0 / 3 | **手动功能通过率**：0 / 22 | **整体通过率**：0 / 25

---

## 已知风险点

| 风险 | 说明 | 严重度 |
|------|------|:--:|
| AppConfig 基类变更 | 从 `ViewModelBase` 改为 `ObservableRecipient`，部分 View 可能通过反射访问基类成员 | 低 |
| ILocalizationService PropertyChanged | `MainWindowViewModel` 订阅 Loc.PropertyChanged 的行为因接口实现改变可能受影响 | 低 |
| ColumnVisibilityKeys 去重 | 旧 Infra 副本已删除，依赖方全量指向 DataViewer 版本。需验证列可见性功能无回归 | 中 |
| BrowserIndex 接口精简 | `IBrowserIndexService` 仅暴露 5 个成员，App 中有 1 处通过 `ViewServices.BrowserIndex.GlobalModNames` 访问（已纳入接口） | 低 |
| GenericDataGridHelper [Obsolete] | 14 个 EntityVisualizer + ReferenceInspector 等仍有引用，产生编译警告（非错误） | 低 |

---

## 下一轮（test_round9）任务

1. 迁移 6 个 View 文件到 DataViewer（SearchResultsView / PeekPanelView / IndexTableView / FindReplacePanel / SearchableDataGrid / DataTableView）
2. 替换 View code-behind 中的 `ViewServices.XXX` 为构造注入
3. GenericDataGridHelper 调用方迁至 DataTableService / ColumnTemplateFactory
4. ViewServices 中 DataGridState / NavigationRouter / DataGridNavigationService / DataGridCellInteraction 的 [Obsolete] 引用清零
5. 15 项完整功能验收

---

## 附录：当前 DataViewer 插件结构

```
NeoEditor.Plugins.DataViewer/
├── NeoEditor.Plugins.DataViewer.csproj
├── DataViewerPlugin.cs
├── Services/
│   ├── DataTableService.cs
│   ├── ColumnTemplateFactory.cs
│   ├── InteractionHandler.cs
│   ├── NavigationRouter.cs
│   ├── DataGridInteractionState.cs
│   ├── DataGridNavigationService.cs
│   ├── DataGridCellInteractionService.cs
│   ├── IDataGridCellInteractionService.cs
│   └── ColumnVisibilityKeys.cs
├── Converters/
│   ├── FieldSourceConverter.cs
│   ├── FieldConflictBackgroundConverter.cs
│   ├── EntityMergedIdConverter.cs
│   ├── ModNameColumnConverter.cs
│   └── OverlayChainConverter.cs
├── ViewModels/                          ← 本轮新增
│   ├── ModDataToolViewModel.cs
│   ├── PeekPanelViewModel.cs
│   ├── IndexTableViewModel.cs
│   ├── GameDataTypeTabItem.cs
│   ├── DataTableViewModel.cs
│   └── SearchResultViewModel.cs
└── Views/                               ← 下一轮

Tests/NeoEditor.Plugins.DataViewer.Tests/
├── NeoEditor.Plugins.DataViewer.Tests.csproj
├── DataViewerPluginTests.cs             (3 tests)
└── Services/DataTableServiceTests.cs    (7 tests)
```

## 附录：Infra 新增接口

```
NeoEditor.Infra/Services/
├── INotificationService.cs      ← 从 App 提取
├── ILocalizationService.cs      ← 新建
├── IConfigService.cs            ← 从 App 提取
└── IBrowserIndexService.cs      ← 新建

NeoEditor.Core/Model/
└── AppConfig.cs                 ← 从 App 迁移
```
