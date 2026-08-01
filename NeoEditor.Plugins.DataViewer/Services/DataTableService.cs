using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Services;

namespace NeoEditor.Plugins.DataViewer.Services;

/// <summary>
/// Injectable service that provides data access for DataGrid tables.
/// Replaces GenericDataGridHelper's static store properties and entity lookup methods.
///
/// R07: Receives all dependencies via constructor injection.
/// Lifetime: scoped to the DataViewer plugin (or singleton if shared across tabs).
/// </summary>
public class DataTableService : IEntityLookupService
{
    private readonly IWorkspaceSession _session;
    private readonly IReferenceResolver _resolver;
    private readonly IDataGridNavigationService? _navigation;

    public DataTableService(IWorkspaceSession session, IReferenceResolver resolver,
        IDataGridNavigationService? navigation = null)
    {
        _session = session;
        _resolver = resolver;
        _navigation = navigation;
    }

    // ── Store access ─────────────────────────────────────────────────────

    public EntityMergeStore? ActiveMergeStore => _session.ActiveMergeStore;
    public EntityMergeStore? BrowserStore => _session.BrowserStore;
    public EditTrackingStore? ActiveEditStore => _session.ActiveEditStore;

    public Dictionary<Type, List<object>> ReferenceLookups =>
        _session.ActiveMergeStore?.ReferenceLookups
        ?? _session.BrowserStore?.ReferenceLookups
        ?? [];

    public HashSet<(string EntityId, string ColumnName)> EditedCells =>
        _session.ActiveEditStore?.EditedCells ?? [];

    public HashSet<string> NewEntityIds =>
        _session.ActiveEditStore?.NewEntityIds ?? [];

    public HashSet<string> OverriddenEntityIds =>
        _session.ActiveMergeStore?.OverriddenEntityIds ?? [];

    public Dictionary<string, string> EntityModNames =>
        _session.ActiveMergeStore?.EntityModNames
        ?? _session.BrowserStore?.EntityModNames
        ?? [];

    public Dictionary<string, string> EntityNamespaces =>
        _session.ActiveMergeStore?.EntityNamespaces
        ?? _session.BrowserStore?.EntityNamespaces
        ?? [];

    public Dictionary<string, string> NamespaceToModName =>
        _session.ActiveMergeStore?.NamespaceToModName ?? [];

    public Dictionary<string, int> EntityMergedIds =>
        _session.ActiveMergeStore?.EntityMergedIds
        ?? _session.BrowserStore?.EntityMergedIds
        ?? [];

    public Dictionary<(string, string), string> FieldSources =>
        _session.ActiveMergeStore?.FieldSources ?? [];

    public HashSet<(string, string)> FieldConflicts =>
        _session.ActiveMergeStore?.FieldConflicts ?? [];

    public Dictionary<string, List<OverlayChainEntry>> OverlayChainDisplay =>
        _session.ActiveMergeStore?.OverlayChainDisplay ?? [];

    // ── Store management ─────────────────────────────────────────────────

    public void SetActiveStores(EntityMergeStore? mergeStore, EditTrackingStore? editStore)
        => _session.SetActiveStores(mergeStore, editStore);

    public object TakeSnapshot()
    {
        var mergeStore = _session.ActiveMergeStore;
        var editStore = _session.ActiveEditStore;
        return (mergeStore, editStore);
    }

    public void RestoreSnapshot(object snapshot)
    {
        if (snapshot is not (EntityMergeStore mergeStore, EditTrackingStore editStore)) return;
        SetActiveStores(mergeStore, editStore);
    }

    // ── Entity lookup helpers ────────────────────────────────────────────

    public string GetEntityModName(IEntity entity) =>
        EntityModNames.TryGetValue(entity.EntityId, out var name) ? name : "";

    public int GetEntityMergedId(IEntity entity) =>
        EntityMergedIds.TryGetValue(entity.EntityId, out var id) ? id : 0;

    public string? GetFieldSource(string entityId, string colName) =>
        FieldSources.TryGetValue((entityId, colName), out var name) ? name : null;

