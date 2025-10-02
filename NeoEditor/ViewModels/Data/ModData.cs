using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.ViewModels.Data;

public partial class ModData : ObservableObject, IComparable<ModData>
{
    [ObservableProperty] public partial string? ModName { get; set; }

    [ObservableProperty] public partial int ModIndex { get; set; }

    [ObservableProperty] public partial string? ModDirectoryPath { get; set; }
    [ObservableProperty] public partial string? ModDirectoryName { get; set; }

    public int CompareTo(ModData? other)
    {
        return ModIndex.CompareTo(other?.ModIndex);
    }
}

public partial class ModXmlData : ObservableObject
{
    [ObservableProperty] public partial string? ModName { get; set; }

    [ObservableProperty] public partial int ModIndex { get; set; }

    [ObservableProperty] public partial string? XmlPath { get; set; }
}