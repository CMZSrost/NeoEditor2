using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NeoEditor.Data.Model.Game;
using NeoEditor.Services;

namespace NeoEditor.Helper;

/// <summary>
/// Per-merge-view reference index.
///
/// Database simulation model:
///   - Each namespace = a separate "file" with the same table schema.
///   - Same namespace + same primary key → REPLACE INTO (override).
///   - Different namespace → INSERT INTO (append, new MergedId).
///
/// Index structure:
///   _nsIndex:       (EntityType, Namespace, PrimaryKey) → EntityId  — for namespace-prefixed lookups
///   _mergedIdIndex: (EntityType, MergedId) → EntityId               — for non-prefixed lookups
///
/// Lookup rules:
///   - Reference has namespace prefix (e.g. "NSE:3")  → lookup by (type, namespace, primary key)
///   - Reference has no namespace prefix (e.g. "3")    → lookup by (type, MergedId)
///     NOTE: MergedId is NOT the entity's Id — it's the final merged auto-increment ID.
/// </summary>
public class ReferenceIndex
{
    private readonly EntityMergeStore _store;

    // ── Core indices ────────────────────────────────────────────────────────

    /// <summary>(EntityType, Namespace, PrimaryKey) → EntityId</summary>
    private readonly Dictionary<(Type EntityType, string Ns, string PrimaryKey), string> _nsIndex = new();

    /// <summary>(EntityType, MergedId) → EntityId</summary>
    private readonly Dictionary<(Type EntityType, int MergedId), string> _mergedIdIndex = new();

    // ── Reverse: targetEntityId → list of (sourceEntityId, propertyName, rawId) ──
    private readonly Dictionary<string, List<(string SourceEntityId, string PropertyName, string RawId)>> _reverse = new();

    // ── Display cache: (targetEntityId) → (Subject, ModName) ──
    private readonly Dictionary<string, (string? Subject, string? ModName)> _display = new();

