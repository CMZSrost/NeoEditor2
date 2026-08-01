using System;
using System.Collections.Generic;
using System.Linq;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using CommunityToolkit.Mvvm.Messaging;
using ILogger = Serilog.ILogger;

namespace NeoEditor.Plugins.DataViewer.Services;

/// <summary>
/// DI singleton navigation router. Uses a responsibility chain pattern:
/// registered INavigationTarget instances are tried in priority order (highest first),
/// and the first that CanNavigate=true handles the navigation.
///
/// R05 compliant: cross-region UI linkage (open tabs, show Peek panel) goes through IMessenger.
/// The Router is the single sender; DocumentWorkspaceViewModel is the single receiver.
/// </summary>
public class NavigationRouter : INavigationRouter
{
    private List<INavigationTarget> _targets = new();
    private readonly object _lock = new();
    private readonly ILogger _logger;
    private readonly IMessenger _messenger;

    public NavigationRouter(IMessenger messenger)
    {
        _messenger = messenger;
        _logger = Serilog.Log.Logger.ForContext<NavigationRouter>();
    }

    public void RegisterTarget(INavigationTarget target)
    {
        lock (_lock)
        {
            // Remove previous registration of the same instance, then insert at front.
            // After stable sort (OrderByDescending), the most recently registered target
            // (at index 0) is tried first among same-priority peers.
            _targets.RemoveAll(t => ReferenceEquals(t, target));
            _targets.Insert(0, target);
            _targets = _targets.OrderByDescending(t => t.Priority).ToList();
        }
        _logger.Debug("[NavRouter] Registered {Target} Priority={Priority} Total={Count}",
            target.GetType().Name, target.Priority, _targets.Count);
    }

    public void UnregisterTarget(INavigationTarget target)
    {
        lock (_lock)
        {
            _targets.RemoveAll(t => ReferenceEquals(t, target));
        }
        _logger.Debug("[NavRouter] Unregistered {Target} Total={Count}",
            target.GetType().Name, _targets.Count);
    }

    public bool Navigate(Type entityType, string entityId)
    {
        return NavigateDataTable(entityType, entityId);
    }

    public bool NavigateDataTable(Type entityType, string entityId)
    {
        if (string.IsNullOrEmpty(entityId))
        {
            _logger.Warning("[NavRouter:NavigateDataTable] Empty entityId for {EntityType}", entityType.Name);
            return false;
        }

        List<INavigationTarget> snapshot;
        lock (_lock) { snapshot = _targets.ToList(); }

        foreach (var target in snapshot)
        {
            try
            {
                if (target.CanNavigate(entityType, entityId))
                {
                    _logger.Information("[NavRouter:NavigateDataTable] {EntityType} eid={EntityId} → {Target} (P={Priority})",
                        entityType.Name, entityId, target.GetType().Name, target.Priority);
                    target.NavigateTo(entityType, entityId);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "[NavRouter:NavigateDataTable] Target {Target} threw for {EntityType} eid={EntityId}",
                    target.GetType().Name, entityType.Name, entityId);
            }
        }

        _logger.Debug("[NavRouter:NavigateDataTable] No target handled {EntityType} eid={EntityId} (targets={Count})",
            entityType.Name, entityId, snapshot.Count);
        return false;
    }

    public void NavigateToEntity(Type entityType, string entityId, IEntity? resolvedEntity = null)
    {
        if (string.IsNullOrEmpty(entityId))
        {
            _logger.Warning("[NavRouter:NavigateToEntity] Empty entityId for {EntityType}", entityType.Name);
            return;
        }

        _logger.Information("[NavRouter:NavigateToEntity] {EntityType} eid={EntityId}", entityType.Name, entityId);

        // First, try to jump the DataTable via responsibility chain
        NavigateDataTable(entityType, entityId);

        // Then, fire cross-region message for Center tab opening (R05)
        _messenger.Send(new NavigateToEntityRequestedMessage(entityType.Name, entityId));
    }

    public void RequestPeek(Type entityType, string rawId, IEntity? entity)
    {
        _logger.Information("[NavRouter:RequestPeek] {EntityType} rawId={RawId}", entityType.Name, rawId);

        // Fire cross-region message for Peek panel display (R05)
        _messenger.Send(new PeekEntityMessage(entityType, rawId, entity));
    }

    public void Peek(Type entityType, string rawId, IEntity? entity)
    {
        // Delegate to RequestPeek — unified message path
        RequestPeek(entityType, rawId, entity);
    }
}
