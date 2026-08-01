using System;
using System.Collections.Generic;

namespace NeoEditor.Services;

/// <summary>
/// Single owner of the current workspace state.
/// Replaces GenericDataGridHelper static store fields and BrowserIndexService static state.
/// DI lifetime: singleton (one per application).
/// </summary>
public interface IWorkspaceSession
{
    /// <summary>
    /// The active EntityMergeStore.
    /// Returns ActiveMergeStore when a merge-view tab is focused; falls back to BrowserStore.
    /// This is the single access point for resolvers and converters.
    /// </summary>
    EntityMergeStore? Store { get; }

    /// <summary>The per-tab merge store currently in focus. Set by SearchableDataGrid on attach.</summary>
    EntityMergeStore? ActiveMergeStore { get; }

    /// <summary>The global browser store built on startup from the full DB scan.</summary>
    EntityMergeStore? BrowserStore { get; }

    /// <summary>The edit-tracking store paired with ActiveMergeStore.</summary>
    EditTrackingStore? ActiveEditStore { get; }

    /// <summary>
    /// Called by SearchableDataGrid on attach/detach to set which tab's store is active.
    /// Replaces GenericDataGridHelper.SetActiveStores().
    /// </summary>
    void SetActiveStores(EntityMergeStore? mergeStore, EditTrackingStore? editStore);

    /// <summary>
    /// Called by BrowserIndexService when the global browser store is ready.
    /// Replaces the assignment to GenericDataGridHelper.BrowserStore.
    /// </summary>
    void SetBrowserStore(EntityMergeStore? store);

    /// <summary>
    /// The profile whose dirty set is exposed by the parameterless dirty members (R26 §3).
    /// -1 = game/base; &gt;= 0 = a mod profile. Set via <c>IHostService.SetActiveProfile</c>.
    /// </summary>
    int CurrentProfileId { get; set; }

    /// <summary>Entity IDs that have unsaved edits in the <b>current profile</b> (R09 + R26 §3).</summary>
    ISet<string> DirtyEntities { get; }

    /// <summary>Get the dirty set for a specific profile (per-profile dirty session, R26 §3).</summary>
    ISet<string> GetDirtyEntities(int profileId);

    /// <summary>Mark a single entity as having unsaved edits in the current profile (N03 encapsulation).</summary>
    void MarkEntityDirty(string entityId);

    /// <summary>Mark multiple entities as having unsaved edits in the current profile (N03 encapsulation).</summary>
    void MarkEntitiesDirty(IEnumerable<string> entityIds);

    /// <summary>Clear all dirty tracking for the current profile (N03 encapsulation).</summary>
    void ClearDirtyEntities();

    /// <summary>Remove specific entities from dirty tracking in the current profile (R11: single-tab save).</summary>
    void RemoveDirtyEntities(IEnumerable<string> entityIds);

    /// <summary>Release a profile's dirty state when the profile is closed/unloaded.</summary>
    void UnloadProfile(int profileId);

    /// <summary>
    /// Forward reference index backed by SQLite (R10 manual refresh).
    /// Built by BrowserIndexService at startup; refreshed on demand.
    /// </summary>
    ReferenceIndexService? ForwardIndex { get; set; }

    /// <summary>
    /// Reverse reference index backed by SQLite (R10 manual refresh).
    /// Built after forward index is complete.
    /// </summary>
    ReferenceIndexService? ReverseIndex { get; set; }

    /// <summary>Fires whenever DirtyEntities is modified (add/remove/clear).</summary>
    event EventHandler? DirtyStateChanged;

    /// <summary>Fires whenever Store, ActiveMergeStore, or BrowserStore changes.</summary>
    event EventHandler? StateChanged;
}
