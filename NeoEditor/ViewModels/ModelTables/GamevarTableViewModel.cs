using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class GamevarTableViewModel(ObservableCollection<BaseDto> rawItems) : TypedTableViewModel<gamevar>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(gamevar.strName) or nameof(gamevar.strType);
    }

    protected override bool MatchesFilter(gamevar item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.strType.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override gamevar CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new gamevar
        {
            serialId_ = newSerialId,
            overId_ = -1,
            strName = "NewGamevar",
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override gamevar CloneItem(gamevar source)
    {
        return new gamevar
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1
        };
    }

    protected override int GetItemIndex(gamevar item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(gamevar item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(gamevar item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(gamevar item, int serialId)
    {
        item.serialId_ = serialId;
    }
}