    public ReferenceIndex(EntityMergeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Build
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Build the index. Prerequisites:
    ///   1. Game data loaded
    ///   2. Mods loaded and MergedIds assigned (same ns → override, different ns → append)
    ///   3. EntityNamespaces and EntityMergedIds populated in the store
    /// </summary>
    public async Task BuildAsync()
    {
        Clear();

        await Task.Run(() =>
        {
            var totalEnt = 0;
            var nsIdxCount = 0;
            var midIdxCount = 0;
            var skippedNoNs = 0;
            var skippedNoMergedId = 0;

            foreach (var (entityType, entities) in _store.ReferenceLookups)
            {
                foreach (var obj in entities)
                {
                    if (obj is not IEntity entity) continue;
                    totalEnt++;

                    // Compute primary key — try Id first, then nID (same as MergeService.ResolveEntityKeyProperty)
                    var primaryKey = ComputeEntityKey(entity);

                    // Index by MergedId — ALWAYS if available (don't skip just because primaryKey is null)
                    var hasMergedId = _store.EntityMergedIds.TryGetValue(entity.EntityId, out var mergedId);
                    if (hasMergedId)
                    {
                        var midKey = (entityType, mergedId);
                        if (_mergedIdIndex.TryGetValue(midKey, out var prevEid))
                        {
                            _store.EntityModNames.TryGetValue(prevEid, out var prevMod);
                            _store.EntityModNames.TryGetValue(entity.EntityId, out var newMod);
                            Serilog.Log.Logger.Debug(
                                "[RefIndex:Build] MergedId OVERWRITE: {Type}:mid={Mid} old={OldEid}(mod={OldMod}) → new={NewEid}(mod={NewMod})",
                                entityType.Name, mergedId, prevEid, prevMod ?? "?", entity.EntityId, newMod ?? "?");
                        }
                        _mergedIdIndex[midKey] = entity.EntityId;
                        midIdxCount++;
                    }
                    else
                    {
                        skippedNoMergedId++;
                    }

                    // Index by namespace — only if we have a namespace and primary key
                    if (primaryKey is null) continue;
                    if (!_store.EntityNamespaces.TryGetValue(entity.EntityId, out var ns))
                    {
                        skippedNoNs++;
                        continue;
                    }

                    var nsKey = (entityType, ReferenceParser.NormalizeNamespace(ns), primaryKey);
                    if (_nsIndex.TryGetValue(nsKey, out var prevNsEid))
                    {
                        _store.EntityModNames.TryGetValue(prevNsEid, out var prevNsMod);
                        _store.EntityModNames.TryGetValue(entity.EntityId, out var newNsMod);
                        Serilog.Log.Logger.Debug(
                            "[RefIndex:Build] NsIndex OVERWRITE: {Type}:ns={Ns}/pk={Pk} old={OldEid}(mod={OldMod}) → new={NewEid}(mod={NewMod})",
                            entityType.Name, ReferenceParser.NormalizeNamespace(ns), primaryKey, prevNsEid, prevNsMod ?? "?", entity.EntityId, newNsMod ?? "?");
                    }
                    _nsIndex[nsKey] = entity.EntityId;
                    nsIdxCount++;
                }
            }

            Serilog.Log.Logger.Information(
                "[RefIndex:Build] totalEnt={Total} nsIdx={Ns} midIdx={Mid} skippedNoNs={NoNs} skippedNoMid={NoMid}",
                totalEnt, nsIdxCount, midIdxCount, skippedNoNs, skippedNoMergedId);

            // Log first 10 MergedId entries for verification
            var sample = _mergedIdIndex.Take(10).Select(kv =>
                $"{kv.Key.EntityType.Name}:mid={kv.Key.MergedId}→{kv.Value}").ToList();
            Serilog.Log.Logger.Information("[RefIndex:Build] mergedIdIndex sample: {Sample}", string.Join(", ", sample));

            // Build reverse index
            BuildReverse();
        });

        Serilog.Log.Logger.Information(
            "[RefIndex:Build] done — nsIdx={Ns} mergedIdIdx={Mid} rev={Rev}",
            _nsIndex.Count, _mergedIdIndex.Count, _reverse.Count);
    }

    /// <summary>Sync wrapper — used where async is not feasible. Prefer BuildAsync.</summary>
    public void Build() => BuildAsync().GetAwaiter().GetResult();

    // ═══════════════════════════════════════════════════════════════════════
    //  Reverse index
    // ═══════════════════════════════════════════════════════════════════════

    private void BuildReverse()
    {
        _reverse.Clear();

        Serilog.Log.Logger.Information("[RefIndex:BuildReverse] === START: iterating {TypeCount} types", _store.ReferenceLookups.Count);

        foreach (var (sourceType, entities) in _store.ReferenceLookups)
        {
            var refProps = sourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetCustomAttribute<ReferenceFieldAttribute>() is not null)
                .ToList();

            if (refProps.Count == 0)
            {
                Serilog.Log.Logger.Information("[RefIndex:BuildReverse] SKIP type={Type} — no ReferenceField props", sourceType.Name);
                continue;
            }

            var entityCount = entities.Count(e => e is IEntity);
            Serilog.Log.Logger.Information(
                "[RefIndex:BuildReverse] Processing type={Type} entityCount={EntityCount} refProps=[{RefProps}]",
                sourceType.Name, entityCount, string.Join(", ", refProps.Select(p => p.Name)));

            int typeResolved = 0, typeUnresolved = 0, typeSkippedEmpty = 0;

            foreach (var obj in entities)
            {
                if (obj is not IEntity sourceEntity) continue;

                // ── Creature-specific diagnostics ──
                if (sourceType.Name == "Creature")
                {
                    _store.EntityNamespaces.TryGetValue(sourceEntity.EntityId, out var crNs);
                    _store.EntityModNames.TryGetValue(sourceEntity.EntityId, out var crMod);
                    Serilog.Log.Logger.Information(
                        "[RefIndex:BuildReverse] Creature entity eid={Eid} mod={Mod} ns={Ns} modId={ModId} Faction='{Faction}' AttackModes='{AttackModes}' BaseConditions='{BaseConditions}' TreasureId='{TreasureId}'",
                        sourceEntity.EntityId, crMod ?? "?", crNs ?? "?", sourceEntity.ModId,
                        sourceType.GetProperty("Faction")?.GetValue(sourceEntity)?.ToString() ?? "(null)",
                        sourceType.GetProperty("AttackModes")?.GetValue(sourceEntity)?.ToString() ?? "(null)",
                        sourceType.GetProperty("BaseConditions")?.GetValue(sourceEntity)?.ToString() ?? "(null)",
                        sourceType.GetProperty("TreasureId")?.GetValue(sourceEntity)?.ToString() ?? "(null)");
                }

                foreach (var prop in refProps)
                {
                    var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>()!;
                    var rawValue = prop.GetValue(sourceEntity)?.ToString();
                    if (string.IsNullOrWhiteSpace(rawValue))
                    {
                        typeSkippedEmpty++;
                        continue;
                    }

                    var ids = ReferenceParser.ExtractIds(rawValue, refAttr);
                    foreach (var (extractedId, _) in ids)
                    {
                        var targetEntityId = Lookup(sourceEntity.EntityId, prop.Name,
                            refAttr.TargetEntityType, extractedId)
                            ?? (refAttr.SecondaryTargetEntityType is not null
                                ? Lookup(sourceEntity.EntityId, prop.Name, refAttr.SecondaryTargetEntityType, extractedId)
                                : null);

                        // FallbackLookup: handle formats that the O(1) index can't resolve
                        // (e.g., "0:mergeId" when _mergedIdIndex was overwritten by a different-ns entity,
                        // or composite keys like "90.5" whose resolution varies per source context)
                        if (targetEntityId is null)
                        {
                            _store.EntityNamespaces.TryGetValue(sourceEntity.EntityId, out var srcNs);
                            var normalizedSrcNs = ReferenceParser.NormalizeNamespace(srcNs);
                            _store.EntityModNames.TryGetValue(sourceEntity.EntityId, out var srcModName);
                            Serilog.Log.Logger.Information(
                                "[RefIndex:BuildReverse] FallbackLookup PRIMARY for {SrcType}::{Prop} rawId={RawId} srcModId={SrcModId} srcMod={SrcMod} srcNs={SrcNs} srcEid={SrcEid} targetType={TargetType}",
                                sourceType.Name, prop.Name, extractedId, sourceEntity.ModId, srcModName ?? "?", normalizedSrcNs, sourceEntity.EntityId, refAttr.TargetEntityType.Name);
                            targetEntityId = ReferenceResolver.Instance.FallbackLookup(
                                refAttr.TargetEntityType, extractedId,
                                normalizedSrcNs, sourceEntity.ModId)?.EntityId;
                            _store.EntityModNames.TryGetValue(targetEntityId ?? "", out var tgtMod);
                            Serilog.Log.Logger.Information(
                                "[RefIndex:BuildReverse] FallbackLookup PRIMARY result for {RawId} → {Result} (mod={TgtMod})",
                                extractedId, targetEntityId ?? "(null)", tgtMod ?? "?");
                            if (targetEntityId is null && refAttr.SecondaryTargetEntityType is not null)
                            {
                                Serilog.Log.Logger.Information(
                                    "[RefIndex:BuildReverse] FallbackLookup SECONDARY for {RawId} targetType={SecTargetType}",
                                    extractedId, refAttr.SecondaryTargetEntityType.Name);
                                targetEntityId = ReferenceResolver.Instance.FallbackLookup(
                                    refAttr.SecondaryTargetEntityType, extractedId,
                                    normalizedSrcNs, sourceEntity.ModId)?.EntityId;
                                _store.EntityModNames.TryGetValue(targetEntityId ?? "", out var tgtMod2);
                                Serilog.Log.Logger.Information(
                                    "[RefIndex:BuildReverse] FallbackLookup SECONDARY result for {RawId} → {Result} (mod={TgtMod})",
                                    extractedId, targetEntityId ?? "(null)", tgtMod2 ?? "?");
                            }
                            if (targetEntityId is null)
                            {
                                Serilog.Log.Logger.Information(
                                    "[RefIndex:BuildReverse] UNRESOLVED reference: {SrcType}::{Prop} rawId={RawId} srcEid={SrcEid} srcMod={SrcMod} srcNs={SrcNs} targetType={TargetType}",
                                    sourceType.Name, prop.Name, extractedId, sourceEntity.EntityId, srcModName ?? "?", normalizedSrcNs, refAttr.TargetEntityType.Name);
                            }
                        }

                        if (targetEntityId is not null)
                        {
                            if (!_reverse.TryGetValue(targetEntityId, out var refs))
                                _reverse[targetEntityId] = refs = new();
                            refs.Add((sourceEntity.EntityId, prop.Name, extractedId));
                            typeResolved++;
                        }
                        else
                        {
                            typeUnresolved++;
                        }
                    }
                }
            }

            Serilog.Log.Logger.Information(
                "[RefIndex:BuildReverse] DONE type={Type}: resolved={Resolved} unresolved={Unresolved} skippedEmpty={SkippedEmpty}",
                sourceType.Name, typeResolved, typeUnresolved, typeSkippedEmpty);
        }

        Serilog.Log.Logger.Information(
            "[RefIndex:BuildReverse] === END: reverse index built with {RevCount} entries", _reverse.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Lookup
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resolve a reference to an EntityId.
    ///
    /// Rules:
    ///   - Has namespace prefix (e.g. "NSE:3")  → lookup by (type, namespace, primary key)
    ///   - No namespace prefix (e.g. "3")       → lookup by (type, MergedId),
    ///     with same-namespace priority and ModId cap (≤ source ModId).
    /// </summary>
    public string? Lookup(string sourceEntityId, string propertyName, Type targetType, string rawId)
    {
        if (string.IsNullOrWhiteSpace(rawId)) return null;

        // ── Entry log for Creature-related lookups (Faction, AttackMode, Condition, TreasureTable) ──
        var targetTypeName = targetType.Name;
        if (targetTypeName is "Faction" or "AttackMode" or "Condition" or "TreasureTable"
            || propertyName is "Faction" or "AttackModes" or "BaseConditions" or "TreasureId")
        {
            _store.EntityNamespaces.TryGetValue(sourceEntityId, out var elNs);
            _store.EntityModNames.TryGetValue(sourceEntityId, out var elMod);
            Serilog.Log.Logger.Information(
                "[RefIndex:Lookup] ENTER prop={Prop} targetType={TargetType} rawId={RawId} srcEid={SrcEid} srcNs={SrcNs} srcMod={SrcMod}",
                propertyName, targetTypeName, rawId, sourceEntityId,
                ReferenceParser.NormalizeNamespace(elNs), elMod ?? "?");
        }

        // ── Source context: same-ns priority + ModId cap ──
        _store.EntityNamespaces.TryGetValue(sourceEntityId, out var sourceNsRaw);
        var sourceNs = ReferenceParser.NormalizeNamespace(sourceNsRaw);
        var sourceModId = GetSourceModId(sourceEntityId);

        // Parse namespace prefix
        string? nsPrefix = null;
        var idOnly = rawId;
        var colonIdx = rawId.IndexOf(':');
        if (colonIdx > 0)
        {
            nsPrefix = rawId[..colonIdx];
            idOnly = rawId[(colonIdx + 1)..];
        }

        var lookupKey = ReferenceParser.BuildLookupKey(idOnly);
        if (lookupKey.Length == 0) return null;

        if (nsPrefix is not null)
        {
            // Normalize namespace: both "0" and "" map to default namespace
            var normalizedNs = ReferenceParser.NormalizeNamespace(nsPrefix);

            // Fast path: "0:mergeId" or ":mergeId" → direct MergedId lookup (O(1))
            // This handles references like "0:42" where the number is a MergedId, not a primary key.
            if ((nsPrefix == "0" || nsPrefix == "") && int.TryParse(lookupKey, out var directMid))
            {
                Serilog.Log.Logger.Information(
                    "[RefIndex:Lookup] 0:mid attempt: type={Type} mid={Mid} rawId={RawId} src={SrcEid} srcModId={SrcModId} srcNs={SrcNs}",
                    targetType.Name, directMid, rawId, sourceEntityId, sourceModId, sourceNs ?? "(none)");

                // Save mergedIdIndex hit for fallback (scope outside TryGetValue block)
                string? directMergedEid = null;
                string? directMergedModName = null;
                string? directMergedNsNorm = null;

                if (_mergedIdIndex.TryGetValue((targetType, directMid), out var directEid))
                {
                    _store.EntityModNames.TryGetValue(directEid, out var idxMod);
                    _store.EntityNamespaces.TryGetValue(directEid, out var idxNs);
                    var idxNsNorm = ReferenceParser.NormalizeNamespace(idxNs);
                    Serilog.Log.Logger.Information(
                        "[RefIndex:Lookup] 0:mid mergedIdIndex hit: eid={Eid} mod={Mod} ns={Ns} nsNorm={NsNorm}",
                        directEid, idxMod ?? "?", idxNs ?? "?", idxNsNorm);

                    if (idxNsNorm == "")
                    {
                        if (ValidateModIdCap(directEid, sourceModId, targetType))
                        {
                            Serilog.Log.Logger.Information(
                                "[RefIndex:Lookup] 0:mid fast-path OK: type={Type} mid={Mid} → {Eid} mod={Mod}",
                                targetType.Name, directMid, directEid, idxMod ?? "?");
                            return directEid;
                        }
                        Serilog.Log.Logger.Information(
                            "[RefIndex:Lookup] 0:mid fast-path ModId cap exceeded mid={Mid} → {Eid} mod={Mod} srcModId={SrcModId}",
                            directMid, directEid, idxMod ?? "?", sourceModId);
                    }
                    else
                    {
                        Serilog.Log.Logger.Information(
                            "[RefIndex:Lookup] 0:mid mergedIdIndex entry is non-default ns={Ns} — scanning entities for default-ns match",
                            idxNsNorm);
                        // Save for cross-mod MergedId fallback (entity-scan may miss due to ModId cap)
                        directMergedEid = directEid;
                        directMergedModName = idxMod;
                        directMergedNsNorm = idxNsNorm;
                    }
                }
                else
                {
                    Serilog.Log.Logger.Information(
                        "[RefIndex:Lookup] 0:mid mergedIdIndex miss mid={Mid} for type={Type}",
                        directMid, targetType.Name);
                }

                // Direct entity scan: find entity with matching MergedId in default namespace
                // This handles the case where _mergedIdIndex was overwritten by a non-default-ns entity
                if (_store.ReferenceLookups.TryGetValue(targetType, out var directScanList))
                {
                    string? directBest = null;
                    int directBestModId = int.MinValue;
                    foreach (var obj in directScanList)
                    {
                        if (obj is not IEntity e) continue;
                        if (sourceModId < int.MaxValue && e.ModId < sourceModId) continue;
                        if (!_store.EntityNamespaces.TryGetValue(e.EntityId, out var eNs)
                            || ReferenceParser.NormalizeNamespace(eNs) != "") continue;
                        if (!_store.EntityMergedIds.TryGetValue(e.EntityId, out var eMid)
                            || eMid != directMid) continue;
                        if (e.ModId > directBestModId)
                        { directBestModId = e.ModId; directBest = e.EntityId; }
                    }
                    if (directBest is not null)
                    {
                        _store.EntityModNames.TryGetValue(directBest, out var directBestMod);
                        Serilog.Log.Logger.Information(
                            "[RefIndex:Lookup] 0:mid entity-scan HIT: mid={Mid} → {Eid} mod={Mod}",
                            directMid, directBest, directBestMod ?? "?");
                        return directBest;
                    }
                    Serilog.Log.Logger.Information(
                        "[RefIndex:Lookup] 0:mid entity-scan MISS: mid={Mid} no default-ns entity for type={Type} (scanned {Count} entities)",
                        directMid, targetType.Name, directScanList.Count);
                }

                // Cross-mod MergedId fallback: if the entity-scan missed (e.g. due to ModId cap)
                // accept the mergedIdIndex hit even if it's from a non-default namespace.
                // This allows NSExtended mod entities to reference base-game entities by MergedId.
                if (directMergedEid is not null)
                {
                    Serilog.Log.Logger.Information(
                        "[RefIndex:Lookup] 0:mid cross-mod MergedId fallback: mid={Mid} → {Eid} mod={Mod} ns={Ns}",
                        directMid, directMergedEid, directMergedModName ?? "?", directMergedNsNorm ?? "?");
                    return directMergedEid;
                }
                // Fall through to normal NS lookup (may succeed via pk match)
            }
            else if ((nsPrefix == "0" || nsPrefix == "") && !int.TryParse(lookupKey, out _))
            {
                // "0:compositeKey" — look up by composite key in default namespace
                Serilog.Log.Logger.Debug(
                    "[RefIndex:Lookup] 0:composite-key attempt: type={Type} key={Key} srcModId={SrcModId}",
                    targetType.Name, lookupKey, sourceModId);
                var compositeResult = LookupByCompositeKeyInNs(targetType, lookupKey, nsPrefix, sourceModId);
                if (compositeResult is not null)
                {
                    _store.EntityModNames.TryGetValue(compositeResult, out var compMod);
                    Serilog.Log.Logger.Debug(
                        "[RefIndex:Lookup] 0:composite-key hit: ns={Ns} key={Key} → {Eid} mod={Mod}",
                        nsPrefix, lookupKey, compositeResult, compMod ?? "?");
                    return compositeResult;
                }
                Serilog.Log.Logger.Debug(
                    "[RefIndex:Lookup] 0:composite-key miss: ns={Ns} key={Key} for type={Type}",
                    nsPrefix, lookupKey, targetType.Name);
            }

            // Namespace-prefixed: lookup by (type, namespace, primary key)
            Serilog.Log.Logger.Debug(
                "[RefIndex:Lookup] NS route: type={Type} ns={Ns} (raw={RawNs}) pk={Pk} rawId={RawId} src={Src} srcModId={SrcModId}",
                targetType.Name, normalizedNs, nsPrefix, lookupKey, rawId, sourceEntityId, sourceModId);

            if (_nsIndex.TryGetValue((targetType, normalizedNs, lookupKey), out var eid))
            {
                if (ValidateModIdCap(eid, sourceModId, targetType))
                {
                    _store.EntityModNames.TryGetValue(eid, out var mod);
                    var entityPk = TryGetEntityPrimaryKey(eid, targetType);
                    Serilog.Log.Logger.Debug("[RefIndex:Lookup] NS hit → {Eid} mod={Mod} pk={Pk}", eid, mod ?? "?", entityPk ?? "?");
                    return eid;
                }
                // ModId cap exceeded — fall through to MergedId fallback
                Serilog.Log.Logger.Debug("[RefIndex:Lookup] NS hit ModId cap exceeded → {Eid}", eid);
            }

            // Also try raw nsPrefix (for non-normalized namespaces like "NSE")
            if (normalizedNs != nsPrefix && _nsIndex.TryGetValue((targetType, nsPrefix, lookupKey), out var rawEid))
            {
                if (ValidateModIdCap(rawEid, sourceModId, targetType))
                {
                    _store.EntityModNames.TryGetValue(rawEid, out var rawMod);
                    Serilog.Log.Logger.Debug("[RefIndex:Lookup] NS raw hit → {Eid} mod={Mod}", rawEid, rawMod ?? "?");
                    return rawEid;
                }
            }

            // Also try mapping through NamespaceToModName
            if (_store.NamespaceToModName.TryGetValue(nsPrefix, out var mapped)
                && _nsIndex.TryGetValue((targetType, mapped, lookupKey), out var mappedEid))
            {
                if (ValidateModIdCap(mappedEid, sourceModId, targetType))
                {
                    _store.EntityModNames.TryGetValue(mappedEid, out var mappedMod);
                    Serilog.Log.Logger.Debug("[RefIndex:Lookup] NS mapped hit ns={Mapped} → {Eid} mod={Mod}", mapped, mappedEid, mappedMod ?? "?");
                    return mappedEid;
                }
            }

            // Try NamespaceToModName with normalized ns
            if (normalizedNs != nsPrefix
                && _store.NamespaceToModName.TryGetValue(normalizedNs, out var mapped2)
                && _nsIndex.TryGetValue((targetType, mapped2, lookupKey), out var mappedEid2))
            {
                if (ValidateModIdCap(mappedEid2, sourceModId, targetType))
                {
                    _store.EntityModNames.TryGetValue(mappedEid2, out var mappedMod2);
                    Serilog.Log.Logger.Debug("[RefIndex:Lookup] NS mapped2 hit ns={Mapped} → {Eid} mod={Mod}", mapped2, mappedEid2, mappedMod2 ?? "?");
                    return mappedEid2;
                }
            }

            // Fallback: try MergedId-based lookup within the namespace prefix
            if (int.TryParse(lookupKey, out var nsMergedId))
            {
                foreach (var ((t, mid), fallbackEid) in _mergedIdIndex)
                {
                    if (t != targetType || mid != nsMergedId) continue;
                    if (!_store.EntityNamespaces.TryGetValue(fallbackEid, out var eidNs)) continue;
                    // Normalize: both "" and "0" mean default namespace
                    var fallbackNs = ReferenceParser.NormalizeNamespace(eidNs);
                    var normalizedPrefix = ReferenceParser.NormalizeNamespace(nsPrefix);
                    if (fallbackNs == normalizedPrefix)
                    {
                        if (!ValidateModIdCap(fallbackEid, sourceModId, targetType)) continue;
                        _store.EntityModNames.TryGetValue(fallbackEid, out var eidMod);
                        Serilog.Log.Logger.Debug("[RefIndex:Lookup] NS MergedId fallback ns={Ns} mid={Mid} → {Eid} mod={Mod}",
                            nsPrefix, nsMergedId, fallbackEid, eidMod ?? "?");
                        return fallbackEid;
                    }
                }

                // Last resort: scan full entity list (handles case where
                // _mergedIdIndex was overwritten by a different-ns entity)
                if (_store.ReferenceLookups.TryGetValue(targetType, out var entityList))
                {
                    string? lastResortBest = null;
                    int lastResortBestModId = int.MinValue;
                    var normalizedPrefix2 = ReferenceParser.NormalizeNamespace(nsPrefix);
                    foreach (var obj in entityList)
                    {
                        if (obj is not IEntity e) continue;
                        if (sourceModId < int.MaxValue && e.ModId < sourceModId) continue;
                        if (!_store.EntityNamespaces.TryGetValue(e.EntityId, out var eNs)
                            || ReferenceParser.NormalizeNamespace(eNs) != normalizedPrefix2) continue;
                        if (!_store.EntityMergedIds.TryGetValue(e.EntityId, out var eMid)
                            || eMid != nsMergedId) continue;
                        if (e.ModId > lastResortBestModId)
                        { lastResortBestModId = e.ModId; lastResortBest = e.EntityId; }
                    }
                    if (lastResortBest is not null)
                    {
                        Serilog.Log.Logger.Debug(
                            "[RefIndex:Lookup] NS MergedId entity-scan ns={Ns} mid={Mid} → {Eid}",
                            nsPrefix, nsMergedId, lastResortBest);
                        return lastResortBest;
                    }
                }
            }

            Serilog.Log.Logger.Debug("[RefIndex:Lookup] NS miss — nsIdx has {Count} entries for {Type}",
                _nsIndex.Count(kv => kv.Key.EntityType == targetType), targetType.Name);
            return null;
        }
        else
        {
            // No namespace prefix: lookup by primary key first (within source's namespace),
            // then fall back to MergedId.
            // Rule: same-ns pk match > MergedId (with same-mod/same-ns priority + ModId cap)
            Serilog.Log.Logger.Information(
                "[RefIndex:Lookup] NO-PREFIX route: type={Type} lookupKey={Key} rawId={RawId} src={Src} srcNs={SrcNs} srcModId={SrcModId}",
                targetType.Name, lookupKey, rawId, sourceEntityId, sourceNs ?? "(none)", sourceModId);

            if (int.TryParse(lookupKey, out var pkValue))
            {
                // Priority 1: same-ns + primary key match (via nsIndex)
                if (_nsIndex.TryGetValue((targetType, sourceNs, lookupKey), out var nsPkEid))
                {
                    if (ValidateModIdCap(nsPkEid, sourceModId, targetType))
                    {
                        _store.EntityModNames.TryGetValue(nsPkEid, out var nsPkMod);
                        var nsPkEntityPk = TryGetEntityPrimaryKey(nsPkEid, targetType);
                        Serilog.Log.Logger.Information(
                            "[RefIndex:Lookup] NO-PREFIX same-ns pk hit: ns={Ns} pk={Pk} → {Eid} mod={Mod} entityPk={EntityPk}",
                            sourceNs, lookupKey, nsPkEid, nsPkMod ?? "?", nsPkEntityPk ?? "?");
                        return nsPkEid;
                    }
                    Serilog.Log.Logger.Information(
                        "[RefIndex:Lookup] NO-PREFIX same-ns pk hit but ModId cap exceeded: pk={Pk} → {Eid}",
                        lookupKey, nsPkEid);
                    // Fall through to MergedId fallback (may find a valid entity)
                }

                // Priority 2: MergedId-based lookup (fallback / default-ns sources)
                if (_mergedIdIndex.TryGetValue((targetType, pkValue), out var eid))
                {
                    _store.EntityModNames.TryGetValue(eid, out var foundMod);
                    _store.EntityNamespaces.TryGetValue(eid, out var foundNs);
                    var entityPk = TryGetEntityPrimaryKey(eid, targetType);
                    var foundNsNorm = ReferenceParser.NormalizeNamespace(foundNs);
                    var foundModId = GetEntityModId(eid, targetType);

                    // Same-mod priority: if found entity is from a different mod,
                    // fall through to ReferenceResolver.FallbackLookup which
                    // prioritizes same-mod entities with the same MergedId.
                    if (foundModId != sourceModId && sourceModId < int.MaxValue)
                    {
                        // Log all candidates with same MergedId for diagnostics
                        if (_store.ReferenceLookups.TryGetValue(targetType, out var sameMidList))
                        {
                            var sameMidCandidates = new System.Text.StringBuilder();
                            foreach (var obj in sameMidList)
                            {
                                if (obj is not IEntity ce) continue;
                                if (!_store.EntityMergedIds.TryGetValue(ce.EntityId, out var cmid) || cmid != pkValue) continue;
                                _store.EntityModNames.TryGetValue(ce.EntityId, out var cmod);
                                sameMidCandidates.Append($" {ce.EntityId}(mod={ce.ModId}:{cmod ?? "?"})");
                            }
                            Serilog.Log.Logger.Information(
                                "[RefIndex:Lookup] MergedId hit mid={Mid} → {Eid} mod={FoundMod}(id={FoundModId}) ≠ src={SrcModId} — falling through. All same-mid candidates:[{Candidates}]",
                                pkValue, eid, foundMod ?? "?", foundModId, sourceModId, sameMidCandidates.ToString());
                        }
                        else
                        {
                            Serilog.Log.Logger.Information(
                                "[RefIndex:Lookup] MergedId hit mid={Mid} → {Eid} mod={FoundMod}(id={FoundModId}) ≠ src={SrcModId} — falling through for same-mod priority",
                                pkValue, eid, foundMod ?? "?", foundModId, sourceModId);
                        }
                        return null;
                    }

                    // Validate same-namespace priority
                    if (!string.IsNullOrEmpty(sourceNs) && foundNsNorm != sourceNs)
                    {
                        Serilog.Log.Logger.Information(
                            "[RefIndex:Lookup] MergedId hit mid={Mid} → {Eid} but ns mismatch (found={FoundNs} src={SrcNs}) — falling through",
                            pkValue, eid, foundNsNorm, sourceNs);
                        return null;
                    }

                    // ModId cap
                    if (!ValidateModIdCap(eid, sourceModId, targetType))
                    {
                        Serilog.Log.Logger.Information(
                            "[RefIndex:Lookup] MergedId hit mid={Mid} → {Eid} but ModId cap exceeded → null",
                            pkValue, eid);
                        return null;
                    }

                    Serilog.Log.Logger.Information(
                        "[RefIndex:Lookup] MergedId hit mid={Mid} → {Eid} mod={Mod} ns={Ns} pk={Pk}",
                        pkValue, eid, foundMod ?? "?", foundNs ?? "?", entityPk ?? "?");
                    return eid;
                }
                Serilog.Log.Logger.Information(
                    "[RefIndex:Lookup] MergedId miss mid={Mid} — mergedIdIdx has {Count} entries for {Type}",
                    pkValue,
                    _mergedIdIndex.Count(kv => kv.Key.EntityType == targetType),
                    targetType.Name);
            }
            else
            {
                // Composite key without prefix (e.g., "90.5" for ItemType):
                // scan by composite key, same-mod priority, ModId cap
                var compositeResult = LookupByCompositeKey(targetType, lookupKey, sourceModId, sourceNs);
                if (compositeResult is not null)
                {
                    Serilog.Log.Logger.Information(
                        "[RefIndex:Lookup] Composite key '{Key}' → {Eid}", lookupKey, compositeResult);
                    return compositeResult;
                }
                Serilog.Log.Logger.Information(
                    "[RefIndex:Lookup] lookupKey '{Key}' is not a valid integer and no composite match — returning null",
                    lookupKey);
            }

            return null;
        }
    }

    /// <summary>Get the ModId of the source entity for ModId cap validation.</summary>
    private int GetSourceModId(string sourceEntityId)
    {
        if (string.IsNullOrEmpty(sourceEntityId)) return int.MaxValue;
        if (_store.ReferenceLookups is null) return int.MaxValue;
        foreach (var (_, entities) in _store.ReferenceLookups)
        {
            foreach (var obj in entities)
            {
                if (obj is IEntity e && e.EntityId == sourceEntityId)
                    return e.ModId;
            }
        }
        return int.MaxValue;
    }

    /// <summary>Get the ModId of a target entity for same-mod priority checks.</summary>
    private int GetEntityModId(string entityId, Type entityType)
    {
        if (!_store.ReferenceLookups.TryGetValue(entityType, out var list) || list is null)
            return int.MaxValue;
        foreach (var obj in list)
        {
            if (obj is IEntity e && e.EntityId == entityId)
                return e.ModId;
        }
        return int.MaxValue;
    }

    /// <summary>Validate that the found entity's ModId ≤ source ModId (cap rule).</summary>
    private bool ValidateModIdCap(string entityId, int sourceModId, Type entityType)
    {
        if (sourceModId >= int.MaxValue) return true; // no cap
        if (!_store.ReferenceLookups.TryGetValue(entityType, out var list) || list is null)
            return true; // can't validate, accept
        foreach (var obj in list)
        {
            if (obj is IEntity e && e.EntityId == entityId)
                return e.ModId >= sourceModId; // ModId cap: allow only same-or-higher priority mods (more positive / less negative)
        }
        return true; // entity not found in list, accept (caller will handle)
    }

    /// <summary>
    /// Look up an entity by composite key (e.g., "90.5" for ItemType GroupId.SubgroupId).
    /// Priority: same-mod → same-ns → highest ModId (within ModId cap).
    /// </summary>
    private string? LookupByCompositeKey(Type targetType, string lookupKey, int sourceModId, string? sourceNs)
    {
        if (!_store.ReferenceLookups.TryGetValue(targetType, out var entities) || entities is null)
            return null;
        var dotIdx = lookupKey.IndexOf('.');
        if (dotIdx <= 0 || dotIdx >= lookupKey.Length - 1) return null;
        if (!int.TryParse(lookupKey[..dotIdx], out var gid)) return null;
        if (!int.TryParse(lookupKey[(dotIdx + 1)..], out var sid)) return null;
        var gp = targetType.GetProperty("GroupId", BindingFlags.Instance | BindingFlags.Public);
        var sp = targetType.GetProperty("SubgroupId", BindingFlags.Instance | BindingFlags.Public);
        if (gp is null || sp is null) return null;

        Serilog.Log.Logger.Debug(
            "[RefIndex:LookupCompositeKey] type={Type} key={Key} (gid={Gid},sid={Sid}) sourceModId={SrcModId} sourceNs={SrcNs}",
            targetType.Name, lookupKey, gid, sid, sourceModId, sourceNs ?? "(none)");

        string? sameModBest = null, sameNsBest = null, globalBest = null;
        int sameNsBestModId = int.MinValue, globalBestModId = int.MinValue;
        var candidates = new System.Text.StringBuilder();
        foreach (var obj in entities)
        {
            if (obj is not IEntity e) continue;
            if (sourceModId < int.MaxValue && e.ModId < sourceModId) continue; // ModId cap: skip lower-priority mods (more negative)
            if (gp.GetValue(e) is not int eg || eg != gid) continue;
            if (sp.GetValue(e) is not int es || es != sid) continue;
            candidates.Append($" {e.EntityId}(mod={e.ModId})");
            if (e.ModId == sourceModId) { sameModBest = e.EntityId; break; }
            _store.EntityNamespaces.TryGetValue(e.EntityId, out var eNs);
            var eNsNorm = ReferenceParser.NormalizeNamespace(eNs);
            var srcNsNorm = ReferenceParser.NormalizeNamespace(sourceNs);
            if (!string.IsNullOrEmpty(srcNsNorm) && eNsNorm == srcNsNorm)
            {
                if (e.ModId > sameNsBestModId) { sameNsBestModId = e.ModId; sameNsBest = e.EntityId; }
            }
            else if (e.ModId > globalBestModId)
            { globalBestModId = e.ModId; globalBest = e.EntityId; }
        }
        var result = sameModBest ?? sameNsBest ?? globalBest;
        Serilog.Log.Logger.Debug(
            "[RefIndex:LookupCompositeKey] result={Result} candidates=[{Candidates}] sameMod={SameMod} sameNs={SameNs} global={Global}",
            result ?? "(null)", candidates.ToString(), sameModBest ?? "-", sameNsBest ?? "-", globalBest ?? "-");
        return result;
    }

    /// <summary>
    /// Look up an entity by composite key within a specific namespace
    /// (for "0:90.5" or "NSE:90.5" references where the key is composite).
    /// </summary>
    private string? LookupByCompositeKeyInNs(Type targetType, string lookupKey, string nsPrefix, int sourceModId)
    {
        if (!_store.ReferenceLookups.TryGetValue(targetType, out var entities) || entities is null)
            return null;
        var dotIdx = lookupKey.IndexOf('.');
        if (dotIdx <= 0 || dotIdx >= lookupKey.Length - 1) return null;
        if (!int.TryParse(lookupKey[..dotIdx], out var gid)) return null;
        if (!int.TryParse(lookupKey[(dotIdx + 1)..], out var sid)) return null;
        var gp = targetType.GetProperty("GroupId", BindingFlags.Instance | BindingFlags.Public);
        var sp = targetType.GetProperty("SubgroupId", BindingFlags.Instance | BindingFlags.Public);
        if (gp is null || sp is null) return null;
        var normalizedNs = ReferenceParser.NormalizeNamespace(nsPrefix);
        string? best = null; int bestModId = int.MinValue;
        foreach (var obj in entities)
        {
            if (obj is not IEntity e) continue;
            if (sourceModId < int.MaxValue && e.ModId < sourceModId) continue; // ModId cap: skip lower-priority mods (more negative)
            if (gp.GetValue(e) is not int eg || eg != gid) continue;
            if (sp.GetValue(e) is not int es || es != sid) continue;
            _store.EntityNamespaces.TryGetValue(e.EntityId, out var eNs);
            if (ReferenceParser.NormalizeNamespace(eNs) != normalizedNs) continue;
            if (e.ModId > bestModId) { bestModId = e.ModId; best = e.EntityId; }
        }
        return best;
    }

    /// <summary>
    /// Simple lookup without source context (for reverse index building etc).
    /// </summary>
    public string? Lookup(Type targetType, string rawId)
        => Lookup("", "", targetType, rawId);

    /// <summary>
    /// Look up display info (Subject, ModName) for a raw ID with source context.
    /// </summary>
    public (string? Subject, string? ModName) LookupDisplay(string sourceEntityId, string propertyName,
        Type targetType, string rawId)
    {
        var entityId = Lookup(sourceEntityId, propertyName, targetType, rawId);
        if (entityId is null) return (null, null);

        if (_display.TryGetValue(entityId, out var cached))
            return cached;

        if (_store.ReferenceLookups.TryGetValue(targetType, out var entities))
        {
            foreach (var obj in entities)
            {
                if (obj is IEntity e && e.EntityId == entityId)
                {
                    _store.EntityModNames.TryGetValue(entityId, out var modName);
                    var result = (e.Subject, modName);
                    _display[entityId] = result;
                    return result;
                }
            }
        }
        return (null, null);
    }

    // ── Simple lookup (no source context) ───────────────────────────────────

    public string? LookupGlobal(Type targetType, string rawId)
        => Lookup("", "", targetType, rawId);

    public (string? Subject, string? ModName) LookupDisplayGlobal(Type targetType, string rawId)
        => LookupDisplay("", "", targetType, rawId);

    // ═══════════════════════════════════════════════════════════════════════
    //  Reverse (for "Referenced By" views)
    // ═══════════════════════════════════════════════════════════════════════

    public IReadOnlyList<(string SourceEntityId, string PropertyName, string RawId)> ReverseLookup(string entityId)
        => _reverse.TryGetValue(entityId, out var refs) ? refs : Array.Empty<(string, string, string)>();

    /// <summary>Number of entries in the MergedId index. Used for cache validation.</summary>
    public int MergedFallbackCount => _mergedIdIndex.Count;

    // ═══════════════════════════════════════════════════════════════════════
    //  Incremental updates
    // ═══════════════════════════════════════════════════════════════════════

    public void UpdateField(IEntity entity, string propertyName, string? oldValue, string? newValue)
    {
        // For incremental edits, just remove old entries and add new ones directly.
        var sourceEid = entity.EntityId;
        var propInfo = entity.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (propInfo is null) return;
        var refAttr = propInfo.GetCustomAttribute<ReferenceFieldAttribute>();
        if (refAttr is null) return;

        // Remove old
        if (!string.IsNullOrWhiteSpace(oldValue))
            RemoveFieldEntries(sourceEid, propertyName, refAttr, oldValue);

        // Add new
        if (!string.IsNullOrWhiteSpace(newValue))
            IndexReferenceField(entity, propertyName, refAttr, newValue);
    }

    private void RemoveFieldEntries(string sourceEid, string propName, ReferenceFieldAttribute refAttr, string rawValue)
    {
        var parts = refAttr.Separator is not null ? rawValue.Split(refAttr.Separator) : [rawValue];
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0) continue;
            var pat = ReferencePattern.FromName(refAttr.Pattern);
            var rawId = pat.ExtractRawId(trimmed).Trim();
            if (rawId.Length == 0) continue;
            var lookupKey = ReferenceParser.BuildLookupKey(rawId);
            if (lookupKey.Length == 0) continue;

            // Remove reverse entries for this (source, prop, rawId) combination
            // We need to find the target EntityId to clean up reverse
            var targetEid = Lookup(sourceEid, propName, refAttr.TargetEntityType, rawId);
            if (targetEid is not null && _reverse.TryGetValue(targetEid, out var refs))
            {
                refs.RemoveAll(r => r.SourceEntityId == sourceEid && r.PropertyName == propName && r.RawId == rawId);
                if (refs.Count == 0) _reverse.Remove(targetEid);
            }
        }
    }

