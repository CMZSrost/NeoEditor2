using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;

namespace NeoEditor.Services;

public interface IMergeService
{
    Task<MergeResult> ComputeMergeAsync(
        GameDbContext db,
        Dictionary<int, (int LoadIndex, string Name, bool IsMerge)> modMeta,
        List<int> allModIds,
        Dictionary<string, string> namespaceToModName,
        HashSet<int> mergeSpaceModIds,
        bool showAll,
        CancellationToken ct = default);
}

public class MergeService : IMergeService
{
    private static readonly Dictionary<Type, PropertyInfo[]> ColumnPropsCache = new();

    public async Task<MergeResult> ComputeMergeAsync(
        GameDbContext db,
        Dictionary<int, (int LoadIndex, string Name, bool IsMerge)> modMeta,
        List<int> allModIds,
        Dictionary<string, string> namespaceToModName,
        HashSet<int> mergeSpaceModIds,
        bool showAll,
        CancellationToken ct = default)
    {
        var types = new List<TypeMergeData>();
        var entityModNames = new Dictionary<string, string>();
        var entityNamespaces = new Dictionary<string, string>();
        var overlayChains = new Dictionary<string, List<OverlayChainEntry>>();
        var fieldSources = new Dictionary<(string, string), string>();
        var fieldConflicts = new HashSet<(string, string)>();
        var entityMergedIds = new Dictionary<string, int>();
        var overriddenEntityIds = new HashSet<string>();
        var referenceLookups = new Dictionary<Type, List<object>>();

        foreach (var (_, entityType) in Constants.GameTypes.OrderBy(x => x.Key))
        {
            ct.ThrowIfCancellationRequested();

            var allTypedEntities = await LoadEntitiesByModIdsAsync(db, entityType, allModIds);
            var allItems = new List<(IEntity Entity, int LoadIndex, string ModName, bool IsMerge)>();
            foreach (var item in allTypedEntities)
            {
                if (modMeta.TryGetValue(item.ModId, out var meta))
                    allItems.Add((item, meta.LoadIndex, meta.Name, meta.IsMerge));
            }

            var typeResult = ComputeTypeMerge(
                allItems, entityType,
                entityModNames, entityNamespaces, overlayChains, fieldSources, fieldConflicts,
                entityMergedIds, overriddenEntityIds, showAll, namespaceToModName);

            types.Add(typeResult);

            if (typeResult.AllEntities.Count > 0)
                referenceLookups[entityType] = typeResult.AllEntities.Select(e => (object)e).ToList();
        }

        return new MergeResult(
            types,
            entityModNames,
            entityNamespaces,
            overlayChains,
            fieldSources,
            fieldConflicts,
            entityMergedIds,
            overriddenEntityIds,
            namespaceToModName,
            mergeSpaceModIds,
            referenceLookups);
    }

