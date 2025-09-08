using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Data.Models.Dto;

public class camptype : ObservableObject
{
    public int idx { get; set; }

    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int overId_ { get; set; } = -1;

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