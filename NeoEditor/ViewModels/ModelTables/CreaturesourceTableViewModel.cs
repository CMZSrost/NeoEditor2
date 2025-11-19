using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class CreaturesourceTableViewModel(ObservableCollection<BaseDto> rawItems)
    : TypedTableViewModel<creaturesource>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(creaturesource.strName);
    }

    protected override bool MatchesFilter(creaturesource item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override creaturesource CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new creaturesource
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override creaturesource CloneItem(creaturesource source)
    {
        return new creaturesource
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1
        };
    }

    protected override int GetItemIndex(creaturesource item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(creaturesource item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(creaturesource item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(creaturesource item, int serialId)
    {
        item.serialId_ = serialId;
    }
}