using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Command;

namespace NeoEditor.Plugins.Mcp;

[PluginKind(PluginKind.Service)]
public class McpPlugin : IServicePlugin
{
    public string Name => "Mcp";
    public Version Version => new(1, 0, 0);

    private IPluginContext? _ctx;

    public Task InitializeAsync(IPluginContext ctx)
    {
        _ctx = ctx;

        // Register a dedicated undo scope for MCP operations.
        // This makes MCP edits undoable and isolates them from UI tab undo stacks.
        var hostService = ctx?.Services.GetService(typeof(IHostService)) as IHostService;
        if (hostService != null)
        {
            var mcpHistory = new CommandHistory();
            hostService.RegisterCommandScope("mcp", mcpHistory);
        }

        return Task.CompletedTask;
    }
}
