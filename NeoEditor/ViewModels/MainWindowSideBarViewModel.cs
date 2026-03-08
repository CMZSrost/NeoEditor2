using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeoEditor.ViewModels.ExplorerPane;
using ModIndexViewModel = NeoEditor.ViewModels.ExplorerPane.ModIndexViewModel;

namespace NeoEditor.ViewModels;

public partial class MainWindowSideBarViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MainWindowSideBarViewModel> _logger;
    private string _currentPaneId = string.Empty;

    public MainWindowSideBarViewModel() : this(App.ServiceProvider!)
    {
    }

    public MainWindowSideBarViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<MainWindowSideBarViewModel>>();
    }

    [ObservableProperty] public partial bool SideBarExpanded { get; set; }
    [ObservableProperty] public partial object? CurrentPaneContent { get; set; }

    [RelayCommand]
    private void TogglePane(string paneId)
    {
        try
        {
            if (_currentPaneId == paneId && SideBarExpanded)
            {
                SideBarExpanded = false;
                CurrentPaneContent = null;
                return;
            }

            _currentPaneId = paneId;
            CurrentPaneContent = CreatePaneContent(paneId);
            SideBarExpanded = true;
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
            "Explorer" => _serviceProvider.GetRequiredService<ResourceManagerViewModel>(),
            "Search" => _serviceProvider.GetRequiredService<SearchPaneViewModel>(),
            "Settings" => _serviceProvider.GetRequiredService<SettingsPaneViewModel>(),
            "ModDatabase" => _serviceProvider.GetRequiredService<ModDatabaseViewModel>(),
            "Profiles" => _serviceProvider.GetRequiredService<ModIndexViewModel>(),
            _ => throw new NotSupportedException()
        };
    }
}

