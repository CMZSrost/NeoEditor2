using System.Collections.Generic;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// A plugin that registers as a Tool pane (left/right/bottom dock).
/// The returned view object is cast to an Avalonia Control by the App shell.
/// Spec: D02-dynamic-dock-layout §四.
/// </summary>
public interface IToolPlugin : IPlugin
{
    string Title { get; }
    ToolDock DefaultDock { get; }
    int Order { get; }

    /// <summary>Create the tool's view. Returns an object to avoid Core depending on Avalonia.</summary>
    object CreateToolView();

    /// <summary>
    /// Optional toolbar buttons contributed to this Tool's own toolbar (fixed
    /// assembly inside the panel). Return <c>null</c> to contribute nothing.
    /// </summary>
    IReadOnlyList<ToolbarItem>? CreateToolbarItems() => null;
}

/// <summary>Dock position for tool plugins.</summary>
public enum ToolDock { Left, Right, Bottom }
