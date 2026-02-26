using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Data.Messages;

namespace NeoEditor.ViewModels;

public partial class AppConfig : ObservableRecipient
{
    [ObservableProperty] public partial string GameRootDir { get; set; } = "";

    public AppConfig()
    {
        IsActive = true;
    }

    partial void OnGameRootDirChanged(string value)
    {
        Messenger.Send(new GameRootDirChangedMessage(value));
    }
}