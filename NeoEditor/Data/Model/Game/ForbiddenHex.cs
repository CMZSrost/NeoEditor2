using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("forbiddenhexes")]
[Comment("保护区场景位置 - 定义不可进入或受保护的区域")]
[Index(nameof(EntityId), nameof(Id), IsUnique =  true, Name = "UID_Key")]
public class ForbiddenHex : IEntity
{

    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("nX")]
    [Comment("X轴坐标")]
    [Display(Name = "X")]
    public int X { get; set; } = 0;

    [Column("nY")]
    [Comment("Y轴坐标")]
    [Display(Name = "Y")]
    public int Y { get; set; } = 0;

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("保护区所属阵营或名称，如'阿尼什纳比部族'")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";
}