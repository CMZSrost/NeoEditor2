using System;
using System.Collections.Generic;
using System.Linq;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Services;

namespace NeoEditor.Plugins.DataViewer.Services;

/// <summary>
/// Extracted navigation logic from GenericDataGridHelper.
/// Handles reference resolution and entity navigation for DataGrid cell interactions.
///
/// R03: Uses injected IReferenceResolver for all reference lookups.
/// R05: Routes navigation through INavigationRouter (messenger-based).
/// R07: Receives all dependencies via constructor injection — no App.* reverse dependency.
/// </summary>
public interface IDataGridNavigationService
{
    /// <summary>Find the best entity match via SQLite IndexService (all reference forms).</summary>
    IEntity? FindBestMatch(Type entityType, string rawId, string? targetKey,
        string sourceEntityId = "", string propertyName = "");

    /// <summary>Resolve entity's EntityId using TargetKey decomposition.</summary>
    string? ResolveEntityIdByTargetKey(Type entityType, string rawId, string? targetKey,
        string sourceEntityId = "", string propertyName = "");

    /// <summary>Navigate DataTable to entity by business key id.</summary>
    void NavigateTo(Type entityType, int id);

    /// <summary>Navigate DataTable to entity by EntityId string.</summary>
    void NavigateToByEntityId(Type entityType, string entityId);

    /// <summary>Open Peek panel for entity reference.</summary>
    void PeekEntity(Type entityType, string entityId);

    /// <summary>Full navigation chain for reference fields (Ctrl+LeftClick).</summary>
    void NavigateToReference(Type targetType, string rawId, string? targetKey,
        Type? secondaryTargetType = null, string? secondaryTargetKey = null,
        string sourceEntityId = "", string propertyName = "");
}

public class DataGridNavigationService : IDataGridNavigationService
{
    private readonly IWorkspaceSession _session;
    private readonly IReferenceResolver _resolver;
    private readonly INavigationRouter _router;

    public DataGridNavigationService(
        IWorkspaceSession session,
        IReferenceResolver resolver,
        INavigationRouter router)
    {
        _session = session;
        _resolver = resolver;
        _router = router;
    }

    private EntityMergeStore? ActiveMergeStore => _session.ActiveMergeStore;
    private EntityMergeStore? BrowserStore => _session.BrowserStore;

    private Dictionary<Type, List<object>> ReferenceLookups =>
        _session.ActiveMergeStore?.ReferenceLookups
        ?? _session.BrowserStore?.ReferenceLookups
        ?? new Dictionary<Type, List<object>>();

    public IEntity? FindBestMatch(Type entityType, string rawId, string? targetKey,
        string sourceEntityId = "", string propertyName = "")
    {
        var indexService = ActiveMergeStore?.IndexService
            ?? BrowserStore?.IndexService;
        if (indexService is null) return null;

        var entityNamespaces = _session.ActiveMergeStore?.EntityNamespaces
            ?? _session.BrowserStore?.EntityNamespaces
            ?? new Dictionary<string, string>();
        var sourceNs = !string.IsNullOrWhiteSpace(sourceEntityId)
            && entityNamespaces.TryGetValue(sourceEntityId, out var sn)
            ? sn : null;

        var entityId = _resolver.LookupEntityId(indexService, entityType.Name, rawId, sourceNs);
        if (entityId is not null && ReferenceLookups.TryGetValue(entityType, out var list))
        {
            foreach (var obj in list)
                if (obj is IEntity e && e.EntityId == entityId)
                    return e;
        }

        return null;
    }

    public string? ResolveEntityIdByTargetKey(Type entityType, string rawId, string? targetKey,
        string sourceEntityId = "", string propertyName = "")
    {
        var best = FindBestMatch(entityType, rawId, targetKey, sourceEntityId, propertyName);
        return best?.EntityId;
    }

    public void NavigateTo(Type entityType, int id)
    {
        var indexService = ActiveMergeStore?.IndexService
            ?? BrowserStore?.IndexService;
        if (indexService is not null)
        {
            var entityId = indexService.LookupByNs(entityType.Name, "", id.ToString());
            if (entityId is not null)
            {
                _router.Navigate(entityType, entityId);
                return;
            }
        }
        Serilog.Log.Logger.Warning("[NavSvc] Could not resolve {EntityType} id={Id} to EntityId",
            entityType.Name, id);
    }

    public void NavigateToByEntityId(Type entityType, string entityId)
    {
        if (string.IsNullOrEmpty(entityId)) return;
        _router.Navigate(entityType, entityId);
    }

    public void PeekEntity(Type entityType, string entityId)
    {
        if (string.IsNullOrEmpty(entityId)) return;
        IEntity? entity = null;
        if (ReferenceLookups.TryGetValue(entityType, out var list) && list is not null)
            entity = list.OfType<IEntity>().FirstOrDefault(e => e.EntityId == entityId);
        _router.RequestPeek(entityType, entityId, entity);
    }

    public void NavigateToReference(Type targetType, string rawId, string? targetKey,
        Type? secondaryTargetType = null, string? secondaryTargetKey = null,
        string sourceEntityId = "", string propertyName = "")
    {
        if (rawId == "0") return;

        var (resolvedType, targetEntity) = ResolveWithSecondary(
            targetType, rawId, targetKey,
            secondaryTargetType, secondaryTargetKey,
            sourceEntityId, propertyName);

        var entityId = ResolveEntityIdByTargetKey(resolvedType, rawId, targetKey,
            sourceEntityId, propertyName);
        _router.NavigateToEntity(resolvedType, targetEntity?.EntityId ?? entityId ?? rawId, targetEntity);

        if (entityId is null)
        {
            var colonIdx = rawId.IndexOf(':');
            var numericPart = colonIdx > 0 ? rawId[(colonIdx + 1)..] : rawId;
            if (int.TryParse(numericPart, out var intId) && intId >= 0)
                NavigateTo(resolvedType, intId);
        }
    }

    private (Type resolvedType, IEntity? entity) ResolveWithSecondary(
        Type targetType, string rawId, string? targetKey,
        Type? secondaryTargetType, string? secondaryTargetKey,
        string sourceEntityId = "", string propertyName = "")
    {
        var entity = FindBestMatch(targetType, rawId, targetKey, sourceEntityId, propertyName);
        if (entity is not null) return (targetType, entity);

        if (secondaryTargetType is not null)
        {
            entity = FindBestMatch(secondaryTargetType, rawId, secondaryTargetKey,
                sourceEntityId, propertyName);
            if (entity is not null) return (secondaryTargetType, entity);
        }

        var colonIdx = rawId.IndexOf(':');
        var numericPart = colonIdx > 0 ? rawId[(colonIdx + 1)..] : rawId;
        if (int.TryParse(numericPart, out var intId) && intId >= 0)
            entity = FindBestMatch(targetType, intId.ToString(), null,
                sourceEntityId, propertyName);

        return (targetType, entity);
    }
}
