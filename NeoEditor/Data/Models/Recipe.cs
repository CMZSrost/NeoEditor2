using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Models;

[Index("nTempTreasureID", Name = "main_recipes_nTempTreasureID_index")]
[Index("nTreasureID", Name = "main_recipes_nTreasureID_index")]
public class recipe
{
    [Key] public int idx { get; set; }

    [Column(TypeName = "varchar(255)")] public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int? overId_ { get; set; }

    public int? nID { get; set; }

    [Column(TypeName = "varchar(255)")] public string strName { get; set; } = null!;

    [Column(TypeName = "varchar(255)")] public string strSecretName { get; set; } = null!;

    [Column(TypeName = "varchar(255)")] public string strTools { get; set; } = null!;

    [Column(TypeName = "varchar(255)")] public string strConsumed { get; set; } = null!;

    [Column(TypeName = "varchar(255)")] public string strDestroyed { get; set; } = null!;

    public int nTreasureID { get; set; }

    [Column(TypeName = "float")] public double fHours { get; set; }

    public int nReverse { get; set; }

    public int nHiddenID { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte bIdentify { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte bTransferComponents { get; set; }

    [Column(TypeName = "varchar(255)")] public string vAlsoTry { get; set; } = null!;

    public int nTempTreasureID { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte bDegradeOutput { get; set; }

    [Column(TypeName = "varchar(255)")] public string strType { get; set; } = null!;

    [Column(TypeName = "tinyint(1)")] public byte bScrap { get; set; }
}