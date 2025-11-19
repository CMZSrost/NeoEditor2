namespace NeoEditor.Data.Models.Dto;

public class datafile : BaseDto
{

    public int id { get; set; }

    public string strName { get; set; } = null!;

    public string strDesc { get; set; } = null!;

    public double fValue { get; set; }

    public string strImg { get; set; } = null!;
}