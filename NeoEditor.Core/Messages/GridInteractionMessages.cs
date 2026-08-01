using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Messages;

/// <summary>Fired when a cell edit is committed.</summary>
public record CellEditCommittedMessage(IEntity Entity, string PropertyName, object? OldValue, object? NewValue);

/// <summary>Request cloning a row.</summary>
public record CloneRowRequestedMessage(IEntity Entity);

/// <summary>Request finding references to an entity.</summary>
public record FindReferencesRequestedMessage(IEntity Entity);

/// <summary>Fired when a cell is edited (for dirty tracking).</summary>
public record CellEditedMessage(System.Type EntityType);

// Q10=A: ShowAllRequestedMessage deleted (dead message).
