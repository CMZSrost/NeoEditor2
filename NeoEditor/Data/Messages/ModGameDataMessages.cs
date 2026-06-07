using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Messages;

/// <summary>Fired when the merge view dirty state changes.</summary>
public record MergeViewDirtyChangedMessage(bool IsDirty);

/// <summary>Request overlay chain display for an entity.</summary>
public record OverlayChainRequestedMessage(string EntityId, string Subject, string EntityType);

/// <summary>Request visual editor display for an entity type.</summary>
public record VisualEditorRequestedMessage(System.Type EntityType, IEntity? Entity);

/// <summary>Request validation of current edits.</summary>
public record RequestValidationMessage;

/// <summary>Fired when field conflicts change.</summary>
public record ConflictsChangedMessage;

/// <summary>Fired when validation completes.</summary>
public record ValidationCompletedMessage(int Warnings, int Errors);

/// <summary>Request navigation to a specific entity.</summary>
public record NavigateToEntityRequestedMessage(string EntityType, string EntityId);

/// <summary>Request save operation.</summary>
public record SaveRequestedMessage;
