# 架构测试第5轮 — M9 前清理：App.* 静态访问器 + 旧 Tests 修复

> 日期：2026-07-25 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round4_summary.md](test_round4_summary.md) (M8 收束 — R09 脏数据指示修复 ✅)
> 后续：[test_round6_summary.md](test_round6_summary.md) (M9 DataViewer Plugin 拆分)

## 本轮目标

M8 完成后，进入 M9（插件化拆分）前的代码清理。两项任务：

1. **删除 `App.*` V6 静态访问器** — 194 处引用分布在 46 个 CS 文件中（评估时估算 ~287，实际残留 ~194）
2. **旧 `NeoEditor.Tests` 修复** — 改为加 Core + Infra 引用，移除 CS0433/CS0436 NoWarn 压制

> 本轮纯清理，不改功能。架构合规 N01（禁止静态可变状态）的最终落地。

---

## 前置条件

- [x] `bash build.sh` 编译通过（0 Error）
- [x] `dotnet test` 19/19 通过（5 新 + 1 旧 = 6 个测试项目）

---

## 任务 1：删除 App.* 静态访问器

### 实施策略（实际执行）

采用 **ViewServices 服务定位器**过渡方案：

1. 在 `App.axaml.cs` `OnFrameworkInitializationCompleted` 中注册 `Resources["Services"] = _host.Services;`
2. 新建 `Helper/ViewServices.cs` — 静态服务定位器，提供 `Get<T>()` + 23 个常用服务便捷属性，内部从 `Application.Current.Resources["Services"]` 解析
3. View code-behind / GDH / non-DI 类用 `ViewServices.XXX` → sed 批量替换所有 `App.XXX`
4. ViewModel 层用构造函数注入 → 手动逐一改 6 个 ViewModel 加 `base(loc, notification, logger)`

### 做法对比

| 使用者类型 | 评估方案 | 实际方案 |
|------------|---------|---------|
| **View code-behind** | IServiceProvider 定位 | ViewServices 静态定位器，批量 sed 替换 |
| **ViewModel** | 加构造参数 | 加构造参数 + 调 base(loc, notification, logger) |
| **GDH (static)** | 方案 C（渐进式拆分） | ViewServices 过渡（M9 拆分时再改） |
| **Service** | 加构造参数 | ViewServices 或加构造参数 |
| **ViewModelBase** | 删 App.* 回退 | 删 App.* 回退，换 ViewServices 回退（防御性） |

### 特殊难点处理

#### GenericDataGridHelper（18 refs，静态类）

通过 `App.*` 访问 12 个服务。实际采用 **ViewServices 过渡**（sed 批量替换 `App.XXX` → `ViewServices.XXX`），M9 DataViewer Plugin 拆分时彻底重构。

#### ModGameDataTabsView（55 refs，4 个 partial 文件）

批量 sed + 手动补 `using NeoEditor.Helper;`。未改动构造函数结构。

#### ViewModelBase（4 refs）

- 移除 `App.Localizor` / `App.Notification` / `App.Logger` 回退
- **换为 `ViewServices.Loc` 防御性回退**（`Loc` getter: `_loc ?? ViewServices.Loc`）
- 避免遗漏的 ViewModel XAML 绑定 `{Binding Loc[...]}` 无声返回空字符串

---

## Bug 记录（启动测试阶段发现）

移除 `App.*` fallback 后，`ViewModelBase` 的 `Loc`/`Notification`/`Logger` 改为 `_field!`（null-forgiving），暴露了**未调用 `base(loc, notification, logger)` 的 ViewModel**。

| # | 崩溃点 | 根因 | 修复 | 严重度 |
|---|--------|------|------|:--:|
| B1 | `PeekPanelViewModel` 构造函数 `Loc["EP.EmptyHint"]` | 无参构造未设 `Loc` | 加 `LocalizationService` 参数 + `base(loc, null!, null)` | FTL |
| B2 | `DocumentWorkspaceViewModel` line 332 `Loc.PropertyChanged` | `IServiceProvider` 构造未设 `Loc` | `Loc = sp.GetRequiredService<LocalizationService>()` | FTL |
| B3 | `MainWindowViewModel` line 64 `Loc.CurrentCulture` | 同上 | 同上 | FTL |
| B4 | **欢迎页文本全部消失** | `HomePageViewModel` 未调 base()，XAML 绑定 `Loc[HomePageXxx]` 返回空 | `HomePageViewModel` 加 `base(loc, notification, null)` + `ViewModelBase.Loc` 加 `ViewServices.Loc` 防御性回退 | 无声 |

> **教训**：移除全局 fallback 后，所有 `IServiceProvider` 构造的 ViewModel 都需要显式初始化 `Loc`/`Notification`。B4 的防御性回退确保未来新增 ViewModel 即使遗漏也不会无声失败。

---

## 任务 2：旧 NeoEditor.Tests 修复

