using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("creaturesources")]
[Comment("生物刷新点 - 定义生物在地图上的刷新位置和数量")]
public class CreatureSource
{
    [Display(Name = "ModId")] public int ModId { get; set; }

    [Key]
    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("生物名称，如'来自东南方的掠夺者'")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("nX")]
    [Comment("X轴坐标")]
    [Display(Name = "X")]
    public int X { get; set; } = -1;

    [Column("nY")]
    [Comment("Y轴坐标")]
    [Display(Name = "Y")]
    public int Y { get; set; } = -1;

    [Column("nCreatureID")]
    [Comment("刷新的生物编号，结合creatures中的标号")]
    [Display(Name = "CreatureId")]
    public int CreatureId { get; set; } = 0;

    [Column("nMin")]
    [Comment("最小刷新数量")]
    [Display(Name = "Min")]
    public int Min { get; set; } = 0;

    [Column("nMax")]
    [Comment("最大刷新数量（数值过大会导致大量生物刷新在同一区块）")]
    [Display(Name = "Max")]
    public int Max { get; set; } = 0;

    [Column("fWeight", TypeName = "float")]
    [Comment("权重，具体含义未知")]
    [Display(Name = "Weight")]
    public double Weight { get; set; } = 1;
}