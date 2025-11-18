using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class AttackModeTableViewModel : TypedTableViewModel<attackmode>
{
    public AttackModeTableViewModel(ObservableCollection<object> rawItems) : base(rawItems)
    {
    }

    public ObservableCollection<attackmode> AttackModes => Items;
    public ObservableCollection<attackmode> FilteredAttackModes => FilteredItems;

    public attackmode? SelectedAttackMode
    {
        get => SelectedItem;
        set => SelectedItem = value;
    }

    protected override bool ShouldRefilterOnPropertyChange(string? propertyName)
    {
        return propertyName is nameof(attackmode.strName) or nameof(attackmode.strNotes);
    }

    protected override bool MatchesFilter(attackmode item, string filterText)
    {
        return item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
               item.strNotes.Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    protected override attackmode CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new attackmode
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            strName = "NewAttackMode" + newSerialId,
            strNotes = string.Empty,
            modName = string.Empty,
            modIndex = 0,
            strChargeProfiles = string.Empty,
            strSnd = string.Empty,
            vAttackerConditions = string.Empty,
            strIMG = string.Empty,
            strWieldPhrase = string.Empty,
            vAttackPhrases = string.Empty,
            nRange = 0,
            fDamageCut = 0,
            fDamageBlunt = 0,
            nPenetration = 0,
            nType = 0,
            bTransfer = false,
            fMorale = 0
        };
    }

    protected override attackmode CloneItem(attackmode source)
    {
        return new attackmode
        {
            modName = source.modName,
            modIndex = source.modIndex,
            isLast_ = source.isLast_,
            overId_ = -1,
            id = source.id,
            strName = source.strName + " Copy",
            strNotes = source.strNotes,
            nRange = source.nRange,
            fDamageCut = source.fDamageCut,
            fDamageBlunt = source.fDamageBlunt,
            strChargeProfiles = source.strChargeProfiles,
            nPenetration = source.nPenetration,
            nType = source.nType,
            strSnd = source.strSnd,
            bTransfer = source.bTransfer,
            vAttackerConditions = source.vAttackerConditions,
            strIMG = source.strIMG,
            fMorale = source.fMorale,
            strWieldPhrase = source.strWieldPhrase,
            vAttackPhrases = source.vAttackPhrases
        };
    }

    protected override int GetItemIndex(attackmode item)
    {
        return item.idx;
    }

    protected override void SetItemIndex(attackmode item, int index)
    {
        item.idx = index;
    }

    protected override int GetItemSerialId(attackmode item)
    {
        return item.serialId_;
    }

    protected override void SetItemSerialId(attackmode item, int serialId)
    {
        item.serialId_ = serialId;
    }
}