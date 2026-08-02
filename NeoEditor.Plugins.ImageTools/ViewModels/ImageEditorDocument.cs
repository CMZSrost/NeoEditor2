using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.Core.Abstractions;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.ImageTools.Helper;
using NeoEditor.Plugins.ImageTools.Services;

namespace NeoEditor.Plugins.ImageTools.ViewModels;

/// <summary>
/// Image editor document (single-image editor): a crop-enabled source slot on the
/// left, a processing-result slot on the right, target size / aspect-ratio logic,
/// pixel-art options, and a manual Apply command (parameter changes mark the result
/// stale instead of clearing it). The document always opens with a source image —
/// the create-image document handles material sourcing.
/// </summary>
public partial class ImageEditorDocument : ImageToolDocumentBase, IDisposable
{
    private readonly IImageEditorProcessingService _processingService;
    private readonly IImageFileService _fileService;
    private bool _isUpdatingAspectRatio;

    /// <summary>Source slot: editable image with the crop overlay.</summary>
    public ImageSlotViewModel Source { get; }

    /// <summary>Result slot: pixelated processing output.</summary>
    public ImageSlotViewModel Result { get; }

    [ObservableProperty] public partial int TargetWidth { get; set; } = 100;
    [ObservableProperty] public partial int TargetHeight { get; set; } = 100;
    [ObservableProperty] public partial bool LockAspectRatio { get; set; } = true;

    // ── Pixel Art Conversion Options ──
    [ObservableProperty] public partial int ColorCount { get; set; } = 24;
    [ObservableProperty] public partial bool EdgeEnhancement { get; set; } = true;
    [ObservableProperty] public partial bool DitheringEnabled { get; set; } = false;
    [ObservableProperty] public partial bool TransparentBackground { get; set; } = true;

    public string ColorCountDisplay => $"{ColorCount} colors";
    public int MaxColorCount => PixelArtConversionOptions.MaxColorCount;
    public int MinColorCount => PixelArtConversionOptions.MinColorCount;

    public bool CanApply => Source.HasImage && TargetWidth > 0 && TargetHeight > 0;

    public ImageEditorDocument(IImageEditorProcessingService processingService,
        IImageFileService fileService,
        ILocalizationService loc)
        : base(loc)
    {
        _processingService = processingService;
        _fileService = fileService;
        Source = new ImageSlotViewModel(loc, fileService, isCropEnabled: true, titleKey: "OriginalImage",
            emptyHintKey: "NoImageSelected");
        Result = new ImageSlotViewModel(loc, fileService, isCropEnabled: false, titleKey: "PixelatedImage",
            emptyHintKey: "NoProcessedImage");
        Source.ImageChanged += OnSourceImageChanged;
        Source.CropChanged += OnSourceCropChanged;
        Source.SetSaveHandler(_fileService.SaveAsync);
        Result.SetSaveHandler(_fileService.SaveAsync);
        SetLocalizedTitle("AddImage");
    }

    [RelayCommand]
    public async Task SelectImage()
    {
        var paths = await _fileService.PickImagesAsync(allowMultiple: false);
        if (paths.Length == 0)
        {
            return;
        }

        LoadImage(paths[0]);
    }

    /// <summary>Load an image file into the source slot (used by the App shell factory).</summary>
    public void LoadImage(string path)
    {
        Source.LoadFile(path);
    }

    /// <summary>Recompute the processing result with the current source, crop and options.</summary>
    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        var request = CreateProcessingRequest();
        if (request is null)
        {
            return;
        }

