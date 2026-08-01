# 34 — ProDataGrid 列过滤器实现计划

> v1.3 · 2026-08-01 · **Phase F1-F4 完成 ✅**

## F4 实施记录 (2026-08-01)

已完成文件：
- `NeoEditor.Plugins.DataViewer/Services/FilterContexts.cs` — 4 个 IFilter*Context 实现 + EnumOption + RelayCommand (~140 行)
- `NeoEditor.Plugins.DataViewer/Services/FilterFlyoutFactory.cs` — 工厂方法（按类型分发 → Text/Number/Enum/Bool flyout + 资源查找 + ColumnFilterFlyout 回退，~220 行）
- `SearchableDataGrid.axaml.cs:626` — OnAutoGeneratingColumn 改用 FilterFlyoutFactory.Create()
- `FilteringIntegrationTests.cs` — 新增 19 个测试（FilterContexts 12 + FilterFlyoutFactory 7）
- `ColumnFilterFlyout.cs` — **保留**：作为 FlyoutFactory 回退 + 虚拟列（→Id, Mod）仍用它
- `ModGameDataTabsView.axaml` — TabStrip 从 WrapPanel 改为 StackPanel + ScrollViewer（垂直滚动 tabs）

333/333 全量测试通过。

ProDataGrid 12.0.4 确认的 API：
- ✅ `IFilterTextContext`, `IFilterNumberContext`, `IFilterEnumContext`, `IFilterDateContext`
- ✅ `IEnumOption`
- ✅ `DataGridFilterFlyoutPresenterTheme` (ControlTheme)
- ⚠️ `DataGridFilterTextEditorTemplate` 等 4 个 DataTemplate — Fluent 主题下运行时解析，测试环境 `Application.Current==null` → 自动回退到 ColumnFilterFlyout
> 基于: [31-prodatagrid-migration-plan.md](31-prodatagrid-migration-plan.md), [CHANGELOG.md](CHANGELOG.md)
> 第三方文档: [third-party/prodatagrid/articles/filtering-model-end-to-end.md](third-party/prodatagrid/articles/filtering-model-end-to-end.md)
> 目标: 用 ProDataGrid 内置模板替换手写 ColumnFilterFlyout

---

## 〇、动机

ProDataGrid 12.0.4 内置了 4 种类型过滤器模板和对应的 context 接口，替代我们手写的
`ColumnFilterFlyout`（只有 Contains 一种操作符、无类型适配）。

---


---

---

## 一、ProDataGrid 12.0.4 过滤器架构

### 1.1 核心流程

```
列头 Filter 按钮 (ShowFilterButton=true)
  ↓ click
FilterFlyout (Flyout presenter)
  ├── FlyoutPresenterTheme → DataGridFilterFlyoutPresenterTheme
  ├── Content → IFilter*Context (ViewModel 侧)
  └── ContentTemplate → DataGridFilter*EditorTemplate (内置模板)
       ↓ 用户输入 → ApplyCommand/ClearCommand
FilteringModel.SetOrUpdate / Remove
       ↓ 驱动
DataGridFilteringAdapter (DataGridAccessorFilteringAdapterFactory)
       ↓ 应用
DataGridCollectionView.Filter predicate
```

### 1.2 内置模板（4 种）

| 模板 Key | 绑定 Context | 适用列类型 | UI 效果 |
|----------|-------------|-----------|---------|
| `DataGridFilterTextEditorTemplate` | `IFilterTextContext` | 文本列 (string, 引用) | 操作符下拉 + 输入框 + Apply/Clear |
| `DataGridFilterNumberEditorTemplate` | `IFilterNumberContext` | 数值列 (int/float/double) | Min-Max 双输入框 + Apply/Clear |
| `DataGridFilterEnumEditorTemplate` | `IFilterEnumContext` | 枚举列 / bool | 多选勾选列表 + Apply/Clear |
| `DataGridFilterDateEditorTemplate` | `IFilterDateContext` | 日期列 | 日期范围选择 + Apply/Clear |
| 主题 Key | 用途 |
|----------|------|
| `DataGridFilterFlyoutPresenterTheme` | Flyout 弹出框主题样式 |

### 1.3 Context 接口

```
IFilterTextContext:
  Prop: string Label { get; }
  Prop: string Text { get; set; }
  Prop: ICommand ApplyCommand { get; }
  Prop: ICommand ClearCommand { get; }

IFilterNumberContext:
  Prop: string Label { get; }
  Prop: double Minimum { get; }
  Prop: double Maximum { get; }
  Prop: double? MinValue { get; set; }
  Prop: double? MaxValue { get; set; }
  Prop: ICommand ApplyCommand { get; }
  Prop: ICommand ClearCommand { get; }

IFilterEnumContext:
  Prop: string Label { get; }
  Prop: ObservableCollection<IEnumOption> Options { get; }
  Prop: ICommand ApplyCommand { get; }
  Prop: ICommand ClearCommand { get; }

IEnumOption:
  Prop: string Display { get; }
  Prop: bool IsSelected { get; set; }

IFilterDateContext: (不需要 — 游戏数据无日期类型)
```

