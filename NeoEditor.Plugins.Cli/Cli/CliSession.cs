using System;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Command;

namespace NeoEditor.Plugins.Cli.Cli;

/// <summary>
/// Manages a CLI session scope — creates an isolated CommandHistory registered
/// with HostService under the "cli" scope, so CLI undo/redo is separate from UI.
/// </summary>
public class CliSession : IDisposable
{
    private readonly IHostService _hostService;
    private readonly ICommandHistory _commandHistory;

    public CliSession(IHostService hostService)
    {
        _hostService = hostService;
        _commandHistory = new CommandHistory();
        _hostService.RegisterCommandScope("cli", _commandHistory);
        _hostService.SetActiveScope("cli");
    }

    public ICommandHistory History => _commandHistory;

    public void Dispose()
    {
        _hostService.UnregisterCommandScope("cli");
    }
}
