using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Services;
// The Infra workspace session is the full interface used by these stubs.
using IWorkspaceSession = NeoEditor.Services.IWorkspaceSession;

namespace NeoEditor.Plugins.EntityEditor.Tests;

/// <summary>
/// Reusable stub implementations for EntityEditor Plugin tests.
/// </summary>

internal class StubReferenceResolver : IReferenceResolver
{
    public IEntity? LookupRefByRawId(IEntity sourceEntity, string rawId, Type targetType, EntityMergeStore? storeOverride = null) => null;
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
    public ISet<string> DirtyEntities { get; } = new HashSet<string>();
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

/// <summary>
/// Minimal <see cref="IHostService"/> stub — enough for factory/document tests that
/// construct EntityEditorDocument without exercising SaveDocument.
/// </summary>
internal sealed class StubHostService : IHostService
{
    public Dictionary<string, IEntity> Cache { get; } = new();
    public HashSet<string> Dirty { get; } = new();

    public int ActiveProfileId => 0;
    public void SetActiveProfile(int profileId) { }
    public ISet<string> DirtyEntities => Dirty;
    public bool HasUnsavedChanges => Dirty.Count > 0;
    public event EventHandler? DirtyStateChanged { add { } remove { } }
    public void MarkEntityDirty(string entityId) => Dirty.Add(entityId);
    public void MarkEntitiesDirty(IEnumerable<string> entityIds)
    {
        foreach (var id in entityIds) Dirty.Add(id);
    }
    public void ClearDirtyEntities() => Dirty.Clear();
    public void RemoveDirtyEntities(IEnumerable<string> entityIds)
    {
        foreach (var id in entityIds) Dirty.Remove(id);
    }

    public Task<CommandResult> ExecuteAsync(IEditorCommand command, string? scopeId = null)
    {
        command.Execute();
        foreach (var (id, entity) in command.GetCacheDelta())
        {
            if (entity is null) Cache.Remove(id);
            else Cache[id] = entity;
        }
        foreach (var id in command.GetAffectedEntityIds()) Dirty.Add(id);
        return Task.FromResult(new CommandResult(true, null, command.GetAffectedEntityIds().ToArray()));
    }

    public Task<CommandResult> ExecuteBatchAsync(IEnumerable<IEditorCommand> commands, string? scopeId = null)
    {
        foreach (var command in commands)
        {
            command.Execute();
            foreach (var (id, entity) in command.GetCacheDelta())
            {
                if (entity is null) Cache.Remove(id);
                else Cache[id] = entity;
            }
            foreach (var id in command.GetAffectedEntityIds()) Dirty.Add(id);
        }
        return Task.FromResult(new CommandResult(true, null, []));
    }

    public Task UndoAsync(string? scopeId = null) => Task.CompletedTask;
    public Task RedoAsync(string? scopeId = null) => Task.CompletedTask;

    public Task<SaveResult> SaveAsync(string? entityId = null)
    {
        var saved = new List<string>();
        if (entityId != null && Dirty.Contains(entityId) && Cache.TryGetValue(entityId, out _))
        {
            Dirty.Remove(entityId);
            saved.Add(entityId);
        }
        return Task.FromResult(new SaveResult([], saved));
    }

    public Task<SaveResult> SaveAllAsync() => Task.FromResult(new SaveResult([], []));
    public Task DiscardAsync(string? entityId = null) => Task.CompletedTask;
    public Task<IReadOnlyList<ExportResult>> ExportModAsync(int modId)
        => Task.FromResult<IReadOnlyList<ExportResult>>([]);
    public Task<IReadOnlyList<ExportResult>> ExportProfileAsync()
        => Task.FromResult<IReadOnlyList<ExportResult>>([]);
    public Task CommitExportAsync(IEnumerable<RowDiff> diffs) => Task.CompletedTask;
    public Task AdvanceBaselineAsync(IReadOnlyList<string> entityIds) => Task.CompletedTask;
    public IReadOnlyList<IEntity> MergeProfileOverlay(IEnumerable<IEntity> baselineEntities) => baselineEntities.ToList();
    public Task<PublishResult> PublishAsync()
        => Task.FromResult(new PublishResult(new SaveResult([], []), []));
    public Task<IReadOnlyList<DiffEntry>> GetDiffAsync(string? entityId = null)
        => Task.FromResult<IReadOnlyList<DiffEntry>>([]);
    public IObservable<EntityChangedEvent> Changes => null!;
    public IEntityRepository<T> Repository<T>() where T : IEntity => throw new NotSupportedException();
    public Task<IReadOnlyList<IEntity>> SearchEntitiesAsync(string query, int limit = 50,
        string? entityType = null, int? modId = null)
        => Task.FromResult<IReadOnlyList<IEntity>>([]);
    public void RegisterEntityCollection(string scopeId, string entityType, System.Collections.IList collection) { }
    public void UnregisterEntityCollections(string scopeId) { }
    public IEntity? GetCachedEntity(string entityId) => Cache.TryGetValue(entityId, out var e) ? e : null;
    public IReadOnlyList<IEntity> GetCachedEntitiesByType(string entityType) => Cache.Values.ToList();
    public void AddEntityToCache(IEntity entity) => Cache[entity.EntityId] = entity;
    public void RemoveEntityFromCache(string entityId) => Cache.Remove(entityId);
    public void RegisterCommandScope(string scopeId, ICommandHistory history) { }
    public void UnregisterCommandScope(string scopeId) { }
    public void SetActiveScope(string? scopeId) { }
    public void RegisterPreSaveHook(IExtensionPoint<PreSaveContext> hook) { }
    public void RegisterPostLoadHook(IExtensionPoint<PostLoadContext> hook) { }
    public void RegisterPreExecuteHook(IExtensionPoint<PreExecuteContext> hook) { }
    public void RegisterPreExportHook(IExtensionPoint<PreExportContext> hook) { }
}

/// <summary>Minimal IXmlParser stub — the XML-diff original lookup is never exercised by
/// these tests (entities are not dirty at construction, so the fast path is taken).</summary>
internal sealed class StubXmlParser : IXmlParser
{
    public System.Collections.Generic.IList<T> ImportEntities<T>(System.Xml.Linq.XDocument doc, int modId, string filePath)
        where T : IEntity, new() => throw new NotSupportedException();
    public System.Xml.Linq.XDocument Export(System.Collections.Generic.IEnumerable<IEntity> entities, string databaseName = "neogame")
        => throw new NotSupportedException();
}

/// <summary>Minimal IConfigService stub for document construction.</summary>
internal sealed class StubConfigService : IConfigService
{
    public NeoEditor.Core.Model.AppConfig Config { get; } = new();
    public Task LoadAsync() => Task.CompletedTask;
    public Task SaveAsync() => Task.CompletedTask;
}
