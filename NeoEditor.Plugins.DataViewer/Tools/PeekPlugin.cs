using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.DataViewer.ViewModels;

namespace NeoEditor.Plugins.DataViewer;

/// <summary>
/// Peek tool (right dock) — quick reference inspector for the current entity.
/// Hosts the shared <see cref="PeekPanelViewModel"/> resolved from DI.
/// Spec: D02-dynamic-dock-layout §五.
/// </summary>
[PluginKind(PluginKind.Workbench)]
public class PeekPlugin : IToolPlugin
{
    private readonly PeekPanelViewModel _viewModel;

    public PeekPlugin(PeekPanelViewModel viewModel) => _viewModel = viewModel;

    public string Name => "DataViewer.Peek";
    public Version Version => new(1, 0, 0);
    public string Title => "Peek";
    public ToolDock DefaultDock => ToolDock.Right;
    public int Order => 10;

    public Task InitializeAsync(IPluginContext ctx) => Task.CompletedTask;

    public object CreateToolView() => new Views.PeekPanelView { DataContext = _viewModel };
}
