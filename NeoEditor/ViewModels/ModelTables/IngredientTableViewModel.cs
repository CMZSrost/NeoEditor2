using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class IngredientTableViewModel(ObservableCollection<BaseDto> rawItems)
    : TypedTableViewModel<ingredient>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(ingredient.strName) or nameof(ingredient.strRequiredProps)
            or nameof(ingredient.strForbiddenProps);
    }

    protected override bool MatchesFilter(ingredient item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.strRequiredProps.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.strForbiddenProps.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override ingredient CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new ingredient
        {
            serialId_ = newSerialId,
            overId_ = -1,
            nID = newSerialId,
            strName = "NewIngredient" + newSerialId,
            strRequiredProps = string.Empty,
            strForbiddenProps = string.Empty,
            modName = string.Empty,
            modIndex = 0
        };
    }

    protected override ingredient CloneItem(ingredient source)
    {
        return new ingredient
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1,
            nID = source.nID,
            strName = source.strName + " Copy",
            strRequiredProps = source.strRequiredProps,
            strForbiddenProps = source.strForbiddenProps
        };
    }

    protected override int GetItemIndex(ingredient item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(ingredient item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(ingredient item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(ingredient item, int serialId)
    {
        item.serialId_ = serialId;
    }
}