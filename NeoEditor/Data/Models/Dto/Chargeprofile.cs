namespace NeoEditor.Data.Models.Dto;

public class chargeprofile : BaseDto
{

    public int nID { get; set; }

    public string strName { get; set; } = null!;

    public string strItemID { get; set; } = null!;

    public double fPerUse { get; set; }

    public double fPerHour { get; set; }

    public double fPerHourEquipped { get; set; }

    public double fPerHex { get; set; }

    public bool bDegrade { get; set; }
}