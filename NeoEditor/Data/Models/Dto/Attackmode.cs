using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Data.Models.Dto;

public class attackmode : ObservableObject
{
    public int idx { get; set; }

    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int overId_ { get; set; } = -1;

    public int id { get; set; }

    public string strName { get; set; } = null!;

    public string strNotes { get; set; } = null!;

    public int nRange { get; set; }

    public double fDamageCut { get; set; }

    public double fDamageBlunt { get; set; }

    public string strChargeProfiles { get; set; } = null!;

    public int nPenetration { get; set; }

    public int nType { get; set; }

    public string strSnd { get; set; } = null!;

    public bool bTransfer { get; set; }

    public string vAttackerConditions { get; set; } = null!;

    public string strIMG { get; set; } = null!;

    public double fMorale { get; set; }

    public string strWieldPhrase { get; set; } = null!;

    public string vAttackPhrases { get; set; } = null!;
}