using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.Core.Abstractions;
using NeoEditor.Plugins.ImageTools.Helper;
using NeoEditor.Plugins.ImageTools.Services;

namespace NeoEditor.Plugins.ImageTools.ViewModels;

/// <summary>
/// One image slot in the editor. Owns its <see cref="Bitmap"/> (dispose-on-replace plus
/// <see cref="IDisposable"/> so closing the document releases the bitmap), the source
/// bytes for the pixelation pipeline, the crop selection (when crop-enabled), and a
/// save command wired by the parent document.
/// </summary>
public partial class ImageSlotViewModel : ObservableObject, IDisposable
{
    private readonly ILocalizationService _loc;
    private readonly IImageFileService _fileService;
    private readonly string _emptyHintKey;
    private Bitmap? _image;
    private byte[]? _sourceBytes;
    private ImageCropSelection _cropSelection = ImageCropSelection.Empty;
    private Func<Bitmap, string, Task>? _saveHandler;
    private string _titleKey;

    /// <summary>Raised after the slot's image is loaded or cleared (via <see cref="LoadFile"/>,
    /// <see cref="LoadBytes"/>, <see cref="ShowBitmap"/> or <see cref="Clear"/>).</summary>
    public event EventHandler? ImageChanged;

    /// <summary>Raised after a crop change is committed (via <see cref="SetCropBounds"/>).</summary>
    public event EventHandler? CropChanged;

    public ImageSlotViewModel(ILocalizationService loc, IImageFileService fileService,
        bool isCropEnabled = false, string titleKey = "", string emptyHintKey = "")
    {
        _loc = loc;
        _fileService = fileService;
        IsCropEnabled = isCropEnabled;
        _titleKey = titleKey;
        _emptyHintKey = emptyHintKey;
    }

    public ILocalizationService Loc => _loc;

    /// <summary>Only the source slot enables the crop overlay and interaction.</summary>
    public bool IsCropEnabled { get; }

    /// <summary>Localization key for the slot title; the image dimensions are appended
    /// automatically when the slot is populated.</summary>
    public string TitleKey
    {
        get => _titleKey;
        set
        {
            if (_titleKey == value)
            {
                return;
            }

            _titleKey = value;
            OnPropertyChanged(nameof(Title));
        }
    }

    public string EmptyHint => _loc[_emptyHintKey];

    public string Title => HasImage && Image is { } image
        ? $"{_loc[_titleKey]} ({image.PixelSize.Width} × {image.PixelSize.Height}px)"
        : _loc[_titleKey];

    public Bitmap? Image
    {
        get => _image;
        private set
        {
            if (ReferenceEquals(_image, value))
            {
                return;
            }

            _image?.Dispose();
            _image = value;
            OnPropertyChanged(nameof(Image));
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(HasNoImage));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(SelectionDimensions));
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Raw source bytes (PNG) of the slot image — the pixelation pipeline reads
    /// straight from these (ImageSharp is pure managed and headless-testable) instead of
    /// re-decoding the display bitmap.</summary>
    public byte[]? SourceBytes => _sourceBytes;

    /// <summary>File path when the slot was loaded from disk (empty for generated/bytes).</summary>
    public string FilePath { get; private set; } = string.Empty;

    /// <summary>Display name of the slot image (file name or generated name).</summary>
    public string ImageName { get; private set; } = string.Empty;

    public bool HasImage => Image is not null;
    public bool HasNoImage => !HasImage;

    // ── Overlay hint (e.g. stale-result marker on the result slot) ──
    public string OverlayText { get; private set; } = string.Empty;
    public bool HasOverlay => !string.IsNullOrWhiteSpace(OverlayText);

    // ── Crop (crop-enabled slot only) ──
    public int CropLeft => _cropSelection.Left;
    public int CropTop => _cropSelection.Top;
    public int CropRight => _cropSelection.Right;
    public int CropBottom => _cropSelection.Bottom;
    public PixelRect? CropRect => TryGetNormalizedCropRect();

    public bool HasSelection => IsCropEnabled && HasImage && CropRect is { } crop && Image is { } image &&
                                (crop.X != 0 || crop.Y != 0 || crop.Width != image.PixelSize.Width ||
                                 crop.Height != image.PixelSize.Height);

