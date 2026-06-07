using System;
using System.Globalization;
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
using NeoEditor.Services;

namespace NeoEditor.ViewModels.ExplorerPane;

public partial class SettingsPaneViewModel : ViewModelBase
{
    private readonly IConfigService _config;
    public AppConfig Config => _config.Config;
    private readonly ILogger<ResourceManagerViewModel> _logger;
    private readonly LocalizationService _localizationService;

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
            App.ApplyFontSize(value);
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
            Messenger.Send(new GridRowHeightChangedMessage { RowHeight = value });
            Helper.AsyncHelper.FireAndForget(_config.SaveAsync());
        }
    }

    public SettingsPaneViewModel() : this(
        App.ServiceProvider!.GetRequiredService<ILogger<ResourceManagerViewModel>>(),
        App.ServiceProvider!.GetRequiredService<LocalizationService>(),
        App.ServiceProvider!.GetRequiredService<IConfigService>())
    {
    }

    public SettingsPaneViewModel(ILogger<ResourceManagerViewModel> logger, LocalizationService localizationService,
        IConfigService configService)
    {
        _config = configService;
        _logger = logger;
        _localizationService = localizationService;
    }

    [RelayCommand]
    private async Task BrowseGameDir()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
            { MainWindow: { } mainWindow }) return;
        if (TopLevel.GetTopLevel(mainWindow) is not { StorageProvider: { } storageProvider }) return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Game Root Directory",
            AllowMultiple = false
        });
        if (folders.Count == 0) return;
        if (folders[0].TryGetLocalPath() is not { } path) return;

        Config.GameRootDir = path;
        await _config.SaveAsync();
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
