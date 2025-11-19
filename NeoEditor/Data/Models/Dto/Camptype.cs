namespace NeoEditor.Data.Models.Dto;

public class camptype : BaseDto
{

    public int id { get; set; }

    public string strDesc { get; set; } = null!;

    public string vImageList { get; set; } = null!;

    public string aCapacities { get; set; } = null!;

    public int nTreasureID { get; set; }

    public double m_fAlertness { get; set; }

    public double m_fVisibility { get; set; }

    public double WetTempAdjustMod { get; set; }

    public double m_fHealPerHourMod { get; set; }

    public double fSleepQuality { get; set; }
}