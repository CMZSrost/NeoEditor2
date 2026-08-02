using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Plugins.DataViewer.Services;
using NeoEditor.Services;
// Alias only IReferenceEntry to avoid IWorkspaceSession/IHostService ambiguity with NeoEditor.Services.
using IReferenceEntry = NeoEditor.Core.Abstractions.IReferenceEntry;

namespace NeoEditor.Helper;

/// <summary>
/// Canonical reference resolution — all paths go through the store's ReferenceIndex.
/// Implements IReferenceResolver as the single blessed interface for entity resolution.
/// All dependencies received via constructor injection.
/// </summary>
public class ReferenceResolver : IReferenceResolver
{
    private readonly IWorkspaceSession _session;
    private readonly CommunityToolkit.Mvvm.Messaging.IMessenger _messenger;
    private readonly IDataGridNavigationService? _navigationService;

    /// <summary>Constructor: receives all dependencies via DI.</summary>
    public ReferenceResolver(IWorkspaceSession session, CommunityToolkit.Mvvm.Messaging.IMessenger messenger,
        IDataGridNavigationService? navigation = null)
    {
        _session = session;
        _messenger = messenger;
        _navigationService = navigation;
    }

    private EntityMergeStore? ActiveStore => _session.ActiveMergeStore ?? _session.BrowserStore;
    private Dictionary<string, string> EntityNamespaces => ActiveStore?.EntityNamespaces ?? [];
    private Dictionary<string, int> EntityMergedIds => ActiveStore?.EntityMergedIds ?? [];

    // ═══════════════════════════════════════════════════════════════════════
    //  IReferenceResolver — instance methods
    // ═══════════════════════════════════════════════════════════════════════

