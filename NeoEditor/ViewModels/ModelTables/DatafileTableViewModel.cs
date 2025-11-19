using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class DatafileTableViewModel(ObservableCollection<BaseDto> rawItems) : TypedTableViewModel<datafile>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(datafile.strName) or nameof(datafile.strDesc);
    }

    protected override bool MatchesFilter(datafile item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.strDesc.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override datafile CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new datafile
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override datafile CloneItem(datafile source)
    {
        return new datafile
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1
        };
    }

    protected override int GetItemIndex(datafile item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(datafile item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(datafile item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(datafile item, int serialId)
    {
        item.serialId_ = serialId;
    }
}