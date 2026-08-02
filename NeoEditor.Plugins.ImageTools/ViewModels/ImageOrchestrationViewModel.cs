using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.DataGridHierarchical;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Data.Messages;
using NeoEditor.Plugins.ImageTools.Services;

namespace NeoEditor.Plugins.ImageTools.ViewModels;

/// <summary>
/// ViewModel for the Image Orchestration Tool Dock (R27).
/// Reads each source's <c>getimages.php</c> (base game + mods), shows the declared
/// normal→x2 pairs in strict declaration order, validates that each referenced file
/// actually exists (R27 path resolution), and lets the user edit a mod's orchestration
/// and write it back via <see cref="IModImageListService.GenerateImagePhp"/>.
/// The base game source is read-only — modders must never rewrite game files.
/// </summary>
public partial class ImageOrchestrationViewModel : ImageToolObservableObject
{
    private readonly IConfigService _config;
    private readonly IModImageListService _imageListService;
    private readonly INotificationService _notification;
    private readonly IProfileModSourceProvider _sourceProvider;

    // Serializes refresh calls so concurrent triggers (auto-load, message nudge,
    // explicit button) never run two source rebuilds that clobber each other.
    private readonly object _refreshGate = new();
    private Task _refreshChain = Task.CompletedTask;

    public ObservableCollection<ImageOrchestrationSourceNode> Sources { get; } = [];

    /// <summary>Hierarchical grid model: source roots with their pairs as children (round22).</summary>
    public HierarchicalModel<object> TreeModel { get; }

    /// <summary>Raw DataGrid selection (a HierarchicalNode wrapper); unwrapped and synced to the pair/source below.</summary>
    [ObservableProperty]
    public partial object? SelectedRow { get; set; }

    [ObservableProperty] public partial ImageOrchestrationSourceNode? SelectedSource { get; set; }

    [ObservableProperty] public partial ImageOrchestrationPairItem? SelectedPair { get; set; }

    [ObservableProperty] public partial bool IsLoading { get; set; }

    public bool HasSelectedSource => SelectedSource is not null;
    public bool IsReadOnly => SelectedSource?.ReadOnly == true;

    /// <summary>
    /// Grid selection → VM state. Selecting a source also selects its first pair (so the
    /// pair toolbar acts immediately, matching the old two-pane behavior); selecting a
    /// pair selects the pair and resolves its owning source.
    /// </summary>
    partial void OnSelectedRowChanged(object? value)
    {
        var item = value switch
        {
            HierarchicalNode node => node.Item,
            _ => value
        };

        switch (item)
        {
            case ImageOrchestrationSourceNode s:
                SelectedSource = s; // OnSelectedSourceChanged auto-selects its first pair
                break;
            case ImageOrchestrationPairItem p:
                SelectedSource = Sources.FirstOrDefault(s => s.Pairs.Contains(p));
                SelectedPair = p; // overrides the auto-selected first pair
                break;
            default:
                SelectedSource = null;
                SelectedPair = null;
                break;
        }
    }

