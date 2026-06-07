using System.Collections.Generic;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using NeoEditor.Data.Messages;

namespace NeoEditor.ViewModels;

public partial class AppConfig : ViewModelBase
{
    [ObservableProperty] public partial string GameRootDir { get; set; } = Path.GetFullPath("./");
    [ObservableProperty] public partial string Language { get; set; } = "zh";
    [ObservableProperty] public partial string Theme { get; set; } = "System";
    [ObservableProperty] public partial int FontSize { get; set; } = 12;
    [ObservableProperty] public partial int ActiveProfileId { get; set; } = -1;
    [ObservableProperty] public partial int AutoSaveInterval { get; set; } = 0;
    [ObservableProperty] public partial string DefaultExportFormat { get; set; } = "csv";
    [ObservableProperty] public partial int GridRowHeight { get; set; } = 0;
    [ObservableProperty] public partial int SnapshotInterval { get; set; } = 10;

    // Panel layout persistence
    [ObservableProperty] public partial double LeftPanelWidth { get; set; } = 220;
    [ObservableProperty] public partial double RightPanelWidth { get; set; } = 280;
    [ObservableProperty] public partial double BottomPanelHeight { get; set; } = 150;
    [ObservableProperty] public partial bool LeftPanelVisible { get; set; } = true;
    [ObservableProperty] public partial bool RightPanelVisible { get; set; } = true;
    [ObservableProperty] public partial bool BottomPanelVisible { get; set; } = true;

    /// <summary>Per-table visible column sets. Table not in dict → default (hide ModId/FilePath/EntityId).</summary>
    public Dictionary<string, HashSet<string>> ColumnVisibility { get; set; } = new();

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