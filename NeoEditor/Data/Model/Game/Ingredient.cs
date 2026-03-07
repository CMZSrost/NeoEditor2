using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

[Table("ingredients")]
[Comment("合成项 - 定义合成所需的各种材料类型")]
[Index(nameof(EntityId), nameof(Id), IsUnique =  true, Name = "UID_Key")]
public class Ingredient : IEntity
{

    [Column("nID")]
    [Comment("合成项目标号，用于合成表")]
    [Display(Name = "Id")]
    public int Id { get; set; }

    [Column("strName", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("合成项名称，如'火源'")]
    [Display(Name = "Name")]
    public string Name { get; set; } = "";

    [Column("strRequiredProps", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("合成项所需的属性ID，可用'&'表示'与'关系")]
    [Display(Name = "RequiredProps")]
    public string RequiredProps { get; set; } = "";

    [Column("strForbidProps", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    [Comment("合成项不可拥有的属性ID")]
    [Display(Name = "ForbidProps")]
    public string ForbidProps { get; set; } = "";
}