namespace NeoEditor.Data.Models.Dto;

public class dmcplace : BaseDto
{

    public int id { get; set; }

    public string strImg { get; set; } = null!;

    public int nEncounterID { get; set; }

    public int nX { get; set; }

    public int nY { get; set; }
}