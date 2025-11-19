using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class ForbiddenhexTableViewModel(ObservableCollection<BaseDto> rawItems)
    : TypedTableViewModel<forbiddenhex>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(forbiddenhex.strName);
    }

    protected override bool MatchesFilter(forbiddenhex item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override forbiddenhex CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new forbiddenhex
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override forbiddenhex CloneItem(forbiddenhex source)
    {
        return new forbiddenhex
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1
        };
    }

    protected override int GetItemIndex(forbiddenhex item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(forbiddenhex item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(forbiddenhex item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(forbiddenhex item, int serialId)
    {
        item.serialId_ = serialId;
    }
}