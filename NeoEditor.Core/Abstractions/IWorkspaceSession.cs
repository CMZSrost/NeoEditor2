using System;
using System.Collections.Generic;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Single owner of the current workspace state.
/// Minimal Core-level contract; the full interface with service types lives in Infra.
/// DI lifetime: singleton (one per application).
/// </summary>
public interface IWorkspaceSession
{
    /// <summary>Entity IDs that have unsaved edits (R09 dirty guard).</summary>
    ISet<string> DirtyEntities { get; }

    /// <summary>Mark a single entity as having unsaved edits.</summary>
    void MarkEntityDirty(string entityId);

    /// <summary>Mark multiple entities as having unsaved edits.</summary>
    void MarkEntitiesDirty(IEnumerable<string> entityIds);

    /// <summary>Clear all dirty entity tracking.</summary>
    void ClearDirtyEntities();

    /// <summary>Remove specific entities from dirty tracking (R11: single-tab save).</summary>
    void RemoveDirtyEntities(IEnumerable<string> entityIds);

    /// <summary>Fires whenever DirtyEntities is modified (add/remove/clear).</summary>
    event EventHandler? DirtyStateChanged;

    /// <summary>Fires whenever session state changes.</summary>
    event EventHandler? StateChanged;
}
