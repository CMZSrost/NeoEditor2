using System.Collections.ObjectModel;

namespace NeoEditor.ViewModels.ExplorerPane;

public class SearchPaneViewModel : ViewModelBase
{
    public ObservableCollection<string> RecentSearches { get; } = [];
}