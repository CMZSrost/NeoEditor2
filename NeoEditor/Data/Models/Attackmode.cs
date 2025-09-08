using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeoEditor.Data.Models;

public class attackmode
{
    [Key] public int idx { get; set; }

    [Column(TypeName = "varchar(255)")] public string modName { get; set; } = null!;

    public int modIndex { get; set; }

    public int serialId_ { get; set; }
    public bool isLast_ { get; set; } = false;

    public int? overId_ { get; set; }

    public int? id { get; set; }

    [Column(TypeName = "varchar(255)")] public string strName { get; set; } = null!;

    [Column(TypeName = "varchar(255)")] public string strNotes { get; set; } = null!;

    public int nRange { get; set; }

    [Column(TypeName = "float")] public double fDamageCut { get; set; }

    [Column(TypeName = "float")] public double fDamageBlunt { get; set; }

    [Column(TypeName = "varchar(24)")] public string strChargeProfiles { get; set; } = null!;

    public int nPenetration { get; set; }

    public int nType { get; set; }

    [Column(TypeName = "varchar(30)")] public string strSnd { get; set; } = null!;

    [Column(TypeName = "tinyint(1)")] public byte bTransfer { get; set; }

    [Column(TypeName = "varchar(255)")] public string vAttackerConditions { get; set; } = null!;

    [Column(TypeName = "varchar(50)")] public string strIMG { get; set; } = null!;

    [Column(TypeName = "float")] public double fMorale { get; set; }

    public string strWieldPhrase { get; set; } = null!;

    public string vAttackPhrases { get; set; } = null!;
}