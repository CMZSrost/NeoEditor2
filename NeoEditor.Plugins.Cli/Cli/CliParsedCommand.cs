using System;

namespace NeoEditor.Plugins.Cli.Cli;

/// <summary>
/// Represents a parsed CLI command with its arguments.
/// </summary>
public class CliParsedCommand
{
    public CliCommandType Command { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? PropertyName { get; set; }
    public string? PropertyValue { get; set; }
    public string? Filter { get; set; }
    public int? Limit { get; set; }
    public int? ModId { get; set; }
    public bool Commit { get; set; }
    public string Format { get; set; } = "text";
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum CliCommandType
{
    Unknown,
    Help,
    GetEntity,
    EditEntity,
    AddEntity,
    DeleteEntity,
    ListEntities,
    Save,
    Diff,
    QueryReferences,
    Undo,
    Redo,
    Publish,
    ExportMod
}
