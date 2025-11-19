using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class ChargeprofileTableViewModel(ObservableCollection<BaseDto> rawItems)
    : TypedTableViewModel<chargeprofile>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(chargeprofile.strName);
    }

    protected override bool MatchesFilter(chargeprofile item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override chargeprofile CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new chargeprofile
        {
            serialId_ = newSerialId,
            overId_ = -1,
            nID = newSerialId,
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override chargeprofile CloneItem(chargeprofile source)
    {
        return new chargeprofile
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1
        };
    }

    protected override int GetItemIndex(chargeprofile item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(chargeprofile item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(chargeprofile item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(chargeprofile item, int serialId)
    {
        item.serialId_ = serialId;
    }
}