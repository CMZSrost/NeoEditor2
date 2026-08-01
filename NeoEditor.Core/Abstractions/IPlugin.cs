using System;
using System.Threading.Tasks;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Base plugin contract. Every plugin implements this to register with the App shell.
/// </summary>
public interface IPlugin
{
    /// <summary>Unique plugin identifier (e.g. "DataViewer", "EntityEditor").</summary>
    string Name { get; }

    /// <summary>Semantic version of this plugin.</summary>
    Version Version { get; }

    /// <summary>
    /// Called by App shell at startup. The context provides access to DI, messaging, and session state.
    /// </summary>
    Task InitializeAsync(IPluginContext ctx);
}
