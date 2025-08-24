using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Models;

[Index("strItemID", Name = "main_chargeprofiles_strItemID_index")]
public partial class chargeprofile
{
    [Key]
    public int idx { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }

    public int? overId_ { get; set; }

    public int? nID { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string strName { get; set; } = null!;

    [Column(TypeName = "varchar(12)")]
    public string strItemID { get; set; } = null!;

    [Column(TypeName = "float")]
    public double fPerUse { get; set; }

    [Column(TypeName = "float")]
    public double fPerHour { get; set; }

    [Column(TypeName = "float")]
    public double fPerHourEquipped { get; set; }

    [Column(TypeName = "float")]
    public double fPerHex { get; set; }

    [Column(TypeName = "tinyint(1)")]
    public byte bDegrade { get; set; }
}
