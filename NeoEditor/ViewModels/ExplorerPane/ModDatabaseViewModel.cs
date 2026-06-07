using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
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
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
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
    private readonly PhpParser _phpParser = new();
    private readonly CsvImportExportService _csvService;
    private readonly DataExportService _dataExportService;
    public AppConfig Config => _config.Config;

    private ProjectDbContextFactory _gameContextFactory;
    private readonly IDbContextFactory<EditorDbContext> _editorContextFactory;
    private readonly EditorDbContext _editorDbContext;
    private readonly IModManager _modManager;

    private readonly ILogger<ModDatabaseViewModel> _logger;
    public ObservableCollection<ModInfo> Mods { get; set; } = [];
    public IRelayCommand<ModInfo?> ShowImageMenuCommand { get; }

    [ObservableProperty] public partial string Filter { get; set; } = "";
    [ObservableProperty] public partial ModInfo? SelectedItem { get; set; }

    public ModDatabaseViewModel() : this(
        App.ServiceProvider!.GetRequiredService<ProjectDbContextFactory>(),
        App.ServiceProvider!.GetRequiredService<ILogger<ModDatabaseViewModel>>(),
        App.ServiceProvider!.GetRequiredService<IDbContextFactory<EditorDbContext>>(),
        App.ServiceProvider!.GetRequiredService<EditorDbContext>(),
        App.ServiceProvider!.GetRequiredService<IModManager>(),
        App.ServiceProvider!.GetRequiredService<IConfigService>(),
        App.ServiceProvider!.GetRequiredService<CsvImportExportService>(),
        App.ServiceProvider!.GetRequiredService<DataExportService>()
    )
    {
    }

    public ModDatabaseViewModel(ProjectDbContextFactory gameContextFactory, ILogger<ModDatabaseViewModel> logger,
        IDbContextFactory<EditorDbContext> editorContextFactory, EditorDbContext editorDbContext,
        IModManager modManager,
        IConfigService configService,
        CsvImportExportService csvService,
        DataExportService dataExportService)
    {
        _config = configService;
        _gameContextFactory = gameContextFactory;
        _logger = logger;
        _editorContextFactory = editorContextFactory;
        _editorDbContext = editorDbContext;
        _modManager = modManager;
        _csvService = csvService;
        _dataExportService = dataExportService;
        ShowImageMenuCommand = new RelayCommand<ModInfo?>(ShowImage);
        // Quick DB scan — no XML parsing
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

            var modInfo = await _modManager.ImportModAsync(folderPath);
            if (modInfo is not null)
                Messenger.Send(new OpenModGameDataDocumentMessage(modInfo));
        }

        Mods.Clear();
        await using var dbContext = await _editorContextFactory.CreateDbContextAsync();
        Mods.AddRange(dbContext.ModInfos.ToList());
    }

    [RelayCommand]
    public async Task RefreshMod(ModInfo? selectedItem = null)
    {
        // One-time: ensure Game base mod exists
        await EnsureGameModAsync();

        Mods.Clear();
        Mods.AddRange(await _editorDbContext.ModInfos.ToListAsync());
    }

    private async Task EnsureGameModAsync()
    {
        await using var db = await _editorContextFactory.CreateDbContextAsync();
        if (await db.ModInfos.FindAsync(-1) is not null) return;

        db.ModInfos.Add(new ModInfo
        {
            ModId = -1,
            Name = "Game",
            Path = "data",
            IsBase = true,
            LastImport = DateTime.Now,
            LastModified = DateTime.Now,
        });
        await db.SaveChangesAsync();
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

    private void ShowImage(ModInfo? selectedItem = null)
    {
        if (selectedItem is null)
        {
            _logger.LogWarning("ModInfo is null in ShowImageCommand");
            return;
        }

        if (!TryResolveGetImagesLocation(selectedItem, out _, out _))
        {
            App.Notification.ShowWarning(Loc["GetImagesFileNotFoundMessage"], Loc["GetImagesFileNotFound"]);
            return;
        }

        Messenger.Send(new OpenModImagesDocumentMessage(selectedItem));
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
            var toRemove = Mods.Where(m => !m.IsBase).ToList();
            if (toRemove.Count == 0)
            {
                App.Notification.ShowInfo(Loc["NoModsToClearMessage"]);
                return;
            }
            db.ModInfos.RemoveRange(toRemove);
            await db.SaveChangesAsync();
            foreach (var m in toRemove) Mods.Remove(m);
        }
        else if (selectedItem.IsBase)
        {
            App.Notification.ShowWarning(Loc["BaseModCannotBeDeletedMessage"], Loc["BaseModCannotBeDeleted"]);
            return;
        }
        else
        {
            var toRemove = await db.ModInfos.FindAsync(selectedItem.ModId);
            if (toRemove is not null)
            {
                db.ModInfos.Remove(toRemove);
                await db.SaveChangesAsync();
                Mods.Remove(Mods.First(m => m.ModId == selectedItem.ModId));
            }
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
            foreach (var modInfo in Mods.Where(m => !m.IsBase).ToList())
            {
                try
                {
                    await _modManager.DeleteMod(modInfo);
                }
                catch (Exception ex)
                {
                    App.Notification.ShowWarning($"Cannot delete {modInfo.Name}: {ex.Message}");
                }
            }
        }
        else
        {
            if (selectedItem.IsBase)
            {
                App.Notification.ShowWarning(Loc["BaseModCannotBeDeletedMessage"], Loc["BaseModCannotBeDeleted"]);
                return;
            }
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
            ModInfo? gameMod = db.ModInfos.Find(-1);
            if (gameMod is null)
            {
                gameMod = new ModInfo
                {
                    ModId = -1,
                    Name = "Game",
                    Path = "data",
                    IsBase = true,
                    LastImport = DateTime.Now,
                    LastModified = DateTime.Now,
                };
                db.ModInfos.Add(gameMod);
                db.SaveChanges();
            }
        }
        catch (Exception e)
        {
            App.Notification!.ShowWarning($"load {gamePath} failed: {e.Message}");
        }
    }

    public void Receive(GameRootDirChangedMessage message)
    {
        // Manual refresh only — user clicks the refresh button
    }

    public void Receive(RefreshModMessage message)
    {
        // Manual refresh only
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

    private bool TryResolveGetImagesLocation(ModInfo modInfo, out string imageRootDirectory, out string getImagesPath)
    {
        imageRootDirectory = string.Empty;
        getImagesPath = string.Empty;

        if (string.IsNullOrWhiteSpace(Config.GameRootDir))
        {
            return false;
        }

        imageRootDirectory = string.Equals(modInfo.Name, "Game", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(Config.GameRootDir)
            : string.IsNullOrWhiteSpace(modInfo.Path)
                ? string.Empty
                : Path.GetFullPath(Path.Combine(Config.GameRootDir, modInfo.Path));
        if (string.IsNullOrWhiteSpace(imageRootDirectory))
        {
            return false;
        }

        getImagesPath = Path.Combine(imageRootDirectory, "getimages.php");
        return File.Exists(getImagesPath);
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

    [RelayCommand]
    private async Task ExportCsv(ModInfo? selectedItem = null)
    {
        var modInfo = selectedItem ?? SelectedItem;
        if (modInfo is null) return;
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;
        if (TopLevel.GetTopLevel(mainWindow) is not { StorageProvider: { } storageProvider }) return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export CSV",
            DefaultExtension = "csv",
            FileTypeChoices = [new FilePickerFileType("CSV Files") { Patterns = ["*.csv"] }]
        });
        if (file?.TryGetLocalPath() is not { } savePath) return;

        try
        {
            using var gameDb = _gameContextFactory.CreateDbContext(Path.Combine(Directory.GetCurrentDirectory(), Data.Constants.GameDatabasePath));
            // Export all entity types for this mod to a single CSV
            var allEntities = new List<IEntity>();
            foreach (var (typeName, type) in Data.Constants.GameTypes)
            {
                var method = typeof(GameDbContext).GetMethod(nameof(GameDbContext.Set), Type.EmptyTypes)!
                    .MakeGenericMethod(type);
                var dbSet = (System.Collections.IEnumerable)method.Invoke(gameDb, null)!;
                foreach (var entity in dbSet)
                {
                    if (entity is IEntity ie && ie.ModId == modInfo.ModId)
                        allEntities.Add(ie);
                }
            }

            // Simple export: write one section per entity type
            var sb = new System.Text.StringBuilder();
            foreach (var group in allEntities.GroupBy(e => e.GetType().Name))
            {
                var entityType = Data.Constants.GameTypes[group.Key];
                var tempPath = System.IO.Path.GetTempFileName();
                _csvService.ExportEntitiesToCsv(group, entityType, tempPath);
                sb.AppendLine($"# {group.Key}");
                sb.AppendLine(System.IO.File.ReadAllText(tempPath));
                sb.AppendLine();
                try { System.IO.File.Delete(tempPath); } catch { }
            }

            System.IO.File.WriteAllText(savePath, sb.ToString(), System.Text.Encoding.UTF8);
            App.Notification.ShowSuccess($"Exported to {savePath}", "CSV Export");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export CSV");
            App.Notification.ShowWarning($"Export failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ImportCsv(ModInfo? selectedItem = null)
    {
        var modInfo = selectedItem ?? SelectedItem;
        if (modInfo is null) return;
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;
        if (TopLevel.GetTopLevel(mainWindow) is not { StorageProvider: { } storageProvider }) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import CSV",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("CSV Files") { Patterns = ["*.csv"] }]
        });
        if (files is null || files.Count == 0) return;
        if (files[0].TryGetLocalPath() is not { } csvPath) return;

        // Let user select entity type to import
        var entityTypes = Data.Constants.GameTypes.Values.OrderBy(t => t.Name).ToList();
        if (entityTypes.Count == 0)
        {
            App.Notification.ShowWarning("No entity types available.", "Import CSV");
            return;
        }

        // Import to first entity type by default (later: show type selector)
        var entityType = entityTypes[0];
        try
        {
            var modDir = ResolveModDirectory(modInfo.Path);
            var filePath = System.IO.Path.Combine(modDir, "neogame.xml");
            var entities = _csvService.ParseCsvToEntities(csvPath, entityType, modInfo.ModId, filePath);

            if (entities.Count == 0)
            {
                App.Notification.ShowInfo("No valid rows found in CSV.", "Import CSV");
                return;
            }

            // Load existing entities for comparison
            using var gameDb = _gameContextFactory.CreateDbContext(Path.Combine(Directory.GetCurrentDirectory(), Data.Constants.GameDatabasePath));
            var method = typeof(GameDbContext).GetMethod(nameof(GameDbContext.Set), Type.EmptyTypes)!
                .MakeGenericMethod(entityType);
            var dbSet = (System.Collections.IEnumerable)method.Invoke(gameDb, null)!;
            var existing = dbSet.Cast<IEntity>().Where(e => e.ModId == modInfo.ModId).ToList();

            var diffs = _csvService.CompareEntities(entities, existing.Cast<object>().ToList(), entityType);
            if (diffs.Count == 0)
            {
                App.Notification.ShowInfo("No differences found. Nothing to import.", "Import CSV");
                return;
            }

            var confirmed = await CsvImportDiffDialog.ShowAsync(mainWindow, diffs);
            if (!confirmed) return;

            // Upsert imported entities using the DbSet
            var dbSetType = typeof(Microsoft.EntityFrameworkCore.DbSet<>).MakeGenericType(entityType);
            var addMethod = dbSetType.GetMethod("Add", [entityType]);
            if (addMethod is null) return;

            foreach (var entity in entities.Cast<IEntity>())
            {
                var existingEntity = existing.FirstOrDefault(e =>
                    ResolveEntityKeyValue(e) == ResolveEntityKeyValue(entity));
                if (existingEntity is not null)
                {
                    // Copy values from imported entity to existing tracked entity
                    foreach (var prop in entityType.GetProperties())
                    {
                        if (prop.DeclaringType == typeof(IEntity)) continue;
                        if (prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>() is null) continue;
                        if (!prop.CanWrite) continue;
                        prop.SetValue(existingEntity, prop.GetValue(entity));
                    }
                }
                else
                {
                    addMethod.Invoke(dbSet, [entity]);
                }
            }
            await gameDb.SaveChangesAsync();

            App.Notification.ShowSuccess($"Imported {entities.Count} rows into {entityType.Name} for {modInfo.Name}.", "Import CSV");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import CSV");
            App.Notification.ShowWarning($"Import failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportModZip(ModInfo? selectedItem = null)
    {
        var modInfo = selectedItem ?? SelectedItem;
        if (modInfo is null) return;
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;
        if (TopLevel.GetTopLevel(mainWindow) is not { StorageProvider: { } storageProvider }) return;

        var suggestedName = $"{modInfo.Name}_{DateTime.Now:yyyyMMdd}.zip";
        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export Mod as .zip",
            SuggestedFileName = suggestedName,
            FileTypeChoices = [new FilePickerFileType("ZIP Archive") { Patterns = ["*.zip"] }]
        });
        if (file?.TryGetLocalPath() is not { } savePath) return;

        try
        {
            await _modManager.ExportModToZipAsync(modInfo, savePath);
            App.Notification.ShowSuccess($"Exported to {savePath}", "Mod Export");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export mod zip");
            App.Notification.ShowWarning($"Export failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ImportModZip()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;
        if (TopLevel.GetTopLevel(mainWindow) is not { StorageProvider: { } storageProvider }) return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import Mod from .zip",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("ZIP Archive") { Patterns = ["*.zip"] }]
        });
        if (files is null || files.Count == 0) return;
        if (files[0].TryGetLocalPath() is not { } zipPath) return;

        try
        {
            var modInfo = await _modManager.ImportModFromZipAsync(zipPath);
            await RefreshMod();
            App.Notification.ShowSuccess($"Imported mod: {modInfo.Name}", "Mod Import");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import mod zip");
            App.Notification.ShowWarning($"Import failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportCraftingCsv()
    {
        var dateStr = DateTime.Now.ToString("yyyyMMdd");
        await ExportWithDialog("Export Crafting Table", "*.csv",
            _dataExportService.ExportCraftingTableAsync, $"crafting_table_{dateStr}.csv");
    }

    [RelayCommand]
    private async Task ExportItemEncyclopedia()
    {
        var dateStr = DateTime.Now.ToString("yyyyMMdd");
        await ExportWithDialog("Export Item Encyclopedia", "*.md",
            _dataExportService.ExportItemEncyclopediaMdAsync, $"item_encyclopedia_{dateStr}.md");
    }

    [RelayCommand]
    private async Task ExportLootTableJson()
    {
        var dateStr = DateTime.Now.ToString("yyyyMMdd");
        await ExportWithDialog("Export Loot Tables", "*.json",
            _dataExportService.ExportLootTableJsonAsync, $"loot_tables_{dateStr}.json");
    }

    [RelayCommand]
    private async Task ExportAllXlsx()
    {
        var dateStr = DateTime.Now.ToString("yyyyMMdd");
        await ExportWithDialog("Export All to Excel", "*.xlsx",
            _dataExportService.ExportAllToXlsxAsync, $"neoscavenger_data_{dateStr}.xlsx");
    }

    private async Task ExportWithDialog(string title, string pattern, Func<string, Task> exportFunc,
        string? suggestedName = null)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;
        if (TopLevel.GetTopLevel(mainWindow) is not { StorageProvider: { } storageProvider }) return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            FileTypeChoices = [new FilePickerFileType("Export") { Patterns = [pattern] }]
        });
        if (file?.TryGetLocalPath() is not { } savePath) return;

        try
        {
            await exportFunc(savePath);
            App.Notification.ShowSuccess($"Exported to {savePath}", title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed for {Title}", title);
            App.Notification.ShowWarning($"Export failed: {ex.Message}");
        }
    }

    private static object? ResolveEntityKeyValue(IEntity entity)
    {
        var type = entity.GetType();
        var keyProp = type.GetProperties()
            .FirstOrDefault(p =>
            {
                var col = p.GetCustomAttribute<System.ComponentModel.DataAnnotations.Schema.ColumnAttribute>();
                return col?.Name == "id" || col?.Name == "nID";
            });
        return keyProp?.GetValue(entity);
    }
}