using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class BarterhexTableViewModel(ObservableCollection<BaseDto> rawItems) : TypedTableViewModel<barterhex>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(barterhex.nX) or nameof(barterhex.nY);
    }

    protected override bool MatchesFilter(barterhex item, string filterText)
    {
        return item.nX.ToString().Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.nY.ToString().Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override barterhex CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new barterhex
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            nX = 0,
            nY = 0,
            bBuys = false,
            nRestockTreasureID = 0,
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override barterhex CloneItem(barterhex source)
    {
        return new barterhex
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1,
            id = source.id,
            nX = source.nX,
            nY = source.nY,
            bBuys = source.bBuys,
            nRestockTreasureID = source.nRestockTreasureID
        };
    }

    protected override int GetItemIndex(barterhex item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(barterhex item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(barterhex item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(barterhex item, int serialId)
    {
        item.serialId_ = serialId;
    }
}