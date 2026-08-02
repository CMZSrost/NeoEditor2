using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("itemtypes")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class ItemType : IEntity
{
 [Column("id")] public int Id { get; set; }

    [Column("nGroupID")]
    
    public int GroupId { get; set; }

    [Column("nSubgroupID")]
    
    public int SubgroupId { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    public string Name { get; set; } = "";

    [Column("strDesc", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    public string Description { get; set; } = "";

    [Column("strDescAlt", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    public string DescriptionAlt { get; set; } = "";

    [Column("nCondID")]
    
    [ReferenceField(typeof(Condition))]
    public ReferenceList<IReferenceEntry> CondId { get; set; } = new();

    [Column("vImageList", TypeName = "longtext")]

    [Display(Name = "ImageList")]
    [ReferenceField(typeof(ImageAsset), Separator = ",", TargetKey = "{FileName}")]
    public ReferenceList<IReferenceEntry> ImageList { get; set; } = new();

    [Column("vSpriteList", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "SpriteList")]
    [ReferenceField(typeof(ImageAsset), Separator = ",", Pattern = "{value}={id}", TargetKey = "{FileName}")]
    public ReferenceList<IReferenceEntry> SpriteList { get; set; } = new();

    [Column("vImageUsage", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    public string ImageUsage { get; set; } = "";

    [Column("fWeight", TypeName = "float")]
    
    public double Weight { get; set; } = 0;

    [Column("fMonetaryValue", TypeName = "float")]
    
    public double MonetaryValue { get; set; } = 0;

    [Column("fMonetaryValueAlt", TypeName = "float")]
    
    public double MonetaryValueAlt { get; set; } = 0;

    [Column("fDurability", TypeName = "float")]
    
    public double Durability { get; set; } = 1;

    [Column("fDegradePerHour", TypeName = "float")]
    
    public double DegradePerHour { get; set; } = 0;

    [Column("fEquipDegradePerHour", TypeName = "float")]
    
    public double EquipDegradePerHour { get; set; } = 0;

    [Column("fDegradePerUse", TypeName = "float")]
    
    public double DegradePerUse { get; set; } = 0;

    [Column("vDegradeTreasureIDs", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [ReferenceField(typeof(TreasureTable), Separator = ",")]
    public ReferenceList<IReferenceEntry> DegradeTreasureIds { get; set; } = new();

    [Column("aEquipConditions", TypeName = "longtext")]
    
    [Display(Name = "EquipConditions")]
    [ReferenceField(typeof(Condition), Separator = ",", Pattern = "{value}={id}")]
    public ReferenceList<IReferenceEntry> EquipConditions { get; set; } = new();

    [Column("aPossessConditions", TypeName = "longtext")]

    [Display(Name = "PossessConditions")]
    [ReferenceField(typeof(Condition), Separator = ",", Pattern = "{value}={id}")]
    public ReferenceList<IReferenceEntry> PossessConditions { get; set; } = new();

    [Column("aUseConditions", TypeName = "longtext")]

    [Display(Name = "UseConditions")]
    [ReferenceField(typeof(Condition), Separator = ",", Pattern = "{value}={id}")]
    public ReferenceList<IReferenceEntry> UseConditions { get; set; } = new();

    [Column("aCapacities", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Capacities")]
    public string Capacities { get; set; } = "";

    [Column("vEquipSlots", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "EquipSlots")]
    public string EquipSlots { get; set; } = "";

    [Column("vUseSlots", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "UseSlots")]
    public string UseSlots { get; set; } = "";

    [Column("bSocketLocked", TypeName = "tinyint(1)")]
    
    [Display(Name = "SocketLocked")]
    public bool SocketLocked { get; set; } = false;

    [Column("vProperties", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Properties")]
    [ReferenceField(typeof(ItemProp), Separator = ",")]
    public ReferenceList<IReferenceEntry> Properties { get; set; } = new();

    [Column("aContentIDs", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "ContentIds")]
    [ReferenceField(typeof(ContainerType), Separator = ",")]
    public ReferenceList<IReferenceEntry> ContentIds { get; set; } = new();

    [Column("nFormatID")]

    [Display(Name = "FormatId")]
    [ReferenceField(typeof(ContainerType))]
    public ReferenceList<IReferenceEntry> FormatId { get; set; } = new();

    [Column("nTreasureID")]

    [Display(Name = "TreasureId")]
    [ReferenceField(typeof(TreasureTable))]
    public ReferenceList<IReferenceEntry> TreasureId { get; set; } = new();

    [Column("nComponentID")]

    [Display(Name = "ComponentId")]
    [ReferenceField(typeof(TreasureTable))]
    public ReferenceList<IReferenceEntry> ComponentId { get; set; } = new();

    [Column("bMirrored", TypeName = "tinyint(1)")]
    
    [Display(Name = "Mirrored")]
    public bool Mirrored { get; set; } = false;

    [Column("nSlotDepth")]
    
    [Display(Name = "SlotDepth")]
    public int SlotDepth { get; set; } = 0;

    [Column("strChargeProfiles", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "ChargeProfiles")]
    [ReferenceField(typeof(ChargeProfile), Separator = ",")]
    public ReferenceList<IReferenceEntry> ChargeProfiles { get; set; } = new();

    [Column("aAttackModes", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "AttackModes")]
    [ReferenceField(typeof(AttackMode), Separator = ",", Pattern = "{value}={id}")]
    public ReferenceList<IReferenceEntry> AttackModes { get; set; } = new();

    [Column("nStackLimit")]
    
    [Display(Name = "StackLimit")]
    public int StackLimit { get; set; } = 1;

    [Column("aSwitchIDs", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "SwitchIds")]
    [ReferenceField(typeof(ItemType), Separator = ",", Pattern = "{value}={id}", TargetKey = "{GroupId}.{SubgroupId}")]
    public ReferenceList<IReferenceEntry> SwitchIds { get; set; } = new();

    [Column("aSounds", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Sounds")]
    public string Sounds { get; set; } = "cuePickup,cuePutdown";
}