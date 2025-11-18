using System.Collections.ObjectModel;
using NeoEditor.Data.Models.Dto;

namespace NeoEditor.ViewModels.ModelTables;

public class ConditionTableViewModel : TypedTableViewModel<condition>
{
    public ConditionTableViewModel(ObservableCollection<object> rawItems) : base(rawItems) {}

    public ObservableCollection<condition> Conditions => Items;
    public ObservableCollection<condition> FilteredConditions => FilteredItems;

    public condition? SelectedCondition
    {
        get => SelectedItem;
        set => SelectedItem = value;
    }

    protected override bool ShouldRefilterOnPropertyChange(string? propertyName) =>
        propertyName is nameof(condition.strName) or nameof(condition.strDesc) or nameof(condition.aFieldNames);

    protected override bool MatchesFilter(condition item, string filterText) =>
        item.strName.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
        item.strDesc.Contains(filterText, StringComparison.OrdinalIgnoreCase) ||
        item.aFieldNames.Contains(filterText, StringComparison.OrdinalIgnoreCase);

    protected override condition CreateNewItem()
    {
        var newSerialId = Items.Count > 0 ? Items.Max(a => a.serialId_) + 1 : 1;
        return new condition
        {
            serialId_ = newSerialId,
            overId_ = -1,
            id = newSerialId,
            strName = "NewCondition" + newSerialId,
            strDesc = string.Empty,
            aFieldNames = string.Empty,
            aModifiers = string.Empty,
            aEffects = string.Empty,
            vIDNext = string.Empty,
            fDuration = 0,
            vChanceNext = string.Empty,
            nColor = 0,
            nTransferRange = 0,
            aThresholds = string.Empty
        };
    }

    protected override condition CloneItem(condition source) => new condition
    {
        modName = source.modName,
        modIndex = source.modIndex,
        isLast_ = source.isLast_,
        overId_ = -1,
        id = source.id,
        strName = source.strName + " Copy",
        strDesc = source.strDesc,
        aFieldNames = source.aFieldNames,
        aModifiers = source.aModifiers,
        aEffects = source.aEffects,
        bFatal = source.bFatal,
        vIDNext = source.vIDNext,
        fDuration = source.fDuration,
        bPermanent = source.bPermanent,
        vChanceNext = source.vChanceNext,
        bStackable = source.bStackable,
        bDisplay = source.bDisplay,
        bDisplayOther = source.bDisplayOther,
        bDisplayGameOver = source.bDisplayGameOver,
        nColor = source.nColor,
        bResetTimer = source.bResetTimer,
        bRemoveAll = source.bRemoveAll,
        bRemovePostCombat = source.bRemovePostCombat,
        nTransferRange = source.nTransferRange,
        aThresholds = source.aThresholds
    };

    protected override int GetItemIndex(condition item) => item.idx;
    protected override void SetItemIndex(condition item, int index) => item.idx = index;
    protected override int GetItemSerialId(condition item) => item.serialId_;
    protected override void SetItemSerialId(condition item, int serialId) => item.serialId_ = serialId;
}
