using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Messages;
using Serilog;

namespace NeoEditor.Services;

/// <summary>
/// R42: plays extracted game sounds via winmm MCI (zero-dependency, Windows-only —
/// the editor targets Windows). Asset index: {GameRootDir}/sounds/index.json
/// produced by player-tools/extract-sounds.js. Cue matching is exact first,
/// then substring (strSnd values like "cueRifle" map to assets like
/// "NEOScavengerSounds_cueRiflePickup").
/// </summary>
public class AudioPlaybackService : IAudioPlaybackService
{
    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, System.Text.StringBuilder? returnString,
        int returnLength, IntPtr hwndCallback);

    private readonly IConfigService _config;
    private string? _currentFile;

    public AudioPlaybackService(IConfigService config)
    {
        _config = config;
        // reload the index when the game root switches
        WeakReferenceMessenger.Default.Register<GameRootDirChangedMessage>(this, (_, _) => _assets = null);
    }

    private List<AudioAsset>? _assets;

    private List<AudioAsset> Assets
    {
        get
        {
            if (_assets is not null) return _assets;
            _assets = LoadIndex();
            return _assets;
        }
    }

    private List<AudioAsset> LoadIndex()
    {
        try
        {
            var gameRoot = _config.Config.GameRootDir;
            var indexPath = Path.Combine(gameRoot, "sounds", "index.json");
            if (!File.Exists(indexPath)) return [];

            var list = System.Text.Json.JsonSerializer.Deserialize<List<AudioAsset>>(File.ReadAllText(indexPath),
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return list ?? [];
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "[Audio] failed to load sound index");
            return [];
        }
    }

    public bool IsAvailable => Assets.Count > 0;

    public void Play(string cueName)
    {
        if (string.IsNullOrWhiteSpace(cueName)) return;
        var file = FindFile(cueName);
        if (file is null) return;
        PlayFile(file);
    }

    public void Stop()
    {
        if (_currentFile is null) return;
        mciSendString($"close snd_{_currentFile.GetHashCode():x}", null, 0, IntPtr.Zero);
        _currentFile = null;
    }

    private string? FindFile(string cueName)
    {
        // exact match on the bare name first, then substring (strSnd → asset)
        var bare = cueName.Trim();
        var exact = Assets.FirstOrDefault(a => string.Equals(a.Name, bare, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact.File;
        var hit = Assets.FirstOrDefault(a =>
            a.Name.Contains(bare, StringComparison.OrdinalIgnoreCase)
            || bare.Contains(a.Name, StringComparison.OrdinalIgnoreCase));
        return hit?.File;
    }

    private void PlayFile(string fileName)
    {
        try
        {
            var gameRoot = _config.Config.GameRootDir;
            var full = Path.Combine(gameRoot, "sounds", fileName);
            if (!File.Exists(full)) return;

            Stop();
            var alias = $"snd_{full.GetHashCode():x}";
            var err = mciSendString($"open \"{full}\" type mpegvideo alias {alias}", null, 0, IntPtr.Zero);
            if (err != 0)
            {
                Log.Logger.Warning("[Audio] mci open failed ({Err}) for {File}", err, full);
                return;
            }
            mciSendString($"play {alias} from 0", null, 0, IntPtr.Zero);
            _currentFile = full;
        }
        catch (Exception ex)
        {
            Log.Logger.Warning(ex, "[Audio] playback failed for {File}", fileName);
        }
    }

    /// <summary>index.json entry — name is the cue/asset name, file the mp3 relative path.</summary>
    private sealed class AudioAsset
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string File { get; set; } = "";
        public int Bytes { get; set; }
    }
}
