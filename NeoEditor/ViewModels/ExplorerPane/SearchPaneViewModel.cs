using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Helper;
using NeoEditor.Services;

namespace NeoEditor.ViewModels.ExplorerPane;

public partial class SearchPaneViewModel : ViewModelBase
{
    private readonly ISearchService _searchService;

    public ObservableCollection<string> RecentSearches { get; } = [];
    public ObservableCollection<SearchResultGroup> Results { get; } = [];

    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial bool IsSearching { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = "";

    public SearchPaneViewModel() : this(
        App.ServiceProvider!.GetRequiredService<ISearchService>())
    {
    }

    public SearchPaneViewModel(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [RelayCommand]
    private async Task Search()
    {
        var query = SearchText?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            Results.Clear();
            StatusText = "";
            return;
        }

        if (!RecentSearches.Contains(query))
        {
            RecentSearches.Insert(0, query);
            while (RecentSearches.Count > 20) RecentSearches.RemoveAt(RecentSearches.Count - 1);
        }

        IsSearching = true;
        Results.Clear();
        StatusText = "Searching...";

        var (groups, statusText) = await _searchService.SearchAsync(query);

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var g in groups) Results.Add(g);
            StatusText = statusText;
        });

        IsSearching = false;
    }

    [RelayCommand]
    private void NavigateToResult(SearchResultItem? item)
    {
        if (item is null) return;
        GenericDataGridHelper.NavigateToByEntityId(item.EntityType, item.Entity.EntityId);
    }
}
