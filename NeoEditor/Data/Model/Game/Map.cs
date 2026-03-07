using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("maps")]
[Comment("地图 - 定义游戏地图数据，结合hextype使用")]
[Index(nameof(EntityId), nameof(Id), IsUnique = true, Name = "UID_Key")]
public class Map : IEntity
{
    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("调用的图片文件名")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strDef", TypeName = "text")]
    [Comment("地图定义数据，大量数字和逗号组成的地形数据")]
    [Display(Name = "Definition")]
    public string Definition { get; set; } = "";
}