namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Classification for plugin types. Every plugin class must carry exactly one <see cref="PluginKindAttribute"/>.
/// See <c>spec/R23-plugin-classification.md</c> for the full specification.
/// </summary>
public enum PluginKind
{
    /// <summary>
    /// Tool-panel and document-editing plugins with UI surface.
    /// Must implement <see cref="IToolPlugin"/> and/or <see cref="IDocumentPlugin"/>.
    /// </summary>
    Workbench,

    /// <summary>
    /// Backend-only plugins with no UI surface.
    /// Must implement <see cref="IServicePlugin"/>. Skipped during dock layout construction.
    /// </summary>
    Service,

    /// <summary>
    /// Plugins that extend behavior through HostService extension points
    /// without owning a dock panel. Hook into <see cref="IExtensionPoint{TContext}"/> pipelines.
    /// </summary>
    Feature
}
