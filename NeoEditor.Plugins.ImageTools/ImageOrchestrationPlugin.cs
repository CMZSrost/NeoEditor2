using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.ImageTools.ViewModels;

namespace NeoEditor.Plugins.ImageTools;

/// <summary>
/// Image Orchestration tool (right dock, R27). Hosts the shared
/// <see cref="ImageOrchestrationViewModel"/> (DI singleton) inside an
/// <see cref="Views.ImageOrchestrationView"/>. Spec: D02-dynamic-dock-layout §五.
/// </summary>
[PluginKind(PluginKind.Workbench)]
public class ImageOrchestrationPlugin : IToolPlugin
{
    private readonly ImageOrchestrationViewModel _viewModel;

    public ImageOrchestrationPlugin(ImageOrchestrationViewModel viewModel) => _viewModel = viewModel;

    public string Name => "ImageTools.ImageOrchestration";
    public Version Version => new(1, 0, 0);
    public string Title => "Image Orchestration";
    public ToolDock DefaultDock => ToolDock.Right;
    public int Order => 35;

    public Task InitializeAsync(IPluginContext ctx) => Task.CompletedTask;

    public object CreateToolView() => new Views.ImageOrchestrationView { DataContext = _viewModel };
}
