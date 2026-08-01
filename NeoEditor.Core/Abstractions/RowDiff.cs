namespace NeoEditor.Core.Abstractions;

/// <summary>
/// A row-level (DB) or file-level (XML) diff entry produced by
/// <see cref="IEntityRepository{T}.GetDiffAsync"/>.
/// DB: <see cref="TargetId"/> = entity id, <see cref="OldContent"/>/<see cref="NewContent"/> = null.
/// XML: <see cref="TargetId"/> = resolved file path, <see cref="OldContent"/>/<see cref="NewContent"/>
/// = disk-old vs generated-new snapshot (diff preview 原料).
/// </summary>
public record RowDiff(
    string TargetId,
    DiffKind Kind,
    string? OldContent = null,
    string? NewContent = null);