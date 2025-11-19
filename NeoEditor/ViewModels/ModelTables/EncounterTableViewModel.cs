using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class EncounterTableViewModel(ObservableCollection<BaseDto> rawItems) : TypedTableViewModel<encounter>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(encounter.strName) or nameof(encounter.strDesc);
    }

    protected override bool MatchesFilter(encounter item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.strDesc.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override encounter CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new encounter
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            strName = "NewEncounter" + newSerialId,
            strDesc = string.Empty,
            strImg = string.Empty,
            modName = string.Empty,
            modIndex = 0,
            nTreasureID = 0,
            nRemoveTreasureID = 0,
            aConditions = string.Empty,
            aPreConditions = string.Empty,
            fPrice = 0,
            aResponses = string.Empty,
            aMinimapHexes = string.Empty,
            bRemoveCreatures = false,
            bRemoveUsed = false,
            nItemsID = 0,
            nCreatureID = 0,
            ptCreatureHex = string.Empty,
            ptTeleport = string.Empty,
            ptEditor = string.Empty,
            nType = 0,
            fLootChance = 0,
            fAccidentChance = 0,
            fCreatureChance = 0,
            vAccidents = string.Empty,
            vLoot = string.Empty
        };
    }

    protected override encounter CloneItem(encounter source)
    {
        return new encounter
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1,
            id = source.id,
            strName = source.strName + " Copy",
            strDesc = source.strDesc,
            strImg = source.strImg,
            nTreasureID = source.nTreasureID,
            nRemoveTreasureID = source.nRemoveTreasureID,
            aConditions = source.aConditions,
            aPreConditions = source.aPreConditions,
            fPrice = source.fPrice,
            aResponses = source.aResponses,
            aMinimapHexes = source.aMinimapHexes,
            bRemoveCreatures = source.bRemoveCreatures,
            bRemoveUsed = source.bRemoveUsed,
            nItemsID = source.nItemsID,
            nCreatureID = source.nCreatureID,
            ptCreatureHex = source.ptCreatureHex,
            ptTeleport = source.ptTeleport,
            ptEditor = source.ptEditor,
            nType = source.nType,
            fLootChance = source.fLootChance,
            fAccidentChance = source.fAccidentChance,
            fCreatureChance = source.fCreatureChance,
            vAccidents = source.vAccidents,
            vLoot = source.vLoot
        };
    }

    protected override int GetItemIndex(encounter item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(encounter item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(encounter item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(encounter item, int serialId)
    {
        item.serialId_ = serialId;
    }
}