using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("chargeprofiles")]
[Comment("内容物/弹药种类 - 定义物品的消耗方式，结合attackmodes中的ChargeProfiles使用")]
public class ChargeProfile
{
    [Display(Name = "ChargeProfile_ModId")]
    public int ModId { get; set; }

    [Key]
    [Column("nID")]
    [Comment("代码标号")]
    [Display(Name = "ChargeProfile_Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("物品名称，如'纳米医疗箱电量'")]
    [Display(Name = "ChargeProfile_Name")]
    public string Name { get; set; } = "";

    [Column("strItemID", TypeName = "varchar(12)")]
    [StringLength(12)]
    [Comment("物品ID，如'10.3'")]
    [Display(Name = "ChargeProfile_ItemId")]
    public string ItemId { get; set; } = "";

    [Column("fPerUse", TypeName = "float")]
    [Comment("每次使用所消耗的数量")]
    [Display(Name = "ChargeProfile_PerUse")]
    public double PerUse { get; set; } = 0;

    [Column("fPerHour", TypeName = "float")]
    [Comment("每小时所消耗数量，基本用于电器的电力消耗")]
    [Display(Name = "ChargeProfile_PerHour")]
    public double PerHour { get; set; } = 0;

    [Column("fPerHourEquipped", TypeName = "float")]
    [Comment("装备在身上时每小时的消耗耐久，仅用于XM54过滤芯片")]
    [Display(Name = "ChargeProfile_PerHourEquipped")]
    public double PerHourEquipped { get; set; } = 0;

    [Column("fPerHex", TypeName = "float")]
    [Comment("每移动一格所消耗的数量")]
    [Display(Name = "ChargeProfile_PerHex")]
    public double PerHex { get; set; } = 0;

    [Column("bDegrade", TypeName = "tinyint(1)")]
    [Comment("是否会降解")]
    [Display(Name = "ChargeProfile_Degrade")]
    public bool Degrade { get; set; } = false;
}