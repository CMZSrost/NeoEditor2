using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeoEditor.Data.Model.Game;

[Table("headlines")]

[UIDKey(nameof(EntityId), nameof(Id))]
public class Headline : IEntity
{

    [Column("id")]
    
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strHeadline", TypeName = "longtext")]
    
    [Display(Name = "HeadlineText")]
    public string HeadlineText { get; set; } = "";

    public override string Subject => $"News #{Id}";
}