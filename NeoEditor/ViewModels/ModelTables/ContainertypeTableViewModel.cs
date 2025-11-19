using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class ContainertypeTableViewModel(ObservableCollection<BaseDto> rawItems)
    : TypedTableViewModel<containertype>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(containertype.strName);
    }

    protected override bool MatchesFilter(containertype item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override containertype CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new containertype
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override containertype CloneItem(containertype source)
    {
        return new containertype
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1
        };
    }

    protected override int GetItemIndex(containertype item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(containertype item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(containertype item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(containertype item, int serialId)
    {
        item.serialId_ = serialId;
    }
}