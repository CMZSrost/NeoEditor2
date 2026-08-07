using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.ViewModels.ExplorerPane;
using NeoEditor.Views.UserControls;

namespace NeoEditor.Plugins;

/// <summary>
/// R42: Sounds Tool (right dock) — browse extracted game audio assets
/// ({GameRootDir}/sounds from player-tools/extract-sounds.js) and play them,
/// so modders can hear what a cue (aSounds / strSnd) actually sounds like.
/// </summary>
[PluginKind(PluginKind.Workbench)]
public class SoundsToolPlugin : IToolPlugin
{
    private readonly SoundsToolViewModel _viewModel;
    private readonly NeoEditor.Infra.Services.ILocalizationService _loc;

    public SoundsToolPlugin(SoundsToolViewModel viewModel, NeoEditor.Infra.Services.ILocalizationService loc)
    {
        _loc = loc;
        _viewModel = viewModel;
    }

    public string Name => "SoundsTool";
    public Version Version => new(1, 0, 0);
    public string Title => _loc["Tools.SoundsTool"];
    public ToolDock DefaultDock => ToolDock.Right;
    public int Order => 40;

    public Task InitializeAsync(IPluginContext ctx) => Task.CompletedTask;

    public object CreateToolView() => new SoundsToolView { DataContext = _viewModel };
}
