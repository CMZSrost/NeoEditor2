using System;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.AiChat.ViewModels;
using NeoEditor.Plugins.AiChat.Views;

namespace NeoEditor.Plugins.AiChat;

/// <summary>
/// AI Chat tool (right dock). Hosts the shared <see cref="AiChatViewModel"/>
/// (DI-registered) inside an <see cref="AiChatView"/>. Spec: D02-dynamic-dock-layout §五.
/// </summary>
[PluginKind(PluginKind.Workbench)]
public class AiChatPlugin : IToolPlugin
{
    private readonly AiChatViewModel _viewModel;

    public AiChatPlugin(AiChatViewModel viewModel) => _viewModel = viewModel;

    public string Name => "AiChat";
    public Version Version => new(1, 0, 0);
    public string Title => "AI Chat";
    public ToolDock DefaultDock => ToolDock.Right;
    public int Order => 40;

    public Task InitializeAsync(IPluginContext ctx) => Task.CompletedTask;

    public object CreateToolView() => new AiChatView { DataContext = _viewModel };
}
