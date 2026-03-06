using System;
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

    private readonly PhpParser _phpParser;

    private readonly IDbContextFactory<EditorDbContext> _factory;

    private readonly IMapper _mapper;
    private readonly ILogger<ModIndexViewModel> _logger;

    public ModIndexViewModel() : this(
        App.ServiceProvider!.GetRequiredService<PhpParser>(),
        App.ServiceProvider!.GetRequiredService<IDbContextFactory<EditorDbContext>>(),
        App.ServiceProvider!.GetRequiredService<IMapper>(),
        App.ServiceProvider!.GetRequiredService<ILogger<ModIndexViewModel>>(),
        App.ServiceProvider!.GetRequiredService<IConfigService>(),
        App.ServiceProvider!.GetRequiredService<IProfileManager>()
    )
    {
    }

    public ModIndexViewModel(PhpParser phpParser, IDbContextFactory<EditorDbContext> factory, IMapper mapper,
        ILogger<ModIndexViewModel> logger,
        IConfigService configService, IProfileManager profileManager)
    {
        _phpParser = phpParser;
        _factory = factory;
        _mapper = mapper;
        _logger = logger;
        _config = configService;
        _profileManager = profileManager;

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
        await LoadProfiles();
        await using var db = await _factory.CreateDbContextAsync();
        Profiles.Clear();
        foreach (var profile in db.ProfileInfos.ToList())
        {
            Profiles.Add(profile);
        }
    }

    // [RelayCommand]
    public async Task LoadProfiles(ProfileInfo? profileInfo = null)
    {
        if (string.IsNullOrWhiteSpace(Config.GameRootDir))
        {
            App.Notification!.ShowWarning("Game root directory is not set. Please set it before loading mods.",
                "Load Warning");
            return;
        }

        Messenger.Send(profileInfo is null
            ? new InitProfileMessage(Path.Combine(Config.GameRootDir, "getmods.php"))
            : new InitProfileMessage(profileInfo.Path));
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

        if (profileInfo.ModIndexInfo is not null)
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
            db.ProfileInfos.Local.Clear();
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
        try
        {
            var content = File.ReadAllText(message.FilePath).ReplaceLineEndings("");
            var entities = _phpParser.ParseContent(content);
            using var db = _factory.CreateDbContext();
            var existedMods = db.ModInfos.ToDictionary(m => m.Path, m => m);

            Info = new ProfileInfo()
            {
                ProfileId = -1,
                Name = "Game",
                Path = message.FilePath,
                Content = content,
                ModIndexInfo = new ModIndexInfo
                {
                    FilePath = message.FilePath,
                    Mods = entities.Select(entry => new ModLoadInfo()
                    {
                        Type = existedMods.ContainsKey(entry.Path) ? entry.Type : ModType.Unknown,
                        Info = existedMods.ContainsKey(entry.Path) switch
                        {
                            true => existedMods[entry.Path],
                            false => new ModInfo()
                            {
                                Name = entry.Name,
                                Path = entry.Path,
                                IsBase = false,
                                LastImport = DateTime.Now,
                                LastModified = DateTime.Now
                            }
                        }
                    }).ToList()
                },
                CreateTime = DateTime.Now,
                UpdateTime = DateTime.Now
            };
        }
        catch (Exception e)
        {
            App.Notification!.ShowWarning($"load {message.FilePath} failed: {e.Message}");
        }
    }

    public void Receive(LoadProfileMessage message)
    {
        try
        {
            var entities = _phpParser.ParseContent(message.ProfileInfo.Content.ReplaceLineEndings(""));
            using var db = _factory.CreateDbContext();
            var existedMods = db.ModInfos.ToDictionary(m => m.Path, m => m);

            message.ProfileInfo.ModIndexInfo = new ModIndexInfo
            {
                FilePath = message.ProfileInfo.Path,
                Mods = entities.Select(entry => new ModLoadInfo()
                {
                    Type = existedMods.ContainsKey(entry.Path) ? entry.Type : ModType.Unknown,
                    Info = existedMods.ContainsKey(entry.Path) switch
                    {
                        true => existedMods[entry.Path],
                        false => new ModInfo()
                        {
                            Name = entry.Name,
                            Path = entry.Path,
                            IsBase = false,
                            LastImport = DateTime.Now,
                            LastModified = DateTime.Now
                        }
                    }
                }).ToList()
            };
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