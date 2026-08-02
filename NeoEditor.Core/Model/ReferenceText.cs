using NeoEditor.Core.Abstractions;
using NeoEditor.Data.Model;

namespace NeoEditor.Helper;

/// <summary>
/// Canonical raw-text extraction for reference property values.
/// ReferenceList must go through <see cref="ReferenceList{T}.ToRawString(string?)"/> —
/// never .ToString(), which emits the damaged "[a, b]" format.
/// (Same pattern round28 applied inline in XmlParser/ReferenceIndex/ReferenceResolver.)
/// </summary>
public static class ReferenceText
{
    /// <summary>
    /// Get the raw serialized text of a property value. ReferenceList → ToRawString(separator),
    /// anything else → ToString() (or "" when null).
    /// </summary>
    public static string GetRawString(object? value, ReferenceFieldAttribute? attr)
        => value is ReferenceList<IReferenceEntry> rl
            ? rl.ToRawString(attr?.Separator)
            : value?.ToString() ?? "";
}