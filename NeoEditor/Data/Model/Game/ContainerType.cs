using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("containertypes")]
[Comment("内容物属性与分类 - 定义物品的容器属性，结合itemtypes中的nTreasureID与aContentIDs使用")]
[Index(nameof(EntityId), nameof(Id), IsUnique =  true, Name = "UID_Key")]
public class ContainerType : IEntity
{

    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("属性名称，如'防水'、'精'、'粗'等")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";
}