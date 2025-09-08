using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Data.Models.Dto;

public class itemtype : ObservableObject
{
    public int idx { get; set; }

    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int overId_ { get; set; } = -1;

    public int id { get; set; }

    public int nGroupID { get; set; }

    public int nSubgroupID { get; set; }

    public string strName { get; set; } = null!;

    public string strDesc { get; set; } = null!;

    public string strDescAlt { get; set; } = null!;

    public int nCondID { get; set; }

    public string vImageList { get; set; } = null!;

    public string vSpriteList { get; set; } = null!;

    public string vImageUsage { get; set; } = null!;

    public double fWeight { get; set; }

    public double fMonetaryValue { get; set; }

    public double fMonetaryValueAlt { get; set; }

    public double fDurability { get; set; }

    public double fDegradePerHour { get; set; }

    public double fEquipDegradePerHour { get; set; }

    public double fDegradePerUse { get; set; }

    public string vDegradeTreasureIDs { get; set; } = null!;

    public string aEquipConditions { get; set; } = null!;

    public string aPossessConditions { get; set; } = null!;

    public string aUseConditions { get; set; } = null!;

    public string aCapacities { get; set; } = null!;

    public string vEquipSlots { get; set; } = null!;

    public string vUseSlots { get; set; } = null!;

    public bool bSocketLocked { get; set; }

    public string vProperties { get; set; } = null!;

    public string aContentIDs { get; set; } = null!;

    public int nFormatID { get; set; }

    public int nTreasureID { get; set; }

    public int nComponentID { get; set; }

    public bool bMirrored { get; set; }

    public int nSlotDepth { get; set; }

    public string strChargeProfiles { get; set; } = null!;

    public string aAttackModes { get; set; } = null!;

    public int nStackLimit { get; set; }

    public string aSwitchIDs { get; set; } = null!;

    public string aSounds { get; set; } = null!;
}