using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NeoEditor.Core.Abstractions;
using NeoEditor.Helper;

namespace NeoEditor.Data.Model.Game;

[Table("datafiles")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class DataFile : IEntity
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

    [Column("fValue", TypeName = "float")]
    
    [Display(Name = "Value")]
    public double Value { get; set; } = 0;

    [Column("strImg", TypeName = "varchar(1000)")]
    [StringLength(1000)]

    [Display(Name = "Image")]
    [ReferenceField(typeof(ImageAsset), TargetKey = "{FileName}")]
    public ReferenceList<IReferenceEntry> Image { get; set; } = new();
}