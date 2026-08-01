using System;
using System.ClientModel;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;
using NeoEditor.Plugins.AiChat.Services;
using NeoEditor.Plugins.AiChat.ViewModels;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;

namespace NeoEditor.Plugins.AiChat;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiChatPlugin(this IServiceCollection services)
    {
        services.AddSingleton<IToolPlugin, AiChatPlugin>();

        // Config source of truth (R28): provider list in config.json first, environment
        // variables as fallback (see AiProviderResolver). Each model picks its own provider
        // by AiModelProviderId / AiEmbeddingProviderId — one provider need not serve all models.
        // When NO provider/environment has an api key the whole AI stack is registered in a
        // DISABLED state (null clients) so the GUI never crashes — AI Chat / RAG report
        // "not configured" instead. Config is read once at startup, so enabling requires a
        // restart (Settings → AI & MCP).

        // OpenAI-compatible ChatClient — built from the chat model's provider.
        services.AddSingleton<ChatClient>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfigService>().Config;
            var provider = AiProviderResolver.Resolve(
                cfg, cfg.AiModelProviderId,
                Environment.GetEnvironmentVariable("OPENAI_ENDPOINT"),
                Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
            if (provider is null) return null!; // disabled — no api key configured

            var modelId = AiProviderResolver.ResolveModelName(
                cfg.AiModel,
                Environment.GetEnvironmentVariable("OPENAI_MODEL"),
                "gpt-4o");
            var options = new OpenAIClientOptions { Endpoint = new Uri(provider.Endpoint) };
            return new OpenAIClient(new ApiKeyCredential(provider.ApiKey), options).GetChatClient(modelId);
        });

        // OpenAI-compatible EmbeddingClient (for RAG) — built from the embedding model's provider.
        //   For Ollama: set the embedding model to e.g. nomic-embed-text
        services.AddSingleton<EmbeddingClient>(sp =>
        {
            var cfg = sp.GetRequiredService<IConfigService>().Config;
            var provider = AiProviderResolver.Resolve(
                cfg, cfg.AiEmbeddingProviderId,
                Environment.GetEnvironmentVariable("OPENAI_ENDPOINT"),
                Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
            if (provider is null) return null!; // disabled — no api key configured

            var modelId = AiProviderResolver.ResolveModelName(
                cfg.AiEmbeddingModel,
                Environment.GetEnvironmentVariable("OPENAI_EMBEDDING_MODEL"),
                "text-embedding-3-small");
            var options = new OpenAIClientOptions { Endpoint = new Uri(provider.Endpoint) };
            return new OpenAIClient(new ApiKeyCredential(provider.ApiKey), options).GetEmbeddingClient(modelId);
        });

        // Services
        services.AddSingleton<SystemPromptBuilder>();
        services.AddSingleton<EntitySummaryBuilder>();
        services.AddSingleton<IRagService, RagService>();
        services.AddSingleton<IChatService, ChatService>();
        services.AddSingleton<ChatHistoryManager>();

        // ViewModel — transient for fresh state per tool instance
        services.AddTransient<AiChatViewModel>();

        return services;
    }
}