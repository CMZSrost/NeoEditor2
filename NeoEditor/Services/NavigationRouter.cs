using System;
using System.Collections.Generic;
using System.Linq;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using ILogger = Serilog.ILogger;

namespace NeoEditor.Services;

/// <summary>
/// DI singleton navigation router. Uses a responsibility chain pattern:
/// registered INavigationTarget instances are tried in priority order (highest first),
/// and the first that CanNavigate=true handles the navigation.
/// </summary>
public class NavigationRouter : INavigationRouter
{
    private List<INavigationTarget> _targets = new();
    private readonly object _lock = new();
    private readonly ILogger _logger;

    public Func<Type, string, IEntity?, bool>? PeekHandler { get; set; }

    public NavigationRouter()
    {
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
        if (string.IsNullOrEmpty(entityId))
        {
            _logger.Warning("[NavRouter:Navigate] Empty entityId for {EntityType}", entityType.Name);
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
                    _logger.Information("[NavRouter:Navigate] {EntityType} eid={EntityId} → {Target} (P={Priority})",
                        entityType.Name, entityId, target.GetType().Name, target.Priority);
                    target.NavigateTo(entityType, entityId);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "[NavRouter:Navigate] Target {Target} threw for {EntityType} eid={EntityId}",
                    target.GetType().Name, entityType.Name, entityId);
            }
        }

        _logger.Debug("[NavRouter:Navigate] No target handled {EntityType} eid={EntityId} (targets={Count})",
            entityType.Name, entityId, snapshot.Count);
        return false;
    }

    public void Peek(Type entityType, string rawId, IEntity? entity)
    {
        if (PeekHandler is null) return;
        try
        {
            PeekHandler(entityType, rawId, entity);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "[NavRouter:Peek] PeekHandler threw for {EntityType} rawId={RawId}",
                entityType.Name, rawId);
        }
    }
}
