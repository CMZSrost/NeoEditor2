using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.ImageTools.Helper;
using NeoEditor.Plugins.ImageTools.Services;

namespace NeoEditor.Plugins.ImageTools.ViewModels;

public sealed class ModImagePairItem
{
    public string NormalImage { get; init; } = string.Empty;
    public string X2Image { get; init; } = string.Empty;
    public string DisplayName => $"{NormalImage}";
}

public sealed class PendingImagePairImport
{
    public required string BaseName { get; init; }
    public string? NormalSourcePath { get; set; }
    public string? X2SourcePath { get; set; }
    public string NormalFileName => Path.GetFileName(NormalSourcePath ?? BaseName);
    public string X2FileName => Path.GetFileName(X2SourcePath ?? $"x2_{BaseName}");
}

public partial class ModImagesDocument : ImageToolDocumentBase
{
    private const string NormalPreviewTarget = "Normal";
    private const string X2PreviewTarget = "X2";

    private readonly IConfigService _config;
    private readonly IModImageListService _imageListService;
    private readonly INotificationService _notification;
    private Bitmap? _selectedNormalImage;
    private Bitmap? _selectedX2Image;

    public ModImagePairDropHandler ImagePairDropHandler { get; }

    public ModImagesDocument(ModInfo modInfo, IConfigService config, IModImageListService imageListService,
        ModImagePairDropHandler dropHandler, INotificationService notificationService,
        ILocalizationService loc)
        : base(loc)
    {
        _notification = notificationService;
        _config = config;
        _imageListService = imageListService;
        ImagePairDropHandler = dropHandler;
        Update(modInfo);
    }

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

    [ObservableProperty]
    public partial int ScrollToSelectedPairRequestId { get; set; }

    [ObservableProperty]
    public partial int HighlightSelectedPairRequestId { get; set; }

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

    public void Update(ModInfo modInfo)
    {
        ModInfo = modInfo;

        var imagePairs = LoadImagePairs(modInfo)
            .Select(static pair => new ModImagePairItem
            {
                NormalImage = pair.NormalImage,
                X2Image = pair.X2Image,
            })
            .ToList();

        ImagePairs.Clear();
        foreach (var pair in ApplySavedImageOrder(modInfo, imagePairs))
        {
            ImagePairs.Add(pair);
        }

        OnPropertyChanged(nameof(HasImages));
        OnPropertyChanged(nameof(HasNoImages));

        SelectedPair = ImagePairs.FirstOrDefault();
        if (SelectedPair is null)
        {
            UpdateSelectedImages(null);
        }

        NeedNotifyWhenClose = false;
    }

    [RelayCommand]
    private async Task Save()
    {
        await SaveCoreAsync(notifySuccess: true);
    }

    [RelayCommand]
    private async Task ImportImages()
    {
        var storageProvider = GetStorageProvider();
        if (storageProvider is null || ModInfo is null)
        {
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Loc["SelectImage"],
            AllowMultiple = true,
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

        var selectedPaths = files
            .Select(file => file.TryGetLocalPath())
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedPaths.Count == 0)
        {
            return;
        }

        var imageDirectory = ResolveImageDirectory(ModInfo);
        if (string.IsNullOrWhiteSpace(imageDirectory))
        {
            _notification.ShowWarning(Loc["GetImagesFileNotFoundMessage"], Loc["GetImagesFileNotFound"]);
            return;
        }

        Directory.CreateDirectory(imageDirectory);

        var pendingPairs = BuildPendingImports(selectedPaths, out var skippedFiles);
        if (pendingPairs.Count == 0)
        {
            NotifyImportSkipSummary(skippedFiles);
            return;
        }

        var overwrittenFiles = new List<string>();
        var importedItems = new List<ModImagePairItem>();

        foreach (var pendingPair in pendingPairs)
        {
            try
            {
                var copiedPair = TryCopyImportedPair(pendingPair, imageDirectory, overwrittenFiles, out var skipReason);
                if (copiedPair is null)
                {
                    if (!string.IsNullOrWhiteSpace(skipReason))
                    {
                        skippedFiles.Add(skipReason);
                    }

                    continue;
                }

                importedItems.Add(UpsertImportedPair(copiedPair));
            }
            catch (Exception ex)
            {
                skippedFiles.Add($"{pendingPair.BaseName} ({ex.Message})");
            }
        }

        if (overwrittenFiles.Count > 0)
        {
            _notification.ShowInfo(
                Loc["ImportImagesOverwriteMessage", string.Join(", ", overwrittenFiles.Distinct(StringComparer.OrdinalIgnoreCase))],
                Loc["ImportImagesOverwrite"]);
        }

        if (importedItems.Count == 0)
        {
            NotifyImportSkipSummary(skippedFiles);
            return;
        }

        var importedSelection = importedItems.Last();
        var selectionUnchanged = ReferenceEquals(SelectedPair, importedSelection);
        SelectedPair = importedSelection;
        if (selectionUnchanged)
        {
            UpdateSelectedImages(importedSelection);
        }
        NotifyImagePairCollectionStateChanged();
        NeedNotifyWhenClose = true;

        if (await SaveCoreAsync(notifySuccess: false))
        {
            _notification.ShowSuccess(Loc["ImportImagesSuccessMessage", importedItems.Count, imageDirectory],
                Loc["ImportImagesSuccess"]);
        }

        NotifyImportSkipSummary(skippedFiles);
    }

