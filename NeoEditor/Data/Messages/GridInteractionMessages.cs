using NeoEditor.Data.Model.Game;

namespace NeoEditor.Data.Messages;

/// <summary>Fired when a cell edit is committed.</summary>
public record CellEditCommittedMessage(IEntity Entity, string PropertyName, object? OldValue, object? NewValue);

/// <summary>Request cloning a row.</summary>
public record CloneRowRequestedMessage(IEntity Entity);

/// <summary>Request finding references to an entity.</summary>
public record FindReferencesRequestedMessage(IEntity Entity);

/// <summary>Request showing all entities (overridden included).</summary>
public record ShowAllRequestedMessage;

/// <summary>Fired when a cell is edited (for dirty tracking).</summary>
public record CellEditedMessage(System.Type EntityType);

/// <summary>Request a peek preview of a referenced entity.</summary>
public record PeekRequestedMessage(System.Type EntityType, string RawId, IEntity? Entity);
