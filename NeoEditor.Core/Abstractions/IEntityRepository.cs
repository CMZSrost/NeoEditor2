using System.Collections.Generic;
using System.Threading.Tasks;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Symmetric full repository contract for a single entity type (R26 v2).
/// DB (<c>DbRepository</c>) and XML (<c>XmlRepository</c>) are two implementations of the same
/// abstract — each implements ALL five capabilities: CRUD, diff (row-level + field-level),
/// dirty (exposed, session-held), save/export, load/import.
/// No backend special-casing: no NotSupported, no empty-return, no single-backend methods.
/// CRUD routes through IHostService commands (undo/dirty/hooks, R24).
/// </summary>
public interface IEntityRepository<T> : IDataRepository<T> where T : IEntity
{
    // ── CRUD：增删查改 ──
    /// <summary>Add a new entity (via AddEntityCommand → HostService.ExecuteAsync).</summary>
    Task AddAsync(T entity);

    /// <summary>Update an entity (via ReplaceEntityCommand → HostService.ExecuteAsync).</summary>
    Task UpdateAsync(T entity);

    /// <summary>Delete an entity by id (via DeleteEntityCommand → HostService.ExecuteAsync).</summary>
    Task DeleteAsync(string entityId);

    // ── diff：行级 + 字段级 ──
    /// <summary>
    /// Row-level diff: which records (DB rows / XML files) would change if
    /// <paramref name="candidates"/> were saved. DB fills <see cref="RowDiff.TargetId"/> + Kind;
    /// XML fills TargetId (file path) + Kind + Old/NewContent snapshots.
    /// </summary>
    Task<IReadOnlyList<RowDiff>> GetDiffAsync(IReadOnlyList<T> candidates);

    /// <summary>Field-level diff between two versions of the same entity (DiffEngine).</summary>
    Task<IReadOnlyList<DiffEntry>> GetFieldDiffAsync(T before, T after);

    // ── dirty：repository 暴露，session 持有（R01）──
    /// <summary>IDs with unsaved edits in the current profile's session.</summary>
    IReadOnlyCollection<string> DirtyIds { get; }

    /// <summary>Mark the given entity ids dirty in the current profile's session.</summary>
    void MarkDirty(IEnumerable<string> ids);

    /// <summary>Clear the given entity ids from the current profile's dirty set.</summary>
    void ClearDirty(IEnumerable<string> ids);

    // ── save/export（一个函数）──
    /// <summary>
    /// Persist entities to the backing store (DB=upsert+delete rows; XML=write files+remove nodes).
    /// Save 与 Export 是同一个动词，只是目标后端不同（R26）。
    /// </summary>
    Task SaveAsync(IEnumerable<T> entities);

    // ── load/import（一个函数）──
    /// <summary>
    /// Load all entities of this type from the backing store
    /// (DB=read all rows; XML=parse the mod this repository is bound to).
    /// </summary>
    Task<IReadOnlyList<T>> LoadAsync();
}