using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeoEditor.Data;
using NeoEditor.Data.Context;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;
using NeoEditor.Helper;
using NeoEditor.Services;
using NeoEditor.Views.Dialog;
using System.Xml.Linq;

namespace NeoEditor.Views.UserControls;

public partial class ModGameDataTabsView : UserControl
{
    private const string UnknownFilePath = "(unknown)";

    private readonly IConfigService _configService;
    private readonly IDbContextFactory<GameDbContext> _gameDbContextFactory;
    private readonly ILogger<ModGameDataTabsView> _logger;
    private readonly XmlParser _xmlParser;
    private int _loadVersion;
    private bool _isSavePreviewOpen;

    public static readonly StyledProperty<bool> ReadOnlyProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>("ReadOnly");

    public static readonly StyledProperty<ModInfo?> ModInfoProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, ModInfo?>(nameof(ModInfo));

    public static readonly StyledProperty<bool> IsPreparingSavePreviewProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>(nameof(IsPreparingSavePreview));

    public static readonly StyledProperty<bool> CanStartSavePreviewProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, bool>(nameof(CanStartSavePreview), true);

    public static readonly StyledProperty<string> SavePreviewButtonTextProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, string>(nameof(SavePreviewButtonText), string.Empty);

    public static readonly StyledProperty<string?> SavePreviewStatusTextProperty =
        AvaloniaProperty.Register<ModGameDataTabsView, string?>(nameof(SavePreviewStatusText));

    public ModInfo? ModInfo
    {
        get => GetValue(ModInfoProperty);
        set => SetValue(ModInfoProperty, value);
    }

    public ObservableCollection<GameDataTypeTabItem> Tabs { get; } = [];
    public LocalizationService Loc { get; set; }

    public bool IsPreparingSavePreview
    {
        get => GetValue(IsPreparingSavePreviewProperty);
        private set => SetValue(IsPreparingSavePreviewProperty, value);
    }

    public bool CanStartSavePreview
    {
        get => GetValue(CanStartSavePreviewProperty);
        private set => SetValue(CanStartSavePreviewProperty, value);
    }

    public string SavePreviewButtonText
    {
        get => GetValue(SavePreviewButtonTextProperty);
        private set => SetValue(SavePreviewButtonTextProperty, value);
    }

    public string? SavePreviewStatusText
    {
        get => GetValue(SavePreviewStatusTextProperty);
        private set => SetValue(SavePreviewStatusTextProperty, value);
    }

    public bool ReadOnly
    {
        get { return GetValue(ReadOnlyProperty); }
        set { SetValue(ReadOnlyProperty, value); }
    }

    public ModGameDataTabsView()
    {
        _configService = App.ServiceProvider.GetRequiredService<IConfigService>();
        _gameDbContextFactory = App.ServiceProvider.GetRequiredService<IDbContextFactory<GameDbContext>>();
        Loc = App.ServiceProvider.GetRequiredService<LocalizationService>();
        _logger = App.ServiceProvider.GetRequiredService<ILogger<ModGameDataTabsView>>();
        _xmlParser = App.ServiceProvider.GetRequiredService<XmlParser>();
        InitializeComponent();
        UpdateSavePreviewUiState();
    }

