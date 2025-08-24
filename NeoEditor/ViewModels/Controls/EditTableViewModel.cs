using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HandyControl.Controls;

namespace NeoEditor.ViewModels.Controls;

public partial class EditTableViewModel : ObservableRecipient
{
    public EditTableViewModel(IEnumerable<TabItem> tabs)
    {
        var tabItems = tabs as TabItem[] ?? tabs.ToArray();
        Console.WriteLine($"tabs count:{tabItems.Length}");
        IsActive = true;
        Tabs = new ObservableCollection<TabItem>(tabItems);
    }

    public ObservableCollection<TabItem> Tabs { get; set; }

    [ObservableProperty] public partial string filter { get; set; } = string.Empty;
}