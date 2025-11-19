namespace NeoEditor.Data.Models.Dto;

public class encountertrigger : BaseDto
{

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