    public void AddEntity(IEntity entity)
    {
        var sourceType = entity.GetType();
        var refProps = sourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.GetCustomAttribute<ReferenceFieldAttribute>() is not null);
        foreach (var prop in refProps)
        {
            var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>()!;
            var rawValue = prop.GetValue(entity)?.ToString();
            if (!string.IsNullOrWhiteSpace(rawValue))
                IndexReferenceField(entity, prop.Name, refAttr, rawValue);
        }
    }

    /// <summary>Index a single reference field value (for incremental updates).</summary>
    private void IndexReferenceField(IEntity sourceEntity, string propName,
        ReferenceFieldAttribute refAttr, string rawValue)
    {
        var parts = refAttr.Separator is not null
            ? rawValue.Split(refAttr.Separator)
            : [rawValue];

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0) continue;

            var pat = ReferencePattern.FromName(refAttr.Pattern);
            var rawId = pat.ExtractRawId(trimmed).Trim();
            if (rawId.Length == 0 || rawId == "0") continue;

            var lookupKey = ReferenceParser.BuildLookupKey(rawId);
            if (lookupKey.Length == 0) continue;

            // Resolve using the standard Lookup, then FallbackLookup for formats the
            // O(1) index can't resolve (e.g., "0:mergeId" with overwritten _mergedIdIndex,
            // or composite keys whose resolution varies per source context)
            var targetEid = Lookup(sourceEntity.EntityId, propName, refAttr.TargetEntityType, rawId);
            if (targetEid is not null)
            {
                Serilog.Log.Logger.Information(
                    "[RefIndex:IndexRefField] Lookup OK: {SrcType}::{Prop} rawId={RawId} → {Eid} srcEid={SrcEid}",
                    sourceEntity.GetType().Name, propName, rawId, targetEid, sourceEntity.EntityId);
            }
            else
            {
                Serilog.Log.Logger.Information(
                    "[RefIndex:IndexRefField] Lookup miss: {SrcType}::{Prop} rawId={RawId} srcEid={SrcEid} — trying FallbackLookup",
                    sourceEntity.GetType().Name, propName, rawId, sourceEntity.EntityId);
                targetEid = (refAttr.SecondaryTargetEntityType is not null
                             ? Lookup(sourceEntity.EntityId, propName, refAttr.SecondaryTargetEntityType, rawId)
                             : null)
                         ?? ReferenceResolver.Instance.FallbackLookup(refAttr.TargetEntityType, rawId,
                             ReferenceParser.NormalizeNamespace(_store.EntityNamespaces.GetValueOrDefault(sourceEntity.EntityId)),
                             sourceEntity.ModId)?.EntityId
                         ?? (refAttr.SecondaryTargetEntityType is not null
                             ? ReferenceResolver.Instance.FallbackLookup(refAttr.SecondaryTargetEntityType, rawId,
                                 ReferenceParser.NormalizeNamespace(_store.EntityNamespaces.GetValueOrDefault(sourceEntity.EntityId)),
                                 sourceEntity.ModId)?.EntityId
                             : null);
                Serilog.Log.Logger.Information(
                    "[RefIndex:IndexRefField] FallbackLookup result: {SrcType}::{Prop} rawId={RawId} → {Result}",
                    sourceEntity.GetType().Name, propName, rawId, targetEid ?? "(null)");
            }

            if (targetEid is null) continue;

            // Update reverse index
            if (!_reverse.TryGetValue(targetEid, out var refs))
                _reverse[targetEid] = refs = new();
            refs.Add((sourceEntity.EntityId, propName, rawId));
        }
    }

    public void RemoveEntity(string entityId)
    {
        // Remove reverse entries
        _reverse.Remove(entityId);

        // Remove from core indices
        RemoveFromValueDict(_nsIndex, entityId);
        RemoveFromValueDict(_mergedIdIndex, entityId);

        // Remove display cache
        _display.Remove(entityId);
    }

    public void Clear()
    {
        _nsIndex.Clear();
        _mergedIdIndex.Clear();
        _reverse.Clear();
        _display.Clear();
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Disk persistence
    // ═══════════════════════════════════════════════════════════════════════

    public void SaveToDisk(string path)
    {
        var data = new IndexDiskData
        {
            Version = 13, // v13: add BuildReverse per-type + Creature diagnostic logging
            NsIndex = _nsIndex.Select(kv => new NsIdxEntry
            {
                Type = kv.Key.EntityType.FullName!,
                Ns = kv.Key.Ns,
                Pk = kv.Key.PrimaryKey,
                Tgt = kv.Value
            }).ToList(),
            MergedIdIndex = _mergedIdIndex.Select(kv => new MidIdxEntry
            {
                Type = kv.Key.EntityType.FullName!,
                Mid = kv.Key.MergedId,
                Tgt = kv.Value
            }).ToList(),
            Reverse = _reverse.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Select(v => new RevEntry { Src = v.SourceEntityId, Prop = v.PropertyName, Key = v.RawId }).ToList()),
            Display = _display.ToDictionary(
                kv => kv.Key,
                kv => new DispEntry { Sub = kv.Value.Subject, Mod = kv.Value.ModName })
        };
        var dir = System.IO.Path.GetDirectoryName(path)!;
        System.IO.Directory.CreateDirectory(dir);
        System.IO.File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(data));
    }

    public bool TryLoadFromDisk(string path)
    {
        if (!System.IO.File.Exists(path)) return false;
        try
        {
            var json = System.IO.File.ReadAllText(path);
            var data = System.Text.Json.JsonSerializer.Deserialize<IndexDiskData>(json);
            if (data is null) return false;

            // Invalidate caches built with an older index strategy
            if (data.Version < 13)
            {
                Serilog.Log.Logger.Information(
                    "[RefIndex:LoadDisk] Stale cache v{OldVersion} (current v13) — rebuilding", data.Version);
                return false;
            }

            Clear();
            foreach (var e in data.NsIndex)
            {
                var t = Type.GetType(e.Type);
                if (t is not null) _nsIndex[(t, e.Ns, e.Pk)] = e.Tgt;
            }
            foreach (var e in data.MergedIdIndex)
            {
                var t = Type.GetType(e.Type);
                if (t is not null) _mergedIdIndex[(t, e.Mid)] = e.Tgt;
            }
            foreach (var (k, v) in data.Reverse)
                _reverse[k] = v.Select(r => (r.Src, r.Prop, r.Key)).ToList();
            foreach (var (k, v) in data.Display)
                _display[k] = (v.Sub, v.Mod);

            Serilog.Log.Logger.Information(
                "[RefIndex:LoadDisk] nsIdx={Ns} mergedIdIdx={Mid} rev={Rev}",
                _nsIndex.Count, _mergedIdIndex.Count, _reverse.Count);

            return _nsIndex.Count > 0 || _mergedIdIndex.Count > 0;
        }
        catch (System.Exception ex)
        {
            Serilog.Log.Logger.Warning("[RefIndex:LoadDisk] Failed: {Msg}", ex.Message);
            return false;
        }
    }

    // ── Disk data types (v7) ────────────────────────────────────────────────

    private class IndexDiskData
    {
        public int Version { get; set; } = 13;
        public List<NsIdxEntry> NsIndex { get; set; } = new();
        public List<MidIdxEntry> MergedIdIndex { get; set; } = new();
        public Dictionary<string, List<RevEntry>> Reverse { get; set; } = new();
        public Dictionary<string, DispEntry> Display { get; set; } = new();
    }

    private class NsIdxEntry { public string Type { get; set; } = ""; public string Ns { get; set; } = ""; public string Pk { get; set; } = ""; public string Tgt { get; set; } = ""; }
    private class MidIdxEntry { public string Type { get; set; } = ""; public int Mid { get; set; } public string Tgt { get; set; } = ""; }
    private class RevEntry { public string Src { get; set; } = ""; public string Prop { get; set; } = ""; public string Key { get; set; } = ""; }
    private class DispEntry { public string? Sub { get; set; } public string? Mod { get; set; } }

    // ═══════════════════════════════════════════════════════════════════════
    //  Internals
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Compute the primary key string for an entity. Uses Id or nID property.</summary>
    /// <summary>Compute the primary key string for an entity. Tries Id first, then nID (same as MergeService).</summary>
    private static string? ComputeEntityKey(IEntity entity)
    {
        var entityType = entity.GetType();
        // Try Id first, then nID (same as MergeService.ResolveEntityKeyProperty)
        var keyProp = entityType.GetProperty("Id",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
            ?? entityType.GetProperty("nID",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (keyProp is null) return null;
        var val = keyProp.GetValue(entity);
        return val switch
        {
            int i => i.ToString(),
            long l => l.ToString(),
            string s => s,
            _ => null
        };
    }

    private static void RemoveFromValueDict<TKey>(Dictionary<TKey, string> dict, string entityId) where TKey : notnull
    {
        var keysToRemove = dict.Where(kv => kv.Value == entityId).Select(kv => kv.Key).ToList();
        foreach (var key in keysToRemove) dict.Remove(key);
    }

    /// <summary>Get an entity's primary key value for diagnostic logging.</summary>
    private string? TryGetEntityPrimaryKey(string entityId, Type entityType)
    {
        if (!_store.ReferenceLookups.TryGetValue(entityType, out var entities))
            return null;
        foreach (var obj in entities)
        {
            if (obj is not IEntity e || e.EntityId != entityId) continue;
            var keyProp = entityType.GetProperty("Id",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase)
                ?? entityType.GetProperty("nID",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
            if (keyProp is null) return null;
            return keyProp.GetValue(e)?.ToString();
        }
        return null;
    }
}
