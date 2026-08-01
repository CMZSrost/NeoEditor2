using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("ingredients")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class Ingredient : IEntity
{

    [Column("nID")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strRequiredProps", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "RequiredProps")]
    [ReferenceField(typeof(ItemProp), Separator = "&")]
    public ReferenceList<IReferenceEntry> RequiredProps { get; set; } = new();

    [Column("strForbidProps", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "ForbidProps")]
    [ReferenceField(typeof(ItemProp), Separator = "&")]
    public ReferenceList<IReferenceEntry> ForbidProps { get; set; } = new();
}