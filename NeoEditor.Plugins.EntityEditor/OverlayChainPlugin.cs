using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.EntityEditor.ViewModels;

namespace NeoEditor.Plugins.EntityEditor;

/// <summary>
/// Overlay chain tool (left dock). Hosts the shared
/// <see cref="OverlayChainToolContent"/> (DI singleton) inside an
/// <see cref="Views.OverlayChainToolView"/>. Spec: D02-dynamic-dock-layout §五.
/// </summary>
[PluginKind(PluginKind.Workbench)]
public class OverlayChainPlugin : IToolPlugin
{
    private readonly OverlayChainToolContent _content;
    private readonly NeoEditor.Infra.Services.ILocalizationService _loc;

    public OverlayChainPlugin(OverlayChainToolContent content, NeoEditor.Infra.Services.ILocalizationService loc)
    {
        _loc = loc;
        _content = content;
    }

    public string Name => "EntityEditor.OverlayChain";
    public Version Version => new(1, 0, 0);
    public string Title => _loc["Tools.OverlayChain"];
    public ToolDock DefaultDock => ToolDock.Left;
    public int Order => 20;

    public Task InitializeAsync(IPluginContext ctx) => Task.CompletedTask;

    public object CreateToolView() => new Views.OverlayChainToolView { DataContext = _content };
}
