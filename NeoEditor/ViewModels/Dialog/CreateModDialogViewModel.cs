using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Data.Messages;
using NeoEditor.Services;

namespace NeoEditor.ViewModels.Dialog;

public partial class CreateModDialogViewModel : ViewModelBase
{
    [ObservableProperty] public partial string Author { get; set; } = "";
    [ObservableProperty] public partial string Name { get; set; } = "";

    public EventHandler? CloseRequested;
    private IModManager _modManager;

    public CreateModDialogViewModel() : this(App.ServiceProvider.GetRequiredService<IModManager>())
    {
    }

    public CreateModDialogViewModel(IModManager modManager)
    {
        _modManager = modManager;
    }

    [RelayCommand]
    private async Task Confirm()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Author))
        {
            App.Notification.ShowError(Loc["ModNameRequired"], Loc["Error"]);
            return;
        }

        await _modManager.CreateModAsync(Name, Author);
        Messenger.Send(new RefreshModMessage());
        App.Notification.ShowSuccess(Loc["CreateModSuccess"], Loc["Success"]);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        App.Notification.ShowWarning(Loc["ModCreationCancelled"], Loc["Cancelled"]);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}