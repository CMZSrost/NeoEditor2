using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("itemprops")]
[Comment("合成项属性/物品属性 - 定义所有物品和合成项可能拥有的属性")]
public class ItemProp
{
    [Display(Name = "ModId")] public int ModId { get; set; }

    [Key]
    [Column("nID")]
    [Comment("合成项属性标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strPropertyName", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("属性名称，如'easily ignitable'（易燃物）")]
    [Display(Name = "PropertyName")]
    public string PropertyName { get; set; } = "";
}