### 实际执行

| # | 操作 | 实际 |
|---|------|------|
| 1 | ProjectReference | 保留 App 引用（测试需要 ViewModels），**加** Core + Infra 引用 |
| 2 | NoWarn | 移除 `<NoWarn>CS0433;CS0436</NoWarn>` |
| 3 | Fake 类 | 未移动（`TestStubs.cs` 原有位置，`IWorkspaceSession` 从 Infra 的 `NeoEditor.Services` 命名空间解析） |
| 4 | CoreFlowTests 去 App.* | 移除 4 行 `App.*` 赋值，加 `ViewServices.TestServiceProvider = _sp;` |
| 5 | 编译 + 测试 | ✅ 0 Error，8/8 通过 |

**特殊处理**：`ViewServices.cs` 加 `TestServiceProvider` 属性——测试环境无 `Application.Current`，通过注入 `TestServiceProvider` 使 `ViewServices` 能在测试中解析服务。

---

## 结果汇总

| # | 验收项 | 结果 |
|---|--------|:--:|
| 1 | App.* 静态访问器 → 0 引用 | ✅ |
| 2 | GenericDataGridHelper 去 App.* 耦合 | ✅ (ViewServices 过渡) |
| 3 | ModGameDataTabsView 去 App.* 耦合 | ✅ (ViewServices 过渡) |
| 4 | ViewModel 层 0 处 App.* 引用 | ✅ |
| 5 | 编译 0 Error / 0 CS0436 / 0 NoWarn | ✅ |
| 6 | 19/19 单测全部通过 | ✅ |
| 7 | 旧 NeoEditor.Tests 编译 + 通过 (8/8) | ✅ |
| 8 | 编辑器启动 + 基本功能正常 | ✅ (发现 2 bug，已修复) |
| 9 | R09 脏数据指示回归 | ✅ (发现 1 bug，已修复) |

**代码通过率**：7 / 7 | **整体通过率**：9 / 9

---

## 验收中发现的 Bug（2026-07-25 修复）

### Bug A：设置页 GameRootDir 重启后重置

**现象**：设置页配置游戏根目录后重启编辑器，设置页显示的路径被重置。
但侧边栏资源管理器的路径正确 → 说明 config.json 持久化本身成功，问题在 UI 显示。

**根因**（两层）：

1. **`ViewModelBase.Loc` 被序列化到 config.json**：M8 ViewServices 重构给 `Loc`/`Notification`/`Logger` 加了 `set`，
导致 `AppConfig`（继承 `ViewModelBase`）序列化时把 `Loc`（`LocalizationService` 类型）也写进 config.json。
反序列化时 `LocalizationService` 没有无参构造函数，抛 `JsonSerializationException`，
在 `Dispatcher.UIThread.Invoke(async () => ...)` 的 `async void` lambda 中被**静默吞掉**。

2. **启动时序竞态**（根本原因）：`ConfigService.LoadAsync()` 在 `Dispatcher.UIThread.Invoke(async () => ...)` 中 fire-and-forget 执行。
`Invoke` 接受 `Action`，`async () =>` 编译为 `async void`——遇到第一个 `await` 就返回。
`MainWindow` 和所有 ViewModel 在 `LoadAsync` 完成**之前**就创建了。
`SettingsPaneViewModel` 构造函数中 `_displayGameRootDir = Config.GameRootDir` 读到的是默认值 `Path.GetFullPath("./")`。

**修复**：

| 文件 | 改动 |
|------|------|
| `ViewModels\ViewModelBase.cs` | `Loc`/`Notification`/`Logger` + `[JsonIgnore]`；`LocalizedObservableObject.Loc` + `[JsonIgnore]` |
| `App.axaml.cs` | `Task.Run(() => ConfigService.LoadAsync()).GetAwaiter().GetResult()` — 线程池执行 + 同步等待，**在 MainWindow 创建前**完成 config 加载 |

> **为什么 `Task.Run` 不死锁？** `LoadAsync` 的 `await File.ReadAllTextAsync` 在线程池执行时无 `SynchronizationContext`，回调不依赖 UI 线程。
> **为什么不能直接 `GetResult()`？** UI 线程有 `SynchronizationContext`，`await` 回调需要回 UI 线程 → 经典死锁。

### Bug B：DataTable 脏数据高亮 / Value Editor Alert / Ctrl+S 状态不一致

**现象**：DataGrid 行没有修改高亮，但 Value Editor 弹出脏数据 Alert，Ctrl+S 提示"没有需要保存的"。

**根因**：`ModGameDataTabsView.Tab.cs:417`，从 `TabSnapshotCache` 恢复时，`_isDirty` 求值在 `PushEditStateToGrid` **之前**。
此时 `GenericDataGridHelper.EditedCells` 读的是 `Session.ActiveEditStore`（仍是旧的/空的），而非刚恢复的 `cached.EditStore`。

