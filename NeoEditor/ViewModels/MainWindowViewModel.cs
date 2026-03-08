using System;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoEditor.Data.Options;
using NeoEditor.Services;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly NeoEditor.Services.IConfigService _config;
    public AppConfig Config => _config.Config;
    private readonly ILogger<MainWindowViewModel> _logger;

    public MainWindowViewModel() : this(App.ServiceProvider!)
    {
    }

    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        SideBar = serviceProvider.GetRequiredService<MainWindowSideBarViewModel>();
        DocumentWorkspace = serviceProvider.GetRequiredService<DocumentWorkspaceViewModel>();
        var cultureSettings = serviceProvider.GetRequiredService<IOptions<CultureSettings>>();
        CurrentCultureInfo = new CultureInfo(cultureSettings.Value.DefaultCulture.Code ?? "en-us");
        SupportedCultures = new ObservableCollection<CultureInfo>(
            cultureSettings.Value?.Cultures.Select((info => new CultureInfo(info.Code))) ??
            [new CultureInfo("en-us"), new CultureInfo("zh")]
        );
        _logger = serviceProvider.GetRequiredService<ILogger<MainWindowViewModel>>();
        _config = serviceProvider.GetRequiredService<NeoEditor.Services.IConfigService>();
    }

    public MainWindowSideBarViewModel SideBar { get; }
    public DocumentWorkspaceViewModel DocumentWorkspace { get; }

    public ObservableCollection<CultureInfo> SupportedCultures { get; }
    [ObservableProperty] public partial CultureInfo CurrentCultureInfo { get; set; }


    [RelayCommand]
    public async Task SetFolder()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
        var storageProvider = topLevel?.StorageProvider;
        if (storageProvider == null)
        {
            return;
        }

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc["SelectGameRootDir"],
            AllowMultiple = false
        });

        foreach (var folder in folders)
        {
            var folderPath = folder.TryGetLocalPath();
            if (folderPath == null)
            {
                continue;
            }

            _logger.LogInformation("Selected folder: {FolderPath}", folderPath);
            Config.GameRootDir = folderPath;
            await _config.SaveAsync();
            return;
        }
    }

    [RelayCommand]
    private void ChangeCulture(CultureInfo? culture)
    {
        _logger.LogInformation("Changing culture to: {CultureName}", culture?.Name);
        if (culture is null)
        {
            return;
        }

        Loc.SetCulture(culture);
        OnPropertyChanged(nameof(Loc));
        CurrentCultureInfo = culture;
    }
}