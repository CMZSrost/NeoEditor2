using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("recipes")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class Recipe : IEntity
{

    [Column("nID")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strSecretName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "SecretName")]
    public string SecretName { get; set; } = "";

    [Column("strTools", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Tools")]
    [ReferenceField(typeof(Ingredient), Separator = "+", Pattern = "{mult}x{id}")]
    public ReferenceList<IReferenceEntry> Tools { get; set; } = new();

    [Column("strConsumed", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "Consumed")]
    [ReferenceField(typeof(Ingredient), Separator = "+", Pattern = "{mult}x{id}")]
    public ReferenceList<IReferenceEntry> Consumed { get; set; } = new();

    [Column("strDestroyed", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "Destroyed")]
    [ReferenceField(typeof(Ingredient), Separator = "+", Pattern = "{mult}x{id}")]
    public ReferenceList<IReferenceEntry> Destroyed { get; set; } = new();

    [Column("nTreasureID")]

    [Display(Name = "TreasureId")]
    [ReferenceField(typeof(TreasureTable))]
    public ReferenceList<IReferenceEntry> TreasureId { get; set; } = new();

    [Column("fHours", TypeName = "float")]
    
    [Display(Name = "Hours")]
    public double Hours { get; set; }

    [Column("nReverse")]
    
    [Display(Name = "Reverse")]
    public int Reverse { get; set; } = 0;

    [Column("nHiddenID")]
    
    [Display(Name = "HiddenId")]
    [ReferenceField(typeof(Recipe))]
    public ReferenceList<IReferenceEntry> HiddenId { get; set; } = new();

    [Column("bIdentify", TypeName = "tinyint(1)")]

    [Display(Name = "Identify")]
    public bool Identify { get; set; } = false;

    [Column("bTransferComponents", TypeName = "tinyint(1)")]

    [Display(Name = "TransferComponents")]
    public bool TransferComponents { get; set; } = false;

    [Column("vAlsoTry", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "AlsoTry")]
    [ReferenceField(typeof(Recipe), Separator = ",")]
    public ReferenceList<IReferenceEntry> AlsoTry { get; set; } = new();

    [Column("nTempTreasureID")]

    [Display(Name = "TempTreasureId")]
    [ReferenceField(typeof(TreasureTable))]
    public ReferenceList<IReferenceEntry> TempTreasureId { get; set; } = new();

    [Column("bDegradeOutput", TypeName = "tinyint(1)")]
    
    [Display(Name = "DegradeOutput")]
    public bool DegradeOutput { get; set; } = false;

    [Column("strType", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Type")]
    public string Type { get; set; } = "";

    [Column("bScrap", TypeName = "tinyint(1)")]
    
    [Display(Name = "Scrap")]
    public bool Scrap { get; set; } = true;
}