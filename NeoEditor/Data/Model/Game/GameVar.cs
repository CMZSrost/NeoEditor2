using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("gamevars")]
[Comment("游戏变量 - 定义游戏的全局参数和初始值")]
public class GameVar
{
    [Display(Name = "GameVar_ModId")] public int ModId { get; set; }

    [Key]
    [Column("strName", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("变量名称，如'nSkillPoints'（技能点数）")]
    [Display(Name = "GameVar_Name")]
    public string Name { get; set; } = "";

    [Column("strType", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("数值类型，如'int'、'Number'等")]
    [Display(Name = "GameVar_Type")]
    public string Type { get; set; } = "";

    [Column("strValue", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("具体数值")]
    [Display(Name = "GameVar_Value")]
    public string Value { get; set; } = "";
}