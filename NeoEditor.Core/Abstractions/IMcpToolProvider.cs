using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Provides MCP tool definitions and execution.
/// Implemented by the MCP Plugin, consumed by CLI / AI Chat plugins
/// through DI — no direct project reference (R17 compliant).
/// </summary>
public interface IMcpToolProvider
{
    /// <summary>Return all registered tool definitions.</summary>
    IReadOnlyList<McpToolInfo> GetTools();

    /// <summary>
    /// Execute a tool by name with JSON-serialized arguments.
    /// Returns the tool result as a JSON string.
    /// </summary>
    Task<string> ExecuteToolAsync(string toolName, string argumentsJson,
        CancellationToken ct = default);
}
