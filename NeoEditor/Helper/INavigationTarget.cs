using System;

namespace NeoEditor.Helper;

/// <summary>
/// Navigation target interface. Components that can display entities (DataGrid tabs,
/// Tab containers, workspace-level views) implement this to participate in the
/// responsibility-chain navigation system.
/// </summary>
public interface INavigationTarget
{
    /// <summary>Whether this target can navigate to the specified entity.</summary>
    bool CanNavigate(Type entityType, string entityId);

    /// <summary>Execute navigation. Called only after CanNavigate returned true.</summary>
    void NavigateTo(Type entityType, string entityId);

    /// <summary>Priority — higher numbers are tried first. Active tab = 100, container = 50, fallback = 0.</summary>
    int Priority { get; }
}
