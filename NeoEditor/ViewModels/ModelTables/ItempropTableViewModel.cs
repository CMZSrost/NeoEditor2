using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class ItempropTableViewModel(ObservableCollection<BaseDto> rawItems) : TypedTableViewModel<itemprop>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(itemprop.strPropertyName);
    }

    protected override bool MatchesFilter(itemprop item, string filterText)
    {
        return item.strPropertyName.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override itemprop CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new itemprop
        {
            serialId_ = newSerialId,
            overId_ = -1,
            nID = newSerialId,
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override itemprop CloneItem(itemprop source)
    {
        return new itemprop
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1
        };
    }

    protected override int GetItemIndex(itemprop item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(itemprop item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(itemprop item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(itemprop item, int serialId)
    {
        item.serialId_ = serialId;
    }
}