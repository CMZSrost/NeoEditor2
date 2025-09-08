namespace NeoEditor.Data.Models.Dto;

public record SerialRecord
{
    # region given

    public int? id { get; set; }
    public int? nID { get; set; }
    public string? strName { get; set; }
    public required int idx { get; set; }
    public required string modName { get; set; }
    public required int modIndex { get; set; }

    # endregion

    # region ordered

    public required int overId_ { get; set; }
    public required string serialId_ { get; set; }
    public required bool isLast_ { get; set; }

    # endregion
}