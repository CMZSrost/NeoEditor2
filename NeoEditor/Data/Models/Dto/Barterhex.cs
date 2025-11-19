namespace NeoEditor.Data.Models.Dto;

public class barterhex : BaseDto
{
    public int id { get; set; }

    public int nX { get; set; }

    public int nY { get; set; }

    public bool bBuys { get; set; }

    public int nRestockTreasureID { get; set; }
}