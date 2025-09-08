using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Models;

[Index("nRestockTreasureID", Name = "main_barterhexes_nRestockTreasureID_index")]
[Index("nX", "nY", Name = "main_barterhexes_nX_nY_index")]
public class barterhex
{
    [Key] public int idx { get; set; }

    [Column(TypeName = "varchar(255)")] public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int? overId_ { get; set; }

    public int? id { get; set; }

    public int nX { get; set; }

    public int nY { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte bBuys { get; set; }

    public int nRestockTreasureID { get; set; }
}