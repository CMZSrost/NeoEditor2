using NeoEditor.Core.Model;

namespace NeoEditor.Core.Tests.Model;

/// <summary>Phase 9D (R28 + provider list): AI/MCP configuration defaults and round-trip.</summary>
public class AppConfigTests
{
    [Fact]
    public void AppConfig_AiMcpFields_HaveExpectedDefaults()
    {
        var cfg = new AppConfig();

        Assert.Empty(cfg.AiProviders);
        Assert.Equal("", cfg.AiModelProviderId);
        Assert.Equal("", cfg.AiEmbeddingProviderId);
        Assert.Equal("", cfg.ImageProviderId);
        Assert.Equal("gpt-4o", cfg.AiModel);
        Assert.Equal("text-embedding-3-small", cfg.AiEmbeddingModel);
        Assert.Equal("dall-e-3", cfg.ImageModel);
        Assert.False(cfg.McpEnabled);
        Assert.Equal(0, cfg.McpPort);
    }

    [Fact]
    public void AppConfig_AiMcpFields_AreRoundTrippableViaJson()
    {
        var cfg = new AppConfig
        {
            AiProviders =
            {
                new AiProviderConfig
                {
                    Id = "openai", Name = "OpenAI",
                    Endpoint = "https://api.openai.com/v1", ApiKey = "key-1"
                },
                new AiProviderConfig
                {
                    Id = "local", Name = "Ollama",
                    Endpoint = "http://localhost:11434/v1", ApiKey = ""
                }
            },
            AiModelProviderId = "openai",
            AiEmbeddingProviderId = "local",
            ImageProviderId = "openai",
            AiModel = "local-model",
            AiEmbeddingModel = "nomic-embed-text",
            ImageModel = "sdxl",
            McpEnabled = true,
            McpPort = 5000
        };

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(cfg);
        var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<AppConfig>(json)!;

        Assert.Equal(2, restored.AiProviders.Count);
        Assert.Equal("openai", restored.AiProviders[0].Id);
        Assert.Equal("OpenAI", restored.AiProviders[0].Name);
        Assert.Equal("https://api.openai.com/v1", restored.AiProviders[0].Endpoint);
        Assert.Equal("key-1", restored.AiProviders[0].ApiKey);
        Assert.Equal("local", restored.AiProviders[1].Id);
        Assert.Equal("openai", restored.AiModelProviderId);
        Assert.Equal("local", restored.AiEmbeddingProviderId);
        Assert.Equal("openai", restored.ImageProviderId);
        Assert.Equal("local-model", restored.AiModel);
        Assert.Equal("nomic-embed-text", restored.AiEmbeddingModel);
        Assert.Equal("sdxl", restored.ImageModel);
        Assert.True(restored.McpEnabled);
        Assert.Equal(5000, restored.McpPort);
    }
}