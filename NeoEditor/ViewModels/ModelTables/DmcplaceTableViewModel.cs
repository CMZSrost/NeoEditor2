using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class DmcplaceTableViewModel(ObservableCollection<BaseDto> rawItems) : TypedTableViewModel<dmcplace>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(dmcplace.strImg);
    }

    protected override bool MatchesFilter(dmcplace item, string filterText)
    {
        return item.strImg.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override dmcplace CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new dmcplace
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override dmcplace CloneItem(dmcplace source)
    {
        return new dmcplace
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1
        };
    }

    protected override int GetItemIndex(dmcplace item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(dmcplace item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(dmcplace item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(dmcplace item, int serialId)
    {
        item.serialId_ = serialId;
    }
}