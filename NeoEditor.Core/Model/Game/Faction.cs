using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("factions")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class Faction : IEntity
{

    [Column("id")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("dictFactions", TypeName = "longtext")]
    
    [Display(Name = "DictFactions")]
    [ReferenceField(typeof(Faction), Separator = ",", Pattern = "{id}={value}")]
    public ReferenceList<IReferenceEntry> DictFactions { get; set; } = new();
}