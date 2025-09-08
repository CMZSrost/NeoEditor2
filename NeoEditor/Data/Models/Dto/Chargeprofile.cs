using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Data.Models.Dto;

public class chargeprofile : ObservableObject
{
    public int idx { get; set; }

    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int overId_ { get; set; } = -1;

    public int nID { get; set; }

    public string strName { get; set; } = null!;

    public string strItemID { get; set; } = null!;

    public double fPerUse { get; set; }

    public double fPerHour { get; set; }

    public double fPerHourEquipped { get; set; }

    public double fPerHex { get; set; }

    public bool bDegrade { get; set; }
}