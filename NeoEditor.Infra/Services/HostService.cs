using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Data;
using NeoEditor.Data.Command;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Data.Repository;
using NeoEditor.Helper;
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
    private readonly IWorkspacePersistenceService _workspacePersistence;
    private readonly EntityChangedSubject _changes = new();

    // ── Scope registry (per-tab undo stacks) ──

    private readonly ConcurrentDictionary<string, ICommandHistory> _scopes = new();
    private string? _activeScopeId;

    // ── Entity cache (central in-memory registry) ──

    private readonly ConcurrentDictionary<string, IEntity> _entityCache = new();

    /// <summary>追修(C): entities deleted this session — entityId → (typeName, modId).
    /// The entity leaves the cache, so the overlay needs this to persist IsDeleted markers.</summary>
    private readonly Dictionary<string, (string TypeName, int ModId)> _pendingDeletes = new();

    /// <summary>追修(C): current profile's overlay cache for ExportModAsync merging.</summary>
    private List<ProfileEdit> _overlayCache = [];

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
        ModManager modManager,
        IWorkspacePersistenceService workspacePersistence)
    {
        _session = session;
        _dbFactory = dbFactory;
        _xmlParser = xmlParser;
        _configService = configService;
        _editorDbFactory = editorDbFactory;
        _modManager = modManager;
        _workspacePersistence = workspacePersistence;

        // Restore the persisted active profile so dirty tracking starts in the right scope (R26 §3).
        _session.CurrentProfileId = configService.Config.ActiveProfileId;
    }

    // ──────────────────────────────────────────────
    //  IHostService — 脏追踪 (delegated to IWorkspaceSession, per-profile scope R26 §3)
    // ──────────────────────────────────────────────

    public int ActiveProfileId => _session.CurrentProfileId;
    public void SetActiveProfile(int profileId)
    {
        _session.CurrentProfileId = profileId;
        // 追修(C): refresh the overlay cache used by ExportModAsync (baseline + overlay).
        _ = RefreshOverlayCacheAsync(profileId);
    }

    private Task RefreshOverlayCacheAsync(int profileId)
        => Task.Run(async () =>
        {
            try
            {
                _overlayCache = await _workspacePersistence.GetProfileEditsAsync(profileId);
            }
            catch
            {
                _overlayCache = [];
            }
        });

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

            // Execute once: scope.Execute runs the command and records it in the undo stack
            // (CommandHistory.Execute calls command.Execute() internally). When no scope is
            // registered, execute manually without recording.
            if (scope != null)
                scope.Execute(command);
            else
                command.Execute();

            // Cache via generic delta (R26 v2)
            ApplyCacheDelta(command.GetCacheDelta());

            // 追修(C): remember deletes — the entity leaves the working-set cache, so
            // SaveAllAsync cannot see it; the overlay needs its type/mod to persist IsDeleted.
            if (command is DeleteEntityCommand del)
                _pendingDeletes[del.Entity.EntityId] = (del.EntityType, del.Entity.ModId);

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

                // Execute once (see ExecuteAsync: scope.Execute runs the command itself).
                if (scope != null)
                    scope.Execute(command);
                else
                    command.Execute();

                ApplyCacheDelta(command.GetCacheDelta());

                // 追修(C): remember deletes (see ExecuteAsync).
                if (command is DeleteEntityCommand del)
                    _pendingDeletes[del.Entity.EntityId] = (del.EntityType, del.Entity.ModId);

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
            // 追修(C): undoing a delete resurrects the entity — drop its IsDeleted marker.
            if (cmd is DeleteEntityCommand del)
                _pendingDeletes.Remove(del.Entity.EntityId);
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
        // 追修(C): discard must also drop the profile overlay — otherwise the edit
        // resurrects on the next load (overlay is the persisted edit, dirty is only memory).
        if (entityId == null)
        {
            _session.ClearDirtyEntities();
            var overlay = _overlayCache;
            if (overlay.Count > 0)
                return _workspacePersistence.ClearProfileEditsAsync(ActiveProfileId,
                    overlay.Select(o => o.EntityId));
        }
        else
        {
            _session.RemoveDirtyEntities([entityId]);
            return _workspacePersistence.ClearProfileEditsAsync(ActiveProfileId, [entityId]);
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

                // Docs/41 追修(C): the shared tables are the BASELINE — merge the current
                // profile's overlay so the export reflects its edits (other profiles' edits
                // stay in their own overlays and never leak into this export).
                entities = MergeProfileOverlay(entities).Where(e => e.ModId == modId).ToList();
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

    public async Task CommitExportAsync(IEnumerable<RowDiff> diffs)
    {
        foreach (var diff in diffs)
        {
            if (string.IsNullOrEmpty(diff.NewContent)) continue;
            var dir = Path.GetDirectoryName(diff.TargetId);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(diff.TargetId, diff.NewContent, new UTF8Encoding(false));
            Log.Information("[Export] wrote {Path}", diff.TargetId);
        }
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
            // In-memory version (edited, may be absent when the entity was deleted but not yet saved).
            _entityCache.TryGetValue(id, out var cached);

            // Stored version from the DB (absent when the entity is newly added).
            IEntity? dbEntity = null;
            foreach (var entityType in Constants.GameTypes.Values)
            {
                try
                {
                    var dbSet = db.GetDbSet(entityType);
                    var found = await FindEntityInDbSet(dbSet, entityType, id);
                    if (found != null)
                    {
                        dbEntity = found;
                        break;
                    }
                }
                catch
                {
                    // Entity type mismatch, try next
                }
            }

            if (dbEntity == null && cached == null) continue;

            // Field-level diff: Modified (both), Added (new entity), Removed (deleted entity).
            results.AddRange(DiffEngine.ComputeDiff(dbEntity, cached));
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

            foreach (var entity in entities)
            {
                if (results.Count >= limit)
                    break;
                if (modId is not null && entity.ModId != modId)
                    continue;
                // Match subject, ID, or any string property (so content-based queries hit).
                if (MatchesQuery(entity, query))
                    results.Add(entity);
            }
        }

        return results;
    }

    public async Task<EntitySearchResult> SearchEntitiesAsync(EntitySearchRequest request)
    {
        if (request.Limit <= 0)
            return new EntitySearchResult([], 0, false);

        var typeNames = request.EntityTypes is { Count: > 0 }
            ? request.EntityTypes.Select(t => t.Trim()).Where(t => t.Length > 0).ToList()
            : null;

        var results = new List<IEntity>();
        foreach (var (typeName, type) in Constants.GameTypes.OrderBy(k => k.Key))
        {
            if (typeNames is not null &&
                !typeNames.Any(t => t.Equals(typeName, StringComparison.OrdinalIgnoreCase)))
                continue;

            var entities = await GetAllEntitiesAsync(type).ConfigureAwait(false);
            foreach (var entity in entities)
            {
                if (request.ModId is not null && entity.ModId != request.ModId)
                    continue;
                if (!string.IsNullOrWhiteSpace(request.Query) && !MatchesQuery(entity, request.Query))
                    continue;
                if (request.Filters is { Count: > 0 } && !MatchesFilters(entity, request.Filters))
                    continue;
                results.Add(entity);
            }
        }

        // Column sort (typed where possible, nulls last)
        if (!string.IsNullOrWhiteSpace(request.SortBy))
        {
            var sortBy = request.SortBy.Trim();
            results.Sort((a, b) =>
            {
                var cmp = CompareSortValues(GetSortValue(a, sortBy), GetSortValue(b, sortBy));
                return request.SortDescending ? -cmp : cmp;
            });
        }

        var total = results.Count;
        var page = results.Skip(request.Offset).Take(request.Limit).ToList();
        return new EntitySearchResult(page, total, request.Offset + page.Count < total);
    }

    // ── Search helpers ──

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> StringSearchPropsCache = new();

    private static PropertyInfo[] GetStringSearchProps(Type type)
        => StringSearchPropsCache.GetOrAdd(type, t => t.GetProperties()
            .Where(p => p.GetIndexParameters().Length == 0 && p.PropertyType == typeof(string))
            .ToArray());

    private static bool MatchesQuery(IEntity entity, string query)
    {
        if ($"{entity.Subject} {entity.EntityId}".Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var prop in GetStringSearchProps(entity.GetType()))
        {
            if (prop.GetValue(entity) is string s && s.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Resolve a filter/sort field to a property: [Column]-attributed properties first
    /// (column name or C# name, case-insensitive, via FilterService), then IEntity base
    /// properties (Subject, EntityId, ModId, ...).
    /// </summary>
    private static PropertyInfo? FindSearchProperty(Type type, string field)
    {
        var prop = FilterService.FindColumnProperty(type, field);
        if (prop is not null) return prop;

        return typeof(IEntity).GetProperties()
            .FirstOrDefault(p => p.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>All filters must pass (AND semantics); an unknown field fails the entity.</summary>
    private static bool MatchesFilters(IEntity entity, IReadOnlyList<EntityFilter> filters)
    {
        foreach (var filter in filters)
        {
            var prop = FindSearchProperty(entity.GetType(), filter.Field);
            if (prop is null) return false;
            if (!MatchesFilterValue(prop.GetValue(entity), prop, filter))
                return false;
        }

        return true;
    }

    private static bool MatchesFilterValue(object? value, PropertyInfo prop, EntityFilter filter)
    {
        // Reference fields (ReferenceList) compare as canonical raw text.
        var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>();
        if (value is ReferenceList<IReferenceEntry> || refAttr is not null)
            return CompareString(ReferenceText.GetRawString(value, refAttr), filter);

        return value switch
        {
            null => CompareString("", filter),
            string s => CompareString(s, filter),
            bool b => CompareBool(b, filter),
            int i => CompareNumeric(i, filter),
            long l => CompareNumeric(l, filter),
            float f => CompareNumeric(f, filter),
            double d => CompareNumeric(d, filter),
            decimal m => CompareNumeric((double)m, filter),
            Enum e => CompareEnum(e, filter),
            _ => CompareString(value.ToString() ?? "", filter)
        };
    }

    /// <summary>Enum filters accept the member name ("Ranged") or its numeric value.</summary>
    private static bool CompareEnum(Enum value, EntityFilter filter)
    {
        if (!Enum.TryParse(value.GetType(), filter.Value, ignoreCase: true, out var parsed))
            return filter.Operator == FilterOperator.NotEquals; // unparseable value can never equal

        var cmp = Convert.ToInt64(value).CompareTo(Convert.ToInt64(parsed));
        return filter.Operator switch
        {
            FilterOperator.Equals => cmp == 0,
            FilterOperator.NotEquals => cmp != 0,
            _ => false
        };
    }

    private static bool CompareString(string value, EntityFilter filter)
    {
        return filter.Operator switch
        {
            FilterOperator.Contains => value.Contains(filter.Value, StringComparison.OrdinalIgnoreCase),
            FilterOperator.Equals => value.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
            FilterOperator.NotEquals => !value.Equals(filter.Value, StringComparison.OrdinalIgnoreCase),
            FilterOperator.StartsWith => value.StartsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
            FilterOperator.EndsWith => value.EndsWith(filter.Value, StringComparison.OrdinalIgnoreCase),
            _ => false // relational operators do not apply to strings
        };
    }

    private static bool CompareNumeric(double value, EntityFilter filter)
    {
        if (!double.TryParse(filter.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return filter.Operator == FilterOperator.NotEquals; // unparseable value can never equal

        return filter.Operator switch
        {
            FilterOperator.Equals => value == parsed,
            FilterOperator.NotEquals => value != parsed,
            FilterOperator.GreaterThan => value > parsed,
            FilterOperator.GreaterThanOrEqual => value >= parsed,
            FilterOperator.LessThan => value < parsed,
            FilterOperator.LessThanOrEqual => value <= parsed,
            _ => false
        };
    }

    private static bool CompareBool(bool value, EntityFilter filter)
    {
        if (!bool.TryParse(filter.Value, out var parsed))
            return false;

        return filter.Operator switch
        {
            FilterOperator.Equals => value == parsed,
            FilterOperator.NotEquals => value != parsed,
            _ => false
        };
    }

    private static object? GetSortValue(IEntity entity, string field)
    {
        var prop = FindSearchProperty(entity.GetType(), field);
        if (prop is null) return null;

        var value = prop.GetValue(entity);
        return value is ReferenceList<IReferenceEntry> rl
            ? rl.ToRawString(prop.GetCustomAttribute<ReferenceFieldAttribute>()?.Separator)
            : value;
    }

    private static int CompareSortValues(object? a, object? b)
    {
        if (a is null && b is null) return 0;
        if (a is null) return 1; // nulls sort last
        if (b is null) return -1;

        if (a is IComparable ca && b is IComparable cb && ca.GetType() == cb.GetType())
            return ca.CompareTo(cb);

        return string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
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
        if (result is not IEnumerable enumerable)
            return Array.Empty<IEntity>();

        // 追修(C): search must see the current profile's edits — merge the overlay.
        return MergeProfileOverlay(enumerable.Cast<IEntity>());
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
        var profileId = _session.CurrentProfileId;
        var entities = new List<IEntity>();
        foreach (var id in entityIds)
            if (_entityCache.TryGetValue(id, out var entity))
                entities.Add(entity);

        // Deleted entities left the cache — remember their type/mod via _pendingDeletes.
        var deletes = entityIds
            .Where(id => !_entityCache.ContainsKey(id))
            .Where(_pendingDeletes.ContainsKey)
            .Select(id => (Id: id, Info: _pendingDeletes[id]))
            .ToList();

        if (entities.Count == 0 && deletes.Count == 0)
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

        // ── Docs/41 追修(C): persist to the PROFILE EDIT OVERLAY, not the shared entity
        // tables. The tables stay the baseline (import/export only); the overlay holds this
        // profile's per-column overrides + new/deleted markers — two profiles editing the
        // same entity never overwrite each other. Loading merges baseline + overlay. ──
        var overlay = new List<ProfileEdit>();
        var savedIds = new List<string>();
        var diff = new List<DiffEntry>();

        await using var db = await _dbFactory.CreateDbContextAsync();

        foreach (var entity in entities)
        {
            var baseline = await LoadBaselineAsync(db, entity);
            if (baseline is null)
            {
                // Created in this profile — whole-entity marker + all column values.
                overlay.Add(new ProfileEdit
                {
                    EntityId = entity.EntityId,
                    IsNew = true,
                    EntityType = entity.GetType().Name,
                    ModId = entity.ModId,
                });
                foreach (var (colName, raw) in SerializeAllColumns(entity))
                    overlay.Add(new ProfileEdit { EntityId = entity.EntityId, ColumnName = colName, RawValue = raw });
            }
            else
            {
                // Diff against the shared baseline → only the truly changed columns.
                var columns = DiffEngine.ComputeChangedColumns(baseline, entity);
                foreach (var colName in columns)
                {
                    var prop = entity.GetType().GetProperties().FirstOrDefault(p =>
                        p.GetCustomAttribute<ColumnAttribute>()?.Name == colName);
                    if (prop is null || !prop.CanWrite) continue;
                    overlay.Add(new ProfileEdit
                    {
                        EntityId = entity.EntityId,
                        ColumnName = colName,
                        RawValue = SerializeValue(entity, prop),
                    });
                }
            }

            savedIds.Add(entity.EntityId);
            diff.Add(new DiffEntry("EntityState", null, "Modified", DiffKind.Modified));
        }

        foreach (var (id, info) in deletes)
        {
            overlay.Add(new ProfileEdit
            {
                EntityId = id,
                IsDeleted = true,
                EntityType = info.TypeName,
                ModId = info.ModId,
            });
            savedIds.Add(id);
            diff.Add(new DiffEntry("EntityState", null, "Deleted", DiffKind.Removed));
            _pendingDeletes.Remove(id);
        }

        if (overlay.Count > 0 && profileId >= 0)
            await _workspacePersistence.ReplaceProfileEditsAsync(profileId, overlay);

        _session.RemoveDirtyEntities(savedIds);
        return new SaveResult(diff, savedIds);
    }

    /// <inheritdoc cref="IHostService.AdvanceBaselineAsync"/>
    public async Task AdvanceBaselineAsync(IReadOnlyList<string> entityIds)
    {
        var ids = entityIds.ToList();
        if (ids.Count == 0) return;

        // Upsert the exported state into the shared entity tables (baseline advance).
        var entities = new List<IEntity>();
        foreach (var id in ids)
            if (_entityCache.TryGetValue(id, out var entity))
                entities.Add(entity);

        foreach (var group in entities.GroupBy(e => e.GetType()))
        {
            var repo = Activator.CreateInstance(typeof(DbRepository<>).MakeGenericType(group.Key),
                           this, _dbFactory)
                       ?? throw new InvalidOperationException($"Cannot create DbRepository<{group.Key.Name}>.");
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(group.Key))!;
            foreach (var entity in group) list.Add(entity);
            var saveMethod = typeof(IEntityRepository<>).MakeGenericType(group.Key)
                .GetMethod(nameof(IEntityRepository<IEntity>.SaveAsync))!;
            await (Task)saveMethod.Invoke(repo, [list])!;
        }

        // Deleted entities leave the tables too (their overlay IsDeleted rows carry the type).
        var deletes = (await _workspacePersistence.GetProfileEditsAsync(ActiveProfileId))
            .Where(e => e.IsDeleted && ids.Contains(e.EntityId))
            .ToList();
        if (deletes.Count == 0) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        foreach (var del in deletes)
        {
            if (string.IsNullOrWhiteSpace(del.EntityType)) continue;
            if (!Constants.GameTypes.TryGetValue(del.EntityType, out var type)) continue;
            var setMethod = typeof(GameDbContext).GetMethod(nameof(GameDbContext.Set), Type.EmptyTypes)!
                .MakeGenericMethod(type);
            var dbSet = (IQueryable)setMethod.Invoke(db, null)!;
            var baseline = await FindEntityInDbSet(dbSet, type, del.EntityId);
            if (baseline is null) continue;
            var removeMethod = dbSet.GetType().GetMethod("Remove",
                [typeof(IEntity)]) ?? dbSet.GetType().GetMethod("Remove");
            removeMethod?.Invoke(dbSet, [baseline]);
        }
        await db.SaveChangesAsync();
    }

    /// <inheritdoc cref="IHostService.MergeProfileOverlay"/>
    public IReadOnlyList<IEntity> MergeProfileOverlay(IEnumerable<IEntity> baselineEntities)
    {
        var overlay = _overlayCache;
        if (overlay.Count == 0) return baselineEntities.ToList();

        var result = baselineEntities.ToList();
        var byId = result.ToDictionary(e => e.EntityId);

        foreach (var row in overlay.Where(o => o.ColumnName is not null))
        {
            if (byId.TryGetValue(row.EntityId, out var e))
                ApplyOverlayRow(e, row);
        }

        foreach (var marker in overlay.Where(o => o.IsNew && o.ColumnName is null))
        {
            if (string.IsNullOrWhiteSpace(marker.EntityType)) continue;
            if (!Constants.GameTypes.TryGetValue(marker.EntityType, out var type)) continue;
            var entity = (IEntity)Activator.CreateInstance(type)!;
            entity.EntityId = marker.EntityId;
            entity.ModId = marker.ModId;
            foreach (var row in overlay.Where(o => o.EntityId == marker.EntityId && o.ColumnName is not null))
                ApplyOverlayRow(entity, row);
            result.Add(entity);
        }

        result.RemoveAll(e => overlay.Any(o => o.IsDeleted && o.EntityId == e.EntityId));
        return result;
    }

    /// <summary>Apply one raw-text overlay row to an entity's [Column] property.</summary>
    private static void ApplyOverlayRow(IEntity entity, ProfileEdit row)
    {
        if (row.ColumnName is null) return;
        var prop = entity.GetType().GetProperties().FirstOrDefault(p =>
            p.GetCustomAttribute<ColumnAttribute>()?.Name == row.ColumnName);
        if (prop is null || !prop.CanWrite) return;
        try
        {
            var raw = row.RawValue ?? "";
            object? value = raw;
            if (prop.PropertyType == typeof(int)) value = int.Parse(raw);
            else if (prop.PropertyType == typeof(long)) value = long.Parse(raw);
            else if (prop.PropertyType == typeof(float)) value = float.Parse(raw);
            else if (prop.PropertyType == typeof(double)) value = double.Parse(raw);
            else if (prop.PropertyType == typeof(bool)) value = bool.Parse(raw);
            else if (prop.PropertyType.IsEnum) value = Enum.ToObject(prop.PropertyType, int.Parse(raw));
            else if (prop.PropertyType == typeof(ReferenceList<IReferenceEntry>))
                value = new ReferenceListSerializer()
                    .Deserialize(raw, prop.GetCustomAttribute<ReferenceFieldAttribute>());
            prop.SetValue(entity, value);
        }
        catch
        {
            // Skip unparseable overlay values — export proceeds with the baseline value.
        }
    }

    /// <summary>Load the shared baseline (entity table) row for an entity, if any.</summary>
    private static async Task<IEntity?> LoadBaselineAsync(GameDbContext db, IEntity entity)
    {
        var setMethod = typeof(GameDbContext).GetMethod(nameof(GameDbContext.Set), Type.EmptyTypes)!
            .MakeGenericMethod(entity.GetType());
        var dbSet = (IQueryable)setMethod.Invoke(db, null)!;
        return await FindEntityInDbSet(dbSet, entity.GetType(), entity.EntityId);
    }

    /// <summary>Serialize every [Column] property (excluding IEntity metadata) as raw text.</summary>
    private static IEnumerable<(string ColumnName, string? RawValue)> SerializeAllColumns(IEntity entity)
    {
        foreach (var prop in entity.GetType().GetProperties())
        {
            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            if (colAttr is null || !prop.CanWrite) continue;
            if (prop.DeclaringType == typeof(IEntity)) continue; // EntityId/ModId/FilePath metadata
            yield return (colAttr.Name ?? prop.Name, SerializeValue(entity, prop));
        }
    }

    private static string? SerializeValue(IEntity entity, PropertyInfo prop)
    {
        var val = prop.GetValue(entity);
        if (val is null) return null;
        var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>();
        return val is ReferenceList<IReferenceEntry>
            ? ReferenceText.GetRawString(val, refAttr)
            : val switch
            {
                bool b => b.ToString(),
                Enum e => Convert.ToInt32(e).ToString(),
                _ => val.ToString(),
            };
    }

    private static async Task<IEntity?> FindEntityInDbSet(
        IQueryable dbSet,
        Type entityType,
        string entityId)
    {
        // Prefer FindAsync(object[], CancellationToken); fall back to the single-arg
        // convenience overload (EF Core 10). Reflection does not fill optional parameters.
        var method = dbSet.GetType().GetMethod("FindAsync",
            [typeof(object[]), typeof(CancellationToken)])
            ?? dbSet.GetType().GetMethod("FindAsync", [typeof(object[])]);
        if (method == null) return null;

        // EF's FindAsync returns ValueTask<T> — await it dynamically.
        dynamic task = method.GetParameters().Length == 2
            ? method.Invoke(dbSet, [new object[] { entityId }, CancellationToken.None])!
            : method.Invoke(dbSet, [new object[] { entityId }])!;
        await task;
        return task.Result as IEntity;
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