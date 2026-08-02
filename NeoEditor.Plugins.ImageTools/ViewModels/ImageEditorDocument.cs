using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.Core.Abstractions;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.ImageTools.Helper;
using NeoEditor.Plugins.ImageTools.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using Size = SixLabors.ImageSharp.Size;

namespace NeoEditor.Plugins.ImageTools.ViewModels;

public partial class ImageEditorDocument : ImageToolDocumentBase
{
    private bool _isUpdatingAspectRatio;
    private readonly IImageEditorProcessingService _processingService;
    private readonly PixelArtConversionService _pixelArtService;
    private readonly IImageGenerationService _imageGenerationService;
    private ImageCropSelection _cropSelection = ImageCropSelection.Empty;
    private const string OutputExtension = ".png";

    /// <summary>Original PNG bytes of the AI image. The pixelation pipeline reads straight
    /// from these bytes (ImageSharp is pure managed and headless-testable) instead of
    /// round-tripping through <see cref="Avalonia.Media.Imaging.Bitmap.Save(System.IO.Stream)"/>,
    /// whose PNG encoding is not reliable under the headless test platform.</summary>
    private byte[]? _aiSourceBytes;

    [ObservableProperty] public partial string ImagePath { get; set; } = string.Empty;
    [ObservableProperty] public partial string ImageName { get; set; } = string.Empty;
    [ObservableProperty] public partial string ImageDimensions { get; set; } = string.Empty;
    [ObservableProperty] public partial string ImageFileSize { get; set; } = string.Empty;
    [ObservableProperty] public partial string NormalOutputPath { get; set; } = string.Empty;
    [ObservableProperty] public partial string NormalOutputName { get; set; } = string.Empty;
    [ObservableProperty] public partial string NormalOutputFileSize { get; set; } = string.Empty;
    [ObservableProperty] public partial string ProcessedImagePath { get; set; } = string.Empty;
    [ObservableProperty] public partial string ProcessedImageName { get; set; } = string.Empty;
    [ObservableProperty] public partial string ProcessedImageDimensions { get; set; } = string.Empty;
    [ObservableProperty] public partial string ProcessedImageFileSize { get; set; } = string.Empty;
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

