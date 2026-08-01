using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;

namespace NeoEditor.Helper;

/// <summary>
/// Shared entity utilities. Single source of truth for entity key resolution.
/// Used by both merge-view index building and forward-index panel.
/// </summary>
public static class EntityHelper
{
    private static readonly Dictionary<Type, PropertyInfo?> KeyPropCache = new();

    /// <summary>
    /// Resolve the primary key property for an entity type.
    /// Priority: [UIDKey] attribute → Id/nID properties → first int [Column] property.
    /// Results are cached per type.
    /// </summary>
    public static PropertyInfo? ResolveKeyProperty(Type entityType)
    {
        if (KeyPropCache.TryGetValue(entityType, out var cached)) return cached;

        // 1. Try [UIDKey] attribute (Core-level business key, replaces EF Core [Index])
        var uidKeyAttr = entityType.GetCustomAttributes<Data.Model.Game.UIDKeyAttribute>().FirstOrDefault();
        if (uidKeyAttr?.PropertyNames is { Length: > 0 })
        {
            var keyName = uidKeyAttr.PropertyNames.FirstOrDefault(n => n != "EntityId");
            if (!string.IsNullOrWhiteSpace(keyName))
            {
                var prop = entityType.GetProperty(keyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                if (prop is not null) { KeyPropCache[entityType] = prop; return prop; }
            }
        }

        // 2. Try Id / nID (convention-based primary keys)
        var keyProp = entityType.GetProperty("Id",
                          BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
                      ?? entityType.GetProperty("nID",
                          BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (keyProp is not null) { KeyPropCache[entityType] = keyProp; return keyProp; }

        // 3. Fallback: first int-typed property with [Column] attribute
        keyProp = entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.DeclaringType != typeof(Data.Model.Game.IEntity))
            .Where(p => p.GetCustomAttribute<ColumnAttribute>() != null)
            .Where(p => p.PropertyType == typeof(int))
            .OrderBy(p => p.MetadataToken)
            .FirstOrDefault();

        KeyPropCache[entityType] = keyProp;
        return keyProp;
    }

    /// <summary>Get the primary key value from an entity. Returns null if no key property found.</summary>
    public static int? GetKeyValue(Data.Model.Game.IEntity entity)
    {
        var keyProp = ResolveKeyProperty(entity.GetType());
        return keyProp?.GetValue(entity) is int k ? k : null;
    }

    /// <summary>
    /// Compute the primary key string for an entity. Used by ReferenceIndex.
    /// Returns null if no key property found.
    /// </summary>
    public static string? ComputeEntityKeyString(Data.Model.Game.IEntity entity)
    {
        var keyProp = ResolveKeyProperty(entity.GetType());
        if (keyProp is null) return null;
        var val = keyProp.GetValue(entity);
        return val switch
        {
            int i => i.ToString(),
            long l => l.ToString(),
            string s => s,
            _ => null
        };
    }
}
