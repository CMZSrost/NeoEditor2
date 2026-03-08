using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.DesignerSupport.Remote.HtmlTransport;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Xaml.Interactions.Events;
using AvaloniaEdit.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Messages;
using NeoEditor.Data.Model;
using NeoEditor.Services;
using NeoEditor.ViewModels.Dialog;
using NeoEditor.Views;
using NeoEditor.Views.Dialog;
using Newtonsoft.Json;

namespace NeoEditor.ViewModels.ExplorerPane;

public partial class ModDatabaseViewModel : ViewModelBase, IRecipient<GameRootDirChangedMessage>,
    IRecipient<InitModMessage>, IRecipient<RefreshModMessage>
{
    private readonly IConfigService _config;
    public AppConfig Config => _config.Config;

    private ProjectDbContextFactory _gameContextFactory;
    private readonly IDbContextFactory<EditorDbContext> _editorContextFactory;
    private readonly EditorDbContext _editorDbContext;
    private readonly IModManager _modManager;

    private readonly ILogger<ModDatabaseViewModel> _logger;
    public ObservableCollection<ModInfo> Mods { get; set; } = [];

    [ObservableProperty] public partial string Filter { get; set; } = "";
    [ObservableProperty] public partial ModInfo? SelectedItem { get; set; }

    public ModDatabaseViewModel() : this(
        App.ServiceProvider!.GetRequiredService<ProjectDbContextFactory>(),
        App.ServiceProvider!.GetRequiredService<ILogger<ModDatabaseViewModel>>(),
        App.ServiceProvider!.GetRequiredService<IDbContextFactory<EditorDbContext>>(),
        App.ServiceProvider!.GetRequiredService<EditorDbContext>(),
        App.ServiceProvider!.GetRequiredService<IModManager>(),
        App.ServiceProvider!.GetRequiredService<IConfigService>()
    )
    {
    }

    public ModDatabaseViewModel(ProjectDbContextFactory gameContextFactory, ILogger<ModDatabaseViewModel> logger,
        IDbContextFactory<EditorDbContext> editorContextFactory, EditorDbContext editorDbContext,
        IModManager modManager,
        IConfigService configService)
    {
        _config = configService;
        _gameContextFactory = gameContextFactory;
        _logger = logger;
        _editorContextFactory = editorContextFactory;
        _editorDbContext = editorDbContext;
        _modManager = modManager;

        Messenger.Send(new InitModMessage());
        Dispatcher.UIThread.InvokeAsync(() => RefreshMod());
    }

    [RelayCommand]
    public async Task CreateMod()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } mainWindow
            }) return;
        if (TopLevel.GetTopLevel(mainWindow) is not { StorageProvider: { } storageProvider }) return;

        var dialog = App.ServiceProvider.GetRequiredService<CreateModDialog>();

        await dialog.ShowDialog<ButtonResult>(mainWindow);
    }

    [RelayCommand]
    public async Task ImportMod()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return;
        if (TopLevel.GetTopLevel(desktop.MainWindow) is not { StorageProvider: { } storageProvider }) return;
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
        {
            Title = App.Localizor!["SelectModFolder"], // 选择Mod文件夹
            AllowMultiple = true, // 允许多选
        });


        foreach (var folder in folders)
        {
            if (folder.TryGetLocalPath() is not { } folderPath) continue;
            if (Mods!.Any(m => m.Path == folderPath))
            {
                App.Notification!.ShowInfo($"Folder already imported: {folderPath}, skipping.");
                return;
            }

            await _modManager.ImportModAsync(folderPath);
        }

        Mods.Clear();
        await using var dbContext = await _editorContextFactory.CreateDbContextAsync();
        Mods.AddRange(dbContext.ModInfos.ToList());
    }

    [RelayCommand]
    public async Task RefreshMod(ModInfo? selectedItem = null)
    {
        Mods.Clear();
        Mods.AddRange(await _editorDbContext.ModInfos.ToListAsync());
        // App.Notification.ShowSuccess(Loc["ReloadModsSuccess"], Loc["ReloadModsSuccessMessage"]);
    }

    [RelayCommand]
    private async Task LoadMod(ModInfo? selectedItem = null)
    {
        await using var db = await _editorContextFactory.CreateDbContextAsync();
        if (selectedItem is null)
        {
            foreach (var modInfo in Mods)
            {
                await _modManager.LoadModAsync(modInfo);
                modInfo.LastImport = DateTime.Now;
                db.ModInfos.Update(modInfo);
            }

            await db.SaveChangesAsync();
        }
        else
        {
            await _modManager.LoadModAsync(selectedItem);
            selectedItem.LastImport = DateTime.Now;
            db.ModInfos.Update(selectedItem);
            await db.SaveChangesAsync();
        }

        App.Notification.ShowSuccess(Loc["ReloadModsSuccess"], Loc["ReloadModsSuccessMessage"]);
    }

    [RelayCommand]
    private void ShowData(ModInfo? selectedItem = null)
    {
        if (selectedItem is null)
        {
            _logger.LogWarning("ModInfo is null in ShowDataCommand");
            return;
        }

        Messenger.Send(new OpenModGameDataDocumentMessage(selectedItem));
    }

    [RelayCommand]
    private async Task OpenModPath(ModInfo? selectedItem = null)
    {
        if (App.Current is not
            { ApplicationLifetime: IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow } }) return;
        if (selectedItem is null) return;
        var modDirectory = ResolveModDirectory(selectedItem.Path);
        if (!Directory.Exists(modDirectory))
        {
            App.Notification.ShowWarning(Loc["ModDirectoryNotFoundMessage"], Loc["ModDirectoryNotFound"]);
        }
        else
        {
            try
            {
                await Task.Run(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = modDirectory,
                    UseShellExecute = true,
                }));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to open mod directory: {ModDirectory}", modDirectory);
                App.Notification.ShowWarning($"Failed to open mod directory: {e.Message}");
            }
        }
    }

    [RelayCommand]
    private async Task ClearMods(ModInfo? selectedItem = null)
    {
        await using var db = await _editorContextFactory.CreateDbContextAsync();
        if (selectedItem is null)
        {
            db.ModInfos.RemoveRange(Mods);
            await db.SaveChangesAsync();
            Mods.Clear();
        }
        else if (selectedItem.IsBase)
        {
            App.Notification.ShowWarning(Loc["BaseModCannotBeDeletedMessage"], Loc["BaseModCannotBeDeleted"]);
        }

        {
            db.ModInfos.Remove(selectedItem);
            await db.SaveChangesAsync();
            Mods.Remove(Mods.First(m => m.ModId == selectedItem.ModId));
        }
        await RefreshMod();
    }

    [RelayCommand]
    public async Task DeleteMods(ModInfo? selectedItem = null)
    {
        if (Application.Current is not
            { ApplicationLifetime: IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow } }) return;

        if (selectedItem is null)
        {
            var msgBox = MessageBoxManager.GetMessageBoxStandard(Loc["DeleteAllModsConfirmMessage"],
                Loc["DeleteAllModsConfirmMessageDetail"], ButtonEnum.YesNo, Icon.Warning);
            var result = await msgBox.ShowWindowDialogAsync(mainWindow);
            if (result != ButtonResult.Yes) return;
            foreach (var modInfo in Mods)
            {
                await _modManager.DeleteMod(modInfo);
            }
        }
        else
        {
            var msgBox = MessageBoxManager.GetMessageBoxStandard(Loc["DeleteModsConfirmMessage"],
                Loc["DeleteModsConfirmMessageDetail"], ButtonEnum.YesNo, Icon.Warning);
            var result = await msgBox.ShowWindowDialogAsync(mainWindow);
            if (result != ButtonResult.Yes) return;
            await _modManager.DeleteMod(selectedItem);
        }

        await RefreshMod();
    }

    public void Receive(InitModMessage message)
    {
        var gamePath = Path.Combine(Config.GameRootDir, "data");
        try
        {
            using var db = _editorContextFactory.CreateDbContext();
            if (db.ModInfos.Find(-1) is not null) return;
            var mod = new ModInfo
            {
                ModId = -1,
                Name = "Game",
                Path = "data",
                IsBase = true,
                LastImport = DateTime.Now,
                LastModified = DateTime.Now,
            };
            db.ModInfos.Add(mod);
            db.SaveChanges();
            // Mods.Add(mod);
        }
        catch (Exception e)
        {
            App.Notification!.ShowWarning($"load {gamePath} failed: {e.Message}");
        }
    }

    public void Receive(GameRootDirChangedMessage message)
    {
        Dispatcher.UIThread.InvokeAsync(() => RefreshMod());
    }

    public void Receive(RefreshModMessage message)
    {
        Dispatcher.UIThread.InvokeAsync(() => RefreshMod());
    }

    [RelayCommand]
    private void ModExpanded(ModInfo? modInfo)
    {
        if (modInfo is null)
        {
            _logger.LogWarning("ModInfo is null in ModExpandedCommand");
            return;
        }

        if (modInfo.XmlFilePathsLoaded)
        {
            _logger.LogDebug("Xml paths for mod {ModName} already loaded", modInfo.Name);
            return;
        }

        try
        {
            var modDirectory = ResolveModDirectory(modInfo.Path);
            modInfo.XmlFilePaths.Clear();

            if (!Directory.Exists(modDirectory))
            {
                _logger.LogWarning("Mod directory not found when expanding: {ModDirectory}", modDirectory);
                modInfo.XmlFilePathsLoaded = true;
                return;
            }

            var xmlFilePaths = Directory
                .GetFiles(modDirectory, "*.xml", SearchOption.AllDirectories)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .Select(s => ToDisplayPath(s, modInfo.Path))
                .ToList();

            modInfo.XmlFilePaths.AddRange(xmlFilePaths);
            modInfo.XmlFilePathsLoaded = true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load xml files for mod {ModName}", modInfo.Name);
            App.Notification.ShowWarning($"load xml files for {modInfo.Name} failed: {e.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenXml(TappedEventArgs args)
    {
        if (args.Source is not Control { DataContext: string xmlPath } control)
            return;
        if (control.FindAncestorOfType<Expander>() is not { DataContext: ModInfo modInfo })
            return;


        if (string.IsNullOrWhiteSpace(xmlPath) || string.IsNullOrWhiteSpace(modInfo.Path))
        {
            _logger.LogWarning("Xml path is null or whitespace for mod {ModName}", modInfo.Name);
            App.Notification.ShowWarning($"open xml failed: empty path");
            return;
        }

        if (!TryResolveXmlPath(xmlPath, modInfo, out var absoluteXmlPath, out var title))
        {
            _logger.LogWarning("Failed to resolve xml path for display path {XmlPath}", xmlPath);
            App.Notification.ShowWarning($"open xml failed: {xmlPath}");
            return;
        }

        Messenger.Send(new OpenXmlDocumentMessage(absoluteXmlPath, title));
        await Task.CompletedTask;
    }

    private bool TryResolveXmlPath(string xmlPath, ModInfo mod, out string absoluteXmlPath, out string title)
    {
        absoluteXmlPath = string.Empty;
        title = xmlPath.Replace("\\", "/");

        var modDirectory = ResolveModDirectory(mod.Path);
        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            return false;
        }

        var candidatePath = Path.Combine(modDirectory,
            xmlPath.Replace('\\', '/'));
        if (!File.Exists(candidatePath))
        {
            return false;
        }

        absoluteXmlPath = candidatePath;
        title = $"{mod.Name}/{xmlPath.Replace("\\", "/")}";
        return true;
    }

    private string ResolveModDirectory(string modPath)
    {
        if (string.IsNullOrWhiteSpace(modPath))
        {
            return string.Empty;
        }

        return Path.IsPathRooted(modPath)
            ? modPath
            : Path.Combine(Config.GameRootDir, modPath);
    }

    private string ToDisplayPath(string xmlPath, string modPath)
    {
        if (!string.IsNullOrWhiteSpace(Config.GameRootDir))
        {
            try
            {
                return Path.GetRelativePath(Config.GameRootDir, xmlPath).Replace("\\", "/").Replace(modPath, "")
                    .TrimStart('/');
            }
            catch (Exception e)
            {
                _logger.LogDebug(e, "Failed to convert xml path {XmlPath} to relative path", xmlPath);
            }
        }

        return xmlPath.Replace("\\", "/");
    }
}