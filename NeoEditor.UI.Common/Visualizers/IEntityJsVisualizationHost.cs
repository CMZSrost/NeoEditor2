using Avalonia.Controls;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.UI.Common.Visualizers;

/// <summary>
/// D09: cross-plugin extension point for the "JS 可视化" tab — a WebView2-hosted
/// JS visualization page fed with entity snapshot JSON. Implemented by the
/// NeoEditor.Plugins.JsVisualization plugin (registered in the App composition
/// root, R20); EntityEditorView only depends on this interface (R17/R18), so the
/// tab stays hidden when no implementation is registered.
/// </summary>
public interface IEntityJsVisualizationHost
{
    /// <summary>Tab name used by the plugin (localized by the plugin itself).</summary>
    string Name { get; }

    /// <summary>
    /// Build the WebView host control. Return null when WebView2 is unavailable on
    /// this platform — the caller then keeps the tab hidden (Avalonia visualizer
    /// stays the only rendering).
    /// </summary>
    Control? BuildView();

    /// <summary>Switch the rendered entity. Called on document open and entity change.</summary>
    void LoadEntity(IEntity entity);
}
