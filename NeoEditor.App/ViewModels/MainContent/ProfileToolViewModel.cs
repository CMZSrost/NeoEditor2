using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Microsoft.EntityFrameworkCore;
using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Context;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Helper;
using NeoEditor.Services;
using Serilog;

namespace NeoEditor.ViewModels.MainContent;

/// <summary>
/// Profile Tool (left dock, D02 §5.0). A single-row icon toolbar (New / Import Mod,
/// Edit Profile / Reload Merge View) that acts on the ACTIVE profile — one page means
/// one profile, so there is no profile selector — plus a hierarchical grid
/// (ProDataGrid <see cref="HierarchicalModel{T}"/>) of the active profile's
/// mods → XML files → each XML's non-empty data classes (round22).
/// The game body is always the first root; the data-class leaves load lazily on expand.
/// </summary>
public partial class ProfileToolViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IModManager _modManager;
    private readonly IProfileManager _profileManager;
    private readonly IConfigService _config;
    private readonly IDbContextFactory<GameDbContext> _gameDbFactory;
    private readonly IMessenger _messenger;

    private ProfileInfo? _currentProfile;

    // Per-mod entity stats (normalized path → type → count), rebuilt on refresh.
    private readonly Dictionary<int, Dictionary<string, Dictionary<string, int>>> _modEntityStats = [];

    /// <summary>Roots + lazy children of the Profile Tool tree.</summary>
    public HierarchicalModel<ProfileTreeItem> TreeModel { get; }

    /// <summary>Raw DataGrid selection (a HierarchicalNode wrapper); unwrapped in <see cref="SelectedItem"/>.</summary>
    [ObservableProperty] private object? _selectedRow;

    public ProfileToolViewModel(
        IServiceProvider serviceProvider,
        IModManager modManager,
        IProfileManager profileManager,
        IConfigService config,
        IDbContextFactory<GameDbContext> gameDbFactory,
        IMessenger messenger)
    {
        _serviceProvider = serviceProvider;
        _modManager = modManager;
        _profileManager = profileManager;
        _config = config;
        _gameDbFactory = gameDbFactory;
        _messenger = messenger;

        TreeModel = new HierarchicalModel<ProfileTreeItem>(
            new HierarchicalOptions<ProfileTreeItem>
            {
                ChildrenSelector = LoadChildren,
                // Data-class rows have no children — hide their expander (task: leaves).
                IsLeafSelector = static item => item.Kind == ProfileTreeItemKind.DataType
            });

        // The active profile is whatever the workspace is loading / opening.
        _messenger.Register<LoadProfileMessage>(this, (_, m) => SetActiveProfile(m.ProfileInfo));
        _messenger.Register<OpenMergeEditorMessage>(this, (_, m) => SetActiveProfile(m.ProfileInfo));
        _messenger.Register<EditProfileMessage>(this, (_, m) =>
        {
            if (_currentProfile is { } cur && m.ProfileInfo.ProfileId == cur.ProfileId)
                SetActiveProfile(m.ProfileInfo);
        });
        _messenger.Register<RefreshModMessage>(this, (_, _) => _ = RebuildTreeAsync());
        _messenger.Register<GameRootDirChangedMessage>(this, (_, _) => _ = RebuildTreeAsync());
    }

    public bool HasActiveProfile => _currentProfile is not null;

    /// <summary>The tree node behind the current DataGrid selection (unwrapped).</summary>
    public ProfileTreeItem? SelectedItem =>
        SelectedRow switch
        {
            HierarchicalNode node => node.Item as ProfileTreeItem,
            ProfileTreeItem item => item,
            _ => null
        };

    partial void OnSelectedRowChanged(object? value)
    {
        _ = value;
        OnPropertyChanged(nameof(SelectedItem));
        OpenDirectoryCommand.NotifyCanExecuteChanged();
        OpenFileCommand.NotifyCanExecuteChanged();
        OpenXmlCommand.NotifyCanExecuteChanged();
    }

    public async Task RefreshAsync() => await RebuildTreeAsync();

    private void SetActiveProfile(ProfileInfo profile)
    {
        _currentProfile = profile;

        // Other receivers may not have filled ModLoadInfos yet — fill idempotently.
        if (profile.ModLoadInfos.Count == 0 && !string.IsNullOrWhiteSpace(profile.Content))
        {
            try
            {
                foreach (var modLoad in _profileManager.LoadMods(profile.Content))
                    profile.ModLoadInfos.Add(modLoad);
            }
            catch
            {
                /* unparseable profile content — the tree just stays empty */
            }
        }

        OnPropertyChanged(nameof(HasActiveProfile));
        EditProfileCommand.NotifyCanExecuteChanged();
        ReloadMergeViewCommand.NotifyCanExecuteChanged();
        _ = RebuildTreeAsync();
    }

    private async Task RebuildTreeAsync()
    {
        _modEntityStats.Clear();

        var gameRoot = _config.Config.GameRootDir;
        List<ProfileTreeItem> roots;
        if (string.IsNullOrWhiteSpace(gameRoot) || _currentProfile is null)
        {
            roots = [];
        }
        else
        {
            // Skip a profile entry that represents the game body — it is added first below.
            var mods = _currentProfile.ModLoadInfos
                .Where(static m => !string.IsNullOrWhiteSpace(m.Info.Path) && m.Info.ModId != -1)
                .Select(static m => m.Info)
                .ToList();

            // ProDataGrid's Expand() blocks the UI thread and resumes the selector on its
            // completing thread — an async selector (DB I/O / Task.Run) would deadlock or
            // crash the grid with VerifyAccess. Pre-warm stats here on the background thread
            // so the tree's ChildrenSelector stays fully synchronous.
            var modIds = mods.Select(static m => m.ModId).Append(-1).Distinct().ToList();
            roots = await Task.Run(() =>
            {
                PrewarmEntityStats(modIds, gameRoot);
                return BuildRootItems(mods, gameRoot);
            });
        }

        TreeModel.SetRoots(roots);
    }

    private static List<ProfileTreeItem> BuildRootItems(IReadOnlyList<ModInfo> mods, string gameRoot)
    {
        var roots = new List<ProfileTreeItem>
        {
            BuildGameNode(gameRoot)
        };

        foreach (var mod in mods)
        {
            var contentRoot = ResolveContentRoot(mod, gameRoot);
            if (string.IsNullOrWhiteSpace(contentRoot) || !Directory.Exists(contentRoot))
                continue;

            var modNode = new ProfileTreeItem
            {
                Kind = ProfileTreeItemKind.Mod,
                Name = mod.Name,
                Path = contentRoot,
                ModId = mod.ModId
            };

            foreach (var xml in Directory.GetFiles(contentRoot, "*.xml", SearchOption.AllDirectories)
                         .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
            {
                AddXmlChild(modNode, contentRoot, xml, mod.ModId);
            }

            roots.Add(modNode);
        }

        return roots;
    }

    /// <summary>Game body node — always the first root (round22: "missing game body" fix).</summary>
    private static ProfileTreeItem BuildGameNode(string gameRoot)
    {
        var gameNode = new ProfileTreeItem
        {
            Kind = ProfileTreeItemKind.Mod,
            Name = "Game",
            Path = gameRoot,
            ModId = -1,
            IsGame = true
        };

        var gameDataDir = Path.Combine(gameRoot, "data");
        if (Directory.Exists(gameDataDir))
        {
            foreach (var xml in Directory.GetFiles(gameDataDir, "*.xml", SearchOption.AllDirectories)
                         .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
            {
                AddXmlChild(gameNode, gameDataDir, xml, -1);
            }
        }

        return gameNode;
    }

    private static void AddXmlChild(ProfileTreeItem parent, string baseDir, string xml, int modId)
    {
        parent.Children.Add(new ProfileTreeItem
        {
            Kind = ProfileTreeItemKind.Xml,
            Name = Path.GetRelativePath(baseDir, xml).Replace('\\', '/'),
            Path = Path.GetFullPath(xml),
            ModId = modId
        });
    }

    private static string? ResolveContentRoot(ModInfo mod, string gameRoot)
    {
        var path = Path.IsPathRooted(mod.Path) ? mod.Path : Path.Combine(gameRoot, mod.Path);
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }
    }

    private bool CanEditProfile() => _currentProfile is not null;

    [RelayCommand(CanExecute = nameof(CanEditProfile))]
    private void EditProfile()
    {
        if (_currentProfile is { } p)
            _messenger.Send(new EditProfileMessage(p));
    }

    private bool CanReloadMergeView() => _currentProfile is not null;

    [RelayCommand(CanExecute = nameof(CanReloadMergeView))]
    private void ReloadMergeView()
    {
        if (_currentProfile is { } p)
            _messenger.Send(new OpenMergeEditorMessage(p));
    }

    // ── Tree interactions (round22) ──

    /// <summary>Double-click an XML node → open the read-only XML document.</summary>
    private bool CanOpenXml() => SelectedItem is { Kind: ProfileTreeItemKind.Xml };

    [RelayCommand(CanExecute = nameof(CanOpenXml))]
    private void OpenXml()
    {
        if (SelectedItem is not { Kind: ProfileTreeItemKind.Xml, Path: { } path })
            return;
        _messenger.Send(new OpenXmlDocumentMessage(path, Path.GetFileName(path)));
    }

    /// <summary>Reveal the node in Explorer — directory for a mod, file for an XML/data-class row.</summary>
    private bool CanOpenDirectory() => SelectedItem is not null;

    [RelayCommand(CanExecute = nameof(CanOpenDirectory))]
    private void OpenDirectory()
    {
        if (SelectedItem is not { Path: { } path })
            return;

        if (Directory.Exists(path))
            Process.Start("explorer.exe", $"\"{path}\"");
        else if (File.Exists(path))
            Process.Start("explorer.exe", $"/select,\"{path}\"");
    }

    /// <summary>Open the underlying file with the default app (XML/data-class rows).</summary>
    private bool CanOpenFile() => SelectedItem is { Kind: not ProfileTreeItemKind.Mod };

    [RelayCommand(CanExecute = nameof(CanOpenFile))]
    private void OpenFile()
    {
        if (SelectedItem is not { Path: { } path } || !File.Exists(path))
            return;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task NewMod()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } mainWindow
            }) return;

        var dialog = Views.Dialog.CreateModDialog.Create(_serviceProvider);
        var result = await dialog.ShowDialog<ModInfo?>(mainWindow);
        if (result is not null)
            _messenger.Send(new OpenModGameDataDocumentMessage(result));
    }

    [RelayCommand]
    private async Task ImportMod()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } mainWindow
            }) return;
        if (TopLevel.GetTopLevel(mainWindow) is not { StorageProvider: { } storageProvider }) return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Mod Folder",
            AllowMultiple = false
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } folderPath)
        {
            var modInfo = await _modManager.ImportModAsync(folderPath);
            if (modInfo is not null)
                _messenger.Send(new OpenModGameDataDocumentMessage(modInfo));
        }
    }

    // ── Lazy leaf loading (data-class stats, per-mod cached) ──

    /// <summary>
    /// Hierarchical model's children selector: XML nodes materialize their data-class leaves
    /// once, on first expand. Mod nodes return their eagerly-scanned XML children.
    /// </summary>
    /// <remarks>
    /// The selector must be fully synchronous — ProDataGrid's <c>Expand()</c> blocks the UI
    /// thread on <c>ExpandAsync(...).GetResult()</c>, so an async selector that yields would
    /// deadlock, and a threadpool continuation would crash the grid's formula model with
    /// VerifyAccess. Entity stats are pre-warmed on a background thread during
    /// <see cref="RebuildTreeAsync"/>; here we only read the cache and mutate the tree on the
    /// UI thread.
    /// </remarks>
    private IEnumerable<ProfileTreeItem> LoadChildren(ProfileTreeItem item)
    {
        if (item.Kind == ProfileTreeItemKind.Xml && !item.TypesLoaded)
        {
            var leaves = LoadXmlLeafTypes(item);
            if (leaves is not null)
            {
                foreach (var leaf in leaves)
                    item.Children.Add(leaf);
                item.TypesLoaded = true;
            }
        }

        return item.Children;
    }

    /// <summary>
    /// Builds the non-empty data-class leaves for an XML node from the pre-warmed
    /// <see cref="_modEntityStats"/> cache. Called on the UI thread.
    /// </summary>
    private List<ProfileTreeItem>? LoadXmlLeafTypes(ProfileTreeItem node)
    {
        if (!_modEntityStats.TryGetValue(node.ModId, out var stats))
            return null;

        var key = ModEntityStats.Normalize(node.Path ?? "");
        if (!stats.TryGetValue(key, out var types))
        {
            // Path may differ (e.g. game root moved since import) — match by file name.
            var baseName = Path.GetFileName(node.Path ?? "");
            types = stats.FirstOrDefault(kv =>
                Path.GetFileName(kv.Key).Equals(baseName, StringComparison.OrdinalIgnoreCase)).Value;
        }

        if (types is null)
            return null;

        return types.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new ProfileTreeItem
            {
                Kind = ProfileTreeItemKind.DataType,
                Name = kv.Key,
                Count = kv.Value,
                ModId = node.ModId,
                Path = node.Path
            })
            .ToList();
    }

    /// <summary>Loads per-mod entity stats from the DB on a background thread (called from <see cref="RebuildTreeAsync"/>).</summary>
    private void PrewarmEntityStats(IReadOnlyList<int> modIds, string gameRoot)
    {
        foreach (var modId in modIds)
        {
            if (_modEntityStats.ContainsKey(modId))
                continue;

            try
            {
                using var db = _gameDbFactory.CreateDbContext();
                _modEntityStats[modId] = ModEntityStats.LoadModEntityStats(db, modId, gameRoot);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load entity stats for ModId {ModId}", modId);
            }
        }
    }
}