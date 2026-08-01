using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeoEditor.Data.Model.Game;

[Table("chargeprofiles")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class ChargeProfile : IEntity
{

    [Column("nID")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strItemID", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "ItemId")]
    public string ItemId { get; set; } = "";

    [Column("fPerUse", TypeName = "float")]
    
    [Display(Name = "PerUse")]
    public double PerUse { get; set; } = 0;

    [Column("fPerHour", TypeName = "float")]
    
    [Display(Name = "PerHour")]
    public double PerHour { get; set; } = 0;

    [Column("fPerHourEquipped", TypeName = "float")]
    
    [Display(Name = "PerHourEquipped")]
    public double PerHourEquipped { get; set; } = 0;

    [Column("fPerHex", TypeName = "float")]
    
    [Display(Name = "PerHex")]
    public double PerHex { get; set; } = 0;

    [Column("bDegrade", TypeName = "tinyint(1)")]
    
    [Display(Name = "Degrade")]
    public bool Degrade { get; set; } = false;
}