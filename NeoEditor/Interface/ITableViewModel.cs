using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.Interface;

public interface ITableViewModel
{
    string FilterText { get; set; }
    BaseDto? SelectedItem { get; set; }
    ObservableCollection<BaseDto> Items { get; }
    ObservableCollection<BaseDto> FilteredItems { get; }
}