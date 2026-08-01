using System;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.Services;

/// <summary>
/// Unified source for the "current focus entity" across workspace regions.
/// Per R12: current entity = last-focused Center document entity (by GotFocus timestamp).
/// Per R15: DataTable single-click highlights a row but does NOT change current entity;
/// only double-click / Ctrl+LMB opening a Center tab changes it.
/// Replaces the ad-hoc EntitySelectedMessage-as-state pattern.
/// </summary>
public interface ISelectionService
{
    /// <summary>
    /// Current focus entity. Set when a Center EntityEditorDocument gains focus.
    /// Null when no Center document has focus. Left KV and OverlayChain follow this.
    /// </summary>
    IEntity? CurrentEntity { get; }

    /// <summary>
    /// Set from Center document GotFocus / IsVisible. Drives Left KV + OverlayChain updates.
    /// </summary>
    void SetCurrentEntity(IEntity? entity);

    /// <summary>
    /// Request to open/activate an entity's Center document tab.
    /// Called by DataTable double-click, Ctrl+LMB on row, or "Open Full" from Peek.
    /// This DOES change CurrentEntity (the opened doc becomes current).
    /// </summary>
    void RequestOpenEntity(IEntity entity);

    /// <summary>
    /// Request to open/activate a Center document for a type+id pair.
    /// Called from reference Ctrl+LMB Navigate when entity is resolved.
    /// </summary>
    void RequestNavigate(Type entityType, string entityId);

    /// <summary>
    /// Fires when CurrentEntity changes. DocumentWorkspaceViewModel subscribes to
    /// update Left KV, OverlayChain, and session status text.
    /// </summary>
    event EventHandler<IEntity?>? CurrentEntityChanged;

    /// <summary>
    /// Fires when an entity should be opened/activated in Center.
    /// DocumentWorkspaceViewModel subscribes to create/activate EntityEditorDocument.
    /// </summary>
    event EventHandler<IEntity>? OpenEntityRequested;

    /// <summary>
    /// Fires when navigation to a type+id pair is requested.
    /// DocumentWorkspaceViewModel subscribes to find the entity and open it.
    /// </summary>
    event EventHandler<(Type EntityType, string EntityId)>? NavigateRequested;
}
