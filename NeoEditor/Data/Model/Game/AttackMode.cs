using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("attackmodes")]
[Comment("攻击类别 - 定义所有武器和攻击方式的属性")]
public class AttackMode
{
    [Display(Name = "ModId")] public int ModId { get; set; } // 编排时使用，表示该数据来源于哪个Mod

    [Key]
    [Column("id")]
    [Comment("代码标号，唯一标识每种攻击方式")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("当前物品名称，如'拳头'、'.308步枪'等")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strNotes", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("物品注释，一般留空，对游戏无影响")]
    [Display(Name = "Notes")]
    public string Notes { get; set; } = "";

    [Column("nRange")]
    [Comment("攻击距离，近战为1，远程武器根据类型不同")]
    [Display(Name = "Range")]
    public int Range { get; set; } = 1;

    [Column("fDamageCut", TypeName = "float")]
    [Comment("切割伤害值")]
    [Display(Name = "DamageCut")]
    public double DamageCut { get; set; } = 0;

    [Column("fDamageBlunt", TypeName = "float")]
    [Comment("钝器伤害值")]
    [Display(Name = "DamageBlunt")]
    public double DamageBlunt { get; set; } = 0;

    [Column("strChargeProfiles", TypeName = "varchar(24)")]
    [StringLength(24)]
    [Comment("内容物/弹药的代码标号，结合chargeprofiles数据类别使用，可能为逗号分隔如'6,31'")]
    [Display(Name = "ChargeProfiles")]
    public string ChargeProfiles { get; set; } = "";

    [Column("nPenetration")]
    [Comment("穿透等级")]
    [Display(Name = "Penetration")]
    public int Penetration { get; set; } = 0;

    [Column("nType")]
    [Comment("攻击类别：0 近战，1 远程")]
    [Display(Name = "Type")]
    public AttackType Type { get; set; } = AttackType.Melee;

    [Column("strSnd", TypeName = "varchar(30)")]
    [StringLength(30)]
    [Comment("武器分类声音：近战/Punch、爪子/Claws、棍棒类/Club、利刃/Blade、" +
             "长枪/Rifle、短枪/Pistol、激光/Laser、弓箭类/Bow、投掷/Throw、" +
             "勒死/Choke、抓住/Grasp、撕咬/Bite")]
    [Display(Name = "Sound")]
    public string Sound { get; set; } = "";

    [Column("bTransfer", TypeName = "tinyint(1)")]
    [Comment("转移性：攻击能否转化为物品，例如弓箭的攻击能够回收箭枝, 弹药位置转移：0为不转移，1为转移（弓箭、弹弓等弹药会留在敌人身上或掉落地面）")]
    [Display(Name = "Transfer")]
    public bool Transfer { get; set; } = false;

    [Column("vAttackerConditions", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("攻击时的状态，结合conditions数据类别使用，如'211x1.0,NSE:42x1'")]
    [Display(Name = "AttackerConditions")]
    public string AttackerConditions { get; set; } = "";

    [Column("strIMG", TypeName = "varchar(50)")]
    [StringLength(50)]
    [Comment("右下角武器图标文件名")]
    [Display(Name = "Image")]
    public string Image { get; set; } = "";

    [Column("fMorale", TypeName = "float")]
    [Comment("该武器默认为你带来的士气补正，计算公式：(1+士气)*(1+近战/远程士气加成)*武器伤害=实际伤害")]
    [Display(Name = "Morale")]
    public double Morale { get; set; } = 0.25;

    [Column("strWieldPhrase", TypeName = "longtext")]
    [Comment("使用该武器进入战斗时的文字描述")]
    [Display(Name = "WieldPhrase")]
    public string WieldPhrase { get; set; } = "";

    [Column("vAttackPhrases", TypeName = "longtext")]
    [Comment("使用该武器攻击敌人时的文字描述，多个描述用逗号分隔")]
    [Display(Name = "AttackPhrases")]
    public string AttackPhrases { get; set; } = "";
}