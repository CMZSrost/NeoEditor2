using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.Core.Abstractions;
using NeoEditor.Infra.Services;

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
    private int _generationTotal;
    private int _succeededCount;

    [ObservableProperty] public partial string AiPrompt { get; set; } = string.Empty;
    [ObservableProperty] public partial int AiWidth { get; set; } = 512;
    [ObservableProperty] public partial int AiHeight { get; set; } = 512;
    [ObservableProperty] public partial int CandidateCount { get; set; } = 4;
    [ObservableProperty] public partial bool IsGenerating { get; set; }
    [ObservableProperty] public partial string GenerationError { get; set; } = string.Empty;
    [ObservableProperty] public partial int CompletedCount { get; set; }

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

    /// <summary>Generation progress text (e.g. "2/4") shown while generating.</summary>
    public string GenerationProgressText => _generationTotal > 0 ? $"{CompletedCount}/{_generationTotal}" : string.Empty;

    public ILocalizationService Loc => _loc;

    public AiGenerationPanelViewModel(
        IImageGenerationService imageGenerationService,
        ILocalizationService loc,
        IConfigService config)
    {
        _imageGenerationService = imageGenerationService;
        _loc = loc;
        _config = config;
        CandidateCount = Math.Clamp(_config.Config.AiCandidateCount, MinCandidateCount, MaxCandidateCount);
    }

    partial void OnAiPromptChanged(string value)
    {
        _ = value;
        // The button's IsEnabled binds to CanGenerate (not just the command), so the
        // property needs a notification — otherwise it stays disabled after typing.
        OnPropertyChanged(nameof(CanGenerate));
        GenerateCommand.NotifyCanExecuteChanged();
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
