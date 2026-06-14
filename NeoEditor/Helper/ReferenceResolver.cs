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

        var sourceNs = GenericDataGridHelper.EntityNamespaces.TryGetValue(sourceEntity.EntityId, out var sn)
            ? sn
            : null;
        // Normalize: both null and "0" map to default namespace ""
        var normalizedSourceNs = ReferenceParser.NormalizeNamespace(sourceNs);

        // 1. Try ReferenceIndex (context-aware, O(1))
        var activeStore = GenericDataGridHelper.ActiveMergeStore ?? GenericDataGridHelper.BrowserStore;
        if (activeStore?.Index is { } index)
        {
            var targetEid = index.Lookup(sourceEntity.EntityId, propertyName, typeof(T), rawId);
            if (targetEid is not null)
            {
                foreach (var obj in list)
                {
                    if (obj is T e && e.EntityId == targetEid)
                    {
                        Serilog.Log.Logger.Information(
                            "[LookupRef] Index returned eid={Eid} modId={ModId} name={Name} for rawId={RawId} srcModId={SrcModId} srcNs={SrcNs}",
                            e.EntityId, e.ModId, e.Subject, rawId, sourceEntity.ModId, normalizedSourceNs);
                        // Index may return a different-mod entity (highest ModId wins in MergedId index).
                        // ModId cap: fall through to FallbackLookup if found entity is from a different mod
                        if (e.ModId != sourceEntity.ModId)
                        {
                            Serilog.Log.Logger.Information(
                                "[LookupRef] Different mod ({FoundModId}≠{SrcModId}) — falling through to FallbackLookup",
                                e.ModId, sourceEntity.ModId);
                            break;
                        }
                        // If source has a namespace and the index result is from a different namespace,
                        // fall through to FallbackLookup which prioritizes same-ns entities.
                        if (SameNs(e, normalizedSourceNs))
                        {
                            Serilog.Log.Logger.Information(
                                "[LookupRef] Same-mod+same-ns match: returning {Eid} name={Name}",
                                e.EntityId, e.Subject);
                            return e;
                        }
                        // Different namespace — fall through to FallbackLookup for same-ns priority
                        Serilog.Log.Logger.Information(
                            "[LookupRef] Same-mod but different ns — falling through to FallbackLookup");
                        break;
                    }
                }
            }
            else
            {
                Serilog.Log.Logger.Information(
                    "[LookupRef] Index.Lookup returned null for rawId={RawId} type={Type} — falling through to FallbackLookup",
                    rawId, typeof(T).Name);
            }
            // Index miss or different namespace — fall through to FallbackLookup
        }

        // 2. Fallback: shared O(n) scan through FallbackLookup
        var prop = sourceEntity.GetType().GetProperty(propertyName);
        var refAttr = prop?.GetCustomAttribute<ReferenceFieldAttribute>();
        var idStr = ReferenceParser.ExtractRawId(rawId, refAttr?.Pattern);
        var result = (T?)FallbackLookup(typeof(T), idStr, normalizedSourceNs, sourceEntity.ModId);
        Serilog.Log.Logger.Information(
            "[LookupRef] FallbackLookup result for rawId={RawId} idStr={IdStr} type={Type}: {Result}",
            rawId, idStr, typeof(T).Name, result?.EntityId ?? "(null)");
        return result;
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
                    {
                        NavigateTo(typeof(T), e.EntityId);
                        return;
                    }
        }

        NavigateToByKey<T>(key);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static Dictionary<Type, List<object>> ReferenceLookups =>
        GenericDataGridHelper.ReferenceLookups;

    private static bool SameNs(IEntity entity, string nsName)
    {
        if (!GenericDataGridHelper.EntityNamespaces.TryGetValue(entity.EntityId, out var em)) return false;
        // Normalize: both "" and "0" mean default namespace
        return ReferenceParser.NormalizeNamespace(em) == ReferenceParser.NormalizeNamespace(nsName);
    }

    /// <summary>
    /// Try to match an entity by composite key "GroupId.SubgroupId" (e.g., ItemType keys like "90.3").
    /// Returns true if the entity's GroupId and SubgroupId properties match the dotted pattern.
    /// </summary>
    private static bool TryMatchCompositeKey(IEntity entity, string cleanId)
    {
        var dotIdx = cleanId.IndexOf('.');
        if (dotIdx <= 0 || dotIdx >= cleanId.Length - 1) return false;
        if (!int.TryParse(cleanId[..dotIdx], out var gid)) return false;
        if (!int.TryParse(cleanId[(dotIdx + 1)..], out var sid)) return false;
        var t = entity.GetType();
        var gp = t.GetProperty("GroupId", BindingFlags.Instance | BindingFlags.Public);
        var sp = t.GetProperty("SubgroupId", BindingFlags.Instance | BindingFlags.Public);
        if (gp is null || sp is null) return false;
        return gp.GetValue(entity) is int eg && eg == gid
            && sp.GetValue(entity) is int es && es == sid;
    }

    /// <summary>
    /// O(n) fallback used by both LookupRef and GDH.FindBestMatch on index miss.
    /// Shared implementation — single source of truth for namespace/MergedId resolution.
    /// Expects pre-extracted rawId (no pattern prefix).
    /// Rules: same-ns first, modId ≤ sourceModId, highest modId wins within constraints.
    /// </summary>
    internal IEntity? FallbackLookup(Type entityType, string rawId, string? sourceNs = null, int sourceModId = int.MaxValue)
    {
        if (string.IsNullOrWhiteSpace(rawId)) return null;
        if (!ReferenceLookups.TryGetValue(entityType, out var list) || list is null)
            return null;
    
        var colonIdx = rawId.IndexOf(':');
        var nsPrefix = colonIdx > 0 ? rawId[..colonIdx] : null;
        var cleanId = colonIdx > 0 ? rawId[(colonIdx + 1)..] : rawId;
    
        Serilog.Log.Logger.Information(
            "[RefResolver:Fallback] type={Type} rawId={RawId} nsPrefix={NsPrefix} cleanId={CleanId} sourceNs={SourceNs} sourceModId={SrcModId}",
            entityType.Name, rawId, nsPrefix ?? "(none)", cleanId, sourceNs ?? "(none)", sourceModId);
    
        if (nsPrefix is not null)
        {
            // Namespace-prefixed: match by primary key within namespace, highest ModId
            var keyProp = EntityHelper.ResolveKeyProperty(entityType);
            if (keyProp is null) return null;
    
            if (!int.TryParse(cleanId, out var intKey))
            {
                // Non-integer key: string match (handle composite keys like "90.3")
                IEntity? strMatch = null;
                foreach (var obj in list)
                {
                    if (obj is not IEntity e) continue;
                    if (sourceModId < int.MaxValue && e.ModId < sourceModId) continue; // ModId cap: skip lower-priority mods
                    if (!SameNs(e, nsPrefix)) continue;
                    var v = keyProp.GetValue(e)?.ToString();
                    if ((v == cleanId || TryMatchCompositeKey(e, cleanId))
                        && (strMatch is null || e.ModId > strMatch.ModId))
                        strMatch = e;
                }
    
                return strMatch;
            }
    
            IEntity? nsBest = null;
            foreach (var obj in list)
            {
                if (obj is not IEntity e) continue;
                if (sourceModId < int.MaxValue && e.ModId < sourceModId) continue; // ModId cap: skip lower-priority mods
                if (!SameNs(e, nsPrefix)) continue;
                if (keyProp.GetValue(e) is not int k || k != intKey) continue;
                if (nsBest is null || e.ModId > nsBest.ModId)
                    nsBest = e;
            }

            // If primary key match failed, try MergedId match within the same namespace
            if (nsBest is null)
            {
                var nsMergedIds = GenericDataGridHelper.EntityMergedIds;
                foreach (var obj in list)
                {
                    if (obj is not IEntity e) continue;
                    if (sourceModId < int.MaxValue && e.ModId < sourceModId) continue; // ModId cap: skip lower-priority mods
                    if (!SameNs(e, nsPrefix)) continue;
                    if (!nsMergedIds.TryGetValue(e.EntityId, out var mid) || mid != intKey) continue;
                    if (nsBest is null || e.ModId > nsBest.ModId)
                        nsBest = e;
                }
            }
    
            if (nsBest is not null)
            {
                Serilog.Log.Logger.Information(
                    "[RefResolver:Fallback] NS result: type={Type} ns={Ns} pk={Pk} → {Result} (modId={ModId})",
                    entityType.Name, nsPrefix, cleanId, nsBest.EntityId, nsBest.ModId);
                return nsBest;
            }

            // "0:" prefix means "same-mod" / "default namespace" —
            // if no entity found in default namespace, fall through to
            // the no-prefix MergedId scan (cross-namespace fallback).
            if (nsPrefix is "0" or "")
            {
                Serilog.Log.Logger.Information(
                    "[RefResolver:Fallback] nsPrefix='{Ns}' returned null for type={Type} pk={Pk} — falling through to no-prefix MergedId scan",
                    nsPrefix, entityType.Name, cleanId);
            }
            else
            {
                Serilog.Log.Logger.Information(
                    "[RefResolver:Fallback] NS result: type={Type} ns={Ns} pk={Pk} → (null)",
                    entityType.Name, nsPrefix, cleanId);
                return null;
            }
        }

        // No namespace prefix (or "0" prefix fell through): lookup by MergedId, same-mod first, same-ns second, highest ModId
            // sameModBest is shared between both branches below
            IEntity? sameModBest = null;

            if (!int.TryParse(cleanId, out var mergedId))
            {
                // Non-integer key without prefix (e.g., composite keys like "90.3"):
                // Try key-property string match, prioritizing: same-mod > same-ns > global
                Serilog.Log.Logger.Information(
                    "[RefResolver:Fallback] composite key lookup: type={Type} key={Key} sourceModId={SrcModId} sourceNs={SrcNs}",
                    entityType.Name, cleanId, sourceModId, sourceNs ?? "(none)");
                var keyProp = EntityHelper.ResolveKeyProperty(entityType);
                IEntity? compositeNsBest = null;
                IEntity? globalBest = null;
                var candidates = new System.Text.StringBuilder();
    
                foreach (var obj in list)
                {
                    if (obj is not IEntity e) continue;
                    if (sourceModId < int.MaxValue && e.ModId < sourceModId) continue; // ModId cap: skip lower-priority mods
                    var keyVal = keyProp?.GetValue(e)?.ToString();
                    var matched = keyVal == cleanId || TryMatchCompositeKey(e, cleanId);
                    if (!matched) continue;
                    candidates.Append($" {e.EntityId}(mod={e.ModId})");

                    if (e.ModId == sourceModId)
                    {
                        sameModBest = e; // exact same mod — highest priority
                    }
                    else if (sourceNs is not null && SameNs(e, sourceNs))
                    {
                        if (compositeNsBest is null || e.ModId > compositeNsBest.ModId)
                            compositeNsBest = e;
                    }
                    else if (globalBest is null || e.ModId > globalBest.ModId)
                        globalBest = e;
                }
    
                var result = sameModBest ?? compositeNsBest ?? globalBest;
                Serilog.Log.Logger.Information(
                    "[RefResolver:Fallback] composite key '{Key}' → {Result} candidates=[{Candidates}] sameMod={SameMod} nsBest={NsBest} global={Global}",
                    cleanId, result?.EntityId ?? "(null)", candidates.ToString(), sameModBest?.EntityId ?? "-", compositeNsBest?.EntityId ?? "-", globalBest?.EntityId ?? "-");
                return result;
            }
    
            IEntity? best = null;
            IEntity? elseNsBest = null;
            var mergedIds = GenericDataGridHelper.EntityMergedIds;
            var midCandidates = new System.Text.StringBuilder();
            foreach (var obj in list)
            {
                if (obj is not IEntity e) continue;
                if (sourceModId < int.MaxValue && e.ModId < sourceModId) continue; // ModId cap: skip lower-priority mods
                if (!mergedIds.TryGetValue(e.EntityId, out var mid) || mid != mergedId) continue;
                GenericDataGridHelper.EntityModNames.TryGetValue(e.EntityId, out var emn);
                midCandidates.Append($" {e.EntityId}(mod={e.ModId}:{emn ?? "?"})");
                if (e.ModId == sourceModId)
                {
                    sameModBest = e; // exact same mod — highest priority
                }
                else if (sourceNs is not null && SameNs(e, sourceNs))
                {
                    if (elseNsBest is null || e.ModId > elseNsBest.ModId)
                        elseNsBest = e;
                }
                else if (best is null || e.ModId > best.ModId)
                    best = e;
            }

            var mergedResult = sameModBest ?? elseNsBest ?? best;
            var pkProp = EntityHelper.ResolveKeyProperty(entityType);

            // Last resort: if ModId cap excluded all candidates (e.g., NSExtended mod
            // referencing a base-game entity), re-scan WITHOUT the ModId cap.
            // The priority chain (sameModBest → sameNsBest → highest ModId globally)
            // still ensures correct prioritization.
            if (mergedResult is null)
            {
                IEntity? noCapSameMod = null;
                IEntity? noCapSameNs = null;
                IEntity? noCapGlobal = null;
                var noCapCandidates = new System.Text.StringBuilder();
                foreach (var obj in list)
                {
                    if (obj is not IEntity e) continue;
                    if (!mergedIds.TryGetValue(e.EntityId, out var mid) || mid != mergedId) continue;
                    GenericDataGridHelper.EntityModNames.TryGetValue(e.EntityId, out var emn);
                    noCapCandidates.Append($" {e.EntityId}(mod={e.ModId}:{emn ?? "?"})");
                    if (e.ModId == sourceModId)
                        noCapSameMod = e;
                    else if (sourceNs is not null && SameNs(e, sourceNs))
                    {
                        if (noCapSameNs is null || e.ModId > noCapSameNs.ModId)
                            noCapSameNs = e;
                    }
                    else if (noCapGlobal is null || e.ModId > noCapGlobal.ModId)
                        noCapGlobal = e;
                }
                var noCapResult = noCapSameMod ?? noCapSameNs ?? noCapGlobal;
                if (noCapResult is not null)
                {
                    Serilog.Log.Logger.Information(
                        "[RefResolver:Fallback] MergedId no-cap fallback: mid={Mid} → {Eid} mod={Mod} sameMod={SameMod} nsBest={NsBest} global={Global} candidates=[{Candidates}]",
                        mergedId, noCapResult.EntityId, noCapResult.ModId,
                        noCapSameMod?.EntityId ?? "-", noCapSameNs?.EntityId ?? "-", noCapGlobal?.EntityId ?? "-",
                        noCapCandidates.ToString());
                    mergedResult = noCapResult;
                }
            }

            // If MergedId scan only found entities from other mods, and the source
            // has a non-default namespace, try same-ns + primary key match.
            // (Non-default-ns entities get APPENDED MergedIds, so the original
            // MergedId may not match — but their primary key (e.g., Id) does.)
            if (mergedResult is null
                && sameModBest is null && elseNsBest is null && best is not null
                && !string.IsNullOrEmpty(sourceNs) && sourceNs != "")
            {
                IEntity? sameNsPkBest = null;
                if (pkProp is not null)
                {
                    foreach (var obj in list)
                    {
                        if (obj is not IEntity e) continue;
                        if (sourceModId < int.MaxValue && e.ModId < sourceModId) continue;
                        if (!SameNs(e, sourceNs)) continue;
                        if (pkProp.GetValue(e) is not int pk || pk != mergedId) continue;
                        if (sameNsPkBest is null || e.ModId > sameNsPkBest.ModId)
                            sameNsPkBest = e;
                    }
                }
                // Also try composite key match (e.g., "GroupId.SubgroupId")
                if (sameNsPkBest is null)
                {
                    foreach (var obj in list)
                    {
                        if (obj is not IEntity e) continue;
                        if (sourceModId < int.MaxValue && e.ModId < sourceModId) continue;
                        if (!SameNs(e, sourceNs)) continue;
                        if (!TryMatchCompositeKey(e, mergedId.ToString())) continue;
                        if (sameNsPkBest is null || e.ModId > sameNsPkBest.ModId)
                            sameNsPkBest = e;
                    }
                }
                if (sameNsPkBest is not null)
                {
                    Serilog.Log.Logger.Information(
                        "[RefResolver:Fallback] MergedId→pk fallback: mid={Mid} srcNs={SrcNs} → same-ns pk match {Eid} mod={Mod}",
                        mergedId, sourceNs, sameNsPkBest.EntityId, sameNsPkBest.ModId);
                    mergedResult = sameNsPkBest;
                }
            }

            // Log all candidates for diagnostics
            Serilog.Log.Logger.Information(
                "[RefResolver:Fallback] MergedId scan: type={Type} mid={Mid} srcModId={SrcModId} srcNs={SrcNs} → result={Result}(mod={ResultMod}) sameMod={SameMod} nsBest={NsBest} global={Global} candidates=[{Candidates}]",
                entityType.Name, mergedId, sourceModId, sourceNs ?? "(none)",
                mergedResult?.EntityId ?? "(null)", mergedResult?.ModId,
                sameModBest?.EntityId ?? "-", elseNsBest?.EntityId ?? "-", best?.EntityId ?? "-",
                midCandidates.ToString());

            // Diagnostic: also check pk-based match
            var pkMatchEids = new List<string>();
            if (pkProp is not null)
            {
                foreach (var obj in list)
                {
                    if (obj is not IEntity e) continue;
                    if (pkProp.GetValue(e) is int pk && pk == mergedId)
                        pkMatchEids.Add($"{e.EntityId}(mod={e.ModId})");
                }
                if (pkMatchEids.Count > 0)
                {
                    Serilog.Log.Logger.Debug(
                        "[RefResolver:Fallback] MergedId pk-match candidates: mid={Mid} pkMatches=[{PkMatches}]",
                        mergedId, string.Join(", ", pkMatchEids));
                }
            }
            return mergedResult;
    }

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