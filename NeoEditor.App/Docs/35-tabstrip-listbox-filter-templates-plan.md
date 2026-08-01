# 35 — TabStrip → ListBox + ProDataGrid 内置 Filter 模板集成

> v1.2 · 2026-08-01 · **P1 ✅ P2 ✅ — 全部完成**
> 基于: [34](34-prodatagrid-column-filter-plan.md), ProDataGrid 源码 `C:\Users\Cromzst\RiderProjects\ProDataGrid`

---

## 一、问题 1：Tab Headers 改用 ListBox ✅ 已完成 (2026-08-01)

### 实施结果

`TabControl` + `TabStripPlacement="Left"` → `ListBox x:Name="TabListBox"`。

**实际修改**（5 文件）：

| 文件 | 变更 |
|------|------|
| `ModGameDataTabsView.axaml` | `TabControl` → `ListBox` + `ItemsPanelTemplate`(StackPanel 禁用虚拟化) + 焦点虚线框隐藏 |
| `ModGameDataTabsView.axaml.cs` | `DataTabs` → `TabListBox`（3 处），移除 `TemplateApplied` hack（~20行），移除 `using Avalonia.Controls.Primitives` |
| `ModGameDataTabsView.Tab.cs` | `DataTabs` → `TabListBox`（3 处），移除 `using Avalonia.Controls.Primitives` |
| `ModGameDataTabsView.Data.cs` | `DataTabs` → `TabListBox`（4 处），移除 `using Avalonia.Controls.Primitives` |
| `DataLoaderService.cs` | `BuildHeader` 不再走 `_loc[]` 本地化，直接用 `entityType.Name`（修复 tab 头部中英文混杂 bug） |

**关键点**：
- ListBox 自带 ScrollViewer，无需模板 hack
- `ItemsPanelTemplate` 用普通 `StackPanel` 禁用虚拟化，避免滚动时宽度跳变
- `SelectionChanged` 签名兼容，`OnTabChanged` 无需改签名
- 去除焦点虚线框：`ListBox:focus /template/ Border#FocusVisual` → `IsVisible=False`

---

## 二、问题 2：ProDataGrid 内置 Filter 模板集成

### 发现

ProDataGrid 12.0.4 的 `Generic.xaml` **包含**内置 filter editor 模板（之前误判为不存在）：

```
加载链: Fluent.v2.xaml → Fluent.xaml → Generic.xaml (filter templates)
```

Generic.xaml 定义了：

| 资源 Key | 类型 | 说明 |
|----------|------|------|
| `DataGridFilterFlyoutPresenterTheme` | `ControlTheme` | Flyout 弹出框主题 |
| `DataGridFilterTextEditorTemplate` | `DataTemplate` | 文本 filter（标签+输入框+Apply/Clear） |
| `DataGridFilterNumberEditorTemplate` | `DataTemplate` | 数值 filter（NumericUpDown Min-Max+Apply/Clear） |
| `DataGridFilterEnumEditorTemplate` | `DataTemplate` | 枚举 filter（CheckBox 多选+Apply/Clear） |
| `DataGridFilterDateEditorTemplate` | `DataTemplate` | 日期 filter（DatePicker From-To+Apply/Clear） |

这些 DataTemplate 绑定到 `IFilterTextContext` / `IFilterNumberContext` / `IFilterEnumContext` / `IFilterDateContext` 接口，
**不绑定到具体实现类**。所以我们的 `TextFilterContext` / `NumberFilterContext` / `EnumFilterContext` 可以直接用作 Content。

### 方案：恢复 FilterFlyoutFactory 使用内置模板

**核心思路**：
```
FilterFlyoutFactory.Create(propertyType, columnKey, propertyPath, model)
  → 创建对应的 FilterContext (Text/Number/Enum/Bool)
  → 查找内置 DataTemplate (DataGridFilter{Text|Number|Enum}EditorTemplate)
  → 查找内置 ControlTheme (DataGridFilterFlyoutPresenterTheme)
  → 创建 Flyout { Content=context, ContentTemplate=template, FlyoutPresenterTheme=theme }
  → 如果模板/主题找不到 → 回退 ColumnFilterFlyout
```

**FilterContext → FilteringDescriptor 映射**：

| Context | Operator | Values |
|---------|----------|--------|
| `TextFilterContext` | `Contains` | `text` |
| `NumberFilterContext` | `Between` | `[min, max]` |
| `EnumFilterContext` | `In` | `[selected1, selected2, ...]` |
| Bool (`EnumFilterContext`) | `In` | `["True"]` / `["False"]` |

### 实施步骤

**P2.1** 恢复 `FilterFlyoutFactory.cs`：
- 删除当前自建 UI 的 `TypeFilterFlyout` 类
- 改为查找内置模板的版本：
  - `Application.Current.FindResource("DataGridFilterFlyoutPresenterTheme")` → `ControlTheme`
  - `Application.Current.FindResource("DataGridFilterTextEditorTemplate")` → `DataTemplate`
  - 如果都找到 → 创建 Flyout with `Content=ctx, ContentTemplate=template, FlyoutPresenterTheme=theme`
  - 否则 → `ColumnFilterFlyout` 回退（测试环境）

**P2.2** 检查 `NumericUpDown` 可用性：
- Generic.xaml 的 number filter 模板用了 `<NumericUpDown>`
- 确认 ProDataGrid 12.0.4 NuGet 包含此控件
- 如果不可用 → number filter 回退到 TextBox-based UI

**P2.3** 文本 filter 操作符增强：
- Generic.xaml 的文本模板只有一个 TextBox（默认 Contains 行为）
- 考虑扩展 `TextFilterContext` 增加 Operator 选择能力
- 或者保持简单：模板只做 Contains，如需其他操作符用户用搜索框

