using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.AiChat.Services;
using NeoEditor.Plugins.AiChat.ViewModels;
using Xunit;

namespace NeoEditor.Plugins.AiChat.Tests;

/// <summary>
/// Regression: when no API key is configured the AI stack registers null clients, so the GUI
/// must not crash at startup and the services must degrade gracefully — <c>IsAvailable == false</c>
/// and sending returns a friendly "not configured" notice instead of throwing.
/// </summary>
public class ChatServiceAvailabilityTests
{
    private static ChatService CreateDisabledChatService()
    {
        // Empty provider: IMcpToolProvider / IRagService resolve to null.
        var provider = new ServiceCollection().BuildServiceProvider();
        return new ChatService(null, provider, new ChatHistoryManager(), new SystemPromptBuilder());
    }

    [Fact]
    public void ChatService_WithoutClient_ReportsUnavailable()
    {
        var service = CreateDisabledChatService();

        Assert.False(service.IsAvailable);
    }

    [Fact]
    public async Task SendMessage_WithoutClient_ReturnsNotConfiguredNotice()
    {
        var service = CreateDisabledChatService();

        var response = await service.SendMessageAsync("hello");

        Assert.Contains("not configured", response, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendMessageStreaming_WithoutClient_YieldsNotConfiguredNotice()
    {
        var service = CreateDisabledChatService();

        var chunks = new List<string>();
        await foreach (var chunk in service.SendMessageStreamingAsync("hello"))
            chunks.Add(chunk);

        Assert.Single(chunks);
        Assert.Contains("not configured", chunks[0], System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddAiChatPlugin_WithoutApiKey_ResolvesAiChatViewModelAsDisabled()
    {
        // Regression for the GUI startup crash: with no API key configured,
        // `new ApiKeyCredential("")` used to throw ArgumentException inside the
        // OpenAIClient factory, which bubbled up through ChatClient → IChatService →
        // AiChatViewModel → DocumentWorkspaceViewModel and killed the whole GUI.
        // Now the client chain degrades to null and the full AiChat graph resolves
        // as disabled — the GUI boots and shows a "not configured" notice instead.
        var services = new ServiceCollection();
        services.AddSingleton<IConfigService>(new StubConfigService());
        // AiChatViewModel → IRagService (RagService) needs IHostService. A null instance
        // is safe here — a disabled RagService never uses it.
        services.AddSingleton<IHostService>(_ => null!);
        services.AddAiChatPlugin();
        var sp = services.BuildServiceProvider();

        var vm = sp.GetRequiredService<AiChatViewModel>();

        Assert.False(vm.IsAvailable);
        Assert.False(vm.CanSend);
        Assert.False(vm.CanBuildIndex);
    }

    private sealed class StubConfigService : IConfigService
    {
        public AppConfig Config { get; } = new AppConfig(); // AiApiKey defaults to empty

        public Task LoadAsync() => Task.CompletedTask;

        public Task SaveAsync() => Task.CompletedTask;
    }
}
