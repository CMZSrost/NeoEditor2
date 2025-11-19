using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class HeadlineTableViewModel(ObservableCollection<BaseDto> rawItems) : TypedTableViewModel<headline>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(headline.strHeadline);
    }

    protected override bool MatchesFilter(headline item, string filterText)
    {
        return item.strHeadline.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override headline CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new headline
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override headline CloneItem(headline source)
    {
        return new headline
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1
        };
    }

    protected override int GetItemIndex(headline item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(headline item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(headline item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(headline item, int serialId)
    {
        item.serialId_ = serialId;
    }
}