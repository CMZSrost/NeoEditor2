using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("treasuretable")]
[Comment("战利品池 - 定义各种战利品生成的概率和内容")]
public class TreasureTable
{
    [Display(Name = "ModId")] public int ModId { get; set; }

    [Key]
    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("战利品名称，如'瓮内容'")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("aTreasures", TypeName = "text")]
    [Comment("战利品池的内容，格式'物品IDx数量x几率'，竖线|表示或，逗号,表示和")]
    [Display(Name = "Treasures")]
    public string Treasures { get; set; } = "";

    [Column("bNested", TypeName = "tinyint(1)")]
    [Comment("生成物品是否会装在同时生成的容器里")]
    [Display(Name = "Nested")]
    public bool Nested { get; set; } = false;

    [Column("bSuppress", TypeName = "tinyint(1)")]
    [Comment("抑制物品可能产生的内容物生成：1时水瓶里不会有水，枪里不会有子弹")]
    [Display(Name = "Suppress")]
    public bool Suppress { get; set; } = false;

    [Column("bIdentify", TypeName = "tinyint(1)")]
    [Comment("生成的物品是否被辨识，是否显示隐藏名称")]
    [Display(Name = "Identify")]
    public bool Identify { get; set; } = false;
}