using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
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

    /// <summary>Merge-view auto-incremented ID. Returns 0 outside merge view.</summary>
    [NotMapped]
    public int MergedId => Helper.GenericDataGridHelper.GetEntityMergedId(this);

    /// <summary>Brief human-readable description of this entity (e.g. "Water Bottle").</summary>
    [NotMapped]
    public virtual string Subject
    {
        get
        {
            var type = GetType();
            // Try common name-like properties first
            foreach (var name in new[] { "strName", "Name", "strLabel", "strTitle", "PropertyName", "strPropertyName" })
            {
                var prop = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (prop?.GetValue(this) is string s && s.Length > 0)
                    return s;
            }
            // Fallback: key column value
            var keyProp = ResolveKeyProperty();
            if (keyProp is not null)
            {
                var keyVal = keyProp.GetValue(this);
                if (keyVal is not null) return $"[{type.Name}] {keyVal}";
            }
            return type.Name;
        }
    }

    private PropertyInfo? ResolveKeyProperty()
    {
        var indexAttr = GetType().GetCustomAttribute<IndexAttribute>();
        var keyName = indexAttr?.PropertyNames?.FirstOrDefault(n => n != nameof(EntityId));
        if (keyName is not null)
            return GetType().GetProperty(keyName, BindingFlags.Instance | BindingFlags.Public);
        return null;
    }
}