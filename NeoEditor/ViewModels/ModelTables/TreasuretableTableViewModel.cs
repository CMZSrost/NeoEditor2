using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class TreasuretableTableViewModel(ObservableCollection<BaseDto> rawItems)
    : TypedTableViewModel<treasuretable>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(treasuretable.strName) or nameof(treasuretable.aTreasures);
    }

    protected override bool MatchesFilter(treasuretable item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.aTreasures.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override treasuretable CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new treasuretable
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            strName = "NewTreasure" + newSerialId,
            aTreasures = string.Empty,
            modName = string.Empty,
            modIndex = 0,
            bNested = false,
            bSuppress = false,
            bIdentify = false
        };
    }

    protected override treasuretable CloneItem(treasuretable source)
    {
        return new treasuretable
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1,
            id = source.id,
            strName = source.strName + " Copy",
            aTreasures = source.aTreasures,
            bNested = source.bNested,
            bSuppress = source.bSuppress,
            bIdentify = source.bIdentify
        };
    }

    protected override int GetItemIndex(treasuretable item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(treasuretable item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(treasuretable item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(treasuretable item, int serialId)
    {
        item.serialId_ = serialId;
    }
}