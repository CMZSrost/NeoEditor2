using System;
using System.Collections.Generic;
using NeoEditor.Plugins.DataViewer.Services;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Plugins.DataViewer.Tests.Services;

public class DataTableServiceTests
{
    [Fact]
    public void GetEntityModName_ReturnsEmpty_WhenNoStore()
    {
        var session = new StubWorkspaceSession(new EntityMergeStore(), new EditTrackingStore());
        var service = new DataTableService(session, null!);

        var result = service.GetEntityModName(new StubEntity("test-id", "Test Subject"));

        Assert.Equal("", result);
    }

    [Fact]
    public void GetOverlayChain_ReturnsEmpty_WhenNoOverlayData()
    {
        var session = new StubWorkspaceSession(new EntityMergeStore(), new EditTrackingStore());
        var service = new DataTableService(session, null!);

        var result = service.GetOverlayChain(new StubEntity("test-id", "Test"));

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void TakeSnapshot_ReturnsNonNull()
    {
        var session = new StubWorkspaceSession(new EntityMergeStore(), new EditTrackingStore());
        var service = new DataTableService(session, null!);

        var snapshot = service.TakeSnapshot();

        Assert.NotNull(snapshot);
    }

    [Fact]
    public void GetEntities_ReturnsEmpty_WhenNoReferenceLookups()
    {
        var session = new StubWorkspaceSession(new EntityMergeStore(), new EditTrackingStore());
        var service = new DataTableService(session, null!);

        var result = service.GetEntities<Data.Model.Game.ItemType>();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void GetDedupedEntities_ReturnsEmpty_WhenNoReferenceLookups()
    {
        var session = new StubWorkspaceSession(new EntityMergeStore(), new EditTrackingStore());
        var service = new DataTableService(session, null!);

        var result = service.GetDedupedEntities<Data.Model.Game.ItemType>();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void SetActiveStores_DelegatesToSession()
    {
        var session = new StubWorkspaceSession(new EntityMergeStore(), new EditTrackingStore());
        var service = new DataTableService(session, null!);

        var newMerge = new EntityMergeStore();
        var newEdit = new EditTrackingStore();
        service.SetActiveStores(newMerge, newEdit);

        Assert.Same(newMerge, session.ActiveMergeStore);
        Assert.Same(newEdit, session.ActiveEditStore);
    }

    // ── Stub workspace session ────────────────────────────────────────────

    private sealed class StubWorkspaceSession : IWorkspaceSession
    {
        private EntityMergeStore? _activeMergeStore;
        private EditTrackingStore? _activeEditStore;
        private EntityMergeStore? _browserStore;

        public StubWorkspaceSession(EntityMergeStore mergeStore, EditTrackingStore editStore)
        {
            _activeMergeStore = mergeStore;
            _activeEditStore = editStore;
        }

        public EntityMergeStore? Store => _activeMergeStore ?? _browserStore;
        public EntityMergeStore? ActiveMergeStore => _activeMergeStore;
        public EntityMergeStore? BrowserStore => _browserStore;
        public EditTrackingStore? ActiveEditStore => _activeEditStore;
        public int CurrentProfileId { get; set; } = -1;
        public ISet<string> DirtyEntities => new HashSet<string>();
        public ReferenceIndexService? ForwardIndex { get; set; }
        public ReferenceIndexService? ReverseIndex { get; set; }

        public event EventHandler? DirtyStateChanged;
        public event EventHandler? StateChanged;

        public void SetActiveStores(EntityMergeStore? mergeStore, EditTrackingStore? editStore)
        {
            _activeMergeStore = mergeStore;
            _activeEditStore = editStore;
        }

        public void SetBrowserStore(EntityMergeStore? store) => _browserStore = store;
        public ISet<string> GetDirtyEntities(int profileId) => new HashSet<string>();
        public void UnloadProfile(int profileId) { }
        public void MarkEntityDirty(string entityId) { }
        public void MarkEntitiesDirty(IEnumerable<string> entityIds) { }
        public void ClearDirtyEntities() { }
        public void RemoveDirtyEntities(IEnumerable<string> entityIds) { }
    }

    private sealed class StubEntity : Data.Model.Game.IEntity
    {
        public StubEntity(string entityId, string subject)
        {
            EntityId = entityId;
        }
    }
}
