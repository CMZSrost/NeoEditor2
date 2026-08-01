namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Result of executing one or more IEditorCommand through IHostService.
/// </summary>
public readonly record struct CommandResult(
    bool Success,
    string? Error,
    string[] AffectedEntityIds
);
