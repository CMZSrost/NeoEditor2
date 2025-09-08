using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Models;

[Index("nCorpseID", Name = "main_creatures_nCorpseID_index")]
[Index("nTreasureID", Name = "main_creatures_nTreasureID_index")]
public class creature
{
    [Key] public int idx { get; set; }

    [Column(TypeName = "varchar(255)")] public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int? overId_ { get; set; }

    public int? id { get; set; }

    [Column(TypeName = "varchar(255)")] public string strName { get; set; } = null!;

    [Column(TypeName = "varchar(255)")] public string strNamePublic { get; set; } = null!;

    [Column(TypeName = "varchar(255)")] public string strNotes { get; set; } = null!;

    [Column(TypeName = "varchar(255)")] public string strImg { get; set; } = null!;

    [Column(TypeName = "varchar(255)")] public string vEncounterIDs { get; set; } = null!;

    public int nMovesPerTurn { get; set; }

    public int nTreasureID { get; set; }

    public int nFaction { get; set; }

    [Column(TypeName = "varchar(25)")] public string vAttackModes { get; set; } = null!;

    public string vBaseConditions { get; set; } = null!;

    public int nCorpseID { get; set; }

    public string vActivities { get; set; } = null!;
}