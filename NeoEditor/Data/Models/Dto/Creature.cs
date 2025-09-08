using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Data.Models.Dto;

public class creature : ObservableObject
{
    public int idx { get; set; }

    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int overId_ { get; set; } = -1;

    public int id { get; set; }

    public string strName { get; set; } = null!;

    public string strNamePublic { get; set; } = null!;

    public string strNotes { get; set; } = null!;

    public string strImg { get; set; } = null!;

    public string vEncounterIDs { get; set; } = null!;

    public int nMovesPerTurn { get; set; }

    public int nTreasureID { get; set; }

    public int nFaction { get; set; }

    public string vAttackModes { get; set; } = null!;

    public string vBaseConditions { get; set; } = null!;

    public int nCorpseID { get; set; }

    public string vActivities { get; set; } = null!;
}