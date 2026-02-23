using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ConfigurationManager = System.Configuration.ConfigurationManager;

namespace NeoEditor.ViewModels;

public record FolderEntity(FileSystemInfo Info, ObservableCollection<FolderEntity>? Children = null);

public partial class ExplorerPaneViewModel : ViewModelBase
{
    private readonly ILogger<ExplorerPaneViewModel> _logger;
    [ObservableProperty] public partial string? GameRootDir { get; set; }
    public ObservableCollection<FolderEntity> Folders { get; } = [];

    public ExplorerPaneViewModel() : this(App.ServiceProvider!.GetRequiredService<ILogger<ExplorerPaneViewModel>>())
    {
    }

    public ExplorerPaneViewModel(ILogger<ExplorerPaneViewModel> logger)
    {
        _logger = logger;

        var rootDir = ConfigurationManager.AppSettings["ProjectSettings:GameRootDir"];
        if (!string.IsNullOrWhiteSpace(rootDir) && Directory.Exists(rootDir))
        {
            // 遍历目录
            GameRootDir = rootDir;
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
                Title = "选择游戏根目录",
                AllowMultiple = false, // 允许多选
            });

            foreach (var folder in folders)
            {
                var folderPath = folder.TryGetLocalPath();
                if (folderPath != null)
                {
                    GameRootDir = folderPath;
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
                        if (section["ProjectSettings:GameRootDir"] is not { } setting)
                        {
                            section.Add("ProjectSettings:GameRootDir", GameRootDir);
                        }
                        else
                        {
                            setting.Value = GameRootDir;
                        }

                        configuration.Save(ConfigurationSaveMode.Modified);
                        ConfigurationManager.RefreshSection("appSettings");
                    }
                }

                _logger.LogInformation($"Selected folder: {GameRootDir}");
                Folders.Clear();

                foreach (var entity in TraverseDirectory(new DirectoryInfo(GameRootDir)))
                {
                    Folders.Add(entity);
                    _logger.LogDebug($"{entity.Info is FileInfo} {entity.Info.Name}");
                }

                return;
            }

            _logger.LogWarning("Failed to get local path for selected folder.");
        }
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

public class ModsIndexViewModel : ViewModelBase
{
    public ObservableCollection<string> RecentSearches { get; } = [];
}

public class SearchPaneViewModel : ViewModelBase
{
    public ObservableCollection<string> RecentSearches { get; } = [];
}

public class SettingsPaneViewModel : ViewModelBase
{
    // 设置选项
}