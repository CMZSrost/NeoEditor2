# 架构测试第15轮 — M10 Phase 4: 25 个 Visualizer 迁移

> 日期：2026-07-28 | 分支：main | 设备：Windows 10 Pro (.NET 10 / Avalonia 11.3)
> 上承：[test_round14_summary.md](test_round14_summary.md) (M9 收尾 + M10 EntityEditor 骨架)

## 本轮目标

完成 M10 Phase 4：将 25 个 IEntityVisualizer 从 `NeoEditor.App/Views/UserControls/Editors/` 迁移到 `NeoEditor.Plugins.EntityEditor/Visualizers/`。包含命名空间重构、VisHelper 静态→实例 DI、RefNode 类型重定向、DataTableService.Instance → DI 注入。

---

## 最终结果

| # | 工作项 | 状态 | 说明 |
|---|--------|:--:|------|
| 4a | 简单 Visualizer 迁移（6 个） | ✅ | DataFile/GameVar/Headline/ForbiddenHex/ItemProp/Default — 纯 `VisHelper.` → `_vis.` |
| 4b | 中等 Visualizer 迁移（10 个） | ✅ | AttackMode/BattleMove/ChargeProfile/Condition/Creature/DmcPlace/EncounterTrigger/HexType/Ingredient/Map — + `Helper.RefNode` → `Services.RefNode` |
| 4c | 复杂 Visualizer 迁移（9 个） | ✅ | BarterHex/CampType/ContainerType/CreatureSource/Encounter/Faction/ItemType/Recipe/TreasureTable — + `DataTableService.Instance` → DI `_dataTable` |
| — | HexMapRenderer → UI.Common | ✅ | 解决 MapVisualizer 的 App 依赖 |
| — | `RegisterEntityEditorVisualizers()` | ✅ | 统一 25 个 Visualizer 注册入口 |
| — | App.axaml.cs 瘦身 | ✅ | 35 行内联注册 → 1 行调用 |
| — | build.sh 更新 | ✅ | 加入 EntityEditor Plugin 项目 |
| — | 编译验证 | ✅ | EntityEditor Plugin 0 error, App 0 C# error |
| — | 测试验证 | ✅ | DataViewer 10/10, App 2/2 |

---

## 1. 迁移模式

### 简单（6 个）：VisHelper 调用

```csharp
// 旧（App）：
public class DataFileEntityVisualizer : IEntityVisualizer
{
    public Control BuildDetail(IEntity entity) {
        VisHelper.LoadImage(df.Image);
        VisHelper.BuildRawDataTable(df);
        VisHelper.AddModBadge(df, row);
        // ...
    }
}

// 新（Plugin）：
public class DataFileEntityVisualizer : IEntityVisualizer
{
    private readonly VisHelperService _vis;
    public DataFileEntityVisualizer(VisHelperService vis) { _vis = vis; }
    public Control BuildDetail(IEntity entity) {
        _vis.LoadImage(df.Image);
        _vis.BuildRawDataTable(df);
        _vis.AddModBadge(df, row);
        // ...
    }
}
```

### 中等（10 个）：VisHelper + RefNode

```csharp
// 旧字段：private readonly Helper.RefNode _refNode;
// 旧构造：ClassName(Helper.RefNode? refNode)
//   =>  fallback: new Helper.RefNode(VisHelper.Resolver, VisHelper.Router)

// 新字段：private readonly VisHelperService _vis;
//        private readonly Services.RefNode _refNode;
// 新构造：ClassName(VisHelperService vis, Services.RefNode? refNode)
//   =>  fallback: new Services.RefNode(vis.Resolver, vis.Router)
```

### 复杂（9 个）：VisHelper + RefNode + DataTableService

```csharp
// 额外字段：private readonly DataTableService _dataTable;
// 额外构造参数：DataTableService? dataTable
// 替换：NeoEditor.Plugins.DataViewer.Services.DataTableService.Instance?.Xxx
//   → _dataTable?.Xxx
```

---

## 2. HexMapRenderer 迁移

MapEntityVisualizer 引用了 App 层的 `HexMapRenderer`，不满足 R18（Plugin 不能依赖 App）。

**解决方案**：将 `HexMapRenderer` 从 `NeoEditor.App/Helper/` 移到 `NeoEditor.UI.Common/Helpers/`。

