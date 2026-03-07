using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("factions")]
[Comment("阵营/派系 - 定义游戏中的派系及其相互关系，与creatures数据相关")]
[Index(nameof(EntityId), nameof(Id), IsUnique =  true, Name = "UID_Key")]
public class Faction : IEntity
{

    [Column("id")]
    [Comment("代码标号/阵营标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("阵营名称，如'狗人'")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("dictFactions", TypeName = "longtext")]
    [Comment("与其他派系的声望关系，格式如'0=-100,1=1,2=-100'")]
    [Display(Name = "DictFactions")]
    public string DictFactions { get; set; } = "";
}