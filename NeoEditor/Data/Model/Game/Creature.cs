using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("creatures")]
[Comment("生物与生物派系 - 定义游戏中的所有生物")]
public class Creature
{
    [Display(Name = "ModId")] [NotMapped] public int ModId { get; set; }

    [Key]
    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("生物名称，如'狗人'")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strNamePublic", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("未接触时显示的名称")]
    [Display(Name = "NamePublic")]
    public string NamePublic { get; set; } = "";

    [Column("strNotes", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("注释")]
    [Display(Name = "Notes")]
    public string Notes { get; set; } = "";

    [Column("strImg", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("该生物在地图上调用的图片文件名")]
    [Display(Name = "Image")]
    public string Image { get; set; } = "";

    [Column("vEncounterIDs", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("遇到该生物会触发的状态ID列表，逗号分隔")]
    [Display(Name = "EncounterIds")]
    public string EncounterIds { get; set; } = "";

    [Column("nMovesPerTurn")]
    [Comment("每回合的行动点数")]
    [Display(Name = "MovesPerTurn")]
    public int MovesPerTurn { get; set; }

    [Column("nTreasureID")]
    [Comment("战利品池ID（击杀掉落）")]
    [Display(Name = "TreasureId")]
    public int TreasureId { get; set; } = 3;

    [Column("nFaction")]
    [Comment("所属阵营ID")]
    [Display(Name = "Faction")]
    public int Faction { get; set; } = 0;

    [Column("vAttackModes", TypeName = "varchar(25)")]
    [StringLength(25)]
    [Comment("攻击方式ID，结合attackmodes使用，可能为单个ID或逗号分隔")]
    [Display(Name = "AttackModes")]
    public string AttackModes { get; set; } = "";

    [Column("vBaseConditions", TypeName = "longtext")]
    [Comment("该生物生成时的基础状态，格式如'38=1,50=0.5'")]
    [Display(Name = "BaseConditions")]
    public string BaseConditions { get; set; } = "";

    [Column("nCorpseID")]
    [Comment("尸体编号（战利品池编号）")]
    [Display(Name = "CorpseId")]
    public int CorpseId { get; set; } = 3;

    [Column("vActivities", TypeName = "longtext")]
    [Comment("该生物的活动方式描述，可能仅为注释用途")]
    [Display(Name = "Activities")]
    public string Activities { get; set; } = "";
}