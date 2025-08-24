using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Models;

[Index("nCreatureID", Name = "main_creaturesources_nCreatureID_index")]
[Index("nX", "nY", Name = "main_creaturesources_nX_nY_index")]
public partial class creaturesource
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

    public int nX { get; set; }

    public int nY { get; set; }

    public int nCreatureID { get; set; }

    public int nMin { get; set; }

    public int nMax { get; set; }

    [Column(TypeName = "float")]
    public double fWeight { get; set; }
}