| 方面 | 旧 | 新 |
|------|----|----|
| 位置 | `NeoEditor.App/Helper/HexMapRenderer.cs` | `NeoEditor.UI.Common/Helpers/HexMapRenderer.cs` |
| 命名空间 | `NeoEditor.Helper` | `NeoEditor.UI.Common.Helpers` |
| 依赖 | Avalonia + Core Model | 同（UI.Common 已有 Avalonia + Core 引用） |
| 消费者 | App 内 MapVisualizer + 日志 | Plugin MapVisualizer + 保留日志引用 |

---

## 3. App.axaml.cs 注册简化

**旧代码**（〜35 行）：
```csharp
// Initialize VisHelper with injected services
Views.UserControls.Editors.VisHelper.SetServices(...);

// Register entity visualizers
var visualizerRegistry = _host.Services.GetRequiredService<EntityVisualizerRegistry>();
var defaultVis = new Views.UserControls.Editors.DefaultEntityVisualizer(typeof(IEntity));
visualizerRegistry.SetDefault(defaultVis);
var refNode = _host.Services.GetRequiredService<Helper.RefNode>();
visualizerRegistry.Register(...) // ×25
```

**新代码**（2 行）：
```csharp
var visualizerRegistry = _host.Services.GetRequiredService<EntityVisualizerRegistry>();
_host.Services.RegisterEntityEditorVisualizers();
```

---

## 4. 编译和自动化测试

| 项目 | 错误 | 警告 | 备注 |
|------|:--:|:--:|------|
| EntityEditor Plugin | 0 | 8 (NU1903+NU1701) | 25 Visualizer 全部 0 error |
| NeoEditor.App（临时输出） | 0 | 已知 CA2017+AVLN3001 | DLL 锁定问题，C# 编译无错误 |
| NeoEditor.Plugins.DataViewer.Tests | — | — | 10/10 ✅ |
| NeoEditor.App.Tests | — | — | 2/2 ✅ |

---

## 5. 架构合规验证

| 规则 | 检查项 | 结果 |
|------|--------|:--:|
| R18 | Plugin 不依赖 App | ✅（HexMapRenderer 已移到 UI.Common） |
| N01 | 无新增静态可变状态 | ✅（25 Visualizer 全部用 DI 注入） |
| N02 | 无 ReferenceResolver.Instance | ✅ |
| R17 | Plugin 互不引用（临时例外） | ⚠️ EntityEditor 仍引用 DataViewer（DataTableService），待提取接口 |
| — | Plugin 中 NeoEditor.App 引用 | **0** |
| — | Plugin 中 `VisHelper.` 静态调用 | **0** |
| — | Plugin 中 `Helper.RefNode` | **0** |
| — | Plugin 中 `DataTableService.Instance` | **0** |
| — | App `Editors/` 目录 | 仅剩 `VisHelper.cs`（Phase 6 清理） |

---

## 已删除的文件（从 App）

| 文件 | 说明 |
|------|------|
| `App/Views/UserControls/Editors/*.cs` (25 文件) | 全部 Visualizer 已移至 Plugin |
| `App/Helper/HexMapRenderer.cs` | 已移至 UI.Common |

---

## 当前 Plugin 结构

```
NeoEditor.Plugins.EntityEditor/
├── EntityEditorPlugin.cs
├── ServiceCollectionExtensions.cs           ← +RegisterEntityEditorVisualizers()
├── Services/
│   ├── VisHelperService.cs                 ← DI 单例（864 行）
│   └── RefNode.cs                          ← Plugin 版引用节点
├── Visualizers/ (25)
│   ├── AttackModeEntityVisualizer.cs       ← 中等（RefNode）
│   ├── BarterHexEntityVisualizer.cs        ← 复杂（+DataTableService）
│   ├── ...
│   └── TreasureTableEntityVisualizer.cs    ← 复杂（+DataTableService）
└── Views/
    └── ZoomableImageView.axaml/.cs
```

---

## 下一步

| # | 工作 | 说明 |
|---|------|------|
| 5 | 迁移 Editor Views/VMs | `EntityEditorView`, `KeyValueEditorView`, `XmlDiffView`, `EntityEditorDocument`, `OverlayChainToolContent`, 对话框 |
| 6 | DI 简化 + App 清理 | 删除旧 `VisHelper.cs`, App `RefNode.cs`, `ZoomableImageView` 副本 |
| 7 | DocumentWorkspaceViewModel 解耦 | `new EntityEditorDocument(...)` → `IDocumentPlugin` 工厂 |
| 8 | EntityEditor.Tests + 人工验收 | VisHelperService, Visualizers, Editor Views 单测 |
