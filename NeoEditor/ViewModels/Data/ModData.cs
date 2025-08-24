using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.ViewModels.Data;

public partial class ModData : ObservableObject
{
    [ObservableProperty] public partial string? ModName { get; set; }

    [ObservableProperty] public partial int ModIndex { get; set; }

    [ObservableProperty] public partial string? ModDirectoryPath { get; set; }
    [ObservableProperty] public partial string? ModDirectory { get; set; }
}