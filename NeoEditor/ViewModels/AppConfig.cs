using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Data.Messages;

namespace NeoEditor.ViewModels;

public partial class AppConfig : ViewModelBase
{
    [ObservableProperty] public partial string GameRootDir { get; set; } = Path.GetFullPath("./");

    public Dictionary<string, List<string>> ModImageOrders { get; set; } = new();

    public AppConfig()
    {
        IsActive = true;
    }

    partial void OnGameRootDirChanged(string value)
    {
        Messenger.Send(new GameRootDirChangedMessage(value));
    }
}