using System.Threading.Tasks;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Marker interface for backend-only plugins with no UI surface.
/// Service plugins are registered in DI but skipped during dock layout construction.
/// Must be decorated with <c>[PluginKind(PluginKind.Service)]</c>.
/// See <c>spec/R23-plugin-classification.md</c>.
/// </summary>
/// <remarks>
/// Inherits <see cref="IPlugin.Name"/>, <see cref="IPlugin.Version"/>,
/// and <see cref="IPlugin.InitializeAsync"/> — no additional members.
/// Classification is conveyed by the attribute.
/// </remarks>
public interface IServicePlugin : IPlugin
{
}
