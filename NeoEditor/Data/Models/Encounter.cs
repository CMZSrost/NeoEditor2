using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Models;

[Index("nCreatureID", Name = "main_encounters_nCreatureID_index")]
[Index("nItemsID", Name = "main_encounters_nItemsID_index")]
[Index("nRemoveTreasureID", Name = "main_encounters_nRemoveTreasureID_index")]
[Index("nTreasureID", Name = "main_encounters_nTreasureID_index")]
public partial class encounter
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

    [Column(TypeName = "varchar(255)")]
    public string strImg { get; set; } = null!;

    public int nTreasureID { get; set; }

    public int nRemoveTreasureID { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string aConditions { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string aPreConditions { get; set; } = null!;

    [Column(TypeName = "float")]
    public double fPrice { get; set; }

    public string aResponses { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string aMinimapHexes { get; set; } = null!;

    [Column(TypeName = "tinyint(1)")]
    public byte bRemoveCreatures { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public byte bRemoveUsed { get; set; }

    public int nItemsID { get; set; }

    public int nCreatureID { get; set; }

    [Column(TypeName = "varchar(9)")]
    public string ptCreatureHex { get; set; } = null!;

    [Column(TypeName = "varchar(9)")]
    public string ptTeleport { get; set; } = null!;

    [Column(TypeName = "varchar(24)")]
    public string ptEditor { get; set; } = null!;

    public int nType { get; set; }

    [Column(TypeName = "float")]
    public double fLootChance { get; set; }

    [Column(TypeName = "float")]
    public double fAccidentChance { get; set; }

    [Column(TypeName = "float")]
    public double fCreatureChance { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string vAccidents { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string vLoot { get; set; } = null!;
}
