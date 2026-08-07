using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.WebView.ViewModels;
using NeoEditor.Plugins.WebView.Views;

namespace NeoEditor.Plugins.WebView;

/// <summary>
/// WebView tool — a generic embedded-browser panel (Docs/42 P1). DefaultDock=Right so the
/// panel sits beside the workspace; Order 20 keeps it near the top of the right dock.
/// The first built-in page is the Ruffle Web SWF preview (Docs/42 P2, ProxyHttpModule).
/// </summary>
[PluginKind(PluginKind.Workbench)]
public class WebViewPlugin : IToolPlugin
{
    private readonly WebViewToolViewModel _viewModel;
    private readonly NeoEditor.Infra.Services.ILocalizationService _loc;

    public WebViewPlugin(WebViewToolViewModel viewModel, NeoEditor.Infra.Services.ILocalizationService loc)
    {
        _loc = loc;
        _viewModel = viewModel;
    }

    public string Name => "WebView.Panel";
    public Version Version => new(1, 0, 0);
    public string Title => _loc["Tools.WebView"];
    public ToolDock DefaultDock => ToolDock.Right;
    public int Order => 20;

    public Task InitializeAsync(IPluginContext ctx) => Task.CompletedTask;

    public object CreateToolView() => new WebViewToolView { DataContext = _viewModel };
}
