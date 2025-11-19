namespace NeoEditor.Data.Models.Dto;

public class treasuretable : BaseDto
{

    public int id { get; set; }

    public string strName { get; set; } = null!;

    public string aTreasures { get; set; } = null!;

    public bool bNested { get; set; }

    public bool bSuppress { get; set; }

    public bool bIdentify { get; set; }
}