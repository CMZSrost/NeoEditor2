using System;
using System.Linq;

namespace NeoEditor.Core.Model;

/// <summary>Resolved endpoint + api key ready for client construction.</summary>
public sealed record ResolvedAiProvider(string Endpoint, string ApiKey);

/// <summary>
/// Pure static resolution helpers for the AI provider list (no mutable state, N01-compliant).
/// Selection precedence for a model's provider: explicit <c>ProviderId</c> match → first
/// provider in the list → environment variables → disabled (null).
/// Model names resolve: config field → environment variable → built-in default.
/// </summary>
public static class AiProviderResolver
{
    private const string DefaultEndpoint = "https://api.openai.com/v1";

    /// <summary>
    /// Pick the provider a model should use. Empty/unknown <paramref name="providerId"/>
    /// resolves to the first configured provider; otherwise the id must match exactly.
    /// </summary>
    public static AiProviderConfig? SelectProvider(AppConfig cfg, string providerId)
    {
        if (cfg.AiProviders.Count == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(providerId))
        {
            var byId = cfg.AiProviders.FirstOrDefault(p => string.Equals(p.Id, providerId, StringComparison.Ordinal));
            if (byId is not null)
                return byId;
        }

        return cfg.AiProviders[0];
    }

    /// <summary>
    /// Resolve endpoint + api key for a model's provider. Returns null (disabled) when no
    /// api key is available from the provider or the environment. Endpoint falls back to the
    /// environment and finally the OpenAI default.
    /// </summary>
    public static ResolvedAiProvider? Resolve(AppConfig cfg, string providerId,
        string? envEndpoint = null, string? envApiKey = null)
    {
        var provider = SelectProvider(cfg, providerId);
        var endpoint = FirstNonEmpty(provider?.Endpoint, envEndpoint, DefaultEndpoint);
        var apiKey = FirstNonEmpty(provider?.ApiKey, envApiKey);
        if (string.IsNullOrWhiteSpace(apiKey))
            return null; // disabled — no key configured anywhere

        return new ResolvedAiProvider(endpoint, apiKey);
    }

    /// <summary>Model id fallback chain: config value → env var → built-in default.</summary>
    public static string ResolveModelName(string configValue, string? envValue, string defaultValue)
        => FirstNonEmpty(configValue, envValue, defaultValue);

    private static string FirstNonEmpty(params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            if (!string.IsNullOrWhiteSpace(c)) return c;
        }

        return "";
    }
}