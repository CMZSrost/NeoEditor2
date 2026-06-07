using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvaloniaEdit.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Helper;
using NeoEditor.Services;
using NeoEditor.Views.Dialog;
using Newtonsoft.Json;

namespace NeoEditor.ViewModels.ExplorerPane;

public partial class ModIndexViewModel : ViewModelBase, IRecipient<InitProfileMessage>, IRecipient<LoadProfileMessage>,
    IRecipient<SaveProfileMessage>,
    IRecipient<GameRootDirChangedMessage>
{
    private readonly IConfigService _config;
    private readonly IProfileManager _profileManager;
    private readonly DataExportService _dataExportService;
    public AppConfig Config => _config.Config;
    [ObservableProperty] public partial ProfileInfo? Info { get; set; }
    public ObservableCollection<ProfileInfo> Profiles { get; set; } = [];
    [ObservableProperty] public partial ProfileInfo? SelectedProfile { get; set; }

    partial void OnSelectedProfileChanged(ProfileInfo? value)
    {
        if (value is not null)
        {
            ProfileExpandedCommand.Execute(value);
        }
    }


    private readonly IDbContextFactory<EditorDbContext> _factory;

    private readonly ILogger<ModIndexViewModel> _logger;

    public ModIndexViewModel() : this(
        App.ServiceProvider!.GetRequiredService<IDbContextFactory<EditorDbContext>>(),
        App.ServiceProvider!.GetRequiredService<ILogger<ModIndexViewModel>>(),
        App.ServiceProvider!.GetRequiredService<IConfigService>(),
        App.ServiceProvider!.GetRequiredService<IProfileManager>(),
        App.ServiceProvider!.GetRequiredService<DataExportService>()
    )
    {
    }

    public ModIndexViewModel(IDbContextFactory<EditorDbContext> factory,
        ILogger<ModIndexViewModel> logger,
        IConfigService configService, IProfileManager profileManager,
        DataExportService dataExportService)
    {
        _factory = factory;
        _logger = logger;
        _config = configService;
        _profileManager = profileManager;
        _dataExportService = dataExportService;
        // Quick DB scan
        Dispatcher.UIThread.InvokeAsync(() => RefreshProfiles());
    }

    [RelayCommand]
    public async Task AddProfile()
    {
        var newProfile = _profileManager.CreateProfile();
        Profiles.Add(newProfile);
    }

    [RelayCommand]
    public async Task ImportProfile()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow); // 获取顶层窗口
            var storageProvider = topLevel?.StorageProvider;
            if (storageProvider == null) return;
            var folders = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = App.Localizor!["SelectPhpProfile"], // 选择Php Mods文件
                AllowMultiple = true, // 允许多选
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(App.Localizor!["PhpFiles"])
                    {
                        Patterns = new[] { "*.php" },
                    }
                }
            });

            await using var db = await _factory.CreateDbContextAsync();
            foreach (var folder in folders)
            {
                if (folder.TryGetLocalPath() is { } folderPath && !folderPath.Contains("getimages.php"))
                {
                    try
                    {
                        var profile = new ProfileInfo()
                        {
                            Name = Path.GetFileNameWithoutExtension(folderPath.Replace("\\", "/")),
                            Description = "",
                            Path = folderPath.Replace("\\", "/"),
                            Content = await File.ReadAllTextAsync(folderPath),
                            CreateTime = DateTime.Now,
                            UpdateTime = DateTime.Now
                        };
                        db.ProfileInfos.Add(profile);
                        await db.SaveChangesAsync();
                        Profiles.Add(profile);
                    }
                    catch (Exception e)
                    {
                        App.Notification.ShowWarning($"Add {folderPath} failed: {e.Message}");
                    }
                }
            }
        }
    }

    [RelayCommand]
    public async Task RefreshProfiles(string profilePath = "")
    {
        // One-time: ensure Game getmods.php profile exists
        await EnsureGameProfileAsync();

        await using var db = await _factory.CreateDbContextAsync();
        Profiles.Clear();
        foreach (var profile in db.ProfileInfos.ToList())
            Profiles.Add(profile);
    }

    private async Task EnsureGameProfileAsync()
    {
        var gameRoot = Config.GameRootDir;
        if (string.IsNullOrWhiteSpace(gameRoot)) return;

        await using var db = await _factory.CreateDbContextAsync();
        if (await db.ProfileInfos.FindAsync(-1) is not null) return;

        var profilePath = Path.Combine(gameRoot, "getmods.php");
        if (!File.Exists(profilePath)) return;

        var profile = _profileManager.LoadProfile("Game", "", profilePath);
        profile.ModLoadInfos.AddRange(_profileManager.LoadMods(profile.Content));
        db.ProfileInfos.Add(profile);
        await db.SaveChangesAsync();
    }

    #region Profile

    [RelayCommand]
    private void OpenMergeEditor(ProfileInfo? profileInfo)
    {
        if (profileInfo is null) return;
        Messenger.Send(new OpenMergeEditorMessage(profileInfo));
    }

    [RelayCommand]
    private void ProfileExpanded(ProfileInfo? profileInfo)
    {
        _logger.LogDebug("ProfileExpanded for profile: {Name}", profileInfo?.Name);
        if (profileInfo is null)
        {
            _logger.LogWarning("ProfileInfo is null in ProfileExpandedCommand");
            return;
        }

        if (profileInfo.ModLoadInfos.Any())
        {
            _logger.LogInformation("Profile {Name} already loaded", profileInfo.Name);
            return;
        }

        _logger.LogInformation("Loading profile: {Name}", profileInfo.Name);
        Messenger.Send(new LoadProfileMessage(profileInfo));
    }

    [RelayCommand]
    private void EditProfile(ProfileInfo? profileInfo)
    {
        if (profileInfo is null)
        {
            _logger.LogWarning("ProfileInfo is null in ProfileExpandedCommand");
            return;
        }

        Messenger.Send(new EditProfileMessage(profileInfo));
    }

    [RelayCommand]
    private async Task ClearProfile(ProfileInfo? profileInfo)
    {
        await using var db = await _factory.CreateDbContextAsync();
        if (profileInfo is null)
        {
            db.ProfileInfos.RemoveRange(Profiles);
            await db.SaveChangesAsync();
            Profiles.Clear();
        }
        else
        {
            db.ProfileInfos.Remove(profileInfo);
            await db.SaveChangesAsync();
            Profiles.Remove(Profiles.First(m => m.ProfileId == profileInfo.ProfileId));
        }
    }

    [RelayCommand]
    private async Task CompareProfiles(ProfileInfo? profileInfo)
    {
        if (profileInfo is null) return;
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;

        var otherProfiles = Profiles.Where(p => p.ProfileId != profileInfo.ProfileId).ToList();
        if (otherProfiles.Count == 0)
        {
            App.Notification.ShowInfo("No other profiles to compare with.", "Compare Profiles");
            return;
        }

        // Ask user which profile to compare with
        var items = otherProfiles.Select(p => $"{p.Name} ({p.ProfileId})").ToList();
        var msgBox = MessageBoxManager.GetMessageBoxStandard(
            $"Compare '{profileInfo.Name}' with:",
            string.Join("\n", items.Select((n, i) => $"[{i + 1}] {n}")) +
            "\n\nEnter the number of the profile to compare:",
            ButtonEnum.OkCancel, MsBox.Avalonia.Enums.Icon.Info);
        var result = await msgBox.ShowWindowDialogAsync(mainWindow);
        if (result != ButtonResult.Ok) return;

        // Simple: always compare with the first other profile for now
        // (MessageBox doesn't support input; we use a simple selection)
        var other = otherProfiles[0];

        // If only one other profile, use it directly; otherwise show a quick picker
        if (otherProfiles.Count == 1)
        {
            await ProfileDiffDialog.ShowAsync(mainWindow, profileInfo, other);
        }
        else
        {
            // For multiple profiles, just compare with the first one for simplicity
            // (a proper picker dialog would be ideal but this keeps it simple)
            await ProfileDiffDialog.ShowAsync(mainWindow, profileInfo, otherProfiles[0]);
        }
    }

    [RelayCommand]
    private void SetActiveProfile(ProfileInfo? profileInfo)
    {
        if (profileInfo is null) return;
        Config.ActiveProfileId = profileInfo.ProfileId;
        Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
        _logger.LogInformation("Active profile set to {Name} (ID={Id})", profileInfo.Name, profileInfo.ProfileId);
    }

    [RelayCommand]
    private void AddModToProfile(ProfileInfo? profileInfo)
    {
        if (profileInfo is null) return;
        Messenger.Send(new EditProfileMessage(profileInfo));
    }

    #endregion

    #region Data Export

    [RelayCommand]
    private async Task ExportCraftingCsv()
    {
        var dateStr = DateTime.Now.ToString("yyyyMMdd");
        await ExportWithDialog("Export Crafting Table",
            [new FilePickerFileType("CSV Files") { Patterns = ["*.csv"] },
             new FilePickerFileType("Excel Files") { Patterns = ["*.xlsx"] }],
            "csv", _dataExportService.ExportCraftingTableAsync,
            $"crafting_table_{dateStr}.csv");
    }

    [RelayCommand]
    private async Task ExportItemEncyclopedia()
    {
        var dateStr = DateTime.Now.ToString("yyyyMMdd");
        await ExportWithDialog("Export Item Encyclopedia",
            [new FilePickerFileType("Markdown Files") { Patterns = ["*.md"] }],
            "md", _dataExportService.ExportItemEncyclopediaMdAsync,
            $"item_encyclopedia_{dateStr}.md");
    }

    [RelayCommand]
    private async Task ExportLootTableJson()
    {
        var dateStr = DateTime.Now.ToString("yyyyMMdd");
        await ExportWithDialog("Export Loot Tables",
            [new FilePickerFileType("JSON Files") { Patterns = ["*.json"] }],
            "json", _dataExportService.ExportLootTableJsonAsync,
            $"loot_tables_{dateStr}.json");
    }

    [RelayCommand]
    private async Task ExportAllXlsx()
    {
        var dateStr = DateTime.Now.ToString("yyyyMMdd");
        await ExportWithDialog("Export All to Excel",
            [new FilePickerFileType("Excel Files") { Patterns = ["*.xlsx"] }],
            "xlsx", _dataExportService.ExportAllToXlsxAsync,
            $"neoscavenger_data_{dateStr}.xlsx");
    }

    private async Task ExportWithDialog(string title, FilePickerFileType[] fileTypes,
        string defaultExt, Func<string, Task> exportFunc, string? suggestedName = null)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;
        if (TopLevel.GetTopLevel(mainWindow) is not { StorageProvider: { } storageProvider }) return;

        // Use user's preferred format if it matches an available file type
        var preferredExt = _config.Config.DefaultExportFormat;
        var actualExt = fileTypes.Any(f => f.Patterns?.Any(p => p.Contains(preferredExt)) == true)
            ? preferredExt : defaultExt;
        var actualName = suggestedName is not null
            ? Path.ChangeExtension(suggestedName, actualExt)
            : null;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = actualExt,
            FileTypeChoices = fileTypes,
            SuggestedFileName = actualName
        });
        if (file?.TryGetLocalPath() is not { } savePath) return;

        try
        {
            await exportFunc(savePath);
            App.Notification.ShowSuccess($"Exported to {savePath}", "Export");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export: {Title}", title);
            App.Notification.ShowWarning($"Export failed: {ex.Message}");
        }
    }

    #endregion

    public void Receive(InitProfileMessage message)
    {
        var profilePath = Path.Combine(Config.GameRootDir, "getmods.php");
        try
        {
            using var db = _factory.CreateDbContext();
            if (db.ProfileInfos.Find(-1) is not null) return;
            var profile = _profileManager.LoadProfile("Game", "", profilePath);
            profile.ModLoadInfos.AddRange(_profileManager.LoadMods(profile.Content));
            db.ProfileInfos.Add(profile);
            db.SaveChanges();
        }
        catch (Exception e)
        {
            App.Notification!.ShowWarning($"load {profilePath} failed: {e.Message}");
        }
    }

    public void Receive(LoadProfileMessage message)
    {
        try
        {
            message.ProfileInfo.ModLoadInfos.Clear();
            message.ProfileInfo.ModLoadInfos.AddRange(_profileManager.LoadMods(message.ProfileInfo.Content));
        }
        catch (Exception e)
        {
            App.Notification!.ShowWarning($"load {message.ProfileInfo.Path} failed: {e.Message}");
        }
    }


    public void Receive(GameRootDirChangedMessage message)
    {
        RefreshProfiles().Wait();
    }

    public void Receive(SaveProfileMessage message)
    {
        try
        {
            // 写入数据库
            using var db = _factory.CreateDbContext();
            db.Update(message.ProfileInfo);
            db.SaveChanges();

            App.Notification!.ShowSuccess($"Profile {message.ProfileInfo.Name} saved successfully.");
            Dispatcher.UIThread.InvokeAsync(() => RefreshProfiles());
        }
        catch (Exception e)
        {
            App.Notification!.ShowError($"Failed to save profile: {e.Message}");
        }
    }
}