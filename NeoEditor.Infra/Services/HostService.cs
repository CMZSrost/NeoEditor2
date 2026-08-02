using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Data.Repository;
using NeoEditor.Infra.Services;
using Serilog;

namespace NeoEditor.Services;

/// <summary>
/// Central service implementing the unified CRUD pipeline (R24).
/// Wraps CommandHistory (per-scope undo stacks), WorkspaceSession (dirty tracking),
/// entity cache, and GameDbContext (persistence) behind a single interface.
/// DI lifetime: singleton.
/// </summary>
public class HostService : IHostService, IModManager
{
    private readonly IWorkspaceSession _session;
    private readonly IDbContextFactory<GameDbContext> _dbFactory;
    private readonly IXmlParser _xmlParser;
    private readonly IConfigService _configService;
    private readonly IDbContextFactory<EditorDbContext> _editorDbFactory;
    private readonly ModManager _modManager;
    private readonly EntityChangedSubject _changes = new();

    // ── Scope registry (per-tab undo stacks) ──

    private readonly ConcurrentDictionary<string, ICommandHistory> _scopes = new();
    private string? _activeScopeId;

    // ── Entity cache (central in-memory registry) ──

    private readonly ConcurrentDictionary<string, IEntity> _entityCache = new();

    // ── Scope → entity type → ObservableCollection mapping (for Add/Delete dispatch) ──

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, IList>> _scopeCollections = new();

    // ── Extension points (R25) ──

    private readonly List<IExtensionPoint<PreSaveContext>> _preSaveHooks = new();
    private readonly List<IExtensionPoint<PostLoadContext>> _postLoadHooks = new();
    private readonly List<IExtensionPoint<PreExecuteContext>> _preExecuteHooks = new();
    private readonly List<IExtensionPoint<PreExportContext>> _preExportHooks = new();

    public HostService(
        IWorkspaceSession session,
        IDbContextFactory<GameDbContext> dbFactory,
        IXmlParser xmlParser,
        IConfigService configService,
        IDbContextFactory<EditorDbContext> editorDbFactory,
        ModManager modManager)
    {
        _session = session;
        _dbFactory = dbFactory;
        _xmlParser = xmlParser;
        _configService = configService;
        _editorDbFactory = editorDbFactory;
        _modManager = modManager;

        // Restore the persisted active profile so dirty tracking starts in the right scope (R26 §3).
        _session.CurrentProfileId = configService.Config.ActiveProfileId;
    }

    // ──────────────────────────────────────────────
    //  IHostService — 脏追踪 (delegated to IWorkspaceSession, per-profile scope R26 §3)
    // ──────────────────────────────────────────────

    public int ActiveProfileId => _session.CurrentProfileId;
    public void SetActiveProfile(int profileId) => _session.CurrentProfileId = profileId;

    public ISet<string> DirtyEntities => _session.DirtyEntities;
    public bool HasUnsavedChanges => _session.DirtyEntities.Count > 0;

    public event EventHandler? DirtyStateChanged
    {
        add => _session.DirtyStateChanged += value;
        remove => _session.DirtyStateChanged -= value;
    }

    public void MarkEntityDirty(string entityId) => _session.MarkEntityDirty(entityId);
    public void MarkEntitiesDirty(IEnumerable<string> entityIds) => _session.MarkEntitiesDirty(entityIds);
    public void ClearDirtyEntities() => _session.ClearDirtyEntities();
    public void RemoveDirtyEntities(IEnumerable<string> entityIds) => _session.RemoveDirtyEntities(entityIds);

    // ──────────────────────────────────────────────
    //  IHostService — Command 执行
    // ──────────────────────────────────────────────

