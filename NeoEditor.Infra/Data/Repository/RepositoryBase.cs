using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Command;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Repository;

/// <summary>
/// Abstract base class for entity repositories (R26 v2 symmetric contract).
/// Provides the command-facade CRUD (routed through <see cref="IHostService"/> so undo/dirty/
/// hooks/R24 apply), the field-level diff (DiffEngine), and dirty delegation (session-held).
/// Concrete backends (<c>DbRepository</c>, <c>XmlRepository</c>) implement read,
/// row-level diff, <see cref="SaveAsync"/> and <see cref="LoadAsync"/>.
/// </summary>
public abstract class RepositoryBase<T> : IEntityRepository<T> where T : IEntity
{
    private readonly IHostService _host;

    protected RepositoryBase(IHostService host)
    {
        _host = host;
    }

    /// <summary>Read a single entity by id from the backing store.</summary>
    public abstract Task<T?> GetByIdAsync(string entityId);

    /// <summary>Read all entities of this type from the backing store.</summary>
    public abstract Task<IReadOnlyList<T>> GetAllAsync();

    // ── CRUD：命令门面（R24，全部走 HostService.ExecuteAsync）──

    /// <inheritdoc />
    public async Task AddAsync(T entity)
    {
        var cmd = new AddEntityCommand(typeof(T).Name, entity);
        await _host.ExecuteAsync(cmd);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(T entity)
    {
        var previous = _host.GetCachedEntity(entity.EntityId) ?? await GetByIdAsync(entity.EntityId);
        var cmd = new ReplaceEntityCommand(typeof(T).Name, entity, previous);
        await _host.ExecuteAsync(cmd);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string entityId)
    {
        var entity = _host.GetCachedEntity(entityId) as T ?? await GetByIdAsync(entityId);
        if (entity is null) return;
        var cmd = new DeleteEntityCommand(typeof(T).Name, entity);
        await _host.ExecuteAsync(cmd);
    }

    // ── diff：字段级（两端同一实现）；行级由后端实现 ──

    /// <inheritdoc />
    public Task<IReadOnlyList<DiffEntry>> GetFieldDiffAsync(T before, T after)
        => Task.FromResult<IReadOnlyList<DiffEntry>>(DiffEngine.ComputeDiff(before, after));

    /// <summary>Row-level diff: which backend records (DB rows / XML files) would change.</summary>
    public abstract Task<IReadOnlyList<RowDiff>> GetDiffAsync(IReadOnlyList<T> candidates);

    // ── dirty：暴露操作，session 持有（R01）──

    /// <inheritdoc />
    public IReadOnlyCollection<string> DirtyIds => _host.DirtyEntities.ToList();

    /// <inheritdoc />
    public void MarkDirty(IEnumerable<string> ids) => _host.MarkEntitiesDirty(ids);

    /// <inheritdoc />
    public void ClearDirty(IEnumerable<string> ids) => _host.RemoveDirtyEntities(ids);

    // ── save/export（一个函数）──

    /// <summary>Persist entities to the backing store (upsert/write).</summary>
    public abstract Task SaveAsync(IEnumerable<T> entities);

    // ── load/import（一个函数）──

    /// <summary>Load all entities of this type from the backing store.</summary>
    public abstract Task<IReadOnlyList<T>> LoadAsync();
}