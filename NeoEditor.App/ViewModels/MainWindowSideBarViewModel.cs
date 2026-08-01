using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeoEditor.ViewModels.ExplorerPane;

namespace NeoEditor.ViewModels;

public partial class MainWindowSideBarViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MainWindowSideBarViewModel> _logger;
    private string _currentPaneId = string.Empty;

    public MainWindowSideBarViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<MainWindowSideBarViewModel>>();
        IsActive = true;
    }

    [ObservableProperty] public partial bool SideBarExpanded { get; set; }
    [ObservableProperty] public partial object? CurrentPaneContent { get; set; }

    [RelayCommand]
    private void TogglePane(string? paneId)
    {
        try
        {
            if (string.IsNullOrEmpty(paneId))
            {
                SideBarExpanded = false;
                CurrentPaneContent = null;
                return;
            }

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
            "Workspace" => _serviceProvider.GetRequiredService<WorkspaceHistoryViewModel>(),
            _ => throw new NotSupportedException()
        };
    }
}