    private static TypeMergeData ComputeTypeMerge(
        List<(IEntity Entity, int LoadIndex, string ModName, bool IsMerge)> allItems,
        Type entityType,
        Dictionary<string, string> entityModNames,
        Dictionary<string, string> entityNamespaces,
        Dictionary<string, List<OverlayChainEntry>> overlayChains,
        Dictionary<(string, string), string> fieldSources,
        HashSet<(string, string)> fieldConflicts,
        Dictionary<string, int> entityMergedIds,
        HashSet<string> overriddenEntityIds,
        bool showAll,
        Dictionary<string, string> namespaceToModName)
    {
        var keyProp = ResolveEntityKeyProperty(entityType);
        var mergedDict = new Dictionary<string, (IEntity Entity, int LoadIndex, string ModName)>();
        var insertedList = new List<(IEntity Entity, int LoadIndex, string ModName)>();
        var entityLoadIndex = new Dictionary<IEntity, int>();

        foreach (var item in allItems)
            entityLoadIndex[item.Entity] = item.LoadIndex;

        // Phase 1: add base game entities to dict
        foreach (var item in allItems.Where(x => x.LoadIndex == -1))
        {
            mergedDict[GetEntityKey(item.Entity, keyProp)] = (item.Entity, item.LoadIndex, item.ModName);
            foreach (var prop in GetColumnPropertiesCached(entityType))
            {
                var colName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
                fieldSources[(item.Entity.EntityId, colName)] = "Game";
            }
        }

        // Phase 2: process mods in load order
        var keyOverlayHistory = new Dictionary<string, List<OverlayChainEntry>>();
        foreach (var loadGroup in allItems.Where(x => x.LoadIndex >= 0).GroupBy(x => x.LoadIndex)
                     .OrderBy(g => g.Key))
        {
            foreach (var item in loadGroup)
            {
                var key = GetEntityKey(item.Entity, keyProp);
                var idVal = keyProp?.GetValue(item.Entity) is int i ? i : 0;

                if (item.IsMerge)
                {
                    if (!keyOverlayHistory.ContainsKey(key))
                    {
                        keyOverlayHistory[key] = new List<OverlayChainEntry>();
                        if (mergedDict.TryGetValue(key, out var baseVal))
                        {
                            var baseIdVal = keyProp?.GetValue(baseVal.Entity) is int bi ? bi : idVal;
                            keyOverlayHistory[key].Add(
                                new OverlayChainEntry("Game", baseIdVal, entityType, baseVal.Entity.EntityId, baseVal.Entity.Subject));
                        }
                    }
                    keyOverlayHistory[key]
                        .Add(new OverlayChainEntry(item.ModName, idVal, entityType, item.Entity.EntityId, item.Entity.Subject));

                    var hadPrevious = mergedDict.TryGetValue(key, out var prevEntity);
                    mergedDict[key] = (item.Entity, item.LoadIndex, item.ModName);

                    if (hadPrevious)
                    {
                        foreach (var prop in GetColumnPropertiesCached(entityType))
                        {
                            var colName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
                            var newVal = prop.GetValue(item.Entity);
                            var oldVal = prop.GetValue(prevEntity.Entity);
                            if (!Equals(newVal, oldVal))
                            {
                                var fsKey = (item.Entity.EntityId, colName);
                                if (fieldSources.TryGetValue(fsKey, out var prevMod)
                                    && prevMod != "Game" && prevMod != item.ModName)
                                {
                                    fieldConflicts.Add(fsKey);
                                }
                                fieldSources[fsKey] = item.ModName;
                            }
                        }
                    }
                    else
                    {
                        foreach (var prop in GetColumnPropertiesCached(entityType))
                        {
                            var colName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
                            fieldSources[(item.Entity.EntityId, colName)] = item.ModName;
                        }
                    }
                }
                else
                {
                    insertedList.Add((item.Entity, item.LoadIndex, item.ModName));
                    foreach (var prop in GetColumnPropertiesCached(entityType))
                    {
                        var colName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
                        fieldSources[(item.Entity.EntityId, colName)] = item.ModName;
                    }
                }
            }
        }

        // Record overlay chains for winners
        foreach (var (key, value) in mergedDict)
        {
            entityModNames[value.Entity.EntityId] = value.ModName;
            var idVal = keyProp?.GetValue(value.Entity) is int i ? i : 0;
            var chain = keyOverlayHistory.TryGetValue(key, out var hist) && hist.Count > 0
                ? hist
                : new List<OverlayChainEntry> { new(value.ModName, idVal, entityType, value.Entity.EntityId) };
            overlayChains[value.Entity.EntityId] = chain;
        }

        foreach (var (entity, _, modName) in insertedList)
        {
            entityModNames[entity.EntityId] = modName;
            var idVal = keyProp?.GetValue(entity) is int i ? i : 0;
            overlayChains[entity.EntityId] =
                new List<OverlayChainEntry> { new(modName, idVal, entityType, entity.EntityId, entity.Subject) };
        }

        var merged = mergedDict.Values.Concat(insertedList).ToList();
        var allEntities = new List<IEntity>();
        foreach (var (entity, _, _) in merged) allEntities.Add(entity);

        // Loser detection
        var winnerEntityIds = new HashSet<string>(allEntities.Select(e => e.EntityId));
        var overriddenThisType = 0;
        foreach (var (entity, _, modName, _) in allItems)
        {
            if (!winnerEntityIds.Contains(entity.EntityId))
            {
                allEntities.Add(entity);
                overriddenEntityIds.Add(entity.EntityId);
                overriddenThisType++;
            }
        }

        // Overlay chain + mod name for overridden entities
        foreach (var (entity, _, modName, _) in allItems)
        {
            if (!overriddenEntityIds.Contains(entity.EntityId)) continue;
            entityModNames[entity.EntityId] = modName;
            var key = GetEntityKey(entity, keyProp);
            if (keyOverlayHistory.TryGetValue(key, out var hist) && hist.Count > 0)
                overlayChains[entity.EntityId] = hist;
            else
            {
                var idVal = keyProp?.GetValue(entity) is int i ? i : 0;
                overlayChains[entity.EntityId] =
                    new List<OverlayChainEntry> { new(modName, idVal, entityType, entity.EntityId, entity.Subject) };
            }
        }

        // Populate entity namespaces (strModName) for ReferenceIndex
        // Build ModId → strModName mapping
        var modIdToNs = new Dictionary<int, string> { [-1] = "0" };
        var dirToNs = new Dictionary<string, string>();
        foreach (var (ns, dir) in namespaceToModName)
            dirToNs[dir] = ns; // last-write wins for duplicate dirs
        foreach (var (entity, _, _, _) in allItems)
        {
            var ns = entity.ModId == -1 ? "0"
                : dirToNs.TryGetValue(entityModNames.GetValueOrDefault(entity.EntityId, ""), out var n) ? n
                : entityModNames.GetValueOrDefault(entity.EntityId, "");
            entityNamespaces[entity.EntityId] = ns;
        }

        // Sort by load index then key
        allEntities = allEntities
            .OrderBy(e => entityLoadIndex.TryGetValue(e, out var idx) ? idx : 999)
            .ThenBy(e => keyProp?.GetValue(e) is int k ? k : 0)
            .ToList();

        // Compute merged IDs
        var mergeSpaceIds = new HashSet<string>(
            allItems.Where(x => x.LoadIndex == -1 || x.IsMerge).Select(x => x.Entity.EntityId));
        var maxKey = allEntities
            .Where(e => mergeSpaceIds.Contains(e.EntityId))
            .Select(e => keyProp?.GetValue(e)).OfType<int>().DefaultIfEmpty(0).Max();
        var insId = maxKey + 1;
        foreach (var entity in allEntities)
        {
            if (mergeSpaceIds.Contains(entity.EntityId))
                entityMergedIds[entity.EntityId] = keyProp?.GetValue(entity) is int ik ? ik : 0;
            else
                entityMergedIds[entity.EntityId] = insId++;
        }

        // Re-sort by merged ID
        allEntities = allEntities
            .OrderBy(e => entityMergedIds.TryGetValue(e.EntityId, out var mid) ? mid : 9999)
            .ThenBy(e => entityLoadIndex.TryGetValue(e, out var idx) ? idx : 9999)
            .ToList();

        // Visible entities (winners only when showAll is off)
        var visibleEntities = showAll
            ? allEntities
            : allEntities.Where(e => !overriddenEntityIds.Contains(e.EntityId)).ToList();

        return new TypeMergeData(entityType, allEntities, visibleEntities, overriddenThisType);
    }

