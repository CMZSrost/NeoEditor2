using System;
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

namespace NeoEditor.ViewModels.MainContent;

public record RecentModEntry(int ModId, string Name, string Path, string EntityCountText, string TimeAgo, bool IsBase);

public record ProfileEntry(int ProfileId, string Name, string ModCountText);

public partial class HomePageViewModel : ViewModelBase
{
    private readonly IModManager _modManager;
    private readonly IConfigService _config;
    private readonly IDbContextFactory<EditorDbContext> _editorDbFactory;
    private readonly IDbContextFactory<GameDbContext> _gameDbFactory;

    public ObservableCollection<RecentModEntry> RecentMods { get; } = [];
    public ObservableCollection<ProfileEntry> Profiles { get; } = [];

    public bool HasRecentMods => RecentMods.Count > 0;

    public HomePageViewModel() : this(
        App.ServiceProvider!.GetRequiredService<IModManager>(),
        App.ServiceProvider!.GetRequiredService<IConfigService>(),
        App.ServiceProvider!.GetRequiredService<IDbContextFactory<EditorDbContext>>(),
        App.ServiceProvider!.GetRequiredService<IDbContextFactory<GameDbContext>>())
    {
    }

    public HomePageViewModel(IModManager modManager, IConfigService config,
        IDbContextFactory<EditorDbContext> editorDbFactory,
        IDbContextFactory<GameDbContext> gameDbFactory)
    {
        _modManager = modManager;
        _config = config;
        _editorDbFactory = editorDbFactory;
        _gameDbFactory = gameDbFactory;
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
        // Count across the main entity tables (covers 99% of data)
        return await db.AttackModes.CountAsync(e => e.ModId == modId)
            + await db.BattleMoves.CountAsync(e => e.ModId == modId)
            + await db.CampTypes.CountAsync(e => e.ModId == modId)
            + await db.Conditions.CountAsync(e => e.ModId == modId)
            + await db.Creatures.CountAsync(e => e.ModId == modId)
            + await db.Encounters.CountAsync(e => e.ModId == modId)
            + await db.Factions.CountAsync(e => e.ModId == modId)
            + await db.HexTypes.CountAsync(e => e.ModId == modId)
            + await db.Ingredients.CountAsync(e => e.ModId == modId)
            + await db.ItemProps.CountAsync(e => e.ModId == modId)
            + await db.ItemTypes.CountAsync(e => e.ModId == modId)
            + await db.Recipes.CountAsync(e => e.ModId == modId)
            + await db.TreasureTables.CountAsync(e => e.ModId == modId);
    }

    private async Task LoadProfilesAsync()
    {
        await using var db = await _editorDbFactory.CreateDbContextAsync();
        var profileInfos = db.ProfileInfos.OrderByDescending(p => p.UpdateTime).Take(4).ToList();
        Profiles.Clear();
        var profileManager = App.ServiceProvider!.GetRequiredService<IProfileManager>();
        foreach (var p in profileInfos)
        {
            var loadInfos = profileManager.LoadMods(p.Content);
            Profiles.Add(new ProfileEntry(p.ProfileId, p.Name,
                Loc["HomePageModsFormat", loadInfos.Count]));
        }
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
    private async Task BrowseGameData()
    {
        var gameMod = await EnsureGameModAsync();
        if (gameMod is not null)
            Messenger.Send(new OpenModGameDataDocumentMessage(gameMod, ReadOnly: true));
    }

    [RelayCommand]
    private async Task NewMod()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;

        var dialog = new Views.Dialog.CreateModDialog();
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
        var modInfo = await db.ModInfos.FindAsync(entry.ModId);
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
        var gameMod = await db.ModInfos.FindAsync(-1);
        if (gameMod is not null) return gameMod;

        gameMod = new ModInfo
        {
            ModId = -1, Name = "Game", Path = "data", IsBase = true,
            LastImport = DateTime.Now, LastModified = DateTime.Now
        };
        db.ModInfos.Add(gameMod);
        await db.SaveChangesAsync();

        try { await _modManager.LoadModAsync(gameMod); }
        catch { }
        return gameMod;
    }
}
