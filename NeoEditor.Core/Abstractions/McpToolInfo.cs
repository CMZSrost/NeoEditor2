namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Describes a single MCP tool: its name, human-readable description,
/// and JSON Schema for its input parameters.
/// Consumed by AI Chat / CLI plugins via <see cref="IMcpToolProvider"/>.
/// </summary>
public sealed record McpToolInfo(
    string Name,
    string Description,
    string InputSchemaJson
);