### 1.4 FilteringDescriptor 操作符全集（12 种）

| 操作符 | 类别 | FilteringDescriptor 用法 |
|--------|------|--------------------------|
| `Contains` | 文本 | `new(columnId, Contains, propPath, value: "text", stringComparison: OrdinalIgnoreCase)` |
| `Equals` | 文本/数值/枚举 | `new(columnId, Equals, propPath, value: val)` |
| `NotEquals` | 文本/数值/枚举 | `new(columnId, NotEquals, propPath, value: val)` |
| `StartsWith` | 文本 | `new(columnId, StartsWith, propPath, value: "pref", stringComparison: ...)` |
| `EndsWith` | 文本 | `new(columnId, EndsWith, propPath, value: "suf", stringComparison: ...)` |
| `GreaterThan` | 数值 | `new(columnId, GreaterThan, propPath, value: 10)` |
| `GreaterThanOrEqual` | 数值 | `new(columnId, GreaterThanOrEqual, propPath, value: 10)` |
| `LessThan` | 数值 | `new(columnId, LessThan, propPath, value: 100)` |
| `LessThanOrEqual` | 数值 | `new(columnId, LessThanOrEqual, propPath, value: 100)` |
| `Between` | 数值/日期 | `new(columnId, Between, propPath, values: new[] { min, max })` |
| `In` | 枚举/布尔 | `new(columnId, In, propPath, values: new[] { "A", "B" })` |
| `Custom` | 任意 | `new(columnId, Custom, propPath, predicate: Func<object,bool>)` |

---

## 二、关键挑战：自动生成列 vs 静态 Flyout 方案

ProDataGrid 文档的推荐方案是用 **静态 XAML Flyout 资源**：

```xml
<Flyout x:Key="CustomerFilterFlyout"
        FlyoutPresenterTheme="{StaticResource DataGridFilterFlyoutPresenterTheme}"
        Content="{Binding CustomerFilter}"
        ContentTemplate="{StaticResource DataGridFilterTextEditorTemplate}" />
```

但 NeoEditor 的列是 **动态自动生成**的（`AutoGenerateColumns="True"`），编译时不知道有哪些列。
因此不能为每列预设 Flyout 资源。需要**程序化创建** filter context + flyout。

---

## 三、实现方案

### 3.1 新建文件

| 文件 | 位置 | 说明 |
|------|------|------|
| `FilterContexts.cs` | `DataViewer/Services/` | 4 个 IFilter*Context 实现类 + IEnumOption 实现 |
| `FilterFlyoutFactory.cs` | `DataViewer/Services/` | 按列类型创建 Flyout + Context 的工厂方法 |

### 3.2 FilterContext 实现类

```csharp
// TextFilterContext : IFilterTextContext
//   构造函数: (string label, Action<string?> apply, Action clear)
//   - ApplyCommand 调用 apply(Text)，然后 clear 重置 Text
//   - ClearCommand 调用 clear()

// NumberFilterContext : IFilterNumberContext  
//   构造函数: (string label, double min, double max, Action<double?,double?> apply, Action clear)
//   - ApplyCommand 调用 apply(MinValue, MaxValue)
//   - ClearCommand 调用 clear()

// EnumFilterContext : IFilterEnumContext
//   构造函数: (string label, IEnumerable<string> allOptions, IEnumerable<string>? selected, Action<IReadOnlyList<string>> apply, Action clear)
//   - Options: ObservableCollection<EnumOption>，每项 Display + IsSelected
//   - ApplyCommand 收集 IsSelected=true 的项调用 apply()

// EnumOption : IEnumOption
//   简单 POCO: (string Display, bool IsSelected)
```

### 3.3 FilterFlyoutFactory

