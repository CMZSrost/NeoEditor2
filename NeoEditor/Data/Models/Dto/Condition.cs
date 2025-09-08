using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Data.Models.Dto;

public class condition : ObservableObject
{
    public int idx { get; set; }

    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int overId_ { get; set; } = -1;

    public int id { get; set; }

    public string strName { get; set; } = null!;

    public string strDesc { get; set; } = null!;

    public string aFieldNames { get; set; } = null!;

    public string aModifiers { get; set; } = null!;

    public string aEffects { get; set; } = null!;

    public bool bFatal { get; set; }

    public string vIDNext { get; set; } = null!;

    public double fDuration { get; set; }

    public bool bPermanent { get; set; }

    public string vChanceNext { get; set; } = null!;

    public bool bStackable { get; set; }

    public bool bDisplay { get; set; }

    public bool bDisplayOther { get; set; }

    public bool bDisplayGameOver { get; set; }

    public int nColor { get; set; }

    public bool bResetTimer { get; set; }

    public bool bRemoveAll { get; set; }

    public bool bRemovePostCombat { get; set; }

    public int nTransferRange { get; set; }

    public string aThresholds { get; set; } = null!;
}