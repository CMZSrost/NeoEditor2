using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Data.Models.Dto;

public class recipe : ObservableObject
{
    public int idx { get; set; }

    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int overId_ { get; set; } = -1;

    public int nID { get; set; }

    public string strName { get; set; } = null!;

    public string strSecretName { get; set; } = null!;

    public string strTools { get; set; } = null!;

    public string strConsumed { get; set; } = null!;

    public string strDestroyed { get; set; } = null!;

    public int nTreasureID { get; set; }

    public double fHours { get; set; }

    public int nReverse { get; set; }

    public int nHiddenID { get; set; }

    public bool bIdentify { get; set; }

    public bool bTransferComponents { get; set; }

    public string vAlsoTry { get; set; } = null!;

    public int nTempTreasureID { get; set; }

    public bool bDegradeOutput { get; set; }

    public string strType { get; set; } = null!;

    public bool bScrap { get; set; }
}