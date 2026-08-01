using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using NeoEditor.Data.Messages;
using NeoEditor.Services;
using Serilog;

namespace NeoEditor.ViewModels.ExplorerPane;

public record FolderEntity(FileSystemInfo Info, ObservableCollection<FolderEntity>? Children = null);

public partial class ResourceManagerViewModel : ViewModelBase, IRecipient<GameRootDirChangedMessage>
{
    private readonly IConfigService _config;
    public AppConfig Config => _config.Config;

    private readonly ILogger<ResourceManagerViewModel> _logger;
    private readonly ILocalizationService _localizationService;
    public ObservableCollection<FolderEntity> Folders { get; } = [];
    [ObservableProperty] public partial FolderEntity? SelectedItem { get; set; }

    public ResourceManagerViewModel(ILogger<ResourceManagerViewModel> logger, ILocalizationService localizationService,
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
                    break;
                case DirectoryInfo directoryInfo:
                    var children = TraverseDirectory(directoryInfo);
                    folders.Add(new FolderEntity(directoryInfo, new ObservableCollection<FolderEntity>(children)));
                    _logger.LogDebug("Added directory {Name} with {ChildCount} children", directoryInfo.Name, children.Count);
                    break;
            }
        }

        return new ObservableCollection<FolderEntity>(folders.OrderBy(f => f.Info is FileInfo)
            .ThenBy(f => f.Info.Name));
    }

    [RelayCommand]
    public void OpenFile(FolderEntity? folder = null)
    {
        if (folder is not { Info: FileInfo fileInfo }) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileInfo.FullName,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to open file: {FilePath}", fileInfo.FullName);
        }
    }

    [RelayCommand]
    public void ReloadFolder()
    {
        Folders.Clear();
        if (string.IsNullOrWhiteSpace(Config?.GameRootDir) || !Directory.Exists(Config?.GameRootDir))
        {
            _logger.LogWarning("Game root directory is not set or does not exist: {GameRootDir}", Config?.GameRootDir);
            return;
        }

        foreach (var entity in TraverseDirectory(new DirectoryInfo(Config.GameRootDir)))
            Folders.Add(entity);
    }

    [RelayCommand]
    public void OpenGameFolder(string? suffix = null)
    {
        var openPath = string.IsNullOrWhiteSpace(suffix)
            ? Config.GameRootDir
            : Path.Combine(Config.GameRootDir ?? "", suffix);
        if (!Directory.Exists(Config.GameRootDir) || !Directory.Exists(openPath)) return;
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
            _logger.LogError(ex, "Failed to open folder: {GameRootDir}", Config.GameRootDir);
        }
    }

    [RelayCommand]
    public async Task DeleteItem(FolderEntity? item = null)
    {
        item ??= SelectedItem;
        if (item is null) return;

        var name = item.Info.Name;
        var isDir = item.Info is DirectoryInfo;
        var typeLabel = isDir ? "folder" : "file";

        var box = MessageBoxManager.GetMessageBoxStandard(
            new MessageBoxStandardParams
            {
                ContentTitle = "Confirm Delete",
                ContentMessage = $"Delete {typeLabel} '{name}'?\nThis action cannot be undone.",
                ButtonDefinitions = ButtonEnum.YesNo,
                Icon = Icon.Warning
            });

        var result = await box.ShowAsync();
        if (result != ButtonResult.Yes) return;

        try
        {
            if (isDir)
                Directory.Delete(item.Info.FullName, recursive: true);
            else
                File.Delete(item.Info.FullName);

            _logger.LogInformation("Deleted {Path}", item.Info.FullName);
            ReloadFolder();
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to delete: {Path}", item.Info.FullName);
        }
    }

    /// <summary>Set by the view to show a rename dialog. Returns null if cancelled, otherwise the new name.</summary>
    public static Func<string, Task<string?>>? RenameDialogRequested;

    [RelayCommand]
    public async Task RenameItem(FolderEntity? item = null)
    {
        item ??= SelectedItem;
        if (item is null) return;

        if (RenameDialogRequested is not null)
        {
            var newName = await RenameDialogRequested(item.Info.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == item.Info.Name) return;

            var dir = Path.GetDirectoryName(item.Info.FullName)!;
            var newPath = Path.Combine(dir, newName);
            try
            {
                if (item.Info is DirectoryInfo)
                    Directory.Move(item.Info.FullName, newPath);
                else
                    File.Move(item.Info.FullName, newPath);
                _logger.LogInformation("Renamed {Old} → {New}", item.Info.FullName, newPath);
                ReloadFolder();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Failed to rename: {Path}", item.Info.FullName);
            }
        }
        else
        {
            // Fallback: open parent folder
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Path.GetDirectoryName(item.Info.FullName)!,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Failed to open parent folder: {Path}", item.Info.FullName);
            }
        }
    }

    [RelayCommand]
    public async Task CopyPath(FolderEntity? item = null)
    {
        item ??= SelectedItem;
        if (item is null) return;

        var path = item.Info.FullName;

        // Use TopLevel to access clipboard in Avalonia
        if (Avalonia.Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is { } window)
        {
            var clipboard = window.Clipboard;
            if (clipboard is not null)
                await clipboard.SetTextAsync(path);
        }
    }

    [RelayCommand]
    public void OpenInExplorer(FolderEntity? item = null)
    {
        item ??= SelectedItem;
        if (item is null) return;

        var path = item.Info.FullName;
        try
        {
            if (item.Info is DirectoryInfo)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            else
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = false
                });
            }
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Failed to open in explorer: {Path}", path);
        }
    }

    public void Receive(GameRootDirChangedMessage message)
    {
        ReloadFolder();
    }
}
