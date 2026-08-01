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
    /// User-facing Ctrl+LMB navigate: resolves entity via IReferenceResolver,
    /// then fires NavigateToEntityRequestedMessage via IMessenger for cross-region UI linkage (R05).
    /// Components subscribe to the message and implement their own open-tab / jump logic.
    /// </summary>
    void NavigateToEntity(Type entityType, string entityId, NeoEditor.Data.Model.Game.IEntity? resolvedEntity = null);

    /// <summary>
    /// User-facing Ctrl+RMB peek: fires PeekEntityMessage via IMessenger for cross-region UI linkage (R05).
    /// The trigger side only resolves the reference; components subscribe and implement their own peek UI.
    /// </summary>
    void RequestPeek(Type entityType, string rawId, NeoEditor.Data.Model.Game.IEntity? entity);

    /// <summary>
    /// Internal: jump the DataTable to a matching row via responsibility chain.
    /// Does NOT fire NavigateToEntityRequestedMessage — used by internal navigation only.
    /// Returns true if a target handled the navigation.
    /// </summary>
    bool NavigateDataTable(Type entityType, string entityId);

    /// <summary>
    /// Push entity info to the Peek panel (ReferenceInspector).
    /// Internal plumbing — prefer RequestPeek for user-triggered peek.
    /// </summary>
    void Peek(Type entityType, string rawId, NeoEditor.Data.Model.Game.IEntity? entity);
}
