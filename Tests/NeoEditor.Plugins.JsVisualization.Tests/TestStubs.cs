using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Services;

namespace NeoEditor.Plugins.JsVisualization.Tests;

/// <summary>
/// Reusable stubs for JsVisualization plugin tests (R21: independent test project,
/// same stub style as the EntityEditor test project).
/// </summary>

internal class StubReferenceResolver : IReferenceResolver
{
    public Dictionary<string, IEntity> Lookup { get; } = new();

    public virtual T? LookupRef<T>(IEntity sourceEntity, string propertyName, string rawId) where T : IEntity
        => Lookup.TryGetValue(rawId, out var e) ? e as T : null;

    public IEntity? LookupRefByRawId(IEntity sourceEntity, string rawId, Type targetType, EntityMergeStore? storeOverride = null)
        => Lookup.TryGetValue(rawId, out var e) && targetType.IsInstanceOfType(e) ? e : null;
    public string? LookupSubject(string sourceEntityId, string propertyName, Type targetType, string rawId,
        Type? secondaryTargetType = null) => null;
    public string? LookupEntityId(ReferenceIndexService indexService, string entityType, string rawId, string? sourceNs) => null;
    public IReadOnlyList<(string SourceEntityId, string PropertyName, string RawId)> ReverseLookup(
        EntityMergeStore store, string targetEntityId) => [];
    public Task BuildReverseIndexAsync(ReferenceIndexService indexService, EntityMergeStore store) => Task.CompletedTask;
    public List<(Type SourceType, string SourceSubject, string SourceEntityId, string PropName)> ResolveReverseRefs(
        EntityMergeStore store, string targetEntityId) => [];
    public void ClearLookupCache() { }
}

internal class StubNavigationRouter : INavigationRouter
{
    public List<(Type Type, string Id)> Navigated { get; } = new();
    public List<(Type Type, string Id)> Peeked { get; } = new();

    public bool Navigate(Type entityType, string entityId) => false;
    public bool NavigateDataTable(Type entityType, string entityId) => false;
    public void NavigateToEntity(Type entityType, string entityId, IEntity? resolvedEntity = null)
        => Navigated.Add((entityType, entityId));
    public void RequestPeek(Type entityType, string entityId, IEntity? entity)
        => Peeked.Add((entityType, entityId));
    public void Peek(Type entityType, string entityId, IEntity? entity) { }
    public void RegisterTarget(INavigationTarget target) { }
    public void UnregisterTarget(INavigationTarget target) { }
}

internal class StubSelectionService : ISelectionService
{
    public IEntity? CurrentEntity { get; private set; }
    public IEntity? Current => CurrentEntity;
    public event EventHandler<IEntity?>? CurrentEntityChanged { add { } remove { } }
    public event EventHandler<IEntity>? OpenEntityRequested { add { } remove { } }
    public event EventHandler<(Type EntityType, string EntityId)>? NavigateRequested { add { } remove { } }

    public void SetCurrentEntity(IEntity? entity) => CurrentEntity = entity;
    public void RequestOpenEntity(IEntity entity) { }
    public void RequestNavigate(Type entityType, string entityId) { }
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
    public virtual Dictionary<string, T> GetCompositeEntities<T>(Func<T, string> keySelector, int sourceModId = int.MaxValue) where T : IEntity
    {
        // D07 §四: "G.S" composite keys (ItemType group.subgroup) — serve from the
        // ReferenceLookups test data like the real DataTableService does.
        var result = new Dictionary<string, T>();
        if (ReferenceLookups.TryGetValue(typeof(T), out var list))
            foreach (var o in list.OfType<T>())
                result[keySelector(o)] = o;
        return result;
    }
    public List<T> GetDedupedEntities<T>() where T : IEntity => [];
    public IEntity? FindBestMatch(Type entityType, string rawId, string? targetKey,
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

internal sealed class StubXmlParser : IXmlParser
{
    public IList<object> Imported { get; set; } = new List<object>();

    public IList<T> ImportEntities<T>(XDocument doc, int modId, string filePath) where T : IEntity, new()
        => Imported.OfType<T>().ToList();
    public XDocument Export(IEnumerable<IEntity> entities, string databaseName = "neogame")
        => new XDocument(new XElement("database"));
}
