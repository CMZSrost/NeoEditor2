using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("dmcplaces")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class DmcPlace : IEntity
{

    [Column("id")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strImg", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Image")]
    public string Image { get; set; } = "";

    [Column("nEncounterID")]
    
    [Display(Name = "EncounterId")]
    [ReferenceField(typeof(Encounter))]
    public ReferenceList<IReferenceEntry> EncounterId { get; set; } = new();

    [Column("nX")]
    
    [Display(Name = "X")]
    public int X { get; set; } = 0;

    [Column("nY")]
    
    [Display(Name = "Y")]
    public int Y { get; set; } = 0;
}