using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class FactionTableViewModel(ObservableCollection<BaseDto> rawItems) : TypedTableViewModel<faction>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(faction.strName) or nameof(faction.dictFactions);
    }

    protected override bool MatchesFilter(faction item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.dictFactions.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override faction CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new faction
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            strName = "NewFaction" + newSerialId,
            dictFactions = string.Empty,
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override faction CloneItem(faction source)
    {
        return new faction
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1,
            id = source.id,
            strName = source.strName + " Copy",
            dictFactions = source.dictFactions
        };
    }

    protected override int GetItemIndex(faction item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(faction item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(faction item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(faction item, int serialId)
    {
        item.serialId_ = serialId;
    }
}