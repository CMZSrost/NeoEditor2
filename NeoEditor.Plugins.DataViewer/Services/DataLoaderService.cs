using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;


namespace NeoEditor.Plugins.DataViewer.Services;

/// <summary>
/// Handles entity data loading from GameDbContext for the DataViewer.
/// Extracted from ModGameDataTabsView.Data.cs per M9 plugin migration #8.
///
/// Responsibilities:
/// - Load entities by type + mod ID (typed + reflection-dispatch variants)
/// - Load entities by multiple mod IDs (merge view)
/// - Resolve entity key properties (PK discovery)
/// - Build tab headers
/// </summary>
public class DataLoaderService
{
    private readonly IDbContextFactory<GameDbContext> _dbFactory;
    private readonly ILogger<DataLoaderService> _logger;

    public DataLoaderService(
        IDbContextFactory<GameDbContext> dbFactory,
        ILogger<DataLoaderService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    // ── Entity loading: single mod ──────────────────────────────────────

    /// <summary>Load entities of a given type for a single mod, returned as ObservableCollection.</summary>
    public async Task<ObservableCollection<object>> LoadEntitiesByTypeAsync(Type entityType, int modId)
    {
        var method = typeof(DataLoaderService)
                         .GetMethod(nameof(LoadEntitiesByTypeTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)
                         ?.MakeGenericMethod(entityType)
                     ?? throw new InvalidOperationException($"Cannot load entity type {entityType.Name}.");

        var task = method.Invoke(this, [modId]) as Task<ObservableCollection<object>>;
        if (task == null)
            throw new InvalidOperationException($"Loading entity type {entityType.Name} did not return a task.");

        return await task;
    }

    private async Task<ObservableCollection<object>> LoadEntitiesByTypeTypedAsync<TEntity>(int modId)
        where TEntity : IEntity
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var list = await db.Set<TEntity>()
            .Where(x => x.ModId == modId)
            .Cast<object>()
            .ToListAsync();
        return new ObservableCollection<object>(list);
    }

    // ── Entity loading: by mod ID (returns List&lt;IEntity&gt;) ──────────

    /// <summary>Load entities as List&lt;IEntity&gt; for a single mod.</summary>
    public async Task<List<IEntity>> LoadEntitiesByModAsync(Type entityType, int modId)
    {
        var method = typeof(DataLoaderService)
                         .GetMethod(nameof(LoadEntitiesByModTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)
                         ?.MakeGenericMethod(entityType)
                     ?? throw new InvalidOperationException($"Cannot load entity type {entityType.Name}.");

        var task = method.Invoke(this, [modId]) as Task<List<IEntity>>;
        return task is not null ? await task : [];
    }

    private async Task<List<IEntity>> LoadEntitiesByModTypedAsync<TEntity>(int modId)
        where TEntity : IEntity
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Set<TEntity>()
            .Where(x => x.ModId == modId)
            .Cast<IEntity>()
            .ToListAsync();
    }

    // ── Entity loading: multiple mod IDs (merge view) ────────────────────

    /// <summary>Load entities as List&lt;IEntity&gt; for multiple mod IDs.</summary>
    public async Task<List<IEntity>> LoadEntitiesByModIdsAsync(Type entityType, List<int> modIds)
    {
        var method = typeof(DataLoaderService)
                         .GetMethod(nameof(LoadEntitiesByModIdsTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)
                         ?.MakeGenericMethod(entityType)
                     ?? throw new InvalidOperationException($"Cannot load entity type {entityType.Name}.");
        var task = method.Invoke(this, [modIds]) as Task<List<IEntity>>;
        return task is not null ? await task : [];
    }

    private async Task<List<IEntity>> LoadEntitiesByModIdsTypedAsync<TEntity>(List<int> modIds)
        where TEntity : IEntity
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Set<TEntity>()
            .Where(x => modIds.Contains(x.ModId))
            .Cast<IEntity>()
            .ToListAsync();
    }

    // ── Entity key resolution ────────────────────────────────────────────

    /// <summary>
    /// Resolve the primary key property for an entity type.
    /// Uses [Index] attribute, falling back to first int [Column] property.
    /// </summary>
    public static PropertyInfo? ResolveEntityKeyProperty(Type entityType)
    {
        var indexAttr = entityType.GetCustomAttributes<IndexAttribute>().FirstOrDefault();
        var keyPropName = indexAttr?.PropertyNames
            .FirstOrDefault(n => n != nameof(IEntity.EntityId));
        if (!string.IsNullOrWhiteSpace(keyPropName))
            return entityType.GetProperty(keyPropName, BindingFlags.Instance | BindingFlags.Public);

        // Fallback: first property with [Column] attribute that is int type
        return entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.DeclaringType != typeof(IEntity))
            .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null)
            .Where(p => p.PropertyType == typeof(int))
            .OrderBy(p => p.MetadataToken)
            .FirstOrDefault();
    }

    // ── Header building ──────────────────────────────────────────────────

    /// <summary>Build a tab header. Entity type names are technical identifiers
    /// and are always displayed in English (the C# class name), not localized.</summary>
    public string BuildHeader(Type entityType, int count)
    {
        return $"{entityType.Name} ({count})";
    }
}
