using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeoEditor.Data.Model;

[Table("workspace_snapshot")]
public class WorkspaceSnapshot
{
    [Key]
    [Column("Id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("TargetType", TypeName = "varchar(20)")]
    [StringLength(20)]
    [Required]
    public string TargetType { get; set; } = ""; // "mod" or "profile"

    [Column("TargetId")]
    [Required]
    public int TargetId { get; set; } // ModId or ProfileId

    [Column("LastCommandSequence")]
    public int LastCommandSequence { get; set; }

    [Column("CreatedAt", TypeName = "datetime")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
