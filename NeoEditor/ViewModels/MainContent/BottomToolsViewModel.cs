using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using NeoEditor.Helper;
using NeoEditor.Services;
using NeoEditor.ViewModels;

namespace NeoEditor.ViewModels.MainContent;

public partial class BottomToolsViewModel : ViewModelBase
{
    private readonly ISearchService _searchService;
    private const int MaxRecentSearches = 15;

    [ObservableProperty] public partial int SelectedTabIndex { get; set; }

    // Search
    [ObservableProperty] public partial string BottomSearchText { get; set; } = "";
    [ObservableProperty] public partial bool IsBottomSearching { get; set; }
    public ObservableCollection<SearchResultGroup> SearchResultGroups { get; } = [];
    [ObservableProperty] public partial string SearchSummary { get; set; } = "No search performed.";
    public ObservableCollection<string> RecentSearches { get; } = [];
    public bool HasRecentSearches => RecentSearches.Count > 0;

    // Conflicts
    public ObservableCollection<ConflictEntryViewModel> ConflictEntries { get; } = [];
    [ObservableProperty] public partial string ConflictSummary { get; set; } = "No conflicts detected.";

    // Validation
    public ObservableCollection<ValidationEntryViewModel> ValidationEntries { get; } = [];
    [ObservableProperty] public partial string ValidationSummary { get; set; } = "No validation run.";

    public BottomToolsViewModel() : this(
        App.ServiceProvider!.GetRequiredService<ISearchService>())
    {
    }

    public BottomToolsViewModel(ISearchService searchService)
    {
        _searchService = searchService;
    }

    [RelayCommand]
    private async Task BottomSearch()
    {
        var query = BottomSearchText?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            SearchResultGroups.Clear();
            SearchSummary = "Enter a search term.";
            return;
        }

        // Track recent search
        RecentSearches.Remove(query);
        RecentSearches.Insert(0, query);
        while (RecentSearches.Count > MaxRecentSearches)
            RecentSearches.RemoveAt(RecentSearches.Count - 1);

        IsBottomSearching = true;
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

        IsBottomSearching = false;
    }

    [RelayCommand]
    private void ClearSearch()
    {
        BottomSearchText = "";
        SearchResultGroups.Clear();
        SearchSummary = "No search performed.";
    }

    [RelayCommand]
    private void SearchRecent(string query)
    {
        BottomSearchText = query;
        BottomSearchCommand.Execute(null);
    }

    public void NavigateToResult(SearchResultItem? item)
    {
        if (item is null) return;
        GenericDataGridHelper.NavigateToByEntityId(item.EntityType, item.Entity.EntityId);
    }

    public void LoadConflicts()
    {
        var conflicts = Helper.GenericDataGridHelper.FieldConflicts;
        ConflictEntries.Clear();
        if (conflicts.Count == 0)
        {
            ConflictSummary = "No conflicts detected.";
            return;
        }

        var grouped = conflicts
            .GroupBy(c => c.Item1)
            .Select(g =>
            {
                var fields = string.Join(", ", g.Select(c => c.Item2));
                return new ConflictEntryViewModel(g.Key, fields, g.Count());
            })
            .OrderByDescending(c => c.FieldCount)
            .ToList();

        foreach (var g in grouped)
            ConflictEntries.Add(g);

        ConflictSummary = $"{conflicts.Count} field conflict(s) across {grouped.Count} entities.";
    }

    public void SetValidationResults(string summary, params string[] entries)
    {
        ValidationSummary = summary;
        ValidationEntries.Clear();
        foreach (var e in entries.Take(100))
            ValidationEntries.Add(new ValidationEntryViewModel(e));
    }
}

public partial class ConflictEntryViewModel : ObservableObject
{
    public string EntityId { get; }
    public string Fields { get; }
    public int FieldCount { get; }
    public string FirstField { get; }

    public ConflictEntryViewModel(string entityId, string fields, int fieldCount)
    {
        EntityId = entityId;
        Fields = fields;
        FieldCount = fieldCount;
        FirstField = fields.Split(',').FirstOrDefault()?.Trim() ?? "";
    }
}

public partial class ValidationEntryViewModel : ObservableObject
{
    public string Message { get; }

    public ValidationEntryViewModel(string message) => Message = message;
}
