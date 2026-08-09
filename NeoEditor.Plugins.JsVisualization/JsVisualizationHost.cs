using System.Collections.Generic;
using Avalonia.Controls;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.JsVisualization.Services;
using NeoEditor.UI.Common.Visualizers;

namespace NeoEditor.Plugins.JsVisualization;

/// <summary>
/// D09 §二: the IEntityJsVisualizationHost implementation. 单 WebView2 共享
/// （P0.8 v3）：实体文档的 JS tab 是轻量壳（无 WebView），共享
/// <see cref="SharedJsVizWebView"/> 单例；壳的 Loaded/Unloaded = 文档 tab
/// 激活/失活信号（Avalonia TabControl 非选中 tab 不 attach）→ 共享 WebView
/// 移入/移回。离屏合成模式下输入走 Avalonia 转发，reparent 安全（P0.8 v3 前提）。
/// BuildView 每次返回新壳（Dock 重建文档视图安全，壳无 native 资源）。
/// </summary>
public sealed class JsVisualizationHost : IEntityJsVisualizationHost
{
    private readonly SharedJsVizWebView _shared;
    private readonly List<JsVizView> _views = new();

    public JsVisualizationHost(SharedJsVizWebView shared)
    {
        _shared = shared;
    }

    public string Name => "JS 可视化";

    public Control? BuildView()
    {
        var view = new JsVizView(_shared);
        _views.Add(view);
        return view;
    }

    public void LoadEntity(IEntity entity)
    {
        // 清理已被 Dock 回收的壳（防列表无限增长）
        _views.RemoveAll(v => v.IsDetached);
        foreach (var view in _views)
            view.LoadEntity(entity);
    }
}

/// <summary>
/// 每文档一个的轻量壳：自身不创建 WebView2；激活（Loaded）时把共享 WebView
/// 移入本壳宿主，失活（Unloaded）时移回。实体变化经 LoadEntity 转发给共享实例。
/// </summary>
public sealed class JsVizView : UserControl
{
    private readonly SharedJsVizWebView _shared;
    private readonly Grid _host = new();

    public JsVizView(SharedJsVizWebView shared)
    {
        _shared = shared;
        Content = _host;
        Loaded += (_, _) => _shared.Attach(_host);
        Unloaded += (_, _) => _shared.Detach(_host);
    }

    /// <summary>True when the shell is not attached to the visual tree (host can drop it).</summary>
    public bool IsDetached => Parent is null;

    /// <summary>Switch the rendered entity (document open / entity change).</summary>
    public void LoadEntity(IEntity entity) => _shared.LoadEntity(entity);
}
