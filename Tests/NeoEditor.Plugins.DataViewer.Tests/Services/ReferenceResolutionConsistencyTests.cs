using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.DataViewer.Services;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Plugins.DataViewer.Tests.Services;

/// <summary>
/// Regression: DataGridNavigationService.FindBestMatch must resolve through the SAME
/// canonical in-memory ReferenceIndex as the DataGrid display (LookupSubject), so
/// value-editor badges / Ctrl+click navigation stay consistent with the cell shown.
///   - no-prefix  → MergedId (R16)
///   - ns-prefix  → (ns, pk) — never falls back to merged
/// </summary>
public class ReferenceResolutionConsistencyTests
{
    private static (EntityMergeStore Store, DataGridNavigationService Nav) Setup()
    {
        var store = new EntityMergeStore();
        var game = new Faction { Id = 2, Name = "Game Faction", EntityId = "f2", ModId = -1 };
        var mod = new Faction { Id = 42, Name = "Mod Faction", EntityId = "f42", ModId = 0 };

        store.ReferenceLookups[typeof(Faction)] = new List<object> { game, mod };
        store.EntityNamespaces["f2"] = "0";
        store.EntityNamespaces["f42"] = "NSE";
        store.EntityMergedIds["f2"] = 2;
        store.EntityMergedIds["f42"] = 5;

        // Build the canonical in-memory index (same as ModGameDataTabsView load path).
        store.Index.Build();

        var session = new SessionStub(store);
        var nav = new DataGridNavigationService(session, new ResolverStub(), null!);
        return (store, nav);
    }

    [Fact]
    public void FindBestMatch_NoPrefix_ResolvesByMergedId()
    {
        var (_, nav) = Setup();

        Assert.Equal("f2", nav.FindBestMatch(typeof(Faction), "2", null)!.EntityId);
        Assert.Equal("f42", nav.FindBestMatch(typeof(Faction), "5", null)!.EntityId);
    }

    [Fact]
    public void FindBestMatch_NamespacePrefix_ResolvesInThatNamespace_NoMergedFallback()
    {
        var (_, nav) = Setup();

        Assert.Equal("f42", nav.FindBestMatch(typeof(Faction), "NSE:42", null)!.EntityId);
        // "NSE:2" must NOT resolve to game faction 2 — prefixed lookups stay in the ns.
        Assert.Null(nav.FindBestMatch(typeof(Faction), "NSE:2", null));
    }

    [Fact]
    public void FindBestMatch_MatchesDisplayLookup_BackendIsConsistent()
    {
        var (store, nav) = Setup();
        const string rawId = "NSE:42";

        var display = store.Index.LookupDisplay("", "", typeof(Faction), rawId);
        var resolved = nav.FindBestMatch(typeof(Faction), rawId, null);

        Assert.NotNull(resolved);
        Assert.Equal("f42", resolved.EntityId);
        Assert.Equal(display.Subject, resolved.Subject);
    }

    // ── Test doubles ─────────────────────────────────────────────────────

    private sealed class SessionStub : IWorkspaceSession
    {
        private readonly EntityMergeStore _merge;
        public SessionStub(EntityMergeStore merge) => _merge = merge;

        public EntityMergeStore? Store => _merge;
        public EntityMergeStore? ActiveMergeStore => _merge;
        public EntityMergeStore? BrowserStore => null;
        public EditTrackingStore? ActiveEditStore => null;
        public int CurrentProfileId { get; set; } = -1;
        public ISet<string> DirtyEntities => new HashSet<string>();
        public ReferenceIndexService? ForwardIndex { get; set; }
        public ReferenceIndexService? ReverseIndex { get; set; }

        public event EventHandler? DirtyStateChanged;
        public event EventHandler? StateChanged;

        public void SetActiveStores(EntityMergeStore? mergeStore, EditTrackingStore? editStore)
        {
        }

        public void SetBrowserStore(EntityMergeStore? store)
        {
        }

        public ISet<string> GetDirtyEntities(int profileId) => new HashSet<string>();

        public void UnloadProfile(int profileId)
        {
        }

        public void MarkEntityDirty(string entityId)
        {
        }

        public void MarkEntitiesDirty(IEnumerable<string> entityIds)
        {
        }

        public void ClearDirtyEntities()
        {
        }

        public void RemoveDirtyEntities(IEnumerable<string> entityIds)
        {
        }
    }

    private sealed class ResolverStub : IReferenceResolver
    {
        public IEntity? LookupRefByRawId(IEntity sourceEntity, string rawId, Type targetType, EntityMergeStore? storeOverride = null) => null;

        public string? LookupSubject(string sourceEntityId, string propertyName, Type targetType,
            string rawId, Type? secondaryTargetType = null) => null;

        public string? LookupEntityId(ReferenceIndexService indexService, string entityType,
            string rawId, string? sourceNs) => null;

        public T? LookupRef<T>(IEntity sourceEntity, string propertyName, string rawId) where T : IEntity => null;

        public IReadOnlyList<(string SourceEntityId, string PropertyName, string RawId)> ReverseLookup(
            EntityMergeStore store, string targetEntityId) => [];

        public Task BuildReverseIndexAsync(ReferenceIndexService indexService, EntityMergeStore store)
            => Task.CompletedTask;

        public List<(Type SourceType, string SourceSubject, string SourceEntityId, string PropName)>
            ResolveReverseRefs(EntityMergeStore store, string targetEntityId) => [];

        public void ClearLookupCache()
        {
        }
    }
}