using System;

namespace NeoEditor.Data.Model.Game;

/// <summary>
/// Marks the unique business key properties for an entity.
/// Replaces EF Core's [Index] for Core-level key resolution
/// without requiring an EF Core dependency.
/// Used by EntityHelper.ResolveKeyProperty() to find the primary key column.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class UIDKeyAttribute : Attribute
{
    /// <summary>Property names that form the unique key, in order.</summary>
    public string[] PropertyNames { get; }

    public UIDKeyAttribute(params string[] propertyNames)
    {
        PropertyNames = propertyNames;
    }
}
