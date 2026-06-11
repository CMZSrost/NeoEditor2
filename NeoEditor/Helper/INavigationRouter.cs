using System;

namespace NeoEditor.Helper;

/// <summary>
/// Navigation router — DI singleton. Maintains a registry of INavigationTarget instances
/// and routes navigation requests through them using a responsibility chain.
/// Replaces GenericDataGridHelper's static navigation methods and global state.
/// </summary>
public interface INavigationRouter
{
    /// <summary>Register a navigation target (called on Tab/view attach).</summary>
    void RegisterTarget(INavigationTarget target);

    /// <summary>Unregister a navigation target (called on Tab/view detach).</summary>
    void UnregisterTarget(INavigationTarget target);

    /// <summary>
    /// Navigate to the specified entity. Iterates registered targets by priority;
    /// the first CanNavigate=true target executes NavigateTo.
    /// Returns true if a target handled the navigation.
    /// </summary>
    bool Navigate(Type entityType, string entityId);

    /// <summary>
    /// Push entity info to the Peek panel (ReferenceInspector).
    /// </summary>
    void Peek(Type entityType, string rawId, NeoEditor.Data.Model.Game.IEntity? entity);

    /// <summary>
    /// Peek handler delegate. Set once by DocumentWorkspaceViewModel at startup.
    /// Signature: (entityType, rawId, entity) → wasHandled
    /// </summary>
    Func<Type, string, NeoEditor.Data.Model.Game.IEntity?, bool>? PeekHandler { get; set; }
}
