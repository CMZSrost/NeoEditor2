using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Infra.Services;
using OpenAI.Chat;
using OpenAI.Embeddings;
using Xunit;

namespace NeoEditor.Plugins.AiChat.Tests;

/// <summary>
/// Provider-list DI wiring (Phase 9D R28 + provider list): when a provider with a key is
/// configured, AddAiChatPlugin resolves real ChatClient / EmbeddingClient; with no provider
/// (and no environment key) both degrade to null so the AI stack reports "not configured".
/// </summary>
public class ChatServiceProviderTests
{
    private sealed class StubConfigService : IConfigService
    {
        public StubConfigService(AppConfig cfg) => Config = cfg;
        public AppConfig Config { get; }
        public Task LoadAsync() => Task.CompletedTask;
        public Task SaveAsync() => Task.CompletedTask;
    }

    private static System.IServiceProvider Build(bool withProvider)
    {
        var cfg = new AppConfig();
        if (withProvider)
        {
            cfg.AiProviders.Add(new AiProviderConfig
            {
                Id = "openai",
                Name = "OpenAI",
                Endpoint = "http://localhost:9999/v1",
                ApiKey = "test-key"
            });
            cfg.AiModel = "chat-model";
            cfg.AiEmbeddingModel = "embed-model";
        }

        var services = new ServiceCollection();
        services.AddSingleton<IConfigService>(new StubConfigService(cfg));
        // AiChatViewModel → IRagService (RagService) needs IHostService; a disabled RagService never uses it.
        services.AddSingleton<IHostService>(_ => null!);
        services.AddAiChatPlugin();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void ConfiguredProvider_ResolvesChatAndEmbeddingClients()
    {
        var sp = Build(withProvider: true);

        Assert.NotNull(sp.GetService<ChatClient>());
        Assert.NotNull(sp.GetService<EmbeddingClient>());
    }

    [Fact]
    public void NoProvider_ResolvesNullClients()
    {
        var sp = Build(withProvider: false);

        Assert.Null(sp.GetService<ChatClient>());
        Assert.Null(sp.GetService<EmbeddingClient>());
    }
}