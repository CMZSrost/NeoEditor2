using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NeoEditor.Core.Abstractions;
using NeoEditor.Infra.Services;

namespace NeoEditor.ViewModels.ExplorerPane;

/// <summary>
/// R42: sounds tool dock — browse the extracted game audio assets
/// ({GameRootDir}/sounds/*.mp3 from player-tools/extract-sounds.js) and play
/// them. Cue names are searchable so a modder can verify what "cueRiflePickup"
/// actually sounds like.
/// </summary>
public partial class SoundsToolViewModel : ViewModelBase
{
    private readonly IAudioPlaybackService _audio;
    private readonly IConfigService _config;
    private readonly ILogger<SoundsToolViewModel> _logger;

    public ObservableCollection<SoundEntry> Sounds { get; } = new();

    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private string? _playingName;
    [ObservableProperty] private string _status = "";

    partial void OnFilterChanged(string value) => ApplyFilter();

    public SoundsToolViewModel(IAudioPlaybackService audio, IConfigService config,
        ILocalizationService localizationService, INotificationService notificationService,
        ILogger<SoundsToolViewModel> logger)
        : base(localizationService, notificationService, logger)
    {
        _audio = audio;
        _config = config;
        _logger = logger;
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        Sounds.Clear();
        try
        {
            var gameRoot = _config.Config.GameRootDir;
            var indexPath = Path.Combine(gameRoot, "sounds", "index.json");
            if (!File.Exists(indexPath))
            {
                Status = "未找到声音索引 — 运行 player-tools/extract-sounds.js 提取";
                return;
            }

            var entries = System.Text.Json.JsonSerializer
                .Deserialize<List<SoundAsset>>(File.ReadAllText(indexPath),
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            foreach (var e in entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
                Sounds.Add(new SoundEntry(e.Name, e.File, e.Bytes));
            Status = $"{Sounds.Count} 个音频资产";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load sounds index");
            Status = $"加载失败: {ex.Message}";
        }
        ApplyFilter();
    }

    private List<SoundEntry> _all = [];

    private void ApplyFilter()
    {
        var q = Filter.Trim();
        _all = string.IsNullOrEmpty(q)
            ? Sounds.ToList()
            : Sounds.Where(s => s.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    [RelayCommand]
    private void PlayToggle(SoundEntry? entry)
    {
        if (entry is null) return;
        if (PlayingName == entry.Name)
        {
            _audio.Stop();
            PlayingName = null;
            return;
        }
        _audio.Play(entry.Name);
        PlayingName = entry.Name;
        Status = $"播放: {entry.Name}";
    }

    public sealed record SoundEntry(string Name, string File, int Bytes)
    {
        public string SizeText => Bytes >= 1024 * 1024
            ? $"{Bytes / 1024.0 / 1024.0:F1} MB"
            : $"{Bytes / 1024.0:F0} KB";
    }

    private sealed class SoundAsset
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string File { get; set; } = "";
        public int Bytes { get; set; }
    }
}