    public string SelectionDimensions => HasImage && CropRect is { } selection
        ? $"{selection.Width} × {selection.Height}px"
        : string.Empty;

    public bool CanSave => HasImage;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (Image is null || _saveHandler is null)
        {
            return;
        }

        await _saveHandler(Image, _fileService.GetSuggestedFileName(ImageName));
    }

    /// <summary>Wire the actual save action (the parent document decides where files go).</summary>
    public void SetSaveHandler(Func<Bitmap, string, Task> saveHandler)
    {
        _saveHandler = saveHandler;
    }

    /// <summary>Load an image file into the slot (also stores its path).</summary>
    public void LoadFile(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            Clear();
            return;
        }

        try
        {
            Image = _fileService.FromFile(fullPath);
            FilePath = fullPath;
            ImageName = Path.GetFileName(fullPath);
            _sourceBytes = null;
            ResetCropToFullImage(Image.PixelSize.Width, Image.PixelSize.Height);
            ImageChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            Clear();
        }
    }

    /// <summary>Load PNG bytes into the slot (AI-generated candidates; no file path).</summary>
    public void LoadBytes(byte[] pngBytes, string name)
    {
        try
        {
            Image = _fileService.FromBytes(pngBytes);
            FilePath = string.Empty;
            ImageName = name;
            _sourceBytes = pngBytes;
            ResetCropToFullImage(Image.PixelSize.Width, Image.PixelSize.Height);
            ImageChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            Clear();
        }
    }

    /// <summary>Show a processing result (the slot does not keep source bytes for it).</summary>
    public void ShowBitmap(Bitmap bitmap)
    {
        Image = bitmap;
        FilePath = string.Empty;
        ImageName = string.Empty;
        _sourceBytes = null;
        ResetCropToEmpty();
        ImageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        Image = null;
        _sourceBytes = null;
        FilePath = string.Empty;
        ImageName = string.Empty;
        ResetCropToEmpty();
        ImageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetOverlay(string text)
    {
        OverlayText = text;
        OnPropertyChanged(nameof(OverlayText));
        OnPropertyChanged(nameof(HasOverlay));
    }

    public void SetCropBounds(int left, int top, int right, int bottom)
    {
        if (!IsCropEnabled || !HasImage || Image is null)
        {
            ResetCropToEmpty();
            return;
        }

        var normalizedCrop = ImageCropSelection.Normalize(left, top, right, bottom, Image.PixelSize.Width,
            Image.PixelSize.Height, minimumSize: 2);
        if (normalizedCrop is null)
        {
            return;
        }

        UpdateCropSelection(normalizedCrop.Value);
        CropChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetCropRect(PixelRect? cropRect)
    {
        if (cropRect is null)
        {
            return;
        }

        SetCropBounds(cropRect.Value.X, cropRect.Value.Y, cropRect.Value.Right, cropRect.Value.Bottom);
    }

    public void Dispose()
    {
        _image?.Dispose();
        _image = null;
        _sourceBytes = null;
    }

    private void UpdateCropSelection(ImageCropSelection cropSelection)
    {
        if (_cropSelection == cropSelection)
        {
            return;
        }

        _cropSelection = cropSelection;
        OnPropertyChanged(nameof(CropLeft));
        OnPropertyChanged(nameof(CropTop));
        OnPropertyChanged(nameof(CropRight));
        OnPropertyChanged(nameof(CropBottom));
        OnPropertyChanged(nameof(CropRect));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionDimensions));
    }

    private PixelRect? TryGetNormalizedCropRect()
    {
        if (!HasImage || Image is null || _cropSelection.Width < 2 || _cropSelection.Height < 2)
        {
            return null;
        }

        return _cropSelection.ToPixelRect();
    }

    private void ResetCropToFullImage(int imageWidth, int imageHeight)
    {
        _cropSelection = ImageCropSelection.FullImage(imageWidth, imageHeight);
        OnPropertyChanged(nameof(CropRect));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionDimensions));
    }

    private void ResetCropToEmpty()
    {
        _cropSelection = ImageCropSelection.Empty;
        OnPropertyChanged(nameof(CropRect));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionDimensions));
    }
}
