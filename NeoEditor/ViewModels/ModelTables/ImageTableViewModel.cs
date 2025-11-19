using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class ImageTableViewModel(ObservableCollection<BaseDto> rawItems) : TypedTableViewModel<image>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(image.imagePath);
    }

    protected override bool MatchesFilter(image item, string filterText)
    {
        return item.imagePath.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override image CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new image
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override image CloneItem(image source)
    {
        return new image
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1
        };
    }

    protected override int GetItemIndex(image item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(image item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(image item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(image item, int serialId)
    {
        item.serialId_ = serialId;
    }
}