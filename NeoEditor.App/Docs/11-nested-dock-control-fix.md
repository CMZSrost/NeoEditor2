# 11 - 嵌套 DockControl 空白渲染修复

> DomainBrowserView 中嵌套 DockControl 的渲染空白问题诊断与修复记录。
> 状态：✅ 已修复并应用于当前代码。DomainBrowserView 现已正常使用嵌套 DockControl。

## 问题描述

`DomainBrowserView` 使用声明式 XAML 在 Grid 右侧嵌入一个 `DockControl`，用于展示实体查看器标签页。
点击左侧实体列表后，标签页被添加到 `DocumentDock`，但右侧区域**完全空白**——内容控件存在但宽度为 0。

### 症状日志

```
PSP: Children=1, Bounds=0,0,1062.67,687.33
  child=ContentPresenter, Proportion=0, Bounds=0,0,0,687.33, DesiredSize=0,687.33
EntityViewerView: Bounds=0,0,0,652, DesiredSize=0,652
```

PSP 自身有 1062px 宽度，但子控件 ContentPresenter 的 Proportion=0 → 分配 0 宽度 → 整条渲染链宽度为 0。

## 根因分析

### 视觉树链

```
DockControl → RootDockControl → ProportionalDockControl → ItemsControl
  → ItemsPresenter → ProportionalStackPanel (PSP)
    → ContentPresenter (Proportion=0 ← 问题所在)
      → DocumentDockControl → DockableControl → DocumentControl
        → DockPanel → Border → Grid → DockableControl
          → DeferredContentControl → EntityViewerView
```

### 渲染链

```
ProportionalDockControl 的 ItemsControl 为每个 IDockable 创建 ContentPresenter 容器
  → ContainerPrepared 事件设置 TwoWay 绑定:
      ContentPresenter[Proportion] ↔ IDockable.Proportion
  → DocumentDock.Proportion 默认值 = 0
  → ContentPresenter.Proportion = 0
```

### PSP 布局算法（反编译自 `Dock.Controls.ProportionalStackPanel.dll`）

```csharp
// ProportionUtils.IsValidProportion
public static bool IsValidProportion(double proportion)
{
    if (!double.IsNaN(proportion))
        return proportion >= 0.0;  // 0 被视为"有效"!
    return false;
}

// ProportionManager.AssignProportions() 流程:
// 1. HandleCollapsedChildren()
//    → 非折叠子控件: TargetProportion = CurrentProportion (= 0)
// 2. AssignUnassignedProportions()
//    → 只处理 !IsValidProportion(TargetProportion) 的子控件
//    → IsValidProportion(0) == true → 跳过! 不重新分配
// 3. NormalizeProportions()
//    → sum = 0 → num <= 0.0 → 直接返回，不做任何事
// 4. ApplyProportions()
//    → proportion * availableWidth = 0 * 1062 = 0
```

**结论：`Proportion=0` 被 PSP 视为"有效的零比例"，不会触发自动分配，导致子控件宽度永远为 0。**

## 修复方案

### 核心修复：设置 `Proportion = double.NaN`

`double.NaN` 使 `IsValidProportion` 返回 `false`，触发 PSP 的 `AssignUnassignedProportions` 自动分配比例。

在 `DomainBrowserView.axaml.cs` 的两处设置：

```csharp
// 1. FindDocumentDock() — 初始化时
if (_viewerDocDock is IDockable dockable)
{
    dockable.Proportion = double.NaN;
}

// 2. OnEntityClicked() — 每次添加 dockable 后
if (_viewerDocDock is IDockable dd)
    dd.Proportion = double.NaN;
```

### 修复后的 PSP 流程

```
CurrentProportion = NaN
HandleCollapsedChildren → TargetProportion = NaN (非折叠，无 CollapsedProportion)
AssignUnassignedProportions → IsValidProportion(NaN) = false → 分配 1.0
NormalizeProportions → sum = 1.0 → 无需归一化
ApplyProportions → 1.0 × 1062.67 = 1062.67 ✓
```

