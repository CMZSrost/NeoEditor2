namespace NeoEditor.ViewModels.ModelTables;

public interface ITableViewModel
{
    string FilterText { get; set; }
    object? SelectedItemObject { get; set; }
    System.Collections.ObjectModel.ObservableCollection<object> ItemsObject { get; }
    System.Collections.ObjectModel.ObservableCollection<object> FilteredItemsObject { get; }
}

