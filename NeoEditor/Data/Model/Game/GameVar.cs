using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("gamevars")]
[Comment("游戏变量 - 定义游戏的全局参数和初始值")]
[Index(nameof(EntityId), nameof(Name), IsUnique =  true, Name = "UID_Key")]
public class GameVar : IEntity
{

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("变量名称，如'nSkillPoints'（技能点数）")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strType", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("数值类型，如'int'、'Number'等")]
    [Display(Name = "Type")]
    public string Type { get; set; } = "";

    [Column("strValue", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("具体数值")]
    [Display(Name = "Value")]
    public string Value { get; set; } = "";
}