using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NeoEditor.Core.Abstractions;

namespace NeoEditor.Player.Core.Services;

/// <summary>One on-disk save backup (v2.37; manual backups v2.41).</summary>
public sealed record SaveBackup(string FilePath, string Key, DateTime SavedAt, long Length,
    bool IsManual, string? Name)
{
    public string SizeText => $"{Length / 1024.0:F1} KB";

    /// <summary>Manual backups carry a user-chosen name; auto backups show the key.</summary>
    public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name! : Key;
}

/// <summary>
/// Save backups (Docs/42 v2.37): the game deletes its save on death, so every
/// localStorage write/remove is preceded by a backup of the OLD value. Backups land in
/// <c>{gameRoot}/save_backup</c> (user requirement) — NOT localStorage, which shares the
/// ~5MB quota with the save itself. The newest <see cref="KeepCount"/> backups are kept.
/// The directory follows <see cref="IConfigService.Config"/> dynamically (the game root
/// is only known once a SWF is opened).
/// </summary>
public sealed class SaveBackupService
{
    public const int KeepCount = 5;
    private const string FilePrefix = "backup-";
    private const string ManualPrefix = "manual-";

    private readonly IConfigService? _config;

    public SaveBackupService(IConfigService? config = null)
    {
        _config = config;
    }

    /// <summary>Backup directory: {gameRoot}/save_backup (LocalAppData fallback when no config).</summary>
    public string Directory => _config is { Config.GameRootDir.Length: > 0 } config
        ? Path.Combine(config.Config.GameRootDir, "save_backup")
        : Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NeoScavengerPlayer", "saves");

    /// <summary>Back up an old value BEFORE it is overwritten/removed (host /__backup).</summary>
    public void Backup(string key, string value)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var safeKey = new string(key.Select(ch =>
                Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());
            var path = Path.Combine(Directory, $"{FilePrefix}{DateTime.Now:yyyyMMdd-HHmmssfff}-{safeKey}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(new BackupPayload
            {
                Key = key,
                SavedAt = DateTime.Now,
                Value = value,
            }));
            TrimOldFiles();
        }
        catch (Exception)
        {
            // backups are best-effort — never take the player down
        }
    }

    /// <summary>Parse a page /__backup POST body ({k, v} — the OLD value before overwrite).</summary>
    public void HandleBackupRequest(string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<BackupRequest>(json);
            if (payload is { K.Length: > 0 } && payload.V is not null)
                Backup(payload.K, payload.V);
        }
        catch (Exception)
        {
        }
    }

    /// <summary>
    /// Manual backup (v2.41): the user explicitly saves the CURRENT value with a chosen
    /// name. Named `manual-*` — the automatic newest-5 trim only touches `backup-*`
    /// files, so manual saves are never overwritten or evicted.
    /// </summary>
    public void SaveManual(string key, string value, string name)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            var safeName = Sanitize(name);
            var path = Path.Combine(Directory,
                $"{ManualPrefix}{DateTime.Now:yyyyMMdd-HHmmssfff}-{safeName}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(new BackupPayload
            {
                Key = key,
                SavedAt = DateTime.Now,
                Value = value,
                Manual = true,
                Name = name,
            }));
        }
        catch (Exception)
        {
            // backups are best-effort — never take the player down
        }
    }

    /// <summary>Rename a manual backup (v2.41): display name changes, timestamp stays.</summary>
    public void Rename(string filePath, string newName)
    {
        try
        {
            if (!File.Exists(filePath)) return;
            var payload = JsonSerializer.Deserialize<BackupPayload>(File.ReadAllText(filePath));
            if (payload is null || !payload.Manual) return;
            payload.Name = newName;

            var fileName = Path.GetFileName(filePath);
            var timestamp = fileName.Length > ManualPrefix.Length + 17
                ? fileName.Substring(ManualPrefix.Length, 17)
                : DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
            var newPath = Path.Combine(Directory,
                $"{ManualPrefix}{timestamp}-{Sanitize(newName)}.json");
            if (string.Equals(newPath, filePath, StringComparison.OrdinalIgnoreCase)) return;
            File.WriteAllText(newPath, JsonSerializer.Serialize(payload));
            File.Delete(filePath);
        }
        catch (Exception)
        {
            // best-effort
        }
    }

    private static string Sanitize(string name)
    {
        var trimmed = name.Trim();
        var safe = new string(trimmed.Select(ch =>
            Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch).ToArray());
        return safe.Length == 0 ? "backup" : safe;
    }

    /// <summary>Newest-first backup list (auto `backup-*` + manual `manual-*`).</summary>
    public IReadOnlyList<SaveBackup> List()
    {
        if (!System.IO.Directory.Exists(Directory)) return [];
        return System.IO.Directory.EnumerateFiles(Directory, "*.json")
            .Where(f => Path.GetFileName(f).StartsWith(FilePrefix) ||
                        Path.GetFileName(f).StartsWith(ManualPrefix))
            .OrderByDescending(Path.GetFileName)
            .Select(TryRead)
            .Where(b => b is not null)
            .Cast<SaveBackup>()
            .ToList();
    }

    /// <summary>The backed-up value of a backup file (null when unreadable).</summary>
    public string? ReadValue(string filePath)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<BackupPayload>(File.ReadAllText(filePath));
            return payload?.Value;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Delete(string filePath)
    {
        try
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
        catch (Exception)
        {
        }
    }

    private SaveBackup? TryRead(string path)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<BackupPayload>(File.ReadAllText(path));
            if (payload is null) return null;
            return new SaveBackup(path, payload.Key, payload.SavedAt,
                new FileInfo(path).Length, payload.Manual, payload.Name);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Trim only AUTO backups (newest 5) — manual saves are never evicted.</summary>
    private void TrimOldFiles()
    {
        try
        {
            var stale = System.IO.Directory.EnumerateFiles(Directory, FilePrefix + "*.json")
                .OrderByDescending(Path.GetFileName)
                .Skip(KeepCount);
            foreach (var file in stale)
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private sealed class BackupRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("k")]
        public string? K { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("v")]
        public string? V { get; set; }
    }

    private sealed class BackupPayload
    {
        public string Key { get; set; } = "";
        public DateTime SavedAt { get; set; }
        public string Value { get; set; } = "";
        public bool Manual { get; set; }
        public string? Name { get; set; }
    }
}
