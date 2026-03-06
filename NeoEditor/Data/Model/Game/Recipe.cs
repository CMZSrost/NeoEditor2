using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("recipes")]
[Comment("合成表 - 定义所有可合成的物品配方")]
public class Recipe
{
    [Display(Name = "ModId")] public int ModId { get; set; }

    [Key]
    [Column("nID")]
    [Comment("代码标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("配方名称，如'中等营火（点燃）'")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strSecretName", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("配方的隐藏名称，用于区分人肉和动物肉、水的毒性等")]
    [Display(Name = "SecretName")]
    public string SecretName { get; set; } = "";

    [Column("strTools", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("合成需要的工具，格式如'1x1'")]
    [Display(Name = "Tools")]
    public string Tools { get; set; } = "";

    [Column("strConsumed", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("合成会消耗/损耗掉的物品，格式如'1x2+1x3'")]
    [Display(Name = "Consumed")]
    public string Consumed { get; set; } = "";

    [Column("strDestroyed", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("摧毁的物品，用于熄灭火把等")]
    [Display(Name = "Destroyed")]
    public string Destroyed { get; set; } = "";

    [Column("nTreasureID")]
    [Comment("调用的战利品池ID，即合成出的物品")]
    [Display(Name = "TreasureId")]
    public int TreasureId { get; set; }

    [Column("fHours", TypeName = "float")]
    [Comment("合成需要消耗的行动点数")]
    [Display(Name = "Hours")]
    public double Hours { get; set; }

    [Column("nReverse")]
    [Comment("是否可以逆向工程：0不可逆，1可逆，2等")]
    [Display(Name = "Reverse")]
    public int Reverse { get; set; } = 0;

    [Column("nHiddenID")]
    [Comment("是否为隐藏配方，需要捡纸片解锁")]
    [Display(Name = "HiddenId")]
    public int HiddenId { get; set; } = 0;

    [Column("bIdentify", TypeName = "tinyint(1)")]
    [Comment("是否能被鉴别，用于配合隐藏名称")]
    [Display(Name = "Identify")]
    public bool Identify { get; set; } = false;

    [Column("bTransferComponents", TypeName = "tinyint(1)")]
    [Comment("未知参数")]
    [Display(Name = "TransferComponents")]
    public bool TransferComponents { get; set; } = false;

    [Column("vAlsoTry", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("使用其他不同配方但相同成品的配方ID")]
    [Display(Name = "AlsoTry")]
    public string AlsoTry { get; set; } = "";

    [Column("nTempTreasureID")]
    [Comment("合成时在成品栏以虚影显示的合成结果ID")]
    [Display(Name = "TempTreasureId")]
    public int TempTreasureId { get; set; } = 3;

    [Column("bDegradeOutput", TypeName = "tinyint(1)")]
    [Comment("合成出的物品耐久是否和材料耐久关联：1有关，0无关")]
    [Display(Name = "DegradeOutput")]
    public bool DegradeOutput { get; set; } = false;

    [Column("strType", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("配方类型，如'工具'、'食物'、'医务'、'武器'、'载具'、'杂项'")]
    [Display(Name = "Type")]
    public string Type { get; set; } = "";

    [Column("bScrap", TypeName = "tinyint(1)")]
    [Comment("是否可分解")]
    [Display(Name = "Scrap")]
    public bool Scrap { get; set; } = true;
}