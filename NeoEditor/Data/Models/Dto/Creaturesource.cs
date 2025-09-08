using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Data.Models.Dto;

public class creaturesource : ObservableObject
{
    public int idx { get; set; }

    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int overId_ { get; set; } = -1;

    public int id { get; set; }

    public string strName { get; set; } = null!;

    public int nX { get; set; }

    public int nY { get; set; }

    public int nCreatureID { get; set; }

    public int nMin { get; set; }

    public int nMax { get; set; }

    public double fWeight { get; set; }
}