using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeoEditor.Data.Models;

[Table("treasuretable")]
public class treasuretable
{
    [Key] public int idx { get; set; }

    [Column(TypeName = "varchar(255)")] public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int? overId_ { get; set; }

    public int? id { get; set; }

    [Column(TypeName = "varchar(255)")] public string strName { get; set; } = null!;

    public string aTreasures { get; set; } = null!;

    [Column(TypeName = "tinyint(1)")] public byte bNested { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte bSuppress { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte bIdentify { get; set; }
}