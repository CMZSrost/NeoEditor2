using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Data.Models.Dto;

public class battlemove : ObservableObject
{
    public int idx { get; set; }

    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int overId_ { get; set; } = -1;

    public int id { get; set; }

    public string strID { get; set; } = null!;

    public string strName { get; set; } = null!;

    public string strNotes { get; set; } = null!;

    public string strSuccess { get; set; } = null!;

    public string? strFail { get; set; }

    public string? strPopUp { get; set; }

    public string vChanceType { get; set; } = null!;

    public string? vUsConditions { get; set; }

    public string? vThemConditions { get; set; }

    public string? vPairConditions { get; set; }

    public string? vUsFailConditions { get; set; }

    public string? vThemFailConditions { get; set; }

    public string? vPairFailConditions { get; set; }

    public string? vUsPreConditions { get; set; }

    public string? vThemPreConditions { get; set; }

    public int nSeeThem { get; set; }

    public int nSeeUs { get; set; }

    public bool bAllOutOfRange { get; set; }

    public bool bInAttackRange { get; set; }

    public int nMinCharges { get; set; }

    public int nMinRange { get; set; }

    public int nMaxRange { get; set; }

    public int nAttackModeType { get; set; }

    public string vHexTypes { get; set; } = null!;

    public double fChance { get; set; }

    public double fPriority { get; set; }

    public double fDetect { get; set; }

    public double fOrder { get; set; }

    public double fFatigue { get; set; }

    public bool bApproach { get; set; }

    public bool bOffense { get; set; }

    public bool bFallBack { get; set; }

    public bool bRetreat { get; set; }

    public bool bPosition { get; set; }

    public bool bPassive { get; set; }
}