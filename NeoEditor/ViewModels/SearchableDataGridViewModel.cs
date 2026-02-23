using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeoEditor.Data.Model;
using NeoEditor.Data.Model.Game;

namespace NeoEditor.ViewModels;

public partial class SearchableDataGridViewModel<T> : ViewModelBase where T : class
{
    public SearchableDataGridViewModel()
    {
        _logger = App.ServiceProvider!.GetRequiredService<ILogger<SearchableDataGridViewModel<T>>>();
    }

    [ObservableProperty] public partial IEnumerable<T>? ItemsSource { get; set; }

    [ObservableProperty] public partial string FilterText { get; set; } = string.Empty;
    private DataGridCollectionView? _collectionView;
    private readonly ILogger<SearchableDataGridViewModel<T>> _logger;
    public IDataGridCollectionView? CollectionView => _collectionView;

    partial void OnItemsSourceChanged(IEnumerable<T>? value)
    {
        if (value != null)
        {
            _collectionView = new DataGridCollectionView(value.ToList()) // 转为 List 以便支持排序过滤
            {
                Filter = FilterPredicate
            };
            _logger.LogDebug(
                "ItemsSource changed, creating new DataGridCollectionView with {Count} items to {CountView}",
                value.Count(), _collectionView.Count);
        }
        else
        {
            _logger.LogDebug("ItemsSource changed, to null");
            _collectionView = null;
        }

        OnPropertyChanged(nameof(CollectionView));
    }

    partial void OnFilterTextChanged(string value)
    {
        _collectionView?.Refresh(); // 重新应用过滤
    }
    private bool FilterPredicate(object obj)
    {
        if (string.IsNullOrWhiteSpace(FilterText))
            return true;

        var item = (T)obj;
        return item?.GetType().GetProperties()
            .Any(prop => prop.GetValue(item)?.ToString()?.Contains(FilterText, StringComparison.OrdinalIgnoreCase) == true) ?? false;
    }
    
}

public class AttackModeDataGridViewModel : SearchableDataGridViewModel<AttackMode>;

public class GameVarDataGridViewModel : SearchableDataGridViewModel<GameVar>;