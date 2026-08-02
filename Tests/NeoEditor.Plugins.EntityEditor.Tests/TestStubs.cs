using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Services;

namespace NeoEditor.Plugins.EntityEditor.Tests;

/// <summary>
/// Reusable stub implementations for EntityEditor Plugin tests.
/// </summary>

internal class StubReferenceResolver : IReferenceResolver
{
    public IEntity? LookupRefByRawId(IEntity sourceEntity, string rawId, Type targetType) => null;
    public string? LookupSubject(string sourceEntityId, string propertyName, Type targetType, string rawId,
        Type? secondaryTargetType = null) => null;
    public string? LookupEntityId(ReferenceIndexService indexService, string entityType, string rawId, string? sourceNs) => null;
    public virtual T? LookupRef<T>(IEntity sourceEntity, string propertyName, string rawId) where T : IEntity => null;
    public IReadOnlyList<(string SourceEntityId, string PropertyName, string RawId)> ReverseLookup(
        EntityMergeStore store, string targetEntityId) => [];
    public Task BuildReverseIndexAsync(ReferenceIndexService indexService, EntityMergeStore store) => Task.CompletedTask;
    public List<(Type SourceType, string SourceSubject, string SourceEntityId, string PropName)> ResolveReverseRefs(
        EntityMergeStore store, string targetEntityId) => [];
    public void ClearLookupCache() { }
}

internal class StubNavigationRouter : INavigationRouter
{
    public event Action<Type, string>? NavigationRequested;
    public event Action<Type, string, IEntity?>? PeekRequested;

    public bool Navigate(Type entityType, string entityId) => false;
    public bool NavigateDataTable(Type entityType, string entityId) => false;
    public virtual void NavigateToEntity(Type entityType, string entityId, IEntity? resolvedEntity = null) { }
    public void NavigateToEntity(string entityTypeName, string entityId) { }
    public void NavigateTo(Type entityType, int id) { }
    public virtual void RequestPeek(Type entityType, string entityId, IEntity? sourceEntity) { }
    public void Peek(Type entityType, string entityId, IEntity? entity) { }
    public void RegisterTarget(INavigationTarget target) { }
    public void UnregisterTarget(INavigationTarget target) { }
}

internal class StubEntityLookupService : IEntityLookupService
{
    public EntityMergeStore? ActiveMergeStore { get; set; }
    public EntityMergeStore? BrowserStore { get; set; }
    public HashSet<(string EntityId, string ColumnName)> EditedCells { get; set; } = [];
    public Dictionary<Type, List<object>> ReferenceLookups { get; set; } = [];
    public Dictionary<string, string> EntityModNames { get; set; } = [];
    public Dictionary<string, string> EntityNamespaces { get; set; } = [];
    public Dictionary<string, int> EntityMergedIds { get; set; } = [];

    public Dictionary<int, T> GetEntities<T>() where T : IEntity => [];
    public Dictionary<string, T> GetCompositeEntities<T>(Func<T, string> keySelector, int sourceModId = int.MaxValue) where T : IEntity => [];
    public List<T> GetDedupedEntities<T>() where T : IEntity => [];
    public virtual IEntity? FindBestMatch(Type entityType, string rawId, string? targetKey,
        string sourceEntityId = "", string propertyName = "") => null;
}

internal sealed class StubLocalizationService : ILocalizationService
{
    public string this[string key] => key;
    public string this[string key, params object[] args] => key;
    public System.Globalization.CultureInfo CurrentCulture => System.Globalization.CultureInfo.InvariantCulture;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public void SetCulture(System.Globalization.CultureInfo culture) { }
}

internal sealed class StubNotificationService : INotificationService
{
    public void ShowSuccess(string message, string title = "Success") { }
    public void ShowError(string message, string title = "Error") { }
    public void ShowInfo(string message, string title = "Info") { }
    public void ShowWarning(string message, string title = "Warning") { }
}

internal sealed class StubWorkspaceSession : IWorkspaceSession
{
    public int CurrentProfileId { get; set; } = -1;
    public EntityMergeStore? Store => null;
    public EntityMergeStore? ActiveMergeStore { get; private set; }
    public EntityMergeStore? BrowserStore => null;
    public EditTrackingStore? ActiveEditStore => null;
    public ISet<string> DirtyEntities => new HashSet<string>();
    public ReferenceIndexService? ForwardIndex { get; set; }
    public ReferenceIndexService? ReverseIndex { get; set; }

    public event EventHandler? DirtyStateChanged;
    public event EventHandler? StateChanged;

    public void SetActiveStores(EntityMergeStore? mergeStore, EditTrackingStore? editStore)
        => ActiveMergeStore = mergeStore;
    public void SetBrowserStore(EntityMergeStore? store) { }
    public ISet<string> GetDirtyEntities(int profileId) => new HashSet<string>();
    public void UnloadProfile(int profileId) { }
    public void MarkEntityDirty(string entityId) { }
    public void MarkEntitiesDirty(IEnumerable<string> entityIds) { }
    public void ClearDirtyEntities() { }
    public void RemoveDirtyEntities(IEnumerable<string> entityIds) { }
}

internal sealed class StubEntity : IEntity
{
    public StubEntity(string entityId, string? subject = null)
    {
        EntityId = entityId;
        Subject = subject;
    }
    // EntityId, ModId, FilePath, MergedId inherited from IEntity base class
    public new string? Subject { get; set; }
}
