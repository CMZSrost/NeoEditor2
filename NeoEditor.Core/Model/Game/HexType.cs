using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("hextypes")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class HexType : IEntity
{
    [Column("id")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strDesc", TypeName = "longtext")]
    
    [Display(Name = "Description")]
    public string Description { get; set; } = "";

    [Column("nTerrainCost")]
    
    [Display(Name = "TerrainCost")]
    public int TerrainCost { get; set; }

    [Column("nVizLimiter")]
    
    [Display(Name = "VizLimiter")]
    public int VizLimiter { get; set; }

    [Column("nVizIncrease")]
    
    [Display(Name = "VizIncrease")]
    public int VizIncrease { get; set; }

    [Column("nTreasureID")]
    
    [Display(Name = "TreasureId")]
    [ReferenceField(typeof(TreasureTable))]
    public ReferenceList<IReferenceEntry> TreasureId { get; set; } = new();

    [Column("bPassable", TypeName = "tinyint(1)")]

    [Display(Name = "Passable")]
    public PassableType Passable { get; set; } = PassableType.Passable;

    [Column("nScavengeInitialID")]

    [Display(Name = "ScavengeInitialId")]
    [ReferenceField(typeof(TreasureTable))]
    public ReferenceList<IReferenceEntry> ScavengeInitialId { get; set; } = new();

    [Column("nScavengeItemsIDPerHour")]

    [Display(Name = "ScavengeItemsIdPerHour")]
    [ReferenceField(typeof(TreasureTable))]
    public ReferenceList<IReferenceEntry> ScavengeItemsIdPerHour { get; set; } = new();

    [Column("nCampItems")]

    [Display(Name = "CampItems")]
    public int CampItems { get; set; } = 5;

    [Column("vLightLevels", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "LightLevels")]
    public string LightLevels { get; set; } = "0.57,1.0,0.57,0.15";

    [Column("nDefaultCampID")]

    [Display(Name = "DefaultCampId")]
    [ReferenceField(typeof(CampType))]
    public ReferenceList<IReferenceEntry> DefaultCampId { get; set; } = new();

    [Column("nMinRange")]
    
    [Display(Name = "MinRange")]
    public int MinRange { get; set; } = 3;

    [Column("nMaxRange")]
    
    [Display(Name = "MaxRange")]
    public int MaxRange { get; set; } = 6;

    [Column("vCondIDs", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    
    [Display(Name = "ConditionIds")]
    [ReferenceField(typeof(Condition), Separator = ",")]
    public ReferenceList<IReferenceEntry> ConditionIds { get; set; } = new();
}