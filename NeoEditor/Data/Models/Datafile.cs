using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Models;

public partial class datafile
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

    [Column(TypeName = "float")]
    public double fValue { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string strImg { get; set; } = null!;
}
