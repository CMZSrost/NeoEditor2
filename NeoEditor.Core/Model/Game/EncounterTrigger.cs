using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("encountertriggers")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class EncounterTrigger : IEntity
{

    [Column("id")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("nEncounterID")]
    
    [Display(Name = "EncounterId")]
    [ReferenceField(typeof(Encounter))]
    public ReferenceList<IReferenceEntry> EncounterId { get; set; } = new();

    [Column("fChance", TypeName = "float")]

    [Display(Name = "Chance")]
    public double Chance { get; set; }

    [Column("bLocBased", TypeName = "tinyint(1)")]

    [Display(Name = "LocBased")]
    public bool LocBased { get; set; }

    [Column("bDateBased", TypeName = "tinyint(1)")]

    [Display(Name = "DateBased")]
    public bool DateBased { get; set; }

    [Column("bHexBased", TypeName = "tinyint(1)")]

    [Display(Name = "HexBased")]
    public bool HexBased { get; set; }

    [Column("bUnique", TypeName = "tinyint(1)")]

    [Display(Name = "Unique")]
    public bool Unique { get; set; }

    [Column("bAIPassable", TypeName = "tinyint(1)")]

    [Display(Name = "AIPassable")]
    public bool AIPassable { get; set; } = true;

    [Column("aArea", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "Area")]
    public string Area { get; set; } = "";

    [Column("dateMin", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "DateMin")]
    public string DateMin { get; set; } = "";

    [Column("dateMax", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "DateMax")]
    public string DateMax { get; set; } = "";

    [Column("aHexTypes", TypeName = "longtext")]

    [Display(Name = "HexTypes")]
    [ReferenceField(typeof(HexType), Separator = ",")]
    public ReferenceList<IReferenceEntry> HexTypes { get; set; } = new();
}