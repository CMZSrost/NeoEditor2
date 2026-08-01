using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("treasuretable")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class TreasureTable : IEntity
{

    [Column("id")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("aTreasures", TypeName = "text")]
    
    [Display(Name = "Treasures")]
    [ReferenceField(typeof(ItemType), Separator = ",", Pattern = "{id}x{mult}",
        TargetKey = "{GroupId}.{SubgroupId}",
        SecondaryTargetEntityType = typeof(TreasureTable), SecondaryTargetKey = "{Id}")]
    public ReferenceList<IReferenceEntry> Treasures { get; set; } = new();

    [Column("bNested", TypeName = "tinyint(1)")]
    
    [Display(Name = "Nested")]
    public bool Nested { get; set; } = false;

    [Column("bSuppress", TypeName = "tinyint(1)")]
    
    [Display(Name = "Suppress")]
    public bool Suppress { get; set; } = false;

    [Column("bIdentify", TypeName = "tinyint(1)")]
    
    [Display(Name = "Identify")]
    public bool Identify { get; set; } = false;
}