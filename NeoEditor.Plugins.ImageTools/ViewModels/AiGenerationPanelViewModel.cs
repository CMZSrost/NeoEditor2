using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.Core.Abstractions;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.ImageTools.Services;

namespace NeoEditor.Plugins.ImageTools.ViewModels;

/// <summary>
/// AI generation form for the create-image document: prompt + free size + a
/// configurable candidate count. Each generated candidate is raised via
/// <see cref="CandidateGenerated"/> as it completes — the host stages it into its
/// pending list (there is no in-panel gallery; the right-side preview pane is the
/// single place to inspect materials).
/// </summary>
public partial class AiGenerationPanelViewModel : ObservableObject
{
    private readonly IImageGenerationService _imageGenerationService;
    private readonly ILocalizationService _loc;
    private readonly IConfigService _config;
    private readonly IAiPromptPresetService _presetService;
    private int _generationTotal;
    private int _succeededCount;

    [ObservableProperty] public partial string AiPrompt { get; set; } = string.Empty;
    [ObservableProperty] public partial int AiWidth { get; set; } = 512;
    [ObservableProperty] public partial int AiHeight { get; set; } = 512;
    [ObservableProperty] public partial int CandidateCount { get; set; } = 4;
    [ObservableProperty] public partial bool IsGenerating { get; set; }
    [ObservableProperty] public partial string GenerationError { get; set; } = string.Empty;
    [ObservableProperty] public partial int CompletedCount { get; set; }

    /// <summary>Presets from ai-prompt-presets.json (dropdown items). Reassigned when a
    /// preset is saved so the dropdown picks up the new entry.</summary>
    public IReadOnlyList<AiPromptPreset> Presets { get; private set; }

    /// <summary>Selected preset; selecting one fills <see cref="AiPrompt"/> (and size when
    /// the preset carries one), then the selection resets so it can be re-applied.</summary>
    [ObservableProperty] public partial AiPromptPreset? SelectedPreset { get; set; }

    /// <summary>Name for the "save as preset" action; empty = auto-generated.</summary>
    [ObservableProperty] public partial string NewPresetName { get; set; } = string.Empty;

    /// <summary>Raised once per successfully generated candidate (name = ai_candidate_N.png).
    /// The host stages the bytes and queues them into its pending list.</summary>
    public event Action<byte[], string>? CandidateGenerated;

    public int AiSizeMin => 512;
    public int AiSizeMax => 2880;
    public int AiSizeStep => 16;
    public int MinCandidateCount => 1;
    public int MaxCandidateCount => 8;

    public bool IsAiAvailable => _imageGenerationService.IsAvailable;
    public bool IsAiUnavailable => !_imageGenerationService.IsAvailable;
    public bool HasGenerationError => !string.IsNullOrWhiteSpace(GenerationError);
    public bool CanGenerate => _imageGenerationService.IsAvailable && !string.IsNullOrWhiteSpace(AiPrompt) && !IsGenerating;
    public bool CanSavePreset => !string.IsNullOrWhiteSpace(AiPrompt);

    /// <summary>Generation progress text (e.g. "2/4") shown while generating.</summary>
    public string GenerationProgressText => _generationTotal > 0 ? $"{CompletedCount}/{_generationTotal}" : string.Empty;

    public ILocalizationService Loc => _loc;

    public AiGenerationPanelViewModel(
        IImageGenerationService imageGenerationService,
        ILocalizationService loc,
        IConfigService config,
        IAiPromptPresetService presetService)
    {
        _imageGenerationService = imageGenerationService;
        _loc = loc;
        _config = config;
        _presetService = presetService;
        Presets = presetService.GetPresets();
        CandidateCount = Math.Clamp(_config.Config.AiCandidateCount, MinCandidateCount, MaxCandidateCount);
    }

