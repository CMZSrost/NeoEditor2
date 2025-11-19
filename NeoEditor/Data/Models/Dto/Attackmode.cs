namespace NeoEditor.Data.Models.Dto;

public class attackmode : BaseDto
{

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