using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("factions")]
[Comment("阵营/派系 - 定义游戏中的派系及其相互关系，与creatures数据相关")]
public class Faction
{
    [Display(Name = "ModId")] [NotMapped] public int ModId { get; set; }

    [Key]
    [Column("id")]
    [Comment("代码标号/阵营标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("阵营名称，如'狗人'")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("dictFactions", TypeName = "longtext")]
    [Comment("与其他派系的声望关系，格式如'0=-100,1=1,2=-100'")]
    [Display(Name = "DictFactions")]
    public string DictFactions { get; set; } = "";
}