using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class RecipeTableViewModel(ObservableCollection<BaseDto> rawItems) : TypedTableViewModel<recipe>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(recipe.strName) or nameof(recipe.strSecretName) or nameof(recipe.strTools);
    }

    protected override bool MatchesFilter(recipe item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.strSecretName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.strTools.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override recipe CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new recipe
        {
            serialId_ = newSerialId,
            overId_ = -1,
            nID = newSerialId,
            strName = "NewRecipe" + newSerialId,
            strSecretName = string.Empty,
            strTools = string.Empty,
            strConsumed = string.Empty,
            strDestroyed = string.Empty,
            vAlsoTry = string.Empty,
            strType = string.Empty,
            modName = string.Empty,
            modIndex = 0,
            nTreasureID = 0,
            fHours = 0,
            nReverse = 0,
            nHiddenID = 0,
            bIdentify = false,
            bTransferComponents = false,
            nTempTreasureID = 0,
            bDegradeOutput = false,
            bScrap = false
        };
    }

    protected override recipe CloneItem(recipe source)
    {
        return new recipe
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1,
            nID = source.nID,
            strName = source.strName + " Copy",
            strSecretName = source.strSecretName,
            strTools = source.strTools,
            strConsumed = source.strConsumed,
            strDestroyed = source.strDestroyed,
            nTreasureID = source.nTreasureID,
            fHours = source.fHours,
            nReverse = source.nReverse,
            nHiddenID = source.nHiddenID,
            bIdentify = source.bIdentify,
            bTransferComponents = source.bTransferComponents,
            vAlsoTry = source.vAlsoTry,
            nTempTreasureID = source.nTempTreasureID,
            bDegradeOutput = source.bDegradeOutput,
            strType = source.strType,
            bScrap = source.bScrap
        };
    }

    protected override int GetItemIndex(recipe item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(recipe item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(recipe item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(recipe item, int serialId)
    {
        item.serialId_ = serialId;
    }
}