```csharp
public static class FilterFlyoutFactory
{
    // 按列类型创建对应的 Flyout + FilterContext
    public static Flyout Create(
        Type propertyType,        // 属性类型
        string columnKey,         // ColumnKey
        string propertyPath,      // SortMemberPath / 属性名
        IFilteringModel model)    // 共享的 FilteringModel
    {
        return propertyType switch
        {
            // int / float / double / long → NumberFilterContext + Between operator
            _ when IsNumeric(propertyType) => CreateNumberFlyout(...),

            // bool → EnumFilterContext (True/False/All)
            _ when propertyType == typeof(bool) => CreateBoolFlyout(...),

            // enum → EnumFilterContext (列出所有值)
            _ when propertyType.IsEnum => CreateEnumFlyout(...),

            // string / reference / default → TextFilterContext
            _ => CreateTextFlyout(...)
        };
    }

    // 文本列: Contains 操作符（文本模板自带操作符下拉）
    private static Flyout CreateTextFlyout(...)
    {
        var ctx = new TextFilterContext(
            $"Filter {columnKey}",
            apply: text => {
                if (string.IsNullOrWhiteSpace(text))
                    model.Remove(columnKey);
                else
                    model.SetOrUpdate(new FilteringDescriptor(
                        columnKey, FilteringOperator.Contains,
                        propertyPath, text,
                        stringComparison: StringComparison.OrdinalIgnoreCase));
            },
            clear: () => model.Remove(columnKey));

        return new Flyout
        {
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            Content = ctx,
            ContentTemplate = TryFindResource<DataTemplate>("DataGridFilterTextEditorTemplate"),
            FlyoutPresenterTheme = TryFindResource<ControlTheme>("DataGridFilterFlyoutPresenterTheme")
        };
    }

    // 数值列: Between (范围) 操作符
    private static Flyout CreateNumberFlyout(...)
    {
        var ctx = new NumberFilterContext(
            $"Filter {columnKey}",
            minimum: double.MinValue, maximum: double.MaxValue,
            apply: (min, max) => {
                if (min == null && max == null)
                    model.Remove(columnKey);
                else
                    model.SetOrUpdate(new FilteringDescriptor(
                        columnKey, FilteringOperator.Between,
                        propertyPath,
                        values: new object[] { min ?? double.MinValue, max ?? double.MaxValue }));
            },
            clear: () => model.Remove(columnKey));

        // Same Flyout creation with DataGridFilterNumberEditorTemplate
    }

    // 枚举/bool 列: In 操作符 (多选)
    private static Flyout CreateEnumFlyout(...)
    {
        var ctx = new EnumFilterContext(
            $"Filter {columnKey}",
            allOptions: Enum.GetNames(propertyType),
            selected: null,
            apply: selected => {
                if (selected.Count == 0)
                    model.Remove(columnKey);
                else
                    model.SetOrUpdate(new FilteringDescriptor(
                        columnKey, FilteringOperator.In,
                        propertyPath,
                        values: selected.Cast<object>().ToArray()));
            },
            clear: () => model.Remove(columnKey));

        // Same Flyout creation with DataGridFilterEnumEditorTemplate
    }
}
```

### 3.4 资源查找

`DataGridFilterTextEditorTemplate` 等模板 Key 是 ProDataGrid 主题内定义的资源，不保证在所有
主题中都存在。策略：
1. 用 `Application.Current?.FindResource(...)` 查找
2. 如果找不到（Fluent 主题可能未定义），**回退到当前 `ColumnFilterFlyout`**
3. 回退行为日志 Warning 一次，避免刷屏

### 3.5 在 OnAutoGeneratingColumn 中集成

```csharp
// 替换:
//   e.Column.FilterFlyout = new ColumnFilterFlyout(_filterModel!, columnKey!, e.PropertyName);
// 为:
e.Column.FilterFlyout = FilterFlyoutFactory.Create(
    propertyType, columnKey!, e.PropertyName, _filterModel!);
```

虚拟列（→Id, Mod）保持 `ColumnFilterFlyout`（值不是实体属性，不需要类型模板）。

### 3.6 移除文件

- `ColumnFilterFlyout.cs` — 不再需要（虚拟列除外，可保留简化为仅文本过滤）

---

## 四、实施步骤

### Phase F4: 内置模板迁移（~3h）

- [ ] **F4.1** 新建 `FilterContexts.cs`（4 个实现类 + EnumOption，~120 行）
- [ ] **F4.2** 新建 `FilterFlyoutFactory.cs`（工厂方法 + 资源查找 + 回退，~100 行）
- [ ] **F4.3** 修改 `SearchableDataGrid.axaml.cs`：OnAutoGeneratingColumn 中调用工厂
- [ ] **F4.4** 构建验证 + 手动测试 4 种列类型（文本/数值/枚举/bool）
- [ ] **F4.5** 更新 `FilteringIntegrationTests.cs`（新增 filter context 测试）

### Phase F5: 收尾

- [ ] **F5.1** 移除 `ColumnFilterFlyout.cs`（或保留用于虚拟列 fallback）
- [ ] **F5.2** 全量测试（314 测试）
- [ ] **F5.3** 更新本文档 + CLAUDE.md

---

## 五、风险与回退

| 风险 | 等级 | 缓解 |
|------|:--:|------|
| Fluent 主题未定义内置模板 Key | 中 | 静态资源查找 + 回退到 ColumnFilterFlyout |
| 枚举列的 In 操作符行为不符预期 | 低 | 先实现文本+数值，枚举可后续迭代 |
| 自动生成列的 propertyPath 和 ColumnKey 不一致 | 低 | 已统一为 e.PropertyName |


