using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("conditions")]
[Comment("状态 - 定义所有角色可以拥有的状态和效果")]
public class Condition
{
    [Display(Name = "Condition_ModId")] public int ModId { get; set; }

    [Key]
    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Condition_Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("状态名称，如'饥肠辘辘'")]
    [Display(Name = "Condition_Name")]
    public string Name { get; set; } = "";

    [Column("strDesc", TypeName = "longtext")]
    [Comment("状态注释/描述，如'&lt;us&gt; 正在挨饿。'")]
    [Display(Name = "Condition_Description")]
    public string Description { get; set; } = "";

    [Column("aFieldNames", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("该状态为你带来的效果字段列表，如'm_fHealPerHourMod,m_fImmuneRestoreRate'等")]
    [Display(Name = "Condition_FieldNames")]
    public string FieldNames { get; set; } = "";

    [Column("aModifiers", TypeName = "varchar(100)")]
    [StringLength(100)]
    [Comment("该状态为你带来的效果的具体影响数值，与FieldNames对应")]
    [Display(Name = "Condition_Modifiers")]
    public string Modifiers { get; set; } = "";

    [Column("aEffects", TypeName = "longtext")]
    [Comment("状态带来的效果，如SetImmunity免疫力，ArmorWound护甲。如'ArmorWound=100,0.1,0.05'，")]
    [Display(Name = "Condition_Effects")]
    public string Effects { get; set; } = "";

    [Column("bFatal", TypeName = "tinyint(1)")]
    [Comment("是否为玩家带来死亡效果（得到这个状态会不会暴毙）")]
    [Display(Name = "Condition_Fatal")]
    public bool Fatal { get; set; } = false;

    [Column("vIDNext", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("该状态下一阶段为你带来的新状态ID，可能为多个逗号分隔")]
    [Display(Name = "Condition_IdNext")]
    public string IdNext { get; set; } = "0";

    [Column("fDuration", TypeName = "float")]
    [Comment("持续时间（小时）")]
    [Display(Name = "Condition_Duration")]
    public double Duration { get; set; } = 0;

    [Column("bPermanent", TypeName = "tinyint(1)")]
    [Comment("是否为会为你带来长期影响（如吃药、割伤等），为true表示回合结束不会自动消失")]
    [Display(Name = "Condition_Permanent")]
    public bool Permanent { get; set; } = false;

    [Column("vChanceNext", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("有多大几率该状态在下个阶段为你带来新的状态")]
    [Display(Name = "Condition_ChanceNext")]
    public string ChanceNext { get; set; } = "0";

    [Column("bStackable", TypeName = "tinyint(1)")]
    [Comment("该状态是否可以堆叠")]
    [Display(Name = "Condition_Stackable")]
    public bool Stackable { get; set; } = false;

    [Column("bDisplay", TypeName = "tinyint(1)")]
    [Comment("该状态是否可见")]
    [Display(Name = "Condition_Display")]
    public bool Display { get; set; } = true;

    [Column("bDisplayOther", TypeName = "tinyint(1)")]
    [Comment("该状态是否可被其他人看见")]
    [Display(Name = "Condition_DisplayOther")]
    public bool DisplayOther { get; set; } = false;

    [Column("bDisplayGameOver", TypeName = "tinyint(1)")]
    [Comment("该状态是否会在你的游戏总结中出现")]
    [Display(Name = "Condition_DisplayGameOver")]
    public bool DisplayGameOver { get; set; } = true;

    [Column("nColor")]
    [Comment("状态颜色：0白色，1红色，2绿色，3黄色")]
    [Display(Name = "Condition_Color")]
    public ConditionColor Color { get; set; } = ConditionColor.White;

    [Column("bResetTimer", TypeName = "tinyint(1)")]
    [Comment("刷新时间，游戏中休息一次为一小时")]
    [Display(Name = "Condition_ResetTimer")]
    public bool ResetTimer { get; set; } = true;

    [Column("bRemoveAll", TypeName = "tinyint(1)")]
    [Comment("未知参数")]
    [Display(Name = "Condition_RemoveAll")]
    public bool RemoveAll { get; set; } = false;

    [Column("bRemovePostCombat", TypeName = "tinyint(1)")]
    [Comment("未知参数")]
    [Display(Name = "Condition_RemovePostCombat")]
    public bool RemovePostCombat { get; set; } = false;

    [Column("nTransferRange")]
    [Comment("传染距离，-1为不传播")]
    [Display(Name = "Condition_TransferRange")]
    public int TransferRange { get; set; } = -1;

    [Column("aThresholds", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("触发阈值，目前用于传奇技能的触发")]
    [Display(Name = "Condition_Thresholds")]
    public string Thresholds { get; set; } = "";
}