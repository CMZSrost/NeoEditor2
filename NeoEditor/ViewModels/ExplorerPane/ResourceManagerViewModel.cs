using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using NeoEditor.Data.Messages;
using NeoEditor.Services;

namespace NeoEditor.ViewModels.ExplorerPane;

public record FolderEntity(FileSystemInfo Info, ObservableCollection<FolderEntity>? Children = null);

public partial class ResourceManagerViewModel : ViewModelBase, IRecipient<GameRootDirChangedMessage>
{
    private readonly IConfigService _config;
    public AppConfig Config => _config.Config;

    private readonly ILogger<ResourceManagerViewModel> _logger;
    private readonly LocalizationService _localizationService;
    public ObservableCollection<FolderEntity> Folders { get; } = [];

    public ResourceManagerViewModel() : this(
        App.ServiceProvider!.GetRequiredService<ILogger<ResourceManagerViewModel>>(),
        App.ServiceProvider!.GetRequiredService<LocalizationService>(),
        App.ServiceProvider!.GetRequiredService<IConfigService>())
    {
    }

    public ResourceManagerViewModel(ILogger<ResourceManagerViewModel> logger, LocalizationService localizationService,
        IConfigService configService)
    {
        _config = configService;
        _logger = logger;
        _localizationService = localizationService;

        ReloadFolder();
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
    public void ReloadFolder()
    {
        // 遍历目录
        Folders.Clear();
        if (string.IsNullOrWhiteSpace(Config.GameRootDir) || !Directory.Exists(Config.GameRootDir))
        {
            _logger.LogWarning($"Game root directory is not set or does not exist: {Config.GameRootDir}");
            return;
        }

        foreach (var entity in TraverseDirectory(new DirectoryInfo(Config.GameRootDir)))
        {
            Folders.Add(entity);
            _logger.LogDebug($"{entity.Info is FileInfo} {entity.Info.Name}");
        }
    }

    [RelayCommand]
    public void OpenGameFolder(string? suffix = null)
    {
        var openPath = string.IsNullOrWhiteSpace(suffix)
            ? Config.GameRootDir
            : Path.Combine(Config.GameRootDir ?? "", suffix);
        // 打开对应文件夹
        if (!Directory.Exists(Config.GameRootDir) || !Directory.Exists(openPath)) return;
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
            _logger.LogError(ex, $"Failed to open folder: {Config.GameRootDir}");
        }
    }

    public void Receive(GameRootDirChangedMessage message)
    {
        ReloadFolder();
    }
}