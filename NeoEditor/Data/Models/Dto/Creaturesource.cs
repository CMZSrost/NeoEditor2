namespace NeoEditor.Data.Models.Dto;

public class creaturesource : BaseDto
{

    public int id { get; set; }

    public string strName { get; set; } = null!;

    public int nX { get; set; }

    public int nY { get; set; }

    public int nCreatureID { get; set; }

    public int nMin { get; set; }

    public int nMax { get; set; }

    public double fWeight { get; set; }
}