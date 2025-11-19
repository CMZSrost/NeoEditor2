namespace NeoEditor.Data.Models.Dto;

public class encounter : BaseDto
{

    public int id { get; set; }

    public string strName { get; set; } = null!;

    public string strDesc { get; set; } = null!;

    public string strImg { get; set; } = null!;

    public int nTreasureID { get; set; }

    public int nRemoveTreasureID { get; set; }

    public string aConditions { get; set; } = null!;

    public string aPreConditions { get; set; } = null!;

    public double fPrice { get; set; }

    public string aResponses { get; set; } = null!;

    public string aMinimapHexes { get; set; } = null!;

    public bool bRemoveCreatures { get; set; }

    public bool bRemoveUsed { get; set; }

    public int nItemsID { get; set; }

    public int nCreatureID { get; set; }

    public string ptCreatureHex { get; set; } = null!;

    public string ptTeleport { get; set; } = null!;

    public string ptEditor { get; set; } = null!;

    public int nType { get; set; }

    public double fLootChance { get; set; }

    public double fAccidentChance { get; set; }

    public double fCreatureChance { get; set; }

    public string vAccidents { get; set; } = null!;

    public string vLoot { get; set; } = null!;
}