    public Bitmap? SelectedImage
    {
        get;
        private set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field?.Dispose();
            SetProperty(ref field, value);
        }
    }

    public Bitmap? ProcessedImage
    {
        get;
        private set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field?.Dispose();
            SetProperty(ref field, value);
        }
    }

    /// <summary>The AI-generated image (workbench). Distinct from the original source.</summary>
    public Bitmap? AiImage
    {
        get;
        private set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field?.Dispose();
            SetProperty(ref field, value);
        }
    }

    /// <summary>Pixel-art processing of the AI-generated image.</summary>
    public Bitmap? AiProcessedImage
    {
        get;
        private set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field?.Dispose();
            SetProperty(ref field, value);
        }
    }

    public bool HasImage => SelectedImage is not null;
    public bool HasNoImage => !HasImage;
    public bool HasProcessedImage => ProcessedImage is not null;
    public bool HasNoProcessedImage => !HasProcessedImage;
    public bool HasAiImage => AiImage is not null;
    public bool HasNoAiImage => !HasAiImage;
    public bool HasAiProcessedImage => AiProcessedImage is not null;
    public bool HasNoAiProcessedImage => !HasAiProcessedImage;

    // ── Slot titles: localised name + image dimensions when the slot is populated. ──
    // Shown in the workbench header of each of the 4 panes; dimensions are hidden when the
    // pane is empty (no image yet).
    public string OriginalTitle => BuildTitle("OriginalImage", HasImage, ImageDimensions);
    public string ProcessedTitle => BuildTitle("PixelatedImage", HasProcessedImage, ProcessedImageDimensions);
    public string AiTitle => BuildTitle("AiGeneratedImage", HasAiImage, AiDimensions);
    public string AiProcessedTitle => BuildTitle("AiPixelatedImage", HasAiProcessedImage, AiProcessedDimensions);

    public string AiDimensions => HasAiImage && AiImage is { } ai ? FormatDimensions(ai.PixelSize.Width, ai.PixelSize.Height) : string.Empty;
    public string AiProcessedDimensions => HasAiProcessedImage && AiProcessedImage is { } aip
        ? FormatDimensions(aip.PixelSize.Width, aip.PixelSize.Height)
        : string.Empty;

    private string BuildTitle(string locKey, bool hasImage, string dimensions)
        => hasImage && !string.IsNullOrWhiteSpace(dimensions)
            ? $"{Loc[locKey]} ({dimensions})"
            : Loc[locKey];
    public int CropLeft => _cropSelection.Left;
    public int CropTop => _cropSelection.Top;
    public int CropRight => _cropSelection.Right;
    public int CropBottom => _cropSelection.Bottom;
    public PixelRect? CropRect => TryGetNormalizedCropRect();

    public bool HasSelection => HasImage && CropRect is { } crop && SelectedImage is { } image &&
                                (crop.X != 0 || crop.Y != 0 || crop.Width != image.PixelSize.Width ||
                                 crop.Height != image.PixelSize.Height);

    public string SelectionDimensions => HasImage && CropRect is { } selection
        ? FormatDimensions(selection.Width, selection.Height)
        : string.Empty;

    public string NormalOutputDimensions => HasImage ? FormatDimensions(TargetWidth, TargetHeight) : string.Empty;

    public string X2OutputDimensions => HasImage
        ? FormatDimensions(TargetWidth * PixelArtOutputSizeCalculator.X2Scale,
            TargetHeight * PixelArtOutputSizeCalculator.X2Scale)
        : string.Empty;

    public bool CanPixelate => HasImage && TargetWidth > 0 && TargetHeight > 0;
    public bool CanPixelateAiImage => HasAiImage && TargetWidth > 0 && TargetHeight > 0;
    public bool CanSaveProcessedImage => HasProcessedImage;
    public bool CanSaveOriginalImage => HasImage;
    public bool CanSaveAiImage => HasAiImage;
    public bool CanSaveAiProcessedImage => HasAiProcessedImage;

    // ── AI image generation (workbench panel) ──
    [ObservableProperty] public partial string AiPrompt { get; set; } = string.Empty;

    /// <summary>Requested width for AI image generation (px). Zhipu CogView requires side
    /// length in [512, 2880] and a multiple of 16 — the XAML NumericUpDown enforces that.</summary>
    [ObservableProperty] public partial int AiWidth { get; set; } = 512;

    /// <summary>Requested height for AI image generation (px).</summary>
    [ObservableProperty] public partial int AiHeight { get; set; } = 512;

    /// <summary>AI size input constraints (CogView-compatible).</summary>
    public int AiSizeMin => 512;
    public int AiSizeMax => 2880;
    public int AiSizeStep => 16;

    /// <summary>True while an AI image is being generated — drives the loading indicator.</summary>
    [ObservableProperty] public partial bool IsGeneratingAi { get; set; }

    /// <summary>Last AI generation error message (empty when the last call succeeded).</summary>
    [ObservableProperty] public partial string AiGenerationError { get; set; } = string.Empty;

    public bool HasAiGenerationError => !string.IsNullOrWhiteSpace(AiGenerationError);

    /// <summary>True when the AI image API is configured (menu/panel can be used).</summary>
    public bool IsAiAvailable => _imageGenerationService.IsAvailable;

    /// <summary>True when the AI image API is NOT configured — shown as an inline hint.</summary>
    public bool IsAiUnavailable => !_imageGenerationService.IsAvailable;

    public bool CanGenerateAiImage => _imageGenerationService.IsAvailable && !string.IsNullOrWhiteSpace(AiPrompt);

    partial void OnAiPromptChanged(string value)
    {
        _ = value;
        // The button's IsEnabled binds to CanGenerateAiImage (not just the command), so it
        // needs a property-changed notification — otherwise the button stays disabled after
        // the user types a prompt.
        OnPropertyChanged(nameof(CanGenerateAiImage));
        AiGenerateCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanGenerateAiImage))]
    private async Task AiGenerateAsync()
    {
        if (IsGeneratingAi)
            return;

        IsGeneratingAi = true;
        AiGenerationError = string.Empty;
        OnPropertyChanged(nameof(HasAiGenerationError));
        try
        {
            var width = AiWidth > 0 ? AiWidth : 512;
            var height = AiHeight > 0 ? AiHeight : 512;
            // The workbench shows the raw AI image first; pixel-art post-processing is a
            // separate, explicit "Pixelate" step. Forcing ApplyPixelArt here (the default)
            // would garble a realistic CogView render into noise — hence false.
            var options = new ImageGenerationOptions(Width: width, Height: height,
                RequestSize: $"{width}x{height}", ApplyPixelArt: false);
            var result = await _imageGenerationService.GenerateAsync(AiPrompt, options);
            LoadGeneratedImage(result.ImageBytes, "ai_generated.png");
        }
        catch (Exception ex)
        {
            // Surface the real failure so the user isn't left staring at an empty AI slot
            // after the loading bar disappears.
            AiGenerationError = ex.Message;
        }
        finally
        {
            IsGeneratingAi = false;
            OnPropertyChanged(nameof(HasAiGenerationError));
        }
    }

    public ImageEditorDocument(IImageEditorProcessingService processingService,
        PixelArtConversionService pixelArtService,
        IImageGenerationService imageGenerationService,
        ILocalizationService loc)
        : base(loc)
    {
        _processingService = processingService;
        _pixelArtService = pixelArtService;
        _imageGenerationService = imageGenerationService;
        SetLocalizedTitle("AddImage");
    }

    [RelayCommand]
    public async Task SelectImage()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
        var storageProvider = topLevel?.StorageProvider;
        if (storageProvider is null)
        {
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc["SelectImage"],
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Loc["ImageFiles"])
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp"]
                }
            ]
        });

        if (files.Count == 0)
        {
            return;
        }

        var path = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        LoadImage(path);
    }

    [RelayCommand]
    public async Task PixelateImage()
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
                ClearProcessedImage();
                return;
            }

            ProcessedImage = processed.PreviewBitmap;
            NormalOutputName = GetSuggestedNormalFileName();
            NormalOutputPath = string.Empty;
            NormalOutputFileSize = string.Empty;
            ProcessedImageName = GetSuggestedX2FileName(NormalOutputName);
            ProcessedImagePath = string.Empty;
            ProcessedImageDimensions = FormatDimensions(processed.X2Width, processed.X2Height);
            ProcessedImageFileSize = string.Empty;
            NotifyProcessedStateChanged();
        }
        catch
        {
            ClearProcessedImage();
        }
    }

    /// <summary>Pixelate the AI-generated image (its own pipeline, no crop).</summary>
    [RelayCommand(CanExecute = nameof(CanPixelateAiImage))]
    private async Task PixelateAiImage()
    {
        if (AiImage is null || _aiSourceBytes is null)
        {
            return;
        }

        try
        {
            // Decode straight from the source bytes — ImageSharp is pure managed code and
            // works under headless, unlike Avalonia's Bitmap PNG encoding (see _aiSourceBytes).
            using var sourceImage = Image.Load<Rgba32>(_aiSourceBytes);
            var pixelOptions = new PixelArtConversionOptions(
                TargetWidth, TargetHeight,
                ColorCount, EdgeEnhancement,
                DitheringEnabled, TransparentBackground);
            using var pixelArtImage = await _pixelArtService.ConvertToPixelArtAsync(sourceImage, pixelOptions);
            AiProcessedImage = ToAvaloniaBitmap(pixelArtImage);
            NotifyAiProcessedStateChanged();
        }
        catch
        {
            ClearAiProcessedImage();
        }
    }

    /// <summary>Encode a bitmap as PNG. Avalonia's <c>Save(Stream, int?)</c> is obsolete in
    /// favor of BitmapEncoderOptions; default PNG quality is exactly what we want, so the
    /// deprecated overload is intentionally used.</summary>
    private static void SavePng(Bitmap bitmap, Stream stream)
    {
#pragma warning disable CS0618 // Bitmap.Save(Stream, int?) is obsolete; default PNG encoding is intended.
        bitmap.Save(stream);
#pragma warning restore CS0618
    }

    private static Bitmap ToAvaloniaBitmap(Image<Rgba32> image)
    {
        // Decode via a temp file (see LoadGeneratedImage): Avalonia Bitmap(Stream) keeps a
        // reference to the source stream, and disposing it can garble Skia's rendering.
        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            image.SaveAsPng(tempPath);
            return new Bitmap(tempPath);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
        }
    }

    /// <summary>Per-image save: every workbench pane saves its own image so it is clear
    /// which one is being written. Each save writes the PNG plus a 2× (x2_) version.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveOriginalImage))]
    private Task SaveOriginalImageAsync() => SaveBitmapPairAsync(SelectedImage, GetSuggestedNormalFileName());

    [RelayCommand(CanExecute = nameof(CanSaveProcessedImage))]
    private Task SaveProcessedImageAsync() => SaveBitmapPairAsync(ProcessedImage, GetSuggestedNormalFileName());

    [RelayCommand(CanExecute = nameof(CanSaveAiImage))]
    private Task SaveAiImageAsync() => SaveBitmapPairAsync(AiImage, "ai_generated.png");

    [RelayCommand(CanExecute = nameof(CanSaveAiProcessedImage))]
    private Task SaveAiProcessedImageAsync() => SaveBitmapPairAsync(AiProcessedImage, "ai_processed.png");

    private async Task SaveBitmapPairAsync(Bitmap? bitmap, string suggestedName)
    {
        if (bitmap is null)
        {
            return;
        }

        var storageProvider = GetStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Loc["SaveImage"],
            SuggestedFileName = suggestedName,
            FileTypeChoices =
            [
                new FilePickerFileType("PNG") { Patterns = ["*.png"] }
            ],
            DefaultExtension = GetDefaultSaveExtension()
        });

        if (file is null)
        {
            return;
        }

        try
        {
            var selectedPath = Path.GetFullPath(file.TryGetLocalPath() ?? string.Empty);
            var directory = Path.GetDirectoryName(selectedPath) ?? string.Empty;
            var normalFileName = NormalizeNormalOutputFileName(selectedPath);
            var normalPath = Path.Combine(directory, normalFileName);
            var x2Path = Path.Combine(directory, GetSuggestedX2FileName(normalFileName));

            await using (var fs = File.Create(normalPath))
            {
                SavePng(bitmap, fs);
            }

            await SaveX2VersionAsync(bitmap, x2Path);
        }
        catch
        {
            // Ignore save failures and leave the preview intact.
        }
    }

    private static async Task SaveX2VersionAsync(Bitmap source, string x2Path)
    {
        try
        {
            using var ms = new MemoryStream();
            SavePng(source, ms);
            ms.Position = 0;
            using var img = Image.Load<Rgba32>(ms);
            var x2Width = img.Width * 2;
            var x2Height = img.Height * 2;
            using var x2 = img.Clone(ctx =>
            {
                ctx.Resize(new ResizeOptions
                {
                    Size = new Size(x2Width, x2Height),
                    Mode = ResizeMode.Stretch,
                    Sampler = KnownResamplers.NearestNeighbor,
                });
            });
            await using var fs = File.Create(x2Path);
            await x2.SaveAsPngAsync(fs);
        }
        catch
        {
            // The 2× version is optional — don't fail the whole save.
        }
    }

    public void LoadImage(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            ClearImage();
            return;
        }

        try
        {
            var bitmap = new Bitmap(fullPath);
            SelectedImage = bitmap;
            ImagePath = fullPath;
            ImageName = Path.GetFileName(fullPath);
            ImageDimensions = FormatDimensions(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
            var fileInfo = new FileInfo(fullPath);
            ImageFileSize = FormatFileSize(fileInfo.Length);
            var initialOutputSize = PixelArtOutputSizeCalculator.ResolveNearest(
                bitmap.PixelSize.Width,
                bitmap.PixelSize.Height,
                bitmap.PixelSize.Width / (double)bitmap.PixelSize.Height);
            TargetWidth = initialOutputSize.Width;
            TargetHeight = initialOutputSize.Height;
            ResetCropToFullImage(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
            ClearProcessedImage();
            SetStaticTitle(ImageName);
        }
        catch
        {
            ClearImage();
        }

        NotifyImageStateChanged();
    }

    /// <summary>
    /// Load an AI-generated image (PNG bytes) into the workbench's AI slot. It is kept
    /// distinct from the original source; the user pixelates it (→ AiProcessedImage) or
    /// saves either directly. The output size defaults to the generated image's size.
    /// </summary>
    public void LoadGeneratedImage(byte[] pngBytes, string name)
    {
        try
        {
            // Decode via a temp file, matching LoadImage's file-path path. Avalonia's
            // Bitmap(Stream) keeps a reference to the source stream; disposing it (the using
            // below) can leave the Skia backend rendering garbled pixels on some platforms.
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
            try
            {
                File.WriteAllBytes(tempPath, pngBytes);
                AiImage = new Bitmap(tempPath);
            }
            finally
            {
                try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
            }
        }
        catch
        {
            ClearAiImage();
            return;
        }

        _aiSourceBytes = pngBytes;

        var initialOutputSize = PixelArtOutputSizeCalculator.ResolveNearest(
            AiImage!.PixelSize.Width,
            AiImage.PixelSize.Height,
            AiImage.PixelSize.Width / (double)AiImage.PixelSize.Height);
        TargetWidth = initialOutputSize.Width;
        TargetHeight = initialOutputSize.Height;
        ClearAiProcessedImage();

        SetStaticTitle(name);
        NotifyAiStateChanged();
    }

    private void ClearImage()
    {
        SelectedImage = null;
        ImagePath = string.Empty;
        ImageName = string.Empty;
        ImageDimensions = string.Empty;
        ImageFileSize = string.Empty;
        TargetWidth = PixelArtOutputSizeCalculator.BaseStep * 10;
        TargetHeight = PixelArtOutputSizeCalculator.BaseStep * 10;
        ResetCropToEmpty();
        ClearProcessedImage();
        SetLocalizedTitle("AddImage");
        NotifyImageStateChanged();
    }

    public void SetCropBounds(int left, int top, int right, int bottom)
    {
        if (!HasImage || SelectedImage is null)
        {
            ResetCropToEmpty();
            return;
        }

        var normalizedCrop = ImageCropSelection.Normalize(left, top, right, bottom, SelectedImage.PixelSize.Width,
            SelectedImage.PixelSize.Height, minimumSize: 2);
        if (normalizedCrop is null)
        {
            return;
        }

        UpdateCropSelection(normalizedCrop.Value);
    }

    public void SetCropRect(PixelRect? cropRect)
    {
        if (cropRect is null)
        {
            return;
        }

        SetCropBounds(cropRect.Value.X, cropRect.Value.Y, cropRect.Value.Right, cropRect.Value.Bottom);
    }

    private void UpdateCropSelection(ImageCropSelection cropSelection)
    {
        if (_cropSelection == cropSelection)
        {
            return;
        }

        _cropSelection = cropSelection;
        NotifyCropStateChanged();
    }

    private void ClearProcessedImage()
    {
        ProcessedImage = null;
        NormalOutputPath = string.Empty;
        NormalOutputName = string.Empty;
        NormalOutputFileSize = string.Empty;
        ProcessedImagePath = string.Empty;
        ProcessedImageName = string.Empty;
        ProcessedImageDimensions = string.Empty;
        ProcessedImageFileSize = string.Empty;
        NotifyProcessedStateChanged();
    }

    private void ClearAiImage()
    {
        AiImage = null;
        _aiSourceBytes = null;
        ClearAiProcessedImage();
        NotifyAiStateChanged();
    }

    private void ClearAiProcessedImage()
    {
        AiProcessedImage = null;
        NotifyAiProcessedStateChanged();
    }

    private ImageEditorProcessingRequest? CreateProcessingRequest()
    {
        if (!CanPixelate || string.IsNullOrWhiteSpace(ImagePath) || !File.Exists(ImagePath))
        {
            return null;
        }

        return new ImageEditorProcessingRequest(ImagePath, TargetWidth, TargetHeight, CropRect);
    }

    private IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
        return topLevel?.StorageProvider;
    }

    private string GetSuggestedNormalFileName()
    {
        if (string.IsNullOrWhiteSpace(ImageName))
        {
            return $"pixelated{OutputExtension}";
        }

        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(ImageName);
        return $"{fileNameWithoutExtension}{OutputExtension}";
    }

    private static string GetSuggestedX2FileName(string normalFileName)
    {
        return $"x2_{NormalizeNormalOutputFileName(normalFileName)}";
    }

    private static string NormalizeNormalOutputFileName(string fileName)
    {
        var normalizedFileName = Path.GetFileName(fileName);
        var fileNameWithoutPrefix = normalizedFileName.StartsWith("x2_", StringComparison.OrdinalIgnoreCase)
            ? normalizedFileName[3..]
            : normalizedFileName;

        return $"{Path.GetFileNameWithoutExtension(fileNameWithoutPrefix)}{OutputExtension}";
    }

    private string GetDefaultSaveExtension()
    {
        return OutputExtension;
    }

    private static string FormatDimensions(int width, int height)
    {
        return $"{width} × {height}px";
    }

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

        NotifyOutputStateChanged();
        InvalidateProcessedPreview();
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

        NotifyOutputStateChanged();
        InvalidateProcessedPreview();
    }

    partial void OnLockAspectRatioChanged(bool value)
    {
        if (!HasImage)
        {
            return;
        }

        if (value)
        {
            SetTargetSize(
                PixelArtOutputSizeCalculator.ResolveNearest(TargetWidth, TargetHeight, GetCurrentAspectRatio()));
        }

        NotifyOutputStateChanged();
        InvalidateProcessedPreview();
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
        InvalidateProcessedPreview();
    }

    partial void OnEdgeEnhancementChanged(bool value) => InvalidateProcessedPreview();

    partial void OnDitheringEnabledChanged(bool value) => InvalidateProcessedPreview();

    partial void OnTransparentBackgroundChanged(bool value) => InvalidateProcessedPreview();

    private void NotifyImageStateChanged()
    {
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(HasNoImage));
        OnPropertyChanged(nameof(CanSaveOriginalImage));
        OnPropertyChanged(nameof(OriginalTitle));
        OnPropertyChanged(nameof(ImageDimensions));
        SaveOriginalImageCommand.NotifyCanExecuteChanged();
        NotifyOutputStateChanged();
    }

    private void NotifyProcessedStateChanged()
    {
        OnPropertyChanged(nameof(HasProcessedImage));
        OnPropertyChanged(nameof(HasNoProcessedImage));
        OnPropertyChanged(nameof(CanSaveProcessedImage));
        OnPropertyChanged(nameof(ProcessedTitle));
        OnPropertyChanged(nameof(ProcessedImageDimensions));
        SaveProcessedImageCommand.NotifyCanExecuteChanged();
    }

    private void NotifyAiStateChanged()
    {
        OnPropertyChanged(nameof(HasAiImage));
        OnPropertyChanged(nameof(HasNoAiImage));
        OnPropertyChanged(nameof(CanSaveAiImage));
        OnPropertyChanged(nameof(AiTitle));
        OnPropertyChanged(nameof(AiDimensions));
        SaveAiImageCommand.NotifyCanExecuteChanged();
        NotifyOutputStateChanged();
    }

    private void NotifyAiProcessedStateChanged()
    {
        OnPropertyChanged(nameof(HasAiProcessedImage));
        OnPropertyChanged(nameof(HasNoAiProcessedImage));
        OnPropertyChanged(nameof(CanSaveAiProcessedImage));
        OnPropertyChanged(nameof(AiProcessedTitle));
        OnPropertyChanged(nameof(AiProcessedDimensions));
        SaveAiProcessedImageCommand.NotifyCanExecuteChanged();
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
    }

    private double GetCurrentAspectRatio()
    {
        if (HasSelection && _cropSelection.Width > 0 && _cropSelection.Height > 0)
        {
            return _cropSelection.AspectRatio;
        }

        if (SelectedImage is { PixelSize.Width: > 0, PixelSize.Height: > 0 } image)
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

    private void NotifyOutputStateChanged()
    {
        OnPropertyChanged(nameof(CanPixelate));
        OnPropertyChanged(nameof(CanPixelateAiImage));
        OnPropertyChanged(nameof(NormalOutputDimensions));
        OnPropertyChanged(nameof(X2OutputDimensions));
        PixelateImageCommand.NotifyCanExecuteChanged();
        PixelateAiImageCommand.NotifyCanExecuteChanged();
    }

    private void InvalidateProcessedPreview()
    {
        if (HasProcessedImage
            || !string.IsNullOrWhiteSpace(NormalOutputName)
            || !string.IsNullOrWhiteSpace(NormalOutputPath)
            || !string.IsNullOrWhiteSpace(NormalOutputFileSize)
            || !string.IsNullOrWhiteSpace(ProcessedImageName)
            || !string.IsNullOrWhiteSpace(ProcessedImagePath)
            || !string.IsNullOrWhiteSpace(ProcessedImageDimensions)
            || !string.IsNullOrWhiteSpace(ProcessedImageFileSize))
        {
            ClearProcessedImage();
        }
    }

    private void NotifyCropStateChanged()
    {
        OnPropertyChanged(nameof(CropLeft));
        OnPropertyChanged(nameof(CropTop));
        OnPropertyChanged(nameof(CropRight));
        OnPropertyChanged(nameof(CropBottom));
        OnPropertyChanged(nameof(CropRect));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionDimensions));
        if (LockAspectRatio)
        {
            SetTargetSize(
                PixelArtOutputSizeCalculator.ResolveNearest(TargetWidth, TargetHeight, GetCurrentAspectRatio()));
        }

        InvalidateProcessedPreview();
    }

    private PixelRect? TryGetNormalizedCropRect()
    {
        if (!HasImage || SelectedImage is null || _cropSelection.Width < 2 || _cropSelection.Height < 2)
        {
            return null;
        }

        return _cropSelection.ToPixelRect();
    }

    private void ResetCropToFullImage(int imageWidth, int imageHeight)
    {
        _cropSelection = ImageCropSelection.FullImage(imageWidth, imageHeight);
        NotifyCropStateChanged();
    }

    private void ResetCropToEmpty()
    {
        _cropSelection = ImageCropSelection.Empty;
        NotifyCropStateChanged();
    }
}