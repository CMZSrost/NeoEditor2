using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeoEditor.Core.Model;

/// <summary>
/// Docs/41 追修(C): per-profile EDIT OVERLAY — the basis of multi-profile isolation.
/// Each profile's edits live here (one row per edited column, raw text values), while the
/// game entity tables stay the SHARED BASELINE (written by import/export only). Loading a
/// profile merges baseline + its own overlay; exporting writes the merged view and clears
/// the overlay. Two profiles editing the same entity therefore never overwrite each other.
/// ColumnName = NULL marks an entity-level event: IsNew (created this session) or
/// IsDeleted (removed this session).
/// </summary>
public class ProfileEdit
{
    [Key]
    [Column("Id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("ProfileId")]
    [Required]
    public int ProfileId { get; set; }

    [Column("EntityId", TypeName = "varchar(64)")]
    [StringLength(64)]
    [Required]
    public string EntityId { get; set; } = "";

    /// <summary>Entity type name (e.g. "AttackMode") — needed to rebuild IsNew entities on load.</summary>
    [Column("EntityType", TypeName = "varchar(64)")]
    [StringLength(64)]
    public string? EntityType { get; set; }

    /// <summary>Source mod of the entity — needed to rebuild IsNew entities on load.</summary>
    [Column("ModId")]
    public int ModId { get; set; } = -1;

    /// <summary>Edited column (NULL = entity-level marker: IsNew / IsDeleted).</summary>
    [Column("ColumnName", TypeName = "varchar(64)")]
    [StringLength(64)]
    public string? ColumnName { get; set; }

    /// <summary>New value as canonical raw text (ReferenceText format for references).</summary>
    [Column("RawValue", TypeName = "varchar(4000)")]
    [StringLength(4000)]
    public string? RawValue { get; set; }

    /// <summary>Entity created in this profile (whole-entity marker).</summary>
    [Column("IsNew")]
    public bool IsNew { get; set; }

    /// <summary>Entity deleted in this profile (whole-entity marker).</summary>
    [Column("IsDeleted")]
    public bool IsDeleted { get; set; }

    [Column("UpdatedAt", TypeName = "datetime")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
