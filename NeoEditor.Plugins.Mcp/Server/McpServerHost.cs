using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.Mcp.Tools;
using Implementation = ModelContextProtocol.Protocol.Implementation;

namespace NeoEditor.Plugins.Mcp.Server;

/// <summary>
/// Wraps an MCP SDK <see cref="McpServer"/> with a transport.
/// Only started when the app is launched with the --mcp flag (or in-GUI TCP via settings).
/// In normal GUI mode, the server is not started — tools remain available
/// in-process via <see cref="IMcpToolProvider"/>.
/// </summary>
public class McpServerHost
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<McpServerHost> _logger;
    private McpServer? _server;

    public McpServerHost(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<McpServerHost>();
    }

    /// <summary>
    /// Start the MCP server. When <paramref name="port"/> is null or &lt;= 0 the server runs on
    /// stdio (headless --mcp mode); otherwise it listens for a single TCP client on the given port
    /// (reserved for GUI in-process start). Blocks until the transport ends or cancellation is requested.
    /// </summary>
    public async Task RunAsync(int? port = null, CancellationToken ct = default)
    {
        var options = BuildOptions();
        if (port is > 0)
            await RunTcpAsync(options, port.Value, ct);
        else
            await RunStdioAsync(options, ct);
    }

    /// <summary>Build MCP server options with all EditorTools registered.</summary>
    internal McpServerOptions BuildOptions()
    {
        var options = new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = "NeoEditor-MCP",
                Version = "1.0.0"
            },
            // The SDK does NOT initialize the tool collection when McpServerOptions is built
            // directly (only the DI builder path does). Without this, ToolCollection is null and
            // the .Add(tool) below throws NullReferenceException at --mcp startup. (SDK preview.3)
            ToolCollection = new McpServerPrimitiveCollection<McpServerTool>(StringComparer.OrdinalIgnoreCase)
        };

        // Register all tools from EditorTools
        var tools = _serviceProvider.GetRequiredService<EditorTools>();
        var toolMethods = typeof(EditorTools).GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Length > 0);

        foreach (var method in toolMethods)
        {
            var descAttr = method.GetCustomAttributes(false)
                .OfType<System.ComponentModel.DescriptionAttribute>()
                .FirstOrDefault();

            var tool = McpServerTool.Create(
                method,
                tools,
                new McpServerToolCreateOptions
                {
                    Name = method.Name,
                    Description = descAttr?.Description ?? method.Name
                });

            options.ToolCollection.Add(tool);
        }

        return options;
    }

    /// <summary>Run the server over stdin/stdout (JSON-RPC, MCP standard transport).</summary>
    private async Task RunStdioAsync(McpServerOptions options, CancellationToken ct)
    {
        var transport = new StdioServerTransport("NeoEditor-MCP", _loggerFactory);
        _server = McpServer.Create(transport, options, _loggerFactory, _serviceProvider);

        try
        {
            await _server.RunAsync(ct);
        }
        catch (OperationCanceledException) { /* graceful shutdown */ }
    }

    /// <summary>
    /// Run the server over a single TCP client session. Reserved transport — the SDK preview ships
    /// stdio + stream transports; a plain TCP session is bridged via <see cref="StreamServerTransport"/>.
    /// </summary>
    private async Task RunTcpAsync(McpServerOptions options, int port, CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        _logger.LogInformation("NeoEditor-MCP listening on tcp://127.0.0.1:{Port}", port);

        try
        {
            using var client = await listener.AcceptTcpClientAsync(ct);
            _logger.LogInformation("NeoEditor-MCP client connected");
            var stream = client.GetStream();
            var sessionId = $"neoeditor-{Guid.NewGuid():N}";
            var transport = new StreamServerTransport(stream, stream, sessionId, _loggerFactory);
            _server = McpServer.Create(transport, options, _loggerFactory, _serviceProvider);

            try
            {
                await _server.RunAsync(ct);
            }
            catch (OperationCanceledException) { /* graceful shutdown */ }
        }
        finally
        {
            listener.Stop();
        }
    }
}
