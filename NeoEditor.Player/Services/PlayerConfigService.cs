using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using NeoEditor.Core.Abstractions;
using NeoEditor.Core.Model;

namespace NeoEditor.Player.Services;

/// <summary>
/// IConfigService for the standalone player: GameRootDir is set at runtime from the
/// opened SWF's directory; Theme/Language persist to
/// %LocalAppData%/NeoScavengerPlayer/settings.json (v2.28).
/// </summary>
public sealed class PlayerConfigService : IConfigService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NeoScavengerPlayer", "settings.json");

    public AppConfig Config { get; } = new();

    /// <summary>UI language code ("zh" / "en") — persisted with the theme.</summary>
    public string Language { get; set; } = "zh";

    public Task LoadAsync()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return Task.CompletedTask;
            var settings = JsonSerializer.Deserialize<PlayerSettings>(File.ReadAllText(SettingsPath));
            if (settings is null) return Task.CompletedTask;

            if (settings.Theme is "System" or "Light" or "Dark")
                Config.Theme = settings.Theme;
            if (settings.Language is "zh" or "en")
                Language = settings.Language;
            // v2.40: the loopback port MUST persist — a fresh random port per launch
            // changes the page origin, and WebView2 isolates localStorage per origin,
            // which makes saves vanish. (v2.36 never serialized it.)
            if (settings.ServerPort is > 0 and < 65536)
                Config.ServerPort = settings.ServerPort.Value;
            // v2.79: 记住上次打开的游戏目录（玩家不懂拖 SWF，启动自动加载上次游戏）。
            if (!string.IsNullOrWhiteSpace(settings.GameRootDir) && System.IO.Directory.Exists(settings.GameRootDir))
                Config.GameRootDir = settings.GameRootDir;
        }
        catch (Exception)
        {
            // Unreadable settings → defaults
        }
        return Task.CompletedTask;
    }

    public Task SaveAsync()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new PlayerSettings
            {
                Theme = Config.Theme,
                Language = Language,
                ServerPort = Config.ServerPort,
                GameRootDir = string.IsNullOrWhiteSpace(Config.GameRootDir) ? null : Config.GameRootDir,
            }));
        }
        catch (Exception)
        {
            // Best-effort persistence
        }
        return Task.CompletedTask;
    }

    private sealed class PlayerSettings
    {
        public string? Theme { get; set; }
        public string? Language { get; set; }
        public int? ServerPort { get; set; }
        public string? GameRootDir { get; set; }
    }
}
