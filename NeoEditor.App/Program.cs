using Avalonia;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Plugins.Mcp.Server;
using Serilog;

namespace NeoEditor;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File("logs/modeditor-.log",
                rollingInterval: RollingInterval.Hour,
                retainedFileCountLimit: 72,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            if (TryParseMcpMode(args, out var port))
            {
                // Headless MCP mode (R28): stdout is the JSON-RPC protocol channel,
                // so all logging is routed to the file sink only (see CreateHost(mcpMode)).
                RunMcpServer(port).GetAwaiter().GetResult();
                return;
            }

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Headless MCP server startup: initializes the composition root, ensures the databases
    /// exist, and runs the MCP server over stdio (default) or TCP (--mcp-port). No GUI is shown.
    /// </summary>
    private static async Task RunMcpServer(int? port)
    {
        // Before the host replaces Log.Logger, make sure the top-level logger never
        // writes to stdout (the MCP protocol channel).
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File("logs/modeditor-.log",
                rollingInterval: RollingInterval.Hour,
                retainedFileCountLimit: 72,
                outputTemplate: "{Timestamp:HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var host = App.CreateHost(mcpMode: true);

        // Wire the ReferenceList value converter into EF Core model building BEFORE creating the
        // databases — same order as App.OnFrameworkInitializationCompleted does for the GUI.
        Data.Context.GameDbContext.ReferenceSerializer =
            host.Services.GetRequiredService<NeoEditor.Core.Abstractions.IReferenceListSerializer>();

        App.EnsureDatabases(host.Services);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            var server = host.Services.GetRequiredService<McpServerHost>();
            Log.Information(port is > 0
                ? "NeoEditor-MCP starting on tcp://127.0.0.1:{Port}"
                : "NeoEditor-MCP starting (stdio) — waiting for a client…", port);
            await server.RunAsync(port, cts.Token);
            Log.Information("NeoEditor-MCP stopped");
        }
        finally
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    /// <summary>
    /// Parse the headless-MCP flags: <c>--mcp</c> enables MCP mode,
    /// <c>--mcp-port &lt;port&gt;</c> switches the transport to a single TCP client.
    /// </summary>
    private static bool TryParseMcpMode(string[] args, out int? port)
    {
        port = null;
        var isMcp = false;
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--mcp", StringComparison.OrdinalIgnoreCase))
                isMcp = true;
            else if (string.Equals(args[i], "--mcp-port", StringComparison.OrdinalIgnoreCase)
                     && i + 1 < args.Length
                     && int.TryParse(args[i + 1], out var p))
                port = p;
        }

        return isMcp;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
