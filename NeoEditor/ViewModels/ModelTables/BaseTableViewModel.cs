using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.Data.Models.Dto;
using NeoEditor.Interface;

namespace NeoEditor.ViewModels.ModelTables;

// Standardized base view model for typed DTO tables. Renamed from BaseTableViewModel to TypedTableViewModel.
public abstract partial class TypedTableViewModel<T> : ObservableObject, ITableViewModel
    where T : BaseDto, INotifyPropertyChanged
{
    private readonly ObservableCollection<BaseDto> _rawItems;
    private ObservableCollection<BaseDto>? _itemsCache;
    private ObservableCollection<BaseDto>? _filteredItemsCache;

    [ObservableProperty] private string _filterText = string.Empty;

    [ObservableProperty] private T? _selectedItem;

    protected TypedTableViewModel(ObservableCollection<BaseDto> rawItems)
    {
        _rawItems = rawItems;
        foreach (var item in rawItems.OfType<T>())
        {
            HookItem(item);
            Items.Add(item);
        }

        _rawItems.CollectionChanged += (_, _) => SyncFromRaw();
        Items.CollectionChanged += OnItemsCollectionChanged;

        ApplyFilter();
    }

    // Typed collections
    public ObservableCollection<T> Items { get; } = new();
    public ObservableCollection<T> FilteredItems { get; } = new();

    // Explicit interface implementations for ITableViewModel
    ObservableCollection<BaseDto> ITableViewModel.Items
    {
        get
        {
            if (_itemsCache == null)
            {
                _itemsCache = new ObservableCollection<BaseDto>();
                // Sync initial items
                foreach (var item in Items)
                    _itemsCache.Add(item);
                // Keep synchronized
                Items.CollectionChanged += (s, e) => SyncCache(_itemsCache, e);
            }

            return _itemsCache;
        }
    }

    ObservableCollection<BaseDto> ITableViewModel.FilteredItems
    {
        get
        {
            if (_filteredItemsCache == null)
            {
                _filteredItemsCache = new ObservableCollection<BaseDto>();
                // Sync initial items
                foreach (var item in FilteredItems)
                    _filteredItemsCache.Add(item);
                // Keep synchronized
                FilteredItems.CollectionChanged += (s, e) => SyncCache(_filteredItemsCache, e);
            }

            return _filteredItemsCache;
        }
    }

    BaseDto? ITableViewModel.SelectedItem
    {
        get => SelectedItem;
        set => SelectedItem = value as T;
    }

    private void SyncCache(ObservableCollection<BaseDto> cache, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                    foreach (BaseDto item in e.NewItems)
                        cache.Insert(e.NewStartingIndex, item);
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                    foreach (BaseDto _ in e.OldItems)
                        cache.RemoveAt(e.OldStartingIndex);
                break;
            case NotifyCollectionChangedAction.Replace:
                if (e.NewItems != null)
                    cache[e.NewStartingIndex] = (BaseDto)e.NewItems[0]!;
                break;
            case NotifyCollectionChangedAction.Move:
                cache.Move(e.OldStartingIndex, e.NewStartingIndex);
                break;
            case NotifyCollectionChangedAction.Reset:
                cache.Clear();
                break;
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (var obj in e.NewItems.OfType<T>())
                HookItem(obj);
        if (e.OldItems != null)
            foreach (var obj in e.OldItems.OfType<T>())
                UnhookItem(obj);
        ApplyFilter();
    }

    private void HookItem(T item)
    {
        item.PropertyChanged += ItemOnPropertyChanged;
    }

    private void UnhookItem(T item)
    {
        item.PropertyChanged -= ItemOnPropertyChanged;
    }

    protected virtual void ItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ShouldRefilterOnPropertyChange(e.PropertyName))
            if (!string.IsNullOrWhiteSpace(FilterText))
                ApplyFilter();
    }

    protected abstract bool ShouldRefilterOnPropertyChange(string? propertyName);
    protected abstract bool MatchesFilter(T item, string filterText);
    protected abstract T CreateNewItem();
    protected abstract T CloneItem(T source);
    protected abstract int GetItemIndex(T item);
    protected abstract void SetItemIndex(T item, int index);
    protected abstract int GetItemSerialId(T item);
    protected abstract void SetItemSerialId(T item, int serialId);

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    protected virtual void ApplyFilter()
    {
        FilteredItems.Clear();
        var query = FilterText?.Trim();
        foreach (var item in Items)
            if (string.IsNullOrWhiteSpace(query) || MatchesFilter(item, query))
                FilteredItems.Add(item);

        if (SelectedItem != null && !FilteredItems.Contains(SelectedItem))
            SelectedItem = FilteredItems.FirstOrDefault();
    }

    private void SyncFromRaw()
    {
        var currentSet = Items.ToHashSet();
        foreach (var item in _rawItems.OfType<T>())
            if (!currentSet.Contains(item))
            {
                HookItem(item);
                Items.Add(item);
            }

        for (var i = Items.Count - 1; i >= 0; i--)
            if (!_rawItems.Contains(Items[i]))
            {
                UnhookItem(Items[i]);
                Items.RemoveAt(i);
            }

        ApplyFilter();
    }

    protected virtual bool CanModify()
    {
        return SelectedItem != null;
    }

    [RelayCommand]
    protected virtual void Add()
    {
        var newIdx = Items.Count > 0 ? Items.Max(GetItemIndex) + 1 : 0;
        var newSerialId = Items.Count > 0 ? Items.Max(GetItemSerialId) + 1 : 1;

        var newItem = CreateNewItem();
        SetItemIndex(newItem, newIdx);
        SetItemSerialId(newItem, newSerialId);

        HookItem(newItem);
        Items.Add(newItem);
        _rawItems.Add(newItem);
        SelectedItem = newItem;
        ApplyFilter();
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    protected virtual void Delete()
    {
        if (SelectedItem == null) return;
        _rawItems.Remove(SelectedItem);
        Items.Remove(SelectedItem);
        SelectedItem = null;
        Reindex();
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    protected virtual void Duplicate()
    {
        if (SelectedItem == null) return;
        var src = SelectedItem;

        var clone = CloneItem(src);
        var newIdx = Items.Count > 0 ? Items.Max(GetItemIndex) + 1 : 0;
        var newSerialId = Items.Count > 0 ? Items.Max(GetItemSerialId) + 1 : 1;

        SetItemIndex(clone, newIdx);
        SetItemSerialId(clone, newSerialId);

        HookItem(clone);
        Items.Add(clone);
        _rawItems.Add(clone);
        SelectedItem = clone;
        ApplyFilter();
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    protected virtual void MoveUp()
    {
        if (SelectedItem == null) return;
        var idxCurrent = Items.IndexOf(SelectedItem);
        if (idxCurrent <= 0) return;
        Items.Move(idxCurrent, idxCurrent - 1);
        Reindex();
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    protected virtual void MoveDown()
    {
        if (SelectedItem == null) return;
        var idxCurrent = Items.IndexOf(SelectedItem);
        if (idxCurrent < 0 || idxCurrent >= Items.Count - 1) return;
        Items.Move(idxCurrent, idxCurrent + 1);
        Reindex();
    }

    [RelayCommand]
    protected virtual void Reindex()
    {
        for (var i = 0; i < Items.Count; i++) SetItemIndex(Items[i], i);
        ApplyFilter();
    }

    partial void OnSelectedItemChanged(T? value)
    {
        DeleteCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }
}