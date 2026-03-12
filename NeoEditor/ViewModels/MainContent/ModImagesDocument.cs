using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Model;
using NeoEditor.Helper;
using NeoEditor.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NeoEditor.ViewModels.MainContent;

public sealed class ModImagePairItem
{
    public string NormalImage { get; init; } = string.Empty;
    public string X2Image { get; init; } = string.Empty;
    public string DisplayName => $"{NormalImage}";
}

public partial class ModImagesDocument : DocumentViewBase
{
    private const string NormalPreviewTarget = "Normal";
    private const string X2PreviewTarget = "X2";

    private readonly IConfigService _config = App.ServiceProvider.GetRequiredService<IConfigService>();
    private readonly PhpParser _phpParser = new();
    private Bitmap? _selectedNormalImage;
    private Bitmap? _selectedX2Image;

    [ObservableProperty]
    public partial ModInfo? ModInfo { get; set; }

    [ObservableProperty]
    public partial ModImagePairItem? SelectedPair { get; set; }

    [ObservableProperty]
    public partial string SelectedNormalPath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedX2Path { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedNormalDimensions { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedX2Dimensions { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PreviewTarget { get; set; } = NormalPreviewTarget;

    public ObservableCollection<ModImagePairItem> ImagePairs { get; } = [];
    public bool HasImages => ImagePairs.Count > 0;
    public bool HasNoImages => !HasImages;
    public bool HasSelectedPair => SelectedPair is not null;
    public string SelectedNormalName => SelectedPair?.NormalImage ?? string.Empty;
    public string SelectedX2Name => SelectedPair?.X2Image ?? string.Empty;
    public Bitmap? PreviewImage => IsX2PreviewSelected ? SelectedX2Image : SelectedNormalImage;
    public string PreviewName => IsX2PreviewSelected ? SelectedX2Name : SelectedNormalName;
    public string PreviewPath => IsX2PreviewSelected ? SelectedX2Path : SelectedNormalPath;
    public string PreviewDimensions => IsX2PreviewSelected ? SelectedX2Dimensions : SelectedNormalDimensions;
    public bool HasPreviewImage => PreviewImage is not null;
    public bool IsNormalPreviewSelected => string.Equals(PreviewTarget, NormalPreviewTarget, StringComparison.Ordinal);
    public bool IsX2PreviewSelected => string.Equals(PreviewTarget, X2PreviewTarget, StringComparison.Ordinal);

    public Bitmap? SelectedNormalImage
    {
        get => _selectedNormalImage;
        private set
        {
            if (ReferenceEquals(_selectedNormalImage, value))
            {
                return;
            }

            _selectedNormalImage?.Dispose();
            SetProperty(ref _selectedNormalImage, value);
        }
    }

    public Bitmap? SelectedX2Image
    {
        get => _selectedX2Image;
        private set
        {
            if (ReferenceEquals(_selectedX2Image, value))
            {
                return;
            }

            _selectedX2Image?.Dispose();
            SetProperty(ref _selectedX2Image, value);
        }
    }

    public ModImagesDocument(ModInfo modInfo)
    {
        Update(modInfo);
    }

    public void Update(ModInfo modInfo)
    {
        ModInfo = modInfo;

        ImagePairs.Clear();
        foreach (var (normalImage, x2Image) in LoadImagePairs(modInfo))
        {
            ImagePairs.Add(new ModImagePairItem
            {
                NormalImage = normalImage,
                X2Image = x2Image,
            });
        }

        OnPropertyChanged(nameof(HasImages));
        OnPropertyChanged(nameof(HasNoImages));

        SelectedPair = ImagePairs.FirstOrDefault();
        if (SelectedPair is null)
        {
            UpdateSelectedImages(null);
        }
    }

    [RelayCommand]
    private void ShowPreview(string? target)
    {
        var normalizedTarget = NormalizePreviewTarget(target);
        if (string.Equals(PreviewTarget, normalizedTarget, StringComparison.Ordinal))
        {
            return;
        }

        PreviewTarget = normalizedTarget;
    }

    partial void OnSelectedPairChanged(ModImagePairItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedPair));
        OnPropertyChanged(nameof(SelectedNormalName));
        OnPropertyChanged(nameof(SelectedX2Name));
        UpdateSelectedImages(value);
    }

    partial void OnPreviewTargetChanged(string value)
    {
        _ = value;
        NotifyPreviewStateChanged();
    }

    private IReadOnlyList<(string NormalImage, string X2Image)> LoadImagePairs(ModInfo modInfo)
    {
        var getImagesPath = ResolveGetImagesPath(modInfo);
        if (string.IsNullOrWhiteSpace(getImagesPath) || !File.Exists(getImagesPath))
        {
            return [];
        }

        return _phpParser.ParseImagePairs(getImagesPath);
    }

    private string ResolveGetImagesPath(ModInfo modInfo)
    {
        var contentRoot = ResolveModContentRoot(modInfo);
        if (string.IsNullOrWhiteSpace(contentRoot))
        {
            return string.Empty;
        }

        return Path.Combine(contentRoot, "getimages.php");
    }

    private void UpdateSelectedImages(ModImagePairItem? pair)
    {
        SelectedNormalPath = ResolveImagePath(pair?.NormalImage);
        SelectedX2Path = ResolveImagePath(pair?.X2Image);
        SelectedNormalImage = LoadBitmap(SelectedNormalPath);
        SelectedX2Image = LoadBitmap(SelectedX2Path);
        SelectedNormalDimensions = FormatDimensions(SelectedNormalImage);
        SelectedX2Dimensions = FormatDimensions(SelectedX2Image);
        PreviewTarget = GetDefaultPreviewTarget();
        NotifyPreviewStateChanged();
    }

    private string ResolveImagePath(string? imagePath)
    {
        if (ModInfo is null || string.IsNullOrWhiteSpace(imagePath))
        {
            return string.Empty;
        }

        var imageDirectory = ResolveImageDirectory(ModInfo);
        if (string.IsNullOrWhiteSpace(imageDirectory))
        {
            return string.Empty;
        }

        var normalizedRelativePath = imagePath.Trim()
            .TrimStart('/', '\\')
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        return Path.GetFullPath(Path.Combine(imageDirectory, normalizedRelativePath));
    }

    private string ResolveModContentRoot(ModInfo modInfo)
    {
        if (string.IsNullOrWhiteSpace(_config.Config.GameRootDir))
        {
            return string.Empty;
        }

        if (IsGameMod(modInfo))
        {
            return Path.GetFullPath(_config.Config.GameRootDir);
        }

        if (string.IsNullOrWhiteSpace(modInfo.Path))
        {
            return string.Empty;
        }

        return Path.GetFullPath(Path.Combine(_config.Config.GameRootDir, modInfo.Path));
    }

    private string ResolveImageDirectory(ModInfo modInfo)
    {
        var contentRoot = ResolveModContentRoot(modInfo);
        return string.IsNullOrWhiteSpace(contentRoot)
            ? string.Empty
            : Path.Combine(contentRoot, "img");
    }

    private static bool IsGameMod(ModInfo modInfo)
    {
        return string.Equals(modInfo.Name, "Game", StringComparison.OrdinalIgnoreCase);
    }

    private static Bitmap? LoadBitmap(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return new Bitmap(path);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatDimensions(Bitmap? bitmap)
    {
        return bitmap is null
            ? string.Empty
            : $"{bitmap.PixelSize.Width} × {bitmap.PixelSize.Height}px";
    }

    private string GetDefaultPreviewTarget()
    {
        if (SelectedNormalImage is not null)
        {
            return NormalPreviewTarget;
        }

        return SelectedX2Image is not null ? X2PreviewTarget : NormalPreviewTarget;
    }

    private static string NormalizePreviewTarget(string? target)
    {
        return string.Equals(target, X2PreviewTarget, StringComparison.OrdinalIgnoreCase)
            ? X2PreviewTarget
            : NormalPreviewTarget;
    }

    private void NotifyPreviewStateChanged()
    {
        OnPropertyChanged(nameof(PreviewImage));
        OnPropertyChanged(nameof(PreviewName));
        OnPropertyChanged(nameof(PreviewPath));
        OnPropertyChanged(nameof(PreviewDimensions));
        OnPropertyChanged(nameof(HasPreviewImage));
        OnPropertyChanged(nameof(IsNormalPreviewSelected));
        OnPropertyChanged(nameof(IsX2PreviewSelected));
    }
}
