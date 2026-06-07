using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Context;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options)
        : base(options)
    {
    }

    // public DbSet<ModInfo> ModInfos { get; set; }
    public DbSet<AttackMode> AttackModes { get; set; }
    public DbSet<BarterHex> BarterHexes { get; set; }
    public DbSet<BattleMove> BattleMoves { get; set; }
    public DbSet<CampType> CampTypes { get; set; }
    public DbSet<ChargeProfile> ChargeProfiles { get; set; }
    public DbSet<Condition> Conditions { get; set; }
    public DbSet<ContainerType> ContainerTypes { get; set; }
    public DbSet<Creature> Creatures { get; set; }
    public DbSet<CreatureSource> CreatureSources { get; set; }
    public DbSet<DataFile> DataFiles { get; set; }
    public DbSet<DmcPlace> DmcPlaces { get; set; }
    public DbSet<Encounter> Encounters { get; set; }
    public DbSet<EncounterTrigger> EncounterTriggers { get; set; }
    public DbSet<Faction> Factions { get; set; }
    public DbSet<ForbiddenHex> ForbiddenHexes { get; set; }
    public DbSet<GameVar> GameVars { get; set; }
    public DbSet<Headline> Headlines { get; set; }
    public DbSet<HexType> HexTypes { get; set; }
    public DbSet<Ingredient> Ingredients { get; set; }
    public DbSet<ItemProp> ItemProps { get; set; }
    public DbSet<ItemType> ItemTypes { get; set; }
    public DbSet<Map> Maps { get; set; }
    public DbSet<Recipe> Recipes { get; set; }
    public DbSet<TreasureTable> TreasureTables { get; set; }

    /// <summary>
    ///     根据表名获取 DbSet（泛型）
    /// </summary>
    public IQueryable GetDbSet(string tableName)
    {
        var property = GetType().GetProperties()
            .FirstOrDefault(p => p.PropertyType.IsGenericType &&
                                 p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>) &&
                                 p.GetCustomAttribute<TableAttribute>()?.Name == tableName);
        if (property == null)
            throw new ArgumentException($"未找到表名为 {tableName} 的 DbSet");

        return (IQueryable)property.GetValue(this)!;
    }

    /// <summary>
    ///     根据实体类型获取 DbSet
    /// </summary>
    public IQueryable GetDbSet(Type entityType)
    {
        // Specify Type.EmptyTypes to disambiguate between Set<TEntity>() and Set<TEntity>(string)
        var method = typeof(GameDbContext).GetMethod(nameof(Set), Type.EmptyTypes)
            ?.MakeGenericMethod(entityType);
        return (IQueryable)method?.Invoke(this, null)!;
    }

    public IQueryable<TEntity> GetDbSet<TEntity>()
    {
        return (IQueryable<TEntity>)GetDbSet(typeof(TEntity));
    }

    public async Task DbBulkInsertOrUpdate(Type entityType, object entities)
    {
        var method = GetType()
            .GetMethod(nameof(DbBulkInsertOrUpdateTyped), BindingFlags.Instance | BindingFlags.NonPublic)
            ?.MakeGenericMethod(entityType);
        if (method == null)
            throw new InvalidOperationException($"Cannot bulk import entity type {entityType.Name}.");

        var task = method.Invoke(this, new[] { entities }) as Task;
        if (task == null)
            throw new InvalidOperationException(
                $"Bulk import for entity type {entityType.Name} did not return a task.");

        await task;
    }

    private Task DbBulkInsertOrUpdateTyped<T>(object entities) where T : IEntity
    {
        if (entities is not IEnumerable<T> typedEntities)
            throw new InvalidOperationException(
                $"Entity payload type {entities.GetType().Name} does not match {typeof(T).Name}.");

        return this.BulkInsertOrUpdateAsync(typedEntities);
    }
}