using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Models;

public partial class condition
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
    public string aFieldNames { get; set; } = null!;

    [Column(TypeName = "varchar(100)")]
    public string aModifiers { get; set; } = null!;

    public string aEffects { get; set; } = null!;

    [Column(TypeName = "tinyint(1)")]
    public byte bFatal { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string vIDNext { get; set; } = null!;

    [Column(TypeName = "float")]
    public double fDuration { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public byte bPermanent { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string vChanceNext { get; set; } = null!;

    [Column(TypeName = "tinyint(1)")]
    public byte bStackable { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public byte bDisplay { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public byte bDisplayOther { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public byte bDisplayGameOver { get; set; }

    public int nColor { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public byte bResetTimer { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public byte bRemoveAll { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public byte bRemovePostCombat { get; set; }

    public int nTransferRange { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string aThresholds { get; set; } = null!;
}
