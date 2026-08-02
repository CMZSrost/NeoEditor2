using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Central service that owns all data modification paths (R24).
/// All CRUD — from UI, CLI, MCP — must flow through this single interface.
/// Wraps CommandHistory for undo/redo, WorkspaceSession for dirty tracking,
/// and IDataRepository for data access — all behind a single pipeline.
/// DI lifetime: singleton.
/// </summary>
public interface IHostService
{
    // ── Command 执行 ──

    /// <summary>Execute a single command and record it in the scope's undo stack.</summary>
    Task<CommandResult> ExecuteAsync(IEditorCommand command, string? scopeId = null);

    /// <summary>Execute multiple commands as a batch under a single scope.</summary>
    Task<CommandResult> ExecuteBatchAsync(IEnumerable<IEditorCommand> commands, string? scopeId = null);

    /// <summary>Undo the last command in the specified (or active) scope.</summary>
    Task UndoAsync(string? scopeId = null);

    /// <summary>Redo the last undone command in the specified (or active) scope.</summary>
    Task RedoAsync(string? scopeId = null);

    // ── 脏追踪 (R01 + R09 + R26 §3 per-profile scope) ──

    /// <summary>
    /// The profile whose dirty set is exposed by the parameterless dirty members (R26 §3).
    /// -1 = game/base; &gt;= 0 = a mod profile.
    /// </summary>
    int ActiveProfileId { get; }

    /// <summary>
    /// Switch the active profile, scoping <see cref="DirtyEntities"/> and the dirty-marking
    /// members to that profile (R26 §3). Called when a profile's workspace is opened/focused.
    /// </summary>
    void SetActiveProfile(int profileId);

    /// <summary>Entity IDs that have unsaved edits in the current profile.</summary>
    ISet<string> DirtyEntities { get; }

    /// <summary>True when one or more entities have unsaved edits.</summary>
    bool HasUnsavedChanges { get; }

    /// <summary>Fires whenever DirtyEntities changes.</summary>
    event EventHandler? DirtyStateChanged;

    /// <summary>Mark a single entity as dirty.</summary>
    void MarkEntityDirty(string entityId);

    /// <summary>Mark multiple entities as dirty.</summary>
    void MarkEntitiesDirty(IEnumerable<string> entityIds);

    /// <summary>Clear all dirty tracking (after successful save or on discard).</summary>
    void ClearDirtyEntities();

    /// <summary>Remove specific entities from dirty tracking (after single-tab save).</summary>
    void RemoveDirtyEntities(IEnumerable<string> entityIds);

    // ── 持久化 ──

    /// <summary>Persist the given entity (or all dirty entities) to the DB backing store (R26 Save action).</summary>
    Task<SaveResult> SaveAsync(string? entityId = null);

    /// <summary>Persist all dirty entities to the DB backing store, grouped by mod (R26 SaveAll action).</summary>
    Task<SaveResult> SaveAllAsync();

    // ── 导出 (R26: Export = DB → XML) ──

    /// <summary>Export all entities of a single mod (by modId) from DB to its XML files.</summary>
    Task<IReadOnlyList<ExportResult>> ExportModAsync(int modId);

    /// <summary>Export all non-game mods of the current profile from DB to their XML files.</summary>
    Task<IReadOnlyList<ExportResult>> ExportProfileAsync();

    /// <summary>Default action: Save (memory → DB) then Export (DB → XML) as one transaction (R26 Publish action).</summary>
    Task<PublishResult> PublishAsync();

    /// <summary>Discard all unsaved changes.</summary>
    Task DiscardAsync(string? entityId = null);

    // ── Diff (字段级) ──

    /// <summary>Get field-level diff between the in-memory and stored version of an entity.</summary>
    Task<IReadOnlyList<DiffEntry>> GetDiffAsync(string? entityId = null);

    // ── 事件 (可观察; Feature Plugin 订阅用) ──

