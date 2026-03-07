using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NeoEditor.Data.Model.Game;

public class IEntity
{
    [Display(Name = "ModId")]
    [Column("mod_id")]
    public int ModId { get; set; } // 编排时使用，表示该数据来源于哪个Mod

    [Display(Name = "FilePath")]
    [Column("file_path", TypeName = "varchar(1000)")]
    [StringLength(1000)]
    public string FilePath { get; set; } // 编排时使用，表示该数据来源于哪个Xml文件

    [Key]
    [Display(Name = "EntityId")]
    [Column("entity_id", TypeName = "varchar(64)")]
    [StringLength(64)]
    public string EntityId { get; set; } // 编排时使用，作为实体的唯一标识符，通常对应数据库中的主键
}