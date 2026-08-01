# 架构测试第17轮 — M10 Phase 6-8: DI 简化 + R17 解除 + EntityEditor.Tests

> 日期：2026-07-29 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round16_summary.md](test_round16_summary.md) (Editor Views/VMs 迁移)

## 本轮目标

完成 M10 Phase 6-8：DI 简化 + App 清理 + R17 违规解除 + EntityEditor.Tests 创建。

---

## 最终结果

| # | 工作项 | 状态 | 说明 |
|---|--------|:--:|------|
| 6a | `IEntityLookupService` 接口创建 | ✅ | Infra.Services 中，替代 DataTableService 直接引用 |
| 6b | DataTableService 实现接口 | ✅ | `: IEntityLookupService` + 属性返回类型兼容 |
| 6c | EntityEditor 9 个引用 DataTableService 的 Visualizer | ✅ | 全部改为 `IEntityLookupService` |
| 6d | VisHelperService 类型 | ✅ | `DataTableService` → `IEntityLookupService` |
| 6e | EntityEditorDocument 类型 | ✅ | `DataTableService` → `IEntityLookupService` |
| 6f | ServiceCollectionExtensions | ✅ | `DataTableService` → `IEntityLookupService` DI 解析 |
| 6g | EntityEditor csproj 移除 DataViewer ProjectReference | ✅ | **R17 违规彻底解除** |
| 6h | DI 注册桥接 (App.axaml.cs) | ✅ | `IEntityLookupService` → `DataTableService` |
| 6i | 删除 App 旧文件 | ✅ | `VisHelper.cs` (864 行)、`RefNode.cs` (158 行)、`Editors/` 目录 |
| 7a | `IEntityEditorDocumentFactory` 接口 (Core.Abstractions) | ✅ | 工厂契约 |
| 7b | `EntityEditorDocumentFactory` 实现 (Plugin.Services) | ✅ | 内部解析 DI 构造参数 |
| 7c | DI 注册 | ✅ | Plugin ServiceCollectionExtensions + App |
| 7d | `DocumentWorkspaceViewModel` 解耦 | ✅ | 3 处 `new EntityEditorDocument(...)` → `_entityEditorFactory.CreateDocument(entity)` |
| 8a | `EntityEditor.Tests` 项目创建 | ✅ | csproj + TestStubs.cs + 3 测试文件 |
| 8b | `EntityEditorPluginTests` (3 个) | ✅ | metadata + SupportedEntityTypes + CreateToolView |
| 8c | `VisHelperServiceTests` (4 个) | ✅ | 构造函数 + Resolver/Router/Loc 注入 |
| 8d | `EntityEditorDocumentFactoryTests` (2 个) | ✅ | 工厂创建 + Entity 正确性 |

---

## 1. Phase 6: DI 简化 + App 清理 + R17 解除

### 问题

EntityEditor Plugin 在 csproj 级别引用了 DataViewer Plugin（R17 违规），因为其 Visualizer 和 VisHelperService 直接依赖 `DataTableService`（定义在 DataViewer 中）。同时 App 中残留了被 Plugin 版本替代的旧文件。

### 方案

**创建 `IEntityLookupService` 接口** 于 `NeoEditor.Infra.Services`，暴露 EntityEditor Visualizer 需要的全部数据访问方法：

```csharp
public interface IEntityLookupService
{
    EntityMergeStore? ActiveMergeStore { get; }
    EntityMergeStore? BrowserStore { get; }
    Dictionary<Type, List<object>> ReferenceLookups { get; }
    Dictionary<string, string> EntityModNames { get; }
    Dictionary<string, string> EntityNamespaces { get; }
    Dictionary<string, int> EntityMergedIds { get; }
    HashSet<(string, string)> EditedCells { get; }
    Dictionary<int, T> GetEntities<T>() where T : IEntity;
    Dictionary<string, T> GetCompositeEntities<T>(...) where T : IEntity;
    // ...
}
```

**DataTableService 实现该接口**：`public class DataTableService : IEntityLookupService`

**DI 桥接注册** (App.axaml.cs)：
```csharp
services.AddSingleton<IEntityLookupService>(
    sp => sp.GetRequiredService<DataTableService>());
```

### 修改的文件

| 文件 | 改动 |
|------|------|
| `Infra/Services/IEntityLookupService.cs` | **新文件** — 数据访问接口 |
| `DataViewer/Services/DataTableService.cs` | `: IEntityLookupService` + using |
| `App/App.axaml.cs` | DI 桥接 + VisHelperService 构造参数改接口 |
| `EntityEditor/Services/VisHelperService.cs` | `DataTableService` → `IEntityLookupService` |
| `EntityEditor/ViewModels/EntityEditorDocument.cs` | 同上 |
| `EntityEditor/ServiceCollectionExtensions.cs` | 同上 + 移除 DataViewer.Services using |
| `EntityEditor/Visualizers/*` (9 个) | `DataTableService` → `IEntityLookupService` |
| `EntityEditor/*.csproj` | 移除 DataViewer ProjectReference + global using |

