using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Core.Abstractions;
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
    private readonly IOnboardingHintService _hintService;

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

    /// <summary>Docs/41 P3: re-enable one-shot onboarding hints (empty-mod banner, first-export toast…).</summary>
    [RelayCommand]
    private async Task ResetOnboardingHints()
    {
        await _hintService.ResetAllAsync();
        _config.Config.EmptyModHintDismissed = false;
        await _config.SaveAsync();
        Notification.ShowSuccess(Loc["SettingsOnboardingHintsReset"]);
    }

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

    // ── Image model connectivity test (Settings "Test" button) ──
    // Resolves the image provider the way ImageGenerationService does, then makes a real
    // /images/generations call so the user can verify endpoint + key + model all work
    // without guessing.

    [ObservableProperty]
    public partial bool IsTestingImageConnection { get; set; }

    [ObservableProperty]
    public partial string ImageTestResult { get; set; } = string.Empty;

    public bool HasImageTestResult => !string.IsNullOrWhiteSpace(ImageTestResult);

    [RelayCommand]
    private async Task TestImageConnection()
    {
        IsTestingImageConnection = true;
        ImageTestResult = string.Empty;
        OnPropertyChanged(nameof(HasImageTestResult));
        try
        {
            var provider = AiProviderResolver.Resolve(
                Config, Config.ImageProviderId,
                Environment.GetEnvironmentVariable("OPENAI_ENDPOINT"),
                Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

            if (provider is null)
            {
                ImageTestResult = _localizationService["Settings.ImageTestNoProvider"];
                OnPropertyChanged(nameof(HasImageTestResult));
                return;
            }

            var model = AiProviderResolver.ResolveModelName(Config.ImageModel,
                Environment.GetEnvironmentVariable("OPENAI_IMAGE_MODEL"), "dall-e-3");

            // Real call: 512x512 is the smallest size shared by dall-e and CogView (CogView
            // rejects anything below 512 and non-multiples of 16). Tiny prompt keeps it cheap.
            var url = $"{provider.Endpoint.TrimEnd('/')}/images/generations";
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {provider.ApiKey}");
            var body = new
            {
                model,
                prompt = "test",
                n = 1,
                size = "512x512",
                quality = "standard",
                response_format = "b64_json"
            };

            var response = await http.PostAsJsonAsync(url, body);
            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                ImageTestResult = _localizationService["Settings.ImageTestOk"];
            }
            else
            {
                // Surface the HTTP status + first line of the error body for diagnosability.
                var detail = responseText.Replace("\n", " ").Trim();
                if (detail.Length > 200) detail = detail[..200] + "…";
                ImageTestResult = string.Format(
                    _localizationService["Settings.ImageTestFailed"], (int)response.StatusCode, detail);
            }
        }
        catch (Exception ex)
        {
            ImageTestResult = string.Format(
                _localizationService["Settings.ImageTestError"], ex.Message);
        }
        finally
        {
            IsTestingImageConnection = false;
            OnPropertyChanged(nameof(HasImageTestResult));
        }
    }

    public int DisplayMaxToolCalls
    {
        get => Config.MaxToolCallsPerConversation;
        set
        {
            if (Config.MaxToolCallsPerConversation == value) return;
            Config.MaxToolCallsPerConversation = value;
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

    // ═══════════════════════════════════════════════════════════════════════
    //  ParaTranz translation platform (D03 §6.1)
    //  Token is encrypted at rest by ConfigService; the API client singleton
    //  (M4 panel / sync services) reads the same token via this VM at load time.
    // ═══════════════════════════════════════════════════════════════════════

    private readonly NeoEditor.Plugins.Paratranz.Services.IParatranzApiClient _paratranzClient;

    /// <summary>ParaTranz API token (Settings text box; persisted encrypted).</summary>
    public string DisplayParatranzToken
    {
        get => Config.ParatranzToken;
        set
        {
            if (Config.ParatranzToken == value) return;
            Config.ParatranzToken = value;
            _paratranzClient.Token = value;
            OnPropertyChanged();
            Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    /// <summary>Projects available to the token (filled after a successful connection test).</summary>
    public ObservableCollection<ParatranzProjectChoice> ParatranzProjectChoices { get; } = new();

    private ParatranzProjectChoice? _selectedParatranzProject;
    public ParatranzProjectChoice? SelectedParatranzProject
    {
        get => _selectedParatranzProject;
        set
        {
            if (_selectedParatranzProject == value) return;
            _selectedParatranzProject = value;
            Config.ParatranzProjectId = value?.Id ?? 0;
            OnPropertyChanged();
            Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    [ObservableProperty]
    public partial bool IsTestingParatranzConnection { get; set; }

    [ObservableProperty]
    public partial string ParatranzTestResult { get; set; } = string.Empty;

    public bool HasParatranzTestResult => !string.IsNullOrWhiteSpace(ParatranzTestResult);

    /// <summary>
    /// Validate the token (GET /projects), then fill the project dropdown.
    /// Mirrors TestImageConnection: real call + diagnosable result text.
    /// </summary>
    [RelayCommand]
    private async Task TestParatranzConnection()
    {
        IsTestingParatranzConnection = true;
        ParatranzTestResult = string.Empty;
        OnPropertyChanged(nameof(HasParatranzTestResult));
        try
        {
            if (string.IsNullOrWhiteSpace(DisplayParatranzToken))
            {
                ParatranzTestResult = _localizationService["Settings.ParatranzTestNoToken"];
                OnPropertyChanged(nameof(HasParatranzTestResult));
                return;
            }

            var projects = await _paratranzClient.GetProjectsAsync();
            if (projects.Count == 0)
            {
                ParatranzTestResult = _localizationService["Settings.ParatranzTestNoProjects"];
                OnPropertyChanged(nameof(HasParatranzTestResult));
                return;
            }

            ParatranzProjectChoices.Clear();
            foreach (var project in projects.OrderBy(p => p.Name))
                ParatranzProjectChoices.Add(new ParatranzProjectChoice(project.Id ?? 0, project.Name ?? $"#{project.Id}"));
            SelectedParatranzProject = ParatranzProjectChoices.FirstOrDefault(p => p.Id == Config.ParatranzProjectId)
                                       ?? ParatranzProjectChoices.FirstOrDefault();

            ParatranzTestResult = string.Format(
                _localizationService["Settings.ParatranzTestOk"], projects.Count);
            OnPropertyChanged(nameof(HasParatranzTestResult));
        }
        catch (NeoEditor.Plugins.Paratranz.Models.ParatranzApiException ex)
        {
            ParatranzTestResult = string.Format(
                _localizationService["Settings.ParatranzTestFailed"], ex.Message);
            OnPropertyChanged(nameof(HasParatranzTestResult));
        }
        catch (Exception ex)
        {
            ParatranzTestResult = string.Format(
                _localizationService["Settings.ParatranzTestError"], ex.Message);
            OnPropertyChanged(nameof(HasParatranzTestResult));
        }
        finally
        {
            IsTestingParatranzConnection = false;
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
        IConfigService configService, IOnboardingHintService onboardingHintService,
        NeoEditor.Plugins.Paratranz.Services.IParatranzApiClient paratranzClient)
    {
        _config = configService;
        _logger = logger;
        _localizationService = localizationService;
        _hintService = onboardingHintService;
        _paratranzClient = paratranzClient;
        // D03: keep the API client token in sync with config at load time (M4 panel reuses it).
        _paratranzClient.Token = Config.ParatranzToken;
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
/// <summary>ParaTranz project shown in the Settings dropdown (D03 §6.1).</summary>
public sealed class ParatranzProjectChoice(int id, string name)
{
    public int Id { get; } = id;
    public string Name { get; } = name;
    public string DisplayLabel => string.IsNullOrWhiteSpace(Name) ? $"#{Id}" : $"{Name} (#{Id})";
}
