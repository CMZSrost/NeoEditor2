using System;
using System.Collections.Generic;
using System.Reflection;

namespace NeoEditor.Helper;

/// <summary>
/// Shared entity utilities. Single source of truth for entity key resolution.
/// Replaces the previously scattered GetProperty("Id") ?? GetProperty("nID") pattern.
/// </summary>
public static class EntityHelper
{
    private static readonly Dictionary<Type, PropertyInfo?> KeyPropCache = new();

    /// <summary>
    /// Resolve the primary key property for an entity type. Tries Id first, then nID.
    /// Results are cached per type.
    /// </summary>
    public static PropertyInfo? ResolveKeyProperty(Type entityType)
    {
        if (KeyPropCache.TryGetValue(entityType, out var cached)) return cached;
        var keyProp = entityType.GetProperty("Id",
                          BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase)
                      ?? entityType.GetProperty("nID",
                          BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
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
