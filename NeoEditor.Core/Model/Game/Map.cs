using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeoEditor.Data.Model.Game;

[Table("maps")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class Map : IEntity
{
    [Column("id")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strDef", TypeName = "text")]
    
    [Display(Name = "Definition")]
    public string Definition { get; set; } = "";
}