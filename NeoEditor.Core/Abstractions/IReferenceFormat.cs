namespace NeoEditor.Core.Abstractions;

/// <summary>
/// A structured reference format — a self-contained unit that describes
/// how a reference segment is composed. Each concrete format class corresponds
/// to one [ReferenceField] pattern and holds its typed parameters directly.
///
/// Unlike the old DecoratedRef + IDecoration model where decorations were
/// split into atomic prefix/suffix fragments, a format class is the WHOLE
/// segment: it owns the entity ref(s) and all parameters, and its
/// <see cref="FormatTemplate"/> string documents the parse/serialize contract.
///
/// Examples:
///   IdXMultFormat  → template "{entityRef}x{mul}"   → "211x1.5"
///   AssignFormat   → template "{entityRef}={value}"  → "38=1"
///   MultiIngredientRecipeFormat → template "{fmt}+{fmt}={target}X{p1}X{p2}..."
/// </summary>
public interface IReferenceFormat : IReferenceEntry
{
    /// <summary>
    /// Human-readable template describing this format's structure.
    /// For documentation and UI display purposes.
    /// </summary>
    string FormatTemplate { get; }
}
