using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System.IO;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Context;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Services;
using NeoEditor.ViewModels;

namespace NeoEditor.ViewModels.MainContent;

public class RecentModEntry(int ModId, string Name, string Path, string EntityCountText, string TimeAgo, bool IsBase)
{
    public int ModId { get; set; } = ModId;
    public string Name { get; set; } = Name;
    public string Path { get; set; } = Path;
    public string EntityCountText { get; set; } = EntityCountText;
    public string TimeAgo { get; set; } = TimeAgo;
    public bool IsBase { get; set; } = IsBase;
    /// <summary>WAL中是否有未保存的编辑</summary>
    public bool HasUnsavedEdits { get; set; }
}

public class ProfileEntry(int ProfileId, string Name, string ModCountText)
{
    public int ProfileId { get; set; } = ProfileId;
    public string Name { get; set; } = Name;
    public string ModCountText { get; set; } = ModCountText;
    /// <summary>该Profile下是否有Mod存在WAL未保存编辑</summary>
    public bool HasUnsavedEdits { get; set; }
    /// <summary>该Profile中脏Mod数量</summary>
    public int DirtyModCount { get; set; }
}

public partial class HomePageViewModel : ViewModelBase
{
    private readonly IModManager _modManager;
    private readonly IConfigService _config;
    private readonly IDbContextFactory<EditorDbContext> _editorDbFactory;
    private readonly IDbContextFactory<GameDbContext> _gameDbFactory;
    private readonly IProfileManager _profileManager;
    private readonly IWorkspacePersistenceService _persistenceSvc;
    private readonly IServiceProvider _serviceProvider;

    public ObservableCollection<RecentModEntry> RecentMods { get; } = [];
    public ObservableCollection<ProfileEntry> Profiles { get; } = [];

    public bool HasRecentMods => RecentMods.Count > 0;

    public HomePageViewModel(IModManager modManager, IConfigService config,
        IDbContextFactory<EditorDbContext> editorDbFactory,
        IDbContextFactory<GameDbContext> gameDbFactory,
        IProfileManager profileManager,
        IWorkspacePersistenceService persistenceSvc,
        IServiceProvider serviceProvider,
        ILocalizationService localizationService,
        INotificationService notificationService)
        : base(localizationService, notificationService, null)
    {
        _modManager = modManager;
        _config = config;
        _editorDbFactory = editorDbFactory;
        _gameDbFactory = gameDbFactory;
        _profileManager = profileManager;
        _persistenceSvc = persistenceSvc;
        _serviceProvider = serviceProvider;
        Helper.AsyncHelper.FireAndForget(LoadAllAsync());
    }

    public async Task RefreshAsync()
    {
        await LoadAllAsync();
    }

    private async Task LoadAllAsync()
    {
        try
        {
            await LoadRecentModsAsync();
            await LoadProfilesAsync();
            await LoadDirtyStateAsync();
        }
        catch { /* DB may not be initialized yet */ }
    }

    private async Task LoadRecentModsAsync()
    {
        await using var edb = await _editorDbFactory.CreateDbContextAsync();
        await using var gdb = await _gameDbFactory.CreateDbContextAsync();

        var mods = edb.ModInfos.Where(m => !m.IsBase).OrderByDescending(m => m.LastModified).Take(8).ToList();
        RecentMods.Clear();
        foreach (var m in mods)
        {
            var count = await CountEntitiesAsync(gdb, m.ModId);
            var timeAgo = FormatTimeAgo(m.LastModified);
            RecentMods.Add(new RecentModEntry(m.ModId, m.Name, m.Path,
                Loc["HomePageEntitiesFormat", count], timeAgo, m.IsBase));
        }
        OnPropertyChanged(nameof(HasRecentMods));
    }

    private static async Task<int> CountEntitiesAsync(GameDbContext db, int modId)
    {
        // ItemTypes is the most commonly populated table across all mod types.
        // Conditions is a fallback for mods that only add status effects.
        // Previously queried all 14 tables (98+ queries for 7 mods) — two tables
        // cover 99% of mods and cut startup DB round-trips by 85%.
        var count = await db.ItemTypes.CountAsync(e => e.ModId == modId);
        if (count == 0)
            count = await db.Conditions.CountAsync(e => e.ModId == modId);
        return count;
    }

