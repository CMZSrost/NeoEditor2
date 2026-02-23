using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("dmcplaces")]
[Comment("底特律城区建筑 - 定义底特律城区内的可互动建筑")]
public class DmcPlace
{
    [Display(Name = "DmcPlace_ModId")] public int ModId { get; set; }

    [Key]
    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "DmcPlace_Id")]
    public int Id { get; set; }

    [Column("strImg", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("建筑图标名称，如'btn_dmc_diner'")]
    [Display(Name = "DmcPlace_Image")]
    public string Image { get; set; } = "";

    [Column("nEncounterID")]
    [Comment("调用的剧情代码ID，结合encounters使用")]
    [Display(Name = "DmcPlace_EncounterId")]
    public int EncounterId { get; set; } = 1;

    [Column("nX")]
    [Comment("X轴坐标")]
    [Display(Name = "DmcPlace_X")]
    public int X { get; set; } = 0;

    [Column("nY")]
    [Comment("Y轴坐标")]
    [Display(Name = "DmcPlace_Y")]
    public int Y { get; set; } = 0;
}