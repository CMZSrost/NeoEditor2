using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("battlemoves")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class BattleMove : IEntity
{

    [Column("id")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strID", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "StrId")]
    public string StrId { get; set; } = "";

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strNotes", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Notes")]
    public string Notes { get; set; } = "";

    [Column("strSuccess", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Success")]
    public string Success { get; set; } = "";

    [Column("strFail", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Fail")]
    public string Fail { get; set; } = "";

    [Column("strPopUp", TypeName = "longtext")]
    
    [Display(Name = "PopUp")]
    public string PopUp { get; set; } = "";

    [Column("vChanceType", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "ChanceType")]
    public string ChanceType { get; set; } = "0,0,0";

    [Column("vUsConditions", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "UsConditions")]
    [ReferenceField(typeof(Condition), Separator = "],[", Pattern = "[{id}")]
    public ReferenceList<IReferenceEntry> UsConditions { get; set; } = new();

    [Column("vThemConditions", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "ThemConditions")]
    [ReferenceField(typeof(Condition), Separator = "],[", Pattern = "[{id}")]
    public ReferenceList<IReferenceEntry> ThemConditions { get; set; } = new();

    [Column("vPairConditions", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "PairConditions")]
    [ReferenceField(typeof(Condition), Separator = "],[", Pattern = "[{id}")]
    public ReferenceList<IReferenceEntry> PairConditions { get; set; } = new();

    [Column("vUsFailConditions", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "UsFailConditions")]
    [ReferenceField(typeof(Condition), Separator = "],[", Pattern = "[{id}")]
    public ReferenceList<IReferenceEntry> UsFailConditions { get; set; } = new();

    [Column("vThemFailConditions", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "ThemFailConditions")]
    [ReferenceField(typeof(Condition), Separator = "],[", Pattern = "[{id}")]
    public ReferenceList<IReferenceEntry> ThemFailConditions { get; set; } = new();

    [Column("vPairFailConditions", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "PairFailConditions")]
    [ReferenceField(typeof(Condition), Separator = "],[", Pattern = "[{id}")]
    public ReferenceList<IReferenceEntry> PairFailConditions { get; set; } = new();

    [Column("vUsPreConditions", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "UsPreConditions")]
    [ReferenceField(typeof(Condition), Separator = ",")]
    public ReferenceList<IReferenceEntry> UsPreConditions { get; set; } = new();

    [Column("vThemPreConditions", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "ThemPreConditions")]
    [ReferenceField(typeof(Condition), Separator = ",")]
    public ReferenceList<IReferenceEntry> ThemPreConditions { get; set; } = new();

    [Column("nSeeThem")]
    
    [Display(Name = "SeeThem")]
    public int SeeThem { get; set; } = 2;

    [Column("nSeeUs")]
    
    [Display(Name = "SeeUs")]
    public int SeeUs { get; set; } = 2;

    [Column("bAllOutOfRange", TypeName = "tinyint(1)")]
    
    [Display(Name = "AllOutOfRange")]
    public bool AllOutOfRange { get; set; } = false;

    [Column("bInAttackRange", TypeName = "tinyint(1)")]
    
    [Display(Name = "InAttackRange")]
    public bool InAttackRange { get; set; } = false;

    [Column("nMinCharges")]
    
    [Display(Name = "MinCharges")]
    public int MinCharges { get; set; } = 0;

    [Column("nMinRange")]
    
    [Display(Name = "MinRange")]
    public int MinRange { get; set; } = -1;

    [Column("nMaxRange")]
    
    [Display(Name = "MaxRange")]
    public int MaxRange { get; set; } = -1;

    [Column("nAttackModeType")]
    
    [Display(Name = "AttackModeType")]
    public BattleMoveType AttackModeType { get; set; } = BattleMoveType.NonAttack;

    [Column("vHexTypes", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "HexTypes")]
    public string HexTypes { get; set; } = "";

    [Column("fChance", TypeName = "float")]
    
    [Display(Name = "Chance")]
    public double Chance { get; set; } = 1;

    [Column("fPriority", TypeName = "float")]
    
    [Display(Name = "Priority")]
    public double Priority { get; set; } = 0;

    [Column("fDetect", TypeName = "float")]
    
    [Display(Name = "Detect")]
    public double Detect { get; set; } = 1;

    [Column("fOrder", TypeName = "float")]
    
    [Display(Name = "Order")]
    public double Order { get; set; } = 0.5;

    [Column("fFatigue", TypeName = "float")]
    
    [Display(Name = "Fatigue")]
    public double Fatigue { get; set; } = 0;

    [Column("bApproach", TypeName = "tinyint(1)")]
    
    [Display(Name = "Approach")]
    public bool Approach { get; set; } = false;

    [Column("bOffense", TypeName = "tinyint(1)")]
    
    [Display(Name = "Offense")]
    public bool Offense { get; set; } = false;

    [Column("bFallBack", TypeName = "tinyint(1)")]
    
    [Display(Name = "FallBack")]
    public bool FallBack { get; set; } = false;

    [Column("bRetreat", TypeName = "tinyint(1)")]
    
    [Display(Name = "Retreat")]
    public bool Retreat { get; set; } = false;

    [Column("bPosition", TypeName = "tinyint(1)")]
    
    [Display(Name = "Position")]
    public bool Position { get; set; } = false;

    [Column("bPassive", TypeName = "tinyint(1)")]
    
    [Display(Name = "Passive")]
    public bool Passive { get; set; } = false;
}