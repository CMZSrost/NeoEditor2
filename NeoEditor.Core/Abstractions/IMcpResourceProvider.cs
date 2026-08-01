using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NeoEditor.Core.Abstractions;

/// <summary>
/// Provides MCP resource URIs and content readers.
/// Resources represent entity data accessible as entity://{type}/{id} URIs.
/// </summary>
public interface IMcpResourceProvider
{
    /// <summary>Return all available resource URIs.</summary>
    IReadOnlyList<string> GetResourceUris();

    /// <summary>Read the content of a resource URI. Returns JSON text, or null if not found.</summary>
    Task<string?> ReadResourceAsync(string uri, CancellationToken ct = default);
}
