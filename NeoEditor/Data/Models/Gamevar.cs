using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Models;

public partial class gamevar
{
    [Key]
    public int idx { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }

    public int? overId_ { get; set; }

    [Column(TypeName = "varchar(255)")]
    public string strName { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string strType { get; set; } = null!;

    [Column(TypeName = "varchar(255)")]
    public string strValue { get; set; } = null!;
}
