using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Repository;

/// <summary>
/// EF Core backed repository (R26 v2 symmetric contract). Implements all five capabilities:
/// CRUD (command facade via <see cref="RepositoryBase{T}"/>), row-level + field-level diff,
/// dirty (session-held), save (upsert into game.db) and load (read all rows).
/// </summary>
public class DbRepository<T> : RepositoryBase<T> where T : IEntity
{
    private readonly IDbContextFactory<GameDbContext> _dbFactory;

    public DbRepository(IHostService host, IDbContextFactory<GameDbContext> dbFactory)
        : base(host)
    {
        _dbFactory = dbFactory;
    }

    public override async Task<T?> GetByIdAsync(string entityId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Set<T>().FindAsync(entityId);
    }

    public override async Task<IReadOnlyList<T>> GetAllAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Set<T>().ToListAsync();
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<RowDiff>> GetDiffAsync(IReadOnlyList<T> candidates)
    {
        if (candidates.Count == 0) return [];

        await using var db = await _dbFactory.CreateDbContextAsync();
        var dbIds = new HashSet<string>(
            await db.Set<T>().Select(e => e.EntityId).ToListAsync());

        var result = new List<RowDiff>(candidates.Count);
        foreach (var entity in candidates)
        {
            result.Add(dbIds.Contains(entity.EntityId)
                ? new RowDiff(entity.EntityId, DiffKind.Modified)
                : new RowDiff(entity.EntityId, DiffKind.Added));
        }

        return result;
    }

    /// <inheritdoc />
    public override async Task SaveAsync(IEnumerable<T> entities)
    {
        var list = entities.ToList();
        if (list.Count == 0) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        await db.DbBulkInsertOrUpdate(typeof(T), list);
    }

    /// <inheritdoc />
    public override async Task<IReadOnlyList<T>> LoadAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Set<T>().ToListAsync();
    }
}