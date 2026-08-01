using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;

namespace NeoEditor.Plugins.DataViewer;

/// <summary>
/// Reverse reference index tool (bottom dock). Shares the singleton Reverse
/// <see cref="ViewModels.IndexTableViewModel"/> with the App shell via
/// <see cref="IIndexTableFactory"/>. Spec: D02-dynamic-dock-layout §五.
/// </summary>
[PluginKind(PluginKind.Workbench)]
public class ReverseIndexPlugin : IToolPlugin
{
    private readonly IIndexTableFactory _indexFactory;

    public ReverseIndexPlugin(IIndexTableFactory indexFactory) => _indexFactory = indexFactory;

    public string Name => "DataViewer.ReverseIndex";
    public Version Version => new(1, 0, 0);
    public string Title => "Reverse Index";
    public ToolDock DefaultDock => ToolDock.Bottom;
    public int Order => 12;

    public Task InitializeAsync(IPluginContext ctx) => Task.CompletedTask;

    public object CreateToolView() => new Views.IndexTableView { DataContext = _indexFactory.Reverse };
}
