using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;

namespace NeoEditor.Plugins.DataViewer;

/// <summary>
/// Forward reference index tool (bottom dock). Shares the singleton Forward
/// <see cref="ViewModels.IndexTableViewModel"/> with the App shell via
/// <see cref="IIndexTableFactory"/>. Spec: D02-dynamic-dock-layout §五.
/// </summary>
[PluginKind(PluginKind.Workbench)]
public class ForwardIndexPlugin : IToolPlugin
{
    private readonly IIndexTableFactory _indexFactory;

    public ForwardIndexPlugin(IIndexTableFactory indexFactory) => _indexFactory = indexFactory;

    public string Name => "DataViewer.ForwardIndex";
    public Version Version => new(1, 0, 0);
    public string Title => "Ref Index";
    public ToolDock DefaultDock => ToolDock.Bottom;
    public int Order => 11;

    public Task InitializeAsync(IPluginContext ctx) => Task.CompletedTask;

    public object CreateToolView() => new Views.IndexTableView { DataContext = _indexFactory.Forward };
}
