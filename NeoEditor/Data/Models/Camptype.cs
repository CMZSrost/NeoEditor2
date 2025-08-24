using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Models;

[Index("nTreasureID", Name = "main_camptypes_nTreasureID_index")]
public partial class camptype
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
    public string strDesc { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string vImageList { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string aCapacities { get; set; } = null!;

    public int nTreasureID { get; set; }

    [Column(TypeName = "float")]
    public double m_fAlertness { get; set; }

    [Column(TypeName = "float")]
    public double m_fVisibility { get; set; }

    [Column(TypeName = "float")]
    public double WetTempAdjustMod { get; set; }

    [Column(TypeName = "float")]
    public double m_fHealPerHourMod { get; set; }

    [Column(TypeName = "float")]
    public double fSleepQuality { get; set; }
}
