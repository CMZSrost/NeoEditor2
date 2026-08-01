using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.AiChat.ViewModels;
using NeoEditor.Plugins.AiChat.Views;
using Xunit;

namespace NeoEditor.Plugins.AiChat.Tests;

public class AiChatPluginTests
{
    private sealed class StubConfigService : IConfigService
    {
        public StubConfigService(AppConfig cfg) => Config = cfg;
        public AppConfig Config { get; }
        public Task LoadAsync() => Task.CompletedTask;
        public Task SaveAsync() => Task.CompletedTask;
    }

    private static System.IServiceProvider Build()
    {
        var cfg = new AppConfig(); // no provider → disabled AI stack, never crashes (9D v1.9)
        var services = new ServiceCollection();
        services.AddSingleton<IConfigService>(new StubConfigService(cfg));
        services.AddSingleton<IHostService>(_ => null!);
        services.AddAiChatPlugin();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Plugin_HasCorrectMetadata()
    {
        var plugin = new AiChatPlugin(null!);

        Assert.Equal("AiChat", plugin.Name);
        Assert.Equal("AI Chat", plugin.Title);
        Assert.Equal(new Version(1, 0, 0), plugin.Version);
        Assert.Equal(ToolDock.Right, plugin.DefaultDock);
        Assert.Equal(40, plugin.Order);
    }

    [Fact]
    public void Plugin_IsDecoratedWith_Workbench_PluginKind()
    {
        var attr = typeof(AiChatPlugin).GetCustomAttributes(typeof(PluginKindAttribute), false);

        Assert.Single(attr);
        var kind = (PluginKindAttribute)attr[0];
        Assert.Equal(PluginKind.Workbench, kind.Kind);
    }

    [Fact]
    public void Plugin_Implements_IToolPlugin()
    {
        var plugin = new AiChatPlugin(null!);
        Assert.IsAssignableFrom<IToolPlugin>(plugin);
        Assert.IsAssignableFrom<IPlugin>(plugin);
    }

    [Fact]
    public async Task InitializeAsync_CompletesSuccessfully()
    {
        var plugin = new AiChatPlugin(null!);
        await plugin.InitializeAsync(null!);
    }

    [Fact]
    public void CreateToolView_ReturnsAiChatViewBoundToViewModel()
    {
        var sp = Build();
        var plugin = new AiChatPlugin(sp.GetRequiredService<AiChatViewModel>());

        var view = plugin.CreateToolView();

        var aiChatView = Assert.IsType<AiChatView>(view);
        Assert.IsType<AiChatViewModel>(aiChatView.DataContext);
    }
}
