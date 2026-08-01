using System.Collections.Generic;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Result of a Save (memory → DB) operation (R26). HostService uses <see cref="PartialDiff"/>
/// and <see cref="SavedEntityIds"/> to drive dirty cleanup (R01/R09); the View never clears dirty manually.
/// </summary>
public record SaveResult(
    IReadOnlyList<DiffEntry> PartialDiff,
    IReadOnlyList<string> SavedEntityIds);

/// <summary>
/// Result of an Export (DB → XML) operation for one mod (R26).
/// <see cref="Files"/> contains per-file <see cref="RowDiff"/> entries: TargetId = file path,
/// OldContent/NewContent = disk-old vs generated-new snapshot pairs (diff preview原料).
/// </summary>
public record ExportResult(
    int ModId,
    IReadOnlyList<RowDiff> Files,
    bool UserConfirmed);

/// <summary>Combined result of a Publish (Save + Export) operation (R26).</summary>
public record PublishResult(
    SaveResult Save,
    IReadOnlyList<ExportResult> Exports);