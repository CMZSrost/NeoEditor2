using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Data.Context;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Helper;
using NeoEditor.Services;

namespace NeoEditor.ViewModels.MainContent;

/// <summary>
/// Profile Tool (left dock, D02 §5.0). A single-row icon toolbar (New / Import Mod,
/// Edit Profile / Reload Merge View) that acts on the ACTIVE profile — one page means
/// one profile, so there is no profile selector — plus a tree of the active profile's
/// mods → their XML files → each XML's non-empty data classes.
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

    public ObservableCollection<ProfileModNode> ModNodes { get; } = [];

    public bool HasActiveProfile => _currentProfile is not null;

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
        ModNodes.Clear();
        _modEntityStats.Clear();

        var gameRoot = _config.Config.GameRootDir;
        if (string.IsNullOrWhiteSpace(gameRoot) || _currentProfile is null)
            return;

        var mods = _currentProfile.ModLoadInfos
            .Where(static m => !string.IsNullOrWhiteSpace(m.Info.Path))
            .Select(static m => m.Info)
            .ToList();
        if (mods.Count == 0)
            return;

        var nodes = await Task.Run(() => BuildModNodes(mods, gameRoot));
        foreach (var node in nodes)
            ModNodes.Add(node);
    }

    private static List<ProfileModNode> BuildModNodes(IReadOnlyList<ModInfo> mods, string gameRoot)
    {
        var nodes = new List<ProfileModNode>();
        foreach (var mod in mods)
        {
            var contentRoot = ResolveContentRoot(mod, gameRoot);
            if (string.IsNullOrWhiteSpace(contentRoot) || !Directory.Exists(contentRoot))
                continue;

            var modNode = new ProfileModNode
            {
                Name = mod.Name,
                ModId = mod.ModId,
                Path = mod.Path,
                ContentRoot = contentRoot,
                IsGame = mod.ModId == -1 ||
                         string.Equals(mod.Name, "Game", StringComparison.OrdinalIgnoreCase)
            };

            foreach (var xml in Directory.GetFiles(contentRoot, "*.xml", SearchOption.AllDirectories)
                         .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
            {
                modNode.XmlNodes.Add(new ProfileXmlNode
                {
                    Name = Path.GetRelativePath(contentRoot, xml).Replace('\\', '/'),
                    AbsolutePath = Path.GetFullPath(xml)
                });
            }

            nodes.Add(modNode);
        }

        return nodes;
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

    /// <summary>Lazy-loads a single XML node's non-empty data classes (per-mod DB pass, cached).</summary>
    [RelayCommand]
    private async Task LoadXmlTypesAsync(ProfileXmlNode? node)
    {
        if (node is null || node.TypesLoaded)
            return;

        var modNode = ModNodes.FirstOrDefault(m => m.XmlNodes.Contains(node));
        if (modNode is null)
            return;

        try
        {
            var stats = await LoadModStatsAsync(modNode.ModId);
            if (stats is null)
                return;

            var key = ModEntityStats.Normalize(node.AbsolutePath);
            if (!stats.TryGetValue(key, out var types))
            {
                // Path may differ (e.g. game root moved since import) — match by file name.
                var baseName = Path.GetFileName(node.AbsolutePath);
                types = stats.FirstOrDefault(kv =>
                    Path.GetFileName(kv.Key).Equals(baseName, StringComparison.OrdinalIgnoreCase)).Value;
            }

            if (types is null)
                return;

            foreach (var (typeName, count) in types.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                node.TypeNodes.Add(new ProfileDataTypeNode { TypeName = typeName, Count = count });

            node.TypesLoaded = true;
        }
        catch
        {
            // Best-effort DB load; leave the node empty rather than crash the tool.
        }
    }

    private async Task<Dictionary<string, Dictionary<string, int>>?> LoadModStatsAsync(int modId)
    {
        if (_modEntityStats.TryGetValue(modId, out var cached))
            return cached;

        try
        {
            await using var db = await _gameDbFactory.CreateDbContextAsync();
            var stats = ModEntityStats.LoadModEntityStats(db, modId);
            _modEntityStats[modId] = stats;
            return stats;
        }
        catch
        {
            return null;
        }
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
}