using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Models;

[Index("nCampItems", Name = "main_hextypes_nCampItems_index")]
[Index("nScavengeInitialID", Name = "main_hextypes_nScavengeInitialID_index")]
[Index("nScavengeItemsIDPerHour", Name = "main_hextypes_nScavengeItemsIDPerHour_index")]
[Index("nTreasureID", Name = "main_hextypes_nTreasureID_index")]
public partial class hextype
{
    [Key]
    public int idx { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }

    public int? overId_ { get; set; }

    public int? id { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string strName { get; set; } = null!;

    public string strDesc { get; set; } = null!;

    public int nTerrainCost { get; set; }

    public int nVizLimiter { get; set; }

    public int nVizIncrease { get; set; }

    public int nTreasureID { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public byte bPassable { get; set; }

    public int nScavengeInitialID { get; set; }

    public int nScavengeItemsIDPerHour { get; set; }

    public int nCampItems { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string vLightLevels { get; set; } = null!;

    public int nDefaultCampID { get; set; }

    public int nMinRange { get; set; }

    public int nMaxRange { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string vCondIDs { get; set; } = null!;
}
