using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeoEditor.Data.Models.Dto;
using NeoEditor.Interface;

namespace NeoEditor.ViewModels.ModelTables;

public partial class ReflectionTableViewModel : ObservableObject, ITableViewModel
{
    private readonly Type _dtoType;
    private readonly ObservableCollection<BaseDto> _rawItems;

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private BaseDto? _selectedItem;

    public ReflectionTableViewModel(ObservableCollection<BaseDto> rawItems, Type dtoType)
    {
        _rawItems = rawItems;
        _dtoType = dtoType;
        foreach (var obj in rawItems.Where(o => o.GetType() == dtoType))
            Items.Add(obj);
        ApplyFilter();
    }

    public string SelectedDisplay => SelectedItem?.ToString() ?? string.Empty;

    public ObservableCollection<BaseDto> Items { get; } = new();
    public ObservableCollection<BaseDto> FilteredItems { get; } = new();

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredItems.Clear();
        var query = FilterText?.Trim();
        foreach (var item in Items)
            if (string.IsNullOrWhiteSpace(query) || ItemMatches(item, query))
                FilteredItems.Add(item);
        if (SelectedItem != null && !FilteredItems.Contains(SelectedItem))
            SelectedItem = FilteredItems.FirstOrDefault();
    }

    private bool ItemMatches(object item, string query)
    {
        foreach (var prop in _dtoType.GetProperties())
            if (prop.PropertyType == typeof(string))
            {
                var str = prop.GetValue(item) as string;
                if (!string.IsNullOrEmpty(str) && str.Contains(query, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

        return false;
    }

    private bool CanModify()
    {
        return SelectedItem != null;
    }

    [RelayCommand]
    private void Add()
    {
        var instanceObj = Activator.CreateInstance(_dtoType);
        if (instanceObj as BaseDto is not { } instance) return;

        // Use direct property access since all DTOs inherit from BaseDto
        var maxIdx = Items.Count > 0 ? Items.Max(i => i.idx) : -1;
        var maxSerial = Items.Count > 0 ? Items.Max(i => i.serialId_) : 0;
        instance.idx = maxIdx + 1;
        instance.serialId_ = maxSerial + 1;

        Items.Add(instance);
        _rawItems.Add(instance);
        SelectedItem = instance;
        ApplyFilter();
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private void Delete()
    {
        if (SelectedItem == null) return;
        _rawItems.Remove(SelectedItem);
        Items.Remove(SelectedItem);
        SelectedItem = null;
        Reindex();
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private void Duplicate()
    {
        if (SelectedItem == null) return;
        var cloneObj = Activator.CreateInstance(_dtoType);
        if (cloneObj is not BaseDto clone) return;

        // Copy all specific DTO properties (not base class properties)
        foreach (var prop in _dtoType.GetProperties())
        {
            if (!prop.CanWrite) continue;
            // Skip base class properties - we'll handle them separately
            if (prop.DeclaringType == typeof(BaseDto)) continue;
            var value = prop.GetValue(SelectedItem);
            prop.SetValue(clone, value);
        }

        // Copy base class properties
        clone.modName = SelectedItem.modName;
        clone.modIndex = SelectedItem.modIndex;
        clone.overId_ = SelectedItem.overId_;
        clone.isLast_ = SelectedItem.isLast_;

        // Assign new idx and serialId
        var maxIdx = Items.Count > 0 ? Items.Max(i => i.idx) : -1;
        var maxSerial = Items.Count > 0 ? Items.Max(i => i.serialId_) : 0;
        clone.idx = maxIdx + 1;
        clone.serialId_ = maxSerial + 1;

        Items.Add(clone);
        _rawItems.Add(clone);
        SelectedItem = clone;
        ApplyFilter();
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private void MoveUp()
    {
        if (SelectedItem == null) return;
        var index = Items.IndexOf(SelectedItem);
        if (index <= 0) return;
        Items.Move(index, index - 1);
        Reindex();
    }

    [RelayCommand(CanExecute = nameof(CanModify))]
    private void MoveDown()
    {
        if (SelectedItem == null) return;
        var index = Items.IndexOf(SelectedItem);
        if (index < 0 || index >= Items.Count - 1) return;
        Items.Move(index, index + 1);
        Reindex();
    }

    [RelayCommand]
    private void Reindex()
    {
        for (var i = 0; i < Items.Count; i++) Items[i].idx = i;
        ApplyFilter();
    }

    partial void OnSelectedItemChanged(BaseDto? value)
    {
        DeleteCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }
}