using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;

namespace NeoEditor.Plugins.Cli;

[PluginKind(PluginKind.Service)]
public class CliPlugin : IServicePlugin
{
    public string Name => "Cli";
    public Version Version => new(1, 0, 0);

    public Task InitializeAsync(IPluginContext ctx)
    {
        // CLI operations are invoked on-demand via CliCommandHandler,
        // which is resolved from DI. No auto-start behavior needed.
        return Task.CompletedTask;
    }
}
