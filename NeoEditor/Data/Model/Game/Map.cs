using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("maps")]
[Comment("地图 - 定义游戏地图数据，结合hextype使用")]
public class Map
{
    [Display(Name = "Map_ModId")] public int ModId { get; set; }

    [Key]
    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Map_Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("调用的图片文件名")]
    [Display(Name = "Map_Name")]
    public string Name { get; set; } = "";

    [Column("strDef", TypeName = "text")]
    [Comment("地图定义数据，大量数字和逗号组成的地形数据")]
    [Display(Name = "Map_Definition")]
    public string Definition { get; set; } = "";
}