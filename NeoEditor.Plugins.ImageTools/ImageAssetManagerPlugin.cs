using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.ImageTools.ViewModels;

namespace NeoEditor.Plugins.ImageTools;

/// <summary>
/// Image Browser tool (left dock, R27). Hosts the shared
/// <see cref="ImageAssetManagerViewModel"/> (DI singleton) inside an
/// <see cref="Views.ImageAssetManagerView"/>. Spec: D02-dynamic-dock-layout §五.
/// </summary>
[PluginKind(PluginKind.Workbench)]
public class ImageAssetManagerPlugin : IToolPlugin
{
    private readonly ImageAssetManagerViewModel _viewModel;
    private readonly NeoEditor.Infra.Services.ILocalizationService _loc;

    public ImageAssetManagerPlugin(ImageAssetManagerViewModel viewModel, NeoEditor.Infra.Services.ILocalizationService loc)
    {
        _loc = loc;
        _viewModel = viewModel;
    }

    public string Name => "ImageTools.ImageAssetManager";
    public Version Version => new(1, 0, 0);
    public string Title => _loc["Tools.ImageBrowser"];
    public ToolDock DefaultDock => ToolDock.Left;
    public int Order => 30;

    public Task InitializeAsync(IPluginContext ctx) => Task.CompletedTask;

    public object CreateToolView() => new Views.ImageAssetManagerView { DataContext = _viewModel };
}