    private bool CanDeleteImagePair(ModImagePairItem? item)
    {
        return (item ?? SelectedPair) is not null;
    }

    [RelayCommand(CanExecute = nameof(CanDeleteImagePair))]
    private void DeleteImagePair(ModImagePairItem? item)
    {
        var targetPair = item ?? SelectedPair;
        if (targetPair is null)
        {
            return;
        }

        SelectedPair = targetPair;

        var currentIndex = ImagePairs.IndexOf(targetPair);
        if (currentIndex < 0)
        {
            return;
        }

        ImagePairs.RemoveAt(currentIndex);
        SelectedPair = ImagePairs.Count == 0
            ? null
            : ImagePairs[Math.Clamp(currentIndex, 0, ImagePairs.Count - 1)];

        NotifyImagePairCollectionStateChanged();
        NeedNotifyWhenClose = true;
    }

    private bool CanRenameImagePair(ModImagePairItem? item)
    {
        var targetPair = item ?? SelectedPair;
        return targetPair is { NormalImage.Length: > 0, X2Image.Length: > 0 };
    }

    [RelayCommand(CanExecute = nameof(CanRenameImagePair))]
    private async Task RenameImagePair(ModImagePairItem? item)
    {
        var targetPair = item ?? SelectedPair;
        if (targetPair is null || ModInfo is null)
        {
            return;
        }

        SelectedPair = targetPair;

        var imageDirectory = ResolveImageDirectory(ModInfo);
        if (string.IsNullOrWhiteSpace(imageDirectory))
        {
            _notification.ShowWarning(Loc["GetImagesFileNotFoundMessage"], Loc["GetImagesFileNotFound"]);
            return;
        }

        var currentNormalPath = ResolveImagePath(targetPair.NormalImage);
        var currentX2Path = ResolveImagePath(targetPair.X2Image);
        if (string.IsNullOrWhiteSpace(currentNormalPath) || string.IsNullOrWhiteSpace(currentX2Path) ||
            !File.Exists(currentNormalPath) || !File.Exists(currentX2Path))
        {
            _notification.ShowWarning(Loc["RenameImagePairMissingFilesMessage"], Loc["RenameImagePairMissingFiles"]);
            return;
        }

        var result = await _imageListService.RequestRenameAsync(
            imageDirectory, currentNormalPath, currentX2Path);
        if (result is null)
        {
            return;
        }

        try
        {
            if (!TryRenameImagePairFiles(targetPair, result.Value.NormalFileName, result.Value.X2FileName,
                    out var renamedPair))
            {
                _notification.ShowWarning(Loc["RenameImagePairFileExists"], Loc["RenameImagePairFailed"]);
                return;
            }

            SelectedPair = renamedPair;
            NeedNotifyWhenClose = true;
            RenameImagePairCommand.NotifyCanExecuteChanged();

            if (await SaveCoreAsync(notifySuccess: false))
            {
                RequestSelectionAttention();
                _notification.ShowSuccess(Loc["RenameImagePairSuccessMessage", renamedPair.NormalImage],
                    Loc["RenameImagePairSuccess"]);
            }
        }
        catch (Exception ex)
        {
            _notification.ShowError(Loc["RenameImagePairFailedMessage", ex.Message], Loc["RenameImagePairFailed"]);
        }
    }

