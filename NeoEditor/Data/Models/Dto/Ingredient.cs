using CommunityToolkit.Mvvm.ComponentModel;

namespace NeoEditor.Data.Models.Dto;

public class ingredient : ObservableObject
{
    public int idx { get; set; }

    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int overId_ { get; set; } = -1;

    public int nID { get; set; }

    public string strName { get; set; } = null!;

    public string strRequiredProps { get; set; } = null!;

    public string strForbiddenProps { get; set; } = null!;
}