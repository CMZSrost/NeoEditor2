using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NeoEditor.Data.Model.Game;
using NeoEditor.Services;

namespace NeoEditor.Helper;

/// <summary>
/// Canonical reference resolution — all paths go through the store's ReferenceIndex.
/// Implements IReferenceResolver as the single blessed interface for entity resolution.
/// Static convenience members delegate to <see cref="Instance"/>.
/// </summary>
public class ReferenceResolver : IReferenceResolver
{
    public static ReferenceResolver Instance { get; } = new();

    // ═══════════════════════════════════════════════════════════════════════
    //  IReferenceResolver — instance methods
    // ═══════════════════════════════════════════════════════════════════════

    public T? LookupRef<T>(IEntity sourceEntity, string propertyName, string rawId) where T : IEntity
    {
        if (string.IsNullOrWhiteSpace(rawId)) return default;
        if (!ReferenceLookups.TryGetValue(typeof(T), out var list) || list is null)
            return default;

        // 1. Try ReferenceIndex (context-aware, O(1))
        var activeStore = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (activeStore?.Index is { } index)
        {
            var targetEid = index.Lookup(sourceEntity.EntityId, propertyName, typeof(T), rawId);
            if (targetEid is not null)
            {
                foreach (var obj in list)
                    if (obj is T e && e.EntityId == targetEid)
                        return e;
            }
        }

        // 2. Fallback: namespace prefix → primary key; no prefix → MergedId
        var sourceNs = GenericDataGridHelper.EntityNamespaces.TryGetValue(sourceEntity.EntityId, out var sn) ? sn : null;
        var prop = sourceEntity.GetType().GetProperty(propertyName);
        var refAttr = prop?.GetCustomAttribute<ReferenceFieldAttribute>();
        var idStr = ReferenceParser.ExtractRawId(rawId, refAttr?.Pattern);
        var colonIdx = idStr.IndexOf(':');
        var nsPrefix = colonIdx > 0 ? idStr[..colonIdx] : null;
        var cleanId = colonIdx > 0 ? idStr[(colonIdx + 1)..] : idStr;

        Serilog.Log.Logger.Debug(
            "[RefResolver:Fallback] type={Type} rawId={RawId} nsPrefix={NsPrefix} cleanId={CleanId}",
            typeof(T).Name, rawId, nsPrefix ?? "(none)", cleanId);

        if (nsPrefix is not null)
        {
            // Namespace-prefixed: match by primary key within namespace, highest ModId
            var keyProp = typeof(T).GetProperty("Id") ?? typeof(T).GetProperty("nID");
            if (!int.TryParse(cleanId, out var intKey))
            {
                // Non-integer key: string match
                T? strMatch = default;
                foreach (var obj in list)
                {
                    if (obj is not T e) continue;
                    if (!SameNs(e, nsPrefix)) continue;
                    var v = keyProp?.GetValue(e)?.ToString();
                    if (v == cleanId && (strMatch is null || e.ModId > strMatch.ModId))
                        strMatch = e;
                }
                return strMatch;
            }
            T? nsBest = default;
            foreach (var obj in list)
            {
                if (obj is not T e) continue;
                if (!SameNs(e, nsPrefix)) continue;
                if (keyProp?.GetValue(e) is not int k || k != intKey) continue;
                if (nsBest is null || e.ModId > nsBest.ModId)
                    nsBest = e;
            }
            Serilog.Log.Logger.Debug(
                "[RefResolver:Fallback] NS result: type={Type} ns={Ns} pk={Pk} → {Result}",
                typeof(T).Name, nsPrefix, cleanId, nsBest?.EntityId ?? "(null)");
            return nsBest;
        }
        else
        {
            // No namespace prefix: lookup by MergedId, highest ModId
            if (!int.TryParse(cleanId, out var mergedId))
            {
                Serilog.Log.Logger.Debug(
                    "[RefResolver:Fallback] non-prefixed key '{Key}' not an int → null", cleanId);
                return default;
            }
            T? best = default;
            var mergedIds = GenericDataGridHelper.EntityMergedIds;
            foreach (var obj in list)
            {
                if (obj is not T e) continue;
                if (!mergedIds.TryGetValue(e.EntityId, out var mid) || mid != mergedId) continue;
                if (best is null || e.ModId > best.ModId)
                    best = e;
            }

            // Diagnostic: also check pk-based match
            var pkProp = typeof(T).GetProperty("Id") ?? typeof(T).GetProperty("nID");
            var pkMatchEids = new List<string>();
            if (pkProp is not null)
            {
                foreach (var obj in list)
                {
                    if (obj is not T e) continue;
                    if (pkProp.GetValue(e) is int pk && pk == mergedId)
                        pkMatchEids.Add($"{e.EntityId}(mod={e.ModId})");
                }
            }

            Serilog.Log.Logger.Debug(
                "[RefResolver:Fallback] MergedId result: type={Type} mid={Mid} → {Result} modId={ModId} (pkMatch={PkMatch})",
                typeof(T).Name, mergedId, best?.EntityId ?? "(null)", best?.ModId ?? -999,
                string.Join(", ", pkMatchEids));
            return best;
        }
    }

