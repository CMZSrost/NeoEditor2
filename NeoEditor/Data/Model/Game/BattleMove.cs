using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("battlemoves")]
[Comment("战斗动作 - 定义所有可在战斗中使用的动作")]
public class BattleMove
{
    [Display(Name = "ModId")] public int ModId { get; set; }

    [Key]
    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strID", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("物品标号，如'90.35'")]
    [Display(Name = "StrId")]
    public string StrId { get; set; } = "";

    [Column("strName", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("动作名称，如'掩体中后退'")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strNotes", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("动作注释，一般留空，对游戏无影响")]
    [Display(Name = "Notes")]
    public string Notes { get; set; } = "";

    [Column("strSuccess", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("动作执行成功后在游戏中显示的文本，&lt;us&gt;代表自己，&lt;them&gt;代表目标")]
    [Display(Name = "Success")]
    public string Success { get; set; } = "";

    [Column("strFail", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("动作执行失败后在游戏中显示的文本")]
    [Display(Name = "Fail")]
    public string Fail { get; set; } = "";

    [Column("strPopUp", TypeName = "longtext")]
    [Comment("游戏内的动作说明，显示在动作选择界面")]
    [Display(Name = "PopUp")]
    public string PopUp { get; set; } = "";

    [Column("vChanceType", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("几率类型（目前未明）")]
    [Display(Name = "ChanceType")]
    public string ChanceType { get; set; } = "0,0,0";

    [Column("vUsConditions", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("需要我方处于的状态，负号表示非，即不处于该状态'")]
    [Display(Name = "UsConditions")]
    public string UsConditions { get; set; } = "";

    [Column("vThemConditions", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("需要对方处于的状态，负号表示非，即不处于该状态")]
    [Display(Name = "ThemConditions")]
    public string ThemConditions { get; set; } = "";

    [Column("vPairConditions", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("该行动对自己带来的影响")]
    [Display(Name = "PairConditions")]
    public string PairConditions { get; set; } = "";

    [Column("vUsFailConditions", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("行动失败时会为自己带来的状态")]
    [Display(Name = "UsFailConditions")]
    public string UsFailConditions { get; set; } = "";

    [Column("vThemFailConditions", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("行动失败时会为敌方带来的状态（官方所有数据均未填写）")]
    [Display(Name = "ThemFailConditions")]
    public string ThemFailConditions { get; set; } = "";

    [Column("vPairFailConditions", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("行动失败时会为自己带来的影响")]
    [Display(Name = "PairFailConditions")]
    public string PairFailConditions { get; set; } = "";

    [Column("vUsPreConditions", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("执行该行动的前置要求，格式如'137,151,-143'（负数表示不可拥有该状态）")]
    [Display(Name = "UsPreConditions")]
    public string UsPreConditions { get; set; } = "";

    [Column("vThemPreConditions", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("敌方需要满足的前置条件")]
    [Display(Name = "ThemPreConditions")]
    public string ThemPreConditions { get; set; } = "";

    [Column("nSeeThem")]
    [Comment("对方需要处于的暴露等级")]
    [Display(Name = "SeeThem")]
    public int SeeThem { get; set; } = 2;

    [Column("nSeeUs")]
    [Comment("自己需要处于的暴露等级")]
    [Display(Name = "SeeUs")]
    public int SeeUs { get; set; } = 2;

    [Column("bAllOutOfRange", TypeName = "tinyint(1)")]
    [Comment("离开所有场上目标距离")]
    [Display(Name = "AllOutOfRange")]
    public bool AllOutOfRange { get; set; } = false;

    [Column("bInAttackRange", TypeName = "tinyint(1)")]
    [Comment("攻击范围，可能用于判定溅射或武器长度")]
    [Display(Name = "InAttackRange")]
    public bool InAttackRange { get; set; } = false;

    [Column("nMinCharges")]
    [Comment("攻击次数（存疑）")]
    [Display(Name = "MinCharges")]
    public int MinCharges { get; set; } = 0;

    [Column("nMinRange")]
    [Comment("距离最小需求，-1为全场覆盖")]
    [Display(Name = "MinRange")]
    public int MinRange { get; set; } = -1;

    [Column("nMaxRange")]
    [Comment("距离最大需求，-1为全场覆盖")]
    [Display(Name = "MaxRange")]
    public int MaxRange { get; set; } = -1;

    [Column("nAttackModeType")]
    [Comment("攻击模式类型：-1非攻击动作，0近战攻击，1远程攻击")]
    [Display(Name = "AttackModeType")]
    public BattleMoveType AttackModeType { get; set; } = BattleMoveType.NonAttack;

    [Column("vHexTypes", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("所在地图格子类型（官方所有数据均留空）")]
    [Display(Name = "HexTypes")]
    public string HexTypes { get; set; } = "";

    [Column("fChance", TypeName = "float")]
    [Comment("可以使用该技能的几率（百分比）")]
    [Display(Name = "Chance")]
    public double Chance { get; set; } = 1;

    [Column("fPriority", TypeName = "float")]
    [Comment("优先级，对玩家无效，Bot专用，同一回合中哪边的战斗行动先触发")]
    [Display(Name = "Priority")]
    public double Priority { get; set; } = 0;

    [Column("fDetect", TypeName = "float")]
    [Comment("执行该动作使你被发现的几率，设为0则不会被发现")]
    [Display(Name = "Detect")]
    public double Detect { get; set; } = 1;

    [Column("fOrder", TypeName = "float")]
    [Comment("未知参数")]
    [Display(Name = "Order")]
    public double Order { get; set; } = 0.5;

    [Column("fFatigue", TypeName = "float")]
    [Comment("疲劳值消耗")]
    [Display(Name = "Fatigue")]
    public double Fatigue { get; set; } = 0;

    [Column("bApproach", TypeName = "tinyint(1)")]
    [Comment("该动作是否会使你接近对方")]
    [Display(Name = "Approach")]
    public bool Approach { get; set; } = false;

    [Column("bOffense", TypeName = "tinyint(1)")]
    [Comment("是否是攻击性动作")]
    [Display(Name = "Offense")]
    public bool Offense { get; set; } = false;

    [Column("bFallBack", TypeName = "tinyint(1)")]
    [Comment("该动作是否为远离动作")]
    [Display(Name = "FallBack")]
    public bool FallBack { get; set; } = false;

    [Column("bRetreat", TypeName = "tinyint(1)")]
    [Comment("该动作是否为撤退动作")]
    [Display(Name = "Retreat")]
    public bool Retreat { get; set; } = false;

    [Column("bPosition", TypeName = "tinyint(1)")]
    [Comment("该动作是否为姿势动作")]
    [Display(Name = "Position")]
    public bool Position { get; set; } = false;

    [Column("bPassive", TypeName = "tinyint(1)")]
    [Comment("该动作是否为被动")]
    [Display(Name = "Passive")]
    public bool Passive { get; set; } = false;
}