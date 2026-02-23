using System.Collections.ObjectModel;

namespace NeoEditor.ViewModels.ExplorerPane;

public class ModsIndexViewModel : ViewModelBase
{
    public ObservableCollection<string> RecentSearches { get; } = [];
}