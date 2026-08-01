using System.IO;
using System.Threading.Tasks;
using NeoEditor.Core.Model;
using NeoEditor.Services;
using Xunit;

namespace NeoEditor.Integration.Tests;

/// <summary>
/// Phase 9D (R28 + provider list): AI API keys must never be stored in plaintext in config.json —
/// each AiProviders[].ApiKey is encrypted at rest with Windows DPAPI (ProtectedData) and decrypted
/// on load. Legacy pre-provider-list configs are migrated to a single "Default" provider.
/// </summary>
public class ConfigServiceEncryptionTests
{
    private const string ConfigPath = "config.json";

    [Fact]
    public async Task SaveAsync_EncryptsProviderApiKeys_AtRest()
    {
        var originalExists = File.Exists(ConfigPath);
        var original = originalExists ? await File.ReadAllTextAsync(ConfigPath) : null;
        try
        {
            var svc = new ConfigService();
            svc.Config.AiProviders.Add(new AiProviderConfig
            {
                Id = "openai",
                Name = "OpenAI",
                Endpoint = "http://localhost:9999/v1",
                ApiKey = "super-secret-key-123"
            });
            await svc.SaveAsync();

            // The raw file must not contain the plaintext key, but must still hold it (encrypted).
            var raw = await File.ReadAllTextAsync(ConfigPath);
            Assert.DoesNotContain("super-secret-key-123", raw);
            Assert.Contains("AiProviders", raw);

            // A fresh ConfigService decrypts it back to plaintext.
            var svc2 = new ConfigService();
            await svc2.LoadAsync();
            var provider = Assert.Single(svc2.Config.AiProviders);
            Assert.Equal("super-secret-key-123", provider.ApiKey);
            Assert.Equal("http://localhost:9999/v1", provider.Endpoint);
        }
        finally
        {
            await RestoreOrDeleteAsync(originalExists, original);
        }
    }

    [Fact]
    public async Task LoadAsync_HandlesLegacyPlaintextProviderKey_WithoutCrashing()
    {
        var originalExists = File.Exists(ConfigPath);
        var original = originalExists ? await File.ReadAllTextAsync(ConfigPath) : null;
        try
        {
            // Config written before the encryption feature: a plaintext provider ApiKey.
            await File.WriteAllTextAsync(ConfigPath,
                "{\"AiProviders\": [{\"Id\": \"default\", \"Name\": \"Default\", " +
                "\"Endpoint\": \"https://api.openai.com/v1\", \"ApiKey\": \"legacy-plain-key\"}]}");

            var svc = new ConfigService();
            await svc.LoadAsync();
            var provider = Assert.Single(svc.Config.AiProviders);
            Assert.Equal("legacy-plain-key", provider.ApiKey);
        }
        finally
        {
            await RestoreOrDeleteAsync(originalExists, original);
        }
    }

    [Fact]
    public async Task LoadAsync_MigratesLegacyFlatAiConfig_ToSingleProvider()
    {
        var originalExists = File.Exists(ConfigPath);
        var original = originalExists ? await File.ReadAllTextAsync(ConfigPath) : null;
        try
        {
            // Legacy pre-provider-list config with flat AiEndpoint / AiApiKey.
            await File.WriteAllTextAsync(ConfigPath,
                "{\"AiEndpoint\": \"http://localhost:9999/v1\", \"AiApiKey\": \"legacy-flat-key\"}");

            var svc = new ConfigService();
            await svc.LoadAsync();
            var provider = Assert.Single(svc.Config.AiProviders);
            Assert.Equal("default", provider.Id);
            Assert.Equal("http://localhost:9999/v1", provider.Endpoint);
            Assert.Equal("legacy-flat-key", provider.ApiKey);
        }
        finally
        {
            await RestoreOrDeleteAsync(originalExists, original);
        }
    }

    private static async Task RestoreOrDeleteAsync(bool originalExists, string? original)
    {
        if (!originalExists)
        {
            if (File.Exists(ConfigPath)) File.Delete(ConfigPath);
        }
        else if (original is not null)
        {
            await File.WriteAllTextAsync(ConfigPath, original);
        }
    }
}