# 35 — Semi/Ursa → Avalonia 12 + Fluent 主题迁移（已完成）

> v3.0 · 2026-07-31 · **已完成** ✅
> 实际执行: Avalonia 11.3.12 → 12.1.1 + 移除 Semi/Ursa + **纯 Fluent 主题（未引入 AtomUI）**
> 详见 memory `avalon12-semi-removal-2026-07-31.md`

---

## 〇、动机

- **Semi.Avalonia.ProDataGrid #820** — ColumnHeader 模板覆盖导致过滤按钮不可见、叠加原生主题后列头消失
- **Semi + Ursa 双主题叠层** — 复杂度高，且 Ursa 实际未使用其控件（只有 `Irihi.Ursa` 包引用 + 一个空的 `using Ursa.Controls;`）
- **Avalonia 12** — 当前 11.3.12，顺势升级到 12.0.4；ProDataGrid 已有 12.0.4 兼容版本
- **AtomUI** — Ant Design 设计体系，企业级控件库，社区活跃

---

## 一、现状

### 1.1 NuGet 依赖现状

| 包 | 当前 | 目标 | 说明 |
|---|------|------|------|
| `Avalonia` | 11.3.12 | **12.0.4** | 主框架 |
| `Avalonia.Desktop` | 11.3.12 | **12.0.4** | |
| `Avalonia.Controls.DataGrid` | — (被 ProDataGrid 覆盖) | — | ProDataGrid 替代 |
| `ProDataGrid` | 11.3.11 | **12.0.4** | 高性能 DataGrid fork |
| `Semi.Avalonia` | 11.3.7.3 | **移除** | |
| `Semi.Avalonia.ProDataGrid` | 11.3.9-beta.1 | **移除** | #820 根本原因 |
| `Semi.Avalonia.Dock` | 11.3.6.2 | **移除** | |
| `Semi.Avalonia.AvaloniaEdit` | 11.2.0.1 | **移除** | |
| `Irihi.Ursa` | 1.15.1 | **移除** | 未实际使用控件 |
| `Irihi.Ursa.Themes.Semi` | 1.15.1 | **移除** | |
| `Dock.Avalonia` | 11.3.11.16 | **保留** | 已有 net10.0 支持 |
| `Dock.Avalonia.Themes.Fluent` | 11.3.11.16 | **保留** | |
| `AvaloniaEdit` | 11.4.1 | **保留** | 已有更新的版本 |
| `AtomUI.Desktop.Controls` | — | **6.0.6** | AtomUI 主控件主题 |
| `AtomUI.Desktop.Controls.DataGrid` | — | **6.0.6** | AtomUI DataGrid 主题（安装备用） |
| `AtomUI.Controls.Shared` | — | **6.0.6** | 共享控件 |
| `AtomUI.Fonts.AlibabaSans` | — | **latest** | 字体 |

### 1.2 当前 App.axaml 主题栈

```xml
<FluentTheme />                     <!-- Avalonia 原生 Fluent -->
<dock:SemiTheme />                  <!-- Semi.Avalonia -->
<u-semi:SemiTheme />                <!-- Irihi.Ursa.Themes.Semi -->
<DockFluentTheme />                 <!-- Dock Fluent -->
<dock:ProDataGridSemiTheme />       <!-- Semi ProDataGrid ← #820 bug -->
<dock:DockSemiTheme />              <!-- Semi Dock -->
<dock:AvaloniaEditSemiTheme />      <!-- Semi AvaloniaEdit -->
```

### 1.3 代码引用

| 位置 | 引用 | 处理 |
|------|------|------|
| `App.axaml` | 6 行 `<dock:*>` / `<u-semi:*>` | 替换 |
| `LocalizationService.cs:10` | `using Semi.Avalonia;` | 替换 |
| `LocalizationService.cs:42` | `SemiTheme.OverrideLocaleResources` | 替换 |
| `ImageTools.csproj` | `Irihi.Ursa` 包引用 | 移除 |
| `ImageEditorDocumentView.axaml.cs:8` | `using Ursa.Controls;` | 移除（未使用） |

---

## 二、目标架构

### 2.1 主题栈

```xml
<FluentTheme />                                          <!-- Avalonia 12 原生 Fluent -->
<atomUI:AtomUITheme />                                   <!-- AtomUI 主主题（Ant Design 风格） -->
<DockFluentTheme />                                      <!-- Dock 主题 -->
<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml" />
                                                         <!-- ProDataGrid 原生 Fluent 主题 -->
<StyleInclude Source="avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml" />
                                                         <!-- AvaloniaEdit Fluent -->
```

### 2.2 ProDataGrid 策略

ProDataGrid 替换了标准 `Avalonia.Controls.DataGrid.dll`（同程序集名，drop-in replacement）。它内建了 Fluent 和 Simple 主题资源，走 `avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml` 加载。

- **不用** `AtomUI.Desktop.Controls.DataGrid`——AtomUI DataGrid 主题针对标准 `Avalonia.Controls.DataGrid`，而 ProDataGrid 用自己的 ColumnHeader 模板
- **不用** `Semi.Avalonia.ProDataGrid`——#820 根源
- **用 ProDataGrid 原生 Fluent 主题**——`StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml"`，ColumnHeader 模板不过滤按钮，`ShowFilterButton` 可正常工作

### 2.3 为什么不做 ProDataGrid → 标准 DataGrid 切换

