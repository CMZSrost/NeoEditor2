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
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.ImageTools.Helper;
using NeoEditor.Plugins.ImageTools.Services;

namespace NeoEditor.Plugins.ImageTools.ViewModels;

public partial class ImageEditorDocument : ImageToolDocumentBase
{
    private bool _isUpdatingAspectRatio;
    private readonly IImageEditorProcessingService _processingService;
    private readonly PixelArtConversionService _pixelArtService;
    private ImageCropSelection _cropSelection = ImageCropSelection.Empty;
    private const string OutputExtension = ".png";

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

    public bool HasImage => SelectedImage is not null;
    public bool HasNoImage => !HasImage;
    public bool HasProcessedImage => ProcessedImage is not null;
    public bool HasNoProcessedImage => !HasProcessedImage;
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
    public bool CanSaveProcessedImage => HasProcessedImage;

    public ImageEditorDocument(IImageEditorProcessingService processingService,
        PixelArtConversionService pixelArtService, ILocalizationService loc)
        : base(loc)
    {
        _processingService = processingService;
        _pixelArtService = pixelArtService;
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

    [RelayCommand]
    public async Task SaveProcessedImage()
    {
        if (!CanSaveProcessedImage)
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
            Title = Loc["SaveImagePair"],
            SuggestedFileName = string.IsNullOrWhiteSpace(NormalOutputName)
                ? GetSuggestedNormalFileName()
                : NormalOutputName,
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
            var request = CreateProcessingRequest();
            if (request is null)
            {
                return;
            }

            var pairPaths = TryCreateOutputPairPaths(file.TryGetLocalPath() ?? string.Empty);
            if (pairPaths is null)
            {
                return;
            }

            var result =
                await _processingService.SaveAsync(pairPaths.Value.NormalPath, pairPaths.Value.X2Path, request);
            if (result is null)
            {
                return;
            }

            NormalOutputName = pairPaths.Value.NormalFileName;
            NormalOutputPath = pairPaths.Value.NormalPath;
            ProcessedImageName = pairPaths.Value.X2FileName;
            ProcessedImagePath = pairPaths.Value.X2Path;
            ProcessedImageDimensions = FormatDimensions(result.X2Width, result.X2Height);

            if (!string.IsNullOrWhiteSpace(NormalOutputPath) && File.Exists(NormalOutputPath))
            {
                var normalFileInfo = new FileInfo(NormalOutputPath);
                NormalOutputFileSize = FormatFileSize(normalFileInfo.Length);
            }

            if (!string.IsNullOrWhiteSpace(ProcessedImagePath) && File.Exists(ProcessedImagePath))
            {
                var fileInfo = new FileInfo(ProcessedImagePath);
                ProcessedImageFileSize = FormatFileSize(fileInfo.Length);
            }

            NotifyProcessedStateChanged();
        }
        catch
        {
            // Ignore save failures and leave the current preview intact.
        }
    }

    private void LoadImage(string path)
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

    private ImageEditorProcessingRequest? CreateProcessingRequest()
    {
        if (!CanPixelate || string.IsNullOrWhiteSpace(ImagePath) || !File.Exists(ImagePath))
        {
            return null;
        }

        return new ImageEditorProcessingRequest(ImagePath, TargetWidth, TargetHeight, CropRect);
    }

    private (string NormalPath, string X2Path, string NormalFileName, string X2FileName)? TryCreateOutputPairPaths(
        string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(selectedPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var selectedFileName = Path.GetFileName(fullPath);
        var normalFileName = NormalizeNormalOutputFileName(selectedFileName);
        var x2FileName = GetSuggestedX2FileName(normalFileName);
        return (
            Path.Combine(directory, normalFileName),
            Path.Combine(directory, x2FileName),
            normalFileName,
            x2FileName);
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
        NotifyOutputStateChanged();
    }

    private void NotifyProcessedStateChanged()
    {
        OnPropertyChanged(nameof(HasProcessedImage));
        OnPropertyChanged(nameof(HasNoProcessedImage));
        OnPropertyChanged(nameof(CanSaveProcessedImage));
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
        OnPropertyChanged(nameof(NormalOutputDimensions));
        OnPropertyChanged(nameof(X2OutputDimensions));
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
