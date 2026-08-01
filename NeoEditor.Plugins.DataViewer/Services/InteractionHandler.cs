using System;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Data.Messages;

namespace NeoEditor.Plugins.DataViewer.Services;

/// <summary>
/// Coordinates high-level DataGrid interaction events (cell edit, clone, find references).
/// Extracted from GenericDataGridHelper's static raise methods.
///
/// R05: Sends messages via IMessenger for cross-region UI linkage.
/// R07: Receives dependencies via constructor injection.
/// </summary>
public class InteractionHandler
{
    private readonly IMessenger _messenger;
    private readonly DataGridInteractionState _state;

    public InteractionHandler(IMessenger messenger, DataGridInteractionState state)
    {
        _messenger = messenger;
        _state = state;
    }

    /// <summary>Set by Ctrl+PointerPressed, checked by ContextRequested to suppress right-click menu.</summary>
    public bool CtrlWasPressed
    {
        get => _state.CtrlWasPressed;
        set => _state.CtrlWasPressed = value;
    }

    /// <summary>
    /// Set by Ctrl+PointerPressed inline handlers BEFORE DataGrid selection change.
    /// Prevents OnDataGridSelectionChanged from sending EntitySelectedMessage.
    /// </summary>
    public bool SuppressNextSelectionChanged
    {
        get => _state.SuppressNextSelectionChanged;
        set => _state.SuppressNextSelectionChanged = value;
    }

    public void RaiseCellEditCommitted(Data.Model.Game.IEntity entity, string propertyName,
        object? oldValue, object? newValue)
        => _messenger.Send(new CellEditCommittedMessage(entity, propertyName, oldValue, newValue));

    public void RaiseCloneRowRequested(Data.Model.Game.IEntity entity)
        => _messenger.Send(new CloneRowRequestedMessage(entity));

    public void RaiseFindReferencesRequested(Data.Model.Game.IEntity entity)
        => _messenger.Send(new FindReferencesRequestedMessage(entity));
}
