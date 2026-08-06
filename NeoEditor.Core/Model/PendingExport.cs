using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeoEditor.Core.Model;

/// <summary>
/// Docs/41 增补: the persisted "edited but NOT yet exported to game XML" set.
/// Auto-save persists edits to game.db and clears the WAL, but the user's changes are
/// still not in the game until Save &amp; Export — this table remembers that state so
/// restart restores the ⚠ badge and row/cell highlights (EditStore is session-scoped).
/// Written on every auto/quick save (upsert per entity), cleared on Save &amp; Export.
/// </summary>
public class PendingExport
{
    [Key]
    [Column("Id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Column("ModId")]
    [Required]
    public int ModId { get; set; }

    [Column("EntityId", TypeName = "varchar(64)")]
    [StringLength(64)]
    [Required]
    public string EntityId { get; set; } = "";

    /// <summary>Edited column of the entity for this marker (NULL = entity-level marker,
    /// e.g. rows created this session, or legacy rows written before per-column tracking).
    /// One row per edited column so field-level highlights survive a restart.</summary>
    [Column("ColumnName", TypeName = "varchar(64)")]
    [StringLength(64)]
    public string? ColumnName { get; set; }

    [Column("IsNew")]
    public bool IsNew { get; set; } // created this session (green) vs modified (yellow)

    [Column("UpdatedAt", TypeName = "datetime")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
