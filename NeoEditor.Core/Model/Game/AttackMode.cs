using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("attackmodes")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class AttackMode : IEntity
{
    [Column("id")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strNotes", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Notes")]
    public string Notes { get; set; } = "";

    [Column("nRange")]
    
    [Display(Name = "Range")]
    public int Range { get; set; } = 1;

    [Column("fDamageCut", TypeName = "float")]
    
    [Display(Name = "DamageCut")]
    public double DamageCut { get; set; } = 0;

    [Column("fDamageBlunt", TypeName = "float")]
    
    [Display(Name = "DamageBlunt")]
    public double DamageBlunt { get; set; } = 0;

    [Column("strChargeProfiles", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "ChargeProfiles")]
    [ReferenceField(typeof(ChargeProfile), Separator = ",")]
    public ReferenceList<IReferenceEntry> ChargeProfiles { get; set; } = new();

    [Column("nPenetration")]

    [Display(Name = "Penetration")]
    public int Penetration { get; set; } = 0;

    [Column("nType")]

    [Display(Name = "Type")]
    public AttackType Type { get; set; } = AttackType.Melee;

    [Column("strSnd", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "Sound")]
    public string Sound { get; set; } = "";

    [Column("bTransfer", TypeName = "tinyint(1)")]

    [Display(Name = "Transfer")]
    public bool Transfer { get; set; } = false;

    [Column("vAttackerConditions", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "AttackerConditions")]
    [ReferenceField(typeof(Condition), Separator = ",", Pattern = "{id}x{mult}")]
    public ReferenceList<IReferenceEntry> AttackerConditions { get; set; } = new();

    [Column("strIMG", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Image")]
    public string Image { get; set; } = "";

    [Column("fMorale", TypeName = "float")]
    
    [Display(Name = "Morale")]
    public double Morale { get; set; } = 0.25;

    [Column("strWieldPhrase", TypeName = "longtext")]
    
    [Display(Name = "WieldPhrase")]
    public string WieldPhrase { get; set; } = "";

    [Column("vAttackPhrases", TypeName = "longtext")]
    
    [Display(Name = "AttackPhrases")]
    public string AttackPhrases { get; set; } = "";
}