    public async Task<CommandResult> ExecuteAsync(IEditorCommand command, string? scopeId = null)
    {
        try
        {
            var scope = ResolveScope(scopeId);
            var effectiveScopeId = scopeId ?? _activeScopeId ?? _scopes.Keys.FirstOrDefault();

            // PreExecuteHook (R25) fires before the command executes.
            foreach (var hook in _preExecuteHooks.OrderBy(h => h.Order))
                await hook.ExecuteAsync(new PreExecuteContext(command));

            // Execute: callbacks handle collection management; cache via generic delta (R26 v2)
            command.Execute();
            ApplyCacheDelta(command.GetCacheDelta());

            // Record in scope's undo stack
            if (scope != null)
                scope.Execute(command);

            // Mark dirty + fire event
            var affected = command.GetAffectedEntityIds();
            _session.MarkEntitiesDirty(affected);

            foreach (var eid in affected)
            {
                _changes.OnNext(new EntityChangedEvent(eid, "", ChangeType.Modified));
            }

            return new CommandResult(true, null, affected.ToArray());
        }
        catch (Exception ex)
        {
            return new CommandResult(false, ex.Message, []);
        }
    }

    public async Task<CommandResult> ExecuteBatchAsync(
        IEnumerable<IEditorCommand> commands, string? scopeId = null)
    {
        var cmdList = commands.ToList();
        var allAffected = new HashSet<string>();

        try
        {
            var scope = ResolveScope(scopeId);

            foreach (var command in cmdList)
            {
                // PreExecuteHook (R25) fires before each command executes.
                foreach (var hook in _preExecuteHooks.OrderBy(h => h.Order))
                    await hook.ExecuteAsync(new PreExecuteContext(command));

                command.Execute();
                ApplyCacheDelta(command.GetCacheDelta());

                if (scope != null)
                    scope.Execute(command);

                allAffected.UnionWith(command.GetAffectedEntityIds());
            }

            _session.MarkEntitiesDirty(allAffected);

            foreach (var eid in allAffected)
            {
                _changes.OnNext(new EntityChangedEvent(eid, "", ChangeType.Modified));
            }

            return new CommandResult(true, null, allAffected.ToArray());
        }
        catch (Exception ex)
        {
            return new CommandResult(false, ex.Message, allAffected.ToArray());
        }
    }

    public Task UndoAsync(string? scopeId = null)
    {
        var scope = ResolveScope(scopeId);
        if (scope == null) return Task.CompletedTask;

        var cmd = scope.Undo();
        if (cmd != null)
        {
            ApplyCacheDelta(cmd.GetUndoCacheDelta());
            MarkAffectedDirty(cmd.GetAffectedEntityIds());
        }

        return Task.CompletedTask;
    }

    public Task RedoAsync(string? scopeId = null)
    {
        var scope = ResolveScope(scopeId);
        if (scope == null) return Task.CompletedTask;

        var cmd = scope.Redo();
        if (cmd != null)
        {
            ApplyCacheDelta(cmd.GetCacheDelta());
            MarkAffectedDirty(cmd.GetAffectedEntityIds());
        }

        return Task.CompletedTask;
    }

    // ──────────────────────────────────────────────
    //  IHostService — 持久化
    // ──────────────────────────────────────────────

    public async Task<SaveResult> SaveAsync(string? entityId = null)
    {
        if (entityId != null)
        {
            if (!_session.DirtyEntities.Contains(entityId))
                return new SaveResult([], []);
            return await PersistEntitiesAsync([entityId]);
        }

        return await SaveAllAsync();
    }

    public async Task<SaveResult> SaveAllAsync()
    {
        if (!HasUnsavedChanges) return new SaveResult([], []);
        var dirty = _session.DirtyEntities.ToList();
        return await PersistEntitiesAsync(dirty);
    }

    public Task DiscardAsync(string? entityId = null)
    {
        if (entityId == null)
        {
            _session.ClearDirtyEntities();
        }
        else
        {
            _session.RemoveDirtyEntities([entityId]);
        }

        return Task.CompletedTask;
    }

    // ──────────────────────────────────────────────
    //  IHostService — 导出 (R26: Export = DB → XML)
    // ──────────────────────────────────────────────

