using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.Paratranz.ViewModels;
using NeoEditor.Plugins.Paratranz.Views;

namespace NeoEditor.Plugins.Paratranz;

/// <summary>
/// ParaTranz (https://paratranz.cn) translation-platform integration.
/// M1 API helper · M2 data conversion · M3 settings · M4 dock panel
/// （双 Tab：同步 + WebView 网页工作台；D03）。
/// </summary>
[PluginKind(PluginKind.Workbench)]
public class ParatranzPlugin : IToolPlugin
{
    private readonly ParatranzPaneViewModel _viewModel;
    private readonly NeoEditor.Infra.Services.ILocalizationService _loc;

    public ParatranzPlugin(ParatranzPaneViewModel viewModel, NeoEditor.Infra.Services.ILocalizationService loc)
    {
        _loc = loc;
        _viewModel = viewModel;
    }

    public string Name => "Paratranz";
    public Version Version => new(1, 0, 0);
    public string Title => _loc["Tools.Paratranz"];
    public ToolDock DefaultDock => ToolDock.Right;
    public int Order => 60;

    public object CreateToolView() => new ParatranzPaneView { DataContext = _viewModel };

    public Task InitializeAsync(IPluginContext ctx) => Task.CompletedTask;
}