| ProDataGrid 特性 | 标准 DataGrid 有吗 |
|-----------------|-------------------|
| `IFilteringModel` 列过滤 | ❌ 无 |
| `ShowFilterButton` + `FilterFlyout` | ❌ 无 |
| Sort NRE 修复（DispatcherPriority.Background） | 未知 |
| `FilteringAdapterFactory` 自动应用过滤 | ❌ 无 |
| 公式引擎（FormulaEngine） | ❌ 无 |
| 虚拟列（→Id、Mod） | ✅ 有 |
| Semi #820 | ❌ 不受影响 |

> 结论：保留 ProDataGrid。我们用它的过滤基础设施（已接线 + 18 个测试），换 Semi 主题为 Fluent 原生主题即可让过滤按钮恢复。

---

## 三、实施步骤

### Phase M0: Avalonia 11 → 12 升级（~1h）

> 先升级 Avalonia，确认 12.x 生态兼容，再做主题替换。这样可以独立验证回退。

- [ ] M0.1 升级 Avalonia 核心包到 12.0.4
  - `Avalonia`、`Avalonia.Desktop`、`Avalonia.Fonts.Inter`、`Avalonia.Diagnostics` 等
  - 升级 `Avalonia.Controls.ColorPicker`（如有）
- [ ] M0.2 升级 ProDataGrid 到 12.0.4
- [ ] M0.3 升级 AvaloniaEdit 到 11.4.1
- [ ] M0.4 编译 + 运行全部测试（314+），修复 Avalonia 12 breaking changes
- [ ] M0.5 启动应用验证基本功能（DataGrid、Dock、Editor）

### Phase M1: 移除 Semi + Ursa（~0.5h）

- [ ] M1.1 移除 NuGet 包
  - `Semi.Avalonia`、`Semi.Avalonia.ProDataGrid`、`Semi.Avalonia.Dock`、`Semi.Avalonia.AvaloniaEdit`
  - `Irihi.Ursa`、`Irihi.Ursa.Themes.Semi`
- [ ] M1.2 清理代码引用
  - `LocalizationService.cs`：移除 `using Semi.Avalonia;` 和 `SemiTheme.OverrideLocaleResources`
  - `ImageTools.csproj`：移除 `Irihi.Ursa` 包引用
  - `ImageEditorDocumentView.axaml.cs`：移除 `using Ursa.Controls;`

### Phase M2: 接入 AtomUI（~1h）

- [ ] M2.1 安装 AtomUI NuGet 包
  - `AtomUI.Desktop.Controls` 6.0.6
  - `AtomUI.Controls.Shared` 6.0.6
  - `AtomUI.Fonts.AlibabaSans`（最新版）
- [ ] M2.2 更新 App.axaml 主题栈
  - 移除所有 `<dock:SemiTheme>`、`<u-semi:SemiTheme>`、`<dock:ProDataGridSemiTheme>`、`<dock:DockSemiTheme>`、`<dock:AvaloniaEditSemiTheme>`
  - 添加 AtomUI 主题声明（参考 AtomUI 官方文档的 App.axaml 配置）
  - 添加 ProDataGrid Fluent 主题：`<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.v2.xaml" />`
- [ ] M2.3 替换 Semi 本地化
  - 移除 `SemiTheme.OverrideLocaleResources` 调用
  - 改用 Avalonia 原生 `CultureInfo.CurrentCulture` 或 AtomUI 本地化机制

### Phase M3: DataGrid 过滤恢复（~0.5h）

- [ ] M3.1 在 `SearchableDataGrid.OnAutoGeneratingColumn` 恢复列过滤赋值
  - `e.Column.ShowFilterButton = true;`
  - `e.Column.ColumnKey = e.PropertyName;`
  - `e.Column.FilterFlyout = new ColumnFilterFlyout(_filterModel, e.PropertyName, e.PropertyName);`
- [ ] M3.2 验证过滤按钮可见 + ColumnFilterFlyout 正常工作

### Phase M4: 测试 & 收尾（~1h）

- [ ] M4.1 全量测试：`dotnet test NeoEditor.sln`，确认 314+ 全过
- [ ] M4.2 目测验证
  - DataGrid 外观 + 过滤按钮 `🔽` 可见
  - Dock 布局（标签页、拖拽、浮动）
  - AvaloniaEdit 编辑器
  - ImageTools 图片编辑
  - AI Chat 面板
  - 整体颜色/字体/间距
- [ ] M4.3 更新 CLAUDE.md + CHANGELOG

---

## 四、风险

| 风险 | 概率 | 缓解 |
|------|:--:|------|
| Avalonia 12 breaking changes | 中 | M0 先单独升级验证，独立于主题替换 |
| AtomUI 6.0.x 不支持 net10.0 | 中 | 安装前验证 NuGet target framework；如不支持则等 AtomUI 更新或用 5.x |
| ProDataGrid Fluent 主题 ColumnHeader 也不显示过滤按钮 | 低 | ProDataGrid 原生模板包含过滤按钮；Semi 是唯一移除它的主题 |
| Dock.Avalonia 11.3.11.16 + Avalonia 12 不兼容 | 低 | 已有 net10.0 target，API 兼容 |
| AtomUI 总体外观不满意 | 中 | 保留 FluentTheme 为 base，可回退到纯 Fluent |
| 移除 Ursa 后缺少控件 | 低 | Ursa 当前无实际使用；App.axaml 的 `u-semi:SemiTheme` 只是主题叠层 |

---

## 五、回退

1. 恢复 `Semi.*` + `Irihi.Ursa*` NuGet 引用
2. 恢复 App.axaml 原始主题栈
3. Avalonia 12 升级独立于主题迁移（M0 先做），如 Avalonia 12 有问题可先回退 Avalonia 再回退主题
4. ProDataGrid 过滤基础设施保留（FilteringModel 已接线，不受主题影响）