    public async Task<IReadOnlyList<ExportResult>> ExportModAsync(int modId)
    {
        // PreExportHook (R25/R26) fires before any export plans are built.
        foreach (var hook in _preExportHooks.OrderBy(h => h.Order))
            await hook.ExecuteAsync(new PreExportContext([$"mod:{modId}"]));

        var files = new List<RowDiff>();
        await using var db = await _dbFactory.CreateDbContextAsync();

        foreach (var (_, entityType) in Constants.GameTypes)
        {
            try
            {
                var setMethod = typeof(GameDbContext).GetMethod(nameof(GameDbContext.Set), Type.EmptyTypes)!
                    .MakeGenericMethod(entityType);
                var dbSet = (IEnumerable)setMethod.Invoke(db, null)!;
                var entities = dbSet.Cast<IEntity>().Where(e => e.ModId == modId).ToList();
                if (entities.Count == 0) continue;

                // Build a concretely-typed list so reflection Invoke matches GetDiffAsync(IReadOnlyList<T>).
                var typedList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entityType))!;
                foreach (var entity in entities) typedList.Add(entity);

                var repo = Activator.CreateInstance(typeof(XmlRepository<>).MakeGenericType(entityType),
                               this, modId, _xmlParser, _configService, _editorDbFactory)
                           ?? throw new InvalidOperationException($"Cannot create XmlRepository<{entityType.Name}>.");

                var diffMethod = typeof(IEntityRepository<>).MakeGenericType(entityType)
                    .GetMethod(nameof(IEntityRepository<IEntity>.GetDiffAsync))!;
                var task = (Task)diffMethod.Invoke(repo, [typedList])!;
                await task;

                if (task.GetType().GetProperty("Result")?.GetValue(task) is IReadOnlyList<RowDiff> typed)
                    files.AddRange(typed);
            }
            catch
            {
                // Skip entity types that don't apply to this mod.
            }
        }

        return files.Count == 0
            ? []
            : [new ExportResult(modId, files, true)];
    }

    public async Task<IReadOnlyList<ExportResult>> ExportProfileAsync()
    {
        var results = new List<ExportResult>();
        await using var edb = await _editorDbFactory.CreateDbContextAsync();
        var modIds = await edb.ModInfos.Where(m => m.ModId >= 0).Select(m => m.ModId).ToListAsync();
        foreach (var modId in modIds)
            results.AddRange(await ExportModAsync(modId));
        return results;
    }

    public async Task<PublishResult> PublishAsync()
    {
        var save = await SaveAllAsync();

        var modIds = new HashSet<int>();
        foreach (var id in save.SavedEntityIds)
            if (_entityCache.TryGetValue(id, out var entity))
                modIds.Add(entity.ModId);

        var exports = new List<ExportResult>();
        foreach (var modId in modIds)
            exports.AddRange(await ExportModAsync(modId));

        return new PublishResult(save, exports);
    }

    // ──────────────────────────────────────────────
    //  IHostService — Diff
    // ──────────────────────────────────────────────

    public async Task<IReadOnlyList<DiffEntry>> GetDiffAsync(string? entityId = null)
    {
        var dirty = entityId != null
            ? _session.DirtyEntities.Where(id => id == entityId).ToList()
            : _session.DirtyEntities.ToList();

        if (dirty.Count == 0) return [];

        var results = new List<DiffEntry>();

        await using var db = await _dbFactory.CreateDbContextAsync();

        foreach (var id in dirty)
        {
            foreach (var entityType in Constants.GameTypes.Values)
            {
                try
                {
                    var dbSet = db.GetDbSet(entityType);
                    var dbEntity = await FindEntityInDbSet(dbSet, entityType, id);
                    if (dbEntity == null) continue;

                    results.Add(new DiffEntry("EntityState", null, "Modified", DiffKind.Modified));
                    break;
                }
                catch
                {
                    // Entity type mismatch, try next
                }
            }
        }

        return results;
    }

    // ──────────────────────────────────────────────
    //  IHostService — 事件
    // ──────────────────────────────────────────────

    public IObservable<EntityChangedEvent> Changes => _changes;

    // ──────────────────────────────────────────────
    //  IHostService — Repository
    // ──────────────────────────────────────────────

    public IEntityRepository<T> Repository<T>() where T : IEntity
    {
        return new DbRepository<T>(this, _dbFactory);
    }

    // ──────────────────────────────────────────────
    //  IHostService — 查询/搜索
    // ──────────────────────────────────────────────

    public async Task<IReadOnlyList<IEntity>> SearchEntitiesAsync(string query, int limit = 50,
        string? entityType = null, int? modId = null)
    {
        if (string.IsNullOrWhiteSpace(query) || limit <= 0)
            return Array.Empty<IEntity>();

        var results = new List<IEntity>();
        foreach (var (typeName, type) in Constants.GameTypes.OrderBy(k => k.Key))
        {
            if (entityType is not null && !typeName.Equals(entityType, StringComparison.OrdinalIgnoreCase))
                continue;
            if (results.Count >= limit)
                break;

            var entities = await GetAllEntitiesAsync(type).ConfigureAwait(false);
            var stringProps = type.GetProperties()
                .Where(p => p.GetIndexParameters().Length == 0 && p.PropertyType == typeof(string))
                .ToArray();

            foreach (var entity in entities)
            {
                if (results.Count >= limit)
                    break;
                if (modId is not null && entity.ModId != modId)
                    continue;
                // Match subject, ID, or any string property (so content-based queries hit).
                if (MatchesQuery(entity, query, stringProps))
                    results.Add(entity);
            }
        }

        return results;
    }

    private static bool MatchesQuery(IEntity entity, string query, PropertyInfo[] stringProps)
    {
        if ($"{entity.Subject} {entity.EntityId}".Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var prop in stringProps)
        {
            if (prop.GetValue(entity) is string s && s.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Reflectively calls <see cref="Repository{T}"/> for a runtime entity type and loads all rows.</summary>
    private async Task<IReadOnlyList<IEntity>> GetAllEntitiesAsync(Type type)
    {
        var getRepo = typeof(IHostService).GetMethod(nameof(Repository))!.MakeGenericMethod(type);
        var repo = getRepo.Invoke(this, null);
        if (repo is null)
            return Array.Empty<IEntity>();

        var getAll = repo.GetType().GetMethod("GetAllAsync");
        if (getAll?.Invoke(repo, null) is not Task task)
            return Array.Empty<IEntity>();

        await task.ConfigureAwait(false);
        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        return result is IEnumerable enumerable
            ? enumerable.Cast<IEntity>().ToList()
            : Array.Empty<IEntity>();
    }

    // ──────────────────────────────────────────────
    //  IHostService — Entity 注册表
    // ──────────────────────────────────────────────

    public void RegisterEntityCollection(string scopeId, string entityType, IList collection)
    {
        var typeDict = _scopeCollections.GetOrAdd(scopeId, _ => new ConcurrentDictionary<string, IList>());
        typeDict[entityType] = collection;
    }

    public void UnregisterEntityCollections(string scopeId)
    {
        _scopeCollections.TryRemove(scopeId, out _);
    }

    public IEntity? GetCachedEntity(string entityId)
    {
        _entityCache.TryGetValue(entityId, out var entity);
        return entity;
    }

    public IReadOnlyList<IEntity> GetCachedEntitiesByType(string entityType)
    {
        return _entityCache.Values
            .Where(e => e.GetType().Name.Equals(entityType, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public void AddEntityToCache(IEntity entity)
    {
        _entityCache[entity.EntityId] = entity;
    }

    public void RemoveEntityFromCache(string entityId)
    {
        _entityCache.TryRemove(entityId, out _);
    }

    // ──────────────────────────────────────────────
    //  IHostService — Scope 管理
    // ──────────────────────────────────────────────

    public void RegisterCommandScope(string scopeId, ICommandHistory history)
    {
        _scopes[scopeId] = history;
    }

    public void UnregisterCommandScope(string scopeId)
    {
        _scopes.TryRemove(scopeId, out _);
        _scopeCollections.TryRemove(scopeId, out _);
        if (_activeScopeId == scopeId)
            _activeScopeId = null;
    }

    public void SetActiveScope(string? scopeId)
    {
        _activeScopeId = scopeId;
    }

    // ──────────────────────────────────────────────
    //  IHostService — Extension Points (R25)
    // ──────────────────────────────────────────────

    public void RegisterPreSaveHook(IExtensionPoint<PreSaveContext> hook)
    {
        _preSaveHooks.Add(hook);
    }

    public void RegisterPostLoadHook(IExtensionPoint<PostLoadContext> hook)
    {
        _postLoadHooks.Add(hook);
    }

    public void RegisterPreExecuteHook(IExtensionPoint<PreExecuteContext> hook)
    {
        _preExecuteHooks.Add(hook);
    }

    public void RegisterPreExportHook(IExtensionPoint<PreExportContext> hook)
    {
        _preExportHooks.Add(hook);
    }

    // ──────────────────────────────────────────────
    //  IModManager — mod file-system + DB orchestration (B5, R24: all writes flow through HostService)
    // ──────────────────────────────────────────────

    public Task<ModInfo?> ImportModAsync(string modFullPath, int? modId = null)
        => _modManager.ImportModAsync(modFullPath, modId);

    public Task LoadModAsync(ModInfo modInfo)
        => _modManager.LoadModAsync(modInfo);

    public Task CreateModAsync(string name, string author)
        => _modManager.CreateModAsync(name, author);

    public Task DeleteMod(string name, string author)
        => _modManager.DeleteMod(name, author);

    public Task DeleteMod(ModInfo modInfo)
        => _modManager.DeleteMod(modInfo);

    public Task DeleteMod(string modPath)
        => _modManager.DeleteMod(modPath);

    public Task ExportModToZipAsync(ModInfo modInfo, string outputPath)
        => _modManager.ExportModToZipAsync(modInfo, outputPath);

    public Task<ModInfo> ImportModFromZipAsync(string zipPath)
        => _modManager.ImportModFromZipAsync(zipPath);

    // ──────────────────────────────────────────────
    //  Private helpers
    // ──────────────────────────────────────────────

    private ICommandHistory? ResolveScope(string? scopeId)
    {
        if (scopeId != null && _scopes.TryGetValue(scopeId, out var h))
            return h;
        if (_activeScopeId != null && _scopes.TryGetValue(_activeScopeId, out var active))
            return active;
        return _scopes.Values.FirstOrDefault();
    }

    /// <summary>Mark affected entities dirty and emit change events (used by undo/redo).</summary>
    private void MarkAffectedDirty(IReadOnlySet<string> affected)
    {
        _session.MarkEntitiesDirty(affected);
        foreach (var eid in affected)
            _changes.OnNext(new EntityChangedEvent(eid, "", ChangeType.Modified));
    }

    /// <summary>
    /// Apply a command's working-set cache delta (R26 v2): a null value removes the entity id,
    /// a non-null value upserts it into the central cache. Replaces the former
    /// <c>is AddEntityCommand</c> / <c>is DeleteEntityCommand</c> type-dispatch.
    /// </summary>
    private void ApplyCacheDelta(IReadOnlyDictionary<string, IEntity?> delta)
    {
        foreach (var (id, entity) in delta)
        {
            if (entity is null) _entityCache.TryRemove(id, out _);
            else _entityCache[id] = entity;
        }
    }

    private void DispatchAddToScopeCollections(string? scopeId, string entityType, IEntity entity)
    {
        if (scopeId == null) return;
        if (!_scopeCollections.TryGetValue(scopeId, out var typeDict)) return;
        if (!typeDict.TryGetValue(entityType, out var collection)) return;
        collection.Add(entity);
    }

    private void DispatchRemoveFromScopeCollections(string? scopeId, string entityType, IEntity entity)
    {
        if (scopeId == null) return;
        if (!_scopeCollections.TryGetValue(scopeId, out var typeDict)) return;
        if (!typeDict.TryGetValue(entityType, out var collection)) return;
        collection.Remove(entity);
    }

    private async Task<SaveResult> PersistEntitiesAsync(IReadOnlyList<string> entityIds)
    {
        var entities = new List<IEntity>();
        foreach (var id in entityIds)
            if (_entityCache.TryGetValue(id, out var entity))
                entities.Add(entity);

        if (entities.Count == 0)
        {
            // R30 (追修 6): a dirty entity that never entered the working-set cache cannot be
            // saved. This used to happen silently (edit commands carried no cache delta), so
            // every save reported "No mod entities to save", the WAL was never cleared, and
            // the same commands replayed (re-dirtying) on every restart.
            Log.Warning("[Save] {Count} dirty entity/entities missing from entity cache — NOT saved: [{Ids}]",
                entityIds.Count, string.Join(",", entityIds.Take(10)));
            _session.RemoveDirtyEntities(entityIds);
            return new SaveResult([], []);
        }

        // PreSaveHook (R25) fires before entities are persisted.
        foreach (var hook in _preSaveHooks.OrderBy(h => h.Order))
            await hook.ExecuteAsync(new PreSaveContext(entityIds));

        var savedIds = new List<string>();
        var diff = new List<DiffEntry>();

        foreach (var group in entities.GroupBy(e => e.GetType()))
        {
            var entityType = group.Key;
            var repo = Activator.CreateInstance(typeof(DbRepository<>).MakeGenericType(entityType), this, _dbFactory)
                       ?? throw new InvalidOperationException($"Cannot create DbRepository<{entityType.Name}>.");

            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(entityType))!;
            foreach (var entity in group) list.Add(entity);

            var saveMethod = typeof(IEntityRepository<>).MakeGenericType(entityType)
                .GetMethod(nameof(IEntityRepository<IEntity>.SaveAsync))!;
            await (Task)saveMethod.Invoke(repo, [list])!;

            savedIds.AddRange(group.Select(e => e.EntityId));
            diff.AddRange(group.Select(e =>
                new DiffEntry("EntityState", null, "Modified", DiffKind.Modified)));
        }

        _session.RemoveDirtyEntities(savedIds);
        return new SaveResult(diff, savedIds);
    }

    private static async Task<IEntity?> FindEntityInDbSet(
        IQueryable dbSet,
        Type entityType,
        string entityId)
    {
        var method = dbSet.GetType().GetMethod("FindAsync",
            [typeof(object[])]);
        if (method == null) return null;

        var task = (Task)method.Invoke(dbSet, [new object[] { entityId }])!;
        await task;

        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task) as IEntity;
    }

    /// <summary>
    /// Minimal IObservable{T} implementation that wraps an event.
    /// No System.Reactive dependency needed.
    /// </summary>
    private class EntityChangedSubject : IObservable<EntityChangedEvent>
    {
        private event Action<EntityChangedEvent>? _handlers;

        public IDisposable Subscribe(IObserver<EntityChangedEvent> observer)
        {
            _handlers += observer.OnNext;
            return new Subscription(() => _handlers -= observer.OnNext);
        }

        public void OnNext(EntityChangedEvent value) => _handlers?.Invoke(value);

        private class Subscription(Action unsubscribe) : IDisposable
        {
            private Action? _unsubscribe = unsubscribe;
            public void Dispose() => _unsubscribe?.Invoke();
        }
    }
}