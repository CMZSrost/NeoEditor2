using System;

namespace NeoEditor.Messaging;

/// <summary>
/// Central message bus abstraction. Provides type-based pub/sub messaging
/// for decoupled communication between modules. Implementations may wrap
/// CommunityToolkit.Mvvm or other messaging libraries.
/// </summary>
public interface IMessageBus
{
    /// <summary>Publish a message to all registered handlers of type T.</summary>
    void Send<T>(T message) where T : class;

    /// <summary>Register a handler for messages of type T on the given recipient.</summary>
    void Register<T>(object recipient, Action<T> handler) where T : class;

    /// <summary>Unregister all handlers of type T for the given recipient.</summary>
    void Unregister<T>(object recipient) where T : class;

    /// <summary>Unregister all handlers for the given recipient.</summary>
    void UnregisterAll(object recipient);
}
