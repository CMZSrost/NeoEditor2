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
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Helper;

namespace NeoEditor.ViewModels.ExplorerPane;

public partial class ModIndexViewModel : ViewModelBase, IRecipient<InitProfileMessage>, IRecipient<LoadProfileMessage>,
    IRecipient<SetGameFolderMessage>
{
    [ObservableProperty] public partial string? GameRootDir { get; set; }
    [ObservableProperty] public partial ModIndexInfo? Info { get; set; }
    public ObservableCollection<ProfileInfo> Profiles { get; set; } = [];

    private readonly PhpParser _phpParser;

    private readonly IDbContextFactory<EditorDbContext> _factory;

    private readonly IMapper _mapper;

    public ModIndexViewModel() : this(
        App.ServiceProvider!.GetRequiredService<PhpParser>(),
        App.ServiceProvider!.GetRequiredService<IDbContextFactory<EditorDbContext>>(),
        App.ServiceProvider!.GetRequiredService<IMapper>()
    )
    {
    }

    public ModIndexViewModel(PhpParser phpParser, IDbContextFactory<EditorDbContext> factory, IMapper mapper)
    {
        _phpParser = phpParser;
        _factory = factory;
        _mapper = mapper;
        GameRootDir = ConfigurationManager.AppSettings[Constants.ProjectSettingsGameRootDir];
        if (Design.IsDesignMode)
        {
            GameRootDir = "D:\\software\\Steam\\steamapps\\common\\Neo Scavenger";
        }

        Dispatcher.UIThread.InvokeAsync(() => RefreshProfiles());
    }

    public void Receive(InitProfileMessage message)
    {
        try
        {
            var entities = _phpParser.Parse(message.FilePath);
            using var db = _factory.CreateDbContext();
            var existedMods = db.ModInfos.ToDictionary(m => m.Path, m => m);

            Info = new ModIndexInfo
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
            var entities = _phpParser.ParseContent(message.ProfileInfo.Content.Replace("\r\n", ""));
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

    public void Receive(SetGameFolderMessage message)
    {
        GameRootDir = message.GameRootDir;
        Console.WriteLine($"ModDatabase received game folder: {GameRootDir}");
    }

    [RelayCommand]
    public async Task AddProfile()
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

    [RelayCommand]
    public async Task LoadProfiles(ProfileInfo? profileInfo = null)
    {
        if (string.IsNullOrWhiteSpace(GameRootDir))
        {
            App.Notification!.ShowWarning("Game root directory is not set. Please set it before loading mods.",
                "Load Warning");
            return;
        }

        Messenger.Send(profileInfo is null
            ? new InitProfileMessage(Path.Combine(GameRootDir, "getmods.php"))
            : new InitProfileMessage(profileInfo.Path));
    }
}