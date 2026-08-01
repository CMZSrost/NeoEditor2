using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeoEditor.Data.Model.Game;

[Table("forbiddenhexes")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class ForbiddenHex : IEntity
{

    [Column("id")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("nX")]
    
    [Display(Name = "X")]
    public int X { get; set; } = 0;

    [Column("nY")]
    
    [Display(Name = "Y")]
    public int Y { get; set; } = 0;

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Name")]
    public string Name { get; set; } = "";
}