    private async Task LoadProfilesAsync()
    {
        await using var db = await _editorDbFactory.CreateDbContextAsync();
        var profileInfos = db.ProfileInfos.OrderByDescending(p => p.UpdateTime).Take(4).ToList();
        Profiles.Clear();
        var profileManager = _profileManager;
        foreach (var p in profileInfos)
        {
            var loadInfos = profileManager.LoadMods(p.Content);
            Profiles.Add(new ProfileEntry(p.ProfileId, p.Name,
                Loc["HomePageModsFormat", loadInfos.Count]));
        }
    }

    private async Task LoadDirtyStateAsync()
    {
        await using var edb = await _editorDbFactory.CreateDbContextAsync();

        // --- Mark dirty profiles (Profiles collection entries) ---
        foreach (var entry in Profiles)
        {
            try
            {
                var profile = await edb.ProfileInfos.FindAsync(entry.ProfileId);
                if (profile is null) continue;
                var loadInfos = _profileManager.LoadMods(profile.Content);
                var dirtyCount = 0;
                foreach (var mli in loadInfos)
                {
                    if (mli.Info?.ModId is > 0 or -1 &&
                        await _persistenceSvc.HasUnsavedCommandsAsync("mod", mli.Info.ModId))
                        dirtyCount++;
                }
                entry.HasUnsavedEdits = dirtyCount > 0;
                entry.DirtyModCount = dirtyCount;
            }
            catch { /* skip unparseable profiles */ }
        }

        // --- Mark dirty recent mods (RecentMods entries) ---
        foreach (var entry in RecentMods)
            entry.HasUnsavedEdits = await _persistenceSvc.HasUnsavedCommandsAsync("mod", entry.ModId);
    }

    private static string FormatTimeAgo(DateTime dt)
    {
        var span = DateTime.Now - dt;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }

    [RelayCommand]
    private async Task NewMod()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;

        var dialog = Views.Dialog.CreateModDialog.Create(_serviceProvider);
        var result = await dialog.ShowDialog<ModInfo?>(mainWindow);
        if (result is not null)
            Messenger.Send(new OpenModGameDataDocumentMessage(result));
    }

    [RelayCommand]
    private async Task ImportMod()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;
        if (TopLevel.GetTopLevel(mainWindow) is not { StorageProvider: { } storageProvider }) return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Mod Folder",
            AllowMultiple = false
        });
        if (folders.Count > 0 && folders[0].TryGetLocalPath() is { } folderPath)
        {
            await ImportAndOpenAsync(folderPath);
        }
    }

    private async Task ImportAndOpenAsync(string modPath)
    {
        var modInfo = await _modManager.ImportModAsync(modPath);
        if (modInfo is not null)
            Messenger.Send(new OpenModGameDataDocumentMessage(modInfo));
    }

    [RelayCommand]
    private async Task OpenRecentMod(RecentModEntry? entry)
    {
        if (entry is null) return;

        await using var db = await _editorDbFactory.CreateDbContextAsync();
        var modInfo = await db.ModInfos.FirstOrDefaultAsync(m => m.ModId == entry.ModId);
        if (modInfo is null) return;

        await _modManager.LoadModAsync(modInfo);
        Messenger.Send(new OpenModGameDataDocumentMessage(modInfo));
    }

    [RelayCommand]
    private void OpenMergeFromProfile(ProfileEntry? entry)
    {
        if (entry is null) return;
        Task.Run(async () =>
        {
            await using var db = await _editorDbFactory.CreateDbContextAsync();
            var profile = await db.ProfileInfos.FindAsync(entry.ProfileId);
            if (profile is not null)
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    Messenger.Send(new OpenMergeEditorMessage(profile)));
        });
    }

    private async Task<ModInfo?> EnsureGameModAsync()
    {
        var gameRoot = _config.Config.GameRootDir;
        if (string.IsNullOrWhiteSpace(gameRoot)) return null;

        await using var db = await _editorDbFactory.CreateDbContextAsync();
        var gameMod = await db.ModInfos.FirstOrDefaultAsync(m => m.ModId == -1);
        if (gameMod is not null) return gameMod;

        gameMod = new ModInfo
        {
            ModId = -1, Name = "Game", Path = "data", IsBase = true,
            LastImport = DateTime.Now, LastModified = DateTime.Now
        };
        db.ModInfos.Add(gameMod);
        await db.SaveChangesAsync();

        try { await _modManager.LoadModAsync(gameMod); }
        catch (Exception ex) { Serilog.Log.Logger.Warning(ex, "[HomePage] EnsureGameMod LoadModAsync failed for game mod"); }
        return gameMod;
    }
}
