using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("encounters")]
[Comment("剧情代码 - 定义所有可触发的游戏剧情")]
public class Encounter
{
    [Display(Name = "Encounter_ModId")] public int ModId { get; set; }

    [Key]
    [Column("id")]
    [Comment("代码标号")]
    [Display(Name = "Encounter_Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("该剧情的名称，如'魔迦怨灵出现'")]
    [Display(Name = "Encounter_Name")]
    public string Name { get; set; } = "";

    [Column("strDesc", TypeName = "longtext")]
    [Comment("剧情文本描述")]
    [Display(Name = "Encounter_Description")]
    public string Description { get; set; } = "";

    [Column("strImg", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("该剧情调用的图片文件名")]
    [Display(Name = "Encounter_Image")]
    public string Image { get; set; } = "EncBlank.png";

    [Column("nTreasureID")]
    [Comment("剧情发生时在玩家脚下生成的战利品池ID")]
    [Display(Name = "Encounter_TreasureId")]
    public int TreasureId { get; set; } = 3;

    [Column("nRemoveTreasureID")]
    [Comment("剧情发生时在玩家脚下移除的战利品池ID")]
    [Display(Name = "Encounter_RemoveTreasureId")]
    public int RemoveTreasureId { get; set; } = 3;

    [Column("aConditions", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("该剧情的附带状态ID列表，逗号分隔")]
    [Display(Name = "Encounter_Conditions")]
    public string Conditions { get; set; } = "1";

    [Column("aPreConditions", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("出现该剧情时必要的前置状态，负数表示不可拥有该状态")]
    [Display(Name = "Encounter_PreConditions")]
    public string PreConditions { get; set; } = "";

    [Column("fPrice", TypeName = "float")]
    [Comment("该剧情是否会让你的资产变动")]
    [Display(Name = "Encounter_Price")]
    public double Price { get; set; } = 0;

    [Column("aResponses", TypeName = "longtext")]
    [Comment("玩家在经历该剧情时的回应选项，格式如'=15x0.083x0x0x0,=16x0.083x0x0x0'")]
    [Display(Name = "Encounter_Responses")]
    public string Responses { get; set; } = "";

    [Column("aMinimapHexes", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("小地图上显示的点坐标")]
    [Display(Name = "Encounter_MinimapHexes")]
    public string MinimapHexes { get; set; } = "";

    [Column("bRemoveCreatures", TypeName = "tinyint(1)")]
    [Comment("该剧情发生时是否需要移除生物")]
    [Display(Name = "Encounter_RemoveCreatures")]
    public bool RemoveCreatures { get; set; } = false;

    [Column("bRemoveUsed", TypeName = "tinyint(1)")]
    [Comment("该剧情是否会移除你的物品")]
    [Display(Name = "Encounter_RemoveUsed")]
    public bool RemoveUsed { get; set; } = false;

    [Column("nItemsID")]
    [Comment("发生该剧情时产生的物品ID")]
    [Display(Name = "Encounter_ItemsId")]
    public int ItemsId { get; set; } = 3;

    [Column("nCreatureID")]
    [Comment("该剧情发生时需要增加的生物ID")]
    [Display(Name = "Encounter_CreatureId")]
    public int CreatureId { get; set; } = 0;

    [Column("ptCreatureHex", TypeName = "varchar(9)")]
    [StringLength(9)]
    [Comment("生物出现位置坐标，格式如'0,0'")]
    [Display(Name = "Encounter_CreatureHex")]
    public string CreatureHex { get; set; } = "0,0";

    [Column("ptTeleport", TypeName = "varchar(9)")]
    [StringLength(9)]
    [Comment("发生该剧情时将玩家传送到的位置，'0,0'为不传送")]
    [Display(Name = "Encounter_Teleport")]
    public string Teleport { get; set; } = "0,0";

    [Column("ptEditor", TypeName = "varchar(24)")]
    [StringLength(24)]
    [Comment("未知参数，编辑器相关")]
    [Display(Name = "Encounter_Editor")]
    public string Editor { get; set; } = "0,0";

    [Column("nType", TypeName = "tinyint(1)")]
    [Comment("剧情类型：0普通剧情，1搜刮剧情")]
    [Display(Name = "Encounter_Type")]
    public EncounterType Type { get; set; } = EncounterType.Normal;

    [Column("fLootChance", TypeName = "float")]
    [Comment("成功搜刮到物品的几率")]
    [Display(Name = "Encounter_LootChance")]
    public double LootChance { get; set; } = 0;

    [Column("fAccidentChance", TypeName = "float")]
    [Comment("发生意外的几率（如破楼倒塌）")]
    [Display(Name = "Encounter_AccidentChance")]
    public double AccidentChance { get; set; } = 0;

    [Column("fCreatureChance", TypeName = "float")]
    [Comment("未知参数")]
    [Display(Name = "Encounter_CreatureChance")]
    public double CreatureChance { get; set; } = 0;

    [Column("vAccidents", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("出现意外时发生的事件ID（调用encounters）")]
    [Display(Name = "Encounter_Accidents")]
    public string Accidents { get; set; } = "1";

    [Column("vLoot", TypeName = "varchar(255)")]
    [StringLength(255)]
    [Comment("搜刮成功时的战利品种类ID")]
    [Display(Name = "Encounter_Loot")]
    public string Loot { get; set; } = "3";
}