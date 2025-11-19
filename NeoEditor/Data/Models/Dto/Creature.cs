namespace NeoEditor.Data.Models.Dto;

public class creature : BaseDto
{

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