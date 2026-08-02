using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Infra.Tests.Data.Repository;

/// <summary>
/// Minimal <see cref="IHostService"/> stub for repository tests (R26 v2).
/// Records the last executed command, applies the generic cache delta like the real
/// HostService, and keeps an in-memory cache + dirty set so the command-facade CRUD
/// (<c>RepositoryBase{T}</c>) can be asserted end-to-end.
/// </summary>
internal sealed class StubHostService : IHostService
{
    public IEditorCommand? LastCommand { get; private set; }
    public List<IEditorCommand> ExecutedCommands { get; } = new();
    public Dictionary<string, IEntity> Cache { get; } = new();
    public HashSet<string> Dirty { get; } = new();

    public int ActiveProfileId { get; } = 0;

    public void SetActiveProfile(int profileId)
    {
    }

    public ISet<string> DirtyEntities => Dirty;
    public bool HasUnsavedChanges => Dirty.Count > 0;

    public event EventHandler? DirtyStateChanged
    {
        add { }
        remove { }
    }

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

    public async Task<CommandResult> ExecuteAsync(IEditorCommand command, string? scopeId = null)
    {
        LastCommand = command;
        ExecutedCommands.Add(command);
        command.Execute();
        foreach (var (id, entity) in command.GetCacheDelta())
        {
            if (entity is null) Cache.Remove(id);
            else Cache[id] = entity;
        }

        foreach (var id in command.GetAffectedEntityIds()) Dirty.Add(id);
        return new CommandResult(true, null, command.GetAffectedEntityIds().ToArray());
    }

    public Task<CommandResult> ExecuteBatchAsync(IEnumerable<IEditorCommand> commands, string? scopeId = null)
    {
        foreach (var command in commands)
        {
            ExecutedCommands.Add(command);
            command.Execute();
        }

        return Task.FromResult(new CommandResult(true, null, []));
    }

    public Task UndoAsync(string? scopeId = null) => Task.CompletedTask;
    public Task RedoAsync(string? scopeId = null) => Task.CompletedTask;

    public Task<SaveResult> SaveAsync(string? entityId = null) => Task.FromResult(new SaveResult([], []));
    public Task<SaveResult> SaveAllAsync() => Task.FromResult(new SaveResult([], []));
    public Task DiscardAsync(string? entityId = null) => Task.CompletedTask;

    public Task<IReadOnlyList<ExportResult>> ExportModAsync(int modId)
        => Task.FromResult<IReadOnlyList<ExportResult>>([]);

    public Task<IReadOnlyList<ExportResult>> ExportProfileAsync()
        => Task.FromResult<IReadOnlyList<ExportResult>>([]);

    public Task<PublishResult> PublishAsync()
        => Task.FromResult(new PublishResult(new SaveResult([], []), []));

    public Task<IReadOnlyList<DiffEntry>> GetDiffAsync(string? entityId = null)
        => Task.FromResult<IReadOnlyList<DiffEntry>>([]);

    public IObservable<EntityChangedEvent> Changes => null!;

    public IEntityRepository<T> Repository<T>() where T : IEntity => new StubEntityRepository<T>();

    public Task<IReadOnlyList<IEntity>> SearchEntitiesAsync(string query, int limit = 50,
        string? entityType = null, int? modId = null)
    {
        var results = Cache.Values
            .Where(e => (e.Subject ?? e.EntityId ?? "").Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();
        return Task.FromResult<IReadOnlyList<IEntity>>(results);
    }

    public void RegisterEntityCollection(string scopeId, string entityType, System.Collections.IList collection)
    {
    }

    public void UnregisterEntityCollections(string scopeId)
    {
    }

    public IEntity? GetCachedEntity(string entityId) => Cache.TryGetValue(entityId, out var e) ? e : null;
    public IReadOnlyList<IEntity> GetCachedEntitiesByType(string entityType) => Cache.Values.ToList();
    public void AddEntityToCache(IEntity entity) => Cache[entity.EntityId] = entity;
    public void RemoveEntityFromCache(string entityId) => Cache.Remove(entityId);

    public void RegisterCommandScope(string scopeId, ICommandHistory history)
    {
    }

    public void UnregisterCommandScope(string scopeId)
    {
    }

    public void SetActiveScope(string? scopeId)
    {
    }

    public void RegisterPreSaveHook(IExtensionPoint<PreSaveContext> hook)
    {
    }

    public void RegisterPostLoadHook(IExtensionPoint<PostLoadContext> hook)
    {
    }

    public void RegisterPreExecuteHook(IExtensionPoint<PreExecuteContext> hook)
    {
    }

    public void RegisterPreExportHook(IExtensionPoint<PreExportContext> hook)
    {
    }

    private sealed class StubEntityRepository<T> : IEntityRepository<T> where T : IEntity
    {
        public Task<T?> GetByIdAsync(string entityId) => Task.FromResult<T?>(default);

        public Task<IReadOnlyList<T>> GetAllAsync()
            => Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());

        public Task AddAsync(T entity) => Task.CompletedTask;
        public Task UpdateAsync(T entity) => Task.CompletedTask;
        public Task DeleteAsync(string entityId) => Task.CompletedTask;

        public Task<IReadOnlyList<RowDiff>> GetDiffAsync(IReadOnlyList<T> candidates)
            => Task.FromResult<IReadOnlyList<RowDiff>>(Array.Empty<RowDiff>());

        public Task<IReadOnlyList<DiffEntry>> GetFieldDiffAsync(T before, T after)
            => Task.FromResult<IReadOnlyList<DiffEntry>>(Array.Empty<DiffEntry>());

        public IReadOnlyCollection<string> DirtyIds => Array.Empty<string>();

        public void MarkDirty(IEnumerable<string> ids)
        {
        }

        public void ClearDirty(IEnumerable<string> ids)
        {
        }

        public Task SaveAsync(IEnumerable<T> entities) => Task.CompletedTask;

        public Task<IReadOnlyList<T>> LoadAsync()
            => Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());
    }
}