    /// <summary>Observable stream of entity change events emitted by this service.</summary>
    IObservable<EntityChangedEvent> Changes { get; }

    // ── Repository 访问 ──

    /// <summary>
    /// Get the typed repository for a given entity type (R26 v2 symmetric contract).
    /// Returns a DbRepository backed by game.db (the working store).
    /// </summary>
    IEntityRepository<T> Repository<T>() where T : IEntity;

    // ── 查询/搜索 ──

    /// <summary>
    /// Search entity types for entities whose subject, ID, or any string property contains
    /// <paramref name="query"/> (case-insensitive substring), up to <paramref name="limit"/>
    /// total results. Optional <paramref name="entityType"/> / <paramref name="modId"/> narrow
    /// the search. Owns the cross-type search so MCP/CLI/AI consumers share one implementation.
    /// </summary>
    Task<IReadOnlyList<IEntity>> SearchEntitiesAsync(string query, int limit = 50,
        string? entityType = null, int? modId = null);

    // ── Entity 注册表 ──

    /// <summary>
    /// Register an entity type's ObservableCollection for a given scope.
    /// When AddEntity/DeleteEntity commands execute in this scope,
    /// HostService automatically dispatches add/remove to this collection.
    /// </summary>
    void RegisterEntityCollection(string scopeId, string entityType, System.Collections.IList collection);

    /// <summary>Unregister all entity collections for a scope.</summary>
    void UnregisterEntityCollections(string scopeId);

    /// <summary>
    /// Get an entity from the central in-memory cache.
    /// Returns null if the entity is not currently loaded.
    /// </summary>
    IEntity? GetCachedEntity(string entityId);

    /// <summary>
    /// Get all cached entities of a given type.
    /// </summary>
    IReadOnlyList<IEntity> GetCachedEntitiesByType(string entityType);

    /// <summary>
    /// Add an entity to the central cache (e.g., when loading a tab or undoing a delete).
    /// </summary>
    void AddEntityToCache(IEntity entity);

    /// <summary>
    /// Remove an entity from the central cache (e.g., when deleting or undoing an add).
    /// </summary>
    void RemoveEntityFromCache(string entityId);

    // ── Scope 管理 ──

    /// <summary>Register a CommandHistory under a named scope (e.g. a per-tab undo stack).</summary>
    void RegisterCommandScope(string scopeId, ICommandHistory history);

    /// <summary>Unregister a previously registered scope.</summary>
    void UnregisterCommandScope(string scopeId);

    /// <summary>Switch the active scope that UndoAsync/RedoAsync targets by default.</summary>
    void SetActiveScope(string? scopeId);

    // ── Extension Points (R25) ──

    /// <summary>
    /// Register a pre-save hook. Invoked before entities are persisted.
    /// Hooks execute in <see cref="IExtensionPoint{TContext}.Order"/> ascending.
    /// </summary>
    void RegisterPreSaveHook(IExtensionPoint<PreSaveContext> hook);

    /// <summary>
    /// Register a post-load hook. Invoked after entities are loaded from the store.
    /// Hooks execute in <see cref="IExtensionPoint{TContext}.Order"/> ascending.
    /// </summary>
    void RegisterPostLoadHook(IExtensionPoint<PostLoadContext> hook);

    /// <summary>
    /// Register a pre-execute hook. Invoked before a command is executed.
    /// Hooks execute in <see cref="IExtensionPoint{TContext}.Order"/> ascending.
    /// </summary>
    void RegisterPreExecuteHook(IExtensionPoint<PreExecuteContext> hook);

    /// <summary>
    /// Register a pre-export hook (R25/R26). Invoked before entities are exported to XML files.
    /// Hooks execute in <see cref="IExtensionPoint{TContext}.Order"/> ascending.
    /// </summary>
    void RegisterPreExportHook(IExtensionPoint<PreExportContext> hook);
}