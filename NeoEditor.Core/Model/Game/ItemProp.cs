using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeoEditor.Data.Model.Game;

[Table("itemprops")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class ItemProp : IEntity
{

    [Column("nID")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strPropertyName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "PropertyName")]
    public string PropertyName { get; set; } = "";
}