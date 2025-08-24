using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Models;

[Index("nComponentID", Name = "main_itemtypes_nComponentID_index")]
[Index("nCondID", Name = "main_itemtypes_nCondID_index")]
[Index("nGroupID", Name = "main_itemtypes_nGroupID_index")]
[Index("nGroupID", "nSubgroupID", Name = "main_itemtypes_nGroupID_nSubgroupID_index")]
[Index("nSubgroupID", Name = "main_itemtypes_nSubgroupID_index")]
[Index("nTreasureID", Name = "main_itemtypes_nTreasureID_index")]
public partial class itemtype
{
    [Key]
    public int idx { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }

    public int? overId_ { get; set; }

    public int? id { get; set; }

    public int nGroupID { get; set; }

    public int nSubgroupID { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string strName { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string strDesc { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string strDescAlt { get; set; } = null!;

    public int nCondID { get; set; }

    public string vImageList { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string vSpriteList { get; set; } = null!;

    [Column(TypeName = "varchar(25)")]
    public string vImageUsage { get; set; } = null!;

    [Column(TypeName = "float")]
    public double fWeight { get; set; }

    [Column(TypeName = "float")]
    public double fMonetaryValue { get; set; }

    [Column(TypeName = "float")]
    public double fMonetaryValueAlt { get; set; }

    [Column(TypeName = "float")]
    public double fDurability { get; set; }

    [Column(TypeName = "float")]
    public double fDegradePerHour { get; set; }

    [Column(TypeName = "float")]
    public double fEquipDegradePerHour { get; set; }

    [Column(TypeName = "float")]
    public double fDegradePerUse { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string vDegradeTreasureIDs { get; set; } = null!;

    public string aEquipConditions { get; set; } = null!;

    public string aPossessConditions { get; set; } = null!;

    public string aUseConditions { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string aCapacities { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string vEquipSlots { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string vUseSlots { get; set; } = null!;

    [Column(TypeName = "tinyint(1)")]
    public byte bSocketLocked { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string vProperties { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string aContentIDs { get; set; } = null!;

    public int nFormatID { get; set; }

    public int nTreasureID { get; set; }

    public int nComponentID { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public byte bMirrored { get; set; }

    public int nSlotDepth { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string strChargeProfiles { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string aAttackModes { get; set; } = null!;

    public int nStackLimit { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string aSwitchIDs { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string aSounds { get; set; } = null!;
}
