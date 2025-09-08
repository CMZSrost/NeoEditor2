using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Models;

[Index("strID", Name = "main_battlemoves_strID_index")]
public class battlemove
{
    [Key] public int idx { get; set; }

    [Column(TypeName = "varchar(255)")] public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int? overId_ { get; set; }

    public int? id { get; set; }

    [Column(TypeName = "varchar(255)")] public string strID { get; set; } = null!;

    [Column(TypeName = "varchar(255)")] public string strName { get; set; } = null!;

    [Column(TypeName = "varchar(255)")] public string strNotes { get; set; } = null!;

    [Column(TypeName = "varchar(255)")] public string strSuccess { get; set; } = null!;

    [Column(TypeName = "varchar(255)")] public string? strFail { get; set; }

    public string? strPopUp { get; set; }

    [Column(TypeName = "varchar(255)")] public string vChanceType { get; set; } = null!;

    [Column(TypeName = "varchar(255)")] public string? vUsConditions { get; set; }

    [Column(TypeName = "varchar(255)")] public string? vThemConditions { get; set; }

    [Column(TypeName = "varchar(255)")] public string? vPairConditions { get; set; }

    [Column(TypeName = "varchar(255)")] public string? vUsFailConditions { get; set; }

    [Column(TypeName = "varchar(255)")] public string? vThemFailConditions { get; set; }

    [Column(TypeName = "varchar(255)")] public string? vPairFailConditions { get; set; }

    [Column(TypeName = "varchar(255)")] public string? vUsPreConditions { get; set; }

    [Column(TypeName = "varchar(255)")] public string? vThemPreConditions { get; set; }

    public int? nSeeThem { get; set; }

    public int? nSeeUs { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte? bAllOutOfRange { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte? bInAttackRange { get; set; }

    public int? nMinCharges { get; set; }

    public int? nMinRange { get; set; }

    public int? nMaxRange { get; set; }

    public int? nAttackModeType { get; set; }

    [Column(TypeName = "varchar(255)")] public string vHexTypes { get; set; } = null!;

    [Column(TypeName = "float")] public double? fChance { get; set; }

    [Column(TypeName = "float")] public double? fPriority { get; set; }

    [Column(TypeName = "float")] public double? fDetect { get; set; }

    [Column(TypeName = "float")] public double? fOrder { get; set; }

    [Column(TypeName = "float")] public double? fFatigue { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte? bApproach { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte? bOffense { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte? bFallBack { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte? bRetreat { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte? bPosition { get; set; }

    [Column(TypeName = "tinyint(1)")] public byte bPassive { get; set; }
}