### 删除的旧文件

| 文件 | 大小 | 说明 |
|------|:--:|------|
| `App/Views/UserControls/Editors/VisHelper.cs` | 864 行 | 被 Plugin VisHelperService 替代 |
| `App/Helper/RefNode.cs` | 158 行 | 被 Plugin Services.RefNode 替代 |
| `App/Views/UserControls/Editors/`（目录） | — | 空目录 |

---

## 2. Phase 7: DocumentWorkspaceViewModel 解耦

### 问题

`DocumentWorkspaceViewModel` 直接 `new EntityEditorDocument(...)` 包含了 Plugin VM 的全套构造参数（5 个 DI 服务），违反 R18（App 不依赖 Plugin 实现细节）。

### 方案

**`IEntityEditorDocumentFactory` 接口** (Core.Abstractions)：
```csharp
public interface IEntityEditorDocumentFactory
{
    object CreateDocument(IEntity entity);
}
```

**实现** (Plugin.Services) — 内部解析 DI 服务，调用方只需传 entity。

**App 侧**改为：
```csharp
// 旧: 3 处 new EntityEditorDocument(entity, _session, _dbFactory, ...)
// 新:
private EntityEditorDocument CreateEntityEditorDocument(IEntity entity)
    => (EntityEditorDocument)_entityEditorFactory.CreateDocument(entity);
```

---

## 3. Phase 8: EntityEditor.Tests

### 测试项目结构

```
Tests/NeoEditor.Plugins.EntityEditor.Tests/
├── NeoEditor.Plugins.EntityEditor.Tests.csproj
├── TestStubs.cs                              ← 7 个共享 stub 实现
├── EntityEditorPluginTests.cs                ← 3 个测试
└── Services/
    ├── VisHelperServiceTests.cs              ← 4 个测试
    └── EntityEditorDocumentFactoryTests.cs   ← 2 个测试
```

### 测试结果

```
已通过! - 失败: 0, 通过: 9, 已跳过: 0, 总计: 9
```

### Stub 复用

`TestStubs.cs` 提供 7 个可复用 stub：
- `StubReferenceResolver` — IReferenceResolver
- `StubNavigationRouter` — INavigationRouter
- `StubEntityLookupService` — IEntityLookupService
- `StubLocalizationService` — ILocalizationService
- `StubNotificationService` — INotificationService
- `StubWorkspaceSession` — IWorkspaceSession
- `StubEntity` — IEntity

---

## 4. 架构合规验证

| 规则 | 检查项 | 结果 |
|------|--------|:--:|
| R17 | Plugin 互不引用 | ✅ — EntityEditor.csproj 无 DataViewer 引用 |
| R18 | Plugin 不依赖 App | ✅ — 0 对 NeoEditor.App 的 ProjectReference |
| R07 | 单向分层 | ✅ — Plugin → Core/Infra/UI.Common，不反向 |
| N01 | 无新增静态可变状态 | ✅ — 所有依赖走 DI 注入 |
| — | EntityEditor.Tests 独立 | ✅ — 不引用 App/DataViewer |
| — | 旧 App 死文件 | 已删除 VisHelper.cs + RefNode.cs |

---

## 5. 编译和自动化测试

| 项目 | 错误 | 警告 | 备注 |
|------|:--:|:--:|------|
| `bash build.sh`（14 项目） | **0** | 8 (NU1903 + stub CS0067) | 13 src + 7 test 全部通过 |
| DataViewer.Tests | — | — | 10/10 ✅ |
| EntityEditor.Tests | — | — | **9/9 ✅ [新]** |

---

## 6. 已知问题

| # | 问题 | 严重性 | 计划 |
|---|------|:--:|------|
| 1 | App `ViewServices.cs` 仍被 20+ 文件使用 | 低 | M12: 全局清理 |
| 2 | `NeoEditor.Tests` 旧项目 44 error | 低 | M12 后重写 |
| 3 | DataViewer.Tests 中 `DataTableService.Instance` 静态访问器仍在使用 | 低 | M12 清理 |
| 4 | `build.sh` 不含 ImageTools 项目 | 低 | M11 添加 |

---

## 7. 下一步

| # | 工作 | 说明 |
|---|------|------|
| M11 | ImageTools Plugin | 将图片编辑功能从 App 迁移到独立 Plugin |