    // ── Helpers (moved from ModGameDataTabsView) ──────────────────────────

    private static async Task<List<IEntity>> LoadEntitiesByModIdsAsync(
        GameDbContext db, Type entityType, List<int> modIds)
    {
        var method = typeof(GameDbContext).GetMethod(nameof(GameDbContext.Set), Type.EmptyTypes)!
            .MakeGenericMethod(entityType);
        var dbSet = (IQueryable<IEntity>)method.Invoke(db, null)!;
        return await dbSet.Where(e => modIds.Contains(e.ModId)).ToListAsync();
    }

    private static PropertyInfo? ResolveEntityKeyProperty(Type entityType)
        => EntityHelper.ResolveKeyProperty(entityType);

    private static string GetEntityKey(IEntity entity, PropertyInfo? keyProp)
    {
        if (keyProp is null) return entity.EntityId;
        var val = keyProp.GetValue(entity);
        return val?.ToString() ?? entity.EntityId;
    }

    private static PropertyInfo[] GetColumnPropertiesCached(Type entityType)
    {
        if (ColumnPropsCache.TryGetValue(entityType, out var cached)) return cached;
        var props = entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.DeclaringType != typeof(IEntity)
                         && p.GetCustomAttribute<ColumnAttribute>() != null)
            .OrderBy(p => p.MetadataToken)
            .ToArray();
        ColumnPropsCache[entityType] = props;
        return props;
    }
}
