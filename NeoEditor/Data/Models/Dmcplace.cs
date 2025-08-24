using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Models;

[Index("nEncounterID", Name = "main_dmcplaces_nEncounterID_index")]
[Index("nX", "nY", Name = "main_dmcplaces_nX_nY_index")]
public partial class dmcplace
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
    public string strImg { get; set; } = null!;

    public int nEncounterID { get; set; }

    public int nX { get; set; }

    public int nY { get; set; }
}
