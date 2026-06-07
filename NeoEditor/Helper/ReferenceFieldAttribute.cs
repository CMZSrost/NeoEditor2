using System;

namespace NeoEditor.Helper;

/// <summary>
/// Marks a property as a reference to another game entity type.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ReferenceFieldAttribute : Attribute
{
    public Type TargetEntityType { get; }

    /// <summary>Segment separator. null = single value, "," = comma-separated, "|" = pipe-separated.</summary>
    public string? Separator { get; init; }

    /// <summary>Parse pattern for each segment. "{id}" (default), "{id}x{mult}", "{id}={value}".</summary>
    public string? Pattern { get; init; }

    /// <summary>
    /// How to interpret the extracted id to find the target entity.
    /// "{Id}" (default), "{GroupId}.{SubgroupId}".
    /// Property names in braces map to properties on the target entity type.
    /// </summary>
    public string? TargetKey { get; init; }

    /// <summary>Fallback target entity type when primary lookup fails (e.g. mixed ref types in one field).</summary>
    public Type? SecondaryTargetEntityType { get; init; }

    /// <summary>TargetKey for the secondary target entity type.</summary>
    public string? SecondaryTargetKey { get; init; }

    /// <summary>Convenience: true when Separator is not null.</summary>
    public bool IsMultiValue => Separator is not null;

    public ReferenceFieldAttribute(Type targetEntityType)
    {
        TargetEntityType = targetEntityType;
    }
}
