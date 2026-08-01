namespace NeoEditor.Core.Abstractions;

/// <summary>Type of change for an entity change event.</summary>
public enum ChangeType
{
    Modified,
    Added,
    Removed
}

/// <summary>
/// Event payload emitted by IHostService.Changes when an entity is modified, added, or removed.
/// Subscribed by Feature Plugins and UI components for reactive updates.
/// </summary>
public readonly record struct EntityChangedEvent(
    string EntityId,
    string EntityType,
    ChangeType Change
);
