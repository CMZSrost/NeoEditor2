using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeoEditor.Data.Model;

[Table("command_log")]
public class CommandLog
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

    [Column("Sequence")]
    [Required]
    public int Sequence { get; set; }

    [Column("CommandType", TypeName = "varchar(50)")]
    [StringLength(50)]
    [Required]
    public string CommandType { get; set; } = ""; // EditCell / AddEntity / DeleteEntity / BatchEdit

    [Column("SerializedData", TypeName = "longtext")]
    [Required]
    public string SerializedData { get; set; } = ""; // JSON

    [Column("IsUnsaved")]
    public bool IsUnsaved { get; set; } = true; // true = part of unsaveCommandList

    [Column("CreatedAt", TypeName = "datetime")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
