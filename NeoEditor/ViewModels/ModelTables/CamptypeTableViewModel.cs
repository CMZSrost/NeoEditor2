using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class CamptypeTableViewModel(ObservableCollection<BaseDto> rawItems) : TypedTableViewModel<camptype>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(camptype.strDesc);
    }

    protected override bool MatchesFilter(camptype item, string filterText)
    {
        return item.strDesc.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override camptype CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new camptype
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override camptype CloneItem(camptype source)
    {
        return new camptype
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1
        };
    }

    protected override int GetItemIndex(camptype item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(camptype item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(camptype item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(camptype item, int serialId)
    {
        item.serialId_ = serialId;
    }
}