    /// <summary>Selecting a preset fills the prompt (and size when present), then resets
    /// the selection so the same preset can be picked again after editing.</summary>
    partial void OnSelectedPresetChanged(AiPromptPreset? value)
    {
        if (value is null)
        {
            return;
        }

        AiPrompt = value.Prompt;
        if (value.Width is { } width)
        {
            AiWidth = width;
        }
        if (value.Height is { } height)
        {
            AiHeight = height;
        }
        SelectedPreset = null;
    }

    partial void OnAiPromptChanged(string value)
    {
        _ = value;
        // The button's IsEnabled binds to CanGenerate (not just the command), so the
        // property needs a notification — otherwise it stays disabled after typing.
        OnPropertyChanged(nameof(CanGenerate));
        GenerateCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanSavePreset));
        SavePresetCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsGeneratingChanged(bool value)
    {
        _ = value;
        OnPropertyChanged(nameof(CanGenerate));
        GenerateCommand.NotifyCanExecuteChanged();
    }

    partial void OnCompletedCountChanged(int value)
    {
        _ = value;
        OnPropertyChanged(nameof(GenerationProgressText));
    }

    partial void OnGenerationErrorChanged(string value)
    {
        _ = value;
        OnPropertyChanged(nameof(HasGenerationError));
    }

    /// <summary>Save the current prompt (and size) as a preset; the dropdown refreshes
    /// immediately. An empty name gets an auto-generated one ("自定义模板", numbered when
    /// taken). Saving works without an AI provider — it is pure local file I/O.</summary>
    [RelayCommand(CanExecute = nameof(CanSavePreset))]
    private void SavePreset()
    {
        var name = NewPresetName.Trim();
        if (name.Length == 0)
        {
            name = GeneratePresetName();
        }

        _presetService.AddOrUpdatePreset(new AiPromptPreset(name, AiPrompt, AiWidth, AiHeight));
        Presets = _presetService.GetPresets();
        OnPropertyChanged(nameof(Presets));
        NewPresetName = string.Empty;
    }

    private string GeneratePresetName()
    {
        var baseName = _loc["AiPromptPresetDefaultName"];
        var taken = Presets.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var name = baseName;
        var suffix = 2;
        while (taken.Contains(name))
        {
            name = $"{baseName} {suffix++}";
        }
        return name;
    }

    /// <summary>Generate <see cref="CandidateCount"/> candidates in parallel; each one is
    /// raised via <see cref="CandidateGenerated"/> as it completes (the awaited
    /// continuations run on the UI thread).</summary>
    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        if (IsGenerating)
        {
            return;
        }

        IsGenerating = true;
        GenerationError = string.Empty;
        CompletedCount = 0;
        _succeededCount = 0;

        // Persist the configured count for next time.
        _config.Config.AiCandidateCount = CandidateCount;

        var count = Math.Clamp(CandidateCount, MinCandidateCount, MaxCandidateCount);
        _generationTotal = count;
        OnPropertyChanged(nameof(GenerationProgressText));

        var width = AiWidth > 0 ? AiWidth : 512;
        var height = AiHeight > 0 ? AiHeight : 512;
        var options = new ImageGenerationOptions(Width: width, Height: height,
            RequestSize: $"{width}x{height}", ApplyPixelArt: false);

        var tasks = Enumerable.Range(0, count).Select(async index =>
        {
            try
            {
                var result = await _imageGenerationService.GenerateAsync(AiPrompt, options);
                _succeededCount++;
                CandidateGenerated?.Invoke(result.ImageBytes, $"ai_candidate_{index + 1}.png");
            }
            catch
            {
                // One candidate failing must not fail the batch; a fully failed batch
                // surfaces below via GenerationError.
            }
            finally
            {
                CompletedCount++;
            }
        });

        await Task.WhenAll(tasks);
        IsGenerating = false;

        if (_succeededCount == 0)
        {
            GenerationError = _loc["AiGenerationFailedHint"];
        }
    }
}
