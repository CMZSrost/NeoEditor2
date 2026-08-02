using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
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

    /// <summary>(EntityType, Namespace, GroupId, SubgroupId) → EntityId — composite-key (ItemType 86.6) ns-prefixed lookups</summary>
    private readonly Dictionary<(Type EntityType, string Ns, int GroupId, int SubgroupId), string> _nsCompositeIndex = new();

    /// <summary>(EntityType, GroupId, SubgroupId) → EntityId — composite-key (ItemType 86.6) merged lookups</summary>
    private readonly Dictionary<(Type EntityType, int GroupId, int SubgroupId), string> _mergedCompositeIndex = new();

    // ── Reverse: targetEntityId → list of (sourceEntityId, propertyName, rawId) ──
    private readonly Dictionary<string, List<(string SourceEntityId, string PropertyName, string RawId)>> _reverse = new();

    // ── Display cache: (targetEntityId) → (Subject, ModName) ──
    private readonly Dictionary<string, (string? Subject, string? ModName)> _display = new();

    // ── R30: segment-normalization support ──
    //   _entityIdToType: EntityId → source entity type (built with the index)
    //   _patternCache:   (sourceEntityId, propertyName) → [ReferenceField].Pattern
    // Used by Lookup to extract the id part of a segment ("67x0.05" → "67") per the
    // source field's parse pattern, so every caller — DataGrid, Value Editor badges,
    // visualizer badges — feeds the SAME canonical backend regardless of whether it
    // passes a full segment or an already-extracted id (ExtractRawId is idempotent).
    private readonly Dictionary<string, Type> _entityIdToType = new();
    private readonly Dictionary<(string SourceEntityId, string PropertyName), string?> _patternCache = new();

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

                    // R30: EntityId → source type, so Lookup can resolve the source field's
                    // parse pattern for segment normalization ("67x0.05" → "67").
                    _entityIdToType[entity.EntityId] = entityType;

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

                    var nsKey = (entityType, ns, primaryKey);
                    if (_nsIndex.TryGetValue(nsKey, out var prevNsEid))
                    {
                        _store.EntityModNames.TryGetValue(prevNsEid, out var prevNsMod);
                        _store.EntityModNames.TryGetValue(entity.EntityId, out var newNsMod);
                        Serilog.Log.Logger.Debug(
                            "[RefIndex:Build] NsIndex OVERWRITE: {Type}:ns={Ns}/pk={Pk} old={OldEid}(mod={OldMod}) → new={NewEid}(mod={NewMod})",
                            entityType.Name, ns, primaryKey, prevNsEid, prevNsMod ?? "?", entity.EntityId, newNsMod ?? "?");
                    }
                    _nsIndex[nsKey] = entity.EntityId;
                    nsIdxCount++;

                    // Composite-key index (e.g. ItemType nGroupID.nSubgroupID) — mirrors nsIndex/mergedIdIndex semantics.
                    // R30 (M5): skip the invalid (0,0) key — every ItemType has GroupId/SubgroupId
                    // defaulting to 0, and "0.0" is not a referenceable id (mirrors the rawId=="0" skip).
                    if (TryGetCompositeKey(entity, out var gid, out var sid) && !(gid == 0 && sid == 0))
                    {
                        _mergedCompositeIndex[(entityType, gid, sid)] = entity.EntityId;
                        _nsCompositeIndex[(entityType, ns, gid, sid)] = entity.EntityId;
                    }
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

        foreach (var (sourceType, entities) in _store.ReferenceLookups)
        {
            var refProps = sourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetCustomAttribute<ReferenceFieldAttribute>() is not null)
                .ToList();

            if (refProps.Count == 0) continue;

            foreach (var obj in entities)
            {
                if (obj is not IEntity sourceEntity) continue;

                foreach (var prop in refProps)
                {
                    var refAttr = prop.GetCustomAttribute<ReferenceFieldAttribute>()!;
                    var rawValue = GetReferenceRawValue(prop.GetValue(sourceEntity), refAttr);
                    if (string.IsNullOrWhiteSpace(rawValue)) continue;

                    var ids = ReferenceParser.ExtractIds(rawValue, refAttr);
                    foreach (var (extractedId, _) in ids)
                    {
                        var targetEntityId = Lookup(sourceEntity.EntityId, prop.Name,
                            refAttr.TargetEntityType, extractedId)
                            ?? (refAttr.SecondaryTargetEntityType is not null
                                ? Lookup(sourceEntity.EntityId, prop.Name, refAttr.SecondaryTargetEntityType, extractedId)
                                : null);

                        if (targetEntityId is not null)
                        {
                            if (!_reverse.TryGetValue(targetEntityId, out var refs))
                                _reverse[targetEntityId] = refs = new();
                            refs.Add((sourceEntity.EntityId, prop.Name, extractedId));
                        }
                    }
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Lookup
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resolve a reference to an EntityId.
    ///
    /// R30: the rawId may be a FULL segment ("67x0.05", "-115x1.0", "[155,0,0]",
    /// "Hood Off=8.7") or an already-extracted id ("67") — the id part is extracted
    /// internally using the source field's [ReferenceField] parse pattern (idempotent),
    /// so every caller resolves with the same semantics.
    ///
    /// Rules:
    ///   - Has namespace prefix (e.g. "NSE:3")  → lookup by (type, namespace, primary key)
    ///   - No namespace prefix (e.g. "3")       → lookup by (type, MergedId)
    /// </summary>
    public string? Lookup(string sourceEntityId, string propertyName, Type targetType, string rawId)
    {
        if (string.IsNullOrWhiteSpace(rawId)) return null;

        // R30: normalize the segment per the source field's pattern before keying.
        var pattern = ResolvePattern(sourceEntityId, propertyName);
        var idOnly = ReferenceParser.ExtractRawId(rawId, pattern);
        if (string.IsNullOrWhiteSpace(idOnly)) return null;

        // Parse namespace prefix
        string? nsPrefix = null;
        var colonIdx = idOnly.IndexOf(':');
        if (colonIdx > 0)
        {
            nsPrefix = idOnly[..colonIdx];
            idOnly = idOnly[(colonIdx + 1)..];
        }

        var lookupKey = ReferenceParser.BuildLookupKey(idOnly);
        if (lookupKey.Length == 0) return null;

        // Composite key form "GroupId.SubgroupId" (e.g. ItemType "86.6")
        int? cGid = null;
        int? cSid = null;
        var dotIdx = lookupKey.IndexOf('.');
        if (dotIdx > 0
            && int.TryParse(lookupKey[..dotIdx], out var g)
            && int.TryParse(lookupKey[(dotIdx + 1)..], out var s))
        {
            cGid = g;
            cSid = s;
        }

        if (nsPrefix is not null)
        {
            // Namespace-prefixed: lookup by (type, namespace, primary key)
            Serilog.Log.Logger.Debug(
                "[RefIndex:Lookup] NS route: type={Type} ns={Ns} pk={Pk} rawId={RawId} src={Src}",
                targetType.Name, nsPrefix, lookupKey, rawId, sourceEntityId);

            if (_nsIndex.TryGetValue((targetType, nsPrefix, lookupKey), out var eid))
            {
                _store.EntityModNames.TryGetValue(eid, out var mod);
                var entityPk = TryGetEntityPrimaryKey(eid, targetType);
                Serilog.Log.Logger.Debug("[RefIndex:Lookup] NS hit → {Eid} mod={Mod} pk={Pk}", eid, mod ?? "?", entityPk ?? "?");
                return eid;
            }

            // Composite ns-prefixed (e.g. "0:86.6")
            if (cGid is { } cg && cSid is { } cs)
            {
                if (_nsCompositeIndex.TryGetValue((targetType, nsPrefix, cg, cs), out var ceid))
                    return ceid;
                if (_store.NamespaceToModName.TryGetValue(nsPrefix, out var cmapped)
                    && _nsCompositeIndex.TryGetValue((targetType, cmapped, cg, cs), out var cmappedEid))
                    return cmappedEid;
            }

            // Also try mapping through NamespaceToModName
            if (_store.NamespaceToModName.TryGetValue(nsPrefix, out var mapped)
                && _nsIndex.TryGetValue((targetType, mapped, lookupKey), out var mappedEid))
            {
                _store.EntityModNames.TryGetValue(mappedEid, out var mappedMod);
                Serilog.Log.Logger.Debug("[RefIndex:Lookup] NS mapped hit ns={Mapped} → {Eid} mod={Mod}", mapped, mappedEid, mappedMod ?? "?");
                return mappedEid;
            }

            Serilog.Log.Logger.Debug("[RefIndex:Lookup] NS miss — nsIdx has {Count} entries for {Type}",
                _nsIndex.Count(kv => kv.Key.EntityType == targetType), targetType.Name);
            return null;
        }
        else
        {
            // No namespace prefix: lookup by MergedId
            Serilog.Log.Logger.Debug(
                "[RefIndex:Lookup] MERGEDID route: type={Type} lookupKey={Key} rawId={RawId} src={Src}",
                targetType.Name, lookupKey, rawId, sourceEntityId);

            if (int.TryParse(lookupKey, out var mergedId))
            {
                if (_mergedIdIndex.TryGetValue((targetType, mergedId), out var eid))
                {
                    // Also log which entity we found
                    _store.EntityModNames.TryGetValue(eid, out var foundMod);
                    _store.EntityNamespaces.TryGetValue(eid, out var foundNs);
                    var entityPk = TryGetEntityPrimaryKey(eid, targetType);
                    Serilog.Log.Logger.Debug(
                        "[RefIndex:Lookup] MergedId hit mid={Mid} → {Eid} mod={Mod} ns={Ns} pk={Pk}",
                        mergedId, eid, foundMod ?? "?", foundNs ?? "?", entityPk ?? "?");
                    return eid;
                }
                Serilog.Log.Logger.Debug(
                    "[RefIndex:Lookup] MergedId miss mid={Mid} — mergedIdIdx has {Count} entries for {Type}",
                    mergedId,
                    _mergedIdIndex.Count(kv => kv.Key.EntityType == targetType),
                    targetType.Name);
            }
            else
            {
                // Composite merged (e.g. "86.6")
                if (cGid is { } cg && cSid is { } cs
                    && _mergedCompositeIndex.TryGetValue((targetType, cg, cs), out var ceid))
                {
                    return ceid;
                }
                Serilog.Log.Logger.Debug(
                    "[RefIndex:Lookup] lookupKey '{Key}' is not a valid MergedId (not an int) — returning null",
                    lookupKey);
            }

            return null;
        }
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
            var rawValue = GetReferenceRawValue(prop.GetValue(entity), refAttr);
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

            // Resolve using the standard Lookup
            var targetEid = Lookup(sourceEntity.EntityId, propName, refAttr.TargetEntityType, rawId)
                         ?? (refAttr.SecondaryTargetEntityType is not null
                             ? Lookup(sourceEntity.EntityId, propName, refAttr.SecondaryTargetEntityType, rawId)
                             : null);

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
        RemoveFromValueDict(_nsCompositeIndex, entityId);
        RemoveFromValueDict(_mergedCompositeIndex, entityId);

        // Remove display cache
        _display.Remove(entityId);
    }

    public void Clear()
    {
        _nsIndex.Clear();
        _mergedIdIndex.Clear();
        _nsCompositeIndex.Clear();
        _mergedCompositeIndex.Clear();
        _reverse.Clear();
        _display.Clear();
        _entityIdToType.Clear();
        _patternCache.Clear();
    }

    /// <summary>R30 (L3): drop only the display-name cache (Subject/ModName) — used after
    /// editing a target entity so cells show the new name without a full rebuild.</summary>
    public void ClearDisplayCache() => _display.Clear();

    /// <summary>
    /// R30: resolve the [ReferenceField].Pattern of the SOURCE field for segment
    /// normalization. Pattern depends on the source field, not the target type —
    /// resolved via EntityId → source type (built at index time) + reflection,
    /// cached per (sourceEntityId, propertyName).
    /// </summary>
    private string? ResolvePattern(string sourceEntityId, string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return null;
        var key = (sourceEntityId, propertyName);
        if (_patternCache.TryGetValue(key, out var cached)) return cached;

        string? pattern = null;
        if (_entityIdToType.TryGetValue(sourceEntityId, out var sourceType))
        {
            var prop = sourceType.GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            pattern = prop?.GetCustomAttribute<ReferenceFieldAttribute>()?.Pattern;
        }

        _patternCache[key] = pattern;
        return pattern;
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Disk persistence
    // ═══════════════════════════════════════════════════════════════════════

    public void SaveToDisk(string path)
    {
        var data = new IndexDiskData
        {
            Version = 8, // v8: adds composite-key (GroupId.SubgroupId) indices
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
            NsCompositeIndex = _nsCompositeIndex.Select(kv => new NsCompositeEntry
            {
                Type = kv.Key.EntityType.FullName!,
                Ns = kv.Key.Ns,
                Gid = kv.Key.GroupId,
                Sid = kv.Key.SubgroupId,
                Tgt = kv.Value
            }).ToList(),
            MergedCompositeIndex = _mergedCompositeIndex.Select(kv => new MergedCompositeEntry
            {
                Type = kv.Key.EntityType.FullName!,
                Gid = kv.Key.GroupId,
                Sid = kv.Key.SubgroupId,
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
            if (data.Version < 8)
            {
                Serilog.Log.Logger.Information(
                    "[RefIndex:LoadDisk] Stale cache v{OldVersion} (current v8) — rebuilding", data.Version);
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
            foreach (var e in data.NsCompositeIndex)
            {
                var t = Type.GetType(e.Type);
                if (t is not null) _nsCompositeIndex[(t, e.Ns, e.Gid, e.Sid)] = e.Tgt;
            }
            foreach (var e in data.MergedCompositeIndex)
            {
                var t = Type.GetType(e.Type);
                if (t is not null) _mergedCompositeIndex[(t, e.Gid, e.Sid)] = e.Tgt;
            }
            foreach (var (k, v) in data.Reverse)
                _reverse[k] = v.Select(r => (r.Src, r.Prop, r.Key)).ToList();
            foreach (var (k, v) in data.Display)
                _display[k] = (v.Sub, v.Mod);

            Serilog.Log.Logger.Information(
                "[RefIndex:LoadDisk] nsIdx={Ns} mergedIdIdx={Mid} nsComposite={NsComp} mergedComposite={MidComp} rev={Rev}",
                _nsIndex.Count, _mergedIdIndex.Count, _nsCompositeIndex.Count, _mergedCompositeIndex.Count, _reverse.Count);

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
        public int Version { get; set; } = 8;
        public List<NsIdxEntry> NsIndex { get; set; } = new();
        public List<MidIdxEntry> MergedIdIndex { get; set; } = new();
        public List<NsCompositeEntry> NsCompositeIndex { get; set; } = new();
        public List<MergedCompositeEntry> MergedCompositeIndex { get; set; } = new();
        public Dictionary<string, List<RevEntry>> Reverse { get; set; } = new();
        public Dictionary<string, DispEntry> Display { get; set; } = new();
    }

    private class NsIdxEntry { public string Type { get; set; } = ""; public string Ns { get; set; } = ""; public string Pk { get; set; } = ""; public string Tgt { get; set; } = ""; }
    private class MidIdxEntry { public string Type { get; set; } = ""; public int Mid { get; set; } public string Tgt { get; set; } = ""; }
    private class NsCompositeEntry { public string Type { get; set; } = ""; public string Ns { get; set; } = ""; public int Gid { get; set; } public int Sid { get; set; } public string Tgt { get; set; } = ""; }
    private class MergedCompositeEntry { public string Type { get; set; } = ""; public int Gid { get; set; } public int Sid { get; set; } public string Tgt { get; set; } = ""; }
    private class RevEntry { public string Src { get; set; } = ""; public string Prop { get; set; } = ""; public string Key { get; set; } = ""; }
    private class DispEntry { public string? Sub { get; set; } public string? Mod { get; set; } }

    // ═══════════════════════════════════════════════════════════════════════
    //  Internals
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get the raw serialized value of a reference property — ReferenceList must go through
    /// <see cref="ReferenceList{T}.ToRawString"/> (never ToString(), which emits "[a, b]").
    /// </summary>
    private static string? GetReferenceRawValue(object? value, ReferenceFieldAttribute refAttr)
    {
        if (value is ReferenceList<IReferenceEntry> rl)
            return rl.ToRawString(refAttr.Separator);
        return value?.ToString();
    }

    /// <summary>Read composite key (GroupId/SubgroupId) from an entity that has them (ItemType).</summary>
    private static bool TryGetCompositeKey(IEntity entity, out int groupId, out int subgroupId)
    {
        groupId = 0;
        subgroupId = 0;
        var type = entity.GetType();
        var g = type.GetProperty("GroupId",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(entity);
        var s = type.GetProperty("SubgroupId",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)?.GetValue(entity);
        if (g is int gi && s is int si)
        {
            groupId = gi;
            subgroupId = si;
            return true;
        }
        return false;
    }

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
