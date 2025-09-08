using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Data.Models.Dto;

public class hextype : ObservableObject
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

    public int nTerrainCost { get; set; }

    public int nVizLimiter { get; set; }

    public int nVizIncrease { get; set; }

    public int nTreasureID { get; set; }

    public bool bPassable { get; set; }

    public int nScavengeInitialID { get; set; }

    public int nScavengeItemsIDPerHour { get; set; }

    public int nCampItems { get; set; }

    public string vLightLevels { get; set; } = null!;

    public int nDefaultCampID { get; set; }

    public int nMinRange { get; set; }

    public int nMaxRange { get; set; }

    public string vCondIDs { get; set; } = null!;
}