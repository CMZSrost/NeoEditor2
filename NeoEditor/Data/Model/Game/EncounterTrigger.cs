using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("encountertriggers")]
[Comment("事件触发器 - 定义剧情事件的触发条件和方式")]
public class EncounterTrigger
{
    [Display(Name = "EncounterTrigger_ModId")]
    public int ModId { get; set; }

    [Key]
    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "EncounterTrigger_Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("触发的剧情名称，如'在冷冻睡眠舱内醒来'")]
    [Display(Name = "EncounterTrigger_Name")]
    public string Name { get; set; } = "";

    [Column("nEncounterID")]
    [Comment("触发器的代码标号")]
    [Display(Name = "EncounterTrigger_EncounterId")]
    public int EncounterId { get; set; }

    [Column("fChance", TypeName = "float")]
    [Comment("触发几率")]
    [Display(Name = "EncounterTrigger_Chance")]
    public double Chance { get; set; }

    [Column("bLocBased", TypeName = "tinyint(1)")]
    [Comment("该触发器是否为固定位置触发")]
    [Display(Name = "EncounterTrigger_LocBased")]
    public bool LocBased { get; set; }

    [Column("bDateBased", TypeName = "tinyint(1)")]
    [Comment("该触发器是否为固定时间触发")]
    [Display(Name = "EncounterTrigger_DateBased")]
    public bool DateBased { get; set; }

    [Column("bHexBased", TypeName = "tinyint(1)")]
    [Comment("该触发器是否为固定场景触发")]
    [Display(Name = "EncounterTrigger_HexBased")]
    public bool HexBased { get; set; }

    [Column("bUnique", TypeName = "tinyint(1)")]
    [Comment("该事件是否是独一无二的")]
    [Display(Name = "EncounterTrigger_Unique")]
    public bool Unique { get; set; }

    [Column("bAIPassable", TypeName = "tinyint(1)")]
    [Comment("该事件是否能被AI触发")]
    [Display(Name = "EncounterTrigger_AIPassable")]
    public bool AIPassable { get; set; } = true;

    [Column("aArea", TypeName = "varchar(25)")]
    [StringLength(25)]
    [Comment("该事件触发的位置坐标，格式如'20,164,0'")]
    [Display(Name = "EncounterTrigger_Area")]
    public string Area { get; set; } = "";

    [Column("dateMin", TypeName = "varchar(15)")]
    [StringLength(15)]
    [Comment("最小触发时间，格式'年-月-日-小时'")]
    [Display(Name = "EncounterTrigger_DateMin")]
    public string DateMin { get; set; } = "";

    [Column("dateMax", TypeName = "varchar(15)")]
    [StringLength(15)]
    [Comment("最大触发时间，格式'年-月-日-小时'")]
    [Display(Name = "EncounterTrigger_DateMax")]
    public string DateMax { get; set; } = "";

    [Column("aHexTypes", TypeName = "longtext")]
    [Comment("可触发该触发器的固定场景ID列表，结合hextypes使用")]
    [Display(Name = "EncounterTrigger_HexTypes")]
    public string HexTypes { get; set; } = "";
}