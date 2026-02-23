using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("barterhexes")]
[Comment("交易区块 - 定义地图上可以进行交易的商店位置")]
public class BarterHex
{
    [Display(Name = "BarterHex_ModId")] public int ModId { get; set; }

    [Key]
    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "BarterHex_Id")]
    public int Id { get; set; }

    [Column("nX")]
    [Comment("X轴坐标")]
    [Display(Name = "BarterHex_X")]
    public int X { get; set; } = 0;

    [Column("nY")]
    [Comment("Y轴坐标")]
    [Display(Name = "BarterHex_Y")]
    public int Y { get; set; } = 0;

    [Column("bBuys", TypeName = "tinyint(1)")]
    [Comment("是否可以购买玩家的物品：0为不可以，1为可以")]
    [Display(Name = "BarterHex_Buys")]
    public bool Buys { get; set; } = false;

    [Column("nRestockTreasureID")]
    [Comment("引用的战利品数据，结合treasuretable使用。注意：底特律C商店是个例外，使用3号战利品池")]
    [Display(Name = "BarterHex_RestockTreasureId")]
    public int RestockTreasureId { get; set; } = 3;
}