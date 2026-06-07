using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("dmcplaces")]
[Comment("底特律城区建筑 - 定义底特律城区内的可互动建筑")]
[Index(nameof(EntityId), nameof(Id), IsUnique =  true, Name = "UID_Key")]
public class DmcPlace : IEntity
{

    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strImg", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("建筑图标名称，如'btn_dmc_diner'")]
    [Display(Name = "Image")]
    public string Image { get; set; } = "";

    [Column("nEncounterID")]
    [Comment("调用的剧情代码ID，结合encounters使用")]
    [Display(Name = "EncounterId")]
    [ReferenceField(typeof(Encounter))]
    public int EncounterId { get; set; } = 1;

    [Column("nX")]
    [Comment("X轴坐标")]
    [Display(Name = "X")]
    public int X { get; set; } = 0;

    [Column("nY")]
    [Comment("Y轴坐标")]
    [Display(Name = "Y")]
    public int Y { get; set; } = 0;
}