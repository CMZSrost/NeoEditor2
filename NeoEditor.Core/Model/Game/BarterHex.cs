using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeoEditor.Data.Model.Game;

[Table("barterhexes")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class BarterHex : IEntity
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

    [Column("bBuys", TypeName = "tinyint(1)")]
    
    [Display(Name = "Buys")]
    public bool Buys { get; set; } = false;

    [Column("nRestockTreasureID")]
    
    [Display(Name = "RestockTreasureId")]
    public int RestockTreasureId { get; set; } = 3;
}