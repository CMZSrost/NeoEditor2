using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NeoEditor.ViewModels.ModelTables;

// Standardized base view model for typed DTO tables. Renamed from BaseTableViewModel to TypedTableViewModel.
public abstract partial class TypedTableViewModel<T> : ObservableObject, ITableViewModel where T : ObservableObject, new()
{
    private readonly ObservableCollection<object> _rawItems;

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private T? _selectedItem;

    protected TypedTableViewModel(ObservableCollection<object> rawItems)
    {
        _rawItems = rawItems;
        foreach (var item in rawItems.OfType<T>())
        {
            HookItem(item);
            Items.Add(item);
            ItemsObject.Add(item); // keep object collection in sync
        }

        _rawItems.CollectionChanged += (_, _) => SyncFromRaw();
        Items.CollectionChanged += OnItemsCollectionChanged;

        ApplyFilter();
    }

    // Typed collections
    public ObservableCollection<T> Items { get; } = new();
    public ObservableCollection<T> FilteredItems { get; } = new();

    // Object-level collections for uniform binding with GenericEditableTable if needed
    public ObservableCollection<object> ItemsObject { get; } = new();
    public ObservableCollection<object> FilteredItemsObject { get; } = new();

    public object? SelectedItemObject
    {
        get => SelectedItem;
        set => SelectedItem = value as T;
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (var obj in e.NewItems.OfType<T>())
            {
                HookItem(obj);
                if (!ItemsObject.Contains(obj)) ItemsObject.Add(obj);
            }
        if (e.OldItems != null)
            foreach (var obj in e.OldItems.OfType<T>())
            {
                UnhookItem(obj);
                ItemsObject.Remove(obj);
            }
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
        FilteredItemsObject.Clear();
        var query = FilterText?.Trim();
        foreach (var item in Items)
            if (string.IsNullOrWhiteSpace(query) || MatchesFilter(item, query))
            {
                FilteredItems.Add(item);
                FilteredItemsObject.Add(item);
            }

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
                ItemsObject.Add(item);
            }

        for (var i = Items.Count - 1; i >= 0; i--)
            if (!_rawItems.Contains(Items[i]))
            {
                UnhookItem(Items[i]);
                ItemsObject.Remove(Items[i]);
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
        ItemsObject.Add(newItem);
        _rawItems.Add(newItem);
        SelectedItem = newItem;
        ApplyFilter();
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    protected virtual void Delete()
    {
        if (SelectedItem == null) return;
        _rawItems.Remove(SelectedItem);
        ItemsObject.Remove(SelectedItem);
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
        ItemsObject.Add(clone);
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
        ItemsObject.Move(idxCurrent, idxCurrent - 1);
        Reindex();
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    protected virtual void MoveDown()
    {
        if (SelectedItem == null) return;
        var idxCurrent = Items.IndexOf(SelectedItem);
        if (idxCurrent < 0 || idxCurrent >= Items.Count - 1) return;
        Items.Move(idxCurrent, idxCurrent + 1);
        ItemsObject.Move(idxCurrent, idxCurrent + 1);
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