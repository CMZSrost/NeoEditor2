using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.Mcp.Resources;
using NeoEditor.Plugins.Mcp.Server;
using NeoEditor.Plugins.Mcp.Tools;

namespace NeoEditor.Plugins.Mcp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMcpPlugin(this IServiceCollection services)
    {
        // Plugin registration
        services.AddSingleton<IServicePlugin, McpPlugin>();

        // Tool implementations (shared between MCP stdio server and in-process IMcpToolProvider)
        services.AddSingleton<EditorTools>();

        // In-process tool provider — consumed by AI Chat / CLI via DI (R17 compliant)
        services.AddSingleton<IMcpToolProvider, McpToolExecutor>();

        // MCP stdio server host (only started with --mcp flag)
        services.AddSingleton<McpServerHost>();

        // Resource provider
        services.AddSingleton<IMcpResourceProvider, EntityResourceProvider>();

        return services;
    }
}
