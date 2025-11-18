using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class ItemTypeTableViewModel : TypedTableViewModel<itemtype>
{
    public ItemTypeTableViewModel(ObservableCollection<object> rawItems) : base(rawItems) {}

    public ObservableCollection<itemtype> ItemTypes => Items;
    public ObservableCollection<itemtype> FilteredItemTypes => FilteredItems;

    public itemtype? SelectedItemType
    {
        get => SelectedItem;
        set => SelectedItem = value;
    }

    protected override bool ShouldRefilterOnPropertyChange(string? propertyName) =>
        propertyName is nameof(itemtype.strName) or nameof(itemtype.strDesc) or nameof(itemtype.strDescAlt);

    protected override bool MatchesFilter(itemtype item, string filterText) =>
        item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
        item.strDesc.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
        item.strDescAlt.Contains(filterText, StringComparison.OrdinalIgnoreCase);

    protected override itemtype CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new itemtype
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            strName = "NewItemType" + newSerialId,
            strDesc = string.Empty,
            strDescAlt = string.Empty,
            vImageList = string.Empty,
            vSpriteList = string.Empty,
            vImageUsage = string.Empty,
            fWeight = 0,
            fMonetaryValue = 0,
            fMonetaryValueAlt = 0,
            fDurability = 0,
            fDegradePerHour = 0,
            fEquipDegradePerHour = 0,
            fDegradePerUse = 0,
            vDegradeTreasureIDs = string.Empty,
            aEquipConditions = string.Empty,
            aPossessConditions = string.Empty,
            aUseConditions = string.Empty,
            aCapacities = string.Empty,
            vEquipSlots = string.Empty,
            vUseSlots = string.Empty,
            vProperties = string.Empty,
            aContentIDs = string.Empty,
            strChargeProfiles = string.Empty,
            aAttackModes = string.Empty,
            aSwitchIDs = string.Empty,
            aSounds = string.Empty,
            nGroupID = 0,
            nSubgroupID = 0,
            nCondID = 0,
            nFormatID = 0,
            nTreasureID = 0,
            nComponentID = 0,
            nSlotDepth = 0,
            nStackLimit = 0,
            bSocketLocked = false,
            bMirrored = false
        };
    }

    protected override itemtype CloneItem(itemtype source) => new itemtype
    {
        modName = source.modName,
        modIndex = source.modIndex,
        isLast_ = source.isLast_,
        overId_ = -1,
        id = source.id,
        strName = source.strName + " Copy",
        strDesc = source.strDesc,
        strDescAlt = source.strDescAlt,
        nGroupID = source.nGroupID,
        nSubgroupID = source.nSubgroupID,
        nCondID = source.nCondID,
        vImageList = source.vImageList,
        vSpriteList = source.vSpriteList,
        vImageUsage = source.vImageUsage,
        fWeight = source.fWeight,
        fMonetaryValue = source.fMonetaryValue,
        fMonetaryValueAlt = source.fMonetaryValueAlt,
        fDurability = source.fDurability,
        fDegradePerHour = source.fDegradePerHour,
        fEquipDegradePerHour = source.fEquipDegradePerHour,
        fDegradePerUse = source.fDegradePerUse,
        vDegradeTreasureIDs = source.vDegradeTreasureIDs,
        aEquipConditions = source.aEquipConditions,
        aPossessConditions = source.aPossessConditions,
        aUseConditions = source.aUseConditions,
        aCapacities = source.aCapacities,
        vEquipSlots = source.vEquipSlots,
        vUseSlots = source.vUseSlots,
        bSocketLocked = source.bSocketLocked,
        vProperties = source.vProperties,
        aContentIDs = source.aContentIDs,
        nFormatID = source.nFormatID,
        nTreasureID = source.nTreasureID,
        nComponentID = source.nComponentID,
        bMirrored = source.bMirrored,
        nSlotDepth = source.nSlotDepth,
        strChargeProfiles = source.strChargeProfiles,
        aAttackModes = source.aAttackModes,
        nStackLimit = source.nStackLimit,
        aSwitchIDs = source.aSwitchIDs,
        aSounds = source.aSounds
    };

    protected override int GetItemIndex(itemtype item) => item.idx;
    protected override void SetItemIndex(itemtype item, int index) => item.idx = index;
    protected override int GetItemSerialId(itemtype item) => item.serialId_;
    protected override void SetItemSerialId(itemtype item, int serialId) => item.serialId_ = serialId;
}