    public string? LookupSubject(string sourceEntityId, string propertyName, Type targetType, string rawId,
        Type? secondaryTargetType = null)
    {
        if (string.IsNullOrWhiteSpace(rawId)) return null;

        var activeStore = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (activeStore?.Index is { } index)
        {
            var (subject, _) = index.LookupDisplay(sourceEntityId, propertyName, targetType, rawId);
            if (subject is not null) return subject;

            if (secondaryTargetType is not null)
            {
                (subject, _) = index.LookupDisplay(sourceEntityId, propertyName, secondaryTargetType, rawId);
                if (subject is not null) return subject;
            }

            (subject, _) = index.LookupDisplayGlobal(targetType, rawId);
            if (subject is not null) return subject;

            if (secondaryTargetType is not null)
            {
                (subject, _) = index.LookupDisplayGlobal(secondaryTargetType, rawId);
                if (subject is not null) return subject;
            }
        }

        return null;
    }

    public IReadOnlyList<(string SourceEntityId, string PropertyName, string RawId)>
        ReverseLookup(EntityMergeStore store, string targetEntityId)
    {
        return store.Index.ReverseLookup(targetEntityId);
    }

    public void NavigateTo(Type entityType, string entityId)
    {
        GenericDataGridHelper.NavigateToByEntityId(entityType, entityId);
        // Also Peek so ReferenceInspector shows the target entity overview
        GenericDataGridHelper.PeekEntity(entityType, entityId);
    }

    public void NavigateToByKey<T>(int key) where T : IEntity
    {
        var lookup = GenericDataGridHelper.GetEntities<T>();
        if (lookup.TryGetValue(key, out var entity))
            NavigateTo(typeof(T), entity.EntityId);
    }

    public void NavigateToByKeyFor<T>(int key, IEntity sourceEntity) where T : IEntity
    {
        var activeStore = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (activeStore?.Index is { } index)
        {
            var targetEid = index.LookupGlobal(typeof(T), key.ToString());
            if (targetEid is not null && ReferenceLookups.TryGetValue(typeof(T), out var list) && list is not null)
                foreach (var obj in list)
                    if (obj is T e && e.EntityId == targetEid)
                    { NavigateTo(typeof(T), e.EntityId); return; }
        }
        NavigateToByKey<T>(key);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static Dictionary<Type, List<object>> ReferenceLookups =>
        GenericDataGridHelper.ReferenceLookups;

    private static bool SameNs(IEntity entity, string nsName)
        => GenericDataGridHelper.EntityNamespaces.TryGetValue(entity.EntityId, out var em) && em == nsName;

    /// <summary>
    /// Convenience: reverse-lookup + resolve source entity display info.
    /// Replaces the former FindReverseReferences full scan.
    /// </summary>
    public static List<(Type SourceType, string SourceSubject, string SourceEntityId, string PropName)>
        ResolveReverseRefs(EntityMergeStore store, string targetEntityId)
    {
        var results = new List<(Type, string, string, string)>();
        var rawRefs = Instance.ReverseLookup(store, targetEntityId);
        foreach (var (srcEid, propName, _) in rawRefs)
        {
            foreach (var (t, entities) in store.ReferenceLookups)
            {
                var match = entities.OfType<IEntity>().FirstOrDefault(e => e.EntityId == srcEid);
                if (match != null)
                {
                    results.Add((t, match.Subject, srcEid, propName));
                    break;
                }
            }
        }
        return results;
    }
}
