using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("itemtypes")]
[Comment("物品详情 - 定义所有可交互物品的完整属性")]
[Index(nameof(EntityId), nameof(Id), IsUnique =  true, Name = "UID_Key")]
public class ItemType : IEntity
{
 [Column("id")] [Comment("代码标号")] public int Id { get; set; }

    [Column("nGroupID")]
    [Comment("物品前ID，与nSubgroupID组合形成完整物品ID")]
    public int GroupId { get; set; }

    [Column("nSubgroupID")]
    [Comment("物品后ID，与nGroupID组合形成完整物品ID")]
    public int SubgroupId { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("物品名称，如'药丸'")]
    public string Name { get; set; } = "";

    [Column("strDesc", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("该物品在游戏中的显示名称，如'白色药片'")]
    public string Description { get; set; } = "";

    [Column("strDescAlt", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("真实描述，常用于需要技能识别的药品和弹药，如'阿莫西林(抗生素)'")]
    public string DescriptionAlt { get; set; } = "";

    [Column("nCondID")]
    [Comment("真实描述的状态前置ID，如53对应'精通医学'")]
    [ReferenceField(typeof(Condition))]
    public string CondId { get; set; } = "1";

    [Column("vImageList", TypeName = "longtext")]
    [Comment("该物品调用的图片文件名")]
    public string ImageList { get; set; } = "";

    [Column("vSpriteList", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("在游戏大地图中显示的人物图片，格式'20=图片名'，20为左手，21右手，22背部")]
    public string SpriteList { get; set; } = "";

    [Column("vImageUsage", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("图片使用位置，6个数字对应不同状态下的图片索引[地上,地上装满,手上,手上装满,物品栏,物品栏装满]")]
    public string ImageUsage { get; set; } = "";

    [Column("fWeight", TypeName = "float")]
    [Comment("该物品的重量")]
    public double Weight { get; set; } = 0;

    [Column("fMonetaryValue", TypeName = "float")]
    [Comment("未识别时的价格")]
    public double MonetaryValue { get; set; } = 0;

    [Column("fMonetaryValueAlt", TypeName = "float")]
    [Comment("识别后的真实价格")]
    public double MonetaryValueAlt { get; set; } = 0;

    [Column("fDurability", TypeName = "float")]
    [Comment("耐久度，设为0则为无限耐久")]
    public double Durability { get; set; } = 1;

    [Column("fDegradePerHour", TypeName = "float")]
    [Comment("每小时耐久消耗")]
    public double DegradePerHour { get; set; } = 0;

    [Column("fEquipDegradePerHour", TypeName = "float")]
    [Comment("装备在身上时每小时的耐久消耗")]
    public double EquipDegradePerHour { get; set; } = 0;

    [Column("fDegradePerUse", TypeName = "float")]
    [Comment("每次使用消耗的耐久")]
    public double DegradePerUse { get; set; } = 0;

    [Column("vDegradeTreasureIDs", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("耐久消耗为0时爆出的零件，分别对应自然损耗和使用损耗")]
    [ReferenceField(typeof(TreasureTable), Separator = ",")]
    public string DegradeTreasureIds { get; set; } = "3,3";

    [Column("aEquipConditions", TypeName = "longtext")]
    [Comment("该物品装备时会为你带来的状态")]
    [Display(Name = "EquipConditions")]
    [ReferenceField(typeof(Condition), Separator = ",", Pattern = "{id}x{mult}")]
    public string EquipConditions { get; set; } = "";

    [Column("aPossessConditions", TypeName = "longtext")]
    [Comment("该物品持有时会为你带来的永久性状态")]
    [Display(Name = "PossessConditions")]
    [ReferenceField(typeof(Condition), Separator = ",", Pattern = "{id}x{mult}")]
    public string PossessConditions { get; set; } = "";

    [Column("aUseConditions", TypeName = "longtext")]
    [Comment("使用该物品会为你带来的状态")]
    [Display(Name = "UseConditions")]
    [ReferenceField(typeof(Condition), Separator = ",", Pattern = "{id}x{mult}")]
    public string UseConditions { get; set; } = "";

    [Column("aCapacities", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("如果该物品是容器，它的容积大小")]
    [Display(Name = "Capacities")]
    public string Capacities { get; set; } = "";

    [Column("vEquipSlots", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("装备插槽，物品能放在身上的位置：20左手，21右手，22背部")]
    [Display(Name = "EquipSlots")]
    public string EquipSlots { get; set; } = "";

    [Column("vUseSlots", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("使用位置，211为直接给自己使用（如吃药）")]
    [Display(Name = "UseSlots")]
    public string UseSlots { get; set; } = "";

    [Column("bSocketLocked", TypeName = "tinyint(1)")]
    [Comment("锁定属性，带此属性的物品无法被玩家移动")]
    [Display(Name = "SocketLocked")]
    public bool SocketLocked { get; set; } = false;

    [Column("vProperties", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("该物品的属性ID列表，用于合成与检定")]
    [Display(Name = "Properties")]
    [ReferenceField(typeof(ItemProp), Separator = ",")]
    public string Properties { get; set; } = "";

    [Column("aContentIDs", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("该物品的空间属性，定义作为容器能存放的物品类型")]
    [Display(Name = "ContentIds")]
    [ReferenceField(typeof(ContainerType), Separator = ",")]
    public string ContentIds { get; set; } = "";

    [Column("nFormatID")]
    [Comment("该物品内部的战利品池ID")]
    [Display(Name = "FormatId")]
    [ReferenceField(typeof(ContainerType))]
    public string FormatId { get; set; } = "3";

    [Column("nTreasureID")]
    [Comment("用于给物品进行大体的分类，结合containertypes使用")]
    [Display(Name = "TreasureId")]
    [ReferenceField(typeof(TreasureTable))]
    public string TreasureId { get; set; } = "3";

    [Column("nComponentID")]
    [Comment("成分ID，结合treasuretable使用,可逆向合成的物品，如果由合成以外的方式获得，拆解时得到的物品ID")]
    [Display(Name = "ComponentId")]
    [ReferenceField(typeof(ItemType), TargetKey = "{GroupId}.{SubgroupId}")]
    public string ComponentId { get; set; }

    [Column("bMirrored", TypeName = "tinyint(1)")]
    [Comment("镜像，专门用于鞋子")]
    [Display(Name = "Mirrored")]
    public bool Mirrored { get; set; } = false;

    [Column("nSlotDepth")]
    [Comment("决定多件衣服等哪件在上面")]
    [Display(Name = "SlotDepth")]
    public int SlotDepth { get; set; } = 0;

    [Column("strChargeProfiles", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("耗电量ID")]
    [Display(Name = "ChargeProfiles")]
    [ReferenceField(typeof(ChargeProfile), Separator = ",")]
    public string ChargeProfiles { get; set; } = "";

    [Column("aAttackModes", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("攻击模式ID列表")]
    [Display(Name = "AttackModes")]
    [ReferenceField(typeof(AttackMode), Separator = ",")]
    public string AttackModes { get; set; } = "";

    [Column("nStackLimit")]
    [Comment("最大堆叠数量")]
    [Display(Name = "StackLimit")]
    public int StackLimit { get; set; } = 1;

    [Column("aSwitchIDs", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("转变的ID，用于电子产品开关机状态切换")]
    [Display(Name = "SwitchIds")]
    [ReferenceField(typeof(ItemType), Separator = ",", TargetKey = "{GroupId}.{SubgroupId}")]
    public string SwitchIds { get; set; } = "";

    [Column("aSounds", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("拿起放下该物品时的声音")]
    [Display(Name = "Sounds")]
    public string Sounds { get; set; } = "cuePickup,cuePutdown";
}