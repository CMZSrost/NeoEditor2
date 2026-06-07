using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("encounters")]
[Comment("剧情代码 - 定义所有可触发的游戏剧情")]
[Index(nameof(EntityId), nameof(Id), IsUnique =  true, Name = "UID_Key")]
public class Encounter : IEntity
{

    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("该剧情的名称，如'魔迦怨灵出现'")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strDesc", TypeName = "longtext")]
    [Comment("剧情文本描述")]
    [Display(Name = "Description")]
    public string Description { get; set; } = "";

    [Column("strImg", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("该剧情调用的图片文件名")]
    [Display(Name = "Image")]
    public string Image { get; set; } = "EncBlank.png";

    [Column("nTreasureID")]
    [Comment("剧情发生时在玩家脚下生成的战利品池ID")]
    [Display(Name = "TreasureId")]
    [ReferenceField(typeof(TreasureTable))]
    public string TreasureId { get; set; } = "3";

    [Column("nRemoveTreasureID")]
    [Comment("剧情发生时在玩家脚下移除的战利品池ID")]
    [Display(Name = "RemoveTreasureId")]
    [ReferenceField(typeof(TreasureTable))]
    public string RemoveTreasureId { get; set; } = "3";

    [Column("aConditions", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("该剧情的附带状态ID列表，逗号分隔")]
    [Display(Name = "Conditions")]
    [ReferenceField(typeof(Condition), Separator = ",")]
    public string Conditions { get; set; } = "1";

    [Column("aPreConditions", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("出现该剧情时必要的前置状态，负数表示不可拥有该状态")]
    [Display(Name = "PreConditions")]
    [ReferenceField(typeof(Condition), Separator = ",")]
    public string PreConditions { get; set; } = "";

    [Column("fPrice", TypeName = "float")]
    [Comment("该剧情是否会让你的资产变动")]
    [Display(Name = "Price")]
    public double Price { get; set; } = 0;

    [Column("aResponses", TypeName = "longtext")]
    [Comment("玩家在经历该剧情时的回应选项，格式如'=15x0.083x0x0x0,=16x0.083x0x0x0'")]
    [Display(Name = "Responses")]
    public string Responses { get; set; } = "";

    [Column("aMinimapHexes", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("小地图上显示的点坐标")]
    [Display(Name = "MinimapHexes")]
    public string MinimapHexes { get; set; } = "";

    [Column("bRemoveCreatures", TypeName = "tinyint(1)")]
    [Comment("该剧情发生时是否需要移除生物")]
    [Display(Name = "RemoveCreatures")]
    public bool RemoveCreatures { get; set; } = false;

    [Column("bRemoveUsed", TypeName = "tinyint(1)")]
    [Comment("该剧情是否会移除你的物品")]
    [Display(Name = "RemoveUsed")]
    public bool RemoveUsed { get; set; } = false;

    [Column("nItemsID")]
    [Comment("发生该剧情时产生的物品ID")]
    [Display(Name = "ItemsId")]
    [ReferenceField(typeof(ItemType), TargetKey = "{GroupId}.{SubgroupId}")]
    public string ItemsId { get; set; } = "3";

    [Column("nCreatureID")]
    [Comment("该剧情发生时需要增加的生物ID")]
    [Display(Name = "CreatureId")]
    [ReferenceField(typeof(Creature))]
    public string CreatureId { get; set; } = "0";

    [Column("ptCreatureHex", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("生物出现位置坐标，格式如'0,0'")]
    [Display(Name = "CreatureHex")]
    public string CreatureHex { get; set; } = "0,0";

    [Column("ptTeleport", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("发生该剧情时将玩家传送到的位置，'0,0'为不传送")]
    [Display(Name = "Teleport")]
    public string Teleport { get; set; } = "0,0";

    [Column("ptEditor", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("未知参数，编辑器相关")]
    [Display(Name = "Editor")]
    public string Editor { get; set; } = "0,0";

    [Column("nType", TypeName = "tinyint(1)")]
    [Comment("剧情类型：0普通剧情，1搜刮剧情")]
    [Display(Name = "Type")]
    public EncounterType Type { get; set; } = EncounterType.Normal;

    [Column("fLootChance", TypeName = "float")]
    [Comment("成功搜刮到物品的几率")]
    [Display(Name = "LootChance")]
    public double LootChance { get; set; } = 0;

    [Column("fAccidentChance", TypeName = "float")]
    [Comment("发生意外的几率（如破楼倒塌）")]
    [Display(Name = "AccidentChance")]
    public double AccidentChance { get; set; } = 0;

    [Column("fCreatureChance", TypeName = "float")]
    [Comment("未知参数")]
    [Display(Name = "CreatureChance")]
    public double CreatureChance { get; set; } = 0;

    [Column("vAccidents", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("出现意外时发生的事件ID（调用encounters）")]
    [Display(Name = "Accidents")]
    [ReferenceField(typeof(Encounter), Separator = ",")]
    public string Accidents { get; set; } = "1";

    [Column("vLoot", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("搜刮成功时的战利品种类ID")]
    [Display(Name = "Loot")]
    [ReferenceField(typeof(TreasureTable))]
    public string Loot { get; set; } = "3";
}