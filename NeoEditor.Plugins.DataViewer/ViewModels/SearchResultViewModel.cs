using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.Helper;
using NeoEditor.Infra.Services;
using NeoEditor.Plugins.DataViewer.Converters;
using NeoEditor.Plugins.DataViewer.Services;
using NeoEditor.Services;

namespace NeoEditor.Plugins.DataViewer.ViewModels;

/// <summary>
/// ViewModel for search results in the DataViewer bottom panel.
/// Extracted from BottomToolsViewModel per M9 plugin migration.
/// </summary>
public partial class SearchResultViewModel : ObservableRecipient
{
    private readonly ISearchService _searchService;
    private const int MaxRecentSearches = 15;

    public ILocalizationService Loc { get; }

    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial bool IsSearching { get; set; }
    public ObservableCollection<SearchResultGroup> SearchResultGroups { get; } = [];
    [ObservableProperty] public partial string SearchSummary { get; set; } = "No search performed.";
    public ObservableCollection<string> RecentSearches { get; } = [];
    public bool HasRecentSearches => RecentSearches.Count > 0;

    public SearchResultViewModel(ISearchService searchService, ILocalizationService loc)
    {
        _searchService = searchService;
        Loc = loc;
        IsActive = true;
    }

    public void NavigateToResult(SearchResultItem? item)
    {
        if (item?.Entity is null) return;
        ConverterServiceHelper.DataTable?.NavigateToByEntityId(item.EntityType, item.Entity.EntityId);
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task Search()
    {
        var query = SearchText?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            SearchResultGroups.Clear();
            SearchSummary = "Enter a search term.";
            return;
        }

        RecentSearches.Remove(query);
        RecentSearches.Insert(0, query);
        while (RecentSearches.Count > MaxRecentSearches)
            RecentSearches.RemoveAt(RecentSearches.Count - 1);

        IsSearching = true;
        SearchResultGroups.Clear();
        SearchSummary = "Searching...";

        var (groups, statusText) = await _searchService.SearchAsync(query);

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            SearchResultGroups.Clear();
            foreach (var g in groups) SearchResultGroups.Add(g);
            var totalItems = groups.Sum(g => g.Items.Count);
            SearchSummary = $"{statusText}  ({totalItems} result(s))";
        });

        IsSearching = false;
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = "";
        SearchResultGroups.Clear();
        SearchSummary = "No search performed.";
    }

    [RelayCommand]
    private void SearchRecent(string query)
    {
        SearchText = query;
        SearchCommand.Execute(null);
    }
}
