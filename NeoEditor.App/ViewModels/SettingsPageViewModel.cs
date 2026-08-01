using System;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;
using NeoEditor.ViewModels.ExplorerPane;
using NeoEditor.ViewModels.MainContent;

namespace NeoEditor.ViewModels;

public partial class SettingsPageViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private SettingsPaneViewModel? _settingsPane;

    public SettingsPaneViewModel SettingsPane =>
        _settingsPane ??= _serviceProvider.GetRequiredService<SettingsPaneViewModel>();

    public SettingsPageViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    [RelayCommand]
    private void GoBack()
    {
        // Return to workspace if documents are open, otherwise return to home
        var workspace = _serviceProvider.GetRequiredService<DocumentWorkspaceViewModel>();
        var targetPage = workspace.Documents.Count > 0 ? PageType.Workspace : PageType.Home;
        Messenger.Send(new NavigateToPageMessage(targetPage));
    }
}