**修复**：

| 文件 | 改动 |
|------|------|
| `Views\UserControls\ModGameDataTabsView.Tab.cs:413-419` | `PushEditStateToGrid(MergeStore, EditStore)` 移到 `_isDirty` 求值**之前** |

---

## 改动统计

| 类别 | 文件数 | 说明 |
|------|:--:|------|
| ViewModel（构造函数注入） | 6 | `ModDatabaseViewModel`, `ModImagesDocument`, `ModIndexViewModel`, `DocumentWorkspaceViewModel`, `CreateModDialogViewModel`, `DataBrowserViewModel` |
| ViewModel（手动 `Loc=`） | 2 | `MainWindowViewModel`, `PeekPanelViewModel` |
| ViewModel（防御性修复） | 2 | `ViewModelBase`（ViewServices 回退 + `[JsonIgnore]`）, `HomePageViewModel`（base 调用） |
| View code-behind（sed 批量） | ~30 | `App.XXX` → `ViewServices.XXX` |
| GDH / Helper / Service（sed 批量） | 5 | 同上 |
| 新建文件 | 1 | `Helper/ViewServices.cs` |
| App.axaml.cs 清理 | 1 | 删 17 个 V6 静态属性 + `ApplyFontSize`；保留 5 个内部用静态属性；`LoadAsync` 改为同步等待 |
| 缺少 using 补充（sed 后） | ~24 | `using NeoEditor.Helper;` |
| NeoEditor.Tests | 2 | 加 Core+Infra 引用，去 NoWarn，`ViewServices.TestServiceProvider` |
| Bug 修复（本轮验收） | 2 | `ViewModelBase.cs`（[JsonIgnore]）, `App.axaml.cs`（startup sync）, `ModGameDataTabsView.Tab.cs`（cache restore order） |

**总计修改文件 ~72，新建 1。**

---

## 残留项（M9 后续处理）

- `App.axaml.cs` 保留 5 个 static 属性（`ServiceProvider`, `Logger`, `ConfigService`, `Localizor`, `Notification`）供 `ImportGameDataOnStartupAsync` 等内部 static 方法使用。M9 后可改为实例方法 + `_host.Services`。
- `ViewServices` 是服务定位器反模式，作为 M9 前过渡。M9-M11 Plugin 拆分时各 View 应逐步改为通过 ViewModel/DI 获取服务。
- `GenericDataGridHelper` 静态类 + 静态方法模式仍在，M9 DataViewer Plugin 中拆分为 `DataTableService` + `ColumnTemplateFactory` + `InteractionHandler`。

### N01 状态

**N01（禁止静态可变状态）已从 spec 层面完全落地。**
- `App.*` 外部引用：0
- `GenericDataGridHelper` 静态可变字段：通过 `ViewServices.DataGridState` 委托给 DI 单例
- 唯一残留：`App.axaml.cs` 内部使用的 5 个 static 属性（不再被外部代码访问）
- `ConfigService.LoadAsync` 改为 `Task.Run(() => ...).GetResult()` 同步等待，消除启动时序竞态

---

## 下轮验收清单（test_round5 验收 8 + 9）

### 验收 8：编辑器基本功能

| 步骤 | 操作 | 检查点 |
|:--:|------|--------|
| 1 | 启动编辑器 | 欢迎页文本完整（Logo / 标题 / 快速入口卡片 / 最近 Mod） |
| 2 | 点 Settings | 设置页正常，语言/主题/字体可选 |
| 3 | 配置 Game Root Dir | 路径保存成功 |
| 4 | Browse Game Data | DataTable 打开，列头有文字，数据行显示 |
| 5 | 单击行 | 行高亮，Bottom 详情正常 |
| 6 | 双击行 | EntityEditorDocument 打开，Visual 选项卡有卡片 |
| 7 | XML 选项卡 | XML 可编辑 |
| 8 | 修改字段 → Ctrl+S | 保存成功，无报错 |
| 9 | KV 编辑 | Key-Value 面板正常 |
| 10 | Home 回主页 | 正常切换 |
| 11 | Import Mod | 导入成功，列表刷新 |
| 12 | Add Profile | 创建成功 |
| 13 | 重启编辑器 | 之前状态恢复（如有 Session 持久化） |

### 验收 9：R09 脏数据回归

| 步骤 | 操作 | 检查点 |
|:--:|------|--------|
| 1 | 双击打开实体 | 标题正常 |
| 2 | 修改字段（不保存） | 标题 `*` 前缀 |
| 3 | 看 Sidebar ModDatabase | ⚠ 脏标记 |
| 4 | 看 HomePage | 最近 Mod 有未保存标记 |
| 5 | Ctrl+S 保存 | `*` 消失，⚠ 清除 |
| 6 | 改后关 Tab | Alert 弹窗提示 |
| 7 | 改后关编辑器，重启 | 脏数据保持 |
