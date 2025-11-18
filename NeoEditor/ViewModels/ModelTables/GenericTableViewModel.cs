using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NeoEditor.ViewModels.ModelTables;

public partial class ReflectionTableViewModel : ObservableObject, ITableViewModel
{
    private readonly ObservableCollection<object> _rawItems;
    private readonly Type _dtoType;

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private object? _selectedItem;

    public ObservableCollection<object> ItemsObject { get; } = new();
    public ObservableCollection<object> FilteredItemsObject { get; } = new();
    public object? SelectedItemObject { get => SelectedItem; set => SelectedItem = value; }
    public ObservableCollection<object> Items => ItemsObject; // backward compatibility
    public ObservableCollection<object> FilteredItems => FilteredItemsObject; // backward compatibility

    public string SelectedDisplay => SelectedItem?.ToString() ?? string.Empty;

    public ReflectionTableViewModel(ObservableCollection<object> rawItems, Type dtoType)
    {
        _rawItems = rawItems;
        _dtoType = dtoType;
        foreach (var obj in rawItems.Where(o => o.GetType() == dtoType))
            ItemsObject.Add(obj);
        ApplyFilter();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredItemsObject.Clear();
        var query = FilterText?.Trim();
        foreach (var item in ItemsObject)
        {
            if (string.IsNullOrWhiteSpace(query) || ItemMatches(item, query))
                FilteredItemsObject.Add(item);
        }
        if (SelectedItem != null && !FilteredItemsObject.Contains(SelectedItem))
            SelectedItem = FilteredItemsObject.FirstOrDefault();
    }

    private bool ItemMatches(object item, string query)
    {
        foreach (var prop in _dtoType.GetProperties())
        {
            if (prop.PropertyType == typeof(string))
            {
                var str = prop.GetValue(item) as string;
                if (!string.IsNullOrEmpty(str) && str.Contains(query, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    private bool CanModify() => SelectedItem != null;

    [RelayCommand]
    private void Add()
    {
        var instance = Activator.CreateInstance(_dtoType);
        if (instance == null) return;
        // attempt idx and serial assignment via reflection
        var idxProp = _dtoType.GetProperty("idx");
        var serialProp = _dtoType.GetProperty("serialId_");
        if (idxProp != null && idxProp.CanWrite)
        {
            var maxIdx = ItemsObject.Select(i => idxProp.GetValue(i)).OfType<int?>().Max() ?? -1;
            idxProp.SetValue(instance, maxIdx + 1);
        }
        if (serialProp != null && serialProp.CanWrite)
        {
            var maxSerial = ItemsObject.Select(i => serialProp.GetValue(i)).OfType<int?>().Max() ?? 0;
            serialProp.SetValue(instance, maxSerial + 1);
        }
        ItemsObject.Add(instance);
        _rawItems.Add(instance);
        SelectedItem = instance;
        ApplyFilter();
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private void Delete()
    {
        if (SelectedItem == null) return;
        _rawItems.Remove(SelectedItem);
        ItemsObject.Remove(SelectedItem);
        SelectedItem = null;
        Reindex();
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private void Duplicate()
    {
        if (SelectedItem == null) return;
        var clone = Activator.CreateInstance(_dtoType);
        if (clone == null) return;
        foreach (var prop in _dtoType.GetProperties())
        {
            if (!prop.CanWrite) continue;
            var value = prop.GetValue(SelectedItem);
            // skip idx and serial will assign new
            if (prop.Name is "idx" or "serialId_") continue;
            prop.SetValue(clone, value);
        }
        var idxProp = _dtoType.GetProperty("idx");
        var serialProp = _dtoType.GetProperty("serialId_");
        if (idxProp != null && idxProp.CanWrite)
        {
            var maxIdx = ItemsObject.Select(i => idxProp.GetValue(i)).OfType<int?>().Max() ?? -1;
            idxProp.SetValue(clone, maxIdx + 1);
        }
        if (serialProp != null && serialProp.CanWrite)
        {
            var maxSerial = ItemsObject.Select(i => serialProp.GetValue(i)).OfType<int?>().Max() ?? 0;
            serialProp.SetValue(clone, maxSerial + 1);
        }
        ItemsObject.Add(clone);
        _rawItems.Add(clone);
        SelectedItem = clone;
        ApplyFilter();
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private void MoveUp()
    {
        if (SelectedItem == null) return;
        var idxProp = _dtoType.GetProperty("idx");
        if (idxProp == null || !idxProp.CanWrite) return;
        var index = ItemsObject.IndexOf(SelectedItem);
        if (index <= 0) return;
        ItemsObject.Move(index, index - 1);
        Reindex();
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private void MoveDown()
    {
        if (SelectedItem == null) return;
        var idxProp = _dtoType.GetProperty("idx");
        if (idxProp == null || !idxProp.CanWrite) return;
        var index = ItemsObject.IndexOf(SelectedItem);
        if (index < 0 || index >= ItemsObject.Count - 1) return;
        ItemsObject.Move(index, index + 1);
        Reindex();
    }

    [RelayCommand]
    private void Reindex()
    {
        var idxProp = _dtoType.GetProperty("idx");
        if (idxProp == null || !idxProp.CanWrite) return;
        for (var i = 0; i < ItemsObject.Count; i++)
        {
            idxProp.SetValue(ItemsObject[i], i);
        }
        ApplyFilter();
    }

    partial void OnSelectedItemChanged(object? value)
    {
        DeleteCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }
}
