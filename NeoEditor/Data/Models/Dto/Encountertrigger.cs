using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Data.Models.Dto;

public class encountertrigger : ObservableObject
{
    public int idx { get; set; }

    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int overId_ { get; set; } = -1;

    public int id { get; set; }

    public string strName { get; set; } = null!;

    public int nEncounterID { get; set; }

    public double fChance { get; set; }

    public bool bLocBased { get; set; }

    public bool bDateBased { get; set; }

    public bool bHexBased { get; set; }

    public bool bUnique { get; set; }

    public bool bAIPassable { get; set; }

    public string aArea { get; set; } = null!;

    public string dateMin { get; set; } = null!;

    public string dateMax { get; set; } = null!;

    public string aHexTypes { get; set; } = null!;
}