using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("creatures")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class Creature : IEntity
{

    [Column("id")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strNamePublic", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "NamePublic")]
    public string NamePublic { get; set; } = "";

    [Column("strNotes", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Notes")]
    public string Notes { get; set; } = "";

    [Column("strImg", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "Image")]
    [ReferenceField(typeof(ImageAsset), TargetKey = "{FileName}")]
    public ReferenceList<IReferenceEntry> Image { get; set; } = new();

    [Column("vEncounterIDs", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "EncounterIds")]
    [ReferenceField(typeof(Encounter), Separator = ",")]
    public ReferenceList<IReferenceEntry> EncounterIds { get; set; } = new();

    [Column("nMovesPerTurn")]

    [Display(Name = "MovesPerTurn")]
    public int MovesPerTurn { get; set; }

    [Column("nTreasureID")]

    [Display(Name = "TreasureId")]
    [ReferenceField(typeof(TreasureTable))]
    public ReferenceList<IReferenceEntry> TreasureId { get; set; } = new();

    [Column("nFaction")]

    [Display(Name = "FactionId")]
    [ReferenceField(typeof(Faction))]
    public ReferenceList<IReferenceEntry> Faction { get; set; } = new();

    [Column("vAttackModes", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "AttackModes")]
    [ReferenceField(typeof(AttackMode), Separator = ",")]
    public ReferenceList<IReferenceEntry> AttackModes { get; set; } = new();

    [Column("vBaseConditions", TypeName = "longtext")]

    [Display(Name = "BaseConditions")]
    [ReferenceField(typeof(Condition), Separator = ",", Pattern = "{id}={value}")]
    public ReferenceList<IReferenceEntry> BaseConditions { get; set; } = new();

    [Column("nCorpseID")]

    [Display(Name = "CorpseId")]
    [ReferenceField(typeof(TreasureTable))]
    public ReferenceList<IReferenceEntry> CorpseId { get; set; } = new();

    [Column("vActivities", TypeName = "longtext")]
    
    [Display(Name = "Activities")]
    public string Activities { get; set; } = "";
}