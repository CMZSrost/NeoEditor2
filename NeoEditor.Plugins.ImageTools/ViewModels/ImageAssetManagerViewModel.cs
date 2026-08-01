using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Data.Messages;

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
    private readonly IConfigService _config;
    private readonly IMessenger _messenger;

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
        IConfigService config,
        IMessenger messenger)
        : base(loc)
    {
        _config = config;
        _messenger = messenger;

        // 议题6: auto-load + refresh on workspace lifecycle changes (game root,
        // profile load, mod create/delete). Refresh is best-effort.
        _messenger.Register<GameRootDirChangedMessage>(this, (_, _) => _ = RefreshAsync());
        _messenger.Register<LoadProfileMessage>(this, (_, _) => _ = RefreshAsync());
        _messenger.Register<RefreshModMessage>(this, (_, _) => _ = RefreshAsync());
        _ = RefreshAsync();
    }

    /// <summary>
    /// Called when SelectedNode changes. Updates the preview pane.
    /// </summary>
    partial void OnSelectedNodeChanged(ModImageTreeNode? value)
    {
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
            var nodes = await Task.Run(BuildTree);
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
                    Children = new ObservableCollection<ModImageTreeNode>(matchingChildren)
                });
            }
        }
    }

    /// <summary>
    /// Builds the file-system tree. Base game = scan <c>img/</c>; mods = scan each
    /// <c>Mods/&lt;mod&gt;/img/</c> subdirectory. Never reads getimages.php (R27).
    /// </summary>
    private List<ModImageTreeNode> BuildTree()
    {
        var nodes = new List<ModImageTreeNode>();
        var gameRoot = _config.Config.GameRootDir;

        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
            return nodes;

        // 1. Base game img/ directory as a pseudo-mod
        var baseImgDir = Path.Combine(gameRoot, "img");
        if (Directory.Exists(baseImgDir))
        {
            nodes.Add(new ModImageTreeNode
            {
                Name = "Base Game",
                ModPath = baseImgDir,
                IsMod = true,
                Children = new ObservableCollection<ModImageTreeNode>(ScanImageDirectory(baseImgDir))
            });
        }

        // 2. Scan Mods/ directory for mod folders, then each mod's img/ subdirectory
        var modsDir = Path.Combine(gameRoot, "Mods");
        if (Directory.Exists(modsDir))
        {
            foreach (var modFolder in Directory.GetDirectories(modsDir))
            {
                var modName = Path.GetFileName(modFolder);
                var modImgDir = Path.Combine(modFolder, "img");

                List<ModImageTreeNode> imageNodes;
                if (Directory.Exists(modImgDir))
                {
                    imageNodes = ScanImageDirectory(modImgDir);
                }
                else
                {
                    imageNodes = [];
                }

                nodes.Add(new ModImageTreeNode
                {
                    Name = modName,
                    ModPath = modFolder,
                    IsMod = true,
                    Children = new ObservableCollection<ModImageTreeNode>(imageNodes)
                });
            }
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