### 辅助措施

- **ForceApplyTemplates**: `AddDockable` 后通过 `Dispatcher.UIThread.Post` 强制应用所有 `TemplatedControl` 的模板并 `UpdateLayout`，确保嵌套 Dock 主题控件（DocumentDockControl 等）在首次布局时正确渲染。
- **ScrollViewer 水平滚动**: `EntityViewerView` 的 ScrollViewer 设置 `HorizontalScrollBarVisibility=Disabled`，防止内容控件请求无限宽度。

## XAML 布局结构

```xml
<Grid ColumnDefinitions="260,4,*">
    <!-- 左侧实体列表 -->
    <Border Grid.Column="0">
        <DockPanel>
            <ListBox x:Name="EntityListBox" ItemsSource="{Binding Entities}" />
        </DockPanel>
    </Border>

    <GridSplitter Grid.Column="1" Width="4" />

    <!-- 右侧嵌套 DockControl -->
    <DockControl x:Name="ViewerDockControl" Grid.Column="2"
        EnableManagedWindowLayer="True"
        Factory="{Binding DataContext.DockFactory, ElementName=Root}"
        InitializeFactory="False" InitializeLayout="False">
        <DockControl.DataTemplates>
            <DataTemplate x:DataType="mainContent:EntityViewerDocument">
                <userControls:EntityViewerView DataContext="{Binding}" />
            </DataTemplate>
        </DockControl.DataTemplates>
        <RootDock Id="ViewerRoot">
            <ProportionalDock>
                <DocumentDock Id="ViewerDocumentsPane" EnableWindowDrag="False">
                    <DocumentDock.DocumentTemplate>
                        <DocumentTemplate>
                            <Grid x:DataType="Document">
                                <ContentControl Content="{Binding Context}" />
                            </Grid>
                        </DocumentTemplate>
                    </DocumentDock.DocumentTemplate>
                </DocumentDock>
            </ProportionalDock>
        </RootDock>
    </DockControl>
</Grid>
```

## 涉及文件

| 文件 | 作用 |
|------|------|
| `Views/UserControls/DomainBrowserView.axaml` | 声明式嵌套 Dock 布局 |
| `Views/UserControls/DomainBrowserView.axaml.cs` | Proportion=NaN 修复 + ForceApplyTemplates |
| `Views/UserControls/EntityViewerView.axaml.cs` | ScrollViewer 水平滚动禁用 |

## 踩坑记录

| 问题 | 原因 | 解决 |
|------|------|------|
| PSP 子控件宽度为 0 | `Proportion=0` 被视为有效比例 | 设置 `Proportion=double.NaN` |
| 模板未应用 | 嵌套 Dock 主题 TemplatedControl 延迟应用 | `ForceApplyTemplates` + `UpdateLayout` |
| ScrollViewer 无限宽度 | `HorizontalScrollBarVisibility=Auto` 导致内容请求无限宽度 | 设为 `Disabled` |
| DLL 锁 (MSB3021) | 僵尸 dotnet 进程锁定 bin/Debug DLL | `Stop-Process -Name "dotnet" -Force` |
| AVLN2000 编译错误 | DataTemplate 中 `{Binding Context}` 无法解析 | 改用 Document 模型 + ContentControl 绑定 |

## PSP Proportion 规则速查

| Proportion 值 | IsValidProportion | PSP 行为 |
|---------------|-------------------|----------|
| `double.NaN` | `false` | 自动分配（`1 - 已用总和` / 未分配数量） |
| `0` | `true` | 视为"有效零比例"，分配 0 宽度 |
| `0.5` | `true` | 按 50% 分配 |
| `1.0` | `true` | 按 100% 分配 |
| 负数 | `false` | 同 NaN，自动分配 |