    partial void OnSelectedSourceChanged(ImageOrchestrationSourceNode? value)
    {
        SelectedPair = value?.Pairs.FirstOrDefault();
        OnPropertyChanged(nameof(HasSelectedSource));
        OnPropertyChanged(nameof(IsReadOnly));
        SaveCommand.NotifyCanExecuteChanged();
        AddPairCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedPairChanged(ImageOrchestrationPairItem? value)
    {
        _ = value;
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
        DeletePairCommand.NotifyCanExecuteChanged();
    }

    public ImageOrchestrationViewModel(
        ILocalizationService loc,
        IConfigService config,
        IModImageListService imageListService,
        INotificationService notificationService,
        IProfileModSourceProvider sourceProvider,
        IMessenger messenger)
        : base(loc)
    {
        _config = config;
        _imageListService = imageListService;
        _notification = notificationService;
        _sourceProvider = sourceProvider;

        TreeModel = new HierarchicalModel<object>(new HierarchicalOptions<object>
        {
            // Source roots expose their declared pairs as children; pairs are leaves.
            ChildrenSelector = o => o is ImageOrchestrationSourceNode s ? s.Pairs : null,
            IsLeafSelector = o => o is not ImageOrchestrationSourceNode
        });

        // 议题6 (R27): auto-load + refresh on workspace lifecycle changes.
        // OpenMergeEditorMessage covers loads triggered from the Profile Tool.
        messenger.Register<GameRootDirChangedMessage>(this, (_, _) => _ = RefreshAsync());
        messenger.Register<LoadProfileMessage>(this, (_, _) => _ = RefreshAsync());
        messenger.Register<OpenMergeEditorMessage>(this, (_, _) => _ = RefreshAsync());
        messenger.Register<RefreshModMessage>(this, (_, _) => _ = RefreshAsync());
        _ = RefreshAsync();
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
            // sources on the background thread.
            var roots = _sourceProvider.GetContentRoots();
            var sources = await Task.Run(() => BuildSources(roots));
            ApplySources(sources);
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

    private void ApplySources(IReadOnlyList<ImageOrchestrationSourceNode> sources)
    {
        Sources.Clear();
        foreach (var source in sources)
        {
            Sources.Add(source);
        }

        TreeModel.SetRoots(sources);
        SelectedRow = null;
    }

    private bool CanSave() => SelectedSource is { ReadOnly: false };

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (SelectedSource is null)
        {
            return;
        }

        try
        {
            var pairs = SelectedSource.Pairs
                .Select(static pair => (pair.NormalImage, pair.X2Image))
                .ToList();

            var phpContent = _imageListService.GenerateImagePhp(pairs);

            var directory = Path.GetDirectoryName(SelectedSource.GetImagesPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(SelectedSource.GetImagesPath, phpContent);
            _notification.ShowSuccess(
                $"Saved {SelectedSource.GetImagesPath} ({pairs.Count} pairs)",
                "Image Orchestration");

            // Re-resolve existence after write (pairings may now resolve to new files).
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _notification.ShowError($"Failed to save {SelectedSource.GetImagesPath}: {ex.Message}",
                "Image Orchestration");
        }
    }

    private bool CanAddPair() => SelectedSource is { ReadOnly: false };

    [RelayCommand(CanExecute = nameof(CanAddPair))]
    private async Task AddPairAsync()
    {
        if (SelectedSource is null)
        {
            return;
        }

        var storageProvider = GetStorageProvider();
        if (storageProvider is null)
        {
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select image files (normal + optional x2_)",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Image files")
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

        var imgDir = Path.Combine(SelectedSource.ContentRoot, "img");
        Directory.CreateDirectory(imgDir);

        // Group by base name using the x2_ convention (same as ModImagesDocument import).
        var pending = new Dictionary<string, (string? Normal, string? X2)>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in selectedPaths)
        {
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            var isX2 = fileName.StartsWith("x2_", StringComparison.OrdinalIgnoreCase);
            var baseName = isX2 ? fileName[3..] : fileName;
            if (!pending.TryGetValue(baseName, out var pair))
            {
                pair = (null, null);
            }

            pending[baseName] = isX2 ? (pair.Normal, path) : (path, pair.X2);
        }

        foreach (var (_, pair) in pending)
        {
            if (pair.Normal is null)
            {
                continue; // x2-only selection — needs a normal image to form a pair
            }

            var normalFileName = Path.GetFileName(pair.Normal);
            var x2FileName = string.IsNullOrWhiteSpace(pair.X2)
                ? $"x2_{normalFileName}"
                : Path.GetFileName(pair.X2);

            CopyInto(normalFileName, pair.Normal, imgDir);
            if (!string.IsNullOrWhiteSpace(pair.X2))
            {
                CopyInto(x2FileName, pair.X2, imgDir);
            }

            SelectedSource.Pairs.Add(BuildPair(SelectedSource.ContentRoot, SelectedSource.GameRoot,
                normalFileName, x2FileName));
        }

        SelectedPair = SelectedSource.Pairs.LastOrDefault();
        _notification.ShowInfo($"Added {pending.Values.Count(p => p.Normal is not null)} image pair(s)",
            "Image Orchestration");
    }

    private static void CopyInto(string fileName, string sourcePath, string targetDirectory)
    {
        try
        {
            var target = Path.Combine(targetDirectory, fileName);
            if (!File.Exists(target) && !PathsEqual(sourcePath, target))
            {
                File.Copy(sourcePath, target);
            }
        }
        catch
        {
            // Copy is best-effort; existence markers will surface any failure.
        }
    }

    private static bool PathsEqual(string a, string b)
    {
        return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    }

    private bool CanMoveUp()
    {
        return SelectedSource is { ReadOnly: false } && SelectedPair is not null &&
               SelectedSource.Pairs.IndexOf(SelectedPair) > 0;
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp()
    {
        if (SelectedSource is null || SelectedPair is null)
        {
            return;
        }

        var index = SelectedSource.Pairs.IndexOf(SelectedPair);
        if (index <= 0)
        {
            return;
        }

        SelectedSource.Pairs.Move(index, index - 1);
    }

    private bool CanMoveDown()
    {
        return SelectedSource is { ReadOnly: false } && SelectedPair is not null &&
               SelectedSource.Pairs.IndexOf(SelectedPair) >= 0 &&
               SelectedSource.Pairs.IndexOf(SelectedPair) < SelectedSource.Pairs.Count - 1;
    }

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown()
    {
        if (SelectedSource is null || SelectedPair is null)
        {
            return;
        }

        var index = SelectedSource.Pairs.IndexOf(SelectedPair);
        if (index < 0 || index >= SelectedSource.Pairs.Count - 1)
        {
            return;
        }

        SelectedSource.Pairs.Move(index, index + 1);
    }

    private bool CanDeletePair() => SelectedSource is { ReadOnly: false } && SelectedPair is not null;

    [RelayCommand(CanExecute = nameof(CanDeletePair))]
    private void DeletePair()
    {
        if (SelectedSource is null || SelectedPair is null)
        {
            return;
        }

        SelectedSource.Pairs.Remove(SelectedPair);
        SelectedPair = SelectedSource?.Pairs.FirstOrDefault();
    }

    /// <summary>
    /// Builds the source list from the active profile's content roots (task 4).
    /// Base game reads <c>&lt;gameRoot&gt;/getimages.php</c>; each mod reads
    /// <c>&lt;contentRoot&gt;/getimages.php</c> where contentRoot comes from the profile's
    /// ModLoadInfos. Sources without a getimages.php file appear empty so the user can
    /// create one by saving.
    /// </summary>
    private List<ImageOrchestrationSourceNode> BuildSources(IReadOnlyList<ModContentRoot> roots)
    {
        var gameRoot = _config.Config.GameRootDir;
        var sources = new List<ImageOrchestrationSourceNode>();

        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            return sources;
        }

        foreach (var root in roots)
        {
            var php = Path.Combine(root.ContentRoot, "getimages.php");
            sources.Add(BuildSource(root.Name, php, root.ContentRoot, gameRoot, isGame: root.IsGame));
        }

        return sources;
    }

    private ImageOrchestrationSourceNode BuildSource(string name, string getImagesPath,
        string contentRoot, string gameRoot, bool isGame)
    {
        var node = new ImageOrchestrationSourceNode
        {
            Name = name,
            GetImagesPath = getImagesPath,
            ContentRoot = contentRoot,
            GameRoot = gameRoot,
            IsGame = isGame,
            HasGetImagesFile = File.Exists(getImagesPath)
        };

        if (File.Exists(getImagesPath))
        {
            try
            {
                foreach (var (normal, x2) in _imageListService.ParseImagePairs(getImagesPath))
                {
                    node.Pairs.Add(BuildPair(contentRoot, gameRoot, normal, x2));
                }
            }
            catch (Exception ex)
            {
                node.ParseError = ex.Message;
            }
        }

        return node;
    }

    private static ImageOrchestrationPairItem BuildPair(string contentRoot, string gameRoot,
        string normal, string x2)
    {
        var normalPath = ResolveImagePath(contentRoot, gameRoot, normal);
        var x2Path = string.IsNullOrWhiteSpace(x2) ? null : ResolveImagePath(contentRoot, gameRoot, x2);

        return new ImageOrchestrationPairItem
        {
            NormalImage = normal,
            X2Image = x2,
            NormalExists = normalPath is not null && File.Exists(normalPath),
            X2Exists = x2Path is not null && File.Exists(x2Path),
            NormalPath = normalPath,
            X2Path = x2Path
        };
    }

    /// <summary>
    /// R27 path resolution: <c>contentRoot/&lt;name&gt;</c> → <c>contentRoot/img/&lt;name&gt;</c>
    /// → <c>gameRoot/img/&lt;name&gt;</c>. Returns the first existing candidate, or null.
    /// </summary>
    private static string? ResolveImagePath(string contentRoot, string gameRoot, string imageName)
    {
        if (string.IsNullOrWhiteSpace(imageName))
        {
            return null;
        }

        var normalized = imageName.Trim()
            .TrimStart('/', '\\')
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var candidates = new[]
        {
            Path.Combine(contentRoot, normalized),
            Path.Combine(contentRoot, "img", normalized),
            Path.Combine(gameRoot, "img", normalized)
        };

        return candidates.FirstOrDefault(File.Exists);
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
}

/// <summary>
/// One getimages.php source: the base game or a single mod. Holds its declared pairs
/// in strict declaration order.
/// </summary>
public sealed class ImageOrchestrationSourceNode : ObservableObject
{
    public required string Name { get; init; }
    public required string GetImagesPath { get; init; }
    public required string ContentRoot { get; init; }
    public required string GameRoot { get; init; }
    public bool IsGame { get; init; }

    /// <summary>Base game is read-only — never write back to game install files.</summary>
    public bool ReadOnly => IsGame;

    public bool HasGetImagesFile { get; init; }
    public string? ParseError { get; set; }

    public ObservableCollection<ImageOrchestrationPairItem> Pairs { get; } = [];

    public int MissingCount => Pairs.Count(pair => pair.HasMissing);
    public bool HasMissing => MissingCount > 0;

    public string Summary
    {
        get
        {
            var summary = $"{Name} — {Pairs.Count} pair(s)";
            if (HasMissing)
            {
                summary += $", {MissingCount} missing";
            }

            if (ParseError is not null)
            {
                summary += ", parse error";
            }

            return summary;
        }
    }

    /// <summary>Compact "N missing" hint for the status column (source rows).</summary>
    public string MissingSummary => HasMissing ? $"{MissingCount} missing" : "";

    // ── Unified grid-row shape (shared with ImageOrchestrationPairItem) — the cell
    //    templates bind to these so the table renders without ContentControl/DataTemplate
    //    type switching (which fell back to ToString() = class name at runtime).
    public string RowTitle => Name;
    public string RowSubtitle => Summary;
    public bool HasRowSubtitle => RowSubtitle.Length > 0;
    public string? RowToolTip => null;
    public string X2Text => MissingSummary;
    public string? X2ToolTip => null;
    public bool IsPair => false;
    public bool NormalMissing => true;
    public bool X2Missing => true;

    public ImageOrchestrationSourceNode()
    {
        Pairs.CollectionChanged += OnPairsChanged;
    }

    private void OnPairsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(MissingCount));
        OnPropertyChanged(nameof(HasMissing));
        OnPropertyChanged(nameof(MissingSummary));
    }
}

/// <summary>One image pair declared in getimages.php, with file-existence markers.</summary>
public sealed class ImageOrchestrationPairItem
{
    public required string NormalImage { get; init; }
    public required string X2Image { get; init; }
    public bool NormalExists { get; init; }
    public bool X2Exists { get; init; }
    public string? NormalPath { get; init; }
    public string? X2Path { get; init; }

    public bool IsMissing => !NormalExists && !X2Exists;
    public bool HasMissing => !NormalExists || !X2Exists;
    public string DisplayName => NormalImage;

    // ── Unified grid-row shape (shared with ImageOrchestrationSourceNode) ──
    public string RowTitle => NormalImage;
    public string RowSubtitle => "";
    public bool HasRowSubtitle => false;
    public string? RowToolTip => NormalPath;
    public string X2Text => X2Image;
    public string? X2ToolTip => X2Path;
    public bool IsPair => true;
    public bool NormalMissing => !NormalExists;
    public bool X2Missing => !X2Exists;
}