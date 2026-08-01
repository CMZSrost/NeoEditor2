using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("conditions")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class Condition : IEntity
{

    [Column("id")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strDesc", TypeName = "longtext")]
    
    [Display(Name = "Description")]
    public string Description { get; set; } = "";

    [Column("aFieldNames", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "FieldNames")]
    public string FieldNames { get; set; } = "";

    [Column("aModifiers", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Modifiers")]
    public string Modifiers { get; set; } = "";

    [Column("aEffects", TypeName = "longtext")]
    
    [Display(Name = "Effects")]
    public string Effects { get; set; } = "";

    [Column("bFatal", TypeName = "tinyint(1)")]
    
    [Display(Name = "Fatal")]
    public bool Fatal { get; set; } = false;

    [Column("vIDNext", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "IdNext")]
    [ReferenceField(typeof(Condition), Separator = ",")]
    public ReferenceList<IReferenceEntry> IdNext { get; set; } = new();

    [Column("fDuration", TypeName = "float")]
    
    [Display(Name = "Duration")]
    public double Duration { get; set; } = 0;

    [Column("bPermanent", TypeName = "tinyint(1)")]
    
    [Display(Name = "Permanent")]
    public bool Permanent { get; set; } = false;

    [Column("vChanceNext", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "ChanceNext")]
    public string ChanceNext { get; set; } = "0";

    [Column("bStackable", TypeName = "tinyint(1)")]
    
    [Display(Name = "Stackable")]
    public bool Stackable { get; set; } = false;

    [Column("bDisplay", TypeName = "tinyint(1)")]
    
    [Display(Name = "Display")]
    public bool Display { get; set; } = true;

    [Column("bDisplayOther", TypeName = "tinyint(1)")]
    
    [Display(Name = "DisplayOther")]
    public bool DisplayOther { get; set; } = false;

    [Column("bDisplayGameOver", TypeName = "tinyint(1)")]
    
    [Display(Name = "DisplayGameOver")]
    public bool DisplayGameOver { get; set; } = true;

    [Column("nColor")]
    
    [Display(Name = "Color")]
    public ConditionColor Color { get; set; } = ConditionColor.White;

    [Column("bResetTimer", TypeName = "tinyint(1)")]
    
    [Display(Name = "ResetTimer")]
    public bool ResetTimer { get; set; } = true;

    [Column("bRemoveAll", TypeName = "tinyint(1)")]
    
    [Display(Name = "RemoveAll")]
    public bool RemoveAll { get; set; } = false;

    [Column("bRemovePostCombat", TypeName = "tinyint(1)")]
    
    [Display(Name = "RemovePostCombat")]
    public bool RemovePostCombat { get; set; } = false;

    [Column("nTransferRange")]
    
    [Display(Name = "TransferRange")]
    public int TransferRange { get; set; } = -1;

    [Column("aThresholds", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Thresholds")]
    public string Thresholds { get; set; } = "";
}