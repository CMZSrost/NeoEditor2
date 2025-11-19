using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class MapTableViewModel(ObservableCollection<BaseDto> rawItems) : TypedTableViewModel<map>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(map.strName) or nameof(map.strDef);
    }

    protected override bool MatchesFilter(map item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.strDef.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override map CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new map
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            strName = "NewMap" + newSerialId,
            strDef = string.Empty,
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override map CloneItem(map source)
    {
        return new map
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1,
            id = source.id,
            strName = source.strName + " Copy",
            strDef = source.strDef
        };
    }

    protected override int GetItemIndex(map item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(map item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(map item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(map item, int serialId)
    {
        item.serialId_ = serialId;
    }
}