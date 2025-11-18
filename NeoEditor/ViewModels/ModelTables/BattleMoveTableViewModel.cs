using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class BattleMoveTableViewModel : TypedTableViewModel<battlemove>
{
    public BattleMoveTableViewModel(ObservableCollection<object> rawItems) : base(rawItems)
    {
    }

    public ObservableCollection<battlemove> BattleMoves => Items;
    public ObservableCollection<battlemove> FilteredBattleMoves => FilteredItems;

    public battlemove? SelectedBattleMove
    {
        get => SelectedItem;
        set => SelectedItem = value;
    }

    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(battlemove.strName) or nameof(battlemove.strNotes) or nameof(battlemove.strID);
    }

    protected override bool MatchesFilter(battlemove item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.strNotes.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.strID.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override battlemove CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new battlemove
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            strID = "NewBattleMove" + newSerialId,
            strName = "New Battle Move" + newSerialId,
            strNotes = string.Empty,
            modName = string.Empty,
            modIndex = 0,
            strSuccess = string.Empty,
            strFail = string.Empty,
            strPopUp = string.Empty,
            vChanceType = string.Empty,
            vUsConditions = string.Empty,
            vThemConditions = string.Empty,
            vPairConditions = string.Empty,
            vUsFailConditions = string.Empty,
            vThemFailConditions = string.Empty,
            vPairFailConditions = string.Empty,
            vUsPreConditions = string.Empty,
            vThemPreConditions = string.Empty,
            nSeeThem = 0,
            nSeeUs = 0,
            bAllOutOfRange = false,
            bInAttackRange = false,
            nMinCharges = 0,
            nMinRange = 0,
            nMaxRange = 0,
            nAttackModeType = 0,
            vHexTypes = string.Empty,
            fChance = 0,
            fPriority = 0,
            fDetect = 0,
            fOrder = 0,
            fFatigue = 0,
            bApproach = false,
            bOffense = false,
            bFallBack = false,
            bRetreat = false,
            bPosition = false,
            bPassive = false
        };
    }

    protected override battlemove CloneItem(battlemove source)
    {
        return new battlemove
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1,
            id = source.id,
            strID = source.strID + " Copy",
            strName = source.strName + " Copy",
            strNotes = source.strNotes,
            strSuccess = source.strSuccess,
            strFail = source.strFail,
            strPopUp = source.strPopUp,
            vChanceType = source.vChanceType,
            vUsConditions = source.vUsConditions,
            vThemConditions = source.vThemConditions,
            vPairConditions = source.vPairConditions,
            vUsFailConditions = source.vUsFailConditions,
            vThemFailConditions = source.vThemFailConditions,
            vPairFailConditions = source.vPairFailConditions,
            vUsPreConditions = source.vUsPreConditions,
            vThemPreConditions = source.vThemPreConditions,
            nSeeThem = source.nSeeThem,
            nSeeUs = source.nSeeUs,
            bAllOutOfRange = source.bAllOutOfRange,
            bInAttackRange = source.bInAttackRange,
            nMinCharges = source.nMinCharges,
            nMinRange = source.nMinRange,
            nMaxRange = source.nMaxRange,
            nAttackModeType = source.nAttackModeType,
            vHexTypes = source.vHexTypes,
            fChance = source.fChance,
            fPriority = source.fPriority,
            fDetect = source.fDetect,
            fOrder = source.fOrder,
            fFatigue = source.fFatigue,
            bApproach = source.bApproach,
            bOffense = source.bOffense,
            bFallBack = source.bFallBack,
            bRetreat = source.bRetreat,
            bPosition = source.bPosition,
            bPassive = source.bPassive
        };
    }

    protected override int GetItemIndex(battlemove item) => item.idx;
    protected override void SetItemIndex(battlemove item, int index) => item.idx = index;
    protected override int GetItemSerialId(battlemove item) => item.serialId_;
    protected override void SetItemSerialId(battlemove item, int serialId) => item.serialId_ = serialId;
}
