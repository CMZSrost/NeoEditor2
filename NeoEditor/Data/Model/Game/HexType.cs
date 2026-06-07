using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("hextypes")]
[Comment("地块类型 - 定义地图上每种格子的属性和效果")]
[Index(nameof(EntityId), nameof(Id), IsUnique = true, Name = "UID_Key")]
public class HexType : IEntity
{
    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("地块名称，如'海洋'")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strDesc", TypeName = "longtext")]
    [Comment("地块在游戏中显示的名称，如'深水'")]
    [Display(Name = "Description")]
    public string Description { get; set; } = "";

    [Column("nTerrainCost")]
    [Comment("在该地块上消耗的行动点数")]
    [Display(Name = "TerrainCost")]
    public int TerrainCost { get; set; }

    [Column("nVizLimiter")]
    [Comment("在该地块上减少的视距")]
    [Display(Name = "VizLimiter")]
    public int VizLimiter { get; set; }

    [Column("nVizIncrease")]
    [Comment("在该地块上增加的视距")]
    [Display(Name = "VizIncrease")]
    public int VizIncrease { get; set; }

    [Column("nTreasureID")]
    [Comment("在该地块上生成的战利品池ID")]
    [Display(Name = "TreasureId")]
    [ReferenceField(typeof(TreasureTable))]
    public string TreasureId { get; set; } = "3";

    [Column("bPassable", TypeName = "tinyint(1)")]
    [Comment("是否可以移动到该地形：0不可通行，1可通行")]
    [Display(Name = "Passable")]
    public PassableType Passable { get; set; } = PassableType.Passable;

    [Column("nScavengeInitialID")]
    [Comment("初次搜刮该地形时调用的战利品池ID")]
    [Display(Name = "ScavengeInitialId")]
    [ReferenceField(typeof(TreasureTable))]
    public string ScavengeInitialId { get; set; } = "3";

    [Column("nScavengeItemsIDPerHour")]
    [Comment("多次搜刮该地形时调用的战利品池ID")]
    [Display(Name = "ScavengeItemsIdPerHour")]
    [ReferenceField(typeof(TreasureTable))]
    public string ScavengeItemsIdPerHour { get; set; } = "25";

    [Column("nCampItems")]
    [Comment("营地类型ID")]
    [Display(Name = "CampItems")]
    public int CampItems { get; set; } = 5;

    [Column("vLightLevels", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("亮度等级，对应：凌晨,上午,中午,下午,傍晚,半夜，6个浮点数")]
    [Display(Name = "LightLevels")]
    public string LightLevels { get; set; } = "0.57,1.0,0.57,0.15";

    [Column("nDefaultCampID")]
    [Comment("默认营地的代码标号，结合treasuretable使用")]
    [Display(Name = "DefaultCampId")]
    [ReferenceField(typeof(CampType))]
    public int DefaultCampId { get; set; } = 517;

    [Column("nMinRange")]
    [Comment("遇到生物时，玩家距离此生物的最小距离")]
    [Display(Name = "MinRange")]
    public int MinRange { get; set; } = 3;

    [Column("nMaxRange")]
    [Comment("遇到生物时，玩家距离此生物的最大距离")]
    [Display(Name = "MaxRange")]
    public int MaxRange { get; set; } = 6;

    [Column("vCondIDs", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("进入该地块会为玩家带来的状态ID")]
    [Display(Name = "ConditionIds")]
    [ReferenceField(typeof(Condition), Separator = ",")]
    public string ConditionIds { get; set; } = "";
}