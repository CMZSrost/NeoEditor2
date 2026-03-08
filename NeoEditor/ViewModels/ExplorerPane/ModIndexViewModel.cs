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
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Helper;
using NeoEditor.Services;
using Newtonsoft.Json;

namespace NeoEditor.ViewModels.ExplorerPane;

public partial class ModIndexViewModel : ViewModelBase, IRecipient<InitProfileMessage>, IRecipient<LoadProfileMessage>,
    IRecipient<SaveProfileMessage>,
    IRecipient<GameRootDirChangedMessage>
{
    private readonly IConfigService _config;
    private readonly IProfileManager _profileManager;
    public AppConfig Config => _config.Config;
    [ObservableProperty] public partial ProfileInfo? Info { get; set; }
    public ObservableCollection<ProfileInfo> Profiles { get; set; } = [];
    [ObservableProperty] public partial ProfileInfo? SelectedProfile { get; set; }


    private readonly IDbContextFactory<EditorDbContext> _factory;

    private readonly ILogger<ModIndexViewModel> _logger;

    public ModIndexViewModel() : this(
        App.ServiceProvider!.GetRequiredService<IDbContextFactory<EditorDbContext>>(),
        App.ServiceProvider!.GetRequiredService<ILogger<ModIndexViewModel>>(),
        App.ServiceProvider!.GetRequiredService<IConfigService>(),
        App.ServiceProvider!.GetRequiredService<IProfileManager>()
    )
    {
    }

    public ModIndexViewModel(IDbContextFactory<EditorDbContext> factory,
        ILogger<ModIndexViewModel> logger,
        IConfigService configService, IProfileManager profileManager)
    {
        _factory = factory;
        _logger = logger;
        _config = configService;
        _profileManager = profileManager;

        Messenger.Send(new InitProfileMessage());
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
        await using var db = await _factory.CreateDbContextAsync();
        Profiles.Clear();
        foreach (var profile in db.ProfileInfos.ToList())
        {
            Profiles.Add(profile);
        }
    }

    #region Profile

    [RelayCommand]
    private void ProfileExpanded(ProfileInfo? profileInfo)
    {
        Console.WriteLine($"ProfileExpandedCommand executed for profile: {profileInfo?.Name}");
        if (profileInfo is null)
        {
            _logger.LogWarning("ProfileInfo is null in ProfileExpandedCommand");
            return;
        }

        if (profileInfo.ModLoadInfos.Any())
        {
            _logger.LogInformation($"Profile {profileInfo.Name} already loaded");
            return;
        }

        _logger.LogInformation($"Loading profile: {profileInfo.Name}");
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