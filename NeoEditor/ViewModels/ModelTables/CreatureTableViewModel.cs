using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class CreatureTableViewModel(ObservableCollection<BaseDto> rawItems) : TypedTableViewModel<creature>(rawItems)
{
    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(creature.strName) or nameof(creature.strNamePublic) or nameof(creature.strNotes);
    }

    protected override bool MatchesFilter(creature item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.strNamePublic.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.strNotes.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override creature CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new creature
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            strName = "NewCreature" + newSerialId,
            strNamePublic = string.Empty,
            strNotes = string.Empty,
            strImg = string.Empty,
            vEncounterIDs = string.Empty,
            nMovesPerTurn = 0,
            nTreasureID = 0,
            nFaction = 0,
            vAttackModes = string.Empty,
            vBaseConditions = string.Empty,
            nCorpseID = 0,
            vActivities = string.Empty
        };
    }

    protected override creature CloneItem(creature source)
    {
        return new creature
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1,
            id = source.id,
            strName = source.strName + " Copy",
            strNamePublic = source.strNamePublic,
            strNotes = source.strNotes,
            strImg = source.strImg,
            vEncounterIDs = source.vEncounterIDs,
            nMovesPerTurn = source.nMovesPerTurn,
            nTreasureID = source.nTreasureID,
            nFaction = source.nFaction,
            vAttackModes = source.vAttackModes,
            vBaseConditions = source.vBaseConditions,
            nCorpseID = source.nCorpseID,
            vActivities = source.vActivities
        };
    }

    protected override int GetItemIndex(creature item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(creature item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(creature item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(creature item, int serialId)
    {
        item.serialId_ = serialId;
    }
}