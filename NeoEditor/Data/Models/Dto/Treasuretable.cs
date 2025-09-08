using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Data.Models.Dto;

public class treasuretable : ObservableObject
{
    public int idx { get; set; }

    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int overId_ { get; set; } = -1;

    public int id { get; set; }

    public string strName { get; set; } = null!;

    public string aTreasures { get; set; } = null!;

    public bool bNested { get; set; }

    public bool bSuppress { get; set; }

    public bool bIdentify { get; set; }
}