namespace NeoEditor.Data.Models.Dto;

public class ingredient : BaseDto
{

    public int nID { get; set; }

    public string strName { get; set; } = null!;

    public string strRequiredProps { get; set; } = null!;

    public string strForbiddenProps { get; set; } = null!;
}