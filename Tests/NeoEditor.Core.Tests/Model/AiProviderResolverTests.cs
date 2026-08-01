using NeoEditor.Core.Model;
using Xunit;

namespace NeoEditor.Core.Tests.Model;

/// <summary>
/// Provider-list resolution: per-model provider selection (by id → first → environment),
/// endpoint/key fallback chains, and the disabled state (null) when no key exists anywhere.
/// </summary>
public class AiProviderResolverTests
{
    private static AppConfig CfgWithProviders(params AiProviderConfig[] providers)
    {
        var cfg = new AppConfig();
        cfg.AiProviders.AddRange(providers);
        return cfg;
    }

    private static AiProviderConfig P(string id, string key = "k", string endpoint = "https://ep/v1") =>
        new() { Id = id, Name = id, Endpoint = endpoint, ApiKey = key };

    [Fact]
    public void SelectProvider_EmptyOrUnknownId_ReturnsFirst()
    {
        var cfg = CfgWithProviders(P("a"), P("b"));

        Assert.Equal("a", AiProviderResolver.SelectProvider(cfg, "")!.Id);
        Assert.Equal("a", AiProviderResolver.SelectProvider(cfg, "unknown")!.Id);
    }

    [Fact]
    public void SelectProvider_ById_ReturnsExactMatch()
    {
        var cfg = CfgWithProviders(P("a"), P("b"));

        Assert.Equal("b", AiProviderResolver.SelectProvider(cfg, "b")!.Id);
    }

    [Fact]
    public void SelectProvider_NoProviders_ReturnsNull()
    {
        var cfg = new AppConfig();

        Assert.Null(AiProviderResolver.SelectProvider(cfg, ""));
    }

    [Fact]
    public void Resolve_UsesSelectedProviderKeyAndEndpoint()
    {
        var cfg = CfgWithProviders(P("a", key: "ka", endpoint: "https://a/v1"),
            P("b", key: "kb", endpoint: "https://b/v1"));

        var r = AiProviderResolver.Resolve(cfg, "b")!;

        Assert.Equal("https://b/v1", r.Endpoint);
        Assert.Equal("kb", r.ApiKey);
    }

    [Fact]
    public void Resolve_FallsBackToEnvironment_WhenNoProviders()
    {
        var cfg = new AppConfig();

        var r = AiProviderResolver.Resolve(cfg, "",
            envEndpoint: "http://localhost:11434/v1", envApiKey: "env-key")!;

        Assert.Equal("http://localhost:11434/v1", r.Endpoint);
        Assert.Equal("env-key", r.ApiKey);
    }

    [Fact]
    public void Resolve_ProviderWithoutKey_FallsBackToEnvironmentKey()
    {
        var cfg = CfgWithProviders(new AiProviderConfig { Id = "a", Endpoint = "https://a/v1" });

        var r = AiProviderResolver.Resolve(cfg, "a", envApiKey: "env-key")!;

        Assert.Equal("https://a/v1", r.Endpoint);
        Assert.Equal("env-key", r.ApiKey);
    }

    [Fact]
    public void Resolve_NoKeyAnywhere_ReturnsNullDisabled()
    {
        var cfg = CfgWithProviders(new AiProviderConfig { Id = "a", Endpoint = "https://a/v1" });

        Assert.Null(AiProviderResolver.Resolve(cfg, "a"));
    }

    [Fact]
    public void Resolve_ProviderEndpointEmpty_UsesOpenAiDefault()
    {
        var cfg = CfgWithProviders(new AiProviderConfig { Id = "a", ApiKey = "k" });

        var r = AiProviderResolver.Resolve(cfg, "a")!;

        Assert.Equal("https://api.openai.com/v1", r.Endpoint);
    }

    [Theory]
    [InlineData("cfg-model", "env-model", "default-model", "cfg-model")]
    [InlineData("", "env-model", "default-model", "env-model")]
    [InlineData("", "", "default-model", "default-model")]
    public void ResolveModelName_Priority(string cfg, string env, string dflt, string expected)
    {
        Assert.Equal(expected, AiProviderResolver.ResolveModelName(cfg, env, dflt));
    }
}