    private async void OnSavePreviewButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ShowSavePreviewAsync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ModInfoProperty)
        {
            _ = ReloadTabsAsync(ModInfo);
        }
    }

    private async Task ReloadTabsAsync(ModInfo? modInfo)
    {
        var loadVersion = ++_loadVersion;
        Tabs.Clear();

        if (modInfo is null)
        {
            return;
        }

        await using var db = await _gameDbContextFactory.CreateDbContextAsync();
        foreach (var (_, entityType) in Constants.GameTypes.OrderBy(x => x.Key))
        {
            var items = await LoadEntitiesByTypeAsync(db, entityType, modInfo.ModId);
            if (loadVersion != _loadVersion)
            {
                return;
            }

            Tabs.Add(new GameDataTypeTabItem
            {
                EntityType = entityType,
                Header = BuildHeader(entityType, items.Count),
                ItemsSource = items
            });
        }
    }

    private async Task ShowSavePreviewAsync()
    {
        if (_isSavePreviewOpen || IsPreparingSavePreview)
        {
            return;
        }

        var modInfo = ModInfo;
        if (modInfo is null)
        {
            App.Notification.ShowWarning(Loc["ModGameDataSaveMissingModMessage"], Loc["Save"]);
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        _isSavePreviewOpen = true;
        SetSavePreviewPreparationState(true, Loc["ModGameDataPreparingSavePreviewScanning"]);
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(static () => { },
            Avalonia.Threading.DispatcherPriority.Render);

        try
        {
            var currentEntities = CaptureCurrentTabEntities();
            var localLoadedSnapshot = await Task.Run(() => BuildLocalXmlLoadedSnapshot(modInfo));

            SetSavePreviewPreparationState(true, Loc["ModGameDataPreparingSavePreviewExporting"]);
            var currentEditorSnapshot = await Task.Run(() => BuildCurrentTabsExportSnapshot(modInfo, currentEntities));

            SetSavePreviewPreparationState(true, Loc["ModGameDataPreparingSavePreviewDiffing"]);
            var diffText = await Task.Run(() => BuildDiffText(localLoadedSnapshot, currentEditorSnapshot));

            SetSavePreviewPreparationState(false);

            var dialog = new ModGameDataSavePreviewDialog(modInfo.Name, localLoadedSnapshot, diffText);
            var confirmed = await dialog.ShowDialog<bool?>(owner);
            if (confirmed == true)
            {
                App.Notification.ShowSuccess(Loc["ModGameDataSaveSuccessMessage"], Loc["Save"]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate save preview for mod {ModId} ({ModName}).", modInfo.ModId,
                modInfo.Name);
            App.Notification.ShowError(Loc["ModGameDataSavePreviewFailedMessage", ex.Message], Loc["Error"]);
        }
        finally
        {
            SetSavePreviewPreparationState(false);
            _isSavePreviewOpen = false;
            UpdateSavePreviewUiState();
        }
    }

    private IReadOnlyList<IEntity> CaptureCurrentTabEntities()
    {
        return Tabs
            .SelectMany(tab => tab.ItemsSource.Cast<object>())
            .OfType<IEntity>()
            .ToList();
    }

    private void SetSavePreviewPreparationState(bool isPreparing, string? statusText = null)
    {
        IsPreparingSavePreview = isPreparing;
        SavePreviewStatusText = isPreparing ? statusText : null;
        UpdateSavePreviewUiState();
    }

    private void UpdateSavePreviewUiState()
    {
        CanStartSavePreview = !_isSavePreviewOpen && !IsPreparingSavePreview;
        SavePreviewButtonText = IsPreparingSavePreview
            ? Loc["ModGameDataPreparingSavePreviewButton"]
            : Loc["Save"];
    }

    private string BuildHeader(Type entityType, int count)
    {
        var title = Loc[entityType.Name];
        return $"{title} ({count})";
    }

    private async Task<IReadOnlyList<object>> LoadEntitiesByTypeAsync(GameDbContext db, Type entityType, int modId)
    {
        var method = typeof(ModGameDataTabsView)
                         .GetMethod(nameof(LoadEntitiesByTypeTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)
                         ?.MakeGenericMethod(entityType)
                     ?? throw new InvalidOperationException($"Cannot load entity type {entityType.Name}.");

        var task = method.Invoke(null, [db, modId]) as Task<IReadOnlyList<object>>;
        if (task == null)
        {
            throw new InvalidOperationException($"Loading entity type {entityType.Name} did not return a task.");
        }

        return await task;
    }

    private static async Task<IReadOnlyList<object>> LoadEntitiesByTypeTypedAsync<TEntity>(GameDbContext db, int modId)
        where TEntity : IEntity
    {
        return await db.Set<TEntity>()
            .Where(x => x.ModId == modId)
            .Cast<object>()
            .ToListAsync();
    }

    private string BuildLocalXmlLoadedSnapshot(ModInfo modInfo)
    {
        var modDirectory = ResolveModDirectory(modInfo);
        if (!Directory.Exists(modDirectory))
        {
            throw new DirectoryNotFoundException($"Mod directory not found: {modDirectory}");
        }

        var exportedFiles = new List<(string FilePath, XDocument Document)>();
        foreach (var xmlPath in Directory.GetFiles(modDirectory, "*.xml", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var doc = XDocument.Load(xmlPath);
            var importedEntities = ImportEntitiesFromDocument(doc, modInfo.ModId, xmlPath);
            exportedFiles.Add((NormalizeComparisonFilePath(modDirectory, xmlPath), _xmlParser.Export(importedEntities)));
        }

        return BuildModExportXml(modInfo, exportedFiles);
    }

    private string BuildCurrentTabsExportSnapshot(ModInfo modInfo, IReadOnlyList<IEntity> allEntities)
    {
        var modDirectory = ResolveModDirectory(modInfo);

        var exportedFiles = allEntities
            .GroupBy(entity => NormalizeComparisonFilePath(modDirectory, entity.FilePath), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => (group.Key, _xmlParser.Export(group)))
            .ToList();

        return BuildModExportXml(modInfo, exportedFiles);
    }

    private IReadOnlyList<IEntity> ImportEntitiesFromDocument(XDocument doc, int modId, string filePath)
    {
        var entities = new List<IEntity>();

        foreach (var (_, entityType) in Constants.GameTypes.OrderBy(x => x.Key))
        {
            var method = typeof(XmlParser).GetMethod(nameof(XmlParser.ImportEntities))?.MakeGenericMethod(entityType);
            if (method?.Invoke(_xmlParser, [doc, modId, filePath]) is not IEnumerable importedEntities)
            {
                continue;
            }

            foreach (var importedEntity in importedEntities)
            {
                if (importedEntity is IEntity entity)
                {
                    entities.Add(entity);
                }
            }
        }

        return entities;
    }

    private string BuildModExportXml(ModInfo modInfo, IEnumerable<(string FilePath, XDocument Document)> exportedFiles)
    {
        var root = new XElement("modGameDataExport",
            new XAttribute("modId", modInfo.ModId),
            new XAttribute("modName", SafeXmlValue(modInfo.Name)),
            new XAttribute("modPath", SafeXmlValue(modInfo.Path)));

        foreach (var (filePath, document) in exportedFiles)
        {
            var fileElement = new XElement("file", new XAttribute("path", SafeXmlValue(filePath)));
            if (document.Root != null)
            {
                fileElement.Add(new XElement(document.Root));
            }

            root.Add(fileElement);
        }

        var aggregatedDocument = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
        return aggregatedDocument.ToString();
    }

    private string ResolveModDirectory(ModInfo modInfo)
    {
        return Path.GetFullPath(Path.Combine(_configService.Config.GameRootDir, modInfo.Path ?? string.Empty));
    }

    private static string NormalizeComparisonFilePath(string modDirectory, string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return UnknownFilePath;
        }

        var normalizedPath = filePath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalizedPath))
        {
            var fullPath = Path.GetFullPath(normalizedPath);
            var fullModDirectory = Path.GetFullPath(modDirectory);
            if (fullPath.StartsWith(fullModDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(fullModDirectory, fullPath).Replace('\\', '/');
            }

            return fullPath.Replace('\\', '/');
        }

        return normalizedPath.Replace('\\', '/');
    }

    private string BuildDiffText(string oldSnapshot, string newSnapshot)
    {
        var tempDirectory =
            Path.Combine(Path.GetTempPath(), "NeoEditor", "ModGameDataDiff", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        var oldSnapshotPath = Path.Combine(tempDirectory, "old.xml");
        var newSnapshotPath = Path.Combine(tempDirectory, "new.xml");

        try
        {
            File.WriteAllText(oldSnapshotPath, oldSnapshot, new UTF8Encoding(false));
            File.WriteAllText(newSnapshotPath, newSnapshot, new UTF8Encoding(false));
            try
            {
                return XmlCompareHelper.Compare(oldSnapshotPath, newSnapshotPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "XmlCompareHelper failed while generating save preview diff text. Falling back to current snapshot text.");
                return newSnapshot;
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
            catch
            {
                // Ignore temp cleanup errors.
            }
        }
    }

    private static string SafeXmlValue(string? value)
    {
        return value ?? string.Empty;
    }
}

public sealed class GameDataTypeTabItem
{
    public required Type EntityType { get; init; }
    public required string Header { get; init; }
    public required IEnumerable ItemsSource { get; init; }
}