    private bool CanMoveUp()
    {
        return SelectedPair is not null && ImagePairs.IndexOf(SelectedPair) > 0;
    }

    private bool CanMoveDown()
    {
        return SelectedPair is not null && ImagePairs.IndexOf(SelectedPair) >= 0 &&
               ImagePairs.IndexOf(SelectedPair) < ImagePairs.Count - 1;
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        if (SelectedPair is null)
        {
            return;
        }

        MoveImagePair(SelectedPair, ImagePairs.IndexOf(SelectedPair) - 1);
    }

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (SelectedPair is null)
        {
            return;
        }

        MoveImagePair(SelectedPair, ImagePairs.IndexOf(SelectedPair) + 1);
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
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        RenameImagePairCommand.NotifyCanExecuteChanged();
        DeleteImagePairCommand.NotifyCanExecuteChanged();
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

        return _imageListService.ParseImagePairs(getImagesPath);
    }

    public void OnImagePairsReordered(ModImagePairItem movedItem)
    {
        SelectedPair = movedItem;
        NeedNotifyWhenClose = true;
    }

    public bool MoveImagePair(ModImagePairItem item, int targetIndex)
    {
        var sourceIndex = ImagePairs.IndexOf(item);
        if (sourceIndex < 0 || targetIndex < 0 || targetIndex >= ImagePairs.Count || sourceIndex == targetIndex)
        {
            return false;
        }

        ImagePairs.Move(sourceIndex, targetIndex);
        SelectedPair = item;
        NeedNotifyWhenClose = true;
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        return true;
    }

    private void NotifyImagePairCollectionStateChanged()
    {
        OnPropertyChanged(nameof(HasImages));
        OnPropertyChanged(nameof(HasNoImages));
        DeleteImagePairCommand.NotifyCanExecuteChanged();
    }

    private async Task<bool> SaveCoreAsync(bool notifySuccess)
    {
        if (ModInfo is null)
        {
            _notification.ShowWarning(Loc["GetImagesFileNotFoundMessage"], Loc["GetImagesFileNotFound"]);
            return false;
        }

        var getImagesPath = ResolveGetImagesPath(ModInfo);
        if (string.IsNullOrWhiteSpace(getImagesPath))
        {
            _notification.ShowWarning(Loc["GetImagesFileNotFoundMessage"], Loc["GetImagesFileNotFound"]);
            return false;
        }

        try
        {
            var imagePairs = ImagePairs
                .Select(static pair => (pair.NormalImage, pair.X2Image))
                .ToList();

            var phpContent = _imageListService.GenerateImagePhp(imagePairs);
            await File.WriteAllTextAsync(getImagesPath, phpContent);

            SaveImageOrderToConfig();
            await _config.SaveAsync();

            NeedNotifyWhenClose = false;
            if (notifySuccess)
            {
                _notification.ShowSuccess(Loc["SaveModImagesSuccessMessage", getImagesPath], Loc["SaveModImagesSuccess"]);
            }

            return true;
        }
        catch (Exception ex)
        {
            _notification.ShowError(Loc["SaveModImagesFailedMessage", ex.Message], Loc["SaveModImagesFailed"]);
            return false;
        }
    }

    private static List<PendingImagePairImport> BuildPendingImports(IReadOnlyList<string> selectedPaths,
        out List<string> skippedFiles)
    {
        var pairs = new Dictionary<string, PendingImagePairImport>(StringComparer.OrdinalIgnoreCase);
        skippedFiles = new List<string>();

        foreach (var path in selectedPaths)
        {
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var isX2 = fileName.StartsWith("x2_", StringComparison.OrdinalIgnoreCase);
            var baseName = isX2 ? fileName[3..] : fileName;
            if (!pairs.TryGetValue(baseName, out var pair))
            {
                pair = new PendingImagePairImport { BaseName = baseName };
                pairs[baseName] = pair;
            }

            if (isX2)
            {
                pair.X2SourcePath = path;
            }
            else
            {
                pair.NormalSourcePath = path;
            }
        }

        var validPairs = new List<PendingImagePairImport>();
        foreach (var pair in pairs.Values)
        {
            if (!string.IsNullOrWhiteSpace(pair.NormalSourcePath) && !string.IsNullOrWhiteSpace(pair.X2SourcePath))
            {
                validPairs.Add(pair);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(pair.NormalSourcePath))
            {
                skippedFiles.Add($"{Path.GetFileName(pair.NormalSourcePath)} → missing x2_{pair.BaseName}");
            }
            else if (!string.IsNullOrWhiteSpace(pair.X2SourcePath))
            {
                skippedFiles.Add($"{Path.GetFileName(pair.X2SourcePath)} → missing {pair.BaseName}");
            }
        }

        return validPairs;
    }

    private static ModImagePairItem? TryCopyImportedPair(PendingImagePairImport pair, string imageDirectory,
        ICollection<string> overwrittenFiles, out string? skipReason)
    {
        skipReason = null;
        if (string.IsNullOrWhiteSpace(pair.NormalSourcePath) || string.IsNullOrWhiteSpace(pair.X2SourcePath))
        {
            return null;
        }

        var normalFileName = Path.GetFileName(pair.NormalSourcePath);
        var x2FileName = Path.GetFileName(pair.X2SourcePath);
        var normalTargetPath = Path.Combine(imageDirectory, normalFileName);
        var x2TargetPath = Path.Combine(imageDirectory, x2FileName);

        if (string.Equals(Path.GetFullPath(pair.NormalSourcePath), Path.GetFullPath(normalTargetPath),
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetFullPath(pair.X2SourcePath), Path.GetFullPath(x2TargetPath),
                StringComparison.OrdinalIgnoreCase))
        {
            skipReason = $"{normalFileName} / {x2FileName}";
            return null;
        }

        if (File.Exists(normalTargetPath))
        {
            overwrittenFiles.Add(normalFileName);
        }

        if (File.Exists(x2TargetPath))
        {
            overwrittenFiles.Add(x2FileName);
        }

        File.Copy(pair.NormalSourcePath, normalTargetPath, overwrite: true);
        File.Copy(pair.X2SourcePath, x2TargetPath, overwrite: true);

        return new ModImagePairItem
        {
            NormalImage = normalFileName,
            X2Image = x2FileName,
        };
    }

    private ModImagePairItem UpsertImportedPair(ModImagePairItem importedPair)
    {
        var existingPair = ImagePairs.FirstOrDefault(pair =>
            string.Equals(pair.NormalImage, importedPair.NormalImage, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(pair.X2Image, importedPair.X2Image, StringComparison.OrdinalIgnoreCase));

        if (existingPair is not null)
        {
            return existingPair;
        }

        ImagePairs.Add(importedPair);
        return importedPair;
    }

    private bool TryRenameImagePairFiles(ModImagePairItem currentPair, string newNormalFileName, string newX2FileName,
        out ModImagePairItem renamedPair)
    {
        renamedPair = currentPair;

        var currentNormalPath = ResolveImagePath(currentPair.NormalImage);
        var currentX2Path = ResolveImagePath(currentPair.X2Image);
        if (string.IsNullOrWhiteSpace(currentNormalPath) || string.IsNullOrWhiteSpace(currentX2Path) ||
            !File.Exists(currentNormalPath) || !File.Exists(currentX2Path))
        {
            return false;
        }

        var imageDirectory = Path.GetDirectoryName(currentNormalPath);
        if (string.IsNullOrWhiteSpace(imageDirectory))
        {
            return false;
        }

        var targetNormalPath = Path.Combine(imageDirectory, newNormalFileName);
        var targetX2Path = Path.Combine(imageDirectory, newX2FileName);
        if (HasConflictingRenameTarget(targetNormalPath, currentNormalPath) ||
            HasConflictingRenameTarget(targetX2Path, currentX2Path))
        {
            return false;
        }

        if (!string.Equals(currentNormalPath, targetNormalPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(currentNormalPath, targetNormalPath);
        }

        if (!string.Equals(currentX2Path, targetX2Path, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(currentX2Path, targetX2Path);
        }

        var currentIndex = ImagePairs.IndexOf(currentPair);
        renamedPair = new ModImagePairItem
        {
            NormalImage = newNormalFileName,
            X2Image = newX2FileName,
        };

        if (currentIndex >= 0)
        {
            ImagePairs[currentIndex] = renamedPair;
        }

        UpdateSelectedImages(renamedPair);
        return true;
    }

    private static bool HasConflictingRenameTarget(string targetPath, string currentPath)
    {
        var normalizedTarget = Path.GetFullPath(targetPath);
        var normalizedCurrent = string.IsNullOrWhiteSpace(currentPath) ? string.Empty : Path.GetFullPath(currentPath);
        return !string.Equals(normalizedTarget, normalizedCurrent, StringComparison.OrdinalIgnoreCase) &&
               File.Exists(normalizedTarget);
    }

    private void RequestSelectionAttention()
    {
        ScrollToSelectedPairRequestId++;
        HighlightSelectedPairRequestId++;
    }

    private void NotifyImportSkipSummary(IReadOnlyList<string> skippedFiles)
    {
        if (skippedFiles.Count == 0)
        {
            return;
        }

        var preview = string.Join("; ", skippedFiles.Take(5));
        if (skippedFiles.Count > 5)
        {
            preview = $"{preview}; +{skippedFiles.Count - 5}";
        }

        _notification.ShowWarning(Loc["ImportImagesSkippedMessage", preview], Loc["ImportImagesSkipped"]);
    }

    private static IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
        {
            return null;
        }

        return TopLevel.GetTopLevel(mainWindow)?.StorageProvider;
    }

    private IReadOnlyList<ModImagePairItem> ApplySavedImageOrder(ModInfo modInfo,
        IReadOnlyList<ModImagePairItem> imagePairs)
    {
        if (imagePairs.Count <= 1)
        {
            return imagePairs;
        }

        var configKey = GetImageOrderConfigKey(modInfo);
        if (string.IsNullOrWhiteSpace(configKey) ||
            !_config.Config.ModImageOrders.TryGetValue(configKey, out var savedOrder) ||
            savedOrder is not { Count: > 0 })
        {
            return imagePairs;
        }

        var remainingByKey = imagePairs
            .GroupBy(GetImagePairOrderKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => new Queue<ModImagePairItem>(group),
                StringComparer.OrdinalIgnoreCase);

        var ordered = new List<ModImagePairItem>(imagePairs.Count);
        foreach (var key in savedOrder)
        {
            if (string.IsNullOrWhiteSpace(key) ||
                !remainingByKey.TryGetValue(key, out var queue) ||
                queue.Count == 0)
            {
                continue;
            }

            ordered.Add(queue.Dequeue());
        }

        foreach (var pair in imagePairs)
        {
            var key = GetImagePairOrderKey(pair);
            if (remainingByKey.TryGetValue(key, out var queue) && queue.Count > 0 && ReferenceEquals(queue.Peek(), pair))
            {
                ordered.Add(queue.Dequeue());
            }
        }

        return ordered;
    }

    private void SaveImageOrderToConfig()
    {
        var configKey = GetImageOrderConfigKey(ModInfo);
        if (string.IsNullOrWhiteSpace(configKey))
        {
            return;
        }

        var order = ImagePairs
            .Select(GetImagePairOrderKey)
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .ToList();

        if (order.Count == 0)
        {
            _config.Config.ModImageOrders.Remove(configKey);
            return;
        }

        _config.Config.ModImageOrders[configKey] = order;
    }

    private string GetImageOrderConfigKey(ModInfo? modInfo)
    {
        if (modInfo is null)
        {
            return string.Empty;
        }

        var getImagesPath = ResolveGetImagesPath(modInfo);
        if (!string.IsNullOrWhiteSpace(getImagesPath))
        {
            return NormalizeConfigPath(getImagesPath);
        }

        if (!string.IsNullOrWhiteSpace(modInfo.Path))
        {
            return NormalizeConfigPath(modInfo.Path);
        }

        return $"name:{modInfo.Name}";
    }

    private static string GetImagePairOrderKey(ModImagePairItem pair)
    {
        return $"{NormalizeOrderComponent(pair.NormalImage)}|{NormalizeOrderComponent(pair.X2Image)}";
    }

    private static string NormalizeOrderComponent(string? value)
    {
        return value?.Trim().Replace('\\', '/').ToLowerInvariant() ?? string.Empty;
    }

    private static string NormalizeConfigPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }
        catch
        {
            return path.Trim().Replace('\\', '/');
        }
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
