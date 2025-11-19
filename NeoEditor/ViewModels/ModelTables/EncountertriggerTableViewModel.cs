using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class EncountertriggerTableViewModel(ObservableCollection<BaseDto> rawItems)
    : TypedTableViewModel<encountertrigger>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(encountertrigger.strName);
    }

    protected override bool MatchesFilter(encountertrigger item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override encountertrigger CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new encountertrigger
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override encountertrigger CloneItem(encountertrigger source)
    {
        return new encountertrigger
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1
        };
    }

    protected override int GetItemIndex(encountertrigger item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(encountertrigger item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(encountertrigger item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(encountertrigger item, int serialId)
    {
        item.serialId_ = serialId;
    }
}