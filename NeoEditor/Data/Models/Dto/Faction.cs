namespace NeoEditor.Data.Models.Dto;

public class faction : BaseDto
{

    public int id { get; set; }

    public string strName { get; set; } = null!;

    public string dictFactions { get; set; } = null!;
}