        try
        {
            var pixelOptions = new PixelArtConversionOptions(
                TargetWidth, TargetHeight,
                ColorCount, EdgeEnhancement,
                DitheringEnabled, TransparentBackground);

            var processed = await _processingService.CreatePixelArtPreviewAsync(request, pixelOptions);
            if (processed is null)
            {
                Result.Clear();
                return;
            }

            Result.ShowBitmap(processed.PreviewBitmap);
            Result.SetOverlay(string.Empty);
        }
        catch
        {
            Result.Clear();
        }
    }

    public void Dispose()
    {
        Source.ImageChanged -= OnSourceImageChanged;
        Source.CropChanged -= OnSourceCropChanged;
        Source.Dispose();
        Result.Dispose();
    }

    // ── Source slot wiring ──

    private void OnSourceImageChanged(object? sender, EventArgs e)
    {
        if (Source.Image is { } image)
        {
            var initialOutputSize = PixelArtOutputSizeCalculator.ResolveNearest(
                image.PixelSize.Width,
                image.PixelSize.Height,
                image.PixelSize.Width / (double)image.PixelSize.Height);
            SetTargetSize(initialOutputSize);
            SetStaticTitle(Source.ImageName);
        }
        else
        {
            SetTargetSize(new PixelArtOutputSize(PixelArtOutputSizeCalculator.BaseStep * 10,
                PixelArtOutputSizeCalculator.BaseStep * 10));
            SetLocalizedTitle("AddImage");
        }

        SetStaleResult();
        ApplyCommand.NotifyCanExecuteChanged();
    }

    private void OnSourceCropChanged(object? sender, EventArgs e)
    {
        if (LockAspectRatio)
        {
            SetTargetSize(
                PixelArtOutputSizeCalculator.ResolveNearest(TargetWidth, TargetHeight, GetCurrentAspectRatio()));
        }

        SetStaleResult();
    }

    /// <summary>Mark the current result as stale (source/crop/options changed since Apply).</summary>
    private void SetStaleResult()
    {
        if (Result.HasImage)
        {
            Result.SetOverlay(Loc["StaleResultHint"]);
        }
    }

    // ── Target size / aspect ratio (snap-to-step + lock) ──

    partial void OnTargetWidthChanged(int value)
    {
        if (value <= 0)
        {
            TargetWidth = PixelArtOutputSizeCalculator.BaseStep;
            return;
        }

        var normalizedWidth = PixelArtOutputSizeCalculator.SnapToBaseStep(value);
        if (normalizedWidth != value)
        {
            TargetWidth = normalizedWidth;
            return;
        }

        if (!_isUpdatingAspectRatio && LockAspectRatio)
        {
            SetTargetSize(PixelArtOutputSizeCalculator.ResolveFromWidth(normalizedWidth, GetCurrentAspectRatio()));
        }

        ApplyCommand.NotifyCanExecuteChanged();
        SetStaleResult();
    }

    partial void OnTargetHeightChanged(int value)
    {
        if (value <= 0)
        {
            TargetHeight = PixelArtOutputSizeCalculator.BaseStep;
            return;
        }

        var normalizedHeight = PixelArtOutputSizeCalculator.SnapToBaseStep(value);
        if (normalizedHeight != value)
        {
            TargetHeight = normalizedHeight;
            return;
        }

        if (!_isUpdatingAspectRatio && LockAspectRatio)
        {
            SetTargetSize(PixelArtOutputSizeCalculator.ResolveFromHeight(normalizedHeight, GetCurrentAspectRatio()));
        }

        ApplyCommand.NotifyCanExecuteChanged();
        SetStaleResult();
    }

    partial void OnLockAspectRatioChanged(bool value)
    {
        if (!Source.HasImage)
        {
            return;
        }

        if (value)
        {
            SetTargetSize(
                PixelArtOutputSizeCalculator.ResolveNearest(TargetWidth, TargetHeight, GetCurrentAspectRatio()));
        }

        SetStaleResult();
    }

    partial void OnColorCountChanged(int value)
    {
        // Clamp to valid range
        if (value < PixelArtConversionOptions.MinColorCount)
        {
            ColorCount = PixelArtConversionOptions.MinColorCount;
            return;
        }

        if (value > PixelArtConversionOptions.MaxColorCount)
        {
            ColorCount = PixelArtConversionOptions.MaxColorCount;
            return;
        }

        OnPropertyChanged(nameof(ColorCountDisplay));
        SetStaleResult();
    }

    partial void OnEdgeEnhancementChanged(bool value) => SetStaleResult();

    partial void OnDitheringEnabledChanged(bool value) => SetStaleResult();

    partial void OnTransparentBackgroundChanged(bool value) => SetStaleResult();

    // ── Processing request ──

    private ImageEditorProcessingRequest? CreateProcessingRequest()
    {
        if (!CanApply)
        {
            return null;
        }

        // Prefer the in-memory source bytes (AI candidates); fall back to the file path.
        var source = Source.SourceBytes is { } bytes
            ? ImageSource.FromBytes(bytes)
            : Source.FilePath is { Length: > 0 } path && File.Exists(path)
                ? ImageSource.FromPath(path)
                : null;
        if (source is null)
        {
            return null;
        }

        return new ImageEditorProcessingRequest(source, TargetWidth, TargetHeight, Source.CropRect);
    }

    private double GetCurrentAspectRatio()
    {
        if (Source.CropRect is { } crop && crop.Width > 0 && crop.Height > 0)
        {
            return crop.Width / (double)crop.Height;
        }

        if (Source.Image is { PixelSize.Width: > 0, PixelSize.Height: > 0 } image)
        {
            return image.PixelSize.Width / (double)image.PixelSize.Height;
        }

        return TargetHeight <= 0 ? 1D : TargetWidth / (double)TargetHeight;
    }

    private void SetTargetSize(PixelArtOutputSize size)
    {
        if (TargetWidth == size.Width && TargetHeight == size.Height)
        {
            return;
        }

        _isUpdatingAspectRatio = true;
        try
        {
            TargetWidth = size.Width;
            TargetHeight = size.Height;
        }
        finally
        {
            _isUpdatingAspectRatio = false;
        }
    }
}