    public T? LookupRef<T>(IEntity sourceEntity, string propertyName, string rawId) where T : IEntity
    {
        if (string.IsNullOrWhiteSpace(rawId)) return default;
        if (!ReferenceLookups.TryGetValue(typeof(T), out var list) || list is null)
            return default;

        // 1. Try ReferenceIndex (context-aware, O(1))
        var activeStore = ActiveStore;
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
        var sourceNs = EntityNamespaces.TryGetValue(sourceEntity.EntityId, out var sn) ? sn : null;
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
                // Composite key (ItemType "86.6"): match by GroupId.SubgroupId, highest ModId
                var gidProp = typeof(T).GetProperty("GroupId",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
                var sidProp = typeof(T).GetProperty("SubgroupId",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
                var dotIdx = cleanId.IndexOf('.');
                if (gidProp is not null && sidProp is not null && dotIdx > 0
                    && int.TryParse(cleanId[..dotIdx], out var gid)
                    && int.TryParse(cleanId[(dotIdx + 1)..], out var sid))
                {
                    T? cbest = default;
                    foreach (var obj in list)
                    {
                        if (obj is not T e) continue;
                        if (gidProp.GetValue(e) is not int gi || gi != gid) continue;
                        if (sidProp.GetValue(e) is not int si || si != sid) continue;
                        if (cbest is null || e.ModId > cbest.ModId) cbest = e;
                    }
                    return cbest;
                }
                Serilog.Log.Logger.Debug(
                    "[RefResolver:Fallback] non-prefixed key '{Key}' not an int → null", cleanId);
                return default;
            }
            T? best = default;
            var mergedIds = EntityMergedIds;
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

        var activeStore = ActiveStore;
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
        _navigationService?.NavigateToByEntityId(entityType, entityId);
        // Also Peek so ReferenceInspector shows the target entity overview
        _navigationService?.PeekEntity(entityType, entityId);
    }

    public void NavigateToByKey<T>(int key) where T : IEntity
    {
        var entity = ActiveStore?.ReferenceLookups
            ?.GetValueOrDefault(typeof(T))
            ?.OfType<T>()
            .FirstOrDefault(e => EntityHelper.ResolveKeyProperty(typeof(T))?.GetValue(e) is int id && id == key);
        if (entity is not null)
            NavigateTo(typeof(T), entity.EntityId);
    }

    public void NavigateToByKeyFor<T>(int key, IEntity sourceEntity) where T : IEntity
    {
        var activeStore = ActiveStore;
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

    private Dictionary<Type, List<object>> ReferenceLookups =>
        ActiveStore?.ReferenceLookups ?? [];

    private bool SameNs(IEntity entity, string nsName)
        => EntityNamespaces.TryGetValue(entity.EntityId, out var em) && em == nsName;

    public IEntity? LookupRefByRawId(IEntity sourceEntity, string rawId, Type targetType)
    {
        if (string.IsNullOrWhiteSpace(rawId)) return null;
        if (!ReferenceLookups.TryGetValue(targetType, out var list) || list is null)
            return null;

        // Try ReferenceIndex first
        var activeStore = ActiveStore;
        if (activeStore?.Index is { } index)
        {
            var targetEid = index.LookupGlobal(targetType, rawId);
            if (targetEid is not null)
                foreach (var obj in list)
                    if (obj is IEntity e && e.EntityId == targetEid)
                        return e;
        }

        // Fallback: iterate ReferenceLookups
        return list.OfType<IEntity>().FirstOrDefault(e => e.EntityId == rawId);
    }

    public async System.Threading.Tasks.Task BuildReverseIndexAsync(
        ReferenceIndexService indexService, EntityMergeStore store)
    {
        var entries = new List<ReferenceIndexService.IndexEntry>();
        foreach (var (entityType, entities) in store.ReferenceLookups)
        {
            foreach (var obj in entities)
            {
                if (obj is not IEntity entity) continue;
                var ns = EntityNamespaces.TryGetValue(entity.EntityId, out var n) ? n : "0";
                var keyProp = entityType.GetProperty("Id",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase)
                    ?? entityType.GetProperty("nID",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
                var pk = keyProp?.GetValue(entity)?.ToString() ?? entity.EntityId;
                entries.Add(new ReferenceIndexService.IndexEntry(
                    entityType.Name, ns, pk, entity.EntityId));
            }
        }
        await indexService.BuildAsync(entries);

        // Build reverse index
        foreach (var (entityType, entities) in store.ReferenceLookups)
        {
            foreach (var obj in entities)
            {
                if (obj is not IEntity entity) continue;
                foreach (var prop in entityType.GetProperties())
                {
                    var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>();
                    if (refAttr is null) continue;
                    var val = prop.GetValue(entity) is ReferenceList<IReferenceEntry> rl
                        ? rl.ToRawString(refAttr.Separator)
                        : prop.GetValue(entity)?.ToString();
                    if (string.IsNullOrWhiteSpace(val)) continue;
                    var parsed = ReferenceParser.Parse(val, refAttr);
                    foreach (var seg in parsed.Segments)
                    {
                        var targetEid = LookupRefByRawId(entity, seg.ExtractedId, refAttr.TargetEntityType)?.EntityId;
                        if (targetEid is not null)
                            await indexService.AddReverseAsync(targetEid, entity.EntityId, prop.Name, seg.RawText);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Convenience: reverse-lookup + resolve source entity display info.
    /// Replaces the former FindReverseReferences full scan.
    /// </summary>
    public List<(Type SourceType, string SourceSubject, string SourceEntityId, string PropName)>
        ResolveReverseRefs(EntityMergeStore store, string targetEntityId)
    {
        var results = new List<(Type, string, string, string)>();
        var rawRefs = ReverseLookup(store, targetEntityId);
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

    public void ClearLookupCache()
    {
        // Cache clearing is handled by store/index rebuild
    }

    public string? LookupEntityId(ReferenceIndexService indexService, string entityType,
        string rawId, string? sourceNs)
    {
        var colonIdx = rawId.IndexOf(':');
        if (colonIdx > 0)
        {
            var ns = rawId[..colonIdx];
            var pk = rawId[(colonIdx + 1)..];
            return LookupPkByNs(indexService, entityType, ns, pk);
        }

        if (sourceNs is not null)
            return LookupPkByNs(indexService, entityType, sourceNs, rawId);

        return LookupPkByNs(indexService, entityType, "0", rawId);
    }

    /// <summary>
    /// Look up by (type, ns, pk), routing composite keys ("86.6") to the composite index.
    /// </summary>
    private static string? LookupPkByNs(ReferenceIndexService indexService, string entityType, string ns, string pk)
    {
        var dotIdx = pk.IndexOf('.');
        if (dotIdx > 0
            && int.TryParse(pk[..dotIdx], out var gid)
            && int.TryParse(pk[(dotIdx + 1)..], out var sid))
        {
            return indexService.LookupByNsComposite(entityType, ns, gid, sid);
        }
        return indexService.LookupByNs(entityType, ns, pk);
    }
}
