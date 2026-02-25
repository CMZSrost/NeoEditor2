using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using NeoEditor.Data;
using NeoEditor.Data.Messages;
using NeoEditor.Services;
using ConfigurationManager = System.Configuration.ConfigurationManager;

namespace NeoEditor.ViewModels.ExplorerPane;

public record FolderEntity(FileSystemInfo Info, ObservableCollection<FolderEntity>? Children = null);

public partial class ResourceManagerViewModel : ViewModelBase
{
    private readonly ILogger<ResourceManagerViewModel> _logger;
    private readonly LocalizationService _localizationService;
    [ObservableProperty] public partial string? GameRootDir { get; set; }
    public ObservableCollection<FolderEntity> Folders { get; } = [];

    public ResourceManagerViewModel() : this(
        App.ServiceProvider!.GetRequiredService<ILogger<ResourceManagerViewModel>>(),
        App.ServiceProvider!.GetRequiredService<LocalizationService>())
    {
    }

    public ResourceManagerViewModel(ILogger<ResourceManagerViewModel> logger, LocalizationService localizationService)
    {
        _logger = logger;
        _localizationService = localizationService;

        var rootDir = ConfigurationManager.AppSettings[Constants.ProjectSettingsGameRootDir];

        if (Design.IsDesignMode)
        {
            rootDir = "D:\\software\\Steam\\steamapps\\common\\Neo Scavenger";
        }

        if (string.IsNullOrWhiteSpace(rootDir) || !Directory.Exists(rootDir))
        {
            if (Design.IsDesignMode) return;
            // 如果配置无效，弹窗提示用户配置根目录，如果用户选择配置根目录，则打开文件夹选择器，否则关闭应用
            _logger.LogWarning("GameRootDir is not set or does not exist. Please set it in the settings.");
            Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var result = await MessageBoxManager.GetMessageBoxStandard(_localizationService["Error"],
                        _localizationService["GameRootDirNotSet"], ButtonEnum.YesNo)
                    .ShowWindowAsync();
                if (result == ButtonResult.Yes)
                {
                    await SetFolder();
                }
                else if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
                else
                {
                    throw new InvalidOperationException(
                        "Unsupported application lifetime. Cannot shutdown application.");
                }
            });
        }
        else
        {
            // 遍历目录
            GameRootDir = rootDir;
            Messenger.Send(new SetGameFolderMessage(GameRootDir ?? ""));
            Folders.Clear();
            foreach (var entity in TraverseDirectory(new DirectoryInfo(GameRootDir)))
            {
                Folders.Add(entity);
                _logger.LogDebug($"{entity.Info is FileInfo} {entity.Info.Name}");
            }
        }
    }

    private ObservableCollection<FolderEntity> TraverseDirectory(DirectoryInfo dirInfo)
    {
        List<FolderEntity> folders = [];
        foreach (var fInfo in dirInfo.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
        {
            switch (fInfo)
            {
                case FileInfo fileInfo:
                    folders.Add(new FolderEntity(fileInfo));
                    // _logger.LogDebug($"Added {fileInfo.Name}");
                    break;
                case DirectoryInfo directoryInfo:
                    // 递归遍历子目录
                    var children = TraverseDirectory(directoryInfo);
                    folders.Add(new FolderEntity(directoryInfo,
                        new ObservableCollection<FolderEntity>(
                            children)));
                    _logger.LogDebug($"Added {directoryInfo.Name} {children.Count}");
                    break;
            }
        }

        return new ObservableCollection<FolderEntity>(folders.OrderBy(f => f.Info is FileInfo)
            .ThenBy(f => f.Info.Name));
    }

    [RelayCommand]
    public async Task SetFolder()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow); // 获取顶层窗口
            var storageProvider = topLevel?.StorageProvider;
            if (storageProvider == null) return;
            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                Title = App.Localizor!["SelectGameRootDir"],
                AllowMultiple = false
            });

            foreach (var folder in folders)
            {
                var folderPath = folder.TryGetLocalPath();
                if (folderPath != null)
                {
                    _logger.LogInformation($"Selected folder: {folderPath}");
                    GameRootDir = folderPath;
                    Messenger.Send(new SetGameFolderMessage(GameRootDir));
                    var configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    if (configuration is null)
                    {
                        _logger.LogWarning("Failed to get configuration for updating GameRootDir.");
                    }
                    else if (configuration.AppSettings.Settings is not { } section)
                    {
                        _logger.LogWarning("Failed to get section for updating GameRootDir.");
                    }
                    else
                    {
                        if (section[Constants.ProjectSettingsGameRootDir] is not { } setting)
                        {
                            section.Add(Constants.ProjectSettingsGameRootDir, GameRootDir);
                        }
                        else
                        {
                            setting.Value = GameRootDir;
                        }

                        configuration.Save(ConfigurationSaveMode.Modified);
                        ConfigurationManager.RefreshSection(Constants.AppSettingsSection);
                    }

                    Folders.Clear();

                    foreach (var entity in TraverseDirectory(new DirectoryInfo(GameRootDir)))
                    {
                        Folders.Add(entity);
                    }

                    return;
                }
            }
        }

        _logger.LogWarning("Failed to get local path for selected folder.");
    }


    [RelayCommand]
    public void OpenGameFolder(string? suffix)
    {
        var openPath = string.IsNullOrWhiteSpace(suffix) ? GameRootDir : Path.Combine(GameRootDir ?? "", suffix);
        // 打开对应文件夹
        if (!Directory.Exists(GameRootDir) || !Directory.Exists(openPath)) return;
        // 这里可以使用系统默认的文件资源管理器打开目录
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = openPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to open folder: {GameRootDir}");
        }
    }
}