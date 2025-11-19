using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class HextypeTableViewModel(ObservableCollection<BaseDto> rawItems) : TypedTableViewModel<hextype>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(hextype.strName) or nameof(hextype.strDesc);
    }

    protected override bool MatchesFilter(hextype item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.strDesc.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override hextype CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new hextype
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            strName = "NewHextype" + newSerialId,
            strDesc = string.Empty,
            modName = string.Empty,
            modIndex = 0,
            nTerrainCost = 0,
            nVizLimiter = 0,
            nVizIncrease = 0,
            nTreasureID = 0,
            bPassable = true,
            nScavengeInitialID = 0,
            nScavengeItemsIDPerHour = 0,
            nCampItems = 0,
            vLightLevels = string.Empty,
            nDefaultCampID = 0,
            nMinRange = 0,
            nMaxRange = 0,
            vCondIDs = string.Empty
        };
    }

    protected override hextype CloneItem(hextype source)
    {
        return new hextype
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1,
            id = source.id,
            strName = source.strName + " Copy",
            strDesc = source.strDesc,
            nTerrainCost = source.nTerrainCost,
            nVizLimiter = source.nVizLimiter,
            nVizIncrease = source.nVizIncrease,
            nTreasureID = source.nTreasureID,
            bPassable = source.bPassable,
            nScavengeInitialID = source.nScavengeInitialID,
            nScavengeItemsIDPerHour = source.nScavengeItemsIDPerHour,
            nCampItems = source.nCampItems,
            vLightLevels = source.vLightLevels,
            nDefaultCampID = source.nDefaultCampID,
            nMinRange = source.nMinRange,
            nMaxRange = source.nMaxRange,
            vCondIDs = source.vCondIDs
        };
    }

    protected override int GetItemIndex(hextype item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(hextype item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(hextype item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(hextype item, int serialId)
    {
        item.serialId_ = serialId;
    }
}