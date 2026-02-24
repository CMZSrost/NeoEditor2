using System;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Services;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeoEditor.Assets;
using NeoEditor.Data.DTO;
using NeoEditor.Data.Model.Game;
using NeoEditor.Data.Options;
using NeoEditor.Helper;
using NeoEditor.ViewModels.ExplorerPane;

namespace NeoEditor.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private INotificationService _notificationService;
    private readonly ILogger<MainWindowViewModel> _logger;

    public ObservableCollection<GameVar> SampleData { get; set; } =
        [new() { ModId = 1, Name = "Sample Var", Value = "123", Type = "int" }];

    public MainWindowViewModel() : this(App.ServiceProvider!)
    {
    }

    public MainWindowViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Loc = serviceProvider.GetRequiredService<LocalizationService>();
        var cultureSettings = serviceProvider.GetRequiredService<IOptions<CultureSettings>>();
        CurrentCultureInfo = new CultureInfo(cultureSettings.Value?.DefaultCulture.Code ?? "en-us");
        SupportedCultures = new ObservableCollection<CultureInfo>(
            cultureSettings.Value?.Cultures.Select((info => new CultureInfo(info.Code))) ??
            [new CultureInfo("en-us"), new CultureInfo("zh")]
        );
        _notificationService = serviceProvider.GetRequiredService<INotificationService>();
        _logger = serviceProvider.GetRequiredService<ILogger<MainWindowViewModel>>();
    }

    #region SideBar

    #region Base

    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty] public partial bool SideBarExpanded { get; set; } = false;
    [ObservableProperty] public partial object? CurrentPaneContent { get; set; }

    private string _currentPaneId = ""; // 当前打开的面板标识

    [RelayCommand]
    private void TogglePane(string paneId)
    {
        try
        {
            // 如果点击的是同一个面板，则切换展开/折叠状态
            if (_currentPaneId == paneId && SideBarExpanded)
            {
                SideBarExpanded = false;
                // 不清除 CurrentPaneContent，保留内容以便下次快速展开
                CurrentPaneContent = null;
            }
            else
            {
                // 切换为新面板，确保展开
                _currentPaneId = paneId;
                CurrentPaneContent = CreatePaneContent(paneId);
                SideBarExpanded = true;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
            throw;
        }
    }

    private object CreatePaneContent(string paneId)
    {
        return paneId switch
        {
            "Explorer" => _serviceProvider.GetRequiredService<ExplorerPane.ResourceManagerViewModel>(),
            "Search" => _serviceProvider.GetRequiredService<SearchPaneViewModel>(),
            "Settings" => _serviceProvider.GetRequiredService<SettingsPaneViewModel>(),
            "ModDatabase" => _serviceProvider.GetRequiredService<ModDatabaseViewModel>(),
            _ => throw new NotSupportedException()
        };
    }

    #endregion

    #endregion

    #region Menu

    #region Project

    [RelayCommand]
    private void CreateProject()
    {
        _logger.LogInformation($"Creating project {DateTime.Now}");
        _notificationService.ShowInfo("创建项目功能尚未实现", "提示");
        // TODO: implement create project flow.
    }

    [RelayCommand]
    private void OpenProject()
    {
        // TODO: implement open project flow.
    }

    [RelayCommand]
    private void CloseProject()
    {
        // TODO: implement close project flow.
    }

    #endregion

    #region Language

    [ObservableProperty] public partial LocalizationService Loc { get; set; }

    public ObservableCollection<CultureInfo> SupportedCultures { get; }
    [ObservableProperty] public partial CultureInfo CurrentCultureInfo { get; set; }

    [RelayCommand]
    private void ChangeCulture(CultureInfo? culture)
    {
        _logger.LogInformation($"Changing culture to: {culture?.Name}");
        if (culture is null)
        {
            return;
        }

        Loc.SetCulture(culture);
        OnPropertyChanged(nameof(Loc));
        CurrentCultureInfo = culture;
    }

    #endregion

    #endregion
}