**P2.4** 更新测试：
- `FilterFlyoutFactory` 的测试在 `Application.Current==null` 时走 fallback
- 验证 fallback 到 `ColumnFilterFlyout` 行为
- 保留 `FilterContexts` 的测试（实现 ProDataGrid 接口）

**P2.5** 清理 & 移除：
- 移除 `FilterFlyoutFactory.cs` 中的 `TypeFilterFlyout` 类
- 移除 `FilterFlyoutFactory.cs` 中的 `FilterKind` enum
- `ColumnFilterFlyout.cs` 保留（虚拟列 + 测试 fallback）
- `FilterContexts.cs` 保留（ProDataGrid 接口实现，被 Flyout 使用）

### 文件清单

| 文件 | 操作 |
|------|------|
| `FilterFlyoutFactory.cs` | **重写**：内外置模板路径 + ColumnFilterFlyout fallback |
| `FilterContexts.cs` | 保留（可能微调，如 TextFilterContext 增加 operator 参数） |
| `ColumnFilterFlyout.cs` | 保留不变 |
| `SearchableDataGrid.axaml.cs` | 工厂调用不变 |
| `FilteringIntegrationTests.cs` | 更新 factory 测试 |
| `App.axaml` | 确认 Fluent.v2.xaml 已加载（已确认，无需改） |

---

## 三、DynamicResource 确认

Generic.xaml 的模板引用了以下 DynamicResource（定义在 Fluent.xaml 的 ThemeDictionaries 中）：

| Key | Fluent 默认值 |
|-----|--------------|
| `DataGridFilterFlyoutPadding` | `12` |
| `DataGridFilterFlyoutCornerRadius` | `4` |
| `DataGridFilterEditorSpacing` | `8` |
| `DataGridFilterEditorActionSpacing` | `6` |
| `DataGridFilterTextEditorWidth` | `240` |
| `DataGridFilterNumberEditorWidth` | `240` |
| `DataGridFilterNumberInputWidth` | `100` |
| `DataGridFilterEnumEditorWidth` | `240` |
| `DataGridFilterFlyoutBackgroundBrush` | `ThemeBackgroundColor` / `SystemChromeMediumLowColor` |

这些由 `Fluent.v2.xaml → Fluent.xaml` 链自动加载，**无需手动添加**。

---

## 四、风险

| 风险 | 等级 | 缓解 |
|------|:--:|------|
| ~~ListBox 替换 TabControl 后现有选中同步/导航逻辑需适配~~ | ~~中~~ | ✅ 已完成：直接替换，`SelectionChanged` 签名兼容，344/344 测试通过 |
| `NumericUpDown` 控件不在 ProDataGrid NuGet | 中 | 编译验证，不可用则 number flyout 用 TextBox fallback |
| Generic.xaml 在运行时不可用（avares 路径问题） | 低 | 已验证加载链 Fluent.v2 → Fluent → Generic |
| 测试环境 `Application.Current==null` → 工厂走 fallback | 低 | 预期行为；真机验证时模板可用 |

---

## 五、不实施

- **不自己画 filter UI**（`TypeFilterFlyout` 删除）——用 ProDataGrid 内置模板
- **不碰虚拟列 filter**（→Id, Mod 继续用 `ColumnFilterFlyout`）
- **Tab 选择逻辑**不变——只换选择器的视觉控件

---

## 六、P2 实施记录 (2026-08-01)

### FilterFlyoutFactory 重写

- 删除 `TypeFilterFlyout` 内部类（~250行）、`FilterKind` enum、`ComboItem` record
- `Create()` 改为：dispatch type → 创建 `FilterContext`（`TextFilterContext` / `NumberFilterContext` / `EnumFilterContext`）+ 真实 `IFilteringModel` callbacks → 通过 `Application.Current.FindResource()` 查找 ProDataGrid 内置 `DataGridFilter{Text|Number|Enum}EditorTemplate` + `DataGridFilterFlyoutPresenterTheme` → 构建 `Flyout`
- 新增 private helpers：`ApplyTextFilter`（Contains）、`ApplyNumberFilter`（Between）、`ApplyEnumFilter`（In）、`TryFindResource<T>`
- `FilterContexts.cs` 签名不变，callback 模式天然解耦
- `Application.Current==null` 时 fallback 到 `ColumnFilterFlyout`（测试环境安全网）

### Column Chooser 迁移

- `ModGameDataTabsView.axaml`：Options 按钮 → `<dg:DataGridColumnChooser>`
- 删除 `OnColumnManagerClick`（手写 ContextMenu ~40行）+ `GetColumnHeaderText`
- 新增 `WireColumnChooser()`（绑定 `ColumnChooser.DataGrid`）+ `HookColumnVisibilityPersistence()`（监听 `AvaloniaPropertyChangedEventArgs.Property.Name == IsVisible` → `ToggleColumnVisibility` 持久化 + `ColumnVisibilityChangedMessage` 同步）
- 新建 `ColumnHeaderTextConverter`：StackPanel / TextBlock / string header → 纯文本，解决 visual parenting 冲突（StackPanel 不能同时属于 DataGridColumnHeader 和 CheckBox）
- `MergedId` 列 `CanUserHide = false`
- `SettingsPaneViewModel.ColumnOption.ToggleInConfig` 增加 `ColumnVisibilityChangedMessage` 发送

### 代码量

- 删除 ~310 行（TypeFilterFlyout + OnColumnManagerClick + GetColumnHeaderText）
- 新增 ~200 行（FilterFlyoutFactory helpers + WireColumnChooser + HookColumnVisibilityPersistence + ColumnHeaderTextConverter）
- 344/344 测试通过 ✅
