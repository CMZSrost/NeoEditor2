using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.DataViewer.ViewModels;

namespace NeoEditor.Plugins.DataViewer;

/// <summary>
/// Search results tool (bottom dock). Hosts the shared
/// <see cref="SearchResultViewModel"/> resolved from DI.
/// Spec: D02-dynamic-dock-layout §五.
/// </summary>
[PluginKind(PluginKind.Workbench)]
public class SearchPlugin : IToolPlugin
{
    private readonly SearchResultViewModel _viewModel;

    public SearchPlugin(SearchResultViewModel viewModel) => _viewModel = viewModel;

    public string Name => "DataViewer.Search";
    public Version Version => new(1, 0, 0);
    public string Title => "Search";
    public ToolDock DefaultDock => ToolDock.Bottom;
    public int Order => 13;

    public Task InitializeAsync(IPluginContext ctx) => Task.CompletedTask;

    public object CreateToolView() => new Views.SearchResultsView { DataContext = _viewModel };
}
