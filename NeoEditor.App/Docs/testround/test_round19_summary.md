# 架构测试第19轮 — M12 收尾完成（终版）

> 日期：2026-07-29 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round18_summary.md](test_round18_summary.md) (M11 ImageTools Plugin)
> 下接：M0-M12 插件化架构迁移路线图 **全部完成** 🎉

## 本轮目标

完成 M12 收尾：项目清理 + 死代码删除 + 静态访问器清零 + Integration.Tests + 文档终稿。

---

## 完成的工作

### 1. 删除旧 NeoEditor.Tests 项目
- 项目目录已删除（3 个源文件：CoreFlowTests.cs、TestStubs.cs、csproj）
- 从 `.sln` 和 `build.sh` 中完全移除
- 已有 44 个编译错误，类型已全部迁出

### 2. 移除 DataTableService.Instance 静态访问器
- **ReferenceResolver.cs**: 移除 `public static Instance` 和 `Svc` 属性，改为注入 `IWorkspaceSession` 和 `IDataGridNavigationService`
- **ReferenceIntegrityRule.cs**: 改为构造注入 `DataTableService?`
- **ModGameDataTabsView.Tab.cs**: `DataTableService.Instance?.SetActiveStores()` → `WorkspaceSession.SetActiveStores()`
- **DataTableService.cs**: 移除 `public static DataTableService? Instance` 属性和构造中的 `Instance = this`
- **DataTableServiceTests.cs**: 移除 `Constructor_SetsStaticInstance` 测试
- App 中 0 处 DataTableService.Instance 引用

### 3. 移除 DocumentWorkspaceViewModel.Instance
- **RightPanelView.axaml.cs**: `DocumentWorkspaceViewModel.Instance` → `ViewServices.Get<>()`
- **DataBrowserViewModel.cs**: 同上
- **DocumentWorkspaceViewModel.cs**: 移除 `public static Instance` 属性和构造中的 `Instance = this`

### 4. 删除空壳 NeoEditor.App.Tests
- 唯一测试文件 `PluginRegistryTests.cs` 随 PluginRegistry 删除
- 从 `.sln` 和 `build.sh` 中移除
- 项目目录已删除

### 5. 删除死代码 5 处
| 文件 | 说明 |
|------|------|
| `Helper/Attributes.cs` | 空文件（仅 namespace 声明） |
| `Helper/Extensions/StringExtension.cs` | `ToCamelCase()` 零引用 |
| `Helper/Converter/PageTypeToBoolConverter.cs` | 零引用（不用于任何 XAML） |
| `Hosting/PluginRegistry.cs` | 已定义但未接入启动流 |
| `Tests/PluginRegistryTests.cs` | 随 PluginRegistry 删除 |

### 6. ViewServices 清理
- 移除 3 个 `[Obsolete]` 访问器：`DataGridState`、`DataGridNavigationService`、`DataGridCellInteraction`（改为 View 字段注入）
- 移除未使用的 `Logger` 属性
- 保留其余 15 个便捷访问器供 code-behind 使用（~31 文件引用）

### 7. 创建 NeoEditor.Integration.Tests
- 10 个集成测试覆盖跨 Plugin 消息流、DI 组合、插件契约验证

---

## 编译和测试

| 项目 | 结果 |
|------|:----:|
| `bash build.sh` (16 项目) | **0 Error** (仅 NU1903) |
| Core.Tests | 3/3 ✅ |
| Infra.Tests | 2/2 ✅ |
| Messaging.Tests | 3/3 ✅ |
| UI.Common.Tests | 1/1 ✅ |
| DataViewer.Tests | 9/9 ✅ |
| EntityEditor.Tests | 9/9 ✅ |
| ImageTools.Tests | 4/4 ✅ |
| **Integration.Tests** | **10/10 ✅** |
| **总计** | **41/41 ✅** |

---

## 迁移里程碑一览

| 阶段 | 内容 | 状态 |
|------|------|:----:|
| M7 | 代码卫生（空 catch + Warning 清零） | ✅ |
| M8 | Core 基础设施（Messaging/Core/Infra/UI.Common/App） | ✅ |
| M9 | DataViewer Plugin | ✅ |
| M10 | EntityEditor Plugin | ✅ |
| M11 | ImageTools Plugin | ✅ |
| **M12** | **收尾：清理 + 集成测试 + 文档终稿** | **✅ 本轮完成** |

## 剩余项（技术债）

| 项 | 说明 |
|---|------|
| ViewServices.cs | ~31 code-behind 文件仍使用，作为 View 层服务定位器保留 |
| Warning 清单 | ~85 非阻塞警告（CS0618/CS8602/NU1903 等），均在安全阈值内 |
