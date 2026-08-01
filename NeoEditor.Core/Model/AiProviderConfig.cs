namespace NeoEditor.Core.Model;

/// <summary>
/// A single OpenAI-compatible API provider (endpoint + api key) that a model can be bound to.
/// One provider may not serve every model (chat / embeddings / image), so AppConfig holds a
/// list of these and each model selects its provider by <see cref="Id"/>.
/// <c>ApiKey</c> is encrypted at rest in config.json (DPAPI) by ConfigService.
/// </summary>
public class AiProviderConfig
{
    /// <summary>Stable identity referenced by the per-model ProviderId fields.</summary>
    public string Id { get; set; } = "";

    /// <summary>Display name shown in the Settings provider list / model dropdowns.</summary>
    public string Name { get; set; } = "";

    /// <summary>OpenAI-compatible base URL (e.g. https://api.openai.com/v1).</summary>
    public string Endpoint { get; set; } = "";

    /// <summary>API key (plaintext in memory, encrypted at rest).</summary>
    public string ApiKey { get; set; } = "";
}