    public List<OverlayChainEntry> GetOverlayChain(IEntity entity) =>
        OverlayChainDisplay.TryGetValue(entity.EntityId, out var chain) ? chain : [];

    // ── Deduped entity queries ───────────────────────────────────────────

    public Dictionary<int, T> GetEntities<T>() where T : IEntity
    {
        if (!ReferenceLookups.TryGetValue(typeof(T), out var list) || list is null) return [];
        var keyProp = EntityHelper.ResolveKeyProperty(typeof(T));
        if (keyProp is null) return [];
        return list.OfType<T>()
            .GroupBy(e => keyProp.GetValue(e) is int id ? id : 0)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.ModId).First());
    }

    public Dictionary<string, T> GetCompositeEntities<T>(Func<T, string> keySelector,
        int sourceModId = int.MaxValue) where T : IEntity
    {
        if (!ReferenceLookups.TryGetValue(typeof(T), out var list) || list is null) return [];
        return list.OfType<T>()
            .GroupBy(keySelector)
            .ToDictionary(g => g.Key, g =>
            {
                var ordered = g.OrderByDescending(e => e.ModId).ToList();
                if (sourceModId < int.MaxValue)
                {
                    var sameMod = ordered.FirstOrDefault(e => e.ModId == sourceModId);
                    if (sameMod is not null) return sameMod;
                }
                return ordered[0];
            });
    }

    public List<T> GetDedupedEntities<T>() where T : IEntity
    {
        if (!ReferenceLookups.TryGetValue(typeof(T), out var list) || list is null) return [];
        var keyProp = EntityHelper.ResolveKeyProperty(typeof(T));
        return list.OfType<T>()
            .GroupBy(e => keyProp?.GetValue(e)?.ToString() ?? e.EntityId)
            .Select(g => g.OrderByDescending(e => e.ModId).First())
            .ToList();
    }

    // ── Subject / reference resolution ───────────────────────────────────

    public string? LookupSubject(Type entityType, int id)
        => _resolver.LookupSubject("", "", entityType, id.ToString());

    public string? LookupSubjectByRawId(Type entityType, string rawId,
        string sourceEntityId, string propertyName,
        Type? secondaryEntityType = null)
        => _resolver.LookupSubject(sourceEntityId, propertyName, entityType, rawId, secondaryEntityType);

    public void ClearSubjectCache()
        => _session.ActiveMergeStore?.SubjectCache.Clear();

    // ── Navigation passthrough (for converters / legacy code) ───────────

    public void NavigateTo(Type entityType, int id)
        => _navigation?.NavigateTo(entityType, id);

    public void NavigateToByEntityId(Type entityType, string entityId)
        => _navigation?.NavigateToByEntityId(entityType, entityId);

    public void PeekEntity(Type entityType, string entityId)
        => _navigation?.PeekEntity(entityType, entityId);

    public string? ResolveEntityIdByTargetKey(Type entityType, string rawId, string? targetKey,
        string sourceEntityId = "", string propertyName = "")
        => _navigation?.ResolveEntityIdByTargetKey(entityType, rawId, targetKey, sourceEntityId, propertyName);

    public IEntity? FindBestMatch(Type entityType, string rawId, string? targetKey,
        string sourceEntityId = "", string propertyName = "")
        => _navigation?.FindBestMatch(entityType, rawId, targetKey, sourceEntityId, propertyName);

    /// <summary>
    /// Format a single segment of a multi-value reference field with Subject name.
    /// </summary>
    public string FormatSegmentDisplay(string segment, Type targetType, string? pattern,
        string sourceEntityId, string propertyName, string? targetKey = null)
    {
        if (string.IsNullOrWhiteSpace(segment)) return segment;
        var pat = ReferencePattern.FromName(pattern);
        var rawId = pat.ExtractRawId(segment);
        var parsed = ReferenceParser.ParseWithPattern(segment, pattern);
        var subject = LookupSubjectByRawId(targetType, rawId, sourceEntityId, propertyName);
        if (string.IsNullOrEmpty(subject)) return segment;
        return pat.FormatDisplay(segment, subject, parsed.ModName);
    }
}
