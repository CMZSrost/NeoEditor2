using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("creaturesources")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class CreatureSource : IEntity
{

    [Column("id")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("nX")]
    
    [Display(Name = "X")]
    public int X { get; set; } = -1;

    [Column("nY")]
    
    [Display(Name = "Y")]
    public int Y { get; set; } = -1;

    [Column("nCreatureID")]
    
    [Display(Name = "CreatureId")]
    [ReferenceField(typeof(Creature))]
    public ReferenceList<IReferenceEntry> CreatureId { get; set; } = new();

    [Column("nMin")]
    
    [Display(Name = "Min")]
    public int Min { get; set; } = 0;

    [Column("nMax")]
    
    [Display(Name = "Max")]
    public int Max { get; set; } = 0;

    [Column("fWeight", TypeName = "float")]
    
    [Display(Name = "Weight")]
    public double Weight { get; set; } = 1;
}