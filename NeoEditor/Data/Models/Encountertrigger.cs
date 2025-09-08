using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Models;

[Index("nEncounterID", Name = "main_encountertriggers_nEncounterID_index")]
public class encountertrigger
{
    [Key] public int idx { get; set; }

    [Column(TypeName = "varchar(255)")] public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int? overId_ { get; set; }

    public int? id { get; set; }

    [Column(TypeName = "varchar(255)")] public string strName { get; set; } = null!;

    public int nEncounterID { get; set; }

    [Column(TypeName = "float")] public double fChance { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte bLocBased { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte bDateBased { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte bHexBased { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte bUnique { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte bAIPassable { get; set; }

    [Column(TypeName = "varchar(25)")] public string aArea { get; set; } = null!;

    [Column(TypeName = "varchar(15)")] public string dateMin { get; set; } = null!;

    [Column(TypeName = "varchar(15)")] public string dateMax { get; set; } = null!;

    public string aHexTypes { get; set; } = null!;
}