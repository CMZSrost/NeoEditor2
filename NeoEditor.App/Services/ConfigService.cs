using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using NeoEditor.Core.Model;
using NeoEditor.Infra.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeoEditor.Services;

public class ConfigService : IConfigService
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public AppConfig Config { get; private set; } = new AppConfig();

    public async Task LoadAsync()
    {
        if (Design.IsDesignMode)
        {
            Config = new AppConfig()
            {
                GameRootDir = "D:\\software\\Steam\\steamapps\\common\\Neo Scavenger"
            };
        }
        else if (!File.Exists("config.json"))
        {
            Config = new AppConfig();
            await SaveAsync();
        }
        else
        {
            var json = await File.ReadAllTextAsync("config.json");
            // R28 + provider list: each AiProviders[].ApiKey is stored encrypted (ProtectedData)
            // — decrypt them back to plaintext in memory so the AI services / settings UI can use them.
            var obj = JObject.Parse(json);
            DecryptProviderKeys(obj);
            MigrateLegacyAiConfig(obj);

            Config = obj.ToObject<AppConfig>() ?? new AppConfig();
        }
    }

    public async Task SaveAsync()
    {
        if (Design.IsDesignMode)
        {
            var json = JsonConvert.SerializeObject(Config, Formatting.Indented);
            Serilog.Log.Logger.Debug("Save config: {Config}", json);
            return;
        }

        await _writeLock.WaitAsync();
        try
        {
            // R28 + provider list: encrypt every AiProviders[].ApiKey at rest — never write
            // a plaintext key to config.json.
            var obj = JObject.FromObject(Config);
            EncryptProviderKeys(obj);

            var json = obj.ToString(Formatting.Indented);
            await File.WriteAllTextAsync("config.json", json);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Encrypt each provider's ApiKey in the JSON tree (no-op when absent).</summary>
    private static void EncryptProviderKeys(JObject obj)
    {
        if (obj["AiProviders"] is not JArray providers) return;
        foreach (var p in providers.OfType<JObject>())
        {
            if (p["ApiKey"] is JValue key)
                p["ApiKey"] = ConfigValueProtector.Encrypt(key.Value<string>());
        }
    }

    /// <summary>Decrypt each provider's ApiKey in the JSON tree back to plaintext.</summary>
    private static void DecryptProviderKeys(JObject obj)
    {
        if (obj["AiProviders"] is not JArray providers) return;
        foreach (var p in providers.OfType<JObject>())
        {
            if (p["ApiKey"] is JValue key)
                p["ApiKey"] = ConfigValueProtector.Decrypt(key.Value<string>());
        }
    }

    /// <summary>
    /// Migrate a pre-provider-list config: when no AiProviders exist but legacy top-level
    /// AiEndpoint / AiApiKey tokens do, synthesize a single "Default" provider from them
    /// (the legacy key may itself be encrypted — Decrypt handles both forms).
    /// </summary>
    private static void MigrateLegacyAiConfig(JObject obj)
    {
        if (obj["AiProviders"] is JArray { Count: > 0 }) return;
        if (obj["AiEndpoint"] is null && obj["AiApiKey"] is null) return;

        var endpoint = obj["AiEndpoint"]?.Value<string>();
        if (string.IsNullOrWhiteSpace(endpoint))
            endpoint = "https://api.openai.com/v1";
        var apiKey = ConfigValueProtector.Decrypt(obj["AiApiKey"]?.Value<string>());

        obj["AiProviders"] = new JArray(new JObject
        {
            ["Id"] = "default",
            ["Name"] = "Default",
            ["Endpoint"] = endpoint,
            ["ApiKey"] = apiKey
        });
        obj.Remove("AiEndpoint");
        obj.Remove("AiApiKey");
    }
}

/// <summary>
/// Encrypts/decrypts a config value with Windows DPAPI (ProtectedData).
/// Scope: CurrentUser (default) — only this user/machine can decrypt.
/// </summary>
internal static class ConfigValueProtector
{
    private static readonly byte[] Entropy =
    {
        0x4E, 0x65, 0x6F, 0x45, 0x64, 0x69, 0x74, 0x6F, 0x72, 0x2D, 0x4D, 0x43, 0x50, 0x2D, 0x41, 0x49
    };

    /// <summary>Encrypt a plaintext value to a base64 string. Null/empty stays unchanged.</summary>
    public static string? Encrypt(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return plaintext;
        var bytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plaintext), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Decrypt a base64-encoded encrypted value back to plaintext. Null/empty stays unchanged.</summary>
    public static string? Decrypt(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return ciphertext;
        try
        {
            var bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(ciphertext), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (CryptographicException)
        {
            // Decryption failed (e.g. key from a different user/machine) — return as-is.
            return ciphertext;
        }
        catch (FormatException)
        {
            // Not encrypted (legacy plaintext key) — return as-is rather than crash.
            return ciphertext;
        }
    }
}