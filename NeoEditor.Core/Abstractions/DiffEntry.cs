namespace NeoEditor.Core.Abstractions;

/// <summary>Kind of change for a single field diff.</summary>
public enum DiffKind
{
    Modified,
    Added,
    Removed
}

/// <summary>
/// A single field-level diff entry between two versions of an entity.
/// Produced by RepositoryBase.GetDiffAsync and consumed by save-preview UIs.
/// </summary>
public readonly record struct DiffEntry(
    string PropertyName,
    string? OldValue,
    string? NewValue,
    DiffKind Kind
);
