using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;
using Microsoft.Extensions.Logging;
using NeoEditor.Helper;
using NeoEditor.Services;

namespace NeoEditor.ViewModels.ExplorerPane;

public partial class SettingsPaneViewModel : ViewModelBase
{
    private readonly IConfigService _config;
    public AppConfig Config => _config.Config;
    private readonly ILogger<ResourceManagerViewModel> _logger;
    private readonly ILocalizationService _localizationService;

    public string[] AvailableLanguages { get; } = ["zh", "en"];
    public string[] AvailableThemes { get; } = ["System", "Light", "Dark"];
    public string[] AvailableExportFormats { get; } = ["csv", "xlsx", "md", "json"];

    public string DisplayLanguage
    {
        get => Config.Language;
        set
        {
            if (Config.Language == value) return;
            Config.Language = value;
            OnPropertyChanged();
            ApplyLanguage(value);
            Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    public string DisplayTheme
    {
        get => Config.Theme;
        set
        {
            if (Config.Theme == value) return;
            Config.Theme = value;
            OnPropertyChanged();
            ApplyTheme(value);
            Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    public int DisplayFontSize
    {
        get => Config.FontSize;
        set
        {
            if (Config.FontSize == value) return;
            Config.FontSize = value;
            OnPropertyChanged();
            if (value is > 0 and <= 24 && Application.Current is App app)
                app.Resources["AppFontSize"] = (double)value;
            Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    public int DisplayAutoSaveInterval
    {
        get => Config.AutoSaveInterval;
        set
        {
            if (Config.AutoSaveInterval == value) return;
            Config.AutoSaveInterval = value;
            OnPropertyChanged();
            Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    public string DisplayDefaultExportFormat
    {
        get => Config.DefaultExportFormat;
        set
        {
            if (Config.DefaultExportFormat == value) return;
            Config.DefaultExportFormat = value;
            OnPropertyChanged();
            Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    public int DisplayGridRowHeight
    {
        get => Config.GridRowHeight;
        set
        {
            if (Config.GridRowHeight == value) return;
            Config.GridRowHeight = value;
            OnPropertyChanged();
            Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  AI / MCP configuration (Phase 9D R28 + provider list)
    //  Endpoint + api key live in a provider list (AiProviderConfig); each model
    //  (chat / embedding / image) selects its provider by id. Empty id = first provider.
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Providers shown in the Settings list editor.</summary>
    public ObservableCollection<AiProviderRowViewModel> DisplayAiProviders { get; } = new();

    /// <summary>Provider dropdown options: an "Auto (first provider)" entry + every provider.</summary>
    public ObservableCollection<AiProviderRowViewModel> ProviderChoices { get; } = new();

    /// <summary>Auto entry standing for "use the first provider" (empty provider id).</summary>
    private readonly AiProviderRowViewModel _autoProviderChoice = null!;

    [RelayCommand]
    private void AddProvider()
    {
        var provider = new AiProviderConfig
        {
            Id = "p" + Guid.NewGuid().ToString("N")[..8],
            Name = $"Provider {Config.AiProviders.Count + 1}"
        };
        Config.AiProviders.Add(provider);
        DisplayAiProviders.Add(CreateProviderRow(provider));
        RebuildProviderChoices();
        AsyncHelper.FireAndForget(_config.SaveAsync());
    }

    /// <summary>Removes a provider row (invoked from the row's own RemoveCommand).</summary>
    private void RemoveProviderRow(AiProviderRowViewModel row)
    {
        Config.AiProviders.Remove(row.Provider);
        DisplayAiProviders.Remove(row);
        RebuildProviderChoices();
        AsyncHelper.FireAndForget(_config.SaveAsync());
    }

    private void RebuildProviderChoices()
    {
        ProviderChoices.Clear();
        ProviderChoices.Add(_autoProviderChoice);
        foreach (var p in DisplayAiProviders) ProviderChoices.Add(p);
        // Re-evaluate the per-model ComboBox selections (provider ids may have changed / been removed).
        OnPropertyChanged(nameof(SelectedChatProvider));
        OnPropertyChanged(nameof(SelectedEmbeddingProvider));
        OnPropertyChanged(nameof(SelectedImageProvider));
    }

    private AiProviderRowViewModel CreateProviderRow(AiProviderConfig provider) => new(
        provider, _config,
        removeRequested: RemoveProviderRow,
        removeToolTip: _localizationService["Settings.RemoveProvider"]);

    private AiProviderRowViewModel? FindChoice(string providerId) =>
        ProviderChoices.FirstOrDefault(c => c.Id == providerId) ?? ProviderChoices.FirstOrDefault();

    public AiProviderRowViewModel? SelectedChatProvider
    {
        get => FindChoice(Config.AiModelProviderId);
        set
        {
            var id = value?.Id ?? "";
            if (Config.AiModelProviderId == id) return;
            Config.AiModelProviderId = id;
            OnPropertyChanged();
            AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    public AiProviderRowViewModel? SelectedEmbeddingProvider
    {
        get => FindChoice(Config.AiEmbeddingProviderId);
        set
        {
            var id = value?.Id ?? "";
            if (Config.AiEmbeddingProviderId == id) return;
            Config.AiEmbeddingProviderId = id;
            OnPropertyChanged();
            AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    public AiProviderRowViewModel? SelectedImageProvider
    {
        get => FindChoice(Config.ImageProviderId);
        set
        {
            var id = value?.Id ?? "";
            if (Config.ImageProviderId == id) return;
            Config.ImageProviderId = id;
            OnPropertyChanged();
            AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    public string DisplayAiModel
    {
        get => Config.AiModel;
        set
        {
            if (Config.AiModel == value) return;
            Config.AiModel = value;
            OnPropertyChanged();
            Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    public string DisplayAiEmbeddingModel
    {
        get => Config.AiEmbeddingModel;
        set
        {
            if (Config.AiEmbeddingModel == value) return;
            Config.AiEmbeddingModel = value;
            OnPropertyChanged();
            Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    public string DisplayImageModel
    {
        get => Config.ImageModel;
        set
        {
            if (Config.ImageModel == value) return;
            Config.ImageModel = value;
            OnPropertyChanged();
            Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    public bool DisplayMcpEnabled
    {
        get => Config.McpEnabled;
        set
        {
            if (Config.McpEnabled == value) return;
            Config.McpEnabled = value;
            OnPropertyChanged();
            Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    public int DisplayMcpPort
    {
        get => Config.McpPort;
        set
        {
            if (Config.McpPort == value) return;
            Config.McpPort = value;
            OnPropertyChanged();
            Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    /// <summary>
    /// Wrapper for GameRootDir that triggers SaveAsync on change.
    /// Direct TextBox binding to Config.GameRootDir would update in-memory only;
    /// this ensures the config is persisted to config.json.
    /// Synced from Config via GameRootDirChangedMessage (handles LoadAsync race).
    /// </summary>
    [ObservableProperty] private string _displayGameRootDir = string.Empty;

    partial void OnDisplayGameRootDirChanged(string value)
    {
        if (Config.GameRootDir == value) return;
        Config.GameRootDir = value;
        Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Column visibility config
    // ═══════════════════════════════════════════════════════════════════════

    public ObservableCollection<TableColumnGroup> TableColumns { get; } = new();

    public void LoadColumnVisibilityConfig()
    {
        TableColumns.Clear();
        var cv = Config.ColumnVisibility;

        foreach (var entityType in ColumnVisibilityKeys.AllEntityTypes
                     .OrderBy(t => ColumnVisibilityKeys.GetTableName(t) ?? t.Name))
        {
            var tableName = ColumnVisibilityKeys.GetTableName(entityType);
            if (tableName is null) continue;

            var group = new TableColumnGroup
            {
                TableName = tableName,
                EntityType = entityType,
                Columns = new List<ColumnOption>(),
                Config = _config
            };

            // All column keys from the single source of truth — identical to
            // what the DataGrid's SortMemberPath produces.
            foreach (var key in ColumnVisibilityKeys.GetKeys(entityType))
            {
                var displayName = ColumnVisibilityKeys.GetDisplayName(entityType, key);
                var opt = new ColumnOption
                {
                    Key = key,
                    DisplayName = displayName,
                    Group = group,
                    IsVisible = ColumnVisibilityKeys.IsVisible(cv, tableName, key)
                };
                group.Columns.Add(opt);
            }

            TableColumns.Add(group);
        }
    }

    public SettingsPaneViewModel(ILogger<ResourceManagerViewModel> logger, ILocalizationService localizationService,
        IConfigService configService)
    {
        _config = configService;
        _logger = logger;
        _localizationService = localizationService;
        _displayGameRootDir = Config.GameRootDir;
        LoadColumnVisibilityConfig();

        // AI provider list: an "Auto (first provider)" pseudo-entry + each configured provider.
        _autoProviderChoice = new AiProviderRowViewModel(
            new AiProviderConfig { Id = "", Name = _localizationService["Settings.ProviderAuto"] }, _config);
        foreach (var provider in Config.AiProviders)
            DisplayAiProviders.Add(CreateProviderRow(provider));
        RebuildProviderChoices();
    }

    [RelayCommand]
    private async Task BrowseGameDir()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            {
                MainWindow: { } mainWindow
            }) return;
        if (TopLevel.GetTopLevel(mainWindow) is not { StorageProvider: { } storageProvider }) return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Game Root Directory",
            AllowMultiple = false
        });
        if (folders.Count == 0) return;
        if (folders[0].TryGetLocalPath() is not { } path) return;

        DisplayGameRootDir = path;
    }

    private void ApplyLanguage(string lang)
    {
        try
        {
            var culture = new CultureInfo(lang);
            _localizationService.SetCulture(culture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply language {Lang}", lang);
        }
    }

    private static void ApplyTheme(string theme)
    {
        if (Application.Current is not App app) return;
        app.RequestedThemeVariant = theme switch
        {
            "Light" => Avalonia.Styling.ThemeVariant.Light,
            "Dark" => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default
        };
    }
}

public class TableColumnGroup
{
    public required string TableName { get; init; }
    public required Type EntityType { get; init; }
    public required List<ColumnOption> Columns { get; init; }
    public required IConfigService Config { get; init; }

    public IRelayCommand SelectAllCommand { get; }
    public IRelayCommand SelectNoneCommand { get; }

    public TableColumnGroup()
    {
        SelectAllCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => SetAll(true));
        SelectNoneCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => SetAll(false));
    }

    public void SetAll(bool visible)
    {
        var cv = Config.Config.ColumnVisibility;
        ColumnVisibilityKeys.SeedAllVisible(cv, EntityType);
        var set = cv[TableName];

        foreach (var col in Columns)
        {
            col.SetSilent(visible);
            if (visible) set.Add(col.Key);
            else set.Remove(col.Key);
        }

        AsyncHelper.FireAndForget(Config.SaveAsync());
        WeakReferenceMessenger.Default.Send(
            new Data.Messages.ColumnVisibilityChangedMessage { TableName = TableName });
    }
}

public partial class ColumnOption : ObservableObject
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required TableColumnGroup Group { get; init; }

    private bool _isVisible;

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (!SetProperty(ref _isVisible, value)) return;
            ToggleInConfig(value);
        }
    }

    internal void ToggleInConfig(bool visible)
    {
        var cv = Group.Config.Config.ColumnVisibility;
        var tableName = Group.TableName;
        if (!cv.TryGetValue(tableName, out _))
        {
            // First toggle: seed with ALL keys so we don't accidentally hide everything else
            ColumnVisibilityKeys.SeedAllVisible(cv, Group.EntityType);
        }

        var set = cv[tableName];
        if (visible) set.Add(Key);
        else set.Remove(Key);
        AsyncHelper.FireAndForget(Group.Config.SaveAsync());
        WeakReferenceMessenger.Default.Send(
            new ColumnVisibilityChangedMessage { TableName = tableName });
    }

    /// <summary>Set IsVisible without triggering individual config save (used by SetAll).</summary>
    internal void SetSilent(bool visible)
    {
        if (_isVisible == visible) return;
        _isVisible = visible;
        OnPropertyChanged(nameof(IsVisible));
    }
}