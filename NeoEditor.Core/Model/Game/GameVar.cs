using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeoEditor.Data.Model.Game;

[Table("gamevars")]

[UIDKey(nameof(EntityId), nameof(Name))]
public class GameVar : IEntity
{

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strType", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Type")]
    public string Type { get; set; } = "";

    [Column("strValue", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Value")]
    public string Value { get; set; } = "";
}