using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NeoEditor.Services;

/// <inheritdoc cref="IWorkspaceSession"/>
public class WorkspaceSession : IWorkspaceSession
{
    private EntityMergeStore? _activeMergeStore;
    private EntityMergeStore? _browserStore;
    private EditTrackingStore? _activeEditStore;
    private int _currentProfileId = -1;

    /// <summary>Per-profile dirty sets (R26 §3): one workspace session owns all profile scopes.</summary>
    private readonly ConcurrentDictionary<int, ISet<string>> _dirtyByProfile = new();

    public EntityMergeStore? Store => _activeMergeStore ?? _browserStore;
    public EntityMergeStore? ActiveMergeStore => _activeMergeStore;
    public EntityMergeStore? BrowserStore => _browserStore;
    public EditTrackingStore? ActiveEditStore => _activeEditStore;

    public ReferenceIndexService? ForwardIndex { get; set; }
    public ReferenceIndexService? ReverseIndex { get; set; }

    public int CurrentProfileId
    {
        get => _currentProfileId;
        set => _currentProfileId = value;
    }

    public ISet<string> DirtyEntities => GetDirtyEntities(CurrentProfileId);

    public ISet<string> GetDirtyEntities(int profileId)
        => _dirtyByProfile.GetOrAdd(profileId, _ => new HashSet<string>());

    public void MarkEntityDirty(string entityId)
    {
        if (GetDirtyEntities(CurrentProfileId).Add(entityId))
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void MarkEntitiesDirty(IEnumerable<string> entityIds)
    {
        var added = false;
        foreach (var eid in entityIds)
        {
            if (GetDirtyEntities(CurrentProfileId).Add(eid))
                added = true;
        }
        if (added)
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearDirtyEntities()
    {
        var set = GetDirtyEntities(CurrentProfileId);
        if (set.Count > 0)
        {
            set.Clear();
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RemoveDirtyEntities(IEnumerable<string> entityIds)
    {
        var set = GetDirtyEntities(CurrentProfileId);
        var before = set.Count;
        set.ExceptWith(entityIds);
        if (set.Count != before)
            DirtyStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UnloadProfile(int profileId)
        => _dirtyByProfile.TryRemove(profileId, out _);

    public event EventHandler? DirtyStateChanged;
    public event EventHandler? StateChanged;

    public void SetActiveStores(EntityMergeStore? mergeStore, EditTrackingStore? editStore)
    {
        _activeMergeStore = mergeStore;
        _activeEditStore = editStore;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetBrowserStore(EntityMergeStore? store)
    {
        _browserStore = store;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
