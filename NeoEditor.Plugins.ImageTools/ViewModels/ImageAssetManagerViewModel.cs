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
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Messages;
using NeoEditor.Plugins.ImageTools.Services;

namespace NeoEditor.Plugins.ImageTools.ViewModels;

/// <summary>
/// ViewModel for the Image Browser Tool Dock (R27: ImageAssetManager split into
/// Browser + Orchestration).
/// Presents a file-system tree of actual image files across the base game and
/// loaded mods, with search/filter, preview, and open-in-editor support.
/// Only scans real files under img/ directories — never parses getimages.php
/// (declaration order / pairing is Image Orchestration's job).
/// </summary>
public partial class ImageAssetManagerViewModel : ImageToolObservableObject
{
    private readonly IMessenger _messenger;
    private readonly IProfileModSourceProvider _sourceProvider;
    private readonly IImageGenerationService _imageGenerationService;

    // Serializes refresh calls so concurrent triggers (auto-load, message nudge,
    // explicit button) never run two tree rebuilds that clobber each other.
    private readonly object _refreshGate = new();
    private Task _refreshChain = Task.CompletedTask;

    // ── Backing store for tree filtering ──
    private List<ModImageTreeNode> _allModNodes = [];

    [ObservableProperty] public partial ObservableCollection<ModImageTreeNode> ModNodes { get; set; } = [];

    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty] public partial ModImageTreeNode? SelectedNode { get; set; }

    [ObservableProperty] public partial ImageAssetInfo? SelectedImage { get; set; }

    [ObservableProperty] public partial bool IsLoading { get; set; }

    public ImageAssetManagerViewModel(
        ILocalizationService loc,
        IProfileModSourceProvider sourceProvider,
        IImageGenerationService imageGenerationService,
        IMessenger messenger)
        : base(loc)
    {
        _messenger = messenger;
        _sourceProvider = sourceProvider;
        _imageGenerationService = imageGenerationService;

        // 议题6: auto-load + refresh on workspace lifecycle changes (game root,
        // profile load, mod create/delete). Refresh is best-effort.
        // OpenMergeEditorMessage covers loads triggered from the Profile Tool.
        _messenger.Register<GameRootDirChangedMessage>(this, (_, _) => _ = RefreshAsync());
        _messenger.Register<LoadProfileMessage>(this, (_, _) => _ = RefreshAsync());
        _messenger.Register<OpenMergeEditorMessage>(this, (_, _) => _ = RefreshAsync());
        _messenger.Register<RefreshModMessage>(this, (_, _) => _ = RefreshAsync());
        _ = RefreshAsync();
    }

    /// <summary>
    /// Called when SelectedNode changes. Updates the preview pane.
    /// </summary>
    partial void OnSelectedNodeChanged(ModImageTreeNode? value)
    {
        AddImageCommand.NotifyCanExecuteChanged();

        if (value?.IsImage == true && !string.IsNullOrWhiteSpace(value.FullImagePath))
        {
            try
            {
                var bitmap = new Bitmap(value.FullImagePath);
                SelectedImage = new ImageAssetInfo
                {
                    FileName = value.Name,
                    FullPath = value.FullImagePath,
                    X2Path = value.X2ImagePath,
                    ModName = value.ModPath ?? "Base Game",
                    Thumbnail = bitmap,
                    Dimensions = $"{bitmap.PixelSize.Width} x {bitmap.PixelSize.Height}"
                };
            }
            catch
            {
                SelectedImage = new ImageAssetInfo
                {
                    FileName = value.Name,
                    FullPath = value.FullImagePath,
                    X2Path = value.X2ImagePath,
                    ModName = value.ModPath ?? "Base Game",
                    Thumbnail = null,
                    Dimensions = "(load error)"
                };
            }
        }
        else
        {
            SelectedImage = null;
        }
    }

    /// <summary>
    /// Called when SearchText changes. Re-filters the visible tree.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        _ = value;
        ApplyFilter();
    }

    [RelayCommand]
    private Task RefreshAsync()
    {
        lock (_refreshGate)
        {
            _refreshChain = RefreshAfterAsync(_refreshChain);
            return _refreshChain;
        }
    }

    private async Task RefreshAfterAsync(Task previous)
    {
        await previous;
        await RefreshCoreAsync();
    }

    private async Task RefreshCoreAsync()
    {
        IsLoading = true;
        try
        {
            // Snapshot the active profile's content roots on the UI thread (message
            // dispatch has completed, so ModLoadInfos are populated) before building
            // the tree on the background thread.
            var roots = _sourceProvider.GetContentRoots();
            var nodes = await Task.Run(() => BuildTree(roots));
            _allModNodes = nodes;
            ApplyFilter();
        }
        catch
        {
            // Refresh is best-effort (auto-load path); a failed scan must not crash the tool.
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenImage()
    {
        if (SelectedNode?.IsImage != true || string.IsNullOrWhiteSpace(SelectedNode.FullImagePath))
            return;

        var title = Path.GetFileName(SelectedNode.FullImagePath);
        _messenger.Send(new OpenImageDocumentMessage(title, SelectedNode.FullImagePath));
    }

    /// <summary>Localized context-menu header for the Add-Image command.</summary>
    public string AddImageHeader => Loc["AddImage"];

    private bool CanAddImage() => SelectedNode is { IsMod: true, IsGame: false };

    /// <summary>
    /// Context-menu "Add Image" on a mod directory: pick image files, copy them into the
    /// mod's <c>img/</c> directory, refresh the tree, then open an editor tab per added
    /// image. Base game is read-only — never copy into game install files.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddImage))]
    private async Task AddImageAsync()
    {
        if (SelectedNode is not { IsMod: true, IsGame: false } || SelectedNode.ModPath is not { } modPath)
            return;

        var storageProvider = GetStorageProvider();
        if (storageProvider is null)
            return;

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
            return;

        var paths = files
            .Select(file => file.TryGetLocalPath())
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (paths.Count == 0)
            return;

        var imgDir = Path.Combine(modPath, "img");
        Directory.CreateDirectory(imgDir);

        var added = new List<string>();
        foreach (var path in paths)
        {
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            var target = Path.Combine(imgDir, fileName);
            try
            {
                if (!File.Exists(target) && !PathsEqual(path, target))
                    File.Copy(path, target);
                added.Add(target);
            }
            catch
            {
                // Copy is best-effort; a failed copy is surfaced by the tree refresh below.
            }
        }

        if (added.Count == 0)
            return;

        // Refresh so the newly copied files appear in the tree.
        await RefreshAsync();

        // Open the image editor tab for each added image (dedup by path in the shell).
        foreach (var addedPath in added)
            _messenger.Send(new OpenImageDocumentMessage(Path.GetFileName(addedPath), addedPath));
    }

    /// <summary>Localized context-menu header for the AI generate action.</summary>
    public string AiGenerateHeader => Loc["AiGenerate"];

    private bool CanGenerateImage() => _imageGenerationService.IsAvailable;

    /// <summary>
    /// Context-menu "AI 生成图片": opens a blank Image Editor workbench whose AI panel
    /// takes a prompt and generates pixel art. Disabled when the AI API is not configured.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanGenerateImage))]
    private void GenerateImage()
    {
        _messenger.Send(new OpenAiImageWorkbenchMessage());
    }

    private static IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } mainWindow
            })
        {
            return null;
        }

        return TopLevel.GetTopLevel(mainWindow)?.StorageProvider;
    }

    private static bool PathsEqual(string a, string b)
    {
        return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyFilter()
    {
        ModNodes.Clear();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            foreach (var node in _allModNodes)
                ModNodes.Add(node);
            return;
        }

        var filter = SearchText.Trim();
        foreach (var modNode in _allModNodes)
        {
            // If the mod name matches, show all children
            if (modNode.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                ModNodes.Add(modNode);
                continue;
            }

            // Otherwise, copy and filter children
            var matchingChildren = modNode.Children
                .Where(c => c.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingChildren.Count > 0)
            {
                ModNodes.Add(new ModImageTreeNode
                {
                    Name = modNode.Name,
                    ModPath = modNode.ModPath,
                    IsMod = true,
                    IsGame = modNode.IsGame,
                    Children = new ObservableCollection<ModImageTreeNode>(matchingChildren)
                });
            }
        }
    }

    /// <summary>
    /// Builds the file-system tree from the active profile's content roots (task 4).
    /// Base game = scan <c>gameRoot/img/</c>; mods = scan <c>contentRoot/img/</c> where
    /// contentRoot comes from the profile's ModLoadInfos. Never reads getimages.php (R27).
    /// </summary>
    private List<ModImageTreeNode> BuildTree(IReadOnlyList<ModContentRoot> roots)
    {
        var nodes = new List<ModImageTreeNode>();

        foreach (var root in roots)
        {
            var imgDir = Path.Combine(root.ContentRoot, "img");
            var children = Directory.Exists(imgDir) ? ScanImageDirectory(imgDir) : [];

            nodes.Add(new ModImageTreeNode
            {
                Name = root.Name,
                ModPath = root.ContentRoot,
                IsMod = true,
                IsGame = root.IsGame,
                Children = new ObservableCollection<ModImageTreeNode>(children)
            });
        }

        return nodes;
    }

    private static List<ModImageTreeNode> ScanImageDirectory(string directory)
    {
        var nodes = new List<ModImageTreeNode>();

        if (!Directory.Exists(directory))
            return nodes;

        var imageFiles = Directory.GetFiles(directory)
            .Where(f =>
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
            })
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        // Pair normal + @2x images by filename convention (@2x / _2x suffix).
        var baseImages = new List<string>();

        foreach (var file in imageFiles)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.EndsWith("@2x", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("_2x", StringComparison.OrdinalIgnoreCase))
            {
                // x2 images are paired with their base counterparts below
                continue;
            }

            baseImages.Add(file);
        }

        foreach (var baseFile in baseImages)
        {
            var baseName = Path.GetFileNameWithoutExtension(baseFile);
            var baseExt = Path.GetExtension(baseFile);
            var dir = Path.GetDirectoryName(baseFile) ?? "";

            // Try @2x variant
            string? x2Path = null;
            var candidates = new[]
            {
                Path.Combine(dir, $"{baseName}@2x{baseExt}"),
                Path.Combine(dir, $"{baseName}_2x{baseExt}"),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    x2Path = candidate;
                    break;
                }
            }

            nodes.Add(new ModImageTreeNode
            {
                Name = Path.GetFileName(baseFile),
                FullImagePath = baseFile,
                X2ImagePath = x2Path,
                IsImage = true
            });
        }

        return nodes;
    }
}

/// <summary>
/// Tree node for the Image Browser tree view.
/// Represents either a mod (container) or an individual image (leaf).
/// </summary>
public class ModImageTreeNode
{
    public string Name { get; set; } = "";
    public string? ModPath { get; set; }
    public string? FullImagePath { get; set; }
    public string? X2ImagePath { get; set; }
    public ObservableCollection<ModImageTreeNode> Children { get; set; } = [];
    public bool IsMod { get; set; }
    public bool IsImage { get; set; }

    /// <summary>True for the base-game root node, which is read-only (never write into game files).</summary>
    public bool IsGame { get; set; }
}

/// <summary>
/// Preview info displayed in the right pane when an image is selected.
/// </summary>
public class ImageAssetInfo
{
    public string FileName { get; set; } = "";
    public string? FullPath { get; set; }
    public string? X2Path { get; set; }
    public string ModName { get; set; } = "";
    public string? Dimensions { get; set; }
    public Bitmap? Thumbnail { get; set; }
}