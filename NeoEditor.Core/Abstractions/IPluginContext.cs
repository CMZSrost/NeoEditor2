using System;
using NeoEditor.Messaging;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Context provided by the App shell to each plugin at initialization.
/// Gives access to DI, messaging, and the active workspace session.
/// </summary>
public interface IPluginContext
{
    /// <summary>DI service provider for resolving registered services.</summary>
    IServiceProvider Services { get; }

    /// <summary>Message bus for cross-plugin communication.</summary>
    IMessageBus MessageBus { get; }

    /// <summary>Active workspace session state.</summary>
    IWorkspaceSession Session { get; }
}
