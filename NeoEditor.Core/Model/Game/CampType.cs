using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("camptypes")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class CampType : IEntity
{

    [Column("id")]

    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strDesc", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "Description")]
    public string Description { get; set; } = "";

    [Column("vImageList", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "ImageList")]
    public string ImageList { get; set; } = "ItmScavengeGrass01.png";

    [Column("aCapacities", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "Capacities")]
    public string Capacities { get; set; } = "30x30";

    [Column("nTreasureID")]

    [Display(Name = "TreasureId")]
    [ReferenceField(typeof(TreasureTable))]
    public ReferenceList<IReferenceEntry> TreasureId { get; set; } = new();

    [Column("m_fAlertness", TypeName = "float")]
    
    [Display(Name = "Alertness")]
    public double Alertness { get; set; } = 0;

    [Column("m_fVisibility", TypeName = "float")]
    
    [Display(Name = "Visibility")]
    public double Visibility { get; set; } = -0.05;

    [Column("WetTempAdjustMod", TypeName = "float")]
    
    [Display(Name = "WetTempAdjustMod")]
    public double WetTempAdjustMod { get; set; } = 0;

    [Column("m_fHealPerHourMod", TypeName = "float")]
    
    [Display(Name = "HealPerHourMod")]
    public double HealPerHourMod { get; set; } = 0;

    [Column("fSleepQuality", TypeName = "float")]
    
    [Display(Name = "SleepQuality")]
    public double SleepQuality { get; set; } = 0;
}