using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("encounters")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class Encounter : IEntity
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

    [Column("strImg", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Image")]
    public string Image { get; set; } = "EncBlank.png";

    [Column("nTreasureID")]
    
    [Display(Name = "TreasureId")]
    [ReferenceField(typeof(TreasureTable))]
    public ReferenceList<IReferenceEntry> TreasureId { get; set; } = new();

    [Column("nRemoveTreasureID")]

    [Display(Name = "RemoveTreasureId")]
    [ReferenceField(typeof(TreasureTable))]
    public ReferenceList<IReferenceEntry> RemoveTreasureId { get; set; } = new();

    [Column("aConditions", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "Conditions")]
    [ReferenceField(typeof(Condition), Separator = ",")]
    public ReferenceList<IReferenceEntry> Conditions { get; set; } = new();

    [Column("aPreConditions", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "PreConditions")]
    [ReferenceField(typeof(Condition), Separator = ",")]
    public ReferenceList<IReferenceEntry> PreConditions { get; set; } = new();

    [Column("fPrice", TypeName = "float")]
    
    [Display(Name = "Price")]
    public double Price { get; set; } = 0;

    [Column("aResponses", TypeName = "longtext")]
    
    [Display(Name = "Responses")]
    public string Responses { get; set; } = "";

    [Column("aMinimapHexes", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "MinimapHexes")]
    public string MinimapHexes { get; set; } = "";

    [Column("bRemoveCreatures", TypeName = "tinyint(1)")]
    
    [Display(Name = "RemoveCreatures")]
    public bool RemoveCreatures { get; set; } = false;

    [Column("bRemoveUsed", TypeName = "tinyint(1)")]
    
    [Display(Name = "RemoveUsed")]
    public bool RemoveUsed { get; set; } = false;

    [Column("nItemsID")]
    
    [Display(Name = "ItemsId")]
    [ReferenceField(typeof(ItemType), TargetKey = "{GroupId}.{SubgroupId}")]
    public ReferenceList<IReferenceEntry> ItemsId { get; set; } = new();

    [Column("nCreatureID")]

    [Display(Name = "CreatureId")]
    [ReferenceField(typeof(Creature))]
    public ReferenceList<IReferenceEntry> CreatureId { get; set; } = new();

    [Column("ptCreatureHex", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "CreatureHex")]
    public string CreatureHex { get; set; } = "0,0";

    [Column("ptTeleport", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Teleport")]
    public string Teleport { get; set; } = "0,0";

    [Column("ptEditor", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Editor")]
    public string Editor { get; set; } = "0,0";

    [Column("nType", TypeName = "tinyint(1)")]
    
    [Display(Name = "Type")]
    public EncounterType Type { get; set; } = EncounterType.Normal;

    [Column("fLootChance", TypeName = "float")]
    
    [Display(Name = "LootChance")]
    public double LootChance { get; set; } = 0;

    [Column("fAccidentChance", TypeName = "float")]
    
    [Display(Name = "AccidentChance")]
    public double AccidentChance { get; set; } = 0;

    [Column("fCreatureChance", TypeName = "float")]
    
    [Display(Name = "CreatureChance")]
    public double CreatureChance { get; set; } = 0;

    [Column("vAccidents", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Accidents")]
    [ReferenceField(typeof(Encounter), Separator = ",")]
    public ReferenceList<IReferenceEntry> Accidents { get; set; } = new();

    [Column("vLoot", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "Loot")]
    [ReferenceField(typeof(TreasureTable))]
    public ReferenceList<IReferenceEntry> Loot { get; set; } = new();
}