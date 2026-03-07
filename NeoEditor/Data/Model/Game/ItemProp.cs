using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("itemprops")]
[Comment("合成项属性/物品属性 - 定义所有物品和合成项可能拥有的属性")]
[Index(nameof(EntityId), nameof(Id), IsUnique =  true, Name = "UID_Key")]
public class ItemProp : IEntity
{

    [Column("nID")]
    [Comment("合成项属性标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strPropertyName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("属性名称，如'easily ignitable'（易燃物）")]
    [Display(Name = "PropertyName")]
    public string PropertyName { get; set; } = "";
}