using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("camptypes")]
[Comment("营地类型 - 定义各种营地的属性和效果")]
[Index(nameof(EntityId), nameof(Id), IsUnique =  true, Name = "UID_Key")]
public class CampType : IEntity
{

    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strDesc", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("营地的描述，如'区域的暗处'")]
    [Display(Name = "Description")]
    public string Description { get; set; } = "";

    [Column("vImageList", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("营地调用的图片文件名")]
    [Display(Name = "ImageList")]
    public string ImageList { get; set; } = "ItmScavengeGrass01.png";

    [Column("aCapacities", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("营地大小，格式如'34x26'")]
    [Display(Name = "Capacities")]
    public string Capacities { get; set; } = "30x30";

    [Column("nTreasureID")]
    [Comment("该营地的战利品池ID")]
    [Display(Name = "TreasureId")]
    public string TreasureId { get; set; } = "3";

    [Column("m_fAlertness", TypeName = "float")]
    [Comment("营地的默认警戒值（百分比）")]
    [Display(Name = "Alertness")]
    public double Alertness { get; set; } = 0;

    [Column("m_fVisibility", TypeName = "float")]
    [Comment("营地的默认可见值（百分比），-0.05表示-5%")]
    [Display(Name = "Visibility")]
    public double Visibility { get; set; } = -0.05;

    [Column("WetTempAdjustMod", TypeName = "float")]
    [Comment("营地的默认温度调节")]
    [Display(Name = "WetTempAdjustMod")]
    public double WetTempAdjustMod { get; set; } = 0;

    [Column("m_fHealPerHourMod", TypeName = "float")]
    [Comment("营地默认每小时带来的恢复效果（百分比）")]
    [Display(Name = "HealPerHourMod")]
    public double HealPerHourMod { get; set; } = 0;

    [Column("fSleepQuality", TypeName = "float")]
    [Comment("营地默认为你带来的睡眠质量（百分比），-0.26表示-26%")]
    [Display(Name = "SleepQuality")]
    public double SleepQuality { get; set; } = 0;
}