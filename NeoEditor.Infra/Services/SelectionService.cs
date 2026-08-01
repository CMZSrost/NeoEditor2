using System;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model.Game;
using ILogger = Serilog.ILogger;

namespace NeoEditor.Services;

/// <inheritdoc cref="ISelectionService"/>
public class SelectionService : ISelectionService
{
    private IEntity? _currentEntity;
    private readonly ILogger _logger;

    public IEntity? CurrentEntity => _currentEntity;

    public event EventHandler<IEntity?>? CurrentEntityChanged;
    public event EventHandler<IEntity>? OpenEntityRequested;
    public event EventHandler<(Type EntityType, string EntityId)>? NavigateRequested;

    public SelectionService()
    {
        _logger = Serilog.Log.Logger.ForContext<SelectionService>();
    }

    public void SetCurrentEntity(IEntity? entity)
    {
        if (ReferenceEquals(_currentEntity, entity)) return;

        _currentEntity = entity;
        _logger.Debug("[Selection] CurrentEntity → {EntityId}",
            entity?.EntityId ?? "<null>");
        CurrentEntityChanged?.Invoke(this, entity);
    }

    public void RequestOpenEntity(IEntity entity)
    {
        _logger.Debug("[Selection] OpenEntity requested: {EntityId}", entity.EntityId);
        SetCurrentEntity(entity);
        OpenEntityRequested?.Invoke(this, entity);
    }

    public void RequestNavigate(Type entityType, string entityId)
    {
        _logger.Debug("[Selection] Navigate requested: {Type} {Id}", entityType.Name, entityId);
        NavigateRequested?.Invoke(this